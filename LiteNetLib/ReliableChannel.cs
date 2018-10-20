using System;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class ReliableChannel : BaseChannel
    {
        private sealed class PendingPacket
        {
            public NetPacket Packet;
            public long TimeStamp;
            public bool Sended;

            public override string ToString()
            {
                return Packet == null ? "Empty" : Packet.Sequence.ToString();
            }
        }

        private readonly NetPacket _outgoingAcks;            //for send acks
        private readonly PendingPacket[] _pendingPackets;    //for unacked packets and duplicates
        private readonly NetPacket[] _receivedPackets;       //for order
        private readonly bool[] _earlyReceived;              //for unordered

        private int _localSeqence;
        private int _remoteSequence;
        private int _localWindowStart;
        private int _remoteWindowStart;

        private bool _mustSendAcks;

        private readonly bool _ordered;
        private readonly int _windowSize;
        private const int BitsInByte = 8;

        public ReliableChannel(NetPeer peer, bool ordered) : base(peer)
        {
            _windowSize = NetConstants.DefaultWindowSize;
            _ordered = ordered;
            _pendingPackets = new PendingPacket[_windowSize];
            for (int i = 0; i < _pendingPackets.Length; i++)
                _pendingPackets[i] = new PendingPacket();

            if (_ordered)
                _receivedPackets = new NetPacket[_windowSize];
            else
                _earlyReceived = new bool[_windowSize];

            _localWindowStart = 0;
            _localSeqence = 0;
            _remoteSequence = 0;
            _remoteWindowStart = 0;

            //Init acks packet
            int bytesCount = (_windowSize - 1) / BitsInByte + 1;
            PacketProperty property = _ordered ? PacketProperty.AckReliableOrdered : PacketProperty.AckReliable;
            _outgoingAcks = new NetPacket(property, bytesCount);
        }

        //ProcessAck in packet
        public void ProcessAck(NetPacket packet)
        {
            if (packet.Size != _outgoingAcks.Size)
            {
                NetUtils.DebugWrite("[PA]Invalid acks packet size");
                return;
            }

            ushort ackWindowStart = packet.Sequence;
            int windowRel = NetUtils.RelativeSequenceNumber(_localWindowStart, ackWindowStart);
            if (ackWindowStart >= NetConstants.MaxSequence || windowRel < 0)
            {
                NetUtils.DebugWrite("[PA]Bad window start");
                return;
            }

            //check relevance     
            if (windowRel >= _windowSize)
            {
                NetUtils.DebugWrite("[PA]Old acks");
                return;
            }

            byte[] acksData = packet.RawData;
            Monitor.Enter(_pendingPackets);
            for(int pendingSeq = _localWindowStart; pendingSeq != _localSeqence; pendingSeq = (pendingSeq + 1) % NetConstants.MaxSequence)
            {
                int rel = NetUtils.RelativeSequenceNumber(pendingSeq, ackWindowStart);
                if (rel >= _windowSize)
                {
                    NetUtils.DebugWrite("[PA]REL: " + rel);
                    break;
                }

                int pendingIdx = pendingSeq % _windowSize;
                int currentByte = NetConstants.SequencedHeaderSize + pendingIdx / BitsInByte;
                int currentBit = pendingIdx % BitsInByte;
                if ((acksData[currentByte] & (1 << currentBit)) == 0)
                {
#if STATS_ENABLED || DEBUG
                    Peer.Statistics.PacketLoss++;
#endif
                    //Skip false ack
                    NetUtils.DebugWrite("[PA]False ack: {0}", pendingSeq);
                    continue;
                }
                if (pendingSeq == _localWindowStart)
                {
                    //Move window                
                    _localWindowStart = (_localWindowStart + 1) % NetConstants.MaxSequence;
                }

                //clear packet
                var pendingPacket = _pendingPackets[pendingIdx];
                if (pendingPacket.Packet != null)
                {
                    Peer.Recycle(pendingPacket.Packet);
                    pendingPacket.Packet = null;
                    NetUtils.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", pendingSeq);
                }
            }
            Monitor.Exit(_pendingPackets);
        }

        public override void SendNextPackets()
        {
            //check sending acks
            if (_mustSendAcks)
            {
                _mustSendAcks = false;
                NetUtils.DebugWrite("[RR]SendAcks");
                Monitor.Enter(_outgoingAcks);
                Peer.SendUserData(_outgoingAcks);
                Monitor.Exit(_outgoingAcks);
            }

            long currentTime = DateTime.UtcNow.Ticks;
            Monitor.Enter(_pendingPackets);
            //get packets from queue
            Monitor.Enter(OutgoingQueue);
            while (OutgoingQueue.Count > 0)
            {
                int relate = NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart);
                if (relate < _windowSize)
                {
                    PendingPacket pendingPacket = _pendingPackets[_localSeqence % _windowSize];
                    pendingPacket.Sended = false;
                    pendingPacket.Packet = OutgoingQueue.Dequeue();
                    pendingPacket.Packet.Sequence = (ushort)_localSeqence;
                    _localSeqence = (_localSeqence + 1) % NetConstants.MaxSequence;
                }
                else //Queue filled
                {
                    break;
                }
            }
            Monitor.Exit(OutgoingQueue);
            //send
            double resendDelay = Peer.ResendDelay;
            for (int pendingSeq = _localWindowStart; pendingSeq != _localSeqence; pendingSeq = (pendingSeq + 1) % NetConstants.MaxSequence)
            {
                PendingPacket currentPacket = _pendingPackets[pendingSeq % _windowSize];
                if (currentPacket.Packet == null)
                    continue;

                if (currentPacket.Sended) //check send time
                {
                    double packetHoldTime = currentTime - currentPacket.TimeStamp;
                    if (packetHoldTime < resendDelay * TimeSpan.TicksPerMillisecond)
                        continue;
                    NetUtils.DebugWrite("[RC]Resend: {0} > {1}", (int) packetHoldTime, resendDelay);
                }

                currentPacket.TimeStamp = currentTime;
                currentPacket.Sended = true;
                Peer.SendUserData(currentPacket.Packet);
            }
            Monitor.Exit(_pendingPackets);
        }

        //Process incoming packet
        public override void ProcessPacket(NetPacket packet)
        {
            int seq = packet.Sequence;
            if (seq >= NetConstants.MaxSequence)
            {
                NetUtils.DebugWrite("[RR]Bad sequence");
                return;
            }

            int relate = NetUtils.RelativeSequenceNumber(seq, _remoteWindowStart);
            int relateSeq = NetUtils.RelativeSequenceNumber(seq, _remoteSequence);

            if (relateSeq > _windowSize)
            {
                NetUtils.DebugWrite("[RR]Bad sequence");
                return;
            }

            //Drop bad packets
            if (relate < 0)
            {
                //Too old packet doesn't ack
                NetUtils.DebugWrite("[RR]ReliableInOrder too old");
                return;
            }
            if (relate >= _windowSize * 2)
            {
                //Some very new packet
                NetUtils.DebugWrite("[RR]ReliableInOrder too new");
                return;
            }

            //If very new - move window
            int ackIdx;
            int ackByte;
            int ackBit;
            Monitor.Enter(_outgoingAcks);
            if (relate >= _windowSize)
            {
                //New window position
                int newWindowStart = (_remoteWindowStart + relate - _windowSize + 1) % NetConstants.MaxSequence;
                _outgoingAcks.Sequence = (ushort)newWindowStart;

                //Clean old data
                while (_remoteWindowStart != newWindowStart)
                {
                    ackIdx = _remoteWindowStart % _windowSize;
                    ackByte = NetConstants.SequencedHeaderSize + ackIdx / BitsInByte;
                    ackBit = ackIdx % BitsInByte;
                    _outgoingAcks.RawData[ackByte] &= (byte)~(1 << ackBit);
                    _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                }
            }

            //Final stage - process valid packet
            //trigger acks send
            _mustSendAcks = true;
            ackIdx = seq % _windowSize;
            ackByte = NetConstants.SequencedHeaderSize + ackIdx / BitsInByte;
            ackBit = ackIdx % BitsInByte;
            if ((_outgoingAcks.RawData[ackByte] & (1 << ackBit)) != 0)
            {
                NetUtils.DebugWrite("[RR]ReliableInOrder duplicate");
                Monitor.Exit(_outgoingAcks);
                return;
            }

            //save ack
            _outgoingAcks.RawData[ackByte] |= (byte)(1 << ackBit);
            Monitor.Exit(_outgoingAcks);

            //detailed check
            if (seq == _remoteSequence)
            {
                NetUtils.DebugWrite("[RR]ReliableInOrder packet succes");
                Peer.AddIncomingPacket(packet);
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;

                if (_ordered)
                {
                    NetPacket p;
                    while ((p = _receivedPackets[_remoteSequence % _windowSize]) != null)
                    {
                        //process holded packet
                        _receivedPackets[_remoteSequence % _windowSize] = null;
                        Peer.AddIncomingPacket(p);
                        _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;
                    }
                }
                else
                {
                    while (_earlyReceived[_remoteSequence % _windowSize])
                    {
                        //process early packet
                        _earlyReceived[_remoteSequence % _windowSize] = false;
                        _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;
                    }
                }

                return;
            }

            //holded packet
            if (_ordered)
            {
                _receivedPackets[ackIdx] = packet;
            }
            else
            {
                _earlyReceived[ackIdx] = true;
                Peer.AddIncomingPacket(packet);
            }
        }
    }
}
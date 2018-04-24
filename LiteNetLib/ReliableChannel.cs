using System;
using System.Collections.Generic;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class ReliableChannel
    {
        private sealed class PendingPacket
        {
            public NetPacket Packet;
            public long TimeStamp;
            public bool Sended;
            public void Clear()
            {
                Packet = null;
                Sended = false;
            }
        }

        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly NetPacket _outgoingAcks;            //for send acks
        private readonly PendingPacket[] _pendingPackets;    //for unacked packets and duplicates
        private readonly NetPacket[] _receivedPackets;       //for order
        private readonly bool[] _earlyReceived;              //for unordered
        private int _pendingPacketsCount;

        private int _localSeqence;
        private int _remoteSequence;
        private int _localWindowStart;
        private int _remoteWindowStart;

        private readonly NetPeer _peer;
        private bool _mustSendAcks;

        private readonly bool _ordered;
        private readonly int _windowSize;
        private const int BitsInByte = 8;
        private int _ackedPackets = 1;

        public int PacketsInQueue
        {
            get { return _outgoingPackets.Count; }
        }

        public ReliableChannel(NetPeer peer, bool ordered)
        {
            _windowSize = NetConstants.DefaultWindowSize;
            _peer = peer;
            _ordered = ordered;

            _outgoingPackets = new Queue<NetPacket>(_windowSize);
            _pendingPackets = new PendingPacket[_windowSize];
            for (int i = 0; i < _pendingPackets.Length; i++)
            {
                _pendingPackets[i] = new PendingPacket();
            }

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
            _outgoingAcks = _peer.GetPacketFromPool(property, bytesCount);
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
            if (ackWindowStart > NetConstants.MaxSequence)
            {
                NetUtils.DebugWrite("[PA]Bad window start");
                return;
            }

            //check relevance
            if (NetUtils.RelativeSequenceNumber(ackWindowStart, _localWindowStart) <= -_windowSize)
            {
                NetUtils.DebugWrite("[PA]Old acks");
                return;
            }

            byte[] acksData = packet.RawData;
            Monitor.Enter(_pendingPackets);
            for(int i = 0; i < _pendingPacketsCount; i++)
            {
                var pendingPacket = _pendingPackets[i];
                int seq = pendingPacket.Packet.Sequence;
                int rel = NetUtils.RelativeSequenceNumber(seq, ackWindowStart);
                if (rel < 0 || rel >= _windowSize)
                {
                    continue;
                }

                int idx = seq % _windowSize;
                int currentByte = NetConstants.SequencedHeaderSize + idx / BitsInByte;
                int currentBit = idx % BitsInByte;
                if ((acksData[currentByte] & (1 << currentBit)) == 0)
                {
#if STATS_ENABLED || DEBUG
                    _peer.Statistics.PacketLoss++;
#endif
                    //Skip false ack
                    continue;
                }

                if (seq == _localWindowStart)
                {
                    //Move window                
                    _localWindowStart = (_localWindowStart + _ackedPackets) % NetConstants.MaxSequence;
                    _ackedPackets = 1;
                }
                else
                {
                    _ackedPackets++;
                }

                //clear acked packet
                _pendingPacketsCount--;
                var tempPacket = _pendingPackets[i];
                _pendingPackets[i] = _pendingPackets[_pendingPacketsCount];
                _pendingPackets[_pendingPacketsCount] = tempPacket;
                i--;
                _peer.Recycle(pendingPacket.Packet);
                pendingPacket.Clear();

                NetUtils.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", seq);
            }
            Monitor.Exit(_pendingPackets);
        }

        public void AddToQueue(NetPacket packet)
        {
            Monitor.Enter(_outgoingPackets);
            _outgoingPackets.Enqueue(packet);
            Monitor.Exit(_outgoingPackets);
        }

        public void SendNextPackets()
        {
            //check sending acks
            if (_mustSendAcks)
            {
                _mustSendAcks = false;
                NetUtils.DebugWrite("[RR]SendAcks");
                Monitor.Enter(_outgoingAcks);
                _peer.SendRawData(_outgoingAcks);
                Monitor.Exit(_outgoingAcks);
            }

            long currentTime = DateTime.UtcNow.Ticks;
            Monitor.Enter(_pendingPackets);
            //get packets from queue
            Monitor.Enter(_outgoingPackets);
            while (_outgoingPackets.Count > 0)
            {
                int relate = NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart);
                if (relate < _windowSize)
                {
                    PendingPacket pendingPacket = _pendingPackets[_pendingPacketsCount];
                    pendingPacket.Packet = _outgoingPackets.Dequeue();
                    pendingPacket.Packet.Sequence = (ushort)_localSeqence;
                    _pendingPacketsCount++;
                    _localSeqence = (_localSeqence + 1) % NetConstants.MaxSequence;
                }
                else //Queue filled
                {
                    break;
                }
            }
            Monitor.Exit(_outgoingPackets);

            //if no pending packets return
            if (_pendingPacketsCount == 0)
            {
                Monitor.Exit(_pendingPackets);
                return;
            }
            //send
            double resendDelay = _peer.ResendDelay;
            for (int i = 0; i < _pendingPacketsCount; i++)
            {
                PendingPacket currentPacket = _pendingPackets[i];
                if (currentPacket.Sended) //check send time
                {
                    double packetHoldTime = currentTime - currentPacket.TimeStamp;
                    if (packetHoldTime < resendDelay * TimeSpan.TicksPerMillisecond)
                    {
                        continue;
                    }
                    NetUtils.DebugWrite("[RC]Resend: {0} > {1}", (int)packetHoldTime, resendDelay);
                }
                currentPacket.TimeStamp = currentTime;
                currentPacket.Sended = true;
                _peer.SendRawData(currentPacket.Packet);
            }
            Monitor.Exit(_pendingPackets);
        }

        //Process incoming packet
        public void ProcessPacket(NetPacket packet)
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
                _peer.AddIncomingPacket(packet);
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;

                if (_ordered)
                {
                    NetPacket p;
                    while ((p = _receivedPackets[_remoteSequence % _windowSize]) != null)
                    {
                        //process holded packet
                        _receivedPackets[_remoteSequence % _windowSize] = null;
                        _peer.AddIncomingPacket(p);
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
                _peer.AddIncomingPacket(packet);
            }
        }
    }
}
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace LiteNetLib
{
    sealed class ReliableChannel
    {
        struct PendingPacket
        {
            public NetPacket Packet;
            public uint TimeStamp;
            public bool NotEmpty { get { return Packet != null; } }
            public void Clear()
            {
                Packet = null;
                TimeStamp = 0;
            }
        }

        //For reliable inOrder
        private ushort _localSeqence;
        private uint _remoteSequence;

        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly bool[] _outgoingAcks;               //for send acks
        private readonly PendingPacket[] _pendingPackets;        //for unacked packets and duplicates
        private readonly NetPacket[] _receivedPackets;       //for order
        private readonly bool[] _earlyReceived;              //for unordered
 
        private int _localWindowStart;
        private int _remoteWindowStart;
        private readonly NetPeer _peer;
        private int _queueIndex;
        private bool _mustSendAcks;

        private readonly Stopwatch _packetTimeStopwatch;

        private long _resendDelay = 500;
        private const int BitsInByte = 8;
        private readonly bool _ordered;
        private readonly int _windowSize;

        private readonly AutoResetEvent _pendingPacketsAccess = new AutoResetEvent(true);

        //Socket constructor
        public ReliableChannel(NetPeer peer, bool ordered, int windowSize)
        {
            _windowSize = windowSize;
            _peer = peer;
            _ordered = ordered;

            _outgoingPackets = new Queue<NetPacket>(_windowSize);

            _outgoingAcks = new bool[_windowSize];
            _pendingPackets = new PendingPacket[_windowSize];

            if (_ordered)
                _receivedPackets = new NetPacket[_windowSize];
            else
                _earlyReceived = new bool[_windowSize];

            _localWindowStart = 0;
            _localSeqence = 0;
            _remoteSequence = 0;
            _remoteWindowStart = 0;

            _packetTimeStopwatch = new Stopwatch();
            _packetTimeStopwatch.Start();
        }

        //ProcessAck in packet
        public void ProcessAck(NetPacket packet)
        {
            ushort ackWindowStart = packet.Sequence;

            //check relevance
            if (NetUtils.RelativeSequenceNumber(ackWindowStart, _localWindowStart) <= -_windowSize)
            {
                _peer.DebugWrite("[PA]Old acks");
                return;
            }

            byte[] acksData = packet.RawData;
            _peer.DebugWrite("[PA]AcksStart: {0}", ackWindowStart);
            int startByte = NetConstants.SequencedHeaderSize;

            _pendingPacketsAccess.WaitOne();
            for (int i = 0; i < _windowSize; i++)
            {
                int ackSequence = ackWindowStart + i;
                if (NetUtils.RelativeSequenceNumber(ackSequence, _localWindowStart) < 0)
                    continue;

                int currentByte = startByte + i / BitsInByte;
                int currentBit = i % BitsInByte;
                if ( (acksData[currentByte] & (1 << currentBit)) == 0 )
                    continue;

                if (ackSequence == _localWindowStart)
                {
                    _localWindowStart = (_localWindowStart + 1) % NetConstants.MaxSequence;
                }

                int storeIdx = ackSequence % _windowSize;

                if (_pendingPackets[storeIdx].NotEmpty)
                {
                    NetPacket removed = _pendingPackets[storeIdx].Packet;
                    _pendingPackets[storeIdx].Clear();
                    _peer.Recycle(removed);

                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", ackSequence);
                }
                else
                {
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - false", ackSequence);
                }
            }
            _pendingPacketsAccess.Set();
        }

        public void AddToQueue(NetPacket packet)
        {
            lock (_outgoingPackets)
            {
                _outgoingPackets.Enqueue(packet);
            }
        }

        private void ProcessQueuedPackets()
        {
            //get packets from queue
            while (_outgoingPackets.Count > 0)
            {
                int relate = NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart);
                if (relate < _windowSize)
                {
                    NetPacket packet;
                    lock (_outgoingPackets)
                    {
                        packet = _outgoingPackets.Dequeue();
                    }
                    packet.Sequence = _localSeqence;

                    _pendingPackets[_localSeqence % _windowSize].Packet = packet;
                    _localSeqence++;
                }
                else
                {
                    break;
                }
            }
        }

        public bool SendNextPacket()
        {
            //check sending acks
            long currentTime = _packetTimeStopwatch.ElapsedMilliseconds;

            _pendingPacketsAccess.WaitOne();
            ProcessQueuedPackets();

            //send
            PendingPacket currentPacket;
            bool packetSearch = true;
            int startQueueIndex = _queueIndex;
            do
            {
                currentPacket = _pendingPackets[_queueIndex];
                //check send time
                if (currentPacket.NotEmpty)
                {
                    long packetHoldTime = currentTime - currentPacket.TimeStamp;
                    if (currentPacket.TimeStamp == 0 || packetHoldTime > _resendDelay)
                    {
                        //Setup timestamp or resend
                        currentPacket.TimeStamp = (uint)currentTime;
                        _pendingPackets[_queueIndex] = currentPacket;
                        packetSearch = false;
                    }
                }

                _queueIndex = (_queueIndex + 1) % _windowSize;
            } while (packetSearch && _queueIndex != startQueueIndex);

            bool sendResult = false;
            if (currentPacket.NotEmpty)
            {
                sendResult = _peer.SendRawData(currentPacket.Packet.RawData);
            }
            _pendingPacketsAccess.Set();
            return sendResult;
        }

        public void Update(int deltaTime)
        {
            _resendDelay = deltaTime*4;
        }

        public void SendAcks()
        {
            if (!_mustSendAcks)
                return;
            _mustSendAcks = false;
            
            _peer.DebugWrite("[RR]SendAcks");

            //Init packet
            int bytesCount = (_windowSize - 1) / BitsInByte + 1;
            PacketProperty property = _ordered ? PacketProperty.AckReliableOrdered : PacketProperty.AckReliable;
            var acksPacket = _peer.GetPacketFromPool(property, bytesCount);

            //For quick access
            byte[] data = acksPacket.RawData; //window start + acks size

            //Put window start
            acksPacket.Sequence = (ushort)_remoteWindowStart;

            //Put acks
            int startAckIndex = _remoteWindowStart % _windowSize;
            int currentAckIndex = startAckIndex;
            int currentBit = 0;
            int currentByte = NetConstants.SequencedHeaderSize;
            do 
            {
                if (_outgoingAcks[currentAckIndex])
                {
                    data[currentByte] |= (byte)(1 << currentBit);
                }

                currentBit++;
                if (currentBit == BitsInByte)
                {
                    currentByte++;
                    currentBit = 0;
                }
                currentAckIndex = (currentAckIndex + 1) % _windowSize;
            } while (currentAckIndex != startAckIndex);

            _peer.SendRawData(acksPacket.RawData);
            _peer.Recycle(acksPacket);
        }

        //Process incoming packet
        public bool ProcessPacket(NetPacket packet)
        {
            int relate = NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteWindowStart);

            //Drop bad packets
            if(relate < 0)
            {
                //Too old packet doesn't ack
                _peer.DebugWrite("[RR]ReliableInOrder too old");
                return false;
            }
            if (relate >= _windowSize * 2)
            {
                //Some very new packet
                _peer.DebugWrite("[RR]ReliableInOrder too new");
                return false;
            }

            //If very new - move window
            if (relate >= _windowSize)
            {
                //New window position
                int newWindowStart = (_remoteWindowStart + relate - _windowSize + 1) % NetConstants.MaxSequence;

                //Clean old data
                while (_remoteWindowStart != newWindowStart)
                {
                    _outgoingAcks[_remoteWindowStart % _windowSize] = false;
                    _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                }
            }

            //Final stage - process valid packet
            //trigger acks send
            _mustSendAcks = true;

            if (_outgoingAcks[packet.Sequence % _windowSize])
            {
                _peer.DebugWrite("[RR]ReliableInOrder duplicate");
                return false;
            }

            //save ack
            _outgoingAcks[packet.Sequence % _windowSize] = true;

            //detailed check
            if (packet.Sequence == _remoteSequence)
            {
                _peer.DebugWrite("[RR]ReliableInOrder packet succes");
                _peer.AddIncomingPacket(packet);
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;

                if (_ordered)
                {
                    NetPacket p;
                    while ( (p = _receivedPackets[_remoteSequence % _windowSize]) != null)
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

                return true;
            }

            //holded packet
            if (_ordered)
            {
                _receivedPackets[packet.Sequence % _windowSize] = packet;
            }
            else
            {
                _earlyReceived[packet.Sequence % _windowSize] = true;
                _peer.AddIncomingPacket(packet);
            }
            return true;
        }
    }
}

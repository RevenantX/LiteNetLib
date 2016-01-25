using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiteNetLib
{
    sealed class ReliableChannel
    {
        //For reliable inOrder
        private ushort _localSeqence;
        private uint _remoteSequence;

        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly bool[] _outgoingAcks;               //for send acks
        private readonly NetPacket[] _pendingPackets;        //for unacked packets and duplicates
        private readonly NetPacket[] _receivedPackets;       //for order
        private readonly bool[] _earlyReceived;              //for unordered
 
        private int _localWindowStart;
        private int _remoteWindowStart;
        private readonly NetPeer _peer;
        private int _queueIndex;
        private bool _mustSendAcks;

        private readonly Stopwatch _packetTimeStopwatch;

        private const long ResendDelay = 300;
        private readonly bool _ordered;
        private readonly int _windowSize;

        //Socket constructor
        public ReliableChannel(NetPeer peer, bool ordered, int windowSize)
        {
            _windowSize = windowSize;
            _peer = peer;
            _ordered = ordered;

            _outgoingPackets = new Queue<NetPacket>(_windowSize);

            _outgoingAcks = new bool[_windowSize];
            _pendingPackets = new NetPacket[_windowSize];

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
            byte[] acksData = packet.RawData;

            if (NetUtils.RelativeSequenceNumber(ackWindowStart, _localWindowStart) <= -_windowSize)
            {
                _peer.DebugWrite("[PA]Old acks");
                return;
            }

            _peer.DebugWrite("[PA]AcksStart: {0}", ackWindowStart);

            for (int i = 0; i < _windowSize; i++)
            {
                int ackSequence = ackWindowStart + i;

                if (ackSequence < _localWindowStart)
                    continue;
                if ( (acksData[NetConstants.SequencedHeaderSize + i/8] & (1 << i%8)) == 0 )
                    continue;


                int storeIdx = ackSequence % _windowSize;
                if (ackSequence == _localWindowStart)
                {
                    _localWindowStart = (_localWindowStart + 1) % NetConstants.MaxSequence;
                }

                if (_pendingPackets[storeIdx] != null)
                {
                    NetPacket removed = _pendingPackets[storeIdx];
                    _pendingPackets[storeIdx] = null;
                    _peer.UpdateRoundTripTime((int)(_packetTimeStopwatch.ElapsedMilliseconds - removed.TimeStamp));
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", ackSequence);
                    _peer.Recycle(removed);
                }
                else
                {
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - false", ackSequence);
                }
            }
        }

        public void AddToQueue(NetPacket packet)
        {
            lock (_outgoingPackets)
            {
                _outgoingPackets.Enqueue(packet);
            }
        }

        public NetPacket GetQueuedPacket()
        {
            long currentTime = _packetTimeStopwatch.ElapsedMilliseconds;
            if (_mustSendAcks)
            {
                _mustSendAcks = false;
                return SendAcks();
            }

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
                    packet.TimeStamp = 0;
                    _pendingPackets[_localSeqence % _windowSize] = packet;
                    _localSeqence++;
                }
                else
                {
                    break;
                }
            }

            int startQueueIndex = _queueIndex;
            NetPacket currentPacket;

            do
            {
                currentPacket = _pendingPackets[_queueIndex];

                if (currentPacket != null)
                {
                    long packetHoldTime = currentTime - currentPacket.TimeStamp;
                    if (currentPacket.TimeStamp == 0 || packetHoldTime > ResendDelay)
                    {
                        //Setup timestamp or resend
                        currentPacket.TimeStamp = currentTime;
                    }
                    else
                    {
                        currentPacket = null;
                    }
                }

                _queueIndex = (_queueIndex + 1) % _windowSize;
            } while (currentPacket == null && _queueIndex != startQueueIndex);

            //return
            return currentPacket;
        }

        private NetPacket SendAcks()
        {
            //_peer.DebugWriteForce("[RR]SendAcks");

            //Init packet
            NetPacket p = _peer.GetPacketFromPool(
                _ordered ? PacketProperty.AckReliableOrdered : PacketProperty.AckReliable,
                _windowSize / 8);
            
            //For quick access
            byte[] data = p.RawData; //window start + acks size

            //Put window start
            p.Sequence = (ushort)_remoteWindowStart;

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
                if (currentBit == 8)
                {
                    currentByte++;
                    currentBit = 0;
                }
                currentAckIndex = (currentAckIndex + 1) % _windowSize;
            } while (currentAckIndex != startAckIndex);

            return p;
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

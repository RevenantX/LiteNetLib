using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiteNetLib
{
    public class ReliableOrderedChannel : INetChannel
    {
        //For reliable inOrder
        private ushort _localSeqence;
        private uint _remoteSequence;

        private Queue<NetPacket> _outgoingPackets;
        private bool[] _outgoingAcks;               //for send acks
        private NetPacket[] _pendingPackets;        //for unacked packets
        private NetPacket[] _receivedPackets;       //for drop duplicates
 
        private int _localWindowStart;
        private int _remoteWindowStart;
        private NetPeer _peer;
        private int _queueIndex;
        private bool _mustSendAcks;

        private Stopwatch _packetTimeStopwatch;

        //Socket constructor
        public ReliableOrderedChannel(NetPeer peer)
        {
            _peer = peer;

            _outgoingPackets = new Queue<NetPacket>();

            _outgoingAcks = new bool[NetConstants.WindowSize];
            _pendingPackets = new NetPacket[NetConstants.WindowSize];
            _receivedPackets = new NetPacket[NetConstants.WindowSize];

            _localWindowStart = 0;
            _localSeqence = 0;
            _remoteSequence = 0;
            _remoteWindowStart = 0;

            _packetTimeStopwatch = new Stopwatch();
            _packetTimeStopwatch.Start();
        }

        //ProcessAck in packet
        public void ProcessAck(byte[] acksData)
        {
            ushort ackWindowStart = BitConverter.ToUInt16(acksData, 0);
            
            if (NetUtils.RelativeSequenceNumber(ackWindowStart, _localWindowStart) <= -NetConstants.WindowSize)
            {
                _peer.DebugWrite("[PA]Old acks");
                return;
            }
            ulong acks = BitConverter.ToUInt64(acksData, 2);
            _peer.DebugWrite("[PA]AcksStart: {0}", ackWindowStart);

            //int acr = 0;

            for (int i = 0; i < 64; i++)
            {
                int ackSequence = ackWindowStart + i;

                if (ackSequence < _localWindowStart)
                    continue;
                if ( (acks & (1ul << i)) == 0 )
                    continue;

                //acr++;
                
                int storeIdx = ackSequence % NetConstants.WindowSize;
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
            //Console.WriteLine("ACR: " + acr + ", wis: " + ackWindowStart + ", lws: " + _localWindowStart );
        }

        public void AddToQueue(NetPacket packet)
        {
            _outgoingPackets.Enqueue(packet);
        }

        public NetPacket GetQueuedPacket()
        {
            if (_mustSendAcks)
            {
                _mustSendAcks = false;
                return SendAcks();
            }

            while (_outgoingPackets.Count > 0)
            {
                int relate = NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart);
                if (relate < NetConstants.WindowSize)
                {
                    NetPacket packet = _outgoingPackets.Dequeue();
                    packet.Sequence = _localSeqence;
                    packet.TimeStamp = _packetTimeStopwatch.ElapsedMilliseconds;
                    _pendingPackets[_localSeqence % NetConstants.WindowSize] = packet;
                    _localSeqence++;
                }
                else
                {
                    break;
                }
            }

            int startQueueIndex = _queueIndex;
            NetPacket currentPacket = _pendingPackets[_queueIndex];
            _queueIndex = (_queueIndex + 1) % NetConstants.WindowSize;

            while (currentPacket == null && _queueIndex != startQueueIndex)
            {
                currentPacket = _pendingPackets[_queueIndex];
                _queueIndex = (_queueIndex + 1) % NetConstants.WindowSize;
            }

            //return
            return currentPacket;
        }

        private NetPacket SendAcks()
        {
            //Init packet
            NetPacket p = _peer.CreatePacket();
            p.Property = PacketProperty.AckReliableOrdered;
            p.Data = new byte[10];

            //Put window start
            FastBitConverter.GetBytes(p.Data, 0, _remoteWindowStart);

            //Put acks
            ulong acks = 0;
            int start = _remoteWindowStart % NetConstants.WindowSize;
            int idx = start;
            int bit = 0;
            //int acs = 0;
            do
            {
                if (_outgoingAcks[idx])
                {
                    acks = acks | (1ul << bit);
                    //acs++;
                }

                bit++;
                idx = (idx + 1) % NetConstants.WindowSize;
            } while (idx != start);

            //Console.WriteLine("ACS: " + acs + ", wis: " + _remoteWindowStart);

            //save to data
            FastBitConverter.GetBytes(p.Data, 2, acks);

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
            if (relate >= NetConstants.WindowSize*2)
            {
                //Some very new packet
                _peer.DebugWrite("[RR]ReliableInOrder too new");
                return false;
            }

            //If very new - move window
            if (relate >= NetConstants.WindowSize)
            {
                //New window position
                int newWindowStart = (_remoteWindowStart + relate - NetConstants.WindowSize + 1) % NetConstants.MaxSequence;

                //Clean old data
                while (_remoteWindowStart != newWindowStart)
                {
                    _outgoingAcks[_remoteWindowStart % NetConstants.WindowSize] = false;
                    _receivedPackets[_remoteWindowStart % NetConstants.WindowSize] = null;
                    _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                }
            }

            //Final stage - process valid packet
            if (_outgoingAcks[packet.Sequence % NetConstants.WindowSize])
            {
                _peer.DebugWrite("[RR]ReliableInOrder duplicate");
                return false;
            }

            //trigger acks send
            _mustSendAcks = true;

            //save ack
            _outgoingAcks[packet.Sequence % NetConstants.WindowSize] = true;

            //detailed check
            if (packet.Sequence == _remoteSequence)
            {
                _peer.DebugWrite("[RR]ReliableInOrder packet succes");
                _peer.AddIncomingPacket(packet);
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;

                while (true)
                {
                    NetPacket p = _receivedPackets[_remoteSequence % NetConstants.WindowSize];
                    if (p == null)
                        break;

                    //process holded packet
                    _receivedPackets[_remoteSequence % NetConstants.WindowSize] = null;
                    _peer.AddIncomingPacket(p);
                    _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;
                }

                return true;
            }

            //holded packet
            _receivedPackets[packet.Sequence % NetConstants.WindowSize] = packet;
            return true;
        }
    }
}

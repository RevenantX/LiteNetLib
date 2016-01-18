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
        private bool[] _pendingAcks;                //for send acks
        private NetPacket[] _pendingPackets;     //for unacked packets
        private NetPacket[] _receivedPackets;            //for drop duplicates
 
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

            _pendingAcks = new bool[NetConstants.WindowSize];
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
            if (NetConstants.RelativeSequenceNumber(ackWindowStart, _localWindowStart) < 0)
            {
                _peer.DebugWrite("[PA]Old acks");
                return;
            }

            ulong acks = BitConverter.ToUInt64(acksData, 2);

            _peer.DebugWrite("[PA]AcksStart: {0}", ackWindowStart);

            int acr = 0;

            for (int i = 0; i < 64; i++)
            {
                if ( ((acks >> i) & 1) == 0 )
                {
                    continue;
                }

                int ack = ackWindowStart + i;
                int storeIdx = ack % NetConstants.WindowSize;

                if (ack == _localWindowStart)
                {
                    _localWindowStart = (_localWindowStart + 1) % NetConstants.MaxSequence;
                    _queueIndex = _localWindowStart % NetConstants.WindowSize;
                }

                if (_pendingPackets[storeIdx] != null)
                {
                    NetPacket removed = _pendingPackets[storeIdx];
                    _pendingPackets[storeIdx] = null;
                    _peer.UpdateFlowMode((int)(_packetTimeStopwatch.ElapsedMilliseconds - removed.TimeStamp));
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", ack);
                    _peer.Recycle(removed);
                }
                else
                {
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - false", ack);
                }
            }
            Console.WriteLine("ACR: " + acr);
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

            NetPacket currentPacket = _pendingPackets[_queueIndex];
            if (currentPacket == null && _outgoingPackets.Count > 0)
            {
                int relate = NetConstants.RelativeSequenceNumber(_localSeqence, _localWindowStart);
                if (relate < NetConstants.WindowSize)
                {
                    currentPacket = _outgoingPackets.Dequeue();
                    currentPacket.Sequence = _localSeqence;
                    currentPacket.TimeStamp = _packetTimeStopwatch.ElapsedMilliseconds;
                    _pendingPackets[_localSeqence % NetConstants.WindowSize] = currentPacket;
                    _localSeqence++;
                }
            }

            //increase idx
            _queueIndex = (_queueIndex + 1) % NetConstants.WindowSize;

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
            int acs = 0;
            do
            {
                if (_pendingAcks[idx])
                {
                    acks |= 1ul << bit;
                    acs++;
                }

                bit++;
                idx = (idx + 1) % NetConstants.WindowSize;
            } while (idx != start);

            Console.WriteLine("ACS: " + acs);

            //save to data
            FastBitConverter.GetBytes(p.Data, 2, acks);

            return p;
        }

        //Process incoming packet
        public bool ProcessPacket(NetPacket packet)
        {
            int relate = NetConstants.RelativeSequenceNumber(packet.Sequence, _remoteWindowStart);

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

            //flag send acks
            _mustSendAcks = true;
            //New packet
            if (relate == NetConstants.WindowSize)
            {
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;
                _peer.AddIncomingPacket(packet);
                _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                _pendingAcks[packet.Sequence % NetConstants.WindowSize] = true;
                return true;
            }

            //Very new packet - hold
            if (relate > NetConstants.WindowSize)
            {
                int newWindowStart = (packet.Sequence - NetConstants.WindowSize) % NetConstants.MaxSequence;
                while (_remoteWindowStart != newWindowStart)
                {
                    _pendingAcks[_remoteWindowStart % NetConstants.WindowSize] = false;
                    _receivedPackets[_remoteWindowStart % NetConstants.WindowSize] = null;

                    _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                }

                _pendingAcks[packet.Sequence % NetConstants.WindowSize] = true;
                _receivedPackets[packet.Sequence % NetConstants.WindowSize] = packet;
                return true;
            }

            //Normal packet

            //Check duplicate
            if (_pendingAcks[packet.Sequence % NetConstants.WindowSize])
            {
                _peer.DebugWrite("[RR]ReliableInOrder duplicate");
                return false;
            }

            //save ack
            _pendingAcks[packet.Sequence % NetConstants.WindowSize] = true;

            //detailed check
            if (packet.Sequence == _remoteSequence)
            {
                _peer.DebugWrite("[RR]ReliableInOrder packet succes");
                _peer.AddIncomingPacket(packet);
                _remoteSequence = (_remoteSequence + 1) % NetConstants.MaxSequence;

                while(true)
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

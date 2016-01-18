using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    public class ReliableOrderedChannel : INetChannel
    {
        //For reliable inOrder
        private ushort _localSeqence;
        private ushort _remoteSequence;

        private Queue<NetPacket> _outgoingPackets;
        private bool[] _pendingAcks;                //for send acks
        private NetPacket[] _pendingAckPackets;     //for unacked packets
        private NetPacket[] _receivedPackets;            //for drop duplicates
 
        private int _windowStart;
        private int _windowSize;
        private int _remoteWindowStart;
        private NetPeer _peer;
        private int _queueIndex;
        private bool _mustSendAcks;

        //Socket constructor
        public ReliableOrderedChannel(NetPeer peer)
        {
            _peer = peer;

            _outgoingPackets = new Queue<NetPacket>();

            _pendingAcks = new bool[_windowSize];
            _pendingAckPackets = new NetPacket[_windowSize];
            _receivedPackets = new NetPacket[_windowSize];

            _windowStart = 0;
            _localSeqence = 0;
            _remoteSequence = 0;
            _remoteWindowStart = 0;
        }

        //ProcessAck in packet
        public void ProcessAck(byte[] acksData)
        {
            int offset = 2;
            ushort reliableAcks = BitConverter.ToUInt16(acksData, 0);
            int reliableInOrderAcks = (acksData.Length - offset) / 2 - reliableAcks;

            _peer.DebugWrite("[PA]Length: {0}\n[PA]RLB Acks: {1}\n[PA]RLB_INO Acks: {2}", acksData.Length, reliableAcks, reliableInOrderAcks);

            for (int i = 0; i < reliableInOrderAcks; i++)
            {
                ushort ack = BitConverter.ToUInt16(acksData, offset);
                int storeIdx = ack % _windowSize;

                if (_pendingAckPackets[storeIdx] != null)
                {
                    NetPacket removed = _pendingAckPackets[storeIdx];
                    _pendingAckPackets[storeIdx] = null;
                    _peer.UpdateFlowMode(removed.timeStamp);
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - true", ack);
                }
                else
                {
                    _peer.DebugWrite("[PA]Removing reliableInOrder ack: {0} - false", ack);
                }

                offset += 2;
            }
        }

        public void AddToQueue(NetPacket packet)
        {
            _outgoingPackets.Enqueue(packet);
        }

        public void ResetQueueIndex()
        {
            _queueIndex = _windowStart;
        }

        private NetPacket SendAcks()
        {
            //Init packet
            NetPacket p = _peer.CreatePacket();
            p.property = PacketProperty.AckReliableOrdered;
            p.data = new byte[10];

            //Put window start
            FastBitConverter.GetBytes(p.data, 0, _windowStart);

            //Put acks
            ulong acks = 0;
            int start = _windowStart % _windowSize;
            int idx = start;
            int bit = 0;
            do
            {
                if(_pendingAcks[idx])
                    acks |= 1ul << bit;

                bit++;
                idx = (idx + 1) % _windowSize;
            } while (idx != start);

            //save to data
            FastBitConverter.GetBytes(p.data, 2, acks);

            return p;
        }

        public NetPacket GetQueuedPacket()
        {
            if (_mustSendAcks)
            {
                _mustSendAcks = false;
                return SendAcks();
            }

            do
            {
                //Return first pending packet
                if (_pendingAckPackets[_queueIndex] != null)
                    return _pendingAckPackets[_queueIndex];

                //increase idx
                _queueIndex = (_queueIndex + 1) % _windowSize;
            } while (_queueIndex != _windowStart);

            return null;
        }

        //Process incoming packet
        public bool ProcessPacket(NetPacket packet)
        {
            int relate = NetConstants.RelativeSequenceNumber(packet.sequence, _remoteWindowStart);
            if(relate < 0)
            {
                //Too old packet
                _peer.DebugWrite("[RR]ReliableInOrder too old");
                return false;
            }

            if (relate >= _windowSize)
            {
                //Too new packet
                _peer.DebugWrite("[RR]ReliableInOrder very new");
                return false;
            }

            //Send ack
            _pendingAcks[packet.sequence % NetConstants.MaxSequence] = true;
            _mustSendAcks = true;

            if (relate == 0)
            {
                _peer.DebugWrite("[RR]ReliableInOrder packet succes");
                _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                _peer.AddIncomingPacket(packet);

                while(true)
                {
                    NetPacket p = _receivedPackets[_remoteWindowStart % _windowSize];
                    if (p == null)
                        break;

                    //process holded packet
                    _receivedPackets[_remoteWindowStart % _windowSize] = null;
                    _peer.ProcessPacket(p);
                    _remoteWindowStart = (_remoteWindowStart + 1) % NetConstants.MaxSequence;
                }

                return true;
            }

            _receivedPackets[packet.sequence % _windowSize] = packet;
            return true;
        }
    }
}

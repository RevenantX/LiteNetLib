using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    class ReliableUnorderedChannel : INetChannel
    {
        private NetPeer _peer;
        private Queue<NetPacket> _packetQueue; 

        public ReliableUnorderedChannel(NetPeer peer)
        {
            _packetQueue = new Queue<NetPacket>();
            _peer = peer;
        }

        public void AddToQueue(NetPacket packet)
        {
            _packetQueue.Enqueue(packet);
        }

        public void ProcessAck(byte[] acksData)
        {
            //for (ushort i = 0; i < reliableAcks; i++)
            //{
            //    ushort ack = BitConverter.ToUInt16(acksData, offset);
            //    int storeIdx = ack % _windowSize;

            //    if (_pendingAckPackets[storeIdx] != null)
            //    {
            //        //Ack received
            //        NetPacket removed = _pendingAckPackets[storeIdx];
            //        _pendingAckPackets[storeIdx] = null;
            //        _peer.UpdateFlowMode(removed.timeStamp);
            //        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliable ack: {0} - true", ack);
            //    }
            //    else
            //    {
            //        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliable ack: {0} - false", ack);
            //    }

            //    offset += 2;
            //}
        }

        public NetPacket GetQueuedPacket()
        {
            return null;
        }

        public bool ProcessPacket(NetPacket packet)
        {
            ////Send ack
            //_ackReliableQueue.Enqueue(packet.sequence);

            ////Drop duplicate
            //if (_receivedPackets[packet.sequence % _windowSize])
            //{
            //    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Duplicate: {0}", packet.sequence);
            //    return false;
            //}

            ////Setting remote sequence
            //if (SequenceMoreRecent(packet.sequence, _remoteSequence))
            //{
            //    _remoteSequence = packet.sequence;
            //    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Set remoteSequence to: {0}", _remoteSequence);
            //}

            ////Adding to received queue
            //NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Add to received Queue");
            //_receivedPackets[packet.sequence % _windowSize] = true;

            return true;
        }

        public NetPacket GetReceivedPacket()
        {
            return null;
        }

        public void ResetQueueIndex()
        {
            
        }
    }
}

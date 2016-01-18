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
            
        }

        public NetPacket GetQueuedPacket()
        {
            
        }
    }
}

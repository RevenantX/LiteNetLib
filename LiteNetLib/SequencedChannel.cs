namespace LiteNetLib
{
    class SequencedChannel : INetChannel
    {
        private NetPeer _peer;

        public SequencedChannel(NetPeer peer)
        {
            _peer = peer;
        }

        public void AddToQueue(NetPacket packet)
        {
            
        }

        public NetPacket GetQueuedPacket()
        {
            
        }
    }
}

namespace LiteNetLib
{
    class SequencedChannel
    {
        private NetConnection netConnection;

        public SequencedChannel(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }

        public void SendPacket(NetPacket packet)
        {
            
        }
    }
}

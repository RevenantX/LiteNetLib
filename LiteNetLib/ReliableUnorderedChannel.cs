namespace LiteNetLib
{
    class ReliableUnorderedChannel
    {
        private NetConnection netConnection;

        public ReliableUnorderedChannel(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }

        public void SendPacket(NetPacket packet)
        {
            
        }
    }
}

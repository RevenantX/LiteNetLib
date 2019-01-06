namespace LiteNetLib
{
    internal sealed class SimpleChannel : BaseChannel
    {
        public SimpleChannel(NetPeer peer) : base(peer)
        {

        }

        public override void SendNextPackets()
        {
            lock (OutgoingQueue)
            {
                while (OutgoingQueue.Count > 0)
                {
                    NetPacket packet = OutgoingQueue.Dequeue();
                    Peer.SendUserData(packet);
                    Peer.Recycle(packet);
                }
            }
        }

        public override void ProcessPacket(NetPacket packet)
        {
            
        }
    }
}

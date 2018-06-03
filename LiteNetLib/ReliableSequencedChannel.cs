namespace LiteNetLib
{
    internal sealed class ReliableSequencedChannel : BaseChannel
    {
        public ReliableSequencedChannel(NetPeer peer) : base(peer)
        {
            //TODO reliable sequenced
        }

        public override void SendNextPackets()
        {
            
        }

        public override void ProcessPacket(NetPacket packet)
        {
            
        }
    }
}

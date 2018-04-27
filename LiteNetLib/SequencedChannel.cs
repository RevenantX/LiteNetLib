namespace LiteNetLib
{
    internal sealed class SequencedChannel : BaseChannel
    {
        private int _localSequence;
        private int _remoteSequence;

        public SequencedChannel(NetPeer peer) : base(peer)
        {

        }

        public override void SendNextPackets()
        {
            lock (OutgoingQueue)
            {
                while (OutgoingQueue.Count > 0)
                {
                    NetPacket packet = OutgoingQueue.Dequeue();
                    _localSequence = (_localSequence + 1) % NetConstants.MaxSequence;
                    packet.Sequence = (ushort)_localSequence;
                    Peer.SendRawData(packet);
                    Peer.Recycle(packet);
                }
            }
        }

        public override void ProcessPacket(NetPacket packet)
        {
            int relative = NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteSequence);
            if (packet.Sequence < NetConstants.MaxSequence && relative > 0)
            {
                Peer.Statistics.PacketLoss += (ulong)(relative - 1);
                _remoteSequence = packet.Sequence;
                Peer.AddIncomingPacket(packet);
            }
        }
    }
}

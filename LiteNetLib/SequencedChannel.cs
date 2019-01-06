namespace LiteNetLib
{
    internal sealed class SequencedChannel : BaseChannel
    {
        private int _localSequence;
        private ushort _remoteSequence;
        private readonly bool _reliable;
        private NetPacket _lastPacket;
        private readonly NetPacket _ackPacket;
        private bool _mustSendAck;

        public SequencedChannel(NetPeer peer, bool reliable) : base(peer)
        {
            _reliable = reliable;
            if (_reliable)
            {
                _ackPacket = new NetPacket(PacketProperty.AckReliableSequenced, 0);
            }
        }

        public override void SendNextPackets()
        {
            if (_reliable && OutgoingQueue.Count == 0)
            {
                var packet = _lastPacket;
                if(packet != null)
                    Peer.SendUserData(packet);
            }
            else
            {
                lock (OutgoingQueue)
                {
                    while (OutgoingQueue.Count > 0)
                    {
                        NetPacket packet = OutgoingQueue.Dequeue();
                        _localSequence = (_localSequence + 1) % NetConstants.MaxSequence;
                        packet.Sequence = (ushort)_localSequence;
                        Peer.SendUserData(packet);

                        if (_reliable && OutgoingQueue.Count == 0)
                            _lastPacket = packet;
                        else
                            Peer.Recycle(packet);
                    }
                }
            }

            if (_reliable && _mustSendAck)
            {
                _ackPacket.Sequence = _remoteSequence;
                Peer.SendUserData(_ackPacket);
            }
        }

        public void ProcessAck(NetPacket packet)
        {
            if (_lastPacket != null && packet.Sequence == _lastPacket.Sequence)
            {
                //TODO: recycle?
                _lastPacket = null;
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
            _mustSendAck = true;
        }
    }
}

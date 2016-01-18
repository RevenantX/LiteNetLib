using System.Collections.Generic;

namespace LiteNetLib
{
    class SequencedChannel : INetChannel
    {
        private ushort _localSequence;
        private ushort _remoteSequence;
        private Queue<NetPacket> _outgoingPackets;
        private NetPeer _peer;

        public SequencedChannel(NetPeer peer)
        {
            _outgoingPackets = new Queue<NetPacket>();
            _peer = peer;
        }

        public void AddToQueue(NetPacket packet)
        {
            _outgoingPackets.Enqueue(packet);
        }

        public NetPacket GetQueuedPacket()
        {
            if (_outgoingPackets.Count == 0)
                return null;

            _localSequence++;
            var p =  _outgoingPackets.Dequeue();
            p.Sequence = _localSequence;
            return p;
        }

        public bool ProcessPacket(NetPacket packet)
        {
            if (NetConstants.SequenceMoreRecent(packet.Sequence, _remoteSequence))
            {
                _remoteSequence = packet.Sequence;
                _peer.AddIncomingPacket(packet);
                return true;
            }
            return false;
        }
    }
}

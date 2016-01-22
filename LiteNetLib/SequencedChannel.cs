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
            lock (_outgoingPackets)
            {
                _outgoingPackets.Enqueue(packet);
            }
        }

        public NetPacket GetQueuedPacket()
        {
            if (_outgoingPackets.Count == 0)
                return null;

            _localSequence++;
            NetPacket packet;
            lock (_outgoingPackets)
            {
                packet = _outgoingPackets.Dequeue();
            }
            packet.Sequence = _localSequence;
            return packet;
        }

        public bool ProcessPacket(NetPacket packet)
        {
            if (NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteSequence) > 0)
            {
                _remoteSequence = packet.Sequence;
                _peer.AddIncomingPacket(packet);
                return true;
            }
            return false;
        }
    }
}

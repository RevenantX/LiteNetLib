using System.Collections.Generic;

namespace LiteNetLib
{
    internal sealed class SequencedChannel
    {
        private int _localSequence;
        private int _remoteSequence;
        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly NetPeer _peer;

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

        public void SendNextPackets()
        {
            lock (_outgoingPackets)
            {
                while (_outgoingPackets.Count > 0)
                {
                    NetPacket packet = _outgoingPackets.Dequeue();
                    _localSequence = (_localSequence + 1) % NetConstants.MaxSequence;
                    packet.Sequence = (ushort)_localSequence;
                    _peer.SendRawData(packet);
                    _peer.Recycle(packet);
                }
            }
        }

        public void ProcessPacket(NetPacket packet)
        {
            if (packet.Sequence < NetConstants.MaxSequence && 
                NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteSequence) > 0)
            {
                _remoteSequence = packet.Sequence;
                _peer.AddIncomingPacket(packet);
            }
        }
    }
}

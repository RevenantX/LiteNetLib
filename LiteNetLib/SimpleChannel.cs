using System.Collections.Generic;

namespace LiteNetLib
{
    internal sealed class SimpleChannel
    {
        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly NetPeer _peer;

        public SimpleChannel(NetPeer peer)
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

        public bool SendNextPacket()
        {
            if (_outgoingPackets.Count == 0)
                return false;

            NetPacket packet;
            lock (_outgoingPackets)
            {
                packet = _outgoingPackets.Dequeue();
            }
            _peer.SendRawData(packet.RawData);
            _peer.Recycle(packet);
            return true;
        }
    }
}

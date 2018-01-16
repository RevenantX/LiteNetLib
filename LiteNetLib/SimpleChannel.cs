using System.Collections.Generic;

namespace LiteNetLib
{
    internal sealed class SimpleChannel
    {
        private readonly SwitchQueue<NetPacket> _outgoingPackets;
        private readonly NetPeer _peer;

        public SimpleChannel(NetPeer peer)
        {
            _outgoingPackets = new SwitchQueue<NetPacket>();
            _peer = peer;
        }

        public void AddToQueue(NetPacket packet)
        {
            _outgoingPackets.Push(packet);
        }

        public bool SendNextPackets()
        {
            NetPacket packet;
            _outgoingPackets.Switch();
            while (_outgoingPackets.Empty() != true)
            {
                packet = _outgoingPackets.Pop();
                _peer.SendRawData(packet);
                _peer.Recycle(packet);
            }
            return true;
        }
    }
}

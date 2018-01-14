using LiteNetLib.Utils;
using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    internal sealed class KcpChannel
    {
        private readonly Queue<NetPacket> _outgoingPackets;
        private readonly NetPeer _peer;
        private readonly KCP _kcp;
        private uint _currentUpdateTime;
        private bool _needUpdateFlag;
        private UInt32 _nextUpdateTime;


        public KcpChannel(NetPeer peer)
        {
            _outgoingPackets = new Queue<NetPacket>();
            _peer = peer;
            _kcp = new KCP(12345, SendKCP);
            _kcp.NoDelay(1, 10, 2, 1);
            _kcp.WndSize(128, 128);
            _currentUpdateTime = 0;
            _nextUpdateTime = 0;
            _needUpdateFlag = false;
        }

        private void SendKCP(byte[] buf, int size)
        {
            NetPacket p = _peer.GetPacketFromPool(PacketProperty.KCP, size);
            Buffer.BlockCopy(buf, 0, p.RawData, NetPacket.GetHeaderSize(PacketProperty.KCP), size);
            lock (_outgoingPackets)
            {
                _outgoingPackets.Enqueue(p);
            }
        }

        public void AddToQueue(NetPacket packet)
        {
            int result = _kcp.Send(packet.CopyPacketData());
            _needUpdateFlag = true;
            if (result == -2)
                NetUtils.DebugWrite("[KCP] Packet size must be lower then 255.");
            if (result == -1)
                NetUtils.DebugWrite("[KCP] Packet buffer is invalid.");
        }

        public void Update(uint dt)
        {
            _currentUpdateTime += dt;
            if (_needUpdateFlag || _currentUpdateTime >= _nextUpdateTime)
            {
                _kcp.Update(_currentUpdateTime);
                _nextUpdateTime = _kcp.Check(_currentUpdateTime);
                _needUpdateFlag = false;
            }
        }

        public void ProcessPacket(NetPacket packet)
        {
            _kcp.Input(packet.RawData);
            _needUpdateFlag = true;

            for (var size = _kcp.PeekSize(); size > 0; size = _kcp.PeekSize())
            {
                NetPacket p = _peer.GetPacketFromPool(PacketProperty.KCP, size);
                if (_kcp.Recv(p.RawData) > 0)
                {
                    _peer.AddIncomingPacket(p);
                }
            }
        }

        public void SendNextPackets()
        {
            NetPacket packet;
            lock (_outgoingPackets)
            {
                while (_outgoingPackets.Count > 0)
                {
                    packet = _outgoingPackets.Dequeue();
                    _peer.SendRawData(packet);
                    _peer.Recycle(packet);
                }
            }
        }
    }
}

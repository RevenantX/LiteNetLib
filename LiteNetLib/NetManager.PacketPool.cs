using System;
using System.Threading;

namespace LiteNetLib
{
    public partial class NetManager
    {
        private NetPacket _poolHead;
        private int _poolCount;
        private readonly object _poolLock = new object();

        private NetPacket PoolGetWithData(PacketProperty property, byte[] data, int start, int length)
        {
            int headerSize = NetPacket.GetHeaderSize(property);
            NetPacket packet = PoolGetPacket(length + headerSize);
            packet.Property = property;
            Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
            return packet;
        }

        //Get packet with size
        private NetPacket PoolGetWithProperty(PacketProperty property, int size)
        {
            NetPacket packet = PoolGetPacket(size + NetPacket.GetHeaderSize(property));
            packet.Property = property;
            return packet;
        }

        private NetPacket PoolGetWithProperty(PacketProperty property)
        {
            NetPacket packet = PoolGetPacket(NetPacket.GetHeaderSize(property));
            packet.Property = property;
            return packet;
        }

        internal NetPacket PoolGetPacket(int size)
        {
            if (size > NetConstants.MaxPacketSize)
                return new NetPacket(size);

            NetPacket packet;
            lock (_poolLock)
            {
                packet = _poolHead;
                if (packet == null)
                    return new NetPacket(size);
                _poolHead = _poolHead.Next;
            }

            Interlocked.Decrement(ref _poolCount);
            packet.Size = size;
            if (packet.RawData.Length < size)
                packet.RawData = new byte[size];
            return packet;
        }

        internal void PoolRecycle(NetPacket packet)
        {
            if (packet.RawData.Length > NetConstants.MaxPacketSize || _poolCount >= NetConstants.PacketPoolSize)
            {
                //Don't pool big packets. Save memory
                return;
            }

            Interlocked.Increment(ref _poolCount);

            //Clean fragmented flag
            packet.RawData[0] = 0;
            lock (_poolLock)
            {
                packet.Next = _poolHead;
                _poolHead = packet;
            }
        }
    }
}

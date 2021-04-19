using System;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class NetPacketPool
    {
        private NetPacket _head;
        private int _count;

        public NetPacket GetWithData(PacketProperty property, byte[] data, int start, int length)
        {
            int headerSize = NetPacket.GetHeaderSize(property);
            NetPacket packet = GetPacket(length + headerSize);
            packet.Property = property;
            Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
            return packet;
        }

        //Get packet with size
        public NetPacket GetWithProperty(PacketProperty property, int size)
        {
            NetPacket packet = GetPacket(size + NetPacket.GetHeaderSize(property));
            packet.Property = property;
            return packet;
        }

        public NetPacket GetWithProperty(PacketProperty property)
        {
            NetPacket packet = GetPacket(NetPacket.GetHeaderSize(property));
            packet.Property = property;
            return packet;
        }

        public NetPacket GetPacket(int size)
        {
            if (size > NetConstants.MaxPacketSize) 
                return new NetPacket(size);

            NetPacket packet;
            do
            {
                packet = _head;
                if (packet == null)
                    return new NetPacket(size);
            } while (packet != Interlocked.CompareExchange(ref _head, packet.Next, packet));

#if DEBUG_REFCOUNT
            if (Interlocked.Increment(ref packet.RefCount) > 1)
            {
                Interlocked.Decrement(ref packet.RefCount);
                NetDebug.WriteError("PacketRefCount more than 1: {0}", Environment.StackTrace);
                return new NetPacket(size);
            }
#endif

            _count--;
            packet.Size = size;
            if (packet.RawData.Length < size)
                packet.RawData = new byte[size];
            return packet;
        }

        public void Recycle(NetPacket packet)
        {
            if (packet.RawData.Length > NetConstants.MaxPacketSize || _count >= NetConstants.PacketPoolSize)
            {
                //Don't pool big packets. Save memory
                return;
            }

            _count++;

            //Clean fragmented flag
            packet.RawData[0] = 0;

#if DEBUG_REFCOUNT
            if (packet.RefCount > 0)
                Interlocked.Decrement(ref packet.RefCount);
#endif

            do
            {
                packet.Next = _head;
            } while (packet.Next != Interlocked.CompareExchange(ref _head, packet, packet.Next));
        }
    }
}

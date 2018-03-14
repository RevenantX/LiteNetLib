using System;

namespace LiteNetLib
{
    internal class NetPacketPool
    {
        private const int PoolLimit = 1000;
        private readonly NetPacket[] _pool = new NetPacket[PoolLimit];
        private int _count;

        public NetPacket GetWithData(PacketProperty property, byte[] data, int start, int length)
        {
            var packet = GetWithProperty(property, length);
            Buffer.BlockCopy(data, start, packet.RawData, NetPacket.GetHeaderSize(property), length);
            return packet;
        }

        public NetPacket GetPacket(int size, bool clear)
        {
            NetPacket packet = null;
            if (size <= NetConstants.MaxPacketSize)
            {
                lock (_pool)
                {
                    if (_count > 0)
                    {
                        _count--;
                        packet = _pool[_count];
                        _pool[_count] = null;
                    }
                }
            }
            if (packet == null)
            {
                //allocate new packet
                packet = new NetPacket(size);
            }
            else
            {
                //reallocate packet data if packet not fits
                if (!packet.Realloc(size) && clear)
                {
                    //clear in not reallocated
                    Array.Clear(packet.RawData, 0, size);
                }
            }
            return packet;
        }

        //Get packet with size
        public NetPacket GetWithProperty(PacketProperty property, int size)
        {
            size += NetPacket.GetHeaderSize(property);
            NetPacket packet = GetPacket(size, true);
            packet.Property = property;
            packet.Size = size;
            return packet;
        }

        public void Recycle(NetPacket packet)
        {
            if (packet.Size > NetConstants.MaxPacketSize)
            {
                //Dont pool big packets. Save memory
                return;
            }

            //Clean fragmented flag
            packet.IsFragmented = false;
            lock (_pool)
            {
                if (_count == PoolLimit)
                    return;
                _pool[_count] = packet;
                _count++;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal class NetPacketPool
    {
        private const int PoolLimit = 1000;
        private readonly Stack<NetPacket> _pool;

        public NetPacketPool()
        {
            _pool = new Stack<NetPacket>();
        }

        public NetPacket GetWithData(PacketProperty property, NetDataWriter writer)
        {
            var packet = Get(property, writer.Length);
            Buffer.BlockCopy(writer.Data, 0, packet.RawData, NetPacket.GetHeaderSize(property), writer.Length);
            return packet;
        }

        public NetPacket GetWithData(PacketProperty property, byte[] data, int start, int length)
        {
            var packet = Get(property, length);
            Buffer.BlockCopy(data, start, packet.RawData, NetPacket.GetHeaderSize(property), length);
            return packet;
        }

        private NetPacket GetPacket(int size, bool clear)
        {
            NetPacket packet = null;
            if (size <= NetConstants.MaxPacketSize)
            {
                lock (_pool)
                {
                    if (_pool.Count > 0)
                    {
                        packet = _pool.Pop();
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

        //Get packet just for read
        public NetPacket GetAndRead(byte[] data, int start, int count)
        {
            NetPacket packet = GetPacket(count, false);
            if (!packet.FromBytes(data, start, count))
            {
                Recycle(packet);
                return null;
            }
            return packet;
        }

        //Get packet with size
        public NetPacket Get(PacketProperty property, int size)
        {
            size += NetPacket.GetHeaderSize(property);
            NetPacket packet = GetPacket(size, true);
            packet.Property = property;
            packet.Size = size;
            return packet;
        }

        public void Recycle(NetPacket packet)
        {
            if (packet.Size > NetConstants.MaxPacketSize || _pool.Count > PoolLimit)
            {
                //Dont pool big packets. Save memory
                return;
            }

            //Clean fragmented flag
            packet.IsFragmented = false;
            lock (_pool)
            {
                _pool.Push(packet);
            }
        }
    }
}

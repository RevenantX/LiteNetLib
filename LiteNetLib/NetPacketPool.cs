using System;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class NetPacketPool
    {
        private readonly NetPacket[] _pool = new NetPacket[NetConstants.PacketPoolSize];
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
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
            if (size <= NetConstants.MaxPacketSize)
            {
                NetPacket packet = null;
                _lock.EnterUpgradeableReadLock();
                if (_count > 0)
                {
                    _lock.EnterWriteLock();
                    _count--;
                    packet = _pool[_count];
                    _pool[_count] = null;
                    _lock.ExitWriteLock();
                }
                _lock.ExitUpgradeableReadLock();
                if (packet != null)
                {
                    packet.Size = (ushort)size;
                    if (packet.RawData.Length < size)
                        packet.RawData = new byte[size];
                    return packet;
                }
            }
            return new NetPacket(size);
        }

        public void Recycle(NetPacket packet)
        {
            if (packet.RawData.Length > NetConstants.MaxPacketSize)
            {
                //Dont pool big packets. Save memory
                return;
            }

            //Clean fragmented flag
            packet.RawData[0] = 0;

            _lock.EnterUpgradeableReadLock();
            if (_count == NetConstants.PacketPoolSize)
            {
                _lock.ExitUpgradeableReadLock();
                return;
            }
            _lock.EnterWriteLock();
            _pool[_count] = packet;
            _count++;
            _lock.ExitWriteLock();
            _lock.ExitUpgradeableReadLock();
        }
    }
}

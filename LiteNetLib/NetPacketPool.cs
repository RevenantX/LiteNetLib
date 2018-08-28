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
            var packet = GetWithProperty(property, length);
            Buffer.BlockCopy(data, start, packet.RawData, NetPacket.GetHeaderSize(property), length);
            return packet;
        }

        public NetPacket GetPacket(int size, bool clear)
        {
            NetPacket packet = null;
            if (size <= NetConstants.MaxPacketSize)
            {
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
            }
            if (packet == null)
            {
                //allocate new packet
                packet = new NetPacket(size);
            }
            else
            {
                //reallocate packet data if packet not fits
                packet.Realloc(size, clear);
            }
            return packet;
        }

        //Get packet with size
        public NetPacket GetWithProperty(PacketProperty property, int size)
        {
            size += NetPacket.GetHeaderSize(property);
            NetPacket packet = GetPacket(size, true);
            packet.Property = property;
            return packet;
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

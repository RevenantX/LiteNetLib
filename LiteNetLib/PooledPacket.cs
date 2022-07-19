using System;

namespace LiteNetLib
{
    public readonly ref struct PooledPacket
    {
        internal readonly NetPacket _packet;
        internal readonly int _channelNumber;

        public readonly int MaxUserDataSize;
        public readonly int UserDataOffset;
        public byte[] Data => _packet.RawData;
        public int UserDataSize
        {
            get => _packet.Size - UserDataOffset;
            set
            {
                if (value > MaxUserDataSize)
                    throw new Exception($"Size bigger than maximum({MaxUserDataSize})");
                _packet.Size = value + UserDataOffset;
            }
        }

        internal PooledPacket(NetPacket packet, int mtu, byte channelNumber)
        {
            _packet = packet;
            UserDataOffset = _packet.GetHeaderSize();
            MaxUserDataSize = mtu - UserDataOffset;
            _channelNumber = channelNumber;
        }
    }
}

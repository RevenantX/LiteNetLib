namespace LiteNetLib
{
    public enum SendOptions
    {
        None,
        Reliable,
        Sequenced,
        ReliableOrdered
    }

    public static class NetConstants
    {
        public const int HeaderSize = 1;
        public const int SequencedHeaderSize = 3;
        public const int WindowSize = 64;
        public const ushort MaxSequence = 65535;
        public const ushort HalfMaxSequence = MaxSequence / 2;
        public const int MaxPacketSize = 1432;
        public const int MaxPacketDataSize = MaxPacketSize - HeaderSize;
    }
}

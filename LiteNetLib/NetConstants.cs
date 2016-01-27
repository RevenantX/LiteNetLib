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
        public const int DefaultWindowSize = 64;
        public const ushort MaxSequence = 65535;
        public const ushort HalfMaxSequence = MaxSequence / 2;
        public const int MaxPacketSize = 1432;
        public const int MaxPacketDataSize = MaxPacketSize - HeaderSize;

        //peer specific
        public const int FlowUpdateTime = 1000;
        public const int FlowIncreaseThreshold = 32;
        public const int PacketsPerSecondMax = 65535;
    }
}

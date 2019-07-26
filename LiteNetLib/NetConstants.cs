namespace LiteNetLib
{
    /// <summary>
    /// Sending method type
    /// </summary>
    public enum DeliveryMethod
    {
        /// <summary>
        /// Unreliable. Packets can be dropped, can be duplicated, can arrive without order.
        /// </summary>
        Unreliable,

        /// <summary>
        /// Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
        /// </summary>
        ReliableUnordered,

        /// <summary>
        /// Unreliable. Packets can be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        Sequenced,

        /// <summary>
        /// Reliable and ordered. Packets won't be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        ReliableOrdered,

        /// <summary>
        /// Reliable only last packet. Packets can be dropped (except the last one), won't be duplicated, will arrive in order.
        /// </summary>
        ReliableSequenced
    }

    /// <summary>
    /// Network constants. Can be tuned from sources for your purposes.
    /// </summary>
    public static class NetConstants
    {
        internal static byte ChannelNumberToId(DeliveryMethod method, byte channelNumber, byte channelsCount)
        {
            int multiplier = 0;
            switch (method)
            {
                case DeliveryMethod.Sequenced: multiplier = 1; break;
                case DeliveryMethod.ReliableOrdered: multiplier = 2; break;
                case DeliveryMethod.ReliableSequenced: multiplier = 3; break;
            }
            return (byte)(channelNumber + multiplier * channelsCount);
        }

        internal static DeliveryMethod ChannelIdToDeliveryMethod(byte channelId, byte channelsCount)
        {
            switch (channelId / channelsCount)
            {
                case 1: return DeliveryMethod.Sequenced;
                case 2: return DeliveryMethod.ReliableOrdered;
                case 3: return DeliveryMethod.ReliableSequenced;
            }
            return DeliveryMethod.ReliableUnordered;
        }

        //can be tuned
        public const int DefaultWindowSize = 64;
        public const int SocketBufferSize = 1024 * 1024; //1mb
        public const int SocketTTL = 255;

        public const int HeaderSize = 1;
        public const int ChanneledHeaderSize = 4;
        public const int FragmentHeaderSize = 6;
        public const ushort MaxSequence = 32768;
        public const ushort HalfMaxSequence = MaxSequence / 2;

        //protocol
        internal const int ProtocolId = 9;
        internal const int MaxUdpHeaderSize = 68;

        internal static readonly int[] PossibleMtu =
        {
            576  - MaxUdpHeaderSize, //minimal
            1232 - MaxUdpHeaderSize,
            1460 - MaxUdpHeaderSize, //google cloud
            1472 - MaxUdpHeaderSize, //VPN
            1492 - MaxUdpHeaderSize, //Ethernet with LLC and SNAP, PPPoE (RFC 1042)
            1500 - MaxUdpHeaderSize  //Ethernet II (RFC 1191)
        };

        internal static readonly int MaxPacketSize = PossibleMtu[PossibleMtu.Length - 1];

        //peer specific
        public const byte MaxConnectionNumber = 4;

        public const int PacketPoolSize = 1000;
    }
}

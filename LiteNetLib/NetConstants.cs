namespace LiteNetLib
{
    /// <summary>
    /// Sending method type
    /// </summary>
    public enum DeliveryMethod
    {
        /// <summary>
        /// Unreliable. Packets can be dropped, duplicated or arrive without order
        /// </summary>
        Unreliable,

        /// <summary>
        /// Reliable. All packets will be sent and received, but without order
        /// </summary>
        ReliableUnordered,

        /// <summary>
        /// Unreliable. Packets can be dropped, but never duplicated and arrive in order
        /// </summary>
        Sequenced,

        /// <summary>
        /// Reliable and ordered. All packets will be sent and received in order
        /// </summary>
        ReliableOrdered,

        /// <summary>
        /// Reliable only last packet
        /// </summary>
        ReliableSequenced
    }

    /// <summary>
    /// Network constants. Can be tuned from sources for your purposes.
    /// </summary>
    public static class NetConstants
    {
        //can be tuned
        public const int DefaultWindowSize = 64;
#if UNITY_PS4 || UNITY_IOS
        public const int SocketBufferSize = 1024 * 1024; //1mb
#else
        public const int SocketBufferSize = 1024 * 1024 * 4; //4mb
#endif
        public const int SocketTTL = 255;

        public const int HeaderSize = 1;
        public const int SequencedHeaderSize = 3;
        public const int FragmentHeaderSize = 6;
        public const ushort MaxSequence = 32768;
        public const ushort HalfMaxSequence = MaxSequence / 2;

        //internal
        internal const string MulticastGroupIPv6 = "FF02:0:0:0:0:0:0:1";

        //protocol
        internal const int ProtocolId = 4;
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

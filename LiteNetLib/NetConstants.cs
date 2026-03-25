namespace LiteNetLib
{
    /// <summary>
    /// Sending method type
    /// </summary>
    public enum DeliveryMethod : byte
    {
        /// <summary>
        /// Unreliable. Packets can be dropped, can be duplicated, can arrive without order.
        /// </summary>
        Unreliable = 4,

        /// <summary>
        /// Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
        /// </summary>
        ReliableUnordered = 0,

        /// <summary>
        /// Unreliable. Packets can be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        Sequenced = 1,

        /// <summary>
        /// Reliable and ordered. Packets won't be dropped, won't be duplicated, will arrive in order.
        /// </summary>
        ReliableOrdered = 2,

        /// <summary>
        /// Reliable only last packet. Packets can be dropped (except the last one), won't be duplicated, will arrive in order.
        /// Cannot be fragmented
        /// </summary>
        ReliableSequenced = 3
    }

    /// <summary>
    /// Network constants. Can be tuned from sources for your purposes.
    /// </summary>
    public static class NetConstants
    {
        /// <summary>
        /// Default window size for reliable channels (number of packets).
        /// </summary>
        public const int DefaultWindowSize = 64;
        /// <summary>
        /// Size of the underlying UDP socket receive and send buffers in bytes. <br/>
        /// Default is 1MB.
        /// </summary>
        public const int SocketBufferSize = 1024 * 1024;
        /// <summary>
        /// Time To Live (TTL) for the UDP packets.
        /// </summary>
        public const int SocketTTL = 255;

        /// <summary>
        /// Size of the base packet header (PacketProperty) in <see cref="byte"/>s.
        /// </summary>
        public const int HeaderSize = 1;
        /// <summary>
        /// Size of the header for sequenced or reliable messages in <see cref="byte"/>s. <br/>
        /// Includes <see cref="HeaderSize"/>, Sequence, and ChannelId.
        /// </summary>
        public const int ChanneledHeaderSize = 4;
        /// <summary>
        /// Additional header size required for fragmented packets in <see cref="byte"/>s. <br/>
        /// Includes FragmentId, FragmentPart, and FragmentsTotal.
        /// </summary>
        public const int FragmentHeaderSize = 6;
        /// <summary>
        /// Total header size for a fragmented channeled packet in <see cref="byte"/>s. <br/>
        /// Combines <see cref="ChanneledHeaderSize"/> and <see cref="FragmentHeaderSize"/>.
        /// </summary>
        public const int FragmentedHeaderTotalSize = ChanneledHeaderSize + FragmentHeaderSize;
        /// <summary>
        /// Maximum possible sequence number before wrapping back to zero.
        /// </summary>
        public const ushort MaxSequence = 32768;
        /// <summary>
        /// Half of the <see cref="MaxSequence"/>, used for sequence comparison and wrap-around logic.
        /// </summary>
        public const ushort HalfMaxSequence = MaxSequence / 2;

        //protocol
        internal const int ProtocolId = 13;
        internal const int MaxUdpHeaderSize = 68;
        internal const int ChannelTypeCount = 4;
        internal const int FragmentedChannelsCount = 2;
        internal const int MaxFragmentsInWindow = DefaultWindowSize / 2;

        internal static readonly int[] PossibleMtu =
        {
            //576  - MaxUdpHeaderSize minimal (RFC 1191)
            1024,                    //most games standard
            1232 - MaxUdpHeaderSize,
            1460 - MaxUdpHeaderSize, //google cloud
            1472 - MaxUdpHeaderSize, //VPN
            1492 - MaxUdpHeaderSize, //Ethernet with LLC and SNAP, PPPoE (RFC 1042)
            1500 - MaxUdpHeaderSize  //Ethernet II (RFC 1191)
        };

        /// <summary>
        /// The starting Maximum Transmission Unit (MTU) used for new connections before path MTU discovery.
        /// </summary>
        public static readonly int InitialMtu = PossibleMtu[0];
        /// <summary>
        /// Maximum possible packet size allowed by the library based on the largest supported MTU.
        /// </summary>
        public static readonly int MaxPacketSize = PossibleMtu[PossibleMtu.Length - 1];
        /// <summary>
        /// Maximum payload size for a single unreliable packet in <see cref="byte"/>s. <br/>
        /// Calculated as <see cref="MaxPacketSize"/> - <see cref="HeaderSize"/>.
        /// </summary>
        public static readonly int MaxUnreliableDataSize = MaxPacketSize - HeaderSize;

        /// <summary>
        /// Maximum possible value for <see cref="NetPacket.ConnectionNumber"/>.
        /// </summary>
        /// <remarks>
        /// This value is used to distinguish between different connection instances from the same <see cref="System.Net.IPEndPoint"/>. <br/>
        /// It allows the receiver to identify and discard packets belonging to previous connection attempts that may arrive
        /// late due to network jitter, even if they originate from the same address and port. <br/>
        /// </remarks>
        public const byte MaxConnectionNumber = 4;
    }
}

using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal enum PacketProperty : byte
    {
        Unreliable,
        Channeled,
        ReliableMerged,
        Ack,
        Ping,
        Pong,
        ConnectRequest,
        ConnectAccept,
        Disconnect,
        UnconnectedMessage,
        MtuCheck,
        MtuOk,
        Broadcast,
        Merged,
        ShutdownOk,
        PeerNotFound,
        InvalidProtocol,
        NatMessage,
        Empty,
        Total
    }

    internal sealed class NetPacket
    {
        private static readonly int PropertiesCount = (int)PacketProperty.Total;
        private static readonly int[] HeaderSizes;

        static NetPacket()
        {
            HeaderSizes = NetUtils.AllocatePinnedUninitializedArray<int>(PropertiesCount);
            for (int i = 0; i < HeaderSizes.Length; i++)
            {
                switch ((PacketProperty)i)
                {
                    case PacketProperty.Channeled:
                    case PacketProperty.Ack:
                    case PacketProperty.ReliableMerged:
                        HeaderSizes[i] = NetConstants.ChanneledHeaderSize;
                        break;
                    case PacketProperty.Ping:
                        HeaderSizes[i] = NetConstants.HeaderSize + 2;
                        break;
                    case PacketProperty.ConnectRequest:
                        HeaderSizes[i] = NetConnectRequestPacket.HeaderSize;
                        break;
                    case PacketProperty.ConnectAccept:
                        HeaderSizes[i] = NetConnectAcceptPacket.Size;
                        break;
                    case PacketProperty.Disconnect:
                        HeaderSizes[i] = NetConstants.HeaderSize + 8;
                        break;
                    case PacketProperty.Pong:
                        HeaderSizes[i] = NetConstants.HeaderSize + 10;
                        break;
                    default:
                        HeaderSizes[i] = NetConstants.HeaderSize;
                        break;
                }
            }
        }

        /// <summary>
        /// Gets or sets the packet property (type).
        /// Stored in the first 5 bits of the first byte (0x1F mask).
        /// </summary>
        public PacketProperty Property
        {
            get => (PacketProperty)(RawData[0] & 0x1F);
            set => RawData[0] = (byte)((RawData[0] & 0xE0) | (byte)value);
        }

        /// <summary>
        /// Gets or sets the connection number used to distinguish between multiple connection instances from the same endpoint.
        /// Stored in bits 6 and 7 of the first byte (0x60 mask).
        /// </summary>
        /// <remarks>
        /// Used to discard packets from previous connections made in the same frame/time.
        /// </remarks>
        public byte ConnectionNumber
        {
            get => (byte)((RawData[0] & 0x60) >> 5);
            set => RawData[0] = (byte)((RawData[0] & 0x9F) | (value << 5));
        }

        /// <summary>
        /// Gets or sets the sequence number of the packet.
        /// Located at offset 1 in <see cref="RawData"/>.
        /// </summary>
        public ushort Sequence
        {
            get => BitConverter.ToUInt16(RawData, 1);
            set => FastBitConverter.GetBytes(RawData, 1, value);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the fragmentation bit (the highest bit of the first byte) is set.
        /// </summary>
        public bool IsFragmented => (RawData[0] & 0x80) != 0;

        /// <summary>
        /// Sets the fragmentation bit (0x80) in the packet header.
        /// </summary>
        public void MarkFragmented() => RawData[0] |= 0x80;

        /// <summary>
        /// Gets or sets the channel identifier.
        /// Located at offset 3 in <see cref="RawData"/>.
        /// </summary>
        public byte ChannelId
        {
            get => RawData[3];
            set => RawData[3] = value;
        }

        /// <summary>
        /// Gets or sets the unique identifier for a fragmented message.
        /// Located at offset 4 in <see cref="RawData"/>.
        /// </summary>
        public ushort FragmentId
        {
            get => BitConverter.ToUInt16(RawData, 4);
            set => FastBitConverter.GetBytes(RawData, 4, value);
        }

        /// <summary>
        /// Gets or sets the index of the current fragment part.
        /// Located at offset 6 in <see cref="RawData"/>.
        /// </summary>
        public ushort FragmentPart
        {
            get => BitConverter.ToUInt16(RawData, 6);
            set => FastBitConverter.GetBytes(RawData, 6, value);
        }

        /// <summary>
        /// Gets or sets the total number of fragments in the message.
        /// Located at offset 8 in <see cref="RawData"/>.
        /// </summary>
        public ushort FragmentsTotal
        {
            get => BitConverter.ToUInt16(RawData, 8);
            set => FastBitConverter.GetBytes(RawData, 8, value);
        }

        /// <summary>
        /// The raw <see cref="byte"/> array containing the packet header and payload.
        /// </summary>
        public byte[] RawData;

        /// <summary>
        /// The actual size of the data in <see cref="RawData"/>, including headers.
        /// </summary>
        public int Size;

        /// <summary>
        /// Custom user data associated with this packet. Used for delivery notifications.
        /// </summary>
        public object UserData;

        /// <summary>
        /// Reference to the next packet in the NetPacketPool.
        /// </summary>
        public NetPacket Next;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetPacket"/> class with a specific buffer size.
        /// </summary>
        /// <param name="size">Total size of the packet including headers.</param>
        public NetPacket(int size)
        {
            RawData = new byte[size];
            Size = size;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetPacket"/> class, calculating the required size based on the property.
        /// </summary>
        /// <param name="property">The type of packet to create.</param>
        /// <param name="payloadSize">Size of the user data payload.</param>
        public NetPacket(PacketProperty property, int payloadSize)
        {
            int totalSize = payloadSize + GetHeaderSize(property);
            RawData = new byte[totalSize];
            Property = property;
            Size = totalSize;
        }

        /// <summary>
        /// Gets the fixed header size for a specific <see cref="PacketProperty"/>.
        /// </summary>
        /// <param name="property">The packet type.</param>
        /// <returns>Header size in bytes.</returns>
        public static int GetHeaderSize(PacketProperty property) => HeaderSizes[(int)property];

        /// <summary>
        /// Gets the header size of the current packet based on its property bits.
        /// </summary>
        public int HeaderSize => HeaderSizes[RawData[0] & 0x1F];

        /// <summary>
        /// Performs a basic check on the packet header and size.
        /// </summary>
        /// <returns><see langword="true"/> if the packet property is valid and the size is sufficient for the headers.</returns>
        public bool Verify()
        {
            byte property = (byte)(RawData[0] & 0x1F);
            if (property >= PropertiesCount)
                return false;
            int headerSize = HeaderSizes[property];
            bool fragmented = (RawData[0] & 0x80) != 0;
            return Size >= headerSize && (!fragmented || Size >= headerSize + NetConstants.FragmentHeaderSize);
        }
    }
}

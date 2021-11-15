using System;
using System.Net;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal enum PacketProperty : byte
    {
        Unreliable,
        Channeled,
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
        Empty
    }

    internal sealed class NetPacket
    {
        private static readonly int PropertiesCount = Enum.GetValues(typeof(PacketProperty)).Length;
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

        //Header
        public PacketProperty Property
        {
            get => (PacketProperty)(RawData[0] & 0x1F);
            set => RawData[0] = (byte)((RawData[0] & 0xE0) | (byte)value);
        }

        public byte ConnectionNumber
        {
            get => (byte)((RawData[0] & 0x60) >> 5);
            set => RawData[0] = (byte) ((RawData[0] & 0x9F) | (value << 5));
        }

        public ushort Sequence
        {
            get => BitConverter.ToUInt16(RawData, 1);
            set => FastBitConverter.GetBytes(RawData, 1, value);
        }

        public bool IsFragmented => (RawData[0] & 0x80) != 0;

        public void MarkFragmented()
        {
            RawData[0] |= 0x80; //set first bit
        }

        public byte ChannelId
        {
            get => RawData[3];
            set => RawData[3] = value;
        }

        public ushort FragmentId
        {
            get => BitConverter.ToUInt16(RawData, 4);
            set => FastBitConverter.GetBytes(RawData, 4, value);
        }

        public ushort FragmentPart
        {
            get => BitConverter.ToUInt16(RawData, 6);
            set => FastBitConverter.GetBytes(RawData, 6, value);
        }

        public ushort FragmentsTotal
        {
            get => BitConverter.ToUInt16(RawData, 8);
            set => FastBitConverter.GetBytes(RawData, 8, value);
        }

        //Data
        public byte[] RawData;
        public int Size;

        //Delivery
        public object UserData;

        //Pool node
        public NetPacket Next;

        public NetPacket(int size)
        {
            RawData = new byte[size];
            Size = size;
        }

        public NetPacket(PacketProperty property, int size)
        {
            size += GetHeaderSize(property);
            RawData = new byte[size];
            Property = property;
            Size = size;
        }

        public static int GetHeaderSize(PacketProperty property)
        {
            return HeaderSizes[(int)property];
        }

        public int GetHeaderSize()
        {
            return HeaderSizes[RawData[0] & 0x1F];
        }

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

    internal sealed class NetConnectRequestPacket
    {
        public const int HeaderSize = 18;
        public readonly long ConnectionTime;
        public byte ConnectionNumber;
        public readonly byte[] TargetAddress;
        public readonly NetDataReader Data;
        public readonly int PeerId;

        private NetConnectRequestPacket(long connectionTime, byte connectionNumber, int localId, byte[] targetAddress, NetDataReader data)
        {
            ConnectionTime = connectionTime;
            ConnectionNumber = connectionNumber;
            TargetAddress = targetAddress;
            Data = data;
            PeerId = localId;
        }

        public static int GetProtocolId(NetPacket packet)
        {
            return BitConverter.ToInt32(packet.RawData, 1);
        }

        public static NetConnectRequestPacket FromData(NetPacket packet)
        {
            if (packet.ConnectionNumber >= NetConstants.MaxConnectionNumber)
                return null;

            //Getting connection time for peer
            long connectionTime = BitConverter.ToInt64(packet.RawData, 5);

            //Get peer id
            int peerId = BitConverter.ToInt32(packet.RawData, 13);

            //Get target address
            int addrSize = packet.RawData[HeaderSize-1];
            if (addrSize != 16 && addrSize != 28)
                return null;
            byte[] addressBytes = new byte[addrSize];
            Buffer.BlockCopy(packet.RawData, HeaderSize, addressBytes, 0, addrSize);

            // Read data and create request
            var reader = new NetDataReader(null, 0, 0);
            if (packet.Size > HeaderSize+addrSize)
                reader.SetSource(packet.RawData, HeaderSize + addrSize, packet.Size);

            return new NetConnectRequestPacket(connectionTime, packet.ConnectionNumber, peerId, addressBytes, reader);
        }

        public static NetPacket Make(NetDataWriter connectData, SocketAddress addressBytes, long connectTime, int localId)
        {
            //Make initial packet
            var packet = new NetPacket(PacketProperty.ConnectRequest, connectData.Length+addressBytes.Size);

            //Add data
            FastBitConverter.GetBytes(packet.RawData, 1, NetConstants.ProtocolId);
            FastBitConverter.GetBytes(packet.RawData, 5, connectTime);
            FastBitConverter.GetBytes(packet.RawData, 13, localId);
            packet.RawData[HeaderSize-1] = (byte)addressBytes.Size;
            for (int i = 0; i < addressBytes.Size; i++)
                packet.RawData[HeaderSize + i] = addressBytes[i];
            Buffer.BlockCopy(connectData.Data, 0, packet.RawData, HeaderSize + addressBytes.Size, connectData.Length);
            return packet;
        }
    }

    internal sealed class NetConnectAcceptPacket
    {
        public const int Size = 15;
        public readonly long ConnectionTime;
        public readonly byte ConnectionNumber;
        public readonly int PeerId;

        private NetConnectAcceptPacket(long connectionTime, byte connectionNumber, int peerId)
        {
            ConnectionTime = connectionTime;
            ConnectionNumber = connectionNumber;
            PeerId = peerId;
        }

        public static NetConnectAcceptPacket FromData(NetPacket packet)
        {
            if (packet.Size != Size)
                return null;

            long connectionId = BitConverter.ToInt64(packet.RawData, 1);

            //check connect num
            byte connectionNumber = packet.RawData[9];
            if (connectionNumber >= NetConstants.MaxConnectionNumber)
                return null;

            //check reused flag
            byte isReused = packet.RawData[10];
            if (isReused > 1)
                return null;

            //get remote peer id
            int peerId = BitConverter.ToInt32(packet.RawData, 11);

            return new NetConnectAcceptPacket(connectionId, connectionNumber, peerId);
        }

        public static NetPacket Make(long connectTime, byte connectNum, bool reusedPeer, int localPeerId)
        {
            var packet = new NetPacket(PacketProperty.ConnectAccept, 0);
            FastBitConverter.GetBytes(packet.RawData, 1, connectTime);
            packet.RawData[9] = connectNum;
            packet.RawData[10] = (byte)(reusedPeer ? 1 : 0);
            FastBitConverter.GetBytes(packet.RawData, 11, localPeerId);
            return packet;
        }
    }
}

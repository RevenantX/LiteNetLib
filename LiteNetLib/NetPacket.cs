using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal enum PacketProperty : byte
    {
        Unreliable,             //0
        ReliableUnordered,      //1
        Sequenced,              //2
        ReliableOrdered,        //3
        AckReliable,            //4
        AckReliableOrdered,     //5
        Ping,                   //6 *
        Pong,                   //7 *
        ConnectRequest,         //8 *
        ConnectAccept,          //9 *
        Disconnect,             //10 *
        UnconnectedMessage,     //11
        NatIntroductionRequest, //12 *
        NatIntroduction,        //13 *
        NatPunchMessage,        //14 *
        MtuCheck,               //15 *
        MtuOk,                  //16 *
        DiscoveryRequest,       //17 *
        DiscoveryResponse,      //18 *
        Merged,                 //19
        ShutdownOk,             //20 *   
        ReliableSequenced,      //21
        AckReliableSequenced    //22
    }

    internal sealed class NetPacket
    {
        private const int LastProperty = 22;

        //Header
        public PacketProperty Property
        {
            get { return (PacketProperty)(RawData[0] & 0x1F); }
            set { RawData[0] = (byte)((RawData[0] & 0xE0) | (byte)value); }
        }

        public byte ConnectionNumber
        {
            get { return (byte)((RawData[0] & 0x60) >> 5); }
            set { RawData[0] = (byte) ((RawData[0] & 0x9F) | (value << 5)); }
        }

        public ushort Sequence
        {
            get { return BitConverter.ToUInt16(RawData, 1); }
            set { FastBitConverter.GetBytes(RawData, 1, value); }
        }

        public bool IsFragmented
        {
            get { return (RawData[0] & 0x80) != 0; }
        }

        public void MarkFragmented()
        {
            RawData[0] |= 0x80; //set first bit
        }

        public ushort FragmentId
        {
            get { return BitConverter.ToUInt16(RawData, 3); }
            set { FastBitConverter.GetBytes(RawData, 3, value); }
        }

        public ushort FragmentPart
        {
            get { return BitConverter.ToUInt16(RawData, 5); }
            set { FastBitConverter.GetBytes(RawData, 5, value); }
        }

        public ushort FragmentsTotal
        {
            get { return BitConverter.ToUInt16(RawData, 7); }
            set { FastBitConverter.GetBytes(RawData, 7, value); }
        }

        //Data
        public byte[] RawData;
        public int Size;

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

        public void Realloc(int toSize, bool clear)
        {
            Size = toSize;
            if (RawData.Length < toSize)
            {
                RawData = new byte[toSize];
                return;
            }
            if (clear)  //clear not reallocated
                Array.Clear(RawData, 0, toSize);
        }

        public static int GetHeaderSize(PacketProperty property)
        {
            switch (property)
            {
                case PacketProperty.ReliableOrdered:
                case PacketProperty.ReliableUnordered:
                case PacketProperty.ReliableSequenced:
                case PacketProperty.Sequenced:
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.AckReliable:
                case PacketProperty.AckReliableOrdered:
                case PacketProperty.AckReliableSequenced:
                    return NetConstants.SequencedHeaderSize;
                case PacketProperty.ConnectRequest:
                    return NetConnectRequestPacket.HeaderSize;
                case PacketProperty.ConnectAccept:
                    return NetConnectAcceptPacket.Size;
                case PacketProperty.Disconnect:
                    return NetConstants.HeaderSize + 8;
                default:
                    return NetConstants.HeaderSize;
            }
        }

        public int GetHeaderSize()
        {
            return GetHeaderSize(Property);
        }

        //Packet contstructor from byte array
        public bool FromBytes(byte[] data, int start, int packetSize)
        {
            //Reading property
            byte property = (byte)(data[start] & 0x1F);
            bool fragmented = (data[start] & 0x80) != 0;
            int headerSize = GetHeaderSize((PacketProperty) property);

            if (property > LastProperty || packetSize < headerSize ||
               (fragmented && packetSize < headerSize + NetConstants.FragmentHeaderSize))
            {
                return false;
            }

            Buffer.BlockCopy(data, start, RawData, 0, packetSize);
            Size = packetSize;
            return true;
        }
    }

    internal sealed class NetConnectRequestPacket
    {
        public const int HeaderSize = 13;
        public readonly long ConnectionId;
        public readonly byte ConnectionNumber;
        public readonly NetDataReader Data;

        private NetConnectRequestPacket(long connectionId, byte connectionNumber, NetDataReader data)
        {
            ConnectionId = connectionId;
            ConnectionNumber = connectionNumber;
            Data = data;
        }
        
        public static NetConnectRequestPacket FromData(NetPacket packet)
        {
            if (packet.ConnectionNumber >= NetConstants.MaxConnectionNumber)
                return null;

            int protoId = BitConverter.ToInt32(packet.RawData, 1);
            if (protoId != NetConstants.ProtocolId)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan,
                    "[NM] Peer connect reject. Invalid protocol ID: " + protoId);
                return null;
            }

            //Getting new id for peer
            long connectionId = BitConverter.ToInt64(packet.RawData, 5);

            // Read data and create request
            var reader = new NetDataReader(null, 0, 0);
            if (packet.Size > HeaderSize)
                reader.SetSource(packet.RawData, HeaderSize, packet.Size);

            return new NetConnectRequestPacket(connectionId, packet.ConnectionNumber, reader);
        }

        public static NetPacket Make(NetDataWriter connectData, long connectId)
        {
            //Make initial packet
            var packet = new NetPacket(PacketProperty.ConnectRequest, connectData.Length);

            //Add data
            FastBitConverter.GetBytes(packet.RawData, 1, NetConstants.ProtocolId);
            FastBitConverter.GetBytes(packet.RawData, 5, connectId);
            Buffer.BlockCopy(connectData.Data, 0, packet.RawData, HeaderSize, connectData.Length);
            return packet;
        }
    }

    internal sealed class NetConnectAcceptPacket
    {
        public const int Size = 11;
        public readonly long ConnectionId;
        public readonly byte ConnectionNumber;
        public readonly bool IsReusedPeer;

        private NetConnectAcceptPacket(long connectionId, byte connectionNumber, bool isReusedPeer)
        {
            ConnectionId = connectionId;
            ConnectionNumber = connectionNumber;
            IsReusedPeer = isReusedPeer;
        }

        public static NetConnectAcceptPacket FromData(NetPacket packet)
        {
            if (packet.Size > Size)
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

            return new NetConnectAcceptPacket(connectionId, connectionNumber, isReused == 1);
        }

        public static NetPacket Make(long connectId, byte connectNum, bool reusedPeer)
        {
            var packet = new NetPacket(PacketProperty.ConnectAccept, 0);
            FastBitConverter.GetBytes(packet.RawData, 1, connectId);
            packet.RawData[9] = connectNum;
            packet.RawData[10] = (byte)(reusedPeer ? 1 : 0);
            return packet;
        }
    }
}

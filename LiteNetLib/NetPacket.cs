using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    // 0 1 2 3 4 5 6 7
    // f ccc ppppppppp
    //
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
        ReliableSequenced       //21
    }

    internal sealed class NetPacket
    {
        private const int LastProperty = 21;

        //Header
        public PacketProperty Property
        {
            get { return (PacketProperty)(RawData[0] & 0x1F); }
            set { RawData[0] = (byte)((RawData[0] & 0xE0) | (byte)value); }
        }

        // Fragmented
        // 0x80 - 1000 0000
        
        // Property
        // 0x1F - 0001 1111
        // 0xE0 - 1110 0000
        
        // Connection number
        // 0x60 - 0110 0000
        // 0x9F - 1001 1111

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

        public bool Realloc(int toSize)
        {
            if (RawData.Length < toSize)
            {
                RawData = new byte[toSize];
                return true;
            }
            return false;
        }

        public static int GetHeaderSize(PacketProperty property)
        {
            switch (property)
            {
                case PacketProperty.ReliableOrdered:
                case PacketProperty.ReliableUnordered:
                case PacketProperty.Sequenced:
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.AckReliable:
                case PacketProperty.AckReliableOrdered:
                case PacketProperty.ReliableSequenced:
                    return NetConstants.SequencedHeaderSize;
                default:
                    return NetConstants.HeaderSize;
            }
        }

        public int GetHeaderSize()
        {
            return GetHeaderSize(Property);
        }

        public byte[] CopyPacketData()
        {
            int headerSize = GetHeaderSize(Property);
            int dataSize = Size - headerSize;
            byte[] data = new byte[dataSize];
            Buffer.BlockCopy(RawData, headerSize, data, 0, dataSize);
            return data;
        }

        //Packet contstructor from byte array
        public bool FromBytes(byte[] data, int start, int packetSize)
        {
            //Reading property
            byte property = (byte)(data[start] & 0x7F);
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

    internal class NetConnectRequestPacket
    {
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
            if (packet.Size < 12)
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
            if (packet.Size > 12)
                reader.SetSource(packet.RawData, 13, packet.Size);

            return new NetConnectRequestPacket(connectionId, packet.ConnectionNumber, reader);
        }

        public static NetPacket Make(NetDataWriter connectData, long connectId)
        {
            //Make initial packet
            var packet = new NetPacket(PacketProperty.ConnectRequest, 12 + connectData.Length);

            //Add data
            FastBitConverter.GetBytes(packet.RawData, 1, NetConstants.ProtocolId);
            FastBitConverter.GetBytes(packet.RawData, 5, connectId);
            Buffer.BlockCopy(connectData.Data, 0, packet.RawData, 13, connectData.Length);
            return packet;
        }
    }

    internal class NetConnectAcceptPacket
    {

    }
}

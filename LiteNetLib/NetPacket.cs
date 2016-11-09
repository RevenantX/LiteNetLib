using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal enum PacketProperty : byte
    {
        Unreliable,             //0
        Reliable,               //1
        Sequenced,              //2
        ReliableOrdered,        //3
        AckReliable,            //4
        AckReliableOrdered,     //5
        Ping,                   //6
        Pong,                   //7
        ConnectRequest,         //8
        ConnectAccept,          //9
        Disconnect,             //10
        UnconnectedMessage,     //11
        NatIntroductionRequest, //12
        NatIntroduction,        //13
        NatPunchMessage,        //14
        MtuCheck,               //15
        MtuOk,                  //16
        DiscoveryRequest,       //17
        DiscoveryResponse,      //18
        Merged                  //19
    }

    internal sealed class NetPacket
    {
        private const int LastProperty = 19;

        //Header
        public PacketProperty Property
        {
            get { return (PacketProperty)(RawData[0] & 0x7F); }
            set { RawData[0] = (byte)((RawData[0] & 0x80) | ((byte)value & 0x7F)); }
        }

        public ushort Sequence
        {
            get { return BitConverter.ToUInt16(RawData, 1); }
            set { FastBitConverter.GetBytes(RawData, 1, value); }
        }

        public bool IsFragmented
        {
            get { return (RawData[0] & 0x80) != 0; }
            set
            {
                if (value)
                    RawData[0] |= 0x80; //set first bit
                else
                    RawData[0] &= 0x7F; //unset first bit
            }
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

        //Packet constructor
        public void Init(PacketProperty property, int dataSize)
        {
            RawData = new byte[GetHeaderSize(property) + dataSize];
            Property = property;
        }

        //Always not fragmented
        public static byte[] CreateRawPacket(PacketProperty property, int dataSize)
        {
            byte[] rawData = new byte[GetHeaderSize(property) + dataSize];
            rawData[0] = (byte) property;
            return rawData;
        }

        public static byte[] CreateRawPacket(PacketProperty property, byte[] data, int start, int count)
        {
            int headerSize = GetHeaderSize(property);
            byte[] rawData = new byte[headerSize + count];
            rawData[0] = (byte)property;
            Buffer.BlockCopy(data, start, rawData, headerSize, count);
            return rawData;
        }

        public static byte[] CreateRawPacket(PacketProperty property, NetDataWriter dataWriter)
        {
            int headerSize = GetHeaderSize(property);
            byte[] rawData = new byte[headerSize + dataWriter.Length];
            rawData[0] = (byte)property;
            Buffer.BlockCopy(dataWriter.Data, 0, rawData, headerSize, dataWriter.Length);
            return rawData;
        }

        public void Init(PacketProperty property, NetDataWriter dataWriter)
        {
            int headerSize = GetHeaderSize(property);
            RawData = new byte[headerSize + dataWriter.Length];
            Property = property;
            Buffer.BlockCopy(dataWriter.Data, 0, RawData, headerSize, dataWriter.Length);
        }

        public void PutData(byte[] data, int start, int length)
        {
            int packetStart = GetHeaderSize(Property) + (IsFragmented ? NetConstants.FragmentHeaderSize : 0);
            Buffer.BlockCopy(data, start, RawData, packetStart, length);
        }

        public static bool GetPacketProperty(byte[] data, out PacketProperty property)
        {
            byte properyByte = (byte)(data[0] & 0x7F);
            if (properyByte > LastProperty)
            {
                property = PacketProperty.Unreliable;
                return false;
            }
            property = (PacketProperty)properyByte;
            return true;
        }

        public static bool ComparePacketProperty(byte[] data, PacketProperty check)
        {
            PacketProperty property;
            if (GetPacketProperty(data, out property))
            {
                return property == check;
            }
            return false;
        }

        public static int GetHeaderSize(PacketProperty property)
        {
            return IsSequenced(property)
                ? NetConstants.SequencedHeaderSize
                : NetConstants.HeaderSize;
        }

        public int GetHeaderSize()
        {
            return GetHeaderSize(Property);
        }

        public byte[] GetPacketData()
        {
            int headerSize = GetHeaderSize(Property);
            int dataSize = RawData.Length - headerSize;
            byte[] data = new byte[dataSize];
            Buffer.BlockCopy(RawData, headerSize, data, 0, dataSize);
            return data;
        }

        public bool IsClientData()
        {
            var property = Property;
            return property == PacketProperty.Reliable ||
                   property == PacketProperty.ReliableOrdered ||
                   property == PacketProperty.Unreliable ||
                   property == PacketProperty.Sequenced;
        }

        public static bool IsSequenced(PacketProperty property)
        {
            return property == PacketProperty.ReliableOrdered ||
                property == PacketProperty.Reliable ||
                property == PacketProperty.Sequenced ||
                property == PacketProperty.Ping ||
                property == PacketProperty.Pong ||
                property == PacketProperty.AckReliable ||
                property == PacketProperty.AckReliableOrdered;
        }

        public static byte[] GetUnconnectedData(byte[] raw, int count)
        {
            int size = count - NetConstants.HeaderSize;
            byte[] data = new byte[size];
            Buffer.BlockCopy(raw, 1, data, 0, size);
            return data;
        }

        //Packet contstructor from byte array
        public bool FromBytes(byte[] data, int start, int packetSize)
        {
            //Reading property
            if ((data[0] & 0x7F) > LastProperty || packetSize > NetConstants.PacketSizeLimit)
                return false;
            RawData = new byte[packetSize];
            Buffer.BlockCopy(data, start, RawData, 0, packetSize);
     
            return true;
        }
    }
}

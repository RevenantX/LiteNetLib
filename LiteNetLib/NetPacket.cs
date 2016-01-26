using System;

namespace LiteNetLib
{
    enum PacketProperty : byte
    {
        None,
        Reliable,
        Sequenced,
        ReliableOrdered,
        AckReliable,
        AckReliableOrdered,
        Ping,
        Pong,
        Connect,
        Disconnect,
        UnconnectedMessage
    }

    sealed class NetPacket
    {
        const int PropertiesCount = 11;
        //Header
        public PacketProperty Property //1 1
        {
            get { return (PacketProperty)RawData[0]; }
            set { RawData[0] = (byte)value; }
        }

        public ushort Sequence //2 3
        {
            get { return BitConverter.ToUInt16(RawData, 1); }
            set { FastBitConverter.GetBytes(RawData, 1, value); }
        }

        //Data
        public byte[] RawData;

        //Additional info!
        public long TimeStamp;

        //Packet constructor
        public void Init(PacketProperty property, int dataSize)
        {
            RawData = new byte[GetHeaderSize(property) + dataSize];
            Property = property;
        }

        public void PutData(byte[] data)
        {
            Buffer.BlockCopy(data, 0, RawData, GetHeaderSize(Property), data.Length);
        }

        public static bool GetPacketProperty(byte[] data, out PacketProperty property)
        {
            byte properyByte = data[0];
            if (properyByte >= PropertiesCount)
            {
                property = PacketProperty.None;
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

        static int GetHeaderSize(PacketProperty property)
        {
            return IsSequenced(property)
                ? NetConstants.SequencedHeaderSize
                : NetConstants.HeaderSize;
        }

        public byte[] GetPacketData()
        {
            int headerSize = GetHeaderSize(Property);
            int dataSize = RawData.Length - headerSize;
            byte[] data = new byte[dataSize];
            Buffer.BlockCopy(RawData, headerSize, data, 0, dataSize);
            return data;
        }

        public static bool IsSequenced(PacketProperty property)
        {
            return property != PacketProperty.Connect &&
                   property != PacketProperty.Disconnect &&
                   property != PacketProperty.None &&
                   property != PacketProperty.UnconnectedMessage;
        }

        public static byte[] GetUnconnectedData(byte[] raw, int count)
        {
            int size = count - NetConstants.HeaderSize;
            byte[] data = new byte[size];
            Buffer.BlockCopy(raw, 1, data, 0, size);
            return data;
        }

        //Packet contstructor from byte array
        public bool FromBytes(byte[] data, int packetSize)
        {
            //Reading property
            if (data[0] >= PropertiesCount || packetSize > NetConstants.MaxPacketSize)
                return false;
            RawData = new byte[packetSize];
            Buffer.BlockCopy(data, 0, RawData, 0, packetSize);
     
            return true;
        }
    }
}

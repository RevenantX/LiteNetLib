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
        Merged,                 //19
        ShutdownOk,             //20     
        ReliableSequenced       //21
    }

    internal sealed class NetPacket
    {
        private const int LastProperty = 21;

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
        public int Size;

        public NetPacket(int size)
        {
            RawData = new byte[size];
            Size = 0;
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

            if (property > LastProperty ||
                packetSize > NetConstants.PacketSizeLimit ||
                packetSize < headerSize ||
                fragmented && packetSize < headerSize + NetConstants.FragmentHeaderSize)
            {
                return false;
            }

            Buffer.BlockCopy(data, start, RawData, 0, packetSize);
            Size = packetSize;
            return true;
        }
    }
}

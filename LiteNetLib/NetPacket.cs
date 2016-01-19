using System;

namespace LiteNetLib
{
    public enum PacketProperty : byte
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
        Disconnect
    }

    public class NetPacket
    {
        //Header
        public PacketProperty Property; //1 1
        public ushort Sequence;         //2 3

        //Data
        public byte[] Data;

        //Additional info!
        public long TimeStamp;

        //Packet constructor
        public NetPacket()
        {
            
        }

        public static bool IsSequenced(PacketProperty property)
        {
            return property == PacketProperty.Reliable || 
                   property == PacketProperty.Sequenced ||
                   property == PacketProperty.ReliableOrdered ||
                   property == PacketProperty.Ping || 
                   property == PacketProperty.Pong;
        }

        //Packet contstructor from byte array
        public bool FromBytes(byte[] data, int packetSize)
        {
            //Reading property
            if (data[0] > 9)
                return false;
            Property = (PacketProperty)data[0];

            //init datasize
            int dataLenght = packetSize;
            int dataStart;

            //Sequence
            if (IsSequenced(Property))
            {
                Sequence = BitConverter.ToUInt16(data, 1);
                dataLenght -= NetConstants.SequencedHeaderSize;
                dataStart = NetConstants.SequencedHeaderSize;
            }
            else
            {
                dataLenght -= NetConstants.HeaderSize;
                dataStart = NetConstants.HeaderSize;
            }

            //Reading other data
            Data = new byte[dataLenght];
            Buffer.BlockCopy(data, dataStart, Data, 0, dataLenght);
            return true;
        }

        //Converting to byte array for sending
        public byte[] ToByteArray()
        {
            byte[] buffer;
            int dataSize = 0;
            int headerSize = NetConstants.SequencedHeaderSize;
            if (Data != null)
                dataSize = Data.Length;

            //write property   
            if (IsSequenced(Property))
            {
                buffer = new byte[NetConstants.SequencedHeaderSize + dataSize];
                FastBitConverter.GetBytes(buffer, 1, Sequence);
            }
            else
            {
                headerSize = NetConstants.HeaderSize;
                buffer = new byte[NetConstants.HeaderSize + dataSize];
            }
            buffer[0] = (byte)Property;

            //Writing data
            if (dataSize > 0)
            {
                Buffer.BlockCopy(Data, 0, buffer, headerSize, dataSize);
            }

            return buffer;
        }
    }
}

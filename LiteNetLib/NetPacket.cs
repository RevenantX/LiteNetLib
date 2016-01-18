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
        public PacketProperty property; //1 1
        public ushort sequence;         //2 3

        //Data
        public byte[] data;

        //Additional info!
        public int timeStamp;

        //Packet constructor
        public NetPacket()
        {
            
        }

        public static bool IsSequenced(PacketProperty property)
        {
            return property == PacketProperty.Reliable || 
                   property == PacketProperty.Sequenced ||
                   property == PacketProperty.ReliableOrdered;
        }

        //Packet contstructor from byte array
        public static NetPacket CreateFromBytes(byte[] data, int packetSize)
        {
            NetPacket p = new NetPacket();

            //Reading property
            if (data[0] > 9)
                return null;
            p.property = (PacketProperty)data[0];

            //init datasize
            int dataLenght = packetSize;
            int dataStart;

            //Sequence
            if (IsSequenced(p.property))
            {
                p.sequence = BitConverter.ToUInt16(data, 2);
                dataLenght -= NetConstants.SequencedHeaderSize;
                dataStart = NetConstants.SequencedHeaderSize;
            }
            else
            {
                dataLenght -= NetConstants.HeaderSize;
                dataStart = NetConstants.HeaderSize;
            }

            //Reading other data
            p.data = new byte[dataLenght];
            Buffer.BlockCopy(data, dataStart, p.data, 0, dataLenght);

            return p;
        }

        //Converting to byte array for sending
        public byte[] ToByteArray()
        {
            byte[] buffer;
            int dataSize = 0;
            int headerSize = NetConstants.SequencedHeaderSize;
            if (data != null)
                dataSize = data.Length;

            //write property   
            if (IsSequenced(property))
            {
                buffer = new byte[NetConstants.SequencedHeaderSize + dataSize];
#if BIGENDIAN
                buffer[1] = (byte)(sequence);
                buffer[2] = (byte)(sequence >> 8);
#else
                buffer[1] = (byte)(sequence);
                buffer[2] = (byte)(sequence >> 8);
#endif
            }
            else
            {
                headerSize = NetConstants.HeaderSize;
                buffer = new byte[NetConstants.HeaderSize + dataSize];
            }
            buffer[0] = (byte)property;

            //Writing data
            if (dataSize > 0)
            {
                Buffer.BlockCopy(data, 0, buffer, headerSize, dataSize);
            }

            return buffer;
        }
    }
}

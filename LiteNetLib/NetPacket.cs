using System;

namespace LiteNetLib
{
    //Packet types
    public enum PacketProperty : byte
    {
        None = 33,
        Reliable = 34,
        InOrder = 35,
        ReliableInOrder = 36,
        Ack = 37,
        Ping = 38,
        Pong = 39
    }

    public enum PacketInfo : byte
    {
        None = 54,
        Connect = 55,
        Disconnect = 56,
    }

    public class NetPacket
    {
        //Header
        public PacketProperty property; //1 1
        public PacketInfo info;         //1 2
        public ushort sequence;         //2 4

        //Data
        public byte[] data;

        //Additional info!
        public int timeStamp;

        //Packet constructor
        public NetPacket()
        {
            info = PacketInfo.None;
        }

        //Packet contstructor from byte array
        public static NetPacket CreateFromBytes(byte[] data, int packetSize)
        {
            NetPacket p = new NetPacket();

            //Reading property
            if (data[0] < 33 || data[0] > 39)
                return null;
            p.property = (PacketProperty)data[0];

            //Reading info
            if (data[1] < 54 || data[1] > 56)
                return null;
            p.info = (PacketInfo)data[1];

            //Sequence
            p.sequence = BitConverter.ToUInt16(data, 2);

            //Reading other data
            int dataLenght = packetSize - NetConstants.HeaderSize;
            p.data = new byte[dataLenght];
            Buffer.BlockCopy(data, NetConstants.HeaderSize, p.data, 0, dataLenght);

            return p;
        }

        //Converting to byte array for sending
        public byte[] ToByteArray()
        {
            byte[] buffer;
                
            //Writing data first
            if(data != null)
            {
                buffer = new byte[NetConstants.HeaderSize + data.Length];
                Buffer.BlockCopy(data, 0, buffer, NetConstants.HeaderSize, data.Length);
            }
            else
            {
                buffer = new byte[NetConstants.HeaderSize];
            }

            buffer[0] = (byte)property;
            buffer[1] = (byte)info;
            #if BIGENDIAN
            buffer[2] = (byte)(sequence);
            buffer[3] = (byte)(sequence >> 8);
            #else
            buffer[2] = (byte)(sequence);
            buffer[3] = (byte)(sequence >> 8);
            #endif

            return buffer;
        }
    }
}

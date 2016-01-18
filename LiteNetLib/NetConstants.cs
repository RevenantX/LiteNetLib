using System.Net;

namespace LiteNetLib
{
    public enum SendOptions
    {
        None,
        Reliable,
        InOrder,
        ReliableInOrder
    }

    public static class NetConstants
    {
        public const int HeaderSize = 1;
        public const int SequencedHeaderSize = 3;
        public const int DefaultWindowSize = 64;
        public const ushort MaxSequence = 65535;
        public const ushort HalfMaxSequence = MaxSequence / 2;
        public const int MaxPacketSize = 1432;
        public const int MaxPacketDataSize = MaxPacketSize - HeaderSize;

        public static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + MaxSequence + HalfMaxSequence) % MaxSequence - HalfMaxSequence;
        }

        public static bool SequenceMoreRecent(uint s1, uint s2)
        {
            return (s1 > s2) && (s1 - s2 <= HalfMaxSequence) ||
                   (s2 > s1) && (s2 - s1 > HalfMaxSequence);
        }

        public static long GetIdFromEndPoint(IPEndPoint ep)
        {
            long id = 0;
            byte[] addr = ep.Address.GetAddressBytes();
            id |= (long)addr[0];
            id |= (long)addr[1] << 8;
            id |= (long)addr[2] << 16;
            id |= (long)addr[3] << 24;
            id |= (long)ep.Port << 32;
            return id;
        }
    }
}

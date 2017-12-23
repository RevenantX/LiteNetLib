namespace LiteNetLib
{
    public sealed class NetStatistics
    {
        public ulong PacketsSent;
        public ulong PacketsReceived;
        public ulong BytesSent;
        public ulong BytesReceived;
        public ulong PacketLoss;
        public ulong PacketLossPercent
        {
            get
            {
                if (PacketsSent == 0)
                {
                    return 0;
                }

                return PacketLoss * 100 / PacketsSent;
            }
        }

        public override string ToString()
        {
            return
                string.Format(
                    "BytesReceived: {0}\nPacketsReceived: {1}\nBytesSent: {2}\nPacketsSent: {3}\nPacketLoss: {4}\nPacketLossPercent: {5}\n",
                    BytesReceived,
                    PacketsReceived,
                    BytesSent,
                    PacketsSent,
                    PacketLoss,
                    PacketLossPercent);
        }
    }
}

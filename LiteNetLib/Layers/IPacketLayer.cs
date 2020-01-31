using System;

namespace LiteNetLib.Layers
{
    /// <summary>
    /// Extra processing of packets, like CRC-checksums or encryption
    /// </summary>
    public interface IPacketLayer
    {
        int ExtraPacketSizeForLayer { get; }
        void ProcessInboundPacket(ref byte[] data, ref int length);
        void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length);
    }
}

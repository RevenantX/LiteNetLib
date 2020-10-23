using System.Net;

namespace LiteNetLib.Layers
{
    public interface IPacketLayer
    {
        int ExtraPacketSize { get; }

        void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length);
        void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length);
    }
}

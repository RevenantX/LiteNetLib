namespace LiteNetLib.Layers
{
    public abstract class PacketLayerBase
    {
        public readonly int ExtraPacketSizeForLayer;

        protected PacketLayerBase(int extraPacketSizeForLayer)
        {
            ExtraPacketSizeForLayer = extraPacketSizeForLayer;
        }

        public abstract void ProcessInboundPacket(ref byte[] data, ref int length);
        public abstract void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length);
    }
}

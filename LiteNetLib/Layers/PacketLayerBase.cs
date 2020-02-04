namespace LiteNetLib.Layers
{
    public abstract class PacketLayerBase
    {
        protected PacketLayerBase(int extraPacketSizeForLayer)
        {
            ExtraPacketSizeForLayer = extraPacketSizeForLayer;
        }

        public readonly int ExtraPacketSizeForLayer;
        public abstract void ProcessInboundPacket(ref byte[] data, ref int length);
        public abstract void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length);
    }
}

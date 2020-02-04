using LiteNetLib.Utils;
using System;

namespace LiteNetLib.Layers
{
    public sealed class Crc32cLayer : PacketLayerBase
    {
        public Crc32cLayer() : base(CRC32C.ChecksumSize)
        {

        }

        public override void ProcessInboundPacket(ref byte[] data, ref int length)
        {
            if (length < NetConstants.HeaderSize + CRC32C.ChecksumSize)
            {
                NetDebug.WriteError("[NM] DataReceived size: bad!");
                return;
            }

            int checksumPoint = length - CRC32C.ChecksumSize;
            if (CRC32C.Compute(data, 0, checksumPoint) != BitConverter.ToUInt32(data, checksumPoint))
            {
                NetDebug.Write("[NM] DataReceived checksum: bad!");
                return;
            }
            length -= CRC32C.ChecksumSize;
        }

        public override void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length)
        {
            FastBitConverter.GetBytes(data, length, CRC32C.Compute(data, offset, length));
            length += CRC32C.ChecksumSize;
        }
    }
}

using System;
using System.Net;
using System.Text;

namespace LiteNetLib.Layers
{
    public struct XorEncryptLayer : IPacketLayer
    {
        private readonly byte[] _byteKey;

        public XorEncryptLayer(byte[] key)
        {
            _byteKey = new byte[key.Length];
            Buffer.BlockCopy(key, 0, _byteKey, 0, key.Length);
        }

        public XorEncryptLayer(string key)
        {
            _byteKey = Encoding.UTF8.GetBytes(key);
        }
        
        public int ExtraPacketSize
        {
            get
            {
                return 0;
            }
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            if (_byteKey == null)
                return;

            var cur = offset;
            for (var i = 0; i < length; i++, cur++)
            {
                data[cur] = (byte)(data[cur] ^ _byteKey[i % _byteKey.Length]);
            }
        }

        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            if (_byteKey == null)
                return;

            var cur = offset;
            for (var i = 0; i < length; i++, cur++)
            {
                data[cur] = (byte)(data[cur] ^ _byteKey[i % _byteKey.Length]);
            }
        }
    }
}

using System;
using System.Text;

namespace LiteNetLib.Encryption
{
    /// <summary>
    ///     Example class; not very good encryption
    /// </summary>
    public class NetXorEncryption : NetEncryption
    {
        private byte[] byteKey;

        /// <summary>
        ///     NetXorEncryption constructor
        /// </summary>
        public NetXorEncryption(byte[] key)
        {
            byteKey = key;
        }

        /// <summary>
        ///     NetXorEncryption constructor
        /// </summary>
        public NetXorEncryption(string key)
        {
            byteKey = Encoding.UTF8.GetBytes(key);
        }

        /// <summary>
        ///     Decrypt an incoming message
        /// </summary>
        public override bool Decrypt(byte[] rawData, int start, ref int length)
        {
            var cur = start;
            for (var i = 0; i < length; i++, cur++)
            {
                var offset = i % byteKey.Length;
                rawData[cur] = (byte) (rawData[cur] ^ byteKey[offset]);
            }
            return true;
        }

        /// <summary>
        ///     Encrypt an outgoing message
        /// </summary>
        public override bool Encrypt(byte[] rawData, ref int start, ref int length)
        {
            var cur = start;
            for (var i = 0; i < length; i++, cur++)
            {
                var offset = i % byteKey.Length;
                rawData[cur] = (byte) (rawData[cur] ^ byteKey[offset]);
            }
            return true;
        }

        public override void SetKey(byte[] data, int offset, int count)
        {
            byteKey = new byte[count];
            Array.Copy(data, offset, byteKey, 0, count);
        }
    }
}
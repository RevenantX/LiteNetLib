#if !WINRT

using System;
using System.IO;
using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public abstract class NetCryptoProviderEncryption : NetEncryption
    {
        private const int IntSize = sizeof(int);

        public override bool Decrypt(byte[] rawData, int start, ref int length)
        {
            var len = BitConverter.ToInt32(rawData, start);
            var buffer = new byte[len];

            using (var ms = new MemoryStream(rawData, start + IntSize, length - IntSize))
            {
                using (var cs = GetDecryptStream(ms))
                {
                    cs.Read(buffer, 0, len);
                }
            }

            length = len;

            Buffer.BlockCopy(buffer, 0, rawData, start, length);

            return true;
        }

        public override bool Encrypt(byte[] rawData, ref int start, ref int length)
        {
            byte[] result;
            using (var ms = new MemoryStream())
            {
                using (var cs = GetEncryptStream(ms))
                {
                    cs.Write(rawData, start, length);
                }

                result = ms.ToArray();
            }

            var lenInByte = BitConverter.GetBytes(length);
            length = result.Length;

            if (rawData.Length < length)
            {
                Array.Resize(ref rawData, length);
            }

            Buffer.BlockCopy(lenInByte, 0, rawData, start, IntSize);
            Buffer.BlockCopy(result, 0, rawData, start + IntSize, length);
            length += IntSize;

            return true;
        }

        protected abstract CryptoStream GetDecryptStream(MemoryStream ms);
        protected abstract CryptoStream GetEncryptStream(MemoryStream ms);
    }
}
#endif
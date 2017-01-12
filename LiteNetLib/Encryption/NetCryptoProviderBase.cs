#if !WINRT

using System;
using System.IO;
using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public abstract class NetCryptoProviderBase : NetEncryption
    {
        private const int IntSize = sizeof(int);
        protected readonly SymmetricAlgorithm Algorithm;

        public NetCryptoProviderBase(SymmetricAlgorithm algo)
        {
            Algorithm = algo;
            Algorithm.GenerateKey();
            Algorithm.GenerateIV();
        }

        public override bool Decrypt(byte[] rawData, int start, ref int length)
        {
            var len = BitConverter.ToInt32(rawData, start);
            var buffer = new byte[len];

            using (var ms = new MemoryStream(rawData, start + IntSize, length - IntSize))
            {
                using (var cs = new CryptoStream(ms, Algorithm.CreateDecryptor(), CryptoStreamMode.Read))
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
                using (var cs = new CryptoStream(ms, Algorithm.CreateEncryptor(), CryptoStreamMode.Write))
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

        public override void SetKey(byte[] data, int offset, int count)
        {
            var len = Algorithm.Key.Length;
            var key = new byte[len];
            for (var i = 0; i < len; i++)
            {
                key[i] = data[offset + i % count];
            }
            Algorithm.Key = key;

            len = Algorithm.IV.Length;
            key = new byte[len];
            for (var i = 0; i < len; i++)
            {
                key[len - 1 - i] = data[offset + i % count];
            }
            Algorithm.IV = key;
        }
    }
}
#endif
#if !WINRT
using System;
using System.Security.Cryptography;
using System.Text;

namespace LiteNetLib.Encryption
{
    /// <summary>
    ///     Methods to encrypt and decrypt data using the XTEA algorithm
    /// </summary>
    public sealed class NetXteaEncryption : NetBlockEncryptionBase
    {
        private const int BVlockSize = 8;
        private const int KeySize = 16;
        private const int Delta = unchecked((int) 0x9E3779B9);
        private static readonly SHA256 Sha = SHA256.Create();

        private readonly int _numRounds;
        private readonly uint[] _sum0;
        private readonly uint[] _sum1;

        /// <summary>
        ///     16 byte key
        /// </summary>
        public NetXteaEncryption(byte[] key, int rounds = 32)
        {
            if (key.Length < KeySize)
            {
                throw new InvalidOperationException("Key too short.");
            }

            _numRounds = rounds;
            _sum0 = new uint[_numRounds];
            _sum1 = new uint[_numRounds];
            var tmp = new uint[8];

            int num2;
            var index = num2 = 0;
            while (index < 4)
            {
                tmp[index] = BitConverter.ToUInt32(key, num2);
                index++;
                num2 += 4;
            }
            for (index = num2 = 0; index < 32; index++)
            {
                _sum0[index] = (uint) num2 + tmp[num2 & 3];
                num2 += -1640531527;
                _sum1[index] = (uint) num2 + tmp[(num2 >> 11) & 3];
            }
        }

        /// <summary>
        ///     String to hash for key
        /// </summary>
        public NetXteaEncryption(string key)
            : this(ComputeSHAHash(Encoding.UTF8.GetBytes(key)))
        {}

        /// <summary>
        ///     Gets the block size for this cipher
        /// </summary>
        public override int BlockSize
        {
            get { return BVlockSize; }
        }

        public static byte[] ComputeSHAHash(byte[] bytes)
        {
            // this is defined in the platform specific files
            return ComputeSHAHash(bytes, 0, bytes.Length);
        }

        public override void SetKey(byte[] data, int offset, int length)
        {
            var key = ComputeSHAHash(data, offset, length);
            if (key.Length < 16)
            {
                throw new InvalidOperationException("Key too short");
            }

            SetKey(key, 0, 16);
        }

        /// <summary>
        ///     Decrypts a block of bytes
        /// </summary>
        protected override void DecryptBlock(byte[] source, int sourceOffset, byte[] destination)
        {
            // Pack bytes into integers
            var v0 = BytesToUInt(source, sourceOffset);
            var v1 = BytesToUInt(source, sourceOffset + 4);

            for (var i = _numRounds - 1; i >= 0; i--)
            {
                v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ _sum1[i];
                v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ _sum0[i];
            }

            UIntToBytes(v0, destination, 0);
            UIntToBytes(v1, destination, 0 + 4);
        }

        /// <summary>
        ///     Encrypts a block of bytes
        /// </summary>
        protected override void EncryptBlock(byte[] source, int sourceOffset, byte[] destination)
        {
            var v0 = BytesToUInt(source, sourceOffset);
            var v1 = BytesToUInt(source, sourceOffset + 4);

            for (var i = 0; i != _numRounds; i++)
            {
                v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ _sum0[i];
                v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ _sum1[i];
            }

            UIntToBytes(v0, destination, 0);
            UIntToBytes(v1, destination, 0 + 4);
        }

        private static uint BytesToUInt(byte[] bytes, int offset)
        {
            var retval = (uint) (bytes[offset] << 24);
            retval |= (uint) (bytes[++offset] << 16);
            retval |= (uint) (bytes[++offset] << 8);
            return retval | bytes[++offset];
        }

        private static byte[] ComputeSHAHash(byte[] bytes, int offset, int count)
        {
            return Sha.ComputeHash(bytes, offset, count);
        }

        private static void UIntToBytes(uint value, byte[] destination, int destinationOffset)
        {
            destination[destinationOffset++] = (byte) (value >> 24);
            destination[destinationOffset++] = (byte) (value >> 16);
            destination[destinationOffset++] = (byte) (value >> 8);
            destination[destinationOffset++] = (byte) value;
        }
    }
}
#endif
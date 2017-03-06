using System;

namespace LiteNetLib.Encryption
{
    /// <summary>
    ///     Base for a non-threadsafe encryption class
    /// </summary>
    public abstract class NetBlockEncryptionBase : NetEncryption
    {
        // temporary space for one block to avoid reallocating every time
        private readonly byte[] _tmp;

        /// <summary>
        ///     NetBlockEncryptionBase constructor
        /// </summary>
        public NetBlockEncryptionBase()
        {
            _tmp = new byte[BlockSize];
        }

        /// <summary>
        ///     Block size in bytes for this cipher
        /// </summary>
        public abstract int BlockSize { get; }

        /// <summary>
        ///     Decrypt an incoming message encrypted with corresponding Encrypt
        /// </summary>
        /// <returns>true if successful; false if failed</returns>
        public override bool Decrypt(byte[] rawData, int start, ref int length)
        {
            var blockSize = BlockSize;
            var numBlocks = length / blockSize;
            if (numBlocks * blockSize != length)
            {
                return false;
            }

            for (var i = 0; i < numBlocks; i++)
            {
                var offset = start + i * blockSize;
                DecryptBlock(rawData, offset, _tmp);
                Buffer.BlockCopy(_tmp, 0, rawData, offset, blockSize);
            }

            return true;
        }

        /// <summary>
        ///     Encrypt am outgoing message with this algorithm.
        ///     No writing can be done to the message after encryption, or message
        ///     will be corrupted
        /// </summary>
        public override bool Encrypt(byte[] rawData, ref int start, ref int length)
        {
            var blockSize = BlockSize;
            var numBlocks = (int) Math.Ceiling(length / (double) blockSize);
            var dstSize = numBlocks * blockSize;

            if (rawData.Length < dstSize)
            {
                Array.Resize(ref rawData, dstSize);
            }

            for (var i = 0; i < numBlocks; i++)
            {
                var offset = start + i * blockSize;

                EncryptBlock(rawData, offset, _tmp);
                Buffer.BlockCopy(_tmp, 0, rawData, offset, blockSize);
            }

            return true;
        }

        /// <summary>
        ///     Decrypt a block of bytes
        /// </summary>
        protected abstract void DecryptBlock(byte[] source, int sourceOffset, byte[] destination);

        /// <summary>
        ///     Encrypt a block of bytes
        /// </summary>
        protected abstract void EncryptBlock(byte[] source, int sourceOffset, byte[] destination);
    }
}
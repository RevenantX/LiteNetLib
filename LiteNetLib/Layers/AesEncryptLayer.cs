using System;
using System.Security.Cryptography;

namespace LiteNetLib.Layers
{
    /// <summary>
    /// Uses AES encryption in CBC mode. Make sure you handle your key properly.
    /// GCHandle.Alloc(key, GCHandleType.Pinned) to avoid your key being moved around different memory segments.
    /// ZeroMemory(gch.AddrOfPinnedObject(), key.Length); to erase it when you are done.
    /// About 4 times slower than XOR, but this is secure, XOR is not. 
    /// Why encrypt: http://ithare.com/udp-for-games-security-encryption-and-ddos-protection/
    /// </summary>
    public class AesEncryptLayer : PacketLayerBase
    {
        public const int KeySize = 256;
        public const int BlockSize = 128;
        public const int KeySizeInBytes = KeySize / 8;
        public const int BlockSizeInBytes = BlockSize / 8;

        private readonly AesCryptoServiceProvider _aes;
        private ICryptoTransform _encryptor;
        private byte[] cipherBuffer = new byte[1500]; //Max possible UDP packet size
        private ICryptoTransform _decryptor;

        /// <summary>
        /// Should be safe against eavesdropping, but is vulnerable to tampering
        /// </summary>
        /// <param name="key"></param>
        /// <param name="initializationVector"></param>
        public AesEncryptLayer(byte[] key) : base(BlockSizeInBytes * 2)
        {
            if (key.Length != KeySizeInBytes) throw new NotSupportedException("EncryptLayer only supports keysize " + KeySize);

            //Switch this with AesGCM for better performance, requires .NET Core 3.0 or Standard 2.1
            _aes = new AesCryptoServiceProvider
            {
                KeySize = KeySize,
                BlockSize = BlockSize,
                Key = key,

                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            _encryptor = _aes.CreateEncryptor();
            _decryptor = _aes.CreateDecryptor();
        }

        public override void ProcessInboundPacket(ref byte[] data, ref int length)
        {
            //Can't copy directly to _aes.IV. It won't work for some reason.
            byte[] iv = new byte[_aes.IV.Length];
            Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
            _aes.IV = iv;
            _decryptor = _aes.CreateDecryptor();

            int currentRead = iv.Length;
            int currentWrite = 0;
            while (length - currentRead > _decryptor.InputBlockSize)
            {
                int decryptedCount = _decryptor.TransformBlock(data, currentRead, _decryptor.InputBlockSize, cipherBuffer, currentWrite);
                currentWrite += decryptedCount;
                currentRead += decryptedCount;
            }

            byte[] lastblock = _decryptor.TransformFinalBlock(data, currentRead, length - currentRead);
            Buffer.BlockCopy(lastblock, 0, cipherBuffer, currentWrite, lastblock.Length);

            data = cipherBuffer;
            length = currentWrite + lastblock.Length;
        }

        public override void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length)
        {
            //Some Unity platforms may need these (and will be slower + generate garbage)
            if (!_encryptor.CanReuseTransform)
            {
                _aes.GenerateIV();
                _encryptor = _aes.CreateEncryptor();
            }

            //Copy IV in plaintext to output, this is standard practice
            _aes.IV.CopyTo(cipherBuffer, 0);

            int currentRead = offset;
            int currentWrite = _aes.IV.Length;
            if (_decryptor.CanTransformMultipleBlocks)
            {
                int len = length / BlockSizeInBytes;
                //Make sure there is some for the last block
                if (length % BlockSizeInBytes == 0) len--;
                int count = _encryptor.TransformBlock(data, currentRead, len, cipherBuffer, currentWrite);
            }
            else
            {
                while (length - currentRead >= _encryptor.InputBlockSize)
                {
                    int encryptedCount = _encryptor.TransformBlock(data, currentRead, _encryptor.InputBlockSize, cipherBuffer, currentWrite);
                    currentRead += encryptedCount;
                    currentWrite += encryptedCount;
                }
            }

            if (length - currentRead > 0)
            {
                //Encrypt and write last block to output
                byte[] lastBytes = _encryptor.TransformFinalBlock(data, currentRead, length - currentRead);
                lastBytes.CopyTo(cipherBuffer, currentWrite);
                currentWrite += lastBytes.Length;
            }

            data = cipherBuffer;
            offset = 0;
            length = currentWrite;
        }
    }
}

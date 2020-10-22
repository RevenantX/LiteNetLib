using LiteNetLib.Layers;
using System;
using System.Net;
using System.Security.Cryptography;

namespace LibSample
{
    /// <summary>
    /// Uses AES encryption in CBC mode. Make sure you handle your key properly.
    /// GCHandle.Alloc(key, GCHandleType.Pinned) to avoid your key being moved around different memory segments.
    /// ZeroMemory(gch.AddrOfPinnedObject(), key.Length); to erase it when you are done.
    /// Speed varies greatly depending on hardware encryption support. 
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
        private byte[] ivBuffer = new byte[BlockSizeInBytes];

        /// <summary>
        /// Should be safe against eavesdropping, but is vulnerable to tampering
        /// Needs a HMAC on top of the encrypted content to be fully safe
        /// </summary>
        /// <param name="key"></param>
        /// <param name="initializationVector"></param>
        public AesEncryptLayer(byte[] key) : base(BlockSizeInBytes * 2)
        {
            if (key.Length != KeySizeInBytes) throw new NotSupportedException("EncryptLayer only supports keysize " + KeySize);

            //Switch this with AesGCM for better performance, requires .NET Core 3.0 or Standard 2.1
            _aes = new AesCryptoServiceProvider();
            _aes.KeySize = KeySize;
            _aes.BlockSize = BlockSize;
            _aes.Key = key;
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;

            _encryptor = _aes.CreateEncryptor();
            _decryptor = _aes.CreateDecryptor();
        }

        public override void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            //Can't copy directly to _aes.IV. It won't work for some reason.
            Buffer.BlockCopy(data, offset, ivBuffer, 0, ivBuffer.Length);
            //_aes.IV = ivBuffer;
            _decryptor = _aes.CreateDecryptor(_aes.Key, ivBuffer);
            offset += BlockSizeInBytes;

            //int currentRead = ivBuffer.Length;
            //int currentWrite = 0;

            //TransformBlocks(_decryptor, data, length, ref currentRead, ref currentWrite);
            byte[] lastBytes = _decryptor.TransformFinalBlock(data, offset, length - offset);

            data = lastBytes;
            offset = 0;
            length = lastBytes.Length;
        }

        public override void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
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
            byte[] lastBytes = _encryptor.TransformFinalBlock(data, currentRead, length - offset);
            lastBytes.CopyTo(cipherBuffer, currentWrite);
            //TransformBlocks(_encryptor, data, length, ref currentRead, ref currentWrite);

            data = cipherBuffer;
            offset = 0;
            length = lastBytes.Length + BlockSizeInBytes;
        }

        private void TransformBlocks(ICryptoTransform transform, byte[] input, int inputLength, ref int currentRead, ref int currentWrite)
        {
            //This loop produces a invalid padding exception
            //I'm leaving it here as a start point in case others need support for 
            //Platforms wheere !transfom.CanTransformMultipleBlocks
            if (!transform.CanTransformMultipleBlocks)
            {
                while (inputLength - currentRead > BlockSizeInBytes)
                {
                    int encryptedCount = transform.TransformBlock(input, currentRead, BlockSizeInBytes, cipherBuffer, currentWrite);
                    currentRead += encryptedCount;
                    currentWrite += encryptedCount;
                }
            }

            byte[] lastBytes = transform.TransformFinalBlock(input, currentRead, inputLength - currentRead);
            lastBytes.CopyTo(cipherBuffer, currentWrite);
            currentWrite += lastBytes.Length;
        }
    }
}

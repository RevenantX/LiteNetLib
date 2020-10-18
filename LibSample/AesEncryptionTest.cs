using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LibSample
{
    class AesEncryptionTest : IExample
    {
        public void Run() => AesLayerEncryptDecrypt();

        private void AesLayerEncryptDecrypt()
        {
            var keyGen = RandomNumberGenerator.Create();
            byte[] key = new byte[AesEncryptLayer.KeySizeInBytes];
            keyGen.GetBytes(key);
            const string testData = "This text is long enough to need multiple blocks to encrypt";

            var outboudLayer = new AesEncryptLayer(key);
            byte[] outbound = Encoding.ASCII.GetBytes(testData);
            int lengthOfPacket = outbound.Length;
            int start = 0;
            int length = outbound.Length;
            outboudLayer.ProcessOutBoundPacket(ref outbound, ref start, ref length);

            int minLenth = lengthOfPacket + AesEncryptLayer.BlockSizeInBytes;
            int maxLength = lengthOfPacket + outboudLayer.ExtraPacketSizeForLayer;
            if (length < minLenth || length > maxLength)
            {
                throw new Exception("Packet length out of bounds");
            }

            var inboundLayer = new AesEncryptLayer(key);
            //Copy array so we dont read and write to same array
            byte[] inboundData = new byte[outbound.Length];
            outbound.CopyTo(inboundData, 0);
            inboundLayer.ProcessInboundPacket(ref inboundData, ref length);

            Console.WriteLine(Encoding.ASCII.GetString(inboundData, 0, length));
            byte[] expectedPlaintext = Encoding.ASCII.GetBytes(testData);

            var isEqualLength = expectedPlaintext.Length == length;
            var areContentEqual = expectedPlaintext.SequenceEqual(inboundData);
            if (isEqualLength && areContentEqual)
            {
                Console.WriteLine("Test complete");
            }
            else
            {
                throw new Exception("Test failed, decrypted data not equal to original");
            }
        }
    }
}

using LiteNetLib.Layers;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace LiteNetLib.Tests
{
    [TestFixture]
    public class EncryptionLayerTest
    {
        private Random _random;
        private Stopwatch _stopwatch;

        [SetUp]
        public void Setup()
        {
            _random = new Random();
            _stopwatch = new Stopwatch();
        }

        [Test]
        public void Aes_Layer_encrypt_decrypt()
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

            Assert.That(length, Is.InRange(lengthOfPacket + AesEncryptLayer.BlockSizeInBytes, lengthOfPacket + outboudLayer.ExtraPacketSizeForLayer));

            var inboundLayer = new AesEncryptLayer(key);
            //Copy array so we dont read and write to same array
            byte[] inboundData = new byte[outbound.Length];
            outbound.CopyTo(inboundData, 0);
            inboundLayer.ProcessInboundPacket(ref inboundData, ref length);

            Console.WriteLine(Encoding.ASCII.GetString(inboundData, 0, length));
            byte[] expectedPlaintext = Encoding.ASCII.GetBytes(testData);
            Assert.AreEqual(expectedPlaintext.Length, length);
            for (int i = 0; i < expectedPlaintext.Length; i++)
            {
                Assert.AreEqual(expectedPlaintext[i], inboundData[i]);
            }
        }

        [Test]
        [Explicit]
        public void SpeedOfXorLayer()
        {
            var keyGen = RandomNumberGenerator.Create();
            byte[] key = new byte[1500]; //As long as packet size since XOR alone is very unsafe
            keyGen.GetBytes(key);
            PacketLayerBase layer = new XorEncryptLayer(key);
            byte[][] sampleData = GenerateData(100000, 1500);
            Encrypt(sampleData, layer);
            Decrypt(sampleData, layer);
            //Check console output for ticks spent to compare with AES
        }

        [Test]
        [Explicit]
        public void SpeedOfAesLayer()
        {
            var keyGen = RandomNumberGenerator.Create();
            byte[] key = new byte[AesEncryptLayer.KeySizeInBytes];
            keyGen.GetBytes(key);
            byte[] iv = new byte[AesEncryptLayer.BlockSizeInBytes];
            keyGen.GetBytes(iv);
            PacketLayerBase layer = new AesEncryptLayer(key);
            byte[][] sampleData = GenerateData(100000, 1500 - layer.ExtraPacketSizeForLayer);
            Encrypt(sampleData, layer);
            Decrypt(sampleData, layer);
            //Check output for ticks spent to compare with XOR
        }

        private byte[][] GenerateData(int numSamples, int sizePerSample)
        {
            byte[][] sampleData = new byte[numSamples][];
            for (int i = 0; i < sampleData.Length; i++)
            {
                sampleData[i] = new byte[sizePerSample];
                _random.NextBytes(sampleData[i]);
            }
            return sampleData;
        }

        private void Encrypt(byte[][] sampleData, PacketLayerBase layer)
        {
            GC.Collect();
            long totalMemoryAllocated = 0;
            for (int i = 0; i < sampleData.Length; i++)
            {
                int length = sampleData[i].Length;
                int start = 0;
                long memUsagePreEncrypt = GC.GetTotalMemory(false);
                _stopwatch.Start();
                layer.ProcessOutBoundPacket(ref sampleData[i], ref start, ref length);
                _stopwatch.Stop();
                totalMemoryAllocated += GC.GetTotalMemory(false) - memUsagePreEncrypt;
                byte[] output = sampleData[i];
                sampleData[i] = new byte[length];
                Buffer.BlockCopy(output, 0, sampleData[i], 0, length);
            }
            Console.WriteLine("Encrypt with {0} took {1} ticks", layer.GetType().Name, _stopwatch.ElapsedTicks);
            Console.WriteLine("Encrypt with {0} generated {1}bytes memory garbage", layer.GetType().Name, totalMemoryAllocated);
        }

        private void Decrypt(byte[][] sampleData, PacketLayerBase layer)
        {
            long memUsagePreDecrypt = GC.GetTotalMemory(true);
            _stopwatch.Start();
            for (int i = 0; i < sampleData.Length; i++)
            {
                int length = sampleData[i].Length;
                layer.ProcessInboundPacket(ref sampleData[i], ref length);
            }
            _stopwatch.Stop();
            long memoryAllocated = GC.GetTotalMemory(false) - memUsagePreDecrypt;
            Console.WriteLine("Decrypt with {0} took {1} ticks", layer.GetType().Name, _stopwatch.ElapsedTicks);
            Console.WriteLine("Decrypt with {0} generated {1}bytes memory garbage", layer.GetType().Name, memoryAllocated);
        }
    }
}

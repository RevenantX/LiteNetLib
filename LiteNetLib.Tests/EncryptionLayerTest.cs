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
            byte[] outboundPlaintext = Encoding.ASCII.GetBytes(testData);
            byte[] cipherText = outboundPlaintext;
            int lengthOfPacket = outboundPlaintext.Length;
            int start = 0;
            int length = outboundPlaintext.Length;
            outboudLayer.ProcessOutBoundPacket(ref cipherText, ref start, ref length);

            //Console.WriteLine(BitConverter.ToString(cipherText, 0, length).Replace("-", ""));
            Assert.That(length, Is.InRange(lengthOfPacket + AesEncryptLayer.BlockSizeInBytes, lengthOfPacket + outboudLayer.ExtraPacketSizeForLayer));

            var inboundLayer = new AesEncryptLayer(key);
            byte[] inboundPlaintext = cipherText;
            inboundLayer.ProcessInboundPacket(ref inboundPlaintext, ref length);
            Assert.That(inboundPlaintext, Is.EquivalentTo(Encoding.ASCII.GetBytes(testData)));
        }

        [Test]
        [Explicit]
        public void SpeedOfXorLayer()
        {
            var keyGen = RandomNumberGenerator.Create();
            byte[] key = new byte[1500]; //As long as packet size since XOR alone is very unsafe
            keyGen.GetBytes(key);
            PacketLayerBase layer = new XorEncryptLayer(key);
            byte[][] sampleData = GenerateData(10000, 1500);
            Encrypt(sampleData, layer);
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
            byte[][] sampleData = GenerateData(10000, 1500 - layer.ExtraPacketSizeForLayer);
            Encrypt(sampleData, layer);
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
            _stopwatch.Start();
            for (int i = 0; i <= sampleData.Length; i++)
            {
                int length = sampleData[i].Length;
                int start = 0;
                layer.ProcessOutBoundPacket(ref sampleData[i], ref start, ref length);
            }
            _stopwatch.Stop();
            Console.WriteLine("Decrypt with " + layer.GetType().Name + " took " + _stopwatch.ElapsedTicks + " ticks");
        }
    }
}

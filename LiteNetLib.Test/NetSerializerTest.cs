using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLib.Test
{
    [TestFixture]
    [Category("Serializer")]
    public class NetSerializerTest
    {
        [SetUp]
        public void Init()
        {
            _samplePacket = new SamplePacket
            {
                SomeFloat = 3.42f,
                SomeIntArray = new[] {6, 5, 4},
                SomeString = "Test String",
                SomeVector2 = new SomeVector2(4, 5),
                SomeVectors = new[] {new SomeVector2(1, 2), new SomeVector2(3, 4)},
                SomeEnum = TestEnum.B,
                TestObj = new SampleNetSerializable {Value = 5}
            };

            _serializer = new NetSerializer();
            _serializer.RegisterCustomType<SampleNetSerializable>();
            _serializer.RegisterCustomType(SomeVector2.Serialize, SomeVector2.Deserialize);
        }

        private SamplePacket _samplePacket;
        private NetSerializer _serializer;

        private struct SomeVector2
        {
            public int X;
            public int Y;

            public SomeVector2(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static void Serialize(NetDataWriter writer, SomeVector2 vector)
            {
                writer.Put(vector.X);
                writer.Put(vector.Y);
            }

            public static SomeVector2 Deserialize(NetDataReader reader)
            {
                var res = new SomeVector2();
                res.X = reader.GetInt();
                res.Y = reader.GetInt();
                return res;
            }
        }

        private struct SampleNetSerializable : INetSerializable
        {
            public int Value;

            public void Serialize(NetDataWriter writer)
            {
                writer.Put(Value);
            }

            public void Desereialize(NetDataReader reader)
            {
                Value = reader.GetInt();
            }
        }

        private enum TestEnum
        {
            A = 1,
            B = 7,
            C = 13
        }

        private class SamplePacket
        {
            public string EmptyString { get; set; }
            public float SomeFloat { get; set; }
            public int[] SomeIntArray { get; set; }
            public string SomeString { get; set; }
            public SomeVector2 SomeVector2 { get; set; }
            public SomeVector2[] SomeVectors { get; set; }
            public TestEnum SomeEnum { get; set; }
            public SampleNetSerializable TestObj { get; set; }
        }

        private static bool AreSame(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            {
                return true;
            }
            return s1 == s2;
        }

        [Test]
        [Timeout(2000)]
        public void CustomPackageTest()
        {
            var writer = new NetDataWriter();
            writer.Put(_serializer.Serialize(_samplePacket));

            var reader = new NetDataReader(writer.CopyData());
            SamplePacket readPackage = null;

            _serializer.SubscribeReusable<SamplePacket>(
                packet =>
                {
                    readPackage = packet;
                });

            _serializer.ReadAllPackets(reader);

            Assert.NotNull(readPackage);
            Assert.IsTrue(AreSame(_samplePacket.EmptyString, readPackage.EmptyString));
            Assert.AreEqual(_samplePacket.SomeFloat, readPackage.SomeFloat);
            Assert.AreEqual(_samplePacket.SomeIntArray, readPackage.SomeIntArray);
            Assert.IsTrue(AreSame(_samplePacket.SomeString, readPackage.SomeString));
            Assert.AreEqual(_samplePacket.SomeVector2, readPackage.SomeVector2);
            Assert.AreEqual(_samplePacket.SomeVectors, readPackage.SomeVectors);
            Assert.AreEqual(_samplePacket.SomeEnum, readPackage.SomeEnum);
            Assert.AreEqual(_samplePacket.TestObj.Value, readPackage.TestObj.Value);
        }
    }
}
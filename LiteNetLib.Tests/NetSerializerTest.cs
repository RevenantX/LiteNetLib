﻿using System.Collections.Generic;
using System.Runtime.Serialization;
using LiteNetLib.Utils;

using NUnit.Framework;

namespace LiteNetLib.Tests
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
                SomeByteArray = new byte[] { 255, 1, 0 },
                TestObj = new SampleNetSerializable {Value = 5},
                TestArray = new [] { new SampleNetSerializable { Value = 6 }, new SampleNetSerializable { Value = 15 } },
                SampleClassArray = new[] { new SampleClass { Value = 6 }, new SampleClass { Value = 15 } },
                SampleClassList = new List<SampleClass> { new SampleClass { Value = 1 }, new SampleClass { Value = 5 }},
                VectorList = new List<SomeVector2> { new SomeVector2(-1,-2), new SomeVector2(700, 800) },
                IgnoreMe = 1337
            };

            _packetProcessor = new NetPacketProcessor();
            _packetProcessor.RegisterNestedType<SampleNetSerializable>();
            _packetProcessor.RegisterNestedType(() => new SampleClass());
            _packetProcessor.RegisterNestedType(SomeVector2.Serialize, SomeVector2.Deserialize);
        }

        private SamplePacket _samplePacket;
        private NetPacketProcessor _packetProcessor;

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
                writer.PutInt(vector.X);
                writer.PutInt(vector.Y);
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
                writer.PutInt(Value);
            }

            public void Deserialize(NetDataReader reader)
            {
                Value = reader.GetInt();
            }
        }

        private class SampleClass : INetSerializable
        {
            public int Value;

            public void Serialize(NetDataWriter writer)
            {
                writer.PutInt(Value);
            }

            public void Deserialize(NetDataReader reader)
            {
                Value = reader.GetInt();
            }

            public override bool Equals(object obj)
            {
                return ((SampleClass)obj).Value == Value;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
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
            public byte[] SomeByteArray { get; set; }
            public string SomeString { get; set; }
            public SomeVector2 SomeVector2 { get; set; }
            public SomeVector2[] SomeVectors { get; set; }
            public TestEnum SomeEnum { get; set; }
            public SampleNetSerializable TestObj { get; set; }
            public SampleNetSerializable[] TestArray { get; set; }
            public SampleClass[] SampleClassArray { get; set; }
            public List<SampleClass> SampleClassList { get; set; }
            public List<SomeVector2> VectorList { get; set; }
            [IgnoreDataMember]
            public int IgnoreMe { get; set; }
        }

        private static bool AreSame(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            {
                return true;
            }
            return s1 == s2;
        }

        [Test, MaxTime(2000)]
        public void CustomPackageTest()
        {
            var writer = new NetDataWriter();
            _packetProcessor.Write(writer, _samplePacket);

            var reader = new NetDataReader(writer);
            SamplePacket readPackage = null;

            _packetProcessor.SubscribeReusable<SamplePacket>(
                packet =>
                {
                    readPackage = packet;
                });

            _packetProcessor.ReadAllPackets(reader);

            Assert.NotNull(readPackage);
            Assert.IsTrue(AreSame(_samplePacket.EmptyString, readPackage.EmptyString));
            Assert.AreEqual(_samplePacket.SomeFloat, readPackage.SomeFloat);
            Assert.AreEqual(_samplePacket.SomeIntArray, readPackage.SomeIntArray);
            Assert.IsTrue(AreSame(_samplePacket.SomeString, readPackage.SomeString));
            Assert.AreEqual(_samplePacket.SomeVector2, readPackage.SomeVector2);
            Assert.AreEqual(_samplePacket.SomeVectors, readPackage.SomeVectors);
            Assert.AreEqual(_samplePacket.SomeEnum, readPackage.SomeEnum);
            Assert.AreEqual(_samplePacket.TestObj.Value, readPackage.TestObj.Value);
            Assert.AreEqual(_samplePacket.TestArray, readPackage.TestArray);
            Assert.AreEqual(_samplePacket.SomeByteArray, readPackage.SomeByteArray);
            Assert.AreEqual(_samplePacket.SampleClassArray, readPackage.SampleClassArray);
            Assert.AreEqual(0, readPackage.IgnoreMe); // expect 0 because it should be ignored
            CollectionAssert.AreEqual(_samplePacket.SampleClassList, readPackage.SampleClassList);
            CollectionAssert.AreEqual(_samplePacket.VectorList, readPackage.VectorList);

            //remove test
            _samplePacket.SampleClassList.RemoveAt(0);
            _samplePacket.SampleClassArray = new []{new SampleClass {Value = 1}};
            _samplePacket.VectorList.RemoveAt(0);

            writer.Reset();
            _packetProcessor.Write(writer, _samplePacket);
            reader.SetSource(writer);
            _packetProcessor.ReadAllPackets(reader);

            Assert.AreEqual(_samplePacket.SampleClassArray, readPackage.SampleClassArray);
            CollectionAssert.AreEqual(_samplePacket.SampleClassList, readPackage.SampleClassList);

            //add test
            _samplePacket.SampleClassList.Add(new SampleClass { Value = 152 });
            _samplePacket.SampleClassList.Add(new SampleClass { Value = 154 });
            _samplePacket.SampleClassArray = new[] { new SampleClass { Value = 1 }, new SampleClass { Value = 2 }, new SampleClass { Value = 3 } };
            _samplePacket.VectorList.Add(new SomeVector2(500,600));

            writer.Reset();
            _packetProcessor.Write(writer, _samplePacket);
            reader.SetSource(writer);
            _packetProcessor.ReadAllPackets(reader);

            Assert.AreEqual(_samplePacket.SampleClassArray, readPackage.SampleClassArray);
            CollectionAssert.AreEqual(_samplePacket.SampleClassList, readPackage.SampleClassList);
        }
    }
}

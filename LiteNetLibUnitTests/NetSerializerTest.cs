using System;
using System.Linq;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture]
    public class NetSerializerTest
    {
        [SetUp]
        public void Init()
        {
            _samplePackage = new SamplePacket
            {
                SomeFloat = 3.42f,
                SomeIntArray = new[] {6, 5, 4},
                SomeString = "Test String",
                SomeVector2 = new SomeVector2(4, 5),
                SomeVectors = new[] {new SomeVector2(1, 2), new SomeVector2(3, 4)},
                TestObj = new SampleNetSerializable {Value = 5}
            };

            _serializer = new NetSerializer();
            _serializer.RegisterCustomType<SampleNetSerializable>();
            _serializer.RegisterCustomType(SomeVector2.Serialize, SomeVector2.Deserialize);
        }

        private SamplePacket _samplePackage;
        private NetSerializer _serializer;

        private struct SomeVector2 : IEquatable<SomeVector2>
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

            public bool Equals(SomeVector2 other)
            {
                return X == other.X && Y == other.Y;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                return obj is SomeVector2 && Equals((SomeVector2) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Y;
                }
            }
        }

        private struct SampleNetSerializable : INetSerializable, IEquatable<SampleNetSerializable>
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

            public bool Equals(SampleNetSerializable other)
            {
                return Value == other.Value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                return obj is SampleNetSerializable && Equals((SampleNetSerializable) obj);
            }

            public override int GetHashCode()
            {
                return Value;
            }
        }

        private class SamplePacket : IEquatable<SamplePacket>
        {
            public string EmptyString { get; set; }
            public float SomeFloat { get; set; }
            public int[] SomeIntArray { get; set; }
            public string SomeString { get; set; }
            public SomeVector2 SomeVector2 { get; set; }
            public SomeVector2[] SomeVectors { get; set; }
            public SampleNetSerializable TestObj { get; set; }

            public bool Equals(SamplePacket other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }
                if (ReferenceEquals(this, other))
                {
                    return true;
                }
                return string.Equals(SomeString, other.SomeString) && SomeFloat.Equals(other.SomeFloat) &&
                       SomeIntArray.SequenceEqual(other.SomeIntArray) && SomeVector2.Equals(other.SomeVector2) &&
                       SomeVectors.SequenceEqual(other.SomeVectors) && string.Equals(EmptyString, other.EmptyString) &&
                       TestObj.Equals(other.TestObj);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }
                if (obj.GetType() != GetType())
                {
                    return false;
                }
                return Equals((SamplePacket) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = SomeString != null ? SomeString.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ SomeFloat.GetHashCode();
                    hashCode = (hashCode * 397) ^ (SomeIntArray != null ? GetHashCode(SomeIntArray) : 0);
                    hashCode = (hashCode * 397) ^ SomeVector2.GetHashCode();
                    hashCode = (hashCode * 397) ^ (SomeVectors != null ? GetHashCode(SomeVectors) : 0);
                    hashCode = (hashCode * 397) ^ (EmptyString != null ? EmptyString.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ TestObj.GetHashCode();
                    return hashCode;
                }
            }

            public int GetHashCode<T>(T[] array)
            {
                unchecked
                {
                    if (array == null)
                    {
                        return 0;
                    }
                    int hash = 17;
                    foreach (T element in array)
                    {
                        hash = hash * 31 + element.GetHashCode();
                    }
                    return hash;
                }
            }
        }

        [Test]
        [Timeout(2000)]
        public void CustomPackageTest()
        {
            var writer = new NetDataWriter();
            writer.Put(_serializer.Serialize(_samplePackage));

            var reader = new NetDataReader(writer.CopyData());
            SamplePacket readPackage = null;

            _serializer.SubscribeReusable<SamplePacket>(
                packet =>
                {
                    readPackage = packet;
                });

            _serializer.ReadAllPackets(reader);

            Assert.NotNull(readPackage);
            Assert.AreEqual(_samplePackage, readPackage);
        }
    }
}
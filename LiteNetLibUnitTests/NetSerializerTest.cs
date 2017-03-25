using System;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture]
    public class NetSerializerTest
    {
        private struct SerializableDataType : INetSerializable, IEquatable<SerializableDataType>
        {
            public int IntValue;
            public string StringValue;

            public static void Serialize(NetDataWriter writer, SerializableDataType data)
            {
                data.Serialize(writer);
            }

            public static SerializableDataType Deserialize(NetDataReader reader)
            {
                var data = new SerializableDataType();

                data.Desereialize(reader);

                return data;
            }

            public void Serialize(NetDataWriter writer)
            {
                writer.Put(IntValue);
                writer.Put(StringValue.Length);
                writer.Put(StringValue);
            }

            public void Desereialize(NetDataReader reader)
            {
                IntValue = reader.GetInt();
                var stringLen = reader.GetInt();
                StringValue = reader.GetString(stringLen);
            }

            public bool Equals(SerializableDataType other)
            {
                return IntValue == other.IntValue && string.Equals(StringValue, other.StringValue);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                return obj is SerializableDataType && Equals((SerializableDataType) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (IntValue * 397) ^ (StringValue != null ? StringValue.GetHashCode() : 0);
                }
            }
        }
        private class CustomPackage : IEquatable<CustomPackage>
        {
            public SerializableDataType Data;
            public string String;
            public float Single;

            public bool Equals(CustomPackage other)
            {
                if (ReferenceEquals(null, other))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;
                return Data.Equals(other.Data) && string.Equals(String, other.String) && Single.Equals(other.Single);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                if (ReferenceEquals(this, obj))
                    return true;
                if (obj.GetType() != this.GetType())
                    return false;
                return Equals((CustomPackage) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Data.GetHashCode();
                    hashCode = (hashCode * 397) ^ (String != null ? String.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ Single.GetHashCode();
                    return hashCode;
                }
            }
        }

        private CustomPackage _customPackageCache;
        private SerializableDataType _customDataCache;

        [SetUp]
        private void Init()
        {
            _customDataCache = new SerializableDataType() { IntValue = 10, StringValue = "String" };
            _customPackageCache = new CustomPackage()
            {
                Data = _customDataCache,
                Single = 3.1415f,
                String = "String",
            };
        }

        [Test, Timeout(2000)]
        public void CustomPackageTest()
        {
            var serializer = new NetSerializer();

            serializer.RegisterCustomType<SerializableDataType>();
            
            var writer = new NetDataWriter();
            serializer.Serialize(writer, _customPackageCache);

            var reader = new NetDataReader(writer.CopyData());
            var deserializedPackage = serializer.ReadKnownPacket<CustomPackage>(reader);

            Assert.AreEqual(_customPackageCache, deserializedPackage);
        }

        [Test, Timeout(2000)]
        public void CustomPackageObservableTest()
        {
            var serializer = new NetSerializer();

            serializer.RegisterCustomType<SerializableDataType>();
            
            var writer = new NetDataWriter();
            serializer.Serialize(writer, _customPackageCache);

            CustomPackage deserializedPackage = null;
            serializer.Subscribe(
                customPackage => deserializedPackage = customPackage,
                () => new CustomPackage());

            var reader = new NetDataReader(writer.CopyData());
            serializer.ReadAllPackets(reader);

            Assert.AreEqual(_customPackageCache, deserializedPackage);
        }
    }
}
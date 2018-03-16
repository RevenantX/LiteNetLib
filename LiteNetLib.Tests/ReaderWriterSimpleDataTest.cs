using LiteNetLib.Utils;

using NUnit.Framework;

namespace LiteNetLib.Tests
{
    [TestFixture]
    [Category("DataReaderWriter")]
    public class ReaderWriterSimpleDataTest
    {
        [Test]
        public void WriteReadBool()
        {
            var ndw = new NetDataWriter();
            ndw.Put(true);

            var ndr = new NetDataReader(ndw.Data);
            var readBool = ndr.GetBool();

            Assert.AreEqual(readBool, true);
        }

        [Test]
        public void WriteReadBoolArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {true, false, true, false, false});

            var ndr = new NetDataReader(ndw.Data);
            var readBoolArray = ndr.GetBoolArray();

            Assert.That(new[] {true, false, true, false, false}, Is.EqualTo(readBoolArray).AsCollection);
        }

        [Test]
        public void WriteReadByte()
        {
            var ndw = new NetDataWriter();
            ndw.Put((byte) 8);

            var ndr = new NetDataReader(ndw.Data);
            var readByte = ndr.GetByte();

            Assert.AreEqual(readByte, (byte) 8);
        }

        [Test]
        public void WriteReadByteArray()
        {
            var ndw = new NetDataWriter();
            ndw.Put(new byte[] {1, 2, 4, 8, 16, byte.MaxValue, byte.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readByteArray = new byte[7];
            ndr.GetBytes(readByteArray, 7);

            Assert.That(
                new byte[] {1, 2, 4, 8, 16, byte.MaxValue, byte.MinValue},
                Is.EqualTo(readByteArray).AsCollection);
        }

        [Test]
        public void WriteReadDouble()
        {
            var ndw = new NetDataWriter();
            ndw.Put(3.1415);

            var ndr = new NetDataReader(ndw.Data);
            var readDouble = ndr.GetDouble();

            Assert.AreEqual(readDouble, 3.1415);
        }

        [Test]
        public void WriteReadDoubleArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1.1, 2.2, 3.3, 4.4, double.MaxValue, double.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readDoubleArray = ndr.GetDoubleArray();

            Assert.That(
                new[] {1.1, 2.2, 3.3, 4.4, double.MaxValue, double.MinValue},
                Is.EqualTo(readDoubleArray).AsCollection);
        }

        [Test]
        public void WriteReadFloat()
        {
            var ndw = new NetDataWriter();
            ndw.Put(3.1415f);

            var ndr = new NetDataReader(ndw.Data);
            var readFloat = ndr.GetFloat();

            Assert.AreEqual(readFloat, 3.1415f);
        }

        [Test]
        public void WriteReadFloatArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1.1f, 2.2f, 3.3f, 4.4f, float.MaxValue, float.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readFloatArray = ndr.GetFloatArray();

            Assert.That(
                new[] {1.1f, 2.2f, 3.3f, 4.4f, float.MaxValue, float.MinValue},
                Is.EqualTo(readFloatArray).AsCollection);
        }

        [Test]
        public void WriteReadInt()
        {
            var ndw = new NetDataWriter();
            ndw.Put(32);

            var ndr = new NetDataReader(ndw.Data);
            var readInt = ndr.GetInt();

            Assert.AreEqual(readInt, 32);
        }

        [Test]
        public void WriteReadIntArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1, 2, 3, 4, 5, 6, 7, int.MaxValue, int.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readIntArray = ndr.GetIntArray();

            Assert.That(new[] {1, 2, 3, 4, 5, 6, 7, int.MaxValue, int.MinValue}, Is.EqualTo(readIntArray).AsCollection);
        }

        [Test]
        public void WriteReadLong()
        {
            var ndw = new NetDataWriter();
            ndw.Put(64L);

            var ndr = new NetDataReader(ndw.Data);
            var readLong = ndr.GetLong();

            Assert.AreEqual(readLong, 64L);
        }

        [Test]
        public void WriteReadLongArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1L, 2L, 3L, 4L, long.MaxValue, long.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readLongArray = ndr.GetLongArray();

            Assert.That(new[] {1L, 2L, 3L, 4L, long.MaxValue, long.MinValue}, Is.EqualTo(readLongArray).AsCollection);
        }

        [Test]
        public void WriteReadNetEndPoint()
        {
            var ndw = new NetDataWriter();
            ndw.Put(new NetEndPoint("127.0.0.1", 7777));

            var ndr = new NetDataReader(ndw.Data);
            var readNetEndPoint = ndr.GetNetEndPoint();

            Assert.AreEqual(readNetEndPoint, new NetEndPoint("127.0.0.1", 7777));
        }

        [Test]
        public void WriteReadSByte()
        {
            var ndw = new NetDataWriter();
            ndw.Put((sbyte) 8);

            var ndr = new NetDataReader(ndw.Data);
            var readSByte = ndr.GetSByte();

            Assert.AreEqual(readSByte, (sbyte) 8);
        }

        [Test]
        public void WriteReadShort()
        {
            var ndw = new NetDataWriter();
            ndw.Put((short) 16);

            var ndr = new NetDataReader(ndw.Data);
            var readShort = ndr.GetShort();

            Assert.AreEqual(readShort, (short) 16);
        }

        [Test]
        public void WriteReadShortArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new short[] {1, 2, 3, 4, 5, 6, short.MaxValue, short.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readShortArray = ndr.GetShortArray();

            Assert.That(
                new short[] {1, 2, 3, 4, 5, 6, short.MaxValue, short.MinValue},
                Is.EqualTo(readShortArray).AsCollection);
        }

        [Test]
        public void WriteReadString()
        {
            var ndw = new NetDataWriter();
            ndw.Put("String", 10);

            var ndr = new NetDataReader(ndw.Data);
            var readString = ndr.GetString(10);

            Assert.AreEqual(readString, "String");
        }

        [Test]
        public void WriteReadStringArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {"First", "Second", "Third", "Fourth"});

            var ndr = new NetDataReader(ndw.Data);
            var readStringArray = ndr.GetStringArray(10);

            Assert.That(new[] {"First", "Second", "Third", "Fourth"}, Is.EqualTo(readStringArray).AsCollection);
        }

        [Test]
        public void WriteReadUInt()
        {
            var ndw = new NetDataWriter();
            ndw.Put(34U);

            var ndr = new NetDataReader(ndw.Data);
            var readUInt = ndr.GetUInt();

            Assert.AreEqual(readUInt, 34U);
        }

        [Test]
        public void WriteReadUIntArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1U, 2U, 3U, 4U, 5U, 6U, uint.MaxValue, uint.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readUIntArray = ndr.GetUIntArray();

            Assert.That(
                new[] {1U, 2U, 3U, 4U, 5U, 6U, uint.MaxValue, uint.MinValue},
                Is.EqualTo(readUIntArray).AsCollection);
        }

        [Test]
        public void WriteReadULong()
        {
            var ndw = new NetDataWriter();
            ndw.Put(64UL);

            var ndr = new NetDataReader(ndw.Data);
            var readULong = ndr.GetULong();

            Assert.AreEqual(readULong, 64UL);
        }

        [Test]
        public void WriteReadULongArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1UL, 2UL, 3UL, 4UL, 5UL, ulong.MaxValue, ulong.MinValue});

            var ndr = new NetDataReader(ndw.Data);
            var readULongArray = ndr.GetULongArray();

            Assert.That(
                new[] {1UL, 2UL, 3UL, 4UL, 5UL, ulong.MaxValue, ulong.MinValue},
                Is.EqualTo(readULongArray).AsCollection);
        }

        [Test]
        public void WriteReadUShort()
        {
            var ndw = new NetDataWriter();
            ndw.Put((ushort) 16);

            var ndr = new NetDataReader(ndw.Data);
            var readUShort = ndr.GetUShort();

            Assert.AreEqual(readUShort, (ushort) 16);
        }
    }
}
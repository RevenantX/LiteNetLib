using LiteNetLib.Utils;

using NUnit.Framework;
using System;
using System.Net;

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

            var ndr = new NetDataReader(ndw);
            var readBool = ndr.GetBool();

            Assert.AreEqual(readBool, true);
        }

        [Test]
        public void WriteReadBoolArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {true, false, true, false, false});

            var ndr = new NetDataReader(ndw);
            var readBoolArray = ndr.GetBoolArray();

            Assert.That(new[] {true, false, true, false, false}, Is.EqualTo(readBoolArray).AsCollection);
        }

        [Test]
        public void WriteReadByte()
        {
            var ndw = new NetDataWriter();
            ndw.Put((byte) 8);

            var ndr = new NetDataReader(ndw);
            var readByte = ndr.GetByte();

            Assert.AreEqual(readByte, (byte) 8);
        }

        [Test]
        public void WriteReadByteArray()
        {
            var ndw = new NetDataWriter();
            ndw.Put(new byte[] {1, 2, 4, 8, 16, byte.MaxValue, byte.MinValue});

            var ndr = new NetDataReader(ndw);
            var readByteArray = new byte[7];
            ndr.GetBytes(readByteArray, 7);

            Assert.That(
                new byte[] {1, 2, 4, 8, 16, byte.MaxValue, byte.MinValue},
                Is.EqualTo(readByteArray).AsCollection);
        }

#if NET5_0_OR_GREATER
        [Test]
        public void WriteReadByteSpan()
        {
            Span<byte> tempBytes = new byte[] { 1, 2, 4, 8 };
            var ndw = new NetDataWriter();
            ndw.Put(tempBytes);
            Span<byte> anotherTempBytes = new byte[] { 16, byte.MaxValue, byte.MinValue };
            ndw.Put(anotherTempBytes);

            var ndr = new NetDataReader(ndw);
            var readByteArray = new byte[7];
            ndr.GetBytes(readByteArray, 7);

            Assert.That(
                new byte[] { 1, 2, 4, 8, 16, byte.MaxValue, byte.MinValue },
                Is.EqualTo(readByteArray).AsCollection);
        }
#endif

        [Test]
        public void WriteReadDouble()
        {
            var ndw = new NetDataWriter();
            ndw.Put(3.1415);

            var ndr = new NetDataReader(ndw);
            var readDouble = ndr.GetDouble();

            Assert.AreEqual(readDouble, 3.1415);
        }

        [Test]
        public void WriteReadDoubleArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1.1, 2.2, 3.3, 4.4, double.MaxValue, double.MinValue});

            var ndr = new NetDataReader(ndw);
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

            var ndr = new NetDataReader(ndw);
            var readFloat = ndr.GetFloat();

            Assert.AreEqual(readFloat, 3.1415f);
        }

        [Test]
        public void WriteReadFloatArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1.1f, 2.2f, 3.3f, 4.4f, float.MaxValue, float.MinValue});

            var ndr = new NetDataReader(ndw);
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

            var ndr = new NetDataReader(ndw);
            var readInt = ndr.GetInt();

            Assert.AreEqual(readInt, 32);
        }

        [Test]
        public void WriteReadIntArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1, 2, 3, 4, 5, 6, 7, int.MaxValue, int.MinValue});

            var ndr = new NetDataReader(ndw);
            var readIntArray = ndr.GetIntArray();

            Assert.That(new[] {1, 2, 3, 4, 5, 6, 7, int.MaxValue, int.MinValue}, Is.EqualTo(readIntArray).AsCollection);
        }

        [Test]
        public void WriteReadLong()
        {
            var ndw = new NetDataWriter();
            ndw.Put(64L);

            var ndr = new NetDataReader(ndw);
            var readLong = ndr.GetLong();

            Assert.AreEqual(readLong, 64L);
        }

        [Test]
        public void WriteReadLongArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1L, 2L, 3L, 4L, long.MaxValue, long.MinValue});

            var ndr = new NetDataReader(ndw);
            var readLongArray = ndr.GetLongArray();

            Assert.That(new[] {1L, 2L, 3L, 4L, long.MaxValue, long.MinValue}, Is.EqualTo(readLongArray).AsCollection);
        }

        [Test]
        public void WriteReadNetEndPoint()
        {
            var ndw = new NetDataWriter();
            ndw.Put(NetUtils.MakeEndPoint("127.0.0.1", 7777));

            var ndr = new NetDataReader(ndw);
            var readNetEndPoint = ndr.GetIPEndPoint();

            Assert.AreEqual(readNetEndPoint, NetUtils.MakeEndPoint("127.0.0.1", 7777));
        }

        [Test]
        public void WriteReadSByte()
        {
            var ndw = new NetDataWriter();
            ndw.Put((sbyte) 8);

            var ndr = new NetDataReader(ndw);
            var readSByte = ndr.GetSByte();

            Assert.AreEqual(readSByte, (sbyte) 8);
        }

        [Test]
        public void WriteReadShort()
        {
            var ndw = new NetDataWriter();
            ndw.Put((short) 16);

            var ndr = new NetDataReader(ndw);
            var readShort = ndr.GetShort();

            Assert.AreEqual(readShort, (short) 16);
        }

        [Test]
        public void WriteReadShortArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new short[] {1, 2, 3, 4, 5, 6, short.MaxValue, short.MinValue});

            var ndr = new NetDataReader(ndw);
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

            var ndr = new NetDataReader(ndw);
            var readString = ndr.GetString(10);

            Assert.AreEqual(readString, "String");
        }

        [Test]
        public void WriteReadStringArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {"First", "Second", "Third", "Fourth"});

            var ndr = new NetDataReader(ndw);
            var readStringArray = ndr.GetStringArray(10);

            Assert.That(new[] {"First", "Second", "Third", "Fourth"}, Is.EqualTo(readStringArray).AsCollection);
        }

        [Test]
        public void WriteReadUInt()
        {
            var ndw = new NetDataWriter();
            ndw.Put(34U);

            var ndr = new NetDataReader(ndw);
            var readUInt = ndr.GetUInt();

            Assert.AreEqual(readUInt, 34U);
        }

        [Test]
        public void WriteReadUIntArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1U, 2U, 3U, 4U, 5U, 6U, uint.MaxValue, uint.MinValue});

            var ndr = new NetDataReader(ndw);
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

            var ndr = new NetDataReader(ndw);
            var readULong = ndr.GetULong();

            Assert.AreEqual(readULong, 64UL);
        }

        [Test]
        public void WriteReadULongArray()
        {
            var ndw = new NetDataWriter();
            ndw.PutArray(new[] {1UL, 2UL, 3UL, 4UL, 5UL, ulong.MaxValue, ulong.MinValue});

            var ndr = new NetDataReader(ndw);
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

            var ndr = new NetDataReader(ndw);
            var readUShort = ndr.GetUShort();

            Assert.AreEqual(readUShort, (ushort) 16);
        }

        [Test]
        public void WriteReadIPEndPoint()
        {
            var ndw = new NetDataWriter();
            var ipep = new IPEndPoint(IPAddress.Broadcast, 12345);
            var ipep6 = new IPEndPoint(IPAddress.IPv6Loopback, 12345);
            ndw.Put(ipep);
            ndw.Put(ipep6);

            var ndr = new NetDataReader(ndw);
            var readIpep = ndr.GetIPEndPoint();
            var readIpep6 = ndr.GetIPEndPoint();

            Assert.AreEqual(ipep, readIpep);
            Assert.AreEqual(ipep6, readIpep6);
            Assert.IsTrue(ndr.AvailableBytes == 0);
        }
    }
}

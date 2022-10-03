using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace LiteNetLib.Utils
{
    public class NetDataWriter
    {
        protected byte[] _data;
        protected int _position;
        private const int InitialSize = 64;
        private readonly bool _autoResize;

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.Length;
        }
        public byte[] Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
        }
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position;
        }

        // Cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before: 1MB GC, 30ms
        // 1000 readers after: .8MB GC, 18ms
        public static readonly UTF8Encoding uTF8Encoding = new UTF8Encoding(false, true);
        public const int StringBufferMaxLength = 1024 * 32; // <- short.MaxValue + 1
        private readonly byte[] _stringBuffer = new byte[StringBufferMaxLength];

        public NetDataWriter() : this(true, InitialSize)
        {
        }

        public NetDataWriter(bool autoResize) : this(autoResize, InitialSize)
        {
        }

        public NetDataWriter(bool autoResize, int initialSize)
        {
            _data = new byte[initialSize];
            _autoResize = autoResize;
        }

        /// <summary>
        /// Creates NetDataWriter from existing ByteArray
        /// </summary>
        /// <param name="bytes">Source byte array</param>
        /// <param name="copy">Copy array to new location or use existing</param>
        public static NetDataWriter FromBytes(byte[] bytes, bool copy)
        {
            if (copy)
            {
                var netDataWriter = new NetDataWriter(true, bytes.Length);
                netDataWriter.Put(bytes);
                return netDataWriter;
            }
            return new NetDataWriter(true, 0) {_data = bytes, _position = bytes.Length};
        }

        /// <summary>
        /// Creates NetDataWriter from existing ByteArray (always copied data)
        /// </summary>
        /// <param name="bytes">Source byte array</param>
        /// <param name="offset">Offset of array</param>
        /// <param name="length">Length of array</param>
        public static NetDataWriter FromBytes(byte[] bytes, int offset, int length)
        {
            var netDataWriter = new NetDataWriter(true, bytes.Length);
            netDataWriter.Put(bytes, offset, length);
            return netDataWriter;
        }

        public static NetDataWriter FromString(string value)
        {
            var netDataWriter = new NetDataWriter();
            netDataWriter.PutString(value);
            return netDataWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResizeIfNeed(int newSize)
        {
            if (_data.Length < newSize)
            {
                Array.Resize(ref _data, Math.Max(newSize, _data.Length * 2));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureFit(int additionalSize) => ResizeIfNeed(_position + additionalSize);

        public void Reset(int size)
        {
            ResizeIfNeed(size);
            _position = 0;
        }

        public void Reset()
        {
            _position = 0;
        }

        public byte[] CopyData()
        {
            byte[] resultData = new byte[_position];
            Buffer.BlockCopy(_data, 0, resultData, 0, _position);
            return resultData;
        }

        /// <summary>
        /// Sets position of NetDataWriter to rewrite previous values
        /// </summary>
        /// <param name="position">new byte position</param>
        /// <returns>previous position of data writer</returns>
        public int SetPosition(int position)
        {
            int prevPosition = _position;
            _position = position;
            return prevPosition;
        }

        #region Put
        public void Put(byte[] data, int offset, int length)
        {
            if (_autoResize) ResizeIfNeed(_position + length);
            Buffer.BlockCopy(data, offset, _data, _position, length);
            _position += length;
        }

        public void Put(byte[] data) => Put(data, 0, data.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Put<T>(T value, int size)
            where T : unmanaged
        {
            if (_autoResize) ResizeIfNeed(_position + size);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += size;
        }

        // ...the expressions presented in the following table are evaluated in compile time to the corresponding constant values and don't require an unsafe cont...
        // Source: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/sizeof
        public void PutByte(byte value) => Put(value, sizeof(byte));
        public void PutSByte(sbyte value) => Put(value, sizeof(sbyte));

        public const byte TRUE = 1, FALSE = 0;

        public void PutBool(bool value) => PutByte(value ? TRUE : FALSE);
        public void PutShort(short value) => Put(value, sizeof(short));
        public void PutUShort(ushort value) => Put(value, sizeof(ushort));
        public void PutChar(char value) => PutUShort(value);
        public void PutInt(int value) => Put(value, sizeof(int));
        public void PutUInt(uint value) => Put(value, sizeof(uint));
        public void PutFloat(float value) => Put(value, sizeof(float));
        public void PutLong(long value) => Put(value, sizeof(long));
        public void PutULong(ulong value) => Put(value, sizeof(ulong));
        public void PutDouble(double value) => Put(value, sizeof(double));

        /// <summary>
        /// Note that "maxLength" limits the number of characters in a string, not its size in bytes.
        /// <para>Writes "null" if "StringBufferMaxLength" is reached</para>
        /// </summary>
        public void PutString(string value, int maxLength)
        {
            if (value == null) {
                PutUShort(0);
                return;
            }

            int length = maxLength > 0 && value.Length > maxLength ? maxLength : value.Length;
            // Size in bytes
            length = uTF8Encoding.GetBytes(value, 0, length, _stringBuffer, 0);

            if (length >= StringBufferMaxLength) {
                PutUShort(0);
                return;
            }

            // Size in bytes
            PutUShort(checked((ushort)(length + 1)));
            Put(_stringBuffer, 0, length);
        }

        /// <summary>Writes "null" if "StringBufferMaxLength" is reached</summary>
        public void PutString(string value) => PutString(value, 0);

        public void PutIPEndPoint(IPEndPoint value)
        {
            PutString(value.Address.ToString());
            PutUShort((ushort)value.Port);
        }

        public void Put<T>(T value) where T : INetSerializable => value.Serialize(this);
        #endregion

        #region Array
        public void PutArray(Array value, int size)
        {
            if (value == null)
            {
                PutUShort(0);
                return;
            }
            ushort length = (ushort)value.Length;
            PutUShort(checked((ushort)(length + 1)));
            if (length == 0) return;
            size *= length;
            if (_autoResize) ResizeIfNeed(_position + size);
            Buffer.BlockCopy(value, 0, _data, _position, size);
            _position += size;
        }

        public void PutByteArray(byte[] value) => PutArray(value, sizeof(byte));
        public void PutSByteArray(sbyte[] value) => PutArray(value, sizeof(sbyte));
        public void PutBoolArray(bool[] value) => PutArray(value, sizeof(bool));
        public void PutShortArray(short[] value) => PutArray(value, sizeof(short));
        public void PutUShortArray(ushort[] value) => PutArray(value, sizeof(ushort));
        public void PutIntArray(int[] value) => PutArray(value, sizeof(int));
        public void PutUIntArray(uint[] value) => PutArray(value, sizeof(uint));
        public void PutFloatArray(float[] value) => PutArray(value, sizeof(float));
        public void PutLongArray(long[] value) => PutArray(value, sizeof(long));
        public void PutULongArray(ulong[] value) => PutArray(value, sizeof(ulong));
        public void PutDoubleArray(double[] value) => PutArray(value, sizeof(double));

        /// <summary>Note that "maxLength" limits the number of characters in a string, not its size in bytes.</summary>
        public void PutStringArray(string[] value, int stringMaxLength)
        {
            if (value == null)
            {
                PutUShort(0);
                return;
            }
            ushort length = (ushort)value.Length;
            PutUShort(checked((ushort)(length + 1)));
            if (length == 0) return;
            for (int i = 0; i < length; i++) {
                PutString(value[i], stringMaxLength);
            }
        }

        public void PutStringArray(string[] value) => PutStringArray(value, 0);
        #endregion

        #region Obsolete

        [Obsolete("Use PutFloat instead")]
        public void Put(float value)
        {
            PutFloat(value);
        }

        [Obsolete("Use PutDouble instead")]
        public void Put(double value)
        {
            PutDouble(value);
        }

        [Obsolete("Use PutLong instead")]
        public void Put(long value)
        {
            PutLong(value);
        }

        [Obsolete("Use PutULong instead")]
        public void Put(ulong value)
        {
            PutULong(value);
        }

        [Obsolete("Use PutInt instead")]
        public void Put(int value)
        {
            PutInt(value);
        }

        [Obsolete("Use PutUInt instead")]
        public void Put(uint value)
        {
            PutUInt(value);
        }

        [Obsolete("Use PutChar instead")]
        public void Put(char value)
        {
            PutChar(value);
        }

        [Obsolete("Use PutUShort instead")]
        public void Put(ushort value)
        {
            PutUShort(value);
        }

        [Obsolete("Use PutShort instead")]
        public void Put(short value)
        {
            PutShort(value);
        }

        [Obsolete("Use PutSByte instead")]
        public void Put(sbyte value)
        {
            PutSByte(value);
        }

        [Obsolete("Use PutByte instead")]
        public void Put(byte value)
        {
            PutByte(value);
        }

        [Obsolete("Use PutSByteArray instead")]
        public void PutSBytesWithLength(sbyte[] data, int offset, ushort length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2 + length);
            FastBitConverter.GetBytes(_data, _position, length);
            Buffer.BlockCopy(data, offset, _data, _position + 2, length);
            _position += 2 + length;
        }

        [Obsolete("Use PutSByteArray instead")]
        public void PutSBytesWithLength(sbyte[] value)
        {
            PutSByteArray(value);
        }

        [Obsolete("Use PutByteArray instead")]
        public void PutBytesWithLength(byte[] data, int offset, ushort length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2 + length);
            FastBitConverter.GetBytes(_data, _position, length);
            Buffer.BlockCopy(data, offset, _data, _position + 2, length);
            _position += 2 + length;
        }

        [Obsolete("Use PutByteArray instead")]
        public void PutBytesWithLength(byte[] value)
        {
            PutByteArray(value);
        }

        [Obsolete("Use PutBool instead")]
        public void Put(bool value)
        {
            PutBool(value);
        }

        [Obsolete("Use PutFloatArray instead")]
        public void PutArray(float[] value)
        {
            PutFloatArray(value);
        }

        [Obsolete("Use PutDoubleArray instead")]
        public void PutArray(double[] value)
        {
            PutDoubleArray(value);
        }

        [Obsolete("Use PutLongArray instead")]
        public void PutArray(long[] value)
        {
            PutLongArray(value);
        }

        [Obsolete("Use PutULongArray instead")]
        public void PutArray(ulong[] value)
        {
            PutULongArray(value);
        }

        [Obsolete("Use PutIntArray instead")]
        public void PutArray(int[] value)
        {
            PutIntArray(value);
        }

        [Obsolete("Use PutUIntArray instead")]
        public void PutArray(uint[] value)
        {
            PutUIntArray(value);
        }

        [Obsolete("Use PutUShortArray instead")]
        public void PutArray(ushort[] value)
        {
            PutUShortArray(value);
        }

        [Obsolete("Use PutShortArray instead")]
        public void PutArray(short[] value)
        {
            PutShortArray(value);
        }

        [Obsolete("Use PutBoolArray instead")]
        public void PutArray(bool[] value)
        {
            PutBoolArray(value);
        }

        [Obsolete("Use PutStringArray instead")]
        public void PutArray(string[] value)
        {
            PutStringArray(value);
        }

        [Obsolete("Use PutStringArray instead")]
        public void PutArray(string[] value, int strMaxLength)
        {
            PutStringArray(value, strMaxLength);
        }

        [Obsolete("Use PutIPEndPoint instead")]
        public void Put(IPEndPoint endPoint)
        {
            PutIPEndPoint(endPoint);
        }

        [Obsolete("Use PutString instead")]
        public void Put(string value)
        {
            PutString(value);
        }

        /// <summary>
        /// Note that "maxLength" only limits the number of characters in a string, not its size in bytes.
        /// </summary>
        [Obsolete("Use PutString instead")]
        public void Put(string value, int maxLength)
        {
            PutString(value, maxLength);
        }

        #endregion
    }
}

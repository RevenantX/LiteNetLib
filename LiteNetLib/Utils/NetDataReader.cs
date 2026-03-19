using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LiteNetLib.Utils
{
    public class NetDataReader
    {
        protected byte[] _data;
        protected int _position;
        protected int _dataSize;
        protected int _offset;

        private const int IPv4Size = 4;
        private const int IPv6Size = 16;

        public byte[] RawData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
        }
        public int RawDataSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize;
        }
        public int UserDataOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _offset;
        }
        public int UserDataSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize - _offset;
        }
        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null;
        }
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position;
        }
        public bool EndOfData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position == _dataSize;
        }
        public int AvailableBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize - _position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureAvailable(int count)
        {
            if ((uint)count > (uint)AvailableBytes)
                ThrowNotEnoughData(count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowNotEnoughData(int count)
        {
            throw new InvalidOperationException(
                $"Not enough data to read {count} byte(s). Position={_position}, DataSize={_dataSize}");
        }

        public void SkipBytes(int count)
        {
            EnsureAvailable(count);
            _position += count;
        }

        public void SetPosition(int position)
        {
            if ((uint)position > (uint)_dataSize)
                throw new ArgumentOutOfRangeException(nameof(position));

            _position = position;
        }

        public void SetSource(NetDataWriter dataWriter)
        {
            _data = dataWriter.Data;
            _position = 0;
            _offset = 0;
            _dataSize = dataWriter.Length;
        }

        public void SetSource(byte[] source)
        {
            _data = source;
            _position = 0;
            _offset = 0;
            _dataSize = source.Length;
        }

        public void SetSource(byte[] source, int offset, int endOffset)
        {
            _data = source;
            _position = offset;
            _offset = offset;
            _dataSize = endOffset;
        }

        public NetDataReader() { }

        public NetDataReader(NetDataWriter writer) => SetSource(writer);

        public NetDataReader(byte[] source) => SetSource(source);

        public NetDataReader(byte[] source, int offset, int endOffset) => SetSource(source, offset, endOffset);

        #region GetMethods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Get<T>(out T result) where T : struct, INetSerializable
            => result = Get<T>();

        public T Get<T>() where T : struct, INetSerializable
        {
            var obj = default(T);
            obj.Deserialize(this);
            return obj;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Get<T>(out T result, Func<T> constructor) where T : class, INetSerializable
            => result = Get(constructor);

        public T Get<T>(Func<T> constructor) where T : class, INetSerializable
        {
            var obj = constructor();
            obj.Deserialize(this);
            return obj;
        }

        public void Get(out IPEndPoint result) => result = GetIPEndPoint();

        public IPEndPoint GetIPEndPoint()
        {
            bool isIPv4 = GetByte() == 0;

            int size = isIPv4 ? IPv4Size : IPv6Size;
            EnsureAvailable(size);

            IPAddress address = new IPAddress(new ReadOnlySpan<byte>(_data, _position, size));
            _position += size;
            return new IPEndPoint(address, GetUShort());
        }

        public T[] GetArray<T>() where T : INetSerializable, new()
        {
            ushort length = GetUShort();

            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                var item = new T();
                item.Deserialize(this);
                result[i] = item;
            }

            return result;
        }

        public T[] GetArray<T>(Func<T> constructor) where T : class, INetSerializable
        {
            ushort length = GetUShort();

            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                Get(out result[i], constructor);
            }

            return result;
        }

        public bool GetBool() => GetByte() == 1;
        public void Get(out bool result) => result = GetBool();
        public bool[] GetBoolArray() => GetUnmanagedArray<bool>();

        public byte GetByte() => GetUnmanaged<byte>();
        public void Get(out byte result) => result = GetByte();
        public byte[] GetBytesWithLength() => GetUnmanagedArray<byte>();

        public sbyte GetSByte() => GetUnmanaged<sbyte>();
        public void Get(out sbyte result) => result = GetSByte();
        public sbyte[] GetSBytesWithLength() => GetUnmanagedArray<sbyte>();

        public char GetChar() => GetUnmanaged<char>();
        public void Get(out char result) => result = GetChar();
        public char[] GetCharArray() => GetUnmanagedArray<char>();

        public short GetShort() => GetUnmanaged<short>();
        public void Get(out short result) => result = GetShort();
        public short[] GetShortArray() => GetUnmanagedArray<short>();

        public ushort GetUShort() => GetUnmanaged<ushort>();
        public void Get(out ushort result) => result = GetUShort();
        public ushort[] GetUShortArray() => GetUnmanagedArray<ushort>();

        public int GetInt() => GetUnmanaged<int>();
        public void Get(out int result) => result = GetInt();
        public int[] GetIntArray() => GetUnmanagedArray<int>();

        public uint GetUInt() => GetUnmanaged<uint>();
        public void Get(out uint result) => result = GetUInt();
        public uint[] GetUIntArray() => GetUnmanagedArray<uint>();

        public float GetFloat() => GetUnmanaged<float>();
        public void Get(out float result) => result = GetFloat();
        public float[] GetFloatArray() => GetUnmanagedArray<float>();

        public long GetLong() => GetUnmanaged<long>();
        public void Get(out long result) => result = GetLong();
        public long[] GetLongArray() => GetUnmanagedArray<long>();

        public ulong GetULong() => GetUnmanaged<ulong>();
        public void Get(out ulong result) => result = GetULong();
        public ulong[] GetULongArray() => GetUnmanagedArray<ulong>();

        public double GetDouble() => GetUnmanaged<double>();
        public void Get(out double result) => result = GetDouble();
        public double[] GetDoubleArray() => GetUnmanagedArray<double>();

        public Guid GetGuid() => GetUnmanaged<Guid>();
        public void Get(out Guid result) => result = GetGuid();
        public Guid[] GetGuidArray() => GetUnmanagedArray<Guid>();

        public void Get(out string result) => result = GetString();
        public void Get(out string result, int maxLength) => result = GetString(maxLength);

        public string GetString()
        {
            ushort size = GetUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            EnsureAvailable(actualSize);

            string result = NetDataWriter.uTF8Encoding.GetString(_data, _position, actualSize);
            _position += actualSize;
            return result;
        }

        /// <summary>
        /// Note that "maxLength" only limits the number of characters in a string, not its size in bytes.
        /// </summary>
        /// <returns>"string.Empty" if value > "maxLength"</returns>
        public string GetString(int maxLength)
        {
            ushort size = GetUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            EnsureAvailable(actualSize);

            string result = maxLength > 0 &&
                            NetDataWriter.uTF8Encoding.GetCharCount(_data, _position, actualSize) > maxLength
                ? string.Empty
                : NetDataWriter.uTF8Encoding.GetString(_data, _position, actualSize);

            _position += actualSize;
            return result;
        }

        public string[] GetStringArray()
        {
            ushort length = GetUShort();
            EnsureAvailable(checked(length * sizeof(ushort))); // 2 bytes (ushort) for string length

            string[] result = new string[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = GetString();
            }

            return result;
        }

        /// <summary>
        /// Note that "maxStringLength" only limits the number of characters in a string, not its size in bytes.
        /// Strings that exceed this parameter are returned as empty
        /// </summary>
        public string[] GetStringArray(int maxStringLength)
        {
            ushort length = GetUShort();
            EnsureAvailable(checked(length * sizeof(ushort))); // 2 bytes (ushort) for string length

            string[] result = new string[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = GetString(maxStringLength);
            }

            return result;
        }

        public string GetLargeString()
        {
            int size = GetInt();
            if (size <= 0)
                return string.Empty;

            EnsureAvailable(size);

            string result = NetDataWriter.uTF8Encoding.GetString(_data, _position, size);
            _position += size;
            return result;
        }

        public ArraySegment<byte> GetBytesSegment(int count)
        {
            EnsureAvailable(count);
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, count);
            _position += count;
            return segment;
        }

        public ArraySegment<byte> GetRemainingBytesSegment()
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return segment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetRemainingBytesSpan()
        {
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetRemainingBytesMemory()
        {
            ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return memory;
        }

        public byte[] GetRemainingBytes()
        {
            byte[] result = _data.AsSpan(_position, AvailableBytes).ToArray();
            _position = _dataSize;
            return result;
        }

        public void GetBytes(byte[] destination, int start, int count)
        {
            EnsureAvailable(count);
            _data.AsSpan(_position, count).CopyTo(destination.AsSpan(start, count));
            _position += count;
        }

        public void GetBytes(byte[] destination, int count)
        {
            EnsureAvailable(count);
            _data.AsSpan(_position, count).CopyTo(destination.AsSpan(0, count));
            _position += count;
        }

        #endregion

        #region PeekMethods

        public bool PeekBool() => PeekByte() == 1;
        public byte PeekByte() => PeekUnmanaged<byte>();
        public sbyte PeekSByte() => PeekUnmanaged<sbyte>();

        public char PeekChar() => PeekUnmanaged<char>();
        public short PeekShort() => PeekUnmanaged<short>();
        public ushort PeekUShort() => PeekUnmanaged<ushort>();

        public int PeekInt() => PeekUnmanaged<int>();
        public uint PeekUInt() => PeekUnmanaged<uint>();
        public float PeekFloat() => PeekUnmanaged<float>();

        public long PeekLong() => PeekUnmanaged<long>();
        public ulong PeekULong() => PeekUnmanaged<ulong>();
        public double PeekDouble() => PeekUnmanaged<double>();

        public Guid PeekGuid() => PeekUnmanaged<Guid>();

        /// <summary>
        /// Note that "maxLength" only limits the number of characters in a string, not its size in bytes.
        /// </summary>
        public string PeekString(int maxLength)
        {
            ushort size = PeekUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            EnsureAvailable(sizeof(ushort) + actualSize);

            return maxLength > 0 &&
                   NetDataWriter.uTF8Encoding.GetCharCount(_data, _position + sizeof(ushort), actualSize) > maxLength
                ? string.Empty
                : NetDataWriter.uTF8Encoding.GetString(_data, _position + sizeof(ushort), actualSize);
        }

        public string PeekString()
        {
            ushort size = PeekUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            EnsureAvailable(sizeof(ushort) + actualSize);

            return NetDataWriter.uTF8Encoding.GetString(_data, _position + sizeof(ushort), actualSize);
        }

        #endregion

        #region TryGetMethods

        public bool TryGetBool(out bool result)
        {
            if (!TryGetByte(out byte value))
            {
                result = default;
                return false;
            }

            result = value == 1;
            return true;
        }

        public bool TryGetByte(out byte result) => TryGetUnmanaged(out result);
        public bool TryGetSByte(out sbyte result) => TryGetUnmanaged(out result);

        public bool TryGetChar(out char result) => TryGetUnmanaged(out result);
        public bool TryGetShort(out short result) => TryGetUnmanaged(out result);
        public bool TryGetUShort(out ushort result) => TryGetUnmanaged(out result);

        public bool TryGetInt(out int result) => TryGetUnmanaged(out result);
        public bool TryGetUInt(out uint result) => TryGetUnmanaged(out result);
        public bool TryGetFloat(out float result) => TryGetUnmanaged(out result);

        public bool TryGetLong(out long result) => TryGetUnmanaged(out result);
        public bool TryGetULong(out ulong result) => TryGetUnmanaged(out result);
        public bool TryGetDouble(out double result) => TryGetUnmanaged(out result);

        public bool TryGetGuid(out Guid result) => TryGetUnmanaged(out result);

        public bool TryGetString(out string result)
        {
            if (AvailableBytes < sizeof(ushort))
            {
                result = null;
                return false;
            }

            ushort size = PeekUShort();
            int actualSize = size == 0 ? 0 : size - 1;

            if (AvailableBytes < sizeof(ushort) + actualSize)
            {
                result = null;
                return false;
            }

            result = GetString();
            return true;
        }

        public bool TryGetStringArray(out string[] result)
        {
            if (AvailableBytes < sizeof(ushort))
            {
                result = null;
                return false;
            }

            int startPosition = _position;

            ushort length = PeekUShort();
            if (AvailableBytes < sizeof(ushort) + checked(length * sizeof(ushort)))
            {
                result = null;
                return false;
            }
            _position += sizeof(ushort);

            string[] values = new string[length];
            for (int i = 0; i < length; i++)
            {
                if (!TryGetString(out values[i]))
                {
                    _position = startPosition;
                    result = null;
                    return false;
                }
            }

            result = values;
            return true;
        }

        public bool TryGetBytesWithLength(out byte[] result)
        {
            if (AvailableBytes < sizeof(ushort))
            {
                result = null;
                return false;
            }

            ushort length = PeekUShort();
            if (AvailableBytes < sizeof(ushort) + length)
            {
                result = null;
                return false;
            }

            result = GetBytesWithLength();
            return true;
        }

        #endregion

        #region Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T PeekUnmanaged<T>() where T : unmanaged
        {
            EnsureAvailable(sizeof(T));
            return Unsafe.ReadUnaligned<T>(ref _data[_position]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T GetUnmanaged<T>() where T : unmanaged
        {
            int size = sizeof(T);
            EnsureAvailable(size);

            T value = Unsafe.ReadUnaligned<T>(ref _data[_position]);
            _position += size;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool TryGetUnmanaged<T>(out T result) where T : unmanaged
        {
            int size = sizeof(T);
            if (size <= AvailableBytes)
            {
                result = Unsafe.ReadUnaligned<T>(ref _data[_position]);
                _position += size;
                return true;
            }

            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T[] GetUnmanagedArray<T>() where T : unmanaged
        {
            ushort length = GetUShort();

            int byteLength = checked(length * sizeof(T));
            EnsureAvailable(byteLength);

            T[] result = new T[length];
            if (byteLength != 0)
            {
                MemoryMarshal.Cast<byte, T>(_data.AsSpan(_position, byteLength))
                    .CopyTo(result.AsSpan());
            }

            _position += byteLength;
            return result;
        }

        #endregion

        public void Clear()
        {
            _position = 0;
            _offset = 0;
            _dataSize = 0;
            _data = null;
        }
    }
}

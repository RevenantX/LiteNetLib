using System;
using System.Net;
using System.Net.Sockets;
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
        private const int GuidSize = 16;

        /// <summary>
        /// Gets the internal <see cref="byte"/> array containing the raw network data.
        /// </summary>
        public byte[] RawData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
        }

        /// <summary>
        /// Gets the total size of the <see cref="RawData"/> buffer.
        /// </summary>
        public int RawDataSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize;
        }

        /// <summary>
        /// Gets the starting offset of the user payload within the <see cref="RawData"/>.
        /// </summary>
        public int UserDataOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _offset;
        }

        /// <summary>
        /// Gets the size of the user payload excluding the initial <see cref="UserDataOffset"/>.
        /// </summary>
        public int UserDataSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize - _offset;
        }

        /// <summary>
        /// Gets a value indicating whether the internal data buffer is <see langword="null"/>.
        /// </summary>
        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null;
        }

        /// <summary>
        /// Gets the current read position within the buffer.
        /// </summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position;
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Position"/> has reached the end of the data.
        /// </summary>
        public bool EndOfData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position == _dataSize;
        }

        /// <summary>
        /// Gets the number of <see cref="byte"/>s remaining to be read.
        /// </summary>
        public int AvailableBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize - _position;
        }

        /// <summary>
        /// Verifies that the buffer has at least <paramref name="count"/> <see cref="byte"/>s available to read.
        /// </summary>
        /// <param name="count">The number of <see cref="byte"/>s required.</param>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="count"/> exceeds <see cref="AvailableBytes"/> or is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureAvailable(int count)
        {
            int available = _dataSize - _position;
            if ((uint)count > (uint)available)
                ThrowNotEnoughData(count);
        }

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> indicating that there is not enough data to read.
        /// </summary>
        /// <param name="count">The number of bytes that were attempted to be read.</param>
        /// <exception cref="InvalidOperationException">Always thrown.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowNotEnoughData(int count)
        {
            throw new InvalidOperationException(
                $"Not enough data to read {count} byte(s). Position={_position}, DataSize={_dataSize}");
        }

        /// <summary>
        /// Advances the <see cref="Position"/> by the specified <paramref name="count"/>.
        /// </summary>
        /// <param name="count">The number of <see cref="byte"/>s to skip.</param>
        public void SkipBytes(int count) => _position += count;

        /// <summary>
        /// Sets the current read <see cref="Position"/> to a specific index.
        /// </summary>
        /// <param name="position">The index to move the <see cref="Position"/> to.</param>
        public void SetPosition(int position) => _position = position;

        /// <summary>
        /// Reinitializes the reader using data from a <see cref="NetDataWriter"/>.
        /// </summary>
        /// <param name="dataWriter">The source <see cref="NetDataWriter"/>.</param>
        public void SetSource(NetDataWriter dataWriter)
        {
            _data = dataWriter.Data;
            _position = 0;
            _offset = 0;
            _dataSize = dataWriter.Length;
        }

        /// <summary>
        /// Reinitializes the reader using a <see cref="byte"/> array.
        /// </summary>
        /// <param name="source">The source <see cref="byte"/> array.</param>
        public void SetSource(byte[] source)
        {
            _data = source;
            _position = 0;
            _offset = 0;
            _dataSize = source.Length;
        }

        /// <summary>
        /// Reinitializes the reader using a segment of a <see cref="byte"/> array.
        /// </summary>
        /// <param name="source">The source <see cref="byte"/> array.</param>
        /// <param name="offset">The starting index for reading.</param>
        /// <param name="maxSize">The total number of <see cref="byte"/>s available to read from the <paramref name="source"/>.</param>
        public void SetSource(byte[] source, int offset, int maxSize)
        {
            _data = source;
            _position = offset;
            _offset = offset;
            _dataSize = maxSize;
        }

        public NetDataReader() { }

        public NetDataReader(NetDataWriter writer) => SetSource(writer);

        public NetDataReader(byte[] source) => SetSource(source);

        public NetDataReader(byte[] source, int offset, int maxSize) => SetSource(source, offset, maxSize);

        #region GetMethods

        /// <summary>
        /// Deserializes a <see langword="struct"/> that implements <see cref="INetSerializable"/>.
        /// </summary>
        /// <typeparam name="T">A <see langword="struct"/> type implementing <see cref="INetSerializable"/>.</typeparam>
        /// <param name="result">The deserialized <see langword="struct"/> output.</param>
        public void Get<T>(out T result) where T : struct, INetSerializable
        {
            result = default;
            result.Deserialize(this);
        }

        /// <summary>
        /// Deserializes a <see langword="class"/> that implements <see cref="INetSerializable"/> using a provided constructor.
        /// </summary>
        /// <typeparam name="T">A <see langword="class"/> type implementing <see cref="INetSerializable"/>.</typeparam>
        /// <param name="result">The deserialized <see langword="class"/> instance output.</param>
        /// <param name="constructor">A factory <see langword="delegate"/> used to instantiate the <see langword="class"/>.</param>
        public void Get<T>(out T result, Func<T> constructor) where T : class, INetSerializable
        {
            result = constructor();
            result.Deserialize(this);
        }

        /// <summary>
        /// Deserializes an <see cref="IPEndPoint"/> and assigns it to the <paramref name="result"/> parameter.
        /// </summary>
        /// <param name="result">The deserialized <see cref="IPEndPoint"/> output.</param>
        public void Get(out IPEndPoint result) => result = GetIPEndPoint();

        /// <summary>
        /// Reads an <see cref="IPEndPoint"/> from the current <see cref="Position"/>.
        /// </summary>
        /// <returns>The deserialized <see cref="IPEndPoint"/>.</returns>
        /// <remarks>
        /// Reads a <see cref="byte"/> to determine the <see cref="AddressFamily"/> (0 for IPv4, 1 for IPv6),
        /// followed by the address bytes and a 2-byte <see cref="ushort"/> port.
        /// </remarks>
        public IPEndPoint GetIPEndPoint()
        {
            bool isIPv4 = GetByte() == 0;

            int size = isIPv4 ? IPv4Size : IPv6Size;
            EnsureAvailable(size);

            IPAddress address = new IPAddress(new ReadOnlySpan<byte>(_data, _position, size));
            _position += size;
            return new IPEndPoint(address, GetUShort());
        }

        /// <summary>Reads a <see cref="byte"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out byte result) => result = GetByte();

        /// <summary>Reads an <see cref="sbyte"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out sbyte result) => result = (sbyte)GetByte();

        /// <summary>Reads a <see cref="bool"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out bool result) => result = GetBool();

        /// <summary>Reads a <see cref="char"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out char result) => result = GetChar();

        /// <summary>Reads a <see cref="ushort"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out ushort result) => result = GetUShort();

        /// <summary>Reads a <see cref="short"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out short result) => result = GetShort();

        /// <summary>Reads a <see cref="ulong"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out ulong result) => result = GetULong();

        /// <summary>Reads a <see cref="long"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out long result) => result = GetLong();

        /// <summary>Reads a <see cref="uint"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out uint result) => result = GetUInt();

        /// <summary>Reads an <see cref="int"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out int result) => result = GetInt();

        /// <summary>Reads a <see cref="double"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out double result) => result = GetDouble();

        /// <summary>Reads a <see cref="float"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out float result) => result = GetFloat();

        /// <summary>Reads a <see cref="string"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out string result) => result = GetString();

        /// <summary>Reads a <see cref="string"/> with a length limit and assigns it to <paramref name="result"/>.</summary>
        public void Get(out string result, int maxLength) => result = GetString(maxLength);

        /// <summary>Reads a <see cref="Guid"/> and assigns it to <paramref name="result"/>.</summary>
        public void Get(out Guid result) => result = GetGuid();

        /// <summary>Reads the next <see cref="byte"/> from the buffer.</summary>
        public byte GetByte()
        {
            EnsureAvailable(1);
            return _data[_position++];
        }

        /// <summary>Reads the next <see cref="sbyte"/> from the buffer.</summary>
        public sbyte GetSByte() => (sbyte)GetByte();

        /// <summary>
        /// Reads an array of unmanaged values prefixed by a <see cref="ushort"/> length.
        /// </summary>
        /// <typeparam name="T">An unmanaged type.</typeparam>
        /// <returns>A new array of type <typeparamref name="T"/>.</returns>
        public unsafe T[] GetUnmanagedArray<T>() where T : unmanaged
        {
            ushort length = GetUShort();

            int byteLength = checked(length * sizeof(T));
            EnsureAvailable(byteLength);

            ReadOnlySpan<byte> slice = _data.AsSpan(_position, byteLength);
            T[] result = MemoryMarshal.Cast<byte, T>(slice)
                .ToArray();
            _position += byteLength;
            return result;
        }

        /// <summary>
        /// Reads an array of values by performing a direct memory copy.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="size">The size of a single element in <see cref="byte"/>s.</param>
        /// <returns>A new array of type <typeparamref name="T"/>.</returns>
        public T[] GetArray<T>(ushort size)
        {
            ushort length = GetUShort();

            int byteLength = checked(length * size);
            EnsureAvailable(byteLength);

            T[] result = new T[length];
            if (byteLength > 0)
                Buffer.BlockCopy(_data, _position, result, 0, byteLength);

            _position += byteLength;
            return result;
        }

        /// <summary>
        /// Reads an array of objects implementing <see cref="INetSerializable"/>.
        /// </summary>
        /// <typeparam name="T">A type with a parameterless constructor implementing <see cref="INetSerializable"/>.</typeparam>
        /// <returns>A new array of type <typeparamref name="T"/>.</returns>
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

        /// <summary>
        /// Reads an array of objects implementing <see cref="INetSerializable"/> using a specific constructor.
        /// </summary>
        /// <typeparam name="T">A <see langword="class"/> type implementing <see cref="INetSerializable"/>.</typeparam>
        /// <param name="constructor">The factory <see langword="delegate"/> used to create instances.</param>
        /// <returns>A new array of type <typeparamref name="T"/>.</returns>
        public T[] GetArray<T>(Func<T> constructor) where T : class, INetSerializable
        {
            ushort length = GetUShort();
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
                Get(out result[i], constructor);
            return result;
        }

        /// <summary>Reads an array of <see cref="bool"/> values.</summary>
        public bool[] GetBoolArray() => GetUnmanagedArray<bool>();

        /// <summary>Reads an array of <see cref="ushort"/> values.</summary>
        public ushort[] GetUShortArray() => GetUnmanagedArray<ushort>();

        /// <summary>Reads an array of <see cref="short"/> values.</summary>
        public short[] GetShortArray() => GetUnmanagedArray<short>();

        /// <summary>Reads an array of <see cref="int"/> values.</summary>
        public int[] GetIntArray() => GetUnmanagedArray<int>();

        /// <summary>Reads an array of <see cref="uint"/> values.</summary>
        public uint[] GetUIntArray() => GetUnmanagedArray<uint>();

        /// <summary>Reads an array of <see cref="float"/> values.</summary>
        public float[] GetFloatArray() => GetUnmanagedArray<float>();

        /// <summary>Reads an array of <see cref="double"/> values.</summary>
        public double[] GetDoubleArray() => GetUnmanagedArray<double>();

        /// <summary>Reads an array of <see cref="long"/> values.</summary>
        public long[] GetLongArray() => GetUnmanagedArray<long>();

        /// <summary>Reads an array of <see cref="ulong"/> values.</summary>
        public ulong[] GetULongArray() => GetUnmanagedArray<ulong>();

        /// <summary>
        /// Reads an array of <see cref="string"/> values.
        /// </summary>
        /// <returns>A new <see cref="string"/> array.</returns>
        /// <remarks>
        /// Reads a 2-byte <see cref="ushort"/> length header followed by each <see cref="string"/> element.
        /// </remarks>
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
        /// Reads an array of <see cref="string"/> values with a maximum character limit per element.
        /// </summary>
        /// <param name="maxStringLength">The maximum number of characters allowed per <see cref="string"/>.</param>
        /// <returns>A new <see cref="string"/> array.</returns>
        /// <remarks>
        /// Strings exceeding <paramref name="maxStringLength"/> are returned as <see cref="string.Empty"/>.
        /// </remarks>
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

        /// <summary>Reads a <see cref="bool"/> value from the current position.</summary>
        /// <returns><see langword="true"/> if the byte is 1; otherwise, <see langword="false"/>.</returns>
        public bool GetBool() => GetByte() == 1;

        /// <summary>Reads a <see cref="char"/> value as a 2-byte <see cref="ushort"/>.</summary>
        public char GetChar() => (char)GetUShort();

        /// <summary>Reads a <see cref="ushort"/> value using unmanaged memory access.</summary>
        public ushort GetUShort() => GetUnmanaged<ushort>();

        /// <summary>Reads a <see cref="short"/> value using unmanaged memory access.</summary>
        public short GetShort() => GetUnmanaged<short>();

        /// <summary>Reads a <see cref="long"/> value using unmanaged memory access.</summary>
        public long GetLong() => GetUnmanaged<long>();

        /// <summary>Reads a <see cref="ulong"/> value using unmanaged memory access.</summary>
        public ulong GetULong() => GetUnmanaged<ulong>();

        /// <summary>Reads an <see cref="int"/> value using unmanaged memory access.</summary>
        public int GetInt() => GetUnmanaged<int>();

        /// <summary>Reads a <see cref="uint"/> value using unmanaged memory access.</summary>
        public uint GetUInt() => GetUnmanaged<uint>();

        /// <summary>Reads a <see cref="float"/> value using unmanaged memory access.</summary>
        public float GetFloat() => GetUnmanaged<float>();

        /// <summary>Reads a <see cref="double"/> value using unmanaged memory access.</summary>
        public double GetDouble() => GetUnmanaged<double>();

        /// <summary>
        /// Reads a <see cref="string"/> with a maximum character limit.
        /// </summary>
        /// <param name="maxLength">The maximum allowed character count.</param>
        /// <returns>The deserialized <see cref="string"/>, or <see cref="string.Empty"/> if the character count exceeds <paramref name="maxLength"/>.</returns>
        /// <remarks>
        /// Note that <paramref name="maxLength"/> limits the number of characters, not the total size in <see cref="byte"/>s.
        /// </remarks>
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

        /// <summary>
        /// Reads a <see cref="string"/> from the current position.
        /// </summary>
        /// <returns>The deserialized <see cref="string"/>.</returns>
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
        /// Reads a <see cref="string"/> prefixed with a 4-byte <see cref="int"/> length header.
        /// </summary>
        /// <returns>The deserialized <see cref="string"/>.</returns>
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

        /// <summary>
        /// Reads a 16-byte <see cref="Guid"/>.
        /// </summary>
        /// <returns>The deserialized <see cref="Guid"/>.</returns>
        public Guid GetGuid()
        {
            EnsureAvailable(GuidSize);
            var result = new Guid(_data.AsSpan(_position, GuidSize));
            _position += GuidSize;
            return result;
        }

        /// <summary>
        /// Gets an <see cref="ArraySegment{T}"/> of <see cref="byte"/>s from the current position.
        /// </summary>
        /// <param name="count">The number of <see cref="byte"/>s to include in the segment.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> wrapping the internal buffer.</returns>
        public ArraySegment<byte> GetBytesSegment(int count)
        {
            EnsureAvailable(count);
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, count);
            _position += count;
            return segment;
        }

        /// <summary>
        /// Gets an <see cref="ArraySegment{T}"/> containing all remaining <see cref="byte"/>s.
        /// </summary>
        /// <returns>An <see cref="ArraySegment{T}"/> from the current position to the end of the data.</returns>
        public ArraySegment<byte> GetRemainingBytesSegment()
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return segment;
        }

        /// <summary>
        /// Deserializes a <see langword="struct"/> that implements <see cref="INetSerializable"/>.
        /// </summary>
        /// <typeparam name="T">A <see langword="struct"/> type implementing <see cref="INetSerializable"/>.</typeparam>
        /// <returns>The deserialized <see langword="struct"/>.</returns>
        public T Get<T>() where T : struct, INetSerializable
        {
            Get(out T result);
            return result;
        }

        /// <summary>
        /// Deserializes a <see langword="class"/> that implements <see cref="INetSerializable"/> using a provided constructor.
        /// </summary>
        /// <typeparam name="T">A <see langword="class"/> type implementing <see cref="INetSerializable"/>.</typeparam>
        /// <param name="constructor">The factory <see langword="delegate"/> used to instantiate the <see langword="class"/>.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        public T Get<T>(Func<T> constructor) where T : class, INetSerializable
        {
            Get(out T result, constructor);
            return result;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/>s containing all remaining data.
        /// </summary>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> from the current <see cref="Position"/> to the end of the buffer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetRemainingBytesSpan()
        {
            var result = new ReadOnlySpan<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return result;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>s containing all remaining data.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyMemory{T}"/> from the current <see cref="Position"/> to the end of the buffer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetRemainingBytesMemory()
        {
            var result = new ReadOnlyMemory<byte>(_data, _position, AvailableBytes);
            _position = _dataSize;
            return result;
        }

        /// <summary>
        /// Reads all remaining <see cref="byte"/>s and returns them as a new array.
        /// </summary>
        /// <returns>A new <see cref="byte"/> array containing the remaining data.</returns>
        /// <remarks>
        /// This method performs a heap allocation and advances the <see cref="Position"/> to the end of the data.
        /// </remarks>
        public byte[] GetRemainingBytes()
        {
            int size = _dataSize - _position;
            byte[] outgoingData = new byte[size];
            Buffer.BlockCopy(_data, _position, outgoingData, 0, size);
            _position = _dataSize;
            return outgoingData;
        }

        /// <summary>
        /// Copies a specified number of <see cref="byte"/>s into a destination array at a specific offset.
        /// </summary>
        /// <param name="destination">The array to copy data into.</param>
        /// <param name="start">The starting index in the <paramref name="destination"/> array.</param>
        /// <param name="count">The number of <see cref="byte"/>s to read.</param>
        public void GetBytes(byte[] destination, int start, int count)
        {
            EnsureAvailable(count);
            Buffer.BlockCopy(_data, _position, destination, start, count);
            _position += count;
        }

        /// <summary>
        /// Copies a specified number of <see cref="byte"/>s into a destination array starting at index 0.
        /// </summary>
        /// <param name="destination">The array to copy data into.</param>
        /// <param name="count">The number of <see cref="byte"/>s to read.</param>
        public void GetBytes(byte[] destination, int count)
        {
            EnsureAvailable(count);
            Buffer.BlockCopy(_data, _position, destination, 0, count);
            _position += count;
        }

        /// <summary>
        /// Reads an <see cref="sbyte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <returns>A new <see cref="sbyte"/> array.</returns>
        public sbyte[] GetSBytesWithLength() => GetUnmanagedArray<sbyte>();

        /// <summary>
        /// Reads a <see cref="byte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <returns>A new <see cref="byte"/> array.</returns>
        public byte[] GetBytesWithLength() => GetUnmanagedArray<byte>();

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the internal byte buffer at the current position,
        /// advancing the position by the size of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">An unmanaged value type to read from the buffer.</typeparam>
        /// <returns>The value of type <typeparamref name="T"/> read from the buffer.</returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T GetUnmanaged<T>() where T : unmanaged
        {
            var size = sizeof(T);
            EnsureAvailable(size);

#if NET8_0_OR_GREATER
            var value = Unsafe.ReadUnaligned<T>(ref _data[_position]);
#else
            T value;
            fixed (byte* ptr = &_data[_position])
            {
                value = *(T*)ptr;
            }
#endif

            _position += size;
            return value;
        }

        /// <summary>
        /// Reads a nullable value of type <typeparamref name="T"/> from the internal byte buffer at the current position,
        /// first reading a <see cref="bool"/> indicating whether the value is present,
        /// and then reading the value itself if it exists. <br/>
        /// Advances the position by 1 byte for the presence flag plus the size of <typeparamref name="T"/> if the value is present.
        /// </summary>
        /// <typeparam name="T">An unmanaged value type to read from the buffer.</typeparam>
        /// <returns>
        /// The nullable value of type <typeparamref name="T"/> read from the buffer.
        /// Returns <see langword="null"/> if the presence flag indicates no value.
        /// </returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetNullableUnmanaged<T>() where T : unmanaged
        {
            var hasValue = GetBool();
            if (!hasValue)
            {
                return null;
            }

            return GetUnmanaged<T>();
        }

        /// <summary>
        /// Reads an enum value of type <typeparamref name="T"/> from the internal data buffer at the current position. <br/>
        /// Advances the position by the size of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">An unmanaged enum type to read.</typeparam>
        /// <returns>The enum value read from the buffer.</returns>
        public unsafe T GetEnum<T>() where T : unmanaged, Enum
        {
            int size = sizeof(T);
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(_data, _position, size);
            _position += size;
#if NET8_0_OR_GREATER
            return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span));
#else
            fixed (byte* ptr = span)
            {
                return *(T*)ptr;
            }
#endif
        }

        #endregion

        #region PeekMethods

        /// <summary>Reads the <see cref="byte"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public byte PeekByte() => _data[_position];

        /// <summary>Reads the <see cref="sbyte"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public sbyte PeekSByte() => (sbyte)_data[_position];

        /// <summary>Reads the <see cref="bool"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public bool PeekBool() => _data[_position] == 1;

        /// <summary>Reads the <see cref="char"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public char PeekChar() => PeekUnmanaged<char>();

        /// <summary>Reads the <see cref="ushort"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public ushort PeekUShort() => PeekUnmanaged<ushort>();

        /// <summary>Reads the <see cref="short"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public short PeekShort() => PeekUnmanaged<short>();

        /// <summary>Reads the <see cref="long"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public long PeekLong() => PeekUnmanaged<long>();

        /// <summary>Reads the <see cref="ulong"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public ulong PeekULong() => PeekUnmanaged<ulong>();

        /// <summary>Reads the <see cref="int"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public int PeekInt() => PeekUnmanaged<int>();

        /// <summary>Reads the <see cref="uint"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public uint PeekUInt() => PeekUnmanaged<uint>();

        /// <summary>Reads the <see cref="float"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public float PeekFloat() => PeekUnmanaged<float>();

        /// <summary>Reads the <see cref="double"/> at the current position without advancing the <see cref="Position"/>.</summary>
        public double PeekDouble() => PeekUnmanaged<double>();

        /// <summary>
        /// Reads a <see cref="string"/> with a character limit without advancing the <see cref="Position"/>.
        /// </summary>
        /// <param name="maxLength">Maximum allowed character count.</param>
        /// <remarks>Strings exceeding <paramref name="maxLength"/> are returned as <see cref="string.Empty"/>.</remarks>
        public string PeekString(int maxLength)
        {
            ushort size = PeekUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            return (maxLength > 0 && NetDataWriter.uTF8Encoding.GetCharCount(_data, _position + 2, actualSize) > maxLength)
                ? string.Empty
                : NetDataWriter.uTF8Encoding.GetString(_data, _position + 2, actualSize);
        }

        /// <summary>
        /// Reads a <see cref="string"/> without advancing the <see cref="Position"/>.
        /// </summary>
        public string PeekString()
        {
            ushort size = PeekUShort();
            if (size == 0)
                return string.Empty;

            int actualSize = size - 1;
            return NetDataWriter.uTF8Encoding.GetString(_data, _position + 2, actualSize);
        }

        /// <summary>
        /// Reads an unmanaged value of type <typeparamref name="T"/> at the current position without advancing the <see cref="Position"/>.
        /// </summary>
        /// <typeparam name="T">An unmanaged value type.</typeparam>
        /// <returns>The value read from the buffer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T PeekUnmanaged<T>() where T : unmanaged
        {
#if NET8_0_OR_GREATER
            return Unsafe.ReadUnaligned<T>(ref _data[_position]);
#else
            T value;
            fixed (byte* ptr = &_data[_position])
            {
                value = *(T*)ptr;
            }
            return value;
#endif
        }
        #endregion

        #region TryGetMethods
        /// <summary>Attempts to read a <see cref="byte"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="byte"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetByte(out byte result)
        {
            if (AvailableBytes >= 1)
            {
                result = GetByte();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read an <see cref="sbyte"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="sbyte"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetSByte(out sbyte result)
        {
            if (AvailableBytes >= 1)
            {
                result = GetSByte();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="bool"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="bool"/>, or <see langword="false"/> if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetBool(out bool result)
        {
            if (AvailableBytes >= 1)
            {
                result = GetBool();
                return true;
            }
            result = false;
            return false;
        }

        /// <summary>Attempts to read a <see cref="char"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="char"/>, or '\0' if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetChar(out char result)
        {
            if (!TryGetUShort(out ushort uShortValue))
            {
                result = '\0';
                return false;
            }
            result = (char)uShortValue;
            return true;
        }

        /// <summary>Attempts to read a <see cref="short"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="short"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetShort(out short result)
        {
            if (AvailableBytes >= 2)
            {
                result = GetShort();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="ushort"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="ushort"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetUShort(out ushort result)
        {
            if (AvailableBytes >= 2)
            {
                result = GetUShort();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read an <see cref="int"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="int"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetInt(out int result)
        {
            if (AvailableBytes >= 4)
            {
                result = GetInt();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="uint"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="uint"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetUInt(out uint result)
        {
            if (AvailableBytes >= 4)
            {
                result = GetUInt();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="long"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="long"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetLong(out long result)
        {
            if (AvailableBytes >= 8)
            {
                result = GetLong();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="ulong"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="ulong"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetULong(out ulong result)
        {
            if (AvailableBytes >= 8)
            {
                result = GetULong();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="float"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="float"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetFloat(out float result)
        {
            if (AvailableBytes >= 4)
            {
                result = GetFloat();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="double"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="double"/>, or 0 if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetDouble(out double result)
        {
            if (AvailableBytes >= 8)
            {
                result = GetDouble();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>Attempts to read a <see cref="string"/> without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="string"/>, or <see langword="null"/> if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetString(out string result)
        {
            if (AvailableBytes >= 2)
            {
                ushort strSize = PeekUShort();
                int actualSize = strSize == 0 ? 0 : strSize - 1;

                if (AvailableBytes >= 2 + actualSize)
                {
                    result = GetString();
                    return true;
                }
            }
            result = null;
            return false;
        }

        /// <summary>Attempts to read a <see cref="string"/> array without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="string"/> array, or <see langword="null"/> if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetStringArray(out string[] result)
        {
            if (!TryGetUShort(out ushort strArrayLength))
            {
                result = null;
                return false;
            }

            result = new string[strArrayLength];
            for (int i = 0; i < strArrayLength; i++)
            {
                if (!TryGetString(out result[i]))
                {
                    result = null;
                    return false;
                }
            }

            return true;
        }

        /// <summary>Attempts to read a <see cref="byte"/> array with a length header without throwing an exception.</summary>
        /// <param name="result">The deserialized <see cref="byte"/> array, or <see langword="null"/> if failed.</param>
        /// <returns><see langword="true"/> if enough data was available; otherwise, <see langword="false"/>.</returns>
        public bool TryGetBytesWithLength(out byte[] result)
        {
            if (AvailableBytes >= 2)
            {
                ushort length = PeekUShort();
                if (AvailableBytes >= 2 + length)
                {
                    result = GetBytesWithLength();
                    return true;
                }
            }
            result = null;
            return false;
        }
        #endregion

        /// <summary>Clears the reader state and releases the reference to the internal buffer.</summary>
        public void Clear()
        {
            _position = 0;
            _offset = 0;
            _dataSize = 0;
            _data = null;
        }
    }
}

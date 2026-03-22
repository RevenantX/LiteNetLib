using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteNetLib.Utils
{
    public class NetDataWriter
    {
        protected byte[] _data;
        protected int _position;
        private const int InitialSize = 64;
        private readonly bool _autoResize;

        /// <summary>
        /// Gets the total capacity of the internal <see cref="byte"/> buffer.
        /// </summary>
        /// <value>The length of the underlying <see cref="_data"/> array.</value>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.Length;
        }

        /// <summary>
        /// Gets the underlying <see cref="byte"/> array used by this writer.
        /// </summary>
        /// <value>The internal <see cref="_data"/> array.</value>
        /// <remarks>
        /// Accessing this directly allows for external manipulation but bypasses bounds checking.
        /// </remarks>
        public byte[] Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
        }

        /// <summary>
        /// Gets the current number of <see cref="byte"/>s written to the buffer.
        /// </summary>
        /// <value>The current <see cref="_position"/>.</value>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> representing the currently used portion of the internal buffer.
        /// </summary>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> from index 0 to <see cref="_position"/>.</returns>
        /// <remarks>
        /// Provides a high-performance, zero-allocation view of the data.
        /// The span becomes invalid if the internal buffer is resized or if <see cref="_position"/> changes.
        /// </remarks>
        public ReadOnlySpan<byte> AsReadOnlySpan() => new ReadOnlySpan<byte>(_data, 0, _position);

        internal static readonly UTF8Encoding uTF8Encoding = new UTF8Encoding(false, true);

        public NetDataWriter() : this(true, InitialSize) { }

        public NetDataWriter(bool autoResize) : this(autoResize, InitialSize) { }

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

        /// <summary>
        /// Creates NetDataWriter from the given <paramref name="bytes"/>.
        /// </summary>
        public static NetDataWriter FromBytes(Span<byte> bytes)
        {
            var netDataWriter = new NetDataWriter(true, bytes.Length);
            netDataWriter.Put(bytes);
            return netDataWriter;
        }

        /// <summary>
        /// Creates a new <see cref="NetDataWriter"/> and serializes a <see cref="string"/> into it.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to serialize.</param>
        /// <returns>A new <see cref="NetDataWriter"/> instance containing the serialized <see cref="string"/>.</returns>
        public static NetDataWriter FromString(string value)
        {
            var netDataWriter = new NetDataWriter();
            netDataWriter.Put(value);
            return netDataWriter;
        }

        /// <summary>
        /// Ensures the internal buffer is at least <paramref name="newSize"/>.
        /// </summary>
        /// <param name="newSize">The required minimum size of the buffer.</param>
        /// <remarks>
        /// If an allocation is necessary, the buffer grows to either <paramref name="newSize"/>
        /// or doubles its current size, whichever is larger.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResizeIfNeed(int newSize)
        {
            if (_data.Length < newSize)
            {
                Array.Resize(ref _data, Math.Max(newSize, _data.Length * 2));
            }
        }

        /// <summary>
        /// Ensures the internal buffer can accommodate <paramref name="additionalSize"/> more <see cref="byte"/>s.
        /// </summary>
        /// <param name="additionalSize">The number of additional <see cref="byte"/>s to fit.</param>
        /// <remarks>
        /// This checks against the current <see cref="_position"/>. If the capacity is insufficient,
        /// the buffer grows to either the required size or doubles its current size.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureFit(int additionalSize)
        {
            if (_data.Length < _position + additionalSize)
            {
                Array.Resize(ref _data, Math.Max(_position + additionalSize, _data.Length * 2));
            }
        }


        /// <summary>
        /// Resets the <see cref="_position"/> to 0 and ensures the internal buffer has at least the specified <paramref name="size"/>.
        /// </summary>
        /// <param name="size">The minimum capacity required for the internal buffer.</param>
        /// <remarks>
        /// If the current buffer is smaller than <paramref name="size"/>, <see cref="ResizeIfNeed(int)"/> will allocate a larger <see cref="byte"/> array.
        /// </remarks>
        public void Reset(int size)
        {
            ResizeIfNeed(size);
            _position = 0;
        }

        /// <summary>
        /// Resets the <see cref="_position"/> to 0, effectively clearing the buffer for reuse.
        /// </summary>
        public void Reset() => _position = 0;

        /// <summary>
        /// Creates a <see cref="byte"/> array containing the current data from the internal buffer.
        /// </summary>
        /// <returns>A new <see cref="byte"/> array of length <see cref="_position"/>.</returns>
        /// <remarks>
        /// This method performs a heap allocation and copies the data using <see cref="Buffer.BlockCopy(Array, int, Array, int, int)"/>.
        /// </remarks>
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

        /// <summary>
        /// Serializes a <see cref="float"/> value.
        /// </summary>
        /// <param name="value">The <see cref="float"/> value to write.</param>
        public void Put(float value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="double"/> value.
        /// </summary>
        /// <param name="value">The <see cref="double"/> value to write.</param>
        public void Put(double value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="long"/> value.
        /// </summary>
        /// <param name="value">The <see cref="long"/> value to write.</param>
        public void Put(long value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="ulong"/> value.
        /// </summary>
        /// <param name="value">The <see cref="ulong"/> value to write.</param>
        public void Put(ulong value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes an <see cref="int"/> value.
        /// </summary>
        /// <param name="value">The <see cref="int"/> value to write.</param>
        public void Put(int value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="uint"/> value.
        /// </summary>
        /// <param name="value">The <see cref="uint"/> value to write.</param>
        public void Put(uint value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="char"/> value as a <see cref="ushort"/>.
        /// </summary>
        /// <param name="value">The <see cref="char"/> value to write.</param>
        public void Put(char value) => Put((ushort)value);

        /// <summary>
        /// Serializes a <see cref="ushort"/> value.
        /// </summary>
        /// <param name="value">The <see cref="ushort"/> value to write.</param>
        public void Put(ushort value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="short"/> value.
        /// </summary>
        /// <param name="value">The <see cref="short"/> value to write.</param>
        public void Put(short value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes an <see cref="sbyte"/> value.
        /// </summary>
        /// <param name="value">The <see cref="sbyte"/> value to write.</param>
        public void Put(sbyte value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="byte"/> value.
        /// </summary>
        /// <param name="value">The <see cref="byte"/> value to write.</param>
        public void Put(byte value) => PutUnmanaged(value);

        /// <summary>
        /// Serializes a <see cref="Guid"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Guid"/> value to write.</param>
        public void Put(Guid value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 16);
            value.TryWriteBytes(_data.AsSpan(_position));
            _position += 16;
        }

        /// <summary>
        /// Serializes a segment of a <see cref="byte"/> array.
        /// </summary>
        /// <param name="data">The source array.</param>
        /// <param name="offset">The starting index in the source array.</param>
        /// <param name="length">The number of <see cref="byte"/>s to write.</param>
        public void Put(byte[] data, int offset, int length)
        {
            Put(data.AsSpan(offset, length));
        }

        /// <summary>
        /// Serializes an entire <see cref="byte"/> array.
        /// </summary>
        /// <param name="data">The source array.</param>
        public void Put(byte[] data)
        {
            Put(data.AsSpan());
        }

        /// <summary>
        /// Serializes a <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/>s to the internal buffer.
        /// </summary>
        /// <param name="data">The span of data to write.</param>
        public void Put(ReadOnlySpan<byte> data)
        {
            if (_autoResize)
                ResizeIfNeed(_position + data.Length);
            data.CopyTo(_data.AsSpan(_position));
            _position += data.Length;
        }

        /// <summary>
        /// Serializes a segment of an <see cref="sbyte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <param name="data">The source <see cref="sbyte"/> array.</param>
        /// <param name="offset">The starting index in the source array.</param>
        /// <param name="length">The number of elements to write.</param>
        public void PutSBytesWithLength(sbyte[] data, int offset, ushort length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2 + length);

            FastBitConverter.GetBytes(_data, _position, length);
            _position += 2;

            if (length > 0)
            {
                ReadOnlySpan<sbyte> source = data.AsSpan(offset, length);
                ReadOnlySpan<byte> sourceBytes = MemoryMarshal.Cast<sbyte, byte>(source);

                sourceBytes.CopyTo(_data.AsSpan(_position));
                _position += length;
            }
        }

        /// <summary>
        /// Serializes an <see cref="sbyte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <param name="data">The source array.</param>
        public void PutSBytesWithLength(sbyte[] data) => PutArray(data, 1);

        /// <summary>
        /// Serializes a segment of a <see cref="byte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <param name="data">The source <see cref="byte"/> array.</param>
        /// <param name="offset">The starting index in the source array.</param>
        /// <param name="length">The number of <see cref="byte"/>s to write.</param>
        public void PutBytesWithLength(byte[] data, int offset, ushort length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2 + length);

            FastBitConverter.GetBytes(_data, _position, length);
            _position += 2;

            if (length > 0)
            {
                data.AsSpan(offset, length).CopyTo(_data.AsSpan(_position));
                _position += length;
            }
        }

        /// <summary>
        /// Serializes a <see cref="byte"/> array prefixed with its <see cref="ushort"/> length.
        /// </summary>
        /// <param name="data">The source array.</param>
        public void PutBytesWithLength(byte[] data) => PutArray(data, 1);

        /// <summary>
        /// Serializes a <see cref="bool"/> value as a single <see cref="byte"/>.
        /// </summary>
        /// <param name="value">The <see cref="bool"/> value to write.</param>
        public void Put(bool value) => Put((byte)(value ? 1 : 0));

        /// <summary>
        /// Serializes an <see cref="Array"/> prefixed with a 2-byte <see cref="ushort"/> length.
        /// </summary>
        /// <param name="arr">The source array to serialize.</param>
        /// <param name="sz">The size of a single element in <see cref="byte"/>s.</param>
        /// <remarks>
        /// If the array is <see langword="null"/>, a length of 0 is written. <br/>
        /// The total payload size is calculated as <c>length * sz</c>.
        /// </remarks>
        public void PutArray(Array arr, int sz)
        {
            ushort length = arr == null ? (ushort)0 : (ushort)arr.Length;
            sz *= length;
            if (_autoResize)
                ResizeIfNeed(_position + sz + 2);
            FastBitConverter.GetBytes(_data, _position, length);
            if (arr != null)
                Buffer.BlockCopy(arr, 0, _data, _position + 2, sz);
            _position += sz + 2;
        }

        /// <summary>
        /// Serializes an array of unmanaged values.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of the array elements.</typeparam>
        /// <param name="arr">The array to serialize.</param>
        public void PutUnmanagedArray<T>(T[] arr) where T : unmanaged
        {
            PutSpan(arr.AsSpan());
        }

        /// <summary>
        /// Serializes a <see cref="Span{T}"/> of unmanaged values to the internal buffer.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of the span elements.</typeparam>
        /// <param name="span">The span of data to write.</param>
        /// <remarks>
        /// Writes a 2-byte <see cref="ushort"/> length header followed by the raw binary data. <br/>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void PutSpan<T>(Span<T> span) where T : unmanaged
        {
            var length = (ushort)span.Length;
            var byteLength = length * sizeof(T);

            if (_autoResize)
                ResizeIfNeed(_position + byteLength + 2);

            FastBitConverter.GetBytes(_data, _position, length);
            _position += 2;

            if (length > 0)
            {
                var sourceBytes = MemoryMarshal.AsBytes(span);
                sourceBytes.CopyTo(_data.AsSpan(_position));
                _position += byteLength;
            }
        }

        /// <summary>
        /// Serializes an array of unmanaged values to the internal buffer.
        /// </summary>
        public void PutArray(float[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(double[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(long[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(ulong[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(int[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(uint[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(ushort[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(short[] value) => PutUnmanagedArray(value);

        /// <inheritdoc cref="PutArray(float[])"/>
        public void PutArray(bool[] value) => PutUnmanagedArray(value);

        /// <summary>
        /// Serializes an array of <see cref="string"/> values.
        /// </summary>
        /// <param name="value">The array of <see cref="string"/> elements to write.</param>
        /// <remarks>
        /// Writes a 2-byte <see cref="ushort"/> length header followed by each <see cref="string"/> element.
        /// </remarks>
        public void PutArray(string[] value)
        {
            ushort strArrayLength = value == null ? (ushort)0 : (ushort)value.Length;
            Put(strArrayLength);
            for (int i = 0; i < strArrayLength; i++)
                Put(value[i]);
        }

        /// <summary>
        /// Serializes an array of <see cref="string"/> values with a maximum length constraint per element.
        /// </summary>
        /// <param name="value">The array of <see cref="string"/> elements to write.</param>
        /// <param name="strMaxLength">The maximum allowed length for each individual <see cref="string"/>.</param>
        public void PutArray(string[] value, int strMaxLength)
        {
            ushort strArrayLength = value == null ? (ushort)0 : (ushort)value.Length;
            Put(strArrayLength);
            for (int i = 0; i < strArrayLength; i++)
                Put(value[i], strMaxLength);
        }

        /// <summary>
        /// Serializes an array of objects implementing <see cref="INetSerializable"/>.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="INetSerializable"/> and has a parameterless constructor.</typeparam>
        /// <param name="value">The array of objects to serialize.</param>
        public void PutArray<T>(T[] value) where T : INetSerializable, new()
        {
            ushort strArrayLength = (ushort)(value?.Length ?? 0);
            Put(strArrayLength);
            for (int i = 0; i < strArrayLength; i++)
                value[i].Serialize(this);
        }

        /// <summary>
        /// Serializes an <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The network endpoint to write.</param>
        /// <exception cref="ArgumentException">Thrown when the <see cref="AddressFamily"/> is not <see cref="AddressFamily.InterNetwork"/> or <see cref="AddressFamily.InterNetworkV6"/>.</exception>
        /// <remarks>
        /// Writes a <see cref="byte"/> (0 for IPv4, 1 for IPv6), followed by the address bytes and a 2-byte <see cref="ushort"/> port.
        /// </remarks>
        public void Put(IPEndPoint endPoint)
        {
            if (endPoint.AddressFamily == AddressFamily.InterNetwork)
            {
                Put((byte)0);
            }
            else if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                Put((byte)1);
            }
            else
            {
                throw new ArgumentException("Unsupported address family: " + endPoint.AddressFamily);
            }

            Put(endPoint.Address.GetAddressBytes());
            Put((ushort)endPoint.Port);
        }

        /// <summary>
        /// Serializes a <see cref="string"/> using a 4-byte <see cref="int"/> length header.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to write.</param>
        /// <remarks>
        /// Recommended for strings that may exceed the 65535 byte limit of standard <see cref="ushort"/> length headers. <br/>
        /// Uses <see cref="Encoding.UTF8"/>.
        /// </remarks>
        public void PutLargeString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Put(0);
                return;
            }
            int size = uTF8Encoding.GetByteCount(value);
            if (size == 0)
            {
                Put(0);
                return;
            }
            Put(size);
            if (_autoResize)
                ResizeIfNeed(_position + size);
            uTF8Encoding.GetBytes(value, 0, size, _data, _position);
            _position += size;
        }

        /// <summary>
        /// Serializes a string using a 2-byte <see cref="float"/> length header.
        /// </summary>
        /// <param name="value">The string to write to the buffer.</param>
        /// <param name="maxLength">
        /// The maximum number of characters to write. If the string is longer, it will be truncated. <br/>
        /// A value of 0 indicates no limit.
        /// </param>
        /// <remarks>
        /// Note that <paramref name="maxLength"/> limits the number of characters, not the total size in <see cref="byte"/>s. <br/>
        /// Uses <see cref="Encoding.UTF8"/>.
        /// </remarks>
        public void Put(string value, int maxLength = 0)
        {
            if (string.IsNullOrEmpty(value))
            {
                Put((ushort)0);
                return;
            }

            int length = maxLength > 0 && value.Length > maxLength ? maxLength : value.Length;
            int maxSize = uTF8Encoding.GetMaxByteCount(length);
            if (_autoResize)
                ResizeIfNeed(_position + maxSize + sizeof(ushort));
            int size = uTF8Encoding.GetBytes(value, 0, length, _data, _position + sizeof(ushort));
            if (size == 0)
            {
                Put((ushort)0);
                return;
            }
            Put(checked((ushort)(size + 1)));
            _position += size;
        }

        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> into the internal byte buffer at the current position,
        /// advancing the position by the size of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">An unmanaged value type to write into the buffer.</typeparam>
        /// <param name="value">The value to write into the buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void PutUnmanaged<T>(T value) where T : unmanaged
        {
            int size = sizeof(T);
            if (_autoResize)
                ResizeIfNeed(_position + size);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += size;
        }

        /// <summary>
        /// Writes a nullable value of type <typeparamref name="T"/> into the internal byte buffer at the current position,
        /// first writing a <see cref="bool"/> indicating whether the value is present,
        /// and then writing the value itself if it exists. <br/> Advances the position by 1 byte for the presence flag plus
        /// the size of <typeparamref name="T"/> if the value is present.
        /// </summary>
        /// <typeparam name="T">An unmanaged value type to write into the buffer.</typeparam>
        /// <param name="value">The nullable value to write into the buffer. If <see langword="null"/>, only a <see langword="false"/> flag is written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PutNullableUnmanaged<T>(T? value) where T : unmanaged
        {
            bool hasValue = value.HasValue;
            Put(hasValue);
            if (!hasValue)
            {
                return;
            }

            PutUnmanaged(value.Value);
        }

        /// <summary>
        /// Writes an enum value of type <typeparamref name="T"/> to the internal data buffer at the current position. <br/>
        /// Automatically resizes the buffer if <see cref="_autoResize"/> is enabled.
        /// Advances the position by the size of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">An unmanaged enum type to write.</typeparam>
        /// <param name="value">The enum value to write.</param>
        public unsafe void PutEnum<T>(T value) where T : unmanaged, Enum
        {
            var size = sizeof(T);
            if (_autoResize)
            {
                ResizeIfNeed(_position + size);
            }

            FastBitConverter.GetBytes(_data, _position, value);
            _position += size;
        }

        /// <summary>
        /// Serializes an object implementing <see cref="INetSerializable"/>.
        /// </summary>
        /// <typeparam name="T">A type that implements the <see cref="INetSerializable"/> interface.</typeparam>
        /// <param name="obj">The object instance to serialize.</param>
        /// <remarks>
        /// This method calls the <see cref="INetSerializable.Serialize(NetDataWriter)"/> method on the provided <paramref name="obj"/>.
        /// </remarks>
        public void Put<T>(T obj) where T : INetSerializable => obj.Serialize(this);
    }
}

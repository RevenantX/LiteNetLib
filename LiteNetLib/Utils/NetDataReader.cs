﻿using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace LiteNetLib.Utils
{
    public class NetDataReader
    {
        protected byte[] _data;
        protected int _position;
        protected int _dataSize;
        private int _offset;

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

        public void SkipBytes(int count)
        {
            _position += count;
        }

        public void SetPosition(int position)
        {
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

        public void SetSource(byte[] source, int offset, int maxSize)
        {
            _data = source;
            _position = offset;
            _offset = offset;
            _dataSize = maxSize;
        }

        public NetDataReader()
        {

        }

        public NetDataReader(NetDataWriter writer)
        {
            SetSource(writer);
        }

        public NetDataReader(byte[] source)
        {
            SetSource(source);
        }

        public NetDataReader(byte[] source, int offset, int maxSize)
        {
            SetSource(source, offset, maxSize);
        }

        #region GetMethods
        public IPEndPoint GetIPEndPoint()
        {
            string host = GetString(1000);
            ushort port = GetUShort();
            return NetUtils.MakeEndPoint(host, port);
        }

        [Obsolete("Use GetIPEndPoint instead")]
        public IPEndPoint GetNetEndPoint() => GetIPEndPoint();

        public byte GetByte()
        {
            byte res = _data[_position];
            _position++;
            return res;
        }

        public sbyte GetSByte()
        {
            return (sbyte)GetByte();
        }

        #region Array
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] GetArray<T>(ushort size)
            where T : unmanaged
        {
            ushort length = GetUShort();
            if (length == 0) return null;
            if (length == 1) return Array.Empty<T>();
            length--;
            T[] result = new T[length];
            length *= size;
            Buffer.BlockCopy(_data, _position, result, 0, length);
            _position += length;
            return result;
        }

        public byte[] GetByteArray() => GetArray<byte>(sizeof(byte));
        public sbyte[] GetSByteArray() => GetArray<sbyte>(sizeof(sbyte));
        public bool[] GetBoolArray() => GetArray<bool>(sizeof(bool));
        public short[] GetShortArray() => GetArray<short>(sizeof(short));
        public ushort[] GetUShortArray() => GetArray<ushort>(sizeof(ushort));
        public int[] GetIntArray() => GetArray<int>(sizeof(int));
        public uint[] GetUIntArray() => GetArray<uint>(sizeof(uint));

        public float[] GetFloatArray() => GetArray<float>(sizeof(float));
        public long[] GetLongArray() => GetArray<long>(sizeof(long));
        public ulong[] GetULongArray() => GetArray<ulong>(sizeof(ulong));
        public double[] GetDoubleArray() => GetArray<double>(sizeof(double));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string[] GetStringArray(int stringMaxLength)
        {
            ushort length = GetUShort();
            if (length == 0) return null;
            if (length == 1) return Array.Empty<string>();
            length--;
            string[] result = new string[length];
            for (ushort i = 0; i < length; i++)
                result[i] = GetString(stringMaxLength);
            return result;
        }

        public string[] GetStringArray() => GetStringArray(0);
        #endregion

        public bool GetBool() => GetByte() == NetDataWriter.TRUE;
        public char GetChar() => (char)GetUShort();

        public ushort GetUShort()
        {
            ushort result = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            return result;
        }

        public short GetShort()
        {
            short result = BitConverter.ToInt16(_data, _position);
            _position += 2;
            return result;
        }

        public long GetLong()
        {
            long result = BitConverter.ToInt64(_data, _position);
            _position += 8;
            return result;
        }

        public ulong GetULong()
        {
            ulong result = BitConverter.ToUInt64(_data, _position);
            _position += 8;
            return result;
        }

        public int GetInt()
        {
            int result = BitConverter.ToInt32(_data, _position);
            _position += 4;
            return result;
        }

        public uint GetUInt()
        {
            uint result = BitConverter.ToUInt32(_data, _position);
            _position += 4;
            return result;
        }

        public float GetFloat()
        {
            float result = BitConverter.ToSingle(_data, _position);
            _position += 4;
            return result;
        }

        public double GetDouble()
        {
            double result = BitConverter.ToDouble(_data, _position);
            _position += 8;
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
            {
                return null;
            }

            int actualSize = size - 1;
            if (actualSize >= NetDataWriter.StringBufferMaxLength)
            {
                return null;
            }

            ArraySegment<byte> data = GetBytesSegment(actualSize);

            return (maxLength > 0 && NetDataWriter.uTF8Encoding.GetCharCount(data.Array, data.Offset, data.Count) > maxLength) ?
                string.Empty :
                NetDataWriter.uTF8Encoding.GetString(data.Array, data.Offset, data.Count);
        }

        public string GetString()
        {
            ushort size = GetUShort();
            if (size == 0)
            {
                return null;
            }

            int actualSize = size - 1;
            if (actualSize >= NetDataWriter.StringBufferMaxLength)
            {
                return null;
            }

            ArraySegment<byte> data = GetBytesSegment(actualSize);

            return NetDataWriter.uTF8Encoding.GetString(data.Array, data.Offset, data.Count);
        }

        public ArraySegment<byte> GetBytesSegment(int count)
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, count);
            _position += count;
            return segment;
        }

        public ArraySegment<byte> GetRemainingBytesSegment()
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_data, _position, AvailableBytes);
            _position = _data.Length;
            return segment;
        }

        public T Get<T>() where T : INetSerializable, new()
        {
            var obj = new T();
            obj.Deserialize(this);
            return obj;
        }

        /// <summary>Raw bytes</summary>
        public byte[] GetRemainingBytes()
        {
            byte[] outgoingData = new byte[AvailableBytes];
            Buffer.BlockCopy(_data, _position, outgoingData, 0, AvailableBytes);
            _position = _data.Length;
            return outgoingData;
        }

        /// <summary>Raw bytes</summary>
        public void GetBytes(byte[] destination, int start, int count)
        {
            Buffer.BlockCopy(_data, _position, destination, start, count);
            _position += count;
        }

        /// <summary>Raw bytes</summary>
        public void GetBytes(byte[] destination, int count)
        {
            Buffer.BlockCopy(_data, _position, destination, 0, count);
            _position += count;
        }

        [Obsolete("Use GetSByteArray instead")]
        public sbyte[] GetSBytesWithLength()
        {
            return GetArray<sbyte>(1);
        }

        [Obsolete("Use GetByteArray instead")]
        public byte[] GetBytesWithLength()
        {
            return GetArray<byte>(1);
        }
        #endregion

        #region PeekMethods

        public byte PeekByte()
        {
            return _data[_position];
        }

        public sbyte PeekSByte()
        {
            return (sbyte)_data[_position];
        }

        public bool PeekBool()
        {
            return _data[_position] == 1;
        }

        public char PeekChar()
        {
            return (char)PeekUShort();
        }

        public ushort PeekUShort()
        {
            return BitConverter.ToUInt16(_data, _position);
        }

        public short PeekShort()
        {
            return BitConverter.ToInt16(_data, _position);
        }

        public long PeekLong()
        {
            return BitConverter.ToInt64(_data, _position);
        }

        public ulong PeekULong()
        {
            return BitConverter.ToUInt64(_data, _position);
        }

        public int PeekInt()
        {
            return BitConverter.ToInt32(_data, _position);
        }

        public uint PeekUInt()
        {
            return BitConverter.ToUInt32(_data, _position);
        }

        public float PeekFloat()
        {
            return BitConverter.ToSingle(_data, _position);
        }

        public double PeekDouble()
        {
            return BitConverter.ToDouble(_data, _position);
        }

        /// <summary>
        /// Note that "maxLength" only limits the number of characters in a string, not its size in bytes.
        /// </summary>
        public string PeekString(int maxLength)
        {
            ushort size = PeekUShort();
            if (size == 0)
            {
                return null;
            }

            int actualSize = size - 1;
            if (actualSize >= NetDataWriter.StringBufferMaxLength)
            {
                return null;
            }

            return (maxLength > 0 && NetDataWriter.uTF8Encoding.GetCharCount(_data, _position + 2, actualSize) > maxLength) ?
                string.Empty :
                NetDataWriter.uTF8Encoding.GetString(_data, _position + 2, actualSize);
        }

        public string PeekString()
        {
            ushort size = PeekUShort();
            if (size == 0)
            {
                return null;
            }

            int actualSize = size - 1;
            if (actualSize >= NetDataWriter.StringBufferMaxLength)
            {
                return null;
            }

            return NetDataWriter.uTF8Encoding.GetString(_data, _position + 2, actualSize);
        }
        #endregion

        #region TryGetMethods
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

        public bool TryGetString(out string result)
        {
            if (AvailableBytes >= 2)
            {
                ushort strSize = PeekUShort();
                if (AvailableBytes >= strSize + 1)
                {
                    result = GetString();
                    return true;
                }
            }
            result = null;
            return false;
        }

        public bool TryGetStringArray(out string[] result)
        {
            if (!TryGetUShort(out ushort strArrayLength)) {
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

        public bool TryGetBytesWithLength(out byte[] result)
        {
            if (AvailableBytes >= 2)
            {
                ushort length = PeekUShort();
                if (length >= 0 && AvailableBytes >= 2 + length)
                {
                    result = GetByteArray();
                    return true;
                }
            }
            result = null;
            return false;
        }
        #endregion

        public void Clear()
        {
            _position = 0;
            _dataSize = 0;
            _data = null;
        }
    }
}

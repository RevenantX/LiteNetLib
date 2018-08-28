using System;
using System.Net;
using System.Text;

namespace LiteNetLib.Utils
{
    public class NetDataReader
    {
        protected byte[] _data;
        protected int _position;
        protected int _dataSize;

        public byte this[int i]
        {
            get { return _data[i+_position]; }
        }

        public bool IsNull
        {
            get { return _data == null; }
        }

        public int Position
        {
            get { return _position; }
        }

        public bool EndOfData
        {
            get { return _position == _dataSize; }
        }

        public int AvailableBytes
        {
            get { return _dataSize - _position; }
        }

        public void SetSource(NetDataWriter dataWriter)
        {
            _data = dataWriter.Data;
            _position = 0;
            _dataSize = dataWriter.Length;
        }

        public void SetSource(byte[] source)
        {
            _data = source;
            _position = 0;
            _dataSize = source.Length;
        }

        public void SetSource(byte[] source, int offset)
        {
            _data = source;
            _position = offset;
            _dataSize = source.Length;
        }

        public void SetSource(byte[] source, int offset, int maxSize)
        {
            _data = source;
            _position = offset;
            _dataSize = maxSize;
        }

        /// <summary>
        /// Clone NetDataReader without data copy (usable for OnReceive)
        /// </summary>
        /// <returns>new NetDataReader instance</returns>
        public NetDataReader Clone()
        {
            return new NetDataReader(_data, _position, _dataSize);
        }

        public NetDataReader()
        {

        }

        public NetDataReader(byte[] source)
        {
            SetSource(source);
        }

        public NetDataReader(byte[] source, int offset)
        {
            SetSource(source, offset);
        }

        public NetDataReader(byte[] source, int offset, int maxSize)
        {
            SetSource(source, offset, maxSize);
        }

        #region GetMethods
        public IPEndPoint GetNetEndPoint()
        {
            string host = GetString(1000);
            int port = GetInt();
            return NetUtils.MakeEndPoint(host, port);
        }

        public byte GetByte()
        {
            byte res = _data[_position];
            _position += 1;
            return res;
        }

        public sbyte GetSByte()
        {
            var b = (sbyte)_data[_position];
            _position++;
            return b;
        }

        public bool[] GetBoolArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new bool[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetBool();
            }
            return arr;
        }

        public ushort[] GetUShortArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new ushort[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetUShort();
            }
            return arr;
        }

        public short[] GetShortArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new short[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetShort();
            }
            return arr;
        }

        public long[] GetLongArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new long[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetLong();
            }
            return arr;
        }

        public ulong[] GetULongArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new ulong[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetULong();
            }
            return arr;
        }

        public int[] GetIntArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new int[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetInt();
            }
            return arr;
        }

        public uint[] GetUIntArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new uint[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetUInt();
            }
            return arr;
        }

        public float[] GetFloatArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new float[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetFloat();
            }
            return arr;
        }

        public double[] GetDoubleArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new double[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetDouble();
            }
            return arr;
        }

        public string[] GetStringArray()
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new string[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetString();
            }
            return arr;
        }

        public string[] GetStringArray(int maxStringLength)
        {
            ushort size = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            var arr = new string[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = GetString(maxStringLength);
            }
            return arr;
        }

        public bool GetBool()
        {
            bool res = _data[_position] > 0;
            _position += 1;
            return res;
        }

        public char GetChar()
        {
            char result = BitConverter.ToChar(_data, _position);
            _position += 2;
            return result;
        }

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

        public string GetString(int maxLength)
        {
            int bytesCount = GetInt();
            if (bytesCount <= 0 || bytesCount > maxLength*2)
            {
                return string.Empty;
            }

            int charCount = Encoding.UTF8.GetCharCount(_data, _position, bytesCount);
            if (charCount > maxLength)
            {
                return string.Empty;
            }

            string result = Encoding.UTF8.GetString(_data, _position, bytesCount);
            _position += bytesCount;
            return result;
        }

        public string GetString()
        {
            int bytesCount = GetInt();
            if (bytesCount <= 0)
            {
                return string.Empty;
            }

            string result = Encoding.UTF8.GetString(_data, _position, bytesCount);
            _position += bytesCount;
            return result;
        }

        public byte[] GetRemainingBytes()
        {
            byte[] outgoingData = new byte[AvailableBytes];
            Buffer.BlockCopy(_data, _position, outgoingData, 0, AvailableBytes);
            _position = _data.Length;
            return outgoingData;
        }

        public void GetBytes(byte[] destination, int start, int count)
        {
            Buffer.BlockCopy(_data, _position, destination, start, count);
            _position += count;
        }

        public void GetBytes(byte[] destination, int count)
        {
            Buffer.BlockCopy(_data, _position, destination, 0, count);
            _position += count;
        }

        public byte[] GetBytesWithLength()
        {
            int length = GetInt();
            byte[] outgoingData = new byte[length];
            Buffer.BlockCopy(_data, _position, outgoingData, 0, length);
            _position += length;
            return outgoingData;
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
            return _data[_position] > 0;
        }

        public char PeekChar()
        {
            return BitConverter.ToChar(_data, _position);
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

        public string PeekString(int maxLength)
        {
            int bytesCount = BitConverter.ToInt32(_data, _position);
            if (bytesCount <= 0 || bytesCount > maxLength * 2)
            {
                return string.Empty;
            }

            int charCount = Encoding.UTF8.GetCharCount(_data, _position + 4, bytesCount);
            if (charCount > maxLength)
            {
                return string.Empty;
            }

            string result = Encoding.UTF8.GetString(_data, _position + 4, bytesCount);
            return result;
        }

        public string PeekString()
        {
            int bytesCount = BitConverter.ToInt32(_data, _position);
            if (bytesCount <= 0)
            {
                return string.Empty;
            }

            string result = Encoding.UTF8.GetString(_data, _position + 4, bytesCount);
            return result;
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


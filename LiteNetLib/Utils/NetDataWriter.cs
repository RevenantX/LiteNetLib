using System;
using System.Net;
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
            get { return _data.Length; }
        }

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
            return new NetDataWriter(true, 0) {_data = bytes};
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
            netDataWriter.Put(value);
            return netDataWriter;
        }

        public void ResizeIfNeed(int newSize)
        {
            int len = _data.Length;
            if (len < newSize)
            {
                while (len < newSize)
                    len *= 2;
                Array.Resize(ref _data, len);
            }
        }

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

        public byte[] Data
        {
            get { return _data; }
        }

        public int Length
        {
            get { return _position; }
        }

        public void Put(float value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 4);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 4;
        }

        public void Put(double value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 8);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 8;
        }

        public void Put(long value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 8);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 8;
        }

        public void Put(ulong value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 8);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 8;
        }

        public void Put(int value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 4);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 4;
        }

        public void Put(uint value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 4);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 4;
        }

        public void Put(char value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 2;
        }

        public void Put(ushort value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 2;
        }

        public void Put(short value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 2);
            FastBitConverter.GetBytes(_data, _position, value);
            _position += 2;
        }

        public void Put(sbyte value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 1);
            _data[_position] = (byte)value;
            _position++;
        }

        public void Put(byte value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 1);
            _data[_position] = value;
            _position++;
        }

        public void Put(byte[] data, int offset, int length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + length);
            Buffer.BlockCopy(data, offset, _data, _position, length);
            _position += length;
        }

        public void Put(byte[] data)
        {
            if (_autoResize)
                ResizeIfNeed(_position + data.Length);
            Buffer.BlockCopy(data, 0, _data, _position, data.Length);
            _position += data.Length;
        }

        public void PutBytesWithLength(byte[] data, int offset, int length)
        {
            if (_autoResize)
                ResizeIfNeed(_position + length + 4);
            FastBitConverter.GetBytes(_data, _position, length);
            Buffer.BlockCopy(data, offset, _data, _position + 4, length);
            _position += length + 4;
        }

        public void PutBytesWithLength(byte[] data)
        {
            if (_autoResize)
                ResizeIfNeed(_position + data.Length + 4);
            FastBitConverter.GetBytes(_data, _position, data.Length);
            Buffer.BlockCopy(data, 0, _data, _position + 4, data.Length);
            _position += data.Length + 4;
        }

        public void Put(bool value)
        {
            if (_autoResize)
                ResizeIfNeed(_position + 1);
            _data[_position] = (byte)(value ? 1 : 0);
            _position++;
        }

        public void PutArray(float[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 4 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(double[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 8 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(long[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 8 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(ulong[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 8 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(int[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 4 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(uint[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 4 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(ushort[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 2 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(short[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len * 2 + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(bool[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            if (_autoResize)
                ResizeIfNeed(_position + len + 2);
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(string[] value)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i]);
            }
        }

        public void PutArray(string[] value, int maxLength)
        {
            ushort len = value == null ? (ushort)0 : (ushort)value.Length;
            Put(len);
            for (int i = 0; i < len; i++)
            {
                Put(value[i], maxLength);
            }
        }

        public void Put(IPEndPoint endPoint)
        {
            Put(endPoint.Address.ToString());
            Put(endPoint.Port);
        }

        public void Put(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Put(0);
                return;
            }

            //put bytes count
            int bytesCount = Encoding.UTF8.GetByteCount(value);
            if (_autoResize)
                ResizeIfNeed(_position + bytesCount + 4);
            Put(bytesCount);

            //put string
            Encoding.UTF8.GetBytes(value, 0, value.Length, _data, _position);
            _position += bytesCount;
        }

        public void Put(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                Put(0);
                return;
            }

            int length = value.Length > maxLength ? maxLength : value.Length;
            //calculate max count
            int bytesCount = Encoding.UTF8.GetByteCount(value);
            if (_autoResize)
                ResizeIfNeed(_position + bytesCount + 4);

            //put bytes count
            Put(bytesCount);

            //put string
            Encoding.UTF8.GetBytes(value, 0, length, _data, _position);

            _position += bytesCount;
        }
    }
}

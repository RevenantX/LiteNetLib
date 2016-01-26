using System;
using System.Text;

namespace LiteNetLib.Utils
{
    public class NetDataWriter
    {
        protected byte[] _data;
        protected int _position;
        protected int _maxLength;
        protected readonly FastBitConverter _fastBitConverter = new FastBitConverter();

        public NetDataWriter()
        {
            _maxLength = 64;
            _data = new byte[_maxLength];
        }

        public NetDataWriter(int initialSize)
        {
            _maxLength = initialSize;
            _data = new byte[_maxLength];
        }

        public void ResizeIfNeed(int newSize)
        {
            if (_maxLength < newSize)
            {
                while (_maxLength < newSize)
                {
                    _maxLength *= 2;
                }
                _data = new byte[_maxLength];
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

        public void Put(double value)
        {
            _position += _fastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(float value)
        {
            _position += _fastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(long value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(ulong value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(int value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(uint value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(ushort value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(short value)
        {
            _position += FastBitConverter.GetBytes(_data, _position, value);
        }

        public void Put(sbyte value)
        {
            _data[_position] = (byte)value;
            _position++;
        }

        public void Put(byte value)
        {
            _data[_position] = value;
            _position++;
        }

        public void Put(byte[] data, int offset, int length)
        {
            Buffer.BlockCopy(data, offset, _data, _position, length);
            _position += length;
        }

        public void Put(byte[] data, int length)
        {
            Put(data, 0, length);
        }

        public void Put(bool value)
        {
            _data[_position] = (byte)(value ? 1 : 0);
            _position++;
        }

        public void Put(string value)
        {
            //put bytes count
            int bytesCount = Encoding.UTF8.GetByteCount(value);
            Put(bytesCount);

            //put string
            Encoding.UTF8.GetBytes(value, 0, value.Length, _data, _position);
            _position += bytesCount;
        }

        public void Put(NetEndPoint endPoint)
        {
            Put(endPoint.Host);
            Put(endPoint.Port);
        }

        public void Put(string value, int maxLength)
        {
            int length = value.Length > maxLength ? maxLength : value.Length;
            if (length == 0)
            {
                return;
            }

            //calculate max count
            int bytesCount = Encoding.UTF8.GetByteCount(value);

            //put bytes count
            Put(bytesCount);

            //put string
            Encoding.UTF8.GetBytes(value, 0, length, _data, _position);

            _position += bytesCount;
        }
    }
}

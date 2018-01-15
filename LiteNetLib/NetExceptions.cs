using System;
namespace LiteNetLib
{
    public class InvalidPacketException: ArgumentException
    {
        public InvalidPacketException()
        {
        }

        public InvalidPacketException(string message): base(message)
        {
        }

        public InvalidPacketException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    public class TooBigPacketException : InvalidPacketException
    {
        public TooBigPacketException()
        {
        }

        public TooBigPacketException(string message) : base(message)
        {
        }

        public TooBigPacketException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
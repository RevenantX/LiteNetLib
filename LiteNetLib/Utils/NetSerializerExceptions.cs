using System;
namespace LiteNetLib.Utils
{
    public class InvalidTypeException : ArgumentException
    {
        public InvalidTypeException()
        {
        }

        public InvalidTypeException(string message) : base(message)
        {
        }

        public InvalidTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public InvalidTypeException(string message, string paramName) : base(message, paramName)
        {
        }

        public InvalidTypeException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
        {
        }
    }

    public class ParseException : Exception
    {
        public ParseException()
        {
        }

        public ParseException(string message) : base(message)
        {
        }

        public ParseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

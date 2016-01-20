using System.Net;

namespace LiteNetLib
{
    public class NetEndPoint
    {
        internal IPEndPoint EndPoint;

        public string Host
        {
            get { return EndPoint.Address.ToString(); }
        }

        public int Port
        {
            get { return EndPoint.Port; }
        }

        public override bool Equals(object comparand)
        {
            if (!(comparand is NetEndPoint))
            {
                return false;
            }
            return ((NetEndPoint)comparand).EndPoint.Equals(EndPoint);
        }

        public override int GetHashCode()
        {
            return EndPoint.GetHashCode();
        }

        public NetEndPoint(IPAddress address, int port)
        {
            EndPoint = new IPEndPoint(address, port);
        }
    }
}

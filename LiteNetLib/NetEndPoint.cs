using System.Net;

namespace LiteNetLib
{
    public class NetEndPoint
    {
        public IPEndPoint EndPoint;

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

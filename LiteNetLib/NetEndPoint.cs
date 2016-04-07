#if !WINRT || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    public sealed class NetEndPoint
    {
        public string Host { get { return EndPoint.Address.ToString(); } }
        public int Port { get { return EndPoint.Port; } }

        internal readonly IPEndPoint EndPoint;

        internal NetEndPoint(IPEndPoint ipEndPoint)
        {
            EndPoint = ipEndPoint;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NetEndPoint))
            {
                return false;
            }
            return EndPoint.Equals(((NetEndPoint)obj).EndPoint);
        }

        public override string ToString()
        {
            return EndPoint.ToString();
        }

        public override int GetHashCode()
        {
            return EndPoint.GetHashCode();
        }

        internal NetEndPoint(ConnectionAddressType addressType, int port)
        {
            EndPoint = new IPEndPoint(addressType == ConnectionAddressType.IPv4 ? IPAddress.Any : IPAddress.IPv6Any, port);
        }

        public NetEndPoint(string hostStr, int port)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(hostStr, out ipAddress))
            {
                IPHostEntry host = Dns.GetHostEntry(hostStr);
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = ip;
                        break;
                    }
                }
            }
            if (ipAddress == null)
            {
                throw new Exception("Invalid address: " + hostStr);
            }
            EndPoint = new IPEndPoint(ipAddress, port);
        }

        internal long GetId()
        {
            long id = 0;
            byte[] addr = EndPoint.Address.GetAddressBytes();

            if (addr.Length == 8) //IPv4
            {
                id |= (long)addr[0];
                id |= (long)addr[1] << 8;
                id |= (long)addr[2] << 16;
                id |= (long)addr[3] << 24;
            }
            else if (addr.Length == 16) //IPv6
            {
                id |= (long)(addr[0] ^ addr[4]);
                id |= (long)(addr[1] ^ addr[5]) << 8;
                id |= (long)(addr[2] ^ addr[6]) << 16;
                id |= (long)(addr[3] ^ addr[7]) << 24;
            }

            id |= (long)EndPoint.Port << 32;
            return id;
        }
    }
}
#endif

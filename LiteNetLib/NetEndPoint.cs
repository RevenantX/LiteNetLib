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
        public ConnectionAddressType AddressType
        {
            get
            {
                return EndPoint.AddressFamily == AddressFamily.InterNetwork
                    ? ConnectionAddressType.IPv4
                    : ConnectionAddressType.IPv6;
            }
        }

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
                    if (ip.AddressFamily == AddressFamily.InterNetwork || 
                        ip.AddressFamily == AddressFamily.InterNetworkV6)
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
            byte[] addr = EndPoint.Address.GetAddressBytes();
            long id = 0;

            if (addr.Length == 4) //IPv4
            {
                id = addr[0];
                id |= (long)addr[1] << 8;
                id |= (long)addr[2] << 16;
                id |= (long)addr[3] << 24;
                id |= (long)EndPoint.Port << 32;
            }
            else if (addr.Length == 16) //IPv6
            {
                id = addr[0] ^ addr[8];
                id |= (long)(addr[1] ^ addr[9]) << 8;
                id |= (long)(addr[2] ^ addr[10]) << 16;
                id |= (long)(addr[3] ^ addr[11]) << 24;
                id |= (long)(addr[4] ^ addr[12]) << 32;
                id |= (long)(addr[5] ^ addr[13]) << 40;
                id |= (long)(addr[6] ^ addr[14]) << 48;
                id |= (long)(Port ^ addr[7] ^ addr[15]) << 56;
            }

            return id;
        }
    }
}
#endif

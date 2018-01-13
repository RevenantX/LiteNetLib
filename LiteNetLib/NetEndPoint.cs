using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    /// <summary>
    /// Network End Point. Contains ip address and port
    /// </summary>
    public sealed class NetEndPoint
    {
        public static readonly string IPv4Any = IPAddress.Any.ToString();
        public static readonly string IPv6Any = IPAddress.IPv6Any.ToString();
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

        public NetEndPoint(string hostStr, int port)
        {
            IPAddress addr = GetFromString(hostStr);
            EndPoint = new IPEndPoint(addr, port);
        }

        internal static IPAddress GetFromString(string hostStr)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(hostStr, out ipAddress))
            {
                if (NetSocket.IPv6Support)
                {
                    if (hostStr == "localhost")
                    {
                        ipAddress = IPAddress.IPv6Loopback;
                    }
                    else
                    {
                        ipAddress = ResolveAddress(hostStr, AddressFamily.InterNetworkV6);
                    }
                }
                if (ipAddress == null)
                {
                    ipAddress = ResolveAddress(hostStr, AddressFamily.InterNetwork);
                }
            }
            if (ipAddress == null)
            {
                throw new Exception("Invalid address: " + hostStr);
            }

            return ipAddress;
        }

        private static IPAddress ResolveAddress(string hostStr, AddressFamily addressFamily)
        {
#if NETCORE
            var hostTask = Dns.GetHostEntryAsync(hostStr);
            hostTask.Wait();
            var host = hostTask.Result;
#else
            var host = Dns.GetHostEntry(hostStr);
#endif
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == addressFamily)
                {
                    return ip;
                }
            }
            return null;
        }
    }
}
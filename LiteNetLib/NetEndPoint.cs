using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    internal class NetEndPointComparer : IEqualityComparer<NetEndPoint>
    {
        public bool Equals(NetEndPoint x, NetEndPoint y)
        {
            return x.EndPoint.Equals(y.EndPoint);
        }

        public int GetHashCode(NetEndPoint obj)
        {
            return obj.GetHashCode();
        }
    }
    /// <summary>
    /// Network End Point. Contains ip address and port
    /// </summary>
    public sealed class NetEndPoint : IEquatable<NetEndPoint>
    {
        public static readonly string IPv4Any = IPAddress.Any.ToString();
        public static readonly string IPv6Any = IPAddress.IPv6Any.ToString();
        public string Host { get { return EndPoint.Address.ToString(); } }
        public int Port { get { return EndPoint.Port; } }

        internal IPEndPoint EndPoint;
        private int _hash;
#if WIN32 && UNSAFE
        internal readonly byte[] SocketAddr;
        private byte[] MakeSocketAddr(IPEndPoint ep)
        {
            var saddr = ep.Serialize();
            byte[] data = new byte[saddr.Size];
            for(int i = 0; i < saddr.Size; i++)
                data[i] = saddr[i];
            return data;
        }
#endif

        internal NetEndPoint(EndPoint ipEndPoint)
        {
            Set(ipEndPoint);
        }

        internal void Set(EndPoint ep)
        {
            EndPoint = (IPEndPoint)ep;
#if WIN32 && UNSAFE
            SocketAddr = MakeSocketAddr(ipEndPoint);
#endif
            _hash = EndPoint.GetHashCode();
        }

        public NetEndPoint Clone()
        {
            return new NetEndPoint(EndPoint);
        }

        public override bool Equals(object obj)
        {
            var ep = obj as NetEndPoint;
            return ep != null && EndPoint.Equals(ep.EndPoint);
        }

        public bool Equals(NetEndPoint other)
        {
            return other != null && other.EndPoint.Equals(EndPoint);
        }

        public override string ToString()
        {
            return EndPoint.ToString();
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        /// <param name="hostStr">A valid host string that can be resolved by DNS or parsed as an IP address</param>
        /// <param name="port">Port of the end point</param>
        /// <exception cref="ArgumentException"> <paramref name="hostStr"/> contains an invalid IP address</exception>>
        /// <exception cref="ArgumentOutOfRangeException"> 
        ///     <paramref name="port"/> is less than IPEndPoint.MinPort or port is greater than IPEndPoint.MaxPort</exception>
        public NetEndPoint(string hostStr, int port)
        {
            IPAddress addr = GetFromString(hostStr);
            Set(new IPEndPoint(addr, port));
        }

        internal static IPAddress GetFromString(string hostStr)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(hostStr, out ipAddress))
            {
                if (NetSocket.IPv6Support)
                {
                    ipAddress = hostStr == "localhost" 
                        ? IPAddress.IPv6Loopback 
                        : ResolveAddress(hostStr, AddressFamily.InterNetworkV6);
                }
                if (ipAddress == null)
                {
                    ipAddress = ResolveAddress(hostStr, AddressFamily.InterNetwork);
                }
            }
            if (ipAddress == null)
            {
                throw new ArgumentException("Invalid address: " + hostStr);
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
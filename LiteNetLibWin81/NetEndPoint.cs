using System;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace LiteNetLib
{
    public class NetEndPoint
    {
        public string Host { get { return HostName.DisplayName; } }
        public int Port { get; private set; }

        internal readonly HostName HostName;
        internal readonly string PortStr;

        internal NetEndPoint(int port)
        {
            HostName = null;
            PortStr = port.ToString();
            Port = port;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NetEndPoint))
            {
                return false;
            }
            NetEndPoint other = (NetEndPoint) obj;
            return HostName.IsEqual(other.HostName) && PortStr.Equals(other.PortStr);
        }

        public override int GetHashCode()
        {
            return HostName.CanonicalName.GetHashCode() ^ PortStr.GetHashCode();
        }

        internal long GetId()
        {
            string hostIp;
            if (HostName == null)
            {
                hostIp = "0.0.0.0";
            }
            else if (HostName.DisplayName == "localhost")
            {
                hostIp = "127.0.0.1";
            }
            else
            {
                var task = Task.Run(async () => await DatagramSocket.GetEndpointPairsAsync(HostName, "0"));
                task.Wait();
                hostIp = task.Result[0].RemoteHostName.CanonicalName;
                if (hostIp.Contains(":"))
                {
                    return hostIp.GetHashCode() ^ Port;
                }
            }
            long id = 0;
            string[] ip = hostIp.Split('.');
            id |= long.Parse(ip[0]);
            id |= long.Parse(ip[1]) << 8;
            id |= long.Parse(ip[2]) << 16;
            id |= long.Parse(ip[3]) << 24;
            id |= (long)Port << 32;
            return id;
        }

        internal NetEndPoint(string hostName, int port)
        {
            var task = Task.Run(async () => await DatagramSocket.GetEndpointPairsAsync(new HostName(hostName), port.ToString()));
            task.Wait();
            HostName = task.Result[0].RemoteHostName;
            Port = port;
            PortStr = port.ToString();
        }

        internal NetEndPoint(HostName hostName, string port)
        {
            HostName = hostName;
            Port = int.Parse(port);
            PortStr = port;
        }
    }
}

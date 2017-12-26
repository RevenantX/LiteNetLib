using System;
using System.Collections.Generic;
using System.Diagnostics;
#if WINRT && !UNITY_EDITOR
using Windows.Networking;
using Windows.Networking.Connectivity;
#else
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
#endif

namespace LiteNetLib
{
#if WINRT && !UNITY_EDITOR
    public enum ConsoleColor
    {
        Gray,
        Yellow,
        Cyan,
        DarkCyan,
        DarkGreen,
        Blue,
        DarkRed,
        Red,
        Green,
        DarkYellow
    }
#endif

    /// <summary>
    /// Address type that you want to receive from NetUtils.GetLocalIp method
    /// </summary>
    [Flags]
    public enum LocalAddrType
    {
        IPv4 = 1,
        IPv6 = 2,
        All = 3
    }

    /// <summary>
    /// Some specific network utilities
    /// </summary>
    public static class NetUtils
    {
        /// <summary>
        /// Request time from NTP server and calls callback (if success)
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address</param>
        /// <param name="port">port</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        public static void RequestTimeFromNTP(string ntpServerAddress, int port, Action<DateTime?> onRequestComplete)
        {
            NetSocket socket = null;
            var ntpEndPoint = new NetEndPoint(ntpServerAddress, port);

            NetManager.OnMessageReceived onReceive = (data, length, code, point) =>
            {
                if (!point.Equals(ntpEndPoint) || length < 48)
                {
                    return;
                }
                socket.Close();

                ulong intPart = (ulong)data[40] << 24 | (ulong)data[41] << 16 | (ulong)data[42] << 8 | (ulong)data[43];
                ulong fractPart = (ulong)data[44] << 24 | (ulong)data[45] << 16 | (ulong)data[46] << 8 | (ulong)data[47];
                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                onRequestComplete(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long) milliseconds));
            };

            //Create and start socket
            socket = new NetSocket(onReceive);
            socket.Bind(0, false);

            //Send request
            int errorCode = 0;
            var sendData = new byte[48];
            sendData[0] = 0x1B;
            var sendCount = socket.SendTo(sendData, 0, sendData.Length, ntpEndPoint, ref errorCode);
            if (errorCode != 0 || sendCount != sendData.Length)
            {
                onRequestComplete(null);
            }
        }

        /// <summary>
        /// Get all local ip addresses
        /// </summary>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        /// <returns>List with all local ip adresses</returns>
        public static List<string> GetLocalIpList(LocalAddrType addrType)
        {
            List<string> targetList = new List<string>();
            GetLocalIpList(targetList, addrType);
            return targetList;
        }

        /// <summary>
        /// Get all local ip addresses (non alloc version)
        /// </summary>
        /// <param name="targetList">result list</param>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        public static void GetLocalIpList(List<string> targetList, LocalAddrType addrType)
        {
            bool ipv4 = (addrType & LocalAddrType.IPv4) == LocalAddrType.IPv4;
            bool ipv6 = (addrType & LocalAddrType.IPv6) == LocalAddrType.IPv6;
#if WINRT && !UNITY_EDITOR
            foreach (HostName localHostName in NetworkInformation.GetHostNames())
            {
                if (localHostName.IPInformation != null && 
                    ((ipv4 && localHostName.Type == HostNameType.Ipv4) ||
                     (ipv6 && localHostName.Type == HostNameType.Ipv6)))
                {
                    targetList.Add(localHostName.ToString());
                }
            }
#else
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    //Skip loopback and disabled network interfaces
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || 
                        ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var ipProps = ni.GetIPProperties();

                    //Skip address without gateway
                    if (ipProps.GatewayAddresses.Count == 0)
                        continue;

                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        var address = ip.Address;
                        if ((ipv4 && address.AddressFamily == AddressFamily.InterNetwork) ||
                            (ipv6 && address.AddressFamily == AddressFamily.InterNetworkV6))
                            targetList.Add(address.ToString());
                    }
                }
            }
            catch
            {
                //ignored
            }

            //Fallback mode (unity android)
            if (targetList.Count == 0)
            {
#if NETCORE
                var hostTask = Dns.GetHostEntryAsync(Dns.GetHostName());
                hostTask.Wait();
                var host = hostTask.Result;
#else
                var host = Dns.GetHostEntry(Dns.GetHostName());
#endif
                foreach (IPAddress ip in host.AddressList)
                {
                    if((ipv4 && ip.AddressFamily == AddressFamily.InterNetwork) ||
                       (ipv6 && ip.AddressFamily == AddressFamily.InterNetworkV6))
                        targetList.Add(ip.ToString());
                }
            }
#endif
            if (targetList.Count == 0)
            {
                if(ipv4)
                    targetList.Add("127.0.0.1");
                if(ipv6)
                    targetList.Add("::1");
            }
        }

        private static readonly List<string> IpList = new List<string>();
        /// <summary>
        /// Get first detected local ip address
        /// </summary>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        /// <returns>IP address if available. Else - string.Empty</returns>
        public static string GetLocalIp(LocalAddrType addrType)
        {
            lock (IpList)
            {
                IpList.Clear();
                GetLocalIpList(IpList, addrType);
                return IpList.Count == 0 ? string.Empty : IpList[0];
            }
        }

        // ===========================================
        // Internal and debug log related stuff
        // ===========================================
        internal static void PrintInterfaceInfos()
        {
#if !WINRT || UNITY_EDITOR
            DebugWriteForce(ConsoleColor.Green, "IPv6Support: {0}", NetSocket.IPv6Support);
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork ||
                            ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            DebugWriteForce(
                                ConsoleColor.Green,
                                "Interface: {0}, Type: {1}, Ip: {2}, OpStatus: {3}",
                                ni.Name,
                                ni.NetworkInterfaceType.ToString(),
                                ip.Address.ToString(),
                                ni.OperationalStatus.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWriteForce(ConsoleColor.Red, "Error while getting interface infos: {0}", e.ToString());
            }
#endif
        }

        internal static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + NetConstants.MaxSequence + NetConstants.HalfMaxSequence) % NetConstants.MaxSequence - NetConstants.HalfMaxSequence;
        }

        private static readonly object DebugLogLock = new object();
        private static void DebugWriteLogic(ConsoleColor color, string str, params object[] args)
        {
            lock (DebugLogLock)
            {

                if (NetDebug.Logger == null)
                {
#if UNITY
                    UnityEngine.Debug.Log(string.Format(str, args));
#elif WINRT
                    Debug.WriteLine(str, args);
#else
                    Console.ForegroundColor = color;
                    Console.WriteLine(str, args);
                    Console.ForegroundColor = ConsoleColor.Gray;
#endif
                }
                else
                {
                    NetDebug.Logger.WriteNet(color, str, args);
                }
            }
        }

        [Conditional("DEBUG_MESSAGES")]
        internal static void DebugWrite(string str, params object[] args)
        {
            DebugWriteLogic(ConsoleColor.DarkGreen, str, args);
        }

        [Conditional("DEBUG_MESSAGES")]
        internal static void DebugWrite(ConsoleColor color, string str, params object[] args)
        {
            DebugWriteLogic(color, str, args);
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal static void DebugWriteForce(ConsoleColor color, string str, params object[] args)
        {
            DebugWriteLogic(color, str, args);
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal static void DebugWriteError(string str, params object[] args)
        {
            DebugWriteLogic(ConsoleColor.Red, str, args);
        }
    }
}

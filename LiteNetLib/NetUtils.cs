using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace LiteNetLib
{
    /// <summary>
    /// Address type that you want to receive from NetUtils.GetLocalIp method
    /// </summary>
    [Flags]
    public enum LocalAddrType
    {
        IPv4 = 1,
        IPv6 = 2,
        All = IPv4 | IPv6
    }

    /// <summary>
    /// Some specific network utilities
    /// </summary>
    public static class NetUtils
    {
        public static IPEndPoint MakeEndPoint(string hostStr, int port)
        {
            return new IPEndPoint(ResolveAddress(hostStr), port);
        }

        public static IPAddress ResolveAddress(string hostStr)
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
#if UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
                    UnityEngine.Debug.Log(string.Format(str, args));
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
        internal static void DebugWriteForce(string str, params object[] args)
        {
            DebugWriteLogic(ConsoleColor.DarkGreen, str, args);
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

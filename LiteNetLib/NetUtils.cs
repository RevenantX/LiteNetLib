using System.Diagnostics;
#if WINRT && !UNITY_EDITOR
using Windows.Networking;
using Windows.Networking.Connectivity;
#else
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
#endif

namespace LiteNetLib
{
    static class NetUtils
    {
        public static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + NetConstants.MaxSequence + NetConstants.HalfMaxSequence) % NetConstants.MaxSequence - NetConstants.HalfMaxSequence;
        }

        public static int GetDividedPacketsCount(int size, int mtu)
        {
            return (size / mtu) + (size % mtu == 0 ? 0 : 1);
        }

        public static void PrintInterfaceInfos()
        {
#if !WINRT || UNITY_EDITOR
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

        public static string GetLocalIp(ConnectionAddressType connectionAddressType)
        {
#if WINRT && !UNITY_EDITOR
            foreach (HostName localHostName in NetworkInformation.GetHostNames())
            {
                if (localHostName.IPInformation != null)
                {
                    if (localHostName.Type == HostNameType.Ipv4)
                    {
                        return localHostName.ToString();
                    }
                }
            }
#else
            var addrFamily =
                connectionAddressType == ConnectionAddressType.IPv4
                    ? AddressFamily.InterNetwork
                    : AddressFamily.InterNetworkV6;

            IPAddress lastAddress = null;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if(ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != addrFamily)
                        continue;

                    if (ni.OperationalStatus == OperationalStatus.Up)
                        return ip.Address.ToString();

                    lastAddress = ip.Address;
                }
            }
            if (lastAddress != null)
                return lastAddress.ToString();
#endif
            return connectionAddressType == ConnectionAddressType.IPv4 ? "127.0.0.1" : "::1";
        }

        private static readonly object DebugLogLock = new object();

        [Conditional("DEBUG_MESSAGES")]
        internal static void DebugWrite(ConsoleColor color, string str, params object[] args)
        {
            lock (DebugLogLock)
            {
#if UNITY
                    string debugStr = string.Format(str, args);
                    UnityEngine.Debug.Log(debugStr);
#elif WINRT
                    Debug.WriteLine(str, args);
#else
                Console.ForegroundColor = color;
                Console.WriteLine(str, args);
                Console.ForegroundColor = ConsoleColor.Gray;
#endif
            }
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal static void DebugWriteForce(ConsoleColor color, string str, params object[] args)
        {
            lock (DebugLogLock)
            {
#if UNITY
                string debugStr = string.Format(str, args);
                UnityEngine.Debug.Log(debugStr);
#elif WINRT
                Debug.WriteLine(str, args);
#else
                Console.ForegroundColor = color;
                Console.WriteLine(str, args);
                Console.ForegroundColor = ConsoleColor.Gray;
#endif
            }
        }
    }
}

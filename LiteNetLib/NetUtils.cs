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
#if WINRT && !UNITY_EDITOR
    public struct ConsoleColor
    {
        public static readonly ConsoleColor Gray = new ConsoleColor();
        public static readonly ConsoleColor Yellow = new ConsoleColor();
        public static readonly ConsoleColor Cyan = new ConsoleColor();
        public static readonly ConsoleColor DarkCyan = new ConsoleColor();
        public static readonly ConsoleColor DarkGreen = new ConsoleColor();
        public static readonly ConsoleColor Blue = new ConsoleColor();
        public static readonly ConsoleColor DarkRed = new ConsoleColor();
        public static readonly ConsoleColor Red = new ConsoleColor();
        public static readonly ConsoleColor Green = new ConsoleColor();
    }
#endif

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
            DebugWriteForce(ConsoleColor.Green, "IPv6Support: {0}", Socket.OSSupportsIPv6);
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

        public static string GetLocalIp(bool preferIPv4 = false)
        {
#if WINRT && !UNITY_EDITOR
            foreach (HostName localHostName in NetworkInformation.GetHostNames())
            {
                if (localHostName.IPInformation != null)
                {
                    return localHostName.ToString();
                }
            }
#else
            IPAddress lastAddress = null;
            IPAddress lastAddressV6 = null;

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                            lastAddressV6 = ip.Address;
                        else
                            lastAddress = ip.Address;
                    }
                }
            }
            catch
            {
                //ignored
            }

            //Fallback mode
            if ((lastAddress == null && lastAddressV6 == null) || (lastAddress == null && preferIPv4))
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
                    if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                        lastAddressV6 = ip;
                    else
                        lastAddress = ip;
                }
            }

            //Prefer IPv4
            if (preferIPv4 && lastAddress != null)
            {
                return lastAddress.ToString();
            }

            //Try IPv6 then IPv4
            if (lastAddressV6 != null)
                return lastAddressV6.ToString();
            if (lastAddress != null)
                return lastAddress.ToString();
#endif
            return preferIPv4 ? "127.0.0.1" : "::1";
        }

        private static readonly object DebugLogLock = new object();

        private static void DebugWriteLogic(ConsoleColor color, string str, params object[] args)
        {
            lock (DebugLogLock)
            {

                if (NetDebug.Logger == null)
                {
#if UNITY
                    UnityEngine.Debug.LogFormat(str, args);
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

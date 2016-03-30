using System.Diagnostics;

#if WINRT && !UNITY_EDITOR
using Windows.Networking;
using Windows.Networking.Connectivity;
#else
using System;
using System.Net;
using System.Net.Sockets;
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

        public static string GetLocalIP()
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
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
#endif
            return "127.0.0.1";
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

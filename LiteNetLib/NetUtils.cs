using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    static class NetUtils
    {
        public static IPAddress GetHostIP(string hostname)
        {
            IPAddress addr;
            if (IPAddress.TryParse(hostname, out addr))
            {
                return addr;
            }
#if !NETFX_CORE
            IPHostEntry host = Dns.GetHostEntry(hostname);
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
#endif
            return null;
        }

        public static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + NetConstants.MaxSequence + NetConstants.HalfMaxSequence) % NetConstants.MaxSequence - NetConstants.HalfMaxSequence;
        }

        public static long GetIdFromEndPoint(IPEndPoint ep)
        {
            long id = 0;
            byte[] addr = ep.Address.GetAddressBytes();
            id |= (long)addr[0];
            id |= (long)addr[1] << 8;
            id |= (long)addr[2] << 16;
            id |= (long)addr[3] << 24;
            id |= (long)ep.Port << 32;
            return id;
        }

#if (DEBUG || UNITY_DEBUG)
        private static readonly object DebugLogLock = new object();

        public static void DebugWrite(ConsoleColor color, string str, params object[] args)
        {
#if DEBUG_MESSAGES
            lock(DebugLogLock)
            {
#if UNITY_DEBUG
                    string debugStr = string.Format(str, args);
                    UnityEngine.Debug.Log(debugStr);
#elif !WINDOWS_UWP
                    Console.ForegroundColor = color;
                    Console.WriteLine(str, args);
                    Console.ForegroundColor = ConsoleColor.Gray;
#endif
            }
#endif
        }

        public static void DebugWriteForce(ConsoleColor color, string str, params object[] args)
        {
            lock (DebugLogLock)
            {
#if UNITY_DEBUG
                string debugStr = string.Format(str, args);
                UnityEngine.Debug.Log(debugStr);
#elif !WINDOWS_UWP
                Console.ForegroundColor = color;
                Console.WriteLine(str, args);
                Console.ForegroundColor = ConsoleColor.Gray;
#endif
            }
#endif
        }
    }
}

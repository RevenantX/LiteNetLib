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

            IPHostEntry host = Dns.GetHostEntry(hostname);
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
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
        private static Object mt = new Object();
#endif

        public static void DebugWrite(ConsoleColor color, string str, params Object[] args)
        {
            DebugWrite(false, color, str, args);
        }

        public static void DebugWrite(bool showAlways, ConsoleColor color, string str, params Object[] args)
        {
#if (DEBUG || UNITY_DEBUG)
            if (showAlways)
            {
                lock (mt)
                {
#if UNITY_DEBUG
                    string debugStr = string.Format(str, args);
                    UnityEngine.Debug.Log(debugStr);
#else
                    Console.ForegroundColor = color;
                    //Console.WriteLine(str, args);
                    Console.ForegroundColor = ConsoleColor.Gray;
#endif
                }
            }
            else
            {
#if (DEBUG || UNITY_DEBUG)
                lock (mt)
                {
#if UNITY_DEBUG
                    string debugStr = string.Format(str, args);
                    UnityEngine.Debug.Log(debugStr);
#else
                    Console.ForegroundColor = color;
                    //Console.WriteLine(str, args);
                    Console.ForegroundColor = ConsoleColor.Gray;
#endif
                }
#endif
            }
#endif
        }
    }
}

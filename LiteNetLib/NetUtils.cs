using System;
using System.Diagnostics;

namespace LiteNetLib
{
    static class NetUtils
    {
        public static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + NetConstants.MaxSequence + NetConstants.HalfMaxSequence) % NetConstants.MaxSequence - NetConstants.HalfMaxSequence;
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
#elif WINRT
                    Debug.WriteLine(str, args);
#else
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
#elif WINRT
                Debug.WriteLine(str, args);
#else
                Console.ForegroundColor = color;
                Console.WriteLine(str, args);
                Console.ForegroundColor = ConsoleColor.Gray;
#endif
            }
#endif
        }
    }
}

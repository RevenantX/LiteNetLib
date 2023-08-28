using System;
using System.Diagnostics;

namespace LiteNetLib
{
    public class InvalidPacketException : ArgumentException
    {
        public InvalidPacketException(string message) : base(message)
        {
        }
    }

    public class TooBigPacketException : InvalidPacketException
    {
        public TooBigPacketException(string message) : base(message)
        {
        }
    }

    public enum NetLogLevel
    {
        Warning,
        Error,
        Trace,
        Info
    }

    /// <summary>
    /// Interface to implement for your own logger
    /// </summary>
    public interface INetLogger
    {
        void WriteNet(NetLogLevel level, string str, params object[] args);
    }

    /// <summary>
    /// Static class for defining your own LiteNetLib logger instead of Console.WriteLine
    /// or Debug.Log if running in Unity.
    /// </summary>
    public static class NetDebug
    {
        public static INetLogger Logger = null;
        private static readonly object DebugLogLock = new object();
        private static Action<string, object[]> fallbackLogMethod = (str, args) =>
        {
            Action<string, object[]> newHandler = null;
#if UNITY_5_3_OR_NEWER
            newHandler = UnityEngine.Debug.LogFormat;
#else
            if (AppEnvironment.Unity.IsAvailable && AppEnvironment.Unity.Version >= new Version(5, 3))
            {
                if (Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule")?.GetMethod("LogFormat", new[] { typeof(string), typeof(object[]) })?.CreateDelegate(typeof(Action<string, object[]>)) is Action<string, object[]> unityLog)
                    newHandler = unityLog;
            }
#endif
            if (newHandler == null)
                newHandler = Console.WriteLine;

            // Set log method function to the new handler for future calls
            fallbackLogMethod = newHandler;
            // We need to call the new handler to not ignore the first message
            fallbackLogMethod(str, args);
        };

        private static void WriteLogic(NetLogLevel logLevel, string str, params object[] args)
        {
            lock (DebugLogLock)
            {
                if (Logger == null)
                {
                    fallbackLogMethod(str, args);
                }
                else
                {
                    Logger.WriteNet(logLevel, str, args);
                }
            }
        }

        [Conditional("DEBUG_MESSAGES")]
        internal static void Write(string str)
        {
            WriteLogic(NetLogLevel.Trace, str);
        }

        [Conditional("DEBUG_MESSAGES")]
        internal static void Write(NetLogLevel level, string str)
        {
            WriteLogic(level, str);
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal static void WriteForce(string str)
        {
            WriteLogic(NetLogLevel.Trace, str);
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal static void WriteForce(NetLogLevel level, string str)
        {
            WriteLogic(level, str);
        }

        internal static void WriteError(string str)
        {
            WriteLogic(NetLogLevel.Error, str);
        }
    }
}

using System;

namespace LiteNetLib
{
    public interface INetLogger
    {
        void WriteNet(ConsoleColor color, string str, params object[] args);
    }

    public static class NetDebug
    {
        public static INetLogger Logger = null;
    }
}

using System;

namespace LiteNetLib
{
    public interface INetLogger
    {
        void NetLog(ConsoleColor color, string str, params object[] args);
    }
}

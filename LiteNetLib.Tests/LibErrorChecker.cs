using NUnit.Framework;

namespace LiteNetLib.Tests;

class LibErrorChecker : INetLogger
{
    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        if(level == NetLogLevel.Error || level == NetLogLevel.Warning)
            Assert.Fail(str, args);
    }
}

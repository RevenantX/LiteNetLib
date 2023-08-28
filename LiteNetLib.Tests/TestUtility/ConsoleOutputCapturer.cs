using System;
using System.IO;

namespace LiteNetLib.Tests.TestUtility;

internal readonly struct ConsoleOutputCapturer : IDisposable
{
    private readonly TextWriter _previousStream;
    private readonly TextWriter _innerWriter;

    public ConsoleOutputCapturer(TextWriter newStream)
    {
        _previousStream = Console.Out;
        Console.SetOut(newStream);
    }

    public ConsoleOutputCapturer(out TextWriter tempOut)
    {
        _previousStream = Console.Out;
        _innerWriter = tempOut = new StringWriter();
        Console.SetOut(tempOut);
    }

    public void Dispose()
    {
        Console.SetOut(_previousStream);
        _innerWriter?.Dispose();
    }
}

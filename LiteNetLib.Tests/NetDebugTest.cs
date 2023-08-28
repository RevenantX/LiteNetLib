using System;
using System.IO;
using LiteNetLib.Tests.TestUtility;
using NUnit.Framework;

namespace LiteNetLib.Tests;

[TestFixture]
public class NetDebugTest
{
    [Test]
    public void UsesConsoleOutputByDefault()
    {
        var input = "hello from console output";
        using (new ConsoleOutputCapturer(out TextWriter consoleOut))
        {
            NetDebug.WriteError(input);
            Assert.AreEqual(input + Environment.NewLine, consoleOut.ToString());
        }
    }

    [Test]
    public void UsesUnityLogFormatIfAvailable()
    {
        // Implements fake Unity log API: https://docs.unity3d.com/ScriptReference/Debug.LogFormat.html
        var code = """
                   namespace UnityEngine;
                   
                   public static class Debug
                   {
                       public static void LogFormat(string format, object[] args)
                       {
                           // Fake implementation to test that this is called
                           System.Console.WriteLine("[Unity] " + format);
                       }
                   }
                   """;
        using (var tempAssembly = CodeContext.CreateAndLoad("UnityEngine.CoreModule", code))
        {
            Action<string, object[]> unityLogFormat = (Action<string, object[]>)tempAssembly
                .GetType("UnityEngine.Debug, UnityEngine.CoreModule")
                ?.GetMethod("LogFormat", new[] { typeof(string), typeof(object[]) })
                ?.CreateDelegate(typeof(Action<string, object[]>));
            Assert.NotNull(unityLogFormat);
            using (new ConsoleOutputCapturer(out TextWriter tempOut))
            {
                unityLogFormat("test", Array.Empty<object>());
                Assert.AreEqual("[Unity] test" + Environment.NewLine, tempOut.ToString());
            }
        }
    }
}

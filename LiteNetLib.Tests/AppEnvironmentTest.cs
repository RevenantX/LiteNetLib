using LiteNetLib.Tests.TestUtility;
using NUnit.Framework;

namespace LiteNetLib.Tests;

[TestFixture]
public class AppEnvironmentTest
{
    [Test]
    public void UnityIsAvailable()
    {
        var unityCode = """
                        namespace UnityEngine;

                        public static class Application
                        {
                        }
                        """;
        string programCode = """
                             using System;
                             using System.Linq;
                             using System.Reflection;

                             public static class Program
                             {
                                 public static bool UnityIsAvailable()
                                 {
                                    return (bool)Type.GetType("LiteNetLib.AppEnvironment, LiteNetLib").GetNestedType("Unity", BindingFlags.NonPublic).GetProperty("IsAvailable").GetMethod.Invoke(null, null);
                                 }
                             }
                             """;

        using (var dynamicCode = new CodeContext())
        {
            dynamicCode.AddAssemblyFromCode("UnityIsAvailable", programCode);
            var isolatedExecute = dynamicCode.GetType("Program, UnityIsAvailable")?.GetMethod("UnityIsAvailable");
            Assert.NotNull(isolatedExecute);
            Assert.IsFalse((bool)isolatedExecute.Invoke(null, null));
            dynamicCode.AddAssemblyFromCode("UnityEngine.CoreModule", unityCode);
            Assert.IsTrue((bool)isolatedExecute.Invoke(null, null));
        }
    }
}

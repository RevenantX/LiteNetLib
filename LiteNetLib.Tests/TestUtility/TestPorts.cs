using System;
using System.Runtime.Versioning;

namespace LiteNetLib.Tests.TestUtility
{
    internal static class TestPorts
    {
        private const int BaselineFrameworkMajor = 8;
        private const int FrameworkPortBlockSize = 1000;

        public static int ForFramework(int basePort)
        {
            var frameworkAttribute = (TargetFrameworkAttribute)Attribute.GetCustomAttribute(
                typeof(TestPorts).Assembly,
                typeof(TargetFrameworkAttribute));

            if (frameworkAttribute == null)
                return basePort;

            const string versionMarker = "Version=v";
            var versionStart = frameworkAttribute.FrameworkName.IndexOf(versionMarker, StringComparison.Ordinal);
            if (versionStart < 0)
                return basePort;

            var versionText = frameworkAttribute.FrameworkName.Substring(versionStart + versionMarker.Length);
            if (!Version.TryParse(versionText, out var version))
                return basePort;

            return basePort + Math.Max(0, version.Major - BaselineFrameworkMajor) * FrameworkPortBlockSize;
        }
    }
}

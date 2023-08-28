using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LiteNetLib
{
    internal static class AppEnvironment
    {
        internal static class Unity
        {
            [ThreadStatic] private static Version _version;
            private static readonly Version _fallbackVersion = new Version(1, 0);
            [ThreadStatic] private static bool? _isEditor;
            [ThreadStatic] private static bool? _isIl2cpp;
            [ThreadStatic] private static bool? _isAndroidPlatform;
            [ThreadStatic] private static bool? _isSwitchPlatform;
            private static object _unityApplicationTypeLock = new object();
            private static Type _unityApplicationType;

            private static Type UnityApplicationType
            {
                get
                {
                    lock (_unityApplicationTypeLock)
                    {

                        if (_unityApplicationType != null)
                        {
                            return _unityApplicationType;
                        }

#if UNITY_2018_3_OR_NEWER
                        return _unityApplicationType = typeof(UnityEngine.Application);
#else
                        return _unityApplicationType = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "UnityEngine.CoreModule")
                            ?.GetType("UnityEngine.Application");
#endif
                    }
                }
            }

            public static bool IsAvailable => UnityApplicationType != null;

            /// <summary>
            ///     Gets the version of the currently executing Unity engine.
            /// </summary>
            public static Version Version
            {
                get
                {
                    if (_version != null)
                    {
                        return _version;
                    }
                    if (!IsAvailable)
                    {
                        return _version = _fallbackVersion;
                    }

                    string version;
#if UNITY_2018_3_OR_NEWER
                    version = UnityEngine.Application.unityVersion;
#else
                    version = UnityApplicationType?.GetProperty("unityVersion")?.GetValue(null) as string;
#endif
                    if (version == null)
                        return _version = _fallbackVersion;
                    // See https://regex101.com/r/gZQvfX/1 to test regex
                    version = new Regex(@"\d+(?:\.\d*){1,3}").Match(version).Value;
                    if (!Version.TryParse(version, out Version result))
                        return null;
                    return _version = result;
                }
            }

            public static bool IsAndroidPlatform
            {
                get
                {
#if UNITY_2018_3_OR_NEWER
                    return UnityEngine.Application.platform == RuntimePlatform.Android;
#else
                    if (!_isAndroidPlatform.HasValue)
                    {
                        _isAndroidPlatform = UnityApplicationType
                                                 ?.GetProperty("platform", BindingFlags.Public | BindingFlags.Static)
                                                 ?.GetValue(null)
                                                 ?.ToString()
                                                 ?.Equals("android", StringComparison.OrdinalIgnoreCase) ??
                                             false;
                    }
                    return _isAndroidPlatform.Value;
#endif
                }
            }

            public static bool IsSwitchPlatform
            {
                get
                {
#if UNITY_2018_3_OR_NEWER
                    return UnityEngine.Application.platform == RuntimePlatform.Switch;
#else
                    if (!_isSwitchPlatform.HasValue)
                    {
                        _isSwitchPlatform = UnityApplicationType
                                                ?.GetProperty("platform", BindingFlags.Public | BindingFlags.Static)
                                                ?.GetValue(null)
                                                ?.ToString()
                                                .Equals("switch", StringComparison.OrdinalIgnoreCase) ??
                                            false;
                    }
                    return _isSwitchPlatform.Value;
#endif
                }
            }

            public static bool IsEditor
            {
                get
                {
#if UNITY_2018_3_OR_NEWER
                    return UnityEngine.Application.isEditor;
#else
                    if (!_isEditor.HasValue)
                    {
                        _isEditor = UnityApplicationType
                            ?.GetProperty("isEditor", BindingFlags.Public | BindingFlags.Static)
                            ?.GetValue(null) is true;
                    }
                    return _isEditor.Value;
#endif
                }
            }

            public static bool IsIl2cpp
            {
                get
                {
#if ENABLE_IL2CPP
                    return true;
#else
                    if (!_isIl2cpp.HasValue)
                    {
                        _isIl2cpp = Type.GetType("System.__Il2CppComObject") != null;
                    }
                    return _isIl2cpp.Value;
#endif
                }
            }
        }
    }
}

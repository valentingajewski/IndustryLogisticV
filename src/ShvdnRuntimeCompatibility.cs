using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GTA;
using LSOL.Config;
using WinForms = System.Windows.Forms;

namespace LSOL
{
    internal static class ShvdnRuntimeCompatibility
    {
        private const string SdkDirectoryName = "scripthookdotnet";

        private static readonly object AssemblyResolverSync = new object();
        private static bool _assemblyResolverInstalled;
        
        private static readonly object NotificationResolveSync = new object();
        private static bool _notificationResolved;
        private static MethodInfo _notificationPostTickerMethod;
        private static MethodInfo _notificationShowMethod;
        private static MethodInfo _screenShowSubtitleMethod;

        public static void EnsureInitialized(string baseDirectory, string assemblyLocation)
        {
            InstallAssemblyResolver(baseDirectory, assemblyLocation);
            ValidateScriptEventSurface();
        }

        public static void PostTicker(string text, bool blink, bool important)
        {
            EnsureNotificationResolved();

            if (_notificationPostTickerMethod != null)
            {
                try
                {
                    _notificationPostTickerMethod.Invoke(null, new object[] { text, blink, important });
                    return;
                }
                catch
                {
                }
            }

            if (_notificationShowMethod != null)
            {
                try
                {
                    _notificationShowMethod.Invoke(null, new object[] { text });
                    return;
                }
                catch
                {
                }
            }

            if (_screenShowSubtitleMethod != null)
            {
                try
                {
                    _screenShowSubtitleMethod.Invoke(null, new object[] { text, 5000 });
                    return;
                }
                catch
                {
                }
            }
        }

        private static void EnsureNotificationResolved()
        {
            if (_notificationResolved)
            {
                return;
            }

            lock (NotificationResolveSync)
            {
                if (_notificationResolved)
                {
                    return;
                }

                try
                {
                    var notificationType = typeof(GTA.UI.Notification);
                    if (notificationType != null)
                    {
                        _notificationPostTickerMethod = notificationType.GetMethod("PostTicker", BindingFlags.Public | BindingFlags.Static);
                        _notificationShowMethod = notificationType.GetMethod("Show", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                    }

                    var screenType = typeof(GTA.UI.Screen);
                    if (screenType != null)
                    {
                        _screenShowSubtitleMethod = screenType.GetMethod("ShowSubtitle", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(int) }, null);
                    }
                }
                catch
                {
                }

                _notificationResolved = true;
            }
        }

        internal static IReadOnlyList<string> GetManagedDependencyProbeDirectories(string baseDirectory, string assemblyLocation)
        {
            var directories = new List<string>();
            var normalizedBaseDirectory = NormalizeDirectoryPath(baseDirectory);
            var normalizedAssemblyDirectory = NormalizeDirectoryPath(Path.GetDirectoryName(assemblyLocation));

            AddDirectory(directories, normalizedAssemblyDirectory);
            AddDirectory(directories, normalizedBaseDirectory);

            if (!string.IsNullOrWhiteSpace(normalizedBaseDirectory))
            {
                AddDirectory(directories, Path.Combine(normalizedBaseDirectory, RuntimeLayoutResolver.ScriptsDirectoryName));
            }

            AddSdkProbeDirectories(directories, normalizedAssemblyDirectory);
            AddSdkProbeDirectories(directories, normalizedBaseDirectory);

            return directories;
        }

        internal static bool ShouldResolveManagedDependency(string assemblyName)
        {
            return assemblyName.Equals("ScriptHookVDotNet3", StringComparison.OrdinalIgnoreCase)
                || assemblyName.Equals("LemonUI.SHVDN3", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ValidateScriptEventSurface()
        {
            ValidateScriptEvent(nameof(Script.Tick), typeof(EventHandler));
            ValidateScriptEvent(nameof(Script.KeyDown), typeof(WinForms.KeyEventHandler));
            ValidateScriptEvent(nameof(Script.KeyUp), typeof(WinForms.KeyEventHandler));
            ValidateScriptEvent(nameof(Script.Aborted), typeof(EventHandler));
        }

        private static void InstallAssemblyResolver(string baseDirectory, string assemblyLocation)
        {
            var probeDirectories = GetManagedDependencyProbeDirectories(baseDirectory, assemblyLocation);

            lock (AssemblyResolverSync)
            {
                if (_assemblyResolverInstalled)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => ResolveManagedDependency(args, probeDirectories);
                _assemblyResolverInstalled = true;
            }
        }

        private static Assembly ResolveManagedDependency(ResolveEventArgs args, IReadOnlyList<string> probeDirectories)
        {
            var requestedAssemblyName = GetAssemblySimpleName(args != null ? args.Name : null);
            if (!ShouldResolveManagedDependency(requestedAssemblyName))
            {
                return null;
            }

            for (int i = 0; i < probeDirectories.Count; i++)
            {
                var candidatePath = Path.Combine(probeDirectories[i], requestedAssemblyName + ".dll");
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                try
                {
                    return Assembly.LoadFrom(candidatePath);
                }
                catch
                {
                }
            }

            return null;
        }

        private static void ValidateScriptEvent(string eventName, Type expectedHandlerType)
        {
            var eventInfo = typeof(Script).GetEvent(eventName);
            if (eventInfo != null && eventInfo.EventHandlerType == expectedHandlerType)
            {
                return;
            }

            var actualTypeName = eventInfo == null || eventInfo.EventHandlerType == null
                ? "<missing>"
                : eventInfo.EventHandlerType.FullName;
            throw new NotSupportedException(
                string.Format(
                    "The active SHVDN v3 runtime is incompatible with LSOL: GTA.Script.{0} must use {1}, but found {2}.",
                    eventName,
                    expectedHandlerType.FullName,
                    actualTypeName));
        }

        private static void AddSdkProbeDirectories(ICollection<string> directories, string originDirectory)
        {
            var current = NormalizeDirectoryPath(originDirectory);
            var depth = 0;
            while (!string.IsNullOrWhiteSpace(current) && depth < 6)
            {
                AddDirectory(directories, Path.Combine(current, SdkDirectoryName));
                current = NormalizeDirectoryPath(Path.GetDirectoryName(current));
                depth += 1;
            }
        }

        private static void AddDirectory(ICollection<string> directories, string path)
        {
            var normalizedPath = NormalizeDirectoryPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            foreach (var existingPath in directories)
            {
                if (string.Equals(existingPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            directories.Add(normalizedPath);
        }

        private static string GetAssemblySimpleName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            try
            {
                return new AssemblyName(assemblyName).Name ?? string.Empty;
            }
            catch
            {
                return assemblyName.Trim();
            }
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
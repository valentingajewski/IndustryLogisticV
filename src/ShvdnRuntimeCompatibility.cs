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

        public static void EnsureInitialized(string baseDirectory, string assemblyLocation)
        {
            InstallAssemblyResolver(baseDirectory, assemblyLocation);
            ValidateScriptEventSurface();
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
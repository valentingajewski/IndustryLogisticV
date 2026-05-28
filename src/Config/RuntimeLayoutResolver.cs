using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LSOL.Config
{
    public sealed class RuntimeLayoutPaths
    {
        public RuntimeLayoutPaths(string scriptsDirectory, string runtimeDirectory, bool usesNestedLayout)
        {
            ScriptsDirectory = scriptsDirectory ?? string.Empty;
            RuntimeDirectory = runtimeDirectory ?? string.Empty;
            UsesNestedLayout = usesNestedLayout;
        }

        public string ScriptsDirectory { get; }

        public string RuntimeDirectory { get; }

        public bool UsesNestedLayout { get; }

        public bool UsesLegacyFallback
        {
            get { return !UsesNestedLayout; }
        }

        public string ConfigDirectory
        {
            get { return Combine(RuntimeDirectory, RuntimeLayoutResolver.ConfigDirectoryName); }
        }

        public string MissionDirectory
        {
            get { return Combine(ConfigDirectory, "missions"); }
        }

        public string AddonsDirectory
        {
            get { return Combine(RuntimeDirectory, RuntimeLayoutResolver.AddonsDirectoryName); }
        }

        public string DefaultStatePath
        {
            get { return Combine(RuntimeDirectory, RuntimeLayoutResolver.StateFileName); }
        }

        public string SavegamesDirectory
        {
            get { return Combine(RuntimeDirectory, RuntimeLayoutResolver.SavegamesDirectoryName); }
        }

        private static string Combine(string root, string name)
        {
            return string.IsNullOrWhiteSpace(root)
                ? string.Empty
                : Path.Combine(root, name);
        }
    }

    public static class RuntimeLayoutResolver
    {
        public const string ScriptsDirectoryName = "scripts";
        public const string RuntimeDirectoryName = "LSOL";
        public const string ConfigDirectoryName = "LSOL_Config";
        public const string AddonsDirectoryName = "LSOL_Addons";
        public const string SavegamesDirectoryName = "LSOLSaves";
        public const string StateFileName = "LSOL.state.xml";

        public const string PreferredRuntimeDirectoryDisplayPath = "scripts/LSOL";
        public const string PreferredConfigDirectoryDisplayPath = "scripts/LSOL/LSOL_Config";
        public const string PreferredAddonsDirectoryDisplayPath = "scripts/LSOL/LSOL_Addons";
        public const string PreferredMissionDirectoryDisplayPath = "scripts/LSOL/LSOL_Config/missions";
        public const string PreferredAddonMissionDirectoryDisplayPath = "scripts/LSOL/LSOL_Addons/*/content/missions";
        public const string PreferredSavegamesDirectoryDisplayPath = "scripts/LSOL/LSOLSaves";
        public const string PreferredStateFileDisplayPath = "scripts/LSOL/LSOL.state.xml";

        public static RuntimeLayoutPaths Resolve(string baseDirectory, string assemblyLocation)
        {
            var scriptDirectoryCandidates = BuildScriptDirectoryCandidates(baseDirectory, assemblyLocation);

            for (int i = 0; i < scriptDirectoryCandidates.Count; i++)
            {
                var nestedRuntimeDirectory = Path.Combine(scriptDirectoryCandidates[i], RuntimeDirectoryName);
                if (Directory.Exists(Path.Combine(nestedRuntimeDirectory, ConfigDirectoryName)))
                {
                    return new RuntimeLayoutPaths(scriptDirectoryCandidates[i], nestedRuntimeDirectory, true);
                }
            }

            for (int i = 0; i < scriptDirectoryCandidates.Count; i++)
            {
                if (Directory.Exists(Path.Combine(scriptDirectoryCandidates[i], ConfigDirectoryName)))
                {
                    return new RuntimeLayoutPaths(scriptDirectoryCandidates[i], scriptDirectoryCandidates[i], false);
                }
            }

            var preferredScriptsDirectory = scriptDirectoryCandidates.FirstOrDefault(Directory.Exists)
                ?? scriptDirectoryCandidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? NormalizeDirectoryPath(Environment.CurrentDirectory);

            return new RuntimeLayoutPaths(
                preferredScriptsDirectory,
                string.IsNullOrWhiteSpace(preferredScriptsDirectory)
                    ? string.Empty
                    : Path.Combine(preferredScriptsDirectory, RuntimeDirectoryName),
                true);
        }

        public static RuntimeLayoutPaths FromConfigDirectory(string configDirectory)
        {
            var fullConfigDirectory = NormalizeDirectoryPath(configDirectory);
            if (string.IsNullOrWhiteSpace(fullConfigDirectory))
            {
                return new RuntimeLayoutPaths(string.Empty, string.Empty, true);
            }

            var runtimeDirectory = Path.GetDirectoryName(fullConfigDirectory) ?? string.Empty;
            var usesNestedLayout = Path.GetFileName(runtimeDirectory)
                .Equals(RuntimeDirectoryName, StringComparison.OrdinalIgnoreCase);
            var scriptsDirectory = usesNestedLayout
                ? Path.GetDirectoryName(runtimeDirectory) ?? runtimeDirectory
                : runtimeDirectory;

            return new RuntimeLayoutPaths(scriptsDirectory, runtimeDirectory, usesNestedLayout);
        }

        public static string BuildPreferredConfigFileDisplayPath(string fileName)
        {
            var normalizedFileName = NormalizeDisplayPath(fileName);
            return string.IsNullOrWhiteSpace(normalizedFileName)
                ? PreferredConfigDirectoryDisplayPath
                : string.Format("{0}/{1}", PreferredConfigDirectoryDisplayPath, normalizedFileName);
        }

        public static string BuildPreferredAddonContentDisplayPath(string packageFolderName, string relativePath)
        {
            var packageSegment = string.IsNullOrWhiteSpace(packageFolderName)
                ? "*"
                : NormalizeDisplayPath(packageFolderName).Replace("/", string.Empty);
            var normalizedRelativePath = NormalizeDisplayPath(relativePath);
            return string.IsNullOrWhiteSpace(normalizedRelativePath)
                ? string.Format("{0}/{1}", PreferredAddonsDirectoryDisplayPath, packageSegment)
                : string.Format("{0}/{1}/{2}", PreferredAddonsDirectoryDisplayPath, packageSegment, normalizedRelativePath);
        }

        private static List<string> BuildScriptDirectoryCandidates(string baseDirectory, string assemblyLocation)
        {
            var candidates = new List<string>();
            AddCandidate(candidates, Path.GetDirectoryName(assemblyLocation));

            var normalizedBaseDirectory = NormalizeDirectoryPath(baseDirectory);
            if (!string.IsNullOrWhiteSpace(normalizedBaseDirectory))
            {
                var baseLeaf = Path.GetFileName(normalizedBaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (baseLeaf.Equals(ScriptsDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    AddCandidate(candidates, normalizedBaseDirectory);
                }
                else
                {
                    AddCandidate(candidates, Path.Combine(normalizedBaseDirectory, ScriptsDirectoryName));
                    AddCandidate(candidates, normalizedBaseDirectory);
                }
            }

            return candidates;
        }

        private static void AddCandidate(ICollection<string> candidates, string path)
        {
            var normalizedPath = NormalizeDirectoryPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            if (!candidates.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(normalizedPath);
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

        private static string NormalizeDisplayPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path
                .Trim()
                .Replace('\\', '/')
                .Trim('/');
        }
    }
}
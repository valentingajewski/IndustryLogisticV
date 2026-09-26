using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Config
{
    [TestClass]
    public sealed class ShvdnRuntimeCompatibilityTests
    {
        [TestMethod]
        public void GetManagedDependencyProbeDirectories_ClimbsToRepoSdkDirectoryFromBuildOutput()
        {
            var root = CreateTempDirectory();

            try
            {
                var buildOutputDirectory = Path.Combine(root, "bin", "Release", "net48");
                var assemblyLocation = Path.Combine(buildOutputDirectory, "LSOL.dll");
                var expectedSdkDirectory = Path.Combine(root, "scripthookdotnet");

                var directories = ShvdnRuntimeCompatibility.GetManagedDependencyProbeDirectories(buildOutputDirectory, assemblyLocation);

                CollectionAssert.Contains((System.Collections.ICollection)directories, Path.GetFullPath(expectedSdkDirectory));
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        [TestMethod]
        public void GetManagedDependencyProbeDirectories_IncludesScriptsDirectoryForGameRootBaseDirectory()
        {
            var root = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(root, "scripts");
                var assemblyLocation = Path.Combine(scriptsDirectory, "LSOL.dll");

                var directories = ShvdnRuntimeCompatibility.GetManagedDependencyProbeDirectories(root, assemblyLocation);

                CollectionAssert.Contains((System.Collections.ICollection)directories, Path.GetFullPath(scriptsDirectory));
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        [TestMethod]
        public void ValidateScriptEventSurface_AcceptsCurrentShvdn3Signature()
        {
            ShvdnRuntimeCompatibility.ValidateScriptEventSurface();
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "lsol-shvdn-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteTempDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            Directory.Delete(directory, true);
        }
    }
}
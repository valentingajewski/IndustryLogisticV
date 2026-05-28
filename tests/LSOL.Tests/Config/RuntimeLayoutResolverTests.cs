using System;
using System.IO;
using LSOL.Config;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Config
{
    [TestClass]
    public sealed class RuntimeLayoutResolverTests
    {
        [TestMethod]
        public void Resolve_PrefersNestedScriptsLsolLayout_WhenNestedAndLegacyConfigsExist()
        {
            var gameRoot = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(gameRoot, RuntimeLayoutResolver.ScriptsDirectoryName);
                Directory.CreateDirectory(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.ConfigDirectoryName));
                Directory.CreateDirectory(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.ConfigDirectoryName));

                var layout = RuntimeLayoutResolver.Resolve(gameRoot, Path.Combine(scriptsDirectory, "LSOL.dll"));

                Assert.IsTrue(layout.UsesNestedLayout);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName), layout.RuntimeDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.ConfigDirectoryName), layout.ConfigDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.AddonsDirectoryName), layout.AddonsDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.StateFileName), layout.DefaultStatePath);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.SavegamesDirectoryName), layout.SavegamesDirectory);
            }
            finally
            {
                DeleteTempDirectory(gameRoot);
            }
        }

        [TestMethod]
        public void Resolve_FallsBackToLegacyTopLevelLayout_WhenNestedConfigIsMissing()
        {
            var gameRoot = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(gameRoot, RuntimeLayoutResolver.ScriptsDirectoryName);
                Directory.CreateDirectory(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.ConfigDirectoryName));

                var layout = RuntimeLayoutResolver.Resolve(gameRoot, Path.Combine(scriptsDirectory, "LSOL.dll"));

                Assert.IsTrue(layout.UsesLegacyFallback);
                Assert.AreEqual(scriptsDirectory, layout.RuntimeDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.ConfigDirectoryName), layout.ConfigDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.AddonsDirectoryName), layout.AddonsDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.StateFileName), layout.DefaultStatePath);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.SavegamesDirectoryName), layout.SavegamesDirectory);
            }
            finally
            {
                DeleteTempDirectory(gameRoot);
            }
        }

        [TestMethod]
        public void Resolve_DefaultsToNestedScriptsLsolLayout_ForFreshInstallPaths()
        {
            var gameRoot = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(gameRoot, RuntimeLayoutResolver.ScriptsDirectoryName);
                Directory.CreateDirectory(scriptsDirectory);

                var layout = RuntimeLayoutResolver.Resolve(gameRoot, Path.Combine(scriptsDirectory, "LSOL.dll"));

                Assert.IsTrue(layout.UsesNestedLayout);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName), layout.RuntimeDirectory);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.StateFileName), layout.DefaultStatePath);
                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.SavegamesDirectoryName), layout.SavegamesDirectory);
            }
            finally
            {
                DeleteTempDirectory(gameRoot);
            }
        }

        [TestMethod]
        public void LoadAddonCatalog_UsesNestedAddonsDirectory_FromNestedConfigRoot()
        {
            var gameRoot = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(gameRoot, RuntimeLayoutResolver.ScriptsDirectoryName);
                var configDirectory = Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.ConfigDirectoryName);
                var packageDirectory = Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.AddonsDirectoryName, "unit-test-pack");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(Path.Combine(packageDirectory, "content", "missions"));
                File.WriteAllText(
                    Path.Combine(packageDirectory, "addon.xml"),
                    "<Addon id=\"unit.test.pack\" version=\"1.0.0\" category=\"mission-pack\" lsolApiVersion=\"1\"><Metadata name=\"Unit Test Pack\" author=\"Tests\" description=\"Covers nested add-on resolution.\" /><Compatibility minLSOLVersion=\"1.0.0\" maxTestedLSOLVersion=\"99.0.0\" /><Capabilities><Capability name=\"content.missions\" /></Capabilities><Content><Directory type=\"missions\" path=\"content/missions\" /></Content></Addon>");

                var catalog = LsolAddonCatalog.Load(configDirectory);

                Assert.AreEqual(Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.AddonsDirectoryName), catalog.AddonsDirectory);
                Assert.AreEqual(1, catalog.Packages.Count);
                Assert.AreEqual("unit.test.pack", catalog.Packages[0].Id);
            }
            finally
            {
                DeleteTempDirectory(gameRoot);
            }
        }

        [TestMethod]
        public void LoadSpecialMissionCatalog_LoadsMissionDefinitions_FromNestedMissionDirectory()
        {
            var gameRoot = CreateTempDirectory();

            try
            {
                var scriptsDirectory = Path.Combine(gameRoot, RuntimeLayoutResolver.ScriptsDirectoryName);
                var configDirectory = Path.Combine(scriptsDirectory, RuntimeLayoutResolver.RuntimeDirectoryName, RuntimeLayoutResolver.ConfigDirectoryName);
                var missionDirectory = Path.Combine(configDirectory, "missions");
                Directory.CreateDirectory(missionDirectory);
                File.WriteAllText(
                    Path.Combine(missionDirectory, "unit-test.xml"),
                    "<Mission id=\"unit.test.mission\" type=\"TrailerDelivery\" name=\"Unit Test Mission\" reward=\"1250\"><Vehicles><Vehicle role=\"Trailer\" model=\"trailersmall\" x=\"1\" y=\"2\" z=\"3\" /></Vehicles><Zones><Zone id=\"Destination\" x=\"10\" y=\"20\" z=\"30\" radius=\"12\" /></Zones></Mission>");

                var catalog = SpecialMissionCatalog.Load(configDirectory);

                Assert.AreEqual(1, catalog.Definitions.Count);
                Assert.AreEqual("unit.test.mission", catalog.Definitions[0].Id);
                Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, catalog.ValidationMessages));
            }
            finally
            {
                DeleteTempDirectory(gameRoot);
            }
        }

        private static string CreateTempDirectory()
        {
            var filePath = TestWorkspace.CreateTempFilePath("placeholder.tmp");
            var directory = Path.GetDirectoryName(filePath);
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
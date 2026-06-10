using System.Linq;
using System.IO;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Config
{
    [TestClass]
    public sealed class ModConfigXmlLoadingTests
    {
        [TestMethod]
        public void Load_WithRepoLsolConfig_PopulatesRuntimeContentFromXml()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");

            var config = ModConfig.Load(configDirectory);

            Assert.IsTrue(config.DistrictConfigs.ContainsKey("Port"));
            Assert.IsTrue(config.IndustryConfigs.ContainsKey("HarborTerminal"));
            Assert.IsTrue(config.IndustryConfigs.ContainsKey("SmeltingFactory"));

            var harborTerminal = config.IndustryConfigs["HarborTerminal"];
            var smeltingFactory = config.IndustryConfigs["SmeltingFactory"];
            var quarry = config.IndustryConfigs.Values.Single(industry => industry != null && industry.LegacyKey == "MineralMine");
            var davisStore = config.IndustryConfigs["Store1"];
            var burtonMall = config.IndustryConfigs["Store2"];
            var elRanchoGasStation = config.IndustryConfigs["Petrol Station 02"];
            var officeGasStation = config.IndustryConfigs["Petrol Station 23"];
            var mainOffice = config.OfficeDefinitions.SingleOrDefault(definition => definition.OfficeId == "1");

            Assert.AreEqual("Port", harborTerminal.DistrictName);
            Assert.AreEqual(SiteRole.Warehouse, harborTerminal.SiteRole);
            CollectionAssert.AreEquivalent(
                new[] { unchecked((int)3008087081u), unchecked((int)3066743879u) },
                smeltingFactory.ObjectToDeleteModelHashes.ToArray());
            Assert.AreEqual(3600f, davisStore.WeeklyPassiveIncome, 0.01f);
            Assert.AreEqual(420f, burtonMall.WeeklyPassiveIncome, 0.01f);
            Assert.IsTrue(davisStore.WeeklyPassiveIncome > burtonMall.WeeklyPassiveIncome);
            Assert.AreEqual(4500f, elRanchoGasStation.WeeklyPassiveIncome, 0.01f);
            Assert.AreEqual(0f, officeGasStation.WeeklyPassiveIncome, 0.01f);
            Assert.IsNotNull(mainOffice);
            Assert.AreEqual("Elysian Island Logistic Office", mainOffice.SiteName);
            Assert.AreEqual("Port", mainOffice.DistrictName);
            Assert.IsTrue(quarry.GatePosition.HasValue);
            Assert.AreEqual(new Vector3(2570.45f, 2710.2f, 41.69f), quarry.GatePosition.Value);
            Assert.IsTrue(mainOffice.GatePosition.HasValue);
            Assert.AreEqual(new Vector3(10.43f, -2538.56f, 5.55f), mainOffice.GatePosition.Value);
        }

        [TestMethod]
        public void Load_WithMissingWeeklyPassiveIncome_DerivesBackwardCompatibleServiceSinkDefault()
        {
            var repoConfigDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var tempSitesPath = TestWorkspace.CreateTempFilePath("Sites.xml");
            var tempConfigDirectory = Path.GetDirectoryName(tempSitesPath);

            File.Copy(Path.Combine(repoConfigDirectory, "Core.xml"), Path.Combine(tempConfigDirectory, "Core.xml"));
            File.Copy(Path.Combine(repoConfigDirectory, "Resources.xml"), Path.Combine(tempConfigDirectory, "Resources.xml"));
            File.Copy(Path.Combine(repoConfigDirectory, "Districts.xml"), Path.Combine(tempConfigDirectory, "Districts.xml"));

            var sitesXml = File.ReadAllText(Path.Combine(repoConfigDirectory, "Sites.xml"));
            sitesXml = sitesXml.Replace(" weeklyPassiveIncome=\"3600\"", string.Empty);
            File.WriteAllText(Path.Combine(tempConfigDirectory, "Sites.xml"), sitesXml);

            var config = ModConfig.Load(tempConfigDirectory);

            Assert.AreEqual(3600f, config.IndustryConfigs["Store1"].WeeklyPassiveIncome, 0.01f);
        }

        [TestMethod]
        public void Load_WithRepoLsolConfig_PopulatesManualDumperLooseCargoRules()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");

            var config = ModConfig.Load(configDirectory);
            var rubbleLayout = config.VehicleObjectLayouts.Single(layout => layout != null && layout.ModelName == "rubble");
            var tiptruckLayout = config.VehicleObjectLayouts.Single(layout => layout != null && layout.ModelName == "tiptruck");
            var tiptruck2Layout = config.VehicleObjectLayouts.Single(layout => layout != null && layout.ModelName == "tiptruck2");

            AssertLooseCargoRule(rubbleLayout, 28);
            AssertLooseCargoRule(tiptruckLayout, 10);
            AssertLooseCargoRule(tiptruck2Layout, 10);
        }

        [TestMethod]
        public void Load_WithRepoLsolConfig_KeepsDeveloperModeDisabledByDefault()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");

            var config = ModConfig.Load(configDirectory);

            Assert.IsFalse(config.DeveloperModeEnabled);
        }

        [TestMethod]
        public void Load_WithDeveloperFlagEnabled_EnablesDeveloperMode()
        {
            var config = LoadWithCoreDeveloperElement("<Developer enableDeveloperMode=\"true\" passkey=\"\" />");

            Assert.IsTrue(config.DeveloperModeEnabled);
        }

        [TestMethod]
        public void Load_WithDeveloperPasskeySet_EnablesDeveloperMode()
        {
            var config = LoadWithCoreDeveloperElement("<Developer enableDeveloperMode=\"false\" passkey=\"abc123\" />");

            Assert.IsTrue(config.DeveloperModeEnabled);
        }

        private static ModConfig LoadWithCoreDeveloperElement(string developerElement)
        {
            var repoConfigDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var tempSitesPath = TestWorkspace.CreateTempFilePath("Sites.xml");
            var tempConfigDirectory = Path.GetDirectoryName(tempSitesPath);

            File.Copy(Path.Combine(repoConfigDirectory, "Resources.xml"), Path.Combine(tempConfigDirectory, "Resources.xml"));
            File.Copy(Path.Combine(repoConfigDirectory, "Districts.xml"), Path.Combine(tempConfigDirectory, "Districts.xml"));
            File.Copy(Path.Combine(repoConfigDirectory, "Sites.xml"), Path.Combine(tempConfigDirectory, "Sites.xml"));

            var coreXml = File.ReadAllText(Path.Combine(repoConfigDirectory, "Core.xml"));
            coreXml = coreXml.Replace("<Developer enableDeveloperMode=\"false\" passkey=\"\" />", developerElement);
            File.WriteAllText(Path.Combine(tempConfigDirectory, "Core.xml"), coreXml);

            return ModConfig.Load(tempConfigDirectory);
        }

        private static void AssertLooseCargoRule(VehicleObjectLayoutDefinition layout, int expectedMaxPropCount)
        {
            Assert.IsNotNull(layout);

            var oreRule = layout.FindLooseCargoVisual("Ore", VehicleCargoType.Aggregates);
            var cropsRule = layout.FindLooseCargoVisual("Crops", VehicleCargoType.Aggregates);

            Assert.IsNotNull(oreRule, layout.ModelName + " ore loose cargo");
            Assert.AreEqual("Gravel", oreRule.ObjectKey, layout.ModelName + " objectKey");
            Assert.AreEqual(expectedMaxPropCount, oreRule.MaxPropCount, layout.ModelName + " maxPropCount");
            Assert.IsTrue(oreRule.ManualSlots.Count >= 1, layout.ModelName + " manual slot count");
            Assert.IsFalse(float.IsNaN(oreRule.ManualSlots[0].HeadingDegrees), layout.ModelName + " first manual slot heading");
            Assert.IsFalse(float.IsNaN(oreRule.ManualSlots[0].PitchDegrees), layout.ModelName + " first manual slot pitch");
            Assert.IsFalse(float.IsNaN(oreRule.ManualSlots[0].RollDegrees), layout.ModelName + " first manual slot roll");
            CollectionAssert.AreEquivalent(new[] { "Coal", "Gravel", "Ore" }, oreRule.Commodities.ToArray(), layout.ModelName + " commodities");
            Assert.IsNull(cropsRule, layout.ModelName + " crops loose cargo");
        }
    }
}
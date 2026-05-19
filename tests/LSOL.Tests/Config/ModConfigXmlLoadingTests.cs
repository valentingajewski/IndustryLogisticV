using System.Linq;
using System.IO;
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
    }
}
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
            var mainOffice = config.OfficeDefinitions.SingleOrDefault(definition => definition.OfficeId == "1");

            Assert.AreEqual("Port", harborTerminal.DistrictName);
            Assert.AreEqual(SiteRole.Warehouse, harborTerminal.SiteRole);
            CollectionAssert.AreEquivalent(
                new[] { unchecked((int)3008087081u), unchecked((int)3066743879u) },
                smeltingFactory.ObjectToDeleteModelHashes.ToArray());
            Assert.IsNotNull(mainOffice);
            Assert.AreEqual("Elysian Island Logistic Office", mainOffice.SiteName);
            Assert.AreEqual("Port", mainOffice.DistrictName);
        }
    }
}
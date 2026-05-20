using System.Collections.Generic;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class CargoTransferControllerTests
    {
        [TestMethod]
        public void ResolveUnloadTargetTons_ClampsRequestedCargoToDestinationFreeCapacity()
        {
            var industry = CreateIndustry("steel_sink", 10f, "Steel");
            industry.BufferStorage["Steel"] = 7f;

            var targetTons = CargoTransferController.ResolveUnloadTargetTons(industry, "Steel", 8f);

            Assert.AreEqual(3f, targetTons, 0.001f);
        }

        [TestMethod]
        public void BuildUnloadingTransferLabel_UsesDeliverableTargetTons()
        {
            var label = CargoTransferController.BuildUnloadingTransferLabel(1.5f, 3f, "Steel");

            Assert.AreEqual("Unloading 1.50/3.00t Steel...", label);
        }

        private static Industry CreateIndustry(string id, float inputCapacityTons, params string[] inputs)
        {
            return new Industry(
                new IndustryConfig
                {
                    Id = id,
                    LegacyKey = id,
                    Name = id,
                    LocationKind = ExternalLocationKind.Industry,
                    SiteRole = SiteRole.ProcessingPlant,
                    InputCapacityTons = inputCapacityTons,
                    OutputCapacityTons = 10f,
                    Inputs = new HashSet<string>(inputs),
                    Outputs = new HashSet<string>(),
                },
                new List<ProductionRecipe>(),
                false,
                1f);
        }
    }
}
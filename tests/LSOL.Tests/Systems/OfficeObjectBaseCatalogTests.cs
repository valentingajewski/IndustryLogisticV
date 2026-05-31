using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class OfficeObjectBaseCatalogTests
    {
        [TestMethod]
        public void BaseCatalog_RemovesRedundantDeskObjects_AndLeavesCabinAsSoleHireNpcSurface()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var officeObjects = config.OfficeObjectDefinitions.ToList();
            var names = officeObjects.Select(definition => definition.DisplayName).ToList();

            CollectionAssert.DoesNotContain(names, "Reception Desk Cluster");
            CollectionAssert.DoesNotContain(names, "Dispatch Console");
            CollectionAssert.DoesNotContain(names, "Fuel Service Counter");

            var hireNpcDefinitions = officeObjects
                .Where(definition => definition != null && definition.InteractionType == OfficeFacilityInteractionType.HireNpc)
                .ToList();

            Assert.AreEqual(1, hireNpcDefinitions.Count);
            Assert.AreEqual("Construction Site Cabin", hireNpcDefinitions[0].DisplayName);
            Assert.AreEqual(OfficeObjectFunction.Npc, hireNpcDefinitions[0].Function);
            Assert.AreEqual(3f, hireNpcDefinitions[0].Capacity, 0.01f);
        }
    }
}
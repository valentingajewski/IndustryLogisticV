using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class FleetManagerCargoVisualModelSelectionTests
    {
        [TestMethod]
        public void CommodityCatalog_WoodDoesNotUseAttachedPropVisual()
        {
            Assert.IsFalse(CommodityCatalog.UsesAttachedPropVisual(VehicleCargoType.Wood));
            Assert.IsFalse(CommodityCatalog.UsesAttachedPropVisual("Wood"));
            Assert.IsFalse(CommodityCatalog.UsesCenteredPropVisual(VehicleCargoType.Wood));
            Assert.IsTrue(CommodityCatalog.UsesAttachedPropVisual(VehicleCargoType.OpenHull));
            Assert.IsTrue(CommodityCatalog.UsesAttachedPropVisual("Lumber"));
        }

        [TestMethod]
        public void ResolveCargoPropModels_LumberAndMetalRemainOnExistingProps()
        {
            var manager = CreateFleetManager(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Lumber", new List<string> { "prop_woodpile_01b" } },
            });

            var lumberModels = InvokeResolveCargoPropModels(manager, "Lumber", VehicleCargoType.OpenHull);
            var metalModels = InvokeResolveCargoPropModels(manager, "Metal", VehicleCargoType.OpenHull);

            CollectionAssert.AreEqual(new[] { "prop_woodpile_01b" }, lumberModels);
            CollectionAssert.AreEqual(new[] { "prop_pipes_04a" }, metalModels);
        }

        [TestMethod]
        public void LoadRepoConfig_WoodObjectGroupIsAbsentWhileLumberRemainsConfigured()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");

            var config = ModConfig.Load(configDirectory);

            Assert.IsFalse(config.ObjectModels.ContainsKey("Wood"));
            CollectionAssert.AreEqual(new[] { "prop_woodpile_01b" }, config.ObjectModels["Lumber"]);
        }

        private static FleetManager CreateFleetManager(Dictionary<string, List<string>> objectModels)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>());
            SetProperty(config, nameof(ModConfig.ObjectModels), objectModels ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            return new FleetManager(config);
        }

        private static List<string> InvokeResolveCargoPropModels(FleetManager manager, string commodity, VehicleCargoType cargoType)
        {
            var method = typeof(FleetManager).GetMethod("ResolveCargoPropModels", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ResolveCargoPropModels");

            var result = method.Invoke(manager, new object[] { commodity, cargoType, null }) as List<string>;
            Assert.IsNotNull(result, commodity);
            return result;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
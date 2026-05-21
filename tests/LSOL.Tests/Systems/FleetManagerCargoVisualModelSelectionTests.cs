using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var lumberModels = InvokeResolveCargoPropModels(manager, "Lumber", VehicleCargoType.OpenHull, null, null);
            var metalModels = InvokeResolveCargoPropModels(manager, "Metal", VehicleCargoType.OpenHull, null, null);

            CollectionAssert.AreEqual(new[] { "prop_woodpile_01b" }, lumberModels);
            CollectionAssert.AreEqual(new[] { "prop_pipes_04a" }, metalModels);
        }

        [TestMethod]
        public void BaseVehicleLayoutWithoutCommodityOverride_StillUsesConfiguredObjectKey()
        {
            var config = LoadRepoConfig();
            var manager = new FleetManager(config);
            var muleDefinition = config.VehicleDefinitions.Single(definition => string.Equals(definition.ModelName, "mule", StringComparison.OrdinalIgnoreCase));
            var muleLayout = config.VehicleObjectLayouts.Single(layout => string.Equals(layout.ModelName, "mule", StringComparison.OrdinalIgnoreCase));

            VehicleObjectPlacementMode placementMode;
            var resolvedLayout = InvokeResolveEffectiveVehicleObjectLayout(manager, muleDefinition, "ProcessedFood", VehicleCargoType.CraftedGoods, out placementMode);
            var models = InvokeResolveCargoPropModels(manager, "ProcessedFood", VehicleCargoType.CraftedGoods, muleDefinition, muleLayout);

            Assert.IsNotNull(resolvedLayout);
            Assert.AreEqual(VehicleObjectPlacementMode.Default, placementMode);
            CollectionAssert.AreEqual(config.ObjectModels["Box"], models);
        }

        [TestMethod]
        public void TrflatBricksOverride_ResolvesGridPlacement()
        {
            AssertTrflatCommodityPlacement("Bricks", VehicleObjectPlacementMode.Grid, "Bricks", 1, 6, 0f, 0f, 1.26f);
        }

        [TestMethod]
        public void TrflatLumberOverride_ResolvesGridPlacement()
        {
            AssertTrflatCommodityPlacement("Lumber", VehicleObjectPlacementMode.Grid, "Lumber", 1, 3, 0f, 0f, 0.90f);
        }

        [TestMethod]
        public void TrflatAlloyOverride_ResolvesSingleCenteredPlacement()
        {
            AssertTrflatCommodityPlacement("Alloy", VehicleObjectPlacementMode.CenteredSingle, "Alloy");
        }

        [TestMethod]
        public void TrflatMetalOverride_ResolvesSingleCenteredPlacement()
        {
            AssertTrflatCommodityPlacement("Metal", VehicleObjectPlacementMode.CenteredSingle, "Metal");
        }

        [TestMethod]
        public void TrflatBeamOverride_ResolvesSingleCenteredPlacement()
        {
            AssertTrflatCommodityPlacement("Beam", VehicleObjectPlacementMode.CenteredSingle, "Beam");
        }

        [TestMethod]
        public void DumperLooseCargoOverlay_OptsInAggregateRocksWithoutMatchingCrops()
        {
            var config = LoadRepoConfig();
            var manager = new FleetManager(config);

            foreach (var modelName in new[] { "rubble", "tiptruck", "tiptruck2" })
            {
                var definition = config.VehicleDefinitions.Single(vehicle => string.Equals(vehicle.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
                var oreOverlay = InvokeFindVehicleLooseCargoVisual(manager, definition, "Ore");
                var cropOverlay = InvokeFindVehicleLooseCargoVisual(manager, definition, "Crops");

                Assert.IsNotNull(oreOverlay, modelName + " ore overlay");
                Assert.AreEqual("LooseRock", oreOverlay.ObjectKey);
                Assert.AreEqual(3, oreOverlay.MaxPropCount);
                Assert.IsNull(cropOverlay, modelName + " crops overlay");
            }
        }

        [TestMethod]
        public void LoadRepoConfig_WoodObjectGroupIsAbsentWhileLumberRemainsConfigured()
        {
            var config = LoadRepoConfig();

            Assert.IsFalse(config.ObjectModels.ContainsKey("Wood"));
            CollectionAssert.AreEqual(new[] { "prop_woodpile_01b" }, config.ObjectModels["Lumber"]);
        }

        private static FleetManager CreateFleetManager(
            Dictionary<string, List<string>> objectModels,
            IEnumerable<VehicleDefinition> vehicleDefinitions = null,
            IEnumerable<VehicleObjectLayoutDefinition> vehicleObjectLayouts = null)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), vehicleDefinitions != null ? vehicleDefinitions.ToList() : new List<VehicleDefinition>());
            SetProperty(config, nameof(ModConfig.VehicleObjectLayouts), vehicleObjectLayouts != null ? vehicleObjectLayouts.ToList() : new List<VehicleObjectLayoutDefinition>());
            SetProperty(config, nameof(ModConfig.ObjectModels), objectModels ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            return new FleetManager(config);
        }

        private static ModConfig LoadRepoConfig()
        {
            return ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
        }

        private static void AssertTrflatCommodityPlacement(
            string commodity,
            VehicleObjectPlacementMode expectedPlacementMode,
            string expectedObjectKey,
            int? expectedMaxLine = null,
            int? expectedMaxRow = null,
            float? expectedCenterX = null,
            float? expectedCenterY = null,
            float? expectedCenterZ = null)
        {
            var config = LoadRepoConfig();
            var manager = new FleetManager(config);
            var definition = config.VehicleDefinitions.Single(vehicle => string.Equals(vehicle.ModelName, "trflat", StringComparison.OrdinalIgnoreCase));

            VehicleObjectPlacementMode placementMode;
            var resolvedLayout = InvokeResolveEffectiveVehicleObjectLayout(manager, definition, commodity, VehicleCargoType.OpenHull, out placementMode);

            Assert.IsNotNull(resolvedLayout, commodity);
            Assert.AreEqual(expectedPlacementMode, placementMode, commodity + " placement");
            Assert.AreEqual(expectedObjectKey, resolvedLayout.ObjectKey, commodity + " objectKey");

            if (expectedMaxLine.HasValue)
            {
                Assert.AreEqual(expectedMaxLine.Value, resolvedLayout.MaxLine, commodity + " maxLine");
            }

            if (expectedMaxRow.HasValue)
            {
                Assert.AreEqual(expectedMaxRow.Value, resolvedLayout.MaxRow, commodity + " maxRow");
            }

            if (expectedCenterX.HasValue)
            {
                Assert.AreEqual(expectedCenterX.Value, resolvedLayout.CenterOffset.X, 0.01f, commodity + " centerX");
            }

            if (expectedCenterY.HasValue)
            {
                Assert.AreEqual(expectedCenterY.Value, resolvedLayout.CenterOffset.Y, 0.01f, commodity + " centerY");
            }

            if (expectedCenterZ.HasValue)
            {
                Assert.AreEqual(expectedCenterZ.Value, resolvedLayout.CenterOffset.Z, 0.01f, commodity + " centerZ");
            }
        }

        private static List<string> InvokeResolveCargoPropModels(FleetManager manager, string commodity, VehicleCargoType cargoType, VehicleDefinition definition, VehicleObjectLayoutDefinition configuredLayout)
        {
            var method = typeof(FleetManager).GetMethod(
                "ResolveCargoPropModels",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(VehicleCargoType), typeof(VehicleDefinition), typeof(VehicleObjectLayoutDefinition) },
                null);
            Assert.IsNotNull(method, "ResolveCargoPropModels");

            var result = method.Invoke(manager, new object[] { commodity, cargoType, definition, configuredLayout }) as List<string>;
            Assert.IsNotNull(result, commodity);
            return result;
        }

        private static VehicleObjectLayoutDefinition InvokeResolveEffectiveVehicleObjectLayout(FleetManager manager, VehicleDefinition definition, string commodity, VehicleCargoType cargoType, out VehicleObjectPlacementMode placementMode)
        {
            var method = typeof(FleetManager).GetMethod(
                "ResolveEffectiveVehicleObjectLayout",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(VehicleDefinition), typeof(string), typeof(VehicleCargoType), typeof(VehicleObjectPlacementMode).MakeByRefType() },
                null);
            Assert.IsNotNull(method, "ResolveEffectiveVehicleObjectLayout");

            var arguments = new object[] { definition, commodity, cargoType, VehicleObjectPlacementMode.Default };
            var result = method.Invoke(manager, arguments) as VehicleObjectLayoutDefinition;
            placementMode = (VehicleObjectPlacementMode)arguments[3];
            return result;
        }

        private static VehicleLooseCargoVisualDefinition InvokeFindVehicleLooseCargoVisual(FleetManager manager, VehicleDefinition definition, string commodity)
        {
            var method = typeof(FleetManager).GetMethod(
                "FindVehicleLooseCargoVisual",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(VehicleDefinition), typeof(string) },
                null);
            Assert.IsNotNull(method, "FindVehicleLooseCargoVisual");
            return method.Invoke(manager, new object[] { definition, commodity }) as VehicleLooseCargoVisualDefinition;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
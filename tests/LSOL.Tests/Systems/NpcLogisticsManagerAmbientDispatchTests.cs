using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class NpcLogisticsManagerAmbientDispatchTests
    {
        [TestMethod]
        public void CanAmbientWorldDispatchBetween_CrossDistrictRequiresLicensedCorridor()
        {
            var context = CreateContext();
            context.Origin.AddOutput("Ore", 6f);

            Assert.IsFalse(InvokeCanAmbientWorldDispatchBetween(context.Manager, context.Origin, context.Destination, "Ore"));

            ApplyCorridorSnapshot(context.TerritoryManager, "Port", "GrandSenora", CorridorRightLevel.Corridor);

            Assert.IsTrue(InvokeCanAmbientWorldDispatchBetween(context.Manager, context.Origin, context.Destination, "Ore"));
        }

        [TestMethod]
        public void TryExecuteInternalTransfer_AmbientJob_DoesNotAdvanceTerritoryProgression()
        {
            var context = CreateContext();
            var addedStock = context.Origin.AddOutput("Ore", 6f);
            ApplyCorridorSnapshot(context.TerritoryManager, "Port", "GrandSenora", CorridorRightLevel.Corridor);

            var originSite = context.TerritoryManager.GetSiteState(context.Origin);
            var destinationSite = context.TerritoryManager.GetSiteState(context.Destination);
            var originDistrict = context.TerritoryManager.GetDistrictState("Port");
            var destinationDistrict = context.TerritoryManager.GetDistrictState("GrandSenora");
            var corridor = context.TerritoryManager.GetCorridorState("Port", "GrandSenora");

            Assert.IsNotNull(originSite);
            Assert.IsNotNull(destinationSite);
            Assert.IsNotNull(originDistrict);
            Assert.IsNotNull(destinationDistrict);
            Assert.IsNotNull(corridor);

            var baselineOriginReputation = originDistrict.ReputationScore;
            var baselineOriginLabel = originDistrict.ReputationLabel;
            var baselineDestinationReputation = destinationDistrict.ReputationScore;
            var baselineDestinationLabel = destinationDistrict.ReputationLabel;
            var baselineCorridorLevel = corridor.RightLevel;
            var baselineOriginStock = context.Origin.GetStock("Ore");
            var baselineDestinationStock = context.Destination.GetStock("Ore");

            Assert.AreEqual(6f, addedStock, 0.01f);

            var job = CreateWorldJob(context.Manager, NpcWorldJobType.ShortageRelief, "Ore", context.Origin.Id, context.Destination.Id, addedStock);

            string outcome;
            var succeeded = InvokeTryExecuteInternalTransfer(context.Manager, job, context.Origin, context.Destination, 600000, out outcome);

            Assert.IsTrue(succeeded, outcome);
            Assert.AreEqual(baselineOriginStock - addedStock, context.Origin.GetStock("Ore"), 0.01f);
            Assert.AreEqual(baselineDestinationStock + addedStock, context.Destination.GetStock("Ore"), 0.01f);

            Assert.AreEqual(0, originSite.LoadRuns);
            Assert.AreEqual(0f, originSite.TotalLoadedTons, 0.01f);
            Assert.AreEqual(0, originSite.NpcLoads);
            Assert.AreEqual(0, destinationSite.UnloadRuns);
            Assert.AreEqual(0, destinationSite.TotalDeliveries);
            Assert.AreEqual(0f, destinationSite.TotalDeliveredTons, 0.01f);
            Assert.AreEqual(0, destinationSite.NpcDeliveries);

            Assert.AreEqual(0, originDistrict.CurrentWeekActivityCount);
            Assert.AreEqual(0f, originDistrict.CurrentWeekActivityTons, 0.01f);
            Assert.AreEqual(0, destinationDistrict.CurrentWeekActivityCount);
            Assert.AreEqual(0f, destinationDistrict.CurrentWeekActivityTons, 0.01f);
            Assert.AreEqual(baselineOriginReputation, originDistrict.ReputationScore, 0.01f);
            Assert.AreEqual(baselineOriginLabel, originDistrict.ReputationLabel);
            Assert.AreEqual(baselineDestinationReputation, destinationDistrict.ReputationScore, 0.01f);
            Assert.AreEqual(baselineDestinationLabel, destinationDistrict.ReputationLabel);

            Assert.AreEqual(0, corridor.DeliveryCount);
            Assert.AreEqual(0f, corridor.TotalDeliveredTons, 0.01f);
            Assert.AreEqual(0, corridor.CurrentWeekDeliveryCount);
            Assert.AreEqual(0f, corridor.CurrentWeekDeliveredTons, 0.01f);
            Assert.AreEqual(baselineCorridorLevel, corridor.RightLevel);
        }

        private static AmbientDispatchTestContext CreateContext()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "alpha-plant", CreateOriginIndustryConfig() },
                { "bravo-plant", CreateDestinationIndustryConfig() },
            });
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>());
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);
            var fleetManager = new FleetManager(config);
            var market = new GlobalMarketManager(0);
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var manager = new NpcLogisticsManager(
                configDirectory,
                industryManager,
                fleetManager,
                market,
                position => position,
                () => 100000f,
                _ => { },
                _ => { },
                _ => { },
                territoryManager,
                () => Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                null,
                null,
                null,
                () => 0,
                null,
                null);

            return new AmbientDispatchTestContext
            {
                Manager = manager,
                TerritoryManager = territoryManager,
                Origin = industryManager.Industries[0],
                Destination = industryManager.Industries[1],
            };
        }

        private static IndustryConfig CreateOriginIndustryConfig()
        {
            return new IndustryConfig
            {
                Id = "alpha-plant",
                LegacyKey = "alpha-plant",
                Name = "Alpha Plant",
                DistrictName = "Port",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.RawProducer,
                OwnershipTier = SiteOwnershipTier.Local,
                Position = Vector3.Zero,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                ProductionRate = 1f,
                InputCapacityTons = 10f,
                OutputCapacityTons = 20f,
                IndustryOwnerCut = 0.5f,
            };
        }

        private static IndustryConfig CreateDestinationIndustryConfig()
        {
            return new IndustryConfig
            {
                Id = "bravo-plant",
                LegacyKey = "bravo-plant",
                Name = "Bravo Plant",
                DistrictName = "GrandSenora",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Position = Vector3.Zero,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                ProductionRate = 1f,
                InputCapacityTons = 20f,
                OutputCapacityTons = 10f,
                IndustryOwnerCut = 0.5f,
            };
        }

        private static void ApplyCorridorSnapshot(TerritoryManager territoryManager, string districtA, string districtB, CorridorRightLevel rightLevel)
        {
            var snapshot = new TerritoryPersistenceSnapshot();
            snapshot.Corridors.Add(new TerritoryCorridorSnapshot
            {
                DistrictA = districtA,
                DistrictB = districtB,
                RightLevel = rightLevel,
            });
            territoryManager.ApplySnapshot(snapshot);
        }

        private static bool InvokeCanAmbientWorldDispatchBetween(NpcLogisticsManager manager, Industry origin, Industry destination, string commodity)
        {
            var method = typeof(NpcLogisticsManager).GetMethod("CanAmbientWorldDispatchBetween", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "CanAmbientWorldDispatchBetween");
            return (bool)method.Invoke(manager, new object[] { origin, destination, commodity });
        }

        private static bool InvokeTryExecuteInternalTransfer(NpcLogisticsManager manager, object job, Industry origin, Industry destination, int now, out string outcome)
        {
            var method = typeof(NpcLogisticsManager).GetMethod("TryExecuteInternalTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TryExecuteInternalTransfer");
            var arguments = new object[] { job, origin, destination, now, string.Empty };
            var result = (bool)method.Invoke(manager, arguments);
            outcome = arguments[4] as string ?? string.Empty;
            return result;
        }

        private static object CreateWorldJob(NpcLogisticsManager manager, NpcWorldJobType type, string commodity, string originIndustryId, string destinationIndustryId, float tons)
        {
            var jobType = manager.GetType().Assembly.GetType("LSOL.Systems.NpcWorldLogisticsJob");
            Assert.IsNotNull(jobType, "NpcWorldLogisticsJob");
            var job = Activator.CreateInstance(jobType);
            Assert.IsNotNull(job, "job");
            SetProperty(job, "Id", 1);
            SetProperty(job, "Type", type);
            SetProperty(job, "Phase", NpcWorldJobPhase.Traveling);
            SetProperty(job, "Commodity", commodity);
            SetProperty(job, "OriginIndustryId", originIndustryId);
            SetProperty(job, "DestinationIndustryId", destinationIndustryId);
            SetProperty(job, "SourceLabel", originIndustryId);
            SetProperty(job, "DestinationLabel", destinationIndustryId);
            SetProperty(job, "Tons", tons);
            SetProperty(job, "RemainingInGameMinutes", 0);
            SetProperty(job, "TotalInGameMinutes", 0);
            SetProperty(job, "CreatedClockMinute", 0);
            SetProperty(job, "StatusText", string.Empty);
            return job;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }

        private sealed class AmbientDispatchTestContext
        {
            public NpcLogisticsManager Manager { get; set; }

            public TerritoryManager TerritoryManager { get; set; }

            public Industry Origin { get; set; }

            public Industry Destination { get; set; }
        }
    }
}
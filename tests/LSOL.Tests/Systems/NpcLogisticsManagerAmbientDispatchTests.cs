using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void TryResolveAmbientWorldVehicleForCommodity_SmallCargo_PrefersSmallRigidVehicle()
        {
            var context = CreateContext(CreateGeneralCargoVehicleDefinitions());

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            string failureReason;
            var resolved = InvokeTryResolveAmbientWorldVehicleForCommodity(context.Manager, "ProcessedFood", 2.5f, out selectedVehicle, out selectedTractor, out failureReason);

            Assert.IsTrue(resolved, failureReason);
            Assert.IsNotNull(selectedVehicle);
            Assert.AreEqual("van-3", selectedVehicle.Id);
            Assert.IsFalse(selectedVehicle.IsTrailer);
            Assert.IsNull(selectedTractor);
        }

        [TestMethod]
        public void TryResolveAmbientWorldVehicleForCommodity_MediumCargo_PrefersMediumRigidVehicle()
        {
            var context = CreateContext(CreateGeneralCargoVehicleDefinitions());

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            string failureReason;
            var resolved = InvokeTryResolveAmbientWorldVehicleForCommodity(context.Manager, "ProcessedFood", 8f, out selectedVehicle, out selectedTractor, out failureReason);

            Assert.IsTrue(resolved, failureReason);
            Assert.IsNotNull(selectedVehicle);
            Assert.AreEqual("truck-10", selectedVehicle.Id);
            Assert.IsFalse(selectedVehicle.IsTrailer);
            Assert.IsNull(selectedTractor);
        }

        [TestMethod]
        public void TryResolveAmbientWorldVehicleForCommodity_LargeCargoWithinRigidCapacity_UsesBigRigidBeforeTrailer()
        {
            var context = CreateContext(CreateGeneralCargoVehicleDefinitions());

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            string failureReason;
            var resolved = InvokeTryResolveAmbientWorldVehicleForCommodity(context.Manager, "ProcessedFood", 18f, out selectedVehicle, out selectedTractor, out failureReason);

            Assert.IsTrue(resolved, failureReason);
            Assert.IsNotNull(selectedVehicle);
            Assert.AreEqual("truck-20", selectedVehicle.Id);
            Assert.IsFalse(selectedVehicle.IsTrailer);
            Assert.IsNull(selectedTractor);
        }

        [TestMethod]
        public void TryResolveAmbientWorldVehicleForCommodity_OverflowCargo_UsesTrailerRig()
        {
            var context = CreateContext(CreateGeneralCargoVehicleDefinitions());

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            string failureReason;
            var resolved = InvokeTryResolveAmbientWorldVehicleForCommodity(context.Manager, "ProcessedFood", 22f, out selectedVehicle, out selectedTractor, out failureReason);

            Assert.IsTrue(resolved, failureReason);
            Assert.IsNotNull(selectedVehicle);
            Assert.AreEqual("trailer-24", selectedVehicle.Id);
            Assert.IsTrue(selectedVehicle.IsTrailer);
            Assert.IsNotNull(selectedTractor);
            Assert.IsTrue(selectedTractor.IsTractor);
        }

        [TestMethod]
        public void TryResolveAmbientWorldVehicleForCommodity_SpecializedTrailerOnlyCommodity_UsesTrailerSetup()
        {
            var context = CreateContext(CreateSpecializedTrailerVehicleDefinitions());

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            string failureReason;
            var resolved = InvokeTryResolveAmbientWorldVehicleForCommodity(context.Manager, "Oil", 10f, out selectedVehicle, out selectedTractor, out failureReason);

            Assert.IsTrue(resolved, failureReason);
            Assert.IsNotNull(selectedVehicle);
            Assert.AreEqual("tanker-trailer", selectedVehicle.Id);
            Assert.IsTrue(selectedVehicle.IsTrailer);
            Assert.IsNotNull(selectedTractor);
            Assert.IsTrue(selectedTractor.IsTractor);
        }

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

        [TestMethod]
        public void GetCorridorCompetitionSummaries_AggregatesOnlyRivalCrossDistrictJobs()
        {
            var context = CreateContext();
            var rivalCrossDistrict = CreateWorldJob(context.Manager, NpcWorldJobType.RivalFreight, "Ore", context.Origin.Id, context.Destination.Id, 6f);
            var nonRivalCrossDistrict = CreateWorldJob(context.Manager, NpcWorldJobType.ShortageRelief, "Ore", context.Origin.Id, context.Destination.Id, 4f);
            var rivalSameDistrict = CreateWorldJob(context.Manager, NpcWorldJobType.RivalFreight, "Ore", context.Origin.Id, context.Origin.Id, 3f);

            SetProperty(rivalCrossDistrict, "IsRivalJob", true);
            SetProperty(rivalCrossDistrict, "HasVisibleConvoy", true);
            SetProperty(nonRivalCrossDistrict, "HasVisibleConvoy", true);
            SetProperty(rivalSameDistrict, "IsRivalJob", true);
            SetProperty(rivalSameDistrict, "HasVisibleConvoy", true);

            AddWorldJob(context.Manager, rivalCrossDistrict);
            AddWorldJob(context.Manager, nonRivalCrossDistrict);
            AddWorldJob(context.Manager, rivalSameDistrict);

            var summaries = context.Manager.GetCorridorCompetitionSummaries();

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual("GrandSenora|Port", summaries[0].CorridorId);
            Assert.AreEqual("GrandSenora", summaries[0].DistrictA);
            Assert.AreEqual("Port", summaries[0].DistrictB);
            Assert.AreEqual(1, summaries[0].ActiveJobCount);
            Assert.AreEqual(1, summaries[0].VisibleConvoyCount);
            Assert.AreEqual(6f, summaries[0].CompetitiveTons, 0.01f);
        }

        [TestMethod]
        public void GetCorridorCompetitionSummaries_NormalizesReversedCrossDistrictRoutes()
        {
            var context = CreateContext();
            var outbound = CreateWorldJob(context.Manager, NpcWorldJobType.RivalFreight, "Ore", context.Origin.Id, context.Destination.Id, 7f);
            var inbound = CreateWorldJob(context.Manager, NpcWorldJobType.RivalFreight, "Ore", context.Destination.Id, context.Origin.Id, 5f);

            SetProperty(outbound, "IsRivalJob", true);
            SetProperty(outbound, "HasVisibleConvoy", true);
            SetProperty(inbound, "IsRivalJob", true);

            AddWorldJob(context.Manager, outbound);
            AddWorldJob(context.Manager, inbound);

            var summaries = context.Manager.GetCorridorCompetitionSummaries();

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual("GrandSenora|Port", summaries[0].CorridorId);
            Assert.AreEqual(2, summaries[0].ActiveJobCount);
            Assert.AreEqual(1, summaries[0].VisibleConvoyCount);
            Assert.AreEqual(12f, summaries[0].CompetitiveTons, 0.01f);
            Assert.IsTrue(summaries[0].PressureScore > 0.2f);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_WithCarrierOwnedRivalJobs_ExposesDominantCarrierMetadata()
        {
            var context = CreateContext();
            ApplyCorridorSnapshot(context.TerritoryManager, "Port", "GrandSenora", CorridorRightLevel.Corridor);

            var snapshot = new NpcLogisticsPersistenceSnapshot();
            snapshot.Carriers.Add(CreateCarrierSnapshot("port-freight", "Port Freight 1", "Port"));
            snapshot.Carriers.Add(CreateCarrierSnapshot("senora-freight", "Senora Freight 2", "GrandSenora"));
            snapshot.WorldJobs.Add(CreateWorldJobSnapshot(1, NpcWorldJobType.RivalFreight, NpcWorldJobPhase.Traveling, "Ore", context.Origin.Id, context.Destination.Id, 6f, true, true, "port-freight"));
            snapshot.WorldJobs.Add(CreateWorldJobSnapshot(2, NpcWorldJobType.RivalFreight, NpcWorldJobPhase.Listed, "Ore", context.Origin.Id, context.Destination.Id, 4f, true, false, "port-freight"));
            snapshot.WorldJobs.Add(CreateWorldJobSnapshot(3, NpcWorldJobType.RivalFreight, NpcWorldJobPhase.Listed, "Ore", context.Origin.Id, context.Destination.Id, 3f, true, false, "senora-freight"));

            context.Manager.ApplyPersistenceSnapshot(snapshot);

            var districtSummaries = context.Manager.GetDistrictCompetitionSummaries();
            var destinationSummary = districtSummaries.Single(summary => string.Equals(summary.DistrictName, "GrandSenora", StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual(2, destinationSummary.ActiveCarrierCount);
            Assert.AreEqual("port-freight", destinationSummary.DominantCarrierId);
            Assert.AreEqual("Port Freight 1", destinationSummary.DominantCarrierName);

            var restoredSnapshot = context.Manager.CreatePersistenceSnapshot();
            Assert.AreEqual(2, restoredSnapshot.Carriers.Count);
            Assert.AreEqual("port-freight", restoredSnapshot.WorldJobs[0].CarrierId);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_LegacyRivalJobsWithoutCarrierId_AssignsFallbackCarrier()
        {
            var context = CreateContext();
            ApplyCorridorSnapshot(context.TerritoryManager, "Port", "GrandSenora", CorridorRightLevel.Corridor);

            var snapshot = new NpcLogisticsPersistenceSnapshot();
            snapshot.WorldJobs.Add(CreateWorldJobSnapshot(1, NpcWorldJobType.RivalFreight, NpcWorldJobPhase.Traveling, "Ore", context.Origin.Id, context.Destination.Id, 5f, true, true, string.Empty));

            context.Manager.ApplyPersistenceSnapshot(snapshot);

            var restoredSnapshot = context.Manager.CreatePersistenceSnapshot();
            Assert.AreEqual(1, restoredSnapshot.Carriers.Count);
            Assert.AreEqual(1, restoredSnapshot.WorldJobs.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(restoredSnapshot.WorldJobs[0].CarrierId));
            Assert.AreEqual(restoredSnapshot.Carriers[0].Id, restoredSnapshot.WorldJobs[0].CarrierId);

            var corridorSummary = context.Manager.GetCorridorCompetitionSummaries().Single();
            Assert.AreEqual(1, corridorSummary.ActiveCarrierCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(corridorSummary.DominantCarrierName));
        }

        [TestMethod]
        public void Update_CarrierEcosystem_GrowsFromOpportunityAndTurnsDormantUnderSustainedDefense()
        {
            var context = CreateContext();
            ConfigureCarrierEcosystem(context.Manager, 1, 1, 0.45f, 0.65f, 1, 0.12f, 0.08f, 1);

            context.TerritoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        CompetitiveOpportunity = 0.42f,
                        CurrentWeekActivityTons = 0f,
                    },
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "GrandSenora",
                        CompetitiveOpportunity = 0.48f,
                        CurrentWeekActivityTons = 0f,
                    },
                },
                Corridors =
                {
                    new TerritoryCorridorSnapshot
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        RightLevel = CorridorRightLevel.ServicePermit,
                        CompetitiveOpportunity = 0.38f,
                    },
                },
            });

            context.Manager.Update(1000, 180);

            var carriersAfterGrowth = GetCarrierNetworks(context.Manager);
            Assert.AreEqual(1, carriersAfterGrowth.Count);
            var growthCarrier = carriersAfterGrowth[0];
            var strengthAfterGrowth = GetPropertyValue<float>(growthCarrier, "Strength");
            Assert.IsTrue(strengthAfterGrowth > 0.28f);
            Assert.IsFalse(GetPropertyValue<bool>(growthCarrier, "IsDormant"));

            var pressureSummary = context.Manager.GetDistrictCompetitionSummaries();
            Assert.IsTrue(pressureSummary.Count > 0);
            Assert.AreEqual(1, pressureSummary[0].ActiveCarrierCount);

            context.TerritoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        CurrentWeekActivityTons = 60f,
                        CompetitiveOpportunity = 0.01f,
                        CompetitiveWinCount = 2,
                    },
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "GrandSenora",
                        CurrentWeekActivityTons = 64f,
                        CompetitiveOpportunity = 0.01f,
                        CompetitiveWinCount = 3,
                    },
                },
                Corridors =
                {
                    new TerritoryCorridorSnapshot
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        RightLevel = CorridorRightLevel.Corridor,
                        CurrentWeekDeliveredTons = 72f,
                        CompetitiveOpportunity = 0.01f,
                        CompetitiveWinCount = 3,
                        ContestedWeekStreak = 2,
                    },
                },
            });

            context.Manager.Update(2000, (7 * 24 * 60) + 180);

            var carriersAfterDefense = GetCarrierNetworks(context.Manager);
            Assert.AreEqual(1, carriersAfterDefense.Count);
            Assert.IsTrue(GetPropertyValue<float>(carriersAfterDefense[0], "Strength") < strengthAfterGrowth);
            Assert.IsTrue(GetPropertyValue<bool>(carriersAfterDefense[0], "IsDormant"));
        }

        private static AmbientDispatchTestContext CreateContext(IReadOnlyList<VehicleDefinition> vehicleDefinitions = null)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "alpha-plant", CreateOriginIndustryConfig() },
                { "bravo-plant", CreateDestinationIndustryConfig() },
            });
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), vehicleDefinitions != null ? new List<VehicleDefinition>(vehicleDefinitions) : new List<VehicleDefinition>());
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

        private static IReadOnlyList<VehicleDefinition> CreateGeneralCargoVehicleDefinitions()
        {
            return new List<VehicleDefinition>
            {
                CreateVehicleDefinition("van-3", "Light Van", "speedo", VehicleCargoType.CraftedGoods, 3f, false, false, "ProcessedFood"),
                CreateVehicleDefinition("van-5", "Box Van", "boxville4", VehicleCargoType.CraftedGoods, 5f, false, false, "ProcessedFood"),
                CreateVehicleDefinition("truck-10", "Medium Truck", "mule", VehicleCargoType.CraftedGoods, 10f, false, false, "ProcessedFood"),
                CreateVehicleDefinition("truck-20", "Heavy Truck", "pounder", VehicleCargoType.CraftedGoods, 20f, false, false, "ProcessedFood"),
                CreateVehicleDefinition("trailer-24", "Cargo Trailer", "trailers2", VehicleCargoType.CraftedGoods, 24f, true, false, "ProcessedFood"),
                CreateVehicleDefinition("tractor-1", "Fleet Tractor A", "phantom", VehicleCargoType.Trailer, 0f, false, true),
                CreateVehicleDefinition("tractor-2", "Fleet Tractor B", "hauler", VehicleCargoType.Trailer, 0f, false, true),
            };
        }

        private static IReadOnlyList<VehicleDefinition> CreateSpecializedTrailerVehicleDefinitions()
        {
            return new List<VehicleDefinition>
            {
                CreateVehicleDefinition("craft-van", "Craft Van", "rumpo3", VehicleCargoType.CraftedGoods, 5f, false, false, "ProcessedFood"),
                CreateVehicleDefinition("tanker-trailer", "Tanker Trailer", "tanker", VehicleCargoType.Liquid, 24f, true, false, "Oil"),
                CreateVehicleDefinition("tractor-1", "Fleet Tractor A", "phantom", VehicleCargoType.Trailer, 0f, false, true),
                CreateVehicleDefinition("tractor-2", "Fleet Tractor B", "hauler", VehicleCargoType.Trailer, 0f, false, true),
            };
        }

        private static VehicleDefinition CreateVehicleDefinition(string id, string displayName, string modelName, VehicleCargoType cargoType, float capacityTons, bool isTrailer, bool isTractor, params string[] acceptedCommodities)
        {
            return new VehicleDefinition
            {
                Id = id,
                DisplayName = displayName,
                ModelName = modelName,
                CargoType = cargoType,
                AcceptedCommodities = acceptedCommodities != null && acceptedCommodities.Length > 0
                    ? new HashSet<string>(acceptedCommodities, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                CapacityTons = capacityTons,
                IsEnabled = true,
                IsTrailer = isTrailer,
                IsTractor = isTractor,
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

        private static bool InvokeTryResolveAmbientWorldVehicleForCommodity(NpcLogisticsManager manager, string commodity, float tons, out VehicleDefinition selectedVehicle, out VehicleDefinition selectedTractor, out string failureReason)
        {
            var method = typeof(NpcLogisticsManager).GetMethod("TryResolveAmbientWorldVehicleForCommodity", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TryResolveAmbientWorldVehicleForCommodity");
            var arguments = new object[] { commodity, tons, null, null, string.Empty };
            var result = (bool)method.Invoke(manager, arguments);
            selectedVehicle = arguments[2] as VehicleDefinition;
            selectedTractor = arguments[3] as VehicleDefinition;
            failureReason = arguments[4] as string ?? string.Empty;
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

        private static void AddWorldJob(NpcLogisticsManager manager, object job)
        {
            var field = typeof(NpcLogisticsManager).GetField("_worldJobs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_worldJobs");
            var jobs = field.GetValue(manager) as System.Collections.IList;
            Assert.IsNotNull(jobs, "jobs");
            jobs.Add(job);
        }

        private static NpcCarrierNetworkSnapshot CreateCarrierSnapshot(string id, string displayName, string homeDistrict)
        {
            var snapshot = new NpcCarrierNetworkSnapshot
            {
                Id = id,
                DisplayName = displayName,
                HomeDistrict = homeDistrict,
                Strength = 0.55f,
                LastActiveWeekIndex = 1,
                LastExpansionWeekIndex = 1,
                VisualSeed = 7,
            };
            snapshot.PreferredDistricts.Add(homeDistrict);
            return snapshot;
        }

        private static NpcWorldLogisticsJobSnapshot CreateWorldJobSnapshot(
            int id,
            NpcWorldJobType type,
            NpcWorldJobPhase phase,
            string commodity,
            string originIndustryId,
            string destinationIndustryId,
            float tons,
            bool isRivalJob,
            bool hasVisibleConvoy,
            string carrierId)
        {
            return new NpcWorldLogisticsJobSnapshot
            {
                Id = id,
                Type = type,
                Phase = phase,
                Commodity = commodity,
                SourceLabel = originIndustryId,
                DestinationLabel = destinationIndustryId,
                OriginIndustryId = originIndustryId,
                DestinationIndustryId = destinationIndustryId,
                Tons = tons,
                RemainingInGameMinutes = 30,
                TotalInGameMinutes = 45,
                CreatedClockMinute = 60,
                HasVisibleConvoy = hasVisibleConvoy,
                IsRivalJob = isRivalJob,
                CarrierId = carrierId,
            };
        }

        private static void ConfigureCarrierEcosystem(
            NpcLogisticsManager manager,
            int minCarrierCount,
            int maxCarrierCount,
            float carrierGrowthRate,
            float carrierDeclineRate,
            int dormancyWeeks,
            float expansionPressureThreshold,
            float collapsePressureThreshold,
            int maxCarrierOwnedJobsPerEvaluation)
        {
            var field = typeof(NpcLogisticsManager).GetField("_worldDispatchConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_worldDispatchConfig");
            var config = field.GetValue(manager);
            Assert.IsNotNull(config, "config");
            SetProperty(config, "MinCarrierCount", minCarrierCount);
            SetProperty(config, "MaxCarrierCount", maxCarrierCount);
            SetProperty(config, "CarrierGrowthRate", carrierGrowthRate);
            SetProperty(config, "CarrierDeclineRate", carrierDeclineRate);
            SetProperty(config, "DormancyWeeks", dormancyWeeks);
            SetProperty(config, "ExpansionPressureThreshold", expansionPressureThreshold);
            SetProperty(config, "CollapsePressureThreshold", collapsePressureThreshold);
            SetProperty(config, "MaxCarrierOwnedJobsPerEvaluation", maxCarrierOwnedJobsPerEvaluation);
        }

        private static IReadOnlyList<object> GetCarrierNetworks(NpcLogisticsManager manager)
        {
            var field = typeof(NpcLogisticsManager).GetField("_carrierNetworks", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_carrierNetworks");
            var carriers = field.GetValue(manager) as System.Collections.IEnumerable;
            Assert.IsNotNull(carriers, "carriers");
            return carriers.Cast<object>().ToArray();
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            return (T)property.GetValue(target, null);
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
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
    public sealed class SpecialMissionManagerRestoreTests
    {
        [TestMethod]
        public void ApplyPersistenceSnapshot_RestoresDynamicCargoMissionProgressAndRoundTripsActiveState()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            InjectIndustry(industryManager, CreateTestIndustry(
                "__restore_source__",
                "Restore Source",
                "Port",
                Array.Empty<string>(),
                new[] { "Steel" }));
            InjectIndustry(industryManager, CreateTestIndustry(
                "__restore_destination__",
                "Restore Destination",
                "GrandSenora",
                new[] { "Steel" },
                Array.Empty<string>()));

            var territoryManager = new TerritoryManager(config, industryManager);
            var fleetManager = new FleetManager(config);
            var globalMarket = new GlobalMarketManager(0);
            var statusMessages = new List<string>();
            var uiDirtyCount = 0;
            var manager = new SpecialMissionManager(
                configDirectory,
                industryManager,
                territoryManager,
                fleetManager,
                globalMarket,
                () => Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                _ => { },
                (message, durationMs) => statusMessages.Add(message),
                () => uiDirtyCount += 1,
                () => 0);

            var definition = new SpecialMissionDefinition
            {
                Id = "__dynamic_restore__",
                Type = SpecialMissionType.DynamicCargoDelivery,
                Category = "Test",
                Name = "Dynamic Restore",
                SourceIndustryId = "__restore_source__",
                DestinationIndustryId = "__restore_destination__",
                Commodity = "Steel",
                TargetTons = 10f,
                Repeatable = true,
            };
            InjectMissionDefinition(manager, definition);

            var snapshot = new SpecialMissionPersistenceSnapshot();
            snapshot.CompletedMissions.Add(new SpecialMissionCompletionSnapshot
            {
                MissionId = definition.Id,
                CompletionCount = 2,
                LastCompletedInGameMinute = 720,
            });
            snapshot.AvailableMissionAnnouncements.Add(new SpecialMissionAvailabilitySnapshot
            {
                MissionId = definition.Id,
            });
            snapshot.ActiveMission = new ActiveSpecialMissionPersistenceSnapshot
            {
                MissionId = definition.Id,
                StageIndex = 1,
                DynamicDeliveredTons = 3.5f,
            };

            manager.ApplyPersistenceSnapshot(snapshot);

            Assert.IsTrue(manager.HasActiveMission);
            Assert.AreEqual(definition.Id, manager.ActiveMissionId);
            Assert.AreEqual(definition.Name, manager.ActiveMissionName);
            Assert.AreEqual(2, manager.GetCompletionCount(definition.Id));
            Assert.AreEqual(720, manager.GetLastCompletedInGameMinute(definition.Id));
            StringAssert.Contains(manager.ActiveObjective, "Deliver");
            StringAssert.Contains(manager.ActiveObjective, definition.Commodity);
            StringAssert.Contains(manager.ActiveObjectiveDetail, "Restore Destination");
            Assert.IsTrue(uiDirtyCount >= 2, "Expected restore to mark the UI dirty during reset and activation.");
            Assert.AreEqual(0, statusMessages.Count);

            var roundTripped = manager.CreatePersistenceSnapshot();

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(1, roundTripped.CompletedMissions.Count);
            Assert.AreEqual(1, roundTripped.AvailableMissionAnnouncements.Count);
            Assert.IsNotNull(roundTripped.ActiveMission);
            Assert.AreEqual(definition.Id, roundTripped.ActiveMission.MissionId);
            Assert.AreEqual(1, roundTripped.ActiveMission.StageIndex);
            Assert.AreEqual(3.5f, roundTripped.ActiveMission.DynamicDeliveredTons, 0.01f);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_UnknownActiveMission_ShowsRestoreErrorAndLeavesNoActiveMission()
        {
            var manager = CreateManagerForRestoreTests(out var statusMessages);
            var snapshot = new SpecialMissionPersistenceSnapshot
            {
                ActiveMission = new ActiveSpecialMissionPersistenceSnapshot
                {
                    MissionId = "missing-mission",
                    StageIndex = 1,
                    DynamicDeliveredTons = 5f,
                },
            };

            manager.ApplyPersistenceSnapshot(snapshot);

            Assert.IsFalse(manager.HasActiveMission);
            Assert.AreEqual(1, statusMessages.Count);
            StringAssert.Contains(statusMessages[0], "Mission definition unavailable");
            Assert.IsNull(manager.CreatePersistenceSnapshot());
        }

        [TestMethod]
        public void GetMissionListings_WithLiveDistrictEvent_GeneratesCrisisContractFromMatchingRoute()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var source = CreateTestIndustry(
                "__crisis_source__",
                "Crisis Source",
                "Port",
                Array.Empty<string>(),
                new[] { "Steel" });
            source.AddOutput("Steel", 18f);
            var destination = CreateTestIndustry(
                "__crisis_destination__",
                "Crisis Destination",
                "Port",
                new[] { "Steel" },
                Array.Empty<string>());
            InjectIndustry(industryManager, source);
            InjectIndustry(industryManager, destination);

            var territoryManager = new TerritoryManager(config, industryManager);
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        LicenseStatus = DistrictLicenseStatus.Active,
                        ActiveEvent = new TerritoryDistrictEventSnapshot
                        {
                            EventId = "district_event_port_steel",
                            DistrictName = "Port",
                            CrisisType = DistrictCrisisType.ConstructionSurge,
                            PreferredCommodity = "Steel",
                            Severity = 0.61f,
                            MarketPressureBonus = 0.16f,
                            ResponseTargetTons = 14f,
                            DeliveredReliefTons = 3f,
                            ReliefDeliveryCount = 1,
                            StartedWeekIndex = 1,
                            EndsAtWeekIndex = 2,
                            TriggerSummary = "Construction load 46% | Permit sites 1 | At-risk yards 1",
                            ImpactSummary = "Build sites are pulling extra tonnage.",
                        },
                    },
                },
            });

            var manager = new SpecialMissionManager(
                configDirectory,
                industryManager,
                territoryManager,
                new FleetManager(config),
                new GlobalMarketManager(0),
                () => Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                _ => { },
                (message, durationMs) => { },
                () => { },
                () => 120);

            var crisisListing = manager.GetMissionListings()
                .FirstOrDefault(listing => listing != null
                    && listing.IsGenerated
                    && listing.ContractFamily == GeneratedContractFamily.CrisisRelief
                    && string.Equals(listing.CrisisDistrictName, "Port", StringComparison.OrdinalIgnoreCase));

            Assert.IsNotNull(crisisListing, "Expected a generated crisis-relief listing for the live Port district event.");
            Assert.AreEqual("Steel", crisisListing.Commodity);
            StringAssert.Contains(crisisListing.Description, "Relief");

            var definition = manager.GetDefinition(crisisListing.MissionId);

            Assert.IsNotNull(definition);
            Assert.AreEqual("district_event_port_steel", definition.CrisisEventId);
            Assert.AreEqual(DistrictCrisisType.ConstructionSurge, definition.CrisisType);
        }

        [TestMethod]
        public void ApplyHandlerContainerRestoreCheckpointPolicy_MidLiftSnapshot_RollsBackToLoadingCheckpoint()
        {
            var snapshot = new ActiveSpecialMissionPersistenceSnapshot
            {
                MissionId = "port_container_handler",
                StageIndex = 4,
                HandlerContainerPickedUp = true,
                HandlerContainerLoaded = false,
            };

            var rolledBack = InvokeApplyRestoreCheckpointPolicy(snapshot, out var stageIndex, out var containerPickedUp, out var containerLoaded);

            Assert.IsTrue(rolledBack);
            Assert.AreEqual(4, stageIndex);
            Assert.IsFalse(containerPickedUp);
            Assert.IsFalse(containerLoaded);
        }

        [TestMethod]
        public void ApplyHandlerContainerRestoreCheckpointPolicy_LoadedContainerSnapshot_PreservesLoadedState()
        {
            var snapshot = new ActiveSpecialMissionPersistenceSnapshot
            {
                MissionId = "port_container_handler",
                StageIndex = 5,
                HandlerContainerPickedUp = false,
                HandlerContainerLoaded = true,
            };

            var rolledBack = InvokeApplyRestoreCheckpointPolicy(snapshot, out var stageIndex, out var containerPickedUp, out var containerLoaded);

            Assert.IsFalse(rolledBack);
            Assert.AreEqual(5, stageIndex);
            Assert.IsFalse(containerPickedUp);
            Assert.IsTrue(containerLoaded);
        }

        [TestMethod]
        public void BuildHandlerContainerRestoreStatusMessage_WhenRollbackOccurs_ExplainsCheckpointFallback()
        {
            var message = InvokeBuildRestoreStatusMessage("Port Container Handling", true);

            StringAssert.Contains(message, "Port Container Handling");
            StringAssert.Contains(message, "rolled back");
            StringAssert.Contains(message, "loading checkpoint");
        }

        private static bool InvokeApplyRestoreCheckpointPolicy(
            ActiveSpecialMissionPersistenceSnapshot snapshot,
            out int stageIndex,
            out bool containerPickedUp,
            out bool containerLoaded)
        {
            var method = GetHandlerContainerRuntimeType().GetMethod(
                "ApplyRestoreCheckpointPolicy",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(ActiveSpecialMissionPersistenceSnapshot),
                    typeof(int).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                },
                null);
            Assert.IsNotNull(method, "ApplyRestoreCheckpointPolicy");

            var arguments = new object[] { snapshot, 0, false, false };
            var rolledBack = (bool)method.Invoke(null, arguments);
            stageIndex = (int)arguments[1];
            containerPickedUp = (bool)arguments[2];
            containerLoaded = (bool)arguments[3];
            return rolledBack;
        }

        private static string InvokeBuildRestoreStatusMessage(string missionName, bool rolledBackToCheckpoint)
        {
            var method = GetHandlerContainerRuntimeType().GetMethod(
                "BuildRestoreStatusMessage",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            Assert.IsNotNull(method, "BuildRestoreStatusMessage");
            return method.Invoke(null, new object[] { missionName, rolledBackToCheckpoint }) as string;
        }

        private static System.Type GetHandlerContainerRuntimeType()
        {
            var runtimeType = typeof(SpecialMissionManager).GetNestedType("HandlerContainerTransferRuntime", BindingFlags.NonPublic);
            Assert.IsNotNull(runtimeType, "HandlerContainerTransferRuntime");
            return runtimeType;
        }

        private static SpecialMissionManager CreateManagerForRestoreTests(out List<string> statusMessages)
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);
            var fleetManager = new FleetManager(config);
            var globalMarket = new GlobalMarketManager(0);
            var messages = new List<string>();
            statusMessages = messages;

            return new SpecialMissionManager(
                configDirectory,
                industryManager,
                territoryManager,
                fleetManager,
                globalMarket,
                () => Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                _ => { },
                (message, durationMs) => messages.Add(message),
                () => { },
                () => 0);
        }

        private static Industry CreateTestIndustry(
            string id,
            string name,
            string districtName,
            IEnumerable<string> inputs,
            IEnumerable<string> outputs)
        {
            return new Industry(
                new IndustryConfig
                {
                    Id = id,
                    LegacyKey = id,
                    Name = name,
                    DistrictName = districtName,
                    Position = Vector3.Zero,
                    LocationKind = ExternalLocationKind.Industry,
                    SiteRole = SiteRole.ProcessingPlant,
                    OwnershipTier = SiteOwnershipTier.Local,
                    Inputs = new HashSet<string>(inputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                    OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Outputs = new HashSet<string>(outputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                    RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    InputCapacityTons = 50f,
                    OutputCapacityTons = 50f,
                    ProductionRate = 4f,
                    DeliveryPayoutMultiplier = 1f,
                },
                new List<ProductionRecipe>(),
                false,
                0.2f);
        }

        private static void InjectIndustry(IndustryManager manager, Industry industry)
        {
            var field = typeof(IndustryManager).GetField("_industries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_industries");

            var industries = field.GetValue(manager) as List<Industry>;
            Assert.IsNotNull(industries, "Industry list");
            industries.Add(industry);
        }

        private static void InjectMissionDefinition(SpecialMissionManager manager, SpecialMissionDefinition definition)
        {
            var field = typeof(SpecialMissionManager).GetField("_definitionsById", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_definitionsById");

            var definitions = field.GetValue(manager) as Dictionary<string, SpecialMissionDefinition>;
            Assert.IsNotNull(definitions, "Definitions dictionary");
            definitions[definition.Id] = definition;
        }
    }
}
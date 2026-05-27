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
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletRoutePlannerTests
    {
        [TestMethod]
        public void GetRoutePlannerCandidates_BuildsAvailableAndBlockedLanes_AndMatchesProjectedToActiveRoute()
        {
            const int currentMinute = 600;
            var configPath = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configPath);
            AppendPlannerIndustries(config);

            var industryManager = new IndustryManager(config);
            industryManager.SetLicensingDifficultyEnabled(true);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0, config.CommodityBasePrices);
            var financeTracker = new CompanyFinanceTracker();
            var npcManager = new NpcLogisticsManager(
                configPath,
                industryManager,
                fleetManager,
                globalMarket,
                position => position,
                () => 0f,
                _ => { },
                _ => { },
                null,
                null,
                null,
                null,
                null,
                financeTracker,
                () => currentMinute,
                null,
                null);

            var origin = industryManager.Industries.Single(industry => string.Equals(industry.Id, "planner_origin_open", StringComparison.OrdinalIgnoreCase));
            var destinationOpen = industryManager.Industries.Single(industry => string.Equals(industry.Id, "planner_destination_open", StringComparison.OrdinalIgnoreCase));
            origin.AddOutput("Fuel", 18f);

            InjectContracts(
                npcManager,
                CreateContract(1, origin, destinationOpen, "Fuel", 9600f, 1200f, 2200f, 4, 18f, 0.04f));

            var store = new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                npcManager,
                null,
                null,
                null,
                financeTracker,
                null,
                () => currentMinute,
                null,
                () => origin,
                () => 0f,
                () => VehicleCargoType.Liquid,
                _ => Vector3.Zero,
                () => false,
                () => string.Empty,
                () => 0,
                () => 0,
                () => 0,
                null,
                null,
                null);

            var candidates = store.GetRoutePlannerCandidates();
            var available = candidates.Single(candidate => string.Equals(candidate.CandidateId, "planner_origin_open|planner_destination_open|Fuel", StringComparison.OrdinalIgnoreCase));
            var blocked = candidates.Single(candidate => string.Equals(candidate.CandidateId, "planner_origin_open|planner_destination_blocked|Fuel", StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual(RoutePlannerAvailabilityState.Available, available.AvailabilityState, available.BlockerSummary);
            Assert.IsTrue(available.CanDraftNpcRoute);
            Assert.IsTrue(available.HasActiveNpcRoute);
            Assert.AreEqual(1, available.MatchingContractId);
            Assert.IsTrue(available.ProjectedValue > 0f);
            Assert.IsTrue(available.ProjectedPayout > 0f);
            Assert.IsTrue(available.RealizedAveragePayout > 0f);
            Assert.IsTrue(available.OptimizerScore > blocked.OptimizerScore);
            Assert.IsFalse(string.IsNullOrWhiteSpace(available.RouteFamily.Label));

            Assert.AreEqual(RoutePlannerAvailabilityState.Blocked, blocked.AvailabilityState);
            Assert.IsFalse(blocked.CanDraftNpcRoute);
            StringAssert.Contains(blocked.BlockerSummary, "permit");
            Assert.IsTrue(blocked.ScoreBreakdown.BlockerPenalty > 0f);
        }

        private static void AppendPlannerIndustries(ModConfig config)
        {
            config.IndustryConfigs["planner_origin_open"] = CreateIndustryConfig(
                "planner_origin_open",
                "Planner Origin Open",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                0f);
            config.IndustryConfigs["planner_destination_open"] = CreateIndustryConfig(
                "planner_destination_open",
                "Planner Destination Open",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                0f);
            config.IndustryConfigs["planner_destination_blocked"] = CreateIndustryConfig(
                "planner_destination_blocked",
                "Planner Destination Blocked",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                2500f);
        }

        private static IndustryConfig CreateIndustryConfig(string id, string name, HashSet<string> inputs, HashSet<string> outputs, float permitPrice)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = name,
                DistrictName = "Port",
                Position = Vector3.Zero,
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                Inputs = inputs,
                Outputs = outputs,
                ProductionRate = 1f,
                InputCapacityTons = 20f,
                OutputCapacityTons = 30f,
                IndustryLicencePrice = permitPrice,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = true,
                StandardEconomy = SiteEconomyPresetValues.Create(
                    1f,
                    permitPrice,
                    0f,
                    20f,
                    30f,
                    1f,
                    permitPrice > 0f),
            };
        }

        private static void InjectContracts(NpcLogisticsManager manager, params NpcLogisticsContract[] contracts)
        {
            var field = typeof(NpcLogisticsManager).GetField("_contracts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_contracts");

            var list = field.GetValue(manager) as List<NpcLogisticsContract>;
            Assert.IsNotNull(list, "_contracts value");

            list.Clear();
            if (contracts != null)
            {
                list.AddRange(contracts.Where(contract => contract != null));
            }
        }

        private static NpcLogisticsContract CreateContract(
            int id,
            Industry origin,
            Industry destination,
            string commodity,
            float totalProfitEarned,
            float contractCost,
            float totalWeeklyWagesPaid,
            int completedDeliveries,
            float totalDeliveredTons,
            float lastJourneyLossRatio)
        {
            var route = new NpcLogisticsRouteDefinition
            {
                OriginIndustry = origin,
                DestinationIndustry = destination,
                Commodity = commodity,
                AssignedVehicleDisplayName = "Planner Truck",
                OriginTriggerThresholdPercent = 20,
                DestinationTriggerThresholdPercent = 85,
            };

            var contract = new NpcLogisticsContract(id)
            {
                OriginIndustry = origin,
                DestinationIndustry = destination,
                Commodity = commodity,
                TotalProfitEarned = totalProfitEarned,
                ContractCost = contractCost,
                TotalWeeklyWagesPaid = totalWeeklyWagesPaid,
                CompletedDeliveries = completedDeliveries,
                TotalDeliveredTons = totalDeliveredTons,
                LastJourneyLossRatio = lastJourneyLossRatio,
                Tier = new NpcDriverTierDefinition("core", "Core", "s_m_m_trucker_01", 0.05f, 1f, 1f, 500f, 750f, 1000f),
                StatusText = "Running route",
            };
            contract.Routes.Add(route);
            return contract;
        }
    }
}
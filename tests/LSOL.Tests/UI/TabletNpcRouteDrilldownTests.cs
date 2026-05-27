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
    public sealed class TabletNpcRouteDrilldownTests
    {
        [TestMethod]
        public void GetNpcRouteDrilldown_SurfacesContractFamilyRollup_AndRecentRouteFinanceEntries()
        {
            const int currentMinute = 600;
            var configPath = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configPath);
            var industryManager = new IndustryManager(config);
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

            var contractA = CreateContract(
                1,
                0,
                9000f,
                1000f,
                2000f,
                4,
                18f,
                0.08f,
                CreateRoute("Fuel Alpha", "Fuel Beta", "Fuel"),
                CreateRoute("Steel Alpha", "Steel Beta", "Steel"));
            var contractB = CreateContract(
                2,
                1,
                7000f,
                800f,
                1600f,
                3,
                14f,
                0.05f,
                CreateRoute("Fuel Gamma", "Fuel Delta", "Fuel"),
                CreateRoute("Steel Gamma", "Steel Delta", "Steel"));

            InjectContracts(npcManager, contractA, contractB);

            financeTracker.RecordIncome(
                CompanyFinanceCategory.NpcDelivery,
                2600f,
                560,
                "NPC delivery to Steel Beta",
                1,
                "Fuel -> Steel");
            financeTracker.RecordExpense(
                CompanyFinanceCategory.NpcWages,
                1200f,
                505,
                "NPC payroll for Fuel Alpha -> Fuel Beta (Fuel)",
                1,
                "Fuel -> Steel");

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
                () => industryManager.Industries.First(),
                () => 0f,
                () => VehicleCargoType.Aggregates,
                _ => Vector3.Zero,
                () => false,
                () => string.Empty,
                () => 0,
                () => 0,
                () => 0,
                null,
                null,
                null);

            var budgetEntry = store.GetBudgetRouteProfitability().Single(entry => entry.ContractId == 1);
            var analyticsEntry = store.GetNpcRoutePerformance().Single(entry => entry.ContractId == 1);
            var detail = store.GetNpcRouteDrilldown(1);

            Assert.AreEqual("Mixed 2-leg chain", budgetEntry.FamilyLabel);
            Assert.AreEqual(2, budgetEntry.FamilyContractCount);
            Assert.AreEqual(2, analyticsEntry.RouteCount);
            Assert.AreEqual(2, analyticsEntry.FamilyContractCount);

            Assert.IsNotNull(detail);
            Assert.AreEqual("Mixed 2-leg chain", detail.RouteFamily.Label);
            Assert.AreEqual(2, detail.RouteFamily.ContractCount);
            Assert.AreEqual(2, detail.RouteLegs.Count);
            Assert.AreEqual(2, detail.RecentFinanceEntries.Count);
            Assert.AreEqual(40, detail.RecentFinanceEntries[0].AgeMinutes);
            Assert.AreEqual("NPC delivery to Steel Beta", detail.RecentFinanceEntries[0].Description);
            Assert.AreEqual(95, detail.RecentFinanceEntries[1].AgeMinutes);
            Assert.AreEqual("Fuel Alpha -> Fuel Beta | 1/2", detail.Label);
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
            int currentRouteIndex,
            float totalProfitEarned,
            float contractCost,
            float totalWeeklyWagesPaid,
            int completedDeliveries,
            float totalDeliveredTons,
            float lastJourneyLossRatio,
            params NpcLogisticsRouteDefinition[] routes)
        {
            var contract = new NpcLogisticsContract(id)
            {
                CurrentRouteIndex = currentRouteIndex,
                TotalProfitEarned = totalProfitEarned,
                ContractCost = contractCost,
                TotalWeeklyWagesPaid = totalWeeklyWagesPaid,
                CompletedDeliveries = completedDeliveries,
                TotalDeliveredTons = totalDeliveredTons,
                LastJourneyLossRatio = lastJourneyLossRatio,
                Tier = new NpcDriverTierDefinition("core", "Core", "s_m_m_trucker_01", 0.05f, 1f, 1f, 500f, 750f, 1000f),
                StatusText = "Running route",
            };

            if (routes != null)
            {
                contract.Routes.AddRange(routes);
                if (routes.Length > 0)
                {
                    var clampedIndex = currentRouteIndex < 0 ? 0 : (currentRouteIndex >= routes.Length ? routes.Length - 1 : currentRouteIndex);
                    contract.OriginIndustry = routes[clampedIndex].OriginIndustry;
                    contract.DestinationIndustry = routes[clampedIndex].DestinationIndustry;
                    contract.Commodity = routes[clampedIndex].Commodity;
                    contract.AssignedVehicleDisplayName = routes[clampedIndex].AssignedVehicleDisplayName;
                    contract.OriginTriggerThresholdPercent = routes[clampedIndex].OriginTriggerThresholdPercent;
                    contract.DestinationTriggerThresholdPercent = routes[clampedIndex].DestinationTriggerThresholdPercent;
                }
            }

            return contract;
        }

        private static NpcLogisticsRouteDefinition CreateRoute(string originName, string destinationName, string commodity)
        {
            return new NpcLogisticsRouteDefinition
            {
                OriginIndustry = CreateIndustry(originName),
                DestinationIndustry = CreateIndustry(destinationName),
                Commodity = commodity,
                AssignedVehicleDisplayName = string.Format("Truck for {0}", commodity),
                OriginTriggerThresholdPercent = 20,
                DestinationTriggerThresholdPercent = 85,
            };
        }

        private static Industry CreateIndustry(string name)
        {
            return new Industry(
                new IndustryConfig
                {
                    Id = name.Replace(" ", "_").ToLowerInvariant(),
                    LegacyKey = name.Replace(" ", "_").ToLowerInvariant(),
                    Name = name,
                    DistrictName = "Port",
                    Position = Vector3.Zero,
                    LocationKind = ExternalLocationKind.Industry,
                    SiteRole = SiteRole.ProcessingPlant,
                    InputCapacityTons = 10f,
                    OutputCapacityTons = 10f,
                    Inputs = new HashSet<string>(),
                    Outputs = new HashSet<string>(),
                },
                new List<ProductionRecipe>(),
                false,
                0.2f);
        }
    }
}
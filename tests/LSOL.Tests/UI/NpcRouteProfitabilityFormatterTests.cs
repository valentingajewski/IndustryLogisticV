using System.Collections.Generic;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class NpcRouteProfitabilityFormatterTests
    {
        [TestMethod]
        public void BuildFamilySummary_WithMixedChain_AggregatesContractsIntoFamilyRollup()
        {
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

            var summary = NpcRouteProfitabilityFormatter.BuildFamilySummary(new[] { contractA, contractB });

            Assert.AreEqual("Mixed 2-leg chain", summary.Label);
            Assert.AreEqual("Fuel -> Steel", summary.CommodityPattern);
            Assert.AreEqual("Liquid + OpenHull", summary.CargoTypePattern);
            Assert.AreEqual(2, summary.ContractCount);
            Assert.AreEqual(2, summary.RouteCount);
            Assert.AreEqual(16000f, summary.Revenue, 0.01f);
            Assert.AreEqual(5400f, summary.OperatingCost, 0.01f);
            Assert.AreEqual(10600f, summary.NetProfit, 0.01f);
            Assert.AreEqual(7, summary.CompletedDeliveries);
            StringAssert.Contains(NpcRouteProfitabilityFormatter.BuildFamilyDetail(summary), "2 contracts");
            StringAssert.Contains(NpcRouteProfitabilityFormatter.BuildFamilyDetail(summary), "Fuel -> Steel");
            StringAssert.Contains(NpcRouteProfitabilityFormatter.BuildFamilyDetail(summary), "+$10,600.00");
        }

        [TestMethod]
        public void BuildContractLabel_WithMultiRouteContract_ShowsActiveLegIndex()
        {
            var contract = CreateContract(
                3,
                1,
                0f,
                0f,
                0f,
                0,
                0f,
                0f,
                CreateRoute("Alpha", "Bravo", "Fuel"),
                CreateRoute("Bravo", "Charlie", "Steel"),
                CreateRoute("Charlie", "Delta", "Vehicles"));

            Assert.AreEqual("Bravo -> Charlie | 2/3", NpcRouteProfitabilityFormatter.BuildContractLabel(contract));
            Assert.AreEqual("3|Fuel>Steel>Vehicles", NpcRouteProfitabilityFormatter.BuildFamilyKey(contract));
        }

        [TestMethod]
        public void BuildLegDetail_WithActiveRoute_IncludesThresholdsTruckAndActiveMarker()
        {
            var summary = new TabletNpcRouteLegSummary
            {
                RouteIndex = 2,
                RouteCount = 3,
                IsCurrentRoute = true,
                OriginName = "Fuel Port",
                DestinationName = "Airport Depot",
                Commodity = "Fuel",
                AssignedVehicleDisplayName = "Hauler 7",
                OriginTriggerThresholdPercent = 25,
                DestinationTriggerThresholdPercent = 90,
            };

            Assert.AreEqual("Route 2/3 [ACTIVE]", NpcRouteProfitabilityFormatter.BuildLegCaption(summary));
            Assert.AreEqual(
                "Fuel Port -> Airport Depot | Fuel | Trigger 25% -> 90% | Truck Hauler 7 | Active leg",
                NpcRouteProfitabilityFormatter.BuildLegDetail(summary));
        }

        [TestMethod]
        public void BuildFinanceEntryDetail_FormatsTypeSignedMoneyAndRelativeAge()
        {
            var entry = new TabletNpcRouteFinanceEntry
            {
                Flow = CompanyFinanceFlow.Expense,
                Category = CompanyFinanceCategory.NpcWages,
                Amount = 1800f,
                AgeMinutes = 185,
                Description = "NPC payroll for Fuel Alpha -> Fuel Beta (Fuel)",
            };

            Assert.AreEqual("Payroll -$1,800.00", NpcRouteProfitabilityFormatter.BuildFinanceCaption(entry));
            Assert.AreEqual(
                "3h 5m ago | NPC payroll for Fuel Alpha -> Fuel Beta (Fuel)",
                NpcRouteProfitabilityFormatter.BuildFinanceDetail(entry));
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
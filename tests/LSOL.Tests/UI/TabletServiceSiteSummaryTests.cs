using System;
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
    public sealed class TabletServiceSiteSummaryTests
    {
        [TestMethod]
        public void BuildSnapshot_SurfacesPassiveIncomePreview_ForUnownedStoreAndGasStation()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0, config.CommodityBasePrices);
            var territoryManager = new TerritoryManager(config, industryManager);

            var store = industryManager.Industries.Single(industry => industry.Id == "Store1");
            var gasStation = industryManager.Industries.Single(industry => industry.Id == "Petrol Station 02");

            var storeState = territoryManager.GetSiteState(store);
            storeState.CurrentWeekServiceTons = 0f;
            storeState.RequiredWeeklyServiceTons = 30f;
            storeState.ServiceContractStatus = "Open market";

            var gasStationState = territoryManager.GetSiteState(gasStation);
            gasStationState.CurrentWeekServiceTons = 0f;
            gasStationState.RequiredWeeklyServiceTons = 45f;
            gasStationState.ServiceContractStatus = "Open market";

            var storeStateStore = CreateStore(industryManager, fleetManager, fuelSystem, globalMarket, territoryManager, store);
            var snapshot = BuildSnapshot(storeStateStore);

            var storeSummary = snapshot.StoreSummaries.Single(summary => summary != null && summary.Industry != null && summary.Industry.Id == "Store1");
            var gasSummary = snapshot.GasStationSummaries.Single(summary => summary != null && summary.Industry != null && summary.Industry.Id == "Petrol Station 02");

            Assert.IsTrue(storeSummary.HasServiceBusinessInfo);
            Assert.AreEqual(store.WeeklyPassiveIncome, storeSummary.ServiceWeeklyIncome, 0.01f);
            Assert.AreEqual(ServiceSiteEconomyPolicy.ComputeWeeklyStaffingCost(store), storeSummary.ServiceWeeklyStaffingCost, 0.01f);
            Assert.IsFalse(storeSummary.ServiceOperational);
            Assert.AreEqual("Potential passive income locked until purchase", storeSummary.ServicePassiveIncomeStatus);
            Assert.AreEqual("Open market", storeSummary.ServiceContractStatus);

            Assert.IsTrue(gasSummary.HasServiceBusinessInfo);
            Assert.AreEqual(gasStation.WeeklyPassiveIncome, gasSummary.ServiceWeeklyIncome, 0.01f);
            Assert.AreEqual(ServiceSiteEconomyPolicy.ComputeWeeklyStaffingCost(gasStation), gasSummary.ServiceWeeklyStaffingCost, 0.01f);
            Assert.IsFalse(gasSummary.ServiceOperational);
            Assert.AreEqual("Potential passive income locked until purchase", gasSummary.ServicePassiveIncomeStatus);
            Assert.AreEqual("Open market", gasSummary.ServiceContractStatus);
        }

        [TestMethod]
        public void BuildSnapshot_SurfacesLivePassiveIncomeReadiness_ForOwnedStore()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0, config.CommodityBasePrices);
            var territoryManager = new TerritoryManager(config, industryManager);

            var store = industryManager.Industries.Single(industry => industry.Id == "Store1");
            store.SetOwned(true);

            var storeState = territoryManager.GetSiteState(store);
            storeState.SiteOperatorAssigned = true;
            storeState.PassiveIncomeStockReady = true;
            storeState.PassiveIncomeOperational = true;
            storeState.PassiveIncomeStatus = "Passive income active";
            storeState.LastPassiveIncomeAmount = 3600f;
            storeState.LastPassiveIncomeWeekIndex = 4;
            storeState.LastPassiveIncomeStatus = "Paid last cycle";

            var storeStateStore = CreateStore(industryManager, fleetManager, fuelSystem, globalMarket, territoryManager, store);
            var snapshot = BuildSnapshot(storeStateStore);
            var storeSummary = snapshot.StoreSummaries.Single(summary => summary != null && summary.Industry != null && summary.Industry.Id == "Store1");

            Assert.IsTrue(storeSummary.HasServiceBusinessInfo);
            Assert.IsTrue(storeSummary.ServiceStaffAssigned);
            Assert.IsTrue(storeSummary.ServiceStockReady);
            Assert.IsTrue(storeSummary.ServiceOperational);
            Assert.AreEqual("Passive income active", storeSummary.ServicePassiveIncomeStatus);
            Assert.AreEqual(3600f, storeSummary.ServiceLastPassiveIncome, 0.01f);
            Assert.AreEqual("Paid last cycle", storeSummary.ServiceRecentPayoutStatus);
        }

        [TestMethod]
        public void BuildSnapshot_SurfacesWeeklyServiceTargetFields_ForUnownedConstructionSiteWithoutPassiveIncomeRows()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0, config.CommodityBasePrices);
            var territoryManager = new TerritoryManager(config, industryManager);

            var constructionSite = industryManager.Industries.Single(industry => industry.Id == "MPConstruction");

            var constructionState = territoryManager.GetSiteState(constructionSite);
            constructionState.CurrentWeekServiceDeliveries = 1;
            constructionState.CurrentWeekServiceTons = 8f;
            constructionState.RequiredWeeklyServiceTons = 18f;
            constructionState.ServicePenaltySteps = 0;
            constructionState.ServiceSuccessStreak = 2;
            constructionState.ServiceTargetMetLastWeek = true;
            constructionState.ServiceContractStatus = "Contract secured";

            var storeStateStore = CreateStore(industryManager, fleetManager, fuelSystem, globalMarket, territoryManager, constructionSite);
            var snapshot = BuildSnapshot(storeStateStore);

            var constructionSummary = snapshot.ConstructionSiteSummaries.Single(summary => summary != null && summary.Industry != null && summary.Industry.Id == "MPConstruction");

            Assert.IsFalse(constructionSummary.HasServiceBusinessInfo);
            Assert.IsTrue(constructionSummary.HasServiceContractInfo);
            Assert.AreEqual(1, constructionSummary.ServiceCurrentWeekDeliveries);
            Assert.AreEqual(8f, constructionSummary.ServiceCurrentWeekTons, 0.01f);
            Assert.AreEqual(18f, constructionSummary.ServiceRequiredWeeklyTons, 0.01f);
            Assert.AreEqual(2, constructionSummary.ServiceSuccessStreak);
            Assert.IsTrue(constructionSummary.ServiceTargetMetLastWeek);
            Assert.AreEqual("Contract secured", constructionSummary.ServiceContractStatus);
        }

        private static TabletStateSnapshot BuildSnapshot(TabletStateStore store)
        {
            var method = typeof(TabletStateStore).GetMethod("BuildSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected private BuildSnapshot method.");
            return (TabletStateSnapshot)method.Invoke(store, null);
        }

        private static TabletStateStore CreateStore(
            IndustryManager industryManager,
            FleetManager fleetManager,
            VehicleFuelSystem fuelSystem,
            GlobalMarketManager globalMarket,
            TerritoryManager territoryManager,
            Industry nearestIndustry)
        {
            return new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                null,
                null,
                null,
                null,
                null,
                territoryManager,
                () => 0,
                null,
                () => nearestIndustry,
                () => 0f,
                () => VehicleCargoType.Aggregates,
                _ => Vector3.Zero,
                () => false,
                () => string.Empty,
                () => 0,
                () => 0,
                () => 0,
                () => territoryManager.DistrictStates,
                () => territoryManager.GetOperationsSummary(),
                currentMinute => territoryManager.GetRemainingOperationsChargeMinutes(currentMinute));
        }
    }
}
using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class TerritoryManagerPassiveIncomeTests
    {
        [TestMethod]
        public void ProcessWeeklyMaintenance_AwardsPassiveIncomeOnlyForStockedStaffedOwnedStoresAndGasStations()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);

            var stockedStore = industryManager.Industries.Single(industry => industry.Id == "Store1");
            var stockedGasStation = industryManager.Industries.Single(industry => industry.Id == "Petrol Station 02");
            var emptyStore = industryManager.Industries.Single(industry => industry.Id == "Store2");
            var constructionSite = industryManager.Industries.Single(industry => industry.Id == "MPConstruction");

            stockedStore.SetOwned(true);
            stockedGasStation.SetOwned(true);
            emptyStore.SetOwned(true);
            constructionSite.SetOwned(true);

            ClearInputStock(stockedStore);
            ClearInputStock(stockedGasStation);
            ClearInputStock(emptyStore);
            ClearInputStock(constructionSite);

            stockedStore.AddInput(stockedStore.SortedAcceptedInputs.First(), 5f);
            stockedGasStation.AddInput(stockedGasStation.SortedAcceptedInputs.First(), 5f);
            constructionSite.AddInput(constructionSite.SortedAcceptedInputs.First(), 5f);

            string toggleResult;
            Assert.IsTrue(territoryManager.TrySetServiceSiteOperatorAssigned(stockedStore, true, out toggleResult), toggleResult);
            Assert.IsTrue(territoryManager.TrySetServiceSiteOperatorAssigned(stockedGasStation, true, out toggleResult), toggleResult);

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var maintenanceResult = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);

            Assert.IsNotNull(maintenanceResult);
            Assert.AreEqual(
                stockedStore.WeeklyPassiveIncome + stockedGasStation.WeeklyPassiveIncome,
                maintenanceResult.ServiceSinkPassiveIncome,
                0.01f);
            Assert.AreEqual(
                territoryManager.GetServiceSiteWeeklyStaffingCost(stockedStore) + territoryManager.GetServiceSiteWeeklyStaffingCost(stockedGasStation),
                maintenanceResult.ServiceSinkStaffingExpense,
                0.01f);
            Assert.IsTrue(territoryManager.GetSiteState(stockedStore).PassiveIncomeOperational);
            StringAssert.StartsWith(territoryManager.GetSiteState(stockedStore).PassiveIncomeStatus, "Passive income active");
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_DoesNotAwardPassiveIncomeForUnstaffedOwnedServiceSink()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);

            var stockedStore = industryManager.Industries.Single(industry => industry.Id == "Store1");
            stockedStore.SetOwned(true);
            ClearInputStock(stockedStore);
            stockedStore.AddInput(stockedStore.SortedAcceptedInputs.First(), 5f);

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var result = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);

            Assert.IsNotNull(result);
            Assert.AreEqual(0f, result.ServiceSinkPassiveIncome, 0.01f);
            Assert.AreEqual(0f, result.ServiceSinkStaffingExpense, 0.01f);
            Assert.IsFalse(territoryManager.GetSiteState(stockedStore).PassiveIncomeOperational);
            Assert.AreEqual("No staff assigned", territoryManager.GetSiteState(stockedStore).PassiveIncomeStatus);
            Assert.AreEqual("Missed last week | No staff assigned", territoryManager.GetSiteState(stockedStore).LastPassiveIncomeStatus);
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_DoesNotAwardPassiveIncomeForUnderstockedStaffedServiceSink()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);

            var store = industryManager.Industries.Single(industry => industry.Id == "Store1");
            store.SetOwned(true);
            ClearInputStock(store);

            string result;
            Assert.IsTrue(territoryManager.TrySetServiceSiteOperatorAssigned(store, true, out result), result);

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var maintenance = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);

            Assert.IsNotNull(maintenance);
            Assert.AreEqual(0f, maintenance.ServiceSinkPassiveIncome, 0.01f);
            Assert.AreEqual(territoryManager.GetServiceSiteWeeklyStaffingCost(store), maintenance.ServiceSinkStaffingExpense, 0.01f);
            Assert.IsFalse(territoryManager.GetSiteState(store).PassiveIncomeOperational);
            Assert.AreEqual("Understocked", territoryManager.GetSiteState(store).PassiveIncomeStatus);
            Assert.AreEqual("Missed last week | Understocked", territoryManager.GetSiteState(store).LastPassiveIncomeStatus);
        }

        private static void ClearInputStock(Industry industry)
        {
            foreach (var commodity in industry.SortedAcceptedInputs)
            {
                var stock = industry.GetStock(commodity);
                if (stock > 0.001f)
                {
                    industry.RemoveInput(commodity, stock);
                }
            }
        }
    }
}
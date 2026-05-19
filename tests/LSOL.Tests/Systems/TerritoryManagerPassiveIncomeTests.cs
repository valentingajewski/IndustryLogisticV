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
        public void ProcessWeeklyMaintenance_AwardsPassiveIncomeOnlyForStockedOwnedStoresAndGasStations()
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

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var result = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);

            Assert.IsNotNull(result);
            Assert.AreEqual(
                stockedStore.WeeklyPassiveIncome + stockedGasStation.WeeklyPassiveIncome,
                result.ServiceSinkPassiveIncome,
                0.01f);
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletUpcomingBillsTests
    {
        [TestMethod]
        public void AddServiceSiteStaffingBills_IncludesOnlyOwnedStaffedStoresAndStations()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);

            var staffedStore = industryManager.Industries.Single(industry => industry.Id == "Store1");
            var unstaffedStation = industryManager.Industries.Single(industry => industry.Id == "Petrol Station 02");

            staffedStore.SetOwned(true);
            unstaffedStation.SetOwned(true);

            string result;
            Assert.IsTrue(territoryManager.TrySetServiceSiteOperatorAssigned(staffedStore, true, out result), result);

            var bills = new List<TabletUpcomingBillEntry>();

            TabletStateStore.AddServiceSiteStaffingBills(bills, industryManager.Industries, territoryManager, 480);

            Assert.AreEqual(1, bills.Count);
            Assert.AreEqual(CompanyFinanceCategory.ServiceSiteStaffing, bills[0].Category);
            Assert.AreEqual(staffedStore.Name, bills[0].Label);
            Assert.AreEqual(territoryManager.GetServiceSiteWeeklyStaffingCost(staffedStore), bills[0].Amount, 0.01f);
            Assert.AreEqual(480, bills[0].DueInMinutes);
            StringAssert.Contains(bills[0].Detail, "payroll");
            Assert.IsFalse(bills.Any(entry => entry.Label == unstaffedStation.Name));
        }
    }
}
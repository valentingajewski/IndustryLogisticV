using LSOL.Config;
using LSOL.Domain;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletLocationEconomicsFormatterTests
    {
        [TestMethod]
        public void BuildOwnerCutDetail_WhenSiteIsUnowned_ExplainsPurchaseRemovesCut()
        {
            var summary = new TabletLocationSummary
            {
                Industry = CreateIndustry(industryOwnerCut: 0.2f, weeklyPassiveIncome: 3600f),
                IsOwnedByPlayer = false,
            };

            Assert.AreEqual(
                "Current owner cut 20.00% | Purchase removes it from site payouts",
                TabletLocationEconomicsFormatter.BuildOwnerCutDetail(summary));
        }

        [TestMethod]
        public void BuildOwnerCutDetail_WhenSiteIsOwned_ShowsCutWasRemoved()
        {
            var summary = new TabletLocationSummary
            {
                Industry = CreateIndustry(industryOwnerCut: 0.15f, weeklyPassiveIncome: 700f),
                IsOwnedByPlayer = true,
            };

            Assert.AreEqual(
                "Previous owner cut 15.00% removed | Owned sites keep the full payout",
                TabletLocationEconomicsFormatter.BuildOwnerCutDetail(summary));
        }

        [TestMethod]
        public void BuildOwnerCutDetail_WhenNoCutConfigured_HidesRow()
        {
            var summary = new TabletLocationSummary
            {
                Industry = CreateIndustry(industryOwnerCut: 0f, weeklyPassiveIncome: 0f),
                IsOwnedByPlayer = false,
            };

            Assert.AreEqual(string.Empty, TabletLocationEconomicsFormatter.BuildOwnerCutDetail(summary));
        }

        [TestMethod]
        public void BuildPassiveIncomeDetail_WhenSiteIsUnowned_ShowsPotentialAndLockedRequirements()
        {
            var summary = new TabletLocationSummary
            {
                HasServiceBusinessInfo = true,
                IsOwnedByPlayer = false,
                ServiceWeeklyIncome = 3600f,
                ServiceWeeklyStaffingCost = 300f,
                ServicePassiveIncomeStatus = "Potential passive income locked until purchase",
            };

            Assert.AreEqual(
                "Potential $3,600.00/wk | Staff $300.00/wk | Potential passive income locked until purchase | Needs operator and stock",
                TabletLocationEconomicsFormatter.BuildPassiveIncomeDetail(summary));
        }

        [TestMethod]
        public void BuildPassiveIncomeDetail_WhenSiteIsOwned_ShowsLiveReadinessAndLastPayout()
        {
            var summary = new TabletLocationSummary
            {
                HasServiceBusinessInfo = true,
                IsOwnedByPlayer = true,
                ServiceWeeklyIncome = 4500f,
                ServiceWeeklyStaffingCost = 300f,
                ServicePassiveIncomeStatus = "Passive income active",
                ServiceLastPassiveIncome = 4500f,
            };

            Assert.AreEqual(
                "Expected $4,500.00/wk | Staff $300.00/wk | Passive income active | Last payout $4,500.00",
                TabletLocationEconomicsFormatter.BuildPassiveIncomeDetail(summary));
        }

        private static Industry CreateIndustry(float industryOwnerCut, float weeklyPassiveIncome)
        {
            var config = new IndustryConfig
            {
                Id = "test-site",
                LegacyKey = "test-site",
                Name = "Test Site",
                LocationKind = ExternalLocationKind.Store,
                SiteRole = SiteRole.StoreSink,
                IndustryOwnerCut = industryOwnerCut,
                WeeklyPassiveIncome = weeklyPassiveIncome,
                InputCapacityTons = 10f,
                OutputCapacityTons = 10f,
                ProductionRate = 1f,
                Inputs = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase),
                Outputs = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase),
            };

            return new Industry(config, new System.Collections.Generic.List<ProductionRecipe>(), false, 1f);
        }
    }
}
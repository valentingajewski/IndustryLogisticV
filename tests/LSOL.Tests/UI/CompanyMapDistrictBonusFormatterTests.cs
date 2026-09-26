using System.Collections.Generic;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class CompanyMapDistrictBonusFormatterTests
    {
        [TestMethod]
        public void BuildCaption_ShowsTotalAgainstTheCap()
        {
            var breakdown = DistrictBonusCatalog.BuildBreakdown("WestLosSantos", Pools(("Garbage", 12.5f)), 30f);

            Assert.AreEqual("District bonus +12.5% / 20%", CompanyMapDistrictBonusFormatter.BuildCaption(breakdown));
        }

        [TestMethod]
        public void BuildCaption_WithoutBreakdown_StillShowsTheCap()
        {
            Assert.AreEqual("District bonus +0% / 20%", CompanyMapDistrictBonusFormatter.BuildCaption(null));
        }

        [TestMethod]
        public void BuildDetail_ListsPoolsInContributionOrderWithTheFadeEstimate()
        {
            var breakdown = DistrictBonusCatalog.BuildBreakdown(
                "WestLosSantos",
                Pools(("Garbage", 6.5f), ("Towing", 4f), ("Bus", 0.2f)),
                18f);

            Assert.AreEqual(
                "Garbage 6.5% | Towing 4% | Bus 0.2% - fades in ~18h",
                CompanyMapDistrictBonusFormatter.BuildDetail(breakdown));
        }

        [TestMethod]
        public void BuildDetail_WithoutBonus_ExplainsHowToBuildIt()
        {
            var breakdown = DistrictBonusCatalog.BuildBreakdown("Port", null, DistrictBonusCatalog.DecayHorizonInGameHours);

            Assert.AreEqual(
                "No side job bonus banked. Garbage and towing work in this district builds it.",
                CompanyMapDistrictBonusFormatter.BuildDetail(breakdown));
            Assert.AreEqual(
                "No side job bonus banked. Garbage and towing work in this district builds it.",
                CompanyMapDistrictBonusFormatter.BuildDetail(null));
        }

        [TestMethod]
        public void GetProgressRatio_MapsTheTotalOntoTheCap()
        {
            var quarter = DistrictBonusCatalog.BuildBreakdown("Port", Pools(("Garbage", 5f)), 40f);
            Assert.AreEqual(0.25f, CompanyMapDistrictBonusFormatter.GetProgressRatio(quarter), 0.001f);

            var overCap = DistrictBonusCatalog.BuildBreakdown("Port", Pools(("Garbage", 80f)), 40f);
            Assert.AreEqual(1f, CompanyMapDistrictBonusFormatter.GetProgressRatio(overCap), 0.001f);

            Assert.AreEqual(0f, CompanyMapDistrictBonusFormatter.GetProgressRatio(null), 0.001f);
        }

        [TestMethod]
        public void BuildShortSuffix_OnlyShowsWhenSomethingIsBanked()
        {
            var banked = DistrictBonusCatalog.BuildBreakdown("Port", Pools(("Towing", 7.5f)), 40f);
            var empty = DistrictBonusCatalog.BuildBreakdown("Port", null, 40f);

            Assert.AreEqual(" | Bonus +7.5%", CompanyMapDistrictBonusFormatter.BuildShortSuffix(banked));
            Assert.AreEqual(string.Empty, CompanyMapDistrictBonusFormatter.BuildShortSuffix(empty));
            Assert.AreEqual(string.Empty, CompanyMapDistrictBonusFormatter.BuildShortSuffix(null));
        }

        [TestMethod]
        public void BuildTabletSummary_NamesTheTopContributingJob()
        {
            Assert.AreEqual("Bonus none", CompanyMapDistrictBonusFormatter.BuildTabletSummary(0f, string.Empty));
            Assert.AreEqual("Bonus +5% (Garbage)", CompanyMapDistrictBonusFormatter.BuildTabletSummary(5f, "Garbage"));
            Assert.AreEqual("Bonus +5%", CompanyMapDistrictBonusFormatter.BuildTabletSummary(5f, string.Empty));
        }

        private static Dictionary<string, float> Pools(params (string JobId, float Points)[] entries)
        {
            var pools = new Dictionary<string, float>();
            foreach (var entry in entries)
            {
                pools[entry.JobId] = entry.Points;
            }

            return pools;
        }
    }
}

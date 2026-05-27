using System.Linq;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class CompanyMapControllerVisualHelperTests
    {
        [TestMethod]
        public void GetWeeklyActivityTargetMetrics_WhenTargetIsZero_ReturnNeutralValues()
        {
            Assert.IsFalse(CompanyMapController.HasWeeklyActivityTarget(0f));
            Assert.AreEqual(0f, CompanyMapController.GetWeeklyActivityTargetMeterRatio(18f, 0f), 0.001f);
            Assert.AreEqual(0f, CompanyMapController.GetWeeklyActivityTargetPercentOfTarget(18f, 0f), 0.001f);
        }

        [TestMethod]
        public void GetWeeklyActivityTargetMetrics_WhenOverTarget_CapsMeterButKeepsDisplayPercent()
        {
            Assert.AreEqual(1f, CompanyMapController.GetWeeklyActivityTargetMeterRatio(60f, 40f), 0.001f);
            Assert.AreEqual(1.5f, CompanyMapController.GetWeeklyActivityTargetPercentOfTarget(60f, 40f), 0.001f);
        }

        [TestMethod]
        public void GetDistrictInfluenceHeatBucket_MapsExpectedThresholds()
        {
            Assert.AreEqual(CompanyMapController.DistrictInfluenceHeatBucket.Cold, CompanyMapController.GetDistrictInfluenceHeatBucket(0.2f));
            Assert.AreEqual(CompanyMapController.DistrictInfluenceHeatBucket.Emerging, CompanyMapController.GetDistrictInfluenceHeatBucket(0.35f));
            Assert.AreEqual(CompanyMapController.DistrictInfluenceHeatBucket.Anchored, CompanyMapController.GetDistrictInfluenceHeatBucket(0.6f));
            Assert.AreEqual(CompanyMapController.DistrictInfluenceHeatBucket.Dominant, CompanyMapController.GetDistrictInfluenceHeatBucket(1.05f));
        }

        [TestMethod]
        public void GetNetworkNodeSize_LongDistrictNamesReceiveWiderCards()
        {
            var shortNode = CompanyMapController.GetNetworkNodeSize("Port");
            var longNode = CompanyMapController.GetNetworkNodeSize("WestLosSantos");

            Assert.IsTrue(longNode.Width > shortNode.Width);
            Assert.IsTrue(longNode.Width >= 170f);
            Assert.IsTrue(longNode.Height >= shortNode.Height);
        }

        [TestMethod]
        public void WrapTextToLines_LongPlannerSummaryWrapsWithinRequestedWidth()
        {
            var lines = CompanyMapController.WrapTextToLines(
                "NPC Ready | Planner Senora freight route remains blocked by a very long selected candidate label",
                32);

            Assert.IsTrue(lines.Length >= 2);
            Assert.IsTrue(lines.All(line => line.Length <= 32));
        }

        [TestMethod]
        public void CalculateNetworkDetailPanelLayout_StacksCardsWithoutOverlap()
        {
            var layout = CompanyMapController.CalculateNetworkDetailPanelLayout(140f, 440f, 8, 72f, 78f);

            Assert.IsTrue(layout.ActivityY >= layout.OverviewY + layout.OverviewHeight);
            Assert.IsTrue(layout.HotNowY >= layout.ActivityY + layout.ActivityHeight);
            Assert.IsTrue(layout.CorridorsDividerY >= layout.HotNowY + layout.HotNowHeight);
            Assert.IsTrue(layout.CorridorsStartY > layout.CorridorsTitleY);
            Assert.IsTrue(layout.RemainingHeight > 0f);
        }
    }
}
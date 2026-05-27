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
    }
}
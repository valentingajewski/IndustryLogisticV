using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletWarehouseRiskFormatterTests
    {
        [TestMethod]
        public void BuildWarehouseOverviewTelemetry_WithRisk_IncludesLevelLastAndNextLoss()
        {
            var risk = new WarehouseStorageRiskSnapshot
            {
                StorageCondition = 0.68f,
                InventoryValue = 120000f,
                HasSensitiveExposure = true,
                LastDaySpoilageValue = 1250f,
                LastDayShrinkageValue = 310f,
                ProjectedSpoilageValue = 980f,
                ProjectedShrinkageValue = 220f,
            };

            var detail = TabletUiHelpers.BuildWarehouseOverviewTelemetry(risk);

            StringAssert.Contains(detail, "Risk High");
            StringAssert.Contains(detail, "$1,560.00");
            StringAssert.Contains(detail, "$1,200.00");
        }

        [TestMethod]
        public void BuildWarehouseStewardshipDetail_WithRisk_SplitsSpoilageAndShrinkage()
        {
            var risk = new WarehouseStorageRiskSnapshot
            {
                StorageCondition = 0.64f,
                InventoryValue = 90000f,
                HasSensitiveExposure = true,
                DominantLossClass = WarehouseLossClass.Spoilage,
                DominantCommodity = "ProcessedFood",
                CurrentWeekSpoilageValue = 2200f,
                CurrentWeekShrinkageValue = 750f,
                ProjectedSpoilageValue = 1100f,
                ProjectedShrinkageValue = 260f,
            };

            var detail = TabletUiHelpers.BuildWarehouseStewardshipDetail(risk);

            StringAssert.Contains(detail, "This week $2,200.00 spoilage + $750.00 shrinkage");
            StringAssert.Contains(detail, "Next day $1,100.00 spoilage + $260.00 shrinkage");
            StringAssert.Contains(detail, "Spoilage on ProcessedFood");
        }

        [TestMethod]
        public void BuildWarehouseRiskLevel_WithoutExposure_ReturnsStable()
        {
            var risk = new WarehouseStorageRiskSnapshot
            {
                StorageCondition = 0.92f,
                InventoryValue = 0f,
                HasSensitiveExposure = false,
            };

            Assert.AreEqual("Stable", TabletUiHelpers.BuildWarehouseRiskLevel(risk));
            Assert.AreEqual(string.Empty, TabletUiHelpers.BuildWarehouseOverviewTelemetry(risk));
        }
    }
}
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class BudgetFleetResaleFormatterTests
    {
        [TestMethod]
        public void BuildOverviewRecoveryDetail_WithOwnedFleet_IncludesBasisResaleAndRecovery()
        {
            var summary = new TabletFleetResaleSummary
            {
                OwnedVehicleCount = 3,
                PurchaseBasis = 240000f,
                EstimatedResaleValue = 102000f,
                RecoveryPercentOfPurchase = 42.5f,
            };

            var detail = BudgetFleetResaleFormatter.BuildOverviewRecoveryDetail(summary);

            StringAssert.Contains(detail, "Owned 3");
            StringAssert.Contains(detail, "$240,000");
            StringAssert.Contains(detail, "$102,000");
            StringAssert.Contains(detail, "42.5%");
        }

        [TestMethod]
        public void BuildOverviewDepreciationDetail_WithWeakestVehicle_IncludesWeakestRecovery()
        {
            var summary = new TabletFleetResaleSummary
            {
                OwnedVehicleCount = 2,
                TotalDepreciationLoss = 98000f,
                WeakestVehicleName = "Truck Rusty",
                WeakestVehicleRecoveryPercent = 35f,
            };

            var detail = BudgetFleetResaleFormatter.BuildOverviewDepreciationDetail(summary);

            StringAssert.Contains(detail, "$98,000");
            StringAssert.Contains(detail, "Truck Rusty");
            StringAssert.Contains(detail, "35.0%");
        }

        [TestMethod]
        public void BuildRootTileDetail_WithoutOwnedFleet_ReturnsNotAvailable()
        {
            var detail = BudgetFleetResaleFormatter.BuildRootTileDetail(new TabletFleetResaleSummary());

            Assert.AreEqual("Fleet resale n/a", detail);
        }
    }
}

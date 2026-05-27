using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class OfficeFuelManagementFormatterTests
    {
        [TestMethod]
        public void BuildTankStatusDetail_WhenNoTankInstalled_PromptsForTank()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = true,
                OfficeIsActive = true,
            };

            Assert.AreEqual("Install a Diesel Tank first.", OfficeFuelManagementFormatter.BuildTankStatusDetail(snapshot));
        }

        [TestMethod]
        public void BuildPricingDetail_WhenTankIsPartiallyFilled_ShowsReplacementAndDeliveredRates()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = true,
                OfficeIsActive = true,
                HasTankInstalled = true,
                StoredLiters = 5200f,
                CapacityLiters = 15000f,
                FreeLiters = 9800f,
                SpotReplacementRatePerLiter = 0.11f,
                DeliveredRatePerLiter = 0.1155f,
                EstimatedFillCost = 1131.9f,
            };

            Assert.AreEqual(
                "Replacement-rate spot $0.11/L | Delivered $0.12/L | Fill remaining about $1,131.90",
                OfficeFuelManagementFormatter.BuildPricingDetail(snapshot));
        }

        [TestMethod]
        public void BuildFuelDeliveryDetail_WhenTankIsFull_ShowsNoRefillNeeded()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = true,
                OfficeIsActive = true,
                HasTankInstalled = true,
                StoredLiters = 15000f,
                CapacityLiters = 15000f,
                FreeLiters = 0f,
                DeliveredRatePerLiter = 0.1155f,
            };

            Assert.AreEqual(
                "Refill state: full | Delivered about $0.12/L | No refill needed.",
                OfficeFuelManagementFormatter.BuildFuelDeliveryDetail(snapshot));
        }

        [TestMethod]
        public void BuildFuelDeliveryDetail_WhenDeliveryIsActive_SurfacesTankerState()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = true,
                OfficeIsActive = true,
                HasTankInstalled = true,
                StoredLiters = 5200f,
                CapacityLiters = 15000f,
                FreeLiters = 9800f,
                HasActiveDeliveryForOffice = true,
                DeliveredRatePerLiter = 0.1155f,
            };

            Assert.AreEqual(
                "Refill state: tanker already en route | 9,800.00L free in tank | Delivered about $0.12/L.",
                OfficeFuelManagementFormatter.BuildFuelDeliveryDetail(snapshot));
        }

        [TestMethod]
        public void BuildVehicleRefuelDetail_WhenTankCoversCurrentRig_ShowsRemainingFuelAfterRefill()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = true,
                OfficeIsActive = true,
                HasTankInstalled = true,
                StoredLiters = 1500f,
                CapacityLiters = 15000f,
                HasEligibleOfficeVehicle = true,
                VehicleFuelNeededLiters = 1260f,
            };

            Assert.AreEqual(
                "Tank covers the current rig | 240.00L available after this refill.",
                OfficeFuelManagementFormatter.BuildVehicleRefuelDetail(snapshot));
        }
    }
}
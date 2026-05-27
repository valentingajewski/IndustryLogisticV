using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class VehicleFuelHudFormatterTests
    {
        [TestMethod]
        public void BuildRangeLabel_WhenEstimateIsUnavailable_ShowsCalibrating()
        {
            var telemetry = new VehicleFuelTelemetry
            {
                CurrentLiters = 85f,
                CapacityLiters = 120f,
                FuelRatio = 85f / 120f,
            };

            Assert.AreEqual("Range calibrating", VehicleFuelHudFormatter.BuildRangeLabel(telemetry, true));
        }

        [TestMethod]
        public void BuildRangeLabel_WhenEstimateIsAvailable_UsesMetricAndImperialUnits()
        {
            var telemetry = new VehicleFuelTelemetry
            {
                CurrentLiters = 60f,
                CapacityLiters = 120f,
                FuelRatio = 0.5f,
                HasRangeEstimate = true,
                EstimatedRangeMeters = 8400f,
            };

            Assert.AreEqual("Range 8.4 km", VehicleFuelHudFormatter.BuildRangeLabel(telemetry, true));
            Assert.AreEqual("Range 5.2 mi", VehicleFuelHudFormatter.BuildRangeLabel(telemetry, false));
        }

        [TestMethod]
        public void BuildRangeLabel_WhenVehicleIsOutOfFuel_ShowsZeroRange()
        {
            var telemetry = new VehicleFuelTelemetry
            {
                CapacityLiters = 120f,
                IsOutOfFuel = true,
            };

            Assert.AreEqual("Range 0 m", VehicleFuelHudFormatter.BuildRangeLabel(telemetry, true));
        }

        [TestMethod]
        public void GetSeverity_UsesSharedWatchAndUrgentThresholds()
        {
            Assert.AreEqual(
                VehicleFuelHudSeverity.Watch,
                VehicleFuelHudFormatter.GetSeverity(new VehicleFuelTelemetry { CapacityLiters = 120f, FuelRatio = 0.49f }));
            Assert.AreEqual(
                VehicleFuelHudSeverity.Urgent,
                VehicleFuelHudFormatter.GetSeverity(new VehicleFuelTelemetry { CapacityLiters = 120f, FuelRatio = 0.19f }));
            Assert.AreEqual(
                VehicleFuelHudSeverity.Empty,
                VehicleFuelHudFormatter.GetSeverity(new VehicleFuelTelemetry { CapacityLiters = 120f, FuelRatio = 0f, IsOutOfFuel = true }));
            Assert.AreEqual(
                VehicleFuelHudSeverity.Healthy,
                VehicleFuelHudFormatter.GetSeverity(new VehicleFuelTelemetry { CapacityLiters = 120f, FuelRatio = 0.5f }));
        }
    }
}
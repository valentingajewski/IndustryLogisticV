using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class VehicleFuelRangeEstimatorTests
    {
        [TestMethod]
        public void AddObservation_WhenMovementIsTooSmallOrTooSlow_DoesNotCalibrate()
        {
            var estimator = new VehicleFuelRangeEstimator();

            estimator.AddObservation(5f, 0.05f, VehicleFuelRangeEstimator.MinimumObservationSpeedMetersPerSecond + 2f);
            estimator.AddObservation(25f, 0.05f, 1f);

            Assert.IsFalse(estimator.HasEstimate);
            Assert.AreEqual(0f, estimator.SmoothedLitersPerMeter, 0.0001f);
            Assert.AreEqual(0f, estimator.EstimateRemainingRangeMeters(40f), 0.01f);
        }

        [TestMethod]
        public void AddObservation_AfterMeaningfulMovement_ProducesStableEstimate()
        {
            var estimator = new VehicleFuelRangeEstimator();

            for (int i = 0; i < 5; i++)
            {
                estimator.AddObservation(25f, 0.05f, 12f);
            }

            Assert.IsTrue(estimator.HasEstimate);
            Assert.AreEqual(0.002f, estimator.SmoothedLitersPerMeter, 0.0001f);
            Assert.AreEqual(20000f, estimator.EstimateRemainingRangeMeters(40f), 1f);
        }

        [TestMethod]
        public void EstimateRemainingRangeMeters_WhenCurrentFuelIsEmpty_ReturnsZero()
        {
            var estimator = new VehicleFuelRangeEstimator();
            for (int i = 0; i < 5; i++)
            {
                estimator.AddObservation(25f, 0.05f, 12f);
            }

            Assert.AreEqual(0f, estimator.EstimateRemainingRangeMeters(0f), 0.01f);
        }
    }
}
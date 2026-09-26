using System.Collections.Generic;
using System.Linq;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class DistrictBonusCatalogTests
    {
        [TestMethod]
        public void GetContributionPointsPerUnit_MatchesDesignValues()
        {
            Assert.AreEqual(2.0f, DistrictBonusCatalog.GetContributionPointsPerUnit("Garbage"), 0.001f);
            Assert.AreEqual(1.0f, DistrictBonusCatalog.GetContributionPointsPerUnit("Towing"), 0.001f);
            Assert.AreEqual(1.5f, DistrictBonusCatalog.GetContributionPointsPerUnit("Bus"), 0.001f);
            Assert.AreEqual(0.5f, DistrictBonusCatalog.GetContributionPointsPerUnit("FoodDelivery"), 0.001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.GetContributionPointsPerUnit("Trucking"), 0.001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.GetContributionPointsPerUnit(null), 0.001f);
        }

        [TestMethod]
        public void NormalizeJobId_IsCaseInsensitiveAndRejectsUnknownJobs()
        {
            Assert.AreEqual("Garbage", DistrictBonusCatalog.NormalizeJobId("garbage"));
            Assert.AreEqual("FoodDelivery", DistrictBonusCatalog.NormalizeJobId(" fooddelivery "));
            Assert.AreEqual(string.Empty, DistrictBonusCatalog.NormalizeJobId("Trucking"));
            Assert.IsFalse(DistrictBonusCatalog.IsKnownJobId("Trucking"));
            Assert.IsTrue(DistrictBonusCatalog.IsKnownJobId("Bus"));
        }

        [TestMethod]
        public void GetDecayFactor_DecaysLinearlyToZeroOverTheHorizon()
        {
            // A clock that has not moved (or moved backwards) must never decay anything.
            Assert.AreEqual(1f, DistrictBonusCatalog.GetDecayFactor(0f), 0.0001f);
            Assert.AreEqual(1f, DistrictBonusCatalog.GetDecayFactor(-90f), 0.0001f);

            var halfHorizonMinutes = (DistrictBonusCatalog.DecayHorizonInGameHours / 2f) * DistrictBonusCatalog.MinutesPerInGameHour;
            Assert.AreEqual(0.5f, DistrictBonusCatalog.GetDecayFactor(halfHorizonMinutes), 0.0001f);

            var fullHorizonMinutes = DistrictBonusCatalog.DecayHorizonInGameHours * DistrictBonusCatalog.MinutesPerInGameHour;
            Assert.AreEqual(0f, DistrictBonusCatalog.GetDecayFactor(fullHorizonMinutes), 0.0001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.GetDecayFactor(fullHorizonMinutes * 3f), 0.0001f);
        }

        [TestMethod]
        public void GetRemainingInGameHours_CountsDownFromTheHorizon()
        {
            Assert.AreEqual(DistrictBonusCatalog.DecayHorizonInGameHours, DistrictBonusCatalog.GetRemainingInGameHours(0f), 0.001f);
            Assert.AreEqual(DistrictBonusCatalog.DecayHorizonInGameHours, DistrictBonusCatalog.GetRemainingInGameHours(-30f), 0.001f);

            var quarterMinutes = (DistrictBonusCatalog.DecayHorizonInGameHours / 4f) * DistrictBonusCatalog.MinutesPerInGameHour;
            Assert.AreEqual(DistrictBonusCatalog.DecayHorizonInGameHours * 0.75f, DistrictBonusCatalog.GetRemainingInGameHours(quarterMinutes), 0.001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.GetRemainingInGameHours(100000f), 0.001f);
        }

        [TestMethod]
        public void GetTotalPercent_ClampsToTheCapAndSkipsTheExcludedJob()
        {
            var pools = new Dictionary<string, float>
            {
                { "Garbage", 12f },
                { "Towing", 4f },
            };

            Assert.AreEqual(16f, DistrictBonusCatalog.GetTotalPercent(pools), 0.001f);
            Assert.AreEqual(4f, DistrictBonusCatalog.GetTotalPercent(pools, "Garbage"), 0.001f);
            Assert.AreEqual(16f, DistrictBonusCatalog.GetTotalPercent(pools, "FoodDelivery"), 0.001f);

            // Exclusion matches the job id case-insensitively.
            Assert.AreEqual(4f, DistrictBonusCatalog.GetTotalPercent(pools, "garbage"), 0.001f);

            pools["Bus"] = 30f;
            Assert.AreEqual(DistrictBonusCatalog.CapPercent, DistrictBonusCatalog.GetTotalPercent(pools), 0.001f);
        }

        [TestMethod]
        public void AddPoints_IgnoresUnknownJobsAndNonPositiveAmounts()
        {
            var pools = new Dictionary<string, float>();

            Assert.AreEqual(2f, DistrictBonusCatalog.AddPoints(pools, "garbage", 2f), 0.001f);
            Assert.AreEqual(3f, DistrictBonusCatalog.AddPoints(pools, "Garbage", 1f), 0.001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.AddPoints(pools, "Trucking", 5f), 0.001f);
            Assert.AreEqual(0f, DistrictBonusCatalog.AddPoints(pools, "Garbage", 0f), 0.001f);
            Assert.IsFalse(pools.ContainsKey("Trucking"));
            Assert.AreEqual(1, pools.Count);
        }

        [TestMethod]
        public void ClampPoolsToCap_ScalesPoolsDownProportionally()
        {
            var pools = new Dictionary<string, float>
            {
                { "Garbage", 30f },
                { "Towing", 10f },
            };

            DistrictBonusCatalog.ClampPoolsToCap(pools);

            // 40 -> 20 is a 0.5 factor: attribution is preserved exactly.
            Assert.AreEqual(15f, pools["Garbage"], 0.001f);
            Assert.AreEqual(5f, pools["Towing"], 0.001f);
            Assert.AreEqual(DistrictBonusCatalog.CapPercent, DistrictBonusCatalog.GetTotalPercent(pools), 0.001f);
        }

        [TestMethod]
        public void ClampPoolsToCap_LeavesPoolsUnderTheCapUntouched()
        {
            var pools = new Dictionary<string, float> { { "Garbage", 7f } };

            DistrictBonusCatalog.ClampPoolsToCap(pools);

            Assert.AreEqual(7f, pools["Garbage"], 0.001f);
        }

        [TestMethod]
        public void ApplyDecayFactor_ShrinksEveryPoolAndDropsEmptyOnes()
        {
            var pools = new Dictionary<string, float>
            {
                { "Garbage", 10f },
                { "Towing", 0.02f },
            };

            DistrictBonusCatalog.ApplyDecayFactor(pools, 0.5f);

            Assert.AreEqual(5f, pools["Garbage"], 0.001f);
            Assert.IsFalse(pools.ContainsKey("Towing"), "pools below the reporting threshold are dropped");
        }

        [TestMethod]
        public void BuildBreakdown_OrdersPoolsAndReportsTheTopContributor()
        {
            var pools = new Dictionary<string, float>
            {
                { "Towing", 3f },
                { "Garbage", 6f },
                { "Bus", 0.5f },
            };

            var breakdown = DistrictBonusCatalog.BuildBreakdown("WestLosSantos", pools, 18f);

            Assert.AreEqual("WestLosSantos", breakdown.DistrictName);
            Assert.AreEqual(9.5f, breakdown.TotalPercent, 0.001f);
            Assert.AreEqual(DistrictBonusCatalog.CapPercent, breakdown.CapPercent, 0.001f);
            Assert.AreEqual(18f, breakdown.RemainingInGameHours, 0.001f);
            Assert.AreEqual(3, breakdown.Pools.Count);
            Assert.AreEqual("Garbage", breakdown.Pools[0].JobId);
            Assert.AreEqual("Garbage", breakdown.TopContributorLabel);

            // Display labels are player facing, not raw ids.
            Assert.AreEqual("Food Delivery", DistrictBonusCatalog.GetJobLabel("FoodDelivery"));
        }

        [TestMethod]
        public void BuildBreakdown_WithoutPools_ReportsNoBonus()
        {
            var breakdown = DistrictBonusCatalog.BuildBreakdown("Port", null, DistrictBonusCatalog.DecayHorizonInGameHours);

            Assert.IsFalse(breakdown.HasAny);
            Assert.AreEqual(0f, breakdown.TotalPercent, 0.001f);
            Assert.AreEqual(0, breakdown.Pools.Count);
            Assert.AreEqual(string.Empty, breakdown.TopContributorLabel);
        }

        [TestMethod]
        public void JobIds_CoverEverySideJobButNotTrucking()
        {
            CollectionAssert.AreEquivalent(
                new[] { "Towing", "Garbage", "FoodDelivery", "Bus" },
                DistrictBonusCatalog.JobIds.ToArray());
        }
    }
}

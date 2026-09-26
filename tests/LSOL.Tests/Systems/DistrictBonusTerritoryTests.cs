using System;
using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class DistrictBonusTerritoryTests
    {
        private const string TestDistrict = "WestLosSantos";

        private sealed class TestClock
        {
            public int Minute { get; set; }
        }

        [TestMethod]
        public void RegisterSideJobCompletion_ReportsAppliedPointsAndNewTotal()
        {
            var territoryManager = CreateTerritoryManager();

            var award = territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);

            Assert.IsTrue(award.Applied);
            Assert.AreEqual(2f, award.AppliedPoints, 0.001f);
            Assert.AreEqual(2f, award.TotalPercent, 0.001f);
            Assert.AreEqual("Garbage", award.JobId);
            Assert.AreEqual("Garbage", award.JobLabel);
            Assert.AreEqual(TestDistrict, award.DistrictName);
            Assert.AreEqual(DistrictBonusCatalog.CapPercent, award.CapPercent, 0.001f);
            Assert.AreEqual(2f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
        }

        [TestMethod]
        public void RegisterSideJobCompletion_IgnoresUnknownJobsAndUnknownDistricts()
        {
            var territoryManager = CreateTerritoryManager();

            var trucking = territoryManager.RegisterSideJobCompletion("Trucking", TestDistrict, 1f);
            var unknownDistrict = territoryManager.RegisterSideJobCompletion("Garbage", "Nowhere", 1f);
            var noUnits = territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 0f);

            Assert.IsFalse(trucking.Applied);
            Assert.IsFalse(unknownDistrict.Applied);
            Assert.IsFalse(noUnits.Applied);
            Assert.AreEqual(0f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
        }

        [TestMethod]
        public void RegisterSideJobCompletion_StopsBankingWorkAtTheCap()
        {
            var territoryManager = CreateTerritoryManager();

            // Garbage pays 2 points per route: 20 routes would be 40 points without the cap.
            for (int i = 0; i < 20; i++)
            {
                territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            }

            Assert.AreEqual(DistrictBonusCatalog.CapPercent, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);

            var overflow = territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            Assert.IsFalse(overflow.Applied);
            Assert.AreEqual(0f, overflow.AppliedPoints, 0.001f);
            Assert.AreEqual(DistrictBonusCatalog.CapPercent, overflow.TotalPercent, 0.001f);
        }

        [TestMethod]
        public void GetDistrictBonusExcluding_RemovesOnlyThatJobsPool()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            territoryManager.RegisterSideJobCompletion("Towing", TestDistrict, 3f);

            Assert.AreEqual(5f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
            Assert.AreEqual(3f, territoryManager.GetDistrictBonusExcluding(TestDistrict, "Garbage"), 0.001f);
            Assert.AreEqual(2f, territoryManager.GetDistrictBonusExcluding(TestDistrict, "Towing"), 0.001f);
        }

        [TestMethod]
        public void GetDistrictBonusBreakdown_AttributesEveryJobThatContributed()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            territoryManager.RegisterSideJobCompletion("Towing", TestDistrict, 4f);

            var breakdown = territoryManager.GetDistrictBonusBreakdown(TestDistrict);

            Assert.IsTrue(breakdown.HasAny);
            Assert.AreEqual(6f, breakdown.TotalPercent, 0.001f);
            Assert.AreEqual("Towing", breakdown.TopContributorLabel);
            Assert.AreEqual(2, breakdown.Pools.Count);
            Assert.AreEqual("Garbage", breakdown.Pools[1].JobId);
        }

        [TestMethod]
        public void GetDistrictBonus_DecaysWithTheInGameClockAndNeverGoesNegative()
        {
            var clock = new TestClock { Minute = 1000 };
            var territoryManager = CreateTerritoryManager();
            territoryManager.ConfigureClock(() => clock.Minute);

            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            Assert.AreEqual(2f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);

            // Half the horizon later the pool is halved.
            clock.Minute += (int)(DistrictBonusCatalog.DecayHorizonInGameHours * DistrictBonusCatalog.MinutesPerInGameHour / 2f);
            Assert.AreEqual(1f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);

            // The rest of the horizon wipes it out completely.
            clock.Minute += (int)(DistrictBonusCatalog.DecayHorizonInGameHours * DistrictBonusCatalog.MinutesPerInGameHour / 2f);
            Assert.AreEqual(0f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
            Assert.IsFalse(territoryManager.GetDistrictBonusBreakdown(TestDistrict).HasAny);
        }

        [TestMethod]
        public void GetDistrictBonus_IgnoresAClockThatMovedBackwards()
        {
            var clock = new TestClock { Minute = 5000 };
            var territoryManager = CreateTerritoryManager();
            territoryManager.ConfigureClock(() => clock.Minute);

            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);

            // Loading an older save can rewind the game clock: that must not decay (or inflate) anything.
            clock.Minute = 100;
            Assert.AreEqual(2f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
            Assert.AreEqual(2f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
        }

        [TestMethod]
        public void CreateSnapshot_KeepsDistrictsThatOnlyHaveASideJobBonus()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);

            var snapshot = territoryManager.CreateSnapshot();
            var districtSnapshot = snapshot.Districts.SingleOrDefault(entry => entry.DistrictName == TestDistrict);

            Assert.IsNotNull(districtSnapshot, "a district with only a side job bonus must still be saved");
            Assert.AreEqual(2f, districtSnapshot.SideJobBonusPools["Garbage"], 0.001f);
        }

        [TestMethod]
        public void ApplySnapshot_RestoresPoolsWithoutMaterializingDecay()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);
            territoryManager.RegisterSideJobCompletion("Towing", TestDistrict, 2f);
            var snapshot = territoryManager.CreateSnapshot();

            var restored = CreateTerritoryManager();
            restored.ApplySnapshot(snapshot);

            // No clock configured: the stored pools are reported exactly as saved.
            Assert.AreEqual(4f, restored.GetDistrictBonus(TestDistrict), 0.001f);
            Assert.AreEqual(2f, restored.GetDistrictBonusExcluding(TestDistrict, "Towing"), 0.001f);
        }

        [TestMethod]
        public void Reset_ClearsStoredPools()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.RegisterSideJobCompletion("Garbage", TestDistrict, 1f);

            territoryManager.Reset();

            Assert.AreEqual(0f, territoryManager.GetDistrictBonus(TestDistrict), 0.001f);
        }

        [TestMethod]
        public void AdjustDeliveryRevenue_AppliesTheDistrictBonusOnlyWhenAsked()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);

            var industry = industryManager.Industries.First(candidate => !string.IsNullOrWhiteSpace(candidate.DistrictName));
            territoryManager.RegisterSideJobCompletion("Garbage", industry.DistrictName, 1f);

            var withBonus = territoryManager.AdjustDeliveryRevenue(industry, null, 10f, 1000f, true);
            var withoutBonus = territoryManager.AdjustDeliveryRevenue(industry, null, 10f, 1000f, false);

            Assert.IsTrue(withBonus > withoutBonus, "the player-driven path must include the district bonus");

            // One garbage route banks 2 points, and the bonus is additive on the multiplier, so the
            // difference on a 1000 base revenue is exactly 2% of it.
            Assert.AreEqual(20f, withBonus - withoutBonus, 0.01f);
        }

        private static TerritoryManager CreateTerritoryManager()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            return new TerritoryManager(config, industryManager);
        }
    }
}

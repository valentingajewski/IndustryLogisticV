using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PlayerSkillSystemTests
    {
        [TestMethod]
        public void AddXp_NpcSource_IsIgnored()
        {
            var system = new PlayerSkillSystem();

            system.AddXp(PlayerSkillId.Trucking, 500f, PlayerSkillXpSource.Npc);

            Assert.AreEqual(0f, system.GetTotalXp(PlayerSkillId.Trucking), 0.001f);
            Assert.AreEqual(0, system.GetLevel(PlayerSkillId.Trucking));
        }

        [TestMethod]
        public void XpToLevelDerivation_Boundaries()
        {
            var system = new PlayerSkillSystem();

            Assert.AreEqual(0, system.GetLevel(PlayerSkillId.Trucking));

            system.AddXp(PlayerSkillId.Trucking, 99.9f, PlayerSkillXpSource.Player);
            Assert.AreEqual(0, system.GetLevel(PlayerSkillId.Trucking));

            system.AddXp(PlayerSkillId.Trucking, 0.1f, PlayerSkillXpSource.Player);
            Assert.AreEqual(1, system.GetLevel(PlayerSkillId.Trucking));

            var toLevel2 = system.GetXpRequiredToReachLevel(PlayerSkillId.Trucking, 2);
            Assert.IsTrue(toLevel2 > 0f);
            system.AddXp(PlayerSkillId.Trucking, toLevel2, PlayerSkillXpSource.Player);
            Assert.AreEqual(2, system.GetLevel(PlayerSkillId.Trucking));
            Assert.AreEqual(0f, system.GetXpIntoLevel(PlayerSkillId.Trucking), 0.01f);
        }

        [TestMethod]
        public void BonusMultiplier_IsMonotonicAndCapped()
        {
            var system = new PlayerSkillSystem();

            Assert.AreEqual(1f, system.GetBonusMultiplier(PlayerSkillId.Trucking), 0.0001f);

            var previous = 1f;
            for (int i = 0; i < 50; i++)
            {
                system.AdvanceLevel(PlayerSkillId.Trucking, 1);
                var multiplier = system.GetBonusMultiplier(PlayerSkillId.Trucking);
                Assert.IsTrue(multiplier >= previous, "bonus multiplier should be monotonic");
                Assert.IsTrue(multiplier <= 1.25f + 0.0001f, "bonus multiplier should never exceed 1 + cap");
                previous = multiplier;
            }

            system.AddXp(PlayerSkillId.Trucking, 10000000000f, PlayerSkillXpSource.Player);
            var cappedLevel = system.GetLevel(PlayerSkillId.Trucking);
            Assert.IsTrue(cappedLevel >= 1000, "expected a very high level after a huge XP grant");
            Assert.IsTrue(system.GetBonusMultiplier(PlayerSkillId.Trucking) <= 1.25f + 0.0001f);
            Assert.IsTrue(system.GetBonusMultiplier(PlayerSkillId.Trucking) > 1.24f);
        }

        [TestMethod]
        public void AdvanceLevel_MovesExactlyRequestedLevelCount()
        {
            var system = new PlayerSkillSystem();

            Assert.AreEqual(0, system.GetLevel(PlayerSkillId.Towing));

            system.AdvanceLevel(PlayerSkillId.Towing, 1);
            Assert.AreEqual(1, system.GetLevel(PlayerSkillId.Towing));

            system.AdvanceLevel(PlayerSkillId.Towing, 2);
            Assert.AreEqual(3, system.GetLevel(PlayerSkillId.Towing));
        }

        [TestMethod]
        public void Snapshot_RoundTrip_PreservesXp()
        {
            var system = new PlayerSkillSystem();
            system.AddXp(PlayerSkillId.Trucking, 100f, PlayerSkillXpSource.Player);

            var snapshot = system.CreatePersistenceSnapshot();
            Assert.IsTrue(snapshot.HasData);

            var restored = new PlayerSkillSystem();
            restored.ApplyPersistenceSnapshot(snapshot);

            Assert.AreEqual(100f, restored.GetTotalXp(PlayerSkillId.Trucking), 0.001f);
            Assert.AreEqual(0f, restored.GetTotalXp(PlayerSkillId.Garbage), 0.001f);
        }

        [TestMethod]
        public void ApplySnapshot_Null_StartsAtZero()
        {
            var system = new PlayerSkillSystem();
            system.AddXp(PlayerSkillId.Trucking, 200f, PlayerSkillXpSource.Player);

            system.ApplyPersistenceSnapshot(null);

            Assert.AreEqual(0f, system.GetTotalXp(PlayerSkillId.Trucking), 0.001f);
            Assert.AreEqual(0, system.GetLevel(PlayerSkillId.Trucking));
        }
    }
}

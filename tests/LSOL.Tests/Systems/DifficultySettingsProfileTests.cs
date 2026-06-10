using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class DifficultySettingsProfileTests
    {
        [TestMethod]
        public void ApplyBooleanBulkState_OnlyChangesBooleanDifficultyFields()
        {
            var profile = new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = EconomyDifficultyPreset.Impossible,
                NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Hardcore,
                NpcRouteLimit = 0,
                MaxModuleLimitPerSite = 7,
                VehicleFuelDifficultyEnabled = false,
                CargoWeightPowerDifficultyEnabled = false,
                CargoDamageDifficultyEnabled = true,
                IndustryPricingDifficultyEnabled = false,
                LicensingDifficultyEnabled = false,
                CorridorRestrictionDifficultyEnabled = true,
                ReputationDifficultyEnabled = true,
                OfficeGarageLimitDifficultyEnabled = true,
                OfficeNpcLimitDifficultyEnabled = false,
            };

            profile.ApplyBooleanBulkState(true);

            Assert.AreEqual(EconomyDifficultyPreset.Impossible, profile.EconomyDifficultyPreset);
            Assert.AreEqual(NpcWeeklyWageDifficulty.Hardcore, profile.NpcWeeklyWageDifficulty);
            Assert.AreEqual(0, profile.NpcRouteLimit);
            Assert.AreEqual(7, profile.MaxModuleLimitPerSite);
            Assert.IsTrue(profile.VehicleFuelDifficultyEnabled);
            Assert.IsTrue(profile.CargoWeightPowerDifficultyEnabled);
            Assert.IsTrue(profile.CargoDamageDifficultyEnabled);
            Assert.IsTrue(profile.IndustryPricingDifficultyEnabled);
            Assert.IsTrue(profile.LicensingDifficultyEnabled);
            Assert.IsTrue(profile.CorridorRestrictionDifficultyEnabled);
            Assert.IsTrue(profile.ReputationDifficultyEnabled);
            Assert.IsTrue(profile.OfficeGarageLimitDifficultyEnabled);
            Assert.IsTrue(profile.OfficeNpcLimitDifficultyEnabled);

            profile.ApplyBooleanBulkState(false);

            Assert.AreEqual(EconomyDifficultyPreset.Impossible, profile.EconomyDifficultyPreset);
            Assert.AreEqual(NpcWeeklyWageDifficulty.Hardcore, profile.NpcWeeklyWageDifficulty);
            Assert.AreEqual(0, profile.NpcRouteLimit);
            Assert.AreEqual(7, profile.MaxModuleLimitPerSite);
            Assert.IsFalse(profile.VehicleFuelDifficultyEnabled);
            Assert.IsFalse(profile.CargoWeightPowerDifficultyEnabled);
            Assert.IsFalse(profile.CargoDamageDifficultyEnabled);
            Assert.IsFalse(profile.IndustryPricingDifficultyEnabled);
            Assert.IsFalse(profile.LicensingDifficultyEnabled);
            Assert.IsFalse(profile.CorridorRestrictionDifficultyEnabled);
            Assert.IsFalse(profile.ReputationDifficultyEnabled);
            Assert.IsFalse(profile.OfficeGarageLimitDifficultyEnabled);
            Assert.IsFalse(profile.OfficeNpcLimitDifficultyEnabled);
        }
    }
}
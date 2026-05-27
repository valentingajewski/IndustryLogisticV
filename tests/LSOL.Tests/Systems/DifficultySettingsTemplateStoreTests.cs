using System;
using System.IO;
using System.Linq;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class DifficultySettingsTemplateStoreTests
    {
        [TestMethod]
        public void LoadTemplates_WithMissingFile_ReturnsEmptyCollection()
        {
            var filePath = TestWorkspace.CreateTempFilePath("DifficultyTemplates.xml");

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var store = new DifficultySettingsTemplateStore(filePath);

                Assert.AreEqual(0, store.LoadTemplates().Count);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveTemplate_AndLoadTemplates_RoundTripsFullDifficultyProfile()
        {
            var filePath = TestWorkspace.CreateTempFilePath("DifficultyTemplates.xml");

            try
            {
                var store = new DifficultySettingsTemplateStore(filePath);
                var profile = new DifficultySettingsProfile
                {
                    EconomyDifficultyPreset = EconomyDifficultyPreset.Hardcore,
                    NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Casual,
                    VehicleFuelDifficultyEnabled = true,
                    CargoWeightPowerDifficultyEnabled = true,
                    CargoDamageDifficultyEnabled = false,
                    IndustryPricingDifficultyEnabled = true,
                    LicensingDifficultyEnabled = true,
                    CorridorRestrictionDifficultyEnabled = false,
                    ReputationDifficultyEnabled = false,
                    OfficeGarageLimitDifficultyEnabled = false,
                    OfficeNpcLimitDifficultyEnabled = true,
                    NpcRouteLimit = 2,
                };

                var result = store.SaveTemplate("Hard Rules", profile);
                var templates = store.LoadTemplates();

                Assert.AreEqual(DifficultyTemplateSaveResult.Created, result);
                Assert.AreEqual(1, templates.Count);
                Assert.AreEqual("Hard Rules", templates[0].Name);
                Assert.AreEqual(profile, templates[0].Profile);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveTemplate_WithDuplicateName_OverwritesExistingTemplateCaseInsensitively()
        {
            var filePath = TestWorkspace.CreateTempFilePath("DifficultyTemplates.xml");

            try
            {
                var store = new DifficultySettingsTemplateStore(filePath);
                store.SaveTemplate("Rush Hour", new DifficultySettingsProfile
                {
                    EconomyDifficultyPreset = EconomyDifficultyPreset.Casual,
                    NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard,
                    VehicleFuelDifficultyEnabled = true,
                    CargoWeightPowerDifficultyEnabled = false,
                    CargoDamageDifficultyEnabled = true,
                    IndustryPricingDifficultyEnabled = false,
                    LicensingDifficultyEnabled = false,
                    CorridorRestrictionDifficultyEnabled = true,
                    ReputationDifficultyEnabled = true,
                    OfficeGarageLimitDifficultyEnabled = true,
                    OfficeNpcLimitDifficultyEnabled = false,
                    NpcRouteLimit = 4,
                });

                var result = store.SaveTemplate("rush hour", new DifficultySettingsProfile
                {
                    EconomyDifficultyPreset = EconomyDifficultyPreset.Impossible,
                    NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Hardcore,
                    VehicleFuelDifficultyEnabled = false,
                    CargoWeightPowerDifficultyEnabled = true,
                    CargoDamageDifficultyEnabled = false,
                    IndustryPricingDifficultyEnabled = true,
                    LicensingDifficultyEnabled = true,
                    CorridorRestrictionDifficultyEnabled = false,
                    ReputationDifficultyEnabled = false,
                    OfficeGarageLimitDifficultyEnabled = false,
                    OfficeNpcLimitDifficultyEnabled = true,
                    NpcRouteLimit = 1,
                });

                var templates = store.LoadTemplates();
                var template = templates.Single();

                Assert.AreEqual(DifficultyTemplateSaveResult.Overwritten, result);
                Assert.AreEqual(1, templates.Count);
                Assert.AreEqual("rush hour", template.Name);
                Assert.AreEqual(EconomyDifficultyPreset.Impossible, template.Profile.EconomyDifficultyPreset);
                Assert.AreEqual(NpcWeeklyWageDifficulty.Hardcore, template.Profile.NpcWeeklyWageDifficulty);
                Assert.IsFalse(template.Profile.VehicleFuelDifficultyEnabled);
                Assert.IsTrue(template.Profile.CargoWeightPowerDifficultyEnabled);
                Assert.IsFalse(template.Profile.CargoDamageDifficultyEnabled);
                Assert.IsTrue(template.Profile.IndustryPricingDifficultyEnabled);
                Assert.IsTrue(template.Profile.LicensingDifficultyEnabled);
                Assert.IsFalse(template.Profile.CorridorRestrictionDifficultyEnabled);
                Assert.IsFalse(template.Profile.ReputationDifficultyEnabled);
                Assert.IsFalse(template.Profile.OfficeGarageLimitDifficultyEnabled);
                Assert.IsTrue(template.Profile.OfficeNpcLimitDifficultyEnabled);
                Assert.AreEqual(1, template.Profile.NpcRouteLimit);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        private static void DeleteTempDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
using System;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class SaveProfilePreviewFormatterTests
    {
        [TestMethod]
        public void BuildDetail_WithGameplayMetadata_IncludesBalanceOfficeFleetAndDifficultySummary()
        {
            var metadata = new IndustryPersistenceMetadata
            {
                HasGameplayMetadata = true,
                Profit = 125000f,
                EconomyDifficultyPreset = EconomyDifficultyPreset.Hardcore,
                NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Casual,
                NpcRouteLimit = 3,
                DifficultySettingsLocked = true,
                VehicleFuelDifficultyEnabled = true,
                IndustryPricingDifficultyEnabled = true,
                PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "downtown",
                },
            };

            metadata.PropertyOwnership.CommercialVehicles.Add(new OwnedCommercialVehiclePersistenceEntry());

            var detail = SaveProfilePreviewFormatter.BuildDetail(
                "Load this saved game.",
                new DateTime(2026, 5, 26, 14, 30, 0),
                metadata,
                new[]
                {
                    new OfficeDefinition
                    {
                        OfficeId = "downtown",
                        SiteName = "Downtown Hub",
                    },
                });

            Assert.AreEqual(
                "Load this saved game. Last updated 2026-05-26 14:30.\nBalance $125,000.00 | Office Downtown Hub | Fleet 1\nDifficulty Hardcore preset | Wages Casual | Routes 3 | Challenges 6/9 | Locked\nFlags fuel, pricing",
                detail);
        }

        [TestMethod]
        public void BuildDetail_WithPropertySnapshotAndLegacyFleet_FallsBackToOfficeIdAndOwnedFleetCount()
        {
            var metadata = new IndustryPersistenceMetadata
            {
                PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "unknown_office",
                },
                OwnedFleet = new OwnedFleetPersistenceSnapshot(),
            };

            metadata.OwnedFleet.Vehicles.Add(new OwnedFleetVehicleSnapshot());
            metadata.OwnedFleet.Vehicles.Add(new OwnedFleetVehicleSnapshot());

            var detail = SaveProfilePreviewFormatter.BuildDetail(
                "Delete this saved game.",
                new DateTime(2026, 5, 27, 9, 5, 0),
                metadata,
                Array.Empty<OfficeDefinition>());

            Assert.AreEqual(
                "Delete this saved game. Last updated 2026-05-27 09:05.\nOffice unknown_office | Fleet 2",
                detail);
        }

        [TestMethod]
        public void BuildDetail_WithoutMetadata_PreservesHeaderOnly()
        {
            var detail = SaveProfilePreviewFormatter.BuildDetail(
                "Currently active save. Load this saved game.",
                new DateTime(2026, 5, 28, 21, 45, 0),
                null,
                Array.Empty<OfficeDefinition>());

            Assert.AreEqual(
                "Currently active save. Load this saved game. Last updated 2026-05-28 21:45.",
                detail);
        }
    }
}
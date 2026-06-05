using System;
using System.IO;
using System.Linq;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class IndustryPersistenceManagerPropertyOwnershipTests
    {
        [TestMethod]
        public void SaveAndLoad_WithOfficeCommercialVehicleEntitlementFlags_RestoresPropertyOwnershipSnapshot()
        {
            var filePath = TestWorkspace.CreateTempFilePath("property-office-commercial-entitlements.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                    {
                        ActiveOfficeId = "main",
                        Offices =
                        {
                            new OfficeOwnershipPersistenceEntry
                            {
                                OfficeId = "main",
                                IsOwned = true,
                                LastChargedWeekIndex = 2,
                                HasConsumedFreeMixerFamilyCommercialVehicle = true,
                            },
                            new OfficeOwnershipPersistenceEntry
                            {
                                OfficeId = "branch",
                                IsOwned = true,
                                LastChargedWeekIndex = 4,
                                HasConsumedFreeTiptruckFamilyCommercialVehicle = true,
                            },
                        },
                    },
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata);

                var rawSave = File.ReadAllText(filePath);
                var loaded = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), null);
                var offices = loaded.Metadata.PropertyOwnership.Offices.ToDictionary(entry => entry.OfficeId, StringComparer.OrdinalIgnoreCase);

                StringAssert.Contains(rawSave, "<Value key=\"HasConsumedFreeMixerFamilyCommercialVehicle\">true</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"HasConsumedFreeTiptruckFamilyCommercialVehicle\">true</Value>");
                Assert.IsTrue(offices["main"].HasConsumedFreeMixerFamilyCommercialVehicle);
                Assert.IsFalse(offices["main"].HasConsumedFreeTiptruckFamilyCommercialVehicle);
                Assert.IsFalse(offices["branch"].HasConsumedFreeMixerFamilyCommercialVehicle);
                Assert.IsTrue(offices["branch"].HasConsumedFreeTiptruckFamilyCommercialVehicle);
            }
            finally
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithCommercialVehicleAssetsAndPairLinks_RestoresIndependentFleetState()
        {
            var filePath = TestWorkspace.CreateTempFilePath("property-commercial-assets.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                    {
                        ActiveOfficeId = "main",
                        Offices =
                        {
                            new OfficeOwnershipPersistenceEntry
                            {
                                OfficeId = "main",
                                IsOwned = true,
                                LastChargedWeekIndex = 2,
                            },
                        },
                        CommercialVehicleAssets =
                        {
                            new OwnedCommercialVehicleAssetPersistenceEntry
                            {
                                AssetId = "tractor-1",
                                DisplayName = "Lead Tractor",
                                ModelName = "phantom",
                                FleetRole = CommercialVehicleFleetRole.Tractor,
                                AssignedOfficeId = "main",
                                PurchasePrice = 90000f,
                            },
                            new OwnedCommercialVehicleAssetPersistenceEntry
                            {
                                AssetId = "trailer-1",
                                DisplayName = "Container Trailer",
                                ModelName = "trailers4",
                                FleetRole = CommercialVehicleFleetRole.Trailer,
                                AssignedOfficeId = "main",
                                PurchasePrice = 30000f,
                                CapacityTons = 24f,
                            },
                        },
                        CommercialVehicles =
                        {
                            new OwnedCommercialVehiclePersistenceEntry
                            {
                                AssetId = "slot-1",
                                DisplayName = "Lead Tractor + Container Trailer",
                                TractorVehicleId = "tractor-1",
                                TrailerVehicleId = "trailer-1",
                                PoweredModelName = "phantom",
                                CargoModelName = "trailers4",
                                HasSeparateCargoVehicle = true,
                                AssignedOfficeId = "main",
                                InActiveGarage = true,
                            },
                        },
                    },
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata);

                var rawSave = File.ReadAllText(filePath);
                var loaded = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), null);
                var property = loaded.Metadata.PropertyOwnership;
                var slot = property.CommercialVehicles.Single(entry => entry.AssetId == "slot-1");
                var assets = property.CommercialVehicleAssets.ToDictionary(entry => entry.AssetId, StringComparer.OrdinalIgnoreCase);

                StringAssert.Contains(rawSave, "PropertyCommercialAsset:tractor-1");
                StringAssert.Contains(rawSave, "PropertyCommercialAsset:trailer-1");
                StringAssert.Contains(rawSave, "<Value key=\"TractorVehicleId\">tractor-1</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"TrailerVehicleId\">trailer-1</Value>");
                Assert.AreEqual("tractor-1", slot.TractorVehicleId);
                Assert.AreEqual("trailer-1", slot.TrailerVehicleId);
                Assert.AreEqual(CommercialVehicleFleetRole.Tractor, assets["tractor-1"].FleetRole);
                Assert.AreEqual(CommercialVehicleFleetRole.Trailer, assets["trailer-1"].FleetRole);
                Assert.AreEqual(24f, assets["trailer-1"].CapacityTons, 0.01f);
            }
            finally
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
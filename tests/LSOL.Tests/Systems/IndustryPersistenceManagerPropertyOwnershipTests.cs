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
    }
}
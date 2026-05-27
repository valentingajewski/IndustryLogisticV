using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PropertyManagerCommercialVehicleResaleTests
    {
        [TestMethod]
        public void GetCommercialVehicleSalePreview_FullConditionAndNoMaintenance_ReturnsBaseRefund()
        {
            var manager = CreatePropertyManager();
            AddVehicles(manager, CreateOwnedVehicle("truck-a", 100000f, 1f, 0f));

            var preview = manager.GetCommercialVehicleSalePreview("truck-a");

            Assert.AreEqual(100000f, preview.PurchasePrice, 0.01f);
            Assert.AreEqual(50000f, preview.BaseRefund, 0.01f);
            Assert.AreEqual(100f, preview.MaintenanceConditionPercent, 0.01f);
            Assert.AreEqual(0f, preview.DepreciationPenaltyPercent, 0.01f);
            Assert.AreEqual(50000f, preview.EstimatedResaleValue, 0.01f);
        }

        [TestMethod]
        public void GetCommercialVehicleSalePreview_PoorCondition_LowersEstimatedResale()
        {
            var manager = CreatePropertyManager();
            AddVehicles(
                manager,
                CreateOwnedVehicle("truck-good", 100000f, 1f, 0f),
                CreateOwnedVehicle("truck-worn", 100000f, 0.5f, 0f));

            var good = manager.GetCommercialVehicleSalePreview("truck-good");
            var worn = manager.GetCommercialVehicleSalePreview("truck-worn");

            Assert.IsTrue(worn.EstimatedResaleValue < good.EstimatedResaleValue);
            Assert.AreEqual(43750f, worn.EstimatedResaleValue, 0.01f);
        }

        [TestMethod]
        public void GetCommercialVehicleSalePreview_HigherLifetimeMaintenance_LowersEstimatedResale()
        {
            var manager = CreatePropertyManager();
            AddVehicles(
                manager,
                CreateOwnedVehicle("truck-low", 100000f, 1f, 1000f),
                CreateOwnedVehicle("truck-high", 100000f, 1f, 12000f));

            var low = manager.GetCommercialVehicleSalePreview("truck-low");
            var high = manager.GetCommercialVehicleSalePreview("truck-high");

            Assert.IsTrue(high.DepreciationPenaltyPercent > low.DepreciationPenaltyPercent);
            Assert.IsTrue(high.EstimatedResaleValue < low.EstimatedResaleValue);
        }

        [TestMethod]
        public void GetCommercialVehicleSalePreview_DepreciationPenaltyCapsAtTwentyPercent()
        {
            var manager = CreatePropertyManager();
            AddVehicles(manager, CreateOwnedVehicle("truck-cap", 100000f, 1f, 500000f));

            var preview = manager.GetCommercialVehicleSalePreview("truck-cap");

            Assert.AreEqual(20f, preview.DepreciationPenaltyPercent, 0.01f);
            Assert.AreEqual(40000f, preview.EstimatedResaleValue, 0.01f);
        }

        [TestMethod]
        public void TrySellCommercialVehicle_UsesSamePreviewValueAsPayout()
        {
            var manager = CreatePropertyManager();
            AddVehicles(manager, CreateOwnedVehicle("truck-sell", 80000f, 0.8f, 5000f));
            var startingBalance = 0f;

            var preview = manager.GetCommercialVehicleSalePreview("truck-sell");
            Assert.IsNotNull(preview);

            Assert.IsTrue(manager.TrySellCommercialVehicle("truck-sell", null, null, ref startingBalance, out _));
            Assert.AreEqual(preview.EstimatedResaleValue, startingBalance, 0.01f);
            Assert.AreEqual(0, manager.CommercialVehicles.Count(entry => string.Equals(entry.AssetId, "truck-sell", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void GetFleetSaleSummary_ExcludesRentalVehicles()
        {
            var manager = CreatePropertyManager();
            AddVehicles(
                manager,
                CreateOwnedVehicle("owned-1", 100000f, 1f, 0f),
                new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "rent-1",
                    DisplayName = "Rental 1",
                    PurchasePrice = 60000f,
                    IsRental = true,
                    DailyRent = 300f,
                    InActiveGarage = true,
                    AssignedOfficeId = "alpha-office",
                });

            var summary = manager.GetFleetSaleSummary();

            Assert.AreEqual(1, summary.OwnedVehicleCount);
            Assert.AreEqual(100000f, summary.TotalPurchaseBasis, 0.01f);
            Assert.AreEqual(50000f, summary.TotalEstimatedResaleValue, 0.01f);
            Assert.AreEqual(50000f, summary.TotalDepreciationLoss, 0.01f);
            Assert.AreEqual("owned-1", summary.WorstVehicleName);
        }

        private static PropertyManager CreatePropertyManager()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new List<OfficeDefinition>
            {
                new OfficeDefinition
                {
                    OfficeId = "alpha-office",
                    SiteName = "Alpha Office",
                    OfficePrice = 1000f,
                    WeeklyOfficeRent = 100f,
                    MaxCommercialVehicles = 5,
                },
            });

            return new PropertyManager(config);
        }

        private static OwnedCommercialVehiclePersistenceEntry CreateOwnedVehicle(string assetId, float purchasePrice, float maintenanceCondition, float lifetimeMaintenanceCost)
        {
            return new OwnedCommercialVehiclePersistenceEntry
            {
                AssetId = assetId,
                DisplayName = assetId,
                PurchasePrice = purchasePrice,
                MaintenanceCondition = maintenanceCondition,
                LifetimeMaintenanceCost = lifetimeMaintenanceCost,
                InActiveGarage = true,
                AssignedOfficeId = "alpha-office",
            };
        }

        private static void AddVehicles(PropertyManager manager, params OwnedCommercialVehiclePersistenceEntry[] vehicles)
        {
            var snapshot = new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha-office",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry
                    {
                        OfficeId = "alpha-office",
                        IsOwned = true,
                        LastChargedWeekIndex = -1,
                    },
                },
            };

            foreach (var vehicle in vehicles.Where(entry => entry != null))
            {
                snapshot.CommercialVehicles.Add(vehicle);
            }

            manager.ApplySnapshot(snapshot, 0);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}

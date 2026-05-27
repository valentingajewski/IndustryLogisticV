using System.Collections.Generic;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PropertyManagerCommercialVehiclePurchaseTests
    {
        [TestMethod]
        public void ApplySnapshotAndCreateSnapshot_PreservesOfficeCommercialFreeFlags()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 1));
            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "alpha",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry
                        {
                            OfficeId = "alpha",
                            IsOwned = true,
                            LastChargedWeekIndex = 2,
                            HasConsumedFreeMixerFamilyCommercialVehicle = true,
                            HasConsumedFreeTiptruckFamilyCommercialVehicle = true,
                        },
                    },
                },
                0);

            var snapshot = manager.CreateSnapshot(null, null);
            var office = snapshot.Offices[0];

            Assert.IsTrue(office.HasConsumedFreeMixerFamilyCommercialVehicle);
            Assert.IsTrue(office.HasConsumedFreeTiptruckFamilyCommercialVehicle);
        }

        [TestMethod]
        public void TryPurchaseCommercialVehicle_MixerEntitlementIsOneTimePerOffice()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 1), CreateOffice("beta", 1));
            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "alpha",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                        new OfficeOwnershipPersistenceEntry { OfficeId = "beta", IsOwned = true, LastChargedWeekIndex = 0 },
                    },
                },
                0);

            var mixer = CreateCommercialVehicle("mixer", "Mixer", 20000f, 0f);
            var mixer2 = CreateCommercialVehicle("mixer2", "Mixer", 20000f, 0f);

            var firstQuote = manager.GetCommercialVehiclePurchaseQuote(mixer, null);
            Assert.IsNotNull(firstQuote);
            Assert.IsTrue(firstQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(CommercialVehiclePurchaseEntitlementFamily.Mixer, firstQuote.EntitlementFamily);
            Assert.AreEqual(20000f, firstQuote.StandardPrice, 0.01f);
            Assert.AreEqual(0f, firstQuote.EffectivePrice, 0.01f);

            var balance = 50000f;
            OwnedCommercialVehiclePersistenceEntry purchasedVehicle;
            string message;
            Assert.IsTrue(manager.TryPurchaseCommercialVehicle(mixer, null, ref balance, out purchasedVehicle, out message));
            Assert.AreEqual(50000f, balance, 0.01f);
            Assert.AreEqual(0f, purchasedVehicle.PurchasePrice, 0.01f);
            Assert.IsTrue(manager.GetOfficeState("alpha").HasConsumedFreeMixerFamilyCommercialVehicle);
            StringAssert.Contains(message, "free");

            var secondQuote = manager.GetCommercialVehiclePurchaseQuote(mixer2, null);
            Assert.IsFalse(secondQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(20000f, secondQuote.EffectivePrice, 0.01f);

            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "beta",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry
                        {
                            OfficeId = "alpha",
                            IsOwned = true,
                            LastChargedWeekIndex = 0,
                            HasConsumedFreeMixerFamilyCommercialVehicle = true,
                        },
                        new OfficeOwnershipPersistenceEntry
                        {
                            OfficeId = "beta",
                            IsOwned = true,
                            LastChargedWeekIndex = 0,
                        },
                    },
                },
                0);

            var betaQuote = manager.GetCommercialVehiclePurchaseQuote(mixer2, null);
            Assert.IsTrue(betaQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(0f, betaQuote.EffectivePrice, 0.01f);
        }

        [TestMethod]
        public void TryPurchaseCommercialVehicle_ConsumesTiptruckEntitlementEvenWhenOfficeGarageOverflowsToReserve()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 0));
            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "alpha",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                    },
                },
                0);

            var tiptruck = CreateCommercialVehicle("tiptruck", "Tipper", 20000f, 0f);
            var balance = 1000f;
            OwnedCommercialVehiclePersistenceEntry purchasedVehicle;
            string message;

            Assert.IsTrue(manager.TryPurchaseCommercialVehicle(tiptruck, null, ref balance, out purchasedVehicle, out message));
            Assert.AreEqual(1000f, balance, 0.01f);
            Assert.IsFalse(purchasedVehicle.InActiveGarage);
            Assert.AreEqual(0f, purchasedVehicle.PurchasePrice, 0.01f);
            Assert.IsTrue(manager.GetOfficeState("alpha").HasConsumedFreeTiptruckFamilyCommercialVehicle);
            StringAssert.Contains(message, "reserve");

            var nextQuote = manager.GetCommercialVehiclePurchaseQuote(tiptruck, null);
            Assert.IsFalse(nextQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(20000f, nextQuote.EffectivePrice, 0.01f);
        }

        [TestMethod]
        public void TryRentCommercialVehicle_DoesNotConsumeTiptruckEntitlement()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 1));
            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "alpha",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                    },
                },
                0);

            var tiptruck = CreateCommercialVehicle("tiptruck2", "Tipper", 20000f, 600f);
            var balance = 0f;
            OwnedCommercialVehiclePersistenceEntry rentedVehicle;
            string message;

            Assert.IsTrue(manager.TryRentCommercialVehicle(tiptruck, null, ref balance, 0, out rentedVehicle, out message));
            Assert.IsFalse(manager.GetOfficeState("alpha").HasConsumedFreeTiptruckFamilyCommercialVehicle);

            var purchaseQuote = manager.GetCommercialVehiclePurchaseQuote(tiptruck, null);
            Assert.IsTrue(purchaseQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(0f, purchaseQuote.EffectivePrice, 0.01f);
        }

        private static PropertyManager CreatePropertyManager(params OfficeDefinition[] offices)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new List<OfficeDefinition>(offices));
            return new PropertyManager(config);
        }

        private static OfficeDefinition CreateOffice(string officeId, int maxCommercialVehicles)
        {
            return new OfficeDefinition
            {
                OfficeId = officeId,
                SiteName = officeId,
                OfficePrice = 1000f,
                WeeklyOfficeRent = 100f,
                MaxCommercialVehicles = maxCommercialVehicles,
            };
        }

        private static VehicleDefinition CreateCommercialVehicle(string modelName, string displayName, float price, float dailyRent)
        {
            return new VehicleDefinition
            {
                Id = modelName,
                DisplayName = displayName,
                ModelName = modelName,
                Price = price,
                DailyRent = dailyRent,
                CapacityTons = 20f,
                FuelCapacityLiters = 450f,
                CargoType = VehicleCargoType.Unknown,
                IsTrailer = false,
                IsTractor = false,
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
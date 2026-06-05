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
            Assert.AreEqual(CommercialVehiclePurchaseEntitlementFamily.Tiptruck, nextQuote.EntitlementFamily);
            Assert.AreEqual(20000f, nextQuote.EffectivePrice, 0.01f);
        }

        [TestMethod]
        public void GetCommercialVehiclePurchaseQuote_Tiptruck2NeverUsesFirstFreeEntitlement()
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

            var tiptruck2 = CreateCommercialVehicle("tiptruck2", "Tipper", 24000f, 0f);

            var quote = manager.GetCommercialVehiclePurchaseQuote(tiptruck2, null);

            Assert.IsNotNull(quote);
            Assert.IsFalse(quote.UsesFirstFreeEntitlement);
            Assert.AreEqual(CommercialVehiclePurchaseEntitlementFamily.None, quote.EntitlementFamily);
            Assert.AreEqual(24000f, quote.StandardPrice, 0.01f);
            Assert.AreEqual(24000f, quote.EffectivePrice, 0.01f);
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

            var tiptruck = CreateCommercialVehicle("tiptruck", "Tipper", 20000f, 600f);
            var balance = 0f;
            OwnedCommercialVehiclePersistenceEntry rentedVehicle;
            string message;

            Assert.IsTrue(manager.TryRentCommercialVehicle(tiptruck, null, ref balance, 0, out rentedVehicle, out message));
            Assert.IsFalse(manager.GetOfficeState("alpha").HasConsumedFreeTiptruckFamilyCommercialVehicle);

            var purchaseQuote = manager.GetCommercialVehiclePurchaseQuote(tiptruck, null);
            Assert.IsTrue(purchaseQuote.UsesFirstFreeEntitlement);
            Assert.AreEqual(CommercialVehiclePurchaseEntitlementFamily.Tiptruck, purchaseQuote.EntitlementFamily);
            Assert.AreEqual(0f, purchaseQuote.EffectivePrice, 0.01f);
        }

        [TestMethod]
        public void TryPurchaseCommercialVehicle_TractorTrailerSelectionCreatesLinkedAssetsAndFleetSlot()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 2));
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

            var tractor = CreateCommercialVehicle("phantom", "Phantom Tractor", 90000f, 0f, CommercialVehicleFleetRole.Tractor);
            var trailer = CreateCommercialVehicle("trailers4", "Container Trailer", 30000f, 0f, CommercialVehicleFleetRole.Trailer);
            var balance = 200000f;

            OwnedCommercialVehiclePersistenceEntry slot;
            string message;
            Assert.IsTrue(manager.TryPurchaseCommercialVehicle(trailer, tractor, ref balance, out slot, out message));

            Assert.IsFalse(string.IsNullOrWhiteSpace(slot.TractorVehicleId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(slot.TrailerVehicleId));
            Assert.AreEqual(2, manager.CommercialVehicleAssets.Count);
            Assert.IsTrue(slot.HasSeparateCargoVehicle);
            Assert.AreEqual("Phantom Tractor + Container Trailer", slot.DisplayName);

            var poweredAsset = manager.GetCommercialVehicleAssetRecord(slot.TractorVehicleId);
            var trailerAsset = manager.GetCommercialVehicleAssetRecord(slot.TrailerVehicleId);
            Assert.IsNotNull(poweredAsset);
            Assert.IsNotNull(trailerAsset);
            Assert.AreEqual(CommercialVehicleFleetRole.Tractor, poweredAsset.FleetRole);
            Assert.AreEqual(CommercialVehicleFleetRole.Trailer, trailerAsset.FleetRole);
            StringAssert.Contains(message, "Purchased");
        }

        [TestMethod]
        public void TryPurchaseCommercialVehicle_TrailerOnlyCreatesTrailerPoolSlot()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 2));
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

            var trailer = CreateCommercialVehicle("trailers2", "Refrigerated Trailer", 26000f, 0f, CommercialVehicleFleetRole.Trailer);
            var balance = 50000f;

            OwnedCommercialVehiclePersistenceEntry slot;
            string message;
            Assert.IsTrue(manager.TryPurchaseCommercialVehicle(trailer, null, ref balance, out slot, out message));

            Assert.IsTrue(string.IsNullOrWhiteSpace(slot.TractorVehicleId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(slot.TrailerVehicleId));
            Assert.AreEqual(1, manager.CommercialVehicleAssets.Count);
            Assert.AreEqual("Refrigerated Trailer", slot.DisplayName);
            Assert.IsFalse(slot.HasSeparateCargoVehicle);
        }

        [TestMethod]
        public void TryAssignCommercialVehicleTrailerAsset_TractorOnlySlotPairsTrailerFromPool()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            var snapshot = CreateOwnedOfficeSnapshot();
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("tractor-1", "Main Tractor", "phantom", CommercialVehicleFleetRole.Tractor));
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("trailer-1", "Pool Trailer", "trailers4", CommercialVehicleFleetRole.Trailer, 24f));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-tractor", "Main Tractor", "tractor-1", null, "phantom", "phantom", false, true));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-trailer", "Pool Trailer", null, "trailer-1", "trailers4", "trailers4", false, false));
            manager.ApplySnapshot(snapshot, 0);

            string message;
            Assert.IsTrue(manager.TryAssignCommercialVehicleTrailerAsset("slot-tractor", "trailer-1", out message));

            var slot = manager.CommercialVehicles.Single(entry => entry.AssetId == "slot-tractor");
            Assert.AreEqual("trailer-1", slot.TrailerVehicleId);
            Assert.AreEqual(1, manager.CommercialVehicles.Count);
            Assert.AreEqual("Main Tractor + Pool Trailer", slot.DisplayName);
            Assert.AreEqual(0, manager.GetAvailableCommercialTrailerAssets().Count());
            StringAssert.Contains(message, "Assigned Pool Trailer");
        }

        [TestMethod]
        public void TryAssignCommercialVehiclePoweredAsset_TrailerOnlySlotPairsTractorFromPool()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            var snapshot = CreateOwnedOfficeSnapshot();
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("tractor-1", "Pool Tractor", "packer", CommercialVehicleFleetRole.Tractor));
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("trailer-1", "Main Trailer", "trailers4", CommercialVehicleFleetRole.Trailer, 24f));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-tractor", "Pool Tractor", "tractor-1", null, "packer", "packer", false, false));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-trailer", "Main Trailer", null, "trailer-1", "trailers4", "trailers4", false, true));
            manager.ApplySnapshot(snapshot, 0);

            string message;
            Assert.IsTrue(manager.TryAssignCommercialVehiclePoweredAsset("slot-trailer", "tractor-1", out message));

            var slot = manager.CommercialVehicles.Single(entry => entry.AssetId == "slot-trailer");
            Assert.AreEqual("tractor-1", slot.TractorVehicleId);
            Assert.AreEqual(1, manager.CommercialVehicles.Count);
            Assert.AreEqual("Pool Tractor + Main Trailer", slot.DisplayName);
            Assert.AreEqual(0, manager.GetAvailableCommercialPoweredAssets().Count());
            StringAssert.Contains(message, "Assigned Pool Tractor");
        }

        [TestMethod]
        public void TryClearCommercialVehicleTrailerAsset_CoupledSlotCreatesTrailerPoolSlot()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            var snapshot = CreateOwnedOfficeSnapshot();
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("tractor-1", "Main Tractor", "phantom", CommercialVehicleFleetRole.Tractor));
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("trailer-1", "Container Trailer", "trailers4", CommercialVehicleFleetRole.Trailer, 24f));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-coupled", "Main Tractor + Container Trailer", "tractor-1", "trailer-1", "phantom", "trailers4", true, true));
            manager.ApplySnapshot(snapshot, 0);

            string message;
            Assert.IsTrue(manager.TryClearCommercialVehicleTrailerAsset("slot-coupled", out message));

            var coupledSlot = manager.CommercialVehicles.Single(entry => entry.AssetId == "slot-coupled");
            Assert.IsTrue(string.IsNullOrWhiteSpace(coupledSlot.TrailerVehicleId));
            Assert.AreEqual(2, manager.CommercialVehicles.Count);
            Assert.IsTrue(manager.CommercialVehicles.Any(entry => entry.AssetId != "slot-coupled" && entry.TrailerVehicleId == "trailer-1" && string.IsNullOrWhiteSpace(entry.TractorVehicleId)));
            StringAssert.Contains(message, "trailer pool");
        }

        [TestMethod]
        public void TryClearCommercialVehiclePoweredAsset_CoupledSlotCreatesPoweredUnitPoolSlot()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            var snapshot = CreateOwnedOfficeSnapshot();
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("tractor-1", "Main Tractor", "phantom", CommercialVehicleFleetRole.Tractor));
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("trailer-1", "Container Trailer", "trailers4", CommercialVehicleFleetRole.Trailer, 24f));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-coupled", "Main Tractor + Container Trailer", "tractor-1", "trailer-1", "phantom", "trailers4", true, true));
            manager.ApplySnapshot(snapshot, 0);

            string message;
            Assert.IsTrue(manager.TryClearCommercialVehiclePoweredAsset("slot-coupled", out message));

            var coupledSlot = manager.CommercialVehicles.Single(entry => entry.AssetId == "slot-coupled");
            Assert.IsTrue(string.IsNullOrWhiteSpace(coupledSlot.TractorVehicleId));
            Assert.AreEqual(2, manager.CommercialVehicles.Count);
            Assert.IsTrue(manager.CommercialVehicles.Any(entry => entry.AssetId != "slot-coupled" && entry.TractorVehicleId == "tractor-1" && string.IsNullOrWhiteSpace(entry.TrailerVehicleId)));
            StringAssert.Contains(message, "powered-unit pool");
        }

        [TestMethod]
        public void TryAssignCommercialVehicleTrailerAsset_RigidSlotRejectsTrailerAssignment()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            var snapshot = CreateOwnedOfficeSnapshot();
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("rigid-1", "Rigid Truck", "mule", CommercialVehicleFleetRole.Rigid, 12f));
            snapshot.CommercialVehicleAssets.Add(CreateCommercialAsset("trailer-1", "Pool Trailer", "trailers4", CommercialVehicleFleetRole.Trailer, 24f));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-rigid", "Rigid Truck", "rigid-1", null, "mule", "mule", false, true));
            snapshot.CommercialVehicles.Add(CreateCommercialSlot("slot-trailer", "Pool Trailer", null, "trailer-1", "trailers4", "trailers4", false, false));
            manager.ApplySnapshot(snapshot, 0);

            string message;
            Assert.IsFalse(manager.TryAssignCommercialVehicleTrailerAsset("slot-rigid", "trailer-1", out message));

            Assert.AreEqual(2, manager.CommercialVehicles.Count);
            StringAssert.Contains(message, "Rigid trucks cannot tow a separate trailer");
        }

        [TestMethod]
        public void ApplySnapshot_LegacyCommercialVehicleSlotsMaterializeIndependentAssets()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", 4));
            manager.ApplySnapshot(
                new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "alpha",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                    },
                    CommercialVehicles =
                    {
                        new OwnedCommercialVehiclePersistenceEntry
                        {
                            AssetId = "legacy-rigid",
                            DisplayName = "Legacy Mule",
                            PoweredModelName = "mule",
                            CargoModelName = "mule",
                            PurchasePrice = 45000f,
                            DailyRent = 350f,
                            AssignedOfficeId = "alpha",
                            CapacityTons = 12f,
                            CargoType = VehicleCargoType.Unknown,
                            InActiveGarage = true,
                        },
                        new OwnedCommercialVehiclePersistenceEntry
                        {
                            AssetId = "legacy-coupled",
                            DisplayName = "Legacy Phantom + Trailer",
                            PoweredModelName = "phantom",
                            CargoModelName = "trailers4",
                            HasSeparateCargoVehicle = true,
                            PurchasePrice = 120000f,
                            DailyRent = 600f,
                            AssignedOfficeId = "alpha",
                            CapacityTons = 24f,
                            CargoType = VehicleCargoType.Unknown,
                            InActiveGarage = true,
                        },
                    },
                },
                0);

            Assert.AreEqual(3, manager.CommercialVehicleAssets.Count);

            var rigidSlot = manager.CommercialVehicles.Single(entry => entry.AssetId == "legacy-rigid");
            var rigidAsset = manager.GetCommercialVehicleAssetRecord(rigidSlot.TractorVehicleId);
            Assert.IsNotNull(rigidAsset);
            Assert.AreEqual(CommercialVehicleFleetRole.Rigid, rigidAsset.FleetRole);
            Assert.AreEqual(12f, rigidAsset.CapacityTons, 0.01f);
            Assert.IsTrue(string.IsNullOrWhiteSpace(rigidSlot.TrailerVehicleId));

            var coupledSlot = manager.CommercialVehicles.Single(entry => entry.AssetId == "legacy-coupled");
            var tractorAsset = manager.GetCommercialVehicleAssetRecord(coupledSlot.TractorVehicleId);
            var trailerAsset = manager.GetCommercialVehicleAssetRecord(coupledSlot.TrailerVehicleId);
            Assert.IsNotNull(tractorAsset);
            Assert.IsNotNull(trailerAsset);
            Assert.AreEqual(CommercialVehicleFleetRole.Tractor, tractorAsset.FleetRole);
            Assert.AreEqual(CommercialVehicleFleetRole.Trailer, trailerAsset.FleetRole);
            Assert.AreEqual(0f, tractorAsset.CapacityTons, 0.01f);
            Assert.AreEqual(24f, trailerAsset.CapacityTons, 0.01f);
        }

        private static PropertyManager CreatePropertyManager(params OfficeDefinition[] offices)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new List<OfficeDefinition>(offices));
            return new PropertyManager(config);
        }

        private static PropertyOwnershipPersistenceSnapshot CreateOwnedOfficeSnapshot()
        {
            return new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            };
        }

        private static OwnedCommercialVehicleAssetPersistenceEntry CreateCommercialAsset(
            string assetId,
            string displayName,
            string modelName,
            CommercialVehicleFleetRole fleetRole,
            float capacityTons = 0f)
        {
            return new OwnedCommercialVehicleAssetPersistenceEntry
            {
                AssetId = assetId,
                DisplayName = displayName,
                ModelName = modelName,
                FleetRole = fleetRole,
                AssignedOfficeId = "alpha",
                PurchasePrice = 10000f,
                CapacityTons = capacityTons,
            };
        }

        private static OwnedCommercialVehiclePersistenceEntry CreateCommercialSlot(
            string slotId,
            string displayName,
            string tractorVehicleId,
            string trailerVehicleId,
            string poweredModelName,
            string cargoModelName,
            bool hasSeparateCargoVehicle,
            bool inActiveGarage)
        {
            return new OwnedCommercialVehiclePersistenceEntry
            {
                AssetId = slotId,
                DisplayName = displayName,
                TractorVehicleId = tractorVehicleId,
                TrailerVehicleId = trailerVehicleId,
                PoweredModelName = poweredModelName,
                CargoModelName = cargoModelName,
                HasSeparateCargoVehicle = hasSeparateCargoVehicle,
                AssignedOfficeId = "alpha",
                InActiveGarage = inActiveGarage,
            };
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

        private static VehicleDefinition CreateCommercialVehicle(string modelName, string displayName, float price, float dailyRent, CommercialVehicleFleetRole fleetRole = CommercialVehicleFleetRole.Rigid)
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
                FleetRole = fleetRole,
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
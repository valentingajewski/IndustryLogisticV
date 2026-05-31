using System.Linq;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PropertyManagerOfficeRentalTests
    {
        private const int MinutesPerWeek = 7 * 24 * 60;

        [TestMethod]
        public void TryRentOffice_WhenAnotherRentalExists_RequiresExplicitTransfer()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 2),
                CreateOffice("bravo", "Bravo Yard", 200f, 3));
            var balance = 1000f;

            Assert.IsTrue(manager.TryRentOffice("alpha", ref balance, 0, out _));
            Assert.IsFalse(manager.TryRentOffice("bravo", ref balance, 0, out var message));

            StringAssert.Contains(message, "transfer");
        }

        [TestMethod]
        public void TryTransferOfficeRental_RelinquishesPreviousRentalAndOnlyBillsDestinationOffice()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 2),
                CreateOffice("bravo", "Bravo Yard", 200f, 3));
            var balance = 1000f;

            Assert.IsTrue(manager.TryRentOffice("alpha", ref balance, 0, out _));
            Assert.IsTrue(manager.TryTransferOfficeRental("bravo", ref balance, 0, out _));

            Assert.AreEqual("bravo", manager.ActiveOfficeId);
            Assert.IsFalse(manager.GetOfficeState("alpha").IsRented);
            Assert.IsTrue(manager.GetOfficeState("bravo").IsRented);

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(500f, balance, 0.01f);
            Assert.AreEqual(0f, manager.GetOfficeState("alpha").OutstandingRent, 0.01f);
            Assert.IsTrue(messages.Any(message => message.Contains("Bravo Yard")));
            Assert.IsFalse(messages.Any(message => message.Contains("Alpha Yard")));
        }

        [TestMethod]
        public void TryPayOfficeArrears_RestoresSuspendedRental()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", "Alpha Yard", 100f, 2));
            var balance = 100f;

            Assert.IsTrue(manager.TryRentOffice("alpha", ref balance, 0, out _));
            manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            var state = manager.GetOfficeState("alpha");
            Assert.IsNotNull(state);
            Assert.IsTrue(state.IsAccessSuspended);
            Assert.AreEqual(100f, state.OutstandingRent, 0.01f);

            balance = 100f;
            Assert.IsTrue(manager.TryPayOfficeArrears("alpha", ref balance, out _));
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.IsTrue(manager.CanUseCommercialSystems(out _));
        }

        [TestMethod]
        public void TryPurchaseOffice_OwnedAccessDoesNotGenerateWeeklyOfficeRent()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", "Alpha Yard", 100f, 2));
            var balance = 5000f;

            Assert.IsTrue(manager.TryPurchaseOffice("alpha", ref balance, 0, out _));

            var state = manager.GetOfficeState("alpha");
            Assert.IsNotNull(state);
            Assert.AreEqual("alpha", manager.ActiveOfficeId);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
            Assert.AreEqual(4000f, balance, 0.01f);

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(4000f, balance, 0.01f);
            Assert.AreEqual(0, messages.Count);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
        }

        [TestMethod]
        public void TryPurchaseOffice_WhenOfficeIsRented_ConvertsRentalToOwnedAndStopsFutureRent()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", "Alpha Yard", 100f, 2));
            var balance = 2500f;

            Assert.IsTrue(manager.TryRentOffice("alpha", ref balance, 0, out _));
            Assert.IsTrue(manager.TryPurchaseOffice("alpha", ref balance, 0, out var message));

            var state = manager.GetOfficeState("alpha");
            Assert.IsNotNull(state);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
            Assert.AreEqual(1400f, balance, 0.01f);
            StringAssert.Contains(message, "converted to owned access");

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(1400f, balance, 0.01f);
            Assert.AreEqual(0, messages.Count);
        }

        [TestMethod]
        public void TryPurchaseOffice_WhenAnotherOfficeIsRented_RelinquishesPreviousRentalAndStopsPreviousOfficeBilling()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 1),
                CreateOffice("bravo", "Bravo Yard", 200f, 2));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                CommercialVehicles =
                {
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "truck-1",
                        DisplayName = "Truck 1",
                        AssignedOfficeId = "alpha",
                        IsRental = true,
                        InActiveGarage = true,
                    },
                },
            }, 0);
            var balance = 4000f;

            Assert.IsTrue(manager.TryRentOffice("alpha", ref balance, 0, out _));
            Assert.IsTrue(manager.TryPurchaseOffice("bravo", ref balance, 0, out _));

            var alphaState = manager.GetOfficeState("alpha");
            var bravoState = manager.GetOfficeState("bravo");

            Assert.IsNotNull(alphaState);
            Assert.IsNotNull(bravoState);
            Assert.IsFalse(alphaState.IsOwned);
            Assert.IsFalse(alphaState.IsRented);
            Assert.AreEqual(0f, alphaState.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, alphaState.LastChargedWeekIndex);
            Assert.IsTrue(bravoState.IsOwned);
            Assert.IsFalse(bravoState.IsRented);
            Assert.AreEqual("bravo", manager.ActiveOfficeId);
            Assert.AreEqual("bravo", manager.CommercialVehicles.Single().AssignedOfficeId);
            Assert.AreEqual(1900f, balance, 0.01f);

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(1900f, balance, 0.01f);
            Assert.IsFalse(messages.Any(message => message.Contains("Alpha Yard")));
            Assert.IsFalse(messages.Any(message => message.Contains("Bravo Yard")));
        }

        [TestMethod]
        public void TryActivateOffice_RelinquishesPreviousRentalAndTransfersCommercialVehicles()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 1),
                CreateOffice("bravo", "Bravo Yard", 150f, 2));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsRented = true, LastChargedWeekIndex = 0 },
                    new OfficeOwnershipPersistenceEntry { OfficeId = "bravo", IsOwned = true, LastChargedWeekIndex = 0 },
                },
                CommercialVehicles =
                {
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "truck-1",
                        DisplayName = "Truck 1",
                        AssignedOfficeId = "alpha",
                        InActiveGarage = true,
                    },
                },
            }, 0);

            Assert.IsTrue(manager.TryActivateOffice("bravo", out _));

            Assert.AreEqual("bravo", manager.ActiveOfficeId);
            Assert.IsFalse(manager.GetOfficeState("alpha").IsRented);
            Assert.AreEqual("bravo", manager.CommercialVehicles.Single().AssignedOfficeId);
        }

        [TestMethod]
        public void TryRelinquishOfficeRental_UsesOwnedFallbackOffice()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 1),
                CreateOffice("bravo", "Bravo Yard", 150f, 2));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsRented = true, LastChargedWeekIndex = 0 },
                    new OfficeOwnershipPersistenceEntry { OfficeId = "bravo", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            Assert.IsTrue(manager.TryRelinquishOfficeRental("alpha", out _));

            Assert.AreEqual("bravo", manager.ActiveOfficeId);
            Assert.IsFalse(manager.GetOfficeState("alpha").IsRented);
        }

        [TestMethod]
        public void ApplySnapshot_WithLegacyMultipleRentals_KeepsOnlyActiveRental()
        {
            var manager = CreatePropertyManager(
                CreateOffice("alpha", "Alpha Yard", 100f, 1),
                CreateOffice("bravo", "Bravo Yard", 150f, 2));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "bravo",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry
                    {
                        OfficeId = "alpha",
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 275f,
                        LastChargedWeekIndex = 3,
                    },
                    new OfficeOwnershipPersistenceEntry
                    {
                        OfficeId = "bravo",
                        IsRented = true,
                        LastChargedWeekIndex = 3,
                    },
                },
            }, 0);

            var alphaState = manager.GetOfficeState("alpha");
            var bravoState = manager.GetOfficeState("bravo");

            Assert.IsNotNull(alphaState);
            Assert.IsNotNull(bravoState);
            Assert.AreEqual("bravo", manager.ActiveOfficeId);
            Assert.IsFalse(alphaState.IsRented);
            Assert.IsFalse(alphaState.IsAccessSuspended);
            Assert.AreEqual(0f, alphaState.OutstandingRent, 0.01f);
            Assert.IsTrue(bravoState.IsRented);
        }

        [TestMethod]
        public void ApplySnapshot_WithOwnedOfficeLegacyRentState_ClearsBogusOwnedOfficeArrears()
        {
            var manager = CreatePropertyManager(CreateOffice("alpha", "Alpha Yard", 100f, 2));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry
                    {
                        OfficeId = "alpha",
                        IsOwned = true,
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 275f,
                        LastChargedWeekIndex = 3,
                    },
                },
            }, 0);

            var state = manager.GetOfficeState("alpha");

            Assert.IsNotNull(state);
            Assert.AreEqual("alpha", manager.ActiveOfficeId);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);

            var balance = 0f;
            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(0f, balance, 0.01f);
            Assert.AreEqual(0, messages.Count);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
        }

        [TestMethod]
        public void TryPurchaseOfficeObject_RoomOnlyFacilityWithoutAnchor_IsBlocked()
        {
            var manager = CreatePropertyManager(
                new[] { CreateOffice("alpha", "Alpha Yard", 100f, 2) },
                new[]
                {
                    new OfficeObjectDefinition
                    {
                        ObjectId = 77,
                        DisplayName = "Boardroom Annex",
                        Function = OfficeObjectFunction.Headquarters,
                        RequiresOwnedOffice = true,
                        PlacementContext = OfficeObjectPlacementContext.Room,
                        AnchorType = OfficeFacilityAnchorType.Boardroom,
                        Price = 25000f,
                    },
                });
            var balance = 100000f;

            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            OfficeObjectPersistenceEntry purchasedEntry;
            string message;

            Assert.IsFalse(manager.TryPurchaseOfficeObject("alpha", 77, ref balance, out purchasedEntry, out message));
            StringAssert.Contains(message, "boardroom");
        }

        [TestMethod]
        public void TryPlaceOfficeObject_WithAssignedFacilityAnchor_PersistsAnchorId()
        {
            var manager = CreatePropertyManager(
                new[]
                {
                    CreateOffice("alpha", "Alpha Yard", 100f, 2, new OfficeFacilityAnchorDefinition
                    {
                        AnchorId = "dispatch-main",
                        AnchorType = OfficeFacilityAnchorType.DispatchDesk,
                        Label = "Dispatch Desk",
                        Position = new Vector3(1f, 2f, 3f),
                        Heading = 90f,
                    }),
                },
                new[]
                {
                    new OfficeObjectDefinition
                    {
                        ObjectId = 15,
                        DisplayName = "Dispatch Desk",
                        Function = OfficeObjectFunction.Npc,
                        PlacementContext = OfficeObjectPlacementContext.Room,
                        AnchorType = OfficeFacilityAnchorType.DispatchDesk,
                        Price = 5000f,
                    },
                });
            var balance = 20000f;

            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            OfficeObjectPersistenceEntry purchasedEntry;
            Assert.IsTrue(manager.TryPurchaseOfficeObject("alpha", 15, ref balance, out purchasedEntry, out _));

            OfficeObjectPersistenceEntry placedEntry;
            Assert.IsTrue(manager.TryPlaceOfficeObject(purchasedEntry.InstanceId, new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, 90f), "dispatch-main", out placedEntry, out _));
            Assert.AreEqual("dispatch-main", placedEntry.AssignedFacilityAnchorId);
        }

        [TestMethod]
        public void PlacedConstructionSiteCabin_ContributesNpcCapacity()
        {
            var manager = CreatePropertyManager(
                new[]
                {
                    CreateOffice("alpha", "Alpha Yard", 100f, 2),
                },
                new[]
                {
                    new OfficeObjectDefinition
                    {
                        ObjectId = 2,
                        DisplayName = "Construction Site Cabin",
                        Function = OfficeObjectFunction.Npc,
                        Capacity = 3f,
                        InteractionType = OfficeFacilityInteractionType.HireNpc,
                        PerOfficeLimit = 4,
                        Price = 25000f,
                    },
                });
            var balance = 50000f;

            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            OfficeObjectPersistenceEntry purchasedEntry;
            Assert.IsTrue(manager.TryPurchaseOfficeObject("alpha", 2, ref balance, out purchasedEntry, out _));

            OfficeObjectPersistenceEntry placedEntry;
            Assert.IsTrue(manager.TryPlaceOfficeObject(purchasedEntry.InstanceId, new Vector3(1f, 2f, 3f), Vector3.Zero, out placedEntry, out _));
            Assert.AreEqual(3f, manager.GetOfficeObjectFunctionCapacity("alpha", OfficeObjectFunction.Npc), 0.01f);
        }

        [TestMethod]
        public void ApplySnapshot_WithRemovedBaseOfficeObjects_PrunesInvalidEntries()
        {
            var manager = CreatePropertyManager(
                new[]
                {
                    CreateOffice("alpha", "Alpha Yard", 100f, 2),
                },
                new[]
                {
                    new OfficeObjectDefinition
                    {
                        ObjectId = 2,
                        DisplayName = "Construction Site Cabin",
                        Function = OfficeObjectFunction.Npc,
                        Capacity = 3f,
                    },
                });

            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
                OfficeObjects =
                {
                    new OfficeObjectPersistenceEntry { InstanceId = "removed-11", OfficeId = "alpha", DefinitionId = 11, IsPlaced = true, Position = new Vector3(1f, 1f, 1f) },
                    new OfficeObjectPersistenceEntry { InstanceId = "removed-12", OfficeId = "alpha", DefinitionId = 12, IsPlaced = true, Position = new Vector3(2f, 2f, 2f) },
                    new OfficeObjectPersistenceEntry { InstanceId = "removed-13", OfficeId = "alpha", DefinitionId = 13, IsPlaced = true, Position = new Vector3(3f, 3f, 3f) },
                    new OfficeObjectPersistenceEntry { InstanceId = "cabin", OfficeId = "alpha", DefinitionId = 2, IsPlaced = true, Position = new Vector3(4f, 4f, 4f) },
                },
            }, 0);

            var officeObjects = manager.GetOfficeObjects("alpha", true).ToList();
            Assert.AreEqual(1, officeObjects.Count);
            Assert.AreEqual(2, officeObjects[0].DefinitionId);
        }

        private static PropertyManager CreatePropertyManager(OfficeDefinition[] offices, OfficeObjectDefinition[] officeObjects)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), offices.ToList());
            SetProperty(config, nameof(ModConfig.OfficeObjectDefinitions), officeObjects.ToList());
            return new PropertyManager(config);
        }

        private static PropertyManager CreatePropertyManager(params OfficeDefinition[] offices)
        {
            return CreatePropertyManager(offices, new OfficeObjectDefinition[0]);
        }

        private static OfficeDefinition CreateOffice(string officeId, string displayName, float weeklyRent, int maxCommercialVehicles, params OfficeFacilityAnchorDefinition[] facilityAnchors)
        {
            return new OfficeDefinition
            {
                OfficeId = officeId,
                SiteName = displayName,
                OfficePrice = weeklyRent * 10f,
                WeeklyOfficeRent = weeklyRent,
                MaxCommercialVehicles = maxCommercialVehicles,
                FacilityAnchors = facilityAnchors != null ? facilityAnchors.ToList() : new System.Collections.Generic.List<OfficeFacilityAnchorDefinition>(),
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
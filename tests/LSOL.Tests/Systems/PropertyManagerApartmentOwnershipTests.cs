using System.Linq;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PropertyManagerApartmentOwnershipTests
    {
        private const int MinutesPerWeek = 7 * 24 * 60;

        [TestMethod]
        public void TryPurchaseApartment_OwnedAccessDoesNotGenerateWeeklyApartmentRent()
        {
            var manager = CreatePropertyManager(CreateApartment("alpha", "Alpha Heights", 1000f, 100f));
            var balance = 5000f;

            Assert.IsTrue(manager.TryPurchaseApartment("alpha", ref balance, 0, out _));

            var state = manager.GetApartmentState("alpha");
            Assert.IsNotNull(state);
            Assert.AreEqual("alpha", manager.ActiveApartmentId);
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
        public void TryRentApartment_RentalAccessBillsWeeklyAndCanUseApartmentSystems()
        {
            var manager = CreatePropertyManager(CreateApartment("alpha", "Alpha Heights", 1000f, 100f));
            var balance = 500f;

            Assert.IsTrue(manager.TryRentApartment("alpha", ref balance, 0, out _));

            var state = manager.GetApartmentState("alpha");
            Assert.IsNotNull(state);
            Assert.AreEqual("alpha", manager.ActiveApartmentId);
            Assert.IsFalse(state.IsOwned);
            Assert.IsTrue(state.IsRented);
            Assert.AreEqual(400f, balance, 0.01f);
            Assert.IsTrue(manager.CanUseApartmentSystems(out _));

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(300f, balance, 0.01f);
            Assert.IsTrue(messages.Any(message => message.Contains("Alpha Heights")));
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(1, state.LastChargedWeekIndex);
        }

        [TestMethod]
        public void TryPurchaseApartment_WhenApartmentIsRented_ConvertsRentalToOwnedAndStopsFutureRent()
        {
            var manager = CreatePropertyManager(CreateApartment("alpha", "Alpha Heights", 1000f, 100f));
            var balance = 2500f;

            Assert.IsTrue(manager.TryRentApartment("alpha", ref balance, 0, out _));
            Assert.IsTrue(manager.TryPurchaseApartment("alpha", ref balance, 0, out _));

            var state = manager.GetApartmentState("alpha");
            Assert.IsNotNull(state);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
            Assert.AreEqual(1400f, balance, 0.01f);

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(1400f, balance, 0.01f);
            Assert.AreEqual(0, messages.Count);
        }

        [TestMethod]
        public void TryRentApartment_WhenAnotherRentalExists_TransfersRentalAndOnlyBillsDestinationApartment()
        {
            var manager = CreatePropertyManager(
                CreateApartment("alpha", "Alpha Heights", 1000f, 100f),
                CreateApartment("bravo", "Bravo Tower", 2000f, 200f));
            var balance = 1000f;

            Assert.IsTrue(manager.TryRentApartment("alpha", ref balance, 0, out _));
            Assert.IsTrue(manager.TryRentApartment("bravo", ref balance, 0, out _));

            var alphaState = manager.GetApartmentState("alpha");
            var bravoState = manager.GetApartmentState("bravo");

            Assert.IsNotNull(alphaState);
            Assert.IsNotNull(bravoState);
            Assert.AreEqual("bravo", manager.ActiveApartmentId);
            Assert.IsFalse(alphaState.IsOwned);
            Assert.IsFalse(alphaState.IsRented);
            Assert.AreEqual(0f, alphaState.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, alphaState.LastChargedWeekIndex);
            Assert.IsTrue(bravoState.IsRented);
            Assert.AreEqual(700f, balance, 0.01f);

            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.AreEqual(500f, balance, 0.01f);
            Assert.IsTrue(messages.Any(message => message.Contains("Bravo Tower")));
            Assert.IsFalse(messages.Any(message => message.Contains("Alpha Heights")));
        }

        [TestMethod]
        public void TryCancelApartmentRental_WhenActiveRentalCanceled_UsesOwnedFallbackAndReassignsVehicles()
        {
            var manager = CreatePropertyManager(
                CreateApartment("alpha", "Alpha Heights", 1000f, 100f),
                CreateApartment("bravo", "Bravo Tower", 2000f, 200f));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveApartmentId = "alpha",
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "alpha", IsRented = true, LastChargedWeekIndex = 0 },
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "bravo", IsOwned = true, LastChargedWeekIndex = -1 },
                },
                PersonalVehicles =
                {
                    new OwnedPersonalVehiclePersistenceEntry
                    {
                        AssetId = "car-1",
                        DisplayName = "Car 1",
                        ModelName = "blista",
                        AssignedApartmentId = "alpha",
                    },
                },
            }, 0);

            Assert.IsTrue(manager.TryCancelApartmentRental("alpha", out _));

            var alphaState = manager.GetApartmentState("alpha");
            var bravoState = manager.GetApartmentState("bravo");

            Assert.AreEqual("bravo", manager.ActiveApartmentId);
            Assert.IsFalse(alphaState.IsRented);
            Assert.IsTrue(bravoState.IsOwned);
            Assert.AreEqual("bravo", manager.PersonalVehicles.Single().AssignedApartmentId);
        }

        [TestMethod]
        public void TrySellApartment_WhenActiveOwnedApartmentSold_UsesRentalFallbackAndRefunds()
        {
            var manager = CreatePropertyManager(
                CreateApartment("alpha", "Alpha Heights", 1000f, 100f),
                CreateApartment("bravo", "Bravo Tower", 2000f, 200f));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveApartmentId = "alpha",
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "alpha", IsOwned = true, LastChargedWeekIndex = -1 },
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "bravo", IsRented = true, LastChargedWeekIndex = 0 },
                },
                PersonalVehicles =
                {
                    new OwnedPersonalVehiclePersistenceEntry
                    {
                        AssetId = "car-1",
                        DisplayName = "Car 1",
                        ModelName = "blista",
                        AssignedApartmentId = "alpha",
                    },
                },
            }, 0);
            var balance = 0f;

            Assert.IsTrue(manager.TrySellApartment("alpha", ref balance, out _));

            var alphaState = manager.GetApartmentState("alpha");
            var bravoState = manager.GetApartmentState("bravo");

            Assert.AreEqual("bravo", manager.ActiveApartmentId);
            Assert.IsFalse(alphaState.IsOwned);
            Assert.IsTrue(bravoState.IsRented);
            Assert.AreEqual(500f, balance, 0.01f);
            Assert.AreEqual("bravo", manager.PersonalVehicles.Single().AssignedApartmentId);
        }

        [TestMethod]
        public void ApplySnapshot_WithOwnedApartmentLegacyRentState_ClearsBogusOwnedApartmentArrears()
        {
            var manager = CreatePropertyManager(CreateApartment("alpha", "Alpha Heights", 1000f, 100f));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveApartmentId = "alpha",
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry
                    {
                        InteriorId = "alpha",
                        IsOwned = true,
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 275f,
                        LastChargedWeekIndex = 3,
                    },
                },
            }, 0);

            var state = manager.GetApartmentState("alpha");
            var balance = 0f;
            var messages = manager.ProcessWeeklyCharges(MinutesPerWeek, ref balance);

            Assert.IsNotNull(state);
            Assert.AreEqual("alpha", manager.ActiveApartmentId);
            Assert.IsTrue(state.IsOwned);
            Assert.IsFalse(state.IsRented);
            Assert.IsFalse(state.IsAccessSuspended);
            Assert.AreEqual(0f, state.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, state.LastChargedWeekIndex);
            Assert.AreEqual(0, messages.Count);
        }

        [TestMethod]
        public void ApplySnapshot_WithMultipleApartmentRentals_KeepsOnlyActiveRental()
        {
            var manager = CreatePropertyManager(
                CreateApartment("alpha", "Alpha Heights", 1000f, 100f),
                CreateApartment("bravo", "Bravo Tower", 2000f, 200f));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveApartmentId = "bravo",
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry
                    {
                        InteriorId = "alpha",
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 180f,
                        LastChargedWeekIndex = 2,
                    },
                    new ApartmentOwnershipPersistenceEntry
                    {
                        InteriorId = "bravo",
                        IsRented = true,
                        LastChargedWeekIndex = 2,
                    },
                },
            }, 0);

            var alphaState = manager.GetApartmentState("alpha");
            var bravoState = manager.GetApartmentState("bravo");

            Assert.IsNotNull(alphaState);
            Assert.IsNotNull(bravoState);
            Assert.AreEqual("bravo", manager.ActiveApartmentId);
            Assert.IsFalse(alphaState.IsRented);
            Assert.IsFalse(alphaState.IsAccessSuspended);
            Assert.AreEqual(0f, alphaState.OutstandingRent, 0.01f);
            Assert.AreEqual(-1, alphaState.LastChargedWeekIndex);
            Assert.IsTrue(bravoState.IsRented);
        }

        [TestMethod]
        public void ApplySnapshot_WhenActiveApartmentIsInvalid_PrefersOwnedApartmentOverRental()
        {
            var manager = CreatePropertyManager(
                CreateApartment("alpha", "Alpha Heights", 1000f, 100f),
                CreateApartment("bravo", "Bravo Tower", 2000f, 200f));
            manager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveApartmentId = "missing",
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "bravo", IsRented = true, LastChargedWeekIndex = 2 },
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "alpha", IsOwned = true, LastChargedWeekIndex = -1 },
                },
            }, 0);

            var alphaState = manager.GetApartmentState("alpha");
            var bravoState = manager.GetApartmentState("bravo");

            Assert.IsNotNull(alphaState);
            Assert.IsNotNull(bravoState);
            Assert.AreEqual("alpha", manager.ActiveApartmentId);
            Assert.IsTrue(alphaState.IsOwned);
            Assert.IsTrue(bravoState.IsRented);
        }

        private static PropertyManager CreatePropertyManager(params InteriorDefinition[] apartments)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.InteriorDefinitions), apartments.ToList());
            return new PropertyManager(config);
        }

        private static InteriorDefinition CreateApartment(string interiorId, string displayName, float purchasePrice, float weeklyRent)
        {
            return new InteriorDefinition
            {
                InteriorId = interiorId,
                InteriorName = displayName,
                InteriorIgName = displayName,
                InteriorPrice = purchasePrice,
                InteriorWeeklyRent = weeklyRent,
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
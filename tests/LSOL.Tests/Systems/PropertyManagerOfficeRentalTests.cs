using System.Linq;
using System.Reflection;
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

        private static PropertyManager CreatePropertyManager(params OfficeDefinition[] offices)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), offices.ToList());
            return new PropertyManager(config);
        }

        private static OfficeDefinition CreateOffice(string officeId, string displayName, float weeklyRent, int maxCommercialVehicles)
        {
            return new OfficeDefinition
            {
                OfficeId = officeId,
                SiteName = displayName,
                OfficePrice = weeklyRent * 10f,
                WeeklyOfficeRent = weeklyRent,
                MaxCommercialVehicles = maxCommercialVehicles,
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
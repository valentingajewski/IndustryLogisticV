using System.IO;
using System.Linq;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletPropertyPortfolioSummaryTests
    {
        [TestMethod]
        public void GetPropertyPortfolioSummary_UsesSharedPropertyBillingAndSurfacesActiveAssignments()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new[]
            {
                CreateOffice("alpha-office", "Alpha Office", "Downtown", 1200f, 120f, 1),
                CreateOffice("bravo-office", "Bravo Office", "Vespucci", 2500f, 250f, 2),
                CreateOffice("charlie-office", "Charlie Office", "Davis", 1800f, 180f, 1),
            }.ToList());
            SetProperty(config, nameof(ModConfig.InteriorDefinitions), new[]
            {
                CreateApartment("alpha-home", "Alta Loft", "Apartment", "Alta Apt", 6000f, 300f),
                CreateApartment("bravo-home", "Del Perro Flat", "Apartment", "Del Perro Apt", 4500f, 180f),
            }.ToList());
            SetProperty(config, nameof(ModConfig.MotelDefinitions), new[]
            {
                new MotelDefinition
                {
                    MotelId = "motel-1",
                    MotelName = "Pink Cage Motel",
                    MotelIgName = "PINKCAGE",
                    MotelType = "Budget Motel",
                    RestPrice = 75f,
                },
            }.ToList());

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0);
            var propertyManager = new PropertyManager(config);
            propertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha-office",
                ActiveApartmentId = "alpha-home",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha-office", IsOwned = true, LastChargedWeekIndex = 0 },
                    new OfficeOwnershipPersistenceEntry
                    {
                        OfficeId = "bravo-office",
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 250f,
                        LastChargedWeekIndex = 0,
                    },
                },
                Apartments =
                {
                    new ApartmentOwnershipPersistenceEntry { InteriorId = "alpha-home", IsOwned = true, LastChargedWeekIndex = -1 },
                    new ApartmentOwnershipPersistenceEntry
                    {
                        InteriorId = "bravo-home",
                        IsRented = true,
                        IsAccessSuspended = true,
                        OutstandingRent = 180f,
                        LastChargedWeekIndex = 0,
                    },
                },
                CommercialVehicles =
                {
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "truck-1",
                        DisplayName = "Truck 1",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = true,
                    },
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "truck-2",
                        DisplayName = "Truck 2",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = false,
                    },
                },
            }, 0);

            var store = new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                null,
                null,
                propertyManager,
                null,
                null,
                null,
                () => 0,
                null,
                null,
                () => 50000f,
                () => VehicleCargoType.Unknown,
                _ => GTA.Math.Vector3.Zero,
                () => false,
                () => string.Empty,
                () => 0,
                () => 0,
                () => 0,
                null,
                null,
                null);

            var summary = store.GetPropertyPortfolioSummary();
            var propertyBills = store.GetUpcomingBills()
                .Where(entry => entry.Category == CompanyFinanceCategory.OfficeRent || entry.Category == CompanyFinanceCategory.ApartmentRent)
                .ToList();

            Assert.AreEqual(3, summary.Offices.Count);
            Assert.AreEqual(2, summary.Apartments.Count);
            Assert.AreEqual(1, summary.Motels.Count);
            Assert.AreEqual(1, summary.OwnedOfficeCount);
            Assert.AreEqual(1, summary.RentedOfficeCount);
            Assert.AreEqual(1, summary.OwnedApartmentCount);
            Assert.AreEqual(1, summary.RentedApartmentCount);
            Assert.AreEqual(430f, summary.TotalArrears, 0.01f);
            Assert.AreEqual(120f, summary.UpcomingWeeklyRent, 0.01f);

            var activeOffice = summary.Offices.Single(entry => entry.OfficeId == "alpha-office");
            Assert.IsTrue(activeOffice.IsActive);
            Assert.AreEqual("Owned", activeOffice.StatusLabel);
            Assert.AreEqual(120f, activeOffice.WeeklyRent, 0.01f);
            Assert.AreEqual(10080, activeOffice.DueInMinutes);
            Assert.AreEqual(1, activeOffice.ActiveGarageVehicleCount);
            Assert.AreEqual(1, activeOffice.ReserveVehicleCount);
            StringAssert.Contains(activeOffice.AssignmentSummary, "Active garage 1/1");

            var overdueOffice = summary.Offices.Single(entry => entry.OfficeId == "bravo-office");
            Assert.AreEqual("Arrears", overdueOffice.StatusLabel);
            Assert.IsTrue(overdueOffice.HasArrears);
            Assert.AreEqual(250f, overdueOffice.ArrearsAmount, 0.01f);
            Assert.AreEqual(0, overdueOffice.DueInMinutes);
            StringAssert.Contains(overdueOffice.BillDetail, "Due now");

            var ownedApartment = summary.Apartments.Single(entry => entry.InteriorId == "alpha-home");
            Assert.IsTrue(ownedApartment.IsActive);
            Assert.AreEqual("Owned", ownedApartment.StatusLabel);
            Assert.AreEqual(int.MaxValue, ownedApartment.DueInMinutes);
            StringAssert.Contains(ownedApartment.BillDetail, "No rent due");

            var overdueApartment = summary.Apartments.Single(entry => entry.InteriorId == "bravo-home");
            Assert.AreEqual("Arrears", overdueApartment.StatusLabel);
            Assert.AreEqual(180f, overdueApartment.ArrearsAmount, 0.01f);
            Assert.AreEqual(0, overdueApartment.DueInMinutes);

            var motel = summary.Motels.Single();
            Assert.AreEqual("Pink Cage Motel", motel.DisplayName);
            Assert.AreEqual("Budget Motel", motel.MotelType);
            Assert.AreEqual("PINKCAGE", motel.MotelIgName);
            Assert.AreEqual(75f, motel.NightlyRestPrice, 0.01f);

            Assert.AreEqual(3, propertyBills.Count, "Portfolio property billing should match the same office/apartment bill scan used by Budget.");
            Assert.IsTrue(propertyBills.Any(entry => entry.Label == "Alpha Office" && entry.Amount == 120f && entry.DueInMinutes == 10080));
            Assert.IsTrue(propertyBills.Any(entry => entry.Label == "Bravo Office" && entry.Amount == 250f && entry.DueInMinutes == 0));
            Assert.IsTrue(propertyBills.Any(entry => entry.Label == "Del Perro Flat" && entry.Amount == 180f && entry.DueInMinutes == 0));
            Assert.IsFalse(propertyBills.Any(entry => entry.Label == "Alta Loft"), "Owned apartments must remain rent-free.");
            Assert.IsFalse(propertyBills.Any(entry => entry.Label == "Pink Cage Motel"), "Motels remain rest-only and must not become billed properties.");
        }

        private static OfficeDefinition CreateOffice(string officeId, string displayName, string districtName, float purchasePrice, float weeklyRent, int maxCommercialVehicles)
        {
            return new OfficeDefinition
            {
                OfficeId = officeId,
                SiteName = displayName,
                DistrictName = districtName,
                OfficePrice = purchasePrice,
                WeeklyOfficeRent = weeklyRent,
                MaxCommercialVehicles = maxCommercialVehicles,
            };
        }

        private static InteriorDefinition CreateApartment(string interiorId, string displayName, string interiorType, string igName, float purchasePrice, float weeklyRent)
        {
            return new InteriorDefinition
            {
                InteriorId = interiorId,
                InteriorName = displayName,
                InteriorType = interiorType,
                InteriorIgName = igName,
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
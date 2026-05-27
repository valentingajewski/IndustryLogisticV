using System.Collections.Generic;
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
    public sealed class PropertyPortfolioTabletAppTests
    {
        [TestMethod]
        public void BuildPage_ShowsPortfolioSectionsAndWiresDetailActions()
        {
            var store = CreateStore();
            var shell = new TabletShellController(new ControlBindings(), store);
            var context = new TabletShellContext(shell, new TabletStateSnapshot { Balance = 50000f });
            var invoked = new List<string>();
            var app = new PropertyPortfolioTabletApp(new PropertyPortfolioTabletActions
            {
                RentOffice = officeId => invoked.Add("rent-office:" + officeId),
                TransferOfficeRental = officeId => invoked.Add("transfer-office:" + officeId),
                PurchaseOffice = officeId => invoked.Add("purchase-office:" + officeId),
                ActivateOffice = officeId => invoked.Add("activate-office:" + officeId),
                RelinquishOfficeRental = officeId => invoked.Add("relinquish-office:" + officeId),
                PayOfficeArrears = officeId => invoked.Add("pay-office-arrears:" + officeId),
                PurchaseApartment = apartmentId => invoked.Add("purchase-apartment:" + apartmentId),
                RentApartment = apartmentId => invoked.Add("rent-apartment:" + apartmentId),
                ActivateApartment = apartmentId => invoked.Add("activate-apartment:" + apartmentId),
                CancelApartmentRental = apartmentId => invoked.Add("cancel-apartment:" + apartmentId),
                SellApartment = apartmentId => invoked.Add("sell-apartment:" + apartmentId),
                PayApartmentArrears = apartmentId => invoked.Add("pay-apartment-arrears:" + apartmentId),
                RestAtMotel = motelId => invoked.Add("rest-motel:" + motelId),
            });

            var rootPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "root"));
            var rootCaptions = rootPage.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(rootCaptions, "Offices");
            CollectionAssert.Contains(rootCaptions, "Apartments");
            CollectionAssert.Contains(rootCaptions, "Motels");

            var availableOfficePage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "office-detail", "charlie-office"));
            var availableOfficeCaptions = availableOfficePage.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(availableOfficeCaptions, "Transfer Office Rental");
            CollectionAssert.Contains(availableOfficeCaptions, "Purchase Office");

            var overdueOfficePage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "office-detail", "bravo-office"));
            var payOfficeArrears = FindItem(overdueOfficePage, "Pay Office Arrears");
            Assert.IsNotNull(payOfficeArrears);
            payOfficeArrears.OnActivate();

            var activeApartmentPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "apartment-detail", "alpha-home"));
            var activeApartmentCaptions = activeApartmentPage.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(activeApartmentCaptions, "Sell Apartment");

            var overdueApartmentPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "apartment-detail", "bravo-home"));
            var payApartmentArrears = FindItem(overdueApartmentPage, "Pay Apartment Arrears");
            Assert.IsNotNull(payApartmentArrears);
            payApartmentArrears.OnActivate();

            var motelPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "motel-detail", "motel-1"));
            var quickRest = FindItem(motelPage, "Quick Rest");
            Assert.IsNotNull(quickRest);
            quickRest.OnActivate();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "pay-office-arrears:bravo-office",
                    "pay-apartment-arrears:bravo-home",
                    "rest-motel:motel-1",
                },
                invoked);
        }

        private static TabletStateStore CreateStore()
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

            return new TabletStateStore(
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
        }

        private static string GetCaption(MenuItem item)
        {
            return item != null && item.CaptionFactory != null
                ? item.CaptionFactory() ?? string.Empty
                : string.Empty;
        }

        private static MenuItem FindItem(TabletShellPage page, string caption)
        {
            return page.Items.FirstOrDefault(item => string.Equals(GetCaption(item), caption, System.StringComparison.Ordinal));
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
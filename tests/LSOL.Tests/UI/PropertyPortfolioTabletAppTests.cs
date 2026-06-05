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
            CollectionAssert.DoesNotContain(rootCaptions, "Fleet Slots");

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

        [TestMethod]
        public void BuildOfficeDetailPage_MarkerOperationsPointsNpcHiringToConstructionSiteCabin()
        {
            var store = CreateStore();
            var shell = new TabletShellController(new ControlBindings(), store);
            var context = new TabletShellContext(shell, new TabletStateSnapshot { Balance = 50000f });
            var app = new PropertyPortfolioTabletApp(new PropertyPortfolioTabletActions());

            var page = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "office-detail", "alpha-office"));
            var markerOperations = FindItem(page, "Marker Operations");

            Assert.IsNotNull(markerOperations);

            var detail = markerOperations.DetailFactory != null ? markerOperations.DetailFactory() ?? string.Empty : string.Empty;
            StringAssert.Contains(detail, "Construction Site Cabin");
            Assert.IsFalse(detail.Contains("Hire NPC remain on the office marker menu"));
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
                CommercialVehicleAssets =
                {
                    new OwnedCommercialVehicleAssetPersistenceEntry
                    {
                        AssetId = "rigid-1",
                        DisplayName = "Truck 1",
                        ModelName = "mule",
                        FleetRole = CommercialVehicleFleetRole.Rigid,
                        AssignedOfficeId = "alpha-office",
                        PurchasePrice = 45000f,
                        CapacityTons = 12f,
                    },
                    new OwnedCommercialVehicleAssetPersistenceEntry
                    {
                        AssetId = "tractor-1",
                        DisplayName = "Lead Tractor",
                        ModelName = "phantom",
                        FleetRole = CommercialVehicleFleetRole.Tractor,
                        AssignedOfficeId = "alpha-office",
                        PurchasePrice = 90000f,
                    },
                    new OwnedCommercialVehicleAssetPersistenceEntry
                    {
                        AssetId = "trailer-1",
                        DisplayName = "Container Trailer",
                        ModelName = "trailers4",
                        FleetRole = CommercialVehicleFleetRole.Trailer,
                        AssignedOfficeId = "alpha-office",
                        PurchasePrice = 30000f,
                        CapacityTons = 24f,
                    },
                    new OwnedCommercialVehicleAssetPersistenceEntry
                    {
                        AssetId = "tractor-pool-1",
                        DisplayName = "Reserve Tractor",
                        ModelName = "packer",
                        FleetRole = CommercialVehicleFleetRole.Tractor,
                        AssignedOfficeId = "alpha-office",
                        PurchasePrice = 80000f,
                    },
                    new OwnedCommercialVehicleAssetPersistenceEntry
                    {
                        AssetId = "trailer-pool-1",
                        DisplayName = "Reefer Trailer",
                        ModelName = "trailers2",
                        FleetRole = CommercialVehicleFleetRole.Trailer,
                        AssignedOfficeId = "alpha-office",
                        PurchasePrice = 26000f,
                        CapacityTons = 22f,
                    },
                },
                CommercialVehicles =
                {
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "slot-rigid",
                        DisplayName = "Truck 1",
                        TractorVehicleId = "rigid-1",
                        PoweredModelName = "mule",
                        CargoModelName = "mule",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = true,
                    },
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "slot-coupled",
                        DisplayName = "Lead Tractor + Container Trailer",
                        TractorVehicleId = "tractor-1",
                        TrailerVehicleId = "trailer-1",
                        PoweredModelName = "phantom",
                        CargoModelName = "trailers4",
                        HasSeparateCargoVehicle = true,
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = true,
                    },
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "slot-tractor-pool",
                        DisplayName = "Reserve Tractor",
                        TractorVehicleId = "tractor-pool-1",
                        PoweredModelName = "packer",
                        CargoModelName = "packer",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = false,
                    },
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "slot-trailer-pool",
                        DisplayName = "Reefer Trailer",
                        TrailerVehicleId = "trailer-pool-1",
                        PoweredModelName = "trailers2",
                        CargoModelName = "trailers2",
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

        [TestMethod]
        public void BuildFleetPages_ShowSlotDetailAndAssignmentPools()
        {
            var store = CreateStore();
            var shell = new TabletShellController(new ControlBindings(), store);
            var context = new TabletShellContext(shell, new TabletStateSnapshot { Balance = 50000f });
            var app = new PropertyPortfolioTabletApp(new PropertyPortfolioTabletActions());

            var fleetPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "fleet"));
            var fleetCaptions = fleetPage.Items.Select(GetCaption).ToList();
            CollectionAssert.Contains(fleetCaptions, "Offices");
            CollectionAssert.Contains(fleetCaptions, "Apartments");
            CollectionAssert.DoesNotContain(fleetCaptions, "Fleet Slots");
            Assert.AreEqual("Property Portfolio", fleetPage.Title);

            var fleetDetailPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "fleet-slot-detail", "slot-coupled"));
            var fleetDetailCaptions = fleetDetailPage.Items.Select(GetCaption).ToList();
            CollectionAssert.Contains(fleetDetailCaptions, "Offices");
            CollectionAssert.DoesNotContain(fleetDetailCaptions, "Detach Powered Unit");

            var poweredPoolPage = app.BuildPage(context, new TabletRoute(TabletAppIds.PropertyPortfolio, "fleet-powered", "slot-trailer-pool"));
            var poweredPoolCaptions = poweredPoolPage.Items.Select(GetCaption).ToList();
            CollectionAssert.Contains(poweredPoolCaptions, "Motels");
            CollectionAssert.DoesNotContain(poweredPoolCaptions, "Reserve Tractor");
        }

        [TestMethod]
        public void BuildFleetRoutes_FallBackToPortfolioRoot()
        {
            BuildFleetPages_ShowSlotDetailAndAssignmentPools();
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
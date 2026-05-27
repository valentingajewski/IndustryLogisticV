using System.Collections.Generic;
using System.IO;
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
    public sealed class TabletBudgetFleetResaleSummaryTests
    {
        [TestMethod]
        public void BudgetAndInventoryViews_SurfaceFleetResaleSummaryWithoutMergingIntoInventoryTotal()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new List<OfficeDefinition>
            {
                new OfficeDefinition
                {
                    OfficeId = "alpha-office",
                    SiteName = "Alpha Office",
                    OfficePrice = 1000f,
                    WeeklyOfficeRent = 100f,
                    MaxCommercialVehicles = 3,
                },
            });

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var market = new GlobalMarketManager(0);
            var propertyManager = new PropertyManager(config);
            propertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
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
                CommercialVehicles =
                {
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "owned-1",
                        DisplayName = "Owned 1",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = true,
                        PurchasePrice = 100000f,
                        MaintenanceCondition = 1f,
                        LifetimeMaintenanceCost = 0f,
                    },
                    new OwnedCommercialVehiclePersistenceEntry
                    {
                        AssetId = "rent-1",
                        DisplayName = "Rental 1",
                        AssignedOfficeId = "alpha-office",
                        InActiveGarage = true,
                        PurchasePrice = 80000f,
                        IsRental = true,
                        DailyRent = 250f,
                    },
                },
            }, 0);

            var store = new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                market,
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

            var overview = store.GetBudgetOverview();
            var valuation = store.GetInventoryValuation();

            var baselinePropertyManager = new PropertyManager(config);
            baselinePropertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
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
            }, 0);
            var baselineStore = new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                market,
                null,
                null,
                baselinePropertyManager,
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
            var baselineInventoryTotal = baselineStore.GetInventoryValuation().TotalValue;

            Assert.IsNotNull(overview.FleetResale);
            Assert.AreEqual(1, overview.FleetResale.OwnedVehicleCount);
            Assert.AreEqual(100000f, overview.FleetResale.PurchaseBasis, 0.01f);
            Assert.AreEqual(50000f, overview.FleetResale.EstimatedResaleValue, 0.01f);
            Assert.AreEqual(50000f, overview.FleetResale.TotalDepreciationLoss, 0.01f);
            Assert.AreEqual(50f, overview.FleetResale.RecoveryPercentOfPurchase, 0.01f);

            Assert.IsNotNull(valuation.FleetResale);
            Assert.AreEqual(1, valuation.FleetResale.OwnedVehicleCount);
            Assert.AreEqual(50000f, valuation.FleetResale.EstimatedResaleValue, 0.01f);
            Assert.AreEqual(baselineInventoryTotal, valuation.TotalValue, 0.01f);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}

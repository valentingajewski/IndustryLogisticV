using System;
using System.Collections.Generic;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class NpcLogisticsManagerPersistenceTests
    {
        [TestMethod]
        public void ApplyPersistenceSnapshot_WhenContractsShareAssignedTruck_SkipsConflictingRestore()
        {
            var commercialVehicles = new List<OwnedCommercialVehiclePersistenceEntry>
            {
                new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "truck-1",
                    DisplayName = "Ore Truck",
                    PoweredModelName = "oretruck",
                    CargoModelName = "oretruck",
                    HasSeparateCargoVehicle = false,
                    InActiveGarage = true,
                    IsDeployed = false,
                    CargoType = VehicleCargoType.Aggregates,
                    CapacityTons = 12f,
                },
            };

            var manager = CreateNpcLogisticsManager(commercialVehicles);
            var tierId = manager.DriverTiers[0].Id;
            var snapshot = new NpcLogisticsPersistenceSnapshot();
            snapshot.Contracts.Add(CreateContractSnapshot(1, tierId, "alpha-depot", "bravo-depot", "truck-1"));
            snapshot.Contracts.Add(CreateContractSnapshot(2, tierId, "bravo-depot", "alpha-depot", "truck-1"));

            manager.ApplyPersistenceSnapshot(snapshot);

            Assert.AreEqual(1, manager.Contracts.Count);
            Assert.AreEqual(1, manager.Contracts[0].Id);
            Assert.AreEqual("truck-1", manager.Contracts[0].AssignedVehicleAssetId);
            Assert.AreEqual("Ore Truck", manager.Contracts[0].AssignedVehicleDisplayName);
        }

        [TestMethod]
        public void FindContractUsingAssignedVehicle_UsesContractLevelFallbackWhenRouteAssetIdsAreBlank()
        {
            var commercialVehicles = new List<OwnedCommercialVehiclePersistenceEntry>
            {
                new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "truck-1",
                    DisplayName = "Ore Truck",
                    PoweredModelName = "oretruck",
                    CargoModelName = "oretruck",
                    HasSeparateCargoVehicle = false,
                    InActiveGarage = true,
                    IsDeployed = false,
                    CargoType = VehicleCargoType.Aggregates,
                    CapacityTons = 12f,
                },
            };

            var manager = CreateNpcLogisticsManager(commercialVehicles);
            var contract = (NpcLogisticsContract)Activator.CreateInstance(
                typeof(NpcLogisticsContract),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { 7 },
                null);

            Assert.IsNotNull(contract);
            contract.AssignedVehicleAssetId = "truck-1";
            contract.AssignedVehicleDisplayName = "Ore Truck";
            contract.Routes.Add(new NpcLogisticsRouteDefinition
            {
                Commodity = "Ore",
                AssignedVehicleAssetId = string.Empty,
                AssignedVehicleDisplayName = string.Empty,
            });

            GetContractsList(manager).Add(contract);

            var resolved = manager.FindContractUsingAssignedVehicle("truck-1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(7, resolved.Id);
        }

        [TestMethod]
        public void FindContractUsingAssignedVehicle_UsesContractLevelFallbackWhenOtherRoutesHaveExplicitAssignments()
        {
            var commercialVehicles = new List<OwnedCommercialVehiclePersistenceEntry>
            {
                new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "truck-1",
                    DisplayName = "Ore Truck A",
                    PoweredModelName = "oretruck",
                    CargoModelName = "oretruck",
                    HasSeparateCargoVehicle = false,
                    InActiveGarage = true,
                    IsDeployed = false,
                    CargoType = VehicleCargoType.Aggregates,
                    CapacityTons = 12f,
                },
                new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "truck-2",
                    DisplayName = "Ore Truck B",
                    PoweredModelName = "oretruck",
                    CargoModelName = "oretruck",
                    HasSeparateCargoVehicle = false,
                    InActiveGarage = true,
                    IsDeployed = false,
                    CargoType = VehicleCargoType.Aggregates,
                    CapacityTons = 12f,
                },
            };

            var manager = CreateNpcLogisticsManager(commercialVehicles);
            var contract = (NpcLogisticsContract)Activator.CreateInstance(
                typeof(NpcLogisticsContract),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { 8 },
                null);

            Assert.IsNotNull(contract);
            contract.AssignedVehicleAssetId = "truck-2";
            contract.AssignedVehicleDisplayName = "Ore Truck B";
            contract.Routes.Add(new NpcLogisticsRouteDefinition
            {
                Commodity = "Ore",
                AssignedVehicleAssetId = "truck-1",
                AssignedVehicleDisplayName = "Ore Truck A",
            });
            contract.Routes.Add(new NpcLogisticsRouteDefinition
            {
                Commodity = "Ore",
                AssignedVehicleAssetId = string.Empty,
                AssignedVehicleDisplayName = string.Empty,
            });

            GetContractsList(manager).Add(contract);

            var resolved = manager.FindContractUsingAssignedVehicle("truck-2");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(8, resolved.Id);
        }

        private static NpcLogisticsManager CreateNpcLogisticsManager(IReadOnlyList<OwnedCommercialVehiclePersistenceEntry> commercialVehicles)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "alpha-depot", CreateIndustryConfig("alpha-depot", "Alpha Depot", "Port") },
                { "bravo-depot", CreateIndustryConfig("bravo-depot", "Bravo Depot", "GrandSenora") },
            });
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>
            {
                new VehicleDefinition
                {
                    Id = "oretruck",
                    DisplayName = "Ore Truck",
                    ModelName = "oretruck",
                    CargoType = VehicleCargoType.Aggregates,
                    AcceptedCommodities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                    CapacityTons = 12f,
                    IsEnabled = true,
                    IsTrailer = false,
                    IsTractor = false,
                },
            });
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var configDirectory = System.IO.Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");

            return new NpcLogisticsManager(
                configDirectory,
                industryManager,
                fleetManager,
                null,
                position => position,
                () => 100000f,
                _ => { },
                _ => { },
                _ => { },
                null,
                () => commercialVehicles,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        private static NpcLogisticsContractSnapshot CreateContractSnapshot(int id, string tierId, string originIndustryId, string destinationIndustryId, string assignedVehicleAssetId)
        {
            var snapshot = new NpcLogisticsContractSnapshot
            {
                Id = id,
                TierId = tierId,
                OriginIndustryId = originIndustryId,
                DestinationIndustryId = destinationIndustryId,
                Commodity = "Ore",
                AssignedVehicleAssetId = assignedVehicleAssetId,
                AssignedVehicleDisplayName = "Ore Truck",
                DestinationTriggerThresholdPercent = 100,
            };
            snapshot.Routes.Add(new NpcLogisticsRouteSnapshot
            {
                OriginIndustryId = originIndustryId,
                DestinationIndustryId = destinationIndustryId,
                Commodity = "Ore",
                AssignedVehicleAssetId = assignedVehicleAssetId,
                AssignedVehicleDisplayName = "Ore Truck",
                DestinationTriggerThresholdPercent = 100,
            });
            return snapshot;
        }

        private static IndustryConfig CreateIndustryConfig(string id, string name, string districtName)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = name,
                DistrictName = districtName,
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.Depot,
                OwnershipTier = SiteOwnershipTier.Local,
                Position = Vector3.Zero,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                ProductionRate = 1f,
                InputCapacityTons = 1f,
                OutputCapacityTons = 1f,
                IndustryOwnerCut = 0.5f,
            };
        }

        private static List<NpcLogisticsContract> GetContractsList(NpcLogisticsManager manager)
        {
            var field = typeof(NpcLogisticsManager).GetField("_contracts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_contracts");
            var contracts = field.GetValue(manager) as List<NpcLogisticsContract>;
            Assert.IsNotNull(contracts, "contracts");
            return contracts;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
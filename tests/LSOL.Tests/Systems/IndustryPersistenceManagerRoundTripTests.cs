using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class IndustryPersistenceManagerRoundTripTests
    {
        [TestMethod]
        public void SaveAndLoad_WithPlayerStatisticsMetadata_RestoresSnapshotAndBumpsVersion()
        {
            var filePath = TestWorkspace.CreateTempFilePath("player-stats.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PlayerStatistics = new PlayerStatisticsPersistenceSnapshot
                    {
                        HighestCompanyBalanceEver = 1250000f,
                        HighestCompanyBalanceBeforeFirstNpcHire = 250000f,
                        HighestCompanyBalanceBeforeFirstLoan = 500000f,
                        TotalSuccessfulDeliveries = 42,
                        TotalSuccessfulCleanDeliveries = 17,
                        DeliveriesBeforeFirstNpcHire = 9,
                        TotalTransportedTons = 512.75f,
                        HasEverHiredNpc = true,
                        HasEverTakenLoan = true,
                        CumulativeNpcDeliveryIncome = 81123.5f,
                        TotalSpecialMissionsCompleted = 6,
                        TotalEmergencyServiceUsages = 3,
                    },
                };

                metadata.PlayerStatistics.CommodityTotals.Add(new PlayerCommodityStatisticSnapshot
                {
                    CommodityId = "Iron",
                    Tons = 33.5f,
                });
                metadata.PlayerStatistics.CommodityTotals.Add(new PlayerCommodityStatisticSnapshot
                {
                    CommodityId = "Fuel",
                    Tons = 12.25f,
                });
                metadata.PlayerStatistics.UnlockedSuccessIds.Add("road_veteran");
                metadata.PlayerStatistics.UnlockedSuccessIds.Add("first_delivery");

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">16</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"Successes\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var snapshot = result.Metadata.PlayerStatistics;

                Assert.IsNotNull(snapshot);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual(1250000f, snapshot.HighestCompanyBalanceEver, 0.01f);
                Assert.AreEqual(250000f, snapshot.HighestCompanyBalanceBeforeFirstNpcHire, 0.01f);
                Assert.AreEqual(500000f, snapshot.HighestCompanyBalanceBeforeFirstLoan, 0.01f);
                Assert.AreEqual(42, snapshot.TotalSuccessfulDeliveries);
                Assert.AreEqual(17, snapshot.TotalSuccessfulCleanDeliveries);
                Assert.AreEqual(9, snapshot.DeliveriesBeforeFirstNpcHire);
                Assert.AreEqual(512.75f, snapshot.TotalTransportedTons, 0.01f);
                Assert.IsTrue(snapshot.HasEverHiredNpc);
                Assert.IsTrue(snapshot.HasEverTakenLoan);
                Assert.AreEqual(81123.5f, snapshot.CumulativeNpcDeliveryIncome, 0.01f);
                Assert.AreEqual(6, snapshot.TotalSpecialMissionsCompleted);
                Assert.AreEqual(3, snapshot.TotalEmergencyServiceUsages);
                Assert.AreEqual(2, snapshot.CommodityTotals.Count);
                Assert.AreEqual("Fuel", snapshot.CommodityTotals[0].CommodityId);
                Assert.AreEqual(12.25f, snapshot.CommodityTotals[0].Tons, 0.01f);
                Assert.AreEqual("Ore", snapshot.CommodityTotals[1].CommodityId);
                Assert.AreEqual(33.5f, snapshot.CommodityTotals[1].Tons, 0.01f);
                CollectionAssert.AreEqual(
                    new[] { "first_delivery", "road_veteran" },
                    snapshot.UnlockedSuccessIds.ToArray());
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithApartmentRentalPropertyState_RestoresRentalFlags()
        {
            var filePath = TestWorkspace.CreateTempFilePath("apartment-rental.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                    {
                        ActiveApartmentId = "bravo",
                    },
                };

                metadata.PropertyOwnership.Apartments.Add(new ApartmentOwnershipPersistenceEntry
                {
                    InteriorId = "alpha",
                    IsOwned = true,
                    LastChargedWeekIndex = -1,
                });
                metadata.PropertyOwnership.Apartments.Add(new ApartmentOwnershipPersistenceEntry
                {
                    InteriorId = "bravo",
                    IsRented = true,
                    IsAccessSuspended = true,
                    OutstandingRent = 225f,
                    LastChargedWeekIndex = 4,
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"IsRented\">true</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var snapshot = result.Metadata.PropertyOwnership;
                var bravoApartment = snapshot.Apartments.Single(entry => string.Equals(entry.InteriorId, "bravo", StringComparison.OrdinalIgnoreCase));

                Assert.IsNotNull(snapshot);
                Assert.AreEqual("bravo", snapshot.ActiveApartmentId);
                Assert.IsTrue(bravoApartment.IsRented);
                Assert.IsFalse(bravoApartment.IsOwned);
                Assert.IsTrue(bravoApartment.IsAccessSuspended);
                Assert.AreEqual(225f, bravoApartment.OutstandingRent, 0.01f);
                Assert.AreEqual(4, bravoApartment.LastChargedWeekIndex);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void Save_WithClearedFinanceSnapshot_DoesNotWriteFinanceSections()
        {
            var filePath = TestWorkspace.CreateTempFilePath("fresh-finance.state.xml");

            try
            {
                var financeTracker = new CompanyFinanceTracker();
                financeTracker.RecordIncome(CompanyFinanceCategory.OtherIncome, 50000f, 1440, "Previous save carry-over");
                financeTracker.RecordExpense(CompanyFinanceCategory.OtherExpense, 2500f, 1500, "Previous save expense");
                financeTracker.Clear();

                var metadata = new IndustryPersistenceMetadata
                {
                    Profit = 20000f,
                    StartingBalance = 20000f,
                    Finance = financeTracker.CreatePersistenceSnapshot(),
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);

                Assert.IsFalse(rawSave.Contains("<Section name=\"FinanceMeta\">"));
                Assert.IsFalse(rawSave.Contains("<Section name=\"Finance:Transaction:"));

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());

                Assert.IsNotNull(result.Metadata);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.IsNull(result.Metadata.Finance);
                Assert.AreEqual(20000f, result.Metadata.Profit, 0.01f);
                Assert.AreEqual(20000f, result.Metadata.StartingBalance, 0.01f);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithGlobalMarketSnapshot_RestoresCommodityMultipliersAndTimers()
        {
            var filePath = TestWorkspace.CreateTempFilePath("market.state.xml");

            try
            {
                var basePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 700f },
                    { "Steel", 900f },
                };

                var sourceMarket = new GlobalMarketManager(0, basePrices);
                sourceMarket.Update(600000);
                sourceMarket.RegisterDelivery("Fuel", 650000);
                sourceMarket.Update(1200000);
                sourceMarket.RegisterDelivery("Steel", 1300000);
                sourceMarket.Update(1500000);

                var metadata = new IndustryPersistenceMetadata
                {
                    Profit = 1000f,
                    StartingBalance = 1000f,
                    Market = sourceMarket.CreatePersistenceSnapshot(1500000),
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">18</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"Market:Commodity:Fuel\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var restoredMarket = new GlobalMarketManager(10000, basePrices);
                restoredMarket.ApplyPersistenceSnapshot(result.Metadata.Market, 10000);

                Assert.IsNotNull(result.Metadata.Market);
                Assert.AreEqual(735f, restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(900f, restoredMarket.GetUnitPrice("Steel"), 0.01f);

                restoredMarket.Update(359999);
                Assert.AreEqual(735f, restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(900f, restoredMarket.GetUnitPrice("Steel"), 0.01f);

                restoredMarket.Update(360000);
                Assert.AreEqual(770f, restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(900f, restoredMarket.GetUnitPrice("Steel"), 0.01f);

                restoredMarket.Update(410000);
                Assert.AreEqual(770f, restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(945f, restoredMarket.GetUnitPrice("Steel"), 0.01f);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithPlayerContractsSnapshot_RestoresContractsAndVehicleTagsAndBumpsVersion()
        {
            var filePath = TestWorkspace.CreateTempFilePath("player-contracts.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PlayerContracts = new PlayerContractsPersistenceSnapshot
                    {
                        NextContractId = 7,
                        LastBoardRefreshMinute = 12345,
                        SelectedCommodityFilter = "Steel",
                    },
                    OwnedFleet = new OwnedFleetPersistenceSnapshot(),
                    PropertyOwnership = new PropertyOwnershipPersistenceSnapshot(),
                };

                metadata.PlayerContracts.Contracts.Add(new PlayerContractSnapshot
                {
                    Id = "pc-001",
                    Type = PlayerContractType.FreightMarket,
                    Status = PlayerContractStatus.Loaded,
                    Commodity = "Steel",
                    OriginIndustryId = "alpha",
                    DestinationIndustryId = "beta",
                    ListedTons = 12.5f,
                    LoadedTons = 10.5f,
                    DeliveredTons = 2f,
                    RouteDistanceMeters = 4200f,
                    QuotedUnitPrice = 860f,
                    QuotedGrossPayout = 10750f,
                    QuotedImbalanceScore = 0.82f,
                    ListedAtMinute = 12000,
                    ExpiryMinute = 12180,
                    AcceptedAtMinute = 12010,
                    AcceptedExpiryMinute = 12310,
                    VehicleRequirementLabel = "Flatbed or lowboy",
                    RequiresOwnedVehicle = true,
                    AssignedCommercialVehicleAssetId = "fleet-1",
                    AssignedCommercialVehicleDisplayName = "Hauler Alpha",
                    CargoCondition = 0.91f,
                    TotalLostTons = 0.4f,
                    SourceDistrictName = "Terminal",
                    StatusMessage = "Loaded 10.50t Steel",
                });
                metadata.PlayerContracts.Cooldowns.Add(new PlayerContractCooldownSnapshot
                {
                    RouteKey = "alpha|beta|steel",
                    AvailableAgainMinute = 12555,
                });

                metadata.OwnedFleet.Vehicles.Add(new OwnedFleetVehicleSnapshot
                {
                    PoweredModelName = "phantom3",
                    CargoModelName = "trailers",
                    HasSeparateCargoVehicle = true,
                    PoweredPosition = Vector3.Zero,
                    PoweredHeading = 180f,
                    CargoType = VehicleCargoType.CraftedGoods,
                    CapacityTons = 20f,
                    Commodity = "Steel",
                    WeightTons = 10.5f,
                    CargoCondition = 0.91f,
                    TotalLostTons = 0.4f,
                    SourceIndustryId = "alpha",
                    SourceDistrictName = "Terminal",
                    PlayerContractId = "pc-001",
                    PlayerContractDestinationIndustryId = "beta",
                });

                metadata.PropertyOwnership.CommercialVehicles.Add(new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = "fleet-1",
                    DisplayName = "Hauler Alpha",
                    PoweredModelName = "phantom3",
                    CargoModelName = "trailers",
                    HasSeparateCargoVehicle = true,
                    AssignedOfficeId = "office-1",
                    InActiveGarage = true,
                    CargoType = VehicleCargoType.CraftedGoods,
                    CapacityTons = 20f,
                    Commodity = "Steel",
                    WeightTons = 10.5f,
                    CargoCondition = 0.91f,
                    TotalLostTons = 0.4f,
                    SourceIndustryId = "alpha",
                    SourceDistrictName = "Terminal",
                    PlayerContractId = "pc-001",
                    PlayerContractDestinationIndustryId = "beta",
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">21</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"PlayerContracts\">");
                StringAssert.Contains(rawSave, "<Value key=\"PlayerContractId\">pc-001</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var contracts = result.Metadata.PlayerContracts;
                var fleetVehicle = result.Metadata.OwnedFleet.Vehicles.Single();
                var commercialVehicle = result.Metadata.PropertyOwnership.CommercialVehicles.Single();

                Assert.IsNotNull(contracts);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual(7, contracts.NextContractId);
                Assert.AreEqual(12345, contracts.LastBoardRefreshMinute);
                Assert.AreEqual("Steel", contracts.SelectedCommodityFilter);
                Assert.AreEqual(1, contracts.Contracts.Count);
                Assert.AreEqual("pc-001", contracts.Contracts[0].Id);
                Assert.AreEqual(PlayerContractType.FreightMarket, contracts.Contracts[0].Type);
                Assert.AreEqual(PlayerContractStatus.Loaded, contracts.Contracts[0].Status);
                Assert.AreEqual("fleet-1", contracts.Contracts[0].AssignedCommercialVehicleAssetId);
                Assert.AreEqual(1, contracts.Cooldowns.Count);
                Assert.AreEqual("alpha|beta|steel", contracts.Cooldowns[0].RouteKey);

                Assert.AreEqual("pc-001", fleetVehicle.PlayerContractId);
                Assert.AreEqual("beta", fleetVehicle.PlayerContractDestinationIndustryId);
                Assert.AreEqual("pc-001", commercialVehicle.PlayerContractId);
                Assert.AreEqual("beta", commercialVehicle.PlayerContractDestinationIndustryId);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void LoadWithMetadata_WithTerritorySnapshot_AppliesSavedSiteAndCorridorState()
        {
            var filePath = TestWorkspace.CreateTempFilePath("territory.state.xml");
            var territoryManager = CreateTerritoryManager();

            try
            {
                var territorySnapshot = new TerritoryPersistenceSnapshot();
                territorySnapshot.Sites.Add(new TerritorySiteSnapshot
                {
                    SiteId = "alpha-depot",
                    ControlLevel = TerritoryControlLevel.Owned,
                    CrewAssigned = true,
                    LoadRuns = 4,
                    UnloadRuns = 2,
                    TotalDeliveries = 9,
                    TotalDeliveredTons = 24.5f,
                    TotalLoadedTons = 15.25f,
                    FranchiseLevel = TerritoryFranchiseLevel.Preferred,
                    LoaderCount = 1,
                    MechanicCount = 2,
                    GuardCount = 1,
                    ManagerCount = 1,
                    Repossessions = 1,
                    NpcLoads = 3,
                    NpcDeliveries = 2,
                    LastCommodity = "Iron",
                    SiteOperatorAssigned = true,
                    LastPassiveIncomeAmount = 425f,
                    LastPassiveIncomeWeekIndex = 12,
                    LastPassiveIncomeStatus = "Paid last week",
                });
                territorySnapshot.Corridors.Add(new TerritoryCorridorSnapshot
                {
                    DistrictA = "Port",
                    DistrictB = "GrandSenora",
                    DeliveryCount = 12,
                    TotalDeliveredTons = 60f,
                    RightLevel = CorridorRightLevel.Corridor,
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), null, territorySnapshot);
                IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), territoryManager);

                var siteState = territoryManager.SiteStates.Single(site => site.SiteId == "alpha-depot");
                var corridorState = territoryManager.GetCorridorState("Port", "GrandSenora");
                var districtState = territoryManager.GetDistrictState("Port");

                Assert.IsNotNull(siteState);
                Assert.AreEqual(TerritoryControlLevel.Owned, siteState.ControlLevel);
                Assert.IsTrue(siteState.CrewAssigned);
                Assert.AreEqual(4, siteState.LoadRuns);
                Assert.AreEqual(2, siteState.UnloadRuns);
                Assert.AreEqual(9, siteState.TotalDeliveries);
                Assert.AreEqual(24.5f, siteState.TotalDeliveredTons, 0.01f);
                Assert.AreEqual(15.25f, siteState.TotalLoadedTons, 0.01f);
                Assert.AreEqual(TerritoryFranchiseLevel.Preferred, siteState.FranchiseLevel);
                Assert.AreEqual(1, siteState.LoaderCount);
                Assert.AreEqual(2, siteState.MechanicCount);
                Assert.AreEqual(1, siteState.GuardCount);
                Assert.AreEqual(1, siteState.ManagerCount);
                Assert.AreEqual(1, siteState.Repossessions);
                Assert.AreEqual(3, siteState.NpcLoads);
                Assert.AreEqual(2, siteState.NpcDeliveries);
                Assert.AreEqual("Ore", siteState.LastCommodity);
                Assert.IsTrue(siteState.SiteOperatorAssigned);
                Assert.AreEqual(425f, siteState.LastPassiveIncomeAmount, 0.01f);
                Assert.AreEqual(12, siteState.LastPassiveIncomeWeekIndex);
                Assert.AreEqual("Paid last week", siteState.LastPassiveIncomeStatus);

                Assert.IsNotNull(corridorState);
                Assert.AreEqual(12, corridorState.DeliveryCount);
                Assert.AreEqual(60f, corridorState.TotalDeliveredTons, 0.01f);
                Assert.AreEqual(CorridorRightLevel.Corridor, corridorState.RightLevel);

                Assert.IsNotNull(districtState);
                Assert.AreEqual("Dominant", districtState.ReputationLabel);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithDistrictReputationDebugOffset_RestoresTerritoryReputationState()
        {
            var filePath = TestWorkspace.CreateTempFilePath("territory-reputation.state.xml");
            var sourceTerritoryManager = CreateTerritoryManager();
            var restoredTerritoryManager = CreateTerritoryManager();

            try
            {
                sourceTerritoryManager.AdjustDistrictReputationDebug("Port", 40f);

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), null, sourceTerritoryManager.CreateSnapshot());
                var rawSave = File.ReadAllText(filePath);
                var document = XDocument.Load(filePath);
                IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), restoredTerritoryManager);

                var restoredDistrict = restoredTerritoryManager.GetDistrictState("Port");
                var versionElement = document
                    .Root?
                    .Elements("Section")
                    .FirstOrDefault(section => string.Equals((string)section.Attribute("name"), "Meta", StringComparison.OrdinalIgnoreCase))?
                    .Elements("Value")
                    .FirstOrDefault(value => string.Equals((string)value.Attribute("key"), "Version", StringComparison.OrdinalIgnoreCase));
                int savedVersion;

                Assert.IsNotNull(versionElement);
                Assert.IsTrue(int.TryParse(versionElement.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out savedVersion));
                Assert.IsTrue(savedVersion >= 17);
                StringAssert.Contains(rawSave, "<Section name=\"TerritoryDistrictReputation:Port\">");
                Assert.IsNotNull(restoredDistrict);
                Assert.AreEqual(40f, restoredTerritoryManager.GetDistrictReputationDebugOffset("Port"), 0.01f);
                Assert.AreEqual("Emerging", restoredDistrict.ReputationLabel);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        private static TerritoryManager CreateTerritoryManager()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "alpha-depot",
                    CreateIndustryConfig("alpha-depot", "Alpha Depot", "Port")
                },
                {
                    "bravo-depot",
                    CreateIndustryConfig("bravo-depot", "Bravo Depot", "GrandSenora")
                },
            });
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "Port",
                    new DistrictConfig { Id = "10001", Name = "Port" }
                },
                {
                    "GrandSenora",
                    new DistrictConfig { Id = "10000", Name = "GrandSenora" }
                },
            });

            var industryManager = new IndustryManager(config);
            return new TerritoryManager(config, industryManager);
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
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                ProductionRate = 1f,
                InputCapacityTons = 1f,
                OutputCapacityTons = 1f,
                IndustryOwnerCut = 0.5f,
            };
        }

        private static void DeleteTempDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class IndustryPersistenceManagerRoundTripTests
    {
        [TestMethod]
        public void SaveAndLoad_WithPlayerStatisticsMetadata_RestoresSnapshotAndBumpsVersion()
        {
            var filePath = TestWorkspace.CreateTempFilePath("player-stats.state.ini");

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
                StringAssert.Contains(rawSave, "Version=16");
                StringAssert.Contains(rawSave, "[Successes]");

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
            var filePath = TestWorkspace.CreateTempFilePath("apartment-rental.state.ini");

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
                StringAssert.Contains(rawSave, "IsRented=true");

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
            var filePath = TestWorkspace.CreateTempFilePath("fresh-finance.state.ini");

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

                Assert.IsFalse(rawSave.Contains("[FinanceMeta]"));
                Assert.IsFalse(rawSave.Contains("[Finance:Transaction:"));

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
            var filePath = TestWorkspace.CreateTempFilePath("market.state.ini");

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
                StringAssert.Contains(rawSave, "Version=18");
                StringAssert.Contains(rawSave, "[Market:Commodity:Fuel]");

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
        public void LoadWithMetadata_WithTerritorySnapshot_AppliesSavedSiteAndCorridorState()
        {
            var filePath = TestWorkspace.CreateTempFilePath("territory.state.ini");
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
            var filePath = TestWorkspace.CreateTempFilePath("territory-reputation.state.ini");
            var sourceTerritoryManager = CreateTerritoryManager();
            var restoredTerritoryManager = CreateTerritoryManager();

            try
            {
                sourceTerritoryManager.AdjustDistrictReputationDebug("Port", 40f);

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), null, sourceTerritoryManager.CreateSnapshot());
                var rawSave = File.ReadAllText(filePath);
                IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), restoredTerritoryManager);

                var restoredDistrict = restoredTerritoryManager.GetDistrictState("Port");

                StringAssert.Contains(rawSave, "Version=17");
                StringAssert.Contains(rawSave, "[TerritoryDistrictReputation:Port]");
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
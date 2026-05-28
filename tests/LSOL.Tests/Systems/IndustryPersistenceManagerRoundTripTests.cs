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
        public void SaveAndLoad_WithWarehouseLossTelemetry_RestoresRecentStorageLossFields()
        {
            var filePath = TestWorkspace.CreateTempFilePath("warehouse-loss-telemetry.state.xml");

            try
            {
                var original = CreateIndustryConfig("warehouse-alpha", "Warehouse Alpha", "Port");
                original.SiteRole = SiteRole.Warehouse;
                original.Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ProcessedFood",
                    "Computer",
                };

                var industry = new Industry(original, new List<ProductionRecipe>(), false, 0.2f);
                industry.ApplyStoragePressureState(0.72f, 9, 3.5f, 4200f);
                industry.ApplyStorageLossTelemetryState(1, 0.5f, 225f, 0.2f, 110f, 1.5f, 900f, 0.8f, 450f);

                IndustryPersistenceManager.Save(filePath, new[] { industry }, null, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">25</Value>");
                StringAssert.Contains(rawSave, "StorageTelemetryWeekIndex");

                var restored = new Industry(original, new List<ProductionRecipe>(), false, 0.2f);
                IndustryPersistenceManager.Load(filePath, new[] { restored });

                Assert.AreEqual(1, restored.StorageTelemetryWeekIndex);
                Assert.AreEqual(0.5f, restored.LastDaySpoilageTons, 0.001f);
                Assert.AreEqual(225f, restored.LastDaySpoilageValue, 0.01f);
                Assert.AreEqual(0.2f, restored.LastDayShrinkageTons, 0.001f);
                Assert.AreEqual(110f, restored.LastDayShrinkageValue, 0.01f);
                Assert.AreEqual(1.5f, restored.CurrentWeekSpoilageTons, 0.001f);
                Assert.AreEqual(900f, restored.CurrentWeekSpoilageValue, 0.01f);
                Assert.AreEqual(0.8f, restored.CurrentWeekShrinkageTons, 0.001f);
                Assert.AreEqual(450f, restored.CurrentWeekShrinkageValue, 0.01f);
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
        public void SaveAndLoad_WithFinanceContractMetadata_RoundTripsPlayerContractCorrelationFields()
        {
            var filePath = TestWorkspace.CreateTempFilePath("finance-contract-metadata.state.xml");

            try
            {
                var financeTracker = new CompanyFinanceTracker();
                financeTracker.RecordIncome(
                    CompanyFinanceCategory.PlayerContract,
                    8425f,
                    1660,
                    "Contract delivery of Steel to Terminal",
                    0,
                    "Acme Bulk | 8.00t Steel | Alpha -> Beta",
                    "pc-778",
                    "company:acme-bulk",
                    "Terminal");

                var metadata = new IndustryPersistenceMetadata
                {
                    Finance = financeTracker.CreatePersistenceSnapshot(),
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"PlayerContractId\">pc-778</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"ShipperKey\">company:acme-bulk</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"DistrictName\">Terminal</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var transaction = result.Metadata.Finance.Transactions.Single();

                Assert.AreEqual("pc-778", transaction.PlayerContractId);
                Assert.AreEqual("company:acme-bulk", transaction.ShipperKey);
                Assert.AreEqual("Terminal", transaction.DistrictName);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithBankOfferHistory_RoundTripsCurrentOffersAndHistory()
        {
            var filePath = TestWorkspace.CreateTempFilePath("bank-history.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    BankLoans = new BankLoanPersistenceSnapshot
                    {
                        ActiveLoan = new CompanyLoanState
                        {
                            BankId = "bank-1",
                            BankName = "Union Credit",
                            OriginalPrincipal = 120000f,
                            LockedInterestRatePercent = 7.1f,
                            TotalRepayment = 128520f,
                            RemainingBalance = 85680f,
                            WeeklyInstallment = 10710f,
                            TermWeeks = 12,
                            WeeksPaid = 4,
                            LastProcessedWeekIndex = 18,
                        },
                    },
                };
                metadata.BankLoans.OfferedRates.Add(new BankOfferRateSnapshot
                {
                    BankId = "bank-1",
                    WeekIndex = 19,
                    RatePercent = 7.1f,
                });
                metadata.BankLoans.OfferHistory.Add(new BankOfferRateSnapshot
                {
                    BankId = "bank-1",
                    WeekIndex = 19,
                    RatePercent = 7.1f,
                });
                metadata.BankLoans.OfferHistory.Add(new BankOfferRateSnapshot
                {
                    BankId = "bank-1",
                    WeekIndex = 18,
                    RatePercent = 7.35f,
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">22</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"BankOffer:bank-1\">");
                StringAssert.Contains(rawSave, "<Section name=\"BankOfferHistory:bank-1:19\">");
                StringAssert.Contains(rawSave, "<Section name=\"BankOfferHistory:bank-1:18\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var snapshot = result.Metadata.BankLoans;

                Assert.IsNotNull(snapshot);
                Assert.IsNotNull(snapshot.ActiveLoan);
                Assert.AreEqual("bank-1", snapshot.ActiveLoan.BankId);
                Assert.AreEqual(1, snapshot.OfferedRates.Count);
                Assert.AreEqual(2, snapshot.OfferHistory.Count);
                Assert.AreEqual(19, snapshot.OfferHistory.Max(entry => entry.WeekIndex));
                Assert.AreEqual(7.35f, snapshot.OfferHistory.Single(entry => entry.WeekIndex == 18).RatePercent, 0.001f);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithAlertRules_RoundTripsSettingsAndBumpsVersion()
        {
            var filePath = TestWorkspace.CreateTempFilePath("alert-rules.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    AlertRules = new AlertRulesPersistenceSnapshot
                    {
                        RentLeadTime = AlertLeadTimeMode.WithinDay,
                        ContractLeadTime = AlertLeadTimeMode.DueNow,
                        FleetMode = FleetAlertMode.WatchAndCritical,
                        TerritoryMode = TerritoryAlertMode.ChargesOnly,
                    },
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">23</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"AlertRules\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var snapshot = result.Metadata.AlertRules;

                Assert.IsNotNull(snapshot);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual(AlertLeadTimeMode.WithinDay, snapshot.RentLeadTime);
                Assert.AreEqual(AlertLeadTimeMode.DueNow, snapshot.ContractLeadTime);
                Assert.AreEqual(FleetAlertMode.WatchAndCritical, snapshot.FleetMode);
                Assert.AreEqual(TerritoryAlertMode.ChargesOnly, snapshot.TerritoryMode);
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
                StringAssert.Contains(rawSave, "<Value key=\"DeliveryMomentum\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var restoredMarket = new GlobalMarketManager(10000, basePrices);
                restoredMarket.ApplyPersistenceSnapshot(result.Metadata.Market, 10000);

                Assert.IsNotNull(result.Metadata.Market);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Fuel"), restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Steel"), restoredMarket.GetUnitPrice("Steel"), 0.01f);

                sourceMarket.Update(1849999);
                restoredMarket.Update(359999);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Fuel"), restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Steel"), restoredMarket.GetUnitPrice("Steel"), 0.01f);

                sourceMarket.Update(1850000);
                restoredMarket.Update(360000);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Fuel"), restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Steel"), restoredMarket.GetUnitPrice("Steel"), 0.01f);

                sourceMarket.Update(1900000);
                restoredMarket.Update(410000);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Fuel"), restoredMarket.GetUnitPrice("Fuel"), 0.01f);
                Assert.AreEqual(sourceMarket.GetUnitPrice("Steel"), restoredMarket.GetUnitPrice("Steel"), 0.01f);
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
                        SelectedDistrictFilter = "Terminal",
                        SelectedRigClassFilter = "OpenHull",
                        SelectedSortMode = PlayerContractBoardSortMode.BestPayoutDensity,
                        SelectedExpiryFilter = PlayerContractBoardExpiryFilter.Within120Minutes,
                        SelectedPayoutDensityFilter = PlayerContractBoardPayoutDensityFilter.AtLeast500PerKm,
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
                    ReputationOutcomeApplied = true,
                    ShipperKey = "company:acme-bulk",
                    ShipperDisplayName = "Acme Bulk",
                    IsPremiumOpportunity = true,
                });
                metadata.PlayerContracts.Cooldowns.Add(new PlayerContractCooldownSnapshot
                {
                    RouteKey = "alpha|beta|steel",
                    AvailableAgainMinute = 12555,
                });
                metadata.PlayerContracts.ShipperReputations.Add(new PlayerContractShipperReputationSnapshot
                {
                    ShipperKey = "company:acme-bulk",
                    DisplayName = "Acme Bulk",
                    TrustScore = 64f,
                    CompletedContracts = 9,
                    FailedContracts = 1,
                    CleanCompletions = 7,
                    CleanStreak = 3,
                    DeliveredTons = 48f,
                    LastTierAwarded = 2,
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
                StringAssert.Contains(rawSave, "<Value key=\"Version\">24</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"PlayerContracts\">");
                StringAssert.Contains(rawSave, "<Value key=\"PlayerContractId\">pc-001</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"PlayerContractShipper:1\">");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var contracts = result.Metadata.PlayerContracts;
                var fleetVehicle = result.Metadata.OwnedFleet.Vehicles.Single();
                var commercialVehicle = result.Metadata.PropertyOwnership.CommercialVehicles.Single();

                Assert.IsNotNull(contracts);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual(7, contracts.NextContractId);
                Assert.AreEqual(12345, contracts.LastBoardRefreshMinute);
                Assert.AreEqual("Steel", contracts.SelectedCommodityFilter);
                Assert.AreEqual("Terminal", contracts.SelectedDistrictFilter);
                Assert.AreEqual("OpenHull", contracts.SelectedRigClassFilter);
                Assert.AreEqual(PlayerContractBoardSortMode.BestPayoutDensity, contracts.SelectedSortMode);
                Assert.AreEqual(PlayerContractBoardExpiryFilter.Within120Minutes, contracts.SelectedExpiryFilter);
                Assert.AreEqual(PlayerContractBoardPayoutDensityFilter.AtLeast500PerKm, contracts.SelectedPayoutDensityFilter);
                Assert.AreEqual(1, contracts.Contracts.Count);
                Assert.AreEqual("pc-001", contracts.Contracts[0].Id);
                Assert.AreEqual(PlayerContractType.FreightMarket, contracts.Contracts[0].Type);
                Assert.AreEqual(PlayerContractStatus.Loaded, contracts.Contracts[0].Status);
                Assert.AreEqual("fleet-1", contracts.Contracts[0].AssignedCommercialVehicleAssetId);
                Assert.IsTrue(contracts.Contracts[0].ReputationOutcomeApplied);
                Assert.AreEqual("company:acme-bulk", contracts.Contracts[0].ShipperKey);
                Assert.AreEqual("Acme Bulk", contracts.Contracts[0].ShipperDisplayName);
                Assert.IsTrue(contracts.Contracts[0].IsPremiumOpportunity);
                Assert.AreEqual(1, contracts.Cooldowns.Count);
                Assert.AreEqual("alpha|beta|steel", contracts.Cooldowns[0].RouteKey);
                Assert.AreEqual(1, contracts.ShipperReputations.Count);
                Assert.AreEqual("company:acme-bulk", contracts.ShipperReputations[0].ShipperKey);
                Assert.AreEqual(64f, contracts.ShipperReputations[0].TrustScore, 0.01f);

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
        public void SaveAndLoad_PlayerContractsWithoutShipperReputation_RemainsBackwardCompatible()
        {
            var filePath = TestWorkspace.CreateTempFilePath("player-contracts-legacy.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PlayerContracts = new PlayerContractsPersistenceSnapshot
                    {
                        NextContractId = 3,
                        LastBoardRefreshMinute = 120,
                    },
                };

                metadata.PlayerContracts.Contracts.Add(new PlayerContractSnapshot
                {
                    Id = "pc-legacy",
                    Type = PlayerContractType.QuickJob,
                    Status = PlayerContractStatus.Listed,
                    Commodity = "Fuel",
                    OriginIndustryId = "origin",
                    DestinationIndustryId = "destination",
                    ListedTons = 3f,
                    QuotedGrossPayout = 2100f,
                    QuotedUnitPrice = 700f,
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">21</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var contracts = result.Metadata.PlayerContracts;

                Assert.IsNotNull(contracts);
                Assert.AreEqual(1, contracts.Contracts.Count);
                Assert.AreEqual(0, contracts.ShipperReputations.Count);
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
                    CompetitivePressure = 0.42f,
                    CompetitiveOpportunity = 0.18f,
                    ActiveCompetitionJobs = 2,
                    VisibleCompetitionCount = 1,
                    CompetitiveTons = 22f,
                    CompetitiveWinCount = 1,
                    ContestedWeekStreak = 3,
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
                Assert.AreEqual(0.42f, corridorState.CompetitivePressure, 0.01f);
                Assert.AreEqual(0.18f, corridorState.CompetitiveOpportunity, 0.01f);
                Assert.AreEqual(2, corridorState.ActiveCompetitionJobs);
                Assert.AreEqual(1, corridorState.VisibleCompetitionCount);
                Assert.AreEqual(22f, corridorState.LastCompetitiveTons, 0.01f);
                Assert.AreEqual(1, corridorState.CompetitiveWinCount);
                Assert.AreEqual(3, corridorState.ContestedWeekStreak);

                Assert.IsNotNull(districtState);
                Assert.AreEqual("Dominant", districtState.ReputationLabel);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithCarrierEcosystemMetadata_RoundTripsNpcAndTerritoryCarrierFields()
        {
            var filePath = TestWorkspace.CreateTempFilePath("carrier-ecosystem.state.xml");
            var territoryManager = CreateTerritoryManager();

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    NpcLogistics = new NpcLogisticsPersistenceSnapshot
                    {
                        LastWorldEvaluationClockMinute = 180,
                    },
                };

                metadata.NpcLogistics.Carriers.Add(new NpcCarrierNetworkSnapshot
                {
                    Id = "port-freight",
                    DisplayName = "Port Freight",
                    HomeDistrict = "Port",
                    Strength = 0.61f,
                    GrowthMomentum = 0.18f,
                    DeclinePressure = 0.07f,
                    IsDormant = false,
                    DormantWeekCount = 0,
                    LastActiveWeekIndex = 3,
                    LastExpansionWeekIndex = 2,
                    VisualSeed = 11,
                });
                metadata.NpcLogistics.Carriers[0].PreferredCommodityFamilies.Add("Ore");
                metadata.NpcLogistics.Carriers[0].PreferredDistricts.Add("Port");
                metadata.NpcLogistics.Carriers[0].PreferredCorridors.Add("GrandSenora|Port");
                metadata.NpcLogistics.WorldJobs.Add(new NpcWorldLogisticsJobSnapshot
                {
                    Id = 1,
                    Type = NpcWorldJobType.RivalFreight,
                    Phase = NpcWorldJobPhase.Traveling,
                    Commodity = "Ore",
                    SourceLabel = "alpha-depot",
                    DestinationLabel = "bravo-depot",
                    OriginIndustryId = "alpha-depot",
                    DestinationIndustryId = "bravo-depot",
                    Tons = 6f,
                    RemainingInGameMinutes = 45,
                    TotalInGameMinutes = 60,
                    CreatedClockMinute = 120,
                    HasVisibleConvoy = true,
                    IsRivalJob = true,
                    CarrierId = "port-freight",
                });

                var territorySnapshot = new TerritoryPersistenceSnapshot();
                territorySnapshot.Districts.Add(new TerritoryDistrictSnapshot
                {
                    DistrictName = "Port",
                    CompetitivePressure = 0.28f,
                    CompetitiveOpportunity = 0.12f,
                    ActiveCompetitionJobs = 2,
                    ActiveCarrierCount = 2,
                    DominantCarrierId = "port-freight",
                    DominantCarrierName = "Port Freight",
                });
                territorySnapshot.Corridors.Add(new TerritoryCorridorSnapshot
                {
                    DistrictA = "Port",
                    DistrictB = "GrandSenora",
                    CompetitivePressure = 0.31f,
                    CompetitiveOpportunity = 0.16f,
                    ActiveCompetitionJobs = 2,
                    ActiveCarrierCount = 1,
                    DominantCarrierId = "port-freight",
                    DominantCarrierName = "Port Freight",
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, territorySnapshot);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Value key=\"Version\">27</Value>");
                StringAssert.Contains(rawSave, "<Section name=\"NpcCarrier:port-freight\">");
                StringAssert.Contains(rawSave, "<Value key=\"CarrierId\">port-freight</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"DominantCarrierName\">Port Freight</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), territoryManager);
                var npcLogistics = result.Metadata.NpcLogistics;
                var restoredDistrict = territoryManager.GetDistrictState("Port");
                var restoredCorridor = territoryManager.GetCorridorState("Port", "GrandSenora");

                Assert.IsNotNull(npcLogistics);
                Assert.AreEqual(1, npcLogistics.Carriers.Count);
                Assert.AreEqual("port-freight", npcLogistics.Carriers[0].Id);
                Assert.AreEqual("Port Freight", npcLogistics.Carriers[0].DisplayName);
                CollectionAssert.AreEqual(new[] { "Ore" }, npcLogistics.Carriers[0].PreferredCommodityFamilies.ToArray());
                Assert.AreEqual("port-freight", npcLogistics.WorldJobs[0].CarrierId);

                Assert.IsNotNull(restoredDistrict);
                Assert.AreEqual(2, restoredDistrict.ActiveCarrierCount);
                Assert.AreEqual("port-freight", restoredDistrict.DominantCarrierId);
                Assert.AreEqual("Port Freight", restoredDistrict.DominantCarrierName);

                Assert.IsNotNull(restoredCorridor);
                Assert.AreEqual(1, restoredCorridor.ActiveCarrierCount);
                Assert.AreEqual("port-freight", restoredCorridor.DominantCarrierId);
                Assert.AreEqual("Port Freight", restoredCorridor.DominantCarrierName);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void LoadWithMetadata_WithLegacyCorridorSnapshot_MissingRivalryFieldsDefaultsToZero()
        {
            var filePath = TestWorkspace.CreateTempFilePath("territory-legacy-corridor.state.xml");
            var territoryManager = CreateTerritoryManager();

            try
            {
                var territorySnapshot = new TerritoryPersistenceSnapshot();
                territorySnapshot.Corridors.Add(new TerritoryCorridorSnapshot
                {
                    DistrictA = "Port",
                    DistrictB = "GrandSenora",
                    DeliveryCount = 8,
                    TotalDeliveredTons = 42f,
                    RightLevel = CorridorRightLevel.ServicePermit,
                    CurrentWeekDeliveryCount = 1,
                    CurrentWeekDeliveredTons = 8f,
                    DecayPressure = 0.4f,
                    CompetitivePressure = 0.35f,
                    CompetitiveOpportunity = 0.14f,
                    ActiveCompetitionJobs = 2,
                    VisibleCompetitionCount = 1,
                    CompetitiveTons = 18f,
                    CompetitiveWinCount = 1,
                    ContestedWeekStreak = 2,
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), null, territorySnapshot);

                var document = XDocument.Load(filePath);
                var corridorSection = document
                    .Root?
                    .Elements("Section")
                    .FirstOrDefault(section => string.Equals((string)section.Attribute("name"), "TerritoryCorridor:GrandSenora|Port", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(corridorSection);

                var rivalryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "CompetitivePressure",
                    "CompetitiveOpportunity",
                    "ActiveCompetitionJobs",
                    "VisibleCompetitionCount",
                    "CompetitiveTons",
                    "CompetitiveWinCount",
                    "ContestedWeekStreak",
                };

                corridorSection
                    .Elements("Value")
                    .Where(value => rivalryKeys.Contains((string)value.Attribute("key") ?? string.Empty))
                    .Remove();
                document.Save(filePath);

                IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), territoryManager);

                var corridorState = territoryManager.GetCorridorState("Port", "GrandSenora");
                Assert.IsNotNull(corridorState);
                Assert.AreEqual(0f, corridorState.CompetitivePressure, 0.01f);
                Assert.AreEqual(0f, corridorState.CompetitiveOpportunity, 0.01f);
                Assert.AreEqual(0, corridorState.ActiveCompetitionJobs);
                Assert.AreEqual(0, corridorState.VisibleCompetitionCount);
                Assert.AreEqual(0f, corridorState.LastCompetitiveTons, 0.01f);
                Assert.AreEqual(0, corridorState.CompetitiveWinCount);
                Assert.AreEqual(0, corridorState.ContestedWeekStreak);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithTerritoryDistrictEvent_RestoresLiveDistrictEventState()
        {
            var filePath = TestWorkspace.CreateTempFilePath("territory-event.state.xml");
            var sourceTerritoryManager = CreateTerritoryManager();
            var restoredTerritoryManager = CreateTerritoryManager();

            try
            {
                sourceTerritoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
                {
                    Districts =
                    {
                        new TerritoryDistrictSnapshot
                        {
                            DistrictName = "Port",
                            ActiveEvent = new TerritoryDistrictEventSnapshot
                            {
                                EventId = "district_event_port_3",
                                DistrictName = "Port",
                                CrisisType = DistrictCrisisType.SupplyDisruption,
                                PreferredCommodity = "MechanicalParts",
                                Severity = 0.58f,
                                MarketPressureBonus = 0.21f,
                                ResponseTargetTons = 24f,
                                DeliveredReliefTons = 9f,
                                ReliefDeliveryCount = 2,
                                StartedWeekIndex = 3,
                                EndsAtWeekIndex = 4,
                                LastEscalatedWeekIndex = 3,
                                TriggerSummary = "Competition 58% | Contested lanes 2 | Operational gap 25%",
                                ImpactSummary = "Competition and site outages are choking throughput.",
                            },
                        },
                    },
                });

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), null, sourceTerritoryManager.CreateSnapshot());
                var rawSave = File.ReadAllText(filePath);
                IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), restoredTerritoryManager);

                var restoredEvent = restoredTerritoryManager.GetDistrictEvent("Port");

                StringAssert.Contains(rawSave, "<Section name=\"TerritoryDistrict:Port\">");
                StringAssert.Contains(rawSave, "<Value key=\"ActiveEventId\">district_event_port_3</Value>");
                Assert.IsNotNull(restoredEvent);
                Assert.AreEqual(DistrictCrisisType.SupplyDisruption, restoredEvent.CrisisType);
                Assert.AreEqual("MechanicalParts", restoredEvent.PreferredCommodity);
                Assert.AreEqual(24f, restoredEvent.ResponseTargetTons, 0.01f);
                Assert.AreEqual(9f, restoredEvent.DeliveredReliefTons, 0.01f);
                Assert.AreEqual(2, restoredEvent.ReliefDeliveryCount);
                Assert.AreEqual(4, restoredEvent.EndsAtWeekIndex);
                Assert.AreEqual("Competition 58% | Contested lanes 2 | Operational gap 25%", restoredEvent.TriggerSummary);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void SaveAndLoad_WithOfficeFacilityAnchorAssignment_RestoresPropertyOfficeObjectAnchorId()
        {
            var filePath = TestWorkspace.CreateTempFilePath("property-office-anchor.state.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    PropertyOwnership = new PropertyOwnershipPersistenceSnapshot
                    {
                        ActiveOfficeId = "main",
                        Offices =
                        {
                            new OfficeOwnershipPersistenceEntry
                            {
                                OfficeId = "main",
                                IsOwned = true,
                                LastChargedWeekIndex = 2,
                            },
                        },
                        OfficeObjects =
                        {
                            new OfficeObjectPersistenceEntry
                            {
                                InstanceId = "office_obj_dispatch_1",
                                OfficeId = "main",
                                DefinitionId = 15,
                                IsPlaced = true,
                                Position = new Vector3(1f, 2f, 3f),
                                Rotation = new Vector3(0f, 0f, 90f),
                                AssignedFacilityAnchorId = "dispatch-main",
                                StoredResourceAmount = 0f,
                            },
                        },
                    },
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata);
                var rawSave = File.ReadAllText(filePath);
                var loaded = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>(), null);

                StringAssert.Contains(rawSave, "<Value key=\"AssignedFacilityAnchorId\">dispatch-main</Value>");
                Assert.IsNotNull(loaded.Metadata.PropertyOwnership);
                Assert.AreEqual(1, loaded.Metadata.PropertyOwnership.OfficeObjects.Count);
                Assert.AreEqual("dispatch-main", loaded.Metadata.PropertyOwnership.OfficeObjects[0].AssignedFacilityAnchorId);
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
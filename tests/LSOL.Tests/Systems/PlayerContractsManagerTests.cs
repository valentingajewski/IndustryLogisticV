using System;
using System.Collections.Generic;
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
    public sealed class PlayerContractsManagerTests
    {
        [TestMethod]
        public void GetListings_WithBoardFilters_AppliesSelectorsAndBuildsDerivedSummaryFields()
        {
            var manager = CreateBoardTestManager();
            SetListedContracts(
                manager,
                CreateListedContract("quick-fuel", PlayerContractType.QuickJob, "Fuel", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.65f),
                CreateListedContract("quick-ore", PlayerContractType.QuickJob, "Ore", "ore_port", "ore_davis", 4f, 4000f, 2400f, 180, 0.82f),
                CreateListedContract("freight-steel", PlayerContractType.FreightMarket, "Steel", "steel_terminal", "steel_port", 8f, 6000f, 7200f, 400, 0.91f));
            SetBoardQuery(
                manager,
                PlayerContractBoardSortMode.Board,
                PlayerContractBoardExpiryFilter.Within60Minutes,
                PlayerContractBoardPayoutDensityFilter.AtLeast1000PerKm,
                "Liquid",
                "Port",
                "Fuel");

            var listings = manager.GetListings(PlayerContractType.QuickJob);
            var overview = manager.GetOverview();

            Assert.AreEqual(1, listings.Count);
            Assert.AreEqual("quick-fuel", listings[0].Id);
            Assert.AreEqual("Port", listings[0].OriginDistrictName);
            Assert.AreEqual("Airport", listings[0].DestinationDistrictName);
            Assert.AreEqual("Port -> Airport", listings[0].RouteDistrictLabel);
            Assert.AreEqual("Liquid", listings[0].RigClassLabel);
            Assert.AreEqual(50, listings[0].RemainingExpiryMinutes);
            Assert.AreEqual(1500f, listings[0].PayoutDensityValue, 0.01f);
            Assert.AreEqual("$1,500.00/km", listings[0].PayoutDensityLabel);

            Assert.AreEqual(1, overview.QuickJobCount);
            Assert.AreEqual(0, overview.FreightMarketCount);
            Assert.AreEqual("Board", overview.SelectedSortMode);
            Assert.AreEqual("<= 60m", overview.SelectedExpiryFilter);
            Assert.AreEqual(">= $1,000/km", overview.SelectedPayoutDensityFilter);
            Assert.AreEqual("Liquid", overview.SelectedRigClassFilter);
            Assert.AreEqual("Port", overview.SelectedDistrictFilter);
            Assert.AreEqual("Fuel", overview.SelectedCommodityFilter);
            StringAssert.Contains(overview.BoardDetail, "Density >= $1,000/km");
        }

        [TestMethod]
        public void GetListings_WithSelectedSortMode_ReordersContractsByRequestedMetric()
        {
            var manager = CreateBoardTestManager();
            SetListedContracts(
                manager,
                CreateListedContract("job-a", PlayerContractType.QuickJob, "Ore", "ore_port", "ore_davis", 4f, 4000f, 2400f, 220, 0.80f),
                CreateListedContract("job-b", PlayerContractType.QuickJob, "Fuel", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.60f),
                CreateListedContract("job-c", PlayerContractType.QuickJob, "Steel", "steel_terminal", "steel_port", 8f, 6000f, 7200f, 320, 0.90f));

            SetBoardQuery(manager, PlayerContractBoardSortMode.ExpirySoonest, PlayerContractBoardExpiryFilter.Any, PlayerContractBoardPayoutDensityFilter.Any);
            CollectionAssert.AreEqual(new[] { "job-b", "job-a", "job-c" }, manager.GetListings(PlayerContractType.QuickJob).Select(listing => listing.Id).ToArray());

            SetBoardQuery(manager, PlayerContractBoardSortMode.BestPayoutDensity, PlayerContractBoardExpiryFilter.Any, PlayerContractBoardPayoutDensityFilter.Any);
            CollectionAssert.AreEqual(new[] { "job-b", "job-c", "job-a" }, manager.GetListings(PlayerContractType.QuickJob).Select(listing => listing.Id).ToArray());

            SetBoardQuery(manager, PlayerContractBoardSortMode.HighestGrossPayout, PlayerContractBoardExpiryFilter.Any, PlayerContractBoardPayoutDensityFilter.Any);
            CollectionAssert.AreEqual(new[] { "job-c", "job-b", "job-a" }, manager.GetListings(PlayerContractType.QuickJob).Select(listing => listing.Id).ToArray());
        }

        [TestMethod]
        public void ForceRefreshBoard_WithInvalidPersistedDynamicFilters_FallsBackToAnyWhileKeepingFixedSelectors()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "ore_pit", CreateOriginIndustryConfig() },
                { "steel_sink", CreateDestinationIndustryConfig() },
            });
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>
            {
                CreateVehicleDefinition("quick-rigid", "Starter Flatbed", "mule", VehicleCargoType.Aggregates, 5f, false, false, "Ore"),
                CreateVehicleDefinition("freight-rigid", "Company Tipper", "pounder", VehicleCargoType.Aggregates, 12f, false, false, "Ore"),
            });

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ore", 900f },
            });

            industryManager.Industries[0].AddOutput("Ore", 18f);

            var manager = new PlayerContractsManager(
                industryManager,
                fleetManager,
                null,
                market,
                position => position,
                null,
                () => 120,
                null,
                null,
                null,
                new Random(7));

            manager.ApplyPersistenceSnapshot(new PlayerContractsPersistenceSnapshot
            {
                SelectedCommodityFilter = "Steel",
                SelectedDistrictFilter = "Airport",
                SelectedRigClassFilter = "Liquid",
                SelectedSortMode = PlayerContractBoardSortMode.BestPayoutDensity,
                SelectedExpiryFilter = PlayerContractBoardExpiryFilter.Within120Minutes,
                SelectedPayoutDensityFilter = PlayerContractBoardPayoutDensityFilter.AtLeast500PerKm,
            });

            manager.ForceRefreshBoard(120);
            var overview = manager.GetOverview();

            Assert.AreEqual("Any", overview.SelectedCommodityFilter);
            Assert.AreEqual("Any", overview.SelectedDistrictFilter);
            Assert.AreEqual("Any", overview.SelectedRigClassFilter);
            Assert.AreEqual("Best $/km", overview.SelectedSortMode);
            Assert.AreEqual("<= 120m", overview.SelectedExpiryFilter);
            Assert.AreEqual(">= $500/km", overview.SelectedPayoutDensityFilter);
        }

        [TestMethod]
        public void ForceRefreshBoard_WithRealSurplusAndDemand_GeneratesContractsFromLiveIndustryState()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "ore_pit", CreateOriginIndustryConfig() },
                { "steel_sink", CreateDestinationIndustryConfig() },
            });
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>
            {
                CreateVehicleDefinition("quick-rigid", "Starter Flatbed", "mule", VehicleCargoType.Aggregates, 5f, false, false, "Ore"),
                CreateVehicleDefinition("freight-rigid", "Company Tipper", "pounder", VehicleCargoType.Aggregates, 12f, false, false, "Ore"),
            });

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ore", 900f },
            });

            var origin = industryManager.Industries[0];
            var destination = industryManager.Industries[1];
            origin.AddOutput("Ore", 18f);

            var manager = new PlayerContractsManager(
                industryManager,
                fleetManager,
                null,
                market,
                position => position,
                null,
                () => 120,
                null,
                null,
                null,
                new Random(7));

            manager.ForceRefreshBoard(120);

            var quickJobs = manager.GetListings(PlayerContractType.QuickJob);
            var freightJobs = manager.GetListings(PlayerContractType.FreightMarket);

            Assert.IsTrue(quickJobs.Count > 0, "Expected at least one Quick Job listing.");
            Assert.IsTrue(freightJobs.Count > 0, "Expected at least one Freight Market listing.");
            Assert.IsTrue(ContainsRoute(quickJobs, "ore_pit", "steel_sink", "Ore"), "Quick Job board should include the live ore route.");
            Assert.IsTrue(ContainsRoute(freightJobs, "ore_pit", "steel_sink", "Ore"), "Freight Market board should include the live ore route.");
        }

        [TestMethod]
        public void GetListings_WithLiveDistrictEvent_AddsEventStatusAndBoostsEstimatedPayout()
        {
            var baselineManager = CreateBoardTestManagerWithDistrictEvent(null);
            var eventManager = CreateBoardTestManagerWithDistrictEvent(new TerritoryDistrictEventSnapshot
            {
                EventId = "district_event_airport_2",
                DistrictName = "Airport",
                CrisisType = DistrictCrisisType.FuelShortage,
                PreferredCommodity = "Fuel",
                Severity = 0.78f,
                MarketPressureBonus = 0.18f,
                ResponseTargetTons = 10f,
                DeliveredReliefTons = 2f,
                ReliefDeliveryCount = 1,
                StartedWeekIndex = 1,
                EndsAtWeekIndex = 2,
                TriggerSummary = "Fuel pressure 41% | Gas sites 1 | At-risk stops 1",
                ImpactSummary = "Fuel stops and service lanes need relief cargo.",
            });

            SetListedContracts(baselineManager, CreateListedContract("quick-fuel", PlayerContractType.QuickJob, "Fuel", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.65f));
            SetListedContracts(eventManager, CreateListedContract("quick-fuel", PlayerContractType.QuickJob, "Fuel", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.65f));

            var baselineListing = baselineManager.GetListings(PlayerContractType.QuickJob).Single();
            var eventListing = eventManager.GetListings(PlayerContractType.QuickJob).Single();

            Assert.IsTrue(eventListing.CurrentEstimatedGrossPayout > baselineListing.CurrentEstimatedGrossPayout, "Expected the live Airport fuel event to raise the current estimated payout.");
            StringAssert.Contains(eventListing.StatusDetail, "Event");
            StringAssert.Contains(eventListing.StatusDetail, "Airport fuel shortage");
        }

        [TestMethod]
        public void GetListings_WithLiveDistrictEventAlternateCommodity_UsesSharedCommoditySemantics()
        {
            try
            {
                CommodityCatalog.Configure(
                    new[]
                    {
                        new ResourceGroupConfig
                        {
                            Name = "Liquid",
                            CargoType = VehicleCargoType.Liquid,
                            Commodities = new List<string> { "Fuel", "Oil" },
                        },
                    },
                    new[]
                    {
                        new ExternalResourceConfig
                        {
                            Commodity = "Fuel",
                            CargoType = VehicleCargoType.Liquid,
                            EconomySemantics = new CommodityEconomySemantics
                            {
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief", "EmergencyRelief" },
                                Substitutes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Oil", 0.82f } },
                                EventResponseAffinity = 1.2f,
                            },
                        },
                        new ExternalResourceConfig
                        {
                            Commodity = "Oil",
                            CargoType = VehicleCargoType.Liquid,
                            EconomySemantics = new CommodityEconomySemantics
                            {
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief" },
                                EventResponseAffinity = 0.95f,
                            },
                        },
                    });

                var basePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 700f },
                    { "Oil", 380f },
                };
                var eventSnapshot = new TerritoryDistrictEventSnapshot
                {
                    EventId = "district_event_airport_7",
                    DistrictName = "Airport",
                    CrisisType = DistrictCrisisType.FuelShortage,
                    PreferredCommodity = "Fuel",
                    Severity = 0.76f,
                    MarketPressureBonus = 0.18f,
                    ResponseTargetTons = 10f,
                    DeliveredReliefTons = 2f,
                    ReliefDeliveryCount = 1,
                    StartedWeekIndex = 1,
                    EndsAtWeekIndex = 2,
                    TriggerSummary = "Fuel pressure 39% | Gas sites 1 | At-risk stops 1",
                    ImpactSummary = "Fuel stops and service lanes need relief cargo.",
                };

                var baselineManager = CreateBoardTestManagerWithDistrictEvent(null, basePrices);
                var eventManager = CreateBoardTestManagerWithDistrictEvent(eventSnapshot, basePrices);

                SetListedContracts(baselineManager, CreateListedContract("quick-oil", PlayerContractType.QuickJob, "Oil", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.65f));
                SetListedContracts(eventManager, CreateListedContract("quick-oil", PlayerContractType.QuickJob, "Oil", "fuel_port", "fuel_airport", 3f, 2000f, 3000f, 170, 0.65f));

                var baselineListing = baselineManager.GetListings(PlayerContractType.QuickJob).Single();
                var eventListing = eventManager.GetListings(PlayerContractType.QuickJob).Single();

                Assert.IsTrue(eventListing.CurrentEstimatedGrossPayout > baselineListing.CurrentEstimatedGrossPayout, "Configured fuel-relief alternates should receive district-event pricing pressure.");
                StringAssert.Contains(eventListing.StatusDetail, "Event");
            }
            finally
            {
                LoadRepoConfig();
            }
        }

        [TestMethod]
        public void ComputeContractGrossPayout_StaysBelowNormalHaulingAndKeepsQuickJobsLowest()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "ore_sink", CreateSinkIndustryConfig() },
            });

            var industryManager = new IndustryManager(config);
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ore", 1000f },
            });

            var sink = industryManager.Industries[0];
            var normal = industryManager.ComputeDeliveryProfit(sink, "Ore", 4f, market, 0);
            var freight = PlayerContractsManager.ComputeContractGrossPayout(PlayerContractType.FreightMarket, 1000f, 0.85f, 8000f, 4f, 1f);
            var quick = PlayerContractsManager.ComputeContractGrossPayout(PlayerContractType.QuickJob, 1000f, 0.85f, 8000f, 4f, 1f);

            Assert.IsTrue(quick > 0f, "Quick Job payout should be positive.");
            Assert.IsTrue(freight > quick, "Freight Market should pay more than Quick Jobs for the same lane.");
            Assert.IsTrue(normal > freight, "Normal hauling should still outpay Freight Market on a strong sink delivery.");
        }

        [TestMethod]
        public void ResolveShipperIdentity_UsesCompanyAndFallsBackToNormalizedSiteKey()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

            var origin = CreateIndustryConfig("origin_hub", "Origin Hub", "Port", new Vector3(0f, 0f, 0f), null, new[] { "Ore" });
            origin.Company = "Acme Bulk LLC";
            var fallbackOrigin = CreateIndustryConfig("origin_fallback", "Fallback Yard", "Port", new Vector3(300f, 0f, 0f), null, new[] { "Ore" });
            var destination = CreateIndustryConfig("destination_yard", "Destination Yard", "Terminal", new Vector3(2500f, 0f, 0f), new[] { "Ore" }, null);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { origin.Id, origin },
                { fallbackOrigin.Id, fallbackOrigin },
                { destination.Id, destination },
            });

            var industryManager = new IndustryManager(config);
            var originIndustry = industryManager.Industries.Single(industry => string.Equals(industry.Id, "origin_hub", StringComparison.OrdinalIgnoreCase));
            var fallbackOriginIndustry = industryManager.Industries.Single(industry => string.Equals(industry.Id, "origin_fallback", StringComparison.OrdinalIgnoreCase));
            var resolveShipperKey = typeof(PlayerContractsManager).GetMethod("ResolveShipperKey", BindingFlags.Static | BindingFlags.NonPublic);
            var resolveShipperDisplay = typeof(PlayerContractsManager).GetMethod("ResolveShipperDisplayName", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(resolveShipperKey, "ResolveShipperKey");
            Assert.IsNotNull(resolveShipperDisplay, "ResolveShipperDisplayName");

            var companyKey = (string)resolveShipperKey.Invoke(null, new object[] { originIndustry, "origin_hub", null });
            var companyDisplay = (string)resolveShipperDisplay.Invoke(null, new object[] { originIndustry, "origin_hub", null });

            Assert.AreEqual("company:acme-bulk-llc", companyKey);
            Assert.AreEqual("Acme Bulk LLC", companyDisplay);

            var fallbackKey = (string)resolveShipperKey.Invoke(null, new object[] { fallbackOriginIndustry, "origin_fallback", null });
            var fallbackDisplay = (string)resolveShipperDisplay.Invoke(null, new object[] { fallbackOriginIndustry, "origin_fallback", null });

            Assert.AreEqual("site:origin-fallback", fallbackKey);
            Assert.AreEqual("Fallback Yard", fallbackDisplay);
        }

        [TestMethod]
        public void ApplyContractOutcome_UpdatesShipperTrustForCleanAndFailedRuns()
        {
            var manager = CreateBoardTestManager();
            var applyContractOutcome = typeof(PlayerContractsManager).GetMethod("ApplyContractOutcome", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(applyContractOutcome, "ApplyContractOutcome");

            manager.ApplyPersistenceSnapshot(new PlayerContractsPersistenceSnapshot
            {
                ShipperReputations =
                {
                    new PlayerContractShipperReputationSnapshot
                    {
                        ShipperKey = "company:acme-bulk",
                        DisplayName = "Acme Bulk",
                        TrustScore = 30f,
                    },
                },
            });

            var completedContract = new PlayerContractEntry
            {
                Id = "pc-outcome-complete",
                OriginIndustryId = "ore_port",
                DestinationIndustryId = "ore_davis",
                Commodity = "Ore",
                ListedTons = 8f,
                DeliveredTons = 8f,
                CargoCondition = 1f,
                TotalLostTons = 0f,
                ShipperKey = "company:acme-bulk",
                ShipperDisplayName = "Acme Bulk",
            };

            var completionMessage = (string)applyContractOutcome.Invoke(manager, new object[] { completedContract, PlayerContractStatus.Completed });
            var trustAfterCompletion = manager.CreatePersistenceSnapshot()
                .ShipperReputations
                .Single(entry => string.Equals(entry.ShipperKey, "company:acme-bulk", StringComparison.OrdinalIgnoreCase))
                .TrustScore;

            Assert.IsTrue(completedContract.ReputationOutcomeApplied);
            Assert.IsFalse(string.IsNullOrWhiteSpace(completionMessage), "Expected completion outcome to award a trust tier message.");
            Assert.IsTrue(trustAfterCompletion > 30f, "Clean completion should increase trust score.");

            var failedContract = new PlayerContractEntry
            {
                Id = "pc-outcome-expired",
                OriginIndustryId = "ore_port",
                DestinationIndustryId = "ore_davis",
                Commodity = "Ore",
                ListedTons = 5f,
                DeliveredTons = 0f,
                CargoCondition = 0.8f,
                TotalLostTons = 0f,
                ShipperKey = "company:acme-bulk",
                ShipperDisplayName = "Acme Bulk",
            };

            applyContractOutcome.Invoke(manager, new object[] { failedContract, PlayerContractStatus.Expired });
            var trustAfterFailure = manager.CreatePersistenceSnapshot()
                .ShipperReputations
                .Single(entry => string.Equals(entry.ShipperKey, "company:acme-bulk", StringComparison.OrdinalIgnoreCase))
                .TrustScore;

            Assert.IsTrue(failedContract.ReputationOutcomeApplied);
            Assert.IsTrue(trustAfterFailure < trustAfterCompletion, "Expired contracts should reduce trust score.");
        }

        [TestMethod]
        public void ForceRefreshBoard_PremiumRoutesRequireDistrictAndShipperTrustGates()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "Port", new DistrictConfig { Id = "port", Name = "Port" } },
                { "Terminal", new DistrictConfig { Id = "terminal", Name = "Terminal" } },
            });

            var premiumOrigin = CreateIndustryConfig("premium_origin", "Premium Origin", "Port", new Vector3(0f, 0f, 0f), null, new[] { "Ore" });
            premiumOrigin.Company = "Acme Bulk";
            premiumOrigin.OutputCapacityTons = 40f;
            var premiumDestination = CreateIndustryConfig("premium_destination", "Premium Destination", "Terminal", new Vector3(17000f, 0f, 0f), new[] { "Ore" }, null);
            premiumDestination.InputCapacityTons = 40f;

            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { premiumOrigin.Id, premiumOrigin },
                { premiumDestination.Id, premiumDestination },
            });
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>());

            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var territoryManager = new TerritoryManager(config, industryManager);
            territoryManager.SetCorridorRestrictionEnabled(false);
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ore", 2500f },
            });

            industryManager.Industries.Single(industry => string.Equals(industry.Id, "premium_origin", StringComparison.OrdinalIgnoreCase)).AddOutput("Ore", 30f);

            var manager = new PlayerContractsManager(
                industryManager,
                fleetManager,
                null,
                market,
                position => position,
                null,
                () => 120,
                null,
                territoryManager,
                null,
                new Random(7),
                null);

            manager.ForceRefreshBoard(120);
            var lockedFreightListings = manager.GetListings(PlayerContractType.FreightMarket);

            Assert.AreEqual(0, lockedFreightListings.Count, "Premium route should stay locked before district and shipper gates are satisfied.");

            territoryManager.ApplyDistrictReputationDebugStateToAll("DOMINANT");
            manager.ApplyPersistenceSnapshot(new PlayerContractsPersistenceSnapshot
            {
                ShipperReputations =
                {
                    new PlayerContractShipperReputationSnapshot
                    {
                        ShipperKey = "company:acme-bulk",
                        DisplayName = "Acme Bulk",
                        TrustScore = 60f,
                    },
                },
            });

            manager.ForceRefreshBoard(121);
            var unlockedFreightListings = manager.GetListings(PlayerContractType.FreightMarket);

            Assert.IsTrue(unlockedFreightListings.Count > 0, "Premium route should appear once district standing and trust gates are met.");
            Assert.IsTrue(unlockedFreightListings.Any(listing => listing.IsPremiumOpportunity), "Expected at least one unlocked premium listing.");
        }

        private static bool ContainsRoute(IReadOnlyList<PlayerContractListingSummary> listings, string originId, string destinationId, string commodity)
        {
            for (int i = 0; i < listings.Count; i++)
            {
                var listing = listings[i];
                if (listing == null)
                {
                    continue;
                }

                if (string.Equals(listing.OriginIndustryId, originId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(listing.DestinationIndustryId, destinationId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(listing.Commodity, commodity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static PlayerContractsManager CreateBoardTestManager()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>());
            SetProperty(config, nameof(ModConfig.IndustryConfigs), CreateBoardTestIndustryConfigs());

            return new PlayerContractsManager(
                new IndustryManager(config),
                new FleetManager(config),
                null,
                null,
                position => position,
                null,
                () => 120,
                null,
                null,
                null,
                new Random(7));
        }

        private static PlayerContractsManager CreateBoardTestManagerWithDistrictEvent(TerritoryDistrictEventSnapshot activeEvent, IReadOnlyDictionary<string, float> marketBasePrices = null)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "Port", new DistrictConfig { Id = "10001", Name = "Port" } },
                { "Davis", new DistrictConfig { Id = "10002", Name = "Davis" } },
                { "Airport", new DistrictConfig { Id = "10003", Name = "Airport" } },
                { "Terminal", new DistrictConfig { Id = "10004", Name = "Terminal" } },
            });
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>());
            SetProperty(config, nameof(ModConfig.IndustryConfigs), CreateBoardTestIndustryConfigs());

            var industryManager = new IndustryManager(config);
            industryManager.Industries.Single(industry => string.Equals(industry.Id, "fuel_port", StringComparison.OrdinalIgnoreCase)).AddOutput("Fuel", 12f);

            var territoryManager = new TerritoryManager(config, industryManager);
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Airport",
                        LicenseStatus = DistrictLicenseStatus.Active,
                        ActiveEvent = activeEvent,
                    },
                },
            });

            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fuel", 700f },
            });
            if (marketBasePrices != null)
            {
                market = new GlobalMarketManager(0, marketBasePrices);
            }
            if (activeEvent != null)
            {
                market.ConfigureShockContext(() => 120, () => new[] { territoryManager.GetDistrictState("Airport") });
                market.Update(0);
            }

            return new PlayerContractsManager(
                industryManager,
                new FleetManager(config),
                null,
                market,
                position => position,
                null,
                () => 120,
                null,
                territoryManager,
                null,
                new Random(7));
        }

        private static Dictionary<string, IndustryConfig> CreateBoardTestIndustryConfigs()
        {
            return new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "ore_port", CreateIndustryConfig("ore_port", "Ore Port", "Port", new Vector3(0f, 0f, 0f), null, new[] { "Ore" }) },
                { "ore_davis", CreateIndustryConfig("ore_davis", "Ore Davis", "Davis", new Vector3(4000f, 0f, 0f), new[] { "Ore" }, null) },
                { "fuel_port", CreateIndustryConfig("fuel_port", "Fuel Port", "Port", new Vector3(0f, 1000f, 0f), null, new[] { "Fuel" }) },
                { "fuel_airport", CreateIndustryConfig("fuel_airport", "Fuel Airport", "Airport", new Vector3(2000f, 1000f, 0f), new[] { "Fuel" }, null) },
                { "steel_terminal", CreateIndustryConfig("steel_terminal", "Steel Terminal", "Terminal", new Vector3(0f, 2000f, 0f), null, new[] { "Steel" }) },
                { "steel_port", CreateIndustryConfig("steel_port", "Steel Port", "Port", new Vector3(6000f, 2000f, 0f), new[] { "Steel" }, null) },
            };
        }

        private static IndustryConfig CreateIndustryConfig(string id, string name, string districtName, Vector3 position, IEnumerable<string> inputs, IEnumerable<string> outputs)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = name,
                DistrictName = districtName,
                Position = position,
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                InputCapacityTons = 20f,
                OutputCapacityTons = 20f,
                Inputs = new HashSet<string>(inputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(outputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            };
        }

        private static PlayerContractEntry CreateListedContract(string id, PlayerContractType type, string commodity, string originId, string destinationId, float tons, float routeDistanceMeters, float quotedGrossPayout, int expiryMinute, float quotedImbalanceScore)
        {
            return new PlayerContractEntry
            {
                Id = id,
                Type = type,
                Status = PlayerContractStatus.Listed,
                Commodity = commodity,
                OriginIndustryId = originId,
                DestinationIndustryId = destinationId,
                ListedTons = tons,
                RouteDistanceMeters = routeDistanceMeters,
                QuotedGrossPayout = quotedGrossPayout,
                QuotedImbalanceScore = quotedImbalanceScore,
                ListedAtMinute = 120,
                ExpiryMinute = expiryMinute,
                VehicleRequirementLabel = "Any compatible rig",
                SuppliesVehicle = type == PlayerContractType.QuickJob,
                RequiresOwnedVehicle = type != PlayerContractType.QuickJob,
            };
        }

        private static void SetListedContracts(PlayerContractsManager manager, params PlayerContractEntry[] contracts)
        {
            var field = typeof(PlayerContractsManager).GetField("_listedContracts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_listedContracts");

            var list = field.GetValue(manager) as List<PlayerContractEntry>;
            Assert.IsNotNull(list, "_listedContracts value");

            list.Clear();
            if (contracts != null)
            {
                list.AddRange(contracts);
            }
        }

        private static void SetBoardQuery(
            PlayerContractsManager manager,
            PlayerContractBoardSortMode sortMode,
            PlayerContractBoardExpiryFilter expiryFilter,
            PlayerContractBoardPayoutDensityFilter payoutDensityFilter,
            string rigClassFilter = "",
            string districtFilter = "",
            string commodityFilter = "")
        {
            SetField(manager, "_selectedSortMode", sortMode);
            SetField(manager, "_selectedExpiryFilter", expiryFilter);
            SetField(manager, "_selectedPayoutDensityFilter", payoutDensityFilter);
            SetField(manager, "_selectedRigClassFilter", rigClassFilter ?? string.Empty);
            SetField(manager, "_selectedDistrictFilter", districtFilter ?? string.Empty);
            SetField(manager, "_selectedCommodityFilter", commodityFilter ?? string.Empty);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static IndustryConfig CreateOriginIndustryConfig()
        {
            return new IndustryConfig
            {
                Id = "ore_pit",
                LegacyKey = "ore_pit",
                Name = "Ore Pit",
                DistrictName = "Port",
                Position = new Vector3(0f, 0f, 0f),
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                InputCapacityTons = 6f,
                OutputCapacityTons = 24f,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
            };
        }

        private static IndustryConfig CreateDestinationIndustryConfig()
        {
            return new IndustryConfig
            {
                Id = "steel_sink",
                LegacyKey = "steel_sink",
                Name = "Steel Sink",
                DistrictName = "Port",
                Position = new Vector3(1800f, 0f, 0f),
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                InputCapacityTons = 20f,
                OutputCapacityTons = 6f,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            };
        }

        private static IndustryConfig CreateSinkIndustryConfig()
        {
            return new IndustryConfig
            {
                Id = "ore_sink",
                LegacyKey = "ore_sink",
                Name = "Ore Sink",
                DistrictName = "Port",
                Position = new Vector3(0f, 0f, 0f),
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ConstructionSiteSink,
                InputCapacityTons = 20f,
                OutputCapacityTons = 0f,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            };
        }

        private static VehicleDefinition CreateVehicleDefinition(string id, string displayName, string modelName, VehicleCargoType cargoType, float capacityTons, bool isTrailer, bool isTractor, params string[] acceptedCommodities)
        {
            return new VehicleDefinition
            {
                Id = id,
                DisplayName = displayName,
                ModelName = modelName,
                CargoType = cargoType,
                CapacityTons = capacityTons,
                IsTrailer = isTrailer,
                IsTractor = isTractor,
                AcceptedCommodities = acceptedCommodities != null && acceptedCommodities.Length > 0
                    ? new HashSet<string>(acceptedCommodities, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                IsEnabled = true,
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }

        private static ModConfig LoadRepoConfig()
        {
            return ModConfig.Load(System.IO.Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
        }
    }
}
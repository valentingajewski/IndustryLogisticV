using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class GlobalMarketManagerTests
    {
        [TestMethod]
        public void Constructor_WithoutOverrides_RemainsInParityWithAuthoredCommodityCatalog()
        {
            var config = LoadRepoConfig();
            var market = new GlobalMarketManager(0);

            AssertConfiguredCommodityPrices(market, config.CommodityBasePrices);
        }

        [TestMethod]
        public void Constructor_WithConfiguredCommodityBasePrices_PricesEveryConfiguredCommodityExplicitly()
        {
            var config = LoadRepoConfig();
            var market = new GlobalMarketManager(0, config.CommodityBasePrices);

            AssertConfiguredCommodityPrices(market, config.CommodityBasePrices);
        }

        [TestMethod]
        public void GetUnitPrice_NormalizesLookupCaseAndAliases_AndPreservesOverrides()
        {
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "pRoCeSsOrS", 1550f },
            });

            float explicitBasePrice;
            Assert.IsTrue(market.TryGetExplicitBasePrice("Electronic", out explicitBasePrice));
            Assert.AreEqual(1550f, explicitBasePrice, 0.01f);
            Assert.AreEqual(1550f, market.GetUnitPrice("Electronic"), 0.01f);
            Assert.AreEqual(1550f, market.GetUnitPrice("electronic"), 0.01f);
            Assert.AreEqual(1550f, market.GetUnitPrice("PROCESSORS"), 0.01f);
            Assert.AreEqual(1550f, market.GetUnitPrice(" processor "), 0.01f);
        }

        [TestMethod]
        public void GetSinkDemandMultiplier_WithLiveDistrictEventShock_UsesDistrictEventCommodityAndDistrict()
        {
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fuel", 700f },
            });
            var districtStates = new[]
            {
                new TerritoryDistrictState
                {
                    DistrictName = "Port",
                    ActiveEvent = new TerritoryDistrictEventState
                    {
                        EventId = "district_event_port_4",
                        DistrictName = "Port",
                        CrisisType = DistrictCrisisType.FuelShortage,
                        PreferredCommodity = "Fuel",
                        Severity = 0.82f,
                        MarketPressureBonus = 0.18f,
                        ResponseTargetTons = 12f,
                        DeliveredReliefTons = 3f,
                        EndsAtWeekIndex = 4,
                        StatusText = "Critical | Relief 3.0/12.0t | closes week 4",
                    },
                },
            };

            market.ConfigureShockContext(() => 60, () => districtStates);
            market.Update(0);

            var sinkDemandMultiplier = market.GetSinkDemandMultiplier("Port", "Fuel");

            Assert.IsTrue(sinkDemandMultiplier > 1f, "Expected the live Port fuel event to raise sink demand for Fuel deliveries into Port.");
            Assert.AreEqual(1f, market.GetSinkDemandMultiplier("Airport", "Fuel"), 0.001f);
            StringAssert.Contains(market.GetCommodityShockSummary("Fuel"), "Fuel Shortage");
            StringAssert.Contains(market.GetCommodityShockSummary("Fuel"), "Port");
        }

        [TestMethod]
        public void GetSinkDemandMultiplier_WithConfiguredEventAlternates_UsesSharedCommoditySemantics()
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
                            Commodities = new List<string> { "Fuel", "Oil", "Chemicals" },
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
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief" },
                                Substitutes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { "Oil", 0.82f },
                                    { "Chemicals", 0.68f },
                                },
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
                        new ExternalResourceConfig
                        {
                            Commodity = "Chemicals",
                            CargoType = VehicleCargoType.Liquid,
                            EconomySemantics = new CommodityEconomySemantics
                            {
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief" },
                                EventResponseAffinity = 0.85f,
                            },
                        },
                    });

                var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 700f },
                    { "Oil", 380f },
                    { "Chemicals", 750f },
                });
                var districtStates = new[]
                {
                    new TerritoryDistrictState
                    {
                        DistrictName = "Port",
                        ActiveEvent = new TerritoryDistrictEventState
                        {
                            EventId = "district_event_port_5",
                            DistrictName = "Port",
                            CrisisType = DistrictCrisisType.FuelShortage,
                            PreferredCommodity = "Fuel",
                            Severity = 0.80f,
                            MarketPressureBonus = 0.18f,
                            ResponseTargetTons = 10f,
                            DeliveredReliefTons = 1f,
                            EndsAtWeekIndex = 5,
                        },
                    },
                };

                market.ConfigureShockContext(() => 60, () => districtStates);
                market.Update(0);

                Assert.IsTrue(market.GetSinkDemandMultiplier("Port", "Oil") > 1f, "Configured fuel-relief alternates should inherit district demand pressure.");
                StringAssert.Contains(market.GetCommodityShockSummary("Oil"), "Fuel Shortage");
            }
            finally
            {
                var config = LoadRepoConfig();
                CommodityCatalog.Configure(config.ExternalCatalog.ResourceGroups, config.ExternalCatalog.ResourcesByCommodity.Values);
            }
        }

        [TestMethod]
        public void Update_WithRecurringSinkDemand_UsesCommoditySemanticsToDifferentiatePriceMovement()
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
                            Commodities = new List<string> { "Fuel", "Water" },
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
                                ScarcitySensitivity = 1.45f,
                                SinkElasticity = 0.25f,
                                Volatility = 1.15f,
                            },
                        },
                        new ExternalResourceConfig
                        {
                            Commodity = "Water",
                            CargoType = VehicleCargoType.Liquid,
                            EconomySemantics = new CommodityEconomySemantics
                            {
                                ScarcitySensitivity = 0.65f,
                                SinkElasticity = 0.90f,
                                Volatility = 0.85f,
                            },
                        },
                    });

                var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 700f },
                    { "Water", 220f },
                });

                for (var minute = 1; minute <= 10; minute++)
                {
                    market.RegisterSinkDemand("Fuel", 1.25f);
                    market.RegisterSinkDemand("Water", 1.25f);
                    market.Update(minute * 60000);
                }

                Assert.IsTrue(market.GetPriceMultiplier("Fuel") > market.GetPriceMultiplier("Water"), "Higher scarcity sensitivity should compound recurring sink demand more aggressively.");
            }
            finally
            {
                var config = LoadRepoConfig();
                CommodityCatalog.Configure(config.ExternalCatalog.ResourceGroups, config.ExternalCatalog.ResourcesByCommodity.Values);
            }
        }

        private static void AssertConfiguredCommodityPrices(GlobalMarketManager market, IReadOnlyDictionary<string, float> expectedBasePrices)
        {
            Assert.IsNotNull(market);
            Assert.IsNotNull(expectedBasePrices);
            Assert.IsTrue(expectedBasePrices.Count > 0);

            foreach (var pair in expectedBasePrices.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                float explicitBasePrice;
                Assert.IsTrue(
                    market.TryGetExplicitBasePrice(pair.Key, out explicitBasePrice),
                    string.Format("Expected an explicit runtime base price for '{0}'.", pair.Key));
                Assert.AreEqual(pair.Value, explicitBasePrice, 0.01f, string.Format("Base price mismatch for '{0}'.", pair.Key));
                Assert.AreEqual(pair.Value, market.GetUnitPrice(pair.Key), 0.01f, string.Format("Unit price mismatch for '{0}'.", pair.Key));
            }
        }

        private static ModConfig LoadRepoConfig()
        {
            return ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
        }
    }
}
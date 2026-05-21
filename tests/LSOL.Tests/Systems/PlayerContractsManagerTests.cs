using System;
using System.Collections.Generic;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PlayerContractsManagerTests
    {
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
    }
}
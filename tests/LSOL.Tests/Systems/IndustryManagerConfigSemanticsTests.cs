using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class IndustryManagerConfigSemanticsTests
    {
        [TestMethod]
        public void SeedIndustryStartingState_UsesAuthoredSiteSemanticsForAuthoredSiteSeeding()
        {
            var authoredConfig = CreateProcessingConfig("authored-site", true);
            var legacyConfig = CreateProcessingConfig("legacy-factory", false);
            var authoredIndustry = CreateIndustry(authoredConfig);
            var legacyIndustry = CreateIndustry(legacyConfig);

            InvokeSeedIndustryStartingState(authoredIndustry, authoredConfig);
            InvokeSeedIndustryStartingState(legacyIndustry, legacyConfig);

            Assert.AreEqual(10f, authoredIndustry.GetStock("Ore"), 0.01f);
            Assert.AreEqual(10f, authoredIndustry.GetStock("Fuel"), 0.01f);
            Assert.AreEqual(15f, authoredIndustry.GetStock("Steel"), 0.01f);

            Assert.AreEqual(0f, legacyIndustry.GetStock("Ore"), 0.01f);
            Assert.AreEqual(20f, legacyIndustry.GetStock("Fuel"), 0.01f);
            Assert.AreEqual(20f, legacyIndustry.GetStock("Steel"), 0.01f);
        }

        [TestMethod]
        public void TryGetSinkDrainRatePerMinute_UsesAuthoredSiteDrainUnitsForAuthoredSites()
        {
            var authoredSink = CreateSinkConfig("authored-sink", true);
            var legacySink = CreateSinkConfig("legacy-sink", false);

            var authoredDrain = InvokeTryGetSinkDrainRatePerMinute(authoredSink);
            var legacyDrain = InvokeTryGetSinkDrainRatePerMinute(legacySink);

            Assert.AreEqual(2f, authoredDrain, 0.001f);
            Assert.AreEqual(0.12f, legacyDrain, 0.001f);
        }

        [TestMethod]
        public void BuildEffectiveIndustryConfig_UsesAuthoredSitePresetValuesForAuthoredSites()
        {
            var authoredConfig = CreateEconomyConfig("authored-site", true);
            var legacyConfig = CreateEconomyConfig("legacy-factory", false);

            var authoredEffective = InvokeBuildEffectiveIndustryConfig(authoredConfig, EconomyDifficultyPreset.Hardcore);
            var legacyEffective = InvokeBuildEffectiveIndustryConfig(legacyConfig, EconomyDifficultyPreset.Hardcore);

            Assert.AreEqual(8.5f, authoredEffective.ProductionRate, 0.01f);
            Assert.AreEqual(24f, authoredEffective.InputCapacityTons, 0.01f);
            Assert.AreEqual(28f, authoredEffective.OutputCapacityTons, 0.01f);
            Assert.AreEqual(280f, authoredEffective.IndustryPrice, 0.01f);
            Assert.AreEqual(135f, authoredEffective.IndustryLicencePrice, 0.01f);

            Assert.AreEqual(24f, legacyEffective.ProductionRate, 0.01f);
            Assert.AreEqual(80f, legacyEffective.InputCapacityTons, 0.01f);
            Assert.AreEqual(70f, legacyEffective.OutputCapacityTons, 0.01f);
            Assert.AreEqual(800000f, legacyEffective.IndustryPrice, 0.01f);
            Assert.AreEqual(18000f, legacyEffective.IndustryLicencePrice, 0.01f);
        }

        [TestMethod]
        public void BuildRecipes_UsesRecipeVariantsAndFallsBackToLegacyRecipeShape()
        {
            var variantConfig = CreateVariantConfig();
            var legacyConfig = CreateLegacyRecipeConfig();

            var variantRecipes = InvokeBuildRecipes(variantConfig);
            var legacyRecipes = InvokeBuildRecipes(legacyConfig);

            Assert.AreEqual(2, variantRecipes.Count);
            CollectionAssert.AreEquivalent(new[] { "Steel route", "Alloy route" }, variantRecipes.Select(x => x.DisplayName).ToArray());

            Assert.AreEqual(1, legacyRecipes.Count);
            Assert.AreEqual(1f, legacyRecipes[0].InputsTons["Ore"], 0.001f);
            Assert.AreEqual(1f, legacyRecipes[0].OutputsTons["Steel"], 0.001f);
        }

        [TestMethod]
        public void Update_SelectsRecipeVariantWithHighestOptionalInputWeight()
        {
            var config = CreateVariantConfig();
            var industry = CreateIndustry(config, InvokeBuildRecipes(config));

            industry.AddInput("Ore", 10f);
            industry.AddInput("Fuel", 1f);
            industry.AddInput("Catalyst", 1f);

            industry.Update(60f, 1f);

            Assert.AreEqual(0f, industry.GetStock("Steel"), 0.01f);
            Assert.IsTrue(industry.GetStock("Alloy") > 0f);
            StringAssert.Contains(industry.GetPrimaryConversionDescription(), "Alloy route");
        }

        [TestMethod]
        public void GetProductionWarning_PrefersBestBlockingRecipeVariant()
        {
            var config = CreateVariantConfig();
            var industry = CreateIndustry(config, InvokeBuildRecipes(config));

            industry.AddInput("Catalyst", 1f);

            var warning = industry.GetProductionWarning();

            StringAssert.Contains(warning, "Alloy");
            Assert.IsFalse(warning.IndexOf("Steel", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        public void Update_DrainSinkStock_UsesElasticDemandAllocationForSubstitutes()
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
                                SubstituteFamily = "FuelSupply",
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief" },
                                Substitutes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Oil", 0.82f } },
                                SinkElasticity = 0.85f,
                                SinkPreferenceWeight = 1f,
                            },
                        },
                        new ExternalResourceConfig
                        {
                            Commodity = "Oil",
                            CargoType = VehicleCargoType.Liquid,
                            EconomySemantics = new CommodityEconomySemantics
                            {
                                SubstituteFamily = "FuelSupply",
                                DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FuelRelief" },
                                SinkElasticity = 0.55f,
                                SinkPreferenceWeight = 1f,
                            },
                        },
                    });

                var sinkConfig = CreateElasticSinkConfig();
                var config = CreateManagerConfig(sinkConfig);
                var manager = new IndustryManager(config);
                var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 700f },
                    { "Oil", 380f },
                });
                market.SetTemporaryDemandShock("fuel-spike", "Fuel", 0.75f);
                manager.ConfigureMarketPressure(market);

                var sink = manager.Industries.Single();
                sink.AddInput("Fuel", 8f);
                sink.AddInput("Oil", 8f);

                manager.Update(1f, 1f);

                var fuelConsumed = 8f - sink.GetStock("Fuel");
                var oilConsumed = 8f - sink.GetStock("Oil");
                Assert.IsTrue(oilConsumed > fuelConsumed, "Elastic demand should shift sink consumption toward the cheaper stocked substitute.");
            }
            finally
            {
                var config = LoadRepoConfig();
                CommodityCatalog.Configure(config.ExternalCatalog.ResourceGroups, config.ExternalCatalog.ResourcesByCommodity.Values);
            }
        }

        [TestMethod]
        public void Update_WithMarketPressure_CanOverrideSelectionPriorityWhenRecipeValueDiverges()
        {
            var config = CreateEconomyAwareVariantConfig();
            var managerConfig = CreateManagerConfig(config);
            var manager = new IndustryManager(managerConfig);
            var market = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ore", 320f },
                { "Steel", 500f },
                { "Alloy", 5000f },
            });
            manager.ConfigureMarketPressure(market);

            var industry = manager.Industries.Single();
            industry.AddInput("Ore", 10f);
            industry.AddInput("Fuel", 2f);
            industry.AddInput("Catalyst", 2f);

            industry.Update(60f, 1f);

            Assert.IsTrue(industry.GetStock("Alloy") > 0f, "Higher-value downstream output should be able to beat a modest authored priority lead.");
            Assert.AreEqual(0f, industry.GetStock("Steel"), 0.01f);
        }

        private static Industry CreateIndustry(IndustryConfig config)
        {
            return new Industry(config, new List<ProductionRecipe>(), false, 0.2f);
        }

        private static Industry CreateIndustry(IndustryConfig config, List<ProductionRecipe> recipes)
        {
            return new Industry(config, recipes, false, 0.2f);
        }

        private static IndustryConfig CreateProcessingConfig(string id, bool usesAuthoredSiteSemantics)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = id,
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = 1f,
                InputCapacityTons = 40f,
                OutputCapacityTons = 100f,
                ProductionRate = 8f,
                StartingTankRatio = 0.5f,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = usesAuthoredSiteSemantics,
            };
        }

        private static IndustryConfig CreateLegacyRecipeConfig()
        {
            return new IndustryConfig
            {
                Id = "legacy-recipe-site",
                LegacyKey = "legacy-recipe-site",
                Name = "legacy-recipe-site",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = 1f,
                InputCapacityTons = 30f,
                OutputCapacityTons = 35f,
                ProductionRate = 5f,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = false,
            };
        }

        private static IndustryConfig CreateVariantConfig()
        {
            return new IndustryConfig
            {
                Id = "variant-recipe-site",
                LegacyKey = "variant-recipe-site",
                Name = "variant-recipe-site",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel", "Catalyst" },
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel", "Alloy" },
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeVariants = new List<IndustryRecipeVariantConfig>
                {
                    new IndustryRecipeVariantConfig
                    {
                        Id = "steel",
                        DisplayName = "Steel route",
                        SelectionPriority = 0,
                        Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                        OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                        BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                        RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Ore", 1f } },
                        RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Steel", 1f } },
                        OptionalInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Fuel", 1f } },
                        InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                        OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    },
                    new IndustryRecipeVariantConfig
                    {
                        Id = "alloy",
                        DisplayName = "Alloy route",
                        SelectionPriority = 0,
                        Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                        OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Catalyst" },
                        BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alloy" },
                        RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Ore", 1f } },
                        RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Alloy", 1f } },
                        OptionalInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Catalyst", 5f } },
                        InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                        OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    },
                },
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = 1f,
                InputCapacityTons = 30f,
                OutputCapacityTons = 35f,
                ProductionRate = 1f,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = false,
            };
        }

        private static IndustryConfig CreateSinkConfig(string id, bool usesAuthoredSiteSemantics)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = id,
                LocationKind = ExternalLocationKind.Store,
                SiteRole = SiteRole.StoreSink,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ProcessedFood" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                EmptyingRate = 2f,
                HasConfiguredEmptyingRate = true,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = usesAuthoredSiteSemantics,
            };
        }

        private static IndustryConfig CreateElasticSinkConfig()
        {
            return new IndustryConfig
            {
                Id = "elastic-sink",
                LegacyKey = "elastic-sink",
                Name = "Elastic Sink",
                LocationKind = ExternalLocationKind.GasStation,
                SiteRole = SiteRole.FuelSink,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel", "Oil" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityTons = 20f,
                SinkPreferenceWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Fuel", 1f },
                    { "Oil", 1f },
                },
                SinkElasticityMultiplier = 1.15f,
                EmptyingRate = 2f,
                HasConfiguredEmptyingRate = true,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = true,
            };
        }

        private static IndustryConfig CreateEconomyConfig(string id, bool usesAuthoredSiteSemantics)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = id,
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = 1f,
                InputCapacityTons = 30f,
                OutputCapacityTons = 35f,
                ProductionRate = 5f,
                IndustryPrice = 200f,
                IndustryLicencePrice = 100f,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = usesAuthoredSiteSemantics,
                StandardEconomy = SiteEconomyPresetValues.Create(10f, 100f, 200f, 30f, 35f, 1f, true),
                HardcoreEconomy = SiteEconomyPresetValues.Create(15f, 300f, 600f, 45f, 55f, 1.5f, false),
            };
        }

        private static IndustryConfig CreateEconomyAwareVariantConfig()
        {
            return new IndustryConfig
            {
                Id = "market-aware-variant-site",
                LegacyKey = "market-aware-variant-site",
                Name = "market-aware-variant-site",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.ProcessingPlant,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel", "Catalyst" },
                BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel", "Alloy" },
                RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                RecipeVariants = new List<IndustryRecipeVariantConfig>
                {
                    new IndustryRecipeVariantConfig
                    {
                        Id = "steel-priority",
                        DisplayName = "Steel priority",
                        SelectionPriority = 1,
                        Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                        OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fuel" },
                        BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Steel" },
                        RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Ore", 1f } },
                        RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Steel", 1f } },
                        OptionalInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Fuel", 1f } },
                        InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                        OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    },
                    new IndustryRecipeVariantConfig
                    {
                        Id = "alloy-value",
                        DisplayName = "Alloy value",
                        SelectionPriority = 0,
                        Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                        OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Catalyst" },
                        BoostInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alloy" },
                        RecipeInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Ore", 1f } },
                        RecipeOutputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Alloy", 1f } },
                        OptionalInputWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "Catalyst", 1f } },
                        InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                        OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    },
                },
                InputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                OutputCapacityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = 1f,
                InputCapacityTons = 30f,
                OutputCapacityTons = 35f,
                ProductionRate = 1f,
                DeliveryPayoutMultiplier = 1f,
                UsesAuthoredSiteSemantics = true,
            };
        }

        private static ModConfig CreateManagerConfig(params IndustryConfig[] industryConfigs)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(
                config,
                nameof(ModConfig.IndustryConfigs),
                industryConfigs.ToDictionary(entry => entry.Id, entry => entry, StringComparer.OrdinalIgnoreCase));
            return config;
        }

        private static void InvokeSeedIndustryStartingState(Industry industry, IndustryConfig config)
        {
            var method = GetIndustryManagerPrivateStaticMethod("SeedIndustryStartingState");
            method.Invoke(null, new object[] { industry, config });
        }

        private static float InvokeTryGetSinkDrainRatePerMinute(IndustryConfig config)
        {
            var method = GetIndustryManagerPrivateStaticMethod("TryGetSinkDrainRatePerMinute");
            var args = new object[] { config, 0f };
            var success = (bool)method.Invoke(null, args);
            Assert.IsTrue(success);
            return (float)args[1];
        }

        private static IndustryConfig InvokeBuildEffectiveIndustryConfig(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            var method = GetIndustryManagerPrivateStaticMethod("BuildEffectiveIndustryConfig");
            return (IndustryConfig)method.Invoke(null, new object[] { config, preset });
        }

        private static List<ProductionRecipe> InvokeBuildRecipes(IndustryConfig config)
        {
            var method = GetIndustryManagerPrivateStaticMethod("BuildRecipes");
            return (List<ProductionRecipe>)method.Invoke(null, new object[] { config, false });
        }

        private static MethodInfo GetIndustryManagerPrivateStaticMethod(string name)
        {
            var method = typeof(IndustryManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, name);
            return method;
        }

        private static ModConfig LoadRepoConfig()
        {
            return ModConfig.Load(System.IO.Path.Combine(TestSupport.TestWorkspace.GetRepoRoot(), "LSOL_Config"));
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
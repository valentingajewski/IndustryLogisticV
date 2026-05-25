using System;
using System.Collections.Generic;
using System.Reflection;
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

        private static Industry CreateIndustry(IndustryConfig config)
        {
            return new Industry(config, new List<ProductionRecipe>(), false, 0.2f);
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

        private static MethodInfo GetIndustryManagerPrivateStaticMethod(string name)
        {
            var method = typeof(IndustryManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, name);
            return method;
        }
    }
}
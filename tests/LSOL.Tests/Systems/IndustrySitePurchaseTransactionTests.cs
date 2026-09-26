using System;
using System.Collections.Generic;
using LSOL.Config;
using LSOL.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class IndustrySitePurchaseTransactionTests
    {
        [TestMethod]
        public void TryPurchase_PricedSite_DeductsBalanceAndMarksOwned()
        {
            var industry = CreatePurchasableSite(SiteRole.ProcessingPlant, 5000f);
            Assert.IsTrue(industry.RequiresPurchase);
            Assert.IsFalse(industry.IsOwned);

            var balance = 8000f;
            float cost;
            string result;
            var purchased = industry.TryPurchase(ref balance, out cost, out result);

            Assert.IsTrue(purchased);
            Assert.AreEqual(5000f, cost, 0.01f);
            Assert.AreEqual(3000f, balance, 0.01f);
            Assert.IsTrue(industry.IsOwned);
        }

        [TestMethod]
        public void TryPurchase_WarehouseSite_DeductsBalanceAndMarksOwned()
        {
            var warehouse = CreatePurchasableSite(SiteRole.Warehouse, 12000f);
            Assert.IsTrue(warehouse.RequiresPurchase);

            var balance = 12000f;
            float cost;
            string result;
            var purchased = warehouse.TryPurchase(ref balance, out cost, out result);

            Assert.IsTrue(purchased);
            Assert.AreEqual(0f, balance, 0.01f);
            Assert.IsTrue(warehouse.IsOwned);
        }

        [TestMethod]
        public void TryPurchase_InsufficientBalance_LeavesSiteUnowned()
        {
            var industry = CreatePurchasableSite(SiteRole.ProcessingPlant, 5000f);

            var balance = 4999f;
            float cost;
            string result;
            var purchased = industry.TryPurchase(ref balance, out cost, out result);

            Assert.IsFalse(purchased);
            Assert.AreEqual(4999f, balance, 0.01f);
            Assert.IsFalse(industry.IsOwned);
        }

        private static Industry CreatePurchasableSite(SiteRole siteRole, float price)
        {
            var config = new IndustryConfig
            {
                Id = "purchasable-site",
                LegacyKey = "purchasable-site",
                Name = "Purchasable Site",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = siteRole,
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
                InputCapacityTons = 40f,
                OutputCapacityTons = 100f,
                ProductionRate = 8f,
                StartingTankRatio = 0.5f,
                DeliveryPayoutMultiplier = 1f,
                IndustryPrice = price,
                IsOwned = false,
            };

            return new Industry(config, new List<ProductionRecipe>(), false, 0.2f);
        }
    }
}

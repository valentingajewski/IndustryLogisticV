using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class IndustryManagerWarehouseStoragePressureTests
    {
        [TestMethod]
        public void Update_WithSensitiveWarehouseStock_RecordsSpoilageAndShrinkageSeparately()
        {
            var manager = CreateManager(CreateWarehouseConfig());
            var warehouse = manager.Industries.Single();
            var financeTracker = new CompanyFinanceTracker();

            warehouse.ApplyStoragePressureState(0.56f, 0, 0f, 0f);
            warehouse.BufferStorage["ProcessedFood"] = 100f;
            warehouse.BufferStorage["Computer"] = 100f;

            manager.ConfigureMarketPressure(new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "ProcessedFood", 2000f },
                { "Computer", 3000f },
            }));
            manager.ConfigureStoragePressure(null, financeTracker, () => 24 * 60, null);

            manager.Update(1f, 1f);

            var spoilageTransactions = financeTracker.GetTransactionsInWindow(24 * 60, 24 * 60, CompanyFinanceFlow.Expense, CompanyFinanceCategory.WarehouseSpoilage);
            var shrinkageTransactions = financeTracker.GetTransactionsInWindow(24 * 60, 24 * 60, CompanyFinanceFlow.Expense, CompanyFinanceCategory.WarehouseShrinkage);
            var risk = manager.GetWarehouseStorageRiskSnapshot(warehouse);

            Assert.AreEqual(1, spoilageTransactions.Count);
            Assert.AreEqual(1, shrinkageTransactions.Count);
            Assert.IsTrue(spoilageTransactions[0].Amount > 0.01f);
            Assert.IsTrue(shrinkageTransactions[0].Amount > 0.01f);
            Assert.IsTrue(warehouse.LastDaySpoilageValue > 0.01f);
            Assert.IsTrue(warehouse.LastDayShrinkageValue > 0.01f);
            Assert.AreEqual(0, warehouse.StorageTelemetryWeekIndex);
            Assert.IsNotNull(risk);
            Assert.AreEqual(WarehouseLossClass.Spoilage, risk.DominantLossClass);
            Assert.AreEqual("ProcessedFood", risk.DominantCommodity);
            Assert.IsTrue(risk.ProjectedNextDayLossValue > 0.01f);
            Assert.IsTrue(risk.ConditionValueLoss > 0.01f);
        }

        [TestMethod]
        public void Update_WhenWeekRollsOver_ResetsCurrentWeekWarehouseLossTelemetry()
        {
            var manager = CreateManager(CreateWarehouseConfig());
            var warehouse = manager.Industries.Single();

            warehouse.ApplyStoragePressureState(0.56f, 6, 0f, 0f);
            warehouse.ApplyStorageLossTelemetryState(0, 5f, 50f, 4f, 40f, 5000f, 50000f, 4000f, 40000f);
            warehouse.BufferStorage["ProcessedFood"] = 50f;
            warehouse.BufferStorage["Computer"] = 50f;

            manager.ConfigureMarketPressure(new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "ProcessedFood", 10f },
                { "Computer", 10f },
            }));
            manager.ConfigureStoragePressure(null, new CompanyFinanceTracker(), () => 8 * 24 * 60, null);

            manager.Update(1f, 1f);

            Assert.AreEqual(1, warehouse.StorageTelemetryWeekIndex);
            Assert.IsTrue(warehouse.CurrentWeekSpoilageValue < 100f);
            Assert.IsTrue(warehouse.CurrentWeekShrinkageValue < 100f);
            Assert.IsTrue(warehouse.LastDaySpoilageValue > 0f);
            Assert.IsTrue(warehouse.LastDayShrinkageValue > 0f);
        }

        [TestMethod]
        public void GetUpgradeCost_Warehouse_AllowsStorageModulesOnly()
        {
            var warehouse = CreateWarehouseIndustry();
            var profit = 20000f;
            float cost;
            string result;

            Assert.IsTrue(warehouse.GetUpgradeCost(IndustryUpgradeModule.InputStorage) > 0f);
            Assert.IsTrue(warehouse.GetUpgradeCost(IndustryUpgradeModule.OutputStorage) > 0f);
            Assert.AreEqual(-1f, warehouse.GetUpgradeCost(IndustryUpgradeModule.Production));
            Assert.AreEqual(-1f, warehouse.GetUpgradeCost(IndustryUpgradeModule.OmegaStorage));
            Assert.IsTrue(warehouse.TryUpgradeModule(IndustryUpgradeModule.InputStorage, ref profit, out cost, out result));
            Assert.AreEqual(1, warehouse.InputStorageModuleLevel);
            Assert.IsTrue(warehouse.InputCapacityTons > 100f);
        }

        private static IndustryManager CreateManager(IndustryConfig warehouseConfig)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.ObjectModels), new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase));
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { warehouseConfig.Id, warehouseConfig },
            });
            return new IndustryManager(config);
        }

        private static Industry CreateWarehouseIndustry()
        {
            return new Industry(CreateWarehouseConfig(), new List<ProductionRecipe>(), false, 0.2f);
        }

        private static IndustryConfig CreateWarehouseConfig()
        {
            return new IndustryConfig
            {
                Id = "warehouse_alpha",
                LegacyKey = "warehouse_alpha",
                Name = "Warehouse Alpha",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = SiteRole.Warehouse,
                DistrictName = "Port",
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ProcessedFood",
                    "Computer",
                },
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                ProductionRate = 0f,
                InputCapacityTons = 100f,
                OutputCapacityTons = 100f,
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
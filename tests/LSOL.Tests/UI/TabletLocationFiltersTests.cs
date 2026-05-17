using System.Collections.Generic;
using LSOL.Config;
using LSOL.Domain;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletLocationFiltersTests
    {
        [TestMethod]
        public void IsProductionIndustry_ExcludesWarehouseAndConstructionSites()
        {
            var production = CreateIndustry("production", SiteRole.ProcessingPlant);
            var warehouse = CreateIndustry("warehouse", SiteRole.Warehouse);
            var construction = CreateIndustry("construction", SiteRole.ConstructionSiteSink);

            Assert.IsTrue(TabletLocationFilters.IsProductionIndustry(production));
            Assert.IsFalse(TabletLocationFilters.IsProductionIndustry(warehouse));
            Assert.IsFalse(TabletLocationFilters.IsProductionIndustry(construction));
        }

        [TestMethod]
        public void IsWarehouse_OnlyMatchesWarehouseSites()
        {
            var production = CreateIndustry("production", SiteRole.ProcessingPlant);
            var warehouse = CreateIndustry("warehouse", SiteRole.Warehouse);

            Assert.IsFalse(TabletLocationFilters.IsWarehouse(production));
            Assert.IsTrue(TabletLocationFilters.IsWarehouse(warehouse));
        }

        [TestMethod]
        public void IsStorageTrackedSite_IncludesProductionAndWarehouseButNotConstruction()
        {
            var production = CreateIndustry("production", SiteRole.ProcessingPlant);
            var warehouse = CreateIndustry("warehouse", SiteRole.Warehouse);
            var construction = CreateIndustry("construction", SiteRole.ConstructionSiteSink);

            Assert.IsTrue(TabletLocationFilters.IsStorageTrackedSite(production));
            Assert.IsTrue(TabletLocationFilters.IsStorageTrackedSite(warehouse));
            Assert.IsFalse(TabletLocationFilters.IsStorageTrackedSite(construction));
        }

        [TestMethod]
        public void IsGameplayOpenToPlayer_UsesImmediateGameplayAccessSemantics()
        {
            var ownedIndustry = CreateSummary(
                industryId: "owned",
                isOwnedByPlayer: true,
                requiresIndustryPurchase: true,
                requiresContractorPermit: true,
                hasContractorPermitForGameplay: false);
            var permitOpenIndustry = CreateSummary(
                industryId: "permit-open",
                isOwnedByPlayer: false,
                requiresIndustryPurchase: true,
                requiresContractorPermit: true,
                hasContractorPermitForGameplay: true);
            var noPermitRequiredIndustry = CreateSummary(
                industryId: "open-no-permit",
                isOwnedByPlayer: false,
                requiresIndustryPurchase: true,
                requiresContractorPermit: false,
                hasContractorPermitForGameplay: false);
            var permitLockedIndustry = CreateSummary(
                industryId: "permit-locked",
                isOwnedByPlayer: false,
                requiresIndustryPurchase: false,
                requiresContractorPermit: true,
                hasContractorPermitForGameplay: false);
            var purchaseLockedIndustry = CreateSummary(
                industryId: "purchase-locked",
                isOwnedByPlayer: false,
                requiresIndustryPurchase: true,
                requiresContractorPermit: true,
                hasContractorPermitForGameplay: false);

            Assert.IsTrue(TabletLocationFilters.IsGameplayOpenToPlayer(ownedIndustry));
            Assert.IsTrue(TabletLocationFilters.IsGameplayOpenToPlayer(permitOpenIndustry));
            Assert.IsTrue(TabletLocationFilters.IsGameplayOpenToPlayer(noPermitRequiredIndustry));
            Assert.IsFalse(TabletLocationFilters.IsGameplayOpenToPlayer(permitLockedIndustry));
            Assert.IsFalse(TabletLocationFilters.IsGameplayOpenToPlayer(purchaseLockedIndustry));
        }

        private static TabletLocationSummary CreateSummary(
            string industryId,
            bool isOwnedByPlayer,
            bool requiresIndustryPurchase,
            bool requiresContractorPermit,
            bool hasContractorPermitForGameplay)
        {
            return new TabletLocationSummary
            {
                Industry = CreateIndustry(industryId, SiteRole.ProcessingPlant),
                LocationKind = ExternalLocationKind.Industry,
                Name = industryId,
                IsOwnedByPlayer = isOwnedByPlayer,
                RequiresIndustryPurchase = requiresIndustryPurchase,
                RequiresContractorPermit = requiresContractorPermit,
                HasContractorPermitForGameplay = hasContractorPermitForGameplay,
            };
        }

        private static Industry CreateIndustry(string id, SiteRole siteRole)
        {
            return new Industry(
                new IndustryConfig
                {
                    Id = id,
                    LegacyKey = id,
                    Name = id,
                    LocationKind = ExternalLocationKind.Industry,
                    SiteRole = siteRole,
                    Inputs = new HashSet<string>(),
                    Outputs = new HashSet<string>(),
                },
                new List<ProductionRecipe>(),
                false,
                1f);
        }
    }
}
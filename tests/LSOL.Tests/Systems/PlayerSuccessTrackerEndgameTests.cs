using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PlayerSuccessTrackerEndgameTests
    {
        [TestMethod]
        public void GetEndgameSummary_WithoutLandmarkHq_UsesHqAsPrimaryOpportunity()
        {
            var tracker = CreateIndustrialTracker(false);

            var doctrines = tracker.GetDoctrineStatuses();
            var industrial = doctrines.First(status => status.Doctrine == CompanyDoctrine.Industrial);
            var summary = tracker.GetEndgameSummary();
            var doctrineComponent = summary.PrestigeBreakdown.Components.First(component => component.Key == "doctrine");
            var headquartersComponent = summary.PrestigeBreakdown.Components.First(component => component.Key == "headquarters");

            Assert.IsTrue(industrial.IsActive, DescribeDoctrineStates(doctrines));
            Assert.AreEqual(1, industrial.Tier);
            Assert.AreEqual(1, industrial.EffectiveTier);
            Assert.AreEqual(0.475f, industrial.ProgressRatio, 0.001f);
            Assert.AreEqual(4, industrial.ProgressComponents.Count);
            Assert.AreEqual("Owned chain", industrial.ProgressComponents[2].Label);
            Assert.AreEqual(0.30f, industrial.ProgressComponents[2].ContributionRatio, 0.001f);
            StringAssert.Contains(industrial.NextTierSummary, "Tier II");
            StringAssert.Contains(industrial.SteeringSummary, "warehouse");
            StringAssert.Contains(industrial.LeadSummary, "only doctrine at Tier I or above");

            Assert.IsFalse(summary.HasLandmarkHeadquarters);
            Assert.AreEqual(14.4f, summary.PrestigeScore, 0.001f);
            Assert.AreEqual(12f, doctrineComponent.Score, 0.001f);
            Assert.AreEqual(0f, headquartersComponent.Score, 0.001f);
            StringAssert.Contains(summary.PrimaryOpportunitySummary, "Landmark HQ");
            StringAssert.Contains(summary.PrestigeBreakdown.PrimaryOpportunity, "Landmark HQ");
        }

        [TestMethod]
        public void GetEndgameSummary_WithLandmarkHq_BoostsActiveDoctrineAndKeepsDoctrineSteeringData()
        {
            var tracker = CreateIndustrialTracker(true);

            var doctrines = tracker.GetDoctrineStatuses();
            var industrial = doctrines.First(status => status.Doctrine == CompanyDoctrine.Industrial);
            var summary = tracker.GetEndgameSummary();
            var doctrineComponent = summary.PrestigeBreakdown.Components.First(component => component.Key == "doctrine");
            var headquartersComponent = summary.PrestigeBreakdown.Components.First(component => component.Key == "headquarters");

            Assert.IsTrue(summary.HasLandmarkHeadquarters, DescribeDoctrineStates(doctrines));
            Assert.IsTrue(industrial.HasHeadquartersBoost, DescribeDoctrineStates(doctrines));
            Assert.AreEqual(1, industrial.Tier);
            Assert.AreEqual(2, industrial.EffectiveTier);
            Assert.AreEqual(50.4f, summary.PrestigeScore, 0.001f);
            Assert.AreEqual(24f, doctrineComponent.Score, 0.001f);
            Assert.AreEqual(24f, headquartersComponent.Score, 0.001f);
            StringAssert.Contains(industrial.NextTierSummary, "Tier II");
            StringAssert.Contains(summary.PrimaryOpportunitySummary, "warehouse");
            StringAssert.Contains(summary.PrestigeBreakdown.PrimaryOpportunity, "Tier II");
        }

        private static PlayerSuccessTracker CreateIndustrialTracker(bool hasLandmarkHq)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "producer", CreateIndustryConfig("producer", "Steel Mill", SiteRole.RawProducer, true, new[] { "Steel" }, new string[0]) },
                { "consumer", CreateIndustryConfig("consumer", "Machine Works", SiteRole.ManufacturingPlant, true, new[] { "Machinery" }, new[] { "Steel" }) },
                { "warehouse", CreateIndustryConfig("warehouse", "Central Warehouse", SiteRole.Warehouse, true, new string[0], new string[0]) },
            });
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), hasLandmarkHq
                ? new[]
                {
                    new OfficeDefinition
                    {
                        OfficeId = "hq",
                        SiteName = "Headquarters",
                        OfficePrice = 10000f,
                        WeeklyOfficeRent = 100f,
                        MaxCommercialVehicles = 2,
                    },
                }.ToList()
                : new List<OfficeDefinition>());
            SetProperty(config, nameof(ModConfig.OfficeObjectDefinitions), hasLandmarkHq
                ? new[]
                {
                    new OfficeObjectDefinition
                    {
                        ObjectId = 1,
                        DisplayName = "Landmark HQ",
                        Function = OfficeObjectFunction.Headquarters,
                    },
                }.ToList()
                : new List<OfficeObjectDefinition>());

            var industryManager = new IndustryManager(config);
            industryManager.Industries.First(industry => industry.Id == "producer").SetOwned(true);
            industryManager.Industries.First(industry => industry.Id == "consumer").SetOwned(true);
            industryManager.Industries.First(industry => industry.Id == "warehouse").SetOwned(true);
            PropertyManager propertyManager = null;
            if (hasLandmarkHq)
            {
                propertyManager = new PropertyManager(config);
                propertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
                {
                    ActiveOfficeId = "hq",
                    Offices =
                    {
                        new OfficeOwnershipPersistenceEntry
                        {
                            OfficeId = "hq",
                            IsOwned = true,
                            LastChargedWeekIndex = 0,
                        },
                    },
                    OfficeObjects =
                    {
                        new OfficeObjectPersistenceEntry
                        {
                            InstanceId = "hq-1",
                            OfficeId = "hq",
                            DefinitionId = 1,
                            IsPlaced = true,
                            Position = Vector3.Zero,
                            Rotation = Vector3.Zero,
                        },
                    },
                }, 0);
            }

            var tracker = new PlayerSuccessTracker(
                industryManager,
                propertyManager,
                null,
                null,
                null,
                null,
                null,
                null);
            tracker.ApplyPersistenceSnapshot(new PlayerStatisticsPersistenceSnapshot
            {
                CommodityTotals =
                {
                    new PlayerCommodityStatisticSnapshot
                    {
                        CommodityId = "Steel",
                        Tons = 12.5f,
                    },
                },
            }, 0f);
            return tracker;
        }

        private static IndustryConfig CreateIndustryConfig(string id, string name, SiteRole siteRole, bool isOwned, IEnumerable<string> outputs, IEnumerable<string> inputs)
        {
            return new IndustryConfig
            {
                Id = id,
                LegacyKey = id,
                Name = name,
                DistrictName = "Port",
                LocationKind = ExternalLocationKind.Industry,
                SiteRole = siteRole,
                OwnershipTier = SiteOwnershipTier.Local,
                IsOwned = isOwned,
                IndustryPrice = siteRole == SiteRole.Warehouse ? 4000f : 6000f,
                IndustryOwnerCut = 0.5f,
                ProductionRate = 1f,
                InputCapacityTons = 10f,
                OutputCapacityTons = 10f,
                Inputs = new HashSet<string>(inputs ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase),
                OptionalInputs = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase),
                BoostInputs = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(outputs ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase),
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }

        private static string DescribeDoctrineStates(IEnumerable<CompanyDoctrineStatus> statuses)
        {
            return string.Join(
                " || ",
                (statuses ?? Enumerable.Empty<CompanyDoctrineStatus>())
                    .Where(status => status != null)
                    .Select(status => string.Format(
                        "{0}: tier={1}, live={2}, progress={3:0.000}, active={4}, next={5}",
                        status.Name,
                        status.Tier,
                        status.EffectiveTier,
                        status.ProgressRatio,
                        status.IsActive,
                        status.SteeringSummary)));
        }
    }
}
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
    public sealed class TerritoryManagerCompetitionTests
    {
        [TestMethod]
        public void RegisterDelivery_MatchingDistrictEventProgressesAndClearsEvent()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        ActiveEvent = new TerritoryDistrictEventSnapshot
                        {
                            EventId = "district_event_port_1",
                            DistrictName = "Port",
                            CrisisType = DistrictCrisisType.FuelShortage,
                            PreferredCommodity = "Fuel",
                            Severity = 0.62f,
                            MarketPressureBonus = 0.28f,
                            ResponseTargetTons = 10f,
                            DeliveredReliefTons = 2f,
                            ReliefDeliveryCount = 1,
                            StartedWeekIndex = 1,
                            EndsAtWeekIndex = 2,
                            TriggerSummary = "Fuel pressure 44% | Gas sites 1 | At-risk stops 1",
                            ImpactSummary = "Fuel stops and service lanes need relief cargo.",
                        },
                    },
                },
            });

            var destination = GetIndustry(territoryManager, "alpha-depot");
            Assert.IsNotNull(destination);
            Assert.IsNotNull(territoryManager.GetDistrictEvent("Port"));

            territoryManager.RegisterDelivery(destination, "Fuel", 8f, false, "bravo-depot", "GrandSenora");

            Assert.IsNull(territoryManager.GetDistrictEvent("Port"), "Expected matching relief cargo to clear the district event once the response target is met.");
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_RiskDistrictCreatesLiveDistrictEvent()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Sites =
                {
                    new TerritorySiteSnapshot
                    {
                        SiteId = "alpha-depot",
                        ControlLevel = TerritoryControlLevel.Owned,
                        CrewAssigned = true,
                        LoadRuns = 6,
                        UnloadRuns = 4,
                        TotalDeliveries = 10,
                        TotalDeliveredTons = 60f,
                        TotalLoadedTons = 42f,
                    },
                },
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        LicenseStatus = DistrictLicenseStatus.Active,
                        CurrentWeekActivityCount = 1,
                        CurrentWeekActivityTons = 4f,
                    },
                },
            });

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var result = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);
            var activeEvent = territoryManager.GetDistrictEvent("Port");

            Assert.IsNotNull(result);
            Assert.IsNotNull(activeEvent, "Expected weekly maintenance to seed a live district event for an under-served licensed district.");
            Assert.AreEqual(DistrictCrisisType.EmergencyRestock, activeEvent.CrisisType);
            Assert.IsTrue(activeEvent.ResponseTargetTons > 0.01f);
            Assert.IsTrue(result.CreatedDistrictEventCount >= 1);
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_HeldLaneReducesCorridorPressureAndAddsOpportunity()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Corridors =
                {
                    new TerritoryCorridorSnapshot
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        RightLevel = CorridorRightLevel.Corridor,
                        CurrentWeekDeliveryCount = 4,
                        CurrentWeekDeliveredTons = 52f,
                        CompetitivePressure = 0.32f,
                        CompetitiveOpportunity = 0.08f,
                    },
                },
                Districts =
                {
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "Port",
                        CurrentWeekActivityCount = 4,
                        CurrentWeekActivityTons = 28f,
                    },
                    new TerritoryDistrictSnapshot
                    {
                        DistrictName = "GrandSenora",
                        CurrentWeekActivityCount = 4,
                        CurrentWeekActivityTons = 28f,
                    },
                },
            });

            territoryManager.ConfigureEndgameContext(
                () => new CompanyEndgameSummary(),
                () => Array.Empty<NpcDistrictCompetitionSummary>(),
                () => new[]
                {
                    new NpcCorridorCompetitionSummary
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        ActiveJobCount = 2,
                        VisibleConvoyCount = 1,
                        CompetitiveTons = 18f,
                        PressureScore = 0.26f,
                    },
                });

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var result = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);
            var corridor = territoryManager.GetCorridorState("Port", "GrandSenora");

            Assert.IsNotNull(result);
            Assert.IsNotNull(corridor);
            Assert.AreEqual(1, corridor.CompetitiveWinCount);
            Assert.IsTrue(corridor.CompetitivePressure < 0.32f, "Expected held-lane pressure relief to reduce corridor pressure.");
            Assert.IsTrue(corridor.CompetitiveOpportunity > 0.08f, "Expected held-lane opportunity to increase after a defensive success.");
            Assert.IsTrue(result.Messages.Exists(message => message != null && message.Contains("held its lane against outside carriers")));
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_NeglectedContestedLaneAddsDecayPressure()
        {
            var territoryManager = CreateTerritoryManager();
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Corridors =
                {
                    new TerritoryCorridorSnapshot
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        RightLevel = CorridorRightLevel.ServicePermit,
                        CurrentWeekDeliveryCount = 1,
                        CurrentWeekDeliveredTons = 4f,
                        DecayPressure = 0f,
                        CompetitivePressure = 0.22f,
                    },
                },
            });

            territoryManager.ConfigureEndgameContext(
                () => new CompanyEndgameSummary(),
                () => Array.Empty<NpcDistrictCompetitionSummary>(),
                () => new[]
                {
                    new NpcCorridorCompetitionSummary
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        ActiveJobCount = 2,
                        CompetitiveTons = 12f,
                        PressureScore = 0.22f,
                    },
                });

            Assert.IsNull(territoryManager.ProcessWeeklyMaintenance(0));

            var result = territoryManager.ProcessWeeklyMaintenance(7 * 24 * 60);
            var corridor = territoryManager.GetCorridorState("Port", "GrandSenora");

            Assert.IsNotNull(result);
            Assert.IsNotNull(corridor);
            Assert.AreEqual(CorridorRightLevel.ServicePermit, corridor.RightLevel);
            Assert.IsTrue(corridor.DecayPressure > 1.5f, "Expected contested neglect to add extra decay pressure before the upkeep threshold is reached.");
            Assert.IsTrue(corridor.CompetitivePressure > 0.22f, "Expected neglected rival activity to raise corridor pressure.");
            StringAssert.Contains(corridor.UpkeepStatus, "Competition");
        }

        [TestMethod]
        public void ProcessWeeklyMaintenance_SyncsCarrierCompetitionMetadataFromNpcSummaries()
        {
            var territoryManager = CreateTerritoryManager();

            territoryManager.ConfigureEndgameContext(
                () => new CompanyEndgameSummary(),
                () => new[]
                {
                    new NpcDistrictCompetitionSummary
                    {
                        DistrictName = "Port",
                        ActiveJobCount = 2,
                        ActiveCarrierCount = 2,
                        DominantCarrierId = "port-freight",
                        DominantCarrierName = "Port Freight",
                    },
                },
                () => new[]
                {
                    new NpcCorridorCompetitionSummary
                    {
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        ActiveJobCount = 3,
                        ActiveCarrierCount = 1,
                        DominantCarrierId = "senora-line",
                        DominantCarrierName = "Senora Line",
                    },
                });

            territoryManager.RefreshState();

            var district = territoryManager.GetDistrictState("Port");
            var corridor = territoryManager.GetCorridorState("Port", "GrandSenora");

            Assert.IsNotNull(district);
            Assert.AreEqual(2, district.ActiveCarrierCount);
            Assert.AreEqual("port-freight", district.DominantCarrierId);
            Assert.AreEqual("Port Freight", district.DominantCarrierName);
            StringAssert.Contains(district.CompetitionStatus, "Carriers 2");

            Assert.IsNotNull(corridor);
            Assert.AreEqual(1, corridor.ActiveCarrierCount);
            Assert.AreEqual("senora-line", corridor.DominantCarrierId);
            Assert.AreEqual("Senora Line", corridor.DominantCarrierName);
            StringAssert.Contains(corridor.CompetitionStatus, "Lead Senora Line");
        }

        private static TerritoryManager CreateTerritoryManager()
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.IndustryOmegaCapacityMultiplier), 0.2f);
            SetProperty(config, nameof(ModConfig.IndustryConfigs), new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "alpha-depot", CreateIndustryConfig("alpha-depot", "Alpha Depot", "Port") },
                { "bravo-depot", CreateIndustryConfig("bravo-depot", "Bravo Depot", "GrandSenora") },
            });
            SetProperty(config, nameof(ModConfig.DistrictConfigs), new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "Port", new DistrictConfig { Id = "10001", Name = "Port" } },
                { "GrandSenora", new DistrictConfig { Id = "10000", Name = "GrandSenora" } },
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
                SiteRole = SiteRole.RawProducer,
                OwnershipTier = SiteOwnershipTier.Local,
                Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ore" },
                ProductionRate = 1f,
                InputCapacityTons = 10f,
                OutputCapacityTons = 10f,
                IndustryOwnerCut = 0.5f,
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }

        private static Industry GetIndustry(TerritoryManager territoryManager, string industryId)
        {
            var field = typeof(TerritoryManager).GetField("_industriesById", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);

            var industriesById = field.GetValue(territoryManager) as Dictionary<string, Industry>;
            Assert.IsNotNull(industriesById);

            Industry industry;
            return industriesById.TryGetValue(industryId, out industry)
                ? industry
                : null;
        }
    }
}
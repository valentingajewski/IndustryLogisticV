using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletEndgameStatusFormatterTests
    {
        [TestMethod]
        public void BuildHomeTileDetail_WhenNoDoctrineLeads_ExplainsInactiveBonus()
        {
            var summary = new CompanyEndgameSummary
            {
                PrestigeScore = 12f,
                HighestPrestigeScore = 18f,
                HasLandmarkHeadquarters = false,
                ActiveDoctrineTier = 0,
                ActiveDoctrineEffectiveTier = 0,
                DoctrineLead = new CompanyDoctrineLeadSummary
                {
                    ReasonSummary = "No doctrine has reached Tier I yet. Integrated Chain is closest at 33%.",
                },
                PrimaryOpportunitySummary = "Next push: reach Tier I to activate a doctrine bonus.",
            };

            Assert.AreEqual(
                "3/38 unlocked | Prestige 12 | Peak 18\nNo leading doctrine yet | HQ Offline\nNext push: reach Tier I to activate a doctrine bonus.",
                TabletEndgameStatusFormatter.BuildHomeTileDetail(3, 38, summary));
        }

        [TestMethod]
        public void BuildStatusDetail_WhenDoctrineIsActiveWithoutHq_ExplainsRawTierAndMissingBoost()
        {
            var summary = new CompanyEndgameSummary
            {
                ActiveDoctrine = CompanyDoctrine.Industrial,
                ActiveDoctrineName = "Integrated Chain",
                ActiveDoctrineTier = 2,
                ActiveDoctrineEffectiveTier = 2,
                PrestigeScore = 44f,
                HighestPrestigeScore = 52f,
                HasLandmarkHeadquarters = false,
                DoctrineLead = new CompanyDoctrineLeadSummary
                {
                    ReasonSummary = "Integrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.",
                },
                PrimaryOpportunitySummary = "Next push: install a Landmark HQ (+24 prestige, +1 live tier).",
            };

            Assert.AreEqual(
                "Prestige 44/100 | Peak 52\nIntegrated Chain II | HQ Offline\nIntegrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.\nNext push: install a Landmark HQ (+24 prestige, +1 live tier).",
                TabletEndgameStatusFormatter.BuildStatusDetail(summary));
        }

        [TestMethod]
        public void BuildStatusDetail_WhenHqBoostsActiveDoctrine_ShowsRawAndEffectiveTier()
        {
            var summary = new CompanyEndgameSummary
            {
                ActiveDoctrine = CompanyDoctrine.Territorial,
                ActiveDoctrineName = "Regional Backbone",
                ActiveDoctrineTier = 2,
                ActiveDoctrineEffectiveTier = 3,
                PrestigeScore = 58f,
                HighestPrestigeScore = 61f,
                HasLandmarkHeadquarters = true,
                DoctrineLead = new CompanyDoctrineLeadSummary
                {
                    ReasonSummary = "Regional Backbone leads on tier over Client Priority.",
                },
                PrimaryOpportunitySummary = "Best push: Crew 1 more support depot (+7% progress).",
            };

            Assert.AreEqual(
                "Prestige 58/100 | Peak 61\nRegional Backbone II -> III | HQ Online\nRegional Backbone leads on tier over Client Priority.\nBest push: Crew 1 more support depot (+7% progress).",
                TabletEndgameStatusFormatter.BuildStatusDetail(summary));
        }

        [TestMethod]
        public void BuildDoctrineDetail_WhenDoctrineIsActive_UsesLiveWordingAndBoostState()
        {
            var status = new CompanyDoctrineStatus
            {
                Name = "Integrated Chain",
                FocusSummary = "Owned production chains, warehouses, and protected throughput.",
                Tier = 2,
                EffectiveTier = 3,
                ProgressRatio = 0.68f,
                BonusSummary = "Owned-network revenue +6% | Route losses -4%",
                TradeoffSummary = "Support -3% | Service targets +15%",
                IsActive = true,
                HasHeadquartersBoost = true,
                LeadSummary = "Integrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.",
                NextTierSummary = "Needs +17% doctrine progress to reach Tier III.",
                SteeringSummary = "Best push: Buy 1 more warehouse (+7% progress).",
            };

            Assert.AreEqual(
                "Owned production chains, warehouses, and protected throughput.\nLive tier II -> III | Progress 68%\nLive bonus: Owned-network revenue +6% | Route losses -4%\nLive tradeoff: Support -3% | Service targets +15%\nIntegrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.\nNeeds +17% doctrine progress to reach Tier III.\nBest push: Buy 1 more warehouse (+7% progress).",
                TabletEndgameStatusFormatter.BuildDoctrineDetail(status));
        }

        [TestMethod]
        public void BuildDoctrineDetail_WhenDoctrineIsNotActive_UsesPotentialWording()
        {
            var status = new CompanyDoctrineStatus
            {
                Name = "Client Priority",
                FocusSummary = "Service sites, active routes, and premium district coverage.",
                Tier = 2,
                EffectiveTier = 2,
                ProgressRatio = 0.63f,
                BonusSummary = "Service revenue +5% | Target relief 8% | Opportunity +3%",
                TradeoffSummary = "Corridor upkeep +16%",
                IsActive = false,
                LeadSummary = "Behind Integrated Chain on progress.",
                NextTierSummary = "Needs +22% doctrine progress to reach Tier III.",
                SteeringSummary = "Best push: Run 1 more NPC route (+6% progress).",
            };

            Assert.AreEqual(
                "Service sites, active routes, and premium district coverage.\nRaw tier II | Progress 63%\nPotential bonus: Service revenue +5% | Target relief 8% | Opportunity +3%\nPotential tradeoff: Corridor upkeep +16%\nBehind Integrated Chain on progress.\nNeeds +22% doctrine progress to reach Tier III.\nBest push: Run 1 more NPC route (+6% progress).",
                TabletEndgameStatusFormatter.BuildDoctrineDetail(status));
        }

        [TestMethod]
        public void BuildPrestigeSummaryDetail_UsesBreakdownAndOpportunity()
        {
            var summary = new CompanyEndgameSummary
            {
                PrestigeScore = 58f,
                HighestPrestigeScore = 61f,
                PrestigeBreakdown = new CompanyPrestigeBreakdown
                {
                    MissingScore = 40f,
                    PrimaryOpportunity = "Convert 1 more district competition into a win for +1.5 prestige.",
                    Components = new[]
                    {
                        new CompanyPrestigeComponent { Label = "Doctrine", Score = 24f },
                        new CompanyPrestigeComponent { Label = "Landmark HQ", Score = 24f },
                        new CompanyPrestigeComponent { Label = "Dominant districts", Score = 12f },
                    },
                },
            };

            Assert.AreEqual(
                "Peak 61 | Missing 40\nDoctrine +24 | Landmark HQ +24 | Dominant districts +12\nConvert 1 more district competition into a win for +1.5 prestige.",
                TabletEndgameStatusFormatter.BuildPrestigeSummaryDetail(summary));
        }

        [TestMethod]
        public void BuildSteeringDetail_UsesLeadRuleAndPrimaryOpportunity()
        {
            var summary = new CompanyEndgameSummary
            {
                DoctrineLead = new CompanyDoctrineLeadSummary
                {
                    TieBreakSummary = "Lead order: tier, then progress, then Industrial > Territorial > Service.",
                    ReasonSummary = "Integrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.",
                },
                PrimaryOpportunitySummary = "Next push: install a Landmark HQ (+24 prestige, +1 live tier).",
            };

            Assert.AreEqual(
                "Lead order: tier, then progress, then Industrial > Territorial > Service.\nIntegrated Chain leads on progress at Tier II, ahead of Regional Backbone by 5%.\nNext push: install a Landmark HQ (+24 prestige, +1 live tier).",
                TabletEndgameStatusFormatter.BuildSteeringDetail(summary));
        }
    }
}
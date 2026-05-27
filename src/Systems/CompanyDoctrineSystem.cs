using System;
using System.Collections.Generic;

namespace LSOL.Systems
{
    internal enum CompanyDoctrine
    {
        Balanced = 0,
        Territorial = 1,
        Industrial = 2,
        Service = 3,
    }

    internal enum CompanyDoctrineLeadReasonType
    {
        None = 0,
        Tier = 1,
        ProgressRatio = 2,
        DoctrinePriority = 3,
    }

    internal sealed class CompanyDoctrineProgressComponent
    {
        public string Key { get; set; }

        public string Label { get; set; }

        public float CurrentValue { get; set; }

        public float TargetValue { get; set; }

        public float MissingValue { get; set; }

        public float CompletionRatio { get; set; }

        public float Weight { get; set; }

        public float ContributionRatio { get; set; }

        public float PotentialStepProgressRatio { get; set; }

        public string StatusText { get; set; }

        public string NextStepText { get; set; }
    }

    internal sealed class CompanyDoctrineLeadSummary
    {
        public CompanyDoctrine LeadingDoctrine { get; set; }

        public string LeadingDoctrineName { get; set; }

        public int LeadingTier { get; set; }

        public float LeadingProgressRatio { get; set; }

        public CompanyDoctrine RunnerUpDoctrine { get; set; }

        public string RunnerUpDoctrineName { get; set; }

        public int RunnerUpTier { get; set; }

        public float RunnerUpProgressRatio { get; set; }

        public int TierGap { get; set; }

        public float ProgressGapRatio { get; set; }

        public CompanyDoctrineLeadReasonType ReasonType { get; set; }

        public string ReasonSummary { get; set; }

        public string TieBreakSummary { get; set; }
    }

    internal sealed class CompanyPrestigeComponent
    {
        public string Key { get; set; }

        public string Label { get; set; }

        public float Score { get; set; }

        public float MaxScore { get; set; }

        public float RemainingScore { get; set; }

        public float ImmediateGain { get; set; }

        public string StatusText { get; set; }

        public string OpportunityText { get; set; }
    }

    internal sealed class CompanyPrestigeBreakdown
    {
        public CompanyPrestigeBreakdown()
        {
            Components = Array.Empty<CompanyPrestigeComponent>();
        }

        public IReadOnlyList<CompanyPrestigeComponent> Components { get; set; }

        public float MissingScore { get; set; }

        public string PrimaryOpportunity { get; set; }
    }

    internal sealed class CompanyDoctrineStatus
    {
        public CompanyDoctrine Doctrine { get; set; }

        public string Name { get; set; }

        public string FocusSummary { get; set; }

        public int Tier { get; set; }

        public int EffectiveTier { get; set; }

        public float ProgressRatio { get; set; }

        public string ProgressText { get; set; }

        public string BonusSummary { get; set; }

        public string TradeoffSummary { get; set; }

        public bool IsActive { get; set; }

        public bool HasHeadquartersBoost { get; set; }

        public CompanyDoctrine LeadingDoctrine { get; set; }

        public CompanyDoctrineLeadReasonType LeadReasonType { get; set; }

        public IReadOnlyList<CompanyDoctrineProgressComponent> ProgressComponents { get; set; }

        public int NextTier { get; set; }

        public float NextTierTargetProgressRatio { get; set; }

        public float NextTierGapProgressRatio { get; set; }

        public string NextTierSummary { get; set; }

        public string LeadSummary { get; set; }

        public string SteeringSummary { get; set; }
    }

    internal sealed class CompanyEndgameSummary
    {
        public CompanyDoctrine ActiveDoctrine { get; set; }

        public string ActiveDoctrineName { get; set; }

        public int ActiveDoctrineTier { get; set; }

        public int ActiveDoctrineEffectiveTier { get; set; }

        public bool HasLandmarkHeadquarters { get; set; }

        public float PrestigeScore { get; set; }

        public float HighestPrestigeScore { get; set; }

        public int DominantDistrictCount { get; set; }

        public int CompetitiveWinCount { get; set; }

        public string Headline { get; set; }

        public string Detail { get; set; }

        public CompanyDoctrineLeadSummary DoctrineLead { get; set; }

        public CompanyPrestigeBreakdown PrestigeBreakdown { get; set; }

        public string PrimaryOpportunitySummary { get; set; }
    }

    internal static class CompanyDoctrineSystem
    {
        public static float GetTierThreshold(int tier)
        {
            switch (Math.Max(0, tier))
            {
                case 1:
                    return 0.35f;
                case 2:
                    return 0.60f;
                case 3:
                    return 0.85f;
                default:
                    return 0f;
            }
        }

        public static float GetNextTierThreshold(int currentTier)
        {
            switch (Math.Max(0, currentTier))
            {
                case 0:
                    return GetTierThreshold(1);
                case 1:
                    return GetTierThreshold(2);
                case 2:
                    return GetTierThreshold(3);
                default:
                    return 0f;
            }
        }

        public static int ResolveTier(float progressRatio)
        {
            progressRatio = Clamp01(progressRatio);
            if (progressRatio >= GetTierThreshold(3))
            {
                return 3;
            }

            if (progressRatio >= GetTierThreshold(2))
            {
                return 2;
            }

            if (progressRatio >= GetTierThreshold(1))
            {
                return 1;
            }

            return 0;
        }

        public static int GetEffectiveTier(int tier, bool hasLandmarkHeadquarters, bool isActive)
        {
            var normalizedTier = Math.Max(0, tier);
            if (!isActive || normalizedTier <= 0 || !hasLandmarkHeadquarters)
            {
                return normalizedTier;
            }

            return Math.Min(4, normalizedTier + 1);
        }

        public static string GetName(CompanyDoctrine doctrine)
        {
            switch (doctrine)
            {
                case CompanyDoctrine.Territorial:
                    return "Regional Backbone";
                case CompanyDoctrine.Industrial:
                    return "Integrated Chain";
                case CompanyDoctrine.Service:
                    return "Client Priority";
                default:
                    return "Balanced";
            }
        }

        public static string GetFocusSummary(CompanyDoctrine doctrine)
        {
            switch (doctrine)
            {
                case CompanyDoctrine.Territorial:
                    return "District charters, depots, and corridor rights.";
                case CompanyDoctrine.Industrial:
                    return "Owned production chains, warehouses, and protected throughput.";
                case CompanyDoctrine.Service:
                    return "Service sites, active routes, and premium district coverage.";
                default:
                    return "No doctrine has taken the lead yet.";
            }
        }

        public static float GetTerritorialSupportBonus(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.015f;
        }

        public static float GetTerritorialLicenseRelief(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.03f;
        }

        public static float GetTerritorialCorridorResilience(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.18f;
        }

        public static float GetTerritorialOperationsCostPenalty(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.06f;
        }

        public static float GetTerritorialCompetitionGrowthPenalty(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.05f;
        }

        public static float GetIndustrialRevenueBonus(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.02f;
        }

        public static float GetIndustrialLossMitigation(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.015f;
        }

        public static float GetIndustrialSupportPenalty(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.01f;
        }

        public static float GetIndustrialServiceTargetPenalty(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.05f;
        }

        public static float GetServiceRevenueBonus(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.025f;
        }

        public static float GetServiceTargetReduction(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.04f;
        }

        public static float GetServiceOpportunityBonus(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.015f;
        }

        public static float GetServiceCorridorTargetPenalty(int effectiveTier)
        {
            return Math.Max(0, effectiveTier) * 0.08f;
        }

        public static string BuildTierLabel(int tier)
        {
            switch (Math.Max(0, tier))
            {
                case 4:
                    return "IV";
                case 3:
                    return "III";
                case 2:
                    return "II";
                case 1:
                    return "I";
                default:
                    return "0";
            }
        }

        public static string BuildBonusSummary(CompanyDoctrine doctrine, int effectiveTier)
        {
            switch (doctrine)
            {
                case CompanyDoctrine.Territorial:
                    return string.Format(
                        "Support +{0:0}% | Charter relief {1:0}% | Corridor resilience +{2:0.0}",
                        GetTerritorialSupportBonus(effectiveTier) * 100f,
                        GetTerritorialLicenseRelief(effectiveTier) * 100f,
                        GetTerritorialCorridorResilience(effectiveTier));
                case CompanyDoctrine.Industrial:
                    return string.Format(
                        "Owned-network revenue +{0:0}% | Route losses -{1:0}%",
                        GetIndustrialRevenueBonus(effectiveTier) * 100f,
                        GetIndustrialLossMitigation(effectiveTier) * 100f);
                case CompanyDoctrine.Service:
                    return string.Format(
                        "Service revenue +{0:0}% | Target relief {1:0}% | Opportunity +{2:0}%",
                        GetServiceRevenueBonus(effectiveTier) * 100f,
                        GetServiceTargetReduction(effectiveTier) * 100f,
                        GetServiceOpportunityBonus(effectiveTier) * 100f);
                default:
                    return "No doctrine bonus active.";
            }
        }

        public static string BuildTradeoffSummary(CompanyDoctrine doctrine, int effectiveTier)
        {
            switch (doctrine)
            {
                case CompanyDoctrine.Territorial:
                    return string.Format(
                        "Ops cost +{0:0}% | Competition growth +{1:0}%",
                        GetTerritorialOperationsCostPenalty(effectiveTier) * 100f,
                        GetTerritorialCompetitionGrowthPenalty(effectiveTier) * 100f);
                case CompanyDoctrine.Industrial:
                    return string.Format(
                        "Support -{0:0}% | Service targets +{1:0}%",
                        GetIndustrialSupportPenalty(effectiveTier) * 100f,
                        GetIndustrialServiceTargetPenalty(effectiveTier) * 100f);
                case CompanyDoctrine.Service:
                    return string.Format(
                        "Corridor upkeep +{0:0}%",
                        GetServiceCorridorTargetPenalty(effectiveTier) * 100f);
                default:
                    return "No doctrine tradeoff active.";
            }
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}

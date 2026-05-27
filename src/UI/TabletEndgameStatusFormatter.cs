using System;
using System.Linq;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class TabletEndgameStatusFormatter
    {
        public static string BuildStatusCaption(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return summary.ActiveDoctrineTier > 0
                ? BuildDoctrineTierLine(summary)
                : "Doctrine Bonus Inactive";
        }

        public static string BuildStatusDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return string.Format(
                "Prestige {0:0}/100 | Peak {1:0}\n{2} | HQ {3}\n{4}\n{5}",
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildDoctrineTierLine(summary),
                summary.HasLandmarkHeadquarters ? "Online" : "Offline",
                BuildLeadReason(summary),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildHomeTileDetail(int unlockedCount, int totalCount, CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return string.Format(
                "{0}/{1} unlocked | Prestige {2:0} | Peak {3:0}\n{4} | HQ {5}\n{6}",
                Math.Max(0, unlockedCount),
                Math.Max(0, totalCount),
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildDoctrineTierLine(summary),
                summary.HasLandmarkHeadquarters ? "Online" : "Offline",
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildSteeringDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            var lead = summary.DoctrineLead;
            return string.Format(
                "{0}\n{1}\n{2}",
                lead != null && !string.IsNullOrWhiteSpace(lead.TieBreakSummary)
                    ? lead.TieBreakSummary
                    : "Lead order: tier, then progress, then Industrial > Territorial > Service.",
                BuildLeadReason(summary),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildPrestigeSummaryDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            var breakdown = summary.PrestigeBreakdown ?? new CompanyPrestigeBreakdown();
            return string.Format(
                "Peak {0:0} | Missing {1:0}\n{2}\n{3}",
                BuildPeakPrestige(summary),
                Math.Max(0f, breakdown.MissingScore > 0.0005f ? breakdown.MissingScore : 100f - Math.Max(0f, summary.PrestigeScore)),
                BuildPrestigeStrengthLine(breakdown),
                !string.IsNullOrWhiteSpace(breakdown.PrimaryOpportunity)
                    ? breakdown.PrimaryOpportunity
                    : BuildPrimaryOpportunity(summary));
        }

        public static string BuildPrestigeBreakdownDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return string.Format(
                "Prestige {0:0}/100 | Peak {1:0}\n{2}\n{3}",
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildPrestigeStrengthLine(summary.PrestigeBreakdown ?? new CompanyPrestigeBreakdown()),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildPrestigeComponentDetail(CompanyPrestigeComponent component)
        {
            component = component ?? new CompanyPrestigeComponent();
            return string.Format(
                "{0}\nScore +{1:0.#}/{2:0.#}\n{3}",
                component.StatusText ?? string.Empty,
                Math.Max(0f, component.Score),
                Math.Max(0f, component.MaxScore),
                !string.IsNullOrWhiteSpace(component.OpportunityText)
                    ? "Next: " + component.OpportunityText
                    : "This prestige source is capped for now.");
        }

        public static string BuildDoctrineListDetail(CompanyDoctrineStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return string.Format(
                "{0} | Progress {1}\n{2}\n{3}",
                BuildDoctrineStatusLine(status),
                FormatPercent(status.ProgressRatio),
                status.LeadSummary ?? string.Empty,
                !string.IsNullOrWhiteSpace(status.SteeringSummary)
                    ? status.SteeringSummary
                    : status.NextTierSummary ?? string.Empty);
        }

        public static string BuildDoctrineDetail(CompanyDoctrineStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            var focusSummary = status.FocusSummary ?? string.Empty;
            if (status.Tier <= 0)
            {
                return string.Format(
                    "{0}\nRaw tier 0 | Progress {1}\nTier I bonus inactive.\n{2}\n{3}\n{4}",
                    focusSummary,
                    FormatPercent(status.ProgressRatio),
                    status.LeadSummary ?? string.Empty,
                    status.NextTierSummary ?? string.Empty,
                    status.SteeringSummary ?? string.Empty);
            }

            if (status.IsActive)
            {
                return string.Format(
                    "{0}\n{1} | Progress {2}\nLive bonus: {3}\nLive tradeoff: {4}\n{5}\n{6}\n{7}",
                    focusSummary,
                    BuildDoctrineStatusLine(status),
                    FormatPercent(status.ProgressRatio),
                    status.BonusSummary ?? string.Empty,
                    status.TradeoffSummary ?? string.Empty,
                    status.LeadSummary ?? string.Empty,
                    status.NextTierSummary ?? string.Empty,
                    status.SteeringSummary ?? string.Empty);
            }

            return string.Format(
                "{0}\n{1} | Progress {2}\nPotential bonus: {3}\nPotential tradeoff: {4}\n{5}\n{6}\n{7}",
                focusSummary,
                BuildDoctrineStatusLine(status),
                FormatPercent(status.ProgressRatio),
                status.BonusSummary ?? string.Empty,
                status.TradeoffSummary ?? string.Empty,
                status.LeadSummary ?? string.Empty,
                status.NextTierSummary ?? string.Empty,
                status.SteeringSummary ?? string.Empty);
        }

        public static string BuildDoctrineEffectsDetail(CompanyDoctrineStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            if (status.Tier <= 0)
            {
                return "Tier I not reached yet.\nNo doctrine bonus or tradeoff is live.\nProgress this track to activate the posture.";
            }

            if (status.IsActive)
            {
                return string.Format(
                    "Live bonus: {0}\nLive tradeoff: {1}\n{2}",
                    status.BonusSummary ?? string.Empty,
                    status.TradeoffSummary ?? string.Empty,
                    status.HasHeadquartersBoost
                        ? "HQ Online | Capstone boost applied."
                        : "HQ Offline | Live doctrine active without the capstone boost.");
            }

            return string.Format(
                "Potential bonus: {0}\nPotential tradeoff: {1}\nBonus not live while another doctrine leads.",
                status.BonusSummary ?? string.Empty,
                status.TradeoffSummary ?? string.Empty);
        }

        public static string BuildDoctrineComponentDetail(CompanyDoctrineProgressComponent component)
        {
            component = component ?? new CompanyDoctrineProgressComponent();
            return string.Format(
                "{0}\nWeight {1:0}% | Contribution +{2:0}%\n{3}",
                component.StatusText ?? string.Empty,
                Math.Max(0f, component.Weight) * 100f,
                Math.Max(0f, component.ContributionRatio) * 100f,
                component.MissingValue > 0.0005f && component.PotentialStepProgressRatio > 0.0005f
                    ? string.Format("Next: {0} (+{1:0}% progress).", component.NextStepText ?? string.Empty, component.PotentialStepProgressRatio * 100f)
                    : "Component capped.");
        }

        private static string BuildDoctrineTierLine(CompanyEndgameSummary summary)
        {
            if (summary == null || summary.ActiveDoctrineTier <= 0)
            {
                return "No leading doctrine yet";
            }

            var doctrineName = !string.IsNullOrWhiteSpace(summary.ActiveDoctrineName)
                ? summary.ActiveDoctrineName
                : CompanyDoctrineSystem.GetName(summary.ActiveDoctrine);
            var rawTierLabel = CompanyDoctrineSystem.BuildTierLabel(summary.ActiveDoctrineTier);
            if (summary.ActiveDoctrineEffectiveTier > summary.ActiveDoctrineTier)
            {
                return string.Format(
                    "{0} {1} -> {2}",
                    doctrineName,
                    rawTierLabel,
                    CompanyDoctrineSystem.BuildTierLabel(summary.ActiveDoctrineEffectiveTier));
            }

            return string.Format("{0} {1}", doctrineName, rawTierLabel);
        }

        private static string BuildDoctrineStatusLine(CompanyDoctrineStatus status)
        {
            if (status == null || status.Tier <= 0)
            {
                return "Raw tier 0";
            }

            var rawTierLabel = CompanyDoctrineSystem.BuildTierLabel(status.Tier);
            if (status.IsActive && status.EffectiveTier > status.Tier)
            {
                return string.Format("Live tier {0} -> {1}", rawTierLabel, CompanyDoctrineSystem.BuildTierLabel(status.EffectiveTier));
            }

            if (status.IsActive)
            {
                return string.Format("Live tier {0}", rawTierLabel);
            }

            return string.Format("Raw tier {0}", rawTierLabel);
        }

        private static float BuildPeakPrestige(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return Math.Max(Math.Max(0f, summary.PrestigeScore), Math.Max(0f, summary.HighestPrestigeScore));
        }

        private static string BuildLeadReason(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            if (summary.DoctrineLead != null && !string.IsNullOrWhiteSpace(summary.DoctrineLead.ReasonSummary))
            {
                return summary.DoctrineLead.ReasonSummary;
            }

            if (summary.ActiveDoctrineTier <= 0)
            {
                return "No doctrine has reached Tier I yet.";
            }

            return summary.HasLandmarkHeadquarters && summary.ActiveDoctrineEffectiveTier > summary.ActiveDoctrineTier
                ? "HQ is boosting the live doctrine."
                : "Doctrine bonus is live.";
        }

        private static string BuildPrimaryOpportunity(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            if (!string.IsNullOrWhiteSpace(summary.PrimaryOpportunitySummary))
            {
                return summary.PrimaryOpportunitySummary;
            }

            if (summary.ActiveDoctrineTier <= 0)
            {
                return "Next push: reach Tier I to activate a doctrine bonus.";
            }

            if (!summary.HasLandmarkHeadquarters)
            {
                return "Next push: install a Landmark HQ (+24 prestige, +1 live tier).";
            }

            return "Next push: keep growing the live doctrine and prestige score.";
        }

        private static string BuildPrestigeStrengthLine(CompanyPrestigeBreakdown breakdown)
        {
            breakdown = breakdown ?? new CompanyPrestigeBreakdown();
            var segments = (breakdown.Components ?? Array.Empty<CompanyPrestigeComponent>())
                .Where(component => component != null && component.Score > 0.0005f)
                .OrderByDescending(component => component.Score)
                .Take(3)
                .Select(component => string.Format("{0} +{1:0.#}", component.Label, component.Score))
                .ToArray();
            return segments.Length > 0
                ? string.Join(" | ", segments)
                : "No active prestige contributors yet.";
        }

        private static string FormatPercent(float ratio)
        {
            return string.Format("{0:0}%", Math.Max(0f, ratio) * 100f);
        }
    }
}
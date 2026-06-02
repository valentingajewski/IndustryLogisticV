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
                : Text("tablet.endgame.statusCaption.inactive", "Doctrine Bonus Inactive");
        }

        public static string BuildStatusDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return string.Format(
                Text("tablet.endgame.statusDetail", "Prestige {0:0}/100 | Peak {1:0}\n{2} | HQ {3}\n{4}\n{5}"),
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildDoctrineTierLine(summary),
                summary.HasLandmarkHeadquarters ? Text("tablet.common.online", "Online") : Text("tablet.common.offline", "Offline"),
                BuildLeadReason(summary),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildHomeTileDetail(int unlockedCount, int totalCount, CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            return string.Format(
                Text("tablet.endgame.homeTileDetail", "{0}/{1} unlocked | Prestige {2:0} | Peak {3:0}\n{4} | HQ {5}\n{6}"),
                Math.Max(0, unlockedCount),
                Math.Max(0, totalCount),
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildDoctrineTierLine(summary),
                summary.HasLandmarkHeadquarters ? Text("tablet.common.online", "Online") : Text("tablet.common.offline", "Offline"),
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
                    : Text("tablet.endgame.leadRuleFallback", "Lead order: tier, then progress, then Industrial > Territorial > Service."),
                BuildLeadReason(summary),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildPrestigeSummaryDetail(CompanyEndgameSummary summary)
        {
            summary = summary ?? new CompanyEndgameSummary();
            var breakdown = summary.PrestigeBreakdown ?? new CompanyPrestigeBreakdown();
            return string.Format(
                Text("tablet.endgame.prestigeSummaryDetail", "Peak {0:0} | Missing {1:0}\n{2}\n{3}"),
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
                Text("tablet.endgame.prestigeBreakdownDetail", "Prestige {0:0}/100 | Peak {1:0}\n{2}\n{3}"),
                Math.Max(0f, summary.PrestigeScore),
                BuildPeakPrestige(summary),
                BuildPrestigeStrengthLine(summary.PrestigeBreakdown ?? new CompanyPrestigeBreakdown()),
                BuildPrimaryOpportunity(summary));
        }

        public static string BuildPrestigeComponentDetail(CompanyPrestigeComponent component)
        {
            component = component ?? new CompanyPrestigeComponent();
            return string.Format(
                Text("tablet.endgame.prestigeComponentDetail", "{0}\nScore +{1:0.#}/{2:0.#}\n{3}"),
                component.StatusText ?? string.Empty,
                Math.Max(0f, component.Score),
                Math.Max(0f, component.MaxScore),
                !string.IsNullOrWhiteSpace(component.OpportunityText)
                    ? Text("tablet.endgame.nextPrefix", "Next: ") + component.OpportunityText
                    : Text("tablet.endgame.prestigeSourceCapped", "This prestige source is capped for now."));
        }

        public static string BuildDoctrineListDetail(CompanyDoctrineStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return string.Format(
                Text("tablet.endgame.doctrineListDetail", "{0} | Progress {1}\n{2}\n{3}"),
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
                    Text("tablet.endgame.doctrineDetail.rawTier", "{0}\nRaw tier 0 | Progress {1}\nTier I bonus inactive.\n{2}\n{3}\n{4}"),
                    focusSummary,
                    FormatPercent(status.ProgressRatio),
                    status.LeadSummary ?? string.Empty,
                    status.NextTierSummary ?? string.Empty,
                    status.SteeringSummary ?? string.Empty);
            }

            if (status.IsActive)
            {
                return string.Format(
                    Text("tablet.endgame.doctrineDetail.live", "{0}\n{1} | Progress {2}\nLive bonus: {3}\nLive tradeoff: {4}\n{5}\n{6}\n{7}"),
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
                Text("tablet.endgame.doctrineDetail.potential", "{0}\n{1} | Progress {2}\nPotential bonus: {3}\nPotential tradeoff: {4}\n{5}\n{6}\n{7}"),
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
                return Text("tablet.endgame.doctrineEffects.inactive", "Tier I not reached yet.\nNo doctrine bonus or tradeoff is live.\nProgress this track to activate the posture.");
            }

            if (status.IsActive)
            {
                return string.Format(
                    Text("tablet.endgame.doctrineEffects.live", "Live bonus: {0}\nLive tradeoff: {1}\n{2}"),
                    status.BonusSummary ?? string.Empty,
                    status.TradeoffSummary ?? string.Empty,
                    status.HasHeadquartersBoost
                        ? Text("tablet.endgame.doctrineEffects.hqOnline", "HQ Online | Capstone boost applied.")
                        : Text("tablet.endgame.doctrineEffects.hqOffline", "HQ Offline | Live doctrine active without the capstone boost."));
            }

            return string.Format(
                Text("tablet.endgame.doctrineEffects.potential", "Potential bonus: {0}\nPotential tradeoff: {1}\nBonus not live while another doctrine leads."),
                status.BonusSummary ?? string.Empty,
                status.TradeoffSummary ?? string.Empty);
        }

        public static string BuildDoctrineComponentDetail(CompanyDoctrineProgressComponent component)
        {
            component = component ?? new CompanyDoctrineProgressComponent();
            return string.Format(
                Text("tablet.endgame.doctrineComponentDetail", "{0}\nWeight {1:0}% | Contribution +{2:0}%\n{3}"),
                component.StatusText ?? string.Empty,
                Math.Max(0f, component.Weight) * 100f,
                Math.Max(0f, component.ContributionRatio) * 100f,
                component.MissingValue > 0.0005f && component.PotentialStepProgressRatio > 0.0005f
                    ? string.Format(Text("tablet.endgame.doctrineComponentNext", "Next: {0} (+{1:0}% progress)."), component.NextStepText ?? string.Empty, component.PotentialStepProgressRatio * 100f)
                    : Text("tablet.endgame.doctrineComponentCapped", "Component capped."));
        }

        private static string BuildDoctrineTierLine(CompanyEndgameSummary summary)
        {
            if (summary == null || summary.ActiveDoctrineTier <= 0)
            {
                return Text("tablet.endgame.noLeadingDoctrine", "No leading doctrine yet");
            }

            var doctrineName = !string.IsNullOrWhiteSpace(summary.ActiveDoctrineName)
                ? summary.ActiveDoctrineName
                : CompanyDoctrineSystem.GetName(summary.ActiveDoctrine);
            var rawTierLabel = CompanyDoctrineSystem.BuildTierLabel(summary.ActiveDoctrineTier);
            if (summary.ActiveDoctrineEffectiveTier > summary.ActiveDoctrineTier)
            {
                return string.Format(
                    Text("tablet.endgame.doctrineTierBoosted", "{0} {1} -> {2}"),
                    doctrineName,
                    rawTierLabel,
                    CompanyDoctrineSystem.BuildTierLabel(summary.ActiveDoctrineEffectiveTier));
            }

            return string.Format(Text("tablet.endgame.doctrineTier", "{0} {1}"), doctrineName, rawTierLabel);
        }

        private static string BuildDoctrineStatusLine(CompanyDoctrineStatus status)
        {
            if (status == null || status.Tier <= 0)
            {
                return Text("tablet.endgame.rawTierZero", "Raw tier 0");
            }

            var rawTierLabel = CompanyDoctrineSystem.BuildTierLabel(status.Tier);
            if (status.IsActive && status.EffectiveTier > status.Tier)
            {
                return string.Format(Text("tablet.endgame.liveTierBoosted", "Live tier {0} -> {1}"), rawTierLabel, CompanyDoctrineSystem.BuildTierLabel(status.EffectiveTier));
            }

            if (status.IsActive)
            {
                return string.Format(Text("tablet.endgame.liveTier", "Live tier {0}"), rawTierLabel);
            }

            return string.Format(Text("tablet.endgame.rawTier", "Raw tier {0}"), rawTierLabel);
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
                return Text("tablet.endgame.leadReason.noTierOne", "No doctrine has reached Tier I yet.");
            }

            return summary.HasLandmarkHeadquarters && summary.ActiveDoctrineEffectiveTier > summary.ActiveDoctrineTier
                ? Text("tablet.endgame.leadReason.hqBoost", "HQ is boosting the live doctrine.")
                : Text("tablet.endgame.leadReason.live", "Doctrine bonus is live.");
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
                return Text("tablet.endgame.primaryOpportunity.tierOne", "Next push: reach Tier I to activate a doctrine bonus.");
            }

            if (!summary.HasLandmarkHeadquarters)
            {
                return Text("tablet.endgame.primaryOpportunity.hq", "Next push: install a Landmark HQ (+24 prestige, +1 live tier).");
            }

            return Text("tablet.endgame.primaryOpportunity.grow", "Next push: keep growing the live doctrine and prestige score.");
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
                : Text("tablet.endgame.noPrestigeContributors", "No active prestige contributors yet.");
        }

        private static string FormatPercent(float ratio)
        {
            return string.Format("{0:0}%", Math.Max(0f, ratio) * 100f);
        }

        private static string Text(string key, string english)
        {
            return LocalizedText.GetOrDefault(key, english);
        }
    }
}
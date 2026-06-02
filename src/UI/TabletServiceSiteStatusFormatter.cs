using System;
using System.Collections.Generic;

namespace LSOL.UI
{
    internal static class TabletServiceSiteStatusFormatter
    {
        private const float ServiceTargetDisplayThresholdTons = 0.01f;
        private const int MaxPenaltySteps = 2;

        public static string BuildWeeklyTargetDetail(TabletLocationSummary summary)
        {
            if (summary == null || summary.ServiceRequiredWeeklyTons <= ServiceTargetDisplayThresholdTons)
            {
                return string.Empty;
            }

            var currentTons = Math.Max(0f, summary.ServiceCurrentWeekTons);
            var requiredTons = Math.Max(0f, summary.ServiceRequiredWeeklyTons);
            var remainingTons = Math.Max(0f, requiredTons - currentTons);
            var deliveries = Math.Max(0, summary.ServiceCurrentWeekDeliveries);
            var progressStatus = remainingTons <= ServiceTargetDisplayThresholdTons
                ? LocalizedText.GetOrDefault("tablet.service.weeklyTarget.met", "Met this week")
                : LocalizedText.FormatOrDefault("tablet.service.weeklyTarget.remaining", "{0:0.0}t remaining", remainingTons);

            return LocalizedText.FormatOrDefault(
                "tablet.service.weeklyTarget.detail",
                "{0:0.0}/{1:0.0}t | {2} | {3} {4}",
                currentTons,
                requiredTons,
                progressStatus,
                deliveries,
                deliveries == 1
                    ? LocalizedText.GetOrDefault("tablet.service.weeklyTarget.delivery", "delivery")
                    : LocalizedText.GetOrDefault("tablet.service.weeklyTarget.deliveries", "deliveries"));
        }

        public static string BuildContractStatusDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            if (!summary.IsOwnedByPlayer && HasServiceReadiness(summary))
            {
                var openMarketStatus = !string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                    ? summary.ServiceContractStatus
                    : LocalizedText.GetOrDefault("tablet.service.contractStatus.openMarket", "Open market");
                return LocalizedText.FormatOrDefault("tablet.service.contractStatus.lockedUntilPurchase", "{0} | Locked until purchase", openMarketStatus);
            }

            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                && !string.Equals(summary.ServiceContractStatus, "Open market", StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(summary.ServiceContractStatus);
            }

            var pressureDetail = BuildPressureDetail(summary);
            if (!string.IsNullOrWhiteSpace(pressureDetail))
            {
                segments.Add(pressureDetail);
            }

            var lastWeekDetail = BuildLastWeekOutcomeDetail(summary);
            if (!string.IsNullOrWhiteSpace(lastWeekDetail))
            {
                segments.Add(lastWeekDetail);
            }

            return segments.Count > 0
                ? string.Join(" | ", segments.ToArray())
                : string.Empty;
        }

        private static string BuildPressureDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            if (summary.ServicePenaltySteps > 0)
            {
                segments.Add(LocalizedText.FormatOrDefault("tablet.service.pressure.penalty", "Penalty {0}/{1}", Math.Max(0, summary.ServicePenaltySteps), MaxPenaltySteps));
            }

            if (summary.ServiceSuccessStreak > 0)
            {
                segments.Add(summary.ServicePenaltySteps > 0
                    ? LocalizedText.FormatOrDefault("tablet.service.pressure.recovery", "Recovery {0}", summary.ServiceSuccessStreak)
                    : LocalizedText.FormatOrDefault("tablet.service.pressure.streak", "Streak {0}", summary.ServiceSuccessStreak));
            }

            return segments.Count > 0
                ? string.Join(" | ", segments.ToArray())
                : string.Empty;
        }

        private static string BuildLastWeekOutcomeDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            if (summary.ServiceTargetMetLastWeek)
            {
                return LocalizedText.GetOrDefault("tablet.service.lastWeek.met", "Last week met");
            }

            if (summary.ServicePenaltySteps > 0)
            {
                return LocalizedText.GetOrDefault("tablet.service.lastWeek.missed", "Last week missed");
            }

            return summary.ServiceRequiredWeeklyTons > ServiceTargetDisplayThresholdTons
                || summary.ServiceCurrentWeekDeliveries > 0
                || summary.ServiceCurrentWeekTons > ServiceTargetDisplayThresholdTons
                || !string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                ? LocalizedText.GetOrDefault("tablet.service.lastWeek.pending", "Last week pending")
                : string.Empty;
        }

        private static bool HasServiceReadiness(TabletLocationSummary summary)
        {
            return summary != null
                && (summary.HasServiceContractInfo
                    || summary.ServiceRequiredWeeklyTons > ServiceTargetDisplayThresholdTons
                    || summary.ServiceCurrentWeekDeliveries > 0
                    || summary.ServiceCurrentWeekTons > ServiceTargetDisplayThresholdTons
                    || !string.IsNullOrWhiteSpace(summary.ServiceContractStatus));
        }
    }
}
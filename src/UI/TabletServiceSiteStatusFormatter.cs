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
                ? LocalizedText.Get("tablet.service.weeklyTarget.met")
                : LocalizedText.Format("tablet.service.weeklyTarget.remaining", remainingTons);

            return LocalizedText.Format(
                "tablet.service.weeklyTarget.detail",
                currentTons,
                requiredTons,
                progressStatus,
                deliveries,
                deliveries == 1
                    ? LocalizedText.Get("tablet.service.weeklyTarget.delivery")
                    : LocalizedText.Get("tablet.service.weeklyTarget.deliveries"));
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
                    ? ServiceStatusCatalog.Display(summary.ServiceContractStatus)
                    : LocalizedText.Get("tablet.service.contractStatus.openMarket");
                return LocalizedText.Format("tablet.service.contractStatus.lockedUntilPurchase", openMarketStatus);
            }

            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                && !ServiceStatusCatalog.IsOpenMarket(summary.ServiceContractStatus))
            {
                segments.Add(ServiceStatusCatalog.Display(summary.ServiceContractStatus));
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
                segments.Add(LocalizedText.Format("tablet.service.pressure.penalty", Math.Max(0, summary.ServicePenaltySteps), MaxPenaltySteps));
            }

            if (summary.ServiceSuccessStreak > 0)
            {
                segments.Add(summary.ServicePenaltySteps > 0
                    ? LocalizedText.Format("tablet.service.pressure.recovery", summary.ServiceSuccessStreak)
                    : LocalizedText.Format("tablet.service.pressure.streak", summary.ServiceSuccessStreak));
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
                return LocalizedText.Get("tablet.service.lastWeek.met");
            }

            if (summary.ServicePenaltySteps > 0)
            {
                return LocalizedText.Get("tablet.service.lastWeek.missed");
            }

            return summary.ServiceRequiredWeeklyTons > ServiceTargetDisplayThresholdTons
                || summary.ServiceCurrentWeekDeliveries > 0
                || summary.ServiceCurrentWeekTons > ServiceTargetDisplayThresholdTons
                || !string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                ? LocalizedText.Get("tablet.service.lastWeek.pending")
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
using System;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class TabletDeadlineFormatter
    {
        public static string FormatDueInMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return LocalizedText.GetOrDefault("tablet.deadline.now", "now");
            }

            var days = minutes / (24 * 60);
            var hours = (minutes % (24 * 60)) / 60;
            var remainingMinutes = minutes % 60;
            if (days > 0)
            {
                return hours > 0
                    ? LocalizedText.FormatOrDefault("tablet.deadline.inDaysHours", "in {0}d {1}h", days, hours)
                    : LocalizedText.FormatOrDefault("tablet.deadline.inDays", "in {0}d", days);
            }

            if (hours > 0)
            {
                return remainingMinutes > 0
                    ? LocalizedText.FormatOrDefault("tablet.deadline.inHoursMinutes", "in {0}h {1}m", hours, remainingMinutes)
                    : LocalizedText.FormatOrDefault("tablet.deadline.inHours", "in {0}h", hours);
            }

            return LocalizedText.FormatOrDefault("tablet.deadline.inMinutes", "in {0}m", remainingMinutes);
        }

        public static string BuildPlayerContractExpiryLabel(PlayerContractListingSummary contract)
        {
            return contract == null
                ? string.Empty
                : BuildPlayerContractExpiryLabel(contract.RemainingExpiryMinutes);
        }

        public static string BuildPlayerContractExpiryLabel(int remainingMinutes)
        {
            return remainingMinutes == int.MaxValue
                ? LocalizedText.GetOrDefault("tablet.deadline.noExpiry", "No expiry")
                : LocalizedText.FormatOrDefault("tablet.deadline.minutesLeft", "{0}m left", Math.Max(0, remainingMinutes));
        }
    }
}
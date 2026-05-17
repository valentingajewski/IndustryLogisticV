using System;

namespace LSOL.Systems
{
    internal static class AmbientWorldDispatchText
    {
        private const string LegacySpotListedStatus = "Spot market window open";
        private const string LegacyRivalListedStatus = "Rival freight listed";
        private const string LegacyRivalTravelStatus = "Rival convoy en route";
        private const string LegacySpotTravelStatus = "Spot window closed; NPC convoy en route";
        private const string LegacyRivalHiddenPrefix = "Rival convoy hidden";
        private const string LegacyRivalPathFailureStatus = "Rival convoy hidden after repeated path failure.";

        public static string BuildQueuedStatusText()
        {
            return "Queued for dispatch";
        }

        public static string BuildTravelingStatusText()
        {
            return "NPC convoy en route";
        }

        public static string BuildRepeatedPathFailureStatusText()
        {
            return "Ambient convoy hidden after repeated path failure.";
        }

        public static string BuildHiddenStatusText(string failureReason)
        {
            var reason = string.IsNullOrWhiteSpace(failureReason)
                ? "visual spawn unavailable"
                : failureReason.Trim().TrimEnd('.');
            return string.Format("Ambient convoy hidden: {0}.", reason);
        }

        public static string BuildBlipName(string commodity)
        {
            var normalizedCommodity = string.IsNullOrWhiteSpace(commodity)
                ? string.Empty
                : commodity.Trim();
            return string.IsNullOrWhiteSpace(normalizedCommodity)
                ? "Ambient Freight"
                : string.Format("Ambient Freight: {0}", normalizedCommodity);
        }

        public static string FormatJobType(NpcWorldJobType type)
        {
            switch (type)
            {
                case NpcWorldJobType.OverflowRescue:
                    return "Overflow";
                case NpcWorldJobType.ShortageRelief:
                    return "Shortage";
                case NpcWorldJobType.ExternalImport:
                    return "Import";
                case NpcWorldJobType.ExternalExport:
                    return "Export";
                case NpcWorldJobType.WarehouseBalancing:
                    return "Warehouse";
                case NpcWorldJobType.ServiceRun:
                    return "Service";
                case NpcWorldJobType.RivalFreight:
                    return "Freight";
                default:
                    return "Dispatch";
            }
        }

        public static string NormalizeStatusText(string statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return string.Empty;
            }

            var normalized = statusText.Trim();
            if (string.Equals(normalized, LegacySpotListedStatus, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, LegacyRivalListedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return BuildQueuedStatusText();
            }

            if (string.Equals(normalized, LegacyRivalTravelStatus, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, LegacySpotTravelStatus, StringComparison.OrdinalIgnoreCase))
            {
                return BuildTravelingStatusText();
            }

            if (string.Equals(normalized, LegacyRivalPathFailureStatus, StringComparison.OrdinalIgnoreCase))
            {
                return BuildRepeatedPathFailureStatusText();
            }

            if (normalized.StartsWith(LegacyRivalHiddenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return "Ambient convoy hidden" + normalized.Substring(LegacyRivalHiddenPrefix.Length);
            }

            return normalized;
        }
    }
}
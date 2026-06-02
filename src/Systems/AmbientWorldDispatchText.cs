using System;
using LSOL.UI;

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
            return LocalizedText.GetOrDefault("tablet.dispatch.status.queued", "Queued for dispatch");
        }

        public static string BuildTravelingStatusText()
        {
            return LocalizedText.GetOrDefault("tablet.dispatch.status.traveling", "NPC convoy en route");
        }

        public static string BuildRepeatedPathFailureStatusText()
        {
            return LocalizedText.GetOrDefault("tablet.dispatch.status.pathFailure", "Ambient convoy hidden after repeated path failure.");
        }

        public static string BuildHiddenStatusText(string failureReason)
        {
            var reason = string.IsNullOrWhiteSpace(failureReason)
                ? LocalizedText.GetOrDefault("tablet.dispatch.status.hiddenReasonDefault", "visual spawn unavailable")
                : failureReason.Trim().TrimEnd('.');
            return LocalizedText.FormatOrDefault("tablet.dispatch.status.hidden", "Ambient convoy hidden: {0}.", reason);
        }

        public static string BuildBlipName(string commodity)
        {
            var normalizedCommodity = string.IsNullOrWhiteSpace(commodity)
                ? string.Empty
                : commodity.Trim();
            return string.IsNullOrWhiteSpace(normalizedCommodity)
                ? LocalizedText.GetOrDefault("tablet.dispatch.blip.generic", "Ambient Freight")
                : LocalizedText.FormatOrDefault("tablet.dispatch.blip.commodity", "Ambient Freight: {0}", normalizedCommodity);
        }

        public static string FormatJobType(NpcWorldJobType type)
        {
            switch (type)
            {
                case NpcWorldJobType.OverflowRescue:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.overflow", "Overflow");
                case NpcWorldJobType.ShortageRelief:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.shortage", "Shortage");
                case NpcWorldJobType.ExternalImport:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.import", "Import");
                case NpcWorldJobType.ExternalExport:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.export", "Export");
                case NpcWorldJobType.WarehouseBalancing:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.warehouse", "Warehouse");
                case NpcWorldJobType.ServiceRun:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.service", "Service");
                case NpcWorldJobType.RivalFreight:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.freight", "Freight");
                default:
                    return LocalizedText.GetOrDefault("tablet.dispatch.jobType.dispatch", "Dispatch");
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
                return LocalizedText.GetOrDefault("tablet.dispatch.status.hiddenPrefix", "Ambient convoy hidden") + normalized.Substring(LegacyRivalHiddenPrefix.Length);
            }

            return normalized;
        }
    }
}
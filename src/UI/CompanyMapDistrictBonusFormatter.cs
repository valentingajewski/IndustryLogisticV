using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LSOL.Systems;

namespace LSOL.UI
{
    /// <summary>
    /// Renders the perishable side job district bonus for the Company Hub. Kept separate from the
    /// controller so the exact wording can be unit tested.
    /// </summary>
    internal static class CompanyMapDistrictBonusFormatter
    {
        public static string BuildCaption(DistrictBonusBreakdown breakdown)
        {
            var cap = ResolveCap(breakdown);
            var total = breakdown != null ? Math.Max(0f, breakdown.TotalPercent) : 0f;
            return string.Format(CultureInfo.InvariantCulture, "District bonus +{0:0.#}% / {1:0.#}%", total, cap);
        }

        /// <summary>
        /// Per-job attribution plus how long the bonus survives. The pool order is already sorted by
        /// contribution, so the first entry is always the job that helped most.
        /// </summary>
        public static string BuildDetail(DistrictBonusBreakdown breakdown)
        {
            if (breakdown == null || !breakdown.HasAny)
            {
                return "No side job bonus banked. Garbage and towing work in this district builds it.";
            }

            var builder = new StringBuilder();
            var pools = breakdown.Pools
                .Where(pool => pool != null && pool.Points > DistrictBonusCatalog.MinimumMeaningfulPoints)
                .ToList();
            for (int i = 0; i < pools.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0} {1:0.#}%",
                    pools[i].Label,
                    pools[i].Points);
            }

            if (builder.Length == 0)
            {
                builder.Append("No side job bonus banked.");
            }

            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                " - fades in ~{0:0.#}h",
                Math.Max(0f, breakdown.RemainingInGameHours));
            return builder.ToString();
        }

        /// <summary>Meter ratio for the menu progress bar.</summary>
        public static float GetProgressRatio(DistrictBonusBreakdown breakdown)
        {
            if (breakdown == null)
            {
                return 0f;
            }

            var cap = ResolveCap(breakdown);
            if (cap <= 0.001f)
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(1f, breakdown.TotalPercent / cap));
        }

        /// <summary>Short suffix for the district ledger rows; empty when nothing is banked.</summary>
        public static string BuildShortSuffix(DistrictBonusBreakdown breakdown)
        {
            if (breakdown == null || !breakdown.HasAny)
            {
                return string.Empty;
            }

            return string.Format(CultureInfo.InvariantCulture, " | Bonus +{0:0.#}%", breakdown.TotalPercent);
        }

        /// <summary>Compact "<total>% (top job)" summary used by the tablet district list.</summary>
        public static string BuildTabletSummary(float totalPercent, string topContributorLabel)
        {
            if (totalPercent <= DistrictBonusCatalog.MinimumMeaningfulPoints)
            {
                return "Bonus none";
            }

            return string.IsNullOrWhiteSpace(topContributorLabel)
                ? string.Format(CultureInfo.InvariantCulture, "Bonus +{0:0.#}%", totalPercent)
                : string.Format(CultureInfo.InvariantCulture, "Bonus +{0:0.#}% ({1})", totalPercent, topContributorLabel);
        }

        private static float ResolveCap(DistrictBonusBreakdown breakdown)
        {
            return breakdown != null && breakdown.CapPercent > 0.001f
                ? breakdown.CapPercent
                : DistrictBonusCatalog.CapPercent;
        }
    }
}

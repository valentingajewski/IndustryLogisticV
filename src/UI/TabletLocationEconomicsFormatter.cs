using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.UI
{
    internal static class TabletLocationEconomicsFormatter
    {
        public static string BuildOwnerCutDetail(TabletLocationSummary summary)
        {
            if (summary == null || summary.Industry == null)
            {
                return string.Empty;
            }

            var ownerCut = Math.Max(0f, summary.Industry.IndustryOwnerCut);
            if (ownerCut <= 0.001f)
            {
                return string.Empty;
            }

            var ownerCutPercent = ModFormatting.FormatPercent(ownerCut * 100f);
            return summary.IsOwnedByPlayer
                ? string.Format("Previous owner cut {0} removed | Owned sites keep the full payout", ownerCutPercent)
                : string.Format("Current owner cut {0} | Purchase removes it from site payouts", ownerCutPercent);
        }

        public static string BuildPassiveIncomeDetail(TabletLocationSummary summary)
        {
            if (summary == null || !summary.HasServiceBusinessInfo)
            {
                return string.Empty;
            }

            var weeklyIncome = Math.Max(0f, summary.ServiceWeeklyIncome);
            var staffingCost = Math.Max(0f, summary.ServiceWeeklyStaffingCost);
            if (weeklyIncome <= 0.01f && staffingCost <= 0.01f && summary.ServiceLastPassiveIncome <= 0.01f)
            {
                return string.Empty;
            }

            var segments = new List<string>
            {
                string.Format(
                    "{0} {1}/wk",
                    summary.IsOwnedByPlayer ? "Expected" : "Potential",
                    ModFormatting.FormatMoney(weeklyIncome)),
                string.Format("Staff {0}/wk", ModFormatting.FormatMoney(staffingCost)),
            };

            if (!string.IsNullOrWhiteSpace(summary.ServicePassiveIncomeStatus))
            {
                segments.Add(summary.ServicePassiveIncomeStatus);
            }

            if (summary.IsOwnedByPlayer)
            {
                if (summary.ServiceLastPassiveIncome > 0.01f)
                {
                    segments.Add(string.Format("Last payout {0}", ModFormatting.FormatMoney(summary.ServiceLastPassiveIncome)));
                }
                else if (!string.IsNullOrWhiteSpace(summary.ServiceRecentPayoutStatus))
                {
                    segments.Add(summary.ServiceRecentPayoutStatus);
                }
            }
            else
            {
                segments.Add("Needs operator and stock");
            }

            return string.Join(" | ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).ToArray());
        }
    }
}
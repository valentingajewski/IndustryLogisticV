using System;

namespace LSOL.UI
{
    internal sealed class BankOfferComparisonRow
    {
        public string BankId { get; set; }

        public string DisplayName { get; set; }

        public bool NameIsDuplicated { get; set; }

        public bool IsCurrentBranch { get; set; }

        public float RequestedPrincipal { get; set; }

        public float MaxAvailablePrincipal { get; set; }

        public float OfferedRatePercent { get; set; }

        public float PreviewPrincipal { get; set; }

        public float PreviewTotalRepayment { get; set; }

        public float PreviewWeeklyInstallment { get; set; }
    }

    internal static class BankOfferComparisonFormatter
    {
        public static string BuildBranchLabel(string displayName, string bankId, bool disambiguate)
        {
            var resolvedName = string.IsNullOrWhiteSpace(displayName)
                ? "Bank"
                : displayName.Trim();
            if (!disambiguate || string.IsNullOrWhiteSpace(bankId))
            {
                return resolvedName;
            }

            return string.Format("{0} #{1}", resolvedName, bankId.Trim());
        }

        public static string BuildCaption(BankOfferComparisonRow row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var label = BuildBranchLabel(row.DisplayName, row.BankId, row.NameIsDuplicated);
            return row.IsCurrentBranch
                ? string.Format("{0} [HERE]", label)
                : label;
        }

        public static string BuildDetail(BankOfferComparisonRow row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            if (IsCapped(row))
            {
                return string.Format(
                    "Requested {0} exceeds max {1} | Rate {2} | Capped total {3} | Capped installment {4}/w",
                    ModFormatting.FormatMoney(Math.Max(0f, row.RequestedPrincipal)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.MaxAvailablePrincipal)),
                    ModFormatting.FormatPercent(Math.Max(0f, row.OfferedRatePercent)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.PreviewTotalRepayment)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.PreviewWeeklyInstallment)));
            }

            return string.Format(
                "Rate {0} | Installment {1}/w | Total {2} | Max {3}",
                ModFormatting.FormatPercent(Math.Max(0f, row.OfferedRatePercent)),
                ModFormatting.FormatMoney(Math.Max(0f, row.PreviewWeeklyInstallment)),
                ModFormatting.FormatMoney(Math.Max(0f, row.PreviewTotalRepayment)),
                ModFormatting.FormatMoney(Math.Max(0f, row.MaxAvailablePrincipal)));
        }

        private static bool IsCapped(BankOfferComparisonRow row)
        {
            return row != null && row.RequestedPrincipal - row.MaxAvailablePrincipal > 0.01f;
        }
    }
}
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
                ? LocalizedText.Get(ModTextKey.BankingMenuTitle)
                : displayName.Trim();
            if (!disambiguate || string.IsNullOrWhiteSpace(bankId))
            {
                return resolvedName;
            }

            return LocalizedText.Format(ModTextKey.BankingValueBranchWithId, resolvedName, bankId.Trim());
        }

        public static string BuildCaption(BankOfferComparisonRow row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var label = BuildBranchLabel(row.DisplayName, row.BankId, row.NameIsDuplicated);
            return row.IsCurrentBranch
                ? LocalizedText.Format(ModTextKey.BankingValueCurrentBranchHere, label)
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
                return LocalizedText.Format(
                    ModTextKey.BankingDetailComparisonCapped,
                    ModFormatting.FormatMoney(Math.Max(0f, row.RequestedPrincipal)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.MaxAvailablePrincipal)),
                    ModFormatting.FormatPercent(Math.Max(0f, row.OfferedRatePercent)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.PreviewTotalRepayment)),
                    ModFormatting.FormatMoney(Math.Max(0f, row.PreviewWeeklyInstallment)));
            }

            return LocalizedText.Format(
                ModTextKey.BankingDetailComparisonNormal,
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Systems
{
    internal sealed class CompanyCreditStanding
    {
        public CompanyCreditStanding()
        {
            Label = string.Empty;
            Summary = string.Empty;
            RateEffectLabel = string.Empty;
        }

        public int Score { get; set; }

        public string Label { get; set; }

        public string Summary { get; set; }

        public string RateEffectLabel { get; set; }

        public float RateAdjustmentPercent { get; set; }
    }

    internal static class BankCreditStandingCalculator
    {
        private const int MinutesPerWeek = 7 * 24 * 60;
        private const int RecentOperatingWindowMinutes = 4 * MinutesPerWeek;
        private const int BaseScore = 50;
        private const float StableOperatingCashflowThreshold = 20000f;
        private const float StrongOperatingCashflowThreshold = 75000f;

        public static CompanyCreditStanding Calculate(IEnumerable<CompanyFinanceTransaction> transactions, CompanyLoanState activeLoan, int currentInGameMinute)
        {
            var entries = transactions == null
                ? Array.Empty<CompanyFinanceTransaction>()
                : transactions.Where(transaction => transaction != null).ToArray();
            var loanDisbursementCount = entries.Count(IsLoanDisbursement);
            var loanRepaymentCount = entries.Count(IsLoanRepayment);
            var recentOperatingNet = CalculateRecentOperatingNet(entries, currentInGameMinute);
            var score = BaseScore;
            var drivers = new List<string>();

            if (loanDisbursementCount == 0 && activeLoan == null)
            {
                drivers.Add("No borrowing history yet");
            }

            if (loanRepaymentCount > 0)
            {
                score += Math.Min(32, loanRepaymentCount * 4);
                drivers.Add(string.Format(
                    "{0} repayment week{1} recorded",
                    loanRepaymentCount,
                    loanRepaymentCount == 1 ? string.Empty : "s"));
            }

            if (loanDisbursementCount > 0 && activeLoan == null && loanRepaymentCount > 0)
            {
                score += 10;
                drivers.Add("Previous loan cycle cleared");
            }

            if (activeLoan != null)
            {
                score += ResolveActiveLoanProgressContribution(activeLoan, drivers);
                score += ResolveActiveLoanLeverageContribution(activeLoan);
            }

            if (recentOperatingNet >= StrongOperatingCashflowThreshold)
            {
                score += 8;
                drivers.Add("Operating cash flow strong");
            }
            else if (recentOperatingNet >= StableOperatingCashflowThreshold)
            {
                score += 4;
                drivers.Add("Operating cash flow stable");
            }
            else if (recentOperatingNet <= -StrongOperatingCashflowThreshold)
            {
                score -= 12;
                drivers.Add("Operating cash flow soft");
            }
            else if (recentOperatingNet <= -StableOperatingCashflowThreshold)
            {
                score -= 5;
                drivers.Add("Operating cash flow cautious");
            }

            score = Math.Max(0, Math.Min(100, score));
            var hasBorrowingHistory = loanDisbursementCount > 0 || activeLoan != null;
            var rateAdjustmentPercent = hasBorrowingHistory
                ? ResolveRateAdjustmentPercent(score)
                : 0f;
            var standing = new CompanyCreditStanding
            {
                Score = score,
                Label = ResolveStandingLabel(score, hasBorrowingHistory),
                Summary = BuildSummary(drivers),
                RateAdjustmentPercent = rateAdjustmentPercent,
            };
            standing.RateEffectLabel = ResolveRateEffectLabel(standing.RateAdjustmentPercent);
            return standing;
        }

        private static int ResolveActiveLoanProgressContribution(CompanyLoanState activeLoan, ICollection<string> drivers)
        {
            if (activeLoan == null)
            {
                return 0;
            }

            var progressRatio = activeLoan.TermWeeks <= 0
                ? 0f
                : ModMath.Clamp01((float)activeLoan.WeeksPaid / activeLoan.TermWeeks);
            if (progressRatio < 0.05f)
            {
                drivers.Add("Fresh leverage on the active loan");
                return 0;
            }

            drivers.Add(string.Format("Active loan {0:0}% repaid", progressRatio * 100f));
            return (int)Math.Round(progressRatio * 12f, MidpointRounding.AwayFromZero);
        }

        private static int ResolveActiveLoanLeverageContribution(CompanyLoanState activeLoan)
        {
            if (activeLoan == null || activeLoan.OriginalPrincipal <= 0.01f)
            {
                return 0;
            }

            var remainingPrincipalRatio = Math.Max(0f, activeLoan.RemainingBalance) / Math.Max(0.01f, activeLoan.OriginalPrincipal);
            if (remainingPrincipalRatio >= 0.85f)
            {
                return -10;
            }

            if (remainingPrincipalRatio >= 0.45f)
            {
                return -5;
            }

            if (remainingPrincipalRatio <= 0.25f)
            {
                return 4;
            }

            return 0;
        }

        private static float CalculateRecentOperatingNet(IEnumerable<CompanyFinanceTransaction> transactions, int currentInGameMinute)
        {
            if (transactions == null)
            {
                return 0f;
            }

            var startMinute = Math.Max(0, currentInGameMinute - RecentOperatingWindowMinutes);
            var endMinute = Math.Max(0, currentInGameMinute);
            var total = 0f;
            foreach (var transaction in transactions)
            {
                if (transaction == null
                    || transaction.InGameMinute < startMinute
                    || transaction.InGameMinute > endMinute
                    || IsLoanDisbursement(transaction)
                    || IsLoanRepayment(transaction))
                {
                    continue;
                }

                total += transaction.Flow == CompanyFinanceFlow.Income
                    ? transaction.Amount
                    : -transaction.Amount;
            }

            return total;
        }

        private static bool IsLoanDisbursement(CompanyFinanceTransaction transaction)
        {
            return transaction != null
                && transaction.Flow == CompanyFinanceFlow.Income
                && transaction.Category == CompanyFinanceCategory.LoanDisbursement;
        }

        private static bool IsLoanRepayment(CompanyFinanceTransaction transaction)
        {
            return transaction != null
                && transaction.Flow == CompanyFinanceFlow.Expense
                && transaction.Category == CompanyFinanceCategory.LoanRepayment;
        }

        private static string ResolveStandingLabel(int score, bool hasBorrowingHistory)
        {
            if (!hasBorrowingHistory)
            {
                return "New Borrower";
            }

            if (score >= 90)
            {
                return "Prime";
            }

            if (score >= 75)
            {
                return "Preferred";
            }

            if (score >= 55)
            {
                return "Stable";
            }

            if (score >= 40)
            {
                return "Building";
            }

            return "Cautious";
        }

        private static float ResolveRateAdjustmentPercent(int score)
        {
            if (score >= 90)
            {
                return -0.35f;
            }

            if (score >= 75)
            {
                return -0.2f;
            }

            if (score >= 55)
            {
                return 0f;
            }

            if (score >= 40)
            {
                return 0.15f;
            }

            return 0.35f;
        }

        private static string ResolveRateEffectLabel(float rateAdjustmentPercent)
        {
            if (rateAdjustmentPercent <= -0.3f)
            {
                return "Preferred | new weekly offers lock with a small standing discount.";
            }

            if (rateAdjustmentPercent < 0f)
            {
                return "Preferred | new weekly offers lock with a slight standing discount.";
            }

            if (rateAdjustmentPercent >= 0.3f)
            {
                return "Cautious | new weekly offers lock with a modest standing premium.";
            }

            if (rateAdjustmentPercent > 0f)
            {
                return "Slightly cautious | new weekly offers lock with a small standing premium.";
            }

            return "Neutral | new weekly offers lock without a standing adjustment.";
        }

        private static string BuildSummary(IReadOnlyList<string> drivers)
        {
            if (drivers == null || drivers.Count == 0)
            {
                return "Standing unchanged.";
            }

            var selectedDrivers = drivers
                .Where(driver => !string.IsNullOrWhiteSpace(driver))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            return selectedDrivers.Length == 0
                ? "Standing unchanged."
                : string.Join(" | ", selectedDrivers);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;
using LSOL.UI;

namespace LSOL.Systems
{
    public sealed class CompanyLoanState
    {
        public string BankId { get; set; }

        public string BankName { get; set; }

        public float OriginalPrincipal { get; set; }

        public float LockedInterestRatePercent { get; set; }

        public float TotalRepayment { get; set; }

        public float RemainingBalance { get; set; }

        public float WeeklyInstallment { get; set; }

        public int TermWeeks { get; set; }

        public int WeeksPaid { get; set; }

        public int LastProcessedWeekIndex { get; set; } = -1;

        public int WeeksRemaining
        {
            get { return Math.Max(0, TermWeeks - WeeksPaid); }
        }

        public int NextDueWeekIndex
        {
            get { return Math.Max(0, LastProcessedWeekIndex + 1); }
        }
    }

    public sealed class CompanyLoanPreview
    {
        public float Principal { get; set; }

        public float RatePercent { get; set; }

        public float TotalRepayment { get; set; }

        public float WeeklyInstallment { get; set; }

        public int TermWeeks { get; set; }
    }

    public sealed class BankOfferRateSnapshot
    {
        public string BankId { get; set; }

        public int WeekIndex { get; set; }

        public float RatePercent { get; set; }
    }

    public sealed class BankLoanPersistenceSnapshot
    {
        public BankLoanPersistenceSnapshot()
        {
            OfferedRates = new List<BankOfferRateSnapshot>();
            OfferHistory = new List<BankOfferRateSnapshot>();
        }

        public CompanyLoanState ActiveLoan { get; set; }

        public List<BankOfferRateSnapshot> OfferedRates { get; }

        public List<BankOfferRateSnapshot> OfferHistory { get; }

        public bool HasData
        {
            get { return ActiveLoan != null || OfferedRates.Count > 0 || OfferHistory.Count > 0; }
        }
    }

    public sealed class BankLoanManager
    {
        private const int MinutesPerWeek = 7 * 24 * 60;
        private const int MaxOfferHistoryEntriesPerBank = 10;
        private static readonly int[] SupportedLoanTerms = { 4, 8, 12, 24 };

        private readonly List<BankDefinition> _banks;
        private readonly Dictionary<string, BankDefinition> _banksById;
        private readonly Dictionary<string, BankOfferRateSnapshot> _offeredRatesByBankId;
        private readonly Dictionary<string, List<BankOfferRateSnapshot>> _offerHistoryByBankId;
        private readonly CompanyFinanceTracker _financeTracker;
        private CompanyLoanState _activeLoan;

        private static string Text(string key)
        {
            return ModLocalization.Service.Get(key);
        }

        private static string Text(string key, params object[] args)
        {
            return ModLocalization.Service.Format(key, args);
        }

        public BankLoanManager(IEnumerable<BankDefinition> banks, CompanyFinanceTracker financeTracker)
        {
            _banks = banks == null
                ? new List<BankDefinition>()
                : banks
                    .Where(bank => bank != null && !string.IsNullOrWhiteSpace(bank.BankId))
                    .OrderBy(bank => bank.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            _banksById = _banks.ToDictionary(bank => bank.BankId, bank => bank, StringComparer.OrdinalIgnoreCase);
            _offeredRatesByBankId = new Dictionary<string, BankOfferRateSnapshot>(StringComparer.OrdinalIgnoreCase);
            _offerHistoryByBankId = new Dictionary<string, List<BankOfferRateSnapshot>>(StringComparer.OrdinalIgnoreCase);
            _financeTracker = financeTracker;
        }

        public IReadOnlyList<BankDefinition> Banks
        {
            get { return _banks; }
        }

        public CompanyLoanState ActiveLoan
        {
            get { return _activeLoan; }
        }

        public bool HasActiveLoan
        {
            get { return _activeLoan != null; }
        }

        public static IReadOnlyList<int> LoanTerms
        {
            get { return SupportedLoanTerms; }
        }

        public IReadOnlyList<BankOfferRateSnapshot> GetOfferHistory(string bankId)
        {
            if (string.IsNullOrWhiteSpace(bankId))
            {
                return Array.Empty<BankOfferRateSnapshot>();
            }

            List<BankOfferRateSnapshot> history;
            if (!_offerHistoryByBankId.TryGetValue(bankId.Trim(), out history) || history == null || history.Count == 0)
            {
                return Array.Empty<BankOfferRateSnapshot>();
            }

            return history
                .OrderByDescending(entry => entry.WeekIndex)
                .Select(CloneOffer)
                .ToArray();
        }

        internal CompanyCreditStanding GetCreditStanding(int currentInGameMinute)
        {
            return BankCreditStandingCalculator.Calculate(
                _financeTracker != null ? _financeTracker.Transactions : Array.Empty<CompanyFinanceTransaction>(),
                _activeLoan,
                currentInGameMinute);
        }

        public BankDefinition GetBank(string bankId)
        {
            if (string.IsNullOrWhiteSpace(bankId))
            {
                return null;
            }

            BankDefinition definition;
            return _banksById.TryGetValue(bankId.Trim(), out definition) ? definition : null;
        }

        public float GetCurrentOfferRate(BankDefinition bank, int currentInGameMinute)
        {
            if (bank == null)
            {
                return 0f;
            }

            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            BankOfferRateSnapshot offer;
            if (!_offeredRatesByBankId.TryGetValue(bank.BankId, out offer) || offer == null || offer.WeekIndex != currentWeekIndex)
            {
                var standing = GetCreditStanding(currentInGameMinute);
                offer = new BankOfferRateSnapshot
                {
                    BankId = bank.BankId,
                    WeekIndex = currentWeekIndex,
                    RatePercent = GenerateOfferRate(bank, currentWeekIndex, standing != null ? standing.RateAdjustmentPercent : 0f),
                };
                _offeredRatesByBankId[bank.BankId] = offer;
            }

            RecordOfferHistory(offer);

            return offer.RatePercent;
        }

        public CompanyLoanPreview CreatePreview(BankDefinition bank, float requestedPrincipal, int termWeeks, int currentInGameMinute)
        {
            if (bank == null)
            {
                return null;
            }

            if (!SupportedLoanTerms.Contains(termWeeks))
            {
                termWeeks = SupportedLoanTerms[0];
            }

            var principal = Math.Max(0f, Math.Min(bank.LoanAmountMaxLimit, requestedPrincipal));
            var ratePercent = GetCurrentOfferRate(bank, currentInGameMinute);
            var totalRepayment = principal * (1f + (Math.Max(0f, ratePercent) / 100f));
            return new CompanyLoanPreview
            {
                Principal = principal,
                RatePercent = ratePercent,
                TotalRepayment = totalRepayment,
                WeeklyInstallment = termWeeks > 0 ? totalRepayment / termWeeks : 0f,
                TermWeeks = termWeeks,
            };
        }

        public bool TryTakeLoan(BankDefinition bank, float requestedPrincipal, int termWeeks, int currentInGameMinute, ref float balance, out string message)
        {
            message = string.Empty;
            if (bank == null)
            {
                message = Text(ModTextKey.BankingStatusNoBankSelected);
                return false;
            }

            if (_activeLoan != null)
            {
                message = Text(ModTextKey.BankingStatusFinishActiveLoanFirst, _activeLoan.BankName);
                return false;
            }

            if (!SupportedLoanTerms.Contains(termWeeks))
            {
                message = Text(ModTextKey.BankingStatusSelectedTermInvalid);
                return false;
            }

            var preview = CreatePreview(bank, requestedPrincipal, termWeeks, currentInGameMinute);
            if (preview == null || preview.Principal <= 0.01f)
            {
                message = Text(ModTextKey.BankingStatusLoanAmountPositive);
                return false;
            }

            if (preview.Principal - bank.LoanAmountMaxLimit > 0.01f)
            {
                message = Text(ModTextKey.BankingStatusBankMaxOffer, bank.DisplayName, ModFormatting.FormatMoney(bank.LoanAmountMaxLimit));
                return false;
            }

            _activeLoan = new CompanyLoanState
            {
                BankId = bank.BankId,
                BankName = bank.DisplayName,
                OriginalPrincipal = preview.Principal,
                LockedInterestRatePercent = preview.RatePercent,
                TotalRepayment = preview.TotalRepayment,
                RemainingBalance = preview.TotalRepayment,
                WeeklyInstallment = preview.WeeklyInstallment,
                TermWeeks = preview.TermWeeks,
                WeeksPaid = 0,
                LastProcessedWeekIndex = GetWeekIndex(currentInGameMinute),
            };

            balance += preview.Principal;
            RecordIncome(
                CompanyFinanceCategory.LoanDisbursement,
                preview.Principal,
                currentInGameMinute,
                Text(ModTextKey.BankingFinanceIncomeLoanFromRate, bank.DisplayName, ModFormatting.FormatPercent(preview.RatePercent)));

            message = Text(
                ModTextKey.BankingStatusSecuredLoan,
                ModFormatting.FormatMoney(preview.Principal),
                bank.DisplayName,
                ModFormatting.FormatMoney(preview.WeeklyInstallment),
                preview.TermWeeks);
            return true;
        }

        public List<string> ProcessWeeklyRepayments(int currentInGameMinute, ref float balance)
        {
            var messages = new List<string>();
            if (_activeLoan == null)
            {
                return messages;
            }

            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            if (_activeLoan.LastProcessedWeekIndex < 0)
            {
                _activeLoan.LastProcessedWeekIndex = currentWeekIndex;
                return messages;
            }

            for (int weekIndex = _activeLoan.LastProcessedWeekIndex + 1; weekIndex <= currentWeekIndex && _activeLoan != null; weekIndex++)
            {
                var dueAmount = ResolveWeeklyInstallmentDue(_activeLoan);
                if (dueAmount <= 0.01f)
                {
                    _activeLoan = null;
                    break;
                }

                balance -= dueAmount;
                RecordExpense(
                    CompanyFinanceCategory.LoanRepayment,
                    dueAmount,
                    Math.Max(0, weekIndex * MinutesPerWeek),
                    Text(ModTextKey.BankingFinanceExpenseWeeklyRepaymentTo, _activeLoan.BankName));

                _activeLoan.RemainingBalance = Math.Max(0f, _activeLoan.RemainingBalance - dueAmount);
                _activeLoan.WeeksPaid = Math.Min(_activeLoan.TermWeeks, _activeLoan.WeeksPaid + 1);
                _activeLoan.LastProcessedWeekIndex = weekIndex;

                if (_activeLoan.RemainingBalance <= 0.01f || _activeLoan.WeeksPaid >= _activeLoan.TermWeeks)
                {
                    var completedBankName = _activeLoan.BankName;
                    messages.Add(Text(
                        ModTextKey.BankingStatusLoanRepaid,
                        completedBankName,
                        ModFormatting.FormatMoney(dueAmount)));
                    _activeLoan = null;
                    continue;
                }

                messages.Add(Text(
                    ModTextKey.BankingStatusLoanRepaymentRemaining,
                    _activeLoan.BankName,
                    ModFormatting.FormatMoney(dueAmount),
                    ModFormatting.FormatMoney(_activeLoan.RemainingBalance),
                    _activeLoan.WeeksRemaining));
            }

            return messages;
        }

        public BankLoanPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new BankLoanPersistenceSnapshot();
            if (_activeLoan != null)
            {
                snapshot.ActiveLoan = CloneLoan(_activeLoan);
            }

            foreach (var offer in _offeredRatesByBankId.Values
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.BankId))
                .OrderBy(entry => entry.BankId, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.OfferedRates.Add(CloneOffer(offer));
            }

            foreach (var offer in _offerHistoryByBankId.Values
                .Where(history => history != null)
                .SelectMany(history => history)
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.BankId))
                .OrderBy(entry => entry.BankId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(entry => entry.WeekIndex))
            {
                snapshot.OfferHistory.Add(CloneOffer(offer));
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(BankLoanPersistenceSnapshot snapshot, int currentInGameMinute)
        {
            _offeredRatesByBankId.Clear();
            _offerHistoryByBankId.Clear();
            _activeLoan = null;

            if (snapshot != null && snapshot.OfferHistory != null)
            {
                foreach (var offer in snapshot.OfferHistory)
                {
                    RecordOfferHistory(offer);
                }
            }

            if (snapshot != null && snapshot.OfferedRates != null)
            {
                foreach (var offer in snapshot.OfferedRates)
                {
                    if (offer == null || string.IsNullOrWhiteSpace(offer.BankId) || offer.RatePercent < 0f)
                    {
                        continue;
                    }

                    var sanitizedOffer = CloneOffer(offer);
                    _offeredRatesByBankId[sanitizedOffer.BankId] = sanitizedOffer;
                    RecordOfferHistory(sanitizedOffer);
                }
            }

            if (snapshot == null || snapshot.ActiveLoan == null)
            {
                return;
            }

            _activeLoan = SanitizeActiveLoan(snapshot.ActiveLoan, currentInGameMinute);
        }

        public static int GetWeekIndex(int currentInGameMinute)
        {
            if (currentInGameMinute <= 0)
            {
                return 0;
            }

            return currentInGameMinute / MinutesPerWeek;
        }

        private static float ResolveWeeklyInstallmentDue(CompanyLoanState loan)
        {
            if (loan == null)
            {
                return 0f;
            }

            if (loan.WeeksPaid + 1 >= loan.TermWeeks)
            {
                return Math.Max(0f, loan.RemainingBalance);
            }

            return Math.Max(0f, Math.Min(loan.WeeklyInstallment, loan.RemainingBalance));
        }

        private static CompanyLoanState SanitizeActiveLoan(CompanyLoanState source, int currentInGameMinute)
        {
            if (source == null || source.OriginalPrincipal <= 0.01f || source.TermWeeks <= 0)
            {
                return null;
            }

            var totalRepayment = source.TotalRepayment > 0.01f
                ? source.TotalRepayment
                : source.OriginalPrincipal * (1f + (Math.Max(0f, source.LockedInterestRatePercent) / 100f));
            var weeklyInstallment = source.WeeklyInstallment > 0.01f
                ? source.WeeklyInstallment
                : totalRepayment / Math.Max(1, source.TermWeeks);
            var remainingBalance = Math.Max(0f, source.RemainingBalance);
            var weeksPaid = Math.Max(0, Math.Min(source.TermWeeks, source.WeeksPaid));
            if (remainingBalance <= 0.01f || weeksPaid >= source.TermWeeks)
            {
                return null;
            }

            return new CompanyLoanState
            {
                BankId = source.BankId ?? string.Empty,
                BankName = string.IsNullOrWhiteSpace(source.BankName) ? (source.BankId ?? "Bank") : source.BankName,
                OriginalPrincipal = Math.Max(0f, source.OriginalPrincipal),
                LockedInterestRatePercent = Math.Max(0f, source.LockedInterestRatePercent),
                TotalRepayment = Math.Max(0f, totalRepayment),
                RemainingBalance = remainingBalance,
                WeeklyInstallment = Math.Max(0f, weeklyInstallment),
                TermWeeks = Math.Max(1, source.TermWeeks),
                WeeksPaid = weeksPaid,
                LastProcessedWeekIndex = source.LastProcessedWeekIndex < -1
                    ? GetWeekIndex(currentInGameMinute)
                    : source.LastProcessedWeekIndex,
            };
        }

        private static CompanyLoanState CloneLoan(CompanyLoanState source)
        {
            if (source == null)
            {
                return null;
            }

            return new CompanyLoanState
            {
                BankId = source.BankId,
                BankName = source.BankName,
                OriginalPrincipal = source.OriginalPrincipal,
                LockedInterestRatePercent = source.LockedInterestRatePercent,
                TotalRepayment = source.TotalRepayment,
                RemainingBalance = source.RemainingBalance,
                WeeklyInstallment = source.WeeklyInstallment,
                TermWeeks = source.TermWeeks,
                WeeksPaid = source.WeeksPaid,
                LastProcessedWeekIndex = source.LastProcessedWeekIndex,
            };
        }

        private static float GenerateOfferRate(BankDefinition bank, int weekIndex, float standingRateAdjustmentPercent)
        {
            if (bank == null)
            {
                return 0f;
            }

            var minimum = Math.Max(0f, Math.Min(bank.LoanInterestMin, bank.LoanInterestMax));
            var maximum = Math.Max(minimum, Math.Max(bank.LoanInterestMin, bank.LoanInterestMax));
            if (maximum - minimum <= 0.001f)
            {
                return minimum;
            }

            unchecked
            {
                var seed = 17;
                var normalizedBankId = (bank.BankId ?? string.Empty).Trim();
                for (int i = 0; i < normalizedBankId.Length; i++)
                {
                    seed = (seed * 31) + normalizedBankId[i];
                }

                seed = (seed * 397) ^ weekIndex;
                var random = new Random(seed);
                var value = random.NextDouble();
                var baseRate = minimum + ((maximum - minimum) * value);
                var adjustedRate = baseRate + standingRateAdjustmentPercent;
                return (float)Math.Round(Math.Max(minimum, Math.Min(maximum, adjustedRate)), 2);
            }
        }

        private void RecordOfferHistory(BankOfferRateSnapshot offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.BankId) || offer.RatePercent < 0f)
            {
                return;
            }

            List<BankOfferRateSnapshot> history;
            if (!_offerHistoryByBankId.TryGetValue(offer.BankId, out history) || history == null)
            {
                history = new List<BankOfferRateSnapshot>();
                _offerHistoryByBankId[offer.BankId] = history;
            }

            var sanitizedOffer = CloneOffer(offer);
            var existingIndex = history.FindIndex(entry => entry != null && entry.WeekIndex == sanitizedOffer.WeekIndex);
            if (existingIndex >= 0)
            {
                history[existingIndex] = sanitizedOffer;
            }
            else
            {
                history.Add(sanitizedOffer);
            }

            history.Sort((left, right) => right.WeekIndex.CompareTo(left.WeekIndex));
            if (history.Count > MaxOfferHistoryEntriesPerBank)
            {
                history.RemoveRange(MaxOfferHistoryEntriesPerBank, history.Count - MaxOfferHistoryEntriesPerBank);
            }
        }

        private static BankOfferRateSnapshot CloneOffer(BankOfferRateSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            return new BankOfferRateSnapshot
            {
                BankId = source.BankId,
                WeekIndex = source.WeekIndex,
                RatePercent = Math.Max(0f, source.RatePercent),
            };
        }

        private void RecordIncome(CompanyFinanceCategory category, float amount, int currentInGameMinute, string description)
        {
            if (_financeTracker == null || amount <= 0.01f)
            {
                return;
            }

            _financeTracker.RecordIncome(category, amount, currentInGameMinute, description);
        }

        private void RecordExpense(CompanyFinanceCategory category, float amount, int currentInGameMinute, string description)
        {
            if (_financeTracker == null || amount <= 0.01f)
            {
                return;
            }

            _financeTracker.RecordExpense(category, amount, currentInGameMinute, description);
        }
    }
}
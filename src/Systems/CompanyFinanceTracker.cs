using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Systems
{
    public enum CompanyFinanceFlow
    {
        Income = 0,
        Expense = 1,
    }

    public enum CompanyFinanceCategory
    {
        PlayerDelivery = 0,
        NpcDelivery = 1,
        IndustryIncome = 2,
        MissionReward = 3,
        OtherIncome = 4,
        OfficeRent = 5,
        ApartmentRent = 6,
        VehicleRent = 7,
        NpcWages = 8,
        FuelPurchase = 9,
        RepairCost = 10,
        ServiceCall = 11,
        PermitOrLicence = 12,
        OtherExpense = 13,
        LoanDisbursement = 14,
        LoanRepayment = 15,
    }

    public sealed class CompanyFinanceTransaction
    {
        public int Sequence { get; internal set; }

        public int InGameMinute { get; internal set; }

        public CompanyFinanceFlow Flow { get; internal set; }

        public CompanyFinanceCategory Category { get; internal set; }

        public float Amount { get; internal set; }

        public string Description { get; internal set; }

        public int RouteContractId { get; internal set; }

        public string RouteLabel { get; internal set; }
    }

    public sealed class CompanyFinanceTransactionSnapshot
    {
        public int Sequence { get; set; }

        public int InGameMinute { get; set; }

        public CompanyFinanceFlow Flow { get; set; }

        public CompanyFinanceCategory Category { get; set; }

        public float Amount { get; set; }

        public string Description { get; set; }

        public int RouteContractId { get; set; }

        public string RouteLabel { get; set; }
    }

    public sealed class CompanyFinancePersistenceSnapshot
    {
        public CompanyFinancePersistenceSnapshot()
        {
            Transactions = new List<CompanyFinanceTransactionSnapshot>();
        }

        public int NextSequence { get; set; } = 1;

        public List<CompanyFinanceTransactionSnapshot> Transactions { get; }

        public bool HasData
        {
            get { return Transactions.Count > 0; }
        }
    }

    public sealed class CompanyFinanceCategoryTotal
    {
        public CompanyFinanceCategory Category { get; set; }

        public float Amount { get; set; }
    }

    public sealed class CompanyFinanceTracker
    {
        private const int MaxTransactions = 512;

        private readonly List<CompanyFinanceTransaction> _transactions;
        private int _nextSequence;

        public CompanyFinanceTracker()
        {
            _transactions = new List<CompanyFinanceTransaction>(MaxTransactions);
            _nextSequence = 1;
        }

        public IReadOnlyList<CompanyFinanceTransaction> Transactions
        {
            get { return _transactions; }
        }

        public bool HasData
        {
            get { return _transactions.Count > 0; }
        }

        public void Clear()
        {
            _transactions.Clear();
            _nextSequence = 1;
        }

        public void RecordIncome(CompanyFinanceCategory category, float amount, int inGameMinute, string description = null, int routeContractId = 0, string routeLabel = null)
        {
            Record(CompanyFinanceFlow.Income, category, amount, inGameMinute, description, routeContractId, routeLabel);
        }

        public void RecordExpense(CompanyFinanceCategory category, float amount, int inGameMinute, string description = null, int routeContractId = 0, string routeLabel = null)
        {
            Record(CompanyFinanceFlow.Expense, category, amount, inGameMinute, description, routeContractId, routeLabel);
        }

        public float GetNetAmount(int currentInGameMinute, int lookbackMinutes)
        {
            var startMinute = GetWindowStart(currentInGameMinute, lookbackMinutes);
            var total = 0f;
            for (int i = 0; i < _transactions.Count; i++)
            {
                var transaction = _transactions[i];
                if (!IsWithinWindow(transaction, startMinute, currentInGameMinute))
                {
                    continue;
                }

                total += transaction.Flow == CompanyFinanceFlow.Income
                    ? transaction.Amount
                    : -transaction.Amount;
            }

            return total;
        }

        public float GetTotalAmount(int currentInGameMinute, int lookbackMinutes, CompanyFinanceFlow flow)
        {
            return GetTransactionsInWindow(currentInGameMinute, lookbackMinutes, flow, null)
                .Sum(transaction => transaction.Amount);
        }

        public IReadOnlyList<CompanyFinanceCategoryTotal> GetCategoryTotals(int currentInGameMinute, int lookbackMinutes, CompanyFinanceFlow flow)
        {
            return GetTransactionsInWindow(currentInGameMinute, lookbackMinutes, flow, null)
                .GroupBy(transaction => transaction.Category)
                .Select(group => new CompanyFinanceCategoryTotal
                {
                    Category = group.Key,
                    Amount = group.Sum(transaction => transaction.Amount),
                })
                .OrderByDescending(total => total.Amount)
                .ThenBy(total => total.Category)
                .ToArray();
        }

        public IReadOnlyList<CompanyFinanceTransaction> GetTransactionsInWindow(int currentInGameMinute, int lookbackMinutes, CompanyFinanceFlow? flow = null, CompanyFinanceCategory? category = null)
        {
            var startMinute = GetWindowStart(currentInGameMinute, lookbackMinutes);
            return _transactions
                .Where(transaction => IsWithinWindow(transaction, startMinute, currentInGameMinute))
                .Where(transaction => !flow.HasValue || transaction.Flow == flow.Value)
                .Where(transaction => !category.HasValue || transaction.Category == category.Value)
                .OrderByDescending(transaction => transaction.InGameMinute)
                .ThenByDescending(transaction => transaction.Sequence)
                .ToArray();
        }

        public CompanyFinancePersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new CompanyFinancePersistenceSnapshot
            {
                NextSequence = Math.Max(1, _nextSequence),
            };

            for (int i = 0; i < _transactions.Count; i++)
            {
                var transaction = _transactions[i];
                if (transaction == null)
                {
                    continue;
                }

                snapshot.Transactions.Add(new CompanyFinanceTransactionSnapshot
                {
                    Sequence = transaction.Sequence,
                    InGameMinute = transaction.InGameMinute,
                    Flow = transaction.Flow,
                    Category = transaction.Category,
                    Amount = transaction.Amount,
                    Description = transaction.Description ?? string.Empty,
                    RouteContractId = transaction.RouteContractId,
                    RouteLabel = transaction.RouteLabel ?? string.Empty,
                });
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(CompanyFinancePersistenceSnapshot snapshot)
        {
            _transactions.Clear();
            _nextSequence = Math.Max(1, snapshot != null ? snapshot.NextSequence : 1);

            if (snapshot == null || snapshot.Transactions == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Transactions.Count; i++)
            {
                var entry = snapshot.Transactions[i];
                if (entry == null || entry.Amount <= 0f)
                {
                    continue;
                }

                _transactions.Add(new CompanyFinanceTransaction
                {
                    Sequence = Math.Max(1, entry.Sequence),
                    InGameMinute = Math.Max(0, entry.InGameMinute),
                    Flow = entry.Flow,
                    Category = entry.Category,
                    Amount = Math.Max(0f, entry.Amount),
                    Description = SanitizeText(entry.Description),
                    RouteContractId = Math.Max(0, entry.RouteContractId),
                    RouteLabel = SanitizeText(entry.RouteLabel),
                });
            }

            _transactions.Sort((left, right) =>
            {
                var minuteCompare = left.InGameMinute.CompareTo(right.InGameMinute);
                if (minuteCompare != 0)
                {
                    return minuteCompare;
                }

                return left.Sequence.CompareTo(right.Sequence);
            });

            if (_transactions.Count > MaxTransactions)
            {
                _transactions.RemoveRange(0, _transactions.Count - MaxTransactions);
            }

            if (_transactions.Count > 0)
            {
                _nextSequence = Math.Max(_nextSequence, _transactions[_transactions.Count - 1].Sequence + 1);
            }
        }

        private void Record(CompanyFinanceFlow flow, CompanyFinanceCategory category, float amount, int inGameMinute, string description, int routeContractId, string routeLabel)
        {
            if (amount <= 0f)
            {
                return;
            }

            var transaction = new CompanyFinanceTransaction
            {
                Sequence = _nextSequence++,
                InGameMinute = Math.Max(0, inGameMinute),
                Flow = flow,
                Category = category,
                Amount = Math.Max(0f, amount),
                Description = SanitizeText(description),
                RouteContractId = Math.Max(0, routeContractId),
                RouteLabel = SanitizeText(routeLabel),
            };

            if (_transactions.Count >= MaxTransactions)
            {
                _transactions.RemoveAt(0);
            }

            _transactions.Add(transaction);
        }

        private static int GetWindowStart(int currentInGameMinute, int lookbackMinutes)
        {
            return Math.Max(0, currentInGameMinute - Math.Max(0, lookbackMinutes));
        }

        private static bool IsWithinWindow(CompanyFinanceTransaction transaction, int startMinute, int currentInGameMinute)
        {
            return transaction != null
                && transaction.InGameMinute >= startMinute
                && transaction.InGameMinute <= Math.Max(0, currentInGameMinute);
        }

        private static string SanitizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
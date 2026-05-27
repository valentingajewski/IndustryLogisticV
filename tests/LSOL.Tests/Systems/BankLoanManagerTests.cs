using System.Linq;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class BankLoanManagerTests
    {
        [TestMethod]
        public void GetCurrentOfferRate_WhenStandingChangesMidWeek_KeepsLockedWeeklyOfferStable()
        {
            var tracker = new CompanyFinanceTracker();
            var manager = new BankLoanManager(new[] { CreateBank("alpha", 6f, 9f) }, tracker);
            var bank = manager.Banks[0];

            var firstRate = manager.GetCurrentOfferRate(bank, 1000);

            tracker.RecordIncome(CompanyFinanceCategory.LoanDisbursement, 120000f, 1100, "Loan draw");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10000f, 1200, "Loan repayment");
            tracker.RecordIncome(CompanyFinanceCategory.PlayerDelivery, 90000f, 1300, "Strong cash week");

            var secondRate = manager.GetCurrentOfferRate(bank, 4000);

            Assert.AreEqual(firstRate, secondRate, 0.0001f);
        }

        [TestMethod]
        public void GetCurrentOfferRate_WhenStandingImprovesForNewWeek_AppliesSmallDiscountWithinBounds()
        {
            const int comparisonMinute = 50000;
            var baselineManager = new BankLoanManager(new[] { CreateBank("alpha", 8f, 12f) }, new CompanyFinanceTracker());
            var baselineRate = baselineManager.GetCurrentOfferRate(baselineManager.Banks[0], comparisonMinute);

            var tracker = new CompanyFinanceTracker();
            tracker.RecordIncome(CompanyFinanceCategory.LoanDisbursement, 120000f, 0, "Loan draw");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10000f, 10080, "Loan repayment");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10000f, 20160, "Loan repayment");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10000f, 30240, "Loan repayment");
            tracker.RecordIncome(CompanyFinanceCategory.PlayerDelivery, 120000f, 35000, "Strong cash week");

            var preferredManager = new BankLoanManager(new[] { CreateBank("alpha", 8f, 12f) }, tracker);
            var preferredRate = preferredManager.GetCurrentOfferRate(preferredManager.Banks[0], comparisonMinute);

            Assert.IsTrue(preferredRate < baselineRate);
            Assert.IsTrue(preferredRate >= 8f && preferredRate <= 12f);
        }

        [TestMethod]
        public void GetOfferHistory_AfterManyWeeks_RetainsRollingRecentOffersOnly()
        {
            var manager = new BankLoanManager(new[] { CreateBank("alpha", 6f, 9f) }, new CompanyFinanceTracker());
            var bank = manager.Banks[0];

            for (int week = 0; week < 14; week++)
            {
                manager.GetCurrentOfferRate(bank, week * 10080);
            }

            var history = manager.GetOfferHistory(bank.BankId);

            Assert.AreEqual(10, history.Count);
            Assert.AreEqual(13, history[0].WeekIndex);
            Assert.AreEqual(4, history[history.Count - 1].WeekIndex);
        }

        private static BankDefinition CreateBank(string bankId, float minRate, float maxRate)
        {
            return new BankDefinition
            {
                BankId = bankId,
                Name = "Bank",
                LoanAmountMaxLimit = 250000f,
                LoanInterestMin = minRate,
                LoanInterestMax = maxRate,
            };
        }
    }
}
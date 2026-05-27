using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class BankCreditStandingCalculatorTests
    {
        [TestMethod]
        public void Calculate_WhenThereIsNoBorrowingHistory_ReturnsNeutralNewBorrowerStanding()
        {
            var standing = BankCreditStandingCalculator.Calculate(null, null, 0);

            Assert.AreEqual(50, standing.Score);
            Assert.AreEqual("New Borrower", standing.Label);
            Assert.AreEqual("No borrowing history yet", standing.Summary);
            Assert.AreEqual(0f, standing.RateAdjustmentPercent, 0.001f);
        }

        [TestMethod]
        public void Calculate_WhenRepaymentHistoryIsPositive_PromotesStanding()
        {
            var tracker = new CompanyFinanceTracker();
            tracker.RecordIncome(CompanyFinanceCategory.LoanDisbursement, 120000f, 0, "Loan draw");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10500f, 10080, "Loan repayment");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10500f, 20160, "Loan repayment");
            tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 10500f, 30240, "Loan repayment");
            tracker.RecordIncome(CompanyFinanceCategory.PlayerDelivery, 120000f, 36000, "Freight run");

            var standing = BankCreditStandingCalculator.Calculate(tracker.Transactions, null, 40320);

            Assert.AreEqual(80, standing.Score);
            Assert.AreEqual("Preferred", standing.Label);
            Assert.AreEqual(-0.2f, standing.RateAdjustmentPercent, 0.001f);
            StringAssert.Contains(standing.Summary, "3 repayment weeks recorded");
        }

        [TestMethod]
        public void Calculate_WhenSignalsAreExtremelyPositive_ClampsToMaximumScore()
        {
            var tracker = new CompanyFinanceTracker();
            tracker.RecordIncome(CompanyFinanceCategory.LoanDisbursement, 90000f, 0, "Loan draw");
            for (int i = 0; i < 12; i++)
            {
                tracker.RecordExpense(CompanyFinanceCategory.LoanRepayment, 9000f, (i + 1) * 10080, "Loan repayment");
            }

            tracker.RecordIncome(CompanyFinanceCategory.PlayerDelivery, 150000f, 130000, "Player delivery");

            var standing = BankCreditStandingCalculator.Calculate(tracker.Transactions, null, 140000);

            Assert.AreEqual(100, standing.Score);
            Assert.AreEqual("Prime", standing.Label);
            Assert.AreEqual(-0.35f, standing.RateAdjustmentPercent, 0.001f);
        }
    }
}
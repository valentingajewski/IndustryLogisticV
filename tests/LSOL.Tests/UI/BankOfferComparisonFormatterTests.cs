using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class BankOfferComparisonFormatterTests
    {
        [TestMethod]
        public void BuildDetail_WhenOfferIsAvailable_ShowsRateInstallmentTotalAndMax()
        {
            var row = new BankOfferComparisonRow
            {
                DisplayName = "Union Credit",
                BankId = "uc-1",
                RequestedPrincipal = 100000f,
                MaxAvailablePrincipal = 150000f,
                OfferedRatePercent = 8.5f,
                PreviewPrincipal = 100000f,
                PreviewTotalRepayment = 112500f,
                PreviewWeeklyInstallment = 28125f,
            };

            Assert.AreEqual(
                "Rate 8.50% | Installment $28,125.00/w | Total $112,500.00 | Max $150,000.00",
                BankOfferComparisonFormatter.BuildDetail(row));
        }

        [TestMethod]
        public void BuildDetail_WhenRequestedAmountExceedsMax_ShowsExplicitCappedMessage()
        {
            var row = new BankOfferComparisonRow
            {
                DisplayName = "Bank",
                BankId = "6",
                RequestedPrincipal = 800000f,
                MaxAvailablePrincipal = 500000f,
                OfferedRatePercent = 6f,
                PreviewPrincipal = 500000f,
                PreviewTotalRepayment = 530000f,
                PreviewWeeklyInstallment = 22083.333f,
            };

            Assert.AreEqual(
                "Requested $800,000.00 exceeds max $500,000.00 | Rate 6.00% | Capped total $530,000.00 | Capped installment $22,083.33/w",
                BankOfferComparisonFormatter.BuildDetail(row));
        }

        [TestMethod]
        public void BuildCaption_WhenGenericBankNamesCollide_DisambiguatesWithBankIdAndCurrentMarker()
        {
            var row = new BankOfferComparisonRow
            {
                DisplayName = "Bank",
                BankId = "6",
                NameIsDuplicated = true,
                IsCurrentBranch = true,
            };

            Assert.AreEqual("Bank #6 [HERE]", BankOfferComparisonFormatter.BuildCaption(row));
        }
    }
}
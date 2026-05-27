using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletServiceSiteStatusFormatterTests
    {
        [TestMethod]
        public void BuildWeeklyTargetDetail_WhenTargetIsIncomplete_ShowsRemainingTonsAndDeliveries()
        {
            var summary = new TabletLocationSummary
            {
                ServiceCurrentWeekDeliveries = 2,
                ServiceCurrentWeekTons = 12f,
                ServiceRequiredWeeklyTons = 30f,
            };

            Assert.AreEqual(
                "12.0/30.0t | 18.0t remaining | 2 deliveries",
                TabletServiceSiteStatusFormatter.BuildWeeklyTargetDetail(summary));
        }

        [TestMethod]
        public void BuildWeeklyTargetDetail_WhenTargetIsUnavailable_HidesTheRow()
        {
            var summary = new TabletLocationSummary
            {
                ServiceCurrentWeekDeliveries = 3,
                ServiceCurrentWeekTons = 18f,
                ServiceRequiredWeeklyTons = 0f,
            };

            Assert.AreEqual(string.Empty, TabletServiceSiteStatusFormatter.BuildWeeklyTargetDetail(summary));
        }

        [TestMethod]
        public void BuildContractStatusDetail_WhenRecoveringFromPenalty_ShowsRiskRecoveryAndLastWeekOutcome()
        {
            var summary = new TabletLocationSummary
            {
                IsOwnedByPlayer = true,
                ServiceContractStatus = "Contract watch",
                ServicePenaltySteps = 1,
                ServiceSuccessStreak = 1,
                ServiceTargetMetLastWeek = true,
                ServiceRequiredWeeklyTons = 30f,
            };

            Assert.AreEqual(
                "Contract watch | Penalty 1/2 | Recovery 1 | Last week met",
                TabletServiceSiteStatusFormatter.BuildContractStatusDetail(summary));
        }

        [TestMethod]
        public void BuildContractStatusDetail_WhenNoPenaltyYet_ShowsPendingLastWeekState()
        {
            var summary = new TabletLocationSummary
            {
                IsOwnedByPlayer = true,
                ServiceContractStatus = "Awaiting weekly service",
                ServiceRequiredWeeklyTons = 30f,
            };

            Assert.AreEqual(
                "Awaiting weekly service | Last week pending",
                TabletServiceSiteStatusFormatter.BuildContractStatusDetail(summary));
        }

        [TestMethod]
        public void BuildContractStatusDetail_WhenSiteIsUnowned_ShowsOpenMarketLockedUntilPurchase()
        {
            var summary = new TabletLocationSummary
            {
                IsOwnedByPlayer = false,
                HasServiceContractInfo = true,
                ServiceContractStatus = "Open market",
                ServiceRequiredWeeklyTons = 30f,
            };

            Assert.AreEqual(
                "Open market | Locked until purchase",
                TabletServiceSiteStatusFormatter.BuildContractStatusDetail(summary));
        }
    }
}
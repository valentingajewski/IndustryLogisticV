using System;
using System.Linq;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class PlayerSuccessTrackerDeliveryProgressTests
    {
        [TestMethod]
        public void RecordDeliveryProgress_PlayerSource_UpdatesPlayerDeliveryCounters()
        {
            var tracker = CreateTracker();

            tracker.RecordDeliveryProgress("Steel", 12.5f, true, true);

            var snapshot = tracker.CreatePersistenceSnapshot();
            Assert.AreEqual(1, snapshot.TotalSuccessfulDeliveries);
            Assert.AreEqual(1, snapshot.TotalSuccessfulCleanDeliveries);
            Assert.AreEqual(1, snapshot.DeliveriesBeforeFirstNpcHire);
            Assert.AreEqual(12.5f, snapshot.TotalTransportedTons, 0.001f);
            Assert.AreEqual(1, snapshot.CommodityTotals.Count);
            Assert.AreEqual("Steel", snapshot.CommodityTotals[0].CommodityId);
            Assert.AreEqual(12.5f, snapshot.CommodityTotals[0].Tons, 0.001f);
            Assert.IsTrue(snapshot.UnlockedSuccessIds.Any(id => string.Equals(id, "first_delivery", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void RecordDeliveryProgress_NpcSource_DoesNotUpdatePlayerDeliveryCounters()
        {
            var tracker = CreateTracker();

            tracker.RecordDeliveryProgress("Steel", 12.5f, true, true, DeliveryProgressSource.Npc);

            var snapshot = tracker.CreatePersistenceSnapshot();
            Assert.AreEqual(0, snapshot.TotalSuccessfulDeliveries);
            Assert.AreEqual(0, snapshot.TotalSuccessfulCleanDeliveries);
            Assert.AreEqual(0, snapshot.DeliveriesBeforeFirstNpcHire);
            Assert.AreEqual(0f, snapshot.TotalTransportedTons, 0.001f);
            Assert.AreEqual(0, snapshot.CommodityTotals.Count);
            Assert.IsFalse(snapshot.UnlockedSuccessIds.Any(id => string.Equals(id, "first_delivery", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void FinanceTracker_NpcDeliveryIncome_StillUnlocksHandsOffIncome()
        {
            var financeTracker = new CompanyFinanceTracker();
            var tracker = CreateTracker(financeTracker);

            financeTracker.RecordIncome(CompanyFinanceCategory.NpcDelivery, 1000000f, 0, "NPC delivery payout");

            var snapshot = tracker.CreatePersistenceSnapshot();
            Assert.AreEqual(1000000f, snapshot.CumulativeNpcDeliveryIncome, 0.001f);
            Assert.IsTrue(snapshot.UnlockedSuccessIds.Any(id => string.Equals(id, "hands_off_income", StringComparison.OrdinalIgnoreCase)));
        }

        private static PlayerSuccessTracker CreateTracker(CompanyFinanceTracker financeTracker = null)
        {
            return new PlayerSuccessTracker(
                null,
                null,
                null,
                null,
                null,
                null,
                financeTracker,
                null);
        }
    }
}
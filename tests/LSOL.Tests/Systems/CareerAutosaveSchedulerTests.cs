using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class CareerAutosaveSchedulerTests
    {
        [TestMethod]
        public void RequestSave_DebouncesUntilDelayExpires()
        {
            var scheduler = new CareerAutosaveScheduler(debounceDelayMs: 5000, retryDelayMs: 2000, periodicCheckpointIntervalMs: 60000);

            scheduler.Reset(1000);
            scheduler.RequestSave(1000);

            Assert.IsTrue(scheduler.HasPendingSave);
            Assert.IsFalse(scheduler.IsSaveDue(5999));
            Assert.IsTrue(scheduler.IsSaveDue(6000));
        }

        [TestMethod]
        public void RequestSave_DoesNotDelayExistingPendingSave()
        {
            var scheduler = new CareerAutosaveScheduler(debounceDelayMs: 5000, retryDelayMs: 2000, periodicCheckpointIntervalMs: 60000);

            scheduler.Reset(0);
            scheduler.RequestSave(1000);
            scheduler.RequestSave(4000);

            Assert.IsTrue(scheduler.IsSaveDue(6000));
        }

        [TestMethod]
        public void TryRequestPeriodicCheckpoint_RequestsImmediateSaveAfterInterval()
        {
            var scheduler = new CareerAutosaveScheduler(debounceDelayMs: 5000, retryDelayMs: 2000, periodicCheckpointIntervalMs: 60000);

            scheduler.Reset(0);

            Assert.IsFalse(scheduler.TryRequestPeriodicCheckpoint(59999));
            Assert.IsTrue(scheduler.TryRequestPeriodicCheckpoint(60000));
            Assert.IsTrue(scheduler.IsSaveDue(60000));
        }

        [TestMethod]
        public void MarkSaveFailed_SchedulesRetryDelay()
        {
            var scheduler = new CareerAutosaveScheduler(debounceDelayMs: 5000, retryDelayMs: 2000, periodicCheckpointIntervalMs: 60000);

            scheduler.Reset(0);
            scheduler.RequestSave(0);
            scheduler.MarkSaveFailed(5000);

            Assert.IsFalse(scheduler.IsSaveDue(6999));
            Assert.IsTrue(scheduler.IsSaveDue(7000));
        }
    }
}
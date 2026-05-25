using System.Reflection;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class SpecialMissionManagerRestoreTests
    {
        [TestMethod]
        public void ApplyHandlerContainerRestoreCheckpointPolicy_MidLiftSnapshot_RollsBackToLoadingCheckpoint()
        {
            var snapshot = new ActiveSpecialMissionPersistenceSnapshot
            {
                MissionId = "port_container_handler",
                StageIndex = 4,
                HandlerContainerPickedUp = true,
                HandlerContainerLoaded = false,
            };

            var rolledBack = InvokeApplyRestoreCheckpointPolicy(snapshot, out var stageIndex, out var containerPickedUp, out var containerLoaded);

            Assert.IsTrue(rolledBack);
            Assert.AreEqual(4, stageIndex);
            Assert.IsFalse(containerPickedUp);
            Assert.IsFalse(containerLoaded);
        }

        [TestMethod]
        public void ApplyHandlerContainerRestoreCheckpointPolicy_LoadedContainerSnapshot_PreservesLoadedState()
        {
            var snapshot = new ActiveSpecialMissionPersistenceSnapshot
            {
                MissionId = "port_container_handler",
                StageIndex = 5,
                HandlerContainerPickedUp = false,
                HandlerContainerLoaded = true,
            };

            var rolledBack = InvokeApplyRestoreCheckpointPolicy(snapshot, out var stageIndex, out var containerPickedUp, out var containerLoaded);

            Assert.IsFalse(rolledBack);
            Assert.AreEqual(5, stageIndex);
            Assert.IsFalse(containerPickedUp);
            Assert.IsTrue(containerLoaded);
        }

        [TestMethod]
        public void BuildHandlerContainerRestoreStatusMessage_WhenRollbackOccurs_ExplainsCheckpointFallback()
        {
            var message = InvokeBuildRestoreStatusMessage("Port Container Handling", true);

            StringAssert.Contains(message, "Port Container Handling");
            StringAssert.Contains(message, "rolled back");
            StringAssert.Contains(message, "loading checkpoint");
        }

        private static bool InvokeApplyRestoreCheckpointPolicy(
            ActiveSpecialMissionPersistenceSnapshot snapshot,
            out int stageIndex,
            out bool containerPickedUp,
            out bool containerLoaded)
        {
            var method = GetHandlerContainerRuntimeType().GetMethod(
                "ApplyRestoreCheckpointPolicy",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(ActiveSpecialMissionPersistenceSnapshot),
                    typeof(int).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                },
                null);
            Assert.IsNotNull(method, "ApplyRestoreCheckpointPolicy");

            var arguments = new object[] { snapshot, 0, false, false };
            var rolledBack = (bool)method.Invoke(null, arguments);
            stageIndex = (int)arguments[1];
            containerPickedUp = (bool)arguments[2];
            containerLoaded = (bool)arguments[3];
            return rolledBack;
        }

        private static string InvokeBuildRestoreStatusMessage(string missionName, bool rolledBackToCheckpoint)
        {
            var method = GetHandlerContainerRuntimeType().GetMethod(
                "BuildRestoreStatusMessage",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            Assert.IsNotNull(method, "BuildRestoreStatusMessage");
            return method.Invoke(null, new object[] { missionName, rolledBackToCheckpoint }) as string;
        }

        private static System.Type GetHandlerContainerRuntimeType()
        {
            var runtimeType = typeof(SpecialMissionManager).GetNestedType("HandlerContainerTransferRuntime", BindingFlags.NonPublic);
            Assert.IsNotNull(runtimeType, "HandlerContainerTransferRuntime");
            return runtimeType;
        }
    }
}
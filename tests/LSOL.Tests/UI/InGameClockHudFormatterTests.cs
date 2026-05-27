using System;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class InGameClockHudFormatterTests
    {
        [TestMethod]
        public void BuildCompactLabel_FormatsKnownDayAndTime()
        {
            var clockDateTime = new DateTime(2026, 5, 25, 8, 35, 0, DateTimeKind.Unspecified);

            Assert.AreEqual("Mon 08:35", InGameClockHudFormatter.BuildCompactLabel(clockDateTime));
        }

        [TestMethod]
        public void BuildCompactLabel_UsesLeadingZeroMinutes()
        {
            var clockDateTime = new DateTime(2026, 5, 26, 14, 5, 0, DateTimeKind.Unspecified);

            Assert.AreEqual("Tue 14:05", InGameClockHudFormatter.BuildCompactLabel(clockDateTime));
        }

        [TestMethod]
        public void BuildCompactLabel_ReflectsDayRollover()
        {
            var beforeMidnight = new DateTime(2026, 5, 31, 23, 59, 0, DateTimeKind.Unspecified);
            var afterMidnight = beforeMidnight.AddMinutes(6);

            Assert.AreEqual("Sun 23:59", InGameClockHudFormatter.BuildCompactLabel(beforeMidnight));
            Assert.AreEqual("Mon 00:05", InGameClockHudFormatter.BuildCompactLabel(afterMidnight));
        }
    }
}
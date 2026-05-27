using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletFleetAlertFormatterTests
    {
        [TestMethod]
        public void BuildFuelRow_WhenFuelIsLow_ShowsActiveVehicleTelemetry()
        {
            var summary = new TabletFleetAlertSummary
            {
                HasActiveCompanyVehicle = true,
                ActiveVehicleName = "Mule",
                FuelCurrentLiters = 38f,
                FuelCapacityLiters = 120f,
                FuelRatio = 38f / 120f,
            };

            var row = TabletFleetAlertFormatter.BuildFuelRow(summary);

            Assert.IsNotNull(row);
            Assert.IsTrue(row.IsAlert);
            Assert.AreEqual("Fuel Watch", row.Caption);
            Assert.AreEqual("Mule at 32% fuel (38/120L). Top it up before a longer lane.", row.Detail);
        }

        [TestMethod]
        public void BuildInspectionRow_WhenVehiclesAreOverdue_SummarizesWorstUnit()
        {
            var summary = new TabletFleetAlertSummary
            {
                OverdueInspectionCount = 2,
                WorstInspectionOverdueWeeks = 3,
                WorstOverdueVehicleName = "Pounder",
            };

            var row = TabletFleetAlertFormatter.BuildInspectionRow(summary);

            Assert.IsNotNull(row);
            Assert.IsTrue(row.IsAlert);
            Assert.AreEqual("Inspection Overdue", row.Caption);
            Assert.AreEqual("2 units overdue; worst Pounder 3w late. Rotate them through repair before weekly maintenance.", row.Detail);
        }

        [TestMethod]
        public void BuildConditionRow_WhenVehiclesHavePoorCondition_SummarizesWorstUnit()
        {
            var summary = new TabletFleetAlertSummary
            {
                PoorConditionCount = 2,
                LowestConditionPercent = 58f,
                WorstConditionVehicleName = "Hauler",
            };

            var row = TabletFleetAlertFormatter.BuildConditionRow(summary);

            Assert.IsNotNull(row);
            Assert.IsTrue(row.IsAlert);
            Assert.AreEqual("Condition Alert", row.Caption);
            Assert.AreEqual("2 units under 75%; worst Hauler 58% condition. Cycle them through the hub.", row.Detail);
        }

        [TestMethod]
        public void BuildRows_WhenThereAreNoAlerts_ReturnsSingleClearRow()
        {
            var rows = TabletFleetAlertFormatter.BuildRows(new TabletFleetAlertSummary());

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Alert Center", rows[0].Caption);
            Assert.AreEqual("No low-fuel, inspection, or condition issues detected.", rows[0].Detail);
            Assert.IsFalse(rows[0].IsAlert);
        }
    }
}
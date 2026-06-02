using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSOL.UI
{
    internal sealed class TabletFleetAlertSummary
    {
        public TabletFleetAlertSummary()
        {
            ActiveVehicleName = string.Empty;
            WorstOverdueVehicleName = string.Empty;
            WorstConditionVehicleName = string.Empty;
        }

        public bool HasActiveCompanyVehicle { get; set; }

        public string ActiveVehicleName { get; set; }

        public bool FuelIsEmpty { get; set; }

        public float FuelCurrentLiters { get; set; }

        public float FuelCapacityLiters { get; set; }

        public float FuelRatio { get; set; }

        public int FleetVehicleCount { get; set; }

        public int OverdueInspectionCount { get; set; }

        public int WorstInspectionOverdueWeeks { get; set; }

        public string WorstOverdueVehicleName { get; set; }

        public int PoorConditionCount { get; set; }

        public float LowestConditionPercent { get; set; }

        public string WorstConditionVehicleName { get; set; }
    }

    internal sealed class TabletFleetAlertRow
    {
        public TabletFleetAlertRow()
        {
            Caption = string.Empty;
            Detail = string.Empty;
        }

        public string Caption { get; set; }

        public string Detail { get; set; }

        public bool IsAlert { get; set; }
    }

    internal static class TabletFleetAlertFormatter
    {
        private const float FuelWatchThreshold = 0.5f;
        private const float FuelUrgentThreshold = 0.2f;
        private const float ConditionAlertThresholdPercent = 75f;

        internal static bool IsFuelWatch(float fuelRatio)
        {
            return Clamp01(fuelRatio) < FuelWatchThreshold;
        }

        internal static bool IsFuelUrgent(float fuelRatio)
        {
            return Clamp01(fuelRatio) < FuelUrgentThreshold;
        }

        public static IReadOnlyList<TabletFleetAlertRow> BuildRows(TabletFleetAlertSummary summary)
        {
            var rows = new List<TabletFleetAlertRow>();
            var fuelRow = BuildFuelRow(summary);
            if (fuelRow != null)
            {
                rows.Add(fuelRow);
            }

            var inspectionRow = BuildInspectionRow(summary);
            if (inspectionRow != null)
            {
                rows.Add(inspectionRow);
            }

            var conditionRow = BuildConditionRow(summary);
            if (conditionRow != null)
            {
                rows.Add(conditionRow);
            }

            if (rows.Count == 0)
            {
                rows.Add(new TabletFleetAlertRow
                {
                    Caption = LocalizedText.GetOrDefault("tablet.fleetAlerts.center", "Alert Center"),
                    Detail = LocalizedText.GetOrDefault("tablet.fleetAlerts.none", "No low-fuel, inspection, or condition issues detected."),
                    IsAlert = false,
                });
            }

            return rows;
        }

        public static TabletFleetAlertRow BuildFuelRow(TabletFleetAlertSummary summary)
        {
            summary = summary ?? new TabletFleetAlertSummary();
            if (!summary.HasActiveCompanyVehicle)
            {
                return null;
            }

            var hasFuelTelemetry = summary.FuelCapacityLiters > 0.001f || summary.FuelCurrentLiters > 0.001f;
            var fuelRatio = Clamp01(summary.FuelRatio);
            if (!summary.FuelIsEmpty && (!hasFuelTelemetry || !IsFuelWatch(fuelRatio)))
            {
                return null;
            }

            var vehicleLabel = ResolveVehicleLabel(summary.ActiveVehicleName, LocalizedText.GetOrDefault("tablet.fleetAlerts.activeUnit", "Active unit"));
            if (summary.FuelIsEmpty)
            {
                return new TabletFleetAlertRow
                {
                    Caption = LocalizedText.GetOrDefault("tablet.fleetAlerts.fuelCritical", "Fuel Critical"),
                    Detail = LocalizedText.FormatOrDefault("tablet.fleetAlerts.fuelCriticalDetail", "{0} is out of fuel. Refuel it now before dispatch stalls.", vehicleLabel),
                    IsAlert = true,
                };
            }

            return new TabletFleetAlertRow
            {
                Caption = IsFuelUrgent(fuelRatio)
                    ? LocalizedText.GetOrDefault("tablet.fleetAlerts.fuelUrgent", "Fuel Urgent")
                    : LocalizedText.GetOrDefault("tablet.fleetAlerts.fuelWatch", "Fuel Watch"),
                Detail = LocalizedText.FormatOrDefault(
                    "tablet.fleetAlerts.fuelDetail",
                    "{0} at {1:0}% fuel ({2:0}/{3:0}L). {4}",
                    vehicleLabel,
                    fuelRatio * 100f,
                    Math.Max(0f, summary.FuelCurrentLiters),
                    Math.Max(0f, summary.FuelCapacityLiters),
                    IsFuelUrgent(fuelRatio)
                        ? LocalizedText.GetOrDefault("tablet.fleetAlerts.fuelUrgentAction", "Refuel before the next run.")
                        : LocalizedText.GetOrDefault("tablet.fleetAlerts.fuelWatchAction", "Top it up before a longer lane.")),
                IsAlert = true,
            };
        }

        public static TabletFleetAlertRow BuildInspectionRow(TabletFleetAlertSummary summary)
        {
            summary = summary ?? new TabletFleetAlertSummary();
            if (summary.OverdueInspectionCount <= 0)
            {
                return null;
            }

            var overdueCount = Math.Max(1, summary.OverdueInspectionCount);
            var vehicleLabel = ResolveVehicleLabel(summary.WorstOverdueVehicleName, string.Empty);
            var overdueWeeks = Math.Max(0, summary.WorstInspectionOverdueWeeks);
            string detail;
            if (overdueCount == 1)
            {
                detail = !string.IsNullOrWhiteSpace(vehicleLabel) && overdueWeeks > 0
                    ? LocalizedText.FormatOrDefault("tablet.fleetAlerts.inspectionSingleWithVehicle", "{0} is {1}w overdue. Rotate it through repair before weekly maintenance.", vehicleLabel, overdueWeeks)
                    : LocalizedText.GetOrDefault("tablet.fleetAlerts.inspectionSingle", "1 unit is overdue. Rotate it through repair before weekly maintenance.");
            }
            else
            {
                detail = !string.IsNullOrWhiteSpace(vehicleLabel) && overdueWeeks > 0
                    ? LocalizedText.FormatOrDefault("tablet.fleetAlerts.inspectionManyWithVehicle", "{0} units overdue; worst {1} {2}w late. Rotate them through repair before weekly maintenance.", overdueCount, vehicleLabel, overdueWeeks)
                    : LocalizedText.FormatOrDefault("tablet.fleetAlerts.inspectionMany", "{0} units overdue. Rotate them through repair before weekly maintenance.", overdueCount);
            }

            return new TabletFleetAlertRow
            {
                Caption = LocalizedText.GetOrDefault("tablet.fleetAlerts.inspectionCaption", "Inspection Overdue"),
                Detail = detail,
                IsAlert = true,
            };
        }

        public static TabletFleetAlertRow BuildConditionRow(TabletFleetAlertSummary summary)
        {
            summary = summary ?? new TabletFleetAlertSummary();
            if (summary.PoorConditionCount <= 0)
            {
                return null;
            }

            var conditionCount = Math.Max(1, summary.PoorConditionCount);
            var vehicleLabel = ResolveVehicleLabel(summary.WorstConditionVehicleName, string.Empty);
            var lowestConditionPercent = Math.Max(0f, summary.LowestConditionPercent);
            string detail;
            if (conditionCount == 1)
            {
                detail = !string.IsNullOrWhiteSpace(vehicleLabel)
                    ? LocalizedText.FormatOrDefault("tablet.fleetAlerts.conditionSingleWithVehicle", "{0} down to {1:0}% condition. Repair it before the next route.", vehicleLabel, lowestConditionPercent)
                    : LocalizedText.FormatOrDefault("tablet.fleetAlerts.conditionSingle", "1 unit down to {0:0}% condition. Repair it before the next route.", lowestConditionPercent);
            }
            else
            {
                detail = !string.IsNullOrWhiteSpace(vehicleLabel)
                    ? LocalizedText.FormatOrDefault("tablet.fleetAlerts.conditionManyWithVehicle", "{0} units under {1:0}%; worst {2} {3:0}% condition. Cycle them through the hub.", conditionCount, ConditionAlertThresholdPercent, vehicleLabel, lowestConditionPercent)
                    : LocalizedText.FormatOrDefault("tablet.fleetAlerts.conditionMany", "{0} units under {1:0}% condition. Cycle them through the hub.", conditionCount, ConditionAlertThresholdPercent);
            }

            return new TabletFleetAlertRow
            {
                Caption = LocalizedText.GetOrDefault("tablet.fleetAlerts.conditionCaption", "Condition Alert"),
                Detail = detail,
                IsAlert = true,
            };
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }

        private static string ResolveVehicleLabel(string label, string fallback)
        {
            return string.IsNullOrWhiteSpace(label)
                ? fallback ?? string.Empty
                : label.Trim();
        }
    }
}
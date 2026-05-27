using System.Globalization;
using LSOL.Systems;

namespace LSOL.UI
{
    internal enum VehicleFuelHudSeverity
    {
        Neutral = 0,
        Healthy = 1,
        Watch = 2,
        Urgent = 3,
        Empty = 4,
    }

    internal sealed class VehicleFuelHudDisplay
    {
        public VehicleFuelHudDisplay()
        {
            PrimaryLabel = string.Empty;
            SecondaryLabel = string.Empty;
        }

        public string PrimaryLabel { get; set; }

        public string SecondaryLabel { get; set; }

        public float FuelRatio { get; set; }

        public VehicleFuelHudSeverity Severity { get; set; }
    }

    internal static class VehicleFuelHudFormatter
    {
        public static VehicleFuelHudDisplay BuildDisplay(VehicleFuelTelemetry telemetry, bool useMetricDistance, bool includeRangeEstimate)
        {
            telemetry = telemetry ?? new VehicleFuelTelemetry();
            return new VehicleFuelHudDisplay
            {
                PrimaryLabel = BuildPrimaryLabel(telemetry),
                SecondaryLabel = includeRangeEstimate ? BuildRangeLabel(telemetry, useMetricDistance) : string.Empty,
                FuelRatio = telemetry.FuelRatio,
                Severity = GetSeverity(telemetry),
            };
        }

        public static string BuildPrimaryLabel(VehicleFuelTelemetry telemetry)
        {
            if (telemetry == null || telemetry.CapacityLiters <= 0.001f)
            {
                return "Fuel n/a";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Fuel {0:0}% | {1:0}/{2:0}L{3}",
                ModMath.Clamp01(telemetry.FuelRatio) * 100f,
                telemetry.CurrentLiters,
                telemetry.CapacityLiters,
                telemetry.UsesSeparatePoweredVehicle ? " | Tractor" : string.Empty);
        }

        public static string BuildRangeLabel(VehicleFuelTelemetry telemetry, bool useMetricDistance)
        {
            if (telemetry == null || telemetry.CapacityLiters <= 0.001f)
            {
                return string.Empty;
            }

            if (telemetry.IsOutOfFuel)
            {
                return string.Format(CultureInfo.InvariantCulture, "Range {0}", ModFormatting.FormatDistance(0d, useMetricDistance));
            }

            if (!telemetry.HasRangeEstimate)
            {
                return "Range calibrating";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Range {0}",
                ModFormatting.FormatDistance(telemetry.EstimatedRangeMeters, useMetricDistance));
        }

        public static VehicleFuelHudSeverity GetSeverity(VehicleFuelTelemetry telemetry)
        {
            if (telemetry == null || telemetry.CapacityLiters <= 0.001f)
            {
                return VehicleFuelHudSeverity.Neutral;
            }

            if (telemetry.IsOutOfFuel)
            {
                return VehicleFuelHudSeverity.Empty;
            }

            if (TabletFleetAlertFormatter.IsFuelUrgent(telemetry.FuelRatio))
            {
                return VehicleFuelHudSeverity.Urgent;
            }

            if (TabletFleetAlertFormatter.IsFuelWatch(telemetry.FuelRatio))
            {
                return VehicleFuelHudSeverity.Watch;
            }

            return VehicleFuelHudSeverity.Healthy;
        }
    }
}
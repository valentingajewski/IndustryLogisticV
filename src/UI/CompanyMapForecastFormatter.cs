using System;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class CompanyMapForecastFormatter
    {
        private const float ForecastDisplayThresholdTons = 0.01f;

        public static string BuildDistrictLicenseForecast(TerritoryDistrictState district)
        {
            if (district == null
                || (district.LicenseStatus != DistrictLicenseStatus.Active && district.LicenseStatus != DistrictLicenseStatus.Probation))
            {
                return string.Empty;
            }

            var requiredTons = Math.Max(0f, district.RequiredWeeklyActivityTons);
            if (requiredTons <= ForecastDisplayThresholdTons)
            {
                return string.Empty;
            }

            var shortfallTons = Math.Max(0f, requiredTons - Math.Max(0f, district.CurrentWeekActivityTons));
            if (shortfallTons <= ForecastDisplayThresholdTons)
            {
                return district.LicenseStatus == DistrictLicenseStatus.Probation
                    ? "Forecast: on pace to restore Active this week."
                    : "Forecast: safe this week.";
            }

            return string.Format(
                "Forecast: {0} | Need {1} more before maintenance.",
                district.LicenseStatus == DistrictLicenseStatus.Probation || district.LicenseStrikeCount > 0
                    ? "suspension risk"
                    : "probation risk",
                FormatTons(shortfallTons));
        }

        public static string BuildCorridorForecast(TerritoryCorridorState corridor)
        {
            if (corridor == null || corridor.RightLevel == CorridorRightLevel.None)
            {
                return string.Empty;
            }

            var requiredTons = Math.Max(0f, corridor.RequiredWeeklyDeliveredTons);
            if (requiredTons <= ForecastDisplayThresholdTons)
            {
                return corridor.UpkeepStatus ?? string.Empty;
            }

            var shortfallTons = Math.Max(0f, requiredTons - Math.Max(0f, corridor.CurrentWeekDeliveredTons));
            if (shortfallTons <= ForecastDisplayThresholdTons)
            {
                return "Target met this week | No corridor decay on maintenance.";
            }

            if (StartsWithStatus(corridor.UpkeepStatus, "At risk"))
            {
                return string.Format("At risk of decay | Need {0} this week to avoid degradation.", FormatTons(shortfallTons));
            }

            if (StartsWithStatus(corridor.UpkeepStatus, "Watch"))
            {
                return string.Format("Watch | Need {0} this week to stay clear.", FormatTons(shortfallTons));
            }

            return string.Format("Stable for now | Need {0} this week to finish upkeep.", FormatTons(shortfallTons));
        }

        private static bool StartsWithStatus(string upkeepStatus, string prefix)
        {
            return !string.IsNullOrWhiteSpace(upkeepStatus)
                && upkeepStatus.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatTons(float tons)
        {
            return string.Format("{0:0.#}t", Math.Max(0f, tons));
        }
    }
}
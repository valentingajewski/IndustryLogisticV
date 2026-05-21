using System;

namespace LSOL.Domain
{
    internal static class ServiceSiteEconomyPolicy
    {
        public const float WeeklyWageRatio = 0.12f;
        public const float WeeklyWageMinimum = 75f;
        public const float WeeklyWageMaximum = 300f;
        public const float WeeklyWageStep = 25f;
        public const float MinimumWeeklyNetMargin = WeeklyWageStep;

        public static float NormalizeWeeklyPassiveIncome(SiteRole siteRole, bool refuelIsFree, float weeklyPassiveIncome)
        {
            var normalizedIncome = Math.Max(0f, weeklyPassiveIncome);
            if (!IsServiceSite(siteRole))
            {
                return normalizedIncome;
            }

            if (refuelIsFree && normalizedIncome <= 0.01f)
            {
                return 0f;
            }

            return normalizedIncome;
        }

        public static float ComputeWeeklyStaffingCost(SiteRole siteRole, bool refuelIsFree, float weeklyPassiveIncome)
        {
            var normalizedIncome = NormalizeWeeklyPassiveIncome(siteRole, refuelIsFree, weeklyPassiveIncome);
            if (!IsServiceSite(siteRole) || normalizedIncome <= 0.01f)
            {
                return 0f;
            }

            var scaledWage = Math.Max(WeeklyWageMinimum, Math.Min(WeeklyWageMaximum, normalizedIncome * WeeklyWageRatio));
            var roundedWage = RoundUpToStep(scaledWage, WeeklyWageStep);
            var affordableWage = RoundDownToStep(Math.Max(0f, normalizedIncome - MinimumWeeklyNetMargin), WeeklyWageStep);
            if (affordableWage <= 0f)
            {
                return 0f;
            }

            return Math.Min(roundedWage, affordableWage);
        }

        public static float ComputeWeeklyStaffingCost(Industry industry)
        {
            return industry == null
                ? 0f
                : ComputeWeeklyStaffingCost(industry.SiteRole, industry.RefuelIsFree, industry.WeeklyPassiveIncome);
        }

        public static bool IsServiceSite(SiteRole siteRole)
        {
            return siteRole == SiteRole.StoreSink || siteRole == SiteRole.FuelSink;
        }

        private static float RoundUpToStep(float value, float step)
        {
            if (value <= 0f || step <= 0f)
            {
                return 0f;
            }

            return (float)(Math.Ceiling(value / step) * step);
        }

        private static float RoundDownToStep(float value, float step)
        {
            if (value <= 0f || step <= 0f)
            {
                return 0f;
            }

            return (float)(Math.Floor(value / step) * step);
        }
    }
}
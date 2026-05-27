using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LSOL;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class CompanyMapDepotSpecializationInfo
    {
        public CompanyMapDepotSpecializationInfo()
        {
            Label = string.Empty;
            EffectSummary = string.Empty;
        }

        public DepotSpecialization Specialization { get; set; }

        public string Label { get; set; }

        public string EffectSummary { get; set; }
    }

    internal static class CompanyMapDepotSpecializationFormatter
    {
        public static string BuildDepotListDetail(string districtName, string activationSummary, CompanyMapDepotSpecializationInfo specializationInfo, TerritorySiteState siteState)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | {1} | {2} | Staff {3}/{4}/{5}/{6}",
                districtName ?? string.Empty,
                activationSummary ?? string.Empty,
                BuildCompactRoleEffect(specializationInfo, true),
                siteState != null ? Math.Max(0, siteState.LoaderCount) : 0,
                siteState != null ? Math.Max(0, siteState.MechanicCount) : 0,
                siteState != null ? Math.Max(0, siteState.GuardCount) : 0,
                siteState != null ? Math.Max(0, siteState.ManagerCount) : 0);
        }

        public static string BuildDepotDistrictBonusDetail(float supportBonusPercent, CompanyMapDepotSpecializationInfo specializationInfo)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Current district support bonus {0}. {1}",
                ModFormatting.FormatSignedPercent(supportBonusPercent),
                BuildCompactRoleEffect(specializationInfo, false));
        }

        public static string BuildDistrictSupportDetail(int franchiseSites, int routeRights, float supportBonusPercent, string riskSuffix, IReadOnlyList<CompanyMapDepotSpecializationInfo> specializationInfos)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Franchises {0} | Corridor rights {1} | Support bonus {2}{3} | {4}",
                Math.Max(0, franchiseSites),
                Math.Max(0, routeRights),
                ModFormatting.FormatSignedPercent(supportBonusPercent),
                riskSuffix ?? string.Empty,
                BuildDistrictRoleSummary(specializationInfos));
        }

        public static string BuildNetworkSupportSummary(int controlledDepots, int franchiseSites, int routeRights, float supportBonusPercent, string riskSuffix, IReadOnlyList<CompanyMapDepotSpecializationInfo> specializationInfos)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Depots {0} | Franchises {1}\nCorridor rights {2} | Support bonus {3}{4}\n{5}",
                Math.Max(0, controlledDepots),
                Math.Max(0, franchiseSites),
                Math.Max(0, routeRights),
                ModFormatting.FormatSignedPercent(supportBonusPercent),
                riskSuffix ?? string.Empty,
                BuildDistrictRoleSummary(specializationInfos));
        }

        public static string BuildDistrictRoleSummary(IReadOnlyList<CompanyMapDepotSpecializationInfo> specializationInfos)
        {
            var summaries = GetActiveEffectSummaries(specializationInfos);
            if (summaries.Count == 0)
            {
                return "Depot roles: general coverage only.";
            }

            return string.Format(CultureInfo.InvariantCulture, "Depot roles: {0}.", string.Join("; ", summaries));
        }

        private static string BuildCompactRoleEffect(CompanyMapDepotSpecializationInfo specializationInfo, bool listFallback)
        {
            if (specializationInfo == null || specializationInfo.Specialization == DepotSpecialization.None)
            {
                return listFallback
                    ? "General coverage only. Use depot detail to assign a district role."
                    : "General coverage only. Use left/right to assign a district role.";
            }

            var summary = NormalizeSummary(specializationInfo.EffectSummary);
            return string.IsNullOrWhiteSpace(summary)
                ? string.Format(CultureInfo.InvariantCulture, "{0} role active.", specializationInfo.Label ?? "Depot")
                : summary;
        }

        private static IReadOnlyList<string> GetActiveEffectSummaries(IReadOnlyList<CompanyMapDepotSpecializationInfo> specializationInfos)
        {
            if (specializationInfos == null || specializationInfos.Count == 0)
            {
                return Array.Empty<string>();
            }

            return specializationInfos
                .Where(info => info != null && info.Specialization != DepotSpecialization.None)
                .Select(info => TrimSummaryPeriod(NormalizeSummary(info.EffectSummary)))
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizeSummary(string summary)
        {
            return (summary ?? string.Empty).Trim();
        }

        private static string TrimSummaryPeriod(string summary)
        {
            var trimmed = NormalizeSummary(summary);
            while (trimmed.EndsWith(".", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
            }

            return trimmed;
        }
    }
}
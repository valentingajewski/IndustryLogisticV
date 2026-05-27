using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal enum AlertRuleDomain
    {
        Rent = 0,
        Contracts = 1,
        Fleet = 2,
        Territory = 3,
    }

    internal enum AlertRuleSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2,
    }

    internal sealed class AlertRulesEvaluationContext
    {
        public AlertRulesEvaluationContext()
        {
            UpcomingBills = Array.Empty<TabletUpcomingBillEntry>();
            AcceptedPlayerContracts = Array.Empty<PlayerContractListingSummary>();
            FleetSummary = new TabletFleetAlertSummary();
            DistrictComparisons = Array.Empty<TabletDistrictComparison>();
        }

        public IReadOnlyList<TabletUpcomingBillEntry> UpcomingBills { get; set; }

        public IReadOnlyList<PlayerContractListingSummary> AcceptedPlayerContracts { get; set; }

        public TabletFleetAlertSummary FleetSummary { get; set; }

        public IReadOnlyList<TabletDistrictComparison> DistrictComparisons { get; set; }
    }

    internal sealed class AlertRuleNotification
    {
        public string Key { get; set; }

        public AlertRuleDomain Domain { get; set; }

        public AlertRuleSeverity Severity { get; set; }

        public int SortMinutes { get; set; } = int.MaxValue;

        public string Message { get; set; }
    }

    internal static class AlertRulesEvaluator
    {
        private const int TerritoryChargeLeadTimeMinutes = 180;
        private const int PlayerContractCriticalMinutes = 15;
        private const float TerritoryCompetitionWarningThresholdPercent = 70f;
        private const float TerritoryCompetitionGapThresholdPercent = 20f;

        public static IReadOnlyList<AlertRuleNotification> BuildCandidates(
            AlertRulesPersistenceSnapshot settings,
            AlertRulesEvaluationContext context)
        {
            settings = settings ?? new AlertRulesPersistenceSnapshot();
            context = context ?? new AlertRulesEvaluationContext();

            var candidates = new List<AlertRuleNotification>();
            AddRentCandidates(candidates, settings.RentLeadTime, context.UpcomingBills);
            AddContractCandidates(candidates, settings.ContractLeadTime, context.AcceptedPlayerContracts, context.UpcomingBills);
            AddFleetCandidates(candidates, settings.FleetMode, context.FleetSummary);
            AddTerritoryCandidates(candidates, settings.TerritoryMode, context.UpcomingBills, context.DistrictComparisons);

            return candidates
                .OrderByDescending(candidate => candidate.Severity)
                .ThenBy(candidate => candidate.SortMinutes)
                .ThenBy(candidate => candidate.Domain)
                .ThenBy(candidate => candidate.Message ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static AlertRuleNotification SelectNextNotification(
            AlertRulesPersistenceSnapshot settings,
            AlertRulesEvaluationContext context,
            IDictionary<string, int> lastShownByKey,
            int currentGameTime,
            int cooldownMs)
        {
            var candidates = BuildCandidates(settings, context);
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Key))
                {
                    continue;
                }

                int lastShownAt;
                if (lastShownByKey != null
                    && lastShownByKey.TryGetValue(candidate.Key, out lastShownAt)
                    && currentGameTime - lastShownAt < cooldownMs)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        internal static int GetLeadTimeMinutes(AlertLeadTimeMode mode)
        {
            switch (mode)
            {
                case AlertLeadTimeMode.DueNow:
                    return 0;
                case AlertLeadTimeMode.Within60Minutes:
                    return 60;
                case AlertLeadTimeMode.Within180Minutes:
                    return 180;
                case AlertLeadTimeMode.WithinDay:
                    return 24 * 60;
                case AlertLeadTimeMode.Off:
                default:
                    return -1;
            }
        }

        private static void AddRentCandidates(
            ICollection<AlertRuleNotification> candidates,
            AlertLeadTimeMode mode,
            IReadOnlyList<TabletUpcomingBillEntry> bills)
        {
            if (candidates == null || !MatchesLeadTime(mode, 0) && mode == AlertLeadTimeMode.Off)
            {
                return;
            }

            if (bills == null)
            {
                return;
            }

            foreach (var bill in bills.Where(entry => entry != null && IsRentBill(entry.Category) && MatchesLeadTime(mode, entry.DueInMinutes)))
            {
                var severity = bill.DueInMinutes <= 0
                    ? AlertRuleSeverity.Critical
                    : AlertRuleSeverity.Warning;
                candidates.Add(new AlertRuleNotification
                {
                    Key = string.Format("rent:{0}:{1}:{2}", bill.Category, bill.Label ?? string.Empty, severity),
                    Domain = AlertRuleDomain.Rent,
                    Severity = severity,
                    SortMinutes = Math.Max(0, bill.DueInMinutes),
                    Message = string.Format(
                        "Rent due {0}: {1} {2}. {3}",
                        bill.DueInMinutes <= 0 ? "now" : TabletDeadlineFormatter.FormatDueInMinutes(bill.DueInMinutes),
                        bill.Label ?? "Charge",
                        ModFormatting.FormatMoney(bill.Amount),
                        bill.Detail ?? string.Empty).Trim(),
                });
            }
        }

        private static void AddContractCandidates(
            ICollection<AlertRuleNotification> candidates,
            AlertLeadTimeMode mode,
            IReadOnlyList<PlayerContractListingSummary> acceptedContracts,
            IReadOnlyList<TabletUpcomingBillEntry> bills)
        {
            if (candidates == null || mode == AlertLeadTimeMode.Off)
            {
                return;
            }

            if (acceptedContracts != null)
            {
                foreach (var contract in acceptedContracts.Where(ShouldAlertForPlayerContract).Where(contract => MatchesLeadTime(mode, contract.RemainingExpiryMinutes)))
                {
                    var severity = contract.RemainingExpiryMinutes <= PlayerContractCriticalMinutes
                        ? AlertRuleSeverity.Critical
                        : AlertRuleSeverity.Warning;
                    candidates.Add(new AlertRuleNotification
                    {
                        Key = string.Format("contracts:player:{0}:{1}", contract.Id ?? string.Empty, severity),
                        Domain = AlertRuleDomain.Contracts,
                        Severity = severity,
                        SortMinutes = Math.Max(0, contract.RemainingExpiryMinutes),
                        Message = string.Format(
                            "Contract expiry: {0} has {1}. {2}",
                            BuildPlayerContractCaption(contract),
                            TabletDeadlineFormatter.BuildPlayerContractExpiryLabel(contract),
                            contract.StatusDetail ?? string.Empty).Trim(),
                    });
                }
            }

            if (bills == null)
            {
                return;
            }

            foreach (var bill in bills.Where(entry => entry != null && entry.Category == CompanyFinanceCategory.NpcWages && MatchesLeadTime(mode, entry.DueInMinutes)))
            {
                var severity = bill.DueInMinutes <= 0
                    ? AlertRuleSeverity.Critical
                    : AlertRuleSeverity.Warning;
                candidates.Add(new AlertRuleNotification
                {
                    Key = string.Format("contracts:payroll:{0}:{1}", bill.Label ?? string.Empty, severity),
                    Domain = AlertRuleDomain.Contracts,
                    Severity = severity,
                    SortMinutes = Math.Max(0, bill.DueInMinutes),
                    Message = string.Format(
                        "NPC payroll due {0}: {1} {2}. {3}",
                        bill.DueInMinutes <= 0 ? "now" : TabletDeadlineFormatter.FormatDueInMinutes(bill.DueInMinutes),
                        bill.Label ?? "Route",
                        ModFormatting.FormatMoney(bill.Amount),
                        bill.Detail ?? string.Empty).Trim(),
                });
            }
        }

        private static void AddFleetCandidates(
            ICollection<AlertRuleNotification> candidates,
            FleetAlertMode mode,
            TabletFleetAlertSummary summary)
        {
            if (candidates == null || mode == FleetAlertMode.Off)
            {
                return;
            }

            summary = summary ?? new TabletFleetAlertSummary();

            var fuelRow = TabletFleetAlertFormatter.BuildFuelRow(summary);
            if (fuelRow != null && (mode == FleetAlertMode.WatchAndCritical || summary.FuelIsEmpty || TabletFleetAlertFormatter.IsFuelUrgent(summary.FuelRatio)))
            {
                var severity = summary.FuelIsEmpty || TabletFleetAlertFormatter.IsFuelUrgent(summary.FuelRatio)
                    ? AlertRuleSeverity.Critical
                    : AlertRuleSeverity.Warning;
                candidates.Add(new AlertRuleNotification
                {
                    Key = string.Format("fleet:fuel:{0}:{1}", summary.ActiveVehicleName ?? string.Empty, severity),
                    Domain = AlertRuleDomain.Fleet,
                    Severity = severity,
                    SortMinutes = summary.FuelIsEmpty ? 0 : 1,
                    Message = string.Format("{0}: {1}", fuelRow.Caption, fuelRow.Detail),
                });
            }

            var inspectionRow = TabletFleetAlertFormatter.BuildInspectionRow(summary);
            if (inspectionRow != null)
            {
                candidates.Add(new AlertRuleNotification
                {
                    Key = string.Format("fleet:inspection:{0}:Critical", summary.WorstOverdueVehicleName ?? string.Empty),
                    Domain = AlertRuleDomain.Fleet,
                    Severity = AlertRuleSeverity.Critical,
                    SortMinutes = 2,
                    Message = string.Format("{0}: {1}", inspectionRow.Caption, inspectionRow.Detail),
                });
            }

            var conditionRow = TabletFleetAlertFormatter.BuildConditionRow(summary);
            if (conditionRow != null)
            {
                candidates.Add(new AlertRuleNotification
                {
                    Key = string.Format("fleet:condition:{0}:Critical", summary.WorstConditionVehicleName ?? string.Empty),
                    Domain = AlertRuleDomain.Fleet,
                    Severity = AlertRuleSeverity.Critical,
                    SortMinutes = 3,
                    Message = string.Format("{0}: {1}", conditionRow.Caption, conditionRow.Detail),
                });
            }
        }

        private static void AddTerritoryCandidates(
            ICollection<AlertRuleNotification> candidates,
            TerritoryAlertMode mode,
            IReadOnlyList<TabletUpcomingBillEntry> bills,
            IReadOnlyList<TabletDistrictComparison> districts)
        {
            if (candidates == null || mode == TerritoryAlertMode.Off)
            {
                return;
            }

            if (bills != null)
            {
                foreach (var bill in bills.Where(entry => entry != null && entry.Category == CompanyFinanceCategory.TerritoryOperations && entry.DueInMinutes <= TerritoryChargeLeadTimeMinutes))
                {
                    var severity = bill.DueInMinutes <= 0
                        ? AlertRuleSeverity.Critical
                        : AlertRuleSeverity.Warning;
                    candidates.Add(new AlertRuleNotification
                    {
                        Key = string.Format("territory:charge:{0}:{1}", bill.Label ?? string.Empty, severity),
                        Domain = AlertRuleDomain.Territory,
                        Severity = severity,
                        SortMinutes = Math.Max(0, bill.DueInMinutes),
                        Message = string.Format(
                            "Territory charge due {0}: {1} {2}. {3}",
                            bill.DueInMinutes <= 0 ? "now" : TabletDeadlineFormatter.FormatDueInMinutes(bill.DueInMinutes),
                            bill.Label ?? "District",
                            ModFormatting.FormatMoney(bill.Amount),
                            bill.Detail ?? string.Empty).Trim(),
                    });
                }
            }

            if (mode != TerritoryAlertMode.ChargesAndRisk || districts == null)
            {
                return;
            }

            foreach (var district in districts)
            {
                var candidate = BuildDistrictRiskNotification(district);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        private static AlertRuleNotification BuildDistrictRiskNotification(TabletDistrictComparison district)
        {
            if (!ShouldAlertForTerritoryDistrict(district))
            {
                return null;
            }

            AlertRuleSeverity severity;
            string headline;

            if (district.LicenseStatus == DistrictLicenseStatus.Suspended)
            {
                severity = AlertRuleSeverity.Critical;
                headline = string.Format("Territory risk: {0} charter suspended.", district.DistrictName);
            }
            else if (district.LicenseStatus == DistrictLicenseStatus.Probation)
            {
                severity = AlertRuleSeverity.Warning;
                headline = string.Format("Territory risk: {0} charter on probation.", district.DistrictName);
            }
            else if (district.CorridorRiskCount > 0 || district.ServiceRiskCount > 0)
            {
                severity = district.CorridorRiskCount + district.ServiceRiskCount >= 2
                    ? AlertRuleSeverity.Critical
                    : AlertRuleSeverity.Warning;
                headline = string.Format("Territory risk: {0} has corridor/service pressure.", district.DistrictName);
            }
            else if (district.CompetitivePressurePercent >= TerritoryCompetitionWarningThresholdPercent
                && district.CompetitivePressurePercent >= district.CompetitiveOpportunityPercent + TerritoryCompetitionGapThresholdPercent)
            {
                severity = AlertRuleSeverity.Warning;
                headline = string.Format("Territory pressure: {0} competition is rising.", district.DistrictName);
            }
            else if (!string.IsNullOrWhiteSpace(district.DistrictEventHeadline))
            {
                severity = district.DistrictEventSeverityPercent >= 70f
                    ? AlertRuleSeverity.Critical
                    : AlertRuleSeverity.Warning;
                headline = string.Format("Territory event: {0}.", district.DistrictEventHeadline);
            }
            else
            {
                return null;
            }

            return new AlertRuleNotification
            {
                Key = string.Format("territory:risk:{0}:{1}", district.DistrictName ?? string.Empty, severity),
                Domain = AlertRuleDomain.Territory,
                Severity = severity,
                SortMinutes = 0,
                Message = string.Format("{0} {1}", headline, BuildDistrictRiskDetail(district)).Trim(),
            };
        }

        private static string BuildDistrictRiskDetail(TabletDistrictComparison district)
        {
            var segments = new List<string>();
            if (district.LicenseTargetTons > 0.01f)
            {
                segments.Add(string.Format("Activity {0:0}/{1:0}t", Math.Max(0f, district.LicenseActivityTons), Math.Max(0f, district.LicenseTargetTons)));
            }

            if (district.CorridorRiskCount > 0 || district.ServiceRiskCount > 0)
            {
                segments.Add(string.Format("Risk C{0}/S{1}", Math.Max(0, district.CorridorRiskCount), Math.Max(0, district.ServiceRiskCount)));
            }

            if (district.CompetitivePressurePercent > 0.01f || district.CompetitiveOpportunityPercent > 0.01f)
            {
                segments.Add(string.Format(
                    "Comp {0:0}% / Opp {1:0}%",
                    Math.Max(0f, district.CompetitivePressurePercent),
                    Math.Max(0f, district.CompetitiveOpportunityPercent)));
            }

            if (!string.IsNullOrWhiteSpace(district.DistrictEventHeadline))
            {
                var commodity = string.IsNullOrWhiteSpace(district.DistrictEventCommodity)
                    ? string.Empty
                    : string.Format(" {0}", district.DistrictEventCommodity);
                var status = string.IsNullOrWhiteSpace(district.DistrictEventStatus)
                    ? district.DistrictEventSeverityLabel
                    : district.DistrictEventStatus;
                segments.Add(string.Format("Event {0}{1} | {2}", district.DistrictEventHeadline, commodity, status).Trim());
            }

            return segments.Count > 0
                ? string.Join(" | ", segments.ToArray())
                : string.Empty;
        }

        private static bool MatchesLeadTime(AlertLeadTimeMode mode, int dueInMinutes)
        {
            var thresholdMinutes = GetLeadTimeMinutes(mode);
            return thresholdMinutes >= 0 && Math.Max(0, dueInMinutes) <= thresholdMinutes;
        }

        private static bool IsRentBill(CompanyFinanceCategory category)
        {
            return category == CompanyFinanceCategory.OfficeRent
                || category == CompanyFinanceCategory.ApartmentRent
                || category == CompanyFinanceCategory.VehicleRent;
        }

        private static bool ShouldAlertForPlayerContract(PlayerContractListingSummary contract)
        {
            return contract != null
                && !string.IsNullOrWhiteSpace(contract.Id)
                && contract.RemainingExpiryMinutes != int.MaxValue
                && contract.Status != PlayerContractStatus.Completed
                && contract.Status != PlayerContractStatus.Cancelled
                && contract.Status != PlayerContractStatus.Expired;
        }

        private static bool ShouldAlertForTerritoryDistrict(TabletDistrictComparison district)
        {
            return district != null
                && !string.IsNullOrWhiteSpace(district.DistrictName)
                && (district.WeeklyOperationsCost > 0.01f
                    || district.ControlledSites > 0
                    || district.ControlledDepots > 0
                    || district.LicenseStatus != DistrictLicenseStatus.None);
        }

        private static string BuildPlayerContractCaption(PlayerContractListingSummary contract)
        {
            if (contract == null)
            {
                return "Contract";
            }

            return string.Format(
                "{0} | {1} -> {2}",
                contract.Commodity ?? "Cargo",
                contract.OriginName ?? contract.OriginIndustryId ?? "Origin",
                contract.DestinationName ?? contract.DestinationIndustryId ?? "Destination");
        }
    }
}
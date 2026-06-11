using System;
using LSOL.UI;

namespace LSOL
{
    internal static class ServiceStatusCatalog
    {
        public const string ContractOpenMarket = "tablet.service.contractStatus.openMarket";
        public const string ContractAtRisk = "tablet.service.contractStatus.atRisk";
        public const string ContractWatch = "tablet.service.contractStatus.watch";
        public const string ContractSecured = "tablet.service.contractStatus.secured";
        public const string ContractAwaitingWeeklyService = "tablet.service.contractStatus.awaitingWeeklyService";

        public const string PassiveIncomeLocked = "tablet.service.passiveIncome.locked";
        public const string PassiveIncomePending = "tablet.service.passiveIncome.pending";
        public const string PassiveIncomeNoCompletedPayout = "tablet.service.passiveIncome.noCompletedPayout";
        public const string PassiveIncomeNoRecentPayout = "tablet.service.passiveIncome.noRecentPayout";
        public const string PassiveIncomeActive = "tablet.service.passiveIncome.active";
        public const string PassiveIncomeActiveAtRisk = "tablet.service.passiveIncome.activeAtRisk";
        public const string PassiveIncomeActiveWatch = "tablet.service.passiveIncome.activeWatch";
        public const string PassiveIncomePaidLastWeek = "tablet.service.passiveIncome.paidLastWeek";
        public const string PassiveIncomePaidLastWeekAtRisk = "tablet.service.passiveIncome.paidLastWeekAtRisk";
        public const string PassiveIncomePaidLastWeekWatch = "tablet.service.passiveIncome.paidLastWeekWatch";
        public const string PassiveIncomeMissedLastWeek = "tablet.service.passiveIncome.missedLastWeek";
        public const string PassiveIncomeMissedLastWeekNoStaff = "tablet.service.passiveIncome.missedLastWeekNoStaff";
        public const string PassiveIncomeMissedLastWeekUnderstocked = "tablet.service.passiveIncome.missedLastWeekUnderstocked";

        public const string StatusNoStaffAssigned = "tablet.service.status.noStaffAssigned";
        public const string StatusUnderstocked = "tablet.service.status.understocked";
        public const string StatusAssigned = "tablet.service.status.assigned";
        public const string StatusMissing = "tablet.service.status.missing";
        public const string StatusRequiresOperator = "tablet.service.status.requiresOperator";
        public const string StatusReady = "tablet.service.status.ready";
        public const string StatusLowStock = "tablet.service.status.lowStock";
        public const string StatusRequiresStock = "tablet.service.status.requiresStock";
        public const string StatusOperational = "tablet.service.status.operational";
        public const string StatusInactive = "tablet.service.status.inactive";
        public const string StatusPotentialOnly = "tablet.service.status.potentialOnly";

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            switch (value)
            {
                case ContractOpenMarket:
                case "Open market":
                    return ContractOpenMarket;
                case ContractAtRisk:
                case "Contract at risk":
                    return ContractAtRisk;
                case ContractWatch:
                case "Contract watch":
                    return ContractWatch;
                case ContractSecured:
                case "Contract secured":
                    return ContractSecured;
                case ContractAwaitingWeeklyService:
                case "Awaiting weekly service":
                    return ContractAwaitingWeeklyService;
                case PassiveIncomeLocked:
                case "Passive income locked":
                    return PassiveIncomeLocked;
                case PassiveIncomePending:
                case "Passive income pending":
                    return PassiveIncomePending;
                case PassiveIncomeNoCompletedPayout:
                case "No completed weekly payout yet":
                    return PassiveIncomeNoCompletedPayout;
                case PassiveIncomeNoRecentPayout:
                case "No recent payout":
                    return PassiveIncomeNoRecentPayout;
                case PassiveIncomeActive:
                case "Passive income active":
                    return PassiveIncomeActive;
                case PassiveIncomeActiveAtRisk:
                case "Passive income active | Contract at risk":
                    return PassiveIncomeActiveAtRisk;
                case PassiveIncomeActiveWatch:
                case "Passive income active | Contract watch":
                    return PassiveIncomeActiveWatch;
                case PassiveIncomePaidLastWeek:
                case "Paid last week":
                    return PassiveIncomePaidLastWeek;
                case PassiveIncomePaidLastWeekAtRisk:
                case "Paid last week | Contract at risk":
                    return PassiveIncomePaidLastWeekAtRisk;
                case PassiveIncomePaidLastWeekWatch:
                case "Paid last week | Contract watch":
                    return PassiveIncomePaidLastWeekWatch;
                case PassiveIncomeMissedLastWeek:
                case "Missed last week":
                    return PassiveIncomeMissedLastWeek;
                case PassiveIncomeMissedLastWeekNoStaff:
                case "Missed last week | No staff assigned":
                    return PassiveIncomeMissedLastWeekNoStaff;
                case PassiveIncomeMissedLastWeekUnderstocked:
                case "Missed last week | Understocked":
                    return PassiveIncomeMissedLastWeekUnderstocked;
                case StatusNoStaffAssigned:
                case "No staff assigned":
                    return StatusNoStaffAssigned;
                case StatusUnderstocked:
                case "Understocked":
                    return StatusUnderstocked;
                case StatusAssigned:
                case "Assigned":
                    return StatusAssigned;
                case StatusMissing:
                case "Missing":
                    return StatusMissing;
                case StatusRequiresOperator:
                case "Requires operator":
                    return StatusRequiresOperator;
                case StatusReady:
                case "Ready":
                    return StatusReady;
                case StatusLowStock:
                case "Low stock":
                    return StatusLowStock;
                case StatusRequiresStock:
                case "Requires stock":
                    return StatusRequiresStock;
                case StatusOperational:
                case "Operational":
                    return StatusOperational;
                case StatusInactive:
                case "Inactive":
                    return StatusInactive;
                case StatusPotentialOnly:
                case "Potential only":
                    return StatusPotentialOnly;
                default:
                    return value;
            }
        }

        public static bool IsOpenMarket(string value)
        {
            return string.Equals(NormalizeKey(value), ContractOpenMarket, StringComparison.OrdinalIgnoreCase);
        }

        public static string Display(string value)
        {
            var normalized = NormalizeKey(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized.StartsWith("tablet.", StringComparison.OrdinalIgnoreCase)
                ? LocalizedText.GetOrDefault(normalized, normalized)
                : normalized;
        }
    }
}
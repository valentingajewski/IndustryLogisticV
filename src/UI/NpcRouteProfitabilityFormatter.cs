using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LSOL;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class TabletNpcRouteFamilySummary
    {
        public TabletNpcRouteFamilySummary()
        {
            Key = string.Empty;
            Label = string.Empty;
            CommodityPattern = string.Empty;
            CargoTypePattern = string.Empty;
        }

        public string Key { get; set; }

        public string Label { get; set; }

        public string CommodityPattern { get; set; }

        public string CargoTypePattern { get; set; }

        public int ContractCount { get; set; }

        public int RouteCount { get; set; }

        public float Revenue { get; set; }

        public float OperatingCost { get; set; }

        public float NetProfit { get; set; }

        public int CompletedDeliveries { get; set; }

        public float DeliveredTons { get; set; }

        public float AveragePayout { get; set; }

        public float AverageLossRatioPercent { get; set; }
    }

    internal sealed class TabletNpcRouteLegSummary
    {
        public TabletNpcRouteLegSummary()
        {
            OriginName = string.Empty;
            DestinationName = string.Empty;
            Commodity = string.Empty;
            AssignedVehicleDisplayName = string.Empty;
        }

        public int RouteIndex { get; set; }

        public int RouteCount { get; set; }

        public bool IsCurrentRoute { get; set; }

        public string OriginName { get; set; }

        public string DestinationName { get; set; }

        public string Commodity { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public int OriginTriggerThresholdPercent { get; set; }

        public int DestinationTriggerThresholdPercent { get; set; }
    }

    internal sealed class TabletNpcRouteFinanceEntry
    {
        public TabletNpcRouteFinanceEntry()
        {
            Description = string.Empty;
        }

        public CompanyFinanceFlow Flow { get; set; }

        public CompanyFinanceCategory Category { get; set; }

        public float Amount { get; set; }

        public int AgeMinutes { get; set; }

        public string Description { get; set; }
    }

    internal sealed class TabletNpcRouteDrilldown
    {
        public TabletNpcRouteDrilldown()
        {
            Label = string.Empty;
            TierLabel = string.Empty;
            StatusText = string.Empty;
            AssignedVehicleDisplayName = string.Empty;
            RouteFamily = new TabletNpcRouteFamilySummary();
            RouteLegs = Array.Empty<TabletNpcRouteLegSummary>();
            RecentFinanceEntries = Array.Empty<TabletNpcRouteFinanceEntry>();
        }

        public int ContractId { get; set; }

        public string Label { get; set; }

        public string TierLabel { get; set; }

        public string StatusText { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public int RouteCount { get; set; }

        public int CurrentRouteIndex { get; set; }

        public float Revenue { get; set; }

        public float OperatingCost { get; set; }

        public float NetProfit { get; set; }

        public int CompletedDeliveries { get; set; }

        public float DeliveredTons { get; set; }

        public float AveragePayout { get; set; }

        public float LossRatioPercent { get; set; }

        public TabletNpcRouteFamilySummary RouteFamily { get; set; }

        public IReadOnlyList<TabletNpcRouteLegSummary> RouteLegs { get; set; }

        public IReadOnlyList<TabletNpcRouteFinanceEntry> RecentFinanceEntries { get; set; }
    }

    internal static class NpcRouteProfitabilityFormatter
    {
        public static string BuildContractLabel(NpcLogisticsContract contract)
        {
            var routes = GetContractRoutes(contract);
            if (routes.Count == 0)
            {
                return "NPC Route";
            }

            var activeIndex = ClampRouteIndex(contract != null ? contract.CurrentRouteIndex : 0, routes.Count);
            var activeRoute = routes[activeIndex];
            var originName = ResolveIndustryName(activeRoute.OriginIndustry, contract != null ? contract.OriginIndustry : null, "Origin");
            var destinationName = ResolveIndustryName(activeRoute.DestinationIndustry, contract != null ? contract.DestinationIndustry : null, "Destination");
            var commodity = NormalizeCommodity(activeRoute.Commodity);

            if (routes.Count == 1)
            {
                return string.IsNullOrWhiteSpace(commodity)
                    ? string.Format(CultureInfo.InvariantCulture, "{0} -> {1}", originName, destinationName)
                    : string.Format(CultureInfo.InvariantCulture, "{0} -> {1} ({2})", originName, destinationName, commodity);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} -> {1} | {2}/{3}", originName, destinationName, activeIndex + 1, routes.Count);
        }

        public static string BuildFamilyKey(NpcLogisticsContract contract)
        {
            var routes = GetContractRoutes(contract);
            if (routes.Count == 0)
            {
                return string.Empty;
            }

            var commodityPattern = string.Join(">", routes.Select(route => NormalizeCommodity(route.Commodity)));
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", routes.Count, commodityPattern);
        }

        public static string BuildFamilyLabel(NpcLogisticsContract contract)
        {
            var routes = GetContractRoutes(contract);
            return BuildFamilyLabel(routes.Select(route => NormalizeCommodity(route.Commodity)).ToArray());
        }

        public static string BuildFamilyDetail(TabletNpcRouteFamilySummary summary)
        {
            summary = summary ?? new TabletNpcRouteFamilySummary();
            var contractCount = Math.Max(0, summary.ContractCount);
            var routeCount = Math.Max(0, summary.RouteCount);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} contract{1} | {2} leg{3} | {4} | {5} rigs | Net {6} | {7} deliveries",
                contractCount,
                contractCount == 1 ? string.Empty : "s",
                routeCount,
                routeCount == 1 ? string.Empty : "s",
                string.IsNullOrWhiteSpace(summary.CommodityPattern) ? "Mixed" : summary.CommodityPattern,
                string.IsNullOrWhiteSpace(summary.CargoTypePattern) ? "Mixed" : summary.CargoTypePattern,
                FormatSignedMoney(summary.NetProfit),
                Math.Max(0, summary.CompletedDeliveries));
        }

        public static TabletNpcRouteFamilySummary BuildFamilySummary(IEnumerable<NpcLogisticsContract> contracts)
        {
            var contractList = contracts != null
                ? contracts.Where(contract => contract != null).ToList()
                : new List<NpcLogisticsContract>();
            if (contractList.Count == 0)
            {
                return new TabletNpcRouteFamilySummary();
            }

            var firstContract = contractList[0];
            var firstRoutes = GetContractRoutes(firstContract);
            var revenue = contractList.Sum(contract => Math.Max(0f, contract.TotalProfitEarned));
            var operatingCost = contractList.Sum(contract => Math.Max(0f, contract.ContractCost) + Math.Max(0f, contract.TotalWeeklyWagesPaid));
            var deliveries = contractList.Sum(contract => Math.Max(0, contract.CompletedDeliveries));

            return new TabletNpcRouteFamilySummary
            {
                Key = BuildFamilyKey(firstContract),
                Label = BuildFamilyLabel(firstContract),
                CommodityPattern = BuildCommodityPattern(firstRoutes.Select(route => NormalizeCommodity(route.Commodity)).ToArray()),
                CargoTypePattern = BuildCargoTypePattern(firstRoutes.Select(route => NormalizeCommodity(route.Commodity)).ToArray()),
                ContractCount = contractList.Count,
                RouteCount = firstRoutes.Count,
                Revenue = revenue,
                OperatingCost = operatingCost,
                NetProfit = revenue - operatingCost,
                CompletedDeliveries = deliveries,
                DeliveredTons = contractList.Sum(contract => Math.Max(0f, contract.TotalDeliveredTons)),
                AveragePayout = deliveries > 0 ? Math.Max(0f, revenue / deliveries) : 0f,
                AverageLossRatioPercent = contractList.Count > 0
                    ? contractList.Average(contract => Math.Max(0f, contract.LastJourneyLossRatio * 100f))
                    : 0f,
            };
        }

        public static string BuildLegCaption(TabletNpcRouteLegSummary route)
        {
            route = route ?? new TabletNpcRouteLegSummary();
            return route.IsCurrentRoute
                ? string.Format(CultureInfo.InvariantCulture, "Route {0}/{1} [ACTIVE]", Math.Max(1, route.RouteIndex), Math.Max(1, route.RouteCount))
                : string.Format(CultureInfo.InvariantCulture, "Route {0}/{1}", Math.Max(1, route.RouteIndex), Math.Max(1, route.RouteCount));
        }

        public static string BuildLegDetail(TabletNpcRouteLegSummary route)
        {
            route = route ?? new TabletNpcRouteLegSummary();
            var truckLabel = string.IsNullOrWhiteSpace(route.AssignedVehicleDisplayName)
                ? "Legacy auto"
                : route.AssignedVehicleDisplayName;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} -> {1} | {2} | Trigger {3}% -> {4}% | Truck {5}{6}",
                string.IsNullOrWhiteSpace(route.OriginName) ? "Origin" : route.OriginName,
                string.IsNullOrWhiteSpace(route.DestinationName) ? "Destination" : route.DestinationName,
                string.IsNullOrWhiteSpace(route.Commodity) ? "Cargo" : route.Commodity,
                Math.Max(0, route.OriginTriggerThresholdPercent),
                Math.Max(0, route.DestinationTriggerThresholdPercent),
                truckLabel,
                route.IsCurrentRoute ? " | Active leg" : string.Empty);
        }

        public static string BuildFinanceCaption(TabletNpcRouteFinanceEntry entry)
        {
            entry = entry ?? new TabletNpcRouteFinanceEntry();
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1}",
                BuildFinanceTypeLabel(entry.Category, entry.Flow),
                entry.Flow == CompanyFinanceFlow.Expense
                    ? FormatSignedMoney(-Math.Abs(entry.Amount))
                    : FormatSignedMoney(Math.Abs(entry.Amount)));
        }

        public static string BuildFinanceDetail(TabletNpcRouteFinanceEntry entry)
        {
            entry = entry ?? new TabletNpcRouteFinanceEntry();
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | {1}",
                BuildRelativeAgeLabel(entry.AgeMinutes),
                string.IsNullOrWhiteSpace(entry.Description) ? "No description recorded." : entry.Description);
        }

        public static string BuildRouteStateDetail(TabletNpcRouteDrilldown detail)
        {
            detail = detail ?? new TabletNpcRouteDrilldown();
            var truckLabel = string.IsNullOrWhiteSpace(detail.AssignedVehicleDisplayName)
                ? "Legacy auto"
                : detail.AssignedVehicleDisplayName;
            if (detail.RouteCount <= 1)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | Truck {1} | {2}",
                    string.IsNullOrWhiteSpace(detail.TierLabel) ? "Route" : detail.TierLabel,
                    truckLabel,
                    string.IsNullOrWhiteSpace(detail.StatusText) ? "Preparing route" : detail.StatusText);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | Truck {1} | Active leg {2}/{3} | {4}",
                string.IsNullOrWhiteSpace(detail.TierLabel) ? "Route" : detail.TierLabel,
                truckLabel,
                Math.Max(1, detail.CurrentRouteIndex + 1),
                Math.Max(1, detail.RouteCount),
                string.IsNullOrWhiteSpace(detail.StatusText) ? "Preparing route" : detail.StatusText);
        }

        private static IReadOnlyList<NpcLogisticsRouteDefinition> GetContractRoutes(NpcLogisticsContract contract)
        {
            if (contract != null && contract.Routes != null && contract.Routes.Count > 0)
            {
                return contract.Routes;
            }

            if (contract == null)
            {
                return Array.Empty<NpcLogisticsRouteDefinition>();
            }

            return new[]
            {
                new NpcLogisticsRouteDefinition
                {
                    OriginIndustry = contract.OriginIndustry,
                    DestinationIndustry = contract.DestinationIndustry,
                    Commodity = contract.Commodity,
                    AssignedVehicleDisplayName = contract.AssignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = contract.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = contract.DestinationTriggerThresholdPercent,
                },
            };
        }

        private static string BuildFamilyLabel(IReadOnlyList<string> commodities)
        {
            commodities = commodities != null
                ? commodities.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
                : Array.Empty<string>();
            if (commodities.Count == 0)
            {
                return "Mixed chain";
            }

            var routeCount = commodities.Count;
            var distinctCommodities = commodities.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (routeCount == 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} lane", commodities[0]);
            }

            if (distinctCommodities.Length == 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} {1}-leg chain", distinctCommodities[0], routeCount);
            }

            return string.Format(CultureInfo.InvariantCulture, "Mixed {0}-leg chain", routeCount);
        }

        private static string BuildCommodityPattern(IReadOnlyList<string> commodities)
        {
            if (commodities == null || commodities.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" -> ", commodities.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildCargoTypePattern(IReadOnlyList<string> commodities)
        {
            if (commodities == null || commodities.Count == 0)
            {
                return string.Empty;
            }

            var labels = new List<string>();
            for (int i = 0; i < commodities.Count; i++)
            {
                var commodity = commodities[i];
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                var cargoTypeLabel = CommodityCatalog.GetCargoTypeForCommodity(commodity).ToDisplayName();
                if (labels.Contains(cargoTypeLabel, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                labels.Add(cargoTypeLabel);
            }

            return string.Join(" + ", labels);
        }

        private static string ResolveIndustryName(Industry routeIndustry, Industry fallbackIndustry, string fallbackLabel)
        {
            var industry = routeIndustry ?? fallbackIndustry;
            return industry != null && !string.IsNullOrWhiteSpace(industry.Name)
                ? industry.Name
                : fallbackLabel;
        }

        private static string NormalizeCommodity(string commodity)
        {
            return CommodityCatalog.Normalize(commodity);
        }

        private static int ClampRouteIndex(int routeIndex, int routeCount)
        {
            if (routeCount <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(routeCount - 1, routeIndex));
        }

        private static string BuildFinanceTypeLabel(CompanyFinanceCategory category, CompanyFinanceFlow flow)
        {
            switch (category)
            {
                case CompanyFinanceCategory.NpcDelivery:
                    return "Delivery";
                case CompanyFinanceCategory.NpcWages:
                    return "Payroll";
                default:
                    return flow == CompanyFinanceFlow.Expense ? "Expense" : "Income";
            }
        }

        private static string BuildRelativeAgeLabel(int ageMinutes)
        {
            var clamped = Math.Max(0, ageMinutes);
            var days = clamped / (24 * 60);
            if (days > 0)
            {
                var remainingHours = (clamped % (24 * 60)) / 60;
                return remainingHours > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0}d {1}h ago", days, remainingHours)
                    : string.Format(CultureInfo.InvariantCulture, "{0}d ago", days);
            }

            var hours = clamped / 60;
            if (hours > 0)
            {
                var remainingMinutes = clamped % 60;
                return remainingMinutes > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0}h {1}m ago", hours, remainingMinutes)
                    : string.Format(CultureInfo.InvariantCulture, "{0}h ago", hours);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}m ago", clamped);
        }

        private static string FormatSignedMoney(float amount)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}",
                amount >= 0f ? "+" : "-",
                ModFormatting.FormatMoney(Math.Abs(amount)));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class TabletUiHelpers
    {
        private static readonly Color InfoIdle = Color.FromArgb(170, 46, 60, 76);
        private static readonly Color InfoActive = Color.FromArgb(205, 92, 124, 152);
        private static readonly Color ActionIdle = Color.FromArgb(170, 50, 64, 50);
        private static readonly Color ActionActive = Color.FromArgb(205, 96, 132, 102);
        private static readonly Color WarningIdle = Color.FromArgb(178, 78, 54, 42);
        private static readonly Color WarningActive = Color.FromArgb(208, 136, 96, 84);
        private static readonly Color NavigationIdle = Color.FromArgb(170, 56, 45, 61);
        private static readonly Color NavigationActive = Color.FromArgb(210, 132, 86, 158);
        private static readonly Color SelectorIdle = Color.FromArgb(178, 54, 66, 96);
        private static readonly Color SelectorActive = Color.FromArgb(222, 122, 166, 228);

        public static MenuItem CreateInfoItem(string caption, string detail, float? progress = null)
        {
            return CreateActionItem(caption, detail, null, InfoIdle, InfoActive, progress);
        }

        public static MenuItem CreateBannerItem(string caption, string detail)
        {
            return CreateActionItem(caption, detail, null, WarningIdle, WarningActive, null);
        }

        public static MenuItem CreateActionItem(
            string caption,
            string detail,
            Action onActivate,
            Color? idle = null,
            Color? active = null,
            float? progress = null,
            string iconLabel = null)
        {
            return new MenuItem
            {
                CaptionFactory = () => caption ?? string.Empty,
                DetailFactory = () => detail ?? string.Empty,
                IconLabelFactory = string.IsNullOrWhiteSpace(iconLabel) ? null : (Func<string>)(() => iconLabel),
                OnActivate = onActivate,
                ProgressRatioFactory = progress.HasValue ? (Func<float?>)(() => progress.Value) : null,
                IdleBackgroundColor = idle ?? ActionIdle,
                SelectedBackgroundColor = active ?? ActionActive,
            };
        }

        public static MenuItem CreateNavigationItem(string caption, string detail, Action onActivate, string iconLabel = null)
        {
            return CreateActionItem(caption, detail, onActivate, NavigationIdle, NavigationActive, null, iconLabel);
        }

        public static MenuItem CreateSelectorItem(
            Func<string> captionFactory,
            Func<string> detailFactory,
            Action onLeft,
            Action onRight,
            Action onActivate = null,
            string iconLabel = null)
        {
            return new MenuItem
            {
                CaptionFactory = () => captionFactory != null ? captionFactory() ?? string.Empty : string.Empty,
                DetailFactory = () => detailFactory != null ? detailFactory() ?? string.Empty : string.Empty,
                IconLabelFactory = string.IsNullOrWhiteSpace(iconLabel) ? null : (Func<string>)(() => iconLabel),
                OnLeft = onLeft,
                OnRight = onRight,
                OnActivate = onActivate,
                IdleBackgroundColor = SelectorIdle,
                SelectedBackgroundColor = SelectorActive,
            };
        }

        public static MenuItem CreateGraphTimeframeSelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => string.Format("Timeframe: {0}", context != null ? context.StateStore.SelectedGraphTimeframe.ToDisplayLabel() : TabletGraphTimeframe.ThirtyMinutes.ToDisplayLabel()),
                () => detail ?? "Left/right changes the graph window. Enter advances.",
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleGraphTimeframe(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleGraphTimeframe(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleGraphTimeframe(1);
                    context.Refresh();
                },
                "TIM");
        }

        public static MenuItem CreateCommoditySelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => string.Format("Trend Resource: {0}", context != null ? context.StateStore.SelectedTrendCommodity : "None"),
                () => detail ?? "Left/right changes the graph resource. Enter advances.",
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleSelectedTrendCommodity(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleSelectedTrendCommodity(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleSelectedTrendCommodity(1);
                    context.Refresh();
                },
                "RES");
        }

        public static string BuildIndustryAccessDetail(TabletLocationSummary summary, Industry industry)
        {
            if (summary == null || industry == null)
            {
                return "Site state unavailable.";
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                return summary.RequiresIndustryPurchase
                    ? string.Format("Storage site | Buy {0}", ModFormatting.FormatMoney(industry.IndustryPrice))
                    : "Storage site | Purchase cleared";
            }

            var ownership = summary.RequiresIndustryPurchase
                ? string.Format("Buy {0}", ModFormatting.FormatMoney(industry.IndustryPrice))
                : "Operations unlocked";
            var permit = !summary.RequiresContractorPermit
                ? "Permit open"
                : (summary.HasContractorPermitForGameplay
                    ? "Permit cleared"
                    : string.Format("Permit {0}", ModFormatting.FormatMoney(industry.IndustryLicencePrice)));
            return string.Format("{0} | {1}", ownership, permit);
        }

        public static string BuildCompactModuleSummary(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            return string.Format(
                "P{0} | In {1} | Out {2} | Om {3}",
                industry.ProductionModuleLevel,
                industry.InputStorageModuleLevel,
                industry.OutputStorageModuleLevel,
                industry.OmegaStorageModuleLevel);
        }

        public static string BuildBalanceChrome(TabletStateSnapshot snapshot)
        {
            var balance = snapshot != null ? snapshot.Balance : 0f;
            return string.Format("Balance {0}", ModFormatting.FormatMoney(balance));
        }

        public static TabletShellPage BuildLegacyIndustryStatisticsPage(
            TabletShellContext context,
            TabletStateSnapshot snapshot,
            Industry industry,
            string footerText,
            Action onSelect = null)
        {
            if (context == null || industry == null)
            {
                return new TabletShellPage();
            }

            var statistics = context.StateStore.GetIndustryStatistics(industry);
            var scrollSlotCount = Math.Max(1, IndustryStatisticsPanelRenderer.GetScrollSlotCount(statistics));
            var scrollItems = Enumerable.Range(0, scrollSlotCount)
                .Select(_ => new MenuItem())
                .ToArray();

            return new TabletShellPage
            {
                Title = IndustryStatisticsPanelRenderer.BuildMenuTitle(industry.Name),
                Subtitle = IndustryStatisticsPanelRenderer.BuildOperationsSubtitle(industry),
                HeaderRightText = string.Empty,
                FooterText = footerText,
                WidthScale = 0.96f,
                MaxVisibleItems = scrollSlotCount,
                ContentRenderer = panel => IndustryStatisticsPanelRenderer.DrawTabletBody(
                    industry,
                    context.StateStore.GetIndustryStatistics(industry),
                    panel != null ? panel.SelectedIndex : 0,
                    panel),
                SelectAction = onSelect ?? context.GoBack,
                Items = scrollItems,
            };
        }

        public static string SummarizeCommodities(IReadOnlyList<string> commodities, int maxVisible = 4)
        {
            if (commodities == null || commodities.Count == 0)
            {
                return "None";
            }

            var entries = commodities
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Take(Math.Max(1, maxVisible))
                .ToList();
            if (entries.Count == 0)
            {
                return "None";
            }

            var summary = string.Join(", ", entries);
            var remaining = commodities.Count - entries.Count;
            if (remaining > 0)
            {
                summary += string.Format(" +{0}", remaining);
            }

            return summary;
        }

        public static string BuildCargoSummary(TabletStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasCargoVehicle)
            {
                return "No cargo vehicle linked to the tablet.";
            }

            var cargoSummary = string.Format(
                "{0} | {1} | {2:0.0}/{3:0.0}t",
                snapshot.CargoVehicleName,
                snapshot.CargoIsEmpty ? "Empty" : snapshot.CargoCommodity,
                snapshot.CargoWeightTons,
                snapshot.CargoCapacityTons);

            if (!snapshot.HasPoweredVehicle || snapshot.FuelCapacityLiters <= 0.001f)
            {
                return cargoSummary;
            }

            var fuelSummary = snapshot.FuelVehicleMatchesCargoVehicle
                ? string.Format("Fuel {0:0}/{1:0}L", snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters)
                : string.Format("Fuel {0} {1:0}/{2:0}L", snapshot.PoweredVehicleName, snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters);
            return string.Format("{0} | {1}", cargoSummary, fuelSummary);
        }

        public static string BuildMarketSummary(TabletStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.MarketHighlights == null || snapshot.MarketHighlights.Count == 0)
            {
                return "No market highlights cached yet.";
            }

            return string.Join(
                " | ",
                snapshot.MarketHighlights.Select(highlight => string.Format("{0} ${1:0}/t", highlight.Commodity, highlight.UnitPrice)).ToArray());
        }

        public static string BuildLocationCaption(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            return string.Format("{0} {1}", summary.Name, summary.OwnershipTag);
        }

        public static string BuildLocationOverviewDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(summary.OverviewDetail))
            {
                return summary.PermitTag ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(summary.PermitTag))
            {
                return summary.OverviewDetail;
            }

            return string.Format("{0} | {1}", summary.PermitTag, summary.OverviewDetail);
        }

        public static string BuildPermitCaption(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            return string.Format("{0} {1}", summary.Name, summary.PermitTag);
        }

        public static TabletLocationSummary FindSummary(TabletShellContext context, Industry industry)
        {
            if (context == null || industry == null || context.Snapshot == null)
            {
                return null;
            }

            return context.Snapshot.IndustrySummaries
                .Concat(context.Snapshot.WarehouseSummaries)
                .Concat(context.Snapshot.StoreSummaries)
                .Concat(context.Snapshot.GasStationSummaries)
                .FirstOrDefault(summary => summary != null && summary.Industry != null && string.Equals(summary.Industry.Id, industry.Id, StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildIndustryStatusDetail(TabletLocationSummary summary, Industry industry)
        {
            if (summary == null || industry == null)
            {
                return "Industry state unavailable.";
            }

            if (!string.IsNullOrWhiteSpace(summary.ProductionWarning))
            {
                return string.Format("~r~{0}~s~", summary.ProductionWarning);
            }

            return string.Empty;
        }

        public static void AppendIndustryDetailItems(List<MenuItem> items, Industry industry, IndustryStatisticsSnapshot statistics)
        {
            if (items == null || industry == null)
            {
                return;
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                AppendWarehouseDetailItems(items, null, industry, statistics);
                return;
            }

            items.Add(CreateInfoItem("Conversion", industry.GetPrimaryConversionDescription()));

            var stockpile = statistics != null ? statistics.Stockpile : 0f;
            var totalCapacity = statistics != null ? statistics.TotalCapacity : 1f;
            var stockRatio = statistics != null ? statistics.StockRatio : 0f;
            var utilizationRatio = statistics != null ? statistics.UtilizationRatio : 0f;

            items.Add(CreateInfoItem(
                string.Format("Stockpile {0:0.0}/{1:0.0}t", stockpile, totalCapacity),
                "Combined input and output storage across the site.",
                stockRatio));
            items.Add(CreateInfoItem(
                string.Format("Production {0:0.0} t/h", industry.CurrentOutputPerHourTons),
                string.Format("Utilization {0:0}% | Omega {1:0.0}/{2:0.0}t", industry.LastUtilizationPercent, industry.OmegaStorage, industry.OmegaCapacityTons),
                utilizationRatio));
            items.Add(CreateInfoItem(
                "Module Levels",
                string.Format(
                    "Prod Lv.{0} | In Lv.{1} | Out Lv.{2} | Omega Lv.{3}",
                    industry.ProductionModuleLevel,
                    industry.InputStorageModuleLevel,
                    industry.OutputStorageModuleLevel,
                    industry.OmegaStorageModuleLevel)));

            AppendIndustryCommodityItems(items, statistics);
        }

        public static void AppendIndustryStatisticsItems(List<MenuItem> items, TabletLocationSummary summary, Industry industry, IndustryStatisticsSnapshot statistics)
        {
            if (items == null || industry == null)
            {
                return;
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                AppendWarehouseDetailItems(items, summary, industry, statistics);
                return;
            }

            var stockpile = statistics != null ? statistics.Stockpile : 0f;
            var totalCapacity = statistics != null ? statistics.TotalCapacity : 1f;
            var stockRatio = statistics != null ? statistics.StockRatio : 0f;
            var legendDetail = "IN = input storage | OUT = output storage";
            if (summary != null && !string.IsNullOrWhiteSpace(summary.ProductionWarning))
            {
                legendDetail = string.Format("{0} | {1}", summary.ProductionWarning, legendDetail);
            }

            items.Add(CreateInfoItem(
                string.Format("Total Stockpile {0:0.0}/{1:0.0}t", stockpile, totalCapacity),
                "Combined input and output storage across the site.",
                stockRatio));
            items.Add(CreateInfoItem(
                string.Format("Utilization {0:0}% | Output {1:0.0} t/h", industry.LastUtilizationPercent, industry.CurrentOutputPerHourTons),
                legendDetail));

            AppendIndustryCommodityItems(items, statistics);
        }

        private static void AppendIndustryCommodityItems(List<MenuItem> items, IndustryStatisticsSnapshot statistics)
        {
            if (items == null)
            {
                return;
            }

            if (statistics == null || statistics.Entries == null || statistics.Entries.Count == 0)
            {
                items.Add(CreateInfoItem("Commodities", "No input or output commodities configured for this site."));
                return;
            }

            for (int i = 0; i < statistics.Entries.Count; i++)
            {
                var entry = statistics.Entries[i];
                var caption = string.Format("{0} {1}", entry.IsInput ? "IN" : "OUT", entry.Commodity);
                var detail = string.Format("{0:0.0}/{1:0.0}t", entry.Stock, entry.Capacity);
                items.Add(CreateInfoItem(caption, detail, entry.Ratio));
            }
        }

        private static void AppendWarehouseDetailItems(List<MenuItem> items, TabletLocationSummary summary, Industry industry, IndustryStatisticsSnapshot statistics)
        {
            if (items == null || industry == null)
            {
                return;
            }

            var totalStorage = statistics != null ? statistics.Stockpile : industry.GetInputStockTotal() + industry.GetOutputStockTotal();
            var totalCapacity = statistics != null ? statistics.TotalCapacity : Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
            var fillRatio = statistics != null ? statistics.StockRatio : ModMath.Clamp01(totalStorage / Math.Max(1f, totalCapacity));
            items.Add(CreateInfoItem("Access", BuildIndustryAccessDetail(summary ?? new TabletLocationSummary { RequiresIndustryPurchase = industry.RequiresPurchase }, industry)));
            items.Add(CreateInfoItem(
                string.Format("Storage {0:0.0}/{1:0.0}t", totalStorage, totalCapacity),
                "Warehouse inventory capacity.",
                fillRatio));
            items.Add(CreateInfoItem(
                "Accepted Resources",
                SummarizeCommodities(industry.SortedInputs, 6)));
        }

        public static string GetUpgradeTitle(IndustryUpgradeModule module)
        {
            if (module == IndustryUpgradeModule.Production)
            {
                return "Production Module";
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                return "Input Storage Module";
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return "Output Storage Module";
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                return "Omega Tank Module";
            }

            return "Module";
        }

        public static string BuildUpgradeDetail(Industry industry, IndustryUpgradeModule module, float balance)
        {
            if (industry == null)
            {
                return "No industry selected.";
            }

            var level = industry.GetUpgradeLevel(module);
            var cost = industry.GetUpgradeCost(module);
            if (cost <= 0f)
            {
                return "Unavailable for this industry.";
            }

            if (balance < cost)
            {
                return string.Format("Lv.{0} -> {1} | Need {2} more", level, ModFormatting.FormatMoney(cost), ModFormatting.FormatMoney(cost - balance));
            }

            return string.Format("Lv.{0} -> {1} | Press Enter to purchase", level, ModFormatting.FormatMoney(cost));
        }
    }

    internal sealed class HomeTabletApp : ITabletApp
    {
        private readonly Action _openCompanyMap;
        private readonly Action _openDistrictView;
        private readonly Action _openDepotView;

        public HomeTabletApp(Action openCompanyMap, Action openDistrictView, Action openDepotView)
        {
            _openCompanyMap = openCompanyMap;
            _openDistrictView = openDistrictView;
            _openDepotView = openDepotView;
        }

        public string AppId
        {
            get { return TabletAppIds.Home; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            var totalTrackedSites = snapshot.IndustrySummaries.Count + snapshot.WarehouseSummaries.Count + snapshot.StoreSummaries.Count + snapshot.GasStationSummaries.Count;
            var warehouseCount = snapshot.WarehouseSummaries.Count;
            var industryCount = snapshot.IndustrySummaries.Count;
            var permitSiteCount = snapshot.IndustrySummaries.Count(summary => summary != null && summary.Industry != null && summary.Industry.RequiresContractorPermit);
            var unlockedPermitCount = snapshot.IndustrySummaries.Count(summary => summary != null && summary.HasContractorPermitForGameplay);
            var statusLine = snapshot.TransferInProgress
                ? "Transfer active"
                : !string.IsNullOrWhiteSpace(snapshot.StatusBanner)
                    ? snapshot.StatusBanner
                    : !string.IsNullOrWhiteSpace(snapshot.NearestIndustryProductionWarning)
                        ? snapshot.NearestIndustryProductionWarning
                        : snapshot.HasCargoVehicle
                            ? TabletUiHelpers.BuildCargoSummary(snapshot)
                            : "No cargo vehicle linked.";
            var operationsHeadline = snapshot.HasNearestIndustry
                ? string.Format("{0} | {1:0.0}m", snapshot.NearestIndustryName, snapshot.NearestIndustryDistance)
                : "No nearby site";
            var operationsDetail = string.Format(
                "{0}\n{1}",
                operationsHeadline,
                statusLine);
            var marketDetail = snapshot.MarketHighlights != null && snapshot.MarketHighlights.Count > 0
                ? string.Format(
                    "{0} ${1:0}/t\n{2} market highlights cached",
                    snapshot.MarketHighlights[0].Commodity,
                    snapshot.MarketHighlights[0].UnitPrice,
                    snapshot.MarketHighlights.Count)
                : "No market highlights cached yet.\nOpen Network to refresh industry pricing.";
            var siteDetail = snapshot.HasNearestIndustry
                ? string.Format(
                    "{0}\n{1:0.0} t/h | {2:0}% utilization",
                    snapshot.NearestIndustryName,
                    snapshot.NearestIndustryProductionRateTonsPerHour,
                    snapshot.NearestIndustryUtilizationPercent)
                : "Browse tracked industries, stores, and stations across the region.";
            var permitDetail = permitSiteCount > 0
                ? string.Format("{0}/{1} transport permits unlocked", unlockedPermitCount, permitSiteCount)
                : "No contractor permits configured.";
            var siteAction = snapshot.HasNearestIndustry
                ? (snapshot.CanInteractWithNearestIndustry
                    ? (Action)(() => context.Push(TabletAppIds.Industry, "main", snapshot.NearestIndustry))
                    : (Action)(() => context.Push(TabletAppIds.Network, "detail", snapshot.NearestIndustry)))
                : (Action)(() => context.Push(TabletAppIds.Network, "industries"));

            items.Add(TabletUiHelpers.CreateActionItem(
                "Company",
                string.Format("{0}\n{1} sites | {2} NPC routes", TabletUiHelpers.BuildBalanceChrome(snapshot), totalTrackedSites, snapshot.ActiveNpcRouteCount),
                () => context.Push(TabletAppIds.Network, "root"),
                Color.FromArgb(176, 30, 46, 74),
                Color.FromArgb(220, 90, 142, 204),
                null,
                "HQ"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Operations",
                operationsDetail,
                siteAction,
                Color.FromArgb(176, 42, 54, 80),
                Color.FromArgb(220, 102, 132, 188),
                null,
                "LIVE"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Context",
                "Cargo vehicle telemetry\nNearby industry context.",
                () => context.Push(TabletAppIds.Context, "root"),
                Color.FromArgb(184, 36, 60, 94),
                Color.FromArgb(226, 102, 162, 236),
                null,
                "CTX"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Industries",
                string.Format("{0} tracked production sites", industryCount),
                () => context.Push(TabletAppIds.Network, "industries"),
                Color.FromArgb(184, 46, 82, 72),
                Color.FromArgb(226, 108, 186, 160),
                null,
                "IND"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Permits",
                permitDetail,
                () => context.Push(TabletAppIds.Network, "permits"),
                Color.FromArgb(188, 92, 64, 34),
                Color.FromArgb(228, 214, 164, 92),
                null,
                "PER"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Stores",
                string.Format("{0} retail delivery locations", snapshot.StoreSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stores"),
                Color.FromArgb(188, 62, 72, 92),
                Color.FromArgb(228, 132, 166, 208),
                null,
                "STR"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Stations",
                string.Format("{0} fuel service stops", snapshot.GasStationSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stations"),
                Color.FromArgb(188, 42, 86, 92),
                Color.FromArgb(228, 92, 186, 194),
                null,
                "GAS"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Market",
                marketDetail,
                () => context.Push(TabletAppIds.Network, "market"),
                Color.FromArgb(188, 74, 48, 86),
                Color.FromArgb(228, 174, 120, 206),
                null,
                "MKT"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Analytics",
                "Profit, market, site, district, and NPC trend surfaces.",
                () => context.Push(TabletAppIds.Analytics, "root"),
                Color.FromArgb(186, 62, 72, 108),
                Color.FromArgb(228, 144, 170, 234),
                null,
                "ANA"));
            items.Add(TabletUiHelpers.CreateActionItem(
                snapshot.HasNearestIndustry ? "Site" : "Sites",
                siteDetail,
                siteAction,
                Color.FromArgb(188, 54, 94, 74),
                Color.FromArgb(228, 112, 196, 152),
                null,
                "SITE"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Warehouse",
                warehouseCount > 0
                    ? string.Format("{0} storage sites | {1} support-enabled", warehouseCount, snapshot.SecuredSupportSiteCount)
                    : "No warehouse sites are configured.",
                () => context.Push(TabletAppIds.Network, "warehouses"),
                Color.FromArgb(188, 58, 84, 110),
                Color.FromArgb(228, 126, 178, 232),
                null,
                "WH"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Footprint",
                string.Format(
                    "{0} districts | {1} corridors\n{2} support sites secured",
                    snapshot.ControlledDistrictCount,
                    snapshot.ActiveCorridorCount,
                    snapshot.SecuredSupportSiteCount),
                _openCompanyMap,
                Color.FromArgb(184, 48, 70, 96),
                Color.FromArgb(224, 118, 168, 220),
                null,
                "FPT"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "District View",
                "Inspect district influence, reputation, and coverage.",
                _openDistrictView,
                Color.FromArgb(184, 64, 82, 78),
                Color.FromArgb(224, 134, 184, 170),
                null,
                "DST"));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Depot / Yard",
                "Lease or buy support sites and grow local crews.",
                _openDepotView,
                Color.FromArgb(184, 78, 62, 78),
                Color.FromArgb(224, 194, 138, 186),
                null,
                "DPT"));
            items.Add(TabletUiHelpers.CreateNavigationItem(
                "Close",
                "Return to gameplay.",
                context.Close,
                "EXIT"));

            return new TabletShellPage
            {
                Title = "LSOL OS",
                Subtitle = "Company hub",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "F6 Context | F8 Company Hub | Arrow Keys Navigate | Enter Select | Backspace/Esc Close",
                WidthScale = 0.96f,
                MaxVisibleItems = 0,
                Layout = SimpleMenuTabletLayout.Dashboard,
                DashboardSidebarCount = 2,
                DashboardTileColumns = 8,
                BottomPanelHeight = 146f,
                BottomPanelRenderer = panel => TabletChartRenderer.DrawHistoryPanel(
                    panel,
                    "Company Profit Over Time",
                    string.Format("{0} window captured during gameplay.", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                    context.StateStore.GetProfitHistory(context.StateStore.SelectedGraphTimeframe),
                    Color.FromArgb(214, 118, 200, 176),
                    value => ModFormatting.FormatMoney(value)),
                Items = items,
            };
        }
    }

    internal sealed class ContextTabletApp : ITabletApp
    {
        public string AppId
        {
            get { return TabletAppIds.Context; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Status", snapshot.StatusBanner));
            }

            items.Add(TabletUiHelpers.CreateInfoItem(
                string.Format("Vehicle: {0}", snapshot.HasCargoVehicle ? snapshot.CargoVehicleName : "None nearby"),
                snapshot.HasCargoVehicle
                    ? string.Format("Cargo type {0}", snapshot.CargoType.ToDisplayName())
                    : "Move near a cargo truck or trailer to inspect active cargo state."));
            items.Add(TabletUiHelpers.CreateInfoItem(
                string.Format("Cargo: {0}", snapshot.HasCargoVehicle ? snapshot.CargoCommodity : "Unavailable"),
                snapshot.HasCargoVehicle
                    ? string.Format("Weight {0:0.0}/{1:0.0}t", snapshot.CargoWeightTons, snapshot.CargoCapacityTons)
                    : "No cargo vehicle is currently resolved by FleetManager.",
                snapshot.CargoCapacityRatio));
            items.Add(TabletUiHelpers.CreateInfoItem(
                snapshot.HasPoweredVehicle
                    ? string.Format("Fuel: {0:0}/{1:0}L", snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters)
                    : "Fuel: unavailable",
                snapshot.HasPoweredVehicle
                    ? (snapshot.FuelVehicleMatchesCargoVehicle
                        ? (snapshot.FuelIsEmpty
                            ? "Powered vehicle tank is empty. Refuel at a petrol station."
                            : "Fuel telemetry for the active powered cargo vehicle.")
                        : string.Format("Powered vehicle {0} | Fuel belongs to the tractor, not the trailer.", snapshot.PoweredVehicleName))
                    : "Move near a powered company cargo vehicle to inspect truck fuel telemetry.",
                snapshot.FuelRatio));

            if (snapshot.HasNearestIndustry)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    string.Format("Nearest Site: {0}", snapshot.NearestIndustryName),
                    string.Format("Inputs {0} | Outputs {1}", TabletUiHelpers.SummarizeCommodities(snapshot.NearestIndustryInputs), TabletUiHelpers.SummarizeCommodities(snapshot.NearestIndustryOutputs))));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    string.Format("Production: {0:0.0} t/h", snapshot.NearestIndustryProductionRateTonsPerHour),
                    string.Format("Utilization {0:0}%", snapshot.NearestIndustryUtilizationPercent),
                    ModMath.Clamp01(snapshot.NearestIndustryUtilizationPercent / 100f)));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    string.Format("Omega: {0:0.0}/{1:0.0}t", snapshot.NearestIndustryOmegaStorageTons, snapshot.NearestIndustryOmegaCapacityTons),
                    string.IsNullOrWhiteSpace(snapshot.NearestIndustryProductionWarning)
                        ? string.Format("{0:0.0}m away | Ownership {1} | Permit {2}", snapshot.NearestIndustryDistance, snapshot.NearestIndustryOwnedForGameplay ? "Open" : "Locked", snapshot.NearestIndustryHasPermitForGameplay ? "Open" : "Locked")
                        : snapshot.NearestIndustryProductionWarning,
                    snapshot.NearestIndustryOmegaCapacityTons <= 0.001f
                        ? 0f
                        : ModMath.Clamp01(snapshot.NearestIndustryOmegaStorageTons / snapshot.NearestIndustryOmegaCapacityTons)));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem("Nearest Site", "No industry telemetry is currently cached in range."));
            }

            if (snapshot.HasNearestIndustry)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    snapshot.CanInteractWithNearestIndustry ? "Open Industry Operations" : "Review Site Detail",
                    snapshot.CanInteractWithNearestIndustry
                        ? "Jump straight into the nearest actionable industry app."
                        : "Open the network detail page for the nearest tracked site.",
                    snapshot.CanInteractWithNearestIndustry
                        ? (Action)(() => context.Push(TabletAppIds.Industry, "main", snapshot.NearestIndustry))
                        : (Action)(() => context.Push(TabletAppIds.Network, "detail", snapshot.NearestIndustry))));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub or previous tablet page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Context",
                Subtitle = "Vehicle cargo, fuel, and nearest industry telemetry",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Enter Select | Backspace/Esc Back",
                WidthScale = 0.98f,
                MaxVisibleItems = 7,
                Items = items,
            };
        }
    }

    internal sealed class NetworkTabletApp : ITabletApp
    {
        private readonly float _interactionDistance;
        private readonly Func<Industry, string> _purchasePermit;

        public NetworkTabletApp(float interactionDistance, Func<Industry, string> purchasePermit)
        {
            _interactionDistance = interactionDistance;
            _purchasePermit = purchasePermit;
        }

        public string AppId
        {
            get { return TabletAppIds.Network; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "industries":
                    return BuildIndustryListPage(context);
                case "stores":
                    return BuildLocationListPage(context, "Stores", "Retail demand, storage, and detail pages", context.Snapshot.StoreSummaries, true, false);
                case "stations":
                    return BuildLocationListPage(context, "Gas Stations", "Fuel storage coverage across service stations", context.Snapshot.GasStationSummaries, false, false);
                case "market":
                    return BuildMarketPage(context);
                case "prices":
                    return BuildResourcePricesPage(context);
                case "warehouses":
                    return BuildWarehousePage(context);
                case "permits":
                    return BuildPermitPage(context);
                case "permit-confirm":
                    return BuildPermitConfirmPage(context, route != null ? route.Payload as Industry : null);
                case "detail":
                    return BuildDetailPage(context, route != null ? route.Payload as Industry : null);
                default:
                    return BuildRootPage(context);
            }
        }

        private static TabletShellPage BuildRootPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Status", snapshot.StatusBanner));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                "Industries",
                string.Format("{0} tracked industry sites with permits, warnings, and detail pages.", snapshot.IndustrySummaries.Count),
                () => context.Push(TabletAppIds.Network, "industries")));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Contractor Permits",
                string.Format(
                    "{0} industries require contractor access.",
                    snapshot.IndustrySummaries.Count(summary => summary != null && summary.Industry != null && summary.Industry.RequiresContractorPermit)),
                () => context.Push(TabletAppIds.Network, "permits")));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Stores",
                string.Format("{0} retail delivery locations.", snapshot.StoreSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stores")));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Gas Stations",
                string.Format("{0} fuel service stations.", snapshot.GasStationSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stations")));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Warehouses",
                string.Format(
                    "{0} storage sites accept commodity-specific inventory.",
                    snapshot.WarehouseSummaries.Count),
                () => context.Push(TabletAppIds.Network, "warehouses")));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Market",
                TabletUiHelpers.BuildMarketSummary(snapshot),
                () => context.Push(TabletAppIds.Network, "market")));
            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub or previous tablet page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Network",
                Subtitle = "Directory, permits, stores, stations, and price board",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.84f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildWarehousePage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var warehouseSummaries = snapshot.WarehouseSummaries
                .OrderBy(summary => summary.Name)
                .ToList();
            var items = new List<MenuItem>();

            for (int i = 0; i < warehouseSummaries.Count; i++)
            {
                var summary = warehouseSummaries[i];
                var acceptedResources = TabletUiHelpers.SummarizeCommodities(summary.Industry.SortedInputs, 4);
                Industry warehouse = summary.Industry;
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildLocationCaption(summary),
                    string.Format("Accepts {0} | {1}", acceptedResources, TabletUiHelpers.BuildLocationOverviewDetail(summary)),
                    () => context.Push(TabletAppIds.Network, "detail", warehouse),
                    iconLabel: "WH"));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No warehouses available", "No warehouse or storage sites are currently configured."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous tablet page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Warehouses",
                Subtitle = "Storage sites and accepted resources",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 5,
                Items = items,
            };
        }

        private static TabletShellPage BuildMarketPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var selectedCommodity = context.StateStore.SelectedTrendCommodity;
            var selectedPrice = snapshot.MarketPrices != null
                ? snapshot.MarketPrices.FirstOrDefault(price => price != null && string.Equals(price.Commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase))
                : null;
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the market graph window."),
                TabletUiHelpers.CreateCommoditySelectorItem(context, "Left/right changes the market graph resource. Enter advances."),
                TabletUiHelpers.CreateInfoItem(
                    "Market Highlights",
                    string.Format("Scarcity x{0:0.00} | {1}", snapshot.MarketMultiplier, TabletUiHelpers.BuildMarketSummary(snapshot)))
            };

            if (snapshot.MarketHighlights != null)
            {
                for (int i = 0; i < snapshot.MarketHighlights.Count; i++)
                {
                    var highlight = snapshot.MarketHighlights[i];
                    var commodity = highlight.Commodity;
                    var isSelectedCommodity = string.Equals(commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase);
                    items.Add(TabletUiHelpers.CreateActionItem(
                        isSelectedCommodity ? string.Format("{0} ~g~[TREND]~s~", commodity) : commodity,
                        string.Format("${0:0}/t | {1} | Press Enter to chart this resource.", highlight.UnitPrice, highlight.Reason),
                        () =>
                        {
                            context.StateStore.SetSelectedTrendCommodity(commodity);
                            context.Refresh();
                        }));
                }
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                "Resources Price",
                "List every known resource and its current unit price.",
                () => context.Push(TabletAppIds.Network, "prices"),
                iconLabel: "$"));
            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous tablet page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Market",
                Subtitle = "Highlights and current resource prices",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Selectors | Enter Select | Backspace/Esc Back",
                WidthScale = 0.86f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 132f,
                BottomPanelRenderer = panel =>
                {
                    if (selectedPrice == null)
                    {
                        TabletChartRenderer.DrawMessagePanel(
                            panel,
                            "Commodity Trend",
                            "No commodity is currently selected for the graph.",
                            "Open the resource board to pick from the full market list.");
                        return;
                    }

                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Price Trend", selectedPrice.Commodity),
                        string.Format(
                            "{0} cargo | Window {1} | Current ${2:0}/t",
                            selectedPrice.CargoType.ToDisplayName(),
                            context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                            selectedPrice.UnitPrice),
                        context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 214, 168, 94),
                        value => string.Format("${0:0}/t", value));
                },
                Items = items,
            };
        }

        private static TabletShellPage BuildResourcePricesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var prices = snapshot.MarketPrices != null
                ? snapshot.MarketPrices.Where(price => price != null).OrderBy(price => price.Commodity, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<TabletMarketResourcePrice>();
            var selectedCommodity = context.StateStore.SelectedTrendCommodity;
            var selectedPrice = prices.FirstOrDefault(price => string.Equals(price.Commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase));
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the resource price graph window."),
                TabletUiHelpers.CreateCommoditySelectorItem(context, "Left/right changes the graphed resource. Enter advances."),
            };

            if (snapshot.MarketPrices != null)
            {
                for (int i = 0; i < snapshot.MarketPrices.Count; i++)
                {
                    var price = snapshot.MarketPrices[i];
                    var commodity = price.Commodity;
                    var isSelectedCommodity = string.Equals(commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase);
                    items.Add(TabletUiHelpers.CreateActionItem(
                        isSelectedCommodity ? string.Format("{0} ~g~[TREND]~s~", commodity) : commodity,
                        string.Format("${0:0}/t | {1} | Press Enter to set graph target.", price.UnitPrice, price.CargoType.ToDisplayName()),
                        () =>
                        {
                            context.StateStore.SetSelectedTrendCommodity(commodity);
                            context.Refresh();
                        }));
                }
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No prices available", "No market price data is currently cached."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the market page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Resources Price",
                Subtitle = "Current unit price for every known resource",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Selectors | Enter Set Trend | Backspace/Esc Back",
                WidthScale = 0.88f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 138f,
                BottomPanelRenderer = panel =>
                {
                    if (selectedPrice == null)
                    {
                        TabletChartRenderer.DrawMessagePanel(
                            panel,
                            "Commodity Trend",
                            "No commodity is currently selected for the graph.",
                            "Select a resource row or use the selector to change the graphed commodity.");
                        return;
                    }

                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Price Trend", selectedPrice.Commodity),
                        string.Format(
                            "{0} cargo | Window {1} | Current ${2:0}/t",
                            selectedPrice.CargoType.ToDisplayName(),
                            context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                            selectedPrice.UnitPrice),
                        context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 214, 168, 94),
                        value => string.Format("${0:0}/t", value));
                },
                Items = items,
            };
        }

        private TabletShellPage BuildIndustryListPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            var industrySummaries = snapshot.IndustrySummaries;

            for (int i = 0; i < industrySummaries.Count; i++)
            {
                var summary = industrySummaries[i];
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildLocationCaption(summary),
                    TabletUiHelpers.BuildLocationOverviewDetail(summary),
                    () => context.Push(TabletAppIds.Network, "detail", summary.Industry)));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No industries available", "No industry nodes are currently configured."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the network hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Industries",
                Subtitle = "Permits, warnings, and live industry detail",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.98f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildLocationListPage(
            TabletShellContext context,
            string title,
            string subtitle,
            IReadOnlyList<TabletLocationSummary> summaries,
            bool openDetail,
            bool includePermitLink)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            if (includePermitLink && summaries != null && summaries.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Contractor Permits",
                    string.Format("Review permit prices for {0} industry sites.", summaries.Count),
                    () => context.Push(TabletAppIds.Network, "permits")));
            }

            if (summaries != null)
            {
                for (int i = 0; i < summaries.Count; i++)
                {
                    var summary = summaries[i];
                    items.Add(TabletUiHelpers.CreateActionItem(
                        TabletUiHelpers.BuildLocationCaption(summary),
                        TabletUiHelpers.BuildLocationOverviewDetail(summary),
                        openDetail ? (Action)(() => context.Push(TabletAppIds.Network, "detail", summary.Industry)) : null));
                }
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(string.Format("No {0} available", title.ToLowerInvariant()), "No configured locations are available for this page."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the network hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = title,
                Subtitle = subtitle,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildPermitPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            for (int i = 0; i < snapshot.IndustrySummaries.Count; i++)
            {
                var summary = snapshot.IndustrySummaries[i];
                var detail = !summary.RequiresContractorPermit
                    ? "~g~OPEN~s~ | No permit required for this industry."
                    : string.Format(
                        "{0} | Permit {1}{2}",
                        summary.HasContractorPermitForGameplay ? "~g~PERMIT~s~" : "~r~LOCKED~s~",
                        ModFormatting.FormatMoney(summary.Industry.IndustryLicencePrice),
                        summary.HasContractorPermitForGameplay ? " | Transport unlocked." : string.Empty);
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildPermitCaption(summary),
                    detail,
                    summary.RequiresContractorPermit ? (Action)(() => context.Push(TabletAppIds.Network, "permit-confirm", summary.Industry)) : null));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No industries available", "No industry permit targets are currently configured."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the industry list.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = "Contractor Permit",
                Subtitle = "Select an industry permit to purchase",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildPermitConfirmPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            if (industry == null)
            {
                return new TabletShellPage
                {
                    Title = "Contractor Permit",
                    Subtitle = "No industry selected",
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    WidthScale = 0.84f,
                    MaxVisibleItems = 4,
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem("No Industry Selected", "Return to the permit list and choose a valid industry."),
                        TabletUiHelpers.CreateNavigationItem("Back", "Return to the permit list.", () => context.GoBack()),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "Purchase Permit",
                    BuildPermitConfirmDetail(context, industry),
                    () =>
                    {
                        if (_purchasePermit != null)
                        {
                            _purchasePermit(industry);
                        }

                        context.Refresh();
                        if (industry.HasContractorPermit || !industry.RequiresContractorPermit)
                        {
                            context.GoBack();
                        }
                    }),
                TabletUiHelpers.CreateNavigationItem("Cancel", "Return to the permit list.", () => context.GoBack()),
            };

            return new TabletShellPage
            {
                Title = "Contractor Permit",
                Subtitle = string.Format("Purchase permit for {0}?", industry.Name),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.82f,
                MaxVisibleItems = 4,
                Items = items,
            };
        }

        private TabletShellPage BuildDetailPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = TabletUiHelpers.FindSummary(context, industry);
            if (industry == null || summary == null)
            {
                return new TabletShellPage
                {
                    Title = "Site Detail",
                    Subtitle = "No site selected",
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem("No Site Selected", "Return to the previous page and choose a valid location."),
                        TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous page.", () => context.GoBack()),
                    },
                };
            }

            if (industry.IsStore || (summary.LocationKind == LSOL.Config.ExternalLocationKind.Industry && industry.SiteRole != SiteRole.Warehouse))
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    "Arrow Up/Down to scroll | Enter, Backspace, or Esc to return");
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateBannerItem(
                    string.Format("{0} {1} {2}", summary.Name, summary.OwnershipTag, summary.PermitTag),
                    TabletUiHelpers.BuildIndustryStatusDetail(summary, industry)),
            };

            TabletUiHelpers.AppendIndustryDetailItems(items, industry, context.StateStore.GetIndustryStatistics(industry));

            float distance;
            if (context.StateStore.IsIndustryInRange(industry, _interactionDistance, out distance))
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Open Nearby Operations",
                    string.Format("Jump into the industry app for {0}.", industry.Name),
                    () => context.Push(TabletAppIds.Industry, "main", industry)));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous page.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = industry.Name,
                Subtitle = industry.SiteRole == SiteRole.Warehouse
                    ? "Storage capacity, accepted resources, and purchase status"
                    : "Conversion, storage, permits, and module levels",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 1.00f,
                MaxVisibleItems = 7,
                Items = items,
            };
        }

        private static string BuildPermitConfirmDetail(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var detail = string.Format(
                "Pay {0} to unlock cargo transport to and from this industry.",
                ModFormatting.FormatMoney(industry.IndustryLicencePrice));
            if (snapshot.Balance < industry.IndustryLicencePrice)
            {
                detail += string.Format(" Need {0} more.", ModFormatting.FormatMoney(industry.IndustryLicencePrice - snapshot.Balance));
            }

            return detail;
        }
    }

    internal sealed class AnalyticsTabletApp : ITabletApp
    {
        public string AppId
        {
            get { return TabletAppIds.Analytics; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "profit":
                    return BuildProfitPage(context);
                case "commodity":
                    return BuildCommodityPage(context);
                case "utilization":
                    return BuildUtilizationPage(context);
                case "storage":
                    return BuildStoragePage(context);
                case "districts":
                    return BuildDistrictPage(context);
                case "routes":
                    return BuildRoutesPage(context);
                default:
                    return BuildRootPage(context);
            }
        }

        private static TabletShellPage BuildRootPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "Profit Trend",
                    "Review company balance history with selectable windows.",
                    () => context.Push(TabletAppIds.Analytics, "profit"),
                    iconLabel: "PRF"),
                TabletUiHelpers.CreateActionItem(
                    "Commodity Trend",
                    "Browse commodity price history over time.",
                    () => context.Push(TabletAppIds.Analytics, "commodity"),
                    iconLabel: "MKT"),
                TabletUiHelpers.CreateActionItem(
                    "Site Utilization",
                    "Inspect per-site utilization history for industries.",
                    () => context.Push(TabletAppIds.Analytics, "utilization"),
                    iconLabel: "UTL"),
                TabletUiHelpers.CreateActionItem(
                    "Storage Fill",
                    "Review warehouse and industry storage fill history.",
                    () => context.Push(TabletAppIds.Analytics, "storage"),
                    iconLabel: "STO"),
                TabletUiHelpers.CreateActionItem(
                    "District Influence",
                    "Compare district influence and current reputation.",
                    () => context.Push(TabletAppIds.Analytics, "districts"),
                    iconLabel: "DST"),
                TabletUiHelpers.CreateActionItem(
                    "NPC Routes",
                    "Delivered tons, loss ratio, and average payout.",
                    () => context.Push(TabletAppIds.Analytics, "routes"),
                    iconLabel: "NPC"),
                TabletUiHelpers.CreateNavigationItem(
                    "Back",
                    "Return to the company hub.",
                    () => context.GoBack(),
                    "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Analytics",
                Subtitle = "Profit, market, site, district, and route telemetry",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Keys Navigate | Enter Open Detail | Backspace/Esc Back",
                WidthScale = 0.94f,
                Layout = SimpleMenuTabletLayout.Dashboard,
                DashboardSidebarCount = 0,
                DashboardTileColumns = 3,
                BottomPanelHeight = 156f,
                BottomPanelRenderer = panel => DrawRootPreviewPanel(panel, context, snapshot),
                Items = items,
            };
        }

        private static TabletShellPage BuildProfitPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the company profit graph window."),
                TabletUiHelpers.CreateInfoItem(
                    "Company Balance",
                    string.Format("Current balance {0} | Window {1}", ModFormatting.FormatMoney(snapshot.Balance), context.StateStore.SelectedGraphTimeframe.ToDisplayLabel())),
                TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Profit Trend",
                Subtitle = "Company balance history across selectable windows",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Timeframe | Enter Select | Backspace/Esc Back",
                WidthScale = 0.88f,
                MaxVisibleItems = 5,
                BottomPanelHeight = 142f,
                BottomPanelRenderer = panel => TabletChartRenderer.DrawHistoryPanel(
                    panel,
                    "Company Profit",
                    string.Format("{0} window | Current {1}", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(), ModFormatting.FormatMoney(snapshot.Balance)),
                    context.StateStore.GetProfitHistory(context.StateStore.SelectedGraphTimeframe),
                    Color.FromArgb(214, 118, 200, 176),
                    value => ModFormatting.FormatMoney(value)),
                Items = items,
            };
        }

        private static TabletShellPage BuildCommodityPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var prices = snapshot.MarketPrices != null
                ? snapshot.MarketPrices.Where(price => price != null).OrderBy(price => price.Commodity, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<TabletMarketResourcePrice>();
            var selectedCommodity = context.StateStore.SelectedTrendCommodity;
            var selectedPrice = prices.FirstOrDefault(price => string.Equals(price.Commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase));
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the commodity graph window."),
                TabletUiHelpers.CreateCommoditySelectorItem(context, "Left/right changes the graphed commodity. Enter advances."),
            };

            for (int i = 0; i < prices.Count; i++)
            {
                var price = prices[i];
                var commodity = price.Commodity;
                var isSelectedCommodity = string.Equals(commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase);
                items.Add(TabletUiHelpers.CreateActionItem(
                    isSelectedCommodity ? string.Format("{0} ~g~[TREND]~s~", commodity) : commodity,
                    string.Format("{0} | Current ${1:0}/t | Press Enter to set graph target.", price.CargoType.ToDisplayName(), price.UnitPrice),
                    () =>
                    {
                        context.StateStore.SetSelectedTrendCommodity(commodity);
                        context.Refresh();
                    }));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No commodities available", "No market prices are currently cached."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Commodity Trend",
                Subtitle = "Select a resource to inspect its recent price history",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Selectors | Enter Set Trend | Backspace/Esc Back",
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 142f,
                BottomPanelRenderer = panel => DrawCommodityTrendPanel(panel, context, snapshot, selectedPrice, "Use the selectors or press Enter on a resource row to change the graphed commodity."),
                Items = items,
            };
        }

        private static TabletShellPage BuildUtilizationPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var industries = snapshot.IndustrySummaries != null
                ? snapshot.IndustrySummaries.Where(summary => summary != null && summary.Industry != null).OrderBy(summary => summary.Name).ToList()
                : new List<TabletLocationSummary>();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the utilization graph window."),
            };

            for (int i = 0; i < industries.Count; i++)
            {
                var summary = industries[i];
                items.Add(TabletUiHelpers.CreateInfoItem(
                    summary.Name,
                    string.Format("{0:0.0} t/h | {1:0}% current use", summary.OutputPerHourTons, summary.UtilizationPercent)));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No industries tracked", "No production sites are currently available for utilization analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Site Utilization",
                Subtitle = "Select an industry to review utilization history",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Timeframe | Enter Select | Backspace/Esc Back",
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 142f,
                BottomPanelRenderer = panel =>
                {
                    if (industries.Count == 0)
                    {
                        TabletChartRenderer.DrawMessagePanel(panel, "Utilization History", "No industries tracked.", "Unlock or configure a production site to begin sampling utilization.");
                        return;
                    }

                    var selectedIndex = GetGraphListSelectionIndex(panel.SelectedIndex, 1, industries.Count);
                    var summary = industries[selectedIndex];
                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Utilization", summary.Name),
                        string.Format("Current {0:0}% | Output {1:0.0} t/h | Window {2}", summary.UtilizationPercent, summary.OutputPerHourTons, context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetSiteUtilizationHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 116, 194, 152),
                        value => string.Format("{0:0}%", value));
                },
                Items = items,
            };
        }

        private static TabletShellPage BuildStoragePage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var siteSummaries = snapshot.WarehouseSummaries
                .Concat(snapshot.IndustrySummaries)
                .Where(summary => summary != null && summary.Industry != null)
                .OrderBy(summary => summary.Name)
                .ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the storage graph window."),
            };

            for (int i = 0; i < siteSummaries.Count; i++)
            {
                var summary = siteSummaries[i];
                items.Add(TabletUiHelpers.CreateInfoItem(
                    summary.Name,
                    string.Format(
                        "{0:0.0}/{1:0.0}t | {2:0}% full | {3}",
                        summary.StorageTons,
                        summary.TotalCapacityTons,
                        summary.FillRatio * 100f,
                        summary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry")));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No storage sites tracked", "No industry or warehouse sites are currently available for storage analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Storage Fill",
                Subtitle = "Select a warehouse or industry to review fill history",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Timeframe | Enter Select | Backspace/Esc Back",
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 142f,
                BottomPanelRenderer = panel =>
                {
                    if (siteSummaries.Count == 0)
                    {
                        TabletChartRenderer.DrawMessagePanel(panel, "Storage Fill", "No storage telemetry tracked.", "Configure sites and run cargo through them to build fill history.");
                        return;
                    }

                    var selectedIndex = GetGraphListSelectionIndex(panel.SelectedIndex, 1, siteSummaries.Count);
                    var summary = siteSummaries[selectedIndex];
                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Storage Fill", summary.Name),
                        string.Format("Current {0:0}% | {1} | Window {2}", summary.FillRatio * 100f, summary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetSiteStorageHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 124, 178, 232),
                        value => string.Format("{0:0}%", value));
                },
                Items = items,
            };
        }

        private static TabletShellPage BuildDistrictPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var districts = context.StateStore.GetDistrictComparisons().ToList();
            var items = new List<MenuItem>();

            for (int i = 0; i < districts.Count; i++)
            {
                var district = districts[i];
                items.Add(TabletUiHelpers.CreateInfoItem(
                    district.DistrictName,
                    string.Format("{0:0}% influence | {1} | {2} depots", district.InfluencePercent, district.ReputationLabel, district.ControlledDepots)));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No districts tracked", "District telemetry is not currently available."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "District Influence",
                Subtitle = "Compare influence across tracked districts",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 142f,
                BottomPanelRenderer = panel =>
                {
                    var liveDistricts = context.StateStore.GetDistrictComparisons().ToList();
                    if (liveDistricts.Count == 0)
                    {
                        TabletChartRenderer.DrawMessagePanel(panel, "District Influence", "No district comparisons available.", "District telemetry will appear once TerritoryManager is active.");
                        return;
                    }

                    var selectedIndex = Math.Max(0, Math.Min(panel.SelectedIndex, liveDistricts.Count - 1));
                    var selectedDistrict = liveDistricts[selectedIndex];
                    var entries = liveDistricts
                        .Select(district => new TabletBarEntry
                        {
                            Label = district.DistrictName,
                            Value = district.InfluencePercent,
                            ValueText = string.Format("{0:0}%", district.InfluencePercent),
                            FillColor = Color.FromArgb(210, 124, 178, 232),
                        })
                        .ToArray();

                    TabletChartRenderer.DrawComparisonBarsPanel(
                        panel,
                        "District Influence Comparison",
                        string.Format("{0} | {1} rep | {2} controlled sites", selectedDistrict.DistrictName, selectedDistrict.ReputationLabel, selectedDistrict.ControlledSites),
                        entries,
                        selectedIndex);
                },
                Items = items,
            };
        }

        private static TabletShellPage BuildRoutesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var routes = context.StateStore.GetNpcRoutePerformance().ToList();
            var items = new List<MenuItem>();

            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                items.Add(TabletUiHelpers.CreateInfoItem(
                    route.Label,
                    string.Format(
                        "{0:0.0}t delivered | {1:0}% loss | Avg {2}",
                        route.DeliveredTons,
                        route.LossRatioPercent,
                        ModFormatting.FormatMoney(route.AveragePayout))));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No NPC routes active", "Hire an NPC route to begin route-performance analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "NPC Route Performance",
                Subtitle = "Select a route to inspect delivered tons, loss ratio, and payout",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel =>
                {
                    var liveRoutes = context.StateStore.GetNpcRoutePerformance().ToList();
                    if (liveRoutes.Count == 0)
                    {
                        TabletChartRenderer.DrawMessagePanel(panel, "NPC Route Metrics", "No routes available.", "Hire an NPC contract to populate this analytics surface.");
                        return;
                    }

                    var selectedIndex = Math.Max(0, Math.Min(panel.SelectedIndex, liveRoutes.Count - 1));
                    var route = liveRoutes[selectedIndex];
                    var maxDelivered = Math.Max(1f, liveRoutes.Max(entry => entry.DeliveredTons));
                    var maxPayout = Math.Max(1f, liveRoutes.Max(entry => entry.AveragePayout));
                    var entries = new[]
                    {
                        new TabletMetricBarEntry
                        {
                            Label = "Delivered Tons",
                            Ratio = route.DeliveredTons / maxDelivered,
                            ValueText = string.Format("{0:0.0}t", route.DeliveredTons),
                            FillColor = Color.FromArgb(214, 118, 192, 164),
                        },
                        new TabletMetricBarEntry
                        {
                            Label = "Loss Ratio",
                            Ratio = route.LossRatioPercent / 100f,
                            ValueText = string.Format("{0:0}%", route.LossRatioPercent),
                            FillColor = Color.FromArgb(214, 214, 156, 84),
                        },
                        new TabletMetricBarEntry
                        {
                            Label = "Average Payout",
                            Ratio = route.AveragePayout / maxPayout,
                            ValueText = ModFormatting.FormatMoney(route.AveragePayout),
                            FillColor = Color.FromArgb(214, 122, 170, 232),
                        },
                    };

                    TabletChartRenderer.DrawMetricBarsPanel(
                        panel,
                        route.Label,
                        string.Format("{0} deliveries | {1}", route.CompletedDeliveries, route.Detail),
                        entries);
                },
                Items = items,
            };
        }

        private static void DrawRootPreviewPanel(SimpleMenuTabletPanelContext panel, TabletShellContext context, TabletStateSnapshot snapshot)
        {
            switch (panel.SelectedIndex)
            {
                case 0:
                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        "Company Profit",
                        string.Format("{0} window captured during gameplay.", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetProfitHistory(context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 118, 200, 176),
                        value => ModFormatting.FormatMoney(value));
                    return;
                case 1:
                    DrawCommodityPreview(panel, context, snapshot);
                    return;
                case 2:
                    DrawUtilizationPreview(panel, context, snapshot);
                    return;
                case 3:
                    DrawStoragePreview(panel, context, snapshot);
                    return;
                case 4:
                    DrawDistrictPreview(panel, context);
                    return;
                case 5:
                    DrawRoutesPreview(panel, context);
                    return;
                default:
                    TabletChartRenderer.DrawMessagePanel(panel, "Analytics", "Select a tile to preview its chart.", "Press Enter on a drill-down tile to browse its dedicated page.");
                    return;
            }
        }

        private static void DrawCommodityTrendPanel(
            SimpleMenuTabletPanelContext panel,
            TabletShellContext context,
            TabletStateSnapshot snapshot,
            TabletMarketResourcePrice selectedPrice,
            string emptyMessage)
        {
            if (selectedPrice == null)
            {
                TabletChartRenderer.DrawMessagePanel(
                    panel,
                    "Commodity Trend",
                    "No commodity is currently selected for the graph.",
                    emptyMessage);
                return;
            }

            TabletChartRenderer.DrawHistoryPanel(
                panel,
                string.Format("{0} Price Trend", selectedPrice.Commodity),
                string.Format(
                    "{0} cargo | Window {1} | Current ${2:0}/t",
                    selectedPrice.CargoType.ToDisplayName(),
                    context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                    selectedPrice.UnitPrice),
                context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                Color.FromArgb(214, 214, 168, 94),
                value => string.Format("${0:0}/t", value));
        }

        private static int GetGraphListSelectionIndex(int selectedIndex, int selectorCount, int entryCount)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            var entryIndex = selectedIndex - Math.Max(0, selectorCount);
            if (entryIndex < 0)
            {
                entryIndex = 0;
            }

            if (entryIndex >= entryCount)
            {
                entryIndex = entryCount - 1;
            }

            return entryIndex;
        }

        private static void DrawCommodityPreview(SimpleMenuTabletPanelContext panel, TabletShellContext context, TabletStateSnapshot snapshot)
        {
            var commodity = context.StateStore.SelectedTrendCommodity;
            if (string.IsNullOrWhiteSpace(commodity))
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Commodity Trend", "No commodities cached.", "Press Enter after the market board has populated to browse detailed commodity history.");
                return;
            }

            var currentPrice = snapshot.MarketPrices != null
                ? snapshot.MarketPrices.FirstOrDefault(price => price != null && string.Equals(price.Commodity, commodity, StringComparison.OrdinalIgnoreCase))
                : null;
            TabletChartRenderer.DrawHistoryPanel(
                panel,
                string.Format("{0} Trend", commodity),
                string.Format(
                    "Window {0} | Current {1} | Press Enter to browse resources",
                    context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                    currentPrice != null ? string.Format("${0:0}/t", currentPrice.UnitPrice) : "price board"),
                context.StateStore.GetCommodityPriceHistory(commodity, context.StateStore.SelectedGraphTimeframe),
                Color.FromArgb(214, 214, 168, 94),
                value => string.Format("${0:0}/t", value));
        }

        private static void DrawUtilizationPreview(SimpleMenuTabletPanelContext panel, TabletShellContext context, TabletStateSnapshot snapshot)
        {
            var industry = snapshot.HasNearestIndustry && snapshot.NearestIndustry != null && snapshot.NearestIndustry.SiteRole != SiteRole.Warehouse
                ? snapshot.NearestIndustry
                : snapshot.IndustrySummaries.FirstOrDefault(summary => summary != null && summary.Industry != null)?.Industry;
            if (industry == null)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Site Utilization", "No industries tracked.", "Press Enter after configuring a production site to inspect utilization history.");
                return;
            }

            TabletChartRenderer.DrawHistoryPanel(
                panel,
                string.Format("{0} Utilization", industry.Name),
                string.Format("Window {0} | Press Enter to browse all industries.", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                context.StateStore.GetSiteUtilizationHistory(industry, context.StateStore.SelectedGraphTimeframe),
                Color.FromArgb(214, 116, 194, 152),
                value => string.Format("{0:0}%", value));
        }

        private static void DrawStoragePreview(SimpleMenuTabletPanelContext panel, TabletShellContext context, TabletStateSnapshot snapshot)
        {
            var site = snapshot.HasNearestIndustry && snapshot.NearestIndustry != null
                ? snapshot.NearestIndustry
                : snapshot.WarehouseSummaries.FirstOrDefault(summary => summary != null && summary.Industry != null)?.Industry
                    ?? snapshot.IndustrySummaries.FirstOrDefault(summary => summary != null && summary.Industry != null)?.Industry;
            if (site == null)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Storage Fill", "No storage sites tracked.", "Press Enter to browse all warehouses and industries with storage history.");
                return;
            }

            TabletChartRenderer.DrawHistoryPanel(
                panel,
                string.Format("{0} Storage Fill", site.Name),
                string.Format("Window {0} | Press Enter to browse all storage sites.", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                context.StateStore.GetSiteStorageHistory(site, context.StateStore.SelectedGraphTimeframe),
                Color.FromArgb(214, 124, 178, 232),
                value => string.Format("{0:0}%", value));
        }

        private static void DrawDistrictPreview(SimpleMenuTabletPanelContext panel, TabletShellContext context)
        {
            var districts = context.StateStore.GetDistrictComparisons();
            if (districts == null || districts.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "District Influence", "No district comparisons available.", "Press Enter after TerritoryManager is active to browse district influence analytics.");
                return;
            }

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                "District Influence",
                "Press Enter to review the full district comparison list.",
                districts.Take(5)
                    .Select(district => new TabletBarEntry
                    {
                        Label = district.DistrictName,
                        Value = district.InfluencePercent,
                        ValueText = string.Format("{0:0}%", district.InfluencePercent),
                        FillColor = Color.FromArgb(210, 124, 178, 232),
                    })
                    .ToArray());
        }

        private static void DrawRoutesPreview(SimpleMenuTabletPanelContext panel, TabletShellContext context)
        {
            var routes = context.StateStore.GetNpcRoutePerformance();
            if (routes == null || routes.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "NPC Route Performance", "No NPC routes are active.", "Press Enter after hiring an NPC route to inspect its analytics bars.");
                return;
            }

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                "NPC Delivered Tons",
                "Press Enter to inspect loss ratio and payout for a specific route.",
                routes.Take(5)
                    .Select(route => new TabletBarEntry
                    {
                        Label = route.Label,
                        Value = route.DeliveredTons,
                        ValueText = string.Format("{0:0.0}t", route.DeliveredTons),
                        FillColor = Color.FromArgb(210, 118, 192, 164),
                    })
                    .ToArray());
        }
    }

    internal sealed class IndustryTabletApp : ITabletApp
    {
        private readonly float _interactionDistance;
        private readonly Action<Industry> _loadRequested;
        private readonly Action<Industry, string> _loadCommodityRequested;
        private readonly Action<Industry> _unloadRequested;
        private readonly Action<Industry, bool> _unloadModeRequested;
        private readonly Action<Industry> _refuelRequested;
        private readonly Action<Industry, IndustryUpgradeModule> _upgradeModuleRequested;
        private readonly Action<Industry> _vehicleSpawnerRequested;
        private readonly Func<Industry, string> _purchaseIndustry;

        public IndustryTabletApp(
            float interactionDistance,
            Action<Industry> loadRequested,
            Action<Industry, string> loadCommodityRequested,
            Action<Industry> unloadRequested,
            Action<Industry, bool> unloadModeRequested,
            Action<Industry> refuelRequested,
            Action<Industry, IndustryUpgradeModule> upgradeModuleRequested,
            Action<Industry> vehicleSpawnerRequested,
            Func<Industry, string> purchaseIndustry)
        {
            _interactionDistance = interactionDistance;
            _loadRequested = loadRequested;
            _loadCommodityRequested = loadCommodityRequested;
            _unloadRequested = unloadRequested;
            _unloadModeRequested = unloadModeRequested;
            _refuelRequested = refuelRequested;
            _upgradeModuleRequested = upgradeModuleRequested;
            _vehicleSpawnerRequested = vehicleSpawnerRequested;
            _purchaseIndustry = purchaseIndustry;
        }

        public string AppId
        {
            get { return TabletAppIds.Industry; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var industry = GetTargetIndustry(context, route);
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "load":
                    return BuildLoadPage(context, industry);
                case "unload":
                    return BuildUnloadPage(context, industry);
                case "stats":
                    return BuildStatsPage(context, industry);
                case "upgrades":
                    return BuildUpgradesPage(context, industry);
                case "purchase-confirm":
                    return BuildPurchaseConfirmPage(context, industry);
                default:
                    return BuildMainPage(context, industry);
            }
        }

        private TabletShellPage BuildMainPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = TabletUiHelpers.FindSummary(context, industry);
            float distance;
            if (industry == null || summary == null)
            {
                return BuildUnavailablePage(snapshot, "No industry selected.", () => context.GoBack());
            }

            if (!context.StateStore.IsIndustryInRange(industry, _interactionDistance, out distance))
            {
                return BuildUnavailablePage(snapshot, string.Format("Tablet signal lost for {0}. Move closer to the marker.", industry.Name), () => context.GoBack());
            }

            var items = new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Status", snapshot.StatusBanner));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    string.Format("{0} {1} {2}", industry.Name, summary.OwnershipTag, summary.PermitTag),
                    TabletUiHelpers.BuildIndustryStatusDetail(summary, industry)));
            }

            var loadSnapshot = context.StateStore.GetLoadOptions(industry);
            items.Add(TabletUiHelpers.CreateActionItem(
                "Load Product",
                BuildLoadActionDetail(loadSnapshot),
                () =>
                {
                    if (loadSnapshot.LoadOptions.Count > 1)
                    {
                        context.Push(TabletAppIds.Industry, "load", industry);
                        return;
                    }

                    if (loadSnapshot.LoadOptions.Count == 1)
                    {
                        _loadCommodityRequested?.Invoke(industry, loadSnapshot.LoadOptions[0]);
                    }
                    else
                    {
                        _loadRequested?.Invoke(industry);
                    }

                    context.Refresh();
                },
                progress: loadSnapshot.HasCargoVehicle && loadSnapshot.FreeCapacityTons > 0f && snapshot.CargoCapacityTons > 0.001f
                    ? ModMath.Clamp01(loadSnapshot.FreeCapacityTons / snapshot.CargoCapacityTons)
                    : (float?)null));

            var canChooseOmegaUnloadMode = industry.SupportsOmegaBoost && CargoTransferController.IndustryHasMultipleInputs(industry);
            items.Add(TabletUiHelpers.CreateActionItem(
                "Unload Cargo",
                BuildUnloadActionDetail(snapshot, industry, canChooseOmegaUnloadMode),
                () =>
                {
                    if (canChooseOmegaUnloadMode)
                    {
                        context.Push(TabletAppIds.Industry, "unload", industry);
                        return;
                    }

                    _unloadRequested?.Invoke(industry);
                    context.Refresh();
                },
                progress: snapshot.CargoCapacityRatio));

            if (industry.IsGasStation && snapshot.HasPoweredVehicle && snapshot.FuelCapacityLiters > 0.001f)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Refuel",
                    BuildRefuelActionDetail(snapshot, industry),
                    () =>
                    {
                        _refuelRequested?.Invoke(industry);
                        context.Refresh();
                    },
                    progress: snapshot.FuelRatio));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                industry.SiteRole == SiteRole.Warehouse ? "Storage Detail" : "Statistics",
                industry.SiteRole == SiteRole.Warehouse
                    ? "Storage capacity, accepted resources, and purchase status."
                    : "Stockpile, utilization, and per-commodity input/output inventories.",
                () => context.Push(TabletAppIds.Industry, "stats", industry)));
            items.Add(TabletUiHelpers.CreateActionItem(
                "Vehicle Spawn",
                "Open the configured company vehicle spawner for this industry pad.",
                () =>
                {
                    _vehicleSpawnerRequested?.Invoke(industry);
                    context.Refresh();
                }));

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                if (summary.RequiresIndustryPurchase)
                {
                    items.Add(TabletUiHelpers.CreateActionItem(
                        "Buy Warehouse",
                        string.Format("Purchase this storage site for {0}.", ModFormatting.FormatMoney(industry.IndustryPrice)),
                        () => context.Push(TabletAppIds.Industry, "purchase-confirm", industry)));
                }
            }
            else
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    summary.RequiresIndustryPurchase ? "Buy Industry" : "Open Upgrades",
                    summary.RequiresIndustryPurchase
                        ? string.Format("Purchase this site for {0} to unlock upgrades.", ModFormatting.FormatMoney(industry.IndustryPrice))
                        : "Review production, input, output, and Omega upgrade modules.",
                    summary.RequiresIndustryPurchase
                        ? (Action)(() => context.Push(TabletAppIds.Industry, "purchase-confirm", industry))
                        : (Action)(() => context.Push(TabletAppIds.Industry, "upgrades", industry))));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub or previous tablet page.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Industry",
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.96f,
                MaxVisibleItems = 8,
                Items = items,
            };
        }

        private TabletShellPage BuildLoadPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            float distance;
            if (industry == null || !context.StateStore.IsIndustryInRange(industry, _interactionDistance, out distance))
            {
                return BuildUnavailablePage(snapshot, "Move back into range to select a load product.", () => context.GoBack());
            }

            var loadSnapshot = context.StateStore.GetLoadOptions(industry);
            var items = new List<MenuItem>();
            for (int i = 0; i < loadSnapshot.LoadOptions.Count; i++)
            {
                var commodity = loadSnapshot.LoadOptions[i];
                string detail;
                if (!loadSnapshot.LoadOptionSubtitles.TryGetValue(commodity, out detail))
                {
                    detail = "Load this commodity into the active cargo vehicle.";
                }

                items.Add(TabletUiHelpers.CreateActionItem(
                    commodity,
                    detail,
                    () =>
                    {
                        _loadCommodityRequested?.Invoke(industry, commodity);
                        context.Refresh();
                    }));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No compatible product", loadSnapshot.StatusText));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back to Operations", "Return to industry actions.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = "Load Selection",
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildUnloadPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            float distance;
            if (industry == null || !context.StateStore.IsIndustryInRange(industry, _interactionDistance, out distance))
            {
                return BuildUnavailablePage(snapshot, "Move back into range to choose an unload mode.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "Unload Truck Cargo",
                    "Send the current truck or trailer cargo into the site buffers.",
                    () =>
                    {
                        _unloadModeRequested?.Invoke(industry, false);
                        context.Refresh();
                    }),
            };

            if (industry.SupportsOmegaBoost)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Unload Omega Only",
                    "Only unload if the active tanker carries Omega boost fluid.",
                    () =>
                    {
                        _unloadModeRequested?.Invoke(industry, true);
                        context.Refresh();
                    }));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back to Operations", "Return to industry actions.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = "Unload Mode",
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.88f,
                MaxVisibleItems = 5,
                Items = items,
            };
        }

        private TabletShellPage BuildStatsPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = TabletUiHelpers.FindSummary(context, industry);
            if (industry == null || summary == null)
            {
                return BuildUnavailablePage(snapshot, "No industry selected.", () => context.GoBack());
            }

            var statistics = context.StateStore.GetIndustryStatistics(industry);
            if (industry.SiteRole != SiteRole.Warehouse)
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    "Arrow Up/Down to scroll | Enter, Backspace, or Esc to return");
            }

            var items = new List<MenuItem>();
            TabletUiHelpers.AppendIndustryStatisticsItems(items, summary, industry, statistics);
            items.Add(TabletUiHelpers.CreateNavigationItem("Back to Operations", "Return to industry actions.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = industry.SiteRole == SiteRole.Warehouse ? "Warehouse Detail" : "Industry Statistics",
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.98f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildUpgradesPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = TabletUiHelpers.FindSummary(context, industry);
            if (industry == null || summary == null)
            {
                return BuildUnavailablePage(snapshot, "No industry selected.", () => context.GoBack());
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                return BuildUnavailablePage(snapshot, "Warehouses do not expose production or storage upgrade modules.", () => context.GoBack());
            }

            if (summary.RequiresIndustryPurchase)
            {
                return BuildPurchaseConfirmPage(context, industry);
            }

            var items = new List<MenuItem>();
            var modules = new[]
            {
                IndustryUpgradeModule.Production,
                IndustryUpgradeModule.InputStorage,
                IndustryUpgradeModule.OutputStorage,
                IndustryUpgradeModule.OmegaStorage,
            };

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (industry.GetUpgradeCost(module) <= 0f)
                {
                    continue;
                }

                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.GetUpgradeTitle(module),
                    TabletUiHelpers.BuildUpgradeDetail(industry, module, snapshot.Balance),
                    () =>
                    {
                        _upgradeModuleRequested?.Invoke(industry, module);
                        context.Refresh();
                    }));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No upgrade modules", "This industry does not expose upgrade modules."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back to Operations", "Return to industry actions.", () => context.GoBack()));

            return new TabletShellPage
            {
                Title = "Industry Upgrades",
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildPurchaseConfirmPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            if (industry == null)
            {
                return BuildUnavailablePage(snapshot, "No industry selected.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "Purchase Industry",
                    BuildIndustryPurchaseDetail(context, industry),
                    () =>
                    {
                        if (_purchaseIndustry != null)
                        {
                            _purchaseIndustry(industry);
                        }

                        context.Refresh();
                        if (industry.IsOwned || !industry.RequiresPurchase)
                        {
                            context.GoBack();
                        }
                    }),
                TabletUiHelpers.CreateNavigationItem("Cancel", "Return to industry operations.", () => context.GoBack()),
            };

            return new TabletShellPage
            {
                Title = "Buy Industry",
                Subtitle = string.Format("Purchase {0}?", industry.Name),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.84f,
                MaxVisibleItems = 4,
                Items = items,
            };
        }

        private static Industry GetTargetIndustry(TabletShellContext context, TabletRoute route)
        {
            var industry = route != null ? route.Payload as Industry : null;
            if (industry != null)
            {
                return industry;
            }

            var snapshot = context != null ? context.Snapshot : null;
            return snapshot != null ? snapshot.NearestIndustry : null;
        }

        private TabletShellPage BuildUnavailablePage(TabletStateSnapshot snapshot, string detail, Action backAction)
        {
            return new TabletShellPage
            {
                Title = "Industry",
                Subtitle = "Signal unavailable",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.82f,
                MaxVisibleItems = 5,
                Items = new[]
                {
                    TabletUiHelpers.CreateBannerItem("Industry Signal Lost", detail),
                    TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous tablet page.", backAction),
                },
            };
        }

        private static string BuildLoadActionDetail(IndustryLoadOptionsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "No load data available.";
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusText))
            {
                return snapshot.StatusText;
            }

            if (snapshot.LoadOptions.Count == 1)
            {
                var commodity = snapshot.LoadOptions[0];
                string detail;
                return snapshot.LoadOptionSubtitles.TryGetValue(commodity, out detail)
                    ? detail
                    : string.Format("Load {0} into the active cargo vehicle.", commodity);
            }

            return string.Format("{0} compatible products ready for selection.", snapshot.LoadOptions.Count);
        }

        private static string BuildUnloadActionDetail(TabletStateSnapshot snapshot, Industry industry, bool canChooseOmegaUnloadMode)
        {
            if (snapshot == null || !snapshot.HasCargoVehicle)
            {
                return "Bring a cargo vehicle close to the industry marker.";
            }

            if (snapshot.CargoIsEmpty)
            {
                return "Vehicle is empty.";
            }

            if (canChooseOmegaUnloadMode)
            {
                return "Choose whether to unload truck cargo or Omega only.";
            }

            if (CargoTransferController.IsOmegaOnlyUnloadIndustry(industry))
            {
                return "This site only accepts Omega fluid from a tanker.";
            }

            return string.Format("Unload {0} from the active cargo vehicle.", snapshot.CargoCommodity);
        }

        private static string BuildRefuelActionDetail(TabletStateSnapshot snapshot, Industry industry)
        {
            if (snapshot == null || !snapshot.HasPoweredVehicle || snapshot.FuelCapacityLiters <= 0.001f)
            {
                return "Bring a powered cargo vehicle close to the petrol station.";
            }

            var tankDetail = snapshot.FuelVehicleMatchesCargoVehicle
                ? string.Format("Tank {0:0}/{1:0}L", snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters)
                : string.Format("{0} tank {1:0}/{2:0}L", snapshot.PoweredVehicleName, snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters);
            var pricingDetail = industry != null && industry.RefuelIsFree
                ? "Free at office station."
                : "Uses station stock and current fuel market price.";

            if (snapshot.FuelIsEmpty)
            {
                pricingDetail = industry != null && industry.RefuelIsFree
                    ? "Truck is empty. Office refill is free."
                    : "Truck is empty. Paid refill uses station stock.";
            }

            return string.Format("{0} | {1}", tankDetail, pricingDetail);
        }

        private static string BuildIndustryPurchaseDetail(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var detail = string.Format("Deduct {0} and unlock upgrades.", ModFormatting.FormatMoney(industry.IndustryPrice));
            if (industry.IndustryOwnerCut > 0f)
            {
                detail += string.Format(" Removes the {0:0}% owner cut.", industry.IndustryOwnerCut * 100f);
            }

            if (snapshot.Balance < industry.IndustryPrice)
            {
                detail += string.Format(" Need {0} more.", ModFormatting.FormatMoney(industry.IndustryPrice - snapshot.Balance));
            }

            return detail;
        }
    }
}
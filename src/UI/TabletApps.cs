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
        private readonly SpecialMissionManager _specialMissionManager;

        public HomeTabletApp(Action openCompanyMap, Action openDistrictView, Action openDepotView, SpecialMissionManager specialMissionManager)
        {
            _openCompanyMap = openCompanyMap;
            _openDistrictView = openDistrictView;
            _openDepotView = openDepotView;
            _specialMissionManager = specialMissionManager;
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
            var missionListings = _specialMissionManager != null
                ? _specialMissionManager.GetMissionListings()
                : Array.Empty<SpecialMissionListing>();
            var availableMissionCount = missionListings.Count(listing => listing != null && listing.CanAccept);
            var activeMission = missionListings.FirstOrDefault(listing => listing != null && listing.IsActive);
            var missionDetail = activeMission != null
                ? string.Format("{0}\n{1}", activeMission.Name, activeMission.Objective)
                : missionListings.Count > 0
                    ? (availableMissionCount > 0
                        ? string.Format("{0} contracts ready\n{1} community missions loaded", availableMissionCount, missionListings.Count)
                        : string.Format("{0} community missions loaded\nGrow district influence to unlock more contracts.", missionListings.Count))
                    : "No mission packs loaded.\nAdd INI files to the missions folder next to LSOL.ini.";
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
                "Missions",
                missionDetail,
                () => context.Push(TabletAppIds.Missions, "root"),
                Color.FromArgb(186, 88, 58, 54),
                Color.FromArgb(228, 208, 144, 112),
                null,
                "MIS"));
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

    internal sealed class SpecialMissionsTabletApp : ITabletApp
    {
        private readonly SpecialMissionManager _missionManager;

        public SpecialMissionsTabletApp(SpecialMissionManager missionManager)
        {
            _missionManager = missionManager;
        }

        public string AppId
        {
            get { return TabletAppIds.Missions; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var pageId = route != null ? route.PageId : string.Empty;
            if (string.Equals(pageId, "detail", StringComparison.OrdinalIgnoreCase))
            {
                return BuildDetailPage(context, route != null ? route.Payload as string : string.Empty);
            }

            return BuildRootPage(context);
        }

        private TabletShellPage BuildRootPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();

            if (_missionManager == null || !_missionManager.HasDefinitions)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    "No mission packs loaded",
                    "Create or copy mission INI files into the missions folder next to LSOL.ini to publish community contracts."));
            }
            else
            {
                if (_missionManager.HasActiveMission)
                {
                    items.Add(TabletUiHelpers.CreateBannerItem(
                        string.Format("ACTIVE | {0}", _missionManager.ActiveMissionName),
                        string.Format("{0} | {1}", _missionManager.ActiveObjective, _missionManager.ActiveObjectiveDetail)));
                }

                if (_missionManager.Catalog != null && _missionManager.Catalog.ValidationMessages.Count > 0)
                {
                    items.Add(TabletUiHelpers.CreateBannerItem(
                        "Mission Pack Warnings",
                        string.Format("{0} pack validation message(s) found. Review the missions folder docs before publishing new contracts.", _missionManager.Catalog.ValidationMessages.Count)));
                }

                var listings = _missionManager.GetMissionListings();
                for (int i = 0; i < listings.Count; i++)
                {
                    var listing = listings[i];
                    var caption = BuildMissionCaption(listing);
                    var detail = BuildMissionListDetail(listing);
                    var idle = listing.IsActive
                        ? Color.FromArgb(184, 82, 88, 52)
                        : (listing.CanAccept
                            ? Color.FromArgb(178, 60, 74, 56)
                            : Color.FromArgb(176, 66, 56, 68));
                    var active = listing.IsActive
                        ? Color.FromArgb(226, 176, 212, 116)
                        : (listing.CanAccept
                            ? Color.FromArgb(222, 126, 182, 138)
                            : Color.FromArgb(214, 154, 128, 154));
                    items.Add(TabletUiHelpers.CreateActionItem(
                        caption,
                        detail,
                        () => context.Push(TabletAppIds.Missions, "detail", listing.MissionId),
                        idle,
                        active,
                        null,
                        "JOB"));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Special Missions",
                Subtitle = "Community contract board",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildDetailPage(TabletShellContext context, string missionId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            if (_missionManager == null)
            {
                return BuildUnavailablePage(snapshot, "Mission manager unavailable.", () => context.GoBack());
            }

            var definition = _missionManager.GetDefinition(missionId);
            var listing = _missionManager.GetMissionListings().FirstOrDefault(entry => entry != null && string.Equals(entry.MissionId, missionId, StringComparison.OrdinalIgnoreCase));
            if (definition == null || listing == null)
            {
                return BuildUnavailablePage(snapshot, "Mission definition unavailable.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateBannerItem(
                    string.Format("{0} | Reward {1}", definition.Category, ModFormatting.FormatMoney(definition.Reward)),
                    string.IsNullOrWhiteSpace(definition.Summary) ? definition.Description : definition.Summary),
                TabletUiHelpers.CreateInfoItem(
                    "Availability",
                    listing.IsActive
                        ? string.Format("Active mission | {0}", _missionManager.ActiveObjective)
                        : listing.AvailabilityDetail),
                TabletUiHelpers.CreateInfoItem(
                    "Description",
                    string.IsNullOrWhiteSpace(definition.Description)
                        ? "No extended mission description configured for this contract."
                        : definition.Description),
                TabletUiHelpers.CreateInfoItem(
                    "Completion",
                    definition.Repeatable
                        ? BuildMissionCompletionDetail(listing)
                        : (listing.CompletionCount > 0 ? "One-off contract already completed on this save." : "One-off contract not completed yet.")),
            };

            if (listing.IsActive)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Cancel Mission",
                    "Abandon the active contract and clean up its staged mission vehicles.",
                    () =>
                    {
                        _missionManager.CancelActiveMission();
                        context.Navigate(TabletAppIds.Missions, "root");
                    },
                    Color.FromArgb(182, 86, 54, 50),
                    Color.FromArgb(224, 194, 112, 102),
                    null,
                    "X"));
            }
            else if (_missionManager.HasActiveMission)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    "Another mission is active",
                    string.Format("Finish or cancel {0} before taking another special contract.", _missionManager.ActiveMissionName)));
            }
            else if (listing.CanAccept)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Accept Mission",
                    string.Format("Stage the mission vehicles and begin {0}.", definition.Name),
                    () =>
                    {
                        _missionManager.TryAcceptMission(definition.Id);
                        context.Refresh();
                    },
                    Color.FromArgb(182, 58, 82, 60),
                    Color.FromArgb(224, 128, 198, 150),
                    null,
                    "GO"));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    "Contract locked",
                    listing.AvailabilityDetail));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the mission board.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = definition.Name,
                Subtitle = "Special mission detail",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private static TabletShellPage BuildUnavailablePage(TabletStateSnapshot snapshot, string detail, Action goBack)
        {
            return new TabletShellPage
            {
                Title = "Special Missions",
                Subtitle = "Unavailable",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                Items = new[]
                {
                    TabletUiHelpers.CreateInfoItem("Unavailable", detail),
                    TabletUiHelpers.CreateNavigationItem("Back", "Return to the mission board.", goBack, "BACK"),
                },
            };
        }

        private static string BuildMissionCaption(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            if (listing.IsActive)
            {
                return string.Format("{0} ~y~[LIVE]~s~", listing.Name);
            }

            if (!listing.IsUnlocked)
            {
                return string.Format("{0} ~r~[LOCKED]~s~", listing.Name);
            }

            if (listing.CompletionCount > 0 && !listing.Repeatable)
            {
                return string.Format("{0} ~g~[DONE]~s~", listing.Name);
            }

            if (listing.RepeatCooldownRemainingMinutes > 0)
            {
                return string.Format("{0} ~r~[COOLDOWN]~s~", listing.Name);
            }

            if (listing.CompletionCount > 0 && listing.Repeatable)
            {
                return string.Format("{0} ~g~[x{1}]~s~", listing.Name, listing.CompletionCount);
            }

            return listing.Name;
        }

        private static string BuildMissionListDetail(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            if (listing.IsActive)
            {
                return string.Format("{0} | {1}", listing.Objective, listing.AvailabilityDetail);
            }

            var summary = string.IsNullOrWhiteSpace(listing.Summary)
                ? listing.Description
                : listing.Summary;
            var availability = string.IsNullOrWhiteSpace(listing.AvailabilityDetail)
                ? string.Empty
                : string.Format(" | {0}", listing.AvailabilityDetail);
            return string.Format(
                "Reward {0} | {1}{2}",
                ModFormatting.FormatMoney(listing.Reward),
                string.IsNullOrWhiteSpace(summary) ? "No briefing provided." : summary,
                availability);
        }

        private static string BuildMissionCompletionDetail(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return "Repeatable contract.";
            }

            var detail = string.Format("Repeatable contract | Completed {0} time(s)", listing.CompletionCount);
            if (listing.RepeatCooldownInGameMonths > 0 || listing.RepeatCooldownInGameMinutes > 0)
            {
                var cooldownLabel = FormatMissionCooldown(listing);
                detail += listing.RepeatCooldownRemainingMinutes > 0
                    ? string.Format(" | Next run in {0}", FormatMissionDuration(listing.RepeatCooldownRemainingMinutes))
                    : string.Format(" | Cooldown {0}", cooldownLabel);
            }

            return detail;
        }

        private static string FormatMissionCooldown(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            if (listing.RepeatCooldownInGameMonths > 0)
            {
                return listing.RepeatCooldownInGameMonths == 1
                    ? "1 in-game month"
                    : string.Format("{0} in-game months", listing.RepeatCooldownInGameMonths);
            }

            var totalMinutes = listing.RepeatCooldownInGameMinutes;
            const int minutesPerWeek = 7 * 24 * 60;
            if (totalMinutes > 0 && totalMinutes % minutesPerWeek == 0)
            {
                var weeks = totalMinutes / minutesPerWeek;
                return weeks == 1
                    ? "1 in-game week"
                    : string.Format("{0} in-game weeks", weeks);
            }

            return FormatMissionDuration(totalMinutes);
        }

        private static string FormatMissionDuration(int totalMinutes)
        {
            totalMinutes = Math.Max(0, totalMinutes);
            var days = totalMinutes / (24 * 60);
            var remainingMinutes = totalMinutes % (24 * 60);
            var hours = remainingMinutes / 60;
            var minutes = remainingMinutes % 60;
            var parts = new List<string>();

            if (days > 0)
            {
                parts.Add(string.Format("{0}d", days));
            }

            if (hours > 0)
            {
                parts.Add(string.Format("{0}h", hours));
            }

            if (minutes > 0 || parts.Count == 0)
            {
                parts.Add(string.Format("{0}m", minutes));
            }

            return string.Join(" ", parts.ToArray());
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
                    snapshot.CanInteractWithNearestIndustry ? "Open Site Operations" : "Review Site Detail",
                    snapshot.CanInteractWithNearestIndustry
                        ? "Jump straight into the nearest actionable site app."
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
        private readonly Action<Industry> _addGpsRoute;
        private readonly Action _clearGpsRoute;

        public NetworkTabletApp(float interactionDistance, Func<Industry, string> purchasePermit, Action<Industry> addGpsRoute, Action clearGpsRoute)
        {
            _interactionDistance = interactionDistance;
            _purchasePermit = purchasePermit;
            _addGpsRoute = addGpsRoute;
            _clearGpsRoute = clearGpsRoute;
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
                    return BuildLocationListPage(context, "Gas Stations", "Fuel storage coverage across service stations", context.Snapshot.GasStationSummaries, true, false);
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
                case "stats":
                    return BuildIndustryStatisticsPage(context, route != null ? route.Payload as Industry : null);
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

            return BuildLocationActionPage(context, snapshot, industry);
        }

        private TabletShellPage BuildLocationStatisticsPage(TabletShellContext context, Industry industry)
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

            if (industry.IsStore)
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    "Arrow Up/Down to scroll | Enter, Backspace, or Esc to return");
            }

            if (summary.LocationKind == LSOL.Config.ExternalLocationKind.Industry && industry.SiteRole != SiteRole.Warehouse)
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

        private TabletShellPage BuildLocationActionPage(TabletShellContext context, TabletStateSnapshot snapshot, Industry industry)
        {
            if (industry == null)
            {
                return new TabletShellPage
                {
                    Title = "Industry",
                    Subtitle = "No industry selected",
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem("No Industry Selected", "Return to the previous page and choose a valid industry."),
                        TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous page.", () => context.GoBack()),
                    },
                };
            }

            var title = industry.IsGasStation
                ? "Petrol Station"
                : (industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : (industry.IsStore ? "Store" : "Industry"));
            var statisticsDetail = industry.SiteRole == SiteRole.Warehouse
                ? "Open storage, accepted resources, and purchase status for this warehouse."
                : (industry.IsGasStation
                    ? "Open fuel-site storage and delivery statistics for this station."
                    : (industry.IsStore
                        ? "Open stockpile, utilization, and retail delivery statistics for this store."
                        : "Open stockpile, utilization, and per-commodity statistics for this industry."));

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "View Statistics",
                    statisticsDetail,
                    () => context.Push(TabletAppIds.Network, "stats", industry)),
                TabletUiHelpers.CreateActionItem(
                    "Add GPS Route",
                    "Set a map waypoint to this site so you can drive there directly.",
                    () => _addGpsRoute?.Invoke(industry)),
                TabletUiHelpers.CreateActionItem(
                    "Clear GPS Route",
                    "Remove the current waypoint from the map.",
                    () => _clearGpsRoute?.Invoke()),
                TabletUiHelpers.CreateNavigationItem("Back", "Return to the industry list.", () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = title,
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.84f,
                MaxVisibleItems = 5,
                Items = items,
            };
        }

        private TabletShellPage BuildIndustryStatisticsPage(TabletShellContext context, Industry industry)
        {
            return BuildLocationStatisticsPage(context, industry);
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
                Title = industry.IsGasStation
                    ? "Petrol Station"
                    : (industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry"),
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
                Title = "Site",
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
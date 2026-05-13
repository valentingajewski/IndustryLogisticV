using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Domain;

namespace LSOL.UI
{
    internal sealed class AnalyticsTabletApp : ITabletApp
    {
        public string AppId
        {
            get { return TabletAppIds.Analytics; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            TabletShellPage page;
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "profit":
                    page = BuildProfitPage(context);
                    break;
                case "commodity":
                    page = BuildCommodityPage(context);
                    break;
                case "utilization":
                    page = BuildUtilizationPage(context);
                    break;
                case "storage":
                    page = BuildStoragePage(context);
                    break;
                case "districts":
                    page = BuildDistrictPage(context);
                    break;
                case "routes":
                    page = BuildRoutesPage(context);
                    break;
                default:
                    page = BuildRootPage(context);
                    break;
            }

            ApplyAnalyticsPageStyle(page);
            return page;
        }

        private static void ApplyAnalyticsPageStyle(TabletShellPage page)
        {
            if (page == null)
            {
                return;
            }

            page.CaptionScale = 0.44f;
            page.DetailScale = 0.275f;
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
                    GetProfitAccent(214),
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
                TabletUiHelpers.CreateSelectorItem(
                    () =>
                    {
                        var selected = industries.FirstOrDefault(summary => string.Equals(summary.Industry.Id, context.StateStore.SelectedUtilizationIndustryId, StringComparison.OrdinalIgnoreCase));
                        return string.Format("Site: < {0} >", selected != null ? selected.Name : "None");
                    },
                    () => "Left/right changes the graphed site. Enter advances.",
                    () =>
                    {
                        context.StateStore.CycleSelectedUtilizationIndustry(-1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleSelectedUtilizationIndustry(1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleSelectedUtilizationIndustry(1);
                        context.Refresh();
                    },
                    "SITE"),
            };

            var selectedSummary = industries.FirstOrDefault(summary => string.Equals(summary.Industry.Id, context.StateStore.SelectedUtilizationIndustryId, StringComparison.OrdinalIgnoreCase));
            if (selectedSummary != null)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    string.Format("{0:0.0} t/h output", selectedSummary.OutputPerHourTons),
                    string.Format("Current utilization {0:0}% | Omega {1:0.0}/{2:0.0}t", selectedSummary.UtilizationPercent, selectedSummary.OmegaStorageTons, selectedSummary.OmegaCapacityTons)));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No industries tracked", "No production sites are currently available for utilization analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Site Utilization",
                Subtitle = "Use left/right to switch the industry shown in the graph",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Selectors | Enter Select | Backspace/Esc Back",
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

                    var summary = industries.FirstOrDefault(entry => string.Equals(entry.Industry.Id, context.StateStore.SelectedUtilizationIndustryId, StringComparison.OrdinalIgnoreCase))
                        ?? industries[0];
                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Utilization", summary.Name),
                        string.Format("Current {0:0}% | Output {1:0.0} t/h | Window {2}", summary.UtilizationPercent, summary.OutputPerHourTons, context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetSiteUtilizationHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        GetUtilizationAccent(214),
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
            var selectedSummary = siteSummaries.FirstOrDefault(summary => string.Equals(summary.Industry.Id, context.StateStore.SelectedStorageIndustryId, StringComparison.OrdinalIgnoreCase));
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, "Left/right changes the storage graph window."),
                TabletUiHelpers.CreateSelectorItem(
                    () => string.Format("Site: < {0} >", selectedSummary != null ? selectedSummary.Name : "None"),
                    () => "Left/right changes the graphed storage site. Enter advances.",
                    () =>
                    {
                        context.StateStore.CycleSelectedStorageIndustry(-1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleSelectedStorageIndustry(1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleSelectedStorageIndustry(1);
                        context.Refresh();
                    },
                    "SITE"),
            };

            if (selectedSummary != null)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    selectedSummary.Name,
                    string.Format(
                        "{0:0.0}/{1:0.0}t | {2:0}% full | {3}",
                        selectedSummary.StorageTons,
                        selectedSummary.TotalCapacityTons,
                        selectedSummary.FillRatio * 100f,
                        selectedSummary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry")));
            }
            else
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

                    var summary = siteSummaries.FirstOrDefault(entry => string.Equals(entry.Industry.Id, context.StateStore.SelectedStorageIndustryId, StringComparison.OrdinalIgnoreCase))
                        ?? siteSummaries[0];
                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        string.Format("{0} Storage Fill", summary.Name),
                        string.Format("Current {0:0}% | {1} | Window {2}", summary.FillRatio * 100f, summary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetSiteStorageHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        GetStorageAccent(214),
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
                            FillColor = GetDistrictAccent(210),
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
                            FillColor = GetRouteDeliveredAccent(214),
                        },
                        new TabletMetricBarEntry
                        {
                            Label = "Loss Ratio",
                            Ratio = route.LossRatioPercent / 100f,
                            ValueText = string.Format("{0:0}%", route.LossRatioPercent),
                            FillColor = GetRouteLossAccent(214),
                        },
                        new TabletMetricBarEntry
                        {
                            Label = "Average Payout",
                            Ratio = route.AveragePayout / maxPayout,
                            ValueText = ModFormatting.FormatMoney(route.AveragePayout),
                            FillColor = GetRoutePayoutAccent(214),
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
                        GetProfitAccent(214),
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
                GetCommodityAccent(214),
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
                GetCommodityAccent(214),
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
                GetUtilizationAccent(214),
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
                GetStorageAccent(214),
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
                        FillColor = GetDistrictAccent(210),
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
                        FillColor = GetRouteDeliveredAccent(210),
                    })
                    .ToArray());
        }

        private static Color GetProfitAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGreen, alpha);
        }

        private static Color GetCommodityAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentOrange, alpha);
        }

        private static Color GetUtilizationAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, alpha);
        }

        private static Color GetStorageAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentBlue, alpha);
        }

        private static Color GetDistrictAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentBlue, alpha);
        }

        private static Color GetRouteDeliveredAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, alpha);
        }

        private static Color GetRouteLossAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentOrange, alpha);
        }

        private static Color GetRoutePayoutAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentPurple, alpha);
        }
    }
}
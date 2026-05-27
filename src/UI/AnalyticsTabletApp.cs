using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class AnalyticsTabletApp : ITabletApp
    {
        private readonly Action _openRoutePlannerMap;
        private readonly Action<NpcLogisticsRouteDefinition> _openNpcPlannerDraft;

        public AnalyticsTabletApp(Action openRoutePlannerMap = null, Action<NpcLogisticsRouteDefinition> openNpcPlannerDraft = null)
        {
            _openRoutePlannerMap = openRoutePlannerMap;
            _openNpcPlannerDraft = openNpcPlannerDraft;
        }

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
                case "route-detail":
                    page = BuildRouteDetailPage(context, route != null ? route.Payload : null);
                    break;
                case "route-planner":
                    page = BuildRoutePlannerPage(context);
                    break;
                case "route-planner-detail":
                    page = BuildRoutePlannerDetailPage(context, route != null ? route.Payload as string : null);
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
                    "Compare district influence, charter status, competition pressure, and recurring territory burden.",
                    () => context.Push(TabletAppIds.Analytics, "districts"),
                    iconLabel: "DST"),
                TabletUiHelpers.CreateActionItem(
                    "NPC Routes",
                    "Delivered tons, loss ratio, average payout, and contract drill-down.",
                    () => context.Push(TabletAppIds.Analytics, "routes"),
                    iconLabel: "NPC"),
                TabletUiHelpers.CreateActionItem(
                    "Route Planner",
                    "Projected versus actual lane value, blockers, and optimizer ranking.",
                    () => context.Push(TabletAppIds.Analytics, "route-planner"),
                    iconLabel: "OPT"),
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
                    string.Format("{0} | Current {1} | Press Enter to set graph target.", price.CargoType.ToDisplayName(), ModFormatting.FormatPricePerTon(price.UnitPrice)),
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
                    string.Format("{0} output", ModFormatting.FormatRatePerHour(selectedSummary.OutputPerHourTons, "t")),
                    string.Format("Current utilization {0} | Omega {1}", ModFormatting.FormatPercent(selectedSummary.UtilizationPercent), ModFormatting.FormatRatio(selectedSummary.OmegaStorageTons, selectedSummary.OmegaCapacityTons, "t"))));
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
                        string.Format("Current {0} | Output {1} | Window {2}", ModFormatting.FormatPercent(summary.UtilizationPercent), ModFormatting.FormatRatePerHour(summary.OutputPerHourTons, "t"), context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                        context.StateStore.GetSiteUtilizationHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        GetUtilizationAccent(214),
                        value => ModFormatting.FormatPercent(value));
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
                    BuildStorageStatusDetail(selectedSummary)));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No storage sites tracked", "No industry or warehouse sites are currently available for storage analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Storage Fill",
                Subtitle = "Select a warehouse or industry to review fill, condition, and loss risk",
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
                        BuildStorageTrendSubtitle(context, summary),
                        context.StateStore.GetSiteStorageHistory(summary.Industry, context.StateStore.SelectedGraphTimeframe),
                        GetStorageAccent(214),
                        value => ModFormatting.FormatPercent(value));
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
                    string.Format(
                        "{0} influence | {1} | Charter {2}{3}{4}{5} | Comp {6} / Opp {7} | Lanes {8}{9}{10}",
                        ModFormatting.FormatPercent(district.InfluencePercent),
                        district.ReputationLabel,
                        district.LicenseStatus,
                        district.LicenseTargetTons > 0.01f
                            ? string.Format(" {0:0}/{1:0} t", district.LicenseActivityTons, district.LicenseTargetTons)
                            : string.Empty,
                        district.WeeklyOperationsCost > 0.01f
                            ? string.Format(" | Ops {0}/wk", ModFormatting.FormatMoney(district.WeeklyOperationsCost))
                            : " | Ops clear",
                        district.CorridorRiskCount > 0 || district.ServiceRiskCount > 0
                            ? string.Format(" | Risk C{0}/S{1}", district.CorridorRiskCount, district.ServiceRiskCount)
                            : string.Empty,
                        ModFormatting.FormatPercent(district.CompetitivePressurePercent),
                        ModFormatting.FormatPercent(district.CompetitiveOpportunityPercent),
                        district.ContestedCorridorCount,
                        !string.IsNullOrWhiteSpace(district.HottestCorridorName)
                            ? string.Format(" | Hot lane {0} {1}", district.HottestCorridorName, ModFormatting.FormatPercent(district.HottestCorridorPressurePercent))
                            : string.Empty,
                        !string.IsNullOrWhiteSpace(district.DistrictEventHeadline)
                            ? string.Format(" | Event {0}", district.DistrictEventHeadline)
                            : string.Empty)));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No districts tracked", "District telemetry is not currently available."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "District Influence",
                Subtitle = "Compare influence across tracked districts and the cost of holding them",
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
                            ValueText = ModFormatting.FormatPercent(district.InfluencePercent),
                            FillColor = GetDistrictAccent(210),
                        })
                        .ToArray();

                    TabletChartRenderer.DrawComparisonBarsPanel(
                        panel,
                        "District Influence Comparison",
                        string.Format(
                            "{0} | {1} rep | Charter {2} | {3} controlled sites | {4}{5} | Comp {6} / Opp {7} | Wins {8} | Lanes {9} / Holds {10}{11}{12}",
                            selectedDistrict.DistrictName,
                            selectedDistrict.ReputationLabel,
                            selectedDistrict.LicenseStatus,
                            selectedDistrict.ControlledSites,
                            selectedDistrict.WeeklyOperationsCost > 0.01f
                                ? string.Format("Ops {0}/wk", ModFormatting.FormatMoney(selectedDistrict.WeeklyOperationsCost))
                                : "Ops clear",
                            selectedDistrict.CorridorRiskCount > 0 || selectedDistrict.ServiceRiskCount > 0
                                ? string.Format(" | Risk C{0}/S{1}", selectedDistrict.CorridorRiskCount, selectedDistrict.ServiceRiskCount)
                                : string.Empty,
                            ModFormatting.FormatPercent(selectedDistrict.CompetitivePressurePercent),
                            ModFormatting.FormatPercent(selectedDistrict.CompetitiveOpportunityPercent),
                            selectedDistrict.CompetitiveWinCount,
                            selectedDistrict.ContestedCorridorCount,
                            selectedDistrict.CorridorHoldCount,
                            !string.IsNullOrWhiteSpace(selectedDistrict.HottestCorridorName)
                                ? string.Format(" | Hot lane {0} {1}", selectedDistrict.HottestCorridorName, ModFormatting.FormatPercent(selectedDistrict.HottestCorridorPressurePercent))
                                : string.Empty,
                            !string.IsNullOrWhiteSpace(selectedDistrict.DistrictEventHeadline)
                                ? string.Format(" | Event {0}", selectedDistrict.DistrictEventHeadline)
                                : string.Empty),
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
                var contractId = route.ContractId;
                items.Add(TabletUiHelpers.CreateActionItem(
                    route.Label,
                    string.Format(
                        "{0} | {1} delivered | {2} loss | Avg {3}",
                        string.IsNullOrWhiteSpace(route.FamilyLabel) ? "Route" : route.FamilyLabel,
                        ModFormatting.FormatTons(route.DeliveredTons),
                        ModFormatting.FormatPercent(route.LossRatioPercent),
                        ModFormatting.FormatMoney(route.AveragePayout)),
                    () => context.Push(TabletAppIds.Analytics, "route-detail", contractId),
                    iconLabel: "NPC"));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No NPC routes active", "Hire an NPC route to begin route-performance analytics."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "NPC Route Performance",
                Subtitle = "Select a route to inspect contract metrics, family rollup, and chain detail",
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
                            ValueText = ModFormatting.FormatTons(route.DeliveredTons),
                            FillColor = GetRouteDeliveredAccent(214),
                        },
                        new TabletMetricBarEntry
                        {
                            Label = "Loss Ratio",
                            Ratio = route.LossRatioPercent / 100f,
                            ValueText = ModFormatting.FormatPercent(route.LossRatioPercent),
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

        private static TabletShellPage BuildRouteDetailPage(TabletShellContext context, object payload)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var detail = context.StateStore.GetNpcRouteDrilldown(ResolveRouteContractId(payload));
            if (detail == null)
            {
                return new TabletShellPage
                {
                    Title = "NPC Route Detail",
                    Subtitle = "Selected NPC contract was not found",
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    WidthScale = 0.94f,
                    MaxVisibleItems = 6,
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem("Route unavailable", "The selected NPC contract no longer exists or has not been recorded yet."),
                        TabletUiHelpers.CreateNavigationItem("Back", "Return to NPC Routes.", () => context.GoBack(), "BACK"),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Route State",
                    NpcRouteProfitabilityFormatter.BuildRouteStateDetail(detail)),
                TabletUiHelpers.CreateInfoItem(
                    "Performance",
                    string.Format(
                        "{0} delivered | {1} loss | Avg {2} | {3} deliveries",
                        ModFormatting.FormatTons(detail.DeliveredTons),
                        ModFormatting.FormatPercent(detail.LossRatioPercent),
                        ModFormatting.FormatMoney(detail.AveragePayout),
                        Math.Max(0, detail.CompletedDeliveries))),
                TabletUiHelpers.CreateInfoItem(
                    "Budget Impact",
                    string.Format(
                        "Net {0} | Revenue {1} | Cost {2}",
                        detail.NetProfit >= 0f
                            ? "+" + ModFormatting.FormatMoney(detail.NetProfit)
                            : "-" + ModFormatting.FormatMoney(Math.Abs(detail.NetProfit)),
                        ModFormatting.FormatMoney(detail.Revenue),
                        ModFormatting.FormatMoney(detail.OperatingCost))),
                TabletUiHelpers.CreateInfoItem(
                    detail.RouteFamily != null && !string.IsNullOrWhiteSpace(detail.RouteFamily.Label)
                        ? detail.RouteFamily.Label
                        : "Contract Family",
                    NpcRouteProfitabilityFormatter.BuildFamilyDetail(detail.RouteFamily)),
            };

            if (detail.RouteLegs != null && detail.RouteLegs.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Route Chain", "Current and queued legs in this NPC contract."));
                for (int i = 0; i < detail.RouteLegs.Count; i++)
                {
                    var leg = detail.RouteLegs[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        NpcRouteProfitabilityFormatter.BuildLegCaption(leg),
                        NpcRouteProfitabilityFormatter.BuildLegDetail(leg)));
                }
            }

            if (detail.RecentFinanceEntries != null && detail.RecentFinanceEntries.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Recent Route Finance", "Latest route-specific income and payroll entries from the company ledger."));
                for (int i = 0; i < detail.RecentFinanceEntries.Count; i++)
                {
                    var entry = detail.RecentFinanceEntries[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        NpcRouteProfitabilityFormatter.BuildFinanceCaption(entry),
                        NpcRouteProfitabilityFormatter.BuildFinanceDetail(entry)));
                }
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    "No recent finance history",
                    "This contract has no route-specific delivery or payroll entries recorded yet."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to NPC Routes.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "NPC Route Detail",
                Subtitle = detail.Label,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildRoutePlannerPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var candidates = context.StateStore.GetRoutePlannerCandidates().ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateRoutePlannerSortSelectorItem(context, "Left/right changes how route candidates are ranked."),
                TabletUiHelpers.CreateRoutePlannerAvailabilitySelectorItem(context, "Left/right narrows the planner by lane state."),
                TabletUiHelpers.CreateRoutePlannerCommoditySelectorItem(context, "Left/right narrows the planner by commodity family."),
                TabletUiHelpers.CreateRoutePlannerDistrictSelectorItem(context, "Left/right narrows the planner by districts touched by the lane."),
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var candidateId = candidate.CandidateId;
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                    TabletUiHelpers.BuildRoutePlannerAnalyticsDetail(candidate),
                    () =>
                    {
                        context.StateStore.SetSelectedRoutePlannerCandidate(candidateId);
                        context.Push(TabletAppIds.Analytics, "route-planner-detail", candidateId);
                    },
                    iconLabel: "OPT"));
            }

            if (candidates.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    "No planner lanes match",
                    "Change the sort or filters, unlock more districts, or grow a commodity surplus to surface lane candidates."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the analytics hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Route Planner",
                Subtitle = "Projected versus actual lane value, blockers, and optimizer ranking",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Up/Down Navigate | Left/Right Change Filters | Enter Inspect Lane | Backspace/Esc Back",
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawRoutePlannerPanel(panel, context),
                Items = items,
            };
        }

        private TabletShellPage BuildRoutePlannerDetailPage(TabletShellContext context, string candidateId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var candidate = context.StateStore.GetRoutePlannerCandidate(candidateId);
            if (candidate == null)
            {
                return new TabletShellPage
                {
                    Title = "Route Planner",
                    Subtitle = "Lane unavailable",
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem("Lane unavailable", "Return to Route Planner and choose a lane that still exists in the current network state."),
                        TabletUiHelpers.CreateNavigationItem("Back", "Return to Route Planner.", () => context.GoBack(), "BACK"),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Planner state",
                    string.Format(
                        "{0} | {1} | Corridor {2}",
                        candidate.AvailabilityLabel,
                        string.IsNullOrWhiteSpace(candidate.DistrictPairLabel) ? "District unknown" : candidate.DistrictPairLabel,
                        string.IsNullOrWhiteSpace(candidate.CorridorId) ? "n/a" : candidate.CorridorId)),
                TabletUiHelpers.CreateInfoItem(
                    "Projection",
                    TabletUiHelpers.BuildRoutePlannerProjectionDetail(candidate)),
                TabletUiHelpers.CreateInfoItem(
                    "Live comparison",
                    TabletUiHelpers.BuildRoutePlannerPerformanceDetail(candidate)),
            };

            if (candidate.IsUnderperformingActiveLane)
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    "Underperforming live lane",
                    "This contract is running behind the planner estimate or is currently losing money."));
            }

            items.Add(TabletUiHelpers.CreateInfoItem(
                candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked ? "Blocker" : "Planner status",
                TabletUiHelpers.BuildRoutePlannerBlockerDetail(candidate)));

            items.Add(TabletUiHelpers.CreateInfoItem(
                candidate.RouteFamily != null && !string.IsNullOrWhiteSpace(candidate.RouteFamily.Label)
                    ? candidate.RouteFamily.Label
                    : "Route family",
                candidate.RouteFamily != null && !string.IsNullOrWhiteSpace(candidate.RouteFamily.Label)
                    ? NpcRouteProfitabilityFormatter.BuildFamilyDetail(candidate.RouteFamily)
                    : "No route-family history is recorded for this commodity pattern yet."));

            if (candidate.MatchingContractId > 0)
            {
                var contractId = candidate.MatchingContractId;
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Open Active NPC Contract",
                    string.IsNullOrWhiteSpace(candidate.MatchingContractLabel)
                        ? "Inspect the current NPC lane using the contract drill-down."
                        : candidate.MatchingContractLabel,
                    () => context.Push(TabletAppIds.Analytics, "route-detail", contractId),
                    iconLabel: "NPC"));
            }

            if (_openRoutePlannerMap != null)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Show on Company Map",
                    "Open the company map with this planner lane highlighted in the overlay.",
                    () =>
                    {
                        context.StateStore.SetSelectedRoutePlannerCandidate(candidate.CandidateId);
                        context.Refresh();
                        _openRoutePlannerMap();
                    },
                    iconLabel: "MAP"));
            }

            if (_openNpcPlannerDraft != null)
            {
                if (candidate.CanDraftNpcRoute)
                {
                    var draftDefinition = TabletUiHelpers.BuildRoutePlannerDraftDefinition(candidate);
                    items.Add(TabletUiHelpers.CreateActionItem(
                        "Draft NPC Lane",
                        "Seed the Hire NPC flow from this analytics-backed lane recommendation.",
                        () => _openNpcPlannerDraft(draftDefinition),
                        iconLabel: "NPC"));
                }
                else
                {
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        "NPC draft locked",
                        TabletUiHelpers.BuildRoutePlannerBlockerDetail(candidate)));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Route Planner.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Route Planner",
                Subtitle = TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private static int ResolveRouteContractId(object payload)
        {
            if (payload is int)
            {
                return (int)payload;
            }

            var raw = payload as string;
            int parsed;
            return !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed)
                ? parsed
                : 0;
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
                case 6:
                    DrawRoutePlannerPreview(panel, context);
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
                BuildCommodityTrendSubtitle(
                    context,
                    selectedPrice.Commodity,
                    string.Format(
                        "{0} cargo | Window {1} | Current {2}",
                        selectedPrice.CargoType.ToDisplayName(),
                        context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                        ModFormatting.FormatPricePerTon(selectedPrice.UnitPrice))),
                context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                GetCommodityAccent(214),
                value => ModFormatting.FormatPricePerTon(value));
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
                BuildCommodityTrendSubtitle(
                    context,
                    commodity,
                    string.Format(
                        "Window {0} | Current {1} | Press Enter to browse resources",
                        context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                        currentPrice != null ? ModFormatting.FormatPricePerTon(currentPrice.UnitPrice) : "price board")),
                context.StateStore.GetCommodityPriceHistory(commodity, context.StateStore.SelectedGraphTimeframe),
                GetCommodityAccent(214),
                value => ModFormatting.FormatPricePerTon(value));
        }

        private static string BuildCommodityTrendSubtitle(TabletShellContext context, string commodity, string baseSubtitle)
        {
            var shockSummary = context != null && context.StateStore != null
                ? context.StateStore.GetCommodityShockSummary(commodity)
                : string.Empty;
            return string.IsNullOrWhiteSpace(shockSummary)
                ? baseSubtitle
                : string.Format("{0} | {1}", baseSubtitle, shockSummary);
        }

        private static string BuildStorageStatusDetail(TabletLocationSummary summary)
        {
            if (summary == null || summary.Industry == null)
            {
                return "No storage data.";
            }

            var baseDetail = string.Format(
                "{0} | {1} full | {2}",
                ModFormatting.FormatRatio(summary.StorageTons, summary.TotalCapacityTons, "t"),
                ModFormatting.FormatPercent(summary.FillRatio * 100f),
                summary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry");
            if (summary.Industry.SiteRole != SiteRole.Warehouse)
            {
                return baseDetail;
            }

            var warehouseRisk = summary.WarehouseRisk;
            var detail = string.Format("{0} | Condition {1:0}%", baseDetail, Math.Max(0f, Math.Min(100f, summary.Industry.StorageCondition * 100f)));
            if (warehouseRisk == null)
            {
                return detail;
            }

            detail += string.Format(
                " | Risk {0} | Week {1} | Next {2}",
                TabletUiHelpers.BuildWarehouseRiskLevel(warehouseRisk),
                ModFormatting.FormatMoney(warehouseRisk.CurrentWeekTotalLossValue),
                ModFormatting.FormatMoney(warehouseRisk.ProjectedNextDayLossValue));
            if (!string.IsNullOrWhiteSpace(warehouseRisk.DominantCommodity))
            {
                detail += string.Format(" | {0}", TabletUiHelpers.BuildWarehouseFocusLabel(warehouseRisk));
            }

            return detail;
        }

        private static string BuildStorageTrendSubtitle(TabletShellContext context, TabletLocationSummary summary)
        {
            if (summary == null || summary.Industry == null)
            {
                return string.Format("Window {0}", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel());
            }

            var baseSubtitle = string.Format(
                "Current {0} | {1} | Window {2}",
                ModFormatting.FormatPercent(summary.FillRatio * 100f),
                summary.Industry.SiteRole == SiteRole.Warehouse ? "Warehouse" : "Industry",
                context.StateStore.SelectedGraphTimeframe.ToDisplayLabel());
            if (summary.Industry.SiteRole != SiteRole.Warehouse)
            {
                return baseSubtitle;
            }

            var subtitle = string.Format("{0} | Condition {1:0}%", baseSubtitle, Math.Max(0f, Math.Min(100f, summary.Industry.StorageCondition * 100f)));
            var warehouseRisk = summary.WarehouseRisk;
            if (warehouseRisk == null)
            {
                return subtitle;
            }

            subtitle += string.Format(
                " | Risk {0} | Next {1}",
                TabletUiHelpers.BuildWarehouseRiskLevel(warehouseRisk),
                ModFormatting.FormatMoney(warehouseRisk.ProjectedNextDayLossValue));
            return subtitle;
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
                value => ModFormatting.FormatPercent(value));
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
                value => ModFormatting.FormatPercent(value));
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
                        ValueText = ModFormatting.FormatPercent(district.InfluencePercent),
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
                        ValueText = ModFormatting.FormatTons(route.DeliveredTons),
                        FillColor = GetRouteDeliveredAccent(210),
                    })
                    .ToArray());
        }

        private static void DrawRoutePlannerPreview(SimpleMenuTabletPanelContext panel, TabletShellContext context)
        {
            var candidate = context.StateStore.GetSelectedRoutePlannerCandidate();
            if (candidate == null)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Route Planner", "No planner lanes available.", "Press Enter after route-planning data is available to compare projected and actual lane performance.");
                return;
            }

            var entries = new[]
            {
                new TabletMetricBarEntry
                {
                    Label = "Projected Payout",
                    Ratio = 1f,
                    ValueText = ModFormatting.FormatMoney(candidate.ProjectedPayout),
                    FillColor = GetRoutePayoutAccent(214),
                },
                new TabletMetricBarEntry
                {
                    Label = "Actual Avg",
                    Ratio = candidate.ProjectedPayout > 0.001f ? Math.Min(1f, candidate.RealizedAveragePayout / candidate.ProjectedPayout) : 0f,
                    ValueText = candidate.HasActiveNpcRoute || candidate.HasRouteFamilyHistory
                        ? ModFormatting.FormatMoney(candidate.RealizedAveragePayout)
                        : "n/a",
                    FillColor = GetRouteDeliveredAccent(214),
                },
                new TabletMetricBarEntry
                {
                    Label = "Optimizer",
                    Ratio = Math.Max(0f, Math.Min(1f, candidate.OptimizerScore / 100f)),
                    ValueText = candidate.OptimizerScore.ToString("0.0"),
                    FillColor = GetRoutePlannerAccent(214),
                },
            };

            TabletChartRenderer.DrawMetricBarsPanel(
                panel,
                TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                TabletUiHelpers.BuildRoutePlannerAnalyticsDetail(candidate),
                entries);
        }

        private static void DrawRoutePlannerPanel(SimpleMenuTabletPanelContext panel, TabletShellContext context)
        {
            var candidates = context.StateStore.GetRoutePlannerCandidates().ToList();
            if (candidates.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Route Planner", "No planner lanes match the current filters.", "Relax the planner filters or unlock more route endpoints.");
                return;
            }

            var selectedIndex = GetGraphListSelectionIndex(panel.SelectedIndex, 4, candidates.Count);
            var candidate = candidates[selectedIndex];
            var maxProjected = Math.Max(1f, candidates.Max(entry => entry.ProjectedPayout));
            var maxActual = Math.Max(1f, candidates.Max(entry => entry.RealizedAveragePayout));
            var maxScore = Math.Max(1f, candidates.Max(entry => entry.OptimizerScore));
            var entries = new[]
            {
                new TabletMetricBarEntry
                {
                    Label = "Projected Payout",
                    Ratio = candidate.ProjectedPayout / maxProjected,
                    ValueText = ModFormatting.FormatMoney(candidate.ProjectedPayout),
                    FillColor = GetRoutePayoutAccent(214),
                },
                new TabletMetricBarEntry
                {
                    Label = "Actual Avg",
                    Ratio = candidate.HasActiveNpcRoute || candidate.HasRouteFamilyHistory ? (candidate.RealizedAveragePayout / maxActual) : 0f,
                    ValueText = candidate.HasActiveNpcRoute || candidate.HasRouteFamilyHistory
                        ? ModFormatting.FormatMoney(candidate.RealizedAveragePayout)
                        : "n/a",
                    FillColor = GetRouteDeliveredAccent(214),
                },
                new TabletMetricBarEntry
                {
                    Label = "Optimizer",
                    Ratio = candidate.OptimizerScore / maxScore,
                    ValueText = candidate.OptimizerScore.ToString("0.0"),
                    FillColor = GetRoutePlannerAccent(214),
                },
            };

            TabletChartRenderer.DrawMetricBarsPanel(
                panel,
                TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                TabletUiHelpers.BuildRoutePlannerAnalyticsDetail(candidate),
                entries);
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

        private static Color GetRoutePlannerAccent(int alpha)
        {
            return AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentBlue, alpha);
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
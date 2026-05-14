using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class TabletBudgetOverview
    {
        public float CurrentBalance { get; set; }

        public float DailyNet { get; set; }

        public float WeeklyNet { get; set; }

        public float WeeklyIncome { get; set; }

        public float WeeklyExpenses { get; set; }

        public float UpcomingBills { get; set; }

        public TabletBudgetForecast Forecast { get; set; }
    }

    internal sealed class TabletBudgetBreakdownEntry
    {
        public CompanyFinanceCategory Category { get; set; }

        public string Label { get; set; }

        public float DayTotal { get; set; }

        public float WeekTotal { get; set; }
    }

    internal sealed class TabletUpcomingBillEntry
    {
        public CompanyFinanceCategory Category { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public float Amount { get; set; }

        public int DueInMinutes { get; set; }
    }

    internal sealed class TabletBudgetForecast
    {
        public float CurrentBalance { get; set; }

        public float EstimatedIncome { get; set; }

        public float KnownBills { get; set; }

        public float ProjectedEndingBalance { get; set; }

        public float LowestProjectedBalance { get; set; }

        public bool TurnsNegative { get; set; }
    }

    internal sealed class TabletBudgetRouteEntry
    {
        public string Label { get; set; }

        public string Detail { get; set; }

        public float Revenue { get; set; }

        public float OperatingCost { get; set; }

        public float NetProfit { get; set; }

        public int CompletedDeliveries { get; set; }

        public float DeliveredTons { get; set; }
    }

    internal sealed class TabletInventoryValueEntry
    {
        public string Label { get; set; }

        public string Detail { get; set; }

        public float Value { get; set; }
    }

    internal sealed class TabletInventoryValuation
    {
        public TabletInventoryValuation()
        {
            TopLocations = Array.Empty<TabletInventoryValueEntry>();
            TopCommodities = Array.Empty<TabletInventoryValueEntry>();
        }

        public float TotalValue { get; set; }

        public IReadOnlyList<TabletInventoryValueEntry> TopLocations { get; set; }

        public IReadOnlyList<TabletInventoryValueEntry> TopCommodities { get; set; }
    }

    internal sealed class BudgetTabletApp : ITabletApp
    {
        public string AppId
        {
            get { return TabletAppIds.Budget; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            TabletShellPage page;
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "overview":
                    page = BuildOverviewPage(context);
                    break;
                case "expenses":
                    page = BuildExpensesPage(context);
                    break;
                case "income":
                    page = BuildIncomePage(context);
                    break;
                case "bills":
                    page = BuildBillsPage(context);
                    break;
                case "forecast":
                    page = BuildForecastPage(context);
                    break;
                case "routes":
                    page = BuildRoutesPage(context);
                    break;
                case "inventory":
                    page = BuildInventoryPage(context);
                    break;
                default:
                    page = BuildRootPage(context);
                    break;
            }

            ApplyPageStyle(page);
            return page;
        }

        private static void ApplyPageStyle(TabletShellPage page)
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
            var overview = context.StateStore.GetBudgetOverview() ?? new TabletBudgetOverview();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    "Cash Summary",
                    string.Format("Balance {0}\n7d net {1}", ModFormatting.FormatMoney(overview.CurrentBalance), FormatSignedMoney(overview.WeeklyNet)),
                    () => context.Push(TabletAppIds.Budget, "overview"),
                    iconLabel: "CSH"),
                TabletUiHelpers.CreateActionItem(
                    "Expense Breakdown",
                    string.Format("7d expenses {0}\n24h net {1}", ModFormatting.FormatMoney(overview.WeeklyExpenses), FormatSignedMoney(overview.DailyNet)),
                    () => context.Push(TabletAppIds.Budget, "expenses"),
                    iconLabel: "EXP"),
                TabletUiHelpers.CreateActionItem(
                    "Income Breakdown",
                    string.Format("7d income {0}\nRecent inflows by source", ModFormatting.FormatMoney(overview.WeeklyIncome)),
                    () => context.Push(TabletAppIds.Budget, "income"),
                    iconLabel: "INC"),
                TabletUiHelpers.CreateActionItem(
                    "Upcoming Bills",
                    string.Format("Known dues {0}\nRent, payroll, and rentals", ModFormatting.FormatMoney(overview.UpcomingBills)),
                    () => context.Push(TabletAppIds.Budget, "bills"),
                    iconLabel: "BIL"),
                TabletUiHelpers.CreateActionItem(
                    "Weekly Forecast",
                    BuildForecastTileDetail(overview.Forecast),
                    () => context.Push(TabletAppIds.Budget, "forecast"),
                    iconLabel: "FRC"),
                TabletUiHelpers.CreateActionItem(
                    "Route Profitability",
                    "Review NPC route revenue against contract and payroll costs.",
                    () => context.Push(TabletAppIds.Budget, "routes"),
                    iconLabel: "RTE"),
                TabletUiHelpers.CreateActionItem(
                    "Inventory Value",
                    "Estimate on-hand cargo, stockpiles, and office fuel value.",
                    () => context.Push(TabletAppIds.Budget, "inventory"),
                    iconLabel: "INV"),
                TabletUiHelpers.CreateNavigationItem(
                    "Back",
                    "Return to the company hub.",
                    () => context.GoBack(),
                    "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Budget",
                Subtitle = "Cashflow, liabilities, forecasts, routes, and inventory value",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Keys Navigate | Enter Open Detail | Backspace/Esc Back",
                WidthScale = 0.94f,
                Layout = SimpleMenuTabletLayout.Dashboard,
                DashboardSidebarCount = 0,
                DashboardTileColumns = 3,
                BottomPanelHeight = 156f,
                BottomPanelRenderer = panel => DrawOverviewBars(panel, overview, "Budget Snapshot"),
                Items = items,
            };
        }

        private static TabletShellPage BuildOverviewPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var overview = context.StateStore.GetBudgetOverview() ?? new TabletBudgetOverview();
            var forecast = overview.Forecast ?? new TabletBudgetForecast();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Cash On Hand",
                    string.Format("Current balance {0}", ModFormatting.FormatMoney(overview.CurrentBalance))),
                TabletUiHelpers.CreateInfoItem(
                    "24h Net",
                    string.Format("Net {0}", FormatSignedMoney(overview.DailyNet))),
                TabletUiHelpers.CreateInfoItem(
                    "7d Net",
                    string.Format("Income {0} | Expenses {1} | Net {2}", ModFormatting.FormatMoney(overview.WeeklyIncome), ModFormatting.FormatMoney(overview.WeeklyExpenses), FormatSignedMoney(overview.WeeklyNet))),
                TabletUiHelpers.CreateInfoItem(
                    "Known Bills",
                    string.Format("Next 7d obligations {0}", ModFormatting.FormatMoney(overview.UpcomingBills))),
                TabletUiHelpers.CreateInfoItem(
                    "Forecast End Balance",
                    string.Format("Projected {0} | Lowest point {1}", ModFormatting.FormatMoney(forecast.ProjectedEndingBalance), ModFormatting.FormatMoney(forecast.LowestProjectedBalance))),
                TabletUiHelpers.CreateInfoItem(
                    "Cash Posture",
                    BuildHealthDetail(overview)),
                TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Cash Summary",
                Subtitle = "Current cash, recent net flow, and expected short-term pressure",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawOverviewBars(panel, overview, "Cash Summary"),
                Items = items,
            };
        }

        private static TabletShellPage BuildExpensesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var entries = context.StateStore.GetExpenseBreakdown().ToList();
            var items = BuildBreakdownItems(entries, "No tracked company expenses yet.");
            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Expense Breakdown",
                Subtitle = "24h and 7d spending grouped by budget category",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawBreakdownBars(panel, "Weekly Expense Mix", entries, GetExpenseAccent(210)),
                Items = items,
            };
        }

        private static TabletShellPage BuildIncomePage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var entries = context.StateStore.GetIncomeBreakdown().ToList();
            var items = BuildBreakdownItems(entries, "No tracked company income yet.");
            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Income Breakdown",
                Subtitle = "24h and 7d inflows grouped by source",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawBreakdownBars(panel, "Weekly Income Mix", entries, GetIncomeAccent(210)),
                Items = items,
            };
        }

        private static TabletShellPage BuildBillsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var bills = context.StateStore.GetUpcomingBills().ToList();
            var items = new List<MenuItem>();
            if (bills.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No bills queued", "No rent, payroll, or rental charges are currently scheduled."));
            }
            else
            {
                for (int i = 0; i < bills.Count; i++)
                {
                    var bill = bills[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        bill.DueInMinutes <= 0 ? string.Format("{0} ~r~DUE~s~", bill.Label) : bill.Label,
                        string.Format("{0} | Due {1} | {2}", ModFormatting.FormatMoney(bill.Amount), FormatDueInMinutes(bill.DueInMinutes), bill.Detail)));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Upcoming Bills",
                Subtitle = "Known rent, payroll, and rental obligations",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawBillsBars(panel, bills),
                Items = items,
            };
        }

        private static TabletShellPage BuildForecastPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var forecast = context.StateStore.GetWeeklyForecast() ?? new TabletBudgetForecast();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Current Balance",
                    string.Format("Starting from {0}", ModFormatting.FormatMoney(forecast.CurrentBalance))),
                TabletUiHelpers.CreateInfoItem(
                    "Estimated 7d Income",
                    string.Format("Recent income pace {0}", ModFormatting.FormatMoney(forecast.EstimatedIncome))),
                TabletUiHelpers.CreateInfoItem(
                    "Known Bills",
                    string.Format("Scheduled charges {0}", ModFormatting.FormatMoney(forecast.KnownBills))),
                TabletUiHelpers.CreateInfoItem(
                    "Projected Balance",
                    string.Format("End of week {0}", ModFormatting.FormatMoney(forecast.ProjectedEndingBalance))),
                TabletUiHelpers.CreateInfoItem(
                    "Lowest Expected Cash",
                    string.Format("Worst point {0} | {1}", ModFormatting.FormatMoney(forecast.LowestProjectedBalance), forecast.TurnsNegative ? "Forecast turns negative" : "Forecast stays above zero")),
                TabletUiHelpers.CreateInfoItem(
                    "Assumption",
                    "Forecast replays the last 7 in-game days of income against currently known bills only."),
                TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Weekly Forecast",
                Subtitle = "Recent income pace minus known obligations over the next week",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawForecastBars(panel, forecast),
                Items = items,
            };
        }

        private static TabletShellPage BuildRoutesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var routes = context.StateStore.GetBudgetRouteProfitability().ToList();
            var items = new List<MenuItem>();

            if (routes.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No route history", "Hire an NPC route to populate route profitability."));
            }
            else
            {
                for (int i = 0; i < routes.Count; i++)
                {
                    var route = routes[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        route.Label,
                        string.Format(
                            "Net {0} | Revenue {1} | Cost {2} | {3} deliveries",
                            FormatSignedMoney(route.NetProfit),
                            ModFormatting.FormatMoney(route.Revenue),
                            ModFormatting.FormatMoney(route.OperatingCost),
                            route.CompletedDeliveries)));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Route Profitability",
                Subtitle = "NPC route earnings against contract and payroll costs",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawRouteMetrics(panel, routes),
                Items = items,
            };
        }

        private static TabletShellPage BuildInventoryPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var valuation = context.StateStore.GetInventoryValuation() ?? new TabletInventoryValuation();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Total Inventory Value",
                    string.Format("Estimated on-hand value {0}", ModFormatting.FormatMoney(valuation.TotalValue))),
            };

            if (valuation.TopLocations.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Highest-Value Locations", "Where the most value is currently sitting."));
                for (int i = 0; i < valuation.TopLocations.Count; i++)
                {
                    var entry = valuation.TopLocations[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(entry.Label, string.Format("{0} | {1}", ModFormatting.FormatMoney(entry.Value), entry.Detail)));
                }
            }

            if (valuation.TopCommodities.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateBannerItem("Commodity Exposure", "The most valuable commodities on hand."));
                for (int i = 0; i < valuation.TopCommodities.Count; i++)
                {
                    var entry = valuation.TopCommodities[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(entry.Label, string.Format("{0} | {1}", ModFormatting.FormatMoney(entry.Value), entry.Detail)));
                }
            }

            if (items.Count == 1)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No inventory on hand", "Industries, vehicles, and office tanks are currently empty."));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Budget.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Inventory Value",
                Subtitle = "Estimated value of stockpiles, cargo, and office fuel",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 144f,
                BottomPanelRenderer = panel => DrawInventoryBars(panel, valuation),
                Items = items,
            };
        }

        private static List<MenuItem> BuildBreakdownItems(IReadOnlyList<TabletBudgetBreakdownEntry> entries, string emptyMessage)
        {
            var items = new List<MenuItem>();
            if (entries == null || entries.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No tracked data", emptyMessage));
                return items;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                items.Add(TabletUiHelpers.CreateInfoItem(
                    entry.Label,
                    string.Format("24h {0} | 7d {1}", ModFormatting.FormatMoney(entry.DayTotal), ModFormatting.FormatMoney(entry.WeekTotal))));
            }

            return items;
        }

        private static void DrawOverviewBars(SimpleMenuTabletPanelContext panel, TabletBudgetOverview overview, string title)
        {
            overview = overview ?? new TabletBudgetOverview();
            var forecast = overview.Forecast ?? new TabletBudgetForecast();
            var entries = new[]
            {
                new TabletBarEntry { Label = "Cash", Value = Math.Abs(overview.CurrentBalance), ValueText = ModFormatting.FormatMoney(overview.CurrentBalance), FillColor = GetIncomeAccent(214) },
                new TabletBarEntry { Label = "7d Income", Value = Math.Abs(overview.WeeklyIncome), ValueText = ModFormatting.FormatMoney(overview.WeeklyIncome), FillColor = GetIncomeAccent(214) },
                new TabletBarEntry { Label = "Bills", Value = Math.Abs(overview.UpcomingBills), ValueText = ModFormatting.FormatMoney(overview.UpcomingBills), FillColor = GetExpenseAccent(214) },
                new TabletBarEntry { Label = "Forecast", Value = Math.Abs(forecast.ProjectedEndingBalance), ValueText = ModFormatting.FormatMoney(forecast.ProjectedEndingBalance), FillColor = forecast.ProjectedEndingBalance >= 0f ? GetIncomeAccent(214) : GetExpenseAccent(214) },
            };

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                title,
                BuildHealthDetail(overview),
                entries);
        }

        private static void DrawBreakdownBars(SimpleMenuTabletPanelContext panel, string title, IReadOnlyList<TabletBudgetBreakdownEntry> entries, Color accent)
        {
            if (entries == null || entries.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, title, string.Empty, "The finance tracker has not recorded matching transactions yet.");
                return;
            }

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                title,
                "Weekly totals by category",
                entries
                    .OrderByDescending(entry => entry.WeekTotal)
                    .Take(5)
                    .Select(entry => new TabletBarEntry
                    {
                        Label = entry.Label,
                        Value = entry.WeekTotal,
                        ValueText = ModFormatting.FormatMoney(entry.WeekTotal),
                        FillColor = accent,
                    })
                    .ToArray());
        }

        private static void DrawBillsBars(SimpleMenuTabletPanelContext panel, IReadOnlyList<TabletUpcomingBillEntry> bills)
        {
            if (bills == null || bills.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Upcoming Bills", string.Empty, "No known obligations are currently scheduled.");
                return;
            }

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                "Upcoming Bills",
                "Known obligations sorted by soonest due time",
                bills
                    .OrderBy(entry => entry.DueInMinutes)
                    .ThenByDescending(entry => entry.Amount)
                    .Take(5)
                    .Select(entry => new TabletBarEntry
                    {
                        Label = entry.Label,
                        Value = entry.Amount,
                        ValueText = FormatDueInMinutes(entry.DueInMinutes),
                        FillColor = entry.DueInMinutes <= 0 ? GetExpenseAccent(220) : Color.FromArgb(214, 198, 146, 94),
                    })
                    .ToArray());
        }

        private static void DrawForecastBars(SimpleMenuTabletPanelContext panel, TabletBudgetForecast forecast)
        {
            forecast = forecast ?? new TabletBudgetForecast();
            var entries = new[]
            {
                new TabletBarEntry { Label = "Starting Cash", Value = Math.Abs(forecast.CurrentBalance), ValueText = ModFormatting.FormatMoney(forecast.CurrentBalance), FillColor = GetIncomeAccent(214) },
                new TabletBarEntry { Label = "Expected Income", Value = Math.Abs(forecast.EstimatedIncome), ValueText = ModFormatting.FormatMoney(forecast.EstimatedIncome), FillColor = GetIncomeAccent(214) },
                new TabletBarEntry { Label = "Known Bills", Value = Math.Abs(forecast.KnownBills), ValueText = ModFormatting.FormatMoney(forecast.KnownBills), FillColor = GetExpenseAccent(214) },
                new TabletBarEntry { Label = "Projected End", Value = Math.Abs(forecast.ProjectedEndingBalance), ValueText = ModFormatting.FormatMoney(forecast.ProjectedEndingBalance), FillColor = forecast.TurnsNegative ? GetExpenseAccent(214) : GetIncomeAccent(214) },
            };

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                "Weekly Forecast",
                forecast.TurnsNegative ? "Forecast drops below zero if recent income pace holds." : "Forecast stays above zero if recent income pace holds.",
                entries);
        }

        private static void DrawRouteMetrics(SimpleMenuTabletPanelContext panel, IReadOnlyList<TabletBudgetRouteEntry> routes)
        {
            if (routes == null || routes.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Route Profitability", string.Empty, "No NPC route profitability data is available yet.");
                return;
            }

            var selectedIndex = Math.Max(0, Math.Min(panel.SelectedIndex, routes.Count - 1));
            var route = routes[selectedIndex];
            var maxMetric = Math.Max(1f, routes.SelectMany(entry => new[] { Math.Abs(entry.Revenue), Math.Abs(entry.OperatingCost), Math.Abs(entry.NetProfit) }).Max());
            TabletChartRenderer.DrawMetricBarsPanel(
                panel,
                route.Label,
                string.Format("{0} | {1} moved", route.Detail, ModFormatting.FormatTons(route.DeliveredTons)),
                new[]
                {
                    new TabletMetricBarEntry { Label = "Revenue", Ratio = Math.Abs(route.Revenue) / maxMetric, ValueText = ModFormatting.FormatMoney(route.Revenue), FillColor = GetIncomeAccent(214) },
                    new TabletMetricBarEntry { Label = "Operating Cost", Ratio = Math.Abs(route.OperatingCost) / maxMetric, ValueText = ModFormatting.FormatMoney(route.OperatingCost), FillColor = GetExpenseAccent(214) },
                    new TabletMetricBarEntry { Label = "Net Profit", Ratio = Math.Abs(route.NetProfit) / maxMetric, ValueText = FormatSignedMoney(route.NetProfit), FillColor = route.NetProfit >= 0f ? GetIncomeAccent(214) : GetExpenseAccent(214) },
                });
        }

        private static void DrawInventoryBars(SimpleMenuTabletPanelContext panel, TabletInventoryValuation valuation)
        {
            valuation = valuation ?? new TabletInventoryValuation();
            if (valuation.TopLocations == null || valuation.TopLocations.Count == 0)
            {
                TabletChartRenderer.DrawMessagePanel(panel, "Inventory Value", string.Empty, "No stockpiles, cargo, or office fuel are currently valued.");
                return;
            }

            TabletChartRenderer.DrawComparisonBarsPanel(
                panel,
                "Inventory Value",
                string.Format("Top locations contributing to {0} on hand", ModFormatting.FormatMoney(valuation.TotalValue)),
                valuation.TopLocations
                    .Take(5)
                    .Select(entry => new TabletBarEntry
                    {
                        Label = entry.Label,
                        Value = entry.Value,
                        ValueText = ModFormatting.FormatMoney(entry.Value),
                        FillColor = Color.FromArgb(214, 118, 168, 198),
                    })
                    .ToArray());
        }

        private static string BuildForecastTileDetail(TabletBudgetForecast forecast)
        {
            forecast = forecast ?? new TabletBudgetForecast();
            return string.Format(
                "Projected {0}\n{1}",
                ModFormatting.FormatMoney(forecast.ProjectedEndingBalance),
                forecast.TurnsNegative ? "Cash dips below zero" : "Known bills stay covered");
        }

        private static string BuildHealthDetail(TabletBudgetOverview overview)
        {
            overview = overview ?? new TabletBudgetOverview();
            var forecast = overview.Forecast ?? new TabletBudgetForecast();
            if (forecast.TurnsNegative)
            {
                return "Forecasted cash dips below zero before the next week closes.";
            }

            if (overview.UpcomingBills > overview.CurrentBalance)
            {
                return "Known bills exceed current cash on hand.";
            }

            if (overview.WeeklyNet < 0f)
            {
                return "Current burn rate is negative, but scheduled bills remain covered.";
            }

            return "Recent income pace currently covers known obligations.";
        }

        private static string FormatSignedMoney(float amount)
        {
            return ModFormatting.FormatSignedMoney(amount);
        }

        private static string FormatDueInMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return "now";
            }

            var days = minutes / (24 * 60);
            var hours = (minutes % (24 * 60)) / 60;
            var remainingMinutes = minutes % 60;
            if (days > 0)
            {
                return hours > 0
                    ? string.Format("in {0}d {1}h", days, hours)
                    : string.Format("in {0}d", days);
            }

            if (hours > 0)
            {
                return remainingMinutes > 0
                    ? string.Format("in {0}h {1}m", hours, remainingMinutes)
                    : string.Format("in {0}h", hours);
            }

            return string.Format("in {0}m", remainingMinutes);
        }

        private static Color GetIncomeAccent(int alpha)
        {
            return Color.FromArgb(alpha, 112, 186, 146);
        }

        private static Color GetExpenseAccent(int alpha)
        {
            return Color.FromArgb(alpha, 198, 110, 106);
        }
    }
}
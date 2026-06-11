using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class TabletUiHelpers
    {
        private static readonly Color InfoIdle = Color.FromArgb(170, 38, 42, 46);
        private static readonly Color InfoActive = Color.FromArgb(205, 82, 94, 100);
        private static readonly Color ActionIdle = Color.FromArgb(170, 42, 48, 42);
        private static readonly Color ActionActive = Color.FromArgb(205, 88, 104, 90);
        private static readonly Color WarningIdle = Color.FromArgb(178, 64, 48, 40);
        private static readonly Color WarningActive = Color.FromArgb(208, 118, 88, 74);
        private static readonly Color NavigationIdle = Color.FromArgb(170, 48, 42, 52);
        private static readonly Color NavigationActive = Color.FromArgb(210, 106, 92, 126);
        private static readonly Color SelectorIdle = Color.FromArgb(178, 44, 50, 60);
        private static readonly Color SelectorActive = Color.FromArgb(222, 98, 112, 132);

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
                () => LocalizedText.Format(ModTextKey.TabletSelectorTimeframeCaption, context != null ? context.StateStore.SelectedGraphTimeframe.ToDisplayLabel() : TabletGraphTimeframe.ThirtyMinutes.ToDisplayLabel()),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorTimeframeDetail),
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
                () => LocalizedText.Format(ModTextKey.TabletSelectorTrendResourceCaption, context != null ? context.StateStore.SelectedTrendCommodity : LocalizedText.Get(ModTextKey.TabletSelectorTrendResourceNone)),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorTrendResourceDetail),
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

        public static MenuItem CreateRoutePlannerSortSelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => LocalizedText.Format(ModTextKey.TabletSelectorRoutePlannerSortCaption, context != null ? FormatRoutePlannerSortMode(context.StateStore.SelectedRoutePlannerSortMode) : FormatRoutePlannerSortMode(RoutePlannerSortMode.Optimizer)),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerSortDetail),
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerSortMode(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerSortMode(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerSortMode(1);
                    context.Refresh();
                },
                "SRT");
        }

        public static MenuItem CreateRoutePlannerAvailabilitySelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => LocalizedText.Format(ModTextKey.TabletSelectorRoutePlannerAvailabilityCaption, context != null ? FormatRoutePlannerAvailabilityFilter(context.StateStore.SelectedRoutePlannerAvailabilityFilterMode) : FormatRoutePlannerAvailabilityFilter(RoutePlannerAvailabilityFilterMode.All)),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerAvailabilityDetail),
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerAvailabilityFilter(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerAvailabilityFilter(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerAvailabilityFilter(1);
                    context.Refresh();
                },
                "FLT");
        }

        public static MenuItem CreateRoutePlannerCommoditySelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => LocalizedText.Format(ModTextKey.TabletSelectorRoutePlannerCommodityCaption, context != null && !string.IsNullOrWhiteSpace(context.StateStore.SelectedRoutePlannerCommodityFilter) ? context.StateStore.SelectedRoutePlannerCommodityFilter : LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerCommodityAny)),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerCommodityDetail),
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerCommodityFilter(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerCommodityFilter(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerCommodityFilter(1);
                    context.Refresh();
                },
                "COM");
        }

        public static MenuItem CreateRoutePlannerDistrictSelectorItem(TabletShellContext context, string detail = null)
        {
            return CreateSelectorItem(
                () => LocalizedText.Format(ModTextKey.TabletSelectorRoutePlannerDistrictCaption, context != null && !string.IsNullOrWhiteSpace(context.StateStore.SelectedRoutePlannerDistrictFilter) ? context.StateStore.SelectedRoutePlannerDistrictFilter : LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerDistrictAll)),
                () => detail ?? LocalizedText.Get(ModTextKey.TabletSelectorRoutePlannerDistrictDetail),
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerDistrictFilter(-1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerDistrictFilter(1);
                    context.Refresh();
                },
                () =>
                {
                    if (context == null)
                    {
                        return;
                    }

                    context.StateStore.CycleRoutePlannerDistrictFilter(1);
                    context.Refresh();
                },
                "DST");
        }

        public static string FormatRoutePlannerSortMode(RoutePlannerSortMode mode)
        {
            switch (mode)
            {
                case RoutePlannerSortMode.ProjectedPayout:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortProjectedPayout);
                case RoutePlannerSortMode.ProjectedValue:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortProjectedValue);
                case RoutePlannerSortMode.RealizedNetProfit:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortLiveNet);
                case RoutePlannerSortMode.UnitPrice:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortUnitPrice);
                case RoutePlannerSortMode.Commodity:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortCommodity);
                case RoutePlannerSortMode.District:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortDistrict);
                case RoutePlannerSortMode.Availability:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortAvailability);
                default:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerSortOptimizer);
            }
        }

        public static string FormatRoutePlannerAvailabilityFilter(RoutePlannerAvailabilityFilterMode mode)
        {
            switch (mode)
            {
                case RoutePlannerAvailabilityFilterMode.Available:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerAvailabilityAvailable);
                case RoutePlannerAvailabilityFilterMode.Blocked:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerAvailabilityBlocked);
                case RoutePlannerAvailabilityFilterMode.ActiveNpc:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerAvailabilityActiveNpc);
                case RoutePlannerAvailabilityFilterMode.Underperforming:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerAvailabilityUnderperforming);
                default:
                    return LocalizedText.Get(ModTextKey.TabletRoutePlannerAvailabilityAll);
            }
        }

        public static string BuildRoutePlannerCandidateCaption(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return LocalizedText.Get("tablet.routePlanner.candidateFallback");
            }

            var originName = candidate.OriginIndustry != null ? candidate.OriginIndustry.Name ?? LocalizedText.Get("tablet.routePlanner.origin") : LocalizedText.Get("tablet.routePlanner.origin");
            var destinationName = candidate.DestinationIndustry != null ? candidate.DestinationIndustry.Name ?? LocalizedText.Get("tablet.routePlanner.destination") : LocalizedText.Get("tablet.routePlanner.destination");
            return LocalizedText.Format(
                "tablet.routePlanner.caption",
                candidate.Commodity ?? LocalizedText.Get("tablet.routePlanner.cargo"),
                originName,
                destinationName,
                BuildRoutePlannerStatusTag(candidate)).Trim();
        }

        public static string BuildRoutePlannerNetworkDetail(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return LocalizedText.Get("tablet.routePlanner.noLaneData");
            }

            var liveLabel = candidate.HasActiveNpcRoute
                ? LocalizedText.Format("tablet.routePlanner.liveAverage", ModFormatting.FormatMoney(candidate.RealizedAveragePayout))
                : (candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked
                    ? candidate.BlockerSummary
                    : LocalizedText.Format("tablet.routePlanner.score", candidate.OptimizerScore));
            return LocalizedText.Format(
                "tablet.routePlanner.networkDetail",
                string.IsNullOrWhiteSpace(candidate.DistrictPairLabel) ? LocalizedText.Get("tablet.routePlanner.districtUnknown") : candidate.DistrictPairLabel,
                ModFormatting.FormatMoney(candidate.ProjectedPayout),
                liveLabel);
        }

        public static string BuildRoutePlannerAnalyticsDetail(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return LocalizedText.Get("tablet.routePlanner.noLaneData");
            }

            var delta = candidate.RealizedAveragePayout - candidate.ProjectedPayout;
            var deltaLabel = delta >= 0f
                ? "+" + ModFormatting.FormatMoney(delta)
                : "-" + ModFormatting.FormatMoney(Math.Abs(delta));
            var actualLabel = candidate.HasActiveNpcRoute || candidate.HasRouteFamilyHistory
                ? ModFormatting.FormatMoney(candidate.RealizedAveragePayout)
                : LocalizedText.Get("tablet.routePlanner.na");
            return LocalizedText.Format(
                "tablet.routePlanner.analyticsDetail",
                candidate.AvailabilityLabel,
                ModFormatting.FormatMoney(candidate.ProjectedPayout),
                actualLabel,
                deltaLabel);
        }

        public static string BuildRoutePlannerProjectionDetail(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            return LocalizedText.Format(
                "tablet.routePlanner.projectionDetail",
                ModFormatting.FormatPricePerTon(candidate.CurrentUnitPrice),
                ModFormatting.FormatTons(candidate.SuggestedShipmentTons),
                ModFormatting.FormatMoney(candidate.ProjectedValue),
                ModFormatting.FormatMoney(candidate.ProjectedPayout));
        }

        public static string BuildRoutePlannerPerformanceDetail(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            if (!candidate.HasActiveNpcRoute && !candidate.HasRouteFamilyHistory)
            {
                return LocalizedText.Format(
                    "tablet.routePlanner.noLiveHistory",
                    candidate.OptimizerScore);
            }

            return LocalizedText.Format(
                "tablet.routePlanner.performanceDetail",
                candidate.RealizedNetProfit >= 0f
                    ? "+" + ModFormatting.FormatMoney(candidate.RealizedNetProfit)
                    : "-" + ModFormatting.FormatMoney(Math.Abs(candidate.RealizedNetProfit)),
                ModFormatting.FormatMoney(candidate.RealizedAveragePayout),
                ModFormatting.FormatPercent(candidate.RealizedLossRatioPercent),
                candidate.OptimizerScore);
        }

        public static string BuildRoutePlannerBlockerDetail(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            if (candidate.AvailabilityState != RoutePlannerAvailabilityState.Blocked)
            {
                return candidate.HasActiveNpcRoute
                    ? LocalizedText.Get("tablet.routePlanner.activeNpcBlocker")
                    : LocalizedText.Get("tablet.routePlanner.availableForDraft");
            }

            return LocalizedText.Format(
                "tablet.routePlanner.blockerDetail",
                string.IsNullOrWhiteSpace(candidate.BlockerSummary) ? LocalizedText.Get("tablet.routePlanner.blocked") : candidate.BlockerSummary,
                candidate.ScoreBreakdown != null ? candidate.ScoreBreakdown.BlockerPenalty : 0f);
        }

        public static NpcLogisticsRouteDefinition BuildRoutePlannerDraftDefinition(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null || candidate.OriginIndustry == null || candidate.DestinationIndustry == null)
            {
                return null;
            }

            return new NpcLogisticsRouteDefinition
            {
                OriginIndustry = candidate.OriginIndustry,
                DestinationIndustry = candidate.DestinationIndustry,
                Commodity = candidate.Commodity,
                OriginTriggerThresholdPercent = 20,
                DestinationTriggerThresholdPercent = 85,
            };
        }

        private static string BuildRoutePlannerStatusTag(TabletRoutePlannerCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            if (candidate.IsUnderperformingActiveLane)
            {
                return "~o~[UNDER]~s~";
            }

            if (candidate.HasActiveNpcRoute)
            {
                return "~b~[LIVE]~s~";
            }

            if (candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked)
            {
                return "~r~[BLOCKED]~s~";
            }

            return "~g~[OPEN]~s~";
        }

        public static string BuildIndustryAccessDetail(TabletLocationSummary summary, Industry industry)
        {
            if (summary == null || industry == null)
            {
                return LocalizedText.Get("tablet.location.access.unavailable");
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                return summary.RequiresIndustryPurchase
                    ? LocalizedText.Format("tablet.location.access.storageBuy", ModFormatting.FormatMoney(industry.IndustryPrice))
                    : LocalizedText.Get("tablet.location.access.storageCleared");
            }

            var ownership = summary.RequiresIndustryPurchase
                ? LocalizedText.Format("tablet.location.access.buy", ModFormatting.FormatMoney(industry.IndustryPrice))
                : LocalizedText.Get("tablet.location.access.operationsUnlocked");
            var permit = !summary.RequiresContractorPermit
                ? LocalizedText.Get("tablet.location.access.permitOpen")
                : (summary.HasContractorPermitForGameplay
                    ? LocalizedText.Get("tablet.location.access.permitCleared")
                    : LocalizedText.Format("tablet.location.access.permitPrice", ModFormatting.FormatMoney(industry.IndustryLicencePrice)));
            return LocalizedText.Format("tablet.location.access.summary", ownership, permit);
        }

        public static string BuildCompactModuleSummary(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            return string.Format(
                LocalizedText.Get("tablet.location.modules.compact"),
                industry.ProductionModuleLevel,
                industry.InputStorageModuleLevel,
                industry.OutputStorageModuleLevel,
                industry.OmegaStorageModuleLevel);
        }

        public static string BuildBalanceChrome(TabletStateSnapshot snapshot)
        {
            var balance = snapshot != null ? snapshot.Balance : 0f;
            return LocalizedText.Format(ModTextKey.TabletCommonBalance, ModFormatting.FormatMoney(balance));
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
                return LocalizedText.Get("tablet.common.none");
            }

            var entries = commodities
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Take(Math.Max(1, maxVisible))
                .ToList();
            if (entries.Count == 0)
            {
                return LocalizedText.Get("tablet.common.none");
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
                return LocalizedText.Get("tablet.cargo.noneLinked");
            }

            var cargoSummary = LocalizedText.Format(
                "tablet.cargo.summary",
                snapshot.CargoVehicleName,
                snapshot.CargoIsEmpty ? LocalizedText.Get("tablet.home.operationsStatus.empty") : snapshot.CargoCommodity,
                snapshot.CargoWeightTons,
                snapshot.CargoCapacityTons);

            if (!snapshot.HasPoweredVehicle || snapshot.FuelCapacityLiters <= 0.001f)
            {
                return cargoSummary;
            }

            var fuelSummary = snapshot.FuelVehicleMatchesCargoVehicle
                ? LocalizedText.Format("tablet.cargo.fuel", snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters)
                : LocalizedText.Format("tablet.cargo.fuelVehicle", snapshot.PoweredVehicleName, snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters);
            return string.Format("{0} | {1}", cargoSummary, fuelSummary);
        }

        public static string BuildMarketSummary(TabletStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.MarketHighlights == null || snapshot.MarketHighlights.Count == 0)
            {
                return LocalizedText.Get("tablet.market.summary.none");
            }

            return string.Join(
                " | ",
                snapshot.MarketHighlights.Select(highlight => LocalizedText.Format("tablet.market.summary.entry", highlight.Commodity, highlight.UnitPrice)).ToArray());
        }

        public static string BuildLocationCaption(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            return summary.Name ?? string.Empty;
        }

        public static string BuildLocationOverviewDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            var ownership = FormatLocationListStatusTag(summary.OwnershipTag);
            if (!string.IsNullOrWhiteSpace(ownership))
            {
                segments.Add(ownership);
            }

            var permit = FormatLocationListStatusTag(summary.PermitTag);
            if (!string.IsNullOrWhiteSpace(permit))
            {
                segments.Add(permit);
            }

            if (!string.IsNullOrWhiteSpace(summary.OverviewDetail))
            {
                segments.Add(summary.OverviewDetail);
            }

            return segments.Count > 0
                ? string.Join(" | ", segments)
                : string.Empty;
        }

        private static string FormatLocationListStatusTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag)
                ? string.Empty
                : tag.Replace("[", string.Empty).Replace("]", string.Empty);
        }

        public static string BuildPermitCaption(string siteName)
        {
            return siteName ?? string.Empty;
        }

        public static string BuildPermitCaption(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            return BuildPermitCaption(summary.Name);
        }

        public static TabletLocationSummary FindSummary(TabletShellContext context, Industry industry)
        {
            if (context == null || industry == null || context.Snapshot == null)
            {
                return null;
            }

            return context.Snapshot.IndustrySummaries
                .Concat(context.Snapshot.ConstructionSiteSummaries)
                .Concat(context.Snapshot.WarehouseSummaries)
                .Concat(context.Snapshot.StoreSummaries)
                .Concat(context.Snapshot.GasStationSummaries)
                .FirstOrDefault(summary => summary != null && summary.Industry != null && string.Equals(summary.Industry.Id, industry.Id, StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildIndustryStatusDetail(TabletLocationSummary summary, Industry industry)
        {
            if (summary == null || industry == null)
            {
                return LocalizedText.Get("tablet.location.stats.industryStateUnavailable");
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

            items.Add(CreateInfoItem(LocalizedText.Get("tablet.location.stats.conversion"), industry.GetPrimaryConversionDescription()));

            var stockpile = statistics != null ? statistics.Stockpile : 0f;
            var totalCapacity = statistics != null ? statistics.TotalCapacity : 1f;
            var stockRatio = statistics != null ? statistics.StockRatio : 0f;
            var utilizationRatio = statistics != null ? statistics.UtilizationRatio : 0f;

            items.Add(CreateInfoItem(
                LocalizedText.Format("tablet.location.stats.stockpileCaption", stockpile, totalCapacity),
                LocalizedText.Get("tablet.location.stats.storageDetail"),
                stockRatio));
            items.Add(CreateInfoItem(
                LocalizedText.Format("tablet.location.stats.productionCaption", industry.CurrentOutputPerHourTons),
                LocalizedText.Format("tablet.location.stats.productionDetail", industry.LastUtilizationPercent, industry.OmegaStorage, industry.OmegaCapacityTons),
                utilizationRatio));
            items.Add(CreateInfoItem(
                LocalizedText.Get("tablet.location.stats.moduleLevels"),
                LocalizedText.Format(
                    "tablet.location.stats.moduleLevelsIndustry",
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
            var legendDetail = LocalizedText.Get("tablet.location.stats.legendBase");
            if (summary != null && !string.IsNullOrWhiteSpace(summary.ProductionWarning))
            {
                legendDetail = LocalizedText.Format("tablet.location.stats.legendWithWarning", summary.ProductionWarning, legendDetail);
            }

            items.Add(CreateInfoItem(
                LocalizedText.Format("tablet.location.stats.totalStockpileCaption", stockpile, totalCapacity),
                LocalizedText.Get("tablet.location.stats.storageDetail"),
                stockRatio));
            items.Add(CreateInfoItem(
                LocalizedText.Format("tablet.location.stats.utilizationCaption", industry.LastUtilizationPercent, industry.CurrentOutputPerHourTons),
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
                items.Add(CreateInfoItem(LocalizedText.Get("tablet.location.stats.commodities"), LocalizedText.Get("tablet.location.stats.commoditiesEmpty")));
                return;
            }

            for (int i = 0; i < statistics.Entries.Count; i++)
            {
                var entry = statistics.Entries[i];
                var caption = LocalizedText.Format("tablet.location.stats.commodityCaption", entry.IsInput ? LocalizedText.Get("tablet.location.stats.commodityInput") : LocalizedText.Get("tablet.location.stats.commodityOutput"), entry.Commodity);
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
            var warehouseRisk = summary != null ? summary.WarehouseRisk : null;
            items.Add(CreateInfoItem(LocalizedText.Get("tablet.location.stats.access"), BuildIndustryAccessDetail(summary ?? new TabletLocationSummary { RequiresIndustryPurchase = industry.RequiresPurchase }, industry)));
            items.Add(CreateInfoItem(
                LocalizedText.Format("tablet.location.stats.storageCaption", totalStorage, totalCapacity),
                LocalizedText.Get("tablet.location.stats.warehouseCapacity"),
                fillRatio));
            if (warehouseRisk != null)
            {
                var projectedLossRatio = warehouseRisk.InventoryValue > 0.01f
                    ? Math.Max(0f, Math.Min(1f, warehouseRisk.ProjectedNextDayLossValue / warehouseRisk.InventoryValue))
                    : 0f;
                items.Add(CreateInfoItem(
                    LocalizedText.Format("tablet.location.stats.stewardshipCaption", BuildWarehouseRiskLevel(warehouseRisk)),
                    BuildWarehouseStewardshipDetail(warehouseRisk),
                    projectedLossRatio));
                items.Add(CreateInfoItem(
                    LocalizedText.Format("tablet.location.stats.inventoryValueCaption", ModFormatting.FormatMoney(warehouseRisk.AdjustedInventoryValue)),
                    LocalizedText.Format(
                        "tablet.location.stats.inventoryValueDetail",
                        ModFormatting.FormatMoney(warehouseRisk.ConditionValueLoss),
                        ModFormatting.FormatMoney(warehouseRisk.ProjectedConditionValueLoss),
                        Math.Max(0f, Math.Min(100f, warehouseRisk.ProjectedNextCondition * 100f)))));
                items.Add(CreateInfoItem(
                    LocalizedText.Get("tablet.location.stats.moduleLevels"),
                    LocalizedText.Format(
                        "tablet.location.stats.moduleLevelsWarehouse",
                        industry.InputStorageModuleLevel,
                        industry.OutputStorageModuleLevel,
                        warehouseRisk.StabilityScore * 100f)));
            }
            else
            {
                items.Add(CreateInfoItem(
                    LocalizedText.Get("tablet.location.stats.moduleLevels"),
                    LocalizedText.Format(
                        "tablet.location.stats.moduleLevelsWarehouseBasic",
                        industry.InputStorageModuleLevel,
                        industry.OutputStorageModuleLevel)));
            }
            items.Add(CreateInfoItem(
                LocalizedText.Get("tablet.location.stats.acceptedResources"),
                SummarizeCommodities(industry.SortedAcceptedInputs, 6)));
        }

        public static string BuildWarehouseRiskLevel(WarehouseStorageRiskSnapshot risk)
        {
            if (risk == null)
            {
                return LocalizedText.Get("tablet.location.stats.risk.stable");
            }

            var projectedLossRatio = risk.InventoryValue > 0.01f
                ? risk.ProjectedNextDayLossValue / risk.InventoryValue
                : 0f;
            var hasRecentOrProjectedLoss = risk.ProjectedNextDayLossValue > 0.01f
                || risk.LastDayTotalLossValue > 0.01f
                || risk.CurrentWeekTotalLossValue > 0.01f;

            if (!risk.HasSensitiveExposure && !hasRecentOrProjectedLoss)
            {
                return risk.StorageCondition < 0.75f ? LocalizedText.Get("tablet.location.stats.risk.watch") : LocalizedText.Get("tablet.location.stats.risk.stable");
            }

            if (risk.ProjectedNextDayLossValue >= 2500f || projectedLossRatio >= 0.020f || risk.StorageCondition < 0.62f)
            {
                return LocalizedText.Get("tablet.location.stats.risk.critical");
            }

            if (risk.ProjectedNextDayLossValue >= 900f || projectedLossRatio >= 0.010f || risk.StorageCondition < 0.72f)
            {
                return LocalizedText.Get("tablet.location.stats.risk.high");
            }

            if (risk.ProjectedNextDayLossValue >= 250f || projectedLossRatio >= 0.004f || risk.StorageCondition < 0.82f)
            {
                return LocalizedText.Get("tablet.location.stats.risk.guarded");
            }

            return LocalizedText.Get("tablet.location.stats.risk.managed");
        }

        public static string BuildWarehouseFocusLabel(WarehouseStorageRiskSnapshot risk)
        {
            if (risk == null)
            {
                return LocalizedText.Get("tablet.location.stats.loss.none");
            }

            var classLabel = GetWarehouseLossClassLabel(risk.DominantLossClass);
            return string.IsNullOrWhiteSpace(risk.DominantCommodity)
                ? classLabel
                : LocalizedText.Format("tablet.location.stats.loss.focus", classLabel, risk.DominantCommodity);
        }

        public static string BuildWarehouseOverviewTelemetry(WarehouseStorageRiskSnapshot risk)
        {
            if (risk == null)
            {
                return string.Empty;
            }

            if (!risk.HasSensitiveExposure && risk.LastDayTotalLossValue <= 0.01f && risk.CurrentWeekTotalLossValue <= 0.01f && risk.ProjectedNextDayLossValue <= 0.01f)
            {
                return string.Empty;
            }

            return LocalizedText.Format(
                "tablet.location.stats.overviewTelemetry",
                BuildWarehouseRiskLevel(risk),
                ModFormatting.FormatMoney(risk.LastDayTotalLossValue),
                ModFormatting.FormatMoney(risk.ProjectedNextDayLossValue));
        }

        public static string BuildWarehouseStewardshipDetail(WarehouseStorageRiskSnapshot risk)
        {
            if (risk == null)
            {
                return LocalizedText.Get("tablet.location.stats.noWarehouseRisk");
            }

            if (!risk.HasSensitiveExposure && risk.LastDayTotalLossValue <= 0.01f && risk.CurrentWeekTotalLossValue <= 0.01f && risk.ProjectedNextDayLossValue <= 0.01f)
            {
                return LocalizedText.Format("tablet.location.stats.noExposureDetail", BuildWarehouseRiskLevel(risk));
            }

            return LocalizedText.Format(
                "tablet.location.stats.stewardshipDetail",
                BuildWarehouseRiskLevel(risk),
                ModFormatting.FormatMoney(risk.CurrentWeekSpoilageValue),
                ModFormatting.FormatMoney(risk.CurrentWeekShrinkageValue),
                ModFormatting.FormatMoney(risk.ProjectedSpoilageValue),
                ModFormatting.FormatMoney(risk.ProjectedShrinkageValue),
                BuildWarehouseFocusLabel(risk));
        }

        private static string GetWarehouseLossClassLabel(WarehouseLossClass lossClass)
        {
            switch (lossClass)
            {
                case WarehouseLossClass.Spoilage:
                    return LocalizedText.Get("tablet.location.stats.loss.spoilage");
                case WarehouseLossClass.Shrinkage:
                    return LocalizedText.Get("tablet.location.stats.loss.shrinkage");
                default:
                    return LocalizedText.Get("tablet.location.stats.loss.none");
            }
        }

        public static string GetUpgradeTitle(IndustryUpgradeModule module)
        {
            if (module == IndustryUpgradeModule.Production)
            {
                return LocalizedText.Get("tablet.industry.upgrade.production");
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                return LocalizedText.Get("tablet.industry.upgrade.inputStorage");
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return LocalizedText.Get("tablet.industry.upgrade.outputStorage");
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                return LocalizedText.Get("tablet.industry.upgrade.omegaStorage");
            }

            return LocalizedText.Get("tablet.industry.upgrade.default");
        }

        public static string BuildUpgradeDetail(Industry industry, IndustryUpgradeModule module, float balance)
        {
            if (industry == null)
            {
                return LocalizedText.Get("tablet.industry.upgrade.noIndustry");
            }

            var level = industry.GetUpgradeLevel(module);
            var cost = industry.GetUpgradeCost(module);
            if (cost <= 0f)
            {
                return LocalizedText.Get("tablet.industry.upgrade.unavailable");
            }

            if (balance < cost)
            {
                return LocalizedText.Format("tablet.industry.upgrade.needMore", level, ModFormatting.FormatMoney(cost), ModFormatting.FormatMoney(cost - balance));
            }

            return LocalizedText.Format("tablet.industry.upgrade.ready", level, ModFormatting.FormatMoney(cost));
        }
    }

    internal sealed class HomeTabletApp : ITabletApp
    {
        private readonly Action _openCompanyMap;
        private readonly Action _openDistrictView;
        private readonly Action _openDepotView;
        private readonly SpecialMissionManager _specialMissionManager;
        private readonly PlayerSuccessTracker _playerSuccessTracker;

        public HomeTabletApp(Action openCompanyMap, Action openDistrictView, Action openDepotView, SpecialMissionManager specialMissionManager, PlayerSuccessTracker playerSuccessTracker)
        {
            _openCompanyMap = openCompanyMap;
            _openDistrictView = openDistrictView;
            _openDepotView = openDepotView;
            _specialMissionManager = specialMissionManager;
            _playerSuccessTracker = playerSuccessTracker;
        }

        public string AppId
        {
            get { return TabletAppIds.Home; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var dispatchOverview = context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview();
            var contractsOverview = context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview();
            var budgetOverview = context.StateStore.GetBudgetOverview() ?? new TabletBudgetOverview();
            var endgame = _playerSuccessTracker != null
                ? _playerSuccessTracker.GetEndgameSummary()
                : new CompanyEndgameSummary();
            var unlockedSuccessCount = _playerSuccessTracker != null ? _playerSuccessTracker.UnlockedCount : 0;
            var totalSuccessCount = _playerSuccessTracker != null ? _playerSuccessTracker.TotalCount : 0;
            var items = new List<MenuItem>();
            var totalTrackedSites = snapshot.IndustrySummaries.Count + snapshot.ConstructionSiteSummaries.Count + snapshot.WarehouseSummaries.Count + snapshot.StoreSummaries.Count + snapshot.GasStationSummaries.Count;
            var warehouseCount = snapshot.WarehouseSummaries.Count;
            var industryCount = snapshot.IndustrySummaries.Count;
            var constructionSiteCount = snapshot.ConstructionSiteSummaries.Count;
            var permitSummaries = snapshot.IndustrySummaries.Concat(snapshot.ConstructionSiteSummaries).ToList();
            var permitSiteCount = permitSummaries.Count(summary => summary != null && summary.Industry != null && summary.Industry.RequiresContractorPermit);
            var unlockedPermitCount = permitSummaries.Count(summary => summary != null && summary.HasContractorPermitForGameplay);
            var operationsHeadline = snapshot.HasNearestIndustry
                ? LocalizedText.Format("tablet.home.operations.nearest", ShortenDashboardLabel(snapshot.NearestIndustryName, 20))
                : LocalizedText.Get("tablet.home.operations.noneNearby");
            var operationsDetail = string.Format(
                "{0}\n{1}",
                operationsHeadline,
                BuildHomeOperationsStatus(snapshot));
            var marketDetail = snapshot.MarketHighlights != null && snapshot.MarketHighlights.Count > 0
                ? LocalizedText.Format(
                    "tablet.home.market.cached",
                    snapshot.MarketHighlights[0].Commodity,
                    snapshot.MarketHighlights[0].UnitPrice,
                    snapshot.MarketHighlights.Count)
                : LocalizedText.Get("tablet.home.market.none");
            var siteDetail = snapshot.HasNearestIndustry
                ? LocalizedText.Format(
                    "tablet.home.site.detail",
                    ShortenDashboardLabel(snapshot.NearestIndustryName, 20),
                    snapshot.NearestIndustryProductionRateTonsPerHour,
                    snapshot.NearestIndustryUtilizationPercent)
                : LocalizedText.Get("tablet.home.site.none");
            var permitDetail = permitSiteCount > 0
                ? LocalizedText.Format("tablet.home.permits.detail", unlockedPermitCount, permitSiteCount)
                : LocalizedText.Get("tablet.home.permits.none");
            var missionListings = _specialMissionManager != null
                ? _specialMissionManager.GetMissionListings()
                : Array.Empty<SpecialMissionListing>();
            var availableMissionCount = missionListings.Count(listing => listing != null && listing.CanAccept);
            var generatedMissionCount = missionListings.Count(listing => listing != null && listing.IsGenerated);
            var crisisMissionCount = missionListings.Count(listing => listing != null && listing.CrisisType != DistrictCrisisType.None);
            var tenderMissionCount = missionListings.Count(listing => listing != null && listing.IsTender);
            var activeMission = missionListings.FirstOrDefault(listing => listing != null && listing.IsActive);
            var missionDetail = activeMission != null
                ? string.Format("{0}\n{1}", activeMission.Name, activeMission.Objective)
                : missionListings.Count > 0
                    ? (availableMissionCount > 0
                        ? (generatedMissionCount > 0
                            ? LocalizedText.Format("tablet.home.missions.readyBoard", availableMissionCount, crisisMissionCount, tenderMissionCount, generatedMissionCount)
                            : LocalizedText.Format("tablet.home.missions.readyCommunity", availableMissionCount, missionListings.Count))
                        : (generatedMissionCount > 0
                            ? LocalizedText.Format("tablet.home.missions.boardLive", missionListings.Count)
                            : LocalizedText.Format("tablet.home.missions.communityLoaded", missionListings.Count)))
                    : LocalizedText.Get("tablet.home.missions.none");
            var siteAction = snapshot.HasNearestIndustry
                ? (snapshot.CanInteractWithNearestIndustry
                    ? (Action)(() => context.Push(TabletAppIds.Industry, "main", snapshot.NearestIndustry))
                    : (Action)(() => context.Push(TabletAppIds.Network, "detail", snapshot.NearestIndustry)))
                : (Action)(() => context.Push(TabletAppIds.Network, "industries"));

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.company"),
                LocalizedText.Format("tablet.home.companyDetail", TabletUiHelpers.BuildBalanceChrome(snapshot), totalTrackedSites, snapshot.ActiveNpcRouteCount, dispatchOverview.ActiveJobCount),
                () => context.Push(TabletAppIds.Network, "root"),
                Color.FromArgb(176, 28, 32, 38),
                Color.FromArgb(220, 88, 106, 118),
                null,
                "HQ"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.operations"),
                operationsDetail,
                siteAction,
                Color.FromArgb(176, 34, 38, 42),
                Color.FromArgb(220, 96, 108, 118),
                null,
                "LIVE"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.budget"),
                LocalizedText.Format(
                    "tablet.home.budgetDetail",
                    budgetOverview.WeeklyNet >= 0f
                        ? "+" + ModFormatting.FormatMoney(budgetOverview.WeeklyNet)
                        : "-" + ModFormatting.FormatMoney(Math.Abs(budgetOverview.WeeklyNet)),
                    ModFormatting.FormatMoney(budgetOverview.UpcomingBills)),
                () => context.Push(TabletAppIds.Budget, "root"),
                Color.FromArgb(184, 42, 56, 44),
                Color.FromArgb(226, 96, 138, 110),
                null,
                "BDG"));
            var propertySummary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var propertyControlledCount = propertySummary.Offices.Count(entry => entry.IsOwned || entry.IsRented)
                + propertySummary.Apartments.Count(entry => entry.IsOwned || entry.IsRented);
            var propertyArrears = propertySummary.TotalArrears;
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.properties"),
                LocalizedText.Format(
                    "tablet.home.propertiesDetail",
                    propertyControlledCount,
                    ModFormatting.FormatMoney(propertyArrears)),
                () => context.Push(TabletAppIds.PropertyPortfolio, "root"),
                Color.FromArgb(184, 54, 48, 38),
                Color.FromArgb(226, 134, 114, 86),
                null,
                "PRP"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.industries"),
                LocalizedText.Format("tablet.home.industriesDetail", industryCount),
                () => context.Push(TabletAppIds.Network, "industries"),
                Color.FromArgb(184, 40, 52, 46),
                Color.FromArgb(226, 98, 124, 108),
                null,
                "IND"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.construction"),
                constructionSiteCount > 0
                    ? LocalizedText.Format("tablet.home.constructionDetail", constructionSiteCount)
                    : LocalizedText.Get("tablet.home.constructionNone"),
                () => context.Push(TabletAppIds.Network, "construction"),
                Color.FromArgb(184, 58, 50, 40),
                Color.FromArgb(226, 124, 104, 84),
                null,
                "CON"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.permits"),
                permitDetail,
                () => context.Push(TabletAppIds.Network, "permits"),
                Color.FromArgb(188, 70, 56, 38),
                Color.FromArgb(228, 154, 126, 82),
                null,
                "PER"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.stores"),
                LocalizedText.Format("tablet.home.storesDetail", snapshot.StoreSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stores"),
                Color.FromArgb(188, 46, 52, 60),
                Color.FromArgb(228, 104, 118, 132),
                null,
                "STR"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.stations"),
                LocalizedText.Format("tablet.home.stationsDetail", snapshot.GasStationSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stations"),
                Color.FromArgb(188, 36, 56, 58),
                Color.FromArgb(228, 88, 128, 130),
                null,
                "GAS"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.services"),
                LocalizedText.Get("tablet.home.servicesDetail"),
                () => context.Push(TabletAppIds.Network, "services"),
                Color.FromArgb(188, 44, 48, 52),
                Color.FromArgb(228, 102, 112, 120),
                null,
                "SRV"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.jobs"),
                LocalizedText.Format(
                    "tablet.home.jobsDetail",
                    Math.Max(0, contractsOverview.QuickJobCount),
                    Math.Max(0, contractsOverview.FreightMarketCount)),
                () => context.Push(TabletAppIds.Network, "jobs"),
                Color.FromArgb(186, 58, 64, 48),
                Color.FromArgb(226, 132, 148, 110),
                null,
                "JOB"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.market"),
                marketDetail,
                () => context.Push(TabletAppIds.Network, "market"),
                Color.FromArgb(188, 54, 44, 58),
                Color.FromArgb(228, 132, 110, 144),
                null,
                "MKT"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.analytics"),
                LocalizedText.Get("tablet.home.analyticsDetail"),
                () => context.Push(TabletAppIds.Analytics, "root"),
                Color.FromArgb(186, 48, 52, 66),
                Color.FromArgb(228, 112, 124, 148),
                null,
                "ANL"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.successes"),
                TabletEndgameStatusFormatter.BuildHomeTileDetail(unlockedSuccessCount, totalSuccessCount, endgame),
                () => context.Push(TabletAppIds.Successes, "root"),
                Color.FromArgb(186, 60, 52, 46),
                Color.FromArgb(228, 140, 122, 104),
                totalSuccessCount > 0
                    ? (float?)unlockedSuccessCount / totalSuccessCount
                    : null,
                "SUS"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.missions"),
                missionDetail,
                () => context.Push(TabletAppIds.Missions, "root"),
                Color.FromArgb(186, 88, 58, 54),
                Color.FromArgb(228, 208, 144, 112),
                null,
                "MIS"));
            items.Add(TabletUiHelpers.CreateActionItem(
                snapshot.HasNearestIndustry
                    ? LocalizedText.Get("tablet.home.site")
                    : LocalizedText.Get("tablet.home.sites"),
                siteDetail,
                siteAction,
                Color.FromArgb(188, 44, 60, 50),
                Color.FromArgb(228, 100, 136, 112),
                null,
                "SITE"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.warehouse"),
                warehouseCount > 0
                    ? LocalizedText.Format("tablet.home.warehouseDetail", warehouseCount, snapshot.SecuredSupportSiteCount)
                    : LocalizedText.Get("tablet.home.warehouseNone"),
                () => context.Push(TabletAppIds.Network, "warehouses"),
                Color.FromArgb(188, 46, 56, 64),
                Color.FromArgb(228, 110, 126, 142),
                null,
                "WH"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.footprint"),
                LocalizedText.Format(
                    "tablet.home.footprintDetail",
                    snapshot.ControlledDistrictCount,
                    snapshot.ActiveCorridorCount,
                    snapshot.SecuredSupportSiteCount),
                _openCompanyMap,
                Color.FromArgb(184, 40, 50, 62),
                Color.FromArgb(224, 96, 116, 136),
                null,
                "FPT"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.districtView"),
                LocalizedText.Get("tablet.home.districtViewDetail"),
                _openDistrictView,
                Color.FromArgb(184, 52, 60, 56),
                Color.FromArgb(224, 110, 132, 122),
                null,
                "DST"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.depotYard"),
                LocalizedText.Get("tablet.home.depotYardDetail"),
                _openDepotView,
                Color.FromArgb(184, 58, 48, 60),
                Color.FromArgb(224, 134, 108, 130),
                null,
                "DPT"));
            items.Add(TabletUiHelpers.CreateNavigationItem(
                LocalizedText.Get("tablet.home.close"),
                LocalizedText.Get("tablet.home.closeDetail"),
                context.Close,
                "EXIT"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.home.title"),
                Subtitle = LocalizedText.Get("tablet.home.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.home.footer"),
                WidthScale = 0.96f,
                CaptionScale = 0.44f,
                DetailScale = 0.275f,
                MaxVisibleItems = 0,
                Layout = SimpleMenuTabletLayout.Dashboard,
                DashboardSidebarCount = 2,
                DashboardTileColumns = 6,
                BottomPanelHeight = 146f,
                BottomPanelRenderer = panel => TabletChartRenderer.DrawHistoryPanel(
                    panel,
                    LocalizedText.Get("tablet.home.profitPanelTitle"),
                    LocalizedText.Format("tablet.home.profitPanelSubtitle", context.StateStore.SelectedGraphTimeframe.ToDisplayLabel()),
                    context.StateStore.GetProfitHistory(context.StateStore.SelectedGraphTimeframe),
                    Color.FromArgb(214, 118, 200, 176),
                    value => ModFormatting.FormatMoney(value)),
                Items = items,
            };
        }

        private static string BuildHomeOperationsStatus(TabletStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return LocalizedText.Get("tablet.home.operationsStatus.noCargo");
            }

            if (snapshot.TransferInProgress)
            {
                return LocalizedText.Get("tablet.home.operationsStatus.transferActive");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                return snapshot.StatusBanner;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.NearestIndustryProductionWarning))
            {
                return snapshot.NearestIndustryProductionWarning;
            }

            if (!snapshot.HasCargoVehicle)
            {
                return LocalizedText.Get("tablet.home.operationsStatus.noCargo");
            }

            var cargoLabel = snapshot.CargoIsEmpty
                ? LocalizedText.Get("tablet.home.operationsStatus.empty")
                : LocalizedText.Format("tablet.home.operationsStatus.cargoLoad", snapshot.CargoCommodity, snapshot.CargoWeightTons, snapshot.CargoCapacityTons);
            if (!snapshot.HasPoweredVehicle || snapshot.FuelCapacityLiters <= 0.001f)
            {
                return cargoLabel;
            }

            return LocalizedText.Format("tablet.home.operationsStatus.fuel", cargoLabel, snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters);
        }

        private static string ShortenDashboardLabel(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || maxLength <= 3 || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength - 3).TrimEnd() + "...";
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

            var validationWarningCount = _missionManager != null && _missionManager.Catalog != null
                ? _missionManager.Catalog.ValidationMessages.Count
                : 0;
            if (validationWarningCount > 0)
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    LocalizedText.Get("tablet.missions.root.addonWarnings"),
                    LocalizedText.Format(
                        "tablet.missions.root.addonWarningsDetail",
                        validationWarningCount,
                        RuntimeLayoutResolver.PreferredMissionDirectoryDisplayPath)));
            }

            if (_missionManager == null || !_missionManager.HasDefinitions)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.missions.root.emptyCaption"),
                    LocalizedText.Format(
                        "tablet.missions.root.emptyDetail",
                        RuntimeLayoutResolver.PreferredMissionDirectoryDisplayPath,
                        RuntimeLayoutResolver.PreferredAddonMissionDirectoryDisplayPath)));
            }
            else
            {
                if (_missionManager.HasActiveMission)
                {
                    items.Add(TabletUiHelpers.CreateBannerItem(
                        LocalizedText.Format("tablet.missions.root.activeBanner", _missionManager.ActiveMissionName),
                        string.Format("{0} | {1}", _missionManager.ActiveObjective, _missionManager.ActiveObjectiveDetail)));
                }

                var listings = _missionManager.GetMissionListings();
                var crisisCount = listings.Count(listing => listing != null && listing.CrisisType != DistrictCrisisType.None);
                var tenderCount = listings.Count(listing => listing != null && listing.IsTender);
                var priorityCount = listings.Count(listing => listing != null && listing.ContractFamily == GeneratedContractFamily.PriorityLinehaul);
                if (crisisCount > 0 || tenderCount > 0 || priorityCount > 0)
                {
                    items.Add(TabletUiHelpers.CreateBannerItem(
                        LocalizedText.Get("tablet.missions.root.boardRotation"),
                        LocalizedText.Format("tablet.missions.root.boardRotationDetail", crisisCount, tenderCount, priorityCount)));
                }

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

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.missions.backToHub"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.missions.title"),
                Subtitle = LocalizedText.Get("tablet.missions.subtitle"),
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.missions.unavailable.manager"), () => context.GoBack());
            }

            var definition = _missionManager.GetDefinition(missionId);
            var listing = _missionManager.GetMissionListings().FirstOrDefault(entry => entry != null && string.Equals(entry.MissionId, missionId, StringComparison.OrdinalIgnoreCase));
            if (definition == null || listing == null)
            {
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.missions.unavailable.definition"), () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateBannerItem(
                    LocalizedText.Format("tablet.missions.rewardBanner", definition.Category, ModFormatting.FormatMoney(definition.Reward)),
                    string.IsNullOrWhiteSpace(definition.Summary) ? definition.Description : definition.Summary),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.missions.availability"),
                    listing.IsActive
                        ? LocalizedText.Format("tablet.missions.availability.active", _missionManager.ActiveObjective)
                        : listing.AvailabilityDetail),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.missions.description"),
                    string.IsNullOrWhiteSpace(definition.Description)
                        ? LocalizedText.Get("tablet.missions.description.empty")
                        : definition.Description),
            };

            if (definition.IsGenerated)
            {
                var liveDistrictEvent = _missionManager.ResolveLiveDistrictEvent(definition.CrisisEventId, definition.CrisisDistrictName);
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.missions.contract"),
                    BuildGeneratedMissionContractDetail(definition, liveDistrictEvent)));
            }

            items.Add(TabletUiHelpers.CreateInfoItem(
                LocalizedText.Get("tablet.missions.completion"),
                definition.Repeatable
                    ? BuildMissionCompletionDetail(listing)
                    : (listing.CompletionCount > 0 ? LocalizedText.Get("tablet.missions.completion.oneOffDone") : LocalizedText.Get("tablet.missions.completion.oneOffPending"))));

            if (listing.IsActive)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.missions.cancel"),
                    LocalizedText.Get("tablet.missions.cancelDetail"),
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
                    LocalizedText.Get("tablet.missions.anotherActive"),
                    LocalizedText.Format("tablet.missions.anotherActiveDetail", _missionManager.ActiveMissionName)));
            }
            else if (listing.CanAccept)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    listing.IsTender ? LocalizedText.Get("tablet.missions.acceptTender") : (definition.IsGenerated ? LocalizedText.Get("tablet.missions.acceptContract") : LocalizedText.Get("tablet.missions.acceptMission")),
                    listing.IsTender
                        ? LocalizedText.Format("tablet.missions.acceptTenderDetail", definition.Name)
                        : (definition.IsGenerated
                            ? LocalizedText.Format("tablet.missions.acceptContractDetail", definition.Name)
                            : LocalizedText.Format("tablet.missions.acceptMissionDetail", definition.Name)),
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
                    LocalizedText.Get("tablet.missions.locked"),
                    listing.AvailabilityDetail));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.missions.backToBoard"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = definition.Name,
                Subtitle = definition.IsGenerated ? LocalizedText.Get("tablet.missions.detail.contractSubtitle") : LocalizedText.Get("tablet.missions.detail.specialSubtitle"),
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
                Title = LocalizedText.Get("tablet.missions.title"),
                Subtitle = LocalizedText.Get("tablet.missions.unavailable.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                Items = new[]
                {
                    TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.missions.unavailable.caption"), detail),
                    TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.missions.backToBoard"), goBack, "BACK"),
                },
            };
        }

        private static string BuildMissionCaption(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            var displayName = BuildMissionBoardLabel(listing);

            if (listing.IsActive)
            {
                return LocalizedText.Format("tablet.missions.label.live", displayName);
            }

            if (!listing.IsUnlocked)
            {
                return LocalizedText.Format("tablet.missions.label.locked", displayName);
            }

            if (listing.CompletionCount > 0 && !listing.Repeatable)
            {
                return LocalizedText.Format("tablet.missions.label.done", displayName);
            }

            if (listing.RepeatCooldownRemainingMinutes > 0)
            {
                return LocalizedText.Format("tablet.missions.label.cooldown", displayName);
            }

            if (listing.CompletionCount > 0 && listing.Repeatable)
            {
                return LocalizedText.Format("tablet.missions.label.repeatCount", displayName, listing.CompletionCount);
            }

            return displayName;
        }

        private static string BuildMissionListDetail(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            if (listing.IsActive)
            {
                return LocalizedText.Format("tablet.missions.list.active", listing.Objective, listing.AvailabilityDetail);
            }

            var summary = string.IsNullOrWhiteSpace(listing.Summary)
                ? listing.Description
                : listing.Summary;
            var availability = string.IsNullOrWhiteSpace(listing.AvailabilityDetail)
                ? string.Empty
                : LocalizedText.Format("tablet.missions.list.availabilitySuffix", listing.AvailabilityDetail);

            if (listing.IsGenerated)
            {
                return LocalizedText.Format(
                    "tablet.missions.list.generated",
                    BuildGeneratedMissionLead(listing),
                    ModFormatting.FormatMoney(listing.Reward),
                    string.IsNullOrWhiteSpace(summary) ? LocalizedText.Get("tablet.missions.list.noBriefing") : summary,
                    availability);
            }

            return LocalizedText.Format(
                "tablet.missions.list.standard",
                ModFormatting.FormatMoney(listing.Reward),
                string.IsNullOrWhiteSpace(summary) ? LocalizedText.Get("tablet.missions.list.noBriefing") : summary,
                availability);
        }

        private static string BuildMissionBoardLabel(SpecialMissionListing listing)
        {
            if (listing == null || !listing.IsGenerated)
            {
                return listing != null ? listing.Name : string.Empty;
            }

            var prefix = listing.IsTender
                ? LocalizedText.Get("tablet.missions.boardLabel.tender")
                : (listing.ContractFamily == GeneratedContractFamily.CrisisRelief
                    ? LocalizedText.Get("tablet.missions.boardLabel.crisis")
                    : (listing.ContractFamily == GeneratedContractFamily.PriorityLinehaul ? LocalizedText.Get("tablet.missions.boardLabel.run") : string.Empty));
            return prefix + listing.Name;
        }

        private static string BuildGeneratedMissionLead(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }

            if (listing.TargetTons > 0.001f && !string.IsNullOrWhiteSpace(listing.Commodity))
            {
                return LocalizedText.Format("tablet.missions.generatedLead", listing.Category, ModFormatting.FormatTons(listing.TargetTons), listing.Commodity);
            }

            return listing.Category ?? LocalizedText.Get("tablet.missions.generatedLeadFallback");
        }

        private static string BuildGeneratedMissionContractDetail(SpecialMissionDefinition definition, TerritoryDistrictEventState liveDistrictEvent)
        {
            if (definition == null)
            {
                return LocalizedText.Get("tablet.missions.generatedContract.fallback");
            }

            var lines = new List<string>
            {
                LocalizedText.Format("tablet.missions.generatedLead", definition.Category, ModFormatting.FormatTons(definition.TargetTons), definition.Commodity),
            };

            if (liveDistrictEvent != null)
            {
                lines.Add(LocalizedText.Format("tablet.missions.generatedContract.event", liveDistrictEvent.Headline, liveDistrictEvent.DistrictName));
                lines.Add(LocalizedText.Format("tablet.missions.generatedContract.status", liveDistrictEvent.StatusText));

                if (!string.IsNullOrWhiteSpace(liveDistrictEvent.ImpactSummary))
                {
                    lines.Add(liveDistrictEvent.ImpactSummary);
                }
            }
            else if (definition.CrisisType != DistrictCrisisType.None)
            {
                lines.Add(LocalizedText.Format("tablet.missions.generatedContract.event", definition.CrisisType, definition.CrisisDistrictName));
            }

            if (!string.IsNullOrWhiteSpace(definition.EligibilitySummary))
            {
                lines.Add(definition.EligibilitySummary);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string BuildMissionCompletionDetail(SpecialMissionListing listing)
        {
            if (listing == null)
            {
                return LocalizedText.Get("tablet.missions.completion.repeatable");
            }

            var detail = LocalizedText.Format("tablet.missions.completion.detail", listing.CompletionCount);
            if (listing.RepeatCooldownInGameMonths > 0 || listing.RepeatCooldownInGameMinutes > 0)
            {
                var cooldownLabel = FormatMissionCooldown(listing);
                detail += listing.RepeatCooldownRemainingMinutes > 0
                    ? LocalizedText.Format("tablet.missions.completion.nextRun", FormatMissionDuration(listing.RepeatCooldownRemainingMinutes))
                    : LocalizedText.Format("tablet.missions.completion.cooldown", cooldownLabel);
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
                    ? LocalizedText.Get("tablet.missions.cooldown.oneMonth")
                    : LocalizedText.Format("tablet.missions.cooldown.months", listing.RepeatCooldownInGameMonths);
            }

            var totalMinutes = listing.RepeatCooldownInGameMinutes;
            const int minutesPerWeek = 7 * 24 * 60;
            if (totalMinutes > 0 && totalMinutes % minutesPerWeek == 0)
            {
                var weeks = totalMinutes / minutesPerWeek;
                return weeks == 1
                    ? LocalizedText.Get("tablet.missions.cooldown.oneWeek")
                    : LocalizedText.Format("tablet.missions.cooldown.weeks", weeks);
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

    internal sealed class NetworkTabletApp : ITabletApp
    {
        private const int InGameMinutesPerDay = 24 * 60;

        private enum LocationListFilterMode
        {
            All = 0,
            Owned = 1,
            NotOwned = 2,
            Open = 3,
            NotOpen = 4,
        }

        private enum DispatchDiagnosticsFilterMode
        {
            All = 0,
            FailuresOnly = 1,
        }

        private readonly float _interactionDistance;
        private readonly Func<Industry, string> _purchasePermit;
        private readonly Action<Industry> _addGpsRoute;
        private readonly Action _clearGpsRoute;
        private readonly Action _requestRefuelService;
        private readonly Action _requestRepairService;
        private readonly Func<Industry, string> _toggleServiceSiteOperator;
        private readonly Action<string> _showStatus;
        private readonly Action _openRoutePlannerMap;
        private readonly Action<NpcLogisticsRouteDefinition> _openNpcPlannerDraft;
        private LocationListFilterMode _industryFilterMode;
        private LocationListFilterMode _permitFilterMode;
        private LocationListFilterMode _storeFilterMode;
        private LocationListFilterMode _stationFilterMode;
        private DispatchDiagnosticsFilterMode _dispatchDiagnosticsFilterMode;

        public NetworkTabletApp(float interactionDistance, Func<Industry, string> purchasePermit, Action<Industry> addGpsRoute, Action clearGpsRoute, Action requestRefuelService, Action requestRepairService, Func<Industry, string> toggleServiceSiteOperator, Action<string> showStatus = null, Action openRoutePlannerMap = null, Action<NpcLogisticsRouteDefinition> openNpcPlannerDraft = null)
        {
            _interactionDistance = interactionDistance;
            _purchasePermit = purchasePermit;
            _addGpsRoute = addGpsRoute;
            _clearGpsRoute = clearGpsRoute;
            _requestRefuelService = requestRefuelService;
            _requestRepairService = requestRepairService;
            _toggleServiceSiteOperator = toggleServiceSiteOperator;
            _showStatus = showStatus;
            _openRoutePlannerMap = openRoutePlannerMap;
            _openNpcPlannerDraft = openNpcPlannerDraft;
            _industryFilterMode = LocationListFilterMode.All;
            _permitFilterMode = LocationListFilterMode.All;
            _storeFilterMode = LocationListFilterMode.All;
            _stationFilterMode = LocationListFilterMode.All;
            _dispatchDiagnosticsFilterMode = DispatchDiagnosticsFilterMode.All;
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
                case "construction":
                    return BuildLocationListPage(context, LocalizedText.Get("tablet.network.constructionSites"), LocalizedText.Get("tablet.location.list.constructionSubtitle"), context.Snapshot.ConstructionSiteSummaries, true, false);
                case "jobs":
                    return BuildJobsPage(context);
                case "dispatch":
                    return BuildDispatchPage(context);
                case "planner":
                    return BuildRoutePlannerPage(context);
                case "planner-detail":
                    return BuildRoutePlannerDetailPage(context, route != null ? route.Payload as string : null);
                case "dispatch-world":
                    return BuildWorldDispatchPage(context);
                case "dispatch-quick-jobs":
                    return BuildPlayerContractListPage(context, PlayerContractType.QuickJob);
                case "dispatch-freight-market":
                    return BuildPlayerContractListPage(context, PlayerContractType.FreightMarket);
                case "dispatch-accepted-contracts":
                    return BuildAcceptedContractsPage(context);
                case "dispatch-contract-detail":
                    return BuildPlayerContractDetailPage(context, route != null ? route.Payload as string : null);
                case "dispatch-diagnostics":
                    return BuildDispatchDiagnosticsPage(context);
                case "stores":
                    return BuildStoreListPage(context);
                case "stations":
                    return BuildStationListPage(context);
                case "services":
                    return BuildServicesPage(context);
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
            var dispatchOverview = context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview();
            var contractsOverview = context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview();
            var permitSummaries = snapshot.IndustrySummaries.Concat(snapshot.ConstructionSiteSummaries).ToList();
            var items = new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                items.Add(TabletUiHelpers.CreateBannerItem(LocalizedText.Get("tablet.network.status"), snapshot.StatusBanner));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.industries"),
                LocalizedText.Format("tablet.home.industriesDetail", snapshot.IndustrySummaries.Count),
                () => context.Push(TabletAppIds.Network, "industries")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.network.constructionSites"),
                LocalizedText.Format("tablet.network.constructionSitesDetail", snapshot.ConstructionSiteSummaries.Count),
                () => context.Push(TabletAppIds.Network, "construction")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.network.contractorPermits"),
                LocalizedText.Format(
                    "tablet.network.contractorPermitsDetail",
                    permitSummaries.Count(summary => summary != null && summary.Industry != null && summary.Industry.RequiresContractorPermit)),
                () => context.Push(TabletAppIds.Network, "permits")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.stores"),
                LocalizedText.Format("tablet.home.storesDetail", snapshot.StoreSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stores")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.network.gasStations"),
                LocalizedText.Format("tablet.network.gasStationsDetail", snapshot.GasStationSummaries.Count),
                () => context.Push(TabletAppIds.Network, "stations")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.network.warehouses"),
                LocalizedText.Format("tablet.network.warehousesDetail", snapshot.WarehouseSummaries.Count),
                () => context.Push(TabletAppIds.Network, "warehouses")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletAnalyticsRoutePlanner),
                LocalizedText.Get("tablet.routePlanner.actionDetail"),
                () => context.Push(TabletAppIds.Network, "planner")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.dispatch"),
                LocalizedText.Format(
                    "tablet.network.dispatchDetail",
                    dispatchOverview.DispatchHeadline ?? LocalizedText.Get("tablet.home.dispatchIdle"),
                    contractsOverview.BoardHeadline ?? LocalizedText.Get("tablet.network.dispatchBoardCooling"),
                    contractsOverview.AcceptedHeadline ?? LocalizedText.Get("tablet.jobs.acceptedFallbackHeadline")),
                () => context.Push(TabletAppIds.Network, "dispatch")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.services"),
                LocalizedText.Get("tablet.home.servicesDetail"),
                () => context.Push(TabletAppIds.Network, "services")));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.home.market"),
                TabletUiHelpers.BuildMarketSummary(snapshot),
                () => context.Push(TabletAppIds.Network, "market")));
            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.network.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.network.title"),
                Subtitle = LocalizedText.Get("tablet.network.subtitle"),
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
                .OrderByDescending(summary => summary != null && summary.WarehouseRisk != null ? summary.WarehouseRisk.ProjectedNextDayLossValue : 0f)
                .ThenByDescending(summary => summary != null && summary.WarehouseRisk != null ? summary.WarehouseRisk.CurrentWeekTotalLossValue : 0f)
                .ThenBy(summary => summary.Name)
                .ToList();
            var items = new List<MenuItem>();

            for (int i = 0; i < warehouseSummaries.Count; i++)
            {
                var summary = warehouseSummaries[i];
                var acceptedResources = TabletUiHelpers.SummarizeCommodities(summary.Industry.SortedAcceptedInputs, 4);
                Industry warehouse = summary.Industry;
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildLocationCaption(summary),
                    LocalizedText.Format("tablet.network.warehouseAccepts", acceptedResources, TabletUiHelpers.BuildLocationOverviewDetail(summary)),
                    () => context.Push(TabletAppIds.Network, "detail", warehouse),
                    iconLabel: "WH"));
            }

            if (items.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.network.warehousesEmptyCaption"), LocalizedText.Get("tablet.home.warehouseNone")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.market.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.network.warehouses"),
                Subtitle = LocalizedText.Get("tablet.network.warehousesSubtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 5,
                Items = items,
            };
        }

        private TabletShellPage BuildRoutePlannerPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var candidates = context.StateStore.GetRoutePlannerCandidates().ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateRoutePlannerSortSelectorItem(context),
                TabletUiHelpers.CreateRoutePlannerAvailabilitySelectorItem(context),
                TabletUiHelpers.CreateRoutePlannerCommoditySelectorItem(context),
                TabletUiHelpers.CreateRoutePlannerDistrictSelectorItem(context),
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var candidateId = candidate.CandidateId;
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                    TabletUiHelpers.BuildRoutePlannerNetworkDetail(candidate),
                    () =>
                    {
                        context.StateStore.SetSelectedRoutePlannerCandidate(candidateId);
                        context.Push(TabletAppIds.Network, "planner-detail", candidateId);
                    },
                    iconLabel: "LAN"));
            }

            if (candidates.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.routePlanner.emptyCaption"),
                    LocalizedText.Get("tablet.routePlanner.emptyDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get(ModTextKey.TabletNavigationReturnToHub), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get(ModTextKey.TabletAnalyticsRoutePlanner),
                Subtitle = LocalizedText.Get("tablet.routePlanner.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.routePlanner.footer"),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
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
                    Title = LocalizedText.Get(ModTextKey.TabletAnalyticsRoutePlanner),
                    Subtitle = LocalizedText.Get("tablet.routePlanner.unavailableSubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.routePlanner.unavailableSubtitle"), LocalizedText.Get("tablet.routePlanner.unavailableDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.routePlanner.backDetail"), () => context.GoBack(), "BACK"),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    candidate.AvailabilityLabel,
                    LocalizedText.Format(
                        "tablet.routePlanner.headerDetail",
                        string.IsNullOrWhiteSpace(candidate.DistrictPairLabel) ? LocalizedText.Get("tablet.routePlanner.districtUnknown") : candidate.DistrictPairLabel,
                        string.IsNullOrWhiteSpace(candidate.CorridorId) ? LocalizedText.Get("tablet.routePlanner.corridorUnknown") : candidate.CorridorId)),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.routePlanner.projection"),
                    TabletUiHelpers.BuildRoutePlannerProjectionDetail(candidate)),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.routePlanner.liveComparison"),
                    TabletUiHelpers.BuildRoutePlannerPerformanceDetail(candidate)),
            };

            if (candidate.IsUnderperformingActiveLane)
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    LocalizedText.Get("tablet.routePlanner.underperforming"),
                    LocalizedText.Get("tablet.routePlanner.underperformingDetail")));
            }

            items.Add(TabletUiHelpers.CreateInfoItem(
                candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked ? LocalizedText.Get("tablet.routePlanner.blocker") : LocalizedText.Get("tablet.routePlanner.status"),
                TabletUiHelpers.BuildRoutePlannerBlockerDetail(candidate)));

            if (candidate.MatchingContractId > 0)
            {
                var contractId = candidate.MatchingContractId;
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.routePlanner.openNpcContract"),
                    string.IsNullOrWhiteSpace(candidate.MatchingContractLabel)
                        ? LocalizedText.Get("tablet.routePlanner.openNpcContractDetail")
                        : candidate.MatchingContractLabel,
                    () => context.Push(TabletAppIds.Analytics, "route-detail", contractId),
                    iconLabel: "NPC"));
            }

            if (candidate.OriginIndustry != null)
            {
                var originIndustry = candidate.OriginIndustry;
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.routePlanner.gpsOrigin"),
                    LocalizedText.Format("tablet.routePlanner.gpsDetail", originIndustry.Name),
                    () => _addGpsRoute?.Invoke(originIndustry),
                    iconLabel: "O"));
            }

            if (candidate.DestinationIndustry != null)
            {
                var destinationIndustry = candidate.DestinationIndustry;
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.routePlanner.gpsDestination"),
                    LocalizedText.Format("tablet.routePlanner.gpsDetail", destinationIndustry.Name),
                    () => _addGpsRoute?.Invoke(destinationIndustry),
                    iconLabel: "D"));
            }

            if (_openRoutePlannerMap != null)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.routePlanner.showOnMap"),
                    LocalizedText.Get("tablet.routePlanner.showOnMapDetail"),
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
                        LocalizedText.Get("tablet.routePlanner.draftNpcLane"),
                        LocalizedText.Get("tablet.routePlanner.draftNpcLaneDetail"),
                        () => _openNpcPlannerDraft(draftDefinition),
                        iconLabel: "NPC"));
                }
                else
                {
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        LocalizedText.Get("tablet.routePlanner.npcDraftLocked"),
                        TabletUiHelpers.BuildRoutePlannerBlockerDetail(candidate)));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.routePlanner.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get(ModTextKey.TabletAnalyticsRoutePlanner),
                Subtitle = TabletUiHelpers.BuildRoutePlannerCandidateCaption(candidate),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private static string BuildFilterCaption(LocationListFilterMode filterMode)
        {
            switch (filterMode)
            {
                case LocationListFilterMode.Owned:
                    return LocalizedText.Format("tablet.location.filter.caption", LocalizedText.Get("tablet.location.filter.owned"));
                case LocationListFilterMode.NotOwned:
                    return LocalizedText.Format("tablet.location.filter.caption", LocalizedText.Get("tablet.location.filter.notOwned"));
                case LocationListFilterMode.Open:
                    return LocalizedText.Format("tablet.location.filter.caption", LocalizedText.Get("tablet.location.filter.open"));
                case LocationListFilterMode.NotOpen:
                    return LocalizedText.Format("tablet.location.filter.caption", LocalizedText.Get("tablet.location.filter.notOpen"));
                default:
                    return LocalizedText.Format("tablet.location.filter.caption", LocalizedText.Get("tablet.location.filter.all"));
            }
        }

        private static string BuildFilterDetail(string title)
        {
            return LocalizedText.Format("tablet.location.filter.detail", title.ToLowerInvariant());
        }

        private static LocationListFilterMode CycleFilter(LocationListFilterMode filterMode, int delta)
        {
            var values = Enum.GetValues(typeof(LocationListFilterMode)).Cast<LocationListFilterMode>().ToArray();
            var currentIndex = Array.IndexOf(values, filterMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = currentIndex + direction;
            while (nextIndex < 0)
            {
                nextIndex += values.Length;
            }

            while (nextIndex >= values.Length)
            {
                nextIndex -= values.Length;
            }

            return values[nextIndex];
        }

        private static bool MatchesFilter(TabletLocationSummary summary, LocationListFilterMode filterMode)
        {
            switch (filterMode)
            {
                case LocationListFilterMode.Owned:
                    return summary != null && summary.IsOwnedByPlayer;
                case LocationListFilterMode.NotOwned:
                    return summary != null && !summary.IsOwnedByPlayer;
                case LocationListFilterMode.Open:
                    return TabletLocationFilters.IsGameplayOpenToPlayer(summary);
                case LocationListFilterMode.NotOpen:
                    return !TabletLocationFilters.IsGameplayOpenToPlayer(summary);
                default:
                    return true;
            }
        }

        private static List<TabletLocationSummary> ApplyFilter(IEnumerable<TabletLocationSummary> summaries, LocationListFilterMode filterMode)
        {
            return summaries == null
                ? new List<TabletLocationSummary>()
                : summaries.Where(summary => summary != null && MatchesFilter(summary, filterMode)).ToList();
        }

        private void CycleIndustryFilter(TabletShellContext context, int delta)
        {
            _industryFilterMode = CycleFilter(_industryFilterMode, delta);
            context.Refresh();
        }

        private void CyclePermitFilter(TabletShellContext context, int delta)
        {
            _permitFilterMode = CycleFilter(_permitFilterMode, delta);
            context.Refresh();
        }

        private void CycleStoreFilter(TabletShellContext context, int delta)
        {
            _storeFilterMode = CycleFilter(_storeFilterMode, delta);
            context.Refresh();
        }

        private void CycleStationFilter(TabletShellContext context, int delta)
        {
            _stationFilterMode = CycleFilter(_stationFilterMode, delta);
            context.Refresh();
        }

        private void CycleDispatchDiagnosticsFilter(TabletShellContext context)
        {
            _dispatchDiagnosticsFilterMode = _dispatchDiagnosticsFilterMode == DispatchDiagnosticsFilterMode.All
                ? DispatchDiagnosticsFilterMode.FailuresOnly
                : DispatchDiagnosticsFilterMode.All;
            context.Refresh();
        }

        private static string BuildDispatchDiagnosticsSummary(NpcWorldDispatchOverview overview)
        {
            overview = overview ?? new NpcWorldDispatchOverview();
            return LocalizedText.Format(
                "tablet.dispatch.diagnostics.summary",
                Math.Max(0, overview.ActiveJobCount),
                Math.Max(0, overview.ListedOpportunityCount),
                Math.Max(0, overview.VisibleConvoyCount),
                Math.Max(0, overview.RecentFailureCount));
        }

        private static string BuildDispatchDiagnosticCaption(NpcWorldDispatchDiagnosticEntry entry)
        {
            if (entry == null)
            {
                return LocalizedText.Get("tablet.dispatch.diagnostics.eventFallback");
            }

            var failureTag = entry.IsFailure ? LocalizedText.Get("tablet.dispatch.diagnostics.failTag") : string.Empty;
            var jobType = entry.JobType.HasValue ? FormatWorldDispatchJobType(entry.JobType.Value) : LocalizedText.Get("tablet.dispatch.jobType.dispatch");
            var commodity = string.IsNullOrWhiteSpace(entry.Commodity) ? string.Empty : string.Format(" {0}", entry.Commodity);
            return string.Format(
                "{0} | {1} | {2}{3}{4}",
                FormatDispatchDiagnosticTime(entry.ClockMinute),
                FormatWorldDispatchDiagnosticStage(entry.Stage),
                jobType,
                commodity,
                failureTag);
        }

        private static string BuildDispatchDiagnosticDetail(NpcWorldDispatchDiagnosticEntry entry)
        {
            if (entry == null)
            {
                return LocalizedText.Get("tablet.dispatch.diagnostics.noDetails");
            }

            var segments = new List<string>();
            var route = BuildDispatchDiagnosticRoute(entry);
            if (!string.IsNullOrWhiteSpace(route))
            {
                segments.Add(route);
            }

            if (entry.Tons.HasValue && entry.Tons.Value > 0.001f)
            {
                segments.Add(string.Format("{0:0.0}t", entry.Tons.Value));
            }

            if (!string.IsNullOrWhiteSpace(entry.Outcome))
            {
                segments.Add(entry.Outcome);
            }

            return segments.Count > 0
                ? string.Join(" | ", segments)
                : LocalizedText.Get("tablet.dispatch.diagnostics.noDetails");
        }

        private static string BuildDispatchDiagnosticRoute(NpcWorldDispatchDiagnosticEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var origin = string.IsNullOrWhiteSpace(entry.OriginLabel) ? string.Empty : entry.OriginLabel.Trim();
            var destination = string.IsNullOrWhiteSpace(entry.DestinationLabel) ? string.Empty : entry.DestinationLabel.Trim();
            var usesExternalEndpoint = entry.JobType == NpcWorldJobType.ExternalImport || entry.JobType == NpcWorldJobType.ExternalExport;
            if (string.IsNullOrWhiteSpace(origin) && string.IsNullOrWhiteSpace(destination))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(origin))
            {
                return usesExternalEndpoint
                    ? LocalizedText.Format("tablet.dispatch.diagnostics.route.externalDestination", destination)
                    : destination;
            }

            if (string.IsNullOrWhiteSpace(destination))
            {
                return usesExternalEndpoint
                    ? LocalizedText.Format("tablet.dispatch.diagnostics.route.externalOrigin", origin)
                    : origin;
            }

            return LocalizedText.Format("tablet.dispatch.diagnostics.route.link", origin, destination);
        }

        private static string FormatDispatchDiagnosticTime(int? clockMinute)
        {
            if (!clockMinute.HasValue)
            {
                return LocalizedText.Get("tablet.dispatch.diagnostics.timeUnknown");
            }

            var normalized = Math.Max(0, clockMinute.Value);
            var dayIndex = (normalized / InGameMinutesPerDay) % 7;
            var minuteOfDay = normalized % InGameMinutesPerDay;
            var hour = minuteOfDay / 60;
            var minute = minuteOfDay % 60;
            return LocalizedText.Format("tablet.dispatch.diagnostics.timeFormat", dayIndex + 1, hour, minute);
        }

        private static string FormatWorldDispatchDiagnosticStage(NpcWorldDispatchDiagnosticStage stage)
        {
            switch (stage)
            {
                case NpcWorldDispatchDiagnosticStage.CandidateGeneration:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.candidate");
                case NpcWorldDispatchDiagnosticStage.EligibilityFiltering:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.eligibility");
                case NpcWorldDispatchDiagnosticStage.Queueing:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.queue");
                case NpcWorldDispatchDiagnosticStage.Revalidation:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.revalidate");
                case NpcWorldDispatchDiagnosticStage.VisualSpawn:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.visual");
                case NpcWorldDispatchDiagnosticStage.Cleanup:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.cleanup");
                case NpcWorldDispatchDiagnosticStage.Completion:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.complete");
                default:
                    return LocalizedText.Get("tablet.dispatch.diagnostics.stage.evaluate");
            }
        }

        private static string FormatWorldDispatchJobType(NpcWorldJobType type)
        {
            return AmbientWorldDispatchText.FormatJobType(type);
        }

        private TabletShellPage BuildServicesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var alertRows = TabletFleetAlertFormatter.BuildRows(context.StateStore.GetFleetAlertSummary(snapshot));
            var items = new List<MenuItem>(alertRows.Count + 3);
            for (int i = 0; i < alertRows.Count; i++)
            {
                var alertRow = alertRows[i];
                if (alertRow == null)
                {
                    continue;
                }

                items.Add(alertRow.IsAlert
                    ? TabletUiHelpers.CreateBannerItem(alertRow.Caption, alertRow.Detail)
                    : TabletUiHelpers.CreateInfoItem(alertRow.Caption, alertRow.Detail));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.services.refuelCurrentVehicle"),
                LocalizedText.Get("tablet.services.refuelCurrentVehicleDetail"),
                () => _requestRefuelService?.Invoke(),
                iconLabel: "FUEL"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.services.repairCurrentVehicle"),
                LocalizedText.Get("tablet.services.repairCurrentVehicleDetail"),
                () => _requestRepairService?.Invoke(),
                iconLabel: "FIX"));
            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get(ModTextKey.TabletNavigationReturnToHub), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.home.services"),
                Subtitle = LocalizedText.Get("tablet.services.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.88f,
                MaxVisibleItems = 5,
                Items = items,
            };
        }

        private TabletShellPage BuildJobsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var contractsOverview = context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview();
            var items = new List<MenuItem>
            {
                CreatePlayerContractsCommodityFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.jobs.contractsCommodity"),
                    LocalizedText.Get("tablet.jobs.contractsCommodityDetail")),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.quickJobs"),
                    LocalizedText.Format("tablet.jobs.quickJobsDetail", Math.Max(0, contractsOverview.QuickJobCount)),
                    () => context.Push(TabletAppIds.Network, "dispatch-quick-jobs"),
                    Color.FromArgb(184, 58, 72, 52),
                    Color.FromArgb(224, 128, 176, 122),
                    null,
                    "QJ"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.freightMarket"),
                    LocalizedText.Format("tablet.jobs.freightMarketDetail", Math.Max(0, contractsOverview.FreightMarketCount)),
                    () => context.Push(TabletAppIds.Network, "dispatch-freight-market"),
                    Color.FromArgb(184, 64, 58, 48),
                    Color.FromArgb(224, 148, 136, 112),
                    null,
                    "FM"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.acceptedContracts"),
                    string.Format("{0}\n{1}", contractsOverview.AcceptedHeadline ?? LocalizedText.Get("tablet.jobs.acceptedFallbackHeadline"), contractsOverview.AcceptedDetail ?? LocalizedText.Get("tablet.jobs.acceptedFallbackDetail")),
                    () => context.Push(TabletAppIds.Network, "dispatch-accepted-contracts"),
                    Color.FromArgb(184, 52, 68, 58),
                    Color.FromArgb(224, 118, 156, 136),
                    null,
                    "ACT"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.refreshContractsBoard"),
                    LocalizedText.Get("tablet.jobs.refreshContractsBoardDetail"),
                    () =>
                    {
                        context.StateStore.RefreshPlayerContractsBoard();
                        _showStatus?.Invoke(LocalizedText.Get("tablet.jobs.boardRefreshed"));
                        context.Refresh();
                    },
                    iconLabel: "REF"),
                TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.successes.backHub"), () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.home.jobs"),
                Subtitle = LocalizedText.Get("tablet.jobs.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.jobs.footer"),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildDispatchPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var worldOverview = context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview();
            var contractsOverview = context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview();
            var items = new List<MenuItem>
            {
                CreatePlayerContractsCommodityFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.jobs.contractsCommodity"),
                    LocalizedText.Get("tablet.jobs.contractsCommodityDetail")),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.refreshContractsBoard"),
                    LocalizedText.Get("tablet.jobs.refreshContractsBoardDetail"),
                    () =>
                    {
                        context.StateStore.RefreshPlayerContractsBoard();
                        _showStatus?.Invoke(LocalizedText.Get("tablet.jobs.boardRefreshed"));
                        context.Refresh();
                    },
                    iconLabel: "REF"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.quickJobs"),
                    LocalizedText.Format("tablet.jobs.quickJobsDetail", Math.Max(0, contractsOverview.QuickJobCount)),
                    () => context.Push(TabletAppIds.Network, "dispatch-quick-jobs"),
                    iconLabel: "QJ"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.freightMarket"),
                    LocalizedText.Format("tablet.jobs.freightMarketDetail", Math.Max(0, contractsOverview.FreightMarketCount)),
                    () => context.Push(TabletAppIds.Network, "dispatch-freight-market"),
                    iconLabel: "FM"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.jobs.acceptedContracts"),
                    string.Format("{0}\n{1}", contractsOverview.AcceptedHeadline ?? LocalizedText.Get("tablet.jobs.acceptedFallbackHeadline"), contractsOverview.AcceptedDetail ?? LocalizedText.Get("tablet.jobs.acceptedFallbackDetail")),
                    () => context.Push(TabletAppIds.Network, "dispatch-accepted-contracts"),
                    iconLabel: "ACT"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.dispatch.worldDispatch"),
                    string.Format("{0}\n{1}", worldOverview.DispatchHeadline ?? LocalizedText.Get("tablet.home.dispatchIdle"), worldOverview.DispatchDetail ?? LocalizedText.Get("tablet.home.dispatchNoBias")),
                    () => context.Push(TabletAppIds.Network, "dispatch-world"),
                    iconLabel: "WRL"),
                TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get(ModTextKey.TabletNavigationReturnToHub), () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.home.dispatch"),
                Subtitle = LocalizedText.Get("tablet.dispatch.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.dispatch.footer"),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildWorldDispatchPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var overview = context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview();
            var jobs = context.StateStore.GetWorldDispatchJobs()
                .OrderByDescending(job => job != null && job.IsSpotOpportunity)
                .ThenByDescending(job => job != null && job.IsRivalJob)
                .ThenBy(job => job != null ? job.RemainingInGameMinutes : int.MaxValue)
                .ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    overview.DispatchHeadline ?? LocalizedText.Get("tablet.home.dispatchIdle"),
                    overview.DispatchDetail ?? LocalizedText.Get("tablet.home.dispatchNoBias")),
                TabletUiHelpers.CreateSelectorItem(
                    () => LocalizedText.Format("tablet.dispatch.world.policyCaption", LocalizedText.Get("tablet.dispatch.world.policyLabel"), FormatWorldDispatchPolicy(overview.DispatchPolicy)),
                    () => LocalizedText.Get("tablet.dispatch.world.policyDetail"),
                    () =>
                    {
                        context.StateStore.CycleWorldDispatchPolicy(-1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldDispatchPolicy(1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldDispatchPolicy(1);
                        context.Refresh();
                    },
                    "POL"),
                TabletUiHelpers.CreateSelectorItem(
                    () => LocalizedText.Format("tablet.dispatch.world.policyCaption", LocalizedText.Get("tablet.dispatch.world.commodityLabel"), string.IsNullOrWhiteSpace((context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview()).PriorityCommodity) ? LocalizedText.Get("tablet.dispatch.world.any") : (context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview()).PriorityCommodity),
                    () => LocalizedText.Get("tablet.dispatch.world.commodityDetail"),
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityCommodity(-1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityCommodity(1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityCommodity(1);
                        context.Refresh();
                    },
                    "COM"),
                TabletUiHelpers.CreateSelectorItem(
                    () => LocalizedText.Format("tablet.dispatch.world.policyCaption", LocalizedText.Get("tablet.dispatch.world.districtLabel"), string.IsNullOrWhiteSpace((context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview()).PriorityDistrict) ? LocalizedText.Get("tablet.dispatch.world.all") : (context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview()).PriorityDistrict),
                    () => LocalizedText.Get("tablet.dispatch.world.districtDetail"),
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityDistrict(-1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityDistrict(1);
                        context.Refresh();
                    },
                    () =>
                    {
                        context.StateStore.CycleWorldPriorityDistrict(1);
                        context.Refresh();
                    },
                    "DST"),
                TabletUiHelpers.CreateActionItem(
                    overview.PremiumDispatchEnabled ? LocalizedText.Get("tablet.dispatch.world.premiumOn") : LocalizedText.Get("tablet.dispatch.world.premiumOff"),
                    LocalizedText.Get("tablet.dispatch.world.premiumDetail"),
                    () =>
                    {
                        context.StateStore.TogglePremiumDispatch();
                        context.Refresh();
                    },
                    iconLabel: "PRM"),
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.dispatch.world.diagnostics"),
                    LocalizedText.Format("tablet.dispatch.world.diagnosticsDetail", BuildDispatchDiagnosticsSummary(overview)),
                    () => context.Push(TabletAppIds.Network, "dispatch-diagnostics"),
                    iconLabel: "LOG"),
            };

            if (jobs.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.dispatch.world.emptyCaption"), LocalizedText.Get("tablet.dispatch.world.emptyDetail")));
            }
            else
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        job.Label,
                        LocalizedText.Format("tablet.dispatch.world.jobDetail", job.Detail, job.Tons, Math.Max(0, job.RemainingInGameMinutes))));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get(ModTextKey.TabletNavigationReturnToHub), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.dispatch.world.title"),
                Subtitle = LocalizedText.Get("tablet.dispatch.world.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.dispatch.world.footer"),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildPlayerContractListPage(TabletShellContext context, PlayerContractType type)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var listings = context.StateStore.GetPlayerContractListings(type)
                .Where(contract => contract != null)
                .ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    type == PlayerContractType.QuickJob ? LocalizedText.Get("tablet.jobs.quickJobs") : LocalizedText.Get("tablet.jobs.freightMarket"),
                    type == PlayerContractType.QuickJob
                        ? LocalizedText.Get("tablet.contracts.quickJobsIntro")
                        : LocalizedText.Get("tablet.contracts.freightMarketIntro")),
                CreatePlayerContractsSortSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.sort"),
                    LocalizedText.Get("tablet.contracts.sortDetail")),
                CreatePlayerContractsExpiryFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.expiry"),
                    LocalizedText.Get("tablet.contracts.expiryDetail")),
                CreatePlayerContractsPayoutDensityFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.payoutDensity"),
                    LocalizedText.Get("tablet.contracts.payoutDensityDetail")),
                CreatePlayerContractsRigClassFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.rigClass"),
                    LocalizedText.Get("tablet.contracts.rigClassDetail")),
                CreatePlayerContractsDistrictFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.district"),
                    LocalizedText.Get("tablet.contracts.districtDetail")),
                CreatePlayerContractsCommodityFilterSelectorItem(
                    context,
                    LocalizedText.Get("tablet.contracts.commodity"),
                    LocalizedText.Get("tablet.contracts.commodityDetail")),
            };

            if (listings.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    type == PlayerContractType.QuickJob ? LocalizedText.Get("tablet.contracts.noQuickJobsListed") : LocalizedText.Get("tablet.contracts.noFreightMarketListed"),
                    LocalizedText.Get("tablet.contracts.noMatchDetail")));
            }
            else
            {
                for (int i = 0; i < listings.Count; i++)
                {
                    var contract = listings[i];
                    var contractId = contract.Id;
                    items.Add(TabletUiHelpers.CreateActionItem(
                        BuildPlayerContractCaption(contract),
                        BuildPlayerContractListDetail(contract),
                        () => context.Push(TabletAppIds.Network, "dispatch-contract-detail", contractId),
                        iconLabel: type == PlayerContractType.QuickJob ? "QJ" : "FM"));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.dispatch.accepted.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = type == PlayerContractType.QuickJob ? LocalizedText.Get("tablet.jobs.quickJobs") : LocalizedText.Get("tablet.jobs.freightMarket"),
                Subtitle = type == PlayerContractType.QuickJob
                    ? LocalizedText.Get("tablet.contracts.quickJobsSubtitle")
                    : LocalizedText.Get("tablet.contracts.freightMarketSubtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.dispatch.footer"),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildAcceptedContractsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var overview = context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview();
            var acceptedContracts = context.StateStore.GetAcceptedPlayerContracts()
                .Where(contract => contract != null)
                .ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    overview.AcceptedHeadline ?? LocalizedText.Get("tablet.jobs.acceptedFallbackHeadline"),
                    overview.AcceptedDetail ?? LocalizedText.Get("tablet.jobs.acceptedFallbackDetail")),
            };

            if (acceptedContracts.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.dispatch.accepted.emptyCaption"),
                    LocalizedText.Get("tablet.dispatch.accepted.emptyDetail")));
            }
            else
            {
                for (int i = 0; i < acceptedContracts.Count; i++)
                {
                    var contract = acceptedContracts[i];
                    var contractId = contract.Id;
                    items.Add(TabletUiHelpers.CreateActionItem(
                        BuildPlayerContractCaption(contract),
                        BuildPlayerContractListDetail(contract),
                        () => context.Push(TabletAppIds.Network, "dispatch-contract-detail", contractId),
                        iconLabel: "ACT"));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.dispatch.accepted.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.dispatch.accepted.title"),
                Subtitle = LocalizedText.Get("tablet.dispatch.accepted.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildPlayerContractDetailPage(TabletShellContext context, string contractId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var contract = context.StateStore.GetPlayerContractById(contractId);
            if (contract == null)
            {
                return new TabletShellPage
                {
                    Title = LocalizedText.Get("tablet.contracts.detailTitle"),
                    Subtitle = LocalizedText.Get("tablet.contracts.unavailableSubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    WidthScale = 0.9f,
                    MaxVisibleItems = 4,
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.contracts.unavailableSubtitle"), LocalizedText.Get("tablet.contracts.unavailableDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.contracts.backPreviousDispatch"), () => context.GoBack(), "BACK"),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    BuildPlayerContractCaption(contract),
                    LocalizedText.Format("tablet.contracts.statusLine", FormatPlayerContractType(contract.Type), contract.StatusDetail ?? string.Empty).Trim()),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.contracts.payout"),
                    LocalizedText.Format(
                        "tablet.contracts.payoutPanelDetail",
                        ModFormatting.FormatMoney(contract.QuotedGrossPayout),
                        ModFormatting.FormatMoney(contract.CurrentEstimatedGrossPayout > 0.001f ? contract.CurrentEstimatedGrossPayout : contract.QuotedGrossPayout),
                        ModFormatting.FormatPercent(contract.CurrentImbalanceScore * 100f))),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.contracts.vehicle"),
                    BuildPlayerContractVehicleDetail(contract)),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.contracts.reputation"),
                    BuildPlayerContractReputationDetail(contract)),
            };

            if (contract.Status == PlayerContractStatus.Listed && contract.CanAccept)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.contracts.accept"),
                    contract.Type == PlayerContractType.QuickJob
                        ? LocalizedText.Get("tablet.contracts.acceptQuickJobDetail")
                        : LocalizedText.Get("tablet.contracts.acceptFreightDetail"),
                    () =>
                    {
                        string message;
                        var accepted = context.StateStore.TryAcceptPlayerContract(contract.Id, out message);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            _showStatus?.Invoke(message);
                        }

                        if (accepted)
                        {
                            context.Navigate(TabletAppIds.Network, "dispatch-accepted-contracts");
                        }
                        else
                        {
                            context.Refresh();
                        }
                    },
                    iconLabel: "OK"));
            }

            if (contract.Type == PlayerContractType.QuickJob && contract.NeedsQuickJobVehicleDeploy)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.contracts.deployQuickJobVehicle"),
                    LocalizedText.Get("tablet.contracts.deployQuickJobVehicleDetail"),
                    () =>
                    {
                        string message;
                        var deployed = context.StateStore.TryDeployQuickJobVehicle(contract.Id, out message);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            _showStatus?.Invoke(message);
                        }

                        if (deployed)
                        {
                            context.Refresh();
                        }
                    },
                    iconLabel: "DEP"));
            }

            if (contract.Status == PlayerContractStatus.Accepted || contract.Status == PlayerContractStatus.Loaded)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.contracts.cancel"),
                    LocalizedText.Get("tablet.contracts.cancelDetail"),
                    () =>
                    {
                        string message;
                        var cancelled = context.StateStore.TryCancelPlayerContract(contract.Id, out message);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            _showStatus?.Invoke(message);
                        }

                        if (cancelled)
                        {
                            context.Navigate(TabletAppIds.Network, "dispatch-accepted-contracts");
                        }
                        else
                        {
                            context.Refresh();
                        }
                    },
                    iconLabel: "CAN"));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.contracts.backPreviousDispatch"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = FormatPlayerContractType(contract.Type),
                Subtitle = LocalizedText.Format("tablet.contracts.stageSubtitle", contract.StageLabel ?? LocalizedText.Get("tablet.dispatch.jobType.dispatch")),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private static string BuildPlayerContractCaption(PlayerContractListingSummary contract)
        {
            if (contract == null)
            {
                return LocalizedText.Get("tablet.contracts.contract");
            }

            return LocalizedText.Format(
                "tablet.contracts.captionRoute",
                contract.Commodity ?? LocalizedText.Get("tablet.contracts.cargo"),
                contract.OriginName ?? contract.OriginIndustryId ?? LocalizedText.Get("tablet.contracts.origin"),
                contract.DestinationName ?? contract.DestinationIndustryId ?? LocalizedText.Get("tablet.contracts.destination"));
        }

        private static string BuildPlayerContractListDetail(PlayerContractListingSummary contract)
        {
            if (contract == null)
            {
                return LocalizedText.Get("tablet.contracts.noDetails");
            }

            var payoutLabel = ModFormatting.FormatMoney(contract.CurrentEstimatedGrossPayout > 0.001f ? contract.CurrentEstimatedGrossPayout : contract.QuotedGrossPayout);
            var payoutDensityLabel = string.IsNullOrWhiteSpace(contract.PayoutDensityLabel) ? LocalizedText.Get("tablet.contracts.naPerKm") : contract.PayoutDensityLabel;
            var routeDistrictLabel = string.IsNullOrWhiteSpace(contract.RouteDistrictLabel) ? LocalizedText.Get("tablet.contracts.unknownDistrict") : contract.RouteDistrictLabel;
            var rigClassLabel = string.IsNullOrWhiteSpace(contract.RigClassLabel) ? LocalizedText.Get("tablet.contracts.unknown") : contract.RigClassLabel;
            var shipperLabel = string.IsNullOrWhiteSpace(contract.ShipperDisplayName) ? LocalizedText.Get("tablet.contracts.unknownShipper") : contract.ShipperDisplayName;
            var trustLabel = string.IsNullOrWhiteSpace(contract.ShipperTrustLabel) ? LocalizedText.Get("tablet.contracts.trustNew") : contract.ShipperTrustLabel;

            if (contract.Status == PlayerContractStatus.Listed)
            {
                return LocalizedText.Format(
                    "tablet.contracts.listedDetail",
                    ModFormatting.FormatTons(contract.ListedTons),
                    payoutLabel,
                    payoutDensityLabel,
                    routeDistrictLabel,
                    shipperLabel,
                    trustLabel,
                    rigClassLabel,
                    BuildPlayerContractExpiryLabel(contract)).Trim();
            }

            return LocalizedText.Format(
                "tablet.contracts.activeDetail",
                routeDistrictLabel,
                shipperLabel,
                trustLabel,
                payoutLabel,
                payoutDensityLabel,
                rigClassLabel,
                contract.StatusDetail ?? string.Empty).Trim();
        }

        private static string BuildPlayerContractVehicleDetail(PlayerContractListingSummary contract)
        {
            if (contract == null)
            {
                return LocalizedText.Get("tablet.contracts.noVehicleRequirement");
            }

            if (contract.Type == PlayerContractType.QuickJob)
            {
                return contract.NeedsQuickJobVehicleDeploy
                    ? LocalizedText.Get("tablet.contracts.quickJobVehicleQueued")
                    : LocalizedText.Format("tablet.contracts.quickJobVehicle", contract.VehicleRequirementLabel ?? string.Empty).Trim();
            }

            if (!string.IsNullOrWhiteSpace(contract.AssignedVehicleDisplayName))
            {
                return LocalizedText.Format("tablet.contracts.assignedVehicle", contract.AssignedVehicleDisplayName);
            }

            return string.IsNullOrWhiteSpace(contract.VehicleRequirementLabel)
                ? LocalizedText.Get("tablet.contracts.compatibleCompanyVehicle")
                : contract.VehicleRequirementLabel;
        }

        private static string BuildPlayerContractExpiryLabel(PlayerContractListingSummary contract)
        {
            return TabletDeadlineFormatter.BuildPlayerContractExpiryLabel(contract);
        }

        private static string BuildPlayerContractReputationDetail(PlayerContractListingSummary contract)
        {
            if (contract == null)
            {
                return LocalizedText.Get("tablet.contracts.noReputationContext");
            }

            var shipper = string.IsNullOrWhiteSpace(contract.ShipperDisplayName) ? LocalizedText.Get("tablet.contracts.unknownShipper") : contract.ShipperDisplayName;
            var trust = string.IsNullOrWhiteSpace(contract.ShipperTrustLabel) ? LocalizedText.Get("tablet.contracts.trustNew") : contract.ShipperTrustLabel;
            var district = string.IsNullOrWhiteSpace(contract.DistrictStandingLabel) ? LocalizedText.Get("tablet.contracts.districtStandingUnavailable") : contract.DistrictStandingLabel;
            var hint = string.IsNullOrWhiteSpace(contract.UnlockHint) ? string.Empty : contract.UnlockHint;
            var premium = contract.IsPremiumOpportunity ? LocalizedText.Get("tablet.contracts.premiumLane") : LocalizedText.Get("tablet.contracts.baselineLane");

            return LocalizedText.Format(
                "tablet.contracts.reputationDetail",
                shipper,
                trust,
                district,
                premium,
                string.IsNullOrWhiteSpace(hint) ? string.Empty : LocalizedText.Format("tablet.contracts.hintSuffix", hint));
        }

        private static MenuItem CreatePlayerContractsSortSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.sort"),
                GetPlayerContractsSortLabel,
                detail ?? LocalizedText.Get("tablet.contracts.sortDetail"),
                CyclePlayerContractsSortMode,
                "SRT");
        }

        private static MenuItem CreatePlayerContractsExpiryFilterSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.expiry"),
                GetPlayerContractsExpiryFilterLabel,
                detail ?? LocalizedText.Get("tablet.contracts.expiryDetail"),
                CyclePlayerContractsExpiryFilter,
                "EXP");
        }

        private static MenuItem CreatePlayerContractsPayoutDensityFilterSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.payoutDensity"),
                GetPlayerContractsPayoutDensityFilterLabel,
                detail ?? LocalizedText.Get("tablet.contracts.payoutDensityDetail"),
                CyclePlayerContractsPayoutDensityFilter,
                "DEN");
        }

        private static MenuItem CreatePlayerContractsRigClassFilterSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.rigClass"),
                GetPlayerContractsRigClassFilterLabel,
                detail ?? LocalizedText.Get("tablet.contracts.rigClassDetail"),
                CyclePlayerContractsRigClassFilter,
                "RIG");
        }

        private static MenuItem CreatePlayerContractsDistrictFilterSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.district"),
                GetPlayerContractsDistrictFilterLabel,
                detail ?? LocalizedText.Get("tablet.contracts.districtDetail"),
                CyclePlayerContractsDistrictFilter,
                "DST");
        }

        private static MenuItem CreatePlayerContractsCommodityFilterSelectorItem(TabletShellContext context, string captionPrefix, string detail)
        {
            return CreatePlayerContractsBoardSelectorItem(
                context,
                captionPrefix ?? LocalizedText.Get("tablet.contracts.commodity"),
                GetPlayerContractsCommodityFilterLabel,
                detail ?? LocalizedText.Get("tablet.contracts.commodityDetail"),
                CyclePlayerContractsCommodityFilter,
                "COM");
        }

        private static MenuItem CreatePlayerContractsBoardSelectorItem(TabletShellContext context, string captionPrefix, Func<TabletShellContext, string> labelGetter, string detail, Action<TabletShellContext, int> cycleAction, string iconLabel)
        {
            return TabletUiHelpers.CreateSelectorItem(
                () => LocalizedText.Format("tablet.contracts.caption", captionPrefix, labelGetter != null ? labelGetter(context) : LocalizedText.Get("tablet.contracts.any")),
                () => detail ?? string.Empty,
                () =>
                {
                    if (cycleAction != null)
                    {
                        cycleAction(context, -1);
                    }
                },
                () =>
                {
                    if (cycleAction != null)
                    {
                        cycleAction(context, 1);
                    }
                },
                () =>
                {
                    if (cycleAction != null)
                    {
                        cycleAction(context, 1);
                    }
                },
                iconLabel);
        }

        private static string GetPlayerContractsCommodityFilterLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedCommodityFilter)
                ? LocalizedText.Get("tablet.contracts.any")
                : overview.SelectedCommodityFilter;
        }

        private static string GetPlayerContractsDistrictFilterLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedDistrictFilter)
                ? LocalizedText.Get("tablet.contracts.any")
                : overview.SelectedDistrictFilter;
        }

        private static string GetPlayerContractsRigClassFilterLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedRigClassFilter)
                ? LocalizedText.Get("tablet.contracts.any")
                : overview.SelectedRigClassFilter;
        }

        private static string GetPlayerContractsExpiryFilterLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedExpiryFilter)
                ? LocalizedText.Get("tablet.contracts.any")
                : overview.SelectedExpiryFilter;
        }

        private static string GetPlayerContractsPayoutDensityFilterLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedPayoutDensityFilter)
                ? LocalizedText.Get("tablet.contracts.any")
                : overview.SelectedPayoutDensityFilter;
        }

        private static string GetPlayerContractsSortLabel(TabletShellContext context)
        {
            var overview = GetPlayerContractsOverview(context);
            return string.IsNullOrWhiteSpace(overview.SelectedSortMode)
                ? LocalizedText.Get("tablet.contracts.board")
                : overview.SelectedSortMode;
        }

        private static PlayerContractsOverview GetPlayerContractsOverview(TabletShellContext context)
        {
            return context != null && context.StateStore != null
                ? context.StateStore.GetPlayerContractsOverview() ?? new PlayerContractsOverview()
                : new PlayerContractsOverview();
        }

        private static void CyclePlayerContractsCommodityFilter(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractCommodityFilter(delta);
            context.Refresh();
        }

        private static void CyclePlayerContractsDistrictFilter(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractDistrictFilter(delta);
            context.Refresh();
        }

        private static void CyclePlayerContractsRigClassFilter(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractRigClassFilter(delta);
            context.Refresh();
        }

        private static void CyclePlayerContractsExpiryFilter(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractExpiryFilter(delta);
            context.Refresh();
        }

        private static void CyclePlayerContractsPayoutDensityFilter(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractPayoutDensityFilter(delta);
            context.Refresh();
        }

        private static void CyclePlayerContractsSortMode(TabletShellContext context, int delta)
        {
            if (context == null)
            {
                return;
            }

            context.StateStore.CyclePlayerContractSortMode(delta);
            context.Refresh();
        }

        private static string FormatPlayerContractType(PlayerContractType type)
        {
            return type == PlayerContractType.QuickJob ? LocalizedText.Get("tablet.jobs.quickJob") : LocalizedText.Get("tablet.jobs.freightMarket");
        }

        private TabletShellPage BuildDispatchDiagnosticsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var overview = context.StateStore.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview();
            var diagnostics = context.StateStore.GetWorldDispatchDiagnostics()
                .Where(entry => entry != null)
                .Where(entry => _dispatchDiagnosticsFilterMode == DispatchDiagnosticsFilterMode.All || entry.IsFailure)
                .ToList();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.dispatch.diagnostics.summaryCaption"),
                    BuildDispatchDiagnosticsSummary(overview)),
                TabletUiHelpers.CreateSelectorItem(
                    () => LocalizedText.Format(
                        "tablet.dispatch.diagnostics.filterCaption",
                        _dispatchDiagnosticsFilterMode == DispatchDiagnosticsFilterMode.FailuresOnly
                            ? LocalizedText.Get("tablet.dispatch.diagnostics.filter.failuresOnly")
                            : LocalizedText.Get("tablet.dispatch.diagnostics.filter.allEvents")),
                    () => LocalizedText.Get("tablet.dispatch.diagnostics.filterDetail"),
                    () => CycleDispatchDiagnosticsFilter(context),
                    () => CycleDispatchDiagnosticsFilter(context),
                    () => CycleDispatchDiagnosticsFilter(context),
                    "FLT"),
            };

            if (diagnostics.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    _dispatchDiagnosticsFilterMode == DispatchDiagnosticsFilterMode.FailuresOnly
                        ? LocalizedText.Get("tablet.dispatch.diagnostics.empty.failuresCaption")
                        : LocalizedText.Get("tablet.dispatch.diagnostics.empty.allCaption"),
                    _dispatchDiagnosticsFilterMode == DispatchDiagnosticsFilterMode.FailuresOnly
                        ? LocalizedText.Get("tablet.dispatch.diagnostics.empty.failuresDetail")
                        : LocalizedText.Get("tablet.dispatch.diagnostics.empty.allDetail")));
            }
            else
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    var entry = diagnostics[i];
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        BuildDispatchDiagnosticCaption(entry),
                        BuildDispatchDiagnosticDetail(entry)));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.dispatch.diagnostics.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.dispatch.diagnostics.title"),
                Subtitle = LocalizedText.Get("tablet.dispatch.diagnostics.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.dispatch.diagnostics.footer"),
                WidthScale = 0.96f,
                MaxVisibleItems = 6,
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
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, LocalizedText.Get("tablet.market.selector.timeframeDetail")),
                TabletUiHelpers.CreateCommoditySelectorItem(context, LocalizedText.Get("tablet.market.selector.resourceDetail")),
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.market.highlights"),
                    LocalizedText.Format("tablet.market.summary", snapshot.MarketMultiplier, TabletUiHelpers.BuildMarketSummary(snapshot)))
            };

            if (snapshot.MarketHighlights != null)
            {
                for (int i = 0; i < snapshot.MarketHighlights.Count; i++)
                {
                    var highlight = snapshot.MarketHighlights[i];
                    var commodity = highlight.Commodity;
                    var isSelectedCommodity = string.Equals(commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase);
                    items.Add(TabletUiHelpers.CreateActionItem(
                        isSelectedCommodity ? LocalizedText.Format("tablet.market.selectedCaption", commodity, LocalizedText.Get("tablet.market.trendTag")) : commodity,
                        LocalizedText.Format("tablet.market.highlightDetail", highlight.UnitPrice, highlight.Reason),
                        () =>
                        {
                            context.StateStore.SetSelectedTrendCommodity(commodity);
                            context.Refresh();
                        }));
                }
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.market.resources.title"),
                LocalizedText.Get("tablet.market.resources.detail"),
                () => context.Push(TabletAppIds.Network, "prices"),
                iconLabel: "$"));
            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.market.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.home.market"),
                Subtitle = LocalizedText.Get("tablet.market.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.market.footer"),
                WidthScale = 0.86f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 132f,
                BottomPanelRenderer = panel =>
                {
                    if (selectedPrice == null)
                    {
                        TabletChartRenderer.DrawMessagePanel(
                            panel,
                            LocalizedText.Get(ModTextKey.TabletAnalyticsCommodityTrend),
                            LocalizedText.Get("tablet.market.chart.noSelectionSubtitle"),
                            LocalizedText.Get("tablet.market.chart.selectFromBoard"));
                        return;
                    }

                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        LocalizedText.Format("tablet.market.chart.priceTrendTitle", selectedPrice.Commodity),
                        LocalizedText.Format(
                            "tablet.market.chart.priceTrendSubtitle",
                            selectedPrice.CargoType.ToDisplayName(),
                            context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                            selectedPrice.UnitPrice),
                        context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 214, 168, 94),
                        value => LocalizedText.Format("tablet.market.valuePerTon", value));
                },
                Items = items,
            };
        }

        private static string FormatWorldDispatchPolicy(NpcWorldDispatchPolicy policy)
        {
            switch (policy)
            {
                case NpcWorldDispatchPolicy.OverflowRescue:
                    return LocalizedText.Get("tablet.dispatch.policy.overflowRescue");
                case NpcWorldDispatchPolicy.ShortageRelief:
                    return LocalizedText.Get("tablet.dispatch.policy.shortageRelief");
                case NpcWorldDispatchPolicy.MarketPriority:
                    return LocalizedText.Get("tablet.dispatch.policy.marketPriority");
                default:
                    return LocalizedText.Get("tablet.dispatch.policy.balanced");
            }
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
                TabletUiHelpers.CreateGraphTimeframeSelectorItem(context, LocalizedText.Get("tablet.market.resources.selector.timeframeDetail")),
                TabletUiHelpers.CreateCommoditySelectorItem(context, LocalizedText.Get("tablet.market.resources.selector.resourceDetail")),
            };

            if (snapshot.MarketPrices != null)
            {
                for (int i = 0; i < snapshot.MarketPrices.Count; i++)
                {
                    var price = snapshot.MarketPrices[i];
                    var commodity = price.Commodity;
                    var isSelectedCommodity = string.Equals(commodity, selectedCommodity, StringComparison.OrdinalIgnoreCase);
                    items.Add(TabletUiHelpers.CreateActionItem(
                        isSelectedCommodity ? LocalizedText.Format("tablet.market.selectedCaption", commodity, LocalizedText.Get("tablet.market.trendTag")) : commodity,
                        LocalizedText.Format("tablet.market.resources.rowDetail", price.UnitPrice, price.CargoType.ToDisplayName()),
                        () =>
                        {
                            context.StateStore.SetSelectedTrendCommodity(commodity);
                            context.Refresh();
                        }));
                }
            }

            if (prices.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.market.resources.noPricesCaption"), LocalizedText.Get("tablet.market.resources.noPricesDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.market.resources.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.market.resources.title"),
                Subtitle = LocalizedText.Get("tablet.market.resources.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = LocalizedText.Get("tablet.market.resources.footer"),
                WidthScale = 0.88f,
                MaxVisibleItems = 6,
                BottomPanelHeight = 138f,
                BottomPanelRenderer = panel =>
                {
                    if (selectedPrice == null)
                    {
                        TabletChartRenderer.DrawMessagePanel(
                            panel,
                            LocalizedText.Get(ModTextKey.TabletAnalyticsCommodityTrend),
                            LocalizedText.Get("tablet.market.chart.noSelectionSubtitle"),
                            LocalizedText.Get("tablet.market.resources.chart.selectResource"));
                        return;
                    }

                    TabletChartRenderer.DrawHistoryPanel(
                        panel,
                        LocalizedText.Format("tablet.market.chart.priceTrendTitle", selectedPrice.Commodity),
                        LocalizedText.Format(
                            "tablet.market.chart.priceTrendSubtitle",
                            selectedPrice.CargoType.ToDisplayName(),
                            context.StateStore.SelectedGraphTimeframe.ToDisplayLabel(),
                            selectedPrice.UnitPrice),
                        context.StateStore.GetCommodityPriceHistory(selectedPrice.Commodity, context.StateStore.SelectedGraphTimeframe),
                        Color.FromArgb(214, 214, 168, 94),
                        value => LocalizedText.Format("tablet.market.valuePerTon", value));
                },
                Items = items,
            };
        }

        private TabletShellPage BuildIndustryListPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            var industrySummaries = ApplyFilter(snapshot.IndustrySummaries, _industryFilterMode);

            items.Add(TabletUiHelpers.CreateSelectorItem(
                () => BuildFilterCaption(_industryFilterMode),
                () => BuildFilterDetail(LocalizedText.Get("tablet.location.list.industriesSubject")),
                () => CycleIndustryFilter(context, -1),
                () => CycleIndustryFilter(context, 1),
                () => CycleIndustryFilter(context, 1),
                "FLT"));

            for (int i = 0; i < industrySummaries.Count; i++)
            {
                var summary = industrySummaries[i];
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildLocationCaption(summary),
                    TabletUiHelpers.BuildLocationOverviewDetail(summary),
                    () => context.Push(TabletAppIds.Network, "detail", summary.Industry)));
            }

            if (items.Count == 1)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.list.industriesEmptyCaption"), LocalizedText.Get("tablet.location.list.industriesEmptyDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.list.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.location.list.industriesTitle"),
                Subtitle = LocalizedText.Get("tablet.location.list.industriesSubtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.98f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildStoreListPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            return BuildFilteredLocationListPage(
                context,
                LocalizedText.Get("tablet.location.type.store"),
                LocalizedText.Get("tablet.location.list.storesSubtitle"),
                snapshot.StoreSummaries,
                true,
                false,
                _storeFilterMode,
                delta => CycleStoreFilter(context, delta));
        }

        private TabletShellPage BuildStationListPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            return BuildFilteredLocationListPage(
                context,
                LocalizedText.Get("tablet.network.gasStations"),
                LocalizedText.Get("tablet.location.list.gasStationsSubtitle"),
                snapshot.GasStationSummaries,
                true,
                false,
                _stationFilterMode,
                delta => CycleStationFilter(context, delta));
        }

        private TabletShellPage BuildLocationListPage(
            TabletShellContext context,
            string title,
            string subtitle,
            IReadOnlyList<TabletLocationSummary> summaries,
            bool openDetail,
            bool includePermitLink)
        {
            return BuildFilteredLocationListPage(context, title, subtitle, summaries, openDetail, includePermitLink, LocationListFilterMode.All, null);
        }

        private TabletShellPage BuildFilteredLocationListPage(
            TabletShellContext context,
            string title,
            string subtitle,
            IReadOnlyList<TabletLocationSummary> summaries,
            bool openDetail,
            bool includePermitLink,
            LocationListFilterMode filterMode,
            Action<int> cycleFilter)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var items = new List<MenuItem>();
            if (includePermitLink && summaries != null && summaries.Count > 0)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.network.contractorPermits"),
                    LocalizedText.Format("tablet.location.list.permitsDetail", summaries.Count),
                    () => context.Push(TabletAppIds.Network, "permits")));
            }

            if (cycleFilter != null)
            {
                items.Add(TabletUiHelpers.CreateSelectorItem(
                    () => BuildFilterCaption(filterMode),
                    () => BuildFilterDetail(title),
                    () => cycleFilter(-1),
                    () => cycleFilter(1),
                    () => cycleFilter(1),
                    "FLT"));
            }

            var filteredSummaries = cycleFilter == null ? (summaries ?? Array.Empty<TabletLocationSummary>()).ToList() : ApplyFilter(summaries, filterMode);
            if (filteredSummaries != null)
            {
                for (int i = 0; i < filteredSummaries.Count; i++)
                {
                    var summary = filteredSummaries[i];
                    items.Add(TabletUiHelpers.CreateActionItem(
                        TabletUiHelpers.BuildLocationCaption(summary),
                        TabletUiHelpers.BuildLocationOverviewDetail(summary),
                        openDetail ? (Action)(() => context.Push(TabletAppIds.Network, "detail", summary.Industry)) : null));
                }
            }

            if (items.Count == 0 || (cycleFilter != null && items.Count == 1))
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Format("tablet.location.list.emptyCaption", title.ToLowerInvariant()), LocalizedText.Get("tablet.location.list.emptyDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.list.backDetail"), () => context.GoBack(), "BACK"));

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
            var permitSummaries = ApplyFilter(snapshot.IndustrySummaries.Concat(snapshot.ConstructionSiteSummaries), _permitFilterMode);
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateSelectorItem(
                    () => BuildFilterCaption(_permitFilterMode),
                    () => BuildFilterDetail(LocalizedText.Get("tablet.permit.filterSubject")),
                    () => CyclePermitFilter(context, -1),
                    () => CyclePermitFilter(context, 1),
                    () => CyclePermitFilter(context, 1),
                    "FLT"),
            };
            for (int i = 0; i < permitSummaries.Count; i++)
            {
                var summary = permitSummaries[i];
                var detail = !summary.RequiresContractorPermit
                    ? LocalizedText.Format("tablet.permit.noPermitRequired", LocalizedText.Get("tablet.permit.statusOpen"))
                    : LocalizedText.Format(
                        "tablet.permit.entryDetail",
                        summary.HasContractorPermitForGameplay ? LocalizedText.Get("tablet.permit.statusOwned") : LocalizedText.Get("tablet.permit.statusLocked"),
                        ModFormatting.FormatMoney(summary.Industry.IndustryLicencePrice),
                        summary.HasContractorPermitForGameplay ? LocalizedText.Get("tablet.permit.transportUnlocked") : string.Empty);
                items.Add(TabletUiHelpers.CreateActionItem(
                    TabletUiHelpers.BuildPermitCaption(summary),
                    detail,
                    summary.RequiresContractorPermit ? (Action)(() => context.Push(TabletAppIds.Network, "permit-confirm", summary.Industry)) : null));
            }

            if (items.Count == 1)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.permit.emptyCaption"), LocalizedText.Get("tablet.permit.emptyDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.permit.backDetail"), () => context.GoBack()));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.permit.title"),
                Subtitle = LocalizedText.Get("tablet.permit.listSubtitle"),
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
                    Title = LocalizedText.Get("tablet.permit.title"),
                    Subtitle = LocalizedText.Get("tablet.permit.noIndustrySubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    WidthScale = 0.84f,
                    MaxVisibleItems = 4,
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.permit.noIndustryCaption"), LocalizedText.Get("tablet.permit.noIndustryDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.permit.backDetail"), () => context.GoBack()),
                    },
                };
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.permit.purchase"),
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
                TabletUiHelpers.CreateNavigationItem(LocalizedText.Get("tablet.permit.cancel"), LocalizedText.Get("tablet.permit.backDetail"), () => context.GoBack()),
            };

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.permit.title"),
                Subtitle = LocalizedText.Format("tablet.permit.confirmSubtitle", industry.Name),
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
                    Title = LocalizedText.Get("tablet.location.detail.title"),
                    Subtitle = LocalizedText.Get("tablet.location.detail.noSiteSubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.detail.noSiteCaption"), LocalizedText.Get("tablet.location.detail.noSiteDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.detail.backDetail"), () => context.GoBack()),
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
                    Title = LocalizedText.Get("tablet.location.detail.title"),
                    Subtitle = LocalizedText.Get("tablet.location.detail.noSiteSubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.detail.noSiteCaption"), LocalizedText.Get("tablet.location.detail.noSiteDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.detail.backDetail"), () => context.GoBack()),
                    },
                };
            }

            if (industry.IsStore)
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    LocalizedText.Get("tablet.location.statistics.footer"));
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    LocalizedText.Get("tablet.location.statistics.footer"));
            }

            if (summary.LocationKind == LSOL.Config.ExternalLocationKind.Industry && industry.SiteRole != SiteRole.Warehouse)
            {
                return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                    context,
                    snapshot,
                    industry,
                    LocalizedText.Get("tablet.location.statistics.footer"));
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
                    LocalizedText.Get("tablet.location.statistics.openNearby"),
                    LocalizedText.Format("tablet.location.statistics.openNearbyDetail", industry.Name),
                    () => context.Push(TabletAppIds.Industry, "main", industry)));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.detail.backDetail"), () => context.GoBack()));

            return new TabletShellPage
            {
                Title = industry.Name,
                Subtitle = industry.SiteRole == SiteRole.Warehouse
                    ? LocalizedText.Get("tablet.location.statistics.warehouseSubtitle")
                    : LocalizedText.Get("tablet.location.statistics.defaultSubtitle"),
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
                    Title = LocalizedText.Get("tablet.location.type.industry"),
                    Subtitle = LocalizedText.Get("tablet.location.noIndustrySubtitle"),
                    HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                    Items = new[]
                    {
                        TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.noIndustryCaption"), LocalizedText.Get("tablet.location.noIndustryDetail")),
                        TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.detail.backDetail"), () => context.GoBack()),
                    },
                };
            }

            var summary = TabletUiHelpers.FindSummary(context, industry);

            var title = industry.IsGasStation
                ? LocalizedText.Get("tablet.location.type.petrolStation")
                : (industry.SiteRole == SiteRole.Warehouse
                    ? LocalizedText.Get("tablet.location.type.warehouse")
                    : (industry.SiteRole == SiteRole.ConstructionSiteSink ? LocalizedText.Get("tablet.location.type.constructionSite") : (industry.IsStore ? LocalizedText.Get("tablet.location.type.store") : LocalizedText.Get("tablet.location.type.industry"))));
            var statisticsDetail = industry.SiteRole == SiteRole.Warehouse
                ? LocalizedText.Get("tablet.location.statisticsDetail.warehouse")
                : (industry.IsGasStation
                    ? LocalizedText.Get("tablet.location.statisticsDetail.petrolStation")
                    : (industry.SiteRole == SiteRole.ConstructionSiteSink
                        ? LocalizedText.Get("tablet.location.statisticsDetail.constructionSite")
                        : (industry.IsStore
                        ? LocalizedText.Get("tablet.location.statisticsDetail.store")
                        : LocalizedText.Get("tablet.location.statisticsDetail.industry"))));

            var items = new List<MenuItem>();
            var ownerCutDetail = TabletLocationEconomicsFormatter.BuildOwnerCutDetail(summary);
            if (!string.IsNullOrWhiteSpace(ownerCutDetail))
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.ownerCut.caption"), ownerCutDetail));
            }

            if (summary != null && summary.HasServiceBusinessInfo)
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    string.Format("{0} {1} {2}", summary.Name, summary.OwnershipTag, summary.PermitTag),
                    ServiceStatusCatalog.Display(summary.ServicePassiveIncomeStatus)));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.Get("tablet.location.passiveIncome.caption"),
                    BuildServiceSiteIncomeDetail(summary)));

                if (_toggleServiceSiteOperator != null && summary.IsOwnedByPlayer)
                {
                    items.Add(TabletUiHelpers.CreateActionItem(
                        summary.ServiceStaffAssigned ? LocalizedText.Get("tablet.location.operator.release") : LocalizedText.Get("tablet.location.operator.assign"),
                        BuildServiceSiteStaffActionDetail(summary),
                        () =>
                        {
                            var message = _toggleServiceSiteOperator(industry);
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                _showStatus?.Invoke(message);
                            }

                            context.Refresh();
                        }));
                }
            }

            if (summary != null)
            {
                var weeklyTargetDetail = TabletServiceSiteStatusFormatter.BuildWeeklyTargetDetail(summary);
                if (!string.IsNullOrWhiteSpace(weeklyTargetDetail))
                {
                    items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.weeklyTarget.caption"), weeklyTargetDetail));
                }

                var contractStatusDetail = TabletServiceSiteStatusFormatter.BuildContractStatusDetail(summary);
                if (!string.IsNullOrWhiteSpace(contractStatusDetail))
                {
                    items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.location.contract.caption"), contractStatusDetail));
                }
            }

            if (summary != null && summary.RequiresIndustryPurchase)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.location.purchaseSite"),
                    LocalizedText.Format(
                        "tablet.location.purchaseSiteDetail",
                        industry.Name,
                        ModFormatting.FormatMoney(industry.IndustryPrice)),
                    () => context.Push(TabletAppIds.Industry, "purchase-confirm", industry)));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.location.viewStatistics"),
                statisticsDetail,
                () => context.Push(TabletAppIds.Network, "stats", industry)));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.location.gps.add"),
                LocalizedText.Get("tablet.location.gps.addDetail"),
                () => _addGpsRoute?.Invoke(industry)));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.location.gps.clear"),
                LocalizedText.Get("tablet.location.gps.clearDetail"),
                () => _clearGpsRoute?.Invoke()));
            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.location.backToSiteList"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = title,
                Subtitle = industry.Name,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 7,
                Items = items,
            };
        }

        private static string BuildServiceSiteIncomeDetail(TabletLocationSummary summary)
        {
            return TabletLocationEconomicsFormatter.BuildPassiveIncomeDetail(summary);
        }

        private static string BuildServiceSiteOperationsDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            var segments = new List<string>
            {
                ServiceStatusCatalog.Display(summary.ServiceOperationsStatus),
                ServiceStatusCatalog.Display(summary.ServicePassiveIncomeStatus),
            };

            return string.Join(" | ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).ToArray());
        }

        private static string BuildServiceSiteStaffActionDetail(TabletLocationSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            return summary.ServiceStaffAssigned
                ? LocalizedText.Format("tablet.location.staff.releaseDetail", ModFormatting.FormatMoney(summary.ServiceWeeklyStaffingCost))
                : LocalizedText.Format("tablet.location.staff.assignDetail", ModFormatting.FormatMoney(summary.ServiceWeeklyStaffingCost));
        }

        private TabletShellPage BuildIndustryStatisticsPage(TabletShellContext context, Industry industry)
        {
            return BuildLocationStatisticsPage(context, industry);
        }

        private static string BuildPermitConfirmDetail(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var detail = LocalizedText.Format(
                "tablet.permit.confirmDetail",
                ModFormatting.FormatMoney(industry.IndustryLicencePrice));
            if (snapshot.Balance < industry.IndustryLicencePrice)
            {
                detail += LocalizedText.Format("tablet.permit.confirmNeedMore", ModFormatting.FormatMoney(industry.IndustryLicencePrice - snapshot.Balance));
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.noIndustry"), () => context.GoBack());
            }

            if (!context.StateStore.IsIndustryInRange(industry, _interactionDistance, out distance))
            {
                return BuildUnavailablePage(snapshot, LocalizedText.Format("tablet.industry.unavailable.signalLost", industry.Name), () => context.GoBack());
            }

            var items = new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(snapshot.StatusBanner))
            {
                items.Add(TabletUiHelpers.CreateBannerItem(LocalizedText.Get("tablet.network.status"), snapshot.StatusBanner));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateBannerItem(
                    string.Format("{0} {1} {2}", industry.Name, summary.OwnershipTag, summary.PermitTag),
                    TabletUiHelpers.BuildIndustryStatusDetail(summary, industry)));
            }

            var loadSnapshot = context.StateStore.GetLoadOptions(industry);
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.industry.loadProduct"),
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
                LocalizedText.Get("tablet.industry.unloadCargo"),
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
                    LocalizedText.Get("tablet.industry.refuel"),
                    BuildRefuelActionDetail(snapshot, industry),
                    () =>
                    {
                        _refuelRequested?.Invoke(industry);
                        context.Refresh();
                    },
                    progress: snapshot.FuelRatio));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                industry.SiteRole == SiteRole.Warehouse ? LocalizedText.Get("tablet.industry.storageDetail") : LocalizedText.Get("tablet.industry.statistics"),
                industry.SiteRole == SiteRole.Warehouse
                    ? LocalizedText.Get("tablet.industry.storageDetailInfo")
                    : LocalizedText.Get("tablet.industry.statisticsInfo"),
                () => context.Push(TabletAppIds.Industry, "stats", industry)));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get("tablet.industry.vehicleSpawn"),
                LocalizedText.Get("tablet.industry.vehicleSpawnDetail"),
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
                        LocalizedText.Get("tablet.industry.buyWarehouse"),
                        LocalizedText.Format("tablet.industry.buyWarehouseDetail", ModFormatting.FormatMoney(industry.IndustryPrice)),
                        () => context.Push(TabletAppIds.Industry, "purchase-confirm", industry)));
                }
            }
            else
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    summary.RequiresIndustryPurchase ? LocalizedText.Get("tablet.industry.buyIndustry") : LocalizedText.Get("tablet.industry.openUpgrades"),
                    summary.RequiresIndustryPurchase
                        ? LocalizedText.Format("tablet.industry.buyIndustryDetail", ModFormatting.FormatMoney(industry.IndustryPrice))
                        : LocalizedText.Get("tablet.industry.openUpgradesDetail"),
                    summary.RequiresIndustryPurchase
                        ? (Action)(() => context.Push(TabletAppIds.Industry, "purchase-confirm", industry))
                        : (Action)(() => context.Push(TabletAppIds.Industry, "upgrades", industry))));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.network.backDetail"), () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = industry.IsGasStation
                    ? LocalizedText.Get("tablet.location.type.petrolStation")
                    : (industry.SiteRole == SiteRole.Warehouse ? LocalizedText.Get("tablet.location.type.warehouse") : LocalizedText.Get("tablet.location.type.industry")),
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.loadRange"), () => context.GoBack());
            }

            var loadSnapshot = context.StateStore.GetLoadOptions(industry);
            var items = new List<MenuItem>();
            for (int i = 0; i < loadSnapshot.LoadOptions.Count; i++)
            {
                var commodity = loadSnapshot.LoadOptions[i];
                string detail;
                if (!loadSnapshot.LoadOptionSubtitles.TryGetValue(commodity, out detail))
                {
                    detail = LocalizedText.Get("tablet.industry.load.pageCommodityDetail");
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
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.industry.load.emptyCaption"), loadSnapshot.StatusText));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get("tablet.industry.operationsBack"), LocalizedText.Get("tablet.industry.operationsBackDetail"), () => context.GoBack()));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.industry.load.pageTitle"),
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.unloadRange"), () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.industry.unload.truck"),
                    LocalizedText.Get("tablet.industry.unload.truckDetail"),
                    () =>
                    {
                        _unloadModeRequested?.Invoke(industry, false);
                        context.Refresh();
                    }),
            };

            if (industry.SupportsOmegaBoost)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.industry.unload.omega"),
                    LocalizedText.Get("tablet.industry.unload.omegaDetail"),
                    () =>
                    {
                        _unloadModeRequested?.Invoke(industry, true);
                        context.Refresh();
                    }));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get("tablet.industry.operationsBack"), LocalizedText.Get("tablet.industry.operationsBackDetail"), () => context.GoBack()));

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.industry.unload.pageTitle"),
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.noIndustry"), () => context.GoBack());
            }

            return TabletUiHelpers.BuildLegacyIndustryStatisticsPage(
                context,
                snapshot,
                industry,
                LocalizedText.Get("tablet.location.statistics.footer"));
        }

        private TabletShellPage BuildUpgradesPage(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = TabletUiHelpers.FindSummary(context, industry);
            if (industry == null || summary == null)
            {
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.noIndustry"), () => context.GoBack());
            }

            if (summary.RequiresIndustryPurchase)
            {
                return BuildPurchaseConfirmPage(context, industry);
            }

            var items = new List<MenuItem>();
            if (industry.SiteRole == SiteRole.Warehouse)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.industry.upgrades.warehouseModules"), LocalizedText.Get("tablet.industry.upgrades.warehouseModulesDetail")));
            }

            var modules = industry.SiteRole == SiteRole.Warehouse
                ? new[]
                {
                    IndustryUpgradeModule.InputStorage,
                    IndustryUpgradeModule.OutputStorage,
                }
                : new[]
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
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.Get("tablet.industry.upgrades.emptyCaption"), LocalizedText.Get("tablet.industry.upgrades.emptyDetail")));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(LocalizedText.Get("tablet.industry.operationsBack"), LocalizedText.Get("tablet.industry.operationsBackDetail"), () => context.GoBack()));

            return new TabletShellPage
            {
                Title = industry.SiteRole == SiteRole.Warehouse ? LocalizedText.Get("tablet.industry.upgrades.warehouseTitle") : LocalizedText.Get("tablet.industry.upgrades.industryTitle"),
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
                return BuildUnavailablePage(snapshot, LocalizedText.Get("tablet.industry.unavailable.noIndustry"), () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateActionItem(
                    LocalizedText.Get("tablet.industry.purchase.action"),
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
                TabletUiHelpers.CreateNavigationItem(LocalizedText.Get("tablet.permit.cancel"), LocalizedText.Get("tablet.industry.operationsBackDetail"), () => context.GoBack()),
            };

            return new TabletShellPage
            {
                Title = LocalizedText.Get("tablet.industry.purchase.pageTitle"),
                Subtitle = LocalizedText.Format("tablet.industry.purchase.pageSubtitle", industry.Name),
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
                Title = LocalizedText.Get("tablet.industry.unavailable.title"),
                Subtitle = LocalizedText.Get("tablet.industry.unavailable.subtitle"),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.82f,
                MaxVisibleItems = 5,
                Items = new[]
                {
                    TabletUiHelpers.CreateBannerItem(LocalizedText.Get("tablet.industry.unavailable.banner"), detail),
                    TabletUiHelpers.CreateNavigationItem(LocalizedText.Get(ModTextKey.TabletNavigationBack), LocalizedText.Get("tablet.market.backDetail"), backAction),
                },
            };
        }

        private static string BuildLoadActionDetail(IndustryLoadOptionsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return LocalizedText.Get("tablet.industry.load.noData");
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
                    : LocalizedText.Format("tablet.industry.load.actionCommodity", commodity);
            }

            return LocalizedText.Format("tablet.industry.load.multipleReady", snapshot.LoadOptions.Count);
        }

        private static string BuildUnloadActionDetail(TabletStateSnapshot snapshot, Industry industry, bool canChooseOmegaUnloadMode)
        {
            if (snapshot == null || !snapshot.HasCargoVehicle)
            {
                return LocalizedText.Get("tablet.industry.unload.noCargoVehicle");
            }

            if (snapshot.CargoIsEmpty)
            {
                return LocalizedText.Get("tablet.industry.unload.emptyVehicle");
            }

            if (canChooseOmegaUnloadMode)
            {
                return LocalizedText.Get("tablet.industry.unload.chooseMode");
            }

            if (CargoTransferController.IsOmegaOnlyUnloadIndustry(industry))
            {
                return LocalizedText.Get("tablet.industry.unload.omegaOnlySite");
            }

            return LocalizedText.Format("tablet.industry.unload.actionCommodity", snapshot.CargoCommodity);
        }

        private static string BuildRefuelActionDetail(TabletStateSnapshot snapshot, Industry industry)
        {
            if (snapshot == null || !snapshot.HasPoweredVehicle || snapshot.FuelCapacityLiters <= 0.001f)
            {
                return LocalizedText.Get("tablet.industry.refuel.noVehicle");
            }

            var tankDetail = snapshot.FuelVehicleMatchesCargoVehicle
                ? LocalizedText.Format("tablet.industry.refuel.tankCurrent", snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters)
                : LocalizedText.Format("tablet.industry.refuel.tankNamed", snapshot.PoweredVehicleName, snapshot.FuelCurrentLiters, snapshot.FuelCapacityLiters);
            var pricingDetail = industry != null && industry.RefuelIsFree
                ? LocalizedText.Get("tablet.industry.refuel.free")
                : LocalizedText.Get("tablet.industry.refuel.market");

            if (snapshot.FuelIsEmpty)
            {
                pricingDetail = industry != null && industry.RefuelIsFree
                    ? LocalizedText.Get("tablet.industry.refuel.emptyFree")
                    : LocalizedText.Get("tablet.industry.refuel.emptyMarket");
            }

            return LocalizedText.Format("tablet.contracts.statusLine", tankDetail, pricingDetail);
        }

        private static string BuildIndustryPurchaseDetail(TabletShellContext context, Industry industry)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var detail = LocalizedText.Format("tablet.industry.purchase.detail", ModFormatting.FormatMoney(industry.IndustryPrice));
            if (industry.IndustryOwnerCut > 0f)
            {
                detail += LocalizedText.Format("tablet.industry.purchase.ownerCut", industry.IndustryOwnerCut * 100f);
            }

            if (snapshot.Balance < industry.IndustryPrice)
            {
                detail += LocalizedText.Format("tablet.permit.confirmNeedMore", ModFormatting.FormatMoney(industry.IndustryPrice - snapshot.Balance));
            }

            return detail;
        }
    }
}
using System;
using System.Collections.Generic;
using LSOL.Domain;

namespace LSOL.UI
{
    internal enum RoutePlannerAvailabilityState
    {
        Available = 0,
        Blocked = 1,
    }

    internal enum RoutePlannerAvailabilityFilterMode
    {
        All = 0,
        Available = 1,
        Blocked = 2,
        ActiveNpc = 3,
        Underperforming = 4,
    }

    internal enum RoutePlannerSortMode
    {
        Optimizer = 0,
        ProjectedPayout = 1,
        ProjectedValue = 2,
        RealizedNetProfit = 3,
        UnitPrice = 4,
        Commodity = 5,
        District = 6,
        Availability = 7,
    }

    public enum RoutePlannerOverlayLaneKind
    {
        ActiveNpc = 0,
        Recommended = 1,
        Blocked = 2,
        Underperforming = 3,
        Selected = 4,
    }

    internal sealed class RoutePlannerScoreBreakdown
    {
        public float EligibilityScore { get; set; }

        public float ProjectedPayoutScore { get; set; }

        public float MarketScore { get; set; }

        public float ActualPerformanceScore { get; set; }

        public float BlockerPenalty { get; set; }

        public float TotalScore { get; set; }
    }

    internal sealed class TabletRoutePlannerCandidate
    {
        public TabletRoutePlannerCandidate()
        {
            CandidateId = string.Empty;
            Commodity = string.Empty;
            DistrictPairLabel = string.Empty;
            CorridorId = string.Empty;
            AvailabilityLabel = string.Empty;
            BlockerSummary = string.Empty;
            MatchingContractLabel = string.Empty;
            RouteFamily = new TabletNpcRouteFamilySummary();
            ScoreBreakdown = new RoutePlannerScoreBreakdown();
        }

        public string CandidateId { get; set; }

        public Industry OriginIndustry { get; set; }

        public Industry DestinationIndustry { get; set; }

        public string Commodity { get; set; }

        public string DistrictPairLabel { get; set; }

        public string CorridorId { get; set; }

        public RoutePlannerAvailabilityState AvailabilityState { get; set; }

        public string AvailabilityLabel { get; set; }

        public string BlockerSummary { get; set; }

        public float CurrentUnitPrice { get; set; }

        public float SuggestedShipmentTons { get; set; }

        public float ProjectedValue { get; set; }

        public float ProjectedPayout { get; set; }

        public float RealizedRevenue { get; set; }

        public float RealizedOperatingCost { get; set; }

        public float RealizedNetProfit { get; set; }

        public float RealizedAveragePayout { get; set; }

        public float RealizedLossRatioPercent { get; set; }

        public int MatchingContractId { get; set; }

        public string MatchingContractLabel { get; set; }

        public TabletNpcRouteFamilySummary RouteFamily { get; set; }

        public bool HasActiveNpcRoute { get; set; }

        public bool HasRouteFamilyHistory { get; set; }

        public bool IsUnderperformingActiveLane { get; set; }

        public bool CanDraftNpcRoute { get; set; }

        public float OptimizerScore { get; set; }

        public RoutePlannerScoreBreakdown ScoreBreakdown { get; set; }

        public float ProjectedActualPayoutDelta
        {
            get { return RealizedAveragePayout - ProjectedPayout; }
        }

        public bool InvolvesDistrict(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return true;
            }

            return string.Equals(OriginIndustry != null ? OriginIndustry.DistrictName : string.Empty, districtName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(DestinationIndustry != null ? DestinationIndustry.DistrictName : string.Empty, districtName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class RoutePlannerOverlayLane
    {
        public RoutePlannerOverlayLane()
        {
            CandidateId = string.Empty;
            DistrictA = string.Empty;
            DistrictB = string.Empty;
            Label = string.Empty;
        }

        public string CandidateId { get; set; }

        public string DistrictA { get; set; }

        public string DistrictB { get; set; }

        public string Label { get; set; }

        public RoutePlannerOverlayLaneKind Kind { get; set; }

        public bool IsSelected { get; set; }
    }

    public sealed class RoutePlannerOverlaySnapshot
    {
        public RoutePlannerOverlaySnapshot()
        {
            SelectedCandidateId = string.Empty;
            SelectedDistrictA = string.Empty;
            SelectedDistrictB = string.Empty;
            Lanes = Array.Empty<RoutePlannerOverlayLane>();
        }

        public string SelectedCandidateId { get; set; }

        public string SelectedDistrictA { get; set; }

        public string SelectedDistrictB { get; set; }

        public IReadOnlyList<RoutePlannerOverlayLane> Lanes { get; set; }
    }
}
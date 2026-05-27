using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class TabletMarketHighlight
    {
        public string Commodity { get; set; }

        public float UnitPrice { get; set; }

        public string Reason { get; set; }
    }

    internal sealed class TabletMarketResourcePrice
    {
        public string Commodity { get; set; }

        public float UnitPrice { get; set; }

        public VehicleCargoType CargoType { get; set; }
    }

    internal sealed class TabletDistrictComparison
    {
        public string DistrictName { get; set; }

        public float InfluencePercent { get; set; }

        public float ReputationScore { get; set; }

        public string ReputationLabel { get; set; }

        public int ControlledSites { get; set; }

        public int ControlledDepots { get; set; }

        public float WeeklyOperationsCost { get; set; }

        public DistrictLicenseStatus LicenseStatus { get; set; }

        public float LicenseActivityTons { get; set; }

        public float LicenseTargetTons { get; set; }

        public int CorridorRiskCount { get; set; }

        public int ServiceRiskCount { get; set; }

        public float CompetitivePressurePercent { get; set; }

        public float CompetitiveOpportunityPercent { get; set; }

        public int ActiveCompetitionJobs { get; set; }

        public int ActiveCarrierCount { get; set; }

        public string DominantCarrierName { get; set; }

        public int VisibleCompetitionCount { get; set; }

        public int CompetitiveWinCount { get; set; }

        public int ContestedCorridorCount { get; set; }

        public int CorridorHoldCount { get; set; }

        public string HottestCorridorName { get; set; }

        public float HottestCorridorPressurePercent { get; set; }

        public string CompetitionStatus { get; set; }

        public string DistrictEventHeadline { get; set; }

        public string DistrictEventCommodity { get; set; }

        public string DistrictEventStatus { get; set; }

        public string DistrictEventImpact { get; set; }

        public float DistrictEventSeverityPercent { get; set; }

        public string DistrictEventSeverityLabel { get; set; }
    }

    internal sealed class TabletNpcRoutePerformance
    {
        public NpcLogisticsContract Contract { get; set; }

        public int ContractId { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public string FamilyLabel { get; set; }

        public int FamilyContractCount { get; set; }

        public int RouteCount { get; set; }

        public float DeliveredTons { get; set; }

        public float LossRatioPercent { get; set; }

        public float AveragePayout { get; set; }

        public int CompletedDeliveries { get; set; }
    }

    internal sealed class TabletHistoryBuffer
    {
        private readonly int _capacity;
        private readonly List<float> _values;

        public TabletHistoryBuffer(int capacity)
        {
            _capacity = Math.Max(2, capacity);
            _values = new List<float>(_capacity);
        }

        public void Add(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
            }

            if (_values.Count >= _capacity)
            {
                _values.RemoveAt(0);
            }

            _values.Add(value);
        }

        public IReadOnlyList<float> Snapshot()
        {
            return _values.Count == 0 ? Array.Empty<float>() : _values.ToArray();
        }
    }

    internal sealed class TabletLocationSummary
    {
        public Industry Industry { get; set; }

        public ExternalLocationKind LocationKind { get; set; }

        public string Name { get; set; }

        public string OwnershipTag { get; set; }

        public string PermitTag { get; set; }

        public bool IsOwnedByPlayer { get; set; }

        public bool RequiresIndustryPurchase { get; set; }

        public bool HasContractorPermitForGameplay { get; set; }

        public bool RequiresContractorPermit { get; set; }

        public float StorageTons { get; set; }

        public float TotalCapacityTons { get; set; }

        public float FillRatio { get; set; }

        public float OmegaStorageTons { get; set; }

        public float OmegaCapacityTons { get; set; }

        public float OutputPerHourTons { get; set; }

        public float UtilizationPercent { get; set; }

        public WarehouseStorageRiskSnapshot WarehouseRisk { get; set; }

        public string OverviewDetail { get; set; }

        public string ProductionWarning { get; set; }

        public string PrimaryConversion { get; set; }

        public string ModuleSummary { get; set; }

        public bool HasServiceBusinessInfo { get; set; }

        public bool HasServiceContractInfo { get; set; }

        public bool ServiceStaffAssigned { get; set; }

        public bool ServiceStockReady { get; set; }

        public bool ServiceOperational { get; set; }

        public float ServiceWeeklyIncome { get; set; }

        public float ServiceWeeklyStaffingCost { get; set; }

        public float ServiceLastPassiveIncome { get; set; }

        public string ServiceStaffStatus { get; set; }

        public string ServiceStockStatus { get; set; }

        public string ServiceOperationsStatus { get; set; }

        public string ServicePassiveIncomeStatus { get; set; }

        public string ServiceRecentPayoutStatus { get; set; }

        public string ServiceContractStatus { get; set; }

        public int ServiceCurrentWeekDeliveries { get; set; }

        public float ServiceCurrentWeekTons { get; set; }

        public float ServiceRequiredWeeklyTons { get; set; }

        public int ServicePenaltySteps { get; set; }

        public int ServiceSuccessStreak { get; set; }

        public bool ServiceTargetMetLastWeek { get; set; }
    }

    internal sealed class IndustryLoadOptionsSnapshot
    {
        public IndustryLoadOptionsSnapshot()
        {
            LoadOptions = Array.Empty<string>();
            LoadOptionSubtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CargoType = VehicleCargoType.Unknown;
            StatusText = string.Empty;
        }

        public bool HasCargoVehicle { get; set; }

        public string CargoVehicleName { get; set; }

        public VehicleCargoType CargoType { get; set; }

        public float FreeCapacityTons { get; set; }

        public IReadOnlyList<string> LoadOptions { get; set; }

        public IReadOnlyDictionary<string, string> LoadOptionSubtitles { get; set; }

        public string StatusText { get; set; }
    }

    internal sealed class TabletStateSnapshot
    {
        public TabletStateSnapshot()
        {
            IndustrySummaries = Array.Empty<TabletLocationSummary>();
            ConstructionSiteSummaries = Array.Empty<TabletLocationSummary>();
            WarehouseSummaries = Array.Empty<TabletLocationSummary>();
            StoreSummaries = Array.Empty<TabletLocationSummary>();
            GasStationSummaries = Array.Empty<TabletLocationSummary>();
            MarketHighlights = Array.Empty<TabletMarketHighlight>();
            MarketPrices = Array.Empty<TabletMarketResourcePrice>();
            NearestIndustryInputs = Array.Empty<string>();
            NearestIndustryOutputs = Array.Empty<string>();
            StatusBanner = string.Empty;
            CargoVehicleName = string.Empty;
            CargoCommodity = string.Empty;
            PoweredVehicleName = string.Empty;
            NearestIndustryName = string.Empty;
            NearestIndustryProductionWarning = string.Empty;
        }

        public float Balance { get; set; }

        public float MarketMultiplier { get; set; }

        public int ActiveNpcRouteCount { get; set; }

        public bool TransferInProgress { get; set; }

        public string StatusBanner { get; set; }

        public bool HasCargoVehicle { get; set; }

        public bool CargoIsEmpty { get; set; }

        public string CargoVehicleName { get; set; }

        public VehicleCargoType CargoType { get; set; }

        public string CargoCommodity { get; set; }

        public float CargoWeightTons { get; set; }

        public float CargoCapacityTons { get; set; }

        public float CargoCapacityRatio { get; set; }

        public bool HasPoweredVehicle { get; set; }

        public string PoweredVehicleName { get; set; }

        public bool FuelVehicleMatchesCargoVehicle { get; set; }

        public bool FuelIsEmpty { get; set; }

        public float FuelCurrentLiters { get; set; }

        public float FuelCapacityLiters { get; set; }

        public float FuelRatio { get; set; }

        public Industry NearestIndustry { get; set; }

        public bool HasNearestIndustry { get; set; }

        public bool CanInteractWithNearestIndustry { get; set; }

        public string NearestIndustryName { get; set; }

        public float NearestIndustryDistance { get; set; }

        public IReadOnlyList<string> NearestIndustryInputs { get; set; }

        public IReadOnlyList<string> NearestIndustryOutputs { get; set; }

        public float NearestIndustryProductionRateTonsPerHour { get; set; }

        public float NearestIndustryUtilizationPercent { get; set; }

        public float NearestIndustryOmegaStorageTons { get; set; }

        public float NearestIndustryOmegaCapacityTons { get; set; }

        public bool NearestIndustryOwnedForGameplay { get; set; }

        public bool NearestIndustryRequiresPurchase { get; set; }

        public bool NearestIndustryHasPermitForGameplay { get; set; }

        public bool NearestIndustryRequiresPermit { get; set; }

        public string NearestIndustryProductionWarning { get; set; }

        public IReadOnlyList<TabletLocationSummary> IndustrySummaries { get; set; }

        public IReadOnlyList<TabletLocationSummary> ConstructionSiteSummaries { get; set; }

        public IReadOnlyList<TabletLocationSummary> WarehouseSummaries { get; set; }

        public IReadOnlyList<TabletLocationSummary> StoreSummaries { get; set; }

        public IReadOnlyList<TabletLocationSummary> GasStationSummaries { get; set; }

        public IReadOnlyList<TabletMarketHighlight> MarketHighlights { get; set; }

        public IReadOnlyList<TabletMarketResourcePrice> MarketPrices { get; set; }

        public int ControlledDistrictCount { get; set; }

        public int ActiveCorridorCount { get; set; }

        public int SecuredSupportSiteCount { get; set; }
    }

    internal sealed partial class TabletStateStore
    {
        private const int SnapshotRefreshIntervalMs = 250;
        private const int LoadOptionsRefreshIntervalMs = 250;
        private const int HistorySampleIntervalMs = 15000;
        private const int InGameMinutesPerDay = 24 * 60;
        private const int InGameMinutesPerWeek = 7 * InGameMinutesPerDay;

        private static readonly CompanyFinanceCategory[] IncomeBudgetCategories =
        {
            CompanyFinanceCategory.PlayerDelivery,
            CompanyFinanceCategory.PlayerContract,
            CompanyFinanceCategory.NpcDelivery,
            CompanyFinanceCategory.IndustryIncome,
            CompanyFinanceCategory.MissionReward,
            CompanyFinanceCategory.LoanDisbursement,
            CompanyFinanceCategory.OtherIncome,
        };

        private static readonly CompanyFinanceCategory[] ExpenseBudgetCategories =
        {
            CompanyFinanceCategory.OfficeRent,
            CompanyFinanceCategory.ApartmentRent,
            CompanyFinanceCategory.VehicleRent,
            CompanyFinanceCategory.NpcWages,
            CompanyFinanceCategory.ServiceSiteStaffing,
            CompanyFinanceCategory.TerritoryOperations,
            CompanyFinanceCategory.CorporateOverhead,
            CompanyFinanceCategory.FleetMaintenance,
            CompanyFinanceCategory.WarehouseSpoilage,
            CompanyFinanceCategory.WarehouseShrinkage,
            CompanyFinanceCategory.InventoryLoss,
            CompanyFinanceCategory.FuelPurchase,
            CompanyFinanceCategory.RepairCost,
            CompanyFinanceCategory.ServiceCall,
            CompanyFinanceCategory.PermitOrLicence,
            CompanyFinanceCategory.LoanRepayment,
            CompanyFinanceCategory.OtherExpense,
        };

        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly VehicleFuelSystem _vehicleFuelSystem;
        private readonly GlobalMarketManager _globalMarket;
        private readonly NpcLogisticsManager _npcLogisticsManager;
        private readonly PlayerContractsManager _playerContractsManager;
        private readonly PropertyManager _propertyManager;
        private readonly BankLoanManager _bankLoanManager;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly TerritoryManager _territoryManager;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Func<Ped> _getPlayer;
        private readonly Func<Industry> _getNearestIndustry;
        private readonly Func<float> _getProfit;
        private readonly Func<VehicleCargoType> _getFallbackCargoType;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<bool> _hasPendingTransfer;
        private readonly Func<string> _getStatusBanner;
        private readonly Func<int> _getControlledDistrictCount;
        private readonly Func<int> _getActiveCorridorCount;
        private readonly Func<int> _getSecuredSupportSiteCount;
        private readonly Func<IEnumerable<TerritoryDistrictState>> _getDistrictStates;
        private readonly Func<TerritoryOperationsSummary> _getTerritoryOperationsSummary;
        private readonly Func<int, int> _getRemainingTerritoryOperationsChargeMinutes;
        private readonly IndustryStatisticsSnapshotCache _statisticsSnapshotCache;
        private readonly List<string> _cachedLoadOptions;
        private readonly Dictionary<string, string> _cachedLoadOptionSubtitles;
        private readonly TabletPersistentAnalyticsSeries _profitHistory;
        private readonly Dictionary<string, TabletPersistentAnalyticsSeries> _commodityPriceHistoryByCommodity;
        private readonly Dictionary<string, TabletPersistentAnalyticsSeries> _siteUtilizationHistoryByIndustryId;
        private readonly Dictionary<string, TabletPersistentAnalyticsSeries> _siteStorageHistoryByIndustryId;

        private Industry _cachedLoadIndustry;
        private int _cachedLoadVehicleHandle;
        private int _lastLoadOptionsRefreshMs;
        private float _cachedLoadFreeCapacityTons;
        private VehicleCargoType _cachedLoadCargoType;
        private bool _hasCachedLoadOptions;
        private int _lastHistorySampleMs;
        private bool _hasHistorySamples;
        private TabletGraphTimeframe _selectedGraphTimeframe;
        private string _selectedTrendCommodity;
        private string _selectedUtilizationIndustryId;
        private string _selectedStorageIndustryId;
        private RoutePlannerSortMode _routePlannerSortMode;
        private RoutePlannerAvailabilityFilterMode _routePlannerAvailabilityFilterMode;
        private string _routePlannerCommodityFilter;
        private string _routePlannerDistrictFilter;
        private string _selectedRoutePlannerCandidateId;

        private int _lastRefreshMs;
        private bool _hasSnapshot;
        private bool _balanceDirty;
        private bool _cargoDirty;
        private bool _nearestIndustryDirty;
        private bool _marketDirty;
        private bool _networkDirty;
        private bool _statusDirty;
        private bool _viewDirty;

        public TabletStateStore(
            IndustryManager industryManager,
            FleetManager fleetManager,
            VehicleFuelSystem vehicleFuelSystem,
            GlobalMarketManager globalMarket,
            NpcLogisticsManager npcLogisticsManager,
            PlayerContractsManager playerContractsManager,
            PropertyManager propertyManager,
            BankLoanManager bankLoanManager,
            CompanyFinanceTracker financeTracker,
            TerritoryManager territoryManager,
            Func<int> getCurrentInGameMinute,
            Func<Ped> getPlayer,
            Func<Industry> getNearestIndustry,
            Func<float> getProfit,
            Func<VehicleCargoType> getFallbackCargoType,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<bool> hasPendingTransfer,
            Func<string> getStatusBanner,
            Func<int> getControlledDistrictCount,
            Func<int> getActiveCorridorCount,
            Func<int> getSecuredSupportSiteCount,
            Func<IEnumerable<TerritoryDistrictState>> getDistrictStates,
            Func<TerritoryOperationsSummary> getTerritoryOperationsSummary,
            Func<int, int> getRemainingTerritoryOperationsChargeMinutes)
        {
            _industryManager = industryManager ?? throw new ArgumentNullException(nameof(industryManager));
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _vehicleFuelSystem = vehicleFuelSystem ?? throw new ArgumentNullException(nameof(vehicleFuelSystem));
            _globalMarket = globalMarket ?? throw new ArgumentNullException(nameof(globalMarket));
            _npcLogisticsManager = npcLogisticsManager;
            _playerContractsManager = playerContractsManager;
            _propertyManager = propertyManager;
            _bankLoanManager = bankLoanManager;
            _financeTracker = financeTracker;
            _territoryManager = territoryManager;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _getPlayer = getPlayer;
            _getNearestIndustry = getNearestIndustry;
            _getProfit = getProfit;
            _getFallbackCargoType = getFallbackCargoType;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _hasPendingTransfer = hasPendingTransfer;
            _getStatusBanner = getStatusBanner;
            _getControlledDistrictCount = getControlledDistrictCount;
            _getActiveCorridorCount = getActiveCorridorCount;
            _getSecuredSupportSiteCount = getSecuredSupportSiteCount;
            _getDistrictStates = getDistrictStates;
            _getTerritoryOperationsSummary = getTerritoryOperationsSummary;
            _getRemainingTerritoryOperationsChargeMinutes = getRemainingTerritoryOperationsChargeMinutes;
            _statisticsSnapshotCache = new IndustryStatisticsSnapshotCache();
            _cachedLoadOptions = new List<string>();
            _cachedLoadOptionSubtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profitHistory = new TabletPersistentAnalyticsSeries();
            _commodityPriceHistoryByCommodity = new Dictionary<string, TabletPersistentAnalyticsSeries>(StringComparer.OrdinalIgnoreCase);
            _siteUtilizationHistoryByIndustryId = new Dictionary<string, TabletPersistentAnalyticsSeries>(StringComparer.OrdinalIgnoreCase);
            _siteStorageHistoryByIndustryId = new Dictionary<string, TabletPersistentAnalyticsSeries>(StringComparer.OrdinalIgnoreCase);
            _cachedLoadCargoType = VehicleCargoType.Unknown;
            _cachedLoadFreeCapacityTons = -1f;
            _lastLoadOptionsRefreshMs = int.MinValue;
            _lastHistorySampleMs = int.MinValue;
            _selectedGraphTimeframe = TabletGraphTimeframeCatalog.GetDefault();
            _selectedTrendCommodity = string.Empty;
            _selectedUtilizationIndustryId = string.Empty;
            _selectedStorageIndustryId = string.Empty;
            _routePlannerSortMode = RoutePlannerSortMode.Optimizer;
            _routePlannerAvailabilityFilterMode = RoutePlannerAvailabilityFilterMode.All;
            _routePlannerCommodityFilter = string.Empty;
            _routePlannerDistrictFilter = string.Empty;
            _selectedRoutePlannerCandidateId = string.Empty;
            Snapshot = new TabletStateSnapshot();
            MarkAllDirty();
        }

        public int Version { get; private set; }

        public TabletStateSnapshot Snapshot { get; private set; }

        public TabletStateSnapshot GetSnapshot()
        {
            Update();
            return Snapshot;
        }

        public void Update()
        {
            var now = Game.GameTime;
            CaptureHistory(now);

            if (!_hasSnapshot || now - _lastRefreshMs >= SnapshotRefreshIntervalMs)
            {
                MarkVolatileSlicesDirty();
            }

            var hasSnapshotDirtyState = HasSnapshotDirtyState();
            if (_hasSnapshot && !hasSnapshotDirtyState && !_viewDirty)
            {
                return;
            }

            if (!_hasSnapshot || Snapshot == null)
            {
                Snapshot = BuildSnapshot();
                _lastRefreshMs = now;
                _hasSnapshot = true;
                ClearSnapshotDirtyState();
            }
            else if (hasSnapshotDirtyState)
            {
                RefreshDirtySnapshotSlices(Snapshot);
                _lastRefreshMs = now;
                ClearSnapshotDirtyState();
            }

            _viewDirty = false;
            Version += 1;
        }

        public void CaptureHistory(int gameTimeMs)
        {
            if (gameTimeMs < 0)
            {
                return;
            }

            if (_hasHistorySamples && gameTimeMs - _lastHistorySampleMs < HistorySampleIntervalMs)
            {
                return;
            }

            _lastHistorySampleMs = gameTimeMs;
            _hasHistorySamples = true;

            _profitHistory.AddSample(_getProfit != null ? _getProfit() : 0f);

            var commodities = CommodityCatalog.GetKnownCommodities();
            for (int i = 0; i < commodities.Count; i++)
            {
                var commodity = commodities[i];
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                GetOrCreateHistoryBuffer(_commodityPriceHistoryByCommodity, commodity)
                    .AddSample(Math.Max(0f, _globalMarket.GetUnitPrice(commodity)));
            }

            if (_industryManager.Industries == null)
            {
                return;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var industry = _industryManager.Industries[i];
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                GetOrCreateHistoryBuffer(_siteStorageHistoryByIndustryId, industry.Id)
                    .AddSample(GetStorageFillPercent(industry));

                if (industry.SiteRole != SiteRole.Warehouse)
                {
                    GetOrCreateHistoryBuffer(_siteUtilizationHistoryByIndustryId, industry.Id)
                        .AddSample(Math.Max(0f, industry.LastUtilizationPercent));
                }
            }
        }

        public void MarkAllDirty()
        {
            _balanceDirty = true;
            _cargoDirty = true;
            _nearestIndustryDirty = true;
            _marketDirty = true;
            _networkDirty = true;
            _statusDirty = true;
            _statisticsSnapshotCache.Invalidate();
            ClearLoadOptionsCache();
        }

        public void MarkStatusDirty()
        {
            _statusDirty = true;
        }

        public void MarkBalanceDirty()
        {
            _balanceDirty = true;
        }

        public void MarkCargoDirty()
        {
            _cargoDirty = true;
            _marketDirty = true;
        }

        public void MarkNearestIndustryDirty()
        {
            _nearestIndustryDirty = true;
            _marketDirty = true;
        }

        public void MarkMarketDirty()
        {
            _marketDirty = true;
        }

        public void MarkNetworkDirty()
        {
            _networkDirty = true;
            _statisticsSnapshotCache.Invalidate();
        }

        public IndustryStatisticsSnapshot GetIndustryStatistics(Industry industry)
        {
            return _statisticsSnapshotCache.GetSnapshot(industry);
        }

        public TabletGraphTimeframe SelectedGraphTimeframe
        {
            get { return _selectedGraphTimeframe; }
        }

        public string SelectedTrendCommodity
        {
            get { return EnsureSelectedTrendCommodity(); }
        }

        public string SelectedUtilizationIndustryId
        {
            get { return EnsureSelectedUtilizationIndustryId(); }
        }

        public string SelectedStorageIndustryId
        {
            get { return EnsureSelectedStorageIndustryId(); }
        }

        public RoutePlannerSortMode SelectedRoutePlannerSortMode
        {
            get { return _routePlannerSortMode; }
        }

        public RoutePlannerAvailabilityFilterMode SelectedRoutePlannerAvailabilityFilterMode
        {
            get { return _routePlannerAvailabilityFilterMode; }
        }

        public string SelectedRoutePlannerCommodityFilter
        {
            get { return EnsureRoutePlannerCommodityFilter(); }
        }

        public string SelectedRoutePlannerDistrictFilter
        {
            get { return EnsureRoutePlannerDistrictFilter(); }
        }

        public string SelectedRoutePlannerCandidateId
        {
            get { return EnsureSelectedRoutePlannerCandidateId(); }
        }

        public void CycleGraphTimeframe(int delta)
        {
            _selectedGraphTimeframe = TabletGraphTimeframeCatalog.Cycle(_selectedGraphTimeframe, delta == 0 ? 1 : delta);
            MarkViewDirty();
        }

        public void CycleSelectedTrendCommodity(int delta)
        {
            var commodities = GetOrderedCommodityOptions();
            if (commodities.Count == 0)
            {
                _selectedTrendCommodity = string.Empty;
                MarkViewDirty();
                return;
            }

            var current = EnsureSelectedTrendCommodity();
            var currentIndex = commodities.FindIndex(commodity => string.Equals(commodity, current, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = currentIndex + direction;
            while (nextIndex < 0)
            {
                nextIndex += commodities.Count;
            }

            while (nextIndex >= commodities.Count)
            {
                nextIndex -= commodities.Count;
            }

            _selectedTrendCommodity = commodities[nextIndex];
            MarkViewDirty();
        }

        public void SetSelectedTrendCommodity(string commodity)
        {
            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            _selectedTrendCommodity = string.IsNullOrWhiteSpace(normalizedCommodity)
                ? string.Empty
                : normalizedCommodity;
            EnsureSelectedTrendCommodity();
            MarkViewDirty();
        }

        public void CycleSelectedUtilizationIndustry(int delta)
        {
            var industries = GetOrderedUtilizationIndustries();
            if (industries.Count == 0)
            {
                _selectedUtilizationIndustryId = string.Empty;
                MarkViewDirty();
                return;
            }

            var currentId = EnsureSelectedUtilizationIndustryId();
            var currentIndex = industries.FindIndex(industry => string.Equals(industry.Id, currentId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = currentIndex + direction;
            while (nextIndex < 0)
            {
                nextIndex += industries.Count;
            }

            while (nextIndex >= industries.Count)
            {
                nextIndex -= industries.Count;
            }

            _selectedUtilizationIndustryId = industries[nextIndex].Id;
            MarkViewDirty();
        }

        public void CycleSelectedStorageIndustry(int delta)
        {
            var industries = GetOrderedStorageIndustries();
            if (industries.Count == 0)
            {
                _selectedStorageIndustryId = string.Empty;
                MarkViewDirty();
                return;
            }

            var currentId = EnsureSelectedStorageIndustryId();
            var currentIndex = industries.FindIndex(industry => string.Equals(industry.Id, currentId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = currentIndex + direction;
            while (nextIndex < 0)
            {
                nextIndex += industries.Count;
            }

            while (nextIndex >= industries.Count)
            {
                nextIndex -= industries.Count;
            }

            _selectedStorageIndustryId = industries[nextIndex].Id;
            MarkViewDirty();
        }

        public void CycleRoutePlannerSortMode(int delta)
        {
            var values = Enum.GetValues(typeof(RoutePlannerSortMode)).Cast<RoutePlannerSortMode>().ToArray();
            var currentIndex = Array.IndexOf(values, _routePlannerSortMode);
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

            _routePlannerSortMode = values[nextIndex];
            MarkViewDirty();
        }

        public void CycleRoutePlannerAvailabilityFilter(int delta)
        {
            var values = Enum.GetValues(typeof(RoutePlannerAvailabilityFilterMode)).Cast<RoutePlannerAvailabilityFilterMode>().ToArray();
            var currentIndex = Array.IndexOf(values, _routePlannerAvailabilityFilterMode);
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

            _routePlannerAvailabilityFilterMode = values[nextIndex];
            MarkViewDirty();
        }

        public void CycleRoutePlannerCommodityFilter(int delta)
        {
            var options = GetRoutePlannerCommodityOptions();
            if (options.Count == 0)
            {
                _routePlannerCommodityFilter = string.Empty;
                MarkViewDirty();
                return;
            }

            _routePlannerCommodityFilter = CycleStringSelection(options, EnsureRoutePlannerCommodityFilter(), delta, CommodityCatalog.Normalize);
            MarkViewDirty();
        }

        public void CycleRoutePlannerDistrictFilter(int delta)
        {
            var options = GetRoutePlannerDistrictOptions();
            if (options.Count == 0)
            {
                _routePlannerDistrictFilter = string.Empty;
                MarkViewDirty();
                return;
            }

            _routePlannerDistrictFilter = CycleStringSelection(options, EnsureRoutePlannerDistrictFilter(), delta, value => (value ?? string.Empty).Trim());
            MarkViewDirty();
        }

        public void SetSelectedRoutePlannerCandidate(string candidateId)
        {
            _selectedRoutePlannerCandidateId = string.IsNullOrWhiteSpace(candidateId) ? string.Empty : candidateId.Trim();
            MarkViewDirty();
        }

        public TabletAnalyticsPersistenceSnapshot CreatePersistenceSnapshot()
        {
            EnsureSelectedTrendCommodity();
            var snapshot = new TabletAnalyticsPersistenceSnapshot
            {
                SelectedGraphTimeframe = _selectedGraphTimeframe,
                SelectedTrendCommodity = _selectedTrendCommodity,
                ProfitHistory = _profitHistory.CreatePersistence(),
            };

            AppendNamedSeriesPersistence(snapshot.CommodityPriceHistories, _commodityPriceHistoryByCommodity);
            AppendNamedSeriesPersistence(snapshot.SiteUtilizationHistories, _siteUtilizationHistoryByIndustryId);
            AppendNamedSeriesPersistence(snapshot.SiteStorageHistories, _siteStorageHistoryByIndustryId);
            return snapshot;
        }

        public void ApplyPersistenceSnapshot(TabletAnalyticsPersistenceSnapshot snapshot)
        {
            ClearAnalyticsHistories();

            if (snapshot == null)
            {
                _selectedGraphTimeframe = TabletGraphTimeframeCatalog.GetDefault();
                _selectedTrendCommodity = string.Empty;
                _selectedUtilizationIndustryId = string.Empty;
                _selectedStorageIndustryId = string.Empty;
                _lastHistorySampleMs = int.MinValue;
                _hasHistorySamples = false;
                MarkViewDirty();
                return;
            }

            _selectedGraphTimeframe = snapshot.SelectedGraphTimeframe;
            _selectedTrendCommodity = CommodityCatalog.Normalize(snapshot.SelectedTrendCommodity);
            _selectedUtilizationIndustryId = EnsureSelectedUtilizationIndustryId();
            _selectedStorageIndustryId = EnsureSelectedStorageIndustryId();
            _profitHistory.Restore(snapshot.ProfitHistory);
            RestoreNamedSeries(_commodityPriceHistoryByCommodity, snapshot.CommodityPriceHistories);
            RestoreNamedSeries(_siteUtilizationHistoryByIndustryId, snapshot.SiteUtilizationHistories);
            RestoreNamedSeries(_siteStorageHistoryByIndustryId, snapshot.SiteStorageHistories);
            EnsureSelectedTrendCommodity();
            EnsureSelectedUtilizationIndustryId();
            EnsureSelectedStorageIndustryId();
            _lastHistorySampleMs = Game.GameTime;
            _hasHistorySamples = snapshot.HasData;
            MarkViewDirty();
        }

        public void ResetAnalyticsState()
        {
            ApplyPersistenceSnapshot(null);
        }

        public IReadOnlyList<float> GetProfitHistory()
        {
            return GetProfitHistory(_selectedGraphTimeframe);
        }

        public IReadOnlyList<float> GetProfitHistory(TabletGraphTimeframe timeframe)
        {
            return _profitHistory.GetValues(timeframe);
        }

        public IReadOnlyList<float> GetCommodityPriceHistory(string commodity)
        {
            return GetCommodityPriceHistory(commodity, _selectedGraphTimeframe);
        }

        public IReadOnlyList<float> GetCommodityPriceHistory(string commodity, TabletGraphTimeframe timeframe)
        {
            return GetHistorySnapshot(_commodityPriceHistoryByCommodity, CommodityCatalog.Normalize(commodity), timeframe);
        }

        public string GetCommodityShockSummary(string commodity)
        {
            return _globalMarket != null
                ? _globalMarket.GetCommodityShockSummary(commodity)
                : string.Empty;
        }

        public IReadOnlyList<float> GetSiteUtilizationHistory(Industry industry)
        {
            return GetSiteUtilizationHistory(industry, _selectedGraphTimeframe);
        }

        public IReadOnlyList<float> GetSiteUtilizationHistory(Industry industry, TabletGraphTimeframe timeframe)
        {
            return GetHistorySnapshot(_siteUtilizationHistoryByIndustryId, GetIndustryHistoryKey(industry), timeframe);
        }

        public IReadOnlyList<float> GetSiteStorageHistory(Industry industry)
        {
            return GetSiteStorageHistory(industry, _selectedGraphTimeframe);
        }

        public IReadOnlyList<float> GetSiteStorageHistory(Industry industry, TabletGraphTimeframe timeframe)
        {
            return GetHistorySnapshot(_siteStorageHistoryByIndustryId, GetIndustryHistoryKey(industry), timeframe);
        }

        public IReadOnlyList<TabletDistrictComparison> GetDistrictComparisons()
        {
            var districts = _getDistrictStates != null
                ? _getDistrictStates()
                : null;
            if (districts == null)
            {
                return Array.Empty<TabletDistrictComparison>();
            }

            var operationsByDistrict = _getTerritoryOperationsSummary != null
                ? (_getTerritoryOperationsSummary() ?? new TerritoryOperationsSummary()).Districts
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DistrictName))
                    .ToDictionary(entry => entry.DistrictName, entry => entry, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, TerritoryDistrictOperationsEntry>(StringComparer.OrdinalIgnoreCase);

            return districts
                .Where(district => district != null && !string.IsNullOrWhiteSpace(district.DistrictName))
                .OrderByDescending(district => district.InfluenceRatio)
                .ThenByDescending(district => district.ReputationScore)
                .Select(district => new TabletDistrictComparison
                {
                    WeeklyOperationsCost = operationsByDistrict.ContainsKey(district.DistrictName)
                        ? Math.Max(0f, operationsByDistrict[district.DistrictName].TotalWeeklyCost)
                        : 0f,
                    LicenseStatus = district.LicenseStatus,
                    LicenseActivityTons = operationsByDistrict.ContainsKey(district.DistrictName)
                        ? Math.Max(0f, operationsByDistrict[district.DistrictName].LicenseActivityTons)
                        : Math.Max(0f, district.CurrentWeekActivityTons),
                    LicenseTargetTons = operationsByDistrict.ContainsKey(district.DistrictName)
                        ? Math.Max(0f, operationsByDistrict[district.DistrictName].LicenseTargetTons)
                        : Math.Max(0f, district.RequiredWeeklyActivityTons),
                    CorridorRiskCount = operationsByDistrict.ContainsKey(district.DistrictName)
                        ? Math.Max(0, operationsByDistrict[district.DistrictName].CorridorRiskCount)
                        : 0,
                    ServiceRiskCount = operationsByDistrict.ContainsKey(district.DistrictName)
                        ? Math.Max(0, operationsByDistrict[district.DistrictName].ServiceRiskCount)
                        : 0,
                    DistrictName = district.DistrictName,
                    InfluencePercent = Math.Max(0f, district.InfluenceRatio * 100f),
                    ReputationScore = Math.Max(0f, district.ReputationScore),
                    ReputationLabel = district.ReputationLabel ?? string.Empty,
                    ControlledSites = district.ControlledSites,
                    ControlledDepots = district.ControlledDepots,
                    CompetitivePressurePercent = Math.Max(0f, Math.Min(100f, district.CompetitivePressure * 100f)),
                    CompetitiveOpportunityPercent = Math.Max(0f, Math.Min(100f, district.CompetitiveOpportunity * 100f)),
                    ActiveCompetitionJobs = Math.Max(0, district.ActiveCompetitionJobs),
                    ActiveCarrierCount = Math.Max(0, district.ActiveCarrierCount),
                    DominantCarrierName = district.DominantCarrierName ?? string.Empty,
                    VisibleCompetitionCount = Math.Max(0, district.VisibleCompetitionCount),
                    CompetitiveWinCount = Math.Max(0, district.CompetitiveWinCount),
                    ContestedCorridorCount = Math.Max(0, district.ContestedCorridorCount),
                    CorridorHoldCount = Math.Max(0, district.CorridorHoldCount),
                    HottestCorridorName = district.HottestCorridorName ?? string.Empty,
                    HottestCorridorPressurePercent = Math.Max(0f, Math.Min(100f, district.HottestCorridorPressure * 100f)),
                    CompetitionStatus = district.CompetitionStatus ?? string.Empty,
                    DistrictEventHeadline = district.ActiveEvent != null ? district.ActiveEvent.Headline ?? string.Empty : string.Empty,
                    DistrictEventCommodity = district.ActiveEvent != null ? district.ActiveEvent.PreferredCommodity ?? string.Empty : string.Empty,
                    DistrictEventStatus = district.ActiveEvent != null ? district.ActiveEvent.StatusText ?? string.Empty : string.Empty,
                    DistrictEventImpact = district.ActiveEvent != null ? district.ActiveEvent.ImpactSummary ?? string.Empty : string.Empty,
                    DistrictEventSeverityPercent = district.ActiveEvent != null ? Math.Max(0f, Math.Min(100f, district.ActiveEvent.Severity * 100f)) : 0f,
                    DistrictEventSeverityLabel = district.ActiveEvent != null ? district.ActiveEvent.SeverityLabel ?? string.Empty : string.Empty,
                })
                .ToArray();
        }

        public IReadOnlyList<TabletNpcRoutePerformance> GetNpcRoutePerformance()
        {
            var contracts = GetNpcRouteContracts();
            if (contracts.Count == 0)
            {
                return Array.Empty<TabletNpcRoutePerformance>();
            }

            var familyLookup = BuildNpcRouteFamilySummaryLookup(contracts);

            return contracts
                .OrderByDescending(contract => contract.TotalDeliveredTons)
                .ThenByDescending(contract => contract.TotalProfitEarned)
                .Select(contract =>
                {
                    var familySummary = ResolveNpcRouteFamilySummary(contract, familyLookup);
                    return new TabletNpcRoutePerformance
                    {
                        Contract = contract,
                        ContractId = contract.Id,
                        Label = BuildRoutePerformanceLabel(contract),
                        Detail = contract.StatusText ?? string.Empty,
                        FamilyLabel = familySummary.Label,
                        FamilyContractCount = Math.Max(1, familySummary.ContractCount),
                        RouteCount = Math.Max(1, familySummary.RouteCount),
                        DeliveredTons = Math.Max(0f, contract.TotalDeliveredTons),
                        LossRatioPercent = Math.Max(0f, contract.LastJourneyLossRatio * 100f),
                        AveragePayout = contract.CompletedDeliveries > 0
                            ? Math.Max(0f, contract.TotalProfitEarned / contract.CompletedDeliveries)
                            : 0f,
                        CompletedDeliveries = Math.Max(0, contract.CompletedDeliveries),
                    };
                })
                .ToArray();
        }

        public TabletNpcRouteDrilldown GetNpcRouteDrilldown(int contractId)
        {
            if (contractId <= 0)
            {
                return null;
            }

            var contracts = GetNpcRouteContracts();
            if (contracts.Count == 0)
            {
                return null;
            }

            var contract = contracts.FirstOrDefault(entry => entry != null && entry.Id == contractId);
            if (contract == null)
            {
                return null;
            }

            var routeLegs = BuildNpcRouteLegSummaries(contract);
            var familyLookup = BuildNpcRouteFamilySummaryLookup(contracts);
            var revenue = Math.Max(0f, contract.TotalProfitEarned);
            var operatingCost = Math.Max(0f, contract.ContractCost) + Math.Max(0f, contract.TotalWeeklyWagesPaid);

            return new TabletNpcRouteDrilldown
            {
                ContractId = contract.Id,
                Label = BuildRoutePerformanceLabel(contract),
                TierLabel = contract.Tier != null ? contract.Tier.DisplayName : "Route",
                StatusText = contract.StatusText ?? string.Empty,
                AssignedVehicleDisplayName = ResolveNpcRouteAssignedVehicleDisplayName(contract, routeLegs),
                RouteCount = routeLegs.Count,
                CurrentRouteIndex = GetNpcRouteCurrentRouteIndex(contract, routeLegs.Count),
                Revenue = revenue,
                OperatingCost = operatingCost,
                NetProfit = revenue - operatingCost,
                CompletedDeliveries = Math.Max(0, contract.CompletedDeliveries),
                DeliveredTons = Math.Max(0f, contract.TotalDeliveredTons),
                AveragePayout = contract.CompletedDeliveries > 0
                    ? Math.Max(0f, contract.TotalProfitEarned / contract.CompletedDeliveries)
                    : 0f,
                LossRatioPercent = Math.Max(0f, contract.LastJourneyLossRatio * 100f),
                RouteFamily = ResolveNpcRouteFamilySummary(contract, familyLookup),
                RouteLegs = routeLegs,
                RecentFinanceEntries = BuildNpcRouteFinanceEntries(contract.Id, GetCurrentFinanceMinute()),
            };
        }

        public IReadOnlyList<TabletRoutePlannerCandidate> GetRoutePlannerCandidates()
        {
            var filtered = ApplyRoutePlannerFiltersAndSort(BuildRoutePlannerCandidatesInternal());
            EnsureSelectedRoutePlannerCandidateId(filtered);
            return filtered;
        }

        public TabletRoutePlannerCandidate GetSelectedRoutePlannerCandidate()
        {
            var candidates = GetRoutePlannerCandidates();
            var selectedId = EnsureSelectedRoutePlannerCandidateId(candidates);
            return candidates.FirstOrDefault(candidate => string.Equals(candidate.CandidateId, selectedId, StringComparison.OrdinalIgnoreCase));
        }

        public TabletRoutePlannerCandidate GetRoutePlannerCandidate(string candidateId)
        {
            candidateId = string.IsNullOrWhiteSpace(candidateId) ? string.Empty : candidateId.Trim();
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                return GetSelectedRoutePlannerCandidate();
            }

            return BuildRoutePlannerCandidatesInternal()
                .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase));
        }

        public RoutePlannerOverlaySnapshot GetRoutePlannerOverlaySnapshot()
        {
            var candidates = GetRoutePlannerCandidates();
            var selected = GetSelectedRoutePlannerCandidate();
            var lanes = new List<RoutePlannerOverlayLane>();

            AddPlannerOverlayCandidates(lanes, candidates.Where(candidate => candidate.HasActiveNpcRoute), RoutePlannerOverlayLaneKind.ActiveNpc, 3);
            AddPlannerOverlayCandidates(lanes, candidates.Where(candidate => candidate.IsUnderperformingActiveLane), RoutePlannerOverlayLaneKind.Underperforming, 3);
            AddPlannerOverlayCandidates(lanes, candidates.Where(candidate => candidate.AvailabilityState == RoutePlannerAvailabilityState.Available && !candidate.HasActiveNpcRoute), RoutePlannerOverlayLaneKind.Recommended, 3);
            AddPlannerOverlayCandidates(lanes, candidates.Where(candidate => candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked), RoutePlannerOverlayLaneKind.Blocked, 3);

            if (selected != null)
            {
                AddPlannerOverlayCandidate(lanes, selected, RoutePlannerOverlayLaneKind.Selected, true);
            }

            return new RoutePlannerOverlaySnapshot
            {
                SelectedCandidateId = selected != null ? selected.CandidateId : string.Empty,
                SelectedDistrictA = selected != null && selected.OriginIndustry != null ? selected.OriginIndustry.DistrictName ?? string.Empty : string.Empty,
                SelectedDistrictB = selected != null && selected.DestinationIndustry != null ? selected.DestinationIndustry.DistrictName ?? string.Empty : string.Empty,
                Lanes = lanes,
            };
        }

        public TabletBudgetOverview GetBudgetOverview()
        {
            var currentMinute = GetCurrentFinanceMinute();
            var currentBalance = _getProfit != null ? _getProfit() : 0f;
            var weeklyIncome = _financeTracker != null
                ? _financeTracker.GetTotalAmount(currentMinute, InGameMinutesPerWeek, CompanyFinanceFlow.Income)
                : 0f;
            var weeklyExpenses = _financeTracker != null
                ? _financeTracker.GetTotalAmount(currentMinute, InGameMinutesPerWeek, CompanyFinanceFlow.Expense)
                : 0f;
            var upcomingBills = GetUpcomingBillsInternal(currentMinute);
            var fleetResale = _propertyManager != null
                ? BuildFleetResaleSummary(_propertyManager.GetFleetSaleSummary())
                : new TabletFleetResaleSummary();
            return new TabletBudgetOverview
            {
                CurrentBalance = currentBalance,
                DailyNet = _financeTracker != null ? _financeTracker.GetNetAmount(currentMinute, InGameMinutesPerDay) : 0f,
                WeeklyNet = weeklyIncome - weeklyExpenses,
                WeeklyIncome = weeklyIncome,
                WeeklyExpenses = weeklyExpenses,
                UpcomingBills = upcomingBills.Sum(entry => entry.Amount),
                Forecast = BuildWeeklyForecast(currentMinute, currentBalance, upcomingBills),
                FleetResale = fleetResale,
            };
        }

        public IReadOnlyList<TabletBudgetBreakdownEntry> GetExpenseBreakdown()
        {
            return BuildBudgetBreakdown(GetCurrentFinanceMinute(), CompanyFinanceFlow.Expense, ExpenseBudgetCategories);
        }

        public IReadOnlyList<TabletBudgetBreakdownEntry> GetIncomeBreakdown()
        {
            return BuildBudgetBreakdown(GetCurrentFinanceMinute(), CompanyFinanceFlow.Income, IncomeBudgetCategories);
        }

        public IReadOnlyList<TabletUpcomingBillEntry> GetUpcomingBills()
        {
            return GetUpcomingBillsInternal(GetCurrentFinanceMinute());
        }

        public TabletPropertyPortfolioSummary GetPropertyPortfolioSummary()
        {
            var summary = new TabletPropertyPortfolioSummary();
            if (_propertyManager == null)
            {
                return summary;
            }

            var currentMinute = GetCurrentFinanceMinute();
            var propertyBills = GetPropertyBillEntries(currentMinute);
            var officeBillLookup = propertyBills
                .Where(entry => entry != null && entry.Category == CompanyFinanceCategory.OfficeRent && !string.IsNullOrWhiteSpace(entry.PropertyId))
                .GroupBy(entry => entry.PropertyId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.DueInMinutes).First(), StringComparer.OrdinalIgnoreCase);
            var apartmentBillLookup = propertyBills
                .Where(entry => entry != null && entry.Category == CompanyFinanceCategory.ApartmentRent && !string.IsNullOrWhiteSpace(entry.PropertyId))
                .GroupBy(entry => entry.PropertyId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.DueInMinutes).First(), StringComparer.OrdinalIgnoreCase);
            var activeGarageVehicleCount = _propertyManager.GetActiveCommercialGarageVehicles().Count();
            var reserveVehicleCount = _propertyManager.GetReserveCommercialVehicles().Count();

            var offices = (_propertyManager.Offices ?? Array.Empty<OfficeDefinition>())
                .Where(entry => entry != null)
                .Select(office =>
                {
                    var state = _propertyManager.GetOfficeState(office.OfficeId);
                    var hasAccess = state != null && (state.IsOwned || state.IsRented);
                    var bill = GetPropertyBill(officeBillLookup, office.OfficeId);
                    var isActive = string.Equals(_propertyManager.ActiveOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase);
                    return new TabletPropertyOfficeEntry
                    {
                        OfficeId = office.OfficeId ?? string.Empty,
                        DisplayName = office.DisplayName,
                        DistrictName = office.DistrictName ?? string.Empty,
                        StatusLabel = BuildOfficePortfolioStatusLabel(state),
                        IsOwned = state != null && state.IsOwned,
                        IsRented = state != null && state.IsRented,
                        IsAccessSuspended = state != null && state.IsAccessSuspended,
                        IsActive = isActive,
                        HasArrears = state != null && state.OutstandingRent > 0.01f,
                        ArrearsAmount = state != null ? Math.Max(0f, state.OutstandingRent) : 0f,
                        WeeklyRent = Math.Max(0f, office.WeeklyOfficeRent),
                        PurchasePrice = Math.Max(0f, office.OfficePrice),
                        DueInMinutes = bill != null ? Math.Max(0, bill.DueInMinutes) : (hasAccess && Math.Max(0f, office.WeeklyOfficeRent) > 0.01f ? InGameMinutesPerWeek : int.MaxValue),
                        BillDetail = BuildOfficePortfolioBillDetail(office, state, bill),
                        ActiveGarageVehicleCount = isActive ? activeGarageVehicleCount : 0,
                        ReserveVehicleCount = isActive ? reserveVehicleCount : 0,
                        AssignmentSummary = BuildOfficeAssignmentSummary(office, state, isActive, activeGarageVehicleCount, reserveVehicleCount),
                    };
                })
                .OrderByDescending(entry => entry.IsActive)
                .ThenByDescending(entry => entry.HasArrears)
                .ThenByDescending(entry => entry.IsOwned)
                .ThenByDescending(entry => entry.IsRented)
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var apartments = (_propertyManager.Interiors ?? Array.Empty<InteriorDefinition>())
                .Where(entry => entry != null)
                .Select(apartment =>
                {
                    var state = _propertyManager.GetApartmentState(apartment.InteriorId);
                    var hasAccess = state != null && (state.IsOwned || state.IsRented);
                    var bill = GetPropertyBill(apartmentBillLookup, apartment.InteriorId);
                    return new TabletPropertyApartmentEntry
                    {
                        InteriorId = apartment.InteriorId ?? string.Empty,
                        DisplayName = apartment.DisplayName,
                        InteriorType = apartment.InteriorType ?? string.Empty,
                        InteriorIgName = apartment.InteriorIgName ?? string.Empty,
                        StatusLabel = BuildApartmentPortfolioStatusLabel(state),
                        IsOwned = state != null && state.IsOwned,
                        IsRented = state != null && state.IsRented,
                        IsAccessSuspended = state != null && state.IsAccessSuspended,
                        IsActive = string.Equals(_propertyManager.ActiveApartmentId, apartment.InteriorId, StringComparison.OrdinalIgnoreCase),
                        HasArrears = state != null && state.OutstandingRent > 0.01f,
                        ArrearsAmount = state != null ? Math.Max(0f, state.OutstandingRent) : 0f,
                        WeeklyRent = Math.Max(0f, apartment.InteriorWeeklyRent),
                        PurchasePrice = Math.Max(0f, apartment.InteriorPrice),
                        DueInMinutes = bill != null ? Math.Max(0, bill.DueInMinutes) : (hasAccess && !IsOwnedApartment(state) && Math.Max(0f, apartment.InteriorWeeklyRent) > 0.01f ? InGameMinutesPerWeek : int.MaxValue),
                        BillDetail = BuildApartmentPortfolioBillDetail(apartment, state, bill),
                    };
                })
                .OrderByDescending(entry => entry.IsActive)
                .ThenByDescending(entry => entry.HasArrears)
                .ThenByDescending(entry => entry.IsOwned)
                .ThenByDescending(entry => entry.IsRented)
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var motels = (_propertyManager.Motels ?? Array.Empty<MotelDefinition>())
                .Where(entry => entry != null)
                .Select(motel => new TabletPropertyMotelEntry
                {
                    MotelId = motel.MotelId ?? string.Empty,
                    DisplayName = motel.DisplayName,
                    MotelType = motel.MotelType ?? string.Empty,
                    MotelIgName = motel.MotelIgName ?? string.Empty,
                    NightlyRestPrice = Math.Max(0f, motel.RestPrice),
                })
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            summary.Offices = offices;
            summary.Apartments = apartments;
            summary.Motels = motels;
            summary.OwnedOfficeCount = offices.Count(entry => entry.IsOwned);
            summary.RentedOfficeCount = offices.Count(entry => entry.IsRented && !entry.IsOwned);
            summary.OwnedApartmentCount = apartments.Count(entry => entry.IsOwned);
            summary.RentedApartmentCount = apartments.Count(entry => entry.IsRented && !entry.IsOwned);
            summary.TotalArrears = offices.Sum(entry => entry.ArrearsAmount) + apartments.Sum(entry => entry.ArrearsAmount);
            summary.UpcomingWeeklyRent = propertyBills
                .Where(entry => entry != null && entry.DueInMinutes > 0)
                .Sum(entry => entry.Amount);
            return summary;
        }

        public TabletBudgetForecast GetWeeklyForecast()
        {
            var currentMinute = GetCurrentFinanceMinute();
            var currentBalance = _getProfit != null ? _getProfit() : 0f;
            return BuildWeeklyForecast(currentMinute, currentBalance, GetUpcomingBillsInternal(currentMinute));
        }

        public TabletFleetAlertSummary GetFleetAlertSummary(TabletStateSnapshot snapshot)
        {
            snapshot = snapshot ?? Snapshot ?? new TabletStateSnapshot();

            var summary = new TabletFleetAlertSummary();
            var activeVehicle = ResolveActiveCommercialVehicleRecord();
            if (activeVehicle != null)
            {
                var activeDefinition = !string.IsNullOrWhiteSpace(activeVehicle.PoweredModelName)
                    ? _fleetManager.FindDefinitionByModelName(activeVehicle.PoweredModelName)
                    : null;
                var fuelCapacityLiters = snapshot.FuelCapacityLiters > 0.001f
                    ? Math.Max(0f, snapshot.FuelCapacityLiters)
                    : (activeDefinition != null ? Math.Max(0f, activeDefinition.FuelCapacityLiters) : 0f);
                var fuelCurrentLiters = Math.Max(0f, snapshot.FuelCurrentLiters);

                summary.HasActiveCompanyVehicle = true;
                summary.ActiveVehicleName = ResolveCommercialVehicleLabel(activeVehicle, snapshot.PoweredVehicleName);
                summary.FuelCurrentLiters = fuelCurrentLiters;
                summary.FuelCapacityLiters = fuelCapacityLiters;
                summary.FuelRatio = fuelCapacityLiters > 0.001f
                    ? ModMath.Clamp01(fuelCurrentLiters / fuelCapacityLiters)
                    : ModMath.Clamp01(snapshot.FuelRatio);
                summary.FuelIsEmpty = snapshot.FuelIsEmpty || (fuelCapacityLiters > 0.001f && fuelCurrentLiters <= 0.001f);
            }

            if (_propertyManager == null || _propertyManager.CommercialVehicles == null)
            {
                return summary;
            }

            var currentMinute = GetCurrentFinanceMinute();
            var currentWeekIndex = GetWeekIndex(currentMinute);
            var maintenancePreview = _propertyManager.GetFleetMaintenancePreview(currentMinute);
            var ownedVehicleCount = 0;
            var overdueInspectionCount = 0;

            for (int i = 0; i < _propertyManager.CommercialVehicles.Count; i++)
            {
                var vehicle = _propertyManager.CommercialVehicles[i];
                if (vehicle == null || vehicle.IsRental)
                {
                    continue;
                }

                ownedVehicleCount += 1;

                var overdueWeeks = GetFleetInspectionOverdueWeeks(vehicle, currentWeekIndex);
                if (overdueWeeks > 0)
                {
                    overdueInspectionCount += 1;
                    if (overdueWeeks > summary.WorstInspectionOverdueWeeks)
                    {
                        summary.WorstInspectionOverdueWeeks = overdueWeeks;
                        summary.WorstOverdueVehicleName = ResolveCommercialVehicleLabel(vehicle);
                    }
                }

                var conditionRatio = NormalizeFleetMaintenanceCondition(vehicle.MaintenanceCondition);
                if (conditionRatio < 0.75f)
                {
                    var conditionPercent = conditionRatio * 100f;
                    summary.PoorConditionCount += 1;
                    if (summary.LowestConditionPercent <= 0.001f || conditionPercent < summary.LowestConditionPercent)
                    {
                        summary.LowestConditionPercent = conditionPercent;
                        summary.WorstConditionVehicleName = ResolveCommercialVehicleLabel(vehicle);
                    }
                }
            }

            summary.FleetVehicleCount = maintenancePreview != null
                ? Math.Max(ownedVehicleCount, Math.Max(0, maintenancePreview.VehicleCount))
                : ownedVehicleCount;
            summary.OverdueInspectionCount = maintenancePreview != null
                ? Math.Max(overdueInspectionCount, Math.Max(0, maintenancePreview.OverdueInspectionCount))
                : overdueInspectionCount;
            return summary;
        }

        public IReadOnlyList<TabletBudgetRouteEntry> GetBudgetRouteProfitability()
        {
            var contracts = GetNpcRouteContracts();
            if (contracts.Count == 0)
            {
                return Array.Empty<TabletBudgetRouteEntry>();
            }

            var familyLookup = BuildNpcRouteFamilySummaryLookup(contracts);

            return contracts
                .Select(contract =>
                {
                    var revenue = Math.Max(0f, contract.TotalProfitEarned);
                    var operatingCost = Math.Max(0f, contract.ContractCost) + Math.Max(0f, contract.TotalWeeklyWagesPaid);
                    var familySummary = ResolveNpcRouteFamilySummary(contract, familyLookup);
                    return new TabletBudgetRouteEntry
                    {
                        ContractId = contract.Id,
                        Label = BuildRoutePerformanceLabel(contract),
                        Detail = string.Format(
                            "{0} | {1}",
                            contract.Tier != null ? contract.Tier.DisplayName : "Route",
                            contract.StatusText ?? string.Empty).Trim(),
                        FamilyLabel = familySummary.Label,
                        FamilyContractCount = Math.Max(1, familySummary.ContractCount),
                        RouteCount = Math.Max(1, familySummary.RouteCount),
                        Revenue = revenue,
                        OperatingCost = operatingCost,
                        NetProfit = revenue - operatingCost,
                        CompletedDeliveries = Math.Max(0, contract.CompletedDeliveries),
                        DeliveredTons = Math.Max(0f, contract.TotalDeliveredTons),
                    };
                })
                .OrderByDescending(entry => entry.NetProfit)
                .ThenByDescending(entry => entry.Revenue)
                .ToArray();
        }

        public TabletInventoryValuation GetInventoryValuation()
        {
            var locationValues = new List<TabletInventoryValueEntry>();
            var commodityValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            float totalValue = 0f;
            var fleetResale = _propertyManager != null
                ? BuildFleetResaleSummary(_propertyManager.GetFleetSaleSummary())
                : new TabletFleetResaleSummary();

            if (_industryManager != null && _industryManager.Industries != null)
            {
                foreach (var industry in _industryManager.Industries.Where(ShouldIncludeIndustryInventory))
                {
                    float locationValue = 0f;
                    float totalTons = 0f;
                    var warehouseRisk = industry.IsWarehouse
                        ? _industryManager.GetWarehouseStorageRiskSnapshot(industry)
                        : null;
                    var storageMultiplier = 1f;
                    if (industry.IsWarehouse)
                    {
                        storageMultiplier = warehouseRisk != null && warehouseRisk.InventoryValue > 0.01f
                            ? Math.Max(0f, Math.Min(1f, warehouseRisk.AdjustedInventoryValue / warehouseRisk.InventoryValue))
                            : Math.Max(0.55f, Math.Min(1f, industry.StorageCondition));
                    }

                    foreach (var commodity in industry.BufferStorage.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                    {
                        var tons = Math.Max(0f, industry.GetStock(commodity));
                        if (tons <= 0.01f)
                        {
                            continue;
                        }

                        var value = tons * _globalMarket.GetUnitPrice(commodity) * storageMultiplier;
                        locationValue += value;
                        totalTons += tons;
                        AddCommodityValue(commodityValues, commodity, value);
                    }

                    if (industry.OmegaStorage > 0.01f)
                    {
                        var omegaValue = industry.OmegaStorage * _globalMarket.GetUnitPrice("Omega");
                        locationValue += omegaValue;
                        totalTons += industry.OmegaStorage;
                        AddCommodityValue(commodityValues, "Omega", omegaValue);
                    }

                    if (locationValue <= 0.01f)
                    {
                        continue;
                    }

                    totalValue += locationValue;
                    locationValues.Add(new TabletInventoryValueEntry
                    {
                        Label = industry.Name,
                        Detail = BuildInventoryLocationDetail(industry, totalTons),
                        Value = locationValue,
                    });
                }
            }

            if (_propertyManager != null)
            {
                var fuelUnitPrice = _globalMarket.GetUnitPrice("Fuel");
                foreach (var office in _propertyManager.Offices.Where(office => office != null))
                {
                    var officeState = _propertyManager.GetOfficeState(office.OfficeId);
                    if (officeState == null || (!officeState.IsOwned && !officeState.IsRented))
                    {
                        continue;
                    }

                    var storedLiters = Math.Max(0f, _propertyManager.GetOfficeObjectStoredResourceAmount(office.OfficeId, OfficeObjectFunction.Refuel));
                    if (storedLiters <= 0.05f)
                    {
                        continue;
                    }

                    var value = (storedLiters / 1000f) * fuelUnitPrice;
                    totalValue += value;
                    AddCommodityValue(commodityValues, "Fuel", value);
                    locationValues.Add(new TabletInventoryValueEntry
                    {
                        Label = string.Format("{0} tank", office.DisplayName),
                        Detail = string.Format("{0:0}L diesel stored", storedLiters),
                        Value = value,
                    });
                }

                foreach (var vehicle in _propertyManager.CommercialVehicles.Where(entry => entry != null))
                {
                    var commodity = CommodityCatalog.Normalize(vehicle.Commodity);
                    var weightTons = Math.Max(0f, vehicle.WeightTons);
                    if (string.IsNullOrWhiteSpace(commodity) || weightTons <= 0.01f)
                    {
                        continue;
                    }

                    var value = weightTons * _globalMarket.GetUnitPrice(commodity);
                    totalValue += value;
                    AddCommodityValue(commodityValues, commodity, value);
                    locationValues.Add(new TabletInventoryValueEntry
                    {
                        Label = vehicle.DisplayName,
                        Detail = string.Format("{0} {1:0.0}t | {2}", commodity, weightTons, vehicle.IsDeployed ? "deployed cargo" : "garage cargo"),
                        Value = value,
                    });
                }
            }

            return new TabletInventoryValuation
            {
                TotalValue = totalValue,
                TopLocations = locationValues
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .ToArray(),
                TopCommodities = commodityValues
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .Select(pair => new TabletInventoryValueEntry
                    {
                        Label = pair.Key,
                        Detail = "Combined value across all tracked locations",
                        Value = pair.Value,
                    })
                    .ToArray(),
                FleetResale = fleetResale,
            };
        }

        public NpcWorldDispatchOverview GetWorldDispatchOverview()
        {
            return _npcLogisticsManager != null
                ? _npcLogisticsManager.GetWorldDispatchOverview() ?? new NpcWorldDispatchOverview()
                : new NpcWorldDispatchOverview();
        }

        public IReadOnlyList<NpcWorldJobSummary> GetWorldDispatchJobs()
        {
            return _npcLogisticsManager != null && _npcLogisticsManager.WorldJobs != null
                ? _npcLogisticsManager.WorldJobs
                : Array.Empty<NpcWorldJobSummary>();
        }

        public PlayerContractsOverview GetPlayerContractsOverview()
        {
            return _playerContractsManager != null
                ? _playerContractsManager.GetOverview() ?? new PlayerContractsOverview()
                : new PlayerContractsOverview();
        }

        public IReadOnlyList<PlayerContractListingSummary> GetPlayerContractListings(PlayerContractType type)
        {
            return _playerContractsManager != null
                ? _playerContractsManager.GetListings(type) ?? Array.Empty<PlayerContractListingSummary>()
                : Array.Empty<PlayerContractListingSummary>();
        }

        public IReadOnlyList<PlayerContractListingSummary> GetAcceptedPlayerContracts()
        {
            return _playerContractsManager != null
                ? _playerContractsManager.GetAcceptedContracts() ?? Array.Empty<PlayerContractListingSummary>()
                : Array.Empty<PlayerContractListingSummary>();
        }

        public PlayerContractListingSummary GetPlayerContractById(string contractId)
        {
            return _playerContractsManager != null
                ? _playerContractsManager.GetContractById(contractId)
                : null;
        }

        public void CyclePlayerContractCommodityFilter(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CycleCommodityFilter(delta);
            MarkViewDirty();
        }

        public void CyclePlayerContractDistrictFilter(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CycleDistrictFilter(delta);
            MarkViewDirty();
        }

        public void CyclePlayerContractRigClassFilter(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CycleRigClassFilter(delta);
            MarkViewDirty();
        }

        public void CyclePlayerContractExpiryFilter(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CycleExpiryFilter(delta);
            MarkViewDirty();
        }

        public void CyclePlayerContractPayoutDensityFilter(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CyclePayoutDensityFilter(delta);
            MarkViewDirty();
        }

        public void CyclePlayerContractSortMode(int delta)
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.CycleSortMode(delta);
            MarkViewDirty();
        }

        public void RefreshPlayerContractsBoard()
        {
            if (_playerContractsManager == null)
            {
                return;
            }

            _playerContractsManager.ForceRefreshBoard(GetCurrentFinanceMinute());
            MarkNetworkDirty();
            MarkViewDirty();
        }

        public bool TryAcceptPlayerContract(string contractId, out string message)
        {
            message = "Player contracts are unavailable.";
            if (_playerContractsManager == null)
            {
                return false;
            }

            var accepted = _playerContractsManager.TryAccept(contractId, out message);
            if (accepted)
            {
                MarkNetworkDirty();
                MarkCargoDirty();
                MarkViewDirty();
            }

            return accepted;
        }

        public bool TryCancelPlayerContract(string contractId, out string message)
        {
            message = "Player contracts are unavailable.";
            if (_playerContractsManager == null)
            {
                return false;
            }

            var cancelled = _playerContractsManager.TryCancelAccepted(contractId, out message);
            if (cancelled)
            {
                MarkNetworkDirty();
                MarkCargoDirty();
                MarkViewDirty();
            }

            return cancelled;
        }

        public bool TryDeployQuickJobVehicle(string contractId, out string message)
        {
            message = "Player contracts are unavailable.";
            if (_playerContractsManager == null)
            {
                return false;
            }

            var deployed = _playerContractsManager.TryDeployQuickJobVehicle(contractId, out message);
            if (deployed)
            {
                MarkNetworkDirty();
                MarkCargoDirty();
                MarkViewDirty();
            }

            return deployed;
        }

        public IReadOnlyList<NpcWorldDispatchDiagnosticEntry> GetWorldDispatchDiagnostics()
        {
            return _npcLogisticsManager != null && _npcLogisticsManager.WorldDispatchDiagnostics != null
                ? _npcLogisticsManager.WorldDispatchDiagnostics
                : Array.Empty<NpcWorldDispatchDiagnosticEntry>();
        }

        public void CycleWorldDispatchPolicy(int delta)
        {
            if (_npcLogisticsManager == null)
            {
                return;
            }

            _npcLogisticsManager.CycleWorldDispatchPolicy(delta);
            MarkViewDirty();
        }

        public void CycleWorldPriorityCommodity(int delta)
        {
            if (_npcLogisticsManager == null)
            {
                return;
            }

            _npcLogisticsManager.CycleWorldPriorityCommodity(delta);
            MarkViewDirty();
        }

        public void CycleWorldPriorityDistrict(int delta)
        {
            if (_npcLogisticsManager == null)
            {
                return;
            }

            _npcLogisticsManager.CycleWorldPriorityDistrict(delta);
            MarkViewDirty();
        }

        public void TogglePremiumDispatch()
        {
            if (_npcLogisticsManager == null)
            {
                return;
            }

            _npcLogisticsManager.TogglePremiumDispatch();
            MarkViewDirty();
        }

        public bool IsIndustryInRange(Industry industry, float interactionDistance, out float distance)
        {
            distance = float.MaxValue;
            var player = _getPlayer != null ? _getPlayer() : null;
            if (industry == null || player == null || !player.Exists() || _getIndustryMarkerPosition == null)
            {
                return false;
            }

            distance = player.Position.DistanceTo(_getIndustryMarkerPosition(industry));
            return distance <= interactionDistance;
        }

        public IndustryLoadOptionsSnapshot GetLoadOptions(Industry industry)
        {
            var snapshot = new IndustryLoadOptionsSnapshot();
            var player = _getPlayer != null ? _getPlayer() : null;
            if (industry == null)
            {
                snapshot.StatusText = "No industry selected.";
                ClearLoadOptionsCache();
                return snapshot;
            }

            if (player == null || !player.Exists())
            {
                snapshot.StatusText = "Player unavailable.";
                ClearLoadOptionsCache();
                return snapshot;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                snapshot.StatusText = "Bring a cargo vehicle close to the industry.";
                ClearLoadOptionsCache();
                return snapshot;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null)
            {
                snapshot.StatusText = "Unable to read cargo state.";
                ClearLoadOptionsCache();
                return snapshot;
            }

            snapshot.HasCargoVehicle = true;
            snapshot.CargoVehicleName = cargoVehicle.DisplayName;
            snapshot.CargoType = cargoState.CargoType;
            snapshot.FreeCapacityTons = Math.Max(0f, cargoState.FreeCapacityTons);

            if (!cargoState.IsEmpty)
            {
                snapshot.StatusText = "Unload current cargo before loading a new product.";
                ClearLoadOptionsCache();
                return snapshot;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = _getFallbackCargoType != null ? _getFallbackCargoType() : VehicleCargoType.Aggregates;
            }

            var freeCapacityTons = Math.Max(0f, cargoState.FreeCapacityTons);
            var now = Game.GameTime;
            if (!CanReuseCachedLoadOptions(industry, cargoVehicle.Handle, cargoType, freeCapacityTons, now))
            {
                _industryManager.PopulateLoadableOutputs(industry, cargoType, _cachedLoadOptions);
                _cachedLoadOptions.RemoveAll(commodity => !_fleetManager.CanVehicleCarryCommodity(cargoVehicle, commodity));
                BuildLoadOptionSubtitles(industry, _cachedLoadOptions, freeCapacityTons, _cachedLoadOptionSubtitles);

                _cachedLoadIndustry = industry;
                _cachedLoadVehicleHandle = cargoVehicle.Handle;
                _cachedLoadCargoType = cargoType;
                _cachedLoadFreeCapacityTons = freeCapacityTons;
                _lastLoadOptionsRefreshMs = now;
                _hasCachedLoadOptions = true;
            }

            snapshot.CargoType = cargoType;
            snapshot.LoadOptions = _cachedLoadOptions.ToArray();
            snapshot.LoadOptionSubtitles = new Dictionary<string, string>(_cachedLoadOptionSubtitles, StringComparer.OrdinalIgnoreCase);
            if (snapshot.LoadOptions.Count == 0)
            {
                snapshot.StatusText = "No compatible product available to load.";
            }

            return snapshot;
        }

        private TabletStateSnapshot BuildSnapshot()
        {
            var snapshot = new TabletStateSnapshot();
            RefreshAllSnapshotSlices(snapshot);
            return snapshot;
        }

        private IReadOnlyList<TabletLocationSummary> BuildLocationSummaries(ExternalLocationKind locationKind, Func<Industry, bool> filter = null)
        {
            var summaries = new List<TabletLocationSummary>();
            var industries = _industryManager.Industries
                .Where(industry => industry != null && industry.LocationKind == locationKind && (filter == null || filter(industry)))
                .OrderBy(industry => industry.Name)
                .ToList();

            for (int i = 0; i < industries.Count; i++)
            {
                var industry = industries[i];
                var storage = industry.GetInputStockTotal() + industry.GetOutputStockTotal();
                var totalCapacity = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
                var fillRatio = ModMath.Clamp01(storage / totalCapacity);
                var productionWarning = industry.GetProductionWarning() ?? string.Empty;
                var requiresPermitForGameplay = _industryManager.RequiresContractorPermit(industry);
                var hasPermitForGameplay = _industryManager.HasContractorPermitForGameplay(industry);
                var isOwnedByPlayer = industry.IsOwned;
                var warehouseRisk = industry.SiteRole == SiteRole.Warehouse
                    ? _industryManager.GetWarehouseStorageRiskSnapshot(industry)
                    : null;

                var summary = new TabletLocationSummary
                {
                    Industry = industry,
                    LocationKind = locationKind,
                    Name = industry.Name,
                    OwnershipTag = isOwnedByPlayer
                        ? "~g~[OWNED]~s~"
                        : "~r~[NOT OWNED]~s~",
                    PermitTag = !requiresPermitForGameplay
                        ? "~g~[OPEN]~s~"
                        : (hasPermitForGameplay ? "~g~[PERMIT]~s~" : "~r~[LOCKED]~s~"),
                    IsOwnedByPlayer = isOwnedByPlayer,
                    RequiresIndustryPurchase = _industryManager.RequiresIndustryPurchase(industry),
                    HasContractorPermitForGameplay = hasPermitForGameplay,
                    RequiresContractorPermit = requiresPermitForGameplay,
                    StorageTons = storage,
                    TotalCapacityTons = totalCapacity,
                    FillRatio = fillRatio,
                    OmegaStorageTons = industry.OmegaStorage,
                    OmegaCapacityTons = industry.OmegaCapacityTons,
                    OutputPerHourTons = industry.CurrentOutputPerHourTons,
                    UtilizationPercent = industry.LastUtilizationPercent,
                    WarehouseRisk = warehouseRisk,
                    ProductionWarning = productionWarning,
                    OverviewDetail = BuildOverviewDetail(locationKind, industry, storage, fillRatio, productionWarning, warehouseRisk),
                    PrimaryConversion = industry.GetPrimaryConversionDescription(),
                    ModuleSummary = industry.SiteRole == SiteRole.Warehouse
                        ? string.Format(
                            "Storage In Lv.{0} | Storage Out Lv.{1}",
                            industry.InputStorageModuleLevel,
                            industry.OutputStorageModuleLevel)
                        : string.Format(
                            "Prod Lv.{0} | In Lv.{1} | Out Lv.{2} | Omega Lv.{3}",
                            industry.ProductionModuleLevel,
                            industry.InputStorageModuleLevel,
                            industry.OutputStorageModuleLevel,
                            industry.OmegaStorageModuleLevel),
                };

                PopulateServiceSiteBusinessSummary(summary, industry, fillRatio);
                summaries.Add(summary);
            }

            return summaries;
        }

        private void PopulateServiceSiteBusinessSummary(TabletLocationSummary summary, Industry industry, float fillRatio)
        {
            if (summary == null || industry == null)
            {
                return;
            }

            var siteState = _territoryManager != null
                ? _territoryManager.GetSiteState(industry)
                : null;
            PopulateServiceSiteContractSummary(summary, industry, siteState);

            PopulateServiceSiteBusinessPreview(summary, industry, siteState);

            if (!industry.IsOwned || !summary.HasServiceBusinessInfo)
            {
                return;
            }

            PopulateOwnedServiceSiteBusinessSummary(summary, industry, fillRatio);
        }

        private static void PopulateServiceSiteBusinessPreview(TabletLocationSummary summary, Industry industry, TerritorySiteState siteState)
        {
            if (!industry.IsStore && !industry.IsGasStation)
            {
                return;
            }

            var weeklyIncome = Math.Max(0f, industry.WeeklyPassiveIncome);
            var staffingCost = Math.Max(0f, ServiceSiteEconomyPolicy.ComputeWeeklyStaffingCost(industry));
            var hasPassiveIncomeInfo = weeklyIncome > 0.01f
                || staffingCost > 0.01f
                || (siteState != null && (siteState.LastPassiveIncomeAmount > 0.01f || !string.IsNullOrWhiteSpace(siteState.LastPassiveIncomeStatus)));
            if (!hasPassiveIncomeInfo)
            {
                return;
            }

            summary.HasServiceBusinessInfo = true;
            summary.ServiceWeeklyIncome = weeklyIncome;
            summary.ServiceWeeklyStaffingCost = staffingCost;
            summary.ServiceStaffAssigned = industry.IsOwned && siteState != null && siteState.SiteOperatorAssigned;
            summary.ServiceStockReady = siteState != null && siteState.PassiveIncomeStockReady;
            summary.ServiceOperational = industry.IsOwned && siteState != null && siteState.PassiveIncomeOperational;
            summary.ServiceLastPassiveIncome = industry.IsOwned && siteState != null ? Math.Max(0f, siteState.LastPassiveIncomeAmount) : 0f;
            summary.ServiceStaffStatus = industry.IsOwned
                ? (summary.ServiceStaffAssigned ? "Assigned" : "Missing")
                : "Requires operator";
            summary.ServiceStockStatus = industry.IsOwned
                ? (summary.ServiceStockReady ? "Ready" : "Low stock")
                : "Requires stock";
            summary.ServiceOperationsStatus = industry.IsOwned
                ? (summary.ServiceOperational ? "Operational" : "Inactive")
                : "Potential only";
            summary.ServicePassiveIncomeStatus = BuildServiceSitePassiveIncomeStatus(industry, siteState);
            summary.ServiceRecentPayoutStatus = BuildServiceSitePayoutStatus(industry, siteState);
        }

        private static void PopulateServiceSiteContractSummary(TabletLocationSummary summary, Industry industry, TerritorySiteState siteState)
        {
            if (summary == null
                || industry == null
                || siteState == null
                || (!industry.IsStore && !industry.IsGasStation && industry.SiteRole != SiteRole.ConstructionSiteSink))
            {
                return;
            }

            summary.HasServiceContractInfo = true;
            summary.ServiceCurrentWeekDeliveries = Math.Max(0, siteState.CurrentWeekServiceDeliveries);
            summary.ServiceCurrentWeekTons = Math.Max(0f, siteState.CurrentWeekServiceTons);
            summary.ServiceRequiredWeeklyTons = Math.Max(0f, siteState.RequiredWeeklyServiceTons);
            summary.ServicePenaltySteps = Math.Max(0, siteState.ServicePenaltySteps);
            summary.ServiceSuccessStreak = Math.Max(0, siteState.ServiceSuccessStreak);
            summary.ServiceTargetMetLastWeek = siteState.ServiceTargetMetLastWeek;
            summary.ServiceContractStatus = siteState.ServiceContractStatus ?? string.Empty;
        }

        private static string BuildServiceSitePassiveIncomeStatus(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (!industry.IsOwned)
            {
                return "Potential passive income locked until purchase";
            }

            if (siteState != null && !string.IsNullOrWhiteSpace(siteState.PassiveIncomeStatus))
            {
                return siteState.PassiveIncomeStatus;
            }

            return "Passive income pending";
        }

        private static string BuildServiceSitePayoutStatus(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null || !industry.IsOwned)
            {
                return string.Empty;
            }

            if (siteState == null || siteState.LastPassiveIncomeWeekIndex < 0)
            {
                return "No completed weekly payout yet";
            }

            return !string.IsNullOrWhiteSpace(siteState.LastPassiveIncomeStatus)
                ? siteState.LastPassiveIncomeStatus
                : "No recent payout";
        }

        private static void PopulateOwnedServiceSiteBusinessSummary(TabletLocationSummary summary, Industry industry, float fillRatio)
        {
            if (summary == null || industry == null)
            {
                return;
            }

            summary.OverviewDetail = BuildOwnedServiceSiteOverviewDetail(summary, industry, fillRatio);
        }

        private static string BuildOwnedServiceSiteOverviewDetail(TabletLocationSummary summary, Industry industry, float fillRatio)
        {
            if (summary == null || industry == null)
            {
                return string.Empty;
            }

            var segments = new List<string>
            {
                string.Format("Income {0}/wk", ModFormatting.FormatMoney(summary.ServiceWeeklyIncome)),
                summary.ServiceOperational ? "Operational" : "Inactive",
                summary.ServiceStaffAssigned ? "Staffed" : "No staff",
                summary.ServiceStockReady ? "Stock ready" : "Low stock",
            };

            if (!string.IsNullOrWhiteSpace(summary.ServiceContractStatus)
                && !string.Equals(summary.ServiceContractStatus, "Open market", StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(summary.ServiceContractStatus);
            }

            if (industry.IsGasStation)
            {
                segments.Add(string.Format("{0:0}% full", fillRatio * 100f));
            }

            return string.Join(" | ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).ToArray());
        }


        private IReadOnlyList<TabletMarketHighlight> BuildMarketHighlights(Industry nearestIndustry, VehicleCargoState cargoState)
        {
            var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cargoCommodity = cargoState != null && !cargoState.IsEmpty
                ? CommodityCatalog.Normalize(cargoState.Commodity)
                : string.Empty;
            var districtName = nearestIndustry != null ? nearestIndustry.DistrictName : string.Empty;

            if (!string.IsNullOrWhiteSpace(cargoCommodity))
            {
                reasons[cargoCommodity] = ResolveMarketHighlightReason(cargoCommodity, districtName, "Active cargo");
            }

            if (nearestIndustry != null)
            {
                AddMarketHighlights(reasons, nearestIndustry.Outputs, districtName, "Nearby output");
                AddMarketHighlights(reasons, nearestIndustry.Inputs, districtName, "Nearby demand");
            }

            if (reasons.Count < 3)
            {
                var networkOutputs = _industryManager.Industries
                    .Where(industry => industry != null && industry.Outputs != null)
                    .SelectMany(industry => industry.Outputs)
                    .Take(24)
                    .ToList();
                AddMarketHighlights(reasons, networkOutputs, string.Empty, "Network output");
            }

            return reasons
                .Select(pair => new TabletMarketHighlight
                {
                    Commodity = pair.Key,
                    Reason = pair.Value,
                    UnitPrice = _globalMarket.GetUnitPrice(pair.Key),
                })
                .OrderByDescending(highlight => highlight.UnitPrice)
                .Take(3)
                .ToArray();
        }

        private IReadOnlyList<TabletMarketResourcePrice> BuildMarketPrices()
        {
            return CommodityCatalog.GetKnownCommodities()
                .Select(commodity => new TabletMarketResourcePrice
                {
                    Commodity = commodity,
                    CargoType = CommodityCatalog.GetCargoTypeForCommodity(commodity),
                    UnitPrice = _globalMarket.GetUnitPrice(commodity),
                })
                .OrderBy(price => price.Commodity, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void AddMarketHighlights(IDictionary<string, string> reasons, IEnumerable<string> commodities, string districtName, string fallbackReason)
        {
            if (reasons == null || commodities == null)
            {
                return;
            }

            foreach (var commodity in commodities)
            {
                var normalized = CommodityCatalog.Normalize(commodity);
                if (string.IsNullOrWhiteSpace(normalized) || reasons.ContainsKey(normalized))
                {
                    continue;
                }

                reasons[normalized] = ResolveMarketHighlightReason(normalized, districtName, fallbackReason);
                if (reasons.Count >= 6)
                {
                    return;
                }
            }
        }

        private string ResolveMarketHighlightReason(string commodity, string districtName, string fallbackReason)
        {
            string shockReason;
            return _globalMarket != null && _globalMarket.TryGetShockHighlightReason(commodity, districtName, out shockReason)
                ? shockReason
                : fallbackReason;
        }

        private static string BuildOverviewDetail(ExternalLocationKind locationKind, Industry industry, float storage, float fillRatio, string productionWarning, WarehouseStorageRiskSnapshot warehouseRisk)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                var detail = string.Format("Storage {0:0.0}t | {1:0}% full | Condition {2:0}%", storage, fillRatio * 100f, Math.Max(0f, Math.Min(100f, industry.StorageCondition * 100f)));
                var warehouseTelemetry = TabletUiHelpers.BuildWarehouseOverviewTelemetry(warehouseRisk);
                return string.IsNullOrWhiteSpace(warehouseTelemetry)
                    ? detail
                    : string.Format("{0} | {1}", detail, warehouseTelemetry);
            }

            if (locationKind == ExternalLocationKind.Industry)
            {
                var detail = string.Format("Storage {0:0.0}t | Omega {1:0.0}t", storage, industry.OmegaStorage);
                if (!string.IsNullOrWhiteSpace(productionWarning))
                {
                    detail += string.Format(" | ~r~{0}~s~", productionWarning);
                }

                return detail;
            }

            if (locationKind == ExternalLocationKind.GasStation)
            {
                return string.Format(
                    "Fuel {0:0.0}t | {1:0}% full{2}",
                    storage,
                    fillRatio * 100f,
                    industry.RefuelIsFree ? " | Free office refuel" : string.Empty);
            }

            return string.Format("Storage {0:0.0}t | {1:0}% full", storage, fillRatio * 100f);
        }

        private static string BuildRoutePerformanceLabel(NpcLogisticsContract contract)
        {
            return NpcRouteProfitabilityFormatter.BuildContractLabel(contract);
        }

        private IReadOnlyList<NpcLogisticsContract> GetNpcRouteContracts()
        {
            return _npcLogisticsManager != null && _npcLogisticsManager.Contracts != null
                ? _npcLogisticsManager.Contracts.Where(contract => contract != null).ToArray()
                : Array.Empty<NpcLogisticsContract>();
        }

        private List<TabletRoutePlannerCandidate> BuildRoutePlannerCandidatesInternal()
        {
            var candidates = new List<TabletRoutePlannerCandidate>();
            if (_npcLogisticsManager == null || _industryManager == null || _globalMarket == null)
            {
                return candidates;
            }

            var currentMinute = GetCurrentFinanceMinute();
            var originCandidates = _npcLogisticsManager.GetRoutePlannerOriginCandidates();
            var contracts = GetNpcRouteContracts();
            var familyLookup = BuildNpcRouteFamilySummaryLookup(contracts);
            var exactContractLookup = BuildRoutePlannerContractLookup(contracts);

            for (int originIndex = 0; originCandidates != null && originIndex < originCandidates.Count; originIndex++)
            {
                var originIndustry = originCandidates[originIndex];
                if (originIndustry == null)
                {
                    continue;
                }

                var destinations = _npcLogisticsManager.GetRoutePlannerDestinationCandidates(originIndustry);
                for (int destinationIndex = 0; destinations != null && destinationIndex < destinations.Count; destinationIndex++)
                {
                    var destinationIndustry = destinations[destinationIndex];
                    if (destinationIndustry == null || string.Equals(originIndustry.Id, destinationIndustry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var resources = _npcLogisticsManager.GetResourceOptions(originIndustry, destinationIndustry);
                    if (resources == null || resources.Count == 0)
                    {
                        continue;
                    }

                    var blockerSummary = _npcLogisticsManager.GetRoutePlannerLaneBlockReason(originIndustry, destinationIndustry);
                    var availabilityState = string.IsNullOrWhiteSpace(blockerSummary)
                        ? RoutePlannerAvailabilityState.Available
                        : RoutePlannerAvailabilityState.Blocked;

                    for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
                    {
                        var commodity = CommodityCatalog.Normalize(resources[resourceIndex]);
                        if (string.IsNullOrWhiteSpace(commodity))
                        {
                            continue;
                        }

                        var candidateId = BuildRoutePlannerCandidateId(originIndustry, destinationIndustry, commodity);
                        NpcLogisticsContract matchingContract;
                        exactContractLookup.TryGetValue(candidateId, out matchingContract);

                        var familyKey = BuildRoutePlannerFamilyKey(commodity);
                        TabletNpcRouteFamilySummary familySummary;
                        var hasFamilySummary = familyLookup.TryGetValue(familyKey, out familySummary) && familySummary != null;
                        familySummary = hasFamilySummary ? familySummary : new TabletNpcRouteFamilySummary();

                        var suggestedShipmentTons = GetSuggestedShipmentTons(originIndustry, destinationIndustry, commodity);
                        var currentUnitPrice = Math.Max(0f, _globalMarket.GetUnitPrice(commodity));
                        var projectedValue = currentUnitPrice * suggestedShipmentTons;
                        var projectedPayout = _industryManager.ComputeDeliveryProfit(destinationIndustry, commodity, suggestedShipmentTons, _globalMarket, currentMinute);
                        var realizedRevenue = matchingContract != null
                            ? Math.Max(0f, matchingContract.TotalProfitEarned)
                            : Math.Max(0f, familySummary.Revenue);
                        var realizedOperatingCost = matchingContract != null
                            ? Math.Max(0f, matchingContract.ContractCost) + Math.Max(0f, matchingContract.TotalWeeklyWagesPaid)
                            : Math.Max(0f, familySummary.OperatingCost);
                        var realizedAveragePayout = matchingContract != null
                            ? (matchingContract.CompletedDeliveries > 0 ? Math.Max(0f, matchingContract.TotalProfitEarned / matchingContract.CompletedDeliveries) : 0f)
                            : Math.Max(0f, familySummary.AveragePayout);
                        var realizedLossRatioPercent = matchingContract != null
                            ? Math.Max(0f, matchingContract.LastJourneyLossRatio * 100f)
                            : Math.Max(0f, familySummary.AverageLossRatioPercent);
                        var candidate = new TabletRoutePlannerCandidate
                        {
                            CandidateId = candidateId,
                            OriginIndustry = originIndustry,
                            DestinationIndustry = destinationIndustry,
                            Commodity = commodity,
                            DistrictPairLabel = BuildRoutePlannerDistrictPairLabel(originIndustry, destinationIndustry),
                            CorridorId = BuildRoutePlannerCorridorId(originIndustry, destinationIndustry),
                            AvailabilityState = availabilityState,
                            AvailabilityLabel = availabilityState == RoutePlannerAvailabilityState.Available
                                ? (matchingContract != null ? "Active NPC lane" : "Available")
                                : "Blocked",
                            BlockerSummary = blockerSummary ?? string.Empty,
                            CurrentUnitPrice = currentUnitPrice,
                            SuggestedShipmentTons = suggestedShipmentTons,
                            ProjectedValue = projectedValue,
                            ProjectedPayout = projectedPayout,
                            RealizedRevenue = realizedRevenue,
                            RealizedOperatingCost = realizedOperatingCost,
                            RealizedNetProfit = realizedRevenue - realizedOperatingCost,
                            RealizedAveragePayout = realizedAveragePayout,
                            RealizedLossRatioPercent = realizedLossRatioPercent,
                            MatchingContractId = matchingContract != null ? matchingContract.Id : 0,
                            MatchingContractLabel = matchingContract != null ? BuildRoutePerformanceLabel(matchingContract) : string.Empty,
                            RouteFamily = familySummary,
                            HasActiveNpcRoute = matchingContract != null,
                            HasRouteFamilyHistory = hasFamilySummary && (familySummary.ContractCount > 0 || familySummary.CompletedDeliveries > 0),
                            CanDraftNpcRoute = availabilityState == RoutePlannerAvailabilityState.Available,
                        };

                        candidate.IsUnderperformingActiveLane = matchingContract != null
                            && matchingContract.CompletedDeliveries > 0
                            && (candidate.RealizedNetProfit < 0f || candidate.RealizedAveragePayout + 0.01f < candidate.ProjectedPayout * 0.80f);

                        candidate.ScoreBreakdown = BuildRoutePlannerScoreBreakdown(candidate);
                        candidate.OptimizerScore = candidate.ScoreBreakdown.TotalScore;
                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        private IReadOnlyList<TabletRoutePlannerCandidate> ApplyRoutePlannerFiltersAndSort(IReadOnlyList<TabletRoutePlannerCandidate> candidates)
        {
            candidates = candidates ?? Array.Empty<TabletRoutePlannerCandidate>();
            var commodityFilter = EnsureRoutePlannerCommodityFilter();
            var districtFilter = EnsureRoutePlannerDistrictFilter();

            IEnumerable<TabletRoutePlannerCandidate> query = candidates;
            if (!string.IsNullOrWhiteSpace(commodityFilter))
            {
                query = query.Where(candidate => string.Equals(candidate.Commodity, commodityFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(districtFilter))
            {
                query = query.Where(candidate => candidate != null && candidate.InvolvesDistrict(districtFilter));
            }

            switch (_routePlannerAvailabilityFilterMode)
            {
                case RoutePlannerAvailabilityFilterMode.Available:
                    query = query.Where(candidate => candidate != null && candidate.AvailabilityState == RoutePlannerAvailabilityState.Available);
                    break;
                case RoutePlannerAvailabilityFilterMode.Blocked:
                    query = query.Where(candidate => candidate != null && candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked);
                    break;
                case RoutePlannerAvailabilityFilterMode.ActiveNpc:
                    query = query.Where(candidate => candidate != null && candidate.HasActiveNpcRoute);
                    break;
                case RoutePlannerAvailabilityFilterMode.Underperforming:
                    query = query.Where(candidate => candidate != null && candidate.IsUnderperformingActiveLane);
                    break;
            }

            switch (_routePlannerSortMode)
            {
                case RoutePlannerSortMode.ProjectedPayout:
                    query = query.OrderByDescending(candidate => candidate.ProjectedPayout).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.ProjectedValue:
                    query = query.OrderByDescending(candidate => candidate.ProjectedValue).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.RealizedNetProfit:
                    query = query.OrderByDescending(candidate => candidate.RealizedNetProfit).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.UnitPrice:
                    query = query.OrderByDescending(candidate => candidate.CurrentUnitPrice).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.Commodity:
                    query = query.OrderBy(candidate => candidate.Commodity, StringComparer.OrdinalIgnoreCase).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.District:
                    query = query.OrderBy(candidate => candidate.DistrictPairLabel, StringComparer.OrdinalIgnoreCase).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                case RoutePlannerSortMode.Availability:
                    query = query.OrderBy(candidate => candidate.AvailabilityState).ThenByDescending(candidate => candidate.OptimizerScore).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    query = query.OrderByDescending(candidate => candidate.OptimizerScore).ThenByDescending(candidate => candidate.ProjectedPayout).ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return query.ToArray();
        }

        private static IDictionary<string, NpcLogisticsContract> BuildRoutePlannerContractLookup(IEnumerable<NpcLogisticsContract> contracts)
        {
            var lookup = new Dictionary<string, NpcLogisticsContract>(StringComparer.OrdinalIgnoreCase);
            if (contracts == null)
            {
                return lookup;
            }

            foreach (var contract in contracts.Where(entry => entry != null))
            {
                foreach (var route in GetContractRoutesForPlanner(contract))
                {
                    var candidateId = BuildRoutePlannerCandidateId(route.OriginIndustry, route.DestinationIndustry, route.Commodity);
                    if (string.IsNullOrWhiteSpace(candidateId) || lookup.ContainsKey(candidateId))
                    {
                        continue;
                    }

                    lookup[candidateId] = contract;
                }
            }

            return lookup;
        }

        private static IReadOnlyList<NpcLogisticsRouteDefinition> GetContractRoutesForPlanner(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return Array.Empty<NpcLogisticsRouteDefinition>();
            }

            if (contract.Routes != null && contract.Routes.Count > 0)
            {
                return contract.Routes.Where(route => route != null).ToArray();
            }

            if (contract.OriginIndustry == null || contract.DestinationIndustry == null)
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
                    AssignedVehicleAssetId = contract.AssignedVehicleAssetId,
                    AssignedVehicleDisplayName = contract.AssignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = contract.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = contract.DestinationTriggerThresholdPercent,
                },
            };
        }

        private static string BuildRoutePlannerCandidateId(Industry originIndustry, Industry destinationIndustry, string commodity)
        {
            if (originIndustry == null || destinationIndustry == null)
            {
                return string.Empty;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            return string.IsNullOrWhiteSpace(commodity)
                ? string.Empty
                : string.Format(
                    "{0}|{1}|{2}",
                    originIndustry.Id ?? string.Empty,
                    destinationIndustry.Id ?? string.Empty,
                    commodity);
        }

        private static string BuildRoutePlannerFamilyKey(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            return string.IsNullOrWhiteSpace(commodity)
                ? string.Empty
                : string.Format("1|{0}", commodity);
        }

        private static string BuildRoutePlannerDistrictPairLabel(Industry originIndustry, Industry destinationIndustry)
        {
            var originDistrict = originIndustry != null ? originIndustry.DistrictName ?? string.Empty : string.Empty;
            var destinationDistrict = destinationIndustry != null ? destinationIndustry.DistrictName ?? string.Empty : string.Empty;
            return string.Equals(originDistrict, destinationDistrict, StringComparison.OrdinalIgnoreCase)
                ? originDistrict
                : string.Format("{0} -> {1}", originDistrict, destinationDistrict);
        }

        private static string BuildRoutePlannerCorridorId(Industry originIndustry, Industry destinationIndustry)
        {
            var originDistrict = originIndustry != null ? originIndustry.DistrictName ?? string.Empty : string.Empty;
            var destinationDistrict = destinationIndustry != null ? destinationIndustry.DistrictName ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(originDistrict) || string.IsNullOrWhiteSpace(destinationDistrict))
            {
                return string.Empty;
            }

            return string.Compare(originDistrict, destinationDistrict, StringComparison.OrdinalIgnoreCase) <= 0
                ? string.Format("{0}->{1}", originDistrict, destinationDistrict)
                : string.Format("{0}->{1}", destinationDistrict, originDistrict);
        }

        private static float GetSuggestedShipmentTons(Industry originIndustry, Industry destinationIndustry, string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            var availableTons = originIndustry != null ? Math.Max(0f, originIndustry.GetStock(commodity)) : 0f;
            var currentDestinationStock = destinationIndustry != null ? Math.Max(0f, destinationIndustry.GetStock(commodity)) : 0f;
            var destinationCapacity = destinationIndustry != null
                ? Math.Max(1f, destinationIndustry.InputCapacityTons + destinationIndustry.OutputCapacityTons)
                : 1f;
            var destinationSlack = Math.Max(1f, destinationCapacity - currentDestinationStock);
            var baseShipment = availableTons > 0.01f ? availableTons : 1f;
            return Math.Max(1f, Math.Min(10f, Math.Min(baseShipment, destinationSlack)));
        }

        private static RoutePlannerScoreBreakdown BuildRoutePlannerScoreBreakdown(TabletRoutePlannerCandidate candidate)
        {
            candidate = candidate ?? new TabletRoutePlannerCandidate();
            var breakdown = new RoutePlannerScoreBreakdown();
            breakdown.EligibilityScore = candidate.AvailabilityState == RoutePlannerAvailabilityState.Available ? 45f : 8f;
            breakdown.ProjectedPayoutScore = Math.Min(28f, candidate.ProjectedPayout / 250f);
            breakdown.MarketScore = Math.Min(15f, candidate.CurrentUnitPrice / 150f);

            if (candidate.HasActiveNpcRoute || candidate.HasRouteFamilyHistory)
            {
                breakdown.ActualPerformanceScore += candidate.RealizedNetProfit >= 0f
                    ? Math.Min(18f, candidate.RealizedNetProfit / 800f)
                    : -Math.Min(18f, Math.Abs(candidate.RealizedNetProfit) / 800f);
                breakdown.ActualPerformanceScore -= Math.Min(8f, candidate.RealizedLossRatioPercent / 6f);
            }

            if (candidate.IsUnderperformingActiveLane)
            {
                breakdown.ActualPerformanceScore -= 8f;
            }

            breakdown.BlockerPenalty = candidate.AvailabilityState == RoutePlannerAvailabilityState.Blocked
                ? ResolveRoutePlannerBlockerPenalty(candidate.BlockerSummary)
                : 0f;
            breakdown.TotalScore = breakdown.EligibilityScore
                + breakdown.ProjectedPayoutScore
                + breakdown.MarketScore
                + breakdown.ActualPerformanceScore
                - breakdown.BlockerPenalty;
            return breakdown;
        }

        private static float ResolveRoutePlannerBlockerPenalty(string blockerSummary)
        {
            blockerSummary = blockerSummary ?? string.Empty;
            if (blockerSummary.IndexOf("permit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 26f;
            }

            if (blockerSummary.IndexOf("established", StringComparison.OrdinalIgnoreCase) >= 0
                || blockerSummary.IndexOf("influence", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 30f;
            }

            if (blockerSummary.IndexOf("corridor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 24f;
            }

            if (blockerSummary.IndexOf("compatible", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 16f;
            }

            return string.IsNullOrWhiteSpace(blockerSummary) ? 0f : 20f;
        }

        private void AddPlannerOverlayCandidates(List<RoutePlannerOverlayLane> lanes, IEnumerable<TabletRoutePlannerCandidate> candidates, RoutePlannerOverlayLaneKind kind, int maxCount)
        {
            if (lanes == null || candidates == null || maxCount <= 0)
            {
                return;
            }

            foreach (var candidate in candidates.Where(entry => entry != null).Take(maxCount))
            {
                AddPlannerOverlayCandidate(lanes, candidate, kind, false);
            }
        }

        private static void AddPlannerOverlayCandidate(List<RoutePlannerOverlayLane> lanes, TabletRoutePlannerCandidate candidate, RoutePlannerOverlayLaneKind kind, bool forceSelection)
        {
            if (lanes == null || candidate == null || candidate.OriginIndustry == null || candidate.DestinationIndustry == null)
            {
                return;
            }

            var existing = lanes.FirstOrDefault(entry => entry != null && string.Equals(entry.CandidateId, candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsSelected = existing.IsSelected || forceSelection;
                if (forceSelection)
                {
                    existing.Kind = RoutePlannerOverlayLaneKind.Selected;
                }

                return;
            }

            lanes.Add(new RoutePlannerOverlayLane
            {
                CandidateId = candidate.CandidateId,
                DistrictA = candidate.OriginIndustry.DistrictName ?? string.Empty,
                DistrictB = candidate.DestinationIndustry.DistrictName ?? string.Empty,
                Label = string.Format("{0} {1}", candidate.Commodity, candidate.DistrictPairLabel).Trim(),
                Kind = forceSelection ? RoutePlannerOverlayLaneKind.Selected : kind,
                IsSelected = forceSelection,
            });
        }

        private List<string> GetRoutePlannerCommodityOptions()
        {
            var options = BuildRoutePlannerCandidatesInternal()
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.Commodity))
                .Select(candidate => candidate.Commodity)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            options.Insert(0, string.Empty);
            return options;
        }

        private List<string> GetRoutePlannerDistrictOptions()
        {
            var options = BuildRoutePlannerCandidatesInternal()
                .Where(candidate => candidate != null)
                .SelectMany(candidate => new[]
                {
                    candidate.OriginIndustry != null ? candidate.OriginIndustry.DistrictName : string.Empty,
                    candidate.DestinationIndustry != null ? candidate.DestinationIndustry.DistrictName : string.Empty,
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            options.Insert(0, string.Empty);
            return options;
        }

        private static IReadOnlyDictionary<string, TabletNpcRouteFamilySummary> BuildNpcRouteFamilySummaryLookup(IEnumerable<NpcLogisticsContract> contracts)
        {
            var lookup = new Dictionary<string, TabletNpcRouteFamilySummary>(StringComparer.OrdinalIgnoreCase);
            if (contracts == null)
            {
                return lookup;
            }

            foreach (var group in contracts
                .Where(contract => contract != null)
                .GroupBy(contract => NpcRouteProfitabilityFormatter.BuildFamilyKey(contract), StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    continue;
                }

                lookup[group.Key] = NpcRouteProfitabilityFormatter.BuildFamilySummary(group);
            }

            return lookup;
        }

        private static TabletNpcRouteFamilySummary ResolveNpcRouteFamilySummary(NpcLogisticsContract contract, IReadOnlyDictionary<string, TabletNpcRouteFamilySummary> familyLookup)
        {
            var key = NpcRouteProfitabilityFormatter.BuildFamilyKey(contract);
            TabletNpcRouteFamilySummary summary;
            if (!string.IsNullOrWhiteSpace(key) && familyLookup != null && familyLookup.TryGetValue(key, out summary) && summary != null)
            {
                return summary;
            }

            return NpcRouteProfitabilityFormatter.BuildFamilySummary(new[] { contract });
        }

        private static IReadOnlyList<TabletNpcRouteLegSummary> BuildNpcRouteLegSummaries(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return Array.Empty<TabletNpcRouteLegSummary>();
            }

            var routes = contract.Routes != null && contract.Routes.Count > 0
                ? contract.Routes
                : new List<NpcLogisticsRouteDefinition>
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

            var routeCount = routes.Count;
            var currentRouteIndex = GetNpcRouteCurrentRouteIndex(contract, routeCount);
            var summaries = new List<TabletNpcRouteLegSummary>(routeCount);
            for (int i = 0; i < routeCount; i++)
            {
                var route = routes[i];
                summaries.Add(new TabletNpcRouteLegSummary
                {
                    RouteIndex = i + 1,
                    RouteCount = routeCount,
                    IsCurrentRoute = i == currentRouteIndex,
                    OriginName = route != null && route.OriginIndustry != null && !string.IsNullOrWhiteSpace(route.OriginIndustry.Name)
                        ? route.OriginIndustry.Name
                        : (contract.OriginIndustry != null ? contract.OriginIndustry.Name : "Origin"),
                    DestinationName = route != null && route.DestinationIndustry != null && !string.IsNullOrWhiteSpace(route.DestinationIndustry.Name)
                        ? route.DestinationIndustry.Name
                        : (contract.DestinationIndustry != null ? contract.DestinationIndustry.Name : "Destination"),
                    Commodity = CommodityCatalog.Normalize(route != null ? route.Commodity : contract.Commodity),
                    AssignedVehicleDisplayName = !string.IsNullOrWhiteSpace(route != null ? route.AssignedVehicleDisplayName : string.Empty)
                        ? route.AssignedVehicleDisplayName
                        : (contract.AssignedVehicleDisplayName ?? string.Empty),
                    OriginTriggerThresholdPercent = route != null ? route.OriginTriggerThresholdPercent : contract.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = route != null ? route.DestinationTriggerThresholdPercent : contract.DestinationTriggerThresholdPercent,
                });
            }

            return summaries;
        }

        private IReadOnlyList<TabletNpcRouteFinanceEntry> BuildNpcRouteFinanceEntries(int contractId, int currentMinute)
        {
            if (_financeTracker == null || contractId <= 0 || _financeTracker.Transactions == null)
            {
                return Array.Empty<TabletNpcRouteFinanceEntry>();
            }

            return _financeTracker.Transactions
                .Where(entry => entry != null && entry.RouteContractId == contractId)
                .OrderByDescending(entry => entry.InGameMinute)
                .ThenByDescending(entry => entry.Sequence)
                .Take(4)
                .Select(entry => new TabletNpcRouteFinanceEntry
                {
                    Flow = entry.Flow,
                    Category = entry.Category,
                    Amount = Math.Max(0f, entry.Amount),
                    AgeMinutes = Math.Max(0, currentMinute - entry.InGameMinute),
                    Description = entry.Description ?? string.Empty,
                })
                .ToArray();
        }

        private static string ResolveNpcRouteAssignedVehicleDisplayName(NpcLogisticsContract contract, IReadOnlyList<TabletNpcRouteLegSummary> routeLegs)
        {
            if (routeLegs != null)
            {
                var activeRoute = routeLegs.FirstOrDefault(route => route != null && route.IsCurrentRoute);
                if (activeRoute != null && !string.IsNullOrWhiteSpace(activeRoute.AssignedVehicleDisplayName))
                {
                    return activeRoute.AssignedVehicleDisplayName;
                }
            }

            return contract != null ? contract.AssignedVehicleDisplayName ?? string.Empty : string.Empty;
        }

        private static int GetNpcRouteCurrentRouteIndex(NpcLogisticsContract contract, int routeCount)
        {
            if (routeCount <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(routeCount - 1, contract != null ? contract.CurrentRouteIndex : 0));
        }

        private IReadOnlyList<TabletBudgetBreakdownEntry> BuildBudgetBreakdown(int currentMinute, CompanyFinanceFlow flow, IReadOnlyList<CompanyFinanceCategory> categories)
        {
            if (_financeTracker == null || categories == null || categories.Count == 0)
            {
                return Array.Empty<TabletBudgetBreakdownEntry>();
            }

            var dayTotals = _financeTracker
                .GetTransactionsInWindow(currentMinute, InGameMinutesPerDay, flow, null)
                .GroupBy(entry => entry.Category)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Amount));
            var weekTotals = _financeTracker
                .GetTransactionsInWindow(currentMinute, InGameMinutesPerWeek, flow, null)
                .GroupBy(entry => entry.Category)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Amount));

            return categories
                .Select(category => new TabletBudgetBreakdownEntry
                {
                    Category = category,
                    Label = GetBudgetCategoryLabel(category),
                    DayTotal = GetCategoryTotal(dayTotals, category),
                    WeekTotal = GetCategoryTotal(weekTotals, category),
                })
                .Where(entry => entry.DayTotal > 0.01f || entry.WeekTotal > 0.01f)
                .OrderByDescending(entry => entry.WeekTotal)
                .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private List<TabletUpcomingBillEntry> GetUpcomingBillsInternal(int currentMinute)
        {
            var bills = new List<TabletUpcomingBillEntry>();
            var currentWeekIndex = GetWeekIndex(currentMinute);
            var currentDayIndex = GetDayIndex(currentMinute);
            var nextWeekDueInMinutes = Math.Max(0, ((currentWeekIndex + 1) * InGameMinutesPerWeek) - currentMinute);
            var nextDayDueInMinutes = Math.Max(0, ((currentDayIndex + 1) * InGameMinutesPerDay) - currentMinute);

            if (_propertyManager != null)
            {
                bills.AddRange(GetPropertyBillEntries(currentMinute).Select(entry => new TabletUpcomingBillEntry
                {
                    Category = entry.Category,
                    Label = entry.Label,
                    Detail = entry.Detail,
                    Amount = entry.Amount,
                    DueInMinutes = entry.DueInMinutes,
                }));

                foreach (var vehicle in _propertyManager.CommercialVehicles.Where(entry => entry != null && entry.IsRental && entry.DailyRent > 0.01f))
                {
                    var overdueDays = vehicle.LastChargedDayIndex >= 0 ? Math.Max(0, currentDayIndex - vehicle.LastChargedDayIndex) : 0;
                    if (overdueDays > 0)
                    {
                        bills.Add(new TabletUpcomingBillEntry
                        {
                            Category = CompanyFinanceCategory.VehicleRent,
                            Label = vehicle.DisplayName,
                            Detail = string.Format("Rental charge overdue by {0} day{1}", overdueDays, overdueDays == 1 ? string.Empty : "s"),
                            Amount = vehicle.DailyRent * overdueDays,
                            DueInMinutes = 0,
                        });
                        continue;
                    }

                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.VehicleRent,
                        Label = vehicle.DisplayName,
                        Detail = "Daily commercial rental charge",
                        Amount = vehicle.DailyRent,
                        DueInMinutes = nextDayDueInMinutes,
                    });
                }

                var corporateOverhead = _propertyManager.GetCorporateOverheadPreview(currentMinute);
                if (corporateOverhead != null && corporateOverhead.WeeklyAmount > 0.01f)
                {
                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.CorporateOverhead,
                        Label = "Corporate overhead",
                        Detail = string.Format(
                            "Scale {0} | Sites {1} | Fleet {2} | NPC {3} | Districts {4} | Support {5} | Corridors {6}",
                            corporateOverhead.ScaleScore,
                            corporateOverhead.OwnedSiteCount,
                            corporateOverhead.OwnedFleetCount,
                            corporateOverhead.ActiveNpcCount,
                            corporateOverhead.LicensedDistrictCount,
                            corporateOverhead.SecuredSupportSiteCount,
                            corporateOverhead.ActiveCorridorCount),
                        Amount = corporateOverhead.AmountDue,
                        DueInMinutes = corporateOverhead.DueInMinutes,
                    });
                }

                var fleetMaintenance = _propertyManager.GetFleetMaintenancePreview(currentMinute);
                if (fleetMaintenance != null && fleetMaintenance.WeeklyAmount > 0.01f)
                {
                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.FleetMaintenance,
                        Label = "Fleet maintenance",
                        Detail = string.Format(
                            "Owned rigs {0} | Bay coverage {1} | Overdue inspections {2} | Avg condition {3:0}%",
                            fleetMaintenance.VehicleCount,
                            fleetMaintenance.CoveredVehicleCount,
                            fleetMaintenance.OverdueInspectionCount,
                            fleetMaintenance.AverageConditionPercent),
                        Amount = fleetMaintenance.AmountDue,
                        DueInMinutes = fleetMaintenance.DueInMinutes,
                    });
                }
            }

            AddServiceSiteStaffingBills(bills, _industryManager != null ? _industryManager.Industries : null, _territoryManager, nextWeekDueInMinutes);

            if (_npcLogisticsManager != null && _npcLogisticsManager.Contracts != null)
            {
                foreach (var contract in _npcLogisticsManager.Contracts.Where(entry => entry != null && entry.Tier != null))
                {
                    var weeklyWage = Math.Max(0f, _npcLogisticsManager.GetWeeklyWage(contract.Tier));
                    if (weeklyWage <= 0.01f)
                    {
                        continue;
                    }

                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.NpcWages,
                        Label = BuildRoutePerformanceLabel(contract),
                        Detail = string.Format("Weekly payroll for {0}", contract.Tier.DisplayName),
                        Amount = weeklyWage,
                        DueInMinutes = _npcLogisticsManager.GetRemainingPayrollMinutes(contract),
                    });
                }
            }

            if (_bankLoanManager != null && _bankLoanManager.ActiveLoan != null)
            {
                var activeLoan = _bankLoanManager.ActiveLoan;
                var dueInMinutes = Math.Max(0, (activeLoan.NextDueWeekIndex * InGameMinutesPerWeek) - currentMinute);
                bills.Add(new TabletUpcomingBillEntry
                {
                    Category = CompanyFinanceCategory.LoanRepayment,
                    Label = string.IsNullOrWhiteSpace(activeLoan.BankName) ? "Company loan" : activeLoan.BankName,
                    Detail = string.Format(
                        "Weekly company loan installment | {0} week{1} remaining",
                        activeLoan.WeeksRemaining,
                        activeLoan.WeeksRemaining == 1 ? string.Empty : "s"),
                    Amount = Math.Max(0f, Math.Min(activeLoan.WeeklyInstallment, activeLoan.RemainingBalance)),
                    DueInMinutes = dueInMinutes,
                });
            }

            if (_getTerritoryOperationsSummary != null)
            {
                var summary = _getTerritoryOperationsSummary() ?? new TerritoryOperationsSummary();
                var dueInMinutes = _getRemainingTerritoryOperationsChargeMinutes != null
                    ? _getRemainingTerritoryOperationsChargeMinutes(currentMinute)
                    : InGameMinutesPerWeek;

                if (summary.Districts != null)
                {
                    foreach (var district in summary.Districts.Where(entry => entry != null && entry.AdministrationCost > 0.01f))
                    {
                        bills.Add(new TabletUpcomingBillEntry
                        {
                            Category = CompanyFinanceCategory.TerritoryOperations,
                            Label = string.Format("{0} charter", district.DistrictName),
                            Detail = BuildTerritoryCharterBillDetail(district),
                            Amount = district.AdministrationCost,
                            DueInMinutes = dueInMinutes,
                        });
                    }
                }

                if (summary.InfrastructureCost > 0.01f)
                {
                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.TerritoryOperations,
                        Label = "Territory footprint",
                        Detail = BuildTerritoryOperationsBillDetail(summary),
                        Amount = summary.InfrastructureCost,
                        DueInMinutes = dueInMinutes,
                    });
                }
            }

            return bills
                .Where(entry => entry != null && entry.Amount > 0.01f)
                .OrderBy(entry => entry.DueInMinutes)
                .ThenByDescending(entry => entry.Amount)
                .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<TabletPropertyBillEntry> GetPropertyBillEntries(int currentMinute)
        {
            var bills = new List<TabletPropertyBillEntry>();
            if (_propertyManager == null)
            {
                return bills;
            }

            var currentWeekIndex = GetWeekIndex(currentMinute);
            var nextWeekDueInMinutes = Math.Max(0, ((currentWeekIndex + 1) * InGameMinutesPerWeek) - currentMinute);

            foreach (var office in _propertyManager.Offices.Where(entry => entry != null))
            {
                var state = _propertyManager.GetOfficeState(office.OfficeId);
                if (state == null || (!state.IsOwned && !state.IsRented))
                {
                    continue;
                }

                if (state.OutstandingRent > 0.01f)
                {
                    bills.Add(new TabletPropertyBillEntry
                    {
                        PropertyId = office.OfficeId ?? string.Empty,
                        Category = CompanyFinanceCategory.OfficeRent,
                        Label = office.DisplayName,
                        Detail = "Office arrears are blocking access until paid.",
                        Amount = state.OutstandingRent,
                        DueInMinutes = 0,
                    });
                    continue;
                }

                var weeklyRent = Math.Max(0f, office.WeeklyOfficeRent);
                if (weeklyRent <= 0.01f)
                {
                    continue;
                }

                bills.Add(new TabletPropertyBillEntry
                {
                    PropertyId = office.OfficeId ?? string.Empty,
                    Category = CompanyFinanceCategory.OfficeRent,
                    Label = office.DisplayName,
                    Detail = "Weekly office rent",
                    Amount = weeklyRent,
                    DueInMinutes = nextWeekDueInMinutes,
                });
            }

            foreach (var apartment in _propertyManager.Interiors.Where(entry => entry != null))
            {
                var state = _propertyManager.GetApartmentState(apartment.InteriorId);
                if (state == null || state.IsOwned || !state.IsRented)
                {
                    continue;
                }

                if (state.OutstandingRent > 0.01f)
                {
                    bills.Add(new TabletPropertyBillEntry
                    {
                        PropertyId = apartment.InteriorId ?? string.Empty,
                        Category = CompanyFinanceCategory.ApartmentRent,
                        Label = apartment.DisplayName,
                        Detail = "Apartment arrears are outstanding.",
                        Amount = state.OutstandingRent,
                        DueInMinutes = 0,
                    });
                    continue;
                }

                var weeklyRent = Math.Max(0f, apartment.InteriorWeeklyRent);
                if (weeklyRent <= 0.01f)
                {
                    continue;
                }

                bills.Add(new TabletPropertyBillEntry
                {
                    PropertyId = apartment.InteriorId ?? string.Empty,
                    Category = CompanyFinanceCategory.ApartmentRent,
                    Label = apartment.DisplayName,
                    Detail = "Weekly apartment rent",
                    Amount = weeklyRent,
                    DueInMinutes = nextWeekDueInMinutes,
                });
            }

            return bills;
        }

        internal static void AddServiceSiteStaffingBills(
            ICollection<TabletUpcomingBillEntry> bills,
            IEnumerable<Industry> industries,
            TerritoryManager territoryManager,
            int dueInMinutes)
        {
            if (bills == null || industries == null || territoryManager == null)
            {
                return;
            }

            foreach (var industry in industries.Where(entry => entry != null && entry.IsOwned && (entry.IsStore || entry.IsGasStation)))
            {
                if (!territoryManager.HasServiceSiteOperatorAssigned(industry))
                {
                    continue;
                }

                var weeklyCost = territoryManager.GetServiceSiteWeeklyStaffingCost(industry);
                if (weeklyCost <= 0.01f)
                {
                    continue;
                }

                bills.Add(new TabletUpcomingBillEntry
                {
                    Category = CompanyFinanceCategory.ServiceSiteStaffing,
                    Label = industry.Name,
                    Detail = industry.IsGasStation
                        ? "Weekly station operator payroll"
                        : "Weekly store operator payroll",
                    Amount = weeklyCost,
                    DueInMinutes = Math.Max(0, dueInMinutes),
                });
            }
        }

        private TabletBudgetForecast BuildWeeklyForecast(int currentMinute, float currentBalance, IReadOnlyList<TabletUpcomingBillEntry> bills)
        {
            var estimatedIncome = _financeTracker != null
                ? _financeTracker.GetTotalAmount(currentMinute, InGameMinutesPerWeek, CompanyFinanceFlow.Income)
                : 0f;
            var knownBills = bills != null
                ? bills.Where(entry => entry != null && entry.DueInMinutes <= InGameMinutesPerWeek).Sum(entry => entry.Amount)
                : 0f;
            var projectedEndingBalance = currentBalance + estimatedIncome - knownBills;
            var lowestProjectedBalance = currentBalance;
            var cumulativeBills = 0f;

            if (bills != null)
            {
                foreach (var bill in bills.Where(entry => entry != null && entry.DueInMinutes <= InGameMinutesPerWeek).OrderBy(entry => entry.DueInMinutes))
                {
                    cumulativeBills += bill.Amount;
                    var incomeRatio = Math.Max(0f, Math.Min(1f, bill.DueInMinutes / (float)InGameMinutesPerWeek));
                    var projectedAtDue = currentBalance + (estimatedIncome * incomeRatio) - cumulativeBills;
                    lowestProjectedBalance = Math.Min(lowestProjectedBalance, projectedAtDue);
                }
            }

            lowestProjectedBalance = Math.Min(lowestProjectedBalance, projectedEndingBalance);
            return new TabletBudgetForecast
            {
                CurrentBalance = currentBalance,
                EstimatedIncome = estimatedIncome,
                KnownBills = knownBills,
                ProjectedEndingBalance = projectedEndingBalance,
                LowestProjectedBalance = lowestProjectedBalance,
                TurnsNegative = lowestProjectedBalance < 0f,
            };
        }

        private static float GetCategoryTotal(IDictionary<CompanyFinanceCategory, float> totals, CompanyFinanceCategory category)
        {
            if (totals == null)
            {
                return 0f;
            }

            float value;
            return totals.TryGetValue(category, out value)
                ? Math.Max(0f, value)
                : 0f;
        }

        private static TabletPropertyBillEntry GetPropertyBill(
            IReadOnlyDictionary<string, TabletPropertyBillEntry> lookup,
            string propertyId)
        {
            if (lookup == null || string.IsNullOrWhiteSpace(propertyId))
            {
                return null;
            }

            TabletPropertyBillEntry entry;
            return lookup.TryGetValue(propertyId.Trim(), out entry)
                ? entry
                : null;
        }

        private static string BuildOfficePortfolioStatusLabel(OfficeOwnershipPersistenceEntry state)
        {
            if (state == null || (!state.IsOwned && !state.IsRented))
            {
                return "Available";
            }

            if (state.OutstandingRent > 0.01f || state.IsAccessSuspended)
            {
                return "Arrears";
            }

            return state.IsOwned ? "Owned" : "Rented";
        }

        private static string BuildApartmentPortfolioStatusLabel(ApartmentOwnershipPersistenceEntry state)
        {
            if (state == null || (!state.IsOwned && !state.IsRented))
            {
                return "Available";
            }

            if (state.OutstandingRent > 0.01f || state.IsAccessSuspended)
            {
                return "Arrears";
            }

            return state.IsOwned ? "Owned" : "Rented";
        }

        private static string BuildOfficePortfolioBillDetail(
            OfficeDefinition office,
            OfficeOwnershipPersistenceEntry state,
            TabletPropertyBillEntry bill)
        {
            if (state == null || (!state.IsOwned && !state.IsRented))
            {
                return string.Format(
                    "Rent {0} | Buy {1}",
                    ModFormatting.FormatMoney(Math.Max(0f, office != null ? office.WeeklyOfficeRent : 0f)),
                    ModFormatting.FormatMoney(Math.Max(0f, office != null ? office.OfficePrice : 0f)));
            }

            if (bill != null)
            {
                return string.Format(
                    "{0} | {1}",
                    ModFormatting.FormatMoney(bill.Amount),
                    bill.DueInMinutes <= 0 ? "Due now" : "Due next week");
            }

            return state.IsOwned
                ? "Owned access. No rent due."
                : string.Format("Weekly rent {0}", ModFormatting.FormatMoney(Math.Max(0f, office != null ? office.WeeklyOfficeRent : 0f)));
        }

        private static string BuildApartmentPortfolioBillDetail(
            InteriorDefinition apartment,
            ApartmentOwnershipPersistenceEntry state,
            TabletPropertyBillEntry bill)
        {
            if (state == null || (!state.IsOwned && !state.IsRented))
            {
                return string.Format(
                    "Rent {0} | Buy {1}",
                    ModFormatting.FormatMoney(Math.Max(0f, apartment != null ? apartment.InteriorWeeklyRent : 0f)),
                    ModFormatting.FormatMoney(Math.Max(0f, apartment != null ? apartment.InteriorPrice : 0f)));
            }

            if (bill != null)
            {
                return string.Format(
                    "{0} | {1}",
                    ModFormatting.FormatMoney(bill.Amount),
                    bill.DueInMinutes <= 0 ? "Due now" : "Due next week");
            }

            return IsOwnedApartment(state)
                ? "Owned residence. No rent due."
                : string.Format("Weekly rent {0}", ModFormatting.FormatMoney(Math.Max(0f, apartment != null ? apartment.InteriorWeeklyRent : 0f)));
        }

        private static string BuildOfficeAssignmentSummary(
            OfficeDefinition office,
            OfficeOwnershipPersistenceEntry state,
            bool isActive,
            int activeGarageVehicleCount,
            int reserveVehicleCount)
        {
            if (state == null || (!state.IsOwned && !state.IsRented))
            {
                return "Acquire access to assign company vehicles here.";
            }

            if (state.OutstandingRent > 0.01f || state.IsAccessSuspended)
            {
                return "Garage assignment is paused until office arrears are cleared.";
            }

            if (!isActive)
            {
                return "Commercial garage assignment follows the active office.";
            }

            var capacity = office != null ? Math.Max(0, office.MaxCommercialVehicles) : 0;
            return string.Format(
                "Active garage {0}/{1} | Reserve {2}",
                activeGarageVehicleCount,
                capacity,
                reserveVehicleCount);
        }

        private static bool IsOwnedApartment(ApartmentOwnershipPersistenceEntry state)
        {
            return state != null && state.IsOwned;
        }

        private static void AddCommodityValue(IDictionary<string, float> commodityValues, string commodity, float value)
        {
            if (commodityValues == null || string.IsNullOrWhiteSpace(commodity) || value <= 0.01f)
            {
                return;
            }

            var normalized = CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            float existing;
            commodityValues.TryGetValue(normalized, out existing);
            commodityValues[normalized] = existing + value;
        }

        private static TabletFleetResaleSummary BuildFleetResaleSummary(FleetSaleSummary summary)
        {
            summary = summary ?? new FleetSaleSummary();
            var purchaseBasis = Math.Max(0f, summary.TotalPurchaseBasis);
            var estimatedResale = Math.Max(0f, summary.TotalEstimatedResaleValue);

            return new TabletFleetResaleSummary
            {
                OwnedVehicleCount = Math.Max(0, summary.OwnedVehicleCount),
                PurchaseBasis = purchaseBasis,
                EstimatedResaleValue = estimatedResale,
                TotalDepreciationLoss = Math.Max(0f, summary.TotalDepreciationLoss),
                RecoveryPercentOfPurchase = purchaseBasis > 0.01f ? (estimatedResale / purchaseBasis) * 100f : 0f,
                WeakestVehicleName = summary.WorstVehicleName ?? string.Empty,
                WeakestVehicleRecoveryPercent = Math.Max(0f, summary.WorstVehicleRecoveryPercent),
            };
        }

        private bool ShouldIncludeIndustryInventory(Industry industry)
        {
            return industry != null && _industryManager.IsIndustryOwnedForGameplay(industry);
        }

        private string BuildInventoryLocationDetail(Industry industry, float totalTons)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            string locationLabel;
            if (industry.IsConstructionSink)
            {
                locationLabel = "Construction site";
            }
            else if (industry.LocationKind == ExternalLocationKind.Store)
            {
                locationLabel = "Store";
            }
            else if (industry.LocationKind == ExternalLocationKind.GasStation)
            {
                locationLabel = "Gas station";
            }
            else
            {
                locationLabel = industry.IsWarehouse ? "Warehouse" : "Industry";
            }

            if (!industry.IsWarehouse)
            {
                return string.Format("{0} | {1:0.0}t on hand", locationLabel, totalTons);
            }

            var detail = string.Format("{0} | {1:0.0}t on hand | Condition {2:0}%", locationLabel, totalTons, Math.Max(0f, Math.Min(100f, industry.StorageCondition * 100f)));
            var risk = _industryManager != null ? _industryManager.GetWarehouseStorageRiskSnapshot(industry) : null;
            var warehouseTelemetry = TabletUiHelpers.BuildWarehouseOverviewTelemetry(risk);
            return string.IsNullOrWhiteSpace(warehouseTelemetry)
                ? detail
                : string.Format("{0} | {1}", detail, warehouseTelemetry);
        }

        private static string BuildTerritoryOperationsBillDetail(TerritoryOperationsSummary summary)
        {
            summary = summary ?? new TerritoryOperationsSummary();
            return string.Format(
                "Weekly corridor, depot, and franchise upkeep | {0} corridor{1} | {2} support site{3} | {4} premium franchise{5} | Risk {6} corridor / {7} contract",
                summary.ActiveCorridorCount,
                summary.ActiveCorridorCount == 1 ? string.Empty : "s",
                summary.SupportSiteCount,
                summary.SupportSiteCount == 1 ? string.Empty : "s",
                summary.PremiumFranchiseCount,
                summary.PremiumFranchiseCount == 1 ? string.Empty : "s",
                summary.AtRiskCorridorCount,
                summary.AtRiskServiceSiteCount);
        }

        private static string BuildTerritoryCharterBillDetail(TerritoryDistrictOperationsEntry entry)
        {
            if (entry == null)
            {
                return "Weekly district charter fee.";
            }

            return string.Format(
                "Weekly operating charter | Status {0} | Activity {1:0}/{2:0} t",
                entry.LicenseStatus,
                entry.LicenseActivityTons,
                entry.LicenseTargetTons);
        }

        private static string GetBudgetCategoryLabel(CompanyFinanceCategory category)
        {
            switch (category)
            {
                case CompanyFinanceCategory.PlayerDelivery:
                    return "Player deliveries";
                case CompanyFinanceCategory.PlayerContract:
                    return "Player contracts";
                case CompanyFinanceCategory.NpcDelivery:
                    return "NPC deliveries";
                case CompanyFinanceCategory.IndustryIncome:
                    return "Industry income";
                case CompanyFinanceCategory.MissionReward:
                    return "Mission rewards";
                case CompanyFinanceCategory.LoanDisbursement:
                    return "Loan disbursements";
                case CompanyFinanceCategory.OfficeRent:
                    return "Office rent";
                case CompanyFinanceCategory.ApartmentRent:
                    return "Apartment rent";
                case CompanyFinanceCategory.VehicleRent:
                    return "Vehicle rent";
                case CompanyFinanceCategory.NpcWages:
                    return "NPC wages";
                case CompanyFinanceCategory.ServiceSiteStaffing:
                    return "Site staffing";
                case CompanyFinanceCategory.TerritoryOperations:
                    return "Territory operations";
                case CompanyFinanceCategory.CorporateOverhead:
                    return "Corporate overhead";
                case CompanyFinanceCategory.FleetMaintenance:
                    return "Fleet maintenance";
                case CompanyFinanceCategory.WarehouseSpoilage:
                    return "Warehouse spoilage";
                case CompanyFinanceCategory.WarehouseShrinkage:
                    return "Warehouse shrinkage";
                case CompanyFinanceCategory.InventoryLoss:
                    return "Inventory losses";
                case CompanyFinanceCategory.FuelPurchase:
                    return "Fuel purchases";
                case CompanyFinanceCategory.RepairCost:
                    return "Repairs";
                case CompanyFinanceCategory.ServiceCall:
                    return "Service calls";
                case CompanyFinanceCategory.PermitOrLicence:
                    return "Permits and licences";
                case CompanyFinanceCategory.LoanRepayment:
                    return "Loan repayments";
                case CompanyFinanceCategory.OtherExpense:
                    return "Other expenses";
                case CompanyFinanceCategory.OtherIncome:
                    return "Other income";
                default:
                    return category.ToString();
            }
        }

        private int GetCurrentFinanceMinute()
        {
            return _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
        }

        private OwnedCommercialVehiclePersistenceEntry ResolveActiveCommercialVehicleRecord()
        {
            if (_propertyManager == null || _getPlayer == null)
            {
                return null;
            }

            var player = _getPlayer();
            if (player == null || !player.Exists())
            {
                return null;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle))
            {
                return null;
            }

            OwnedCommercialVehiclePersistenceEntry entry;
            if (poweredVehicle != null
                && poweredVehicle.Exists()
                && _propertyManager.TryResolveCommercialVehicleRecord(poweredVehicle, out entry))
            {
                return entry;
            }

            if (cargoVehicle != null
                && cargoVehicle.Exists()
                && _propertyManager.TryResolveCommercialVehicleRecord(cargoVehicle, out entry))
            {
                return entry;
            }

            return null;
        }

        private string ResolveCommercialVehicleLabel(OwnedCommercialVehiclePersistenceEntry vehicle, string fallbackLabel = null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackLabel))
            {
                return fallbackLabel.Trim();
            }

            if (vehicle == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(vehicle.DisplayName))
            {
                return vehicle.DisplayName.Trim();
            }

            var definition = !string.IsNullOrWhiteSpace(vehicle.PoweredModelName)
                ? _fleetManager.FindDefinitionByModelName(vehicle.PoweredModelName)
                : null;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName.Trim();
            }

            return vehicle.PoweredModelName ?? string.Empty;
        }

        private static int GetFleetInspectionOverdueWeeks(OwnedCommercialVehiclePersistenceEntry vehicle, int currentWeekIndex)
        {
            if (vehicle == null)
            {
                return 0;
            }

            var lastInspectionWeekIndex = vehicle.LastInspectionWeekIndex < 0 ? currentWeekIndex : vehicle.LastInspectionWeekIndex;
            return Math.Max(0, currentWeekIndex - lastInspectionWeekIndex - 1);
        }

        private static float NormalizeFleetMaintenanceCondition(float value)
        {
            if (value <= 0f)
            {
                return 1f;
            }

            return Math.Max(0.35f, Math.Min(1f, value));
        }

        private static int GetWeekIndex(int currentInGameMinute)
        {
            return currentInGameMinute <= 0 ? 0 : currentInGameMinute / InGameMinutesPerWeek;
        }

        private static int GetDayIndex(int currentInGameMinute)
        {
            return currentInGameMinute <= 0 ? 0 : currentInGameMinute / InGameMinutesPerDay;
        }

        private static string GetIndustryHistoryKey(Industry industry)
        {
            return industry != null ? industry.Id ?? string.Empty : string.Empty;
        }

        private List<string> GetOrderedCommodityOptions()
        {
            return CommodityCatalog.GetKnownCommodities()
                .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                .Select(CommodityCatalog.Normalize)
                .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(commodity => commodity, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<Industry> GetOrderedUtilizationIndustries()
        {
            return _industryManager.Industries
                .Where(industry => industry != null
                    && TabletLocationFilters.IsProductionIndustry(industry)
                    && !string.IsNullOrWhiteSpace(industry.Id))
                .OrderBy(industry => industry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<Industry> GetOrderedStorageIndustries()
        {
            return _industryManager.Industries
                .Where(industry => industry != null
                    && !string.IsNullOrWhiteSpace(industry.Id)
                    && TabletLocationFilters.IsStorageTrackedSite(industry))
                .OrderBy(industry => industry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void MarkViewDirty()
        {
            _viewDirty = true;
        }

        private string EnsureSelectedTrendCommodity()
        {
            var normalized = CommodityCatalog.Normalize(_selectedTrendCommodity);
            var commodities = GetOrderedCommodityOptions();
            if (commodities.Count == 0)
            {
                _selectedTrendCommodity = string.Empty;
                return _selectedTrendCommodity;
            }

            if (string.IsNullOrWhiteSpace(normalized)
                || !commodities.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                normalized = commodities[0];
            }

            _selectedTrendCommodity = normalized;
            return _selectedTrendCommodity;
        }

        private string EnsureSelectedUtilizationIndustryId()
        {
            var industries = GetOrderedUtilizationIndustries();
            if (industries.Count == 0)
            {
                _selectedUtilizationIndustryId = string.Empty;
                return _selectedUtilizationIndustryId;
            }

            var selected = industries.FirstOrDefault(industry => string.Equals(industry.Id, _selectedUtilizationIndustryId, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                _selectedUtilizationIndustryId = industries[0].Id;
            }

            return _selectedUtilizationIndustryId;
        }

        private string EnsureSelectedStorageIndustryId()
        {
            var industries = GetOrderedStorageIndustries();
            if (industries.Count == 0)
            {
                _selectedStorageIndustryId = string.Empty;
                return _selectedStorageIndustryId;
            }

            var selected = industries.FirstOrDefault(industry => string.Equals(industry.Id, _selectedStorageIndustryId, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                _selectedStorageIndustryId = industries[0].Id;
            }

            return _selectedStorageIndustryId;
        }

        private string EnsureRoutePlannerCommodityFilter()
        {
            var options = GetRoutePlannerCommodityOptions();
            if (options.Count == 0)
            {
                _routePlannerCommodityFilter = string.Empty;
                return _routePlannerCommodityFilter;
            }

            var normalized = CommodityCatalog.Normalize(_routePlannerCommodityFilter);
            if (string.IsNullOrWhiteSpace(normalized) || !options.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _routePlannerCommodityFilter = string.Empty;
            }
            else
            {
                _routePlannerCommodityFilter = normalized;
            }

            return _routePlannerCommodityFilter;
        }

        private string EnsureRoutePlannerDistrictFilter()
        {
            var options = GetRoutePlannerDistrictOptions();
            if (options.Count == 0)
            {
                _routePlannerDistrictFilter = string.Empty;
                return _routePlannerDistrictFilter;
            }

            var normalized = (_routePlannerDistrictFilter ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !options.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _routePlannerDistrictFilter = string.Empty;
            }
            else
            {
                _routePlannerDistrictFilter = normalized;
            }

            return _routePlannerDistrictFilter;
        }

        private string EnsureSelectedRoutePlannerCandidateId()
        {
            return EnsureSelectedRoutePlannerCandidateId(ApplyRoutePlannerFiltersAndSort(BuildRoutePlannerCandidatesInternal()));
        }

        private string EnsureSelectedRoutePlannerCandidateId(IReadOnlyList<TabletRoutePlannerCandidate> candidates)
        {
            candidates = candidates ?? Array.Empty<TabletRoutePlannerCandidate>();
            if (candidates.Count == 0)
            {
                _selectedRoutePlannerCandidateId = string.Empty;
                return _selectedRoutePlannerCandidateId;
            }

            if (string.IsNullOrWhiteSpace(_selectedRoutePlannerCandidateId)
                || !candidates.Any(candidate => candidate != null && string.Equals(candidate.CandidateId, _selectedRoutePlannerCandidateId, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedRoutePlannerCandidateId = candidates[0].CandidateId;
            }

            return _selectedRoutePlannerCandidateId;
        }

        private static string CycleStringSelection(IReadOnlyList<string> options, string currentValue, int delta, Func<string, string> normalize)
        {
            if (options == null || options.Count == 0)
            {
                return string.Empty;
            }

            normalize = normalize ?? (value => value ?? string.Empty);
            currentValue = normalize(currentValue);
            var currentIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(normalize(options[i]), currentValue, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = currentIndex + direction;
            while (nextIndex < 0)
            {
                nextIndex += options.Count;
            }

            while (nextIndex >= options.Count)
            {
                nextIndex -= options.Count;
            }

            return normalize(options[nextIndex]);
        }

        private static IReadOnlyList<float> GetHistorySnapshot(
            IDictionary<string, TabletPersistentAnalyticsSeries> historyByKey,
            string key,
            TabletGraphTimeframe timeframe)
        {
            if (historyByKey == null || string.IsNullOrWhiteSpace(key))
            {
                return Array.Empty<float>();
            }

            TabletPersistentAnalyticsSeries buffer;
            return historyByKey.TryGetValue(key, out buffer) && buffer != null
                ? buffer.GetValues(timeframe)
                : Array.Empty<float>();
        }

        private static TabletPersistentAnalyticsSeries GetOrCreateHistoryBuffer(IDictionary<string, TabletPersistentAnalyticsSeries> historyByKey, string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
            TabletPersistentAnalyticsSeries buffer;
            if (!historyByKey.TryGetValue(key, out buffer) || buffer == null)
            {
                buffer = new TabletPersistentAnalyticsSeries();
                historyByKey[key] = buffer;
            }

            return buffer;
        }

        private static float GetStorageFillPercent(Industry industry)
        {
            if (industry == null)
            {
                return 0f;
            }

            var capacity = Math.Max(0.001f, industry.InputCapacityTons + industry.OutputCapacityTons);
            var storage = Math.Max(0f, industry.GetInputStockTotal() + industry.GetOutputStockTotal());
            return ModMath.Clamp01(storage / capacity) * 100f;
        }

        private void ClearAnalyticsHistories()
        {
            _profitHistory.Clear();
            _commodityPriceHistoryByCommodity.Clear();
            _siteUtilizationHistoryByIndustryId.Clear();
            _siteStorageHistoryByIndustryId.Clear();
        }

        private static void AppendNamedSeriesPersistence(
            ICollection<TabletNamedTimeSeriesPersistence> target,
            IDictionary<string, TabletPersistentAnalyticsSeries> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var pair in source.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                target.Add(new TabletNamedTimeSeriesPersistence
                {
                    Key = pair.Key,
                    Series = pair.Value.CreatePersistence(),
                });
            }
        }

        private static void RestoreNamedSeries(
            IDictionary<string, TabletPersistentAnalyticsSeries> target,
            IEnumerable<TabletNamedTimeSeriesPersistence> source)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var entry in source)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Series == null)
                {
                    continue;
                }

                var series = new TabletPersistentAnalyticsSeries();
                series.Restore(entry.Series);
                target[entry.Key.Trim()] = series;
            }
        }

        private bool CanReuseCachedLoadOptions(Industry industry, int cargoVehicleHandle, VehicleCargoType cargoType, float freeCapacityTons, int now)
        {
            return _hasCachedLoadOptions
                && ReferenceEquals(_cachedLoadIndustry, industry)
                && _cachedLoadVehicleHandle == cargoVehicleHandle
                && _cachedLoadCargoType == cargoType
                && Math.Abs(_cachedLoadFreeCapacityTons - freeCapacityTons) < 0.05f
                && now - _lastLoadOptionsRefreshMs < LoadOptionsRefreshIntervalMs;
        }

        private void BuildLoadOptionSubtitles(Industry industry, List<string> loadOptions, float truckFreeCapacityTons, Dictionary<string, string> subtitles)
        {
            if (subtitles == null)
            {
                return;
            }

            subtitles.Clear();
            if (industry == null || loadOptions == null || loadOptions.Count == 0)
            {
                return;
            }

            var maxLoadTons = Math.Max(0f, truckFreeCapacityTons);
            for (int i = 0; i < loadOptions.Count; i++)
            {
                var commodity = loadOptions[i];
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                var availableTons = Math.Max(0f, industry.GetStock(commodity));
                var loadableTons = Math.Min(availableTons, maxLoadTons);
                var unitPrice = Math.Max(0f, _globalMarket.GetUnitPrice(commodity));
                var cargoValue = loadableTons * unitPrice;

                subtitles[commodity.Trim()] = string.Format(
                    "Cargo value: ${0:0} ({1:0.0}t | ${2:0}/t)",
                    cargoValue,
                    loadableTons,
                    unitPrice);
            }
        }

        private void ClearLoadOptionsCache()
        {
            _cachedLoadIndustry = null;
            _cachedLoadVehicleHandle = 0;
            _cachedLoadCargoType = VehicleCargoType.Unknown;
            _cachedLoadFreeCapacityTons = -1f;
            _lastLoadOptionsRefreshMs = int.MinValue;
            _hasCachedLoadOptions = false;
            _cachedLoadOptions.Clear();
            _cachedLoadOptionSubtitles.Clear();
        }
    }
}
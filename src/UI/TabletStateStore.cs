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

        public int VisibleCompetitionCount { get; set; }

        public int CompetitiveWinCount { get; set; }

        public string CompetitionStatus { get; set; }
    }

    internal sealed class TabletNpcRoutePerformance
    {
        public NpcLogisticsContract Contract { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

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

        public string OverviewDetail { get; set; }

        public string ProductionWarning { get; set; }

        public string PrimaryConversion { get; set; }

        public string ModuleSummary { get; set; }

        public bool HasServiceBusinessInfo { get; set; }

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
                    VisibleCompetitionCount = Math.Max(0, district.VisibleCompetitionCount),
                    CompetitiveWinCount = Math.Max(0, district.CompetitiveWinCount),
                    CompetitionStatus = district.CompetitionStatus ?? string.Empty,
                })
                .ToArray();
        }

        public IReadOnlyList<TabletNpcRoutePerformance> GetNpcRoutePerformance()
        {
            if (_npcLogisticsManager == null || _npcLogisticsManager.Contracts == null)
            {
                return Array.Empty<TabletNpcRoutePerformance>();
            }

            return _npcLogisticsManager.Contracts
                .Where(contract => contract != null)
                .OrderByDescending(contract => contract.TotalDeliveredTons)
                .ThenByDescending(contract => contract.TotalProfitEarned)
                .Select(contract => new TabletNpcRoutePerformance
                {
                    Contract = contract,
                    Label = BuildRoutePerformanceLabel(contract),
                    Detail = contract.StatusText ?? string.Empty,
                    DeliveredTons = Math.Max(0f, contract.TotalDeliveredTons),
                    LossRatioPercent = Math.Max(0f, contract.LastJourneyLossRatio * 100f),
                    AveragePayout = contract.CompletedDeliveries > 0
                        ? Math.Max(0f, contract.TotalProfitEarned / contract.CompletedDeliveries)
                        : 0f,
                    CompletedDeliveries = Math.Max(0, contract.CompletedDeliveries),
                })
                .ToArray();
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
            return new TabletBudgetOverview
            {
                CurrentBalance = currentBalance,
                DailyNet = _financeTracker != null ? _financeTracker.GetNetAmount(currentMinute, InGameMinutesPerDay) : 0f,
                WeeklyNet = weeklyIncome - weeklyExpenses,
                WeeklyIncome = weeklyIncome,
                WeeklyExpenses = weeklyExpenses,
                UpcomingBills = upcomingBills.Sum(entry => entry.Amount),
                Forecast = BuildWeeklyForecast(currentMinute, currentBalance, upcomingBills),
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

        public TabletBudgetForecast GetWeeklyForecast()
        {
            var currentMinute = GetCurrentFinanceMinute();
            var currentBalance = _getProfit != null ? _getProfit() : 0f;
            return BuildWeeklyForecast(currentMinute, currentBalance, GetUpcomingBillsInternal(currentMinute));
        }

        public IReadOnlyList<TabletBudgetRouteEntry> GetBudgetRouteProfitability()
        {
            if (_npcLogisticsManager == null || _npcLogisticsManager.Contracts == null)
            {
                return Array.Empty<TabletBudgetRouteEntry>();
            }

            return _npcLogisticsManager.Contracts
                .Where(contract => contract != null)
                .Select(contract =>
                {
                    var revenue = Math.Max(0f, contract.TotalProfitEarned);
                    var operatingCost = Math.Max(0f, contract.ContractCost) + Math.Max(0f, contract.TotalWeeklyWagesPaid);
                    return new TabletBudgetRouteEntry
                    {
                        Label = BuildRoutePerformanceLabel(contract),
                        Detail = string.Format(
                            "{0} | {1}",
                            contract.Tier != null ? contract.Tier.DisplayName : "Route",
                            contract.StatusText ?? string.Empty).Trim(),
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

            if (_industryManager != null && _industryManager.Industries != null)
            {
                foreach (var industry in _industryManager.Industries.Where(ShouldIncludeIndustryInventory))
                {
                    float locationValue = 0f;
                    float totalTons = 0f;
                    foreach (var commodity in industry.BufferStorage.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                    {
                        var tons = Math.Max(0f, industry.GetStock(commodity));
                        if (tons <= 0.01f)
                        {
                            continue;
                        }

                        var storageMultiplier = industry.IsWarehouse ? Math.Max(0.55f, Math.Min(1f, industry.StorageCondition)) : 1f;
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
                    ProductionWarning = productionWarning,
                    OverviewDetail = BuildOverviewDetail(locationKind, industry, storage, fillRatio, productionWarning),
                    PrimaryConversion = industry.GetPrimaryConversionDescription(),
                    ModuleSummary = string.Format(
                        "Prod Lv.{0} | In Lv.{1} | Out Lv.{2} | Omega Lv.{3}",
                        industry.ProductionModuleLevel,
                        industry.InputStorageModuleLevel,
                        industry.OutputStorageModuleLevel,
                        industry.OmegaStorageModuleLevel),
                };

                PopulateOwnedServiceSiteBusinessSummary(summary, industry, fillRatio);
                summaries.Add(summary);
            }

            return summaries;
        }

        private void PopulateOwnedServiceSiteBusinessSummary(TabletLocationSummary summary, Industry industry, float fillRatio)
        {
            if (summary == null
                || industry == null
                || _territoryManager == null
                || !industry.IsOwned
                || (!industry.IsStore && !industry.IsGasStation))
            {
                return;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            summary.HasServiceBusinessInfo = true;
            summary.ServiceStaffAssigned = siteState != null && siteState.SiteOperatorAssigned;
            summary.ServiceStockReady = _territoryManager.IsServiceSinkStockedForPassiveIncome(industry);
            summary.ServiceOperational = _territoryManager.IsServiceSinkOperationalForPassiveIncome(industry);
            summary.ServiceWeeklyIncome = Math.Max(0f, industry.WeeklyPassiveIncome);
            summary.ServiceWeeklyStaffingCost = _territoryManager.GetServiceSiteWeeklyStaffingCost(industry);
            summary.ServiceLastPassiveIncome = siteState != null ? Math.Max(0f, siteState.LastPassiveIncomeAmount) : 0f;
            summary.ServiceStaffStatus = summary.ServiceStaffAssigned ? "Assigned" : "Missing";
            summary.ServiceStockStatus = summary.ServiceStockReady ? "Ready" : "Low stock";
            summary.ServiceOperationsStatus = summary.ServiceOperational ? "Operational" : "Inactive";
            summary.ServicePassiveIncomeStatus = siteState != null && !string.IsNullOrWhiteSpace(siteState.PassiveIncomeStatus)
                ? siteState.PassiveIncomeStatus
                : (summary.ServiceOperational ? "Passive income active" : "Inactive");
            summary.ServiceRecentPayoutStatus = BuildOwnedServiceSitePayoutStatus(siteState);
            summary.ServiceContractStatus = siteState != null ? siteState.ServiceContractStatus ?? string.Empty : string.Empty;
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

        private static string BuildOwnedServiceSitePayoutStatus(TerritorySiteState siteState)
        {
            if (siteState == null || siteState.LastPassiveIncomeWeekIndex < 0)
            {
                return "No completed weekly payout yet";
            }

            return !string.IsNullOrWhiteSpace(siteState.LastPassiveIncomeStatus)
                ? siteState.LastPassiveIncomeStatus
                : "No recent payout";
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

        private static string BuildOverviewDetail(ExternalLocationKind locationKind, Industry industry, float storage, float fillRatio, string productionWarning)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (industry.SiteRole == SiteRole.Warehouse)
            {
                return string.Format("Storage {0:0.0}t | {1:0}% full | Condition {2:0}%", storage, fillRatio * 100f, Math.Max(0f, Math.Min(100f, industry.StorageCondition * 100f)));
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
            if (contract == null)
            {
                return "NPC Route";
            }

            var originName = contract.OriginIndustry != null ? contract.OriginIndustry.Name : "Origin";
            var destinationName = contract.DestinationIndustry != null ? contract.DestinationIndustry.Name : "Destination";
            return string.Format("{0} -> {1}", originName, destinationName);
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
                foreach (var office in _propertyManager.Offices.Where(entry => entry != null))
                {
                    var state = _propertyManager.GetOfficeState(office.OfficeId);
                    if (state == null || (!state.IsOwned && !state.IsRented))
                    {
                        continue;
                    }

                    if (state.OutstandingRent > 0.01f)
                    {
                        bills.Add(new TabletUpcomingBillEntry
                        {
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

                    bills.Add(new TabletUpcomingBillEntry
                    {
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
                        bills.Add(new TabletUpcomingBillEntry
                        {
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

                    bills.Add(new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.ApartmentRent,
                        Label = apartment.DisplayName,
                        Detail = "Weekly apartment rent",
                        Amount = weeklyRent,
                        DueInMinutes = nextWeekDueInMinutes,
                    });
                }

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

        private bool ShouldIncludeIndustryInventory(Industry industry)
        {
            return industry != null && _industryManager.IsIndustryOwnedForGameplay(industry);
        }

        private static string BuildInventoryLocationDetail(Industry industry, float totalTons)
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

            return industry.IsWarehouse
                ? string.Format("{0} | {1:0.0}t on hand | Condition {2:0}%", locationLabel, totalTons, Math.Max(0f, Math.Min(100f, industry.StorageCondition * 100f)))
                : string.Format("{0} | {1:0.0}t on hand", locationLabel, totalTons);
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
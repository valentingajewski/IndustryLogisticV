using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class NpcLogisticsManager
    {
        private const int DefaultRouteLimit = 5;
        private const int MaxRouteLimit = 10;
        private const float ArrivalDistance = 50f;
        private const int SpawnStaggerDelayMs = 10000;
        private const int DriveTaskRefreshIntervalMs = 4000;
        private const int InGameMinutesPerDay = 24 * 60;
        private const int InGameMinutesPerWeek = 7 * InGameMinutesPerDay;
        private const int LoadDelayMs = 2200;
        private const int UnloadDelayMs = 2400;
        private const int RetryDelayMs = 9000;
        private const float BaseDriveSpeed = 20f;
        private const int DriveStyle = 786603;
        private const string DefaultNpcModel = "s_m_m_trucker_01";

        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly TerritoryManager _territoryManager;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<float> _getProfit;
        private readonly Action<float> _deductProfit;
        private readonly Action<float> _addProfit;
        private readonly Action<string> _showStatus;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Func<IReadOnlyList<OwnedCommercialVehiclePersistenceEntry>> _getCommercialVehicles;
        private readonly Func<string> _getActiveOfficeId;
        private readonly Func<OfficeDefinition> _getActiveOffice;
        private readonly List<NpcDriverTierDefinition> _driverTiers;
        private readonly List<NpcLogisticsContract> _contracts;
        private readonly Dictionary<string, int> _lastContractSpawnMsByOriginId;
        private readonly NpcWorldDispatchConfig _worldDispatchConfig;
        private readonly List<NpcWorldLogisticsJob> _worldJobs;
        private readonly Random _random;

        private int _nextContractId;
        private int _nextWorldJobId;
        private int _lastObservedClockMinute;
        private int _lastWorldEvaluationClockMinute;
        private int _worldEvaluationElapsedMinutes;
        private int _completedWorldDispatches;
        private NpcWeeklyWageDifficulty _weeklyWageDifficulty;
        private NpcWorldDispatchPolicy _worldDispatchPolicy;
        private string _worldPriorityCommodity;
        private string _worldPriorityDistrict;
        private bool _premiumDispatchEnabled;
        private bool _officeDeliveryNotificationsEnabled;
        private int _routeLimit;

        public NpcLogisticsManager(
            string configDirectory,
            IndustryManager industryManager,
            FleetManager fleetManager,
            GlobalMarketManager globalMarket,
            Func<Vector3, Vector3> getGroundPosition,
            Func<float> getProfit,
            Action<float> deductProfit,
            Action<float> addProfit,
            Action<string> showStatus,
            TerritoryManager territoryManager = null,
            Func<IReadOnlyList<OwnedCommercialVehiclePersistenceEntry>> getCommercialVehicles = null,
            Func<string> getActiveOfficeId = null,
            Func<OfficeDefinition> getActiveOffice = null,
            CompanyFinanceTracker financeTracker = null,
            Func<int> getCurrentInGameMinute = null)
        {
            _industryManager = industryManager;
            _fleetManager = fleetManager;
            _globalMarket = globalMarket;
            _territoryManager = territoryManager;
            _getGroundPosition = getGroundPosition;
            _getProfit = getProfit;
            _deductProfit = deductProfit;
            _addProfit = addProfit;
            _showStatus = showStatus;
            _financeTracker = financeTracker;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _getCommercialVehicles = getCommercialVehicles;
            _getActiveOfficeId = getActiveOfficeId;
            _getActiveOffice = getActiveOffice;
            _driverTiers = LoadDriverTiers(configDirectory);
            _contracts = new List<NpcLogisticsContract>();
            _lastContractSpawnMsByOriginId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _worldDispatchConfig = NpcWorldDispatchConfigLoader.Load(configDirectory);
            _worldJobs = new List<NpcWorldLogisticsJob>();
            _random = new Random();
            _nextContractId = 1;
            _nextWorldJobId = 1;
            _lastObservedClockMinute = -1;
            _lastWorldEvaluationClockMinute = -1;
            _worldEvaluationElapsedMinutes = _worldDispatchConfig != null ? _worldDispatchConfig.EvaluationIntervalMinutes : 0;
            _completedWorldDispatches = 0;
            _weeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard;
            _worldDispatchPolicy = NpcWorldDispatchPolicy.Balanced;
            _worldPriorityCommodity = string.Empty;
            _worldPriorityDistrict = string.Empty;
            _premiumDispatchEnabled = false;
            _officeDeliveryNotificationsEnabled = true;
            _routeLimit = DefaultRouteLimit;
        }

        public IReadOnlyList<NpcDriverTierDefinition> DriverTiers
        {
            get { return _driverTiers; }
        }

        public IReadOnlyList<NpcLogisticsContract> Contracts
        {
            get { return _contracts; }
        }

        public NpcWeeklyWageDifficulty WeeklyWageDifficulty
        {
            get { return _weeklyWageDifficulty; }
        }

        public NpcWorldDispatchPolicy WorldDispatchPolicy
        {
            get { return _worldDispatchPolicy; }
        }

        public int RouteLimit
        {
            get { return _routeLimit; }
        }

        public string WorldPriorityCommodity
        {
            get { return _worldPriorityCommodity; }
        }

        public string WorldPriorityDistrict
        {
            get { return _worldPriorityDistrict; }
        }

        public bool PremiumDispatchEnabled
        {
            get { return _premiumDispatchEnabled; }
        }

        public bool OfficeDeliveryNotificationsEnabled
        {
            get { return _officeDeliveryNotificationsEnabled; }
        }

        public void SetRouteLimit(int routeLimit)
        {
            _routeLimit = Math.Max(0, Math.Min(MaxRouteLimit, routeLimit));
        }

        public void SetOfficeDeliveryNotificationsEnabled(bool enabled)
        {
            _officeDeliveryNotificationsEnabled = enabled;
        }

        public IReadOnlyList<OwnedCommercialVehiclePersistenceEntry> GetAssignableGarageVehicles(string commodity, string includeAssignedAssetId = null)
        {
            return GetAssignableGarageVehicles(
                commodity,
                string.IsNullOrWhiteSpace(includeAssignedAssetId) ? null : new[] { includeAssignedAssetId },
                null);
        }

        public IReadOnlyList<OwnedCommercialVehiclePersistenceEntry> GetAssignableGarageVehicles(
            string commodity,
            IEnumerable<string> includeAssignedAssetIds,
            NpcLogisticsContract includeContract)
        {
            var allVehicles = _getCommercialVehicles != null ? _getCommercialVehicles() : null;
            if (allVehicles == null || allVehicles.Count == 0)
            {
                return Array.Empty<OwnedCommercialVehiclePersistenceEntry>();
            }

            var activeOfficeId = _getActiveOfficeId != null ? _getActiveOfficeId() ?? string.Empty : string.Empty;
            var includedAssetIds = BuildAssignedVehicleAssetIdSet(includeAssignedAssetIds);
            var vehicles = new List<OwnedCommercialVehiclePersistenceEntry>();
            for (int i = 0; i < allVehicles.Count; i++)
            {
                var entry = allVehicles[i];
                if (!ShouldIncludeAssignableVehicle(entry, activeOfficeId, commodity, includedAssetIds, includeContract))
                {
                    continue;
                }

                vehicles.Add(entry);
            }

            return vehicles
                .OrderBy(entry => entry.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<NpcWorldJobSummary> WorldJobs
        {
            get
            {
                return _worldJobs
                    .Select(BuildWorldJobSummary)
                    .ToArray();
            }
        }

        public NpcWorldDispatchOverview GetWorldDispatchOverview()
        {
            return new NpcWorldDispatchOverview
            {
                Enabled = _worldDispatchConfig != null && _worldDispatchConfig.Enabled,
                ActiveJobCount = _worldJobs.Count(job => job != null && (job.Phase == NpcWorldJobPhase.Listed || job.Phase == NpcWorldJobPhase.Traveling)),
                ListedOpportunityCount = _worldJobs.Count(job => job != null && job.Phase == NpcWorldJobPhase.Listed),
                RivalJobCount = _worldJobs.Count(job => job != null && job.IsRivalJob),
                VisibleConvoyCount = _worldJobs.Count(job => job != null && job.HasVisibleConvoy),
                CompletedDispatchCount = Math.Max(0, _completedWorldDispatches),
                DispatchPolicy = _worldDispatchPolicy,
                PriorityCommodity = _worldPriorityCommodity ?? string.Empty,
                PriorityDistrict = _worldPriorityDistrict ?? string.Empty,
                PremiumDispatchEnabled = _premiumDispatchEnabled,
                DispatchHeadline = BuildWorldDispatchHeadline(),
                DispatchDetail = BuildWorldDispatchDetail(),
            };
        }

        public IReadOnlyList<string> GetWorldDispatchCommodityOptions()
        {
            var items = new List<string> { string.Empty };
            items.AddRange(CommodityCatalog.GetKnownCommodities());
            return items.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public IReadOnlyList<string> GetWorldDispatchDistrictOptions()
        {
            var items = new List<string> { string.Empty };
            if (_industryManager != null && _industryManager.Industries != null)
            {
                items.AddRange(_industryManager.Industries
                    .Where(industry => industry != null && !string.IsNullOrWhiteSpace(industry.DistrictName))
                    .Select(industry => industry.DistrictName));
            }

            return items
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void CycleWorldDispatchPolicy(int delta)
        {
            var values = (NpcWorldDispatchPolicy[])Enum.GetValues(typeof(NpcWorldDispatchPolicy));
            if (values.Length == 0)
            {
                return;
            }

            var currentIndex = Array.IndexOf(values, _worldDispatchPolicy);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + delta) % values.Length;
            if (nextIndex < 0)
            {
                nextIndex += values.Length;
            }

            _worldDispatchPolicy = values[nextIndex];
        }

        public void CycleWorldPriorityCommodity(int delta)
        {
            _worldPriorityCommodity = CycleStringSelection(GetWorldDispatchCommodityOptions(), _worldPriorityCommodity, delta, CommodityCatalog.Normalize);
        }

        public void CycleWorldPriorityDistrict(int delta)
        {
            _worldPriorityDistrict = CycleStringSelection(GetWorldDispatchDistrictOptions(), _worldPriorityDistrict, delta, value => (value ?? string.Empty).Trim());
        }

        public void TogglePremiumDispatch()
        {
            _premiumDispatchEnabled = !_premiumDispatchEnabled;
        }

        public List<Industry> GetOriginIndustryOptions()
        {
            return _industryManager.Industries
                .Where(CanUseAsOrigin)
                .OrderBy(industry => industry.Name)
                .ToList();
        }

        public List<Industry> GetDestinationIndustryOptions(Industry originIndustry)
        {
            if (originIndustry == null)
            {
                return new List<Industry>();
            }

            return _industryManager.Industries
                .Where(industry => CanUseAsDestination(originIndustry, industry))
                .OrderBy(industry => industry.Name)
                .ToList();
        }

        public string BuildOriginAvailabilityDetail()
        {
            var candidates = GetOriginAvailabilityCandidates();
            if (candidates.Count == 0)
            {
                return "No industry with available outputs is configured.";
            }

            var blocked = candidates
                .Select(industry => BuildOriginAvailabilityMessage(industry))
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(3)
                .ToList();

            return blocked.Count == 0
                ? "No industry with available outputs is currently accessible."
                : string.Format("Blocked origins: {0}", string.Join(" | ", blocked));
        }

        public string BuildDestinationAvailabilityDetail(Industry originIndustry)
        {
            if (originIndustry == null)
            {
                return "No compatible destination is available because no starting point is currently accessible.";
            }

            var originBlocker = BuildAutomationBlockReason(originIndustry);
            if (!string.IsNullOrWhiteSpace(originBlocker))
            {
                return string.Format("{0} is not ready for automation: {1}.", originIndustry.Name, originBlocker.TrimEnd('.'));
            }

            var candidates = GetDestinationAvailabilityCandidates(originIndustry);
            if (candidates.Count == 0)
            {
                return string.Format("No compatible destination accepts outputs from {0}.", originIndustry.Name);
            }

            var blocked = candidates
                .Select(destinationIndustry => BuildDestinationAvailabilityMessage(originIndustry, destinationIndustry))
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(3)
                .ToList();

            return blocked.Count == 0
                ? "No compatible destination is currently accessible."
                : string.Format("Blocked destinations: {0}", string.Join(" | ", blocked));
        }

        public List<string> GetResourceOptions(Industry originIndustry, Industry destinationIndustry)
        {
            if (originIndustry == null)
            {
                return new List<string>();
            }

            var resources = originIndustry
                .GetSortedOutputs()
                .Where(resource => !string.IsNullOrWhiteSpace(resource));

            if (destinationIndustry != null)
            {
                resources = resources.Where(destinationIndustry.AcceptsCommodity);
            }

            return resources
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource)
                .ToList();
        }

        public float GetContractCost(string commodity, NpcDriverTierDefinition tier)
        {
            if (tier == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            return Math.Max(0f, tier.PriceMultiplier * _globalMarket.GetUnitPrice(normalizedCommodity));
        }

        public void SetWeeklyWageDifficulty(NpcWeeklyWageDifficulty difficulty)
        {
            _weeklyWageDifficulty = difficulty;
        }

        public float GetWeeklyWage(NpcDriverTierDefinition tier)
        {
            return tier != null ? tier.GetWeeklyWage(_weeklyWageDifficulty) : 0f;
        }

        public int GetRemainingPayrollMinutes(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return InGameMinutesPerWeek;
            }

            return Math.Max(0, InGameMinutesPerWeek - Math.Max(0, contract.PayrollElapsedInGameMinutes));
        }

        public string BuildPayrollStatus(NpcLogisticsContract contract)
        {
            if (contract == null || contract.Tier == null)
            {
                return "Payroll unavailable";
            }

            return string.Format(
                "Weekly {0} | {1}",
                ModFormatting.FormatMoney(GetWeeklyWage(contract.Tier)),
                FormatPayrollCountdown(GetRemainingPayrollMinutes(contract)));
        }

        public bool TryCreateContract(
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            NpcDriverTierDefinition tier,
            OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            int originTriggerThresholdPercent,
            int destinationTriggerThresholdPercent,
            out string message)
        {
            return TryUpsertContract(
                null,
                new[]
                {
                    new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = originIndustry,
                        DestinationIndustry = destinationIndustry,
                        Commodity = commodity,
                        AssignedVehicleAssetId = assignedVehicle != null ? assignedVehicle.AssetId : string.Empty,
                        AssignedVehicleDisplayName = assignedVehicle != null ? BuildAssignedVehicleDisplayName(assignedVehicle) : string.Empty,
                        OriginTriggerThresholdPercent = originTriggerThresholdPercent,
                        DestinationTriggerThresholdPercent = destinationTriggerThresholdPercent,
                    },
                },
                tier,
                out message);
        }

        public bool TryCreateContract(
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            NpcDriverTierDefinition tier,
            out string message)
        {
            return TryUpsertContract(null, routes, tier, out message);
        }

        public bool TryCreateContract(
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            NpcDriverTierDefinition tier,
            OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            out string message)
        {
            return TryUpsertContract(null, ApplyFallbackAssignedVehicle(routes, assignedVehicle), tier, out message);
        }

        public bool TryModifyContract(
            NpcLogisticsContract contract,
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            NpcDriverTierDefinition tier,
            OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            int originTriggerThresholdPercent,
            int destinationTriggerThresholdPercent,
            out string message)
        {
            return TryUpsertContract(
                contract,
                new[]
                {
                    new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = originIndustry,
                        DestinationIndustry = destinationIndustry,
                        Commodity = commodity,
                        AssignedVehicleAssetId = assignedVehicle != null ? assignedVehicle.AssetId : string.Empty,
                        AssignedVehicleDisplayName = assignedVehicle != null ? BuildAssignedVehicleDisplayName(assignedVehicle) : string.Empty,
                        OriginTriggerThresholdPercent = originTriggerThresholdPercent,
                        DestinationTriggerThresholdPercent = destinationTriggerThresholdPercent,
                    },
                },
                tier,
                out message);
        }

        public bool TryModifyContract(
            NpcLogisticsContract contract,
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            NpcDriverTierDefinition tier,
            out string message)
        {
            return TryUpsertContract(contract, routes, tier, out message);
        }

        public bool TryModifyContract(
            NpcLogisticsContract contract,
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            NpcDriverTierDefinition tier,
            OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            out string message)
        {
            return TryUpsertContract(contract, ApplyFallbackAssignedVehicle(routes, assignedVehicle), tier, out message);
        }

        public bool TryFireContract(NpcLogisticsContract contract, out string message)
        {
            if (contract == null || !_contracts.Contains(contract))
            {
                message = "Selected NPC route was not found.";
                return false;
            }

            CleanupContractEntities(contract);
            _contracts.Remove(contract);
            message = string.Format("Fired NPC on route {0}.", BuildContractLabel(contract));
            return true;
        }

        public void Update(int now, int currentClockMinute)
        {
            var elapsedPayrollMinutes = GetElapsedPayrollMinutes(currentClockMinute);
            for (int i = 0; i < _contracts.Count; i++)
            {
                if (elapsedPayrollMinutes > 0)
                {
                    UpdatePayroll(_contracts[i], elapsedPayrollMinutes);
                }

                UpdateContract(_contracts[i], now);
            }

            UpdateWorldDispatch(now, currentClockMinute, elapsedPayrollMinutes);
        }

        public void ClearAll()
        {
            for (int i = 0; i < _contracts.Count; i++)
            {
                CleanupContractEntities(_contracts[i]);
            }

            for (int i = 0; i < _worldJobs.Count; i++)
            {
                CleanupWorldJobVisual(_worldJobs[i]);
            }

            _contracts.Clear();
            _worldJobs.Clear();
            _lastContractSpawnMsByOriginId.Clear();
            _nextContractId = 1;
            _nextWorldJobId = 1;
            _lastObservedClockMinute = -1;
            _lastWorldEvaluationClockMinute = -1;
            _worldEvaluationElapsedMinutes = _worldDispatchConfig != null ? _worldDispatchConfig.EvaluationIntervalMinutes : 0;
            _completedWorldDispatches = 0;
            _officeDeliveryNotificationsEnabled = true;
        }

        public NpcLogisticsPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new NpcLogisticsPersistenceSnapshot();
            for (int i = 0; i < _contracts.Count; i++)
            {
                var contract = _contracts[i];
                var primaryRoute = contract != null && contract.Routes.Count > 0
                    ? contract.Routes[0]
                    : null;
                if (contract == null || primaryRoute == null || primaryRoute.OriginIndustry == null || primaryRoute.DestinationIndustry == null || contract.Tier == null)
                {
                    continue;
                }

                var contractSnapshot = new NpcLogisticsContractSnapshot
                {
                    Id = contract.Id,
                    OriginIndustryId = primaryRoute.OriginIndustry.Id,
                    DestinationIndustryId = primaryRoute.DestinationIndustry.Id,
                    Commodity = primaryRoute.Commodity,
                    TierId = contract.Tier.Id,
                    AssignedVehicleAssetId = contract.AssignedVehicleAssetId,
                    AssignedVehicleDisplayName = contract.AssignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = primaryRoute.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = primaryRoute.DestinationTriggerThresholdPercent,
                    CurrentRouteIndex = contract.CurrentRouteIndex,
                    ContractCost = contract.ContractCost,
                    PayrollElapsedInGameMinutes = contract.PayrollElapsedInGameMinutes,
                    CompletedPayrollCycles = contract.CompletedPayrollCycles,
                    TotalWeeklyWagesPaid = contract.TotalWeeklyWagesPaid,
                    CompletedDeliveries = contract.CompletedDeliveries,
                    TotalDeliveredTons = contract.TotalDeliveredTons,
                    TotalProfitEarned = contract.TotalProfitEarned,
                    LastJourneyLossRatio = contract.LastJourneyLossRatio,
                };

                for (int routeIndex = 0; routeIndex < contract.Routes.Count; routeIndex++)
                {
                    var route = contract.Routes[routeIndex];
                    if (route == null || route.OriginIndustry == null || route.DestinationIndustry == null || string.IsNullOrWhiteSpace(route.Commodity))
                    {
                        continue;
                    }

                    contractSnapshot.Routes.Add(new NpcLogisticsRouteSnapshot
                    {
                        OriginIndustryId = route.OriginIndustry.Id,
                        DestinationIndustryId = route.DestinationIndustry.Id,
                        Commodity = route.Commodity,
                        AssignedVehicleAssetId = route.AssignedVehicleAssetId,
                        AssignedVehicleDisplayName = route.AssignedVehicleDisplayName,
                        OriginTriggerThresholdPercent = route.OriginTriggerThresholdPercent,
                        DestinationTriggerThresholdPercent = route.DestinationTriggerThresholdPercent,
                    });
                }

                snapshot.Contracts.Add(contractSnapshot);
            }

            snapshot.DispatchPolicy = _worldDispatchPolicy;
            snapshot.PriorityCommodity = _worldPriorityCommodity ?? string.Empty;
            snapshot.PriorityDistrict = _worldPriorityDistrict ?? string.Empty;
            snapshot.PremiumDispatchEnabled = _premiumDispatchEnabled;
            snapshot.OfficeDeliveryNotificationsEnabled = _officeDeliveryNotificationsEnabled;
            snapshot.LastWorldEvaluationClockMinute = _lastWorldEvaluationClockMinute;
            snapshot.CompletedWorldDispatches = _completedWorldDispatches;

            for (int i = 0; i < _worldJobs.Count; i++)
            {
                var job = _worldJobs[i];
                if (job == null)
                {
                    continue;
                }

                snapshot.WorldJobs.Add(new NpcWorldLogisticsJobSnapshot
                {
                    Id = job.Id,
                    Type = job.Type,
                    Phase = job.Phase,
                    Commodity = job.Commodity,
                    SourceLabel = job.SourceLabel,
                    DestinationLabel = job.DestinationLabel,
                    OriginIndustryId = job.OriginIndustryId,
                    DestinationIndustryId = job.DestinationIndustryId,
                    Tons = job.Tons,
                    RemainingInGameMinutes = job.RemainingInGameMinutes,
                    TotalInGameMinutes = job.TotalInGameMinutes,
                    CreatedClockMinute = job.CreatedClockMinute,
                    IsSpotOpportunity = job.IsSpotOpportunity,
                    UsesPremiumDispatch = job.UsesPremiumDispatch,
                    IsPriorityMatch = job.IsPriorityMatch,
                    HasVisibleConvoy = job.HasVisibleConvoy,
                    IsRivalJob = job.IsRivalJob,
                    BackhaulDepth = job.BackhaulDepth,
                    StatusText = job.StatusText,
                });
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(NpcLogisticsPersistenceSnapshot snapshot)
        {
            ClearAll();

            if (snapshot == null)
            {
                return;
            }

            _worldDispatchPolicy = snapshot.DispatchPolicy;
            _worldPriorityCommodity = CommodityCatalog.Normalize(snapshot.PriorityCommodity);
            _worldPriorityDistrict = snapshot.PriorityDistrict ?? string.Empty;
            _premiumDispatchEnabled = snapshot.PremiumDispatchEnabled;
            _officeDeliveryNotificationsEnabled = snapshot.OfficeDeliveryNotificationsEnabled;
            _lastWorldEvaluationClockMinute = snapshot.LastWorldEvaluationClockMinute;
            _worldEvaluationElapsedMinutes = 0;
            _completedWorldDispatches = Math.Max(0, snapshot.CompletedWorldDispatches);

            if (snapshot.Contracts == null || snapshot.Contracts.Count == 0)
            {
                return;
            }

            var nextContractId = 1;
            for (int i = 0; i < snapshot.Contracts.Count; i++)
            {
                var entry = snapshot.Contracts[i];
                if (entry == null)
                {
                    continue;
                }

                var originIndustry = FindIndustryById(entry.OriginIndustryId);
                var destinationIndustry = FindIndustryById(entry.DestinationIndustryId);
                var tier = FindDriverTier(entry.TierId);
                if (originIndustry == null || destinationIndustry == null || tier == null)
                {
                    continue;
                }

                var normalizedCommodity = CommodityCatalog.Normalize(entry.Commodity);
                var routeDefinitions = new List<NpcLogisticsRouteDefinition>();
                if (entry.Routes != null && entry.Routes.Count > 0)
                {
                    for (int routeIndex = 0; routeIndex < entry.Routes.Count; routeIndex++)
                    {
                        var routeEntry = entry.Routes[routeIndex];
                        if (routeEntry == null)
                        {
                            continue;
                        }

                        var routeOrigin = FindIndustryById(routeEntry.OriginIndustryId);
                        var routeDestination = FindIndustryById(routeEntry.DestinationIndustryId);
                        var routeCommodity = CommodityCatalog.Normalize(routeEntry.Commodity);
                        if (routeOrigin == null || routeDestination == null || string.IsNullOrWhiteSpace(routeCommodity))
                        {
                            continue;
                        }

                        routeDefinitions.Add(new NpcLogisticsRouteDefinition
                        {
                            OriginIndustry = routeOrigin,
                            DestinationIndustry = routeDestination,
                            Commodity = routeCommodity,
                            AssignedVehicleAssetId = string.IsNullOrWhiteSpace(routeEntry.AssignedVehicleAssetId) ? entry.AssignedVehicleAssetId : routeEntry.AssignedVehicleAssetId,
                            AssignedVehicleDisplayName = string.IsNullOrWhiteSpace(routeEntry.AssignedVehicleDisplayName) ? entry.AssignedVehicleDisplayName : routeEntry.AssignedVehicleDisplayName,
                            OriginTriggerThresholdPercent = ClampTriggerPercent(routeEntry.OriginTriggerThresholdPercent, 0),
                            DestinationTriggerThresholdPercent = ClampTriggerPercent(routeEntry.DestinationTriggerThresholdPercent, 100),
                        });
                    }
                }

                if (routeDefinitions.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(normalizedCommodity))
                    {
                        continue;
                    }

                    routeDefinitions.Add(new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = originIndustry,
                        DestinationIndustry = destinationIndustry,
                        Commodity = normalizedCommodity,
                        AssignedVehicleAssetId = entry.AssignedVehicleAssetId,
                        AssignedVehicleDisplayName = entry.AssignedVehicleDisplayName,
                        OriginTriggerThresholdPercent = ClampTriggerPercent(entry.OriginTriggerThresholdPercent, 0),
                        DestinationTriggerThresholdPercent = ClampTriggerPercent(entry.DestinationTriggerThresholdPercent, 100),
                    });
                }

                VehicleDefinition selectedVehicle;
                VehicleDefinition selectedTractor;
                OwnedCommercialVehiclePersistenceEntry assignedVehicle;
                var currentRoute = routeDefinitions[Math.Max(0, Math.Min(routeDefinitions.Count - 1, entry.CurrentRouteIndex))];
                var assignedVehicleAssetId = !string.IsNullOrWhiteSpace(currentRoute.AssignedVehicleAssetId)
                    ? currentRoute.AssignedVehicleAssetId
                    : (entry.AssignedVehicleAssetId ?? string.Empty);
                var assignedVehicleDisplayName = !string.IsNullOrWhiteSpace(currentRoute.AssignedVehicleDisplayName)
                    ? currentRoute.AssignedVehicleDisplayName
                    : (entry.AssignedVehicleDisplayName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(assignedVehicleAssetId))
                {
                    assignedVehicle = null;
                    if (!TryResolveVehicleForCommodity(currentRoute.Commodity, out selectedVehicle, out selectedTractor))
                    {
                        continue;
                    }

                    assignedVehicleDisplayName = string.IsNullOrWhiteSpace(assignedVehicleDisplayName)
                        ? "Legacy auto-select"
                        : assignedVehicleDisplayName;
                }
                else if (!TryResolveAssignedVehicleForCommodity(assignedVehicleAssetId, currentRoute.Commodity, out assignedVehicle, out selectedVehicle, out selectedTractor))
                {
                    continue;
                }

                var contract = new NpcLogisticsContract(Math.Max(1, entry.Id))
                {
                    OriginIndustry = currentRoute.OriginIndustry,
                    DestinationIndustry = currentRoute.DestinationIndustry,
                    Commodity = currentRoute.Commodity,
                    Tier = tier,
                    VehicleDefinition = selectedVehicle,
                    TractorDefinition = selectedTractor,
                    AssignedVehicleAssetId = assignedVehicle != null ? assignedVehicle.AssetId : assignedVehicleAssetId,
                    AssignedVehicleDisplayName = assignedVehicle != null ? BuildAssignedVehicleDisplayName(assignedVehicle) : assignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = currentRoute.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = currentRoute.DestinationTriggerThresholdPercent,
                    CurrentRouteIndex = Math.Max(0, Math.Min(routeDefinitions.Count - 1, entry.CurrentRouteIndex)),
                    ContractCost = Math.Max(0f, entry.ContractCost),
                    PayrollElapsedInGameMinutes = Math.Max(0, entry.PayrollElapsedInGameMinutes),
                    CompletedPayrollCycles = Math.Max(0, entry.CompletedPayrollCycles),
                    TotalWeeklyWagesPaid = Math.Max(0f, entry.TotalWeeklyWagesPaid),
                    CompletedDeliveries = Math.Max(0, entry.CompletedDeliveries),
                    TotalDeliveredTons = Math.Max(0f, entry.TotalDeliveredTons),
                    TotalProfitEarned = Math.Max(0f, entry.TotalProfitEarned),
                    LastJourneyLossRatio = Math.Max(0f, entry.LastJourneyLossRatio),
                    StatusText = "Preparing route",
                };

                SetContractRoutes(contract, routeDefinitions, contract.CurrentRouteIndex);
                UpdateContractAssignedVehicleSummary(contract);

                _contracts.Add(contract);
                nextContractId = Math.Max(nextContractId, contract.Id + 1);
            }

            _nextContractId = nextContractId;
            _lastObservedClockMinute = -1;

            var nextWorldJobId = 1;
            if (snapshot.WorldJobs != null)
            {
                for (int i = 0; i < snapshot.WorldJobs.Count; i++)
                {
                    var entry = snapshot.WorldJobs[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var job = new NpcWorldLogisticsJob
                    {
                        Id = Math.Max(1, entry.Id),
                        Type = entry.Type,
                        Phase = entry.Phase,
                        Commodity = CommodityCatalog.Normalize(entry.Commodity),
                        SourceLabel = entry.SourceLabel ?? string.Empty,
                        DestinationLabel = entry.DestinationLabel ?? string.Empty,
                        OriginIndustryId = entry.OriginIndustryId ?? string.Empty,
                        DestinationIndustryId = entry.DestinationIndustryId ?? string.Empty,
                        Tons = Math.Max(0f, entry.Tons),
                        RemainingInGameMinutes = Math.Max(0, entry.RemainingInGameMinutes),
                        TotalInGameMinutes = Math.Max(0, entry.TotalInGameMinutes),
                        CreatedClockMinute = Math.Max(0, entry.CreatedClockMinute),
                        IsSpotOpportunity = entry.IsSpotOpportunity,
                        UsesPremiumDispatch = entry.UsesPremiumDispatch,
                        IsPriorityMatch = entry.IsPriorityMatch,
                        HasVisibleConvoy = entry.HasVisibleConvoy,
                        IsRivalJob = entry.IsRivalJob,
                        BackhaulDepth = Math.Max(0, entry.BackhaulDepth),
                        StatusText = entry.StatusText ?? string.Empty,
                    };

                    _worldJobs.Add(job);
                    nextWorldJobId = Math.Max(nextWorldJobId, job.Id + 1);
                }
            }

            _nextWorldJobId = nextWorldJobId;
        }

        private bool TryUpsertContract(
            NpcLogisticsContract contract,
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            NpcDriverTierDefinition tier,
            out string message)
        {
            message = string.Empty;
            var isNewContract = contract == null;

            List<NpcLogisticsRouteDefinition> normalizedRoutes;
            if (!TryNormalizeRouteDefinitions(routes, out normalizedRoutes, out message))
            {
                return false;
            }

            if (tier == null)
            {
                message = "Select an NPC tier first.";
                return false;
            }

            var currentContractAssignedAssetIds = GetContractAssignedVehicleAssetIds(contract);
            for (int routeIndex = 0; routeIndex < normalizedRoutes.Count; routeIndex++)
            {
                OwnedCommercialVehiclePersistenceEntry routeAssignedVehicle;
                string assignedVehicleMessage;
                if (!TryResolveRouteAssignedVehicle(normalizedRoutes[routeIndex].AssignedVehicleAssetId, currentContractAssignedAssetIds, contract, out routeAssignedVehicle, out assignedVehicleMessage))
                {
                    message = string.Format("Route {0}: {1}", routeIndex + 1, assignedVehicleMessage);
                    return false;
                }

                OwnedCommercialVehiclePersistenceEntry resolvedAssignedVehicle;
                VehicleDefinition routeVehicle;
                VehicleDefinition routeTractor;
                string routeMessage;
                if (!TryResolveAssignedVehicleForCommodity(routeAssignedVehicle.AssetId, normalizedRoutes[routeIndex].Commodity, out resolvedAssignedVehicle, out routeVehicle, out routeTractor, out routeMessage))
                {
                    message = string.Format("Route {0}: {1}", routeIndex + 1, routeMessage);
                    return false;
                }

                normalizedRoutes[routeIndex].AssignedVehicleAssetId = resolvedAssignedVehicle.AssetId;
                normalizedRoutes[routeIndex].AssignedVehicleDisplayName = BuildAssignedVehicleDisplayName(resolvedAssignedVehicle);
            }

            var totalCost = normalizedRoutes.Sum(route => GetContractCost(route.Commodity, tier));
            var currentCost = contract != null ? contract.ContractCost : 0f;
            var additionalCost = Math.Max(0f, totalCost - currentCost);
            if (additionalCost > 0f && _getProfit != null && _getProfit() + 0.001f < additionalCost)
            {
                message = string.Format("Not enough profit. Need {0} more.", ModFormatting.FormatMoney(additionalCost - _getProfit()));
                return false;
            }

            if (additionalCost > 0f && _deductProfit != null)
            {
                _deductProfit(additionalCost);
                RecordFinanceExpense(
                    CompanyFinanceCategory.OtherExpense,
                    additionalCost,
                    string.Format("NPC route contract fee for {0} route{1}", normalizedRoutes.Count, normalizedRoutes.Count == 1 ? string.Empty : "s"),
                    contract != null ? contract.Id : 0,
                    contract != null ? BuildContractLabel(contract) : string.Empty);
            }

            if (isNewContract)
            {
                contract = new NpcLogisticsContract(_nextContractId++);
                _contracts.Add(contract);
                contract.PayrollElapsedInGameMinutes = 0;
                contract.CompletedPayrollCycles = 0;
                contract.TotalWeeklyWagesPaid = 0f;
            }
            else
            {
                CleanupContractEntities(contract);
            }

            contract.Tier = tier;
            contract.CurrentRouteIndex = 0;
            SetContractRoutes(contract, normalizedRoutes, contract.CurrentRouteIndex);
            UpdateContractAssignedVehicleSummary(contract);
            contract.ContractCost = totalCost;
            contract.StatusText = "Preparing route";
            contract.Phase = NpcRoutePhase.PendingSpawn;
            contract.WaitUntilMs = 0;
            contract.NextDriveTaskRefreshMs = 0;
            contract.LastJourneyLossRatio = 0f;

            message = isNewContract
                ? string.Format("Hired {0} NPC with {1} route{2}.", tier.DisplayName, normalizedRoutes.Count, normalizedRoutes.Count == 1 ? string.Empty : "s")
                : string.Format("Updated NPC route chain to {0} route{1}.", normalizedRoutes.Count, normalizedRoutes.Count == 1 ? string.Empty : "s");
            return true;
        }

        private void UpdateWorldDispatch(int now, int currentClockMinute, int elapsedClockMinutes)
        {
            if (_worldDispatchConfig == null || !_worldDispatchConfig.Enabled)
            {
                return;
            }

            if (_lastWorldEvaluationClockMinute < 0)
            {
                _lastWorldEvaluationClockMinute = Math.Max(0, currentClockMinute);
            }

            if (elapsedClockMinutes > 0)
            {
                _worldEvaluationElapsedMinutes += elapsedClockMinutes;
            }

            UpdateWorldJobProgress(now, currentClockMinute, Math.Max(0, elapsedClockMinutes));

            if (_worldEvaluationElapsedMinutes >= _worldDispatchConfig.EvaluationIntervalMinutes)
            {
                _worldEvaluationElapsedMinutes = 0;
                _lastWorldEvaluationClockMinute = Math.Max(0, currentClockMinute);
                EvaluateWorldDispatch(now, currentClockMinute);
            }

            CleanupInactiveWorldJobs();
        }

        private void UpdateWorldJobProgress(int now, int currentClockMinute, int elapsedClockMinutes)
        {
            for (int i = 0; i < _worldJobs.Count; i++)
            {
                var job = _worldJobs[i];
                if (job == null)
                {
                    continue;
                }

                switch (job.Phase)
                {
                    case NpcWorldJobPhase.Listed:
                        if (elapsedClockMinutes > 0)
                        {
                            job.RemainingInGameMinutes = Math.Max(0, job.RemainingInGameMinutes - elapsedClockMinutes);
                        }

                        if (job.RemainingInGameMinutes <= 0)
                        {
                            StartWorldJob(job, now, currentClockMinute);
                        }

                        break;

                    case NpcWorldJobPhase.Traveling:
                        if (elapsedClockMinutes > 0)
                        {
                            job.RemainingInGameMinutes = Math.Max(0, job.RemainingInGameMinutes - elapsedClockMinutes);
                        }

                        RefreshWorldJobVisual(job, now);
                        if (job.RemainingInGameMinutes <= 0)
                        {
                            CompleteWorldJob(job, now, currentClockMinute);
                        }

                        break;
                }
            }
        }

        private void EvaluateWorldDispatch(int now, int currentClockMinute)
        {
            var activeJobs = _worldJobs.Count(job => job != null && (job.Phase == NpcWorldJobPhase.Listed || job.Phase == NpcWorldJobPhase.Traveling));
            var availableSlots = Math.Max(0, _worldDispatchConfig.MaxActiveJobs - activeJobs);
            if (availableSlots <= 0)
            {
                return;
            }

            var candidates = new List<NpcWorldJobCandidate>();
            BuildOverflowCandidates(candidates);
            BuildShortageCandidates(candidates);
            BuildRivalCandidates(candidates);

            if (candidates.Count == 0)
            {
                return;
            }

            var queuedCount = 0;
            var targetQueueCount = Math.Min(availableSlots, 2);
            foreach (var candidate in candidates.OrderByDescending(entry => entry != null ? entry.Score : 0f))
            {
                if (queuedCount >= targetQueueCount)
                {
                    break;
                }

                if (!QueueWorldJob(candidate, currentClockMinute))
                {
                    continue;
                }

                queuedCount += 1;
            }
        }

        private void BuildOverflowCandidates(List<NpcWorldJobCandidate> candidates)
        {
            if (candidates == null || _industryManager == null || _industryManager.Industries == null)
            {
                return;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var origin = _industryManager.Industries[i];
                if (origin == null || !CanUseAsWorldDispatchOrigin(origin) || origin.Outputs == null || origin.Outputs.Count == 0)
                {
                    continue;
                }

                var outputs = origin.GetSortedOutputs();
                for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
                {
                    var commodity = CommodityCatalog.Normalize(outputs[outputIndex]);
                    var availableTons = GetDispatchOriginAvailableTons(origin, commodity);
                    if (availableTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                    {
                        continue;
                    }

                    var fillRatio = GetCommodityFillRatio(origin, commodity);
                    if (fillRatio + 0.001f < _worldDispatchConfig.OverflowThreshold)
                    {
                        continue;
                    }

                    var destination = FindBestDestinationForCommodity(origin, commodity);
                    if (destination != null)
                    {
                        var destinationFreeTons = GetDispatchDestinationFreeTons(destination, commodity);
                        var tons = ComputeDispatchTons(availableTons, destinationFreeTons);
                        var type = origin.IsWarehouse || destination.IsWarehouse
                            ? NpcWorldJobType.WarehouseBalancing
                            : NpcWorldJobType.OverflowRescue;
                        var candidate = CreateWorldJobCandidate(
                            type,
                            origin,
                            destination,
                            commodity,
                            tons,
                            55f + (fillRatio * 65f),
                            false,
                            0);
                        if (candidate != null)
                        {
                            candidates.Add(candidate);
                        }
                        continue;
                    }

                    if (fillRatio + 0.001f < Math.Min(0.98f, _worldDispatchConfig.OverflowThreshold + 0.05f))
                    {
                        continue;
                    }

                    var exportTons = ComputeDispatchTons(availableTons, _worldDispatchConfig.MaxDispatchTons);
                    var exportCandidate = CreateWorldJobCandidate(
                        NpcWorldJobType.ExternalExport,
                        origin,
                        null,
                        commodity,
                        exportTons,
                        45f + (fillRatio * 50f),
                        false,
                        0);
                    if (exportCandidate != null)
                    {
                        exportCandidate.DestinationLabel = "Outside buyers";
                        candidates.Add(exportCandidate);
                    }
                }
            }
        }

        private void BuildShortageCandidates(List<NpcWorldJobCandidate> candidates)
        {
            if (candidates == null || _industryManager == null || _industryManager.Industries == null)
            {
                return;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var destination = _industryManager.Industries[i];
                if (destination == null || !CanUseAsWorldDispatchDestination(destination))
                {
                    continue;
                }

                var commodities = GetDispatchDemandCommodities(destination);
                for (int commodityIndex = 0; commodityIndex < commodities.Count; commodityIndex++)
                {
                    var commodity = commodities[commodityIndex];
                    var fillRatio = GetCommodityFillRatio(destination, commodity);
                    var shortageThreshold = IsServiceCommodity(commodity)
                        ? _worldDispatchConfig.ServiceShortageThreshold
                        : _worldDispatchConfig.ShortageThreshold;
                    if (fillRatio - 0.001f > shortageThreshold)
                    {
                        continue;
                    }

                    var destinationFreeTons = GetDispatchDestinationFreeTons(destination, commodity);
                    if (destinationFreeTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                    {
                        continue;
                    }

                    var origin = FindBestOriginForCommodity(destination, commodity);
                    var isServiceRun = IsServiceCommodity(commodity);
                    if (origin != null)
                    {
                        var type = isServiceRun
                            ? NpcWorldJobType.ServiceRun
                            : (origin.IsWarehouse || destination.IsWarehouse
                                ? NpcWorldJobType.WarehouseBalancing
                                : NpcWorldJobType.ShortageRelief);
                        var tons = ComputeDispatchTons(GetDispatchOriginAvailableTons(origin, commodity), destinationFreeTons);
                        var candidate = CreateWorldJobCandidate(
                            type,
                            origin,
                            destination,
                            commodity,
                            tons,
                            60f + ((1f - fillRatio) * 80f) + (isServiceRun ? 10f : 0f),
                            false,
                            0);
                        if (candidate != null)
                        {
                            candidates.Add(candidate);
                        }

                        continue;
                    }

                    var importType = isServiceRun ? NpcWorldJobType.ServiceRun : NpcWorldJobType.ExternalImport;
                    var importTons = ComputeDispatchTons(_worldDispatchConfig.MaxDispatchTons, destinationFreeTons);
                    var importCandidate = CreateWorldJobCandidate(
                        importType,
                        null,
                        destination,
                        commodity,
                        importTons,
                        50f + ((1f - fillRatio) * 75f) + (isServiceRun ? 14f : 0f),
                        false,
                        0);
                    if (importCandidate != null)
                    {
                        importCandidate.SourceLabel = isServiceRun ? "Regional service yard" : "External suppliers";
                        candidates.Add(importCandidate);
                    }
                }
            }
        }

        private void BuildRivalCandidates(List<NpcWorldJobCandidate> candidates)
        {
            if (candidates == null || _industryManager == null || _industryManager.Industries == null)
            {
                return;
            }

            if (_random.NextDouble() > _worldDispatchConfig.RivalJobChance)
            {
                return;
            }

            NpcWorldJobCandidate bestCandidate = null;
            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var origin = _industryManager.Industries[i];
                if (origin == null || !CanUseAsWorldDispatchOrigin(origin) || origin.Outputs == null || origin.Outputs.Count == 0)
                {
                    continue;
                }

                var outputs = origin.GetSortedOutputs();
                for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
                {
                    var commodity = CommodityCatalog.Normalize(outputs[outputIndex]);
                    var unitPrice = _globalMarket != null ? _globalMarket.GetUnitPrice(commodity) : 0f;
                    if (unitPrice < 700f)
                    {
                        continue;
                    }

                    var originTons = GetDispatchOriginAvailableTons(origin, commodity);
                    if (originTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                    {
                        continue;
                    }

                    var destination = FindBestDestinationForCommodity(origin, commodity, true);
                    if (destination == null)
                    {
                        continue;
                    }

                    var tons = ComputeDispatchTons(originTons, GetDispatchDestinationFreeTons(destination, commodity));
                    var candidate = CreateWorldJobCandidate(
                        NpcWorldJobType.RivalFreight,
                        origin,
                        destination,
                        commodity,
                        tons,
                        35f + (unitPrice / 60f),
                        true,
                        0);
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (bestCandidate == null || candidate.Score > bestCandidate.Score)
                    {
                        bestCandidate = candidate;
                    }
                }
            }

            if (bestCandidate != null)
            {
                candidates.Add(bestCandidate);
            }
        }

        private NpcWorldJobCandidate CreateWorldJobCandidate(
            NpcWorldJobType type,
            Industry origin,
            Industry destination,
            string commodity,
            float tons,
            float baseScore,
            bool isRivalJob,
            int backhaulDepth)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity) || tons + 0.001f < _worldDispatchConfig.MinDispatchTons)
            {
                return null;
            }

            if (HasConflictingManualRoute(origin, destination, commodity))
            {
                return null;
            }

            var priorityMatch = IsPriorityMatch(type, origin, destination, commodity);
            var isSpotOpportunity = !isRivalJob && _random.NextDouble() <= _worldDispatchConfig.SpotOpportunityChance;
            var listingLeadTime = isRivalJob
                ? Math.Max(30, _worldDispatchConfig.ListingLeadTimeMinutes / 3)
                : (isSpotOpportunity
                    ? _worldDispatchConfig.ListingLeadTimeMinutes
                    : Math.Max(20, _worldDispatchConfig.ListingLeadTimeMinutes / 2));

            return new NpcWorldJobCandidate
            {
                Type = type,
                OriginIndustry = origin,
                DestinationIndustry = destination,
                Commodity = commodity,
                SourceLabel = origin != null ? origin.Name : "External suppliers",
                DestinationLabel = destination != null ? destination.Name : "Outside buyers",
                Tons = tons,
                Score = baseScore + GetRouteSupportScore(origin, destination) + GetPriorityScoreBonus(type, origin, destination, commodity, priorityMatch),
                IsSpotOpportunity = isSpotOpportunity,
                IsPriorityMatch = priorityMatch,
                UsesPremiumDispatch = _premiumDispatchEnabled && priorityMatch && !isRivalJob,
                HasVisibleConvoy = origin != null && destination != null && _random.NextDouble() <= _worldDispatchConfig.VisualSpawnChance,
                IsRivalJob = isRivalJob,
                ListingLeadTimeMinutes = listingLeadTime,
                BackhaulDepth = Math.Max(0, backhaulDepth),
            };
        }

        private bool QueueWorldJob(NpcWorldJobCandidate candidate, int currentClockMinute)
        {
            if (candidate == null || IsDuplicateWorldJob(candidate))
            {
                return false;
            }

            _worldJobs.Add(new NpcWorldLogisticsJob
            {
                Id = _nextWorldJobId++,
                Type = candidate.Type,
                Phase = NpcWorldJobPhase.Listed,
                Commodity = candidate.Commodity,
                SourceLabel = candidate.SourceLabel ?? string.Empty,
                DestinationLabel = candidate.DestinationLabel ?? string.Empty,
                OriginIndustryId = candidate.OriginIndustry != null ? candidate.OriginIndustry.Id : string.Empty,
                DestinationIndustryId = candidate.DestinationIndustry != null ? candidate.DestinationIndustry.Id : string.Empty,
                Tons = candidate.Tons,
                RemainingInGameMinutes = Math.Max(1, candidate.ListingLeadTimeMinutes),
                TotalInGameMinutes = Math.Max(1, candidate.ListingLeadTimeMinutes),
                CreatedClockMinute = Math.Max(0, currentClockMinute),
                IsSpotOpportunity = candidate.IsSpotOpportunity,
                UsesPremiumDispatch = candidate.UsesPremiumDispatch,
                IsPriorityMatch = candidate.IsPriorityMatch,
                HasVisibleConvoy = candidate.HasVisibleConvoy,
                IsRivalJob = candidate.IsRivalJob,
                BackhaulDepth = candidate.BackhaulDepth,
                StatusText = candidate.IsSpotOpportunity
                    ? "Spot market window open"
                    : (candidate.IsRivalJob ? "Rival freight listed" : "Queued for dispatch"),
            });

            return true;
        }

        private void StartWorldJob(NpcWorldLogisticsJob job, int now, int currentClockMinute)
        {
            if (job == null || job.Phase != NpcWorldJobPhase.Listed)
            {
                return;
            }

            string blocker;
            if (!IsWorldJobStillNeeded(job, true, out blocker))
            {
                job.StatusText = blocker;
                job.Phase = NpcWorldJobPhase.Cancelled;
                CleanupWorldJobVisual(job);
                return;
            }

            ChargePremiumDispatch(job);
            job.Phase = NpcWorldJobPhase.Traveling;
            job.RemainingInGameMinutes = ComputeTravelMinutes(job);
            job.TotalInGameMinutes = Math.Max(job.TotalInGameMinutes, job.RemainingInGameMinutes);
            job.CreatedClockMinute = Math.Max(0, currentClockMinute);
            job.StatusText = job.IsRivalJob
                ? "Rival convoy en route"
                : (job.IsSpotOpportunity ? "Spot window closed; NPC convoy en route" : "NPC convoy en route");
            string visualFailure;
            if (!TryStartWorldJobVisual(job, now, out visualFailure) && job.HasVisibleConvoy)
            {
                job.HasVisibleConvoy = false;
                job.StatusText = BuildWorldVisualFailureStatus(job, visualFailure);
                _showStatus?.Invoke(job.StatusText);
            }
        }

        private void CompleteWorldJob(NpcWorldLogisticsJob job, int now, int currentClockMinute)
        {
            if (job == null || job.Phase != NpcWorldJobPhase.Traveling)
            {
                return;
            }

            string outcome;
            var succeeded = TryResolveWorldJob(job, now, out outcome);
            CleanupWorldJobVisual(job);
            job.StatusText = outcome;
            job.Phase = succeeded ? NpcWorldJobPhase.Completed : NpcWorldJobPhase.Cancelled;
            if (!succeeded)
            {
                return;
            }

            _completedWorldDispatches += 1;
            if (job.IsSpotOpportunity || job.UsesPremiumDispatch || job.IsRivalJob)
            {
                _showStatus?.Invoke(outcome);
            }

            TryQueueBackhaulJob(job, currentClockMinute);
        }

        private bool TryResolveWorldJob(NpcWorldLogisticsJob job, int now, out string outcome)
        {
            outcome = string.Empty;
            if (job == null)
            {
                outcome = "Dispatch job is unavailable.";
                return false;
            }

            var origin = FindIndustryById(job.OriginIndustryId);
            var destination = FindIndustryById(job.DestinationIndustryId);
            if (job.Type == NpcWorldJobType.ExternalImport || (job.Type == NpcWorldJobType.ServiceRun && origin == null))
            {
                return TryExecuteExternalImport(job, destination, now, out outcome);
            }

            if (job.Type == NpcWorldJobType.ExternalExport)
            {
                return TryExecuteExternalExport(job, origin, out outcome);
            }

            return TryExecuteInternalTransfer(job, origin, destination, now, out outcome);
        }

        private bool TryExecuteExternalImport(NpcWorldLogisticsJob job, Industry destination, int now, out string outcome)
        {
            outcome = "Import route is no longer valid.";
            if (job == null || destination == null || !CanUseAsWorldDispatchDestination(destination))
            {
                return false;
            }

            var acceptedTons = TryStoreDispatchCommodity(destination, job.Commodity, Math.Min(job.Tons, GetDispatchDestinationFreeTons(destination, job.Commodity)));
            if (acceptedTons <= 0.001f)
            {
                outcome = string.Format("{0} no longer needs {1}.", destination.Name, job.Commodity);
                return false;
            }

            _globalMarket?.RegisterDelivery(job.Commodity, now);
            _territoryManager?.RegisterDelivery(destination, job.Commodity, acceptedTons, true, string.Empty, string.Empty);
            outcome = string.Format("{0} delivered {1} {2} to {3}.", FormatWorldJobType(job.Type), ModFormatting.FormatTons(acceptedTons), job.Commodity, destination.Name);
            return true;
        }

        private bool TryExecuteExternalExport(NpcWorldLogisticsJob job, Industry origin, out string outcome)
        {
            outcome = "Export route is no longer valid.";
            if (job == null || origin == null || !CanUseAsWorldDispatchOrigin(origin))
            {
                return false;
            }

            var removedTons = TryTakeDispatchCommodity(origin, job.Commodity, Math.Min(job.Tons, GetDispatchOriginAvailableTons(origin, job.Commodity)));
            if (removedTons <= 0.001f)
            {
                outcome = string.Format("{0} no longer has enough {1} for export.", origin.Name, job.Commodity);
                return false;
            }

            _territoryManager?.RegisterLoad(origin, job.Commodity, removedTons, true);
            outcome = string.Format("Exported {0} {1} from {2} to outside buyers.", ModFormatting.FormatTons(removedTons), job.Commodity, origin.Name);
            return true;
        }

        private bool TryExecuteInternalTransfer(NpcWorldLogisticsJob job, Industry origin, Industry destination, int now, out string outcome)
        {
            outcome = "Dispatch route is no longer valid.";
            if (job == null || origin == null || destination == null || !CanDispatchBetween(origin, destination))
            {
                return false;
            }

            var requestedTons = Math.Min(job.Tons, Math.Min(GetDispatchOriginAvailableTons(origin, job.Commodity), GetDispatchDestinationFreeTons(destination, job.Commodity)));
            if (requestedTons <= 0.001f)
            {
                outcome = string.Format("{0} or {1} is no longer ready for {2}.", origin.Name, destination.Name, job.Commodity);
                return false;
            }

            var removedTons = TryTakeDispatchCommodity(origin, job.Commodity, requestedTons);
            if (removedTons <= 0.001f)
            {
                outcome = string.Format("{0} no longer has enough {1}.", origin.Name, job.Commodity);
                return false;
            }

            var acceptedTons = TryStoreDispatchCommodity(destination, job.Commodity, removedTons);
            if (acceptedTons <= 0.001f)
            {
                TryRestoreDispatchCommodity(origin, job.Commodity, removedTons);
                outcome = string.Format("{0} no longer has room for {1}.", destination.Name, job.Commodity);
                return false;
            }

            if (acceptedTons + 0.001f < removedTons)
            {
                TryRestoreDispatchCommodity(origin, job.Commodity, removedTons - acceptedTons);
            }

            _territoryManager?.RegisterLoad(origin, job.Commodity, acceptedTons, true);
            _territoryManager?.RegisterDelivery(destination, job.Commodity, acceptedTons, true, origin.Id, origin.DistrictName);
            _industryManager.ComputeDeliveryProfit(destination, job.Commodity, acceptedTons, _globalMarket, now);
            outcome = string.Format(
                "{0} moved {1} {2} from {3} to {4}.",
                FormatWorldJobType(job.Type),
                ModFormatting.FormatTons(acceptedTons),
                job.Commodity,
                origin.Name,
                destination.Name);
            return true;
        }

        private void TryQueueBackhaulJob(NpcWorldLogisticsJob completedJob, int currentClockMinute)
        {
            if (completedJob == null || completedJob.BackhaulDepth >= 1 || completedJob.IsRivalJob)
            {
                return;
            }

            if (_worldJobs.Count(job => job != null && (job.Phase == NpcWorldJobPhase.Listed || job.Phase == NpcWorldJobPhase.Traveling)) >= _worldDispatchConfig.MaxActiveJobs)
            {
                return;
            }

            var backhaulOrigin = FindIndustryById(completedJob.DestinationIndustryId);
            var preferredDestination = FindIndustryById(completedJob.OriginIndustryId);
            if (backhaulOrigin == null || !CanUseAsWorldDispatchOrigin(backhaulOrigin) || backhaulOrigin.Outputs == null || backhaulOrigin.Outputs.Count == 0)
            {
                return;
            }

            NpcWorldJobCandidate bestCandidate = null;
            var outputs = backhaulOrigin.GetSortedOutputs();
            for (int i = 0; i < outputs.Count; i++)
            {
                var commodity = CommodityCatalog.Normalize(outputs[i]);
                var originTons = GetDispatchOriginAvailableTons(backhaulOrigin, commodity);
                if (originTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                {
                    continue;
                }

                var destination = preferredDestination != null && CanDispatchBetween(backhaulOrigin, preferredDestination) && GetDispatchDestinationFreeTons(preferredDestination, commodity) > 0.001f
                    ? preferredDestination
                    : FindBackhaulDestination(backhaulOrigin, commodity);
                if (destination == null)
                {
                    continue;
                }

                var tons = ComputeDispatchTons(originTons, GetDispatchDestinationFreeTons(destination, commodity));
                var candidate = CreateWorldJobCandidate(
                    backhaulOrigin.IsWarehouse || destination.IsWarehouse ? NpcWorldJobType.WarehouseBalancing : NpcWorldJobType.OverflowRescue,
                    backhaulOrigin,
                    destination,
                    commodity,
                    tons,
                    40f,
                    false,
                    completedJob.BackhaulDepth + 1);
                if (candidate == null)
                {
                    continue;
                }

                candidate.IsSpotOpportunity = false;
                candidate.ListingLeadTimeMinutes = Math.Max(15, _worldDispatchConfig.ListingLeadTimeMinutes / 3);
                if (bestCandidate == null || candidate.Score > bestCandidate.Score)
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
            {
                QueueWorldJob(bestCandidate, currentClockMinute);
            }
        }

        private Industry FindBestOriginForCommodity(Industry destination, string commodity)
        {
            Industry bestOrigin = null;
            var bestScore = float.MinValue;
            if (destination == null || string.IsNullOrWhiteSpace(commodity) || _industryManager == null || _industryManager.Industries == null)
            {
                return null;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var origin = _industryManager.Industries[i];
                if (origin == null || string.Equals(origin.Id, destination.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CanDispatchBetween(origin, destination))
                {
                    continue;
                }

                var availableTons = GetDispatchOriginAvailableTons(origin, commodity);
                if (availableTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                {
                    continue;
                }

                var score = (GetCommodityFillRatio(origin, commodity) * 70f) + Math.Min(18f, availableTons);
                if (origin.IsWarehouse)
                {
                    score += 12f;
                }

                score += GetRouteSupportScore(origin, destination);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOrigin = origin;
                }
            }

            return bestOrigin;
        }

        private Industry FindBestDestinationForCommodity(Industry origin, string commodity, bool preferSinks = false)
        {
            Industry bestDestination = null;
            var bestScore = float.MinValue;
            if (origin == null || string.IsNullOrWhiteSpace(commodity) || _industryManager == null || _industryManager.Industries == null)
            {
                return null;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var destination = _industryManager.Industries[i];
                if (destination == null || string.Equals(destination.Id, origin.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CanDispatchBetween(origin, destination))
                {
                    continue;
                }

                var freeTons = GetDispatchDestinationFreeTons(destination, commodity);
                if (freeTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                {
                    continue;
                }

                var score = ((1f - GetCommodityFillRatio(destination, commodity)) * 75f) + Math.Min(16f, freeTons);
                if (destination.IsWarehouse)
                {
                    score += 8f;
                }

                if (preferSinks && (destination.IsSink || destination.IsStore || destination.IsGasStation || destination.IsConstructionSink))
                {
                    score += 14f;
                }

                score += GetRouteSupportScore(origin, destination);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDestination = destination;
                }
            }

            return bestDestination;
        }

        private Industry FindBackhaulDestination(Industry origin, string commodity)
        {
            Industry bestDestination = null;
            var bestScore = float.MinValue;
            if (origin == null || string.IsNullOrWhiteSpace(commodity) || _industryManager == null || _industryManager.Industries == null)
            {
                return null;
            }

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var destination = _industryManager.Industries[i];
                if (destination == null || string.Equals(destination.Id, origin.Id, StringComparison.OrdinalIgnoreCase) || !CanDispatchBetween(origin, destination))
                {
                    continue;
                }

                if (origin.Position.DistanceTo(destination.Position) > _worldDispatchConfig.BackhaulSearchRadius)
                {
                    continue;
                }

                var freeTons = GetDispatchDestinationFreeTons(destination, commodity);
                if (freeTons + 0.001f < _worldDispatchConfig.MinDispatchTons)
                {
                    continue;
                }

                var score = 25f + ((1f - GetCommodityFillRatio(destination, commodity)) * 40f);
                if (destination.IsWarehouse)
                {
                    score += 5f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDestination = destination;
                }
            }

            return bestDestination;
        }

        private List<string> GetDispatchDemandCommodities(Industry industry)
        {
            if (industry == null)
            {
                return new List<string>();
            }

            return industry.SortedInputs
                .Concat(industry.SortedOptionalInputs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                .Select(CommodityCatalog.Normalize)
                .ToList();
        }

        private bool CanUseAsWorldDispatchOrigin(Industry industry)
        {
            return industry != null
                && HasGameplayAccess(industry)
                && MeetsDistrictNpcRequirement(industry)
                && (industry.Outputs.Count > 0 || industry.IsWarehouse);
        }

        private bool CanUseAsWorldDispatchDestination(Industry industry)
        {
            return industry != null
                && HasGameplayAccess(industry)
                && MeetsDistrictNpcRequirement(industry)
                && (industry.Inputs.Count > 0 || industry.OptionalInputs.Count > 0 || industry.IsWarehouse);
        }

        private bool CanDispatchBetween(Industry origin, Industry destination)
        {
            if (origin == null || destination == null || !CanUseAsWorldDispatchOrigin(origin) || !CanUseAsWorldDispatchDestination(destination))
            {
                return false;
            }

            return true;
        }

        private bool HasConflictingManualRoute(Industry origin, Industry destination, string commodity)
        {
            for (int i = 0; i < _contracts.Count; i++)
            {
                var contract = _contracts[i];
                if (contract == null || !CommodityCatalog.IsSameCommodity(contract.Commodity, commodity))
                {
                    continue;
                }

                var sameOrigin = origin != null && contract.OriginIndustry != null && string.Equals(contract.OriginIndustry.Id, origin.Id, StringComparison.OrdinalIgnoreCase);
                var sameDestination = destination != null && contract.DestinationIndustry != null && string.Equals(contract.DestinationIndustry.Id, destination.Id, StringComparison.OrdinalIgnoreCase);
                if ((origin == null || sameOrigin) && (destination == null || sameDestination))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDuplicateWorldJob(NpcWorldJobCandidate candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            for (int i = 0; i < _worldJobs.Count; i++)
            {
                var job = _worldJobs[i];
                if (job == null || (job.Phase != NpcWorldJobPhase.Listed && job.Phase != NpcWorldJobPhase.Traveling))
                {
                    continue;
                }

                if (!CommodityCatalog.IsSameCommodity(job.Commodity, candidate.Commodity))
                {
                    continue;
                }

                if (!string.Equals(job.OriginIndustryId ?? string.Empty, candidate.OriginIndustry != null ? candidate.OriginIndustry.Id : string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(job.DestinationIndustryId ?? string.Empty, candidate.DestinationIndustry != null ? candidate.DestinationIndustry.Id : string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsWorldJobStillNeeded(NpcWorldLogisticsJob job, bool strictShortageCheck, out string blocker)
        {
            blocker = string.Empty;
            if (job == null)
            {
                blocker = "Dispatch job no longer exists.";
                return false;
            }

            var origin = FindIndustryById(job.OriginIndustryId);
            var destination = FindIndustryById(job.DestinationIndustryId);
            if (job.Type == NpcWorldJobType.ExternalImport || (job.Type == NpcWorldJobType.ServiceRun && origin == null))
            {
                if (destination == null || !CanUseAsWorldDispatchDestination(destination))
                {
                    blocker = "Destination is no longer available for dispatch.";
                    return false;
                }

                if (strictShortageCheck)
                {
                    var fillRatio = GetCommodityFillRatio(destination, job.Commodity);
                    var threshold = IsServiceCommodity(job.Commodity)
                        ? _worldDispatchConfig.ServiceShortageThreshold
                        : _worldDispatchConfig.ShortageThreshold;
                    if (fillRatio - 0.001f > threshold)
                    {
                        blocker = string.Format("{0} no longer needs {1}.", destination.Name, job.Commodity);
                        return false;
                    }
                }

                return true;
            }

            if (job.Type == NpcWorldJobType.ExternalExport)
            {
                if (origin == null || !CanUseAsWorldDispatchOrigin(origin))
                {
                    blocker = "Origin is no longer available for export.";
                    return false;
                }

                if (strictShortageCheck && GetCommodityFillRatio(origin, job.Commodity) + 0.001f < _worldDispatchConfig.OverflowThreshold)
                {
                    blocker = string.Format("{0} is no longer overflowing with {1}.", origin.Name, job.Commodity);
                    return false;
                }

                return GetDispatchOriginAvailableTons(origin, job.Commodity) > 0.001f;
            }

            if (origin == null || destination == null || !CanDispatchBetween(origin, destination))
            {
                blocker = "Route endpoints are no longer available.";
                return false;
            }

            if (GetDispatchOriginAvailableTons(origin, job.Commodity) <= 0.001f)
            {
                blocker = string.Format("{0} no longer has enough {1}.", origin.Name, job.Commodity);
                return false;
            }

            if (GetDispatchDestinationFreeTons(destination, job.Commodity) <= 0.001f)
            {
                blocker = string.Format("{0} no longer has room for {1}.", destination.Name, job.Commodity);
                return false;
            }

            if (strictShortageCheck && (job.Type == NpcWorldJobType.ShortageRelief || job.Type == NpcWorldJobType.ServiceRun || job.Type == NpcWorldJobType.WarehouseBalancing))
            {
                var fillRatio = GetCommodityFillRatio(destination, job.Commodity);
                var threshold = IsServiceCommodity(job.Commodity)
                    ? _worldDispatchConfig.ServiceShortageThreshold
                    : _worldDispatchConfig.ShortageThreshold;
                if (!destination.IsWarehouse && fillRatio - 0.001f > threshold && job.Type != NpcWorldJobType.RivalFreight)
                {
                    blocker = string.Format("{0} already stabilized {1}.", destination.Name, job.Commodity);
                    return false;
                }
            }

            return true;
        }

        private void ChargePremiumDispatch(NpcWorldLogisticsJob job)
        {
            if (job == null || !job.UsesPremiumDispatch || _getProfit == null || _deductProfit == null || _globalMarket == null)
            {
                return;
            }

            var fee = ComputePremiumDispatchFee(job);
            if (fee <= 0.001f)
            {
                return;
            }

            if (_getProfit() + 0.001f < fee)
            {
                job.UsesPremiumDispatch = false;
                job.StatusText = "Premium dispatch skipped; balance too low";
                return;
            }

            _deductProfit(fee);
            RecordFinanceExpense(
                CompanyFinanceCategory.ServiceCall,
                fee,
                string.Format("Premium dispatch for {0}", job.Commodity ?? string.Empty));
        }

        private float ComputePremiumDispatchFee(NpcWorldLogisticsJob job)
        {
            if (job == null || _globalMarket == null)
            {
                return 0f;
            }

            return Math.Max(0f, _globalMarket.GetUnitPrice(job.Commodity) * Math.Max(0f, job.Tons) * Math.Max(0f, _worldDispatchConfig.PremiumDispatchCostMultiplier - 1f));
        }

        private int ComputeTravelMinutes(NpcWorldLogisticsJob job)
        {
            var origin = FindIndustryById(job != null ? job.OriginIndustryId : string.Empty);
            var destination = FindIndustryById(job != null ? job.DestinationIndustryId : string.Empty);
            var minutes = _worldDispatchConfig != null ? _worldDispatchConfig.BaseTravelMinutes : 180;

            if (origin != null && destination != null)
            {
                minutes += (int)Math.Round(origin.Position.DistanceTo(destination.Position) / 110f);
            }
            else if (origin != null || destination != null)
            {
                minutes += 90;
            }

            if (job != null && job.BackhaulDepth > 0)
            {
                minutes = (int)(minutes * 0.75f);
            }

            if (job != null && IsServiceCommodity(job.Commodity))
            {
                minutes = (int)(minutes * 0.85f);
            }

            if (job != null && job.IsPriorityMatch)
            {
                minutes = (int)(minutes * (1f - (_worldDispatchConfig.PriorityScoreBonus * 0.30f)));
            }

            if (job != null && job.UsesPremiumDispatch)
            {
                minutes = (int)(minutes * (1f - (_worldDispatchConfig.PremiumDispatchScoreBonus * 0.50f)));
            }

            return Math.Max(20, minutes);
        }

        private bool TryStartWorldJobVisual(NpcWorldLogisticsJob job, int now, out string failureReason)
        {
            failureReason = string.Empty;
            if (job == null || !job.HasVisibleConvoy)
            {
                return false;
            }

            var origin = FindIndustryById(job.OriginIndustryId);
            var destination = FindIndustryById(job.DestinationIndustryId);
            if (origin == null || destination == null)
            {
                failureReason = "Route endpoints are unavailable";
                return false;
            }

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            if (!TryResolveVehicleForCommodity(job.Commodity, out selectedVehicle, out selectedTractor, out failureReason))
            {
                return false;
            }

            var visualRoute = new NpcLogisticsContract(-Math.Max(1, job.Id))
            {
                OriginIndustry = origin,
                DestinationIndustry = destination,
                Commodity = job.Commodity,
                Tier = ResolveWorldDriverTier(job),
                VehicleDefinition = selectedVehicle,
                TractorDefinition = selectedTractor,
                StatusText = job.StatusText,
            };

            string spawnStatus;
            if (!TrySpawnRouteEntities(visualRoute, out spawnStatus))
            {
                failureReason = spawnStatus;
                return false;
            }

            var cargoVehicle = GetCargoVehicle(visualRoute);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                cargoState.Commodity = job.Commodity;
                cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(job.Commodity);
                cargoState.WeightTons = Math.Max(_worldDispatchConfig.MinDispatchTons, job.Tons);
                cargoState.CargoCondition = job.IsRivalJob ? 0.70f : 0.88f;
                cargoState.SourceIndustryId = origin.Id;
                cargoState.SourceDistrictName = origin.DistrictName;
                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            }

            if (visualRoute.RouteBlip != null && visualRoute.RouteBlip.Exists())
            {
                visualRoute.RouteBlip.Name = job.IsRivalJob
                    ? string.Format("Rival Freight: {0}", job.Commodity)
                    : string.Format("World Freight: {0}", job.Commodity);
                visualRoute.RouteBlip.Color = job.IsRivalJob ? BlipColor.Red : (job.IsSpotOpportunity ? BlipColor.Yellow : BlipColor.White);
            }

            visualRoute.Phase = NpcRoutePhase.DrivingToDestination;
            visualRoute.NextDriveTaskRefreshMs = 0;
            EnsureDriveTask(visualRoute, GetDestinationRoutePosition(visualRoute), now);
            job.VisualRoute = visualRoute;
            return true;
        }

        private void RefreshWorldJobVisual(NpcWorldLogisticsJob job, int now)
        {
            if (job == null || job.VisualRoute == null)
            {
                return;
            }

            if (!HasOperationalEntities(job.VisualRoute))
            {
                CleanupWorldJobVisual(job);
                job.HasVisibleConvoy = false;
                return;
            }

            RefreshBlip(job.VisualRoute);
            EnsureDriveTask(job.VisualRoute, GetDestinationRoutePosition(job.VisualRoute), now);
            var driverVehicle = GetDriverVehicle(job.VisualRoute);
            if (driverVehicle != null && driverVehicle.Exists())
            {
                if (driverVehicle.Position.DistanceTo(GetDestinationRoutePosition(job.VisualRoute)) <= ArrivalDistance)
                {
                    job.RemainingInGameMinutes = 0;
                }
            }
        }

        private void CleanupWorldJobVisual(NpcWorldLogisticsJob job)
        {
            if (job == null || job.VisualRoute == null)
            {
                return;
            }

            CleanupContractEntities(job.VisualRoute);
            job.VisualRoute = null;
        }

        private void CleanupInactiveWorldJobs()
        {
            for (int i = _worldJobs.Count - 1; i >= 0; i--)
            {
                var job = _worldJobs[i];
                if (job == null)
                {
                    _worldJobs.RemoveAt(i);
                    continue;
                }

                if (job.Phase == NpcWorldJobPhase.Completed || job.Phase == NpcWorldJobPhase.Cancelled)
                {
                    CleanupWorldJobVisual(job);
                    _worldJobs.RemoveAt(i);
                }
            }
        }

        private NpcDriverTierDefinition ResolveWorldDriverTier(NpcWorldLogisticsJob job)
        {
            var preferred = job != null && (job.IsRivalJob || job.UsesPremiumDispatch)
                ? FindDriverTier("Veteran")
                : FindDriverTier("Professional");
            return preferred ?? _driverTiers.FirstOrDefault(tier => tier != null) ?? new NpcDriverTierDefinition("Rookie", "Rookie", DefaultNpcModel, 0.4f, 0.6f, 25f, 1500f, 2000f, 3000f);
        }

        private float GetDispatchOriginAvailableTons(Industry industry, string commodity)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            if (industry.ProducesCommodity(commodity))
            {
                return Math.Max(0f, industry.GetStock(commodity));
            }

            return industry.IsWarehouse && industry.AcceptsCommodity(commodity)
                ? Math.Max(0f, industry.GetStock(commodity))
                : 0f;
        }

        private float GetDispatchDestinationFreeTons(Industry industry, string commodity)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            if (industry.AcceptsCommodity(commodity))
            {
                return Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            }

            return industry.IsWarehouse && industry.AcceptsCommodity(commodity)
                ? Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity))
                : 0f;
        }

        private float TryTakeDispatchCommodity(Industry industry, string commodity, float tons)
        {
            if (industry == null || tons <= 0f)
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            if (industry.ProducesCommodity(commodity))
            {
                return industry.RemoveOutput(commodity, tons);
            }

            return industry.IsWarehouse && industry.AcceptsCommodity(commodity)
                ? industry.RemoveInput(commodity, tons)
                : 0f;
        }

        private float TryStoreDispatchCommodity(Industry industry, string commodity, float tons)
        {
            if (industry == null || tons <= 0f)
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            if (industry.AcceptsCommodity(commodity))
            {
                return industry.AddInput(commodity, tons);
            }

            return industry.ProducesCommodity(commodity)
                ? industry.AddOutput(commodity, tons)
                : 0f;
        }

        private void TryRestoreDispatchCommodity(Industry industry, string commodity, float tons)
        {
            if (industry == null || tons <= 0f)
            {
                return;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            if (industry.ProducesCommodity(commodity))
            {
                industry.AddOutput(commodity, tons);
                return;
            }

            if (industry.AcceptsCommodity(commodity))
            {
                industry.AddInput(commodity, tons);
            }
        }

        private float GetCommodityFillRatio(Industry industry, string commodity)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            var current = Math.Max(0f, industry.GetStock(commodity));
            var capacity = current + Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            return capacity <= 0.001f ? 0f : current / capacity;
        }

        private float ComputeDispatchTons(float availableTons, float freeTons)
        {
            var ceiling = Math.Min(Math.Max(0f, availableTons), Math.Max(0f, freeTons));
            if (ceiling + 0.001f < _worldDispatchConfig.MinDispatchTons)
            {
                return 0f;
            }

            return Math.Max(
                _worldDispatchConfig.MinDispatchTons,
                Math.Min(_worldDispatchConfig.MaxDispatchTons, ceiling));
        }

        private bool IsServiceCommodity(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            return _worldDispatchConfig != null
                && _worldDispatchConfig.ServiceCommodities != null
                && _worldDispatchConfig.ServiceCommodities.Contains(commodity, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsPriorityMatch(NpcWorldJobType type, Industry origin, Industry destination, string commodity)
        {
            if (!string.IsNullOrWhiteSpace(_worldPriorityCommodity) && CommodityCatalog.IsSameCommodity(_worldPriorityCommodity, commodity))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_worldPriorityDistrict))
            {
                if ((origin != null && string.Equals(origin.DistrictName, _worldPriorityDistrict, StringComparison.OrdinalIgnoreCase))
                    || (destination != null && string.Equals(destination.DistrictName, _worldPriorityDistrict, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            switch (_worldDispatchPolicy)
            {
                case NpcWorldDispatchPolicy.OverflowRescue:
                    return type == NpcWorldJobType.OverflowRescue || type == NpcWorldJobType.ExternalExport || type == NpcWorldJobType.WarehouseBalancing;
                case NpcWorldDispatchPolicy.ShortageRelief:
                    return type == NpcWorldJobType.ShortageRelief || type == NpcWorldJobType.ExternalImport || type == NpcWorldJobType.ServiceRun || type == NpcWorldJobType.WarehouseBalancing;
                case NpcWorldDispatchPolicy.MarketPriority:
                    return _globalMarket != null && _globalMarket.GetUnitPrice(commodity) >= 1000f;
                default:
                    return false;
            }
        }

        private float GetPriorityScoreBonus(NpcWorldJobType type, Industry origin, Industry destination, string commodity, bool isPriorityMatch)
        {
            var bonus = 0f;
            switch (_worldDispatchPolicy)
            {
                case NpcWorldDispatchPolicy.OverflowRescue:
                    if (type == NpcWorldJobType.OverflowRescue || type == NpcWorldJobType.ExternalExport || type == NpcWorldJobType.WarehouseBalancing)
                    {
                        bonus += 16f;
                    }

                    break;
                case NpcWorldDispatchPolicy.ShortageRelief:
                    if (type == NpcWorldJobType.ShortageRelief || type == NpcWorldJobType.ExternalImport || type == NpcWorldJobType.ServiceRun || type == NpcWorldJobType.WarehouseBalancing)
                    {
                        bonus += 18f;
                    }

                    break;
                case NpcWorldDispatchPolicy.MarketPriority:
                    bonus += Math.Min(22f, (_globalMarket != null ? _globalMarket.GetUnitPrice(commodity) : 0f) / 250f);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_worldPriorityCommodity) && CommodityCatalog.IsSameCommodity(_worldPriorityCommodity, commodity))
            {
                bonus += 24f;
            }

            if (!string.IsNullOrWhiteSpace(_worldPriorityDistrict)
                && ((origin != null && string.Equals(origin.DistrictName, _worldPriorityDistrict, StringComparison.OrdinalIgnoreCase))
                    || (destination != null && string.Equals(destination.DistrictName, _worldPriorityDistrict, StringComparison.OrdinalIgnoreCase))))
            {
                bonus += 18f;
            }

            if (_premiumDispatchEnabled && isPriorityMatch)
            {
                bonus += 10f + (_worldDispatchConfig.PremiumDispatchScoreBonus * 25f);
            }

            return bonus;
        }

        private float GetRouteSupportScore(Industry origin, Industry destination)
        {
            if (_territoryManager == null || _worldDispatchConfig == null)
            {
                return 0f;
            }

            var support = 0f;
            if (origin != null)
            {
                support += _territoryManager.GetDistrictSupportBonus(origin.DistrictName);
            }

            if (destination != null)
            {
                support += _territoryManager.GetDistrictSupportBonus(destination.DistrictName);
            }

            return support * 100f * Math.Max(0f, _worldDispatchConfig.DistrictSupportWeight);
        }

        private int ClampTriggerPercent(int thresholdPercent, int fallbackValue)
        {
            var value = thresholdPercent < 0 ? fallbackValue : thresholdPercent;
            return Math.Max(0, Math.Min(100, value));
        }

        private float GetCommodityCapacity(Industry industry, string commodity)
        {
            return industry != null ? Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity)) : 0f;
        }

        private bool IsOriginTriggerSatisfied(NpcLogisticsContract contract, out float availableOriginStock)
        {
            availableOriginStock = contract != null && contract.OriginIndustry != null
                ? Math.Max(0f, contract.OriginIndustry.GetStock(contract.Commodity))
                : 0f;
            if (contract == null || availableOriginStock <= 0.001f)
            {
                return false;
            }

            var thresholdPercent = ClampTriggerPercent(contract.OriginTriggerThresholdPercent, 0);
            if (thresholdPercent <= 0)
            {
                return true;
            }

            var capacity = GetCommodityCapacity(contract.OriginIndustry, contract.Commodity);
            if (capacity <= 0.001f)
            {
                return false;
            }

            var thresholdTons = capacity * thresholdPercent / 100f;
            return availableOriginStock + 0.001f >= thresholdTons;
        }

        private float GetDestinationTargetTons(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return 0f;
            }

            var capacity = GetCommodityCapacity(contract.DestinationIndustry, contract.Commodity);
            return capacity * ClampTriggerPercent(contract.DestinationTriggerThresholdPercent, 100) / 100f;
        }

        private float GetDestinationRemainingNeedTons(NpcLogisticsContract contract)
        {
            if (contract == null || contract.DestinationIndustry == null)
            {
                return 0f;
            }

            var currentStock = Math.Max(0f, contract.DestinationIndustry.GetStock(contract.Commodity));
            return Math.Max(0f, GetDestinationTargetTons(contract) - currentStock);
        }

        private bool CanStartOriginPickup(NpcLogisticsContract contract, out string waitStatus)
        {
            waitStatus = string.Empty;
            if (contract == null || contract.OriginIndustry == null || contract.DestinationIndustry == null || string.IsNullOrWhiteSpace(contract.Commodity))
            {
                waitStatus = "Route configuration is incomplete.";
                return false;
            }

            var destinationThreshold = ClampTriggerPercent(contract.DestinationTriggerThresholdPercent, 100);
            if (GetDestinationRemainingNeedTons(contract) <= 0.001f)
            {
                waitStatus = string.Format(
                    "Waiting at office for {0} storage under {1}.",
                    contract.DestinationIndustry.Name,
                    ModFormatting.FormatPercent(destinationThreshold));
                return false;
            }

            float availableOriginStock;
            if (!IsOriginTriggerSatisfied(contract, out availableOriginStock))
            {
                var originThreshold = ClampTriggerPercent(contract.OriginTriggerThresholdPercent, 0);
                waitStatus = originThreshold <= 0
                    ? string.Format("Waiting at office for stock at {0}.", contract.OriginIndustry.Name)
                    : string.Format("Waiting at office for {0} stock above {1}.", contract.OriginIndustry.Name, ModFormatting.FormatPercent(originThreshold));
                return false;
            }

            return true;
        }

        private bool CanResumeDestinationDelivery(NpcLogisticsContract contract, out string waitStatus)
        {
            waitStatus = string.Empty;
            if (contract == null || contract.DestinationIndustry == null)
            {
                waitStatus = "Route configuration is incomplete.";
                return false;
            }

            var destinationThreshold = ClampTriggerPercent(contract.DestinationTriggerThresholdPercent, 100);
            if (GetDestinationRemainingNeedTons(contract) <= 0.001f)
            {
                waitStatus = string.Format(
                    "Waiting at office for {0} storage under {1}.",
                    contract.DestinationIndustry.Name,
                    ModFormatting.FormatPercent(destinationThreshold));
                return false;
            }

            return true;
        }

        private bool TryGetActiveOfficeReturnTarget(out Vector3 position)
        {
            position = Vector3.Zero;
            var office = _getActiveOffice != null ? _getActiveOffice() : null;
            if (office == null)
            {
                return false;
            }

            position = _getGroundPosition != null
                ? _getGroundPosition(office.SpawnPosition)
                : office.SpawnPosition;
            return true;
        }

        private void BeginReturnToOffice(NpcLogisticsContract contract, string reason, int now)
        {
            if (contract == null)
            {
                return;
            }

            Vector3 officePosition;
            if (!TryGetActiveOfficeReturnTarget(out officePosition))
            {
                contract.Phase = NpcRoutePhase.WaitingAtOffice;
                contract.StatusText = string.IsNullOrWhiteSpace(reason) ? "Waiting at office" : reason;
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            contract.Phase = NpcRoutePhase.ReturningToOffice;
            contract.StatusText = string.IsNullOrWhiteSpace(reason)
                ? "Returning to office"
                : string.Format("Returning to office: {0}", reason.TrimEnd('.'));
            contract.WaitUntilMs = 0;
            contract.NextDriveTaskRefreshMs = 0;
            EnsureDriveTask(contract, officePosition, now);
        }

        private void UpdateReturnToOffice(NpcLogisticsContract contract, int now)
        {
            Vector3 officePosition;
            if (!TryGetActiveOfficeReturnTarget(out officePosition))
            {
                contract.Phase = NpcRoutePhase.WaitingAtOffice;
                contract.StatusText = "Waiting at office";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            if (GetDriverVehicle(contract).Position.DistanceTo(officePosition) <= ArrivalDistance)
            {
                ClearPedTasks(contract.Driver);
                contract.Phase = NpcRoutePhase.WaitingAtOffice;
                contract.NextDriveTaskRefreshMs = 0;
                contract.WaitUntilMs = now + 1000;
                var cargoState = _fleetManager.GetOrCreateCargoState(GetCargoVehicle(contract));
                contract.StatusText = cargoState != null && !cargoState.IsEmpty
                    ? "Holding cargo at office"
                    : "Waiting at office";
                return;
            }

            EnsureDriveTask(contract, officePosition, now);
            contract.StatusText = "Returning to office";
        }

        private void UpdateWaitingAtOffice(NpcLogisticsContract contract, int now)
        {
            var cargoState = _fleetManager.GetOrCreateCargoState(GetCargoVehicle(contract));
            var hasCargo = cargoState != null && !cargoState.IsEmpty;

            string waitStatus;
            if (hasCargo)
            {
                if (!CanResumeDestinationDelivery(contract, out waitStatus))
                {
                    contract.StatusText = waitStatus;
                    contract.WaitUntilMs = now + RetryDelayMs;
                    return;
                }

                contract.Phase = NpcRoutePhase.DrivingToDestination;
                contract.NextDriveTaskRefreshMs = 0;
                contract.WaitUntilMs = 0;
                contract.StatusText = string.Format("Resuming delivery to {0}", contract.DestinationIndustry.Name);
                EnsureDriveTask(contract, GetDestinationRoutePosition(contract), now);
                return;
            }

            if (!CanStartOriginPickup(contract, out waitStatus))
            {
                contract.StatusText = waitStatus;
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            contract.Phase = NpcRoutePhase.DrivingToOrigin;
            contract.NextDriveTaskRefreshMs = 0;
            contract.WaitUntilMs = 0;
            contract.StatusText = string.Format("Driving to {0}", contract.OriginIndustry.Name);
            EnsureDriveTask(contract, GetOriginRoutePosition(contract), now);
        }

        private void UpdateContract(NpcLogisticsContract contract, int now)
        {
            if (contract == null)
            {
                return;
            }

            if (!SyncContractToCurrentRoute(contract))
            {
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            if (!HasGameplayAccess(contract.OriginIndustry) || !HasGameplayAccess(contract.DestinationIndustry))
            {
                contract.StatusText = "Waiting for industry permits";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            if (!HasOperationalEntities(contract))
            {
                if (now < contract.WaitUntilMs)
                {
                    return;
                }

                string waitStatus;
                if (!CanStartOriginPickup(contract, out waitStatus))
                {
                    contract.StatusText = waitStatus;
                    contract.Phase = NpcRoutePhase.PendingSpawn;
                    contract.WaitUntilMs = now + RetryDelayMs;
                    return;
                }

                int nextAllowedSpawnMs;
                if (!CanAttemptContractSpawn(contract, now, out nextAllowedSpawnMs))
                {
                    contract.StatusText = string.Format(
                        "Spawn queue active at {0}; retrying in {1}s",
                        contract.OriginIndustry != null ? contract.OriginIndustry.Name : "origin",
                        ModFormatting.FormatNumber(Math.Max(0, nextAllowedSpawnMs - now) / 1000f));
                    contract.Phase = NpcRoutePhase.PendingSpawn;
                    contract.WaitUntilMs = nextAllowedSpawnMs;
                    return;
                }

                string spawnStatus;
                if (!TrySpawnRouteEntities(contract, out spawnStatus))
                {
                    contract.StatusText = spawnStatus;
                    contract.Phase = NpcRoutePhase.PendingSpawn;
                    contract.WaitUntilMs = now + RetryDelayMs;
                    return;
                }

                RegisterContractSpawn(contract, now);
                contract.StatusText = spawnStatus;

                contract.Phase = NpcRoutePhase.DrivingToOrigin;
                contract.NextDriveTaskRefreshMs = 0;
            }

            RefreshBlip(contract);

            if (contract.WaitUntilMs > now)
            {
                return;
            }

            if (contract.Phase == NpcRoutePhase.PendingSpawn)
            {
                contract.Phase = NpcRoutePhase.DrivingToOrigin;
            }

            switch (contract.Phase)
            {
                case NpcRoutePhase.DrivingToOrigin:
                    UpdateDriveToOrigin(contract, now);
                    break;
                case NpcRoutePhase.Loading:
                    CompleteLoading(contract, now);
                    break;
                case NpcRoutePhase.DrivingToDestination:
                    UpdateDriveToDestination(contract, now);
                    break;
                case NpcRoutePhase.Unloading:
                    CompleteUnloading(contract, now);
                    break;
                case NpcRoutePhase.ReturningToOffice:
                    UpdateReturnToOffice(contract, now);
                    break;
                case NpcRoutePhase.WaitingAtOffice:
                    UpdateWaitingAtOffice(contract, now);
                    break;
            }
        }

        private void UpdatePayroll(NpcLogisticsContract contract, int elapsedPayrollMinutes)
        {
            if (contract == null || contract.Tier == null || elapsedPayrollMinutes <= 0)
            {
                return;
            }

            contract.PayrollElapsedInGameMinutes += elapsedPayrollMinutes;

            var payrollCyclesDue = contract.PayrollElapsedInGameMinutes / InGameMinutesPerWeek;
            if (payrollCyclesDue <= 0)
            {
                return;
            }

            contract.PayrollElapsedInGameMinutes %= InGameMinutesPerWeek;

            var weeklyWage = GetWeeklyWage(contract.Tier);
            var totalCharge = weeklyWage * payrollCyclesDue;
            if (totalCharge > 0f && _deductProfit != null)
            {
                _deductProfit(totalCharge);
                RecordFinanceExpense(
                    CompanyFinanceCategory.NpcWages,
                    totalCharge,
                    string.Format("NPC payroll for {0}", BuildContractLabel(contract)),
                    contract.Id,
                    BuildContractLabel(contract));
            }

            contract.CompletedPayrollCycles += payrollCyclesDue;
            contract.TotalWeeklyWagesPaid += totalCharge;

            if (_showStatus != null && totalCharge > 0f)
            {
                _showStatus(string.Format(
                    "NPC payroll charged {0} for {1}.",
                    ModFormatting.FormatMoney(totalCharge),
                    BuildContractLabel(contract)));
            }
        }

        private void UpdateDriveToOrigin(NpcLogisticsContract contract, int now)
        {
            string waitStatus;
            if (!CanStartOriginPickup(contract, out waitStatus))
            {
                BeginReturnToOffice(contract, waitStatus, now);
                return;
            }

            var originPosition = GetOriginRoutePosition(contract);
            if (GetDriverVehicle(contract).Position.DistanceTo(originPosition) <= ArrivalDistance)
            {
                ClearPedTasks(contract.Driver);
                contract.Phase = NpcRoutePhase.Loading;
                contract.StatusText = "Loading cargo";
                contract.WaitUntilMs = now + LoadDelayMs;
                return;
            }

            EnsureDriveTask(contract, originPosition, now);
            contract.StatusText = string.Format("Driving to {0}", contract.OriginIndustry.Name);
        }

        private void CompleteLoading(NpcLogisticsContract contract, int now)
        {
            string waitStatus;
            if (!CanStartOriginPickup(contract, out waitStatus))
            {
                BeginReturnToOffice(contract, waitStatus, now);
                return;
            }

            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null)
            {
                contract.StatusText = "Waiting for cargo vehicle";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            var destinationCapacity = GetDestinationRemainingNeedTons(contract);
            var availableOriginStock = contract.OriginIndustry.GetStock(contract.Commodity);
            var loadTargetTons = Math.Min(cargoState.CapacityTons, Math.Min(destinationCapacity, availableOriginStock));
            if (loadTargetTons <= 0.001f)
            {
                BeginReturnToOffice(contract, availableOriginStock <= 0.001f ? "Waiting for origin stock" : "Waiting for destination trigger", now);
                return;
            }

            cargoState.ClearCargo();
            float loadedTons;
            if (!_industryManager.TryLoadCommodity(contract.OriginIndustry, cargoState.CargoType, contract.Commodity, loadTargetTons, out loadedTons) || loadedTons <= 0.001f)
            {
                contract.StatusText = "Waiting for origin stock";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            var lossRatio = (float)(_random.NextDouble() * Math.Max(0f, contract.Tier.CargoLossRate));
            if (_territoryManager != null)
            {
                lossRatio = _territoryManager.AdjustNpcLossRatio(contract.OriginIndustry, contract.DestinationIndustry, lossRatio);
            }

            var deliveredTons = Math.Max(0.1f, loadedTons * (1f - lossRatio));

            cargoState.Commodity = contract.Commodity;
            cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(contract.Commodity);
            cargoState.WeightTons = deliveredTons;
            cargoState.TotalLostTons += Math.Max(0f, loadedTons - deliveredTons);
            cargoState.CargoCondition = Math.Max(0.25f, 1f - lossRatio);
            cargoState.SourceIndustryId = contract.OriginIndustry != null ? contract.OriginIndustry.Id : string.Empty;
            cargoState.SourceDistrictName = contract.OriginIndustry != null ? contract.OriginIndustry.DistrictName : string.Empty;
            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            if (_territoryManager != null)
            {
                _territoryManager.RegisterLoad(contract.OriginIndustry, contract.Commodity, loadedTons, true);
            }

            if (_officeDeliveryNotificationsEnabled && _showStatus != null)
            {
                _showStatus(string.Format(
                    "NPC loaded {0} {1} at {2}.",
                    ModFormatting.FormatTons(deliveredTons),
                    contract.Commodity,
                    contract.OriginIndustry != null ? contract.OriginIndustry.Name : "origin"));
            }

            contract.LastJourneyLossRatio = lossRatio;
            contract.Phase = NpcRoutePhase.DrivingToDestination;
            contract.NextDriveTaskRefreshMs = 0;
            contract.WaitUntilMs = 0;
            contract.StatusText = string.Format("Delivering {0}", contract.Commodity);
            EnsureDriveTask(contract, GetDestinationRoutePosition(contract), now);
        }

        private void UpdateDriveToDestination(NpcLogisticsContract contract, int now)
        {
            string waitStatus;
            if (!CanResumeDestinationDelivery(contract, out waitStatus))
            {
                BeginReturnToOffice(contract, waitStatus, now);
                return;
            }

            var destinationPosition = GetDestinationRoutePosition(contract);
            if (GetDriverVehicle(contract).Position.DistanceTo(destinationPosition) <= ArrivalDistance)
            {
                ClearPedTasks(contract.Driver);
                contract.Phase = NpcRoutePhase.Unloading;
                contract.StatusText = "Unloading cargo";
                contract.WaitUntilMs = now + UnloadDelayMs;
                return;
            }

            EnsureDriveTask(contract, destinationPosition, now);
            contract.StatusText = string.Format("En route to {0}", contract.DestinationIndustry.Name);
        }

        private void CompleteUnloading(NpcLogisticsContract contract, int now)
        {
            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || cargoState.IsEmpty)
            {
                string waitStatus;
                if (!CanStartOriginPickup(contract, out waitStatus))
                {
                    BeginReturnToOffice(contract, waitStatus, now);
                    return;
                }

                contract.Phase = NpcRoutePhase.DrivingToOrigin;
                contract.NextDriveTaskRefreshMs = 0;
                contract.StatusText = string.Format("Returning to {0}", contract.OriginIndustry.Name);
                return;
            }

            var unloadTargetTons = Math.Min(cargoState.WeightTons, GetDestinationRemainingNeedTons(contract));
            if (unloadTargetTons <= 0.001f)
            {
                BeginReturnToOffice(contract, "Waiting for destination trigger", now);
                return;
            }

            float acceptedTons;
            if (!_industryManager.TryUnload(contract.DestinationIndustry, contract.Commodity, unloadTargetTons, out acceptedTons) || acceptedTons <= 0.001f)
            {
                BeginReturnToOffice(contract, "Waiting for destination capacity", now);
                return;
            }

            var revenue = _industryManager.ComputeDeliveryProfit(contract.DestinationIndustry, contract.Commodity, acceptedTons, _globalMarket, now);
            if (_territoryManager != null)
            {
                revenue = _territoryManager.AdjustDeliveryRevenue(contract.DestinationIndustry, contract.Commodity, acceptedTons, revenue);
                _territoryManager.RegisterDelivery(
                    contract.DestinationIndustry,
                    contract.Commodity,
                    acceptedTons,
                    true,
                    contract.OriginIndustry != null ? contract.OriginIndustry.Id : cargoState.SourceIndustryId,
                    contract.OriginIndustry != null ? contract.OriginIndustry.DistrictName : cargoState.SourceDistrictName);
            }

            var creditedRevenue = Math.Max(0f, revenue);
            if (_addProfit != null && creditedRevenue > 0f)
            {
                _addProfit(creditedRevenue);
                RecordFinanceIncome(
                    CompanyFinanceCategory.NpcDelivery,
                    creditedRevenue,
                    string.Format("NPC delivery to {0}", contract.DestinationIndustry != null ? contract.DestinationIndustry.Name : "destination"),
                    contract.Id,
                    BuildContractLabel(contract));
            }

            if (_officeDeliveryNotificationsEnabled && _showStatus != null)
            {
                _showStatus(string.Format(
                    "NPC unloaded {0} {1} at {2}. Earned {3}.",
                    ModFormatting.FormatTons(acceptedTons),
                    contract.Commodity,
                    contract.DestinationIndustry != null ? contract.DestinationIndustry.Name : "destination",
                    ModFormatting.FormatMoney(creditedRevenue)));
            }

            contract.TotalDeliveredTons += acceptedTons;

            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - acceptedTons);
            if (cargoState.WeightTons <= 0.001f)
            {
                cargoState.ClearCargo();
                _fleetManager.ClearCargoVisuals(cargoState);
                contract.CompletedDeliveries += 1;
                contract.TotalProfitEarned += creditedRevenue;
                if (contract.Routes.Count > 1)
                {
                    var previousCommodity = contract.Commodity;
                    AdvanceToNextRoute(contract);
                    if (!string.Equals(previousCommodity, contract.Commodity, StringComparison.OrdinalIgnoreCase))
                    {
                        BeginReturnToOffice(
                            contract,
                            string.Format("Switching to route {0}/{1}", contract.CurrentRouteIndex + 1, contract.Routes.Count),
                            now);
                        return;
                    }

                    string nextRouteWaitStatus;
                    if (!CanStartOriginPickup(contract, out nextRouteWaitStatus))
                    {
                        BeginReturnToOffice(contract, nextRouteWaitStatus, now);
                        return;
                    }

                    contract.Phase = NpcRoutePhase.DrivingToOrigin;
                    contract.StatusText = string.Format("Queued route {0}/{1}.", contract.CurrentRouteIndex + 1, contract.Routes.Count);
                    contract.NextDriveTaskRefreshMs = 0;
                    contract.WaitUntilMs = now + 1000;
                    return;
                }

                string waitStatus;
                if (!CanStartOriginPickup(contract, out waitStatus))
                {
                    BeginReturnToOffice(contract, waitStatus, now);
                }
                else
                {
                    contract.Phase = NpcRoutePhase.DrivingToOrigin;
                    contract.StatusText = string.Format(
                        "Delivered {0} {1}",
                        ModFormatting.FormatTons(acceptedTons),
                        contract.Commodity);
                    contract.NextDriveTaskRefreshMs = 0;
                    contract.WaitUntilMs = now + 1000;
                }
                return;
            }

            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            string destinationWaitStatus;
            if (!CanResumeDestinationDelivery(contract, out destinationWaitStatus))
            {
                BeginReturnToOffice(contract, destinationWaitStatus, now);
                return;
            }

            contract.StatusText = "Partially unloaded cargo";
            contract.WaitUntilMs = now + RetryDelayMs;
        }

        private bool TrySpawnRouteEntities(NpcLogisticsContract contract, out string message)
        {
            message = string.Empty;
            if (contract == null || contract.VehicleDefinition == null || contract.Tier == null)
            {
                message = "Route configuration is incomplete.";
                return false;
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            if (!_fleetManager.SpawnSelectedVehicle(
                contract.VehicleDefinition,
                contract.TractorDefinition,
                _getGroundPosition(GetOriginSpawnPosition(contract)),
                GetOriginSpawnHeading(contract),
                out truck,
                out cargoVehicle,
                out message))
            {
                return false;
            }

            var driver = CreateDriverPed(contract, truck);
            if (driver == null || !driver.Exists())
            {
                if (cargoVehicle != null && cargoVehicle.Exists())
                {
                    cargoVehicle.Delete();
                }

                if (truck != null && truck.Exists())
                {
                    truck.Delete();
                }

                message = "Failed to create NPC driver.";
                return false;
            }

            contract.Truck = truck;
            contract.CargoVehicle = cargoVehicle;
            contract.Driver = driver;
            if (contract.CargoVehicle != null && contract.CargoVehicle.Exists())
            {
                contract.CargoVehicle.IsPersistent = true;
            }

            contract.RouteBlip = CreateRouteBlip(contract);
            contract.StatusText = string.Format("Spawned {0}", contract.Tier.DisplayName);

            var cargoState = _fleetManager.GetOrCreateCargoState(GetCargoVehicle(contract));
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(contract.Commodity);
            }

            return true;
        }

        private Ped CreateDriverPed(NpcLogisticsContract contract, Vehicle truck)
        {
            if (truck == null || !truck.Exists())
            {
                return null;
            }

            var modelName = contract.Tier != null ? contract.Tier.NpcModel : DefaultNpcModel;
            var model = new Model(modelName);
            model.Request(1000);
            if (!model.IsLoaded)
            {
                model = new Model(DefaultNpcModel);
                model.Request(1000);
            }

            if (!model.IsLoaded)
            {
                return null;
            }

            var pedHandle = Function.Call<int>(Hash.CREATE_PED_INSIDE_VEHICLE, truck.Handle, 26, model.Hash, -1, true, true);
            var driver = Entity.FromHandle(pedHandle) as Ped;
            if (driver == null || !driver.Exists())
            {
                return null;
            }

            driver.IsPersistent = true;
            truck.IsPersistent = true;

            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, driver.Handle, true);
            Function.Call(Hash.SET_PED_KEEP_TASK, driver.Handle, true);
            Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, driver.Handle, false);
            Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, Math.Max(0f, Math.Min(1f, contract.Tier.SpeedMultiplier)));
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, Math.Max(0f, Math.Min(1f, contract.Tier.SpeedMultiplier)));
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, truck.Handle, true, true, false);
            return driver;
        }

        private void EnsureDriveTask(NpcLogisticsContract contract, Vector3 targetPosition, int now)
        {
            if (contract == null || contract.Driver == null || !contract.Driver.Exists())
            {
                return;
            }

            if (contract.Truck == null || !contract.Truck.Exists())
            {
                return;
            }

            if (now < contract.NextDriveTaskRefreshMs)
            {
                return;
            }

            Function.Call(
                Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                contract.Driver.Handle,
                contract.Truck.Handle,
                targetPosition.X,
                targetPosition.Y,
                targetPosition.Z,
                Math.Max(8f, BaseDriveSpeed * Math.Max(0.1f, contract.Tier.SpeedMultiplier)),
                DriveStyle,
                ArrivalDistance * 0.5f);
            contract.NextDriveTaskRefreshMs = now + DriveTaskRefreshIntervalMs;
        }

        private void ClearPedTasks(Ped driver)
        {
            if (driver == null || !driver.Exists())
            {
                return;
            }

            Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
        }

        private Blip CreateRouteBlip(NpcLogisticsContract contract)
        {
            var driverVehicle = GetDriverVehicle(contract);
            if (driverVehicle == null || !driverVehicle.Exists())
            {
                return null;
            }

            var blip = World.CreateBlip(driverVehicle.Position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = BlipSprite.Truck;
            blip.Color = BlipColor.Blue;
            blip.Name = string.Format("NPC Route: {0}", BuildContractLabel(contract));
            blip.Scale = 0.85f;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private void RefreshBlip(NpcLogisticsContract contract)
        {
            var driverVehicle = GetDriverVehicle(contract);
            if (driverVehicle == null || !driverVehicle.Exists())
            {
                if (contract.RouteBlip != null && contract.RouteBlip.Exists())
                {
                    contract.RouteBlip.Delete();
                }

                contract.RouteBlip = null;
                return;
            }

            if (contract.RouteBlip == null || !contract.RouteBlip.Exists())
            {
                contract.RouteBlip = CreateRouteBlip(contract);
            }

            if (contract.RouteBlip != null && contract.RouteBlip.Exists())
            {
                contract.RouteBlip.Position = driverVehicle.Position;
            }
        }

        private bool HasOperationalEntities(NpcLogisticsContract contract)
        {
            return contract != null
                && contract.Driver != null
                && contract.Driver.Exists()
                && contract.Driver.Health > 0
                && contract.Truck != null
                && contract.Truck.Exists()
                && GetCargoVehicle(contract) != null
                && GetCargoVehicle(contract).Exists();
        }

        private bool CanAttemptContractSpawn(NpcLogisticsContract contract, int now, out int nextAllowedSpawnMs)
        {
            nextAllowedSpawnMs = now;
            var originId = contract != null && contract.OriginIndustry != null
                ? (contract.OriginIndustry.Id ?? string.Empty).Trim()
                : string.Empty;
            if (string.IsNullOrWhiteSpace(originId))
            {
                return true;
            }

            int lastSpawnMs;
            if (!_lastContractSpawnMsByOriginId.TryGetValue(originId, out lastSpawnMs))
            {
                return true;
            }

            nextAllowedSpawnMs = lastSpawnMs + SpawnStaggerDelayMs;
            return now >= nextAllowedSpawnMs;
        }

        private void RegisterContractSpawn(NpcLogisticsContract contract, int now)
        {
            var originId = contract != null && contract.OriginIndustry != null
                ? (contract.OriginIndustry.Id ?? string.Empty).Trim()
                : string.Empty;
            if (string.IsNullOrWhiteSpace(originId))
            {
                return;
            }

            _lastContractSpawnMsByOriginId[originId] = now;
        }

        private void CleanupContractEntities(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return;
            }

            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            if (contract.RouteBlip != null && contract.RouteBlip.Exists())
            {
                contract.RouteBlip.Delete();
            }

            if (contract.Driver != null && contract.Driver.Exists())
            {
                contract.Driver.Delete();
            }

            if (contract.CargoVehicle != null && contract.CargoVehicle.Exists())
            {
                contract.CargoVehicle.Delete();
            }

            if (contract.Truck != null && contract.Truck.Exists() && (contract.CargoVehicle == null || contract.Truck.Handle != contract.CargoVehicle.Handle))
            {
                contract.Truck.Delete();
            }

            contract.Driver = null;
            contract.Truck = null;
            contract.CargoVehicle = null;
            contract.RouteBlip = null;
            contract.Phase = NpcRoutePhase.PendingSpawn;
            contract.WaitUntilMs = 0;
            contract.NextDriveTaskRefreshMs = 0;
        }

        private bool CanUseAsOrigin(Industry industry)
        {
            return industry != null
                && industry.Outputs != null
                && industry.Outputs.Count > 0
                && HasGameplayAccess(industry)
                && MeetsDistrictNpcRequirement(industry);
        }

        private bool CanUseAsDestination(Industry originIndustry, Industry destinationIndustry)
        {
            string reason;
            return destinationIndustry != null
                && originIndustry != null
                && !string.Equals(originIndustry.Id, destinationIndustry.Id, StringComparison.OrdinalIgnoreCase)
                && HasGameplayAccess(destinationIndustry)
                && MeetsDistrictNpcRequirement(destinationIndustry)
                && GetResourceOptions(originIndustry, destinationIndustry).Count > 0
                && (_territoryManager == null || _territoryManager.CanCreateNpcRouteWithPermits(originIndustry, destinationIndustry, out reason));
        }

        private bool HasGameplayAccess(Industry industry)
        {
            return industry != null && !_industryManager.RequiresContractorPermit(industry);
        }

        private bool MeetsDistrictNpcRequirement(Industry industry)
        {
            return industry != null
                && (_territoryManager == null || _territoryManager.IsDistrictEstablishedForNpc(industry.DistrictName));
        }

        private List<Industry> GetOriginAvailabilityCandidates()
        {
            return _industryManager.Industries
                .Where(industry => industry != null && industry.Outputs != null && industry.Outputs.Count > 0)
                .OrderByDescending(GetAvailabilityPriority)
                .ThenBy(industry => industry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<Industry> GetDestinationAvailabilityCandidates(Industry originIndustry)
        {
            if (originIndustry == null)
            {
                return new List<Industry>();
            }

            return _industryManager.Industries
                .Where(destinationIndustry =>
                    destinationIndustry != null
                    && !string.Equals(originIndustry.Id, destinationIndustry.Id, StringComparison.OrdinalIgnoreCase)
                    && GetResourceOptions(originIndustry, destinationIndustry).Count > 0)
                .OrderByDescending(GetAvailabilityPriority)
                .ThenBy(destinationIndustry => destinationIndustry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string BuildOriginAvailabilityMessage(Industry industry)
        {
            var blocker = BuildAutomationBlockReason(industry);
            return string.IsNullOrWhiteSpace(blocker)
                ? string.Empty
                : string.Format("{0}: {1}", industry.Name, blocker.TrimEnd('.'));
        }

        private string BuildDestinationAvailabilityMessage(Industry originIndustry, Industry destinationIndustry)
        {
            var blocker = BuildAutomationBlockReason(destinationIndustry);
            if (!string.IsNullOrWhiteSpace(blocker))
            {
                return string.Format("{0}: {1}", destinationIndustry.Name, blocker.TrimEnd('.'));
            }

            if (_territoryManager == null)
            {
                return string.Empty;
            }

            string routeReason;
            if (_territoryManager.CanCreateNpcRouteWithPermits(originIndustry, destinationIndustry, out routeReason))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(routeReason)
                ? string.Empty
                : string.Format("{0}: {1}", destinationIndustry.Name, routeReason.TrimEnd('.'));
        }

        private string BuildAutomationBlockReason(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (_industryManager.RequiresContractorPermit(industry))
            {
                return "Purchase contractor permit";
            }

            if (_territoryManager != null)
            {
                var districtBlocker = _territoryManager.GetNpcDistrictRequirementSummary(industry.DistrictName);
                if (!string.IsNullOrWhiteSpace(districtBlocker))
                {
                    return districtBlocker;
                }
            }

            return string.Empty;
        }

        private int GetAvailabilityPriority(Industry industry)
        {
            if (industry == null)
            {
                return 0;
            }

            var score = 0;
            if (industry.HasContractorPermit)
            {
                score += 2;
            }

            if (_territoryManager != null)
            {
                var siteState = _territoryManager.GetSiteState(industry);
                if (siteState != null && (siteState.LoadRuns > 0 || siteState.UnloadRuns > 0 || siteState.TotalDeliveries > 0))
                {
                    score += 1;
                }
            }

            return score;
        }

        private Industry FindIndustryById(string industryId)
        {
            if (string.IsNullOrWhiteSpace(industryId) || _industryManager.Industries == null)
            {
                return null;
            }

            return _industryManager.Industries.FirstOrDefault(industry =>
                industry != null &&
                string.Equals(industry.Id, industryId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private NpcDriverTierDefinition FindDriverTier(string tierId)
        {
            if (string.IsNullOrWhiteSpace(tierId))
            {
                return null;
            }

            return _driverTiers.FirstOrDefault(tier =>
                tier != null &&
                string.Equals(tier.Id, tierId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private bool ShouldIncludeAssignableVehicle(
            OwnedCommercialVehiclePersistenceEntry vehicle,
            string activeOfficeId,
            string commodity,
            ISet<string> includeAssignedAssetIds,
            NpcLogisticsContract includeContract)
        {
            if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.AssetId))
            {
                return false;
            }

            var isCurrentAssignment = includeAssignedAssetIds != null && includeAssignedAssetIds.Contains(vehicle.AssetId);
            if (!isCurrentAssignment)
            {
                if (vehicle.IsDeployed || !vehicle.InActiveGarage)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(activeOfficeId)
                    && !string.Equals(vehicle.AssignedOfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (IsVehicleAssignedToAnotherContract(vehicle.AssetId, includeAssignedAssetIds, includeContract))
                {
                    return false;
                }
            }

            return CanGarageVehicleCarryCommodity(vehicle, commodity);
        }

        private bool IsVehicleAssignedToAnotherContract(string assetId, ISet<string> includeAssignedAssetIds, NpcLogisticsContract includeContract)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return false;
            }

            for (int i = 0; i < _contracts.Count; i++)
            {
                var contract = _contracts[i];
                if (contract == null)
                {
                    continue;
                }

                if (includeContract != null && contract.Id == includeContract.Id)
                {
                    continue;
                }

                var contractAssignedAssetIds = GetContractAssignedVehicleAssetIds(contract);
                if (!contractAssignedAssetIds.Contains(assetId))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool CanGarageVehicleCarryCommodity(OwnedCommercialVehiclePersistenceEntry vehicle, string commodity)
        {
            if (vehicle == null || _fleetManager == null)
            {
                return false;
            }

            var poweredDefinition = _fleetManager.FindDefinitionByModelName(vehicle.PoweredModelName);
            var cargoDefinition = _fleetManager.FindDefinitionByModelName(vehicle.CargoModelName);
            if (vehicle.HasSeparateCargoVehicle)
            {
                if (poweredDefinition == null || !poweredDefinition.IsTractor)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(commodity))
                {
                    return true;
                }

                if (cargoDefinition != null && cargoDefinition.IsTrailer && _fleetManager.CanDefinitionCarryCommodity(cargoDefinition, commodity))
                {
                    return true;
                }

                VehicleDefinition trailerDefinition;
                return TryResolveTrailerForCommodity(commodity, out trailerDefinition, out _);
            }

            if (poweredDefinition != null && poweredDefinition.IsTractor)
            {
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    return true;
                }

                VehicleDefinition trailerDefinition;
                return TryResolveTrailerForCommodity(commodity, out trailerDefinition, out _);
            }

            if (cargoDefinition == null || cargoDefinition.IsTrailer)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(commodity) || _fleetManager.CanDefinitionCarryCommodity(cargoDefinition, commodity);
        }

        private OwnedCommercialVehiclePersistenceEntry FindGarageVehicleEntry(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId) || _getCommercialVehicles == null)
            {
                return null;
            }

            var allVehicles = _getCommercialVehicles();
            if (allVehicles == null || allVehicles.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < allVehicles.Count; i++)
            {
                var vehicle = allVehicles[i];
                if (vehicle != null && string.Equals(vehicle.AssetId, assetId, StringComparison.OrdinalIgnoreCase))
                {
                    return vehicle;
                }
            }

            return null;
        }

        private bool TryResolveAssignedVehicleForCommodity(
            string assetId,
            string commodity,
            out OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            out VehicleDefinition selectedVehicle,
            out VehicleDefinition selectedTractor)
        {
            string failureReason;
            return TryResolveAssignedVehicleForCommodity(assetId, commodity, out assignedVehicle, out selectedVehicle, out selectedTractor, out failureReason);
        }

        private bool TryResolveAssignedVehicleForCommodity(
            string assetId,
            string commodity,
            out OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            out VehicleDefinition selectedVehicle,
            out VehicleDefinition selectedTractor,
            out string failureReason)
        {
            assignedVehicle = FindGarageVehicleEntry(assetId);
            selectedVehicle = null;
            selectedTractor = null;
            failureReason = string.Empty;

            if (assignedVehicle == null)
            {
                failureReason = "Assigned truck is no longer available in your fleet.";
                return false;
            }

            if (!CanGarageVehicleCarryCommodity(assignedVehicle, commodity))
            {
                failureReason = string.Format("{0} cannot haul {1}.", BuildAssignedVehicleDisplayName(assignedVehicle), CommodityCatalog.Normalize(commodity));
                return false;
            }

            var poweredDefinition = _fleetManager != null ? _fleetManager.FindDefinitionByModelName(assignedVehicle.PoweredModelName) : null;
            var cargoDefinition = _fleetManager != null ? _fleetManager.FindDefinitionByModelName(assignedVehicle.CargoModelName) : null;
            if (assignedVehicle.HasSeparateCargoVehicle)
            {
                if (poweredDefinition == null || !poweredDefinition.IsTractor)
                {
                    failureReason = string.Format("{0} has an invalid truck or trailer setup.", BuildAssignedVehicleDisplayName(assignedVehicle));
                    return false;
                }

                if (string.IsNullOrWhiteSpace(commodity))
                {
                    selectedVehicle = cargoDefinition;
                    selectedTractor = poweredDefinition;
                    return true;
                }

                if (cargoDefinition != null && cargoDefinition.IsTrailer && _fleetManager.CanDefinitionCarryCommodity(cargoDefinition, commodity))
                {
                    selectedVehicle = cargoDefinition;
                    selectedTractor = poweredDefinition;
                    return true;
                }

                VehicleDefinition trailerDefinition;
                if (!TryResolveTrailerForCommodity(commodity, out trailerDefinition, out failureReason))
                {
                    return false;
                }

                selectedVehicle = trailerDefinition;
                selectedTractor = poweredDefinition;
                return true;
            }

            if (poweredDefinition != null && poweredDefinition.IsTractor)
            {
                VehicleDefinition trailerDefinition;
                if (!TryResolveTrailerForCommodity(commodity, out trailerDefinition, out failureReason))
                {
                    return false;
                }

                selectedVehicle = trailerDefinition;
                selectedTractor = poweredDefinition;
                return true;
            }

            if (cargoDefinition == null || cargoDefinition.IsTrailer || !_fleetManager.CanDefinitionCarryCommodity(cargoDefinition, commodity))
            {
                failureReason = string.Format("{0} cannot haul {1}.", BuildAssignedVehicleDisplayName(assignedVehicle), CommodityCatalog.Normalize(commodity));
                return false;
            }

            selectedVehicle = cargoDefinition;
            selectedTractor = null;
            return true;
        }

        private bool TryResolveTrailerForCommodity(string commodity, out VehicleDefinition trailerDefinition, out string failureReason)
        {
            trailerDefinition = null;
            failureReason = string.Empty;

            if (_fleetManager == null)
            {
                failureReason = "Fleet manager unavailable";
                return false;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            trailerDefinition = _fleetManager
                .GetSpawnableForCommodity(normalizedCommodity)
                .Where(definition => definition.IsTrailer)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();
            if (trailerDefinition == null)
            {
                failureReason = string.Format("No enabled trailer can carry {0}.", normalizedCommodity);
                return false;
            }

            return true;
        }

        private IReadOnlyList<NpcLogisticsRouteDefinition> ApplyFallbackAssignedVehicle(
            IReadOnlyList<NpcLogisticsRouteDefinition> routes,
            OwnedCommercialVehiclePersistenceEntry assignedVehicle)
        {
            if (routes == null || routes.Count == 0 || assignedVehicle == null || string.IsNullOrWhiteSpace(assignedVehicle.AssetId))
            {
                return routes;
            }

            var displayName = BuildAssignedVehicleDisplayName(assignedVehicle);
            return routes
                .Select(route => route == null
                    ? null
                    : new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = route.OriginIndustry,
                        DestinationIndustry = route.DestinationIndustry,
                        Commodity = route.Commodity,
                        AssignedVehicleAssetId = string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId) ? assignedVehicle.AssetId : route.AssignedVehicleAssetId,
                        AssignedVehicleDisplayName = string.IsNullOrWhiteSpace(route.AssignedVehicleDisplayName) ? displayName : route.AssignedVehicleDisplayName,
                        OriginTriggerThresholdPercent = route.OriginTriggerThresholdPercent,
                        DestinationTriggerThresholdPercent = route.DestinationTriggerThresholdPercent,
                    })
                .ToList();
        }

        private bool TryResolveRouteAssignedVehicle(
            string assetId,
            ISet<string> currentContractAssignedAssetIds,
            NpcLogisticsContract currentContract,
            out OwnedCommercialVehiclePersistenceEntry assignedVehicle,
            out string message)
        {
            assignedVehicle = FindGarageVehicleEntry(assetId);
            message = string.Empty;

            if (assignedVehicle == null)
            {
                message = string.IsNullOrWhiteSpace(assetId)
                    ? "Assign a truck from the active office garage first."
                    : "Assigned truck is no longer available in your fleet.";
                return false;
            }

            if (IsVehicleAssignedToAnotherContract(assignedVehicle.AssetId, currentContractAssignedAssetIds, currentContract))
            {
                message = "That truck is already assigned to another NPC route.";
                return false;
            }

            var keepingCurrentAssignment = currentContractAssignedAssetIds != null && currentContractAssignedAssetIds.Contains(assignedVehicle.AssetId);
            if (!keepingCurrentAssignment)
            {
                var activeOfficeId = _getActiveOfficeId != null ? _getActiveOfficeId() ?? string.Empty : string.Empty;
                if (assignedVehicle.IsDeployed || !assignedVehicle.InActiveGarage)
                {
                    message = "Move the selected truck into the active office garage before assigning it to an NPC route.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(activeOfficeId)
                    && !string.Equals(assignedVehicle.AssignedOfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Only trucks assigned to the active office garage can be reserved by NPC routes.";
                    return false;
                }
            }

            return true;
        }

        private static HashSet<string> BuildAssignedVehicleAssetIdSet(IEnumerable<string> assetIds)
        {
            var normalizedAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (assetIds == null)
            {
                return normalizedAssetIds;
            }

            foreach (var assetId in assetIds)
            {
                if (!string.IsNullOrWhiteSpace(assetId))
                {
                    normalizedAssetIds.Add(assetId.Trim());
                }
            }

            return normalizedAssetIds;
        }

        private HashSet<string> GetContractAssignedVehicleAssetIds(NpcLogisticsContract contract)
        {
            var assignedAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (contract == null)
            {
                return assignedAssetIds;
            }

            if (contract.Routes != null)
            {
                for (int i = 0; i < contract.Routes.Count; i++)
                {
                    var route = contract.Routes[i];
                    if (route != null && !string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId))
                    {
                        assignedAssetIds.Add(route.AssignedVehicleAssetId.Trim());
                    }
                }
            }

            if (assignedAssetIds.Count == 0 && !string.IsNullOrWhiteSpace(contract.AssignedVehicleAssetId))
            {
                assignedAssetIds.Add(contract.AssignedVehicleAssetId.Trim());
            }

            return assignedAssetIds;
        }

        private void UpdateContractAssignedVehicleSummary(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return;
            }

            var currentRoute = GetCurrentRoute(contract);
            contract.AssignedVehicleAssetId = currentRoute != null && !string.IsNullOrWhiteSpace(currentRoute.AssignedVehicleAssetId)
                ? currentRoute.AssignedVehicleAssetId
                : GetContractAssignedVehicleAssetIds(contract).FirstOrDefault() ?? string.Empty;

            var routeVehicleNames = contract.Routes
                .Where(route => route != null && !string.IsNullOrWhiteSpace(route.AssignedVehicleDisplayName))
                .Select(route => route.AssignedVehicleDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (routeVehicleNames.Count == 0)
            {
                contract.AssignedVehicleDisplayName = string.Empty;
            }
            else if (routeVehicleNames.Count == 1)
            {
                contract.AssignedVehicleDisplayName = routeVehicleNames[0];
            }
            else
            {
                contract.AssignedVehicleDisplayName = string.Format("{0} + {1} more", routeVehicleNames[0], routeVehicleNames.Count - 1);
            }
        }

        private string BuildAssignedVehicleDisplayName(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return "Unassigned";
            }

            if (!string.IsNullOrWhiteSpace(vehicle.DisplayName))
            {
                return vehicle.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(vehicle.PoweredModelName) && !string.IsNullOrWhiteSpace(vehicle.CargoModelName)
                && !string.Equals(vehicle.PoweredModelName, vehicle.CargoModelName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0} + {1}", vehicle.PoweredModelName, vehicle.CargoModelName);
            }

            return !string.IsNullOrWhiteSpace(vehicle.PoweredModelName)
                ? vehicle.PoweredModelName
                : (vehicle.CargoModelName ?? "Assigned truck");
        }

        private bool TryNormalizeRouteDefinitions(IReadOnlyList<NpcLogisticsRouteDefinition> routeSelections, out List<NpcLogisticsRouteDefinition> normalizedRoutes, out string message)
        {
            normalizedRoutes = new List<NpcLogisticsRouteDefinition>();
            message = string.Empty;

            if (_routeLimit <= 0)
            {
                message = "Hiring NPC is disabled for this save.";
                return false;
            }

            if (routeSelections == null || routeSelections.Count == 0)
            {
                message = "Configure at least one route first.";
                return false;
            }

            if (routeSelections.Count > _routeLimit)
            {
                message = string.Format("A hired NPC can have at most {0} route{1}.", _routeLimit, _routeLimit == 1 ? string.Empty : "s");
                return false;
            }

            for (int routeIndex = 0; routeIndex < routeSelections.Count; routeIndex++)
            {
                var route = routeSelections[routeIndex];
                if (route == null || route.OriginIndustry == null)
                {
                    message = string.Format("Select a starting point for route {0}.", routeIndex + 1);
                    return false;
                }

                if (route.DestinationIndustry == null)
                {
                    message = string.Format("Select a destination for route {0}.", routeIndex + 1);
                    return false;
                }

                if (string.Equals(route.OriginIndustry.Id, route.DestinationIndustry.Id, StringComparison.OrdinalIgnoreCase))
                {
                    message = string.Format("Route {0} must use different origin and destination industries.", routeIndex + 1);
                    return false;
                }

                if (!HasGameplayAccess(route.OriginIndustry) || !HasGameplayAccess(route.DestinationIndustry))
                {
                    message = string.Format("Route {0} requires industry permits first.", routeIndex + 1);
                    return false;
                }

                var originBlocker = BuildAutomationBlockReason(route.OriginIndustry);
                if (!string.IsNullOrWhiteSpace(originBlocker))
                {
                    message = string.Format("Route {0}: {1}: {2}", routeIndex + 1, route.OriginIndustry.Name, originBlocker.TrimEnd('.'));
                    return false;
                }

                var destinationBlocker = BuildAutomationBlockReason(route.DestinationIndustry);
                if (!string.IsNullOrWhiteSpace(destinationBlocker))
                {
                    message = string.Format("Route {0}: {1}: {2}", routeIndex + 1, route.DestinationIndustry.Name, destinationBlocker.TrimEnd('.'));
                    return false;
                }

                var normalizedCommodity = CommodityCatalog.Normalize(route.Commodity);
                if (string.IsNullOrWhiteSpace(normalizedCommodity))
                {
                    message = string.Format("Select a resource for route {0}.", routeIndex + 1);
                    return false;
                }

                var validResources = GetResourceOptions(route.OriginIndustry, route.DestinationIndustry);
                if (!validResources.Contains(normalizedCommodity, StringComparer.OrdinalIgnoreCase))
                {
                    message = string.Format("Route {0}: the selected resource cannot be transported on that route.", routeIndex + 1);
                    return false;
                }

                normalizedRoutes.Add(new NpcLogisticsRouteDefinition
                {
                    OriginIndustry = route.OriginIndustry,
                    DestinationIndustry = route.DestinationIndustry,
                    Commodity = normalizedCommodity,
                    AssignedVehicleAssetId = route.AssignedVehicleAssetId != null ? route.AssignedVehicleAssetId.Trim() : string.Empty,
                    AssignedVehicleDisplayName = route.AssignedVehicleDisplayName != null ? route.AssignedVehicleDisplayName.Trim() : string.Empty,
                    OriginTriggerThresholdPercent = ClampTriggerPercent(route.OriginTriggerThresholdPercent, 0),
                    DestinationTriggerThresholdPercent = ClampTriggerPercent(route.DestinationTriggerThresholdPercent, 100),
                });
            }

            return true;
        }

        private NpcLogisticsRouteDefinition GetCurrentRoute(NpcLogisticsContract contract)
        {
            if (contract == null || contract.Routes.Count == 0)
            {
                return null;
            }

            var clampedIndex = Math.Max(0, Math.Min(contract.Routes.Count - 1, contract.CurrentRouteIndex));
            return contract.Routes[clampedIndex];
        }

        private void SetContractRoutes(NpcLogisticsContract contract, IEnumerable<NpcLogisticsRouteDefinition> routes, int currentRouteIndex = 0)
        {
            if (contract == null)
            {
                return;
            }

            contract.Routes.Clear();
            if (routes != null)
            {
                foreach (var route in routes)
                {
                    if (route == null || route.OriginIndustry == null || route.DestinationIndustry == null || string.IsNullOrWhiteSpace(route.Commodity))
                    {
                        continue;
                    }

                    contract.Routes.Add(new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = route.OriginIndustry,
                        DestinationIndustry = route.DestinationIndustry,
                        Commodity = CommodityCatalog.Normalize(route.Commodity),
                        AssignedVehicleAssetId = route.AssignedVehicleAssetId != null ? route.AssignedVehicleAssetId.Trim() : string.Empty,
                        AssignedVehicleDisplayName = route.AssignedVehicleDisplayName != null ? route.AssignedVehicleDisplayName.Trim() : string.Empty,
                        OriginTriggerThresholdPercent = ClampTriggerPercent(route.OriginTriggerThresholdPercent, 0),
                        DestinationTriggerThresholdPercent = ClampTriggerPercent(route.DestinationTriggerThresholdPercent, 100),
                    });
                }
            }

            contract.CurrentRouteIndex = contract.Routes.Count == 0
                ? 0
                : Math.Max(0, Math.Min(contract.Routes.Count - 1, currentRouteIndex));
            SyncContractToCurrentRoute(contract);
        }

        private bool SyncContractToCurrentRoute(NpcLogisticsContract contract)
        {
            var route = GetCurrentRoute(contract);
            if (route == null)
            {
                contract.OriginIndustry = null;
                contract.DestinationIndustry = null;
                contract.Commodity = string.Empty;
                contract.VehicleDefinition = null;
                contract.TractorDefinition = null;
                return false;
            }

            contract.CurrentRouteIndex = Math.Max(0, Math.Min(contract.Routes.Count - 1, contract.CurrentRouteIndex));
            contract.OriginIndustry = route.OriginIndustry;
            contract.DestinationIndustry = route.DestinationIndustry;
            contract.Commodity = route.Commodity;
            contract.OriginTriggerThresholdPercent = route.OriginTriggerThresholdPercent;
            contract.DestinationTriggerThresholdPercent = route.DestinationTriggerThresholdPercent;

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            var assignedVehicleAssetId = !string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId)
                ? route.AssignedVehicleAssetId
                : contract.AssignedVehicleAssetId;
            if (string.IsNullOrWhiteSpace(assignedVehicleAssetId))
            {
                if (!TryResolveVehicleForCommodity(route.Commodity, out selectedVehicle, out selectedTractor))
                {
                    contract.VehicleDefinition = null;
                    contract.TractorDefinition = null;
                    contract.StatusText = string.Format("No spawnable vehicle is configured for {0}.", route.Commodity);
                    return false;
                }
            }
            else
            {
                OwnedCommercialVehiclePersistenceEntry assignedVehicle;
                string failureReason;
                if (!TryResolveAssignedVehicleForCommodity(assignedVehicleAssetId, route.Commodity, out assignedVehicle, out selectedVehicle, out selectedTractor, out failureReason))
                {
                    contract.VehicleDefinition = null;
                    contract.TractorDefinition = null;
                    contract.StatusText = failureReason;
                    return false;
                }

                if (assignedVehicle != null)
                {
                    route.AssignedVehicleAssetId = assignedVehicle.AssetId;
                    route.AssignedVehicleDisplayName = BuildAssignedVehicleDisplayName(assignedVehicle);
                }
            }

            contract.VehicleDefinition = selectedVehicle;
            contract.TractorDefinition = selectedTractor;
            UpdateContractAssignedVehicleSummary(contract);
            return true;
        }

        private void AdvanceToNextRoute(NpcLogisticsContract contract)
        {
            if (contract == null || contract.Routes.Count == 0)
            {
                return;
            }

            contract.CurrentRouteIndex = (contract.CurrentRouteIndex + 1) % contract.Routes.Count;
            SyncContractToCurrentRoute(contract);
        }

        private bool TryResolveVehicleForCommodity(string commodity, out VehicleDefinition selectedVehicle, out VehicleDefinition selectedTractor)
        {
            string failureReason;
            return TryResolveVehicleForCommodity(commodity, out selectedVehicle, out selectedTractor, out failureReason);
        }

        private bool TryResolveVehicleForCommodity(string commodity, out VehicleDefinition selectedVehicle, out VehicleDefinition selectedTractor, out string failureReason)
        {
            selectedVehicle = null;
            selectedTractor = null;
            failureReason = string.Empty;

            if (_fleetManager == null)
            {
                failureReason = "Fleet manager unavailable";
                return false;
            }

            commodity = CommodityCatalog.Normalize(commodity);

            selectedVehicle = _fleetManager
                .GetSpawnableForCommodity(commodity)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();
            if (selectedVehicle == null)
            {
                failureReason = string.Format("No enabled vehicle can carry {0}", commodity);
                return false;
            }

            if (selectedVehicle.IsTrailer)
            {
                selectedTractor = _fleetManager
                    .GetTractorDefinitions()
                    .OrderByDescending(definition => definition.CapacityTons)
                    .FirstOrDefault();
                if (selectedTractor == null)
                {
                    failureReason = string.Format("No enabled truck tractor is configured for {0}", commodity);
                    return false;
                }
            }

            return true;
        }

        private Vector3 GetOriginSpawnPosition(NpcLogisticsContract contract)
        {
            var originIndustry = contract != null ? contract.OriginIndustry : null;
            if (originIndustry != null && originIndustry.VehicleSpawnPosition.HasValue)
            {
                return originIndustry.VehicleSpawnPosition.Value;
            }

            return originIndustry != null ? originIndustry.Position : Vector3.Zero;
        }

        private float GetOriginSpawnHeading(NpcLogisticsContract contract)
        {
            var originIndustry = contract != null ? contract.OriginIndustry : null;
            if (originIndustry != null && originIndustry.VehicleSpawnHeading.HasValue)
            {
                return originIndustry.VehicleSpawnHeading.Value;
            }

            return 0f;
        }

        private Vector3 GetOriginRoutePosition(NpcLogisticsContract contract)
        {
            return _getGroundPosition(GetOriginSpawnPosition(contract));
        }

        private Vector3 GetDestinationRoutePosition(NpcLogisticsContract contract)
        {
            if (contract == null || contract.DestinationIndustry == null)
            {
                return Vector3.Zero;
            }

            if (contract.DestinationIndustry.VehicleSpawnPosition.HasValue)
            {
                return _getGroundPosition(contract.DestinationIndustry.VehicleSpawnPosition.Value);
            }

            return _getGroundPosition(contract.DestinationIndustry.Position);
        }

        private Vehicle GetDriverVehicle(NpcLogisticsContract contract)
        {
            return contract != null && contract.Truck != null && contract.Truck.Exists()
                ? contract.Truck
                : (contract != null ? contract.CargoVehicle : null);
        }

        private Vehicle GetCargoVehicle(NpcLogisticsContract contract)
        {
            return contract != null && contract.CargoVehicle != null && contract.CargoVehicle.Exists()
                ? contract.CargoVehicle
                : (contract != null ? contract.Truck : null);
        }

        private int GetElapsedPayrollMinutes(int currentClockMinute)
        {
            var normalizedClockMinute = Math.Max(0, currentClockMinute);
            if (_lastObservedClockMinute < 0)
            {
                _lastObservedClockMinute = normalizedClockMinute;
                return 0;
            }

            var elapsedMinutes = normalizedClockMinute - _lastObservedClockMinute;
            if (elapsedMinutes < 0)
            {
                _lastObservedClockMinute = normalizedClockMinute;
                return 0;
            }

            _lastObservedClockMinute = normalizedClockMinute;
            return Math.Max(0, elapsedMinutes);
        }

        private static string FormatPayrollCountdown(int remainingMinutes)
        {
            if (remainingMinutes <= 0)
            {
                return "Payroll due";
            }

            var days = remainingMinutes / InGameMinutesPerDay;
            var hours = (remainingMinutes % InGameMinutesPerDay) / 60;
            var minutes = remainingMinutes % 60;
            if (days > 0)
            {
                return hours > 0
                    ? string.Format("Payroll in {0}d {1}h", days, hours)
                    : string.Format("Payroll in {0}d", days);
            }

            if (hours > 0)
            {
                return minutes > 0
                    ? string.Format("Payroll in {0}h {1}m", hours, minutes)
                    : string.Format("Payroll in {0}h", hours);
            }

            return string.Format("Payroll in {0}m", Math.Max(1, minutes));
        }

        private static string BuildContractLabel(NpcLogisticsContract contract)
        {
            if (contract == null || contract.OriginIndustry == null || contract.DestinationIndustry == null)
            {
                return "Unknown route";
            }

            return string.Format(
                "{0} -> {1} ({2})",
                contract.OriginIndustry.Name,
                contract.DestinationIndustry.Name,
                contract.Commodity);
        }

        private void RecordFinanceExpense(CompanyFinanceCategory category, float amount, string description, int routeContractId = 0, string routeLabel = null)
        {
            if (_financeTracker == null || amount <= 0f)
            {
                return;
            }

            _financeTracker.RecordExpense(category, amount, ResolveFinanceMinute(), description, routeContractId, routeLabel);
        }

        private void RecordFinanceIncome(CompanyFinanceCategory category, float amount, string description, int routeContractId = 0, string routeLabel = null)
        {
            if (_financeTracker == null || amount <= 0f)
            {
                return;
            }

            _financeTracker.RecordIncome(category, amount, ResolveFinanceMinute(), description, routeContractId, routeLabel);
        }

        private int ResolveFinanceMinute()
        {
            return _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
        }

        private NpcWorldJobSummary BuildWorldJobSummary(NpcWorldLogisticsJob job)
        {
            if (job == null)
            {
                return new NpcWorldJobSummary();
            }

            return new NpcWorldJobSummary
            {
                Id = job.Id,
                Type = job.Type,
                Label = string.Format("{0}: {1}", FormatWorldJobType(job.Type), job.Commodity),
                Detail = string.Format("{0} -> {1} | {2}", job.SourceLabel, job.DestinationLabel, job.StatusText),
                Commodity = job.Commodity,
                SourceLabel = job.SourceLabel,
                DestinationLabel = job.DestinationLabel,
                Tons = job.Tons,
                RemainingInGameMinutes = job.RemainingInGameMinutes,
                IsSpotOpportunity = job.IsSpotOpportunity,
                UsesPremiumDispatch = job.UsesPremiumDispatch,
                IsRivalJob = job.IsRivalJob,
                HasVisibleConvoy = job.HasVisibleConvoy,
            };
        }

        private string BuildWorldDispatchHeadline()
        {
            if (_worldJobs.Count == 0)
            {
                return _worldDispatchConfig != null && _worldDispatchConfig.Enabled
                    ? "World dispatch idle"
                    : "World dispatch offline";
            }

            return string.Format(
                "{0} active | {1} listed | {2} rivals",
                _worldJobs.Count(job => job != null && (job.Phase == NpcWorldJobPhase.Listed || job.Phase == NpcWorldJobPhase.Traveling)),
                _worldJobs.Count(job => job != null && job.Phase == NpcWorldJobPhase.Listed),
                _worldJobs.Count(job => job != null && job.IsRivalJob));
        }

        private string BuildWorldDispatchDetail()
        {
            var policyLabel = FormatWorldDispatchPolicy(_worldDispatchPolicy);
            var priorityCommodity = string.IsNullOrWhiteSpace(_worldPriorityCommodity) ? "Any cargo" : _worldPriorityCommodity;
            var priorityDistrict = string.IsNullOrWhiteSpace(_worldPriorityDistrict) ? "All districts" : _worldPriorityDistrict;
            return string.Format(
                "{0} | {1} | {2} | Premium {3}",
                policyLabel,
                priorityCommodity,
                priorityDistrict,
                _premiumDispatchEnabled ? "On" : "Off");
        }

        private static string FormatWorldDispatchPolicy(NpcWorldDispatchPolicy policy)
        {
            switch (policy)
            {
                case NpcWorldDispatchPolicy.OverflowRescue:
                    return "Overflow Rescue";
                case NpcWorldDispatchPolicy.ShortageRelief:
                    return "Shortage Relief";
                case NpcWorldDispatchPolicy.MarketPriority:
                    return "Market Priority";
                default:
                    return "Balanced";
            }
        }

        private static string FormatWorldJobType(NpcWorldJobType type)
        {
            switch (type)
            {
                case NpcWorldJobType.OverflowRescue:
                    return "Overflow";
                case NpcWorldJobType.ShortageRelief:
                    return "Shortage";
                case NpcWorldJobType.ExternalImport:
                    return "Import";
                case NpcWorldJobType.ExternalExport:
                    return "Export";
                case NpcWorldJobType.WarehouseBalancing:
                    return "Warehouse";
                case NpcWorldJobType.ServiceRun:
                    return "Service";
                case NpcWorldJobType.RivalFreight:
                    return "Rival";
                default:
                    return "Dispatch";
            }
        }

        private static string BuildWorldVisualFailureStatus(NpcWorldLogisticsJob job, string failureReason)
        {
            var prefix = job != null && job.IsRivalJob
                ? "Rival convoy hidden"
                : "Ambient convoy hidden";
            var reason = string.IsNullOrWhiteSpace(failureReason)
                ? "visual spawn unavailable"
                : failureReason.Trim().TrimEnd('.');
            return string.Format("{0}: {1}.", prefix, reason);
        }

        private static string CycleStringSelection(IEnumerable<string> options, string currentValue, int delta, Func<string, string> normalize)
        {
            var values = (options ?? Array.Empty<string>())
                .Select(option => normalize != null ? normalize(option) : option)
                .Where(option => option != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (values.Length == 0)
            {
                return string.Empty;
            }

            var normalizedCurrent = normalize != null ? normalize(currentValue) : currentValue;
            var currentIndex = Array.FindIndex(values, option => string.Equals(option, normalizedCurrent, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + delta) % values.Length;
            if (nextIndex < 0)
            {
                nextIndex += values.Length;
            }

            return values[nextIndex] ?? string.Empty;
        }

        private static List<NpcDriverTierDefinition> LoadDriverTiers(string configDirectory)
        {
            var defaults = new[]
            {
                BuildTier("Rookie", "Rookie", DefaultNpcModel, 0.40f, 0.60f, 25f, 1500f, 2000f, 3000f),
                BuildTier("Professional", "Professional", "s_m_y_construct_01", 0.20f, 0.80f, 50f, 3000f, 4000f, 6000f),
                BuildTier("Veteran", "Veteran", "s_m_m_dockwork_01", 0.10f, 0.90f, 100f, 6000f, 8000f, 12000f),
            };
            var filePath = Path.Combine(configDirectory ?? string.Empty, "HiringNPC.xml");
            if (!File.Exists(filePath))
            {
                return defaults.ToList();
            }

            XDocument document;
            try
            {
                document = XDocument.Load(filePath, LoadOptions.None);
            }
            catch
            {
                return defaults.ToList();
            }

            var root = document.Root;
            if (root == null)
            {
                return defaults.ToList();
            }

            var defaultsById = defaults.ToDictionary(tier => tier.Id, StringComparer.OrdinalIgnoreCase);
            var parsedTiers = new List<NpcDriverTierDefinition>();
            foreach (var element in root.Elements("Tier"))
            {
                var id = ReadAttribute(element, "id", ReadAttribute(element, "name"));
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                NpcDriverTierDefinition fallback;
                if (!defaultsById.TryGetValue(id, out fallback))
                {
                    fallback = BuildTier(id, id, DefaultNpcModel, 0.40f, 0.60f, 25f, 1500f, 2000f, 3000f);
                }

                parsedTiers.Add(BuildTier(
                    id,
                    ReadAttribute(element, "name", fallback.DisplayName),
                    ReadAttribute(element, "model", fallback.NpcModel),
                    ReadFloatAttribute(element, "cargoLossRate", fallback.CargoLossRate),
                    ReadFloatAttribute(element, "speedMultiplier", fallback.SpeedMultiplier),
                    ReadFloatAttribute(element, "priceMultiplier", fallback.PriceMultiplier),
                    ReadFloatAttribute(element, "weeklyWageCasual", fallback.WeeklyWageCasual),
                    ReadFloatAttribute(element, "weeklyWageStandard", fallback.WeeklyWageStandard),
                    ReadFloatAttribute(element, "weeklyWageHardcore", fallback.WeeklyWageHardcore)));
            }

            return parsedTiers.Count > 0
                ? parsedTiers
                : defaults.ToList();
        }

        private static NpcDriverTierDefinition BuildTier(
            string id,
            string displayName,
            string npcModel,
            float cargoLossRate,
            float speedMultiplier,
            float priceMultiplier,
            float weeklyWageCasual,
            float weeklyWageStandard,
            float weeklyWageHardcore)
        {
            return new NpcDriverTierDefinition(
                id,
                displayName,
                string.IsNullOrWhiteSpace(npcModel) ? DefaultNpcModel : npcModel.Trim().Trim('"'),
                Math.Max(0f, cargoLossRate),
                Math.Max(0.1f, speedMultiplier),
                Math.Max(1f, priceMultiplier),
                Math.Max(0f, weeklyWageCasual),
                Math.Max(0f, weeklyWageStandard),
                Math.Max(0f, weeklyWageHardcore));
        }

        private static string ReadAttribute(XElement element, string name, string fallback = "")
        {
            return element != null && element.Attribute(name) != null
                ? (element.Attribute(name).Value ?? string.Empty).Trim()
                : fallback;
        }

        private static float ReadFloatAttribute(XElement element, string name, float fallback)
        {
            float parsed;
            return float.TryParse(ReadAttribute(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                || float.TryParse(ReadAttribute(element, name), NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
                ? parsed
                : fallback;
        }
    }

    public enum NpcWeeklyWageDifficulty
    {
        Casual = 0,
        Standard = 1,
        Hardcore = 2,
    }

    public sealed class NpcDriverTierDefinition
    {
        public NpcDriverTierDefinition(
            string id,
            string displayName,
            string npcModel,
            float cargoLossRate,
            float speedMultiplier,
            float priceMultiplier,
            float weeklyWageCasual,
            float weeklyWageStandard,
            float weeklyWageHardcore)
        {
            Id = id;
            DisplayName = displayName;
            NpcModel = npcModel;
            CargoLossRate = cargoLossRate;
            SpeedMultiplier = speedMultiplier;
            PriceMultiplier = priceMultiplier;
            WeeklyWageCasual = weeklyWageCasual;
            WeeklyWageStandard = weeklyWageStandard;
            WeeklyWageHardcore = weeklyWageHardcore;
        }

        public string Id { get; private set; }

        public string DisplayName { get; private set; }

        public string NpcModel { get; private set; }

        public float CargoLossRate { get; private set; }

        public float SpeedMultiplier { get; private set; }

        public float PriceMultiplier { get; private set; }

        public float WeeklyWageCasual { get; private set; }

        public float WeeklyWageStandard { get; private set; }

        public float WeeklyWageHardcore { get; private set; }

        public float GetWeeklyWage(NpcWeeklyWageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcWeeklyWageDifficulty.Casual:
                    return WeeklyWageCasual;
                case NpcWeeklyWageDifficulty.Hardcore:
                    return WeeklyWageHardcore;
                default:
                    return WeeklyWageStandard;
            }
        }
    }

    public sealed class NpcLogisticsRouteDefinition
    {
        public Industry OriginIndustry { get; set; }

        public Industry DestinationIndustry { get; set; }

        public string Commodity { get; set; }

        public string AssignedVehicleAssetId { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public int OriginTriggerThresholdPercent { get; set; }

        public int DestinationTriggerThresholdPercent { get; set; } = 100;
    }

    public sealed class NpcLogisticsContract
    {
        internal NpcLogisticsContract(int id)
        {
            Id = id;
            Routes = new List<NpcLogisticsRouteDefinition>();
            StatusText = "Preparing route";
            Phase = NpcRoutePhase.PendingSpawn;
            OriginTriggerThresholdPercent = 0;
            DestinationTriggerThresholdPercent = 100;
        }

        public int Id { get; private set; }

        public Industry OriginIndustry { get; internal set; }

        public Industry DestinationIndustry { get; internal set; }

        public string Commodity { get; internal set; }

        public NpcDriverTierDefinition Tier { get; internal set; }

        public VehicleDefinition VehicleDefinition { get; internal set; }

        public VehicleDefinition TractorDefinition { get; internal set; }

        public string AssignedVehicleAssetId { get; internal set; }

        public string AssignedVehicleDisplayName { get; internal set; }

        public List<NpcLogisticsRouteDefinition> Routes { get; private set; }

        public int CurrentRouteIndex { get; internal set; }

        public int OriginTriggerThresholdPercent { get; internal set; }

        public int DestinationTriggerThresholdPercent { get; internal set; }

        public float ContractCost { get; internal set; }

        public int PayrollElapsedInGameMinutes { get; internal set; }

        public int CompletedPayrollCycles { get; internal set; }

        public float TotalWeeklyWagesPaid { get; internal set; }

        public int CompletedDeliveries { get; internal set; }

        public float TotalDeliveredTons { get; internal set; }

        public float TotalProfitEarned { get; internal set; }

        public string StatusText { get; internal set; }

        public float LastJourneyLossRatio { get; internal set; }

        internal NpcRoutePhase Phase { get; set; }

        internal int WaitUntilMs { get; set; }

        internal int NextDriveTaskRefreshMs { get; set; }

        internal Ped Driver { get; set; }

        internal Vehicle Truck { get; set; }

        internal Vehicle CargoVehicle { get; set; }

        internal Blip RouteBlip { get; set; }
    }

    internal enum NpcRoutePhase
    {
        PendingSpawn = 0,
        DrivingToOrigin = 1,
        Loading = 2,
        DrivingToDestination = 3,
        Unloading = 4,
        ReturningToOffice = 5,
        WaitingAtOffice = 6,
    }
}
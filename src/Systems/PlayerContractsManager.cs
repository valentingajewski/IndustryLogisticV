using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum PlayerContractType
    {
        QuickJob = 0,
        FreightMarket = 1,
    }

    public enum PlayerContractStatus
    {
        Listed = 0,
        Accepted = 1,
        Loaded = 2,
        Completed = 3,
        Cancelled = 4,
        Expired = 5,
    }

    internal enum PlayerContractTransferResolution
    {
        None = 0,
        Allowed = 1,
        Blocked = 2,
    }

    public sealed class PlayerContractsOverview
    {
        public int QuickJobCount { get; set; }

        public int FreightMarketCount { get; set; }

        public int AcceptedCount { get; set; }

        public string SelectedCommodityFilter { get; set; }

        public string BoardHeadline { get; set; }

        public string BoardDetail { get; set; }

        public string AcceptedHeadline { get; set; }

        public string AcceptedDetail { get; set; }
    }

    public sealed class PlayerContractListingSummary
    {
        public string Id { get; set; }

        public PlayerContractType Type { get; set; }

        public PlayerContractStatus Status { get; set; }

        public string Commodity { get; set; }

        public string OriginIndustryId { get; set; }

        public string OriginName { get; set; }

        public string DestinationIndustryId { get; set; }

        public string DestinationName { get; set; }

        public float ListedTons { get; set; }

        public float LoadedTons { get; set; }

        public float DeliveredTons { get; set; }

        public float RouteDistanceMeters { get; set; }

        public float QuotedUnitPrice { get; set; }

        public float QuotedGrossPayout { get; set; }

        public float CurrentEstimatedGrossPayout { get; set; }

        public float QuotedImbalanceScore { get; set; }

        public float CurrentImbalanceScore { get; set; }

        public int ExpiryInGameMinute { get; set; }

        public string VehicleRequirementLabel { get; set; }

        public bool SuppliesVehicle { get; set; }

        public bool RequiresOwnedVehicle { get; set; }

        public bool CanAccept { get; set; }

        public bool NeedsQuickJobVehicleDeploy { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public string StageLabel { get; set; }

        public string StatusDetail { get; set; }
    }

    public sealed class PlayerContractVehicleSelection
    {
        public string PoweredModelName { get; set; }

        public string CargoModelName { get; set; }

        public bool HasSeparateCargoVehicle { get; set; }

        public float CapacityTons { get; set; }

        public string DisplayName { get; set; }
    }

    public sealed class PlayerContractUnloadResult
    {
        public float Payout { get; set; }

        public bool ContractCompleted { get; set; }

        public bool ShouldCleanupQuickJobVehicle { get; set; }

        public string Message { get; set; }
    }

    public sealed class PlayerContractsPersistenceSnapshot
    {
        public PlayerContractsPersistenceSnapshot()
        {
            Contracts = new List<PlayerContractSnapshot>();
            Cooldowns = new List<PlayerContractCooldownSnapshot>();
        }

        public int NextContractId { get; set; } = 1;

        public int LastBoardRefreshMinute { get; set; } = -1;

        public string SelectedCommodityFilter { get; set; }

        public List<PlayerContractSnapshot> Contracts { get; }

        public List<PlayerContractCooldownSnapshot> Cooldowns { get; }

        public bool HasData
        {
            get
            {
                return Contracts.Count > 0
                    || Cooldowns.Count > 0
                    || NextContractId > 1
                    || LastBoardRefreshMinute >= 0
                    || !string.IsNullOrWhiteSpace(SelectedCommodityFilter);
            }
        }
    }

    public sealed class PlayerContractSnapshot
    {
        public string Id { get; set; }

        public PlayerContractType Type { get; set; }

        public PlayerContractStatus Status { get; set; }

        public string Commodity { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public float ListedTons { get; set; }

        public float LoadedTons { get; set; }

        public float DeliveredTons { get; set; }

        public float RouteDistanceMeters { get; set; }

        public float QuotedUnitPrice { get; set; }

        public float QuotedGrossPayout { get; set; }

        public float QuotedImbalanceScore { get; set; }

        public int ListedAtMinute { get; set; }

        public int ExpiryMinute { get; set; }

        public int AcceptedAtMinute { get; set; }

        public int AcceptedExpiryMinute { get; set; }

        public string VehicleRequirementLabel { get; set; }

        public bool SuppliesVehicle { get; set; }

        public bool RequiresOwnedVehicle { get; set; }

        public string AssignedCommercialVehicleAssetId { get; set; }

        public string AssignedCommercialVehicleDisplayName { get; set; }

        public string QuickJobPoweredModelName { get; set; }

        public string QuickJobCargoModelName { get; set; }

        public bool QuickJobHasSeparateCargoVehicle { get; set; }

        public float QuickJobCapacityTons { get; set; }

        public bool QuickJobNeedsDeploy { get; set; }

        public float CargoCondition { get; set; }

        public float TotalLostTons { get; set; }

        public string SourceDistrictName { get; set; }

        public string StatusMessage { get; set; }
    }

    public sealed class PlayerContractCooldownSnapshot
    {
        public string RouteKey { get; set; }

        public int AvailableAgainMinute { get; set; }
    }

    internal sealed class PlayerContractEntry
    {
        public string Id { get; set; }

        public PlayerContractType Type { get; set; }

        public PlayerContractStatus Status { get; set; }

        public string Commodity { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public float ListedTons { get; set; }

        public float LoadedTons { get; set; }

        public float DeliveredTons { get; set; }

        public float RouteDistanceMeters { get; set; }

        public float QuotedUnitPrice { get; set; }

        public float QuotedGrossPayout { get; set; }

        public float QuotedImbalanceScore { get; set; }

        public int ListedAtMinute { get; set; }

        public int ExpiryMinute { get; set; }

        public int AcceptedAtMinute { get; set; }

        public int AcceptedExpiryMinute { get; set; }

        public string VehicleRequirementLabel { get; set; }

        public bool SuppliesVehicle { get; set; }

        public bool RequiresOwnedVehicle { get; set; }

        public string AssignedCommercialVehicleAssetId { get; set; }

        public string AssignedCommercialVehicleDisplayName { get; set; }

        public string QuickJobPoweredModelName { get; set; }

        public string QuickJobCargoModelName { get; set; }

        public bool QuickJobHasSeparateCargoVehicle { get; set; }

        public float QuickJobCapacityTons { get; set; }

        public int QuickJobTruckHandle { get; set; }

        public int QuickJobCargoHandle { get; set; }

        public bool QuickJobNeedsDeploy { get; set; }

        public bool QuickJobCleanupPending { get; set; }

        public float CargoCondition { get; set; }

        public float TotalLostTons { get; set; }

        public string SourceDistrictName { get; set; }

        public string StatusMessage { get; set; }

        public PlayerContractEntry CloneAccepted(int currentMinute, int acceptedLifetimeMinutes)
        {
            return new PlayerContractEntry
            {
                Id = Id,
                Type = Type,
                Status = PlayerContractStatus.Accepted,
                Commodity = Commodity,
                OriginIndustryId = OriginIndustryId,
                DestinationIndustryId = DestinationIndustryId,
                ListedTons = ListedTons,
                LoadedTons = 0f,
                DeliveredTons = 0f,
                RouteDistanceMeters = RouteDistanceMeters,
                QuotedUnitPrice = QuotedUnitPrice,
                QuotedGrossPayout = QuotedGrossPayout,
                QuotedImbalanceScore = QuotedImbalanceScore,
                ListedAtMinute = ListedAtMinute,
                ExpiryMinute = ExpiryMinute,
                AcceptedAtMinute = currentMinute,
                AcceptedExpiryMinute = currentMinute + Math.Max(1, acceptedLifetimeMinutes),
                VehicleRequirementLabel = VehicleRequirementLabel,
                SuppliesVehicle = SuppliesVehicle,
                RequiresOwnedVehicle = RequiresOwnedVehicle,
                AssignedCommercialVehicleAssetId = string.Empty,
                AssignedCommercialVehicleDisplayName = string.Empty,
                QuickJobPoweredModelName = QuickJobPoweredModelName,
                QuickJobCargoModelName = QuickJobCargoModelName,
                QuickJobHasSeparateCargoVehicle = QuickJobHasSeparateCargoVehicle,
                QuickJobCapacityTons = QuickJobCapacityTons,
                QuickJobTruckHandle = 0,
                QuickJobCargoHandle = 0,
                QuickJobNeedsDeploy = Type == PlayerContractType.QuickJob,
                QuickJobCleanupPending = false,
                CargoCondition = 1f,
                TotalLostTons = 0f,
                SourceDistrictName = string.Empty,
                StatusMessage = string.Empty,
            };
        }

        public PlayerContractSnapshot ToSnapshot()
        {
            return new PlayerContractSnapshot
            {
                Id = Id,
                Type = Type,
                Status = Status,
                Commodity = Commodity,
                OriginIndustryId = OriginIndustryId,
                DestinationIndustryId = DestinationIndustryId,
                ListedTons = ListedTons,
                LoadedTons = LoadedTons,
                DeliveredTons = DeliveredTons,
                RouteDistanceMeters = RouteDistanceMeters,
                QuotedUnitPrice = QuotedUnitPrice,
                QuotedGrossPayout = QuotedGrossPayout,
                QuotedImbalanceScore = QuotedImbalanceScore,
                ListedAtMinute = ListedAtMinute,
                ExpiryMinute = ExpiryMinute,
                AcceptedAtMinute = AcceptedAtMinute,
                AcceptedExpiryMinute = AcceptedExpiryMinute,
                VehicleRequirementLabel = VehicleRequirementLabel,
                SuppliesVehicle = SuppliesVehicle,
                RequiresOwnedVehicle = RequiresOwnedVehicle,
                AssignedCommercialVehicleAssetId = AssignedCommercialVehicleAssetId,
                AssignedCommercialVehicleDisplayName = AssignedCommercialVehicleDisplayName,
                QuickJobPoweredModelName = QuickJobPoweredModelName,
                QuickJobCargoModelName = QuickJobCargoModelName,
                QuickJobHasSeparateCargoVehicle = QuickJobHasSeparateCargoVehicle,
                QuickJobCapacityTons = QuickJobCapacityTons,
                QuickJobNeedsDeploy = QuickJobNeedsDeploy,
                CargoCondition = CargoCondition,
                TotalLostTons = TotalLostTons,
                SourceDistrictName = SourceDistrictName,
                StatusMessage = StatusMessage,
            };
        }

        public static PlayerContractEntry FromSnapshot(PlayerContractSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new PlayerContractEntry
            {
                Id = snapshot.Id,
                Type = snapshot.Type,
                Status = snapshot.Status,
                Commodity = CommodityCatalog.Normalize(snapshot.Commodity),
                OriginIndustryId = snapshot.OriginIndustryId ?? string.Empty,
                DestinationIndustryId = snapshot.DestinationIndustryId ?? string.Empty,
                ListedTons = Math.Max(0f, snapshot.ListedTons),
                LoadedTons = Math.Max(0f, snapshot.LoadedTons),
                DeliveredTons = Math.Max(0f, snapshot.DeliveredTons),
                RouteDistanceMeters = Math.Max(0f, snapshot.RouteDistanceMeters),
                QuotedUnitPrice = Math.Max(0f, snapshot.QuotedUnitPrice),
                QuotedGrossPayout = Math.Max(0f, snapshot.QuotedGrossPayout),
                QuotedImbalanceScore = ModMath.Clamp01(snapshot.QuotedImbalanceScore),
                ListedAtMinute = snapshot.ListedAtMinute,
                ExpiryMinute = snapshot.ExpiryMinute,
                AcceptedAtMinute = snapshot.AcceptedAtMinute,
                AcceptedExpiryMinute = snapshot.AcceptedExpiryMinute,
                VehicleRequirementLabel = snapshot.VehicleRequirementLabel ?? string.Empty,
                SuppliesVehicle = snapshot.SuppliesVehicle,
                RequiresOwnedVehicle = snapshot.RequiresOwnedVehicle,
                AssignedCommercialVehicleAssetId = snapshot.AssignedCommercialVehicleAssetId ?? string.Empty,
                AssignedCommercialVehicleDisplayName = snapshot.AssignedCommercialVehicleDisplayName ?? string.Empty,
                QuickJobPoweredModelName = snapshot.QuickJobPoweredModelName ?? string.Empty,
                QuickJobCargoModelName = snapshot.QuickJobCargoModelName ?? string.Empty,
                QuickJobHasSeparateCargoVehicle = snapshot.QuickJobHasSeparateCargoVehicle,
                QuickJobCapacityTons = Math.Max(0f, snapshot.QuickJobCapacityTons),
                QuickJobNeedsDeploy = snapshot.QuickJobNeedsDeploy,
                CargoCondition = ModMath.Clamp01(snapshot.CargoCondition <= 0f ? 1f : snapshot.CargoCondition),
                TotalLostTons = Math.Max(0f, snapshot.TotalLostTons),
                SourceDistrictName = snapshot.SourceDistrictName ?? string.Empty,
                StatusMessage = snapshot.StatusMessage ?? string.Empty,
            };
        }
    }

    internal sealed class PlayerContractTransferContext
    {
        public PlayerContractEntry Contract { get; set; }

        public Industry Origin { get; set; }

        public Industry Destination { get; set; }

        public float AvailableTons { get; set; }

        public float DestinationFreeTons { get; set; }
    }

    internal static class DispatchCommodityMath
    {
        public static float GetOriginAvailableTons(Industry industry, string commodity)
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

        public static float GetDestinationFreeTons(Industry industry, string commodity)
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

        public static float GetCommodityFillRatio(Industry industry, string commodity)
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
    }

    public sealed class PlayerContractsManager
    {
        private const int BoardRefreshIntervalMinutes = 180;
        private const int QuickJobListingLifetimeMinutes = 240;
        private const int FreightListingLifetimeMinutes = 360;
        private const int QuickJobAcceptedLifetimeMinutes = 420;
        private const int FreightAcceptedLifetimeMinutes = 540;
        private const int RouteCooldownMinutes = 360;
        private const int MaxQuickJobListings = 4;
        private const int MaxFreightListings = 6;
        private const float QuickJobMinTons = 1.25f;
        private const float QuickJobMaxTons = 6f;
        private const float FreightMinTons = 2.5f;
        private const float FreightMaxTons = 12f;
        private const float QuickJobMaxDistanceMeters = 6500f;
        private const float FreightMaxDistanceMeters = 18000f;

        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly PropertyManager _propertyManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly TerritoryManager _territoryManager;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<Ped> _getPlayer;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Action<string> _showStatus;
        private readonly Action _onContractsChanged;
        private readonly Random _random;
        private readonly Dictionary<string, int> _routeCooldownUntilMinute;
        private readonly Dictionary<string, Industry> _industriesById;
        private readonly List<PlayerContractEntry> _listedContracts;

        private PlayerContractEntry _acceptedContract;
        private int _nextContractId;
        private int _lastBoardRefreshMinute;
        private string _selectedCommodityFilter;

        public PlayerContractsManager(
            IndustryManager industryManager,
            FleetManager fleetManager,
            PropertyManager propertyManager,
            GlobalMarketManager globalMarket,
            Func<Vector3, Vector3> getGroundPosition,
            Func<Ped> getPlayer,
            Func<int> getCurrentInGameMinute,
            Action<string> showStatus,
            TerritoryManager territoryManager = null,
            Action onContractsChanged = null,
            Random random = null)
        {
            _industryManager = industryManager;
            _fleetManager = fleetManager;
            _propertyManager = propertyManager;
            _globalMarket = globalMarket;
            _territoryManager = territoryManager;
            _getGroundPosition = getGroundPosition;
            _getPlayer = getPlayer;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _showStatus = showStatus;
            _onContractsChanged = onContractsChanged;
            _random = random ?? new Random();
            _routeCooldownUntilMinute = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _listedContracts = new List<PlayerContractEntry>();
            _industriesById = industryManager != null && industryManager.Industries != null
                ? industryManager.Industries
                    .Where(industry => industry != null && !string.IsNullOrWhiteSpace(industry.Id))
                    .ToDictionary(industry => industry.Id, industry => industry, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Industry>(StringComparer.OrdinalIgnoreCase);
            _nextContractId = 1;
            _lastBoardRefreshMinute = -1;
            _selectedCommodityFilter = string.Empty;
        }

        public IReadOnlyList<PlayerContractListingSummary> GetListings(PlayerContractType type)
        {
            var currentMinute = GetCurrentInGameMinute();
            return _listedContracts
                .Where(contract => contract != null && contract.Type == type)
                .Where(ShouldIncludeForSelectedCommodity)
                .OrderByDescending(contract => contract.QuotedImbalanceScore)
                .ThenBy(contract => contract.ExpiryMinute)
                .Select(contract => BuildSummary(contract, currentMinute))
                .ToArray();
        }

        public IReadOnlyList<PlayerContractListingSummary> GetAcceptedContracts()
        {
            if (_acceptedContract == null || IsTerminal(_acceptedContract.Status))
            {
                return Array.Empty<PlayerContractListingSummary>();
            }

            return new[] { BuildSummary(_acceptedContract, GetCurrentInGameMinute()) };
        }

        public PlayerContractListingSummary GetContractById(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId))
            {
                return null;
            }

            var currentMinute = GetCurrentInGameMinute();
            var match = _listedContracts.FirstOrDefault(contract => contract != null && string.Equals(contract.Id, contractId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return BuildSummary(match, currentMinute);
            }

            if (_acceptedContract != null && string.Equals(_acceptedContract.Id, contractId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildSummary(_acceptedContract, currentMinute);
            }

            return null;
        }

        public PlayerContractsOverview GetOverview()
        {
            var currentMinute = GetCurrentInGameMinute();
            var filteredQuick = _listedContracts.Count(contract => contract != null && contract.Type == PlayerContractType.QuickJob && ShouldIncludeForSelectedCommodity(contract));
            var filteredFreight = _listedContracts.Count(contract => contract != null && contract.Type == PlayerContractType.FreightMarket && ShouldIncludeForSelectedCommodity(contract));
            var acceptedSummary = _acceptedContract != null && !IsTerminal(_acceptedContract.Status)
                ? BuildSummary(_acceptedContract, currentMinute)
                : null;

            return new PlayerContractsOverview
            {
                QuickJobCount = filteredQuick,
                FreightMarketCount = filteredFreight,
                AcceptedCount = acceptedSummary != null ? 1 : 0,
                SelectedCommodityFilter = string.IsNullOrWhiteSpace(_selectedCommodityFilter) ? "Any" : _selectedCommodityFilter,
                BoardHeadline = filteredQuick + filteredFreight > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} Quick | {1} Freight", filteredQuick, filteredFreight)
                    : "Dispatch board cooling down",
                BoardDetail = filteredQuick + filteredFreight > 0
                    ? "Permit-free side contracts generated from live stock imbalances."
                    : "New offers appear when real source surplus and destination demand reopen a lane.",
                AcceptedHeadline = acceptedSummary != null
                    ? string.Format(CultureInfo.InvariantCulture, "{0} {1}", acceptedSummary.Type == PlayerContractType.QuickJob ? "Quick Job" : "Freight Market", acceptedSummary.StageLabel)
                    : "No accepted contract",
                AcceptedDetail = acceptedSummary != null
                    ? string.Format(CultureInfo.InvariantCulture, "{0} -> {1} | {2}", acceptedSummary.OriginName, acceptedSummary.DestinationName, acceptedSummary.StatusDetail)
                    : "Accept a dispatch-board contract to open a permit-free side lane.",
            };
        }

        public IReadOnlyList<string> GetCommodityFilterOptions()
        {
            var values = _listedContracts
                .Where(contract => contract != null && !string.IsNullOrWhiteSpace(contract.Commodity))
                .Select(contract => contract.Commodity)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            values.Insert(0, string.Empty);
            return values;
        }

        public void CycleCommodityFilter(int delta)
        {
            var options = GetCommodityFilterOptions();
            if (options.Count == 0)
            {
                _selectedCommodityFilter = string.Empty;
                return;
            }

            var currentIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (!string.Equals(options[i], _selectedCommodityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                currentIndex = i;
                break;
            }
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            currentIndex = (currentIndex + delta % options.Count + options.Count) % options.Count;
            _selectedCommodityFilter = options[currentIndex] ?? string.Empty;
            NotifyContractsChanged();
        }

        public void Update(int gameTimeMs, int currentInGameMinute)
        {
            ExpireListedContracts(currentInGameMinute);
            UpdateAcceptedContract(currentInGameMinute);
            TryCleanupTemporaryQuickJobVehicle();

            if (_lastBoardRefreshMinute < 0 || currentInGameMinute - _lastBoardRefreshMinute >= BoardRefreshIntervalMinutes)
            {
                ForceRefreshBoard(currentInGameMinute);
            }
        }

        public void ForceRefreshBoard(int currentInGameMinute)
        {
            _listedContracts.Clear();
            PruneExpiredCooldowns(currentInGameMinute);

            var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddListingsForType(PlayerContractType.QuickJob, MaxQuickJobListings, currentInGameMinute, usedRoutes);
            AddListingsForType(PlayerContractType.FreightMarket, MaxFreightListings, currentInGameMinute, usedRoutes);

            _lastBoardRefreshMinute = currentInGameMinute;
            NotifyContractsChanged();
        }

        public bool TryAccept(string contractId, out string message)
        {
            message = string.Empty;
            if (_acceptedContract != null && !IsTerminal(_acceptedContract.Status))
            {
                message = "Complete or cancel the active contract before accepting another.";
                return false;
            }

            var listing = _listedContracts.FirstOrDefault(contract => contract != null && string.Equals(contract.Id, contractId, StringComparison.OrdinalIgnoreCase));
            if (listing == null)
            {
                message = "Contract listing unavailable.";
                return false;
            }

            if (listing.Type == PlayerContractType.FreightMarket)
            {
                string availabilityReason;
                if (!HasCompatibleCompanyVehicle(listing.Commodity, out availabilityReason))
                {
                    message = availabilityReason;
                    return false;
                }
            }

            _acceptedContract = listing.CloneAccepted(
                GetCurrentInGameMinute(),
                listing.Type == PlayerContractType.QuickJob ? QuickJobAcceptedLifetimeMinutes : FreightAcceptedLifetimeMinutes);
            _listedContracts.Remove(listing);

            if (_acceptedContract.Type == PlayerContractType.QuickJob)
            {
                string deployMessage;
                if (!TryDeployQuickJobVehicle(_acceptedContract.Id, out deployMessage))
                {
                    _acceptedContract.QuickJobNeedsDeploy = true;
                    message = string.IsNullOrWhiteSpace(deployMessage)
                        ? "Accepted Quick Job. Open Accepted Contracts to deploy the supplied vehicle."
                        : deployMessage;
                }
                else
                {
                    message = string.IsNullOrWhiteSpace(deployMessage)
                        ? "Accepted Quick Job. The supplied vehicle is ready."
                        : deployMessage;
                }
            }
            else
            {
                message = "Accepted Freight Market contract. Bring a compatible company vehicle to the origin to load it.";
            }

            RegisterCooldown(_acceptedContract, GetCurrentInGameMinute());
            NotifyContractsChanged();
            return true;
        }

        public bool TryCancelAccepted(string contractId, out string message)
        {
            message = string.Empty;
            if (_acceptedContract == null || IsTerminal(_acceptedContract.Status))
            {
                message = "No active contract to cancel.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(contractId)
                && !string.Equals(_acceptedContract.Id, contractId, StringComparison.OrdinalIgnoreCase))
            {
                message = "That contract is no longer active.";
                return false;
            }

            if (_acceptedContract.Status == PlayerContractStatus.Loaded)
            {
                ForfeitLoadedContractCargo(_acceptedContract);
            }

            _acceptedContract.Status = PlayerContractStatus.Cancelled;
            _acceptedContract.StatusMessage = "Cancelled";
            RegisterCooldown(_acceptedContract, GetCurrentInGameMinute());
            if (_acceptedContract.Type == PlayerContractType.QuickJob)
            {
                _acceptedContract.QuickJobCleanupPending = true;
            }

            message = _acceptedContract.Type == PlayerContractType.QuickJob
                ? "Cancelled Quick Job. The supplied vehicle will be reclaimed when it is safe to clean up."
                : "Cancelled Freight Market contract. Any loaded contract cargo was forfeited.";
            NotifyContractsChanged();
            return true;
        }

        public bool TryDeployQuickJobVehicle(string contractId, out string message)
        {
            message = string.Empty;
            if (_acceptedContract == null
                || IsTerminal(_acceptedContract.Status)
                || _acceptedContract.Type != PlayerContractType.QuickJob
                || (!string.IsNullOrWhiteSpace(contractId) && !string.Equals(_acceptedContract.Id, contractId, StringComparison.OrdinalIgnoreCase)))
            {
                message = "No accepted Quick Job is waiting for a supplied vehicle.";
                return false;
            }

            if (HasLiveQuickJobVehicle(_acceptedContract))
            {
                _acceptedContract.QuickJobNeedsDeploy = false;
                message = "The supplied contract vehicle is already active.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(_acceptedContract.QuickJobCargoModelName))
            {
                message = "This Quick Job no longer has a valid supplied vehicle definition.";
                return false;
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            if (!TrySpawnQuickJobVehicle(_acceptedContract, out truck, out cargoVehicle, out message))
            {
                _acceptedContract.QuickJobNeedsDeploy = true;
                return false;
            }

            _acceptedContract.QuickJobTruckHandle = truck != null && truck.Exists() ? truck.Handle : 0;
            _acceptedContract.QuickJobCargoHandle = cargoVehicle != null && cargoVehicle.Exists() ? cargoVehicle.Handle : 0;
            _acceptedContract.QuickJobNeedsDeploy = false;

            var cargoState = cargoVehicle != null && cargoVehicle.Exists()
                ? _fleetManager.GetOrCreateCargoState(cargoVehicle)
                : null;
            if (cargoState != null)
            {
                cargoState.CargoType = cargoVehicle != null && cargoVehicle.Exists()
                    ? cargoState.CargoType
                    : CommodityCatalog.GetCargoTypeForCommodity(_acceptedContract.Commodity);

                if (_acceptedContract.Status == PlayerContractStatus.Loaded && _acceptedContract.LoadedTons > 0.001f)
                {
                    cargoState.Commodity = _acceptedContract.Commodity;
                    cargoState.WeightTons = _acceptedContract.LoadedTons;
                    cargoState.CargoCondition = ModMath.Clamp01(_acceptedContract.CargoCondition);
                    cargoState.TotalLostTons = Math.Max(0f, _acceptedContract.TotalLostTons);
                    cargoState.SourceIndustryId = _acceptedContract.OriginIndustryId ?? string.Empty;
                    cargoState.SourceDistrictName = _acceptedContract.SourceDistrictName ?? string.Empty;
                    cargoState.PlayerContractId = _acceptedContract.Id;
                    cargoState.PlayerContractDestinationIndustryId = _acceptedContract.DestinationIndustryId ?? string.Empty;
                    _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                }
            }

            message = _acceptedContract.Status == PlayerContractStatus.Loaded && _acceptedContract.LoadedTons > 0.001f
                ? "Redeployed the supplied Quick Job vehicle with the saved contract cargo." 
                : "Deployed the supplied Quick Job vehicle.";
            NotifyContractsChanged();
            return true;
        }

        public PlayerContractsPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var currentMinute = GetCurrentInGameMinute();
            var snapshot = new PlayerContractsPersistenceSnapshot
            {
                NextContractId = Math.Max(1, _nextContractId),
                LastBoardRefreshMinute = _lastBoardRefreshMinute,
                SelectedCommodityFilter = _selectedCommodityFilter ?? string.Empty,
            };

            if (_acceptedContract != null && !IsTerminal(_acceptedContract.Status))
            {
                CaptureQuickJobRuntimeState(_acceptedContract);
                snapshot.Contracts.Add(_acceptedContract.ToSnapshot());
            }

            foreach (var pair in _routeCooldownUntilMinute)
            {
                if (pair.Value <= currentMinute)
                {
                    continue;
                }

                snapshot.Cooldowns.Add(new PlayerContractCooldownSnapshot
                {
                    RouteKey = pair.Key,
                    AvailableAgainMinute = pair.Value,
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        public void ApplyPersistenceSnapshot(PlayerContractsPersistenceSnapshot snapshot)
        {
            _listedContracts.Clear();
            _acceptedContract = null;
            _routeCooldownUntilMinute.Clear();
            _nextContractId = 1;
            _lastBoardRefreshMinute = -1;
            _selectedCommodityFilter = string.Empty;

            if (snapshot == null)
            {
                return;
            }

            _nextContractId = Math.Max(1, snapshot.NextContractId);
            _lastBoardRefreshMinute = snapshot.LastBoardRefreshMinute;
            _selectedCommodityFilter = snapshot.SelectedCommodityFilter ?? string.Empty;

            if (snapshot.Cooldowns != null)
            {
                for (int i = 0; i < snapshot.Cooldowns.Count; i++)
                {
                    var cooldown = snapshot.Cooldowns[i];
                    if (cooldown == null || string.IsNullOrWhiteSpace(cooldown.RouteKey))
                    {
                        continue;
                    }

                    _routeCooldownUntilMinute[cooldown.RouteKey] = cooldown.AvailableAgainMinute;
                }
            }

            if (snapshot.Contracts == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Contracts.Count; i++)
            {
                var contract = PlayerContractEntry.FromSnapshot(snapshot.Contracts[i]);
                if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
                {
                    continue;
                }

                if (!_industriesById.ContainsKey(contract.OriginIndustryId) || !_industriesById.ContainsKey(contract.DestinationIndustryId))
                {
                    continue;
                }

                if (IsTerminal(contract.Status))
                {
                    continue;
                }

                contract.QuickJobTruckHandle = 0;
                contract.QuickJobCargoHandle = 0;
                contract.QuickJobCleanupPending = false;
                contract.QuickJobNeedsDeploy = contract.Type == PlayerContractType.QuickJob;
                _acceptedContract = contract;
                break;
            }

            NotifyContractsChanged();
        }

        public void ClearAll()
        {
            if (_acceptedContract != null && _acceptedContract.Type == PlayerContractType.QuickJob)
            {
                ForceDeleteQuickJobVehicle(_acceptedContract);
            }

            _listedContracts.Clear();
            _acceptedContract = null;
            _routeCooldownUntilMinute.Clear();
            _lastBoardRefreshMinute = -1;
            NotifyContractsChanged();
        }

        internal PlayerContractTransferResolution ResolveLoadContract(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            string commodity,
            out PlayerContractTransferContext context,
            out string message)
        {
            context = null;
            message = string.Empty;
            if (_acceptedContract == null || IsTerminal(_acceptedContract.Status))
            {
                return PlayerContractTransferResolution.None;
            }

            if (_acceptedContract.Status != PlayerContractStatus.Accepted)
            {
                return PlayerContractTransferResolution.None;
            }

            if (industry == null || cargoVehicle == null || !cargoVehicle.Exists() || cargoState == null)
            {
                return PlayerContractTransferResolution.None;
            }

            if (!string.Equals(_acceptedContract.OriginIndustryId, industry.Id, StringComparison.OrdinalIgnoreCase)
                || !CommodityCatalog.IsSameCommodity(_acceptedContract.Commodity, commodity))
            {
                return PlayerContractTransferResolution.None;
            }

            if (!cargoState.IsEmpty)
            {
                message = "Empty the vehicle before loading contract cargo.";
                return PlayerContractTransferResolution.Blocked;
            }

            var origin = FindIndustry(_acceptedContract.OriginIndustryId);
            var destination = FindIndustry(_acceptedContract.DestinationIndustryId);
            if (origin == null || destination == null)
            {
                message = "The accepted contract endpoints are no longer available.";
                return PlayerContractTransferResolution.Blocked;
            }

            if (!ValidateContractVehicleForLoad(_acceptedContract, cargoVehicle, out message))
            {
                return PlayerContractTransferResolution.Blocked;
            }

            var availableTons = DispatchCommodityMath.GetOriginAvailableTons(origin, _acceptedContract.Commodity);
            var destinationFreeTons = DispatchCommodityMath.GetDestinationFreeTons(destination, _acceptedContract.Commodity);
            if (availableTons <= 0.001f)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} no longer has enough {1} for this contract.", origin.Name, _acceptedContract.Commodity);
                return PlayerContractTransferResolution.Blocked;
            }

            if (destinationFreeTons <= 0.001f)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} is no longer accepting more {1} right now.", destination.Name, _acceptedContract.Commodity);
                return PlayerContractTransferResolution.Blocked;
            }

            context = new PlayerContractTransferContext
            {
                Contract = _acceptedContract,
                Origin = origin,
                Destination = destination,
                AvailableTons = availableTons,
                DestinationFreeTons = destinationFreeTons,
            };
            return PlayerContractTransferResolution.Allowed;
        }

        internal PlayerContractTransferResolution ResolveUnloadContract(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            out PlayerContractTransferContext context,
            out string message)
        {
            context = null;
            message = string.Empty;
            if (cargoState == null || string.IsNullOrWhiteSpace(cargoState.PlayerContractId))
            {
                return PlayerContractTransferResolution.None;
            }

            if (_acceptedContract == null
                || IsTerminal(_acceptedContract.Status)
                || !string.Equals(_acceptedContract.Id, cargoState.PlayerContractId, StringComparison.OrdinalIgnoreCase))
            {
                message = "This cargo no longer belongs to an active dispatch-board contract.";
                return PlayerContractTransferResolution.Blocked;
            }

            var origin = FindIndustry(_acceptedContract.OriginIndustryId);
            var destination = FindIndustry(_acceptedContract.DestinationIndustryId);
            if (origin == null || destination == null)
            {
                message = "The accepted contract route is no longer available.";
                return PlayerContractTransferResolution.Blocked;
            }

            if (industry == null || !string.Equals(industry.Id, _acceptedContract.DestinationIndustryId, StringComparison.OrdinalIgnoreCase))
            {
                message = string.Format(CultureInfo.InvariantCulture, "Contract cargo for {0} must be delivered to {1}.", _acceptedContract.Commodity, destination.Name);
                return PlayerContractTransferResolution.Blocked;
            }

            context = new PlayerContractTransferContext
            {
                Contract = _acceptedContract,
                Origin = origin,
                Destination = destination,
                AvailableTons = DispatchCommodityMath.GetOriginAvailableTons(origin, _acceptedContract.Commodity),
                DestinationFreeTons = DispatchCommodityMath.GetDestinationFreeTons(destination, _acceptedContract.Commodity),
            };
            return PlayerContractTransferResolution.Allowed;
        }

        internal float GetContractLoadCeiling(PlayerContractTransferContext context, VehicleCargoState cargoState)
        {
            if (context == null || context.Contract == null || cargoState == null)
            {
                return 0f;
            }

            var ceiling = Math.Min(
                Math.Min(context.AvailableTons, context.DestinationFreeTons),
                Math.Max(0f, cargoState.FreeCapacityTons));
            ceiling = Math.Min(ceiling, Math.Max(0f, context.Contract.ListedTons));
            return ceiling <= 0.001f ? 0f : ceiling;
        }

        internal void CommitContractLoad(PlayerContractTransferContext context, Vehicle cargoVehicle, VehicleCargoState cargoState, float loadedTons)
        {
            if (context == null || context.Contract == null || cargoState == null || loadedTons <= 0.001f)
            {
                return;
            }

            var contract = context.Contract;
            contract.Status = PlayerContractStatus.Loaded;
            contract.LoadedTons = loadedTons;
            contract.CargoCondition = ModMath.Clamp01(cargoState.CargoCondition);
            contract.TotalLostTons = Math.Max(0f, cargoState.TotalLostTons);
            contract.SourceDistrictName = cargoState.SourceDistrictName ?? string.Empty;
            contract.StatusMessage = string.Format(CultureInfo.InvariantCulture, "Loaded {0} {1}", ModFormatting.FormatTons(loadedTons), contract.Commodity);
            cargoState.PlayerContractId = contract.Id;
            cargoState.PlayerContractDestinationIndustryId = contract.DestinationIndustryId ?? string.Empty;

            if (contract.Type == PlayerContractType.FreightMarket && _propertyManager != null)
            {
                OwnedCommercialVehiclePersistenceEntry entry;
                if (_propertyManager.TryResolveCommercialVehicleRecord(cargoVehicle, out entry) && entry != null)
                {
                    contract.AssignedCommercialVehicleAssetId = entry.AssetId ?? string.Empty;
                    contract.AssignedCommercialVehicleDisplayName = entry.DisplayName ?? string.Empty;
                }
            }

            NotifyContractsChanged();
        }

        internal PlayerContractUnloadResult CommitContractUnload(PlayerContractTransferContext context, Vehicle cargoVehicle, VehicleCargoState cargoState, float acceptedTons, int gameTimeMs)
        {
            var result = new PlayerContractUnloadResult();
            if (context == null || context.Contract == null || cargoState == null || acceptedTons <= 0.001f)
            {
                result.Message = "Contract unloading failed.";
                return result;
            }

            var contract = context.Contract;
            var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
            var liveImbalance = ComputeCurrentImbalanceScore(contract);
            var unitPrice = ComputeContractUnitPrice(
                contract.Type,
                _globalMarket != null ? _globalMarket.GetUnitPrice(contract.Commodity) : 0f,
                liveImbalance,
                contract.RouteDistanceMeters);
            var payout = Math.Max(0f, acceptedTons * unitPrice * conditionRatio);
            contract.DeliveredTons += acceptedTons;
            contract.LoadedTons = Math.Max(0f, contract.LoadedTons - acceptedTons);
            contract.CargoCondition = conditionRatio;
            contract.TotalLostTons = Math.Max(0f, cargoState.TotalLostTons);

            if (_globalMarket != null && context.Destination != null && !context.Destination.IsWarehouse)
            {
                _globalMarket.RegisterDelivery(contract.Commodity, gameTimeMs);
            }

            var completed = cargoState.WeightTons <= 0.001f || contract.LoadedTons <= 0.001f;
            if (completed)
            {
                contract.Status = PlayerContractStatus.Completed;
                contract.StatusMessage = string.Format(CultureInfo.InvariantCulture, "Completed {0} contract", contract.Type == PlayerContractType.QuickJob ? "Quick Job" : "Freight Market");
                RegisterCooldown(contract, GetCurrentInGameMinute());
                result.ContractCompleted = true;
                result.ShouldCleanupQuickJobVehicle = contract.Type == PlayerContractType.QuickJob;
                result.Message = string.Format(CultureInfo.InvariantCulture, "Delivered {0} {1}. Contract payout {2}.", ModFormatting.FormatTons(acceptedTons), contract.Commodity, ModFormatting.FormatMoney(payout));
            }
            else
            {
                result.Message = string.Format(CultureInfo.InvariantCulture, "Delivered {0} {1}. Contract payout {2}. Remaining cargo stays under contract.", ModFormatting.FormatTons(acceptedTons), contract.Commodity, ModFormatting.FormatMoney(payout));
            }

            result.Payout = payout;
            if (result.ShouldCleanupQuickJobVehicle)
            {
                contract.QuickJobCleanupPending = true;
            }

            NotifyContractsChanged();
            return result;
        }

        public static float ComputeImbalanceScore(float sourceStock, float sourceFreeSpace, float destinationStock, float destinationFreeSpace)
        {
            var sourceDenominator = Math.Max(0.001f, sourceStock + sourceFreeSpace);
            var destinationDenominator = Math.Max(0.001f, destinationStock + destinationFreeSpace);
            var sourcePressure = ModMath.Clamp01(sourceStock / sourceDenominator);
            var destinationDemand = ModMath.Clamp01(destinationFreeSpace / destinationDenominator);
            var harmonic = sourcePressure <= 0.001f || destinationDemand <= 0.001f
                ? 0f
                : (2f * sourcePressure * destinationDemand) / Math.Max(0.001f, sourcePressure + destinationDemand);
            var geometric = (float)Math.Sqrt(Math.Max(0f, sourcePressure * destinationDemand));
            var average = (sourcePressure + destinationDemand) * 0.5f;
            return ModMath.Clamp01((harmonic * 0.45f) + (geometric * 0.35f) + (average * 0.20f));
        }

        public static float ComputeContractUnitPrice(PlayerContractType type, float anchorUnitPrice, float imbalanceScore, float routeDistanceMeters)
        {
            var normalizedImbalance = ModMath.Clamp01(imbalanceScore);
            var distanceFactor = 0.96f + (Math.Min(Math.Max(0f, routeDistanceMeters), FreightMaxDistanceMeters) / FreightMaxDistanceMeters) * 0.08f;
            var freightRatio = (0.60f + (0.15f * normalizedImbalance)) * distanceFactor;
            if (type == PlayerContractType.QuickJob)
            {
                freightRatio *= 0.35f + (0.15f * normalizedImbalance);
            }

            return Math.Max(0f, anchorUnitPrice) * Math.Max(0f, freightRatio);
        }

        public static float ComputeContractGrossPayout(PlayerContractType type, float anchorUnitPrice, float imbalanceScore, float routeDistanceMeters, float tons, float conditionRatio)
        {
            return Math.Max(0f, tons) * ComputeContractUnitPrice(type, anchorUnitPrice, imbalanceScore, routeDistanceMeters) * ModMath.Clamp01(conditionRatio);
        }

        private void AddListingsForType(PlayerContractType type, int maxCount, int currentMinute, HashSet<string> usedRoutes)
        {
            var candidates = BuildCandidates(type)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.RouteDistanceMeters)
                .ThenBy(candidate => candidate.OriginIndustry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.DestinationIndustry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var commodityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count && _listedContracts.Count(contract => contract.Type == type) < maxCount; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || usedRoutes.Contains(candidate.RouteKey) || IsOnCooldown(candidate.RouteKey, currentMinute))
                {
                    continue;
                }

                int commodityCount;
                commodityCounts.TryGetValue(candidate.Commodity, out commodityCount);
                if (commodityCount >= 2)
                {
                    continue;
                }

                var listing = BuildListing(candidate, currentMinute);
                if (listing == null)
                {
                    continue;
                }

                _listedContracts.Add(listing);
                usedRoutes.Add(candidate.RouteKey);
                commodityCounts[candidate.Commodity] = commodityCount + 1;
            }
        }

        private IReadOnlyList<PlayerContractCandidate> BuildCandidates(PlayerContractType type)
        {
            if (_industryManager == null || _industryManager.Industries == null || _industryManager.Industries.Count == 0)
            {
                return Array.Empty<PlayerContractCandidate>();
            }

            var results = new List<PlayerContractCandidate>();
            var industries = _industryManager.Industries.Where(industry => industry != null).ToList();
            for (int originIndex = 0; originIndex < industries.Count; originIndex++)
            {
                var origin = industries[originIndex];
                var commodities = GetOriginCommodities(origin);
                if (commodities.Count == 0)
                {
                    continue;
                }

                for (int commodityIndex = 0; commodityIndex < commodities.Count; commodityIndex++)
                {
                    var commodity = commodities[commodityIndex];
                    var availableTons = DispatchCommodityMath.GetOriginAvailableTons(origin, commodity);
                    if (availableTons <= 0.001f)
                    {
                        continue;
                    }

                    var originFillRatio = DispatchCommodityMath.GetCommodityFillRatio(origin, commodity);
                    var originFreeSpace = ResolveCommodityFreeSpace(origin, commodity);
                    if (originFillRatio <= 0.10f)
                    {
                        continue;
                    }

                    for (int destinationIndex = 0; destinationIndex < industries.Count; destinationIndex++)
                    {
                        var destination = industries[destinationIndex];
                        if (destination == null || string.Equals(origin.Id, destination.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!destination.AcceptsCommodity(commodity))
                        {
                            continue;
                        }

                        if (_territoryManager != null)
                        {
                            string routeReason;
                            if (!_territoryManager.CanCreateNpcRouteWithPermits(origin, destination, out routeReason))
                            {
                                continue;
                            }
                        }

                        var destinationFreeTons = DispatchCommodityMath.GetDestinationFreeTons(destination, commodity);
                        if (destinationFreeTons <= 0.001f)
                        {
                            continue;
                        }

                        var destinationStock = Math.Max(0f, destination.GetStock(commodity));
                        var distanceMeters = origin.Position.DistanceTo(destination.Position);
                        if (type == PlayerContractType.QuickJob && distanceMeters > QuickJobMaxDistanceMeters)
                        {
                            continue;
                        }

                        if (type == PlayerContractType.FreightMarket && distanceMeters > FreightMaxDistanceMeters)
                        {
                            continue;
                        }

                        var vehicleSelection = type == PlayerContractType.QuickJob
                            ? ResolveQuickJobVehicleSelection(commodity, Math.Min(availableTons, destinationFreeTons))
                            : null;
                        if (type == PlayerContractType.QuickJob && vehicleSelection == null)
                        {
                            continue;
                        }

                        var imbalanceScore = ComputeImbalanceScore(
                            availableTons,
                            originFreeSpace,
                            destinationStock,
                            destinationFreeTons);
                        if (imbalanceScore <= 0.15f)
                        {
                            continue;
                        }

                        var listedTons = ResolveListedTons(type, availableTons, destinationFreeTons, imbalanceScore, vehicleSelection);
                        if (listedTons <= 0.001f)
                        {
                            continue;
                        }

                        var anchorUnitPrice = _globalMarket != null ? _globalMarket.GetUnitPrice(commodity) : 0f;
                        var quotedUnitPrice = ComputeContractUnitPrice(type, anchorUnitPrice, imbalanceScore, distanceMeters);
                        var score = (imbalanceScore * 100f)
                            + Math.Min(18f, availableTons)
                            + Math.Min(16f, destinationFreeTons)
                            + (type == PlayerContractType.QuickJob
                                ? Math.Max(0f, 16f - (distanceMeters / 450f))
                                : Math.Min(12f, distanceMeters / 1500f));

                        results.Add(new PlayerContractCandidate
                        {
                            Type = type,
                            Commodity = commodity,
                            OriginIndustry = origin,
                            DestinationIndustry = destination,
                            RouteDistanceMeters = distanceMeters,
                            AvailableTons = availableTons,
                            DestinationFreeTons = destinationFreeTons,
                            ImbalanceScore = imbalanceScore,
                            ListedTons = listedTons,
                            QuotedUnitPrice = quotedUnitPrice,
                            VehicleSelection = vehicleSelection,
                            Score = score,
                            RouteKey = BuildRouteKey(type, origin.Id, destination.Id, commodity),
                        });
                    }
                }
            }

            return results;
        }

        private PlayerContractEntry BuildListing(PlayerContractCandidate candidate, int currentMinute)
        {
            if (candidate == null || candidate.OriginIndustry == null || candidate.DestinationIndustry == null)
            {
                return null;
            }

            var expiry = currentMinute + (candidate.Type == PlayerContractType.QuickJob
                ? QuickJobListingLifetimeMinutes
                : FreightListingLifetimeMinutes) + _random.Next(0, 45);

            return new PlayerContractEntry
            {
                Id = BuildContractId(),
                Type = candidate.Type,
                Status = PlayerContractStatus.Listed,
                Commodity = candidate.Commodity,
                OriginIndustryId = candidate.OriginIndustry.Id,
                DestinationIndustryId = candidate.DestinationIndustry.Id,
                ListedTons = candidate.ListedTons,
                LoadedTons = 0f,
                DeliveredTons = 0f,
                RouteDistanceMeters = candidate.RouteDistanceMeters,
                QuotedUnitPrice = candidate.QuotedUnitPrice,
                QuotedGrossPayout = candidate.QuotedUnitPrice * candidate.ListedTons,
                QuotedImbalanceScore = candidate.ImbalanceScore,
                ListedAtMinute = currentMinute,
                ExpiryMinute = expiry,
                AcceptedAtMinute = -1,
                AcceptedExpiryMinute = -1,
                VehicleRequirementLabel = candidate.Type == PlayerContractType.QuickJob
                    ? string.Format(CultureInfo.InvariantCulture, "Supplied vehicle: {0}", candidate.VehicleSelection != null ? candidate.VehicleSelection.DisplayName : "Contract truck")
                    : "Use a compatible company vehicle from the commercial garage.",
                SuppliesVehicle = candidate.Type == PlayerContractType.QuickJob,
                RequiresOwnedVehicle = candidate.Type == PlayerContractType.FreightMarket,
                QuickJobPoweredModelName = candidate.VehicleSelection != null ? candidate.VehicleSelection.PoweredModelName : string.Empty,
                QuickJobCargoModelName = candidate.VehicleSelection != null ? candidate.VehicleSelection.CargoModelName : string.Empty,
                QuickJobHasSeparateCargoVehicle = candidate.VehicleSelection != null && candidate.VehicleSelection.HasSeparateCargoVehicle,
                QuickJobCapacityTons = candidate.VehicleSelection != null ? candidate.VehicleSelection.CapacityTons : 0f,
                QuickJobNeedsDeploy = false,
                CargoCondition = 1f,
                StatusMessage = string.Empty,
            };
        }

        private bool ValidateContractVehicleForLoad(PlayerContractEntry contract, Vehicle cargoVehicle, out string message)
        {
            message = string.Empty;
            if (contract == null || cargoVehicle == null || !cargoVehicle.Exists())
            {
                message = "No contract vehicle is available.";
                return false;
            }

            if (contract.Type == PlayerContractType.QuickJob)
            {
                if (contract.QuickJobNeedsDeploy || !HasLiveQuickJobVehicle(contract))
                {
                    message = "Deploy the supplied Quick Job vehicle from Accepted Contracts before loading.";
                    return false;
                }

                if (cargoVehicle.Handle != contract.QuickJobCargoHandle && cargoVehicle.Handle != contract.QuickJobTruckHandle)
                {
                    message = "Use the supplied Quick Job vehicle for this contract.";
                    return false;
                }

                return true;
            }

            string officeReason;
            if (_propertyManager != null && !_propertyManager.CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            if (_propertyManager == null)
            {
                message = "Company vehicle records are unavailable.";
                return false;
            }

            OwnedCommercialVehiclePersistenceEntry entry;
            if (!_propertyManager.TryResolveCommercialVehicleRecord(cargoVehicle, out entry) || entry == null)
            {
                message = "Use a deployed company commercial vehicle from the office fleet for Freight Market contracts.";
                return false;
            }

            if (!CanCommercialVehicleCarryCommodity(entry, contract.Commodity))
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} cannot carry {1}.", entry.DisplayName ?? "This company vehicle", contract.Commodity);
                return false;
            }

            return true;
        }

        private bool HasCompatibleCompanyVehicle(string commodity, out string reason)
        {
            reason = string.Empty;
            if (_propertyManager == null)
            {
                reason = "Open a commercial office before taking Freight Market work.";
                return false;
            }

            string officeReason;
            if (!_propertyManager.CanUseCommercialSystems(out officeReason))
            {
                reason = officeReason;
                return false;
            }

            var hasCompatible = _propertyManager.CommercialVehicles != null
                && _propertyManager.CommercialVehicles.Any(entry => entry != null && CanCommercialVehicleCarryCommodity(entry, commodity));
            if (!hasCompatible)
            {
                reason = string.Format(CultureInfo.InvariantCulture, "No compatible company vehicle is currently assigned to your office fleet for {0}.", commodity);
                return false;
            }

            return true;
        }

        private bool CanCommercialVehicleCarryCommodity(OwnedCommercialVehiclePersistenceEntry entry, string commodity)
        {
            if (entry == null || _fleetManager == null || string.IsNullOrWhiteSpace(commodity))
            {
                return false;
            }

            var cargoDefinition = _fleetManager.FindDefinitionByModelName(entry.CargoModelName);
            if (cargoDefinition != null && _fleetManager.CanDefinitionCarryCommodity(cargoDefinition, commodity))
            {
                return true;
            }

            var poweredDefinition = _fleetManager.FindDefinitionByModelName(entry.PoweredModelName);
            return poweredDefinition != null && _fleetManager.CanDefinitionCarryCommodity(poweredDefinition, commodity);
        }

        private static float ResolveListedTons(PlayerContractType type, float availableTons, float destinationFreeTons, float imbalanceScore, PlayerContractVehicleSelection vehicleSelection)
        {
            var ceiling = Math.Min(Math.Max(0f, availableTons), Math.Max(0f, destinationFreeTons));
            if (ceiling <= 0.001f)
            {
                return 0f;
            }

            var minTons = type == PlayerContractType.QuickJob ? QuickJobMinTons : FreightMinTons;
            var maxTons = type == PlayerContractType.QuickJob ? QuickJobMaxTons : FreightMaxTons;
            if (type == PlayerContractType.QuickJob && vehicleSelection != null && vehicleSelection.CapacityTons > 0.001f)
            {
                maxTons = Math.Min(maxTons, vehicleSelection.CapacityTons);
            }

            if (ceiling + 0.001f < minTons)
            {
                return 0f;
            }

            var target = minTons + ((maxTons - minTons) * ModMath.Clamp01(imbalanceScore));
            return RoundContractTons(Math.Min(ceiling, target));
        }

        private PlayerContractVehicleSelection ResolveQuickJobVehicleSelection(string commodity, float targetTons)
        {
            if (_fleetManager == null || string.IsNullOrWhiteSpace(commodity))
            {
                return null;
            }

            var compatibleVehicles = _fleetManager.GetSpawnableForCommodity(commodity)
                .Where(definition => definition != null && definition.IsEnabled)
                .ToList();
            if (compatibleVehicles.Count == 0)
            {
                return null;
            }

            var tractors = _fleetManager.GetTractorDefinitions()
                .Where(definition => definition != null && definition.IsEnabled)
                .OrderBy(definition => definition.CapacityTons)
                .ThenBy(definition => definition.DisplayName ?? definition.ModelName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var chosen = compatibleVehicles
                .Where(definition => !definition.IsTrailer || tractors.Count > 0)
                .OrderBy(definition => definition.IsTrailer ? 1 : 0)
                .ThenBy(definition => Math.Abs(Math.Max(1f, definition.CapacityTons) - Math.Max(1f, targetTons)))
                .ThenBy(definition => definition.CapacityTons)
                .ThenBy(definition => definition.DisplayName ?? definition.ModelName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (chosen == null)
            {
                return null;
            }

            var tractor = chosen.IsTrailer ? tractors.FirstOrDefault() : null;
            return new PlayerContractVehicleSelection
            {
                PoweredModelName = tractor != null ? tractor.ModelName : chosen.ModelName,
                CargoModelName = chosen.ModelName,
                HasSeparateCargoVehicle = tractor != null,
                CapacityTons = Math.Max(1f, chosen.CapacityTons),
                DisplayName = tractor != null
                    ? string.Format(CultureInfo.InvariantCulture, "{0} + {1}", tractor.DisplayName ?? tractor.ModelName, chosen.DisplayName ?? chosen.ModelName)
                    : (chosen.DisplayName ?? chosen.ModelName),
            };
        }

        private bool TrySpawnQuickJobVehicle(PlayerContractEntry contract, out Vehicle truck, out Vehicle cargoVehicle, out string message)
        {
            truck = null;
            cargoVehicle = null;
            message = string.Empty;
            if (_fleetManager == null || contract == null)
            {
                message = "Vehicle systems are unavailable.";
                return false;
            }

            var cargoDefinition = _fleetManager.FindDefinitionByModelName(contract.QuickJobCargoModelName);
            var poweredDefinition = contract.QuickJobHasSeparateCargoVehicle
                ? _fleetManager.FindDefinitionByModelName(contract.QuickJobPoweredModelName)
                : cargoDefinition;
            if (cargoDefinition == null || poweredDefinition == null)
            {
                message = "The supplied vehicle definition could not be resolved.";
                return false;
            }

            var spawnPosition = Vector3.Zero;
            var heading = 0f;
            var activeOffice = _propertyManager != null ? _propertyManager.ActiveOffice : null;
            if (activeOffice != null)
            {
                spawnPosition = activeOffice.SpawnPosition;
                heading = activeOffice.SpawnHeading;
            }
            else
            {
                var player = _getPlayer != null ? _getPlayer() : null;
                if (player == null || !player.Exists())
                {
                    message = "Player position unavailable for Quick Job deployment.";
                    return false;
                }

                var forward = player.ForwardVector;
                if (forward.Length() <= 0.001f)
                {
                    forward = HeadingToDirection(player.Heading);
                }

                spawnPosition = player.Position + (forward * 14f);
                heading = player.Heading;
            }

            if (_getGroundPosition != null)
            {
                spawnPosition = _getGroundPosition(spawnPosition);
            }

            if (!_fleetManager.SpawnSelectedVehicle(
                cargoDefinition,
                contract.QuickJobHasSeparateCargoVehicle ? poweredDefinition : null,
                spawnPosition,
                heading,
                out truck,
                out cargoVehicle,
                out message))
            {
                return false;
            }

            return cargoVehicle != null && cargoVehicle.Exists();
        }

        private void CaptureQuickJobRuntimeState(PlayerContractEntry contract)
        {
            if (contract == null || contract.Type != PlayerContractType.QuickJob || !HasLiveQuickJobVehicle(contract))
            {
                return;
            }

            var cargoVehicle = ResolveVehicleHandle(contract.QuickJobCargoHandle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return;
            }

            var cargoState = _fleetManager != null ? _fleetManager.GetOrCreateCargoState(cargoVehicle) : null;
            if (cargoState == null)
            {
                return;
            }

            contract.LoadedTons = Math.Max(0f, cargoState.WeightTons);
            contract.CargoCondition = ModMath.Clamp01(cargoState.CargoCondition);
            contract.TotalLostTons = Math.Max(0f, cargoState.TotalLostTons);
            contract.SourceDistrictName = cargoState.SourceDistrictName ?? string.Empty;
        }

        private void UpdateAcceptedContract(int currentMinute)
        {
            if (_acceptedContract == null || IsTerminal(_acceptedContract.Status))
            {
                return;
            }

            if (_acceptedContract.AcceptedExpiryMinute >= 0 && currentMinute > _acceptedContract.AcceptedExpiryMinute)
            {
                if (_acceptedContract.Status == PlayerContractStatus.Loaded)
                {
                    ForfeitLoadedContractCargo(_acceptedContract);
                }

                _acceptedContract.Status = PlayerContractStatus.Expired;
                _acceptedContract.StatusMessage = "Expired";
                _acceptedContract.QuickJobCleanupPending = _acceptedContract.Type == PlayerContractType.QuickJob;
                RegisterCooldown(_acceptedContract, currentMinute);
                NotifyContractsChanged();
            }

            if (_acceptedContract.Type == PlayerContractType.QuickJob && !HasLiveQuickJobVehicle(_acceptedContract))
            {
                _acceptedContract.QuickJobNeedsDeploy = true;
            }
        }

        private void ForfeitLoadedContractCargo(PlayerContractEntry contract)
        {
            if (contract == null)
            {
                return;
            }

            if (contract.Type == PlayerContractType.QuickJob)
            {
                var cargoVehicle = ResolveVehicleHandle(contract.QuickJobCargoHandle);
                if (cargoVehicle != null && cargoVehicle.Exists())
                {
                    var cargoState = _fleetManager != null ? _fleetManager.GetOrCreateCargoState(cargoVehicle) : null;
                    if (cargoState != null)
                    {
                        cargoState.ClearCargo();
                        _fleetManager.ClearCargoVisuals(cargoState);
                    }
                }

                contract.LoadedTons = 0f;
                return;
            }

            Vehicle cargoCarrier;
            if (TryFindFreightContractVehicle(contract, out cargoCarrier) && cargoCarrier != null && cargoCarrier.Exists())
            {
                var cargoState = _fleetManager != null ? _fleetManager.GetOrCreateCargoState(cargoCarrier) : null;
                if (cargoState != null)
                {
                    cargoState.ClearCargo();
                    _fleetManager.ClearCargoVisuals(cargoState);
                }
            }

            contract.LoadedTons = 0f;
        }

        private bool TryFindFreightContractVehicle(PlayerContractEntry contract, out Vehicle cargoVehicle)
        {
            cargoVehicle = null;
            if (contract == null || _propertyManager == null || _fleetManager == null)
            {
                return false;
            }

            var blips = _propertyManager.GetCommercialVehicleBlipInfos();
            if (blips == null || blips.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < blips.Count; i++)
            {
                var info = blips[i];
                if (info == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(contract.AssignedCommercialVehicleAssetId)
                    && !string.Equals(info.AssetId, contract.AssignedCommercialVehicleAssetId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var truck = Entity.FromHandle(info.TruckHandle) as Vehicle;
                if (truck == null || !truck.Exists())
                {
                    continue;
                }

                var candidateCargoVehicle = truck.TowedVehicle != null && truck.TowedVehicle.Exists()
                    ? truck.TowedVehicle
                    : truck;
                var cargoState = _fleetManager.GetOrCreateCargoState(candidateCargoVehicle);
                if (cargoState == null || !string.Equals(cargoState.PlayerContractId, contract.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cargoVehicle = candidateCargoVehicle;
                return true;
            }

            return false;
        }

        private void ExpireListedContracts(int currentMinute)
        {
            if (_listedContracts.Count == 0)
            {
                return;
            }

            var removed = false;
            for (int i = _listedContracts.Count - 1; i >= 0; i--)
            {
                var contract = _listedContracts[i];
                if (contract == null || contract.ExpiryMinute > currentMinute)
                {
                    continue;
                }

                RegisterCooldown(contract, currentMinute);
                _listedContracts.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                NotifyContractsChanged();
            }
        }

        private void RegisterCooldown(PlayerContractEntry contract, int currentMinute)
        {
            if (contract == null)
            {
                return;
            }

            _routeCooldownUntilMinute[BuildRouteKey(contract.Type, contract.OriginIndustryId, contract.DestinationIndustryId, contract.Commodity)] = currentMinute + RouteCooldownMinutes;
        }

        private bool IsOnCooldown(string routeKey, int currentMinute)
        {
            int availableAgainMinute;
            if (!_routeCooldownUntilMinute.TryGetValue(routeKey ?? string.Empty, out availableAgainMinute))
            {
                return false;
            }

            return availableAgainMinute > currentMinute;
        }

        private void PruneExpiredCooldowns(int currentMinute)
        {
            var expiredKeys = _routeCooldownUntilMinute
                .Where(pair => pair.Value <= currentMinute)
                .Select(pair => pair.Key)
                .ToList();
            for (int i = 0; i < expiredKeys.Count; i++)
            {
                _routeCooldownUntilMinute.Remove(expiredKeys[i]);
            }
        }

        private float ComputeCurrentImbalanceScore(PlayerContractEntry contract)
        {
            if (contract == null)
            {
                return 0f;
            }

            var origin = FindIndustry(contract.OriginIndustryId);
            var destination = FindIndustry(contract.DestinationIndustryId);
            if (origin == null || destination == null)
            {
                return 0f;
            }

            var originStock = DispatchCommodityMath.GetOriginAvailableTons(origin, contract.Commodity);
            var originFreeSpace = ResolveCommodityFreeSpace(origin, contract.Commodity);
            var destinationStock = Math.Max(0f, destination.GetStock(contract.Commodity));
            var destinationFree = DispatchCommodityMath.GetDestinationFreeTons(destination, contract.Commodity);
            return ComputeImbalanceScore(originStock, originFreeSpace, destinationStock, destinationFree);
        }

        private float ComputeCurrentEstimatedPayout(PlayerContractEntry contract)
        {
            if (contract == null)
            {
                return 0f;
            }

            var liveImbalance = ComputeCurrentImbalanceScore(contract);
            var unitPrice = ComputeContractUnitPrice(
                contract.Type,
                _globalMarket != null ? _globalMarket.GetUnitPrice(contract.Commodity) : 0f,
                liveImbalance,
                contract.RouteDistanceMeters);

            var targetTons = contract.Status == PlayerContractStatus.Loaded
                ? Math.Max(0f, contract.LoadedTons)
                : Math.Max(0f, contract.ListedTons);
            return Math.Max(0f, targetTons * unitPrice * Math.Max(0.75f, ModMath.Clamp01(contract.CargoCondition <= 0f ? 1f : contract.CargoCondition)));
        }

        private PlayerContractListingSummary BuildSummary(PlayerContractEntry contract, int currentMinute)
        {
            if (contract == null)
            {
                return null;
            }

            var origin = FindIndustry(contract.OriginIndustryId);
            var destination = FindIndustry(contract.DestinationIndustryId);
            var currentImbalance = ComputeCurrentImbalanceScore(contract);
            return new PlayerContractListingSummary
            {
                Id = contract.Id,
                Type = contract.Type,
                Status = contract.Status,
                Commodity = contract.Commodity,
                OriginIndustryId = contract.OriginIndustryId,
                OriginName = origin != null ? origin.Name : contract.OriginIndustryId,
                DestinationIndustryId = contract.DestinationIndustryId,
                DestinationName = destination != null ? destination.Name : contract.DestinationIndustryId,
                ListedTons = contract.ListedTons,
                LoadedTons = contract.LoadedTons,
                DeliveredTons = contract.DeliveredTons,
                RouteDistanceMeters = contract.RouteDistanceMeters,
                QuotedUnitPrice = contract.QuotedUnitPrice,
                QuotedGrossPayout = contract.QuotedGrossPayout,
                CurrentEstimatedGrossPayout = ComputeCurrentEstimatedPayout(contract),
                QuotedImbalanceScore = contract.QuotedImbalanceScore,
                CurrentImbalanceScore = currentImbalance,
                ExpiryInGameMinute = contract.Status == PlayerContractStatus.Listed ? contract.ExpiryMinute : contract.AcceptedExpiryMinute,
                VehicleRequirementLabel = contract.VehicleRequirementLabel,
                SuppliesVehicle = contract.SuppliesVehicle,
                RequiresOwnedVehicle = contract.RequiresOwnedVehicle,
                CanAccept = contract.Status == PlayerContractStatus.Listed && (_acceptedContract == null || IsTerminal(_acceptedContract.Status)),
                NeedsQuickJobVehicleDeploy = contract.Type == PlayerContractType.QuickJob && contract.QuickJobNeedsDeploy,
                AssignedVehicleDisplayName = contract.AssignedCommercialVehicleDisplayName,
                StageLabel = BuildStageLabel(contract),
                StatusDetail = BuildStatusDetail(contract, currentMinute),
            };
        }

        private bool ShouldIncludeForSelectedCommodity(PlayerContractEntry contract)
        {
            return contract != null
                && (string.IsNullOrWhiteSpace(_selectedCommodityFilter)
                    || CommodityCatalog.IsSameCommodity(_selectedCommodityFilter, contract.Commodity));
        }

        private static string BuildStageLabel(PlayerContractEntry contract)
        {
            if (contract == null)
            {
                return string.Empty;
            }

            switch (contract.Status)
            {
                case PlayerContractStatus.Listed:
                    return "Listed";
                case PlayerContractStatus.Accepted:
                    return "Accepted";
                case PlayerContractStatus.Loaded:
                    return "Loaded";
                case PlayerContractStatus.Completed:
                    return "Completed";
                case PlayerContractStatus.Cancelled:
                    return "Cancelled";
                case PlayerContractStatus.Expired:
                    return "Expired";
                default:
                    return contract.Status.ToString();
            }
        }

        private static string BuildStatusDetail(PlayerContractEntry contract, int currentMinute)
        {
            if (contract == null)
            {
                return string.Empty;
            }

            var expiryMinute = contract.Status == PlayerContractStatus.Listed ? contract.ExpiryMinute : contract.AcceptedExpiryMinute;
            var expiryText = expiryMinute >= 0
                ? string.Format(CultureInfo.InvariantCulture, "Expires in {0}m", Math.Max(0, expiryMinute - currentMinute))
                : string.Empty;

            if (contract.Status == PlayerContractStatus.Loaded)
            {
                var status = string.Format(CultureInfo.InvariantCulture, "Loaded {0} | Delivered {1}", ModFormatting.FormatTons(contract.LoadedTons), ModFormatting.FormatTons(contract.DeliveredTons));
                if (!string.IsNullOrWhiteSpace(expiryText))
                {
                    status += " | " + expiryText;
                }

                return status;
            }

            var baseStatus = string.Format(CultureInfo.InvariantCulture, "{0} | Quote {1}", ModFormatting.FormatTons(contract.ListedTons), ModFormatting.FormatMoney(contract.QuotedGrossPayout));
            if (!string.IsNullOrWhiteSpace(expiryText))
            {
                baseStatus += " | " + expiryText;
            }

            if (contract.Type == PlayerContractType.QuickJob && contract.QuickJobNeedsDeploy)
            {
                baseStatus += " | Vehicle awaiting deploy";
            }

            return baseStatus;
        }

        private static float ResolveCommodityFreeSpace(Industry industry, string commodity)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            var stock = Math.Max(0f, industry.GetStock(commodity));
            var fillRatio = DispatchCommodityMath.GetCommodityFillRatio(industry, commodity);
            if (fillRatio <= 0.001f)
            {
                return stock + Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            }

            return Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
        }

        private IReadOnlyList<string> GetOriginCommodities(Industry industry)
        {
            if (industry == null)
            {
                return Array.Empty<string>();
            }

            var commodities = new List<string>();
            if (industry.SortedOutputs != null)
            {
                for (int i = 0; i < industry.SortedOutputs.Count; i++)
                {
                    var commodity = CommodityCatalog.Normalize(industry.SortedOutputs[i]);
                    if (!string.IsNullOrWhiteSpace(commodity) && !commodities.Contains(commodity, StringComparer.OrdinalIgnoreCase))
                    {
                        commodities.Add(commodity);
                    }
                }
            }

            if (industry.IsWarehouse && industry.SortedAcceptedInputs != null)
            {
                for (int i = 0; i < industry.SortedAcceptedInputs.Count; i++)
                {
                    var commodity = CommodityCatalog.Normalize(industry.SortedAcceptedInputs[i]);
                    if (!string.IsNullOrWhiteSpace(commodity) && !commodities.Contains(commodity, StringComparer.OrdinalIgnoreCase))
                    {
                        commodities.Add(commodity);
                    }
                }
            }

            return commodities;
        }

        private Industry FindIndustry(string industryId)
        {
            if (string.IsNullOrWhiteSpace(industryId))
            {
                return null;
            }

            Industry industry;
            return _industriesById.TryGetValue(industryId.Trim(), out industry)
                ? industry
                : null;
        }

        private bool HasLiveQuickJobVehicle(PlayerContractEntry contract)
        {
            return ResolveVehicleHandle(contract != null ? contract.QuickJobCargoHandle : 0) != null;
        }

        private Vehicle ResolveVehicleHandle(int handle)
        {
            if (handle <= 0)
            {
                return null;
            }

            return Entity.FromHandle(handle) as Vehicle;
        }

        private void TryCleanupTemporaryQuickJobVehicle()
        {
            if (_acceptedContract == null
                || _acceptedContract.Type != PlayerContractType.QuickJob
                || !_acceptedContract.QuickJobCleanupPending)
            {
                return;
            }

            var cargoVehicle = ResolveVehicleHandle(_acceptedContract.QuickJobCargoHandle);
            var truck = ResolveVehicleHandle(_acceptedContract.QuickJobTruckHandle);
            var player = _getPlayer != null ? _getPlayer() : null;
            var occupiedByPlayer = player != null && player.Exists() && player.IsInVehicle()
                && ((cargoVehicle != null && player.CurrentVehicle != null && player.CurrentVehicle.Handle == cargoVehicle.Handle)
                    || (truck != null && player.CurrentVehicle != null && player.CurrentVehicle.Handle == truck.Handle));
            if (occupiedByPlayer)
            {
                return;
            }

            ForceDeleteQuickJobVehicle(_acceptedContract);
        }

        private void ForceDeleteQuickJobVehicle(PlayerContractEntry contract)
        {
            if (contract == null)
            {
                return;
            }

            var cargoVehicle = ResolveVehicleHandle(contract.QuickJobCargoHandle);
            var truck = ResolveVehicleHandle(contract.QuickJobTruckHandle);
            TryDeleteVehicle(cargoVehicle);
            if (truck != null && truck.Exists() && (cargoVehicle == null || truck.Handle != cargoVehicle.Handle))
            {
                TryDeleteVehicle(truck);
            }

            contract.QuickJobTruckHandle = 0;
            contract.QuickJobCargoHandle = 0;
            contract.QuickJobCleanupPending = false;
            contract.QuickJobNeedsDeploy = contract.Status == PlayerContractStatus.Loaded || contract.Status == PlayerContractStatus.Accepted;
        }

        private static void TryDeleteVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            try
            {
                vehicle.Delete();
            }
            catch
            {
                try
                {
                    vehicle.IsVisible = false;
                    vehicle.Position = new Vector3(vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z - 250f);
                    vehicle.Delete();
                }
                catch
                {
                    // Ignore best-effort cleanup failures.
                }
            }
        }

        private static bool IsTerminal(PlayerContractStatus status)
        {
            return status == PlayerContractStatus.Completed
                || status == PlayerContractStatus.Cancelled
                || status == PlayerContractStatus.Expired;
        }

        private int GetCurrentInGameMinute()
        {
            return _getCurrentInGameMinute != null ? _getCurrentInGameMinute() : 0;
        }

        private static float RoundContractTons(float tons)
        {
            return (float)(Math.Round(Math.Max(0d, tons) * 4d, MidpointRounding.AwayFromZero) / 4d);
        }

        private string BuildContractId()
        {
            var id = _nextContractId;
            _nextContractId += 1;
            return string.Format(CultureInfo.InvariantCulture, "PC-{0:D4}", id);
        }

        private static string BuildRouteKey(PlayerContractType type, string originIndustryId, string destinationIndustryId, string commodity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}",
                type,
                originIndustryId ?? string.Empty,
                destinationIndustryId ?? string.Empty,
                CommodityCatalog.Normalize(commodity));
        }

        private void NotifyContractsChanged()
        {
            if (_onContractsChanged != null)
            {
                _onContractsChanged();
            }
        }

        private static Vector3 HeadingToDirection(float heading)
        {
            var radians = heading * (float)Math.PI / 180f;
            return new Vector3(-(float)Math.Sin(radians), (float)Math.Cos(radians), 0f);
        }

        private sealed class PlayerContractCandidate
        {
            public PlayerContractType Type { get; set; }

            public string Commodity { get; set; }

            public Industry OriginIndustry { get; set; }

            public Industry DestinationIndustry { get; set; }

            public float AvailableTons { get; set; }

            public float DestinationFreeTons { get; set; }

            public float ImbalanceScore { get; set; }

            public float ListedTons { get; set; }

            public float RouteDistanceMeters { get; set; }

            public float QuotedUnitPrice { get; set; }

            public float Score { get; set; }

            public string RouteKey { get; set; }

            public PlayerContractVehicleSelection VehicleSelection { get; set; }
        }

    }
}
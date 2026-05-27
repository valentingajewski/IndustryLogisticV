using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum DeliveryProgressSource
    {
        Player = 0,
        Npc = 1,
    }

    public sealed class PlayerCommodityStatisticSnapshot
    {
        public string CommodityId { get; set; }

        public float Tons { get; set; }
    }

    public sealed class PlayerStatisticsPersistenceSnapshot
    {
        public PlayerStatisticsPersistenceSnapshot()
        {
            CommodityTotals = new List<PlayerCommodityStatisticSnapshot>();
            UnlockedSuccessIds = new List<string>();
            IsInitialized = true;
        }

        public bool IsInitialized { get; set; }

        public float HighestCompanyBalanceEver { get; set; }

        public float HighestCompanyBalanceBeforeFirstNpcHire { get; set; }

        public float HighestCompanyBalanceBeforeFirstLoan { get; set; }

        public int TotalSuccessfulDeliveries { get; set; }

        public int TotalSuccessfulCleanDeliveries { get; set; }

        public int DeliveriesBeforeFirstNpcHire { get; set; }

        public float TotalTransportedTons { get; set; }

        public bool HasEverHiredNpc { get; set; }

        public bool HasEverTakenLoan { get; set; }

        public float CumulativeNpcDeliveryIncome { get; set; }

        public int TotalSpecialMissionsCompleted { get; set; }

        public int TotalEmergencyServiceUsages { get; set; }

        public float HighestPrestigeScore { get; set; }

        public string HighestDoctrineId { get; set; }

        public int HighestDoctrineTier { get; set; }

        public List<PlayerCommodityStatisticSnapshot> CommodityTotals { get; }

        public List<string> UnlockedSuccessIds { get; }

        public bool HasData
        {
            get
            {
                return IsInitialized
                    || HighestCompanyBalanceEver > 0.001f
                    || HighestCompanyBalanceBeforeFirstNpcHire > 0.001f
                    || HighestCompanyBalanceBeforeFirstLoan > 0.001f
                    || TotalSuccessfulDeliveries > 0
                    || TotalSuccessfulCleanDeliveries > 0
                    || DeliveriesBeforeFirstNpcHire > 0
                    || TotalTransportedTons > 0.001f
                    || HasEverHiredNpc
                    || HasEverTakenLoan
                    || CumulativeNpcDeliveryIncome > 0.001f
                    || TotalSpecialMissionsCompleted > 0
                    || TotalEmergencyServiceUsages > 0
                    || HighestPrestigeScore > 0.001f
                    || !string.IsNullOrWhiteSpace(HighestDoctrineId)
                    || HighestDoctrineTier > 0
                    || CommodityTotals.Count > 0
                    || UnlockedSuccessIds.Count > 0;
            }
        }
    }

    internal sealed class PlayerSuccessStatus
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsUnlocked { get; set; }

        public float ProgressRatio { get; set; }

        public string ProgressText { get; set; }
    }

    internal sealed partial class PlayerSuccessTracker
    {
        private const string PortOfficeId = "1";
        private const string PortTerminalSiteId = "HarborTerminal";
        private const float MoneyThresholdFirstPaycheck = 10000f;
        private const float MoneyThresholdSmallOperator = 100000f;
        private const float MoneyThresholdMillionaireHauler = 1000000f;
        private const float MoneyThresholdLogisticsTycoon = 10000000f;
        private const float MoneyThresholdMultiMillionCompany = 100000000f;
        private const int DeliveryThresholdFirstDelivery = 1;
        private const int DeliveryThresholdRoadVeteran = 100;
        private const int DeliveryThresholdRoadLegend = 1000;
        private const int DeliveryThresholdEndlessFreight = 10000;
        private const int DeliveryThresholdSoloGrinder = 100;
        private const float TonnageThresholdHeavyHauler = 100f;
        private const float TonnageThresholdIndustrialBackbone = 1000f;
        private const float TonnageThresholdSupplyChainKing = 10000f;
        private const int CleanDeliveryThresholdPerfectionist = 25;
        private const int NpcThresholdDispatcher = 5;
        private const int ActiveNpcRoutesThreshold = 3;
        private const float NpcIncomeThresholdHandsOffIncome = 1000000f;
        private const int IndustryThresholdIndustrialist = 5;
        private const int IndustryThresholdEstablishedIndustrialist = 15;
        private const int FleetThresholdCompanyFleet = 10;
        private const int FleetRoleThresholdMechanizedEmpire = 4;
        private const int SpecialMissionThresholdFirst = 1;
        private const int SpecialMissionThresholdRegular = 10;
        private const int SpecialMissionThresholdExpert = 50;
        private const int EmergencyServiceThreshold = 15;
        private const float ReputationThresholdBuilt = 250f;
        private const float ReputationThresholdStatewide = 100f;
        private const int ReputationDistrictThresholdStatewide = 3;
        private const float CommodityThresholdFreightSpecialist = 500f;
        private const int CommodityThresholdDiversifiedCarrier = 10;
        private const int FounderRequirementCount = 5;
        private const int MegalomaniacRequirementCount = 4;

        private const string SuccessIdRoadVeteran = "road_veteran";
        private const string SuccessIdEndlessFreight = "endless_freight";
        private const string SuccessIdSelfMade = "self_made";
        private const string SuccessIdWorldwideOperator = "worldwide_operator";
        private const string SuccessIdIndustrialMaster = "industrial_master";

        private readonly IndustryManager _industryManager;
        private readonly PropertyManager _propertyManager;
        private readonly NpcLogisticsManager _npcLogisticsManager;
        private readonly TerritoryManager _territoryManager;
        private readonly SpecialMissionManager _specialMissionManager;
        private readonly BankLoanManager _bankLoanManager;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly Action<string, int> _showStatus;
        private readonly Dictionary<string, float> _commodityTonsById;
        private readonly HashSet<string> _unlockedSuccessIds;

        private PlayerStatisticsPersistenceSnapshot _snapshot;
        private bool _hasObservedBalance;
        private float _lastObservedBalance;

        private static readonly IReadOnlyList<PlayerSuccessDefinition> Definitions = BuildDefinitions();

        public PlayerSuccessTracker(
            IndustryManager industryManager,
            PropertyManager propertyManager,
            NpcLogisticsManager npcLogisticsManager,
            TerritoryManager territoryManager,
            SpecialMissionManager specialMissionManager,
            BankLoanManager bankLoanManager,
            CompanyFinanceTracker financeTracker,
            Action<string, int> showStatus)
        {
            _industryManager = industryManager;
            _propertyManager = propertyManager;
            _npcLogisticsManager = npcLogisticsManager;
            _territoryManager = territoryManager;
            _specialMissionManager = specialMissionManager;
            _bankLoanManager = bankLoanManager;
            _financeTracker = financeTracker;
            _showStatus = showStatus;
            _commodityTonsById = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _unlockedSuccessIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _snapshot = new PlayerStatisticsPersistenceSnapshot();

            if (_financeTracker != null)
            {
                _financeTracker.TransactionRecorded += HandleTransactionRecorded;
            }
        }

        public int TotalCount
        {
            get { return Definitions.Count; }
        }

        public int UnlockedCount
        {
            get { return Definitions.Count(definition => _unlockedSuccessIds.Contains(definition.Id)); }
        }

        public bool HasRoadVeteranUnlocked
        {
            get { return HasUnlockedSuccess(SuccessIdRoadVeteran); }
        }

        public bool HasUnlockedSuccess(string successId)
        {
            var normalizedId = NormalizeSuccessId(successId);
            return !string.IsNullOrWhiteSpace(normalizedId) && _unlockedSuccessIds.Contains(normalizedId);
        }

        public void ResetForNewSave(float currentBalance)
        {
            currentBalance = Math.Max(0f, currentBalance);
            _snapshot = new PlayerStatisticsPersistenceSnapshot
            {
                HighestCompanyBalanceEver = currentBalance,
                HighestCompanyBalanceBeforeFirstNpcHire = currentBalance,
                HighestCompanyBalanceBeforeFirstLoan = currentBalance,
            };

            _commodityTonsById.Clear();
            _unlockedSuccessIds.Clear();
            _hasObservedBalance = true;
            _lastObservedBalance = currentBalance;
            ReevaluateCurrentState(false);
        }

        public void ApplyPersistenceSnapshot(PlayerStatisticsPersistenceSnapshot snapshot, float currentBalance)
        {
            currentBalance = Math.Max(0f, currentBalance);
            _commodityTonsById.Clear();
            _unlockedSuccessIds.Clear();

            if (snapshot == null || !snapshot.IsInitialized)
            {
                _snapshot = new PlayerStatisticsPersistenceSnapshot
                {
                    HighestCompanyBalanceEver = currentBalance,
                };
            }
            else
            {
                _snapshot = CloneSnapshot(snapshot);
                _snapshot.HighestCompanyBalanceEver = Math.Max(_snapshot.HighestCompanyBalanceEver, currentBalance);
                if (!_snapshot.HasEverHiredNpc)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstNpcHire = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstNpcHire, currentBalance);
                }

                if (!_snapshot.HasEverTakenLoan)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstLoan = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstLoan, currentBalance);
                }

                if (snapshot.CommodityTotals != null)
                {
                    for (int i = 0; i < snapshot.CommodityTotals.Count; i++)
                    {
                        var entry = snapshot.CommodityTotals[i];
                        var commodityId = CommodityCatalog.Normalize(entry != null ? entry.CommodityId : string.Empty);
                        if (string.IsNullOrWhiteSpace(commodityId) || entry == null || entry.Tons <= 0.001f)
                        {
                            continue;
                        }

                        _commodityTonsById[commodityId] = Math.Max(0f, entry.Tons);
                    }
                }

                if (snapshot.UnlockedSuccessIds != null)
                {
                    for (int i = 0; i < snapshot.UnlockedSuccessIds.Count; i++)
                    {
                        var id = NormalizeSuccessId(snapshot.UnlockedSuccessIds[i]);
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            _unlockedSuccessIds.Add(id);
                        }
                    }
                }
            }

            _hasObservedBalance = true;
            _lastObservedBalance = currentBalance;
        }

        public PlayerStatisticsPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = CloneSnapshot(_snapshot);
            snapshot.CommodityTotals.Clear();
            snapshot.UnlockedSuccessIds.Clear();

            foreach (var entry in _commodityTonsById
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0.001f)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.CommodityTotals.Add(new PlayerCommodityStatisticSnapshot
                {
                    CommodityId = entry.Key,
                    Tons = Math.Max(0f, entry.Value),
                });
            }

            foreach (var successId in _unlockedSuccessIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.UnlockedSuccessIds.Add(successId);
            }

            return snapshot;
        }

        public IReadOnlyList<PlayerSuccessStatus> GetStatuses()
        {
            return Definitions
                .Select(BuildStatus)
                .ToArray();
        }

        public IReadOnlyList<CompanyDoctrineStatus> GetDoctrineStatuses()
        {
            return BuildDoctrineStatuses();
        }

        public CompanyEndgameSummary GetEndgameSummary()
        {
            return BuildEndgameSummary();
        }

        public void UpdateCompanyBalance(float currentBalance, bool notifyUnlocks = true)
        {
            currentBalance = Math.Max(0f, currentBalance);
            if (!_hasObservedBalance)
            {
                _hasObservedBalance = true;
                _lastObservedBalance = currentBalance;
                _snapshot.HighestCompanyBalanceEver = Math.Max(_snapshot.HighestCompanyBalanceEver, currentBalance);
                if (!_snapshot.HasEverHiredNpc)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstNpcHire = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstNpcHire, currentBalance);
                }

                if (!_snapshot.HasEverTakenLoan)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstLoan = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstLoan, currentBalance);
                }

                EvaluateUnlocks(notifyUnlocks);
                return;
            }

            if (currentBalance > _snapshot.HighestCompanyBalanceEver)
            {
                _snapshot.HighestCompanyBalanceEver = currentBalance;
            }

            if (currentBalance > _lastObservedBalance + 0.001f)
            {
                if (!_snapshot.HasEverHiredNpc)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstNpcHire = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstNpcHire, currentBalance);
                }

                if (!_snapshot.HasEverTakenLoan)
                {
                    _snapshot.HighestCompanyBalanceBeforeFirstLoan = Math.Max(_snapshot.HighestCompanyBalanceBeforeFirstLoan, currentBalance);
                }
            }

            _lastObservedBalance = currentBalance;
            EvaluateUnlocks(notifyUnlocks);
        }

        public void RecordDeliveryProgress(string commodity, float deliveredTons, bool completedDelivery, bool isCleanDelivery, DeliveryProgressSource source = DeliveryProgressSource.Player)
        {
            if (source != DeliveryProgressSource.Player)
            {
                return;
            }

            deliveredTons = Math.Max(0f, deliveredTons);
            if (deliveredTons <= 0.001f)
            {
                return;
            }

            _snapshot.TotalTransportedTons += deliveredTons;

            var commodityId = CommodityCatalog.Normalize(commodity);
            if (!string.IsNullOrWhiteSpace(commodityId))
            {
                float currentTons;
                _commodityTonsById.TryGetValue(commodityId, out currentTons);
                _commodityTonsById[commodityId] = currentTons + deliveredTons;
            }

            if (completedDelivery)
            {
                _snapshot.TotalSuccessfulDeliveries += 1;
                if (isCleanDelivery)
                {
                    _snapshot.TotalSuccessfulCleanDeliveries += 1;
                }

                if (!_snapshot.HasEverHiredNpc)
                {
                    _snapshot.DeliveriesBeforeFirstNpcHire += 1;
                }
            }

            ReevaluateCurrentState(true);
        }

        public void NotifyNpcContractsChanged()
        {
            if (GetCurrentNpcDriverCount() > 0)
            {
                _snapshot.HasEverHiredNpc = true;
            }

            ReevaluateCurrentState(true);
        }

        public void RecordSpecialMissionCompleted()
        {
            _snapshot.TotalSpecialMissionsCompleted = Math.Max(_snapshot.TotalSpecialMissionsCompleted + 1, GetExactMissionCompletionCount());
            EvaluateUnlocks(true);
        }

        public void RecordEmergencyServiceUsage()
        {
            _snapshot.TotalEmergencyServiceUsages += 1;
            EvaluateUnlocks(true);
        }

        public void ReevaluateCurrentState(bool notifyUnlocks)
        {
            SyncHistoricalFlagsFromCurrentState();
            _snapshot.TotalSpecialMissionsCompleted = Math.Max(_snapshot.TotalSpecialMissionsCompleted, GetExactMissionCompletionCount());
            EvaluateUnlocks(notifyUnlocks);
        }

        private void HandleTransactionRecorded(CompanyFinanceTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }

            if (transaction.Flow == CompanyFinanceFlow.Income && transaction.Category == CompanyFinanceCategory.NpcDelivery)
            {
                _snapshot.CumulativeNpcDeliveryIncome += Math.Max(0f, transaction.Amount);
                EvaluateUnlocks(true);
                return;
            }

            if (transaction.Flow == CompanyFinanceFlow.Income && transaction.Category == CompanyFinanceCategory.LoanDisbursement)
            {
                _snapshot.HasEverTakenLoan = true;
                EvaluateUnlocks(true);
            }
        }

        private void SyncHistoricalFlagsFromCurrentState()
        {
            if (!_snapshot.HasEverHiredNpc && GetCurrentNpcDriverCount() > 0)
            {
                _snapshot.HasEverHiredNpc = true;
            }

            if (_snapshot.HasEverTakenLoan)
            {
                return;
            }

            if (_bankLoanManager != null && _bankLoanManager.HasActiveLoan)
            {
                _snapshot.HasEverTakenLoan = true;
                return;
            }

            if (_financeTracker != null
                && _financeTracker.Transactions.Any(transaction => transaction != null
                    && transaction.Flow == CompanyFinanceFlow.Income
                    && transaction.Category == CompanyFinanceCategory.LoanDisbursement))
            {
                _snapshot.HasEverTakenLoan = true;
            }
        }

        private void EvaluateUnlocks(bool notifyUnlocks)
        {
            SyncEndgameProgressFromCurrentState();

            for (int i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition == null || _unlockedSuccessIds.Contains(definition.Id))
                {
                    continue;
                }

                var evaluation = definition.Evaluate(this);
                if (evaluation != null && evaluation.IsComplete)
                {
                    Unlock(definition, notifyUnlocks);
                }
            }
        }

        private void Unlock(PlayerSuccessDefinition definition, bool notifyUnlocks)
        {
            if (definition == null || !_unlockedSuccessIds.Add(definition.Id))
            {
                return;
            }

            if (notifyUnlocks && _showStatus != null)
            {
                _showStatus(string.Format("Success unlocked: {0}", definition.Name), 5000);
            }
        }

        private PlayerSuccessStatus BuildStatus(PlayerSuccessDefinition definition)
        {
            var unlocked = definition != null && _unlockedSuccessIds.Contains(definition.Id);
            var evaluation = definition != null
                ? (definition.Evaluate(this) ?? PlayerSuccessEvaluation.Incomplete(0f, string.Empty))
                : PlayerSuccessEvaluation.Incomplete(0f, string.Empty);

            return new PlayerSuccessStatus
            {
                Id = definition != null ? definition.Id : string.Empty,
                Name = definition != null ? definition.Name : string.Empty,
                Description = definition != null ? definition.Description : string.Empty,
                IsUnlocked = unlocked,
                ProgressRatio = unlocked ? 1f : Clamp01(evaluation.ProgressRatio),
                ProgressText = unlocked ? "Unlocked" : evaluation.ProgressText,
            };
        }

        private int GetCurrentNpcDriverCount()
        {
            return _npcLogisticsManager != null && _npcLogisticsManager.Contracts != null
                ? _npcLogisticsManager.Contracts.Count(contract => contract != null)
                : 0;
        }

        private int GetCurrentNpcRouteCount()
        {
            if (_npcLogisticsManager == null || _npcLogisticsManager.Contracts == null)
            {
                return 0;
            }

            return _npcLogisticsManager.Contracts
                .Where(contract => contract != null && contract.Routes != null)
                .Sum(contract => contract.Routes.Count(route => route != null
                    && route.OriginIndustry != null
                    && route.DestinationIndustry != null
                    && !string.IsNullOrWhiteSpace(route.Commodity)));
        }

        private int GetOwnedOfficeCount()
        {
            return GetOfficeDefinitions()
                .Count(office => office != null && GetOfficeState(office.OfficeId)?.IsOwned == true);
        }

        private int GetOfficeAccessCount()
        {
            return GetOfficeDefinitions()
                .Count(office =>
                {
                    var state = GetOfficeState(office != null ? office.OfficeId : null);
                    return state != null && (state.IsOwned || state.IsRented);
                });
        }

        private bool OwnsPortOffice()
        {
            var officeState = GetOfficeState(PortOfficeId);
            return officeState != null && officeState.IsOwned;
        }

        private int GetApartmentAccessCount()
        {
            if (_propertyManager == null || _propertyManager.Interiors == null)
            {
                return 0;
            }

            return _propertyManager.Interiors.Count(interior =>
            {
                var state = interior != null ? GetApartmentState(interior.InteriorId) : null;
                return state != null && (state.IsOwned || state.IsRented);
            });
        }

        private IEnumerable<OwnedCommercialVehiclePersistenceEntry> GetOwnedCommercialVehicles()
        {
            if (_propertyManager == null || _propertyManager.CommercialVehicles == null)
            {
                return Enumerable.Empty<OwnedCommercialVehiclePersistenceEntry>();
            }

            return _propertyManager.CommercialVehicles.Where(vehicle => vehicle != null && !vehicle.IsRental);
        }

        private int GetOwnedCommercialVehicleCount()
        {
            return GetOwnedCommercialVehicles().Count();
        }

        private int GetDistinctOwnedFleetRoles()
        {
            return GetOwnedCommercialVehicles()
                .Select(vehicle => vehicle.CargoType)
                .Where(cargoType => cargoType != VehicleCargoType.Unknown && cargoType != VehicleCargoType.Trailer)
                .Distinct()
                .Count();
        }

        private int GetActiveCorridorCount()
        {
            return _territoryManager != null
                ? _territoryManager.GetActiveCorridorCount()
                : 0;
        }

        private int GetLicensedDistrictCount()
        {
            return GetDistrictStates().Count(state => state != null
                && (state.LicenseStatus == DistrictLicenseStatus.Active || state.LicenseStatus == DistrictLicenseStatus.Probation));
        }

        private int GetSecuredSupportSiteCount()
        {
            if (_territoryManager == null)
            {
                return 0;
            }

            return _territoryManager.GetDepotIndustries().Count(industry =>
            {
                var siteState = _territoryManager.GetSiteState(industry);
                return industry != null
                    && siteState != null
                    && siteState.CrewAssigned
                    && (industry.IsStarterHeadquarters || siteState.ControlLevel != TerritoryControlLevel.None);
            });
        }

        private GarageFillState GetBestOwnedGarageFillState()
        {
            var best = new GarageFillState();
            foreach (var office in GetOfficeDefinitions().Where(entry => entry != null))
            {
                var state = GetOfficeState(office.OfficeId);
                if (state == null || !state.IsOwned)
                {
                    continue;
                }

                var capacity = Math.Max(0, office.MaxCommercialVehicles);
                var count = _propertyManager != null && _propertyManager.CommercialVehicles != null
                    ? _propertyManager.CommercialVehicles.Count(vehicle => vehicle != null
                        && vehicle.InActiveGarage
                        && string.Equals(vehicle.AssignedOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
                    : 0;
                var ratio = capacity > 0 ? (float)count / capacity : 0f;

                if (capacity > best.Capacity || (capacity == best.Capacity && ratio > best.ProgressRatio))
                {
                    best = new GarageFillState
                    {
                        Current = count,
                        Capacity = capacity,
                        ProgressRatio = capacity > 0 ? Clamp01((float)count / capacity) : 0f,
                    };
                }
            }

            return best;
        }

        private IEnumerable<Industry> GetAllSites()
        {
            return _industryManager != null && _industryManager.Industries != null
                ? _industryManager.Industries.Where(industry => industry != null)
                : Enumerable.Empty<Industry>();
        }

        private IEnumerable<Industry> GetActualIndustrialSites()
        {
            return GetAllSites().Where(site => site.LocationKind == LSOL.Config.ExternalLocationKind.Industry
                && !site.IsWarehouse
                && !site.IsConstructionSink
                && site.RequiresPurchase);
        }

        private int GetOwnedActualIndustryCount()
        {
            return GetActualIndustrialSites().Count(site => site.IsOwned);
        }

        private int GetPurchasableActualIndustryCount()
        {
            return GetActualIndustrialSites().Count();
        }

        private int GetOwnedIndustryDistrictCount()
        {
            return GetActualIndustrialSites()
                .Where(site => site.IsOwned && !string.IsNullOrWhiteSpace(site.DistrictName))
                .Select(site => site.DistrictName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private int GetOwnedWarehouseCount()
        {
            return GetAllSites().Count(site => site.IsWarehouse && site.IsOwned);
        }

        private int GetOwnedStoreCount()
        {
            return GetAllSites().Count(site => site.IsStore && site.IsOwned);
        }

        private int GetOwnedGasStationCount()
        {
            return GetAllSites().Count(site => site.IsGasStation && site.IsOwned);
        }

        private int GetTotalFranchiseSiteCount()
        {
            return GetDistrictStates().Sum(state => state != null ? Math.Max(0, state.FranchiseSites) : 0);
        }

        private int GetCompetitiveWinCount()
        {
            return GetDistrictStates().Sum(state => state != null ? Math.Max(0, state.CompetitiveWinCount) : 0);
        }

        private int GetLowPressureDominantDistrictCount()
        {
            return GetDistrictStates().Count(state => state != null
                && string.Equals(state.ReputationLabel, "Dominant", StringComparison.OrdinalIgnoreCase)
                && state.CompetitivePressure <= 0.35f);
        }

        private bool HasLandmarkHeadquarters()
        {
            return _propertyManager != null
                && _propertyManager.HasAnyOfficeObjectFunction(OfficeObjectFunction.Headquarters);
        }

        private bool OwnsPortTerminal()
        {
            return GetAllSites().Any(site => site.IsOwned && string.Equals(site.Id, PortTerminalSiteId, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasOwnedProducerConsumerMatch()
        {
            var ownedProducers = GetActualIndustrialSites().Where(site => site.IsOwned && site.Outputs != null && site.Outputs.Count > 0).ToList();
            if (ownedProducers.Count == 0)
            {
                return false;
            }

            var ownedConsumers = GetAllSites().Where(site => site.IsOwned && !site.IsWarehouse).ToList();
            if (ownedConsumers.Count == 0)
            {
                return false;
            }

            for (int producerIndex = 0; producerIndex < ownedProducers.Count; producerIndex++)
            {
                var producer = ownedProducers[producerIndex];
                foreach (var commodity in producer.Outputs)
                {
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    if (ownedConsumers.Any(consumer => consumer != null
                        && !string.Equals(consumer.Id, producer.Id, StringComparison.OrdinalIgnoreCase)
                        && consumer.AcceptsCommodity(commodity)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetBestDistrictReputationScore()
        {
            return GetDistrictStates().DefaultIfEmpty(null).Max(state => state != null ? Math.Max(0f, state.ReputationScore) : 0f);
        }

        private int GetDistrictCountAtReputation(float threshold)
        {
            return GetDistrictStates().Count(state => state != null && state.ReputationScore >= threshold);
        }

        private int GetDominantDistrictCount()
        {
            return GetDistrictStates().Count(state => state != null && string.Equals(state.ReputationLabel, "Dominant", StringComparison.OrdinalIgnoreCase));
        }

        private int GetTotalDistrictCount()
        {
            return GetDistrictStates().Count();
        }

        private int GetDistinctDeliveredCommodityCount()
        {
            return _commodityTonsById.Count(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0.001f);
        }

        private float GetBestCommodityTonnage()
        {
            return _commodityTonsById.Count == 0
                ? 0f
                : _commodityTonsById.Max(pair => Math.Max(0f, pair.Value));
        }

        private int GetFounderProgressCount()
        {
            var count = 0;
            if (GetOfficeAccessCount() > 0)
            {
                count += 1;
            }

            if (GetApartmentAccessCount() > 0)
            {
                count += 1;
            }

            if (GetOwnedActualIndustryCount() > 0)
            {
                count += 1;
            }

            if (GetOwnedWarehouseCount() > 0)
            {
                count += 1;
            }

            if (GetOwnedCommercialVehicleCount() > 0)
            {
                count += 1;
            }

            return count;
        }

        private int GetDoctrineTier(CompanyDoctrine doctrine)
        {
            return BuildDoctrineStatuses()
                .Where(status => status != null && status.Doctrine == doctrine)
                .Select(status => Math.Max(0, status.Tier))
                .DefaultIfEmpty(0)
                .Max();
        }

        private float GetCurrentPrestigeScore()
        {
            return BuildEndgameSummary().PrestigeScore;
        }

        private int GetMegalomaniacProgressCount()
        {
            var count = 0;
            if (_unlockedSuccessIds.Contains(SuccessIdEndlessFreight))
            {
                count += 1;
            }

            if (_unlockedSuccessIds.Contains(SuccessIdSelfMade))
            {
                count += 1;
            }

            if (_unlockedSuccessIds.Contains(SuccessIdWorldwideOperator))
            {
                count += 1;
            }

            if (_unlockedSuccessIds.Contains(SuccessIdIndustrialMaster))
            {
                count += 1;
            }

            return count;
        }

        private int GetExactMissionCompletionCount()
        {
            if (_specialMissionManager == null || _specialMissionManager.Definitions == null)
            {
                return Math.Max(0, _snapshot.TotalSpecialMissionsCompleted);
            }

            var total = 0;
            foreach (var definition in _specialMissionManager.Definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                total += Math.Max(0, _specialMissionManager.GetCompletionCount(definition.Id));
            }

            return Math.Max(total, _snapshot.TotalSpecialMissionsCompleted);
        }

        private IEnumerable<OfficeDefinition> GetOfficeDefinitions()
        {
            return _propertyManager != null && _propertyManager.Offices != null
                ? _propertyManager.Offices.Where(office => office != null)
                : Enumerable.Empty<OfficeDefinition>();
        }

        private OfficeOwnershipPersistenceEntry GetOfficeState(string officeId)
        {
            return _propertyManager != null && !string.IsNullOrWhiteSpace(officeId)
                ? _propertyManager.GetOfficeState(officeId)
                : null;
        }

        private ApartmentOwnershipPersistenceEntry GetApartmentState(string interiorId)
        {
            return _propertyManager != null && !string.IsNullOrWhiteSpace(interiorId)
                ? _propertyManager.GetApartmentState(interiorId)
                : null;
        }

        private IEnumerable<TerritoryDistrictState> GetDistrictStates()
        {
            return _territoryManager != null && _territoryManager.DistrictStates != null
                ? _territoryManager.DistrictStates.Where(state => state != null)
                : Enumerable.Empty<TerritoryDistrictState>();
        }

        private sealed class DoctrineEvaluationResult
        {
            public IReadOnlyList<CompanyDoctrineStatus> Statuses { get; set; }

            public CompanyDoctrineStatus ActiveStatus { get; set; }

            public CompanyDoctrineLeadSummary LeadSummary { get; set; }

            public bool HasLandmarkHeadquarters { get; set; }
        }

        private IReadOnlyList<CompanyDoctrineStatus> BuildDoctrineStatuses()
        {
            return BuildDoctrineEvaluation().Statuses;
        }

        private DoctrineEvaluationResult BuildDoctrineEvaluation()
        {
            var statuses = new[]
            {
                BuildTerritorialDoctrineStatus(),
                BuildIndustrialDoctrineStatus(),
                BuildServiceDoctrineStatus(),
            };

            var activeDoctrine = DetermineActiveDoctrine(statuses);
            var hasLandmarkHeadquarters = HasLandmarkHeadquarters();
            CompanyDoctrineStatus activeStatus = null;
            for (int i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                if (status == null)
                {
                    continue;
                }

                status.IsActive = status.Tier > 0 && status.Doctrine == activeDoctrine;
                status.EffectiveTier = CompanyDoctrineSystem.GetEffectiveTier(status.Tier, hasLandmarkHeadquarters, status.IsActive);
                status.HasHeadquartersBoost = status.EffectiveTier > status.Tier;
                status.BonusSummary = CompanyDoctrineSystem.BuildBonusSummary(status.Doctrine, status.EffectiveTier);
                status.TradeoffSummary = CompanyDoctrineSystem.BuildTradeoffSummary(status.Doctrine, status.EffectiveTier);
                if (status.IsActive)
                {
                    activeStatus = status;
                }
            }

            var leadSummary = BuildDoctrineLeadSummary(statuses, activeStatus);
            for (int i = 0; i < statuses.Length; i++)
            {
                PopulateDoctrineContext(statuses[i], leadSummary, hasLandmarkHeadquarters);
            }

            return new DoctrineEvaluationResult
            {
                Statuses = statuses,
                ActiveStatus = activeStatus,
                LeadSummary = leadSummary,
                HasLandmarkHeadquarters = hasLandmarkHeadquarters,
            };
        }

        private CompanyEndgameSummary BuildEndgameSummary()
        {
            var doctrineEvaluation = BuildDoctrineEvaluation();
            var doctrines = doctrineEvaluation.Statuses.ToArray();
            var activeDoctrine = doctrineEvaluation.ActiveStatus;
            var hasLandmarkHeadquarters = doctrineEvaluation.HasLandmarkHeadquarters;
            var dominantDistricts = GetDominantDistrictCount();
            var competitiveWins = GetCompetitiveWinCount();
            var ownedIndustries = GetOwnedActualIndustryCount();
            var doctrinePrestige = activeDoctrine != null ? activeDoctrine.EffectiveTier * 12f : 0f;
            var headquartersPrestige = hasLandmarkHeadquarters ? 24f : 0f;
            var districtPrestige = Math.Min(24f, dominantDistricts * 6f);
            var industryPrestige = Math.Min(14f, ownedIndustries * 1.2f);
            var competitionPrestige = Math.Min(12f, competitiveWins * 1.5f);
            var missionPrestige = Math.Min(14f, _snapshot.TotalSpecialMissionsCompleted * 1.2f);
            var prestige = doctrinePrestige
                + headquartersPrestige
                + districtPrestige
                + industryPrestige
                + competitionPrestige
                + missionPrestige;
            prestige = Math.Max(0f, Math.Min(100f, prestige));
            var prestigeBreakdown = BuildPrestigeBreakdown(
                activeDoctrine,
                doctrineEvaluation.LeadSummary,
                hasLandmarkHeadquarters,
                dominantDistricts,
                ownedIndustries,
                competitiveWins,
                _snapshot.TotalSpecialMissionsCompleted,
                prestige,
                doctrinePrestige,
                headquartersPrestige,
                districtPrestige,
                industryPrestige,
                competitionPrestige,
                missionPrestige);
            var primaryOpportunity = BuildPrimaryOpportunitySummary(doctrines, activeDoctrine, doctrineEvaluation.LeadSummary, hasLandmarkHeadquarters, prestigeBreakdown);

            var activeDoctrineName = activeDoctrine != null
                ? activeDoctrine.Name ?? CompanyDoctrineSystem.GetName(activeDoctrine.Doctrine)
                : CompanyDoctrineSystem.GetName(CompanyDoctrine.Balanced);
            var headline = activeDoctrine != null
                ? string.Format("{0} {1}", activeDoctrineName, CompanyDoctrineSystem.BuildTierLabel(activeDoctrine.EffectiveTier))
                : (hasLandmarkHeadquarters ? "Landmark HQ Online" : "Endgame posture forming");
            var detail = string.Format(
                "Prestige {0:0}/100 | HQ {1} | {2} dominant districts{3}",
                prestige,
                hasLandmarkHeadquarters ? "Online" : "Offline",
                dominantDistricts,
                competitiveWins > 0 ? string.Format(" | {0} competition wins", competitiveWins) : string.Empty);

            return new CompanyEndgameSummary
            {
                ActiveDoctrine = activeDoctrine != null ? activeDoctrine.Doctrine : CompanyDoctrine.Balanced,
                ActiveDoctrineName = activeDoctrineName,
                ActiveDoctrineTier = activeDoctrine != null ? activeDoctrine.Tier : 0,
                ActiveDoctrineEffectiveTier = activeDoctrine != null ? activeDoctrine.EffectiveTier : 0,
                HasLandmarkHeadquarters = hasLandmarkHeadquarters,
                PrestigeScore = prestige,
                HighestPrestigeScore = Math.Max(_snapshot.HighestPrestigeScore, prestige),
                DominantDistrictCount = dominantDistricts,
                CompetitiveWinCount = competitiveWins,
                Headline = headline,
                Detail = detail,
                DoctrineLead = doctrineEvaluation.LeadSummary,
                PrestigeBreakdown = prestigeBreakdown,
                PrimaryOpportunitySummary = primaryOpportunity,
            };
        }

        private void SyncEndgameProgressFromCurrentState()
        {
            var endgame = BuildEndgameSummary();
            if (endgame == null)
            {
                return;
            }

            _snapshot.HighestPrestigeScore = Math.Max(_snapshot.HighestPrestigeScore, Math.Max(0f, endgame.PrestigeScore));
            if (endgame.ActiveDoctrineTier < _snapshot.HighestDoctrineTier)
            {
                return;
            }

            if (endgame.ActiveDoctrineTier == _snapshot.HighestDoctrineTier
                && !string.IsNullOrWhiteSpace(_snapshot.HighestDoctrineId))
            {
                return;
            }

            _snapshot.HighestDoctrineTier = Math.Max(0, endgame.ActiveDoctrineTier);
            _snapshot.HighestDoctrineId = endgame.ActiveDoctrine == CompanyDoctrine.Balanced
                ? string.Empty
                : endgame.ActiveDoctrine.ToString();
        }

        private CompanyDoctrineStatus BuildTerritorialDoctrineStatus()
        {
            var districtTarget = Math.Max(1, Math.Min(3, Math.Max(1, GetTotalDistrictCount())));
            var corridorTarget = Math.Max(2, Math.Min(4, Math.Max(2, GetTotalDistrictCount())));
            var supportTarget = 3;
            var charterTarget = districtTarget;
            var dominantDistricts = GetDominantDistrictCount();
            var activeCorridors = GetActiveCorridorCount();
            var securedSupportSites = GetSecuredSupportSiteCount();
            var licensedDistricts = GetLicensedDistrictCount();
            var components = new[]
            {
                BuildDoctrineProgressComponent(
                    "dominant-districts",
                    "Dominant districts",
                    dominantDistricts,
                    districtTarget,
                    0.35f,
                    string.Format("{0}/{1} dominant districts", dominantDistricts, districtTarget),
                    "Secure 1 more dominant district"),
                BuildDoctrineProgressComponent(
                    "active-corridors",
                    "Active corridors",
                    activeCorridors,
                    corridorTarget,
                    0.25f,
                    string.Format("{0}/{1} active corridors", activeCorridors, corridorTarget),
                    "Open 1 more active corridor"),
                BuildDoctrineProgressComponent(
                    "secured-depots",
                    "Secured depots",
                    securedSupportSites,
                    supportTarget,
                    0.20f,
                    string.Format("{0}/{1} secured depots", securedSupportSites, supportTarget),
                    "Crew 1 more support depot"),
                BuildDoctrineProgressComponent(
                    "district-charters",
                    "District charters",
                    licensedDistricts,
                    charterTarget,
                    0.20f,
                    string.Format("{0}/{1} district charters", licensedDistricts, charterTarget),
                    "Stabilize 1 more district charter"),
            };
            var progressRatio = Clamp01(
                components.Sum(component => component != null ? component.ContributionRatio : 0f));

            return BuildDoctrineStatus(
                CompanyDoctrine.Territorial,
                progressRatio,
                components,
                string.Format(
                    "{0}/{1} dominant | {2}/{3} corridors | {4}/{5} depots | {6}/{7} charters",
                    dominantDistricts,
                    districtTarget,
                    activeCorridors,
                    corridorTarget,
                    securedSupportSites,
                    supportTarget,
                    licensedDistricts,
                    charterTarget));
        }

        private CompanyDoctrineStatus BuildIndustrialDoctrineStatus()
        {
            var industryTarget = 8;
            var warehouseTarget = 3;
            var commodityTarget = 6;
            var ownedIndustries = GetOwnedActualIndustryCount();
            var ownedWarehouses = GetOwnedWarehouseCount();
            var deliveredCommodities = GetDistinctDeliveredCommodityCount();
            var hasSupplyChain = HasOwnedProducerConsumerMatch();
            var components = new[]
            {
                BuildDoctrineProgressComponent(
                    "owned-industries",
                    "Owned industries",
                    ownedIndustries,
                    industryTarget,
                    0.30f,
                    string.Format("{0}/{1} owned industries", ownedIndustries, industryTarget),
                    "Buy 1 more production site"),
                BuildDoctrineProgressComponent(
                    "warehouses",
                    "Warehouses",
                    ownedWarehouses,
                    warehouseTarget,
                    0.20f,
                    string.Format("{0}/{1} owned warehouses", ownedWarehouses, warehouseTarget),
                    "Buy 1 more warehouse"),
                BuildDoctrineProgressComponent(
                    "owned-chain",
                    "Owned chain",
                    hasSupplyChain ? 1f : 0f,
                    1f,
                    0.30f,
                    string.Format("Owned producer-consumer chain {0}", hasSupplyChain ? "online" : "offline"),
                    "Connect one owned producer to one owned consumer"),
                BuildDoctrineProgressComponent(
                    "delivered-commodities",
                    "Delivered commodities",
                    deliveredCommodities,
                    commodityTarget,
                    0.20f,
                    string.Format("{0}/{1} delivered commodities", deliveredCommodities, commodityTarget),
                    "Deliver 1 new commodity type"),
            };
            var progressRatio = Clamp01(
                components.Sum(component => component != null ? component.ContributionRatio : 0f));

            return BuildDoctrineStatus(
                CompanyDoctrine.Industrial,
                progressRatio,
                components,
                string.Format(
                    "{0}/{1} industries | {2}/{3} warehouses | Chain {4} | {5}/{6} commodities",
                    ownedIndustries,
                    industryTarget,
                    ownedWarehouses,
                    warehouseTarget,
                    hasSupplyChain ? "Online" : "Offline",
                    deliveredCommodities,
                    commodityTarget));
        }

        private CompanyDoctrineStatus BuildServiceDoctrineStatus()
        {
            var serviceSiteTarget = 4;
            var franchiseTarget = 4;
            var routeTarget = 4;
            var missionTarget = 6;
            var serviceSites = GetOwnedGasStationCount() + GetOwnedStoreCount();
            var franchiseSites = GetTotalFranchiseSiteCount();
            var activeRoutes = GetCurrentNpcRouteCount();
            var missions = _snapshot.TotalSpecialMissionsCompleted;
            var components = new[]
            {
                BuildDoctrineProgressComponent(
                    "service-sites",
                    "Service sites",
                    serviceSites,
                    serviceSiteTarget,
                    0.30f,
                    string.Format("{0}/{1} owned service sites", serviceSites, serviceSiteTarget),
                    "Acquire 1 more service site"),
                BuildDoctrineProgressComponent(
                    "franchise-sites",
                    "Franchise sites",
                    franchiseSites,
                    franchiseTarget,
                    0.20f,
                    string.Format("{0}/{1} franchise sites", franchiseSites, franchiseTarget),
                    "Expand into 1 more franchise site"),
                BuildDoctrineProgressComponent(
                    "active-routes",
                    "Active routes",
                    activeRoutes,
                    routeTarget,
                    0.25f,
                    string.Format("{0}/{1} active NPC routes", activeRoutes, routeTarget),
                    "Run 1 more NPC route"),
                BuildDoctrineProgressComponent(
                    "special-missions",
                    "Special missions",
                    missions,
                    missionTarget,
                    0.25f,
                    string.Format("{0}/{1} special missions", missions, missionTarget),
                    "Complete 1 more special mission"),
            };
            var progressRatio = Clamp01(
                components.Sum(component => component != null ? component.ContributionRatio : 0f));

            return BuildDoctrineStatus(
                CompanyDoctrine.Service,
                progressRatio,
                components,
                string.Format(
                    "{0}/{1} service sites | {2}/{3} franchises | {4}/{5} routes | {6}/{7} missions",
                    serviceSites,
                    serviceSiteTarget,
                    franchiseSites,
                    franchiseTarget,
                    activeRoutes,
                    routeTarget,
                    missions,
                    missionTarget));
        }

        private static CompanyDoctrine DetermineActiveDoctrine(IEnumerable<CompanyDoctrineStatus> statuses)
        {
            var active = (statuses ?? Enumerable.Empty<CompanyDoctrineStatus>())
                .Where(status => status != null && status.Tier > 0)
                .OrderByDescending(status => status.Tier)
                .ThenByDescending(status => status.ProgressRatio)
                .ThenByDescending(status => GetDoctrinePriority(status.Doctrine))
                .FirstOrDefault();
            return active != null ? active.Doctrine : CompanyDoctrine.Balanced;
        }

        private static int GetDoctrinePriority(CompanyDoctrine doctrine)
        {
            switch (doctrine)
            {
                case CompanyDoctrine.Industrial:
                    return 3;
                case CompanyDoctrine.Territorial:
                    return 2;
                case CompanyDoctrine.Service:
                    return 1;
                default:
                    return 0;
            }
        }

        private static CompanyDoctrineStatus BuildDoctrineStatus(
            CompanyDoctrine doctrine,
            float progressRatio,
            IReadOnlyList<CompanyDoctrineProgressComponent> components,
            string progressText)
        {
            return new CompanyDoctrineStatus
            {
                Doctrine = doctrine,
                Name = CompanyDoctrineSystem.GetName(doctrine),
                FocusSummary = CompanyDoctrineSystem.GetFocusSummary(doctrine),
                Tier = CompanyDoctrineSystem.ResolveTier(progressRatio),
                EffectiveTier = 0,
                ProgressRatio = Clamp01(progressRatio),
                ProgressText = progressText ?? string.Empty,
                BonusSummary = string.Empty,
                TradeoffSummary = string.Empty,
                IsActive = false,
                HasHeadquartersBoost = false,
                LeadingDoctrine = CompanyDoctrine.Balanced,
                LeadReasonType = CompanyDoctrineLeadReasonType.None,
                ProgressComponents = components ?? Array.Empty<CompanyDoctrineProgressComponent>(),
                NextTier = 0,
                NextTierTargetProgressRatio = 0f,
                NextTierGapProgressRatio = 0f,
                NextTierSummary = string.Empty,
                LeadSummary = string.Empty,
                SteeringSummary = string.Empty,
            };
        }

        private static CompanyDoctrineProgressComponent BuildDoctrineProgressComponent(
            string key,
            string label,
            float currentValue,
            float targetValue,
            float weight,
            string statusText,
            string nextStepText)
        {
            var completionRatio = BuildRatio(currentValue, targetValue);
            var missingValue = Math.Max(0f, targetValue - currentValue);
            var currentContribution = completionRatio * weight;
            float nextContribution;
            if (targetValue <= 1f)
            {
                nextContribution = currentValue >= 1f ? currentContribution : weight;
            }
            else
            {
                nextContribution = BuildRatio(currentValue + Math.Min(1f, missingValue), targetValue) * weight;
            }

            return new CompanyDoctrineProgressComponent
            {
                Key = key ?? string.Empty,
                Label = label ?? string.Empty,
                CurrentValue = Math.Max(0f, currentValue),
                TargetValue = Math.Max(0f, targetValue),
                MissingValue = missingValue,
                CompletionRatio = completionRatio,
                Weight = Math.Max(0f, weight),
                ContributionRatio = currentContribution,
                PotentialStepProgressRatio = Math.Max(0f, nextContribution - currentContribution),
                StatusText = statusText ?? string.Empty,
                NextStepText = nextStepText ?? string.Empty,
            };
        }

        private static CompanyDoctrineLeadSummary BuildDoctrineLeadSummary(IReadOnlyList<CompanyDoctrineStatus> statuses, CompanyDoctrineStatus activeStatus)
        {
            var allStatuses = (statuses ?? Array.Empty<CompanyDoctrineStatus>())
                .Where(status => status != null)
                .ToArray();
            var activeCandidates = allStatuses
                .Where(status => status.Tier > 0)
                .OrderByDescending(status => status.Tier)
                .ThenByDescending(status => status.ProgressRatio)
                .ThenByDescending(status => GetDoctrinePriority(status.Doctrine))
                .ToArray();
            var inactiveCandidates = allStatuses
                .OrderByDescending(status => status.ProgressRatio)
                .ThenByDescending(status => GetDoctrinePriority(status.Doctrine))
                .ToArray();
            var leader = activeStatus ?? activeCandidates.FirstOrDefault() ?? inactiveCandidates.FirstOrDefault();
            var runnerUp = activeStatus != null
                ? activeCandidates.Skip(1).FirstOrDefault()
                : inactiveCandidates.Skip(1).FirstOrDefault();
            var tieBreakSummary = "Lead order: tier, then progress, then Industrial > Territorial > Service.";
            if (leader == null)
            {
                return new CompanyDoctrineLeadSummary
                {
                    LeadingDoctrine = CompanyDoctrine.Balanced,
                    LeadingDoctrineName = CompanyDoctrineSystem.GetName(CompanyDoctrine.Balanced),
                    ReasonType = CompanyDoctrineLeadReasonType.None,
                    ReasonSummary = "No doctrine data available.",
                    TieBreakSummary = tieBreakSummary,
                };
            }

            var summary = new CompanyDoctrineLeadSummary
            {
                LeadingDoctrine = leader.Doctrine,
                LeadingDoctrineName = leader.Name ?? CompanyDoctrineSystem.GetName(leader.Doctrine),
                LeadingTier = Math.Max(0, leader.Tier),
                LeadingProgressRatio = Clamp01(leader.ProgressRatio),
                RunnerUpDoctrine = runnerUp != null ? runnerUp.Doctrine : CompanyDoctrine.Balanced,
                RunnerUpDoctrineName = runnerUp != null ? runnerUp.Name ?? CompanyDoctrineSystem.GetName(runnerUp.Doctrine) : string.Empty,
                RunnerUpTier = runnerUp != null ? Math.Max(0, runnerUp.Tier) : 0,
                RunnerUpProgressRatio = runnerUp != null ? Clamp01(runnerUp.ProgressRatio) : 0f,
                TierGap = runnerUp != null ? Math.Max(0, leader.Tier - runnerUp.Tier) : Math.Max(0, leader.Tier),
                ProgressGapRatio = runnerUp != null ? Math.Max(0f, leader.ProgressRatio - runnerUp.ProgressRatio) : Clamp01(leader.ProgressRatio),
                TieBreakSummary = tieBreakSummary,
            };

            if (activeStatus == null)
            {
                if (leader.ProgressRatio <= 0.001f)
                {
                    summary.ReasonType = CompanyDoctrineLeadReasonType.None;
                    summary.ReasonSummary = "No doctrine has any progress yet.";
                    return summary;
                }

                if (runnerUp != null && leader.ProgressRatio > runnerUp.ProgressRatio + 0.0005f)
                {
                    summary.ReasonType = CompanyDoctrineLeadReasonType.ProgressRatio;
                    summary.ReasonSummary = string.Format(
                        "No doctrine has reached Tier I yet. {0} is closest at {1:0}%.",
                        summary.LeadingDoctrineName,
                        summary.LeadingProgressRatio * 100f);
                    return summary;
                }

                if (runnerUp != null && GetDoctrinePriority(leader.Doctrine) > GetDoctrinePriority(runnerUp.Doctrine))
                {
                    summary.ReasonType = CompanyDoctrineLeadReasonType.DoctrinePriority;
                    summary.ReasonSummary = string.Format(
                        "No doctrine has reached Tier I yet. {0} currently wins the tie-break over {1}.",
                        summary.LeadingDoctrineName,
                        summary.RunnerUpDoctrineName);
                    return summary;
                }

                summary.ReasonType = CompanyDoctrineLeadReasonType.None;
                summary.ReasonSummary = string.Format(
                    "No doctrine has reached Tier I yet. {0} is closest at {1:0}%.",
                    summary.LeadingDoctrineName,
                    summary.LeadingProgressRatio * 100f);
                return summary;
            }

            if (runnerUp == null)
            {
                summary.ReasonType = CompanyDoctrineLeadReasonType.None;
                summary.ReasonSummary = string.Format("{0} is the only doctrine at Tier I or above.", summary.LeadingDoctrineName);
                return summary;
            }

            if (leader.Tier > runnerUp.Tier)
            {
                summary.ReasonType = CompanyDoctrineLeadReasonType.Tier;
                summary.ReasonSummary = string.Format("{0} leads on tier over {1}.", summary.LeadingDoctrineName, summary.RunnerUpDoctrineName);
                return summary;
            }

            if (leader.ProgressRatio > runnerUp.ProgressRatio + 0.0005f)
            {
                summary.ReasonType = CompanyDoctrineLeadReasonType.ProgressRatio;
                summary.ReasonSummary = string.Format(
                    "{0} leads on progress at Tier {1}, ahead of {2} by {3:0}%.",
                    summary.LeadingDoctrineName,
                    CompanyDoctrineSystem.BuildTierLabel(summary.LeadingTier),
                    summary.RunnerUpDoctrineName,
                    summary.ProgressGapRatio * 100f);
                return summary;
            }

            summary.ReasonType = CompanyDoctrineLeadReasonType.DoctrinePriority;
            summary.ReasonSummary = string.Format(
                "{0} wins the tie-break over {1} on doctrine priority.",
                summary.LeadingDoctrineName,
                summary.RunnerUpDoctrineName);
            return summary;
        }

        private static void PopulateDoctrineContext(CompanyDoctrineStatus status, CompanyDoctrineLeadSummary leadSummary, bool hasLandmarkHeadquarters)
        {
            if (status == null)
            {
                return;
            }

            status.LeadingDoctrine = leadSummary != null ? leadSummary.LeadingDoctrine : CompanyDoctrine.Balanced;
            status.LeadReasonType = leadSummary != null ? leadSummary.ReasonType : CompanyDoctrineLeadReasonType.None;
            status.NextTier = ResolveNextTier(status.Tier);
            status.NextTierTargetProgressRatio = CompanyDoctrineSystem.GetNextTierThreshold(status.Tier);
            status.NextTierGapProgressRatio = status.NextTierTargetProgressRatio > 0f
                ? Math.Max(0f, status.NextTierTargetProgressRatio - status.ProgressRatio)
                : 0f;
            status.NextTierSummary = BuildDoctrineNextTierSummary(status, hasLandmarkHeadquarters);
            status.LeadSummary = BuildDoctrineLeadSummaryForStatus(status, leadSummary);
            status.SteeringSummary = BuildDoctrineSteeringSummary(status, hasLandmarkHeadquarters);
        }

        private static int ResolveNextTier(int tier)
        {
            switch (Math.Max(0, tier))
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 3;
                default:
                    return 0;
            }
        }

        private static string BuildDoctrineNextTierSummary(CompanyDoctrineStatus status, bool hasLandmarkHeadquarters)
        {
            if (status == null)
            {
                return string.Empty;
            }

            if (status.NextTier > 0 && status.NextTierGapProgressRatio > 0.0005f)
            {
                return string.Format(
                    "Needs +{0:0}% doctrine progress to reach Tier {1}.",
                    status.NextTierGapProgressRatio * 100f,
                    CompanyDoctrineSystem.BuildTierLabel(status.NextTier));
            }

            if (status.IsActive && status.Tier > 0 && !hasLandmarkHeadquarters && !status.HasHeadquartersBoost)
            {
                return string.Format(
                    "Landmark HQ would raise the live doctrine from Tier {0} to {1}.",
                    CompanyDoctrineSystem.BuildTierLabel(status.Tier),
                    CompanyDoctrineSystem.BuildTierLabel(Math.Min(4, status.Tier + 1)));
            }

            if (status.IsActive && status.HasHeadquartersBoost)
            {
                return string.Format(
                    "Landmark HQ is already boosting the live doctrine to Tier {0}.",
                    CompanyDoctrineSystem.BuildTierLabel(status.EffectiveTier));
            }

            if (status.Tier >= 3)
            {
                return status.IsActive
                    ? "Raw doctrine cap reached."
                    : "Raw doctrine cap reached, but the doctrine is not live while another track leads.";
            }

            return status.Tier <= 0
                ? "Needs Tier I before the doctrine can go live."
                : string.Empty;
        }

        private static string BuildDoctrineLeadSummaryForStatus(CompanyDoctrineStatus status, CompanyDoctrineLeadSummary leadSummary)
        {
            if (status == null)
            {
                return string.Empty;
            }

            if (status.IsActive)
            {
                return leadSummary != null ? leadSummary.ReasonSummary ?? string.Empty : "Doctrine bonus live.";
            }

            if (leadSummary == null || leadSummary.LeadingDoctrine == CompanyDoctrine.Balanced)
            {
                return status.Tier <= 0
                    ? "Needs Tier I before the doctrine can go live."
                    : "Bonus not live while another doctrine lead is unresolved.";
            }

            if (status.Doctrine == leadSummary.LeadingDoctrine && leadSummary.LeadingTier <= 0)
            {
                return status.ProgressRatio > 0.001f
                    ? string.Format("Closest to Tier I at {0:0}% progress.", status.ProgressRatio * 100f)
                    : "No doctrine progress recorded yet.";
            }

            switch (leadSummary.ReasonType)
            {
                case CompanyDoctrineLeadReasonType.Tier:
                    return string.Format("Behind {0} on tier.", leadSummary.LeadingDoctrineName);
                case CompanyDoctrineLeadReasonType.ProgressRatio:
                    return string.Format("Behind {0} on progress.", leadSummary.LeadingDoctrineName);
                case CompanyDoctrineLeadReasonType.DoctrinePriority:
                    return string.Format("Behind {0} on doctrine priority tie-break.", leadSummary.LeadingDoctrineName);
                default:
                    return status.Tier <= 0
                        ? "Needs Tier I before the doctrine can go live."
                        : string.Format("Behind {0}.", leadSummary.LeadingDoctrineName);
            }
        }

        private static string BuildDoctrineSteeringSummary(CompanyDoctrineStatus status, bool hasLandmarkHeadquarters)
        {
            if (status == null)
            {
                return string.Empty;
            }

            var bestComponent = (status.ProgressComponents ?? Array.Empty<CompanyDoctrineProgressComponent>())
                .Where(component => component != null
                    && component.MissingValue > 0.0005f
                    && component.PotentialStepProgressRatio > 0.0005f)
                .OrderByDescending(component => component.PotentialStepProgressRatio)
                .ThenByDescending(component => component.Weight)
                .FirstOrDefault();
            if (bestComponent != null)
            {
                return string.Format(
                    "Best push: {0} (+{1:0}% progress).",
                    bestComponent.NextStepText,
                    bestComponent.PotentialStepProgressRatio * 100f);
            }

            if (status.IsActive && status.Tier > 0 && !hasLandmarkHeadquarters && !status.HasHeadquartersBoost)
            {
                return "Best push: install a Landmark HQ for +24 prestige and a live capstone tier.";
            }

            if (status.IsActive && status.HasHeadquartersBoost)
            {
                return "Raw doctrine progress is capped; prestige now grows through districts, wins, industries, and missions.";
            }

            return status.Tier <= 0
                ? "Best push: reach Tier I to activate the doctrine bonus."
                : "Doctrine progress is capped until this track takes the lead.";
        }

        private CompanyPrestigeBreakdown BuildPrestigeBreakdown(
            CompanyDoctrineStatus activeDoctrine,
            CompanyDoctrineLeadSummary leadSummary,
            bool hasLandmarkHeadquarters,
            int dominantDistricts,
            int ownedIndustries,
            int competitiveWins,
            int completedMissions,
            float prestige,
            float doctrinePrestige,
            float headquartersPrestige,
            float districtPrestige,
            float industryPrestige,
            float competitionPrestige,
            float missionPrestige)
        {
            var leadingDoctrineName = leadSummary != null && !string.IsNullOrWhiteSpace(leadSummary.LeadingDoctrineName)
                ? leadSummary.LeadingDoctrineName
                : CompanyDoctrineSystem.GetName(CompanyDoctrine.Balanced);
            var doctrineOpportunity = string.Empty;
            var doctrineImmediateGain = 0f;
            if (activeDoctrine != null)
            {
                if (!hasLandmarkHeadquarters && activeDoctrine.EffectiveTier == activeDoctrine.Tier)
                {
                    doctrineOpportunity = string.Format(
                        "Install a Landmark HQ to lift {0} from live Tier {1} to {2} for +12 doctrine prestige.",
                        activeDoctrine.Name,
                        CompanyDoctrineSystem.BuildTierLabel(activeDoctrine.Tier),
                        CompanyDoctrineSystem.BuildTierLabel(Math.Min(4, activeDoctrine.Tier + 1)));
                    doctrineImmediateGain = 12f;
                }
                else if (activeDoctrine.NextTier > 0)
                {
                    doctrineOpportunity = string.Format(
                        "Push {0} to Tier {1} for +12 doctrine prestige.",
                        activeDoctrine.Name,
                        CompanyDoctrineSystem.BuildTierLabel(activeDoctrine.NextTier));
                    doctrineImmediateGain = 12f;
                }
            }
            else
            {
                doctrineOpportunity = string.Format("Reach Tier I in {0} to turn on +12 doctrine prestige.", leadingDoctrineName);
                doctrineImmediateGain = 12f;
            }

            var components = new[]
            {
                BuildPrestigeComponent(
                    "doctrine",
                    "Doctrine",
                    doctrinePrestige,
                    48f,
                    doctrineImmediateGain,
                    activeDoctrine != null
                        ? string.Format("{0} live Tier {1} = +{2:0.#}/48", activeDoctrine.Name, CompanyDoctrineSystem.BuildTierLabel(activeDoctrine.EffectiveTier), doctrinePrestige)
                        : "No live doctrine = +0/48",
                    doctrineOpportunity),
                BuildPrestigeComponent(
                    "headquarters",
                    "Landmark HQ",
                    headquartersPrestige,
                    24f,
                    hasLandmarkHeadquarters ? 0f : 24f,
                    hasLandmarkHeadquarters ? "Landmark HQ online = +24/24" : "Landmark HQ offline = +0/24",
                    hasLandmarkHeadquarters ? string.Empty : "Install a Landmark HQ for +24 prestige and a live doctrine boost."),
                BuildPrestigeComponent(
                    "dominant-districts",
                    "Dominant districts",
                    districtPrestige,
                    24f,
                    districtPrestige < 24f ? Math.Min(6f, 24f - districtPrestige) : 0f,
                    string.Format("{0} dominant districts = +{1:0.#}/24", dominantDistricts, districtPrestige),
                    districtPrestige < 24f ? "Secure 1 more dominant district for +6 prestige." : string.Empty),
                BuildPrestigeComponent(
                    "owned-industries",
                    "Owned industries",
                    industryPrestige,
                    14f,
                    industryPrestige < 14f ? Math.Min(1.2f, 14f - industryPrestige) : 0f,
                    string.Format("{0} owned industries = +{1:0.#}/14", ownedIndustries, industryPrestige),
                    industryPrestige < 14f ? "Buy 1 more production site for +1.2 prestige." : string.Empty),
                BuildPrestigeComponent(
                    "competition-wins",
                    "Competition wins",
                    competitionPrestige,
                    12f,
                    competitionPrestige < 12f ? Math.Min(1.5f, 12f - competitionPrestige) : 0f,
                    string.Format("{0} competition wins = +{1:0.#}/12", competitiveWins, competitionPrestige),
                    competitionPrestige < 12f ? "Convert 1 more district competition into a win for +1.5 prestige." : string.Empty),
                BuildPrestigeComponent(
                    "special-missions",
                    "Special missions",
                    missionPrestige,
                    14f,
                    missionPrestige < 14f ? Math.Min(1.2f, 14f - missionPrestige) : 0f,
                    string.Format("{0} completed special missions = +{1:0.#}/14", completedMissions, missionPrestige),
                    missionPrestige < 14f ? "Finish 1 more special mission for +1.2 prestige." : string.Empty),
            };
            var primaryOpportunity = components
                .Where(component => component != null
                    && component.ImmediateGain > 0.0005f
                    && !string.IsNullOrWhiteSpace(component.OpportunityText))
                .OrderByDescending(component => component.ImmediateGain)
                .ThenByDescending(component => component.RemainingScore)
                .Select(component => component.OpportunityText)
                .FirstOrDefault() ?? string.Empty;

            return new CompanyPrestigeBreakdown
            {
                Components = components,
                MissingScore = Math.Max(0f, 100f - prestige),
                PrimaryOpportunity = primaryOpportunity,
            };
        }

        private static CompanyPrestigeComponent BuildPrestigeComponent(
            string key,
            string label,
            float score,
            float maxScore,
            float immediateGain,
            string statusText,
            string opportunityText)
        {
            return new CompanyPrestigeComponent
            {
                Key = key ?? string.Empty,
                Label = label ?? string.Empty,
                Score = Math.Max(0f, score),
                MaxScore = Math.Max(0f, maxScore),
                RemainingScore = Math.Max(0f, maxScore - score),
                ImmediateGain = Math.Max(0f, immediateGain),
                StatusText = statusText ?? string.Empty,
                OpportunityText = opportunityText ?? string.Empty,
            };
        }

        private static string BuildPrimaryOpportunitySummary(
            IReadOnlyList<CompanyDoctrineStatus> doctrines,
            CompanyDoctrineStatus activeDoctrine,
            CompanyDoctrineLeadSummary leadSummary,
            bool hasLandmarkHeadquarters,
            CompanyPrestigeBreakdown prestigeBreakdown)
        {
            if (activeDoctrine != null && activeDoctrine.Tier > 0 && !hasLandmarkHeadquarters)
            {
                return "Next push: install a Landmark HQ (+24 prestige, +1 live tier).";
            }

            if (activeDoctrine != null && !string.IsNullOrWhiteSpace(activeDoctrine.SteeringSummary))
            {
                return activeDoctrine.SteeringSummary;
            }

            var leadingDoctrine = leadSummary != null ? leadSummary.LeadingDoctrine : CompanyDoctrine.Balanced;
            var leadingStatus = (doctrines ?? Array.Empty<CompanyDoctrineStatus>())
                .FirstOrDefault(status => status != null && status.Doctrine == leadingDoctrine);
            if (leadingStatus != null && !string.IsNullOrWhiteSpace(leadingStatus.SteeringSummary))
            {
                return leadingStatus.SteeringSummary;
            }

            return prestigeBreakdown != null ? prestigeBreakdown.PrimaryOpportunity ?? string.Empty : string.Empty;
        }

        private static PlayerStatisticsPersistenceSnapshot CloneSnapshot(PlayerStatisticsPersistenceSnapshot snapshot)
        {
            var clone = new PlayerStatisticsPersistenceSnapshot
            {
                IsInitialized = snapshot != null && snapshot.IsInitialized,
                HighestCompanyBalanceEver = snapshot != null ? Math.Max(0f, snapshot.HighestCompanyBalanceEver) : 0f,
                HighestCompanyBalanceBeforeFirstNpcHire = snapshot != null ? Math.Max(0f, snapshot.HighestCompanyBalanceBeforeFirstNpcHire) : 0f,
                HighestCompanyBalanceBeforeFirstLoan = snapshot != null ? Math.Max(0f, snapshot.HighestCompanyBalanceBeforeFirstLoan) : 0f,
                TotalSuccessfulDeliveries = snapshot != null ? Math.Max(0, snapshot.TotalSuccessfulDeliveries) : 0,
                TotalSuccessfulCleanDeliveries = snapshot != null ? Math.Max(0, snapshot.TotalSuccessfulCleanDeliveries) : 0,
                DeliveriesBeforeFirstNpcHire = snapshot != null ? Math.Max(0, snapshot.DeliveriesBeforeFirstNpcHire) : 0,
                TotalTransportedTons = snapshot != null ? Math.Max(0f, snapshot.TotalTransportedTons) : 0f,
                HasEverHiredNpc = snapshot != null && snapshot.HasEverHiredNpc,
                HasEverTakenLoan = snapshot != null && snapshot.HasEverTakenLoan,
                CumulativeNpcDeliveryIncome = snapshot != null ? Math.Max(0f, snapshot.CumulativeNpcDeliveryIncome) : 0f,
                TotalSpecialMissionsCompleted = snapshot != null ? Math.Max(0, snapshot.TotalSpecialMissionsCompleted) : 0,
                TotalEmergencyServiceUsages = snapshot != null ? Math.Max(0, snapshot.TotalEmergencyServiceUsages) : 0,
                HighestPrestigeScore = snapshot != null ? Math.Max(0f, snapshot.HighestPrestigeScore) : 0f,
                HighestDoctrineId = snapshot != null ? snapshot.HighestDoctrineId ?? string.Empty : string.Empty,
                HighestDoctrineTier = snapshot != null ? Math.Max(0, snapshot.HighestDoctrineTier) : 0,
            };

            return clone;
        }

        private static string NormalizeSuccessId(string id)
        {
            return string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : id.Trim().ToLowerInvariant();
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float BuildRatio(float current, float target)
        {
            return target <= 0.001f ? 0f : Clamp01(current / target);
        }

        private static float BuildRatio(int current, int target)
        {
            return target <= 0 ? 0f : Clamp01((float)current / target);
        }

        private static string BuildMoneyProgress(float current, float target)
        {
            return string.Format("{0} / {1}", ModFormatting.FormatMoney(current), ModFormatting.FormatMoney(target));
        }

        private static string BuildTonnageProgress(float current, float target)
        {
            return string.Format("{0} / {1}", ModFormatting.FormatTons(current), ModFormatting.FormatTons(target));
        }

        private static string BuildCountProgress(int current, int target, string suffix = null)
        {
            return string.IsNullOrWhiteSpace(suffix)
                ? string.Format("{0}/{1}", current, target)
                : string.Format("{0}/{1} {2}", current, target, suffix);
        }

        private static string BuildReputationProgress(float current, float target)
        {
            return string.Format("{0:0}/{1:0} reputation", current, target);
        }

        private sealed class GarageFillState
        {
            public int Current { get; set; }

            public int Capacity { get; set; }

            public float ProgressRatio { get; set; }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;

namespace LSOL.Systems
{
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

    internal sealed class PlayerSuccessTracker
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

        public void RecordDeliveryProgress(string commodity, float deliveredTons, bool completedDelivery, bool isCleanDelivery)
        {
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

            return _propertyManager.Interiors.Count(interior => interior != null && GetApartmentState(interior.InteriorId)?.IsOwned == true);
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

        private int GetOwnedGasStationCount()
        {
            return GetAllSites().Count(site => site.IsGasStation && site.IsOwned);
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

        private static PlayerSuccessEvaluation BuildBinaryEvaluation(bool isComplete, string progressText)
        {
            return new PlayerSuccessEvaluation(isComplete, isComplete ? 1f : 0f, progressText);
        }

        private static IReadOnlyList<PlayerSuccessDefinition> BuildDefinitions()
        {
            return new List<PlayerSuccessDefinition>
            {
                CreateFloatThresholdDefinition("first_paycheck", "First Paycheck", "Earn your first $10,000.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdFirstPaycheck, BuildMoneyProgress),
                CreateFloatThresholdDefinition("small_operator", "Small Operator", "Reach $100,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdSmallOperator, BuildMoneyProgress),
                CreateFloatThresholdDefinition("millionaire_hauler", "Millionaire Hauler", "Reach $1,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateFloatThresholdDefinition("logistics_tycoon", "Logistics Tycoon", "Reach $10,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdLogisticsTycoon, BuildMoneyProgress),
                CreateFloatThresholdDefinition("multimillion_company", "MultiMillion Company", "Reach $100,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdMultiMillionCompany, BuildMoneyProgress),
                CreateFloatThresholdDefinition("self_made", "Self-Made", "Accumulate $1,000,000 without recruiting any NPC.", tracker => tracker._snapshot.HighestCompanyBalanceBeforeFirstNpcHire, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateIntThresholdDefinition("first_delivery", "First Delivery", "Complete your first delivery.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdFirstDelivery, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("road_veteran", "Road Veteran", "Complete 100 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdRoadVeteran, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("road_legend", "Road Legend", "Complete 1,000 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdRoadLegend, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("endless_freight", "Endless Freight", "Complete 10,000 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdEndlessFreight, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateFloatThresholdDefinition("heavy_hauler", "Heavy Hauler", "Transport a total of 100t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdHeavyHauler, BuildTonnageProgress),
                CreateFloatThresholdDefinition("industrial_backbone", "Industrial Backbone", "Transport a total of 1,000t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdIndustrialBackbone, BuildTonnageProgress),
                CreateFloatThresholdDefinition("supply_chain_king", "Supply Chain King", "Transport a total of 10,000t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdSupplyChainKing, BuildTonnageProgress),
                CreateStateDefinition("first_office", "First Office", "Buy or rent your first office.", tracker => tracker.GetOfficeAccessCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOfficeAccessCount()), 1, "office"), tracker => tracker.GetOfficeAccessCount() > 0 ? 1f : 0f),
                CreateStateDefinition("company_footprint", "Company Footprint", "Own an office.", tracker => tracker.GetOwnedOfficeCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedOfficeCount()), 1, "owned office"), tracker => tracker.GetOwnedOfficeCount() > 0 ? 1f : 0f),
                CreateStateDefinition("empire", "Empire", "Own the port office.", tracker => tracker.OwnsPortOffice(), tracker => tracker.OwnsPortOffice() ? "1/1 port office owned" : "0/1 port office owned", tracker => tracker.OwnsPortOffice() ? 1f : 0f),
                CreateStateDefinition("home_base", "Home Base", "Buy or rent your first apartment.", tracker => tracker.GetApartmentAccessCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetApartmentAccessCount()), 1, "apartment"), tracker => tracker.GetApartmentAccessCount() > 0 ? 1f : 0f),
                CreateStateDefinition("fleet_owner", "Fleet Owner", "Own your first commercial vehicle.", tracker => tracker.GetOwnedCommercialVehicleCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedCommercialVehicleCount()), 1, "vehicle"), tracker => tracker.GetOwnedCommercialVehicleCount() > 0 ? 1f : 0f),
                CreateCustomDefinition("full_garage", "Full Garage", "Fill an office garage to its vehicle capacity.", tracker =>
                {
                    var fill = tracker.GetBestOwnedGarageFillState();
                    if (fill.Capacity <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No owned office garage capacity.");
                    }

                    return new PlayerSuccessEvaluation(fill.Current >= fill.Capacity, fill.ProgressRatio, string.Format("{0}/{1} garage slots filled", fill.Current, fill.Capacity));
                }),
                CreateStateDefinition("company_fleet", "Company Fleet", "Own 10 commercial vehicles.", tracker => tracker.GetOwnedCommercialVehicleCount() >= FleetThresholdCompanyFleet, tracker => BuildCountProgress(tracker.GetOwnedCommercialVehicleCount(), FleetThresholdCompanyFleet, "owned vehicles"), tracker => BuildRatio(tracker.GetOwnedCommercialVehicleCount(), FleetThresholdCompanyFleet)),
                CreateStateDefinition("mechanized_empire", "Mechanized Empire", "Own a diversified fleet with at least one truck for several different cargo roles.", tracker => tracker.GetDistinctOwnedFleetRoles() >= FleetRoleThresholdMechanizedEmpire, tracker => BuildCountProgress(tracker.GetDistinctOwnedFleetRoles(), FleetRoleThresholdMechanizedEmpire, "cargo roles"), tracker => BuildRatio(tracker.GetDistinctOwnedFleetRoles(), FleetRoleThresholdMechanizedEmpire)),
                CreateStateDefinition("first_employee", "First Employee", "Recruit your first NPC driver.", tracker => tracker._snapshot.HasEverHiredNpc, tracker => tracker._snapshot.HasEverHiredNpc ? "1/1 hired NPC" : "0/1 hired NPC", tracker => tracker._snapshot.HasEverHiredNpc ? 1f : 0f),
                CreateStateDefinition("dispatcher", "Dispatcher", "Have 5 active NPCs.", tracker => tracker.GetCurrentNpcDriverCount() >= NpcThresholdDispatcher, tracker => BuildCountProgress(tracker.GetCurrentNpcDriverCount(), NpcThresholdDispatcher, "active NPCs"), tracker => BuildRatio(tracker.GetCurrentNpcDriverCount(), NpcThresholdDispatcher)),
                CreateStateDefinition("logistics_manager", "Logistics Manager", "Have multiple NPC routes running at the same time.", tracker => tracker.GetCurrentNpcRouteCount() >= ActiveNpcRoutesThreshold, tracker => BuildCountProgress(tracker.GetCurrentNpcRouteCount(), ActiveNpcRoutesThreshold, "active routes"), tracker => BuildRatio(tracker.GetCurrentNpcRouteCount(), ActiveNpcRoutesThreshold)),
                CreateFloatThresholdDefinition("hands_off_income", "Hands-Off Income", "Earn a large amount only from NPC deliveries.", tracker => tracker._snapshot.CumulativeNpcDeliveryIncome, NpcIncomeThresholdHandsOffIncome, BuildMoneyProgress),
                CreateStateDefinition("entrepreneur", "Entrepreneur", "Own your first industry.", tracker => tracker.GetOwnedActualIndustryCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedActualIndustryCount()), 1, "industry"), tracker => tracker.GetOwnedActualIndustryCount() > 0 ? 1f : 0f),
                CreateStateDefinition("industrialist", "Industrialist", "Owns 5 industries.", tracker => tracker.GetOwnedActualIndustryCount() >= IndustryThresholdIndustrialist, tracker => BuildCountProgress(tracker.GetOwnedActualIndustryCount(), IndustryThresholdIndustrialist, "industries"), tracker => BuildRatio(tracker.GetOwnedActualIndustryCount(), IndustryThresholdIndustrialist)),
                CreateStateDefinition("established_industrialist", "Established Industrialist", "Owns 15 industries", tracker => tracker.GetOwnedActualIndustryCount() >= IndustryThresholdEstablishedIndustrialist, tracker => BuildCountProgress(tracker.GetOwnedActualIndustryCount(), IndustryThresholdEstablishedIndustrialist, "industries"), tracker => BuildRatio(tracker.GetOwnedActualIndustryCount(), IndustryThresholdEstablishedIndustrialist)),
                CreateCustomDefinition("industrial_master", "Industrial Master", "Owns all industry", tracker =>
                {
                    var total = tracker.GetPurchasableActualIndustryCount();
                    if (total <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No purchasable industries configured.");
                    }

                    var owned = tracker.GetOwnedActualIndustryCount();
                    return new PlayerSuccessEvaluation(owned >= total, BuildRatio(owned, total), string.Format("{0}/{1} purchasable industries", owned, total));
                }),
                CreateStateDefinition("vertical_integration", "Vertical Integration", "Own both a producer and a consumer in the same supply chain.", tracker => tracker.HasOwnedProducerConsumerMatch(), tracker => tracker.HasOwnedProducerConsumerMatch() ? "1/1 matching supply chain" : "0/1 matching supply chain", tracker => tracker.HasOwnedProducerConsumerMatch() ? 1f : 0f),
                CreateStateDefinition("supply_network", "Supply Network", "Own 1 industry in 3 districts.", tracker => tracker.GetOwnedIndustryDistrictCount() >= 3, tracker => BuildCountProgress(tracker.GetOwnedIndustryDistrictCount(), 3, "districts"), tracker => BuildRatio(tracker.GetOwnedIndustryDistrictCount(), 3)),
                CreateStateDefinition("warehouse_master", "Warehouse Master", "Buy your first warehouse.", tracker => tracker.GetOwnedWarehouseCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedWarehouseCount()), 1, "warehouse"), tracker => tracker.GetOwnedWarehouseCount() > 0 ? 1f : 0f),
                CreateStateDefinition("fuel_baron", "Fuel Baron", "Own your first gas station.", tracker => tracker.GetOwnedGasStationCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedGasStationCount()), 1, "gas station"), tracker => tracker.GetOwnedGasStationCount() > 0 ? 1f : 0f),
                CreateStateDefinition("port_authority", "Port Authority", "Buy the Port Terminal.", tracker => tracker.OwnsPortTerminal(), tracker => tracker.OwnsPortTerminal() ? "1/1 Port Terminal owned" : "0/1 Port Terminal owned", tracker => tracker.OwnsPortTerminal() ? 1f : 0f),
                CreateStateDefinition("clean_run", "Clean Run", "Complete a delivery without cargo damage.", tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries >= 1, tracker => BuildCountProgress(Math.Min(1, tracker._snapshot.TotalSuccessfulCleanDeliveries), 1, "clean delivery"), tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries >= 1 ? 1f : 0f),
                CreateIntThresholdDefinition("perfectionist", "Perfectionist", "Complete 25 deliveries with no cargo damage.", tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries, CleanDeliveryThresholdPerfectionist, (current, target) => BuildCountProgress(current, target, "clean deliveries")),
                CreateIntThresholdDefinition("emergency_services", "Emergency Services", "Use a recovery/refuel/service system 15 times.", tracker => tracker._snapshot.TotalEmergencyServiceUsages, EmergencyServiceThreshold, (current, target) => BuildCountProgress(current, target, "service uses")),
                CreateIntThresholdDefinition("special_contractor", "Special Contractor", "Complete your first special mission.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdFirst, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateIntThresholdDefinition("regular_contractor", "Regular Contractor", "Complete 10 special missions.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdRegular, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateIntThresholdDefinition("expert_contractor", "Expert Contractor", "Complete 50 special missions.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdExpert, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateFloatThresholdDefinition("reputation_built", "Reputation Built", "Reach a 250 reputation score in one district.", tracker => tracker.GetBestDistrictReputationScore(), ReputationThresholdBuilt, BuildReputationProgress),
                CreateStateDefinition("statewide_operator", "Statewide Operator", "Reach 100 reputation in 3 districts.", tracker => tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide) >= ReputationDistrictThresholdStatewide, tracker => BuildCountProgress(tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide), ReputationDistrictThresholdStatewide, "districts"), tracker => BuildRatio(tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide), ReputationDistrictThresholdStatewide)),
                CreateCustomDefinition("worldwide_operator", "Worldwide Operator", "Reach dominant reputation in all districts.", tracker =>
                {
                    var total = tracker.GetTotalDistrictCount();
                    if (total <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No districts configured.");
                    }

                    var dominant = tracker.GetDominantDistrictCount();
                    return new PlayerSuccessEvaluation(dominant >= total, BuildRatio(dominant, total), string.Format("{0}/{1} dominant districts", dominant, total));
                }),
                CreateIntThresholdDefinition("solo_grinder", "Solo Grinder", "Complete 100 deliveries before hiring any NPC.", tracker => tracker._snapshot.DeliveriesBeforeFirstNpcHire, DeliveryThresholdSoloGrinder, (current, target) => BuildCountProgress(current, target, "deliveries before first NPC")),
                CreateFloatThresholdDefinition("no_debt_needed", "No Debt Needed", "Reach $1,000,000 without taking a loan", tracker => tracker._snapshot.HighestCompanyBalanceBeforeFirstLoan, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateFloatThresholdDefinition("freight_specialist", "Freight Specialist", "Deliver a large amount of one specific commodity type.", tracker => tracker.GetBestCommodityTonnage(), CommodityThresholdFreightSpecialist, BuildTonnageProgress),
                CreateStateDefinition("diversified_carrier", "Diversified Carrier", "Successfully transport many different resource types.", tracker => tracker.GetDistinctDeliveredCommodityCount() >= CommodityThresholdDiversifiedCarrier, tracker => BuildCountProgress(tracker.GetDistinctDeliveredCommodityCount(), CommodityThresholdDiversifiedCarrier, "resource types"), tracker => BuildRatio(tracker.GetDistinctDeliveredCommodityCount(), CommodityThresholdDiversifiedCarrier)),
                CreateStateDefinition("lsol_founder", "LSOL Founder", "Own an office, an apartment, at least one industry, a warehouse, and a working fleet at the same time.", tracker => tracker.GetFounderProgressCount() >= FounderRequirementCount, tracker => BuildCountProgress(tracker.GetFounderProgressCount(), FounderRequirementCount, "founder requirements"), tracker => BuildRatio(tracker.GetFounderProgressCount(), FounderRequirementCount)),
                CreateStateDefinition("megalomaniac", "Megalomaniac", "Complete the following Success: \"Endless Freight\", \"Self-Made\", \"Worldwide Operator\" and \"Industrial Master\"", tracker => tracker.GetMegalomaniacProgressCount() >= MegalomaniacRequirementCount, tracker => BuildCountProgress(tracker.GetMegalomaniacProgressCount(), MegalomaniacRequirementCount, "required Successes"), tracker => BuildRatio(tracker.GetMegalomaniacProgressCount(), MegalomaniacRequirementCount)),
            };
        }

        private static PlayerSuccessDefinition CreateFloatThresholdDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, float> currentValueFactory,
            float threshold,
            Func<float, float, string> progressTextFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var current = Math.Max(0f, currentValueFactory != null ? currentValueFactory(tracker) : 0f);
                return new PlayerSuccessEvaluation(current >= threshold, BuildRatio(current, threshold), progressTextFactory != null ? progressTextFactory(current, threshold) : string.Empty);
            });
        }

        private static PlayerSuccessDefinition CreateIntThresholdDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, int> currentValueFactory,
            int threshold,
            Func<int, int, string> progressTextFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var current = Math.Max(0, currentValueFactory != null ? currentValueFactory(tracker) : 0);
                return new PlayerSuccessEvaluation(current >= threshold, BuildRatio(current, threshold), progressTextFactory != null ? progressTextFactory(current, threshold) : string.Empty);
            });
        }

        private static PlayerSuccessDefinition CreateStateDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, bool> isCompleteFactory,
            Func<PlayerSuccessTracker, string> progressTextFactory,
            Func<PlayerSuccessTracker, float> progressRatioFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var isComplete = isCompleteFactory != null && isCompleteFactory(tracker);
                var progressText = progressTextFactory != null ? progressTextFactory(tracker) : string.Empty;
                var ratio = progressRatioFactory != null ? Clamp01(progressRatioFactory(tracker)) : (isComplete ? 1f : 0f);
                return new PlayerSuccessEvaluation(isComplete, ratio, progressText);
            });
        }

        private static PlayerSuccessDefinition CreateCustomDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, PlayerSuccessEvaluation> evaluationFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker => evaluationFactory != null ? evaluationFactory(tracker) : PlayerSuccessEvaluation.Incomplete(0f, string.Empty));
        }

        private sealed class PlayerSuccessDefinition
        {
            public PlayerSuccessDefinition(string id, string name, string description, Func<PlayerSuccessTracker, PlayerSuccessEvaluation> evaluate)
            {
                Id = NormalizeSuccessId(id);
                Name = name ?? string.Empty;
                Description = description ?? string.Empty;
                Evaluate = evaluate;
            }

            public string Id { get; }

            public string Name { get; }

            public string Description { get; }

            public Func<PlayerSuccessTracker, PlayerSuccessEvaluation> Evaluate { get; }
        }

        private sealed class PlayerSuccessEvaluation
        {
            public PlayerSuccessEvaluation(bool isComplete, float progressRatio, string progressText)
            {
                IsComplete = isComplete;
                ProgressRatio = progressRatio;
                ProgressText = progressText ?? string.Empty;
            }

            public bool IsComplete { get; }

            public float ProgressRatio { get; }

            public string ProgressText { get; }

            public static PlayerSuccessEvaluation Incomplete(float progressRatio, string progressText)
            {
                return new PlayerSuccessEvaluation(false, progressRatio, progressText);
            }
        }

        private sealed class GarageFillState
        {
            public int Current { get; set; }

            public int Capacity { get; set; }

            public float ProgressRatio { get; set; }
        }
    }
}
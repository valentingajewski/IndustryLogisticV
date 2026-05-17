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

        private sealed class GarageFillState
        {
            public int Current { get; set; }

            public int Capacity { get; set; }

            public float ProgressRatio { get; set; }
        }
    }
}
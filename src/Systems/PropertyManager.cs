using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class CommercialVehicleMapBlipInfo
    {
        public string AssetId { get; set; }

        public string DisplayName { get; set; }

        public int TruckHandle { get; set; }
    }

    public sealed class PersonalVehicleMapBlipInfo
    {
        public string AssetId { get; set; }

        public string DisplayName { get; set; }

        public int VehicleHandle { get; set; }
    }

    public sealed class CorporateOverheadChargePreview
    {
        public float WeeklyAmount { get; set; }

        public float AmountDue { get; set; }

        public int DueInMinutes { get; set; }

        public int WeeksDue { get; set; }

        public int ScaleScore { get; set; }

        public int OwnedSiteCount { get; set; }

        public int OwnedFleetCount { get; set; }

        public int ActiveNpcCount { get; set; }

        public int LicensedDistrictCount { get; set; }

        public int SecuredSupportSiteCount { get; set; }

        public int ActiveCorridorCount { get; set; }
    }

    public sealed class FleetMaintenanceChargePreview
    {
        public float WeeklyAmount { get; set; }

        public float AmountDue { get; set; }

        public int DueInMinutes { get; set; }

        public int VehicleCount { get; set; }

        public int CoveredVehicleCount { get; set; }

        public int OverdueInspectionCount { get; set; }

        public int AtRiskVehicleCount { get; set; }

        public float AverageConditionPercent { get; set; }
    }

    public sealed class PropertyManager
    {
        private const int MinutesPerWeek = 7 * 24 * 60;
        private const int MinutesPerDay = 24 * 60;
        private const float ApartmentSaleRefundRatio = 0.5f;
        private const float CommercialVehicleSaleRefundRatio = 0.5f;
        private const float CorporateOverheadBaseCharge = 250f;
        private const float CorporateOverheadScaleCharge = 45f;
        private const float CorporateOverheadPerOwnedSite = 210f;
        private const float CorporateOverheadPerOwnedFleetVehicle = 135f;
        private const float CorporateOverheadPerNpcCrew = 175f;
        private const float CorporateOverheadPerLicensedDistrict = 260f;
        private const float CorporateOverheadPerSupportSite = 180f;
        private const float CorporateOverheadPerCorridor = 65f;
        private const float FleetMaintenanceMinimumWeeklyCharge = 110f;
        private const float FleetMaintenancePurchaseRate = 0.0014f;
        private const float FleetMaintenanceCapacityRate = 10f;
        private const float FleetMaintenanceCoverageDiscount = 0.72f;
        private const float FleetMaintenanceInspectionSurcharge = 65f;
        private const float FleetMaintenanceCoverageRecovery = 0.10f;
        private const float FleetMaintenanceWearPerWeek = 0.045f;
        private const float FleetMaintenanceDeployedWearBonus = 0.02f;
        private const float FleetMaintenanceOverdueWearBonus = 0.05f;
        private const int FleetInspectionIntervalWeeks = 2;
        private const float MinimumMaintenanceCondition = 0.35f;

        private readonly List<OfficeDefinition> _officeDefinitions;
        private readonly Dictionary<string, OfficeDefinition> _officeDefinitionsById;
        private readonly List<OfficeObjectDefinition> _officeObjectDefinitions;
        private readonly Dictionary<int, OfficeObjectDefinition> _officeObjectDefinitionsById;
        private readonly List<InteriorDefinition> _interiorDefinitions;
        private readonly Dictionary<string, InteriorDefinition> _interiorDefinitionsById;
        private readonly List<MotelDefinition> _motelDefinitions;
        private readonly List<DealershipVehicleDefinition> _personalVehicleDefinitions;
        private readonly Dictionary<string, DealershipVehicleDefinition> _personalVehicleDefinitionsById;
        private readonly Dictionary<string, CommercialVehicleRuntimeState> _commercialRuntime;
        private readonly Dictionary<string, PersonalVehicleRuntimeState> _personalRuntime;

        private PropertyOwnershipPersistenceSnapshot _state;
        private bool _officeGarageLimitEnforced;
        private CompanyFinanceTracker _financeTracker;
        private Func<int> _getCurrentInGameMinute;
        private Func<int> _getOwnedSiteCount;
        private Func<int> _getActiveNpcCount;
        private Func<int> _getLicensedDistrictCount;
        private Func<int> _getSecuredSupportSiteCount;
        private Func<int> _getActiveCorridorCount;

        public PropertyManager(ModConfig config)
        {
            _officeDefinitions = config != null && config.OfficeDefinitions != null
                ? config.OfficeDefinitions.OrderBy(x => x != null ? x.OfficePrice : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<OfficeDefinition>();
            _officeDefinitionsById = _officeDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.OfficeId))
                .ToDictionary(x => x.OfficeId, x => x, StringComparer.OrdinalIgnoreCase);
            _officeObjectDefinitions = config != null && config.OfficeObjectDefinitions != null
                ? config.OfficeObjectDefinitions.OrderBy(x => x != null ? x.Price : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<OfficeObjectDefinition>();
            _officeObjectDefinitionsById = _officeObjectDefinitions
                .Where(x => x != null && x.ObjectId > 0)
                .GroupBy(x => x.ObjectId)
                .ToDictionary(group => group.Key, group => group.First());
            _interiorDefinitions = config != null && config.InteriorDefinitions != null
                ? config.InteriorDefinitions.OrderBy(x => x != null ? x.InteriorPrice : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<InteriorDefinition>();
            _interiorDefinitionsById = _interiorDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.InteriorId))
                .ToDictionary(x => x.InteriorId, x => x, StringComparer.OrdinalIgnoreCase);
            _motelDefinitions = config != null && config.MotelDefinitions != null
                ? config.MotelDefinitions.OrderBy(x => x != null ? x.RestPrice : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<MotelDefinition>();
            _personalVehicleDefinitions = config != null && config.PersonalVehicleDefinitions != null
                ? config.PersonalVehicleDefinitions.OrderBy(x => x != null ? x.Price : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<DealershipVehicleDefinition>();
            _personalVehicleDefinitionsById = _personalVehicleDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.VehicleId))
                .ToDictionary(x => x.VehicleId, x => x, StringComparer.OrdinalIgnoreCase);
            _commercialRuntime = new Dictionary<string, CommercialVehicleRuntimeState>(StringComparer.OrdinalIgnoreCase);
            _personalRuntime = new Dictionary<string, PersonalVehicleRuntimeState>(StringComparer.OrdinalIgnoreCase);
            _state = new PropertyOwnershipPersistenceSnapshot();
            _officeGarageLimitEnforced = true;
        }

        public void ConfigureFinanceTracking(CompanyFinanceTracker financeTracker, Func<int> getCurrentInGameMinute)
        {
            _financeTracker = financeTracker;
            _getCurrentInGameMinute = getCurrentInGameMinute;
        }

        public void ConfigureEconomicPressure(
            Func<int> getOwnedSiteCount,
            Func<int> getActiveNpcCount,
            Func<int> getLicensedDistrictCount,
            Func<int> getSecuredSupportSiteCount,
            Func<int> getActiveCorridorCount)
        {
            _getOwnedSiteCount = getOwnedSiteCount;
            _getActiveNpcCount = getActiveNpcCount;
            _getLicensedDistrictCount = getLicensedDistrictCount;
            _getSecuredSupportSiteCount = getSecuredSupportSiteCount;
            _getActiveCorridorCount = getActiveCorridorCount;
        }

        public IReadOnlyList<OfficeDefinition> Offices
        {
            get { return _officeDefinitions; }
        }

        public IReadOnlyList<InteriorDefinition> Interiors
        {
            get { return _interiorDefinitions; }
        }

        public IReadOnlyList<MotelDefinition> Motels
        {
            get { return _motelDefinitions; }
        }

        public IReadOnlyList<OfficeObjectDefinition> OfficeObjectCatalog
        {
            get { return _officeObjectDefinitions; }
        }

        public IReadOnlyList<DealershipVehicleDefinition> PersonalVehicleCatalog
        {
            get { return _personalVehicleDefinitions; }
        }

        public IReadOnlyList<OwnedCommercialVehiclePersistenceEntry> CommercialVehicles
        {
            get { return _state.CommercialVehicles; }
        }

        public IReadOnlyList<OwnedPersonalVehiclePersistenceEntry> PersonalVehicles
        {
            get { return _state.PersonalVehicles; }
        }

        public string ActiveOfficeId
        {
            get { return _state.ActiveOfficeId ?? string.Empty; }
        }

        public string ActiveApartmentId
        {
            get { return _state.ActiveApartmentId ?? string.Empty; }
        }

        public int LastSuccessfulApartmentSleepMinute
        {
            get { return _state.LastSuccessfulApartmentSleepMinute; }
        }

        public OfficeDefinition ActiveOffice
        {
            get { return GetOfficeDefinition(_state.ActiveOfficeId); }
        }

        public InteriorDefinition ActiveApartment
        {
            get { return GetInteriorDefinition(_state.ActiveApartmentId); }
        }

        public void RecordSuccessfulApartmentSleep(int currentInGameMinute)
        {
            _state.LastSuccessfulApartmentSleepMinute = currentInGameMinute;
        }

        public void SetOfficeGarageLimitEnforced(bool enforced)
        {
            if (_officeGarageLimitEnforced == enforced)
            {
                return;
            }

            _officeGarageLimitEnforced = enforced;
            if (!string.IsNullOrWhiteSpace(_state.ActiveOfficeId) && _state.CommercialVehicles.Count > 0)
            {
                NormalizeCommercialGarageAssignments();
            }
        }

        public void ResetState()
        {
            ClearRuntimeState();
            _state = new PropertyOwnershipPersistenceSnapshot();
        }

        public PropertyOwnershipPersistenceSnapshot CreateSnapshot(FleetManager fleetManager, VehicleFuelSystem fuelSystem)
        {
            CaptureAllRuntimeState(fleetManager, fuelSystem);
            return CloneSnapshot(_state);
        }

        public void ApplySnapshot(PropertyOwnershipPersistenceSnapshot snapshot, int currentInGameMinute)
        {
            ClearRuntimeState();
            _state = snapshot != null ? CloneSnapshot(snapshot) : new PropertyOwnershipPersistenceSnapshot();
            EnsureValidSelections();
            NormalizeOfficeAccessContracts();
            NormalizeApartmentAccessContracts();
            PruneInvalidOfficeObjects();
            InitializeRentTracking(currentInGameMinute);
            NormalizeCommercialGarageAssignments();
            ReassignPersonalVehiclesToActiveApartment();
        }

        public PropertyOwnershipPersistenceSnapshot CreateLegacyMigrationSnapshot(OwnedFleetPersistenceSnapshot legacyOwnedFleet, int currentInGameMinute)
        {
            if (legacyOwnedFleet == null || legacyOwnedFleet.Vehicles == null || legacyOwnedFleet.Vehicles.Count == 0)
            {
                return null;
            }

            var snapshot = new PropertyOwnershipPersistenceSnapshot();
            var migrationOffice = _officeDefinitions.FirstOrDefault(definition => definition != null && string.Equals(definition.LegacyKey, "MainOffice", StringComparison.OrdinalIgnoreCase))
                ?? _officeDefinitions.FirstOrDefault();
            if (migrationOffice != null)
            {
                snapshot.ActiveOfficeId = migrationOffice.OfficeId;
                snapshot.Offices.Add(new OfficeOwnershipPersistenceEntry
                {
                    OfficeId = migrationOffice.OfficeId,
                    IsOwned = true,
                    IsRented = false,
                    IsAccessSuspended = false,
                    OutstandingRent = 0f,
                    LastChargedWeekIndex = GetWeekIndex(currentInGameMinute),
                });
            }

            for (int i = 0; i < legacyOwnedFleet.Vehicles.Count; i++)
            {
                var legacy = legacyOwnedFleet.Vehicles[i];
                if (legacy == null || string.IsNullOrWhiteSpace(legacy.PoweredModelName))
                {
                    continue;
                }

                snapshot.CommercialVehicles.Add(new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                    DisplayName = BuildCommercialDisplayName(legacy.PoweredModelName, legacy.CargoModelName, legacy.HasSeparateCargoVehicle),
                    PoweredModelName = legacy.PoweredModelName,
                    CargoModelName = legacy.CargoModelName,
                    HasSeparateCargoVehicle = legacy.HasSeparateCargoVehicle,
                    PurchasePrice = 0f,
                    AssignedOfficeId = snapshot.ActiveOfficeId,
                    InActiveGarage = migrationOffice != null && i < Math.Max(0, migrationOffice.MaxCommercialVehicles),
                    IsDeployed = true,
                    PoweredPosition = legacy.PoweredPosition,
                    PoweredHeading = legacy.PoweredHeading,
                    CargoType = legacy.CargoType,
                    CapacityTons = legacy.CapacityTons,
                    Commodity = legacy.Commodity,
                    WeightTons = legacy.WeightTons,
                    CargoCondition = legacy.CargoCondition,
                    TotalLostTons = legacy.TotalLostTons,
                    SourceIndustryId = legacy.SourceIndustryId,
                    SourceDistrictName = legacy.SourceDistrictName,
                    CurrentFuelLiters = legacy.CurrentFuelLiters,
                    MaintenanceCondition = legacy.MaintenanceCondition,
                    LastMaintenanceWeekIndex = legacy.LastMaintenanceWeekIndex,
                    LastInspectionWeekIndex = legacy.LastInspectionWeekIndex,
                    InspectionOverdueWeeks = legacy.InspectionOverdueWeeks,
                    LifetimeMaintenanceCost = legacy.LifetimeMaintenanceCost,
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        public int RestoreWorldState(FleetManager fleetManager, VehicleFuelSystem fuelSystem, Func<Vector3, Vector3> getGroundPosition)
        {
            if (fleetManager == null)
            {
                return 0;
            }

            var restoredCount = 0;

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var entry = _state.CommercialVehicles[i];
                if (entry == null || !entry.IsDeployed)
                {
                    continue;
                }

                string ignoredMessage;
                if (TrySpawnCommercialVehicle(entry, fleetManager, fuelSystem, getGroundPosition, entry.PoweredPosition, entry.PoweredHeading, out ignoredMessage))
                {
                    restoredCount += 1;
                }
            }

            for (int i = 0; i < _state.PersonalVehicles.Count; i++)
            {
                var entry = _state.PersonalVehicles[i];
                if (entry == null || !entry.IsDeployed)
                {
                    continue;
                }

                if (TrySpawnPersonalVehicle(entry, entry.Position, entry.Heading, false, out _))
                {
                    restoredCount += 1;
                }
            }

            return restoredCount;
        }

        public List<string> ProcessWeeklyCharges(int currentInGameMinute, ref float balance)
        {
            var messages = new List<string>();
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            var currentDayIndex = GetDayIndex(currentInGameMinute);

            for (int i = 0; i < _state.Offices.Count; i++)
            {
                var officeState = _state.Offices[i];
                ProcessOfficeWeeklyCharge(GetOfficeDefinition(officeState != null ? officeState.OfficeId : null), officeState, currentWeekIndex, ref balance, messages);
            }

            for (int i = 0; i < _state.Apartments.Count; i++)
            {
                var apartmentState = _state.Apartments[i];
                ProcessApartmentWeeklyCharge(GetInteriorDefinition(apartmentState != null ? apartmentState.InteriorId : null), apartmentState, currentWeekIndex, ref balance, messages);
            }

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                ProcessCommercialVehicleDailyCharge(_state.CommercialVehicles[i], currentDayIndex, ref balance, messages);
            }

            ProcessCorporateOverheadCharge(currentWeekIndex, ref balance, messages);
            ProcessFleetMaintenanceCharges(currentWeekIndex, ref balance, messages);

            return messages;
        }

        public CorporateOverheadChargePreview GetCorporateOverheadPreview(int currentInGameMinute)
        {
            return BuildCorporateOverheadPreview(GetWeekIndex(currentInGameMinute), currentInGameMinute);
        }

        public FleetMaintenanceChargePreview GetFleetMaintenancePreview(int currentInGameMinute)
        {
            return BuildFleetMaintenancePreview(GetWeekIndex(currentInGameMinute), currentInGameMinute);
        }

        public void RecordMaintenanceBayService(OwnedCommercialVehiclePersistenceEntry vehicle, int currentInGameMinute)
        {
            if (vehicle == null)
            {
                return;
            }

            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            vehicle.MaintenanceCondition = 1f;
            vehicle.LastMaintenanceWeekIndex = currentWeekIndex;
            vehicle.LastInspectionWeekIndex = currentWeekIndex;
            vehicle.InspectionOverdueWeeks = 0;
        }

        public bool CanUseCommercialSystems(out string reason)
        {
            reason = string.Empty;
            var office = ActiveOffice;
            var officeState = GetOfficeState(_state.ActiveOfficeId);
            if (office == null || officeState == null || (!officeState.IsOwned && !officeState.IsRented))
            {
                reason = "Rent or purchase an office to access commercial logistics.";
                return false;
            }

            if (officeState.IsAccessSuspended || officeState.OutstandingRent > 0.01f)
            {
                reason = string.Format("{0} is unavailable until outstanding office rent is settled.", office.DisplayName);
                return false;
            }

            return true;
        }

        public bool CanUseApartmentSystems(out string reason)
        {
            reason = string.Empty;
            var apartment = ActiveApartment;
            var apartmentState = GetApartmentState(_state.ActiveApartmentId);
            if (apartment == null || apartmentState == null || !HasApartmentAccess(apartmentState))
            {
                reason = "Rent or purchase and activate an apartment to access personal storage.";
                return false;
            }

            if (apartmentState.IsAccessSuspended || apartmentState.OutstandingRent > 0.01f)
            {
                reason = string.Format("{0} is unavailable until outstanding apartment rent is settled.", apartment.DisplayName);
                return false;
            }

            return true;
        }

        public bool TryRentOffice(string officeId, ref float balance, int currentInGameMinute, out string message)
        {
            return TryAcquireOfficeRental(officeId, ref balance, currentInGameMinute, false, out message);
        }

        public bool TryTransferOfficeRental(string officeId, ref float balance, int currentInGameMinute, out string message)
        {
            return TryAcquireOfficeRental(officeId, ref balance, currentInGameMinute, true, out message);
        }

        public bool TryPurchaseOffice(string officeId, ref float balance, int currentInGameMinute, out string message)
        {
            message = string.Empty;
            var definition = GetOfficeDefinition(officeId);
            if (definition == null)
            {
                message = "Office definition unavailable.";
                return false;
            }

            var state = GetOrCreateOfficeState(officeId);
            if (state.IsOwned)
            {
                message = string.Format("{0} is already owned.", definition.DisplayName);
                return false;
            }

            if (!state.IsRented && state.OutstandingRent > 0.01f)
            {
                message = string.Format("Settle {0} in outstanding rent before purchasing {1}.", ModFormatting.FormatMoney(state.OutstandingRent), definition.DisplayName);
                return false;
            }

            if (balance < definition.OfficePrice)
            {
                message = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.OfficePrice), definition.DisplayName);
                return false;
            }

            balance -= definition.OfficePrice;
            RecordFinanceExpense(CompanyFinanceCategory.OtherExpense, definition.OfficePrice, currentInGameMinute, string.Format("Purchased office {0}", definition.DisplayName));
            state.IsOwned = true;
            state.IsRented = false;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = GetWeekIndex(currentInGameMinute);
            if (string.IsNullOrWhiteSpace(_state.ActiveOfficeId))
            {
                _state.ActiveOfficeId = definition.OfficeId;
            }

            NormalizeCommercialGarageAssignments();
            message = string.Format("Purchased {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(definition.OfficePrice));
            return true;
        }

        public bool TryActivateOffice(string officeId, out string message)
        {
            message = string.Empty;
            var definition = GetOfficeDefinition(officeId);
            var state = GetOfficeState(officeId);
            if (definition == null || state == null || (!state.IsOwned && !state.IsRented))
            {
                message = "Acquire the office before activating it.";
                return false;
            }

            if (state.IsAccessSuspended || state.OutstandingRent > 0.01f)
            {
                message = string.Format("{0} is unavailable until rent arrears are paid.", definition.DisplayName);
                return false;
            }

            var relinquishedRentals = RelinquishOtherOfficeRentals(definition.OfficeId, false);
            _state.ActiveOfficeId = definition.OfficeId;
            TransferCommercialVehiclesToActiveOffice();
            message = _officeGarageLimitEnforced
                ? string.Format("Activated {0}. Commercial garage capacity is now {1}.", definition.DisplayName, Math.Max(0, definition.MaxCommercialVehicles))
                : string.Format("Activated {0}. Commercial garage limit is disabled for this save.", definition.DisplayName);
            if (relinquishedRentals.Count > 0)
            {
                message += string.Format(" Relinquished rental{0} at {1}.", relinquishedRentals.Count == 1 ? string.Empty : "s", string.Join(", ", relinquishedRentals));
            }

            return true;
        }

        public bool TryRelinquishOfficeRental(string officeId, out string message)
        {
            message = string.Empty;
            var definition = GetOfficeDefinition(officeId);
            var state = GetOfficeState(officeId);
            if (definition == null || !IsRentalOnlyOfficeAccess(state))
            {
                message = "No office rental to relinquish.";
                return false;
            }

            var hasOutstandingRent = state.OutstandingRent > 0.01f;
            if (string.Equals(_state.ActiveOfficeId, definition.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                var fallbackState = FindOperationalFallbackOfficeState(definition.OfficeId);
                if (fallbackState == null)
                {
                    message = "Activate or acquire another available office before relinquishing this rental.";
                    return false;
                }

                var fallbackDefinition = GetOfficeDefinition(fallbackState.OfficeId);
                ReleaseOfficeRental(state, false);
                _state.ActiveOfficeId = fallbackDefinition != null ? fallbackDefinition.OfficeId : string.Empty;
                TransferCommercialVehiclesToActiveOffice();
                message = hasOutstandingRent
                    ? string.Format("Relinquished rental for {0}. Outstanding arrears remain due. Operations moved to {1}.", definition.DisplayName, fallbackDefinition != null ? fallbackDefinition.DisplayName : "another office")
                    : string.Format("Relinquished rental for {0}. Operations moved to {1}.", definition.DisplayName, fallbackDefinition != null ? fallbackDefinition.DisplayName : "another office");
                return true;
            }

            ReleaseOfficeRental(state, false);
            message = hasOutstandingRent
                ? string.Format("Relinquished rental for {0}. Outstanding arrears remain due.", definition.DisplayName)
                : string.Format("Relinquished rental for {0}.", definition.DisplayName);
            return true;
        }

        public bool TryPayOfficeArrears(string officeId, ref float balance, out string message)
        {
            message = string.Empty;
            var definition = GetOfficeDefinition(officeId);
            var state = GetOfficeState(officeId);
            if (definition == null || state == null || state.OutstandingRent <= 0.01f)
            {
                message = "No outstanding office rent.";
                return false;
            }

            if (balance < state.OutstandingRent)
            {
                message = string.Format("Need {0} to settle office arrears.", ModFormatting.FormatMoney(state.OutstandingRent));
                return false;
            }

            var rentDue = state.OutstandingRent;
            balance -= state.OutstandingRent;
            state.OutstandingRent = 0f;
            state.IsAccessSuspended = false;
            RecordFinanceExpense(CompanyFinanceCategory.OfficeRent, rentDue, string.Format("Office arrears for {0}", definition.DisplayName));
            message = string.Format("Settled office rent for {0}.", definition.DisplayName);
            return true;
        }

        private bool TryAcquireOfficeRental(string officeId, ref float balance, int currentInGameMinute, bool allowTransfer, out string message)
        {
            message = string.Empty;
            var definition = GetOfficeDefinition(officeId);
            if (definition == null)
            {
                message = "Office definition unavailable.";
                return false;
            }

            var state = GetOrCreateOfficeState(officeId);
            if (state.IsOwned || state.IsRented)
            {
                message = string.Format("{0} already has access.", definition.DisplayName);
                return false;
            }

            if (state.OutstandingRent > 0.01f)
            {
                message = string.Format("Settle {0} in outstanding rent before renting {1} again.", ModFormatting.FormatMoney(state.OutstandingRent), definition.DisplayName);
                return false;
            }

            var sourceRental = GetTransferableOfficeRental(officeId);
            if (sourceRental != null && !allowTransfer)
            {
                var sourceDefinition = GetOfficeDefinition(sourceRental.OfficeId);
                message = sourceDefinition != null
                    ? string.Format("Relinquish or transfer your current rental at {0} before renting {1}.", sourceDefinition.DisplayName, definition.DisplayName)
                    : string.Format("Relinquish or transfer your current office rental before renting {0}.", definition.DisplayName);
                return false;
            }

            var upfrontRent = Math.Max(0f, definition.WeeklyOfficeRent);
            if (balance < upfrontRent)
            {
                message = string.Format("Need {0} to rent {1}.", ModFormatting.FormatMoney(upfrontRent), definition.DisplayName);
                return false;
            }

            balance -= upfrontRent;
            RecordFinanceExpense(CompanyFinanceCategory.OfficeRent, upfrontRent, currentInGameMinute, string.Format("Office access for {0}", definition.DisplayName));
            if (sourceRental != null)
            {
                ReleaseOfficeRental(sourceRental, false);
            }

            state.IsRented = true;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = GetWeekIndex(currentInGameMinute);
            _state.ActiveOfficeId = definition.OfficeId;
            TransferCommercialVehiclesToActiveOffice();
            message = sourceRental != null
                ? string.Format("Transferred office rental to {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(upfrontRent))
                : string.Format("Rented {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(upfrontRent));
            return true;
        }

        public bool TryRentApartment(string interiorId, ref float balance, int currentInGameMinute, out string message)
        {
            return TryAcquireApartmentRental(interiorId, ref balance, currentInGameMinute, out message);
        }

        private bool TryAcquireApartmentRental(string interiorId, ref float balance, int currentInGameMinute, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            if (definition == null)
            {
                message = "Apartment definition unavailable.";
                return false;
            }

            var state = GetOrCreateApartmentState(interiorId);
            if (state.IsOwned || state.IsRented)
            {
                message = string.Format("{0} already has access.", definition.DisplayName);
                return false;
            }

            if (state.OutstandingRent > 0.01f)
            {
                message = string.Format("Settle {0} in outstanding rent before renting {1} again.", ModFormatting.FormatMoney(state.OutstandingRent), definition.DisplayName);
                return false;
            }

            var sourceRental = GetTransferableApartmentRental(interiorId);
            var upfrontRent = Math.Max(0f, definition.InteriorWeeklyRent);
            if (balance < upfrontRent)
            {
                message = string.Format("Need {0} to rent {1}.", ModFormatting.FormatMoney(upfrontRent), definition.DisplayName);
                return false;
            }

            balance -= upfrontRent;
            RecordFinanceExpense(CompanyFinanceCategory.ApartmentRent, upfrontRent, currentInGameMinute, string.Format("Apartment access for {0}", definition.DisplayName));
            if (sourceRental != null)
            {
                ReleaseApartmentRental(sourceRental, false);
            }

            state.IsOwned = false;
            state.IsRented = true;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = GetWeekIndex(currentInGameMinute);
            _state.ActiveApartmentId = definition.InteriorId;
            ReassignPersonalVehiclesToActiveApartment();
            message = sourceRental != null
                ? string.Format("Transferred apartment rental to {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(upfrontRent))
                : string.Format("Rented {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(upfrontRent));
            return true;
        }

        public bool TryPurchaseApartment(string interiorId, ref float balance, int currentInGameMinute, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            if (definition == null)
            {
                message = "Apartment definition unavailable.";
                return false;
            }

            var state = GetOrCreateApartmentState(interiorId);
            if (state.IsOwned)
            {
                message = string.Format("{0} is already owned.", definition.DisplayName);
                return false;
            }

            var wasRental = state.IsRented;
            if (!wasRental && state.OutstandingRent > 0.01f)
            {
                message = string.Format("Settle {0} in outstanding rent before purchasing {1}.", ModFormatting.FormatMoney(state.OutstandingRent), definition.DisplayName);
                return false;
            }

            if (balance < definition.InteriorPrice)
            {
                message = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.InteriorPrice), definition.DisplayName);
                return false;
            }

            balance -= definition.InteriorPrice;
            RecordFinanceExpense(CompanyFinanceCategory.OtherExpense, definition.InteriorPrice, currentInGameMinute, string.Format("Purchased apartment {0}", definition.DisplayName));
            state.IsOwned = true;
            state.IsRented = false;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = -1;
            _state.ActiveApartmentId = definition.InteriorId;

            ReassignPersonalVehiclesToActiveApartment();
            message = wasRental
                ? string.Format("Purchased {0} for {1}. Rental access was converted to owned access.", definition.DisplayName, ModFormatting.FormatMoney(definition.InteriorPrice))
                : string.Format("Purchased {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(definition.InteriorPrice));
            return true;
        }

        public bool TryActivateApartment(string interiorId, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            var state = GetApartmentState(interiorId);
            if (definition == null || state == null || !HasApartmentAccess(state))
            {
                message = "Acquire the apartment before activating it.";
                return false;
            }

            if (state.IsAccessSuspended || state.OutstandingRent > 0.01f)
            {
                message = string.Format("{0} is unavailable until rent arrears are paid.", definition.DisplayName);
                return false;
            }

            _state.ActiveApartmentId = definition.InteriorId;
            ReassignPersonalVehiclesToActiveApartment();
            message = string.Format("Activated {0}.", definition.DisplayName);
            return true;
        }

        public bool TryCancelApartmentRental(string interiorId, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            var state = GetApartmentState(interiorId);
            if (definition == null || !IsRentalOnlyApartmentAccess(state))
            {
                message = "No apartment rental to cancel.";
                return false;
            }

            var hasOutstandingRent = state.OutstandingRent > 0.01f;
            var isActiveApartment = string.Equals(_state.ActiveApartmentId, definition.InteriorId, StringComparison.OrdinalIgnoreCase);
            if (isActiveApartment)
            {
                var fallbackState = FindOperationalFallbackApartmentState(definition.InteriorId);
                var fallbackDefinition = fallbackState != null ? GetInteriorDefinition(fallbackState.InteriorId) : null;
                ReleaseApartmentRental(state, false);
                _state.ActiveApartmentId = fallbackDefinition != null ? fallbackDefinition.InteriorId : string.Empty;
                ReassignPersonalVehiclesToActiveApartment();
                message = hasOutstandingRent
                    ? string.Format(
                        "Canceled rental for {0}. Outstanding arrears remain due.{1}",
                        definition.DisplayName,
                        fallbackDefinition != null ? string.Format(" Active residence moved to {0}.", fallbackDefinition.DisplayName) : " No active apartment is selected.")
                    : string.Format(
                        "Canceled rental for {0}.{1}",
                        definition.DisplayName,
                        fallbackDefinition != null ? string.Format(" Active residence moved to {0}.", fallbackDefinition.DisplayName) : " No active apartment is selected.");
                return true;
            }

            ReleaseApartmentRental(state, false);
            message = hasOutstandingRent
                ? string.Format("Canceled rental for {0}. Outstanding arrears remain due.", definition.DisplayName)
                : string.Format("Canceled rental for {0}.", definition.DisplayName);
            return true;
        }

        public bool TrySellApartment(string interiorId, ref float balance, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            var state = GetApartmentState(interiorId);
            if (definition == null || state == null || !state.IsOwned)
            {
                message = "No owned apartment to sell.";
                return false;
            }

            var refund = Math.Max(0f, definition.InteriorPrice * ApartmentSaleRefundRatio);
            var isActiveApartment = string.Equals(_state.ActiveApartmentId, definition.InteriorId, StringComparison.OrdinalIgnoreCase);
            var fallbackState = isActiveApartment ? FindOperationalFallbackApartmentState(definition.InteriorId) : null;
            var fallbackDefinition = fallbackState != null ? GetInteriorDefinition(fallbackState.InteriorId) : null;

            balance += refund;
            RecordFinanceIncome(CompanyFinanceCategory.OtherIncome, refund, string.Format("Sold apartment {0}", definition.DisplayName));
            ReleaseApartmentOwnership(state);
            if (isActiveApartment)
            {
                _state.ActiveApartmentId = fallbackDefinition != null ? fallbackDefinition.InteriorId : string.Empty;
                ReassignPersonalVehiclesToActiveApartment();
            }

            message = string.Format(
                "Sold {0} for {1}.{2}",
                definition.DisplayName,
                ModFormatting.FormatMoney(refund),
                isActiveApartment
                    ? fallbackDefinition != null
                        ? string.Format(" Active residence moved to {0}.", fallbackDefinition.DisplayName)
                        : " No active apartment is selected."
                    : string.Empty);
            return true;
        }

        public bool TryPayApartmentArrears(string interiorId, ref float balance, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            var state = GetApartmentState(interiorId);
            if (definition == null || state == null || state.OutstandingRent <= 0.01f)
            {
                message = "No outstanding apartment rent.";
                return false;
            }

            if (balance < state.OutstandingRent)
            {
                message = string.Format("Need {0} to settle apartment arrears.", ModFormatting.FormatMoney(state.OutstandingRent));
                return false;
            }

            var rentDue = state.OutstandingRent;
            balance -= state.OutstandingRent;
            state.OutstandingRent = 0f;
            state.IsAccessSuspended = false;
            RecordFinanceExpense(CompanyFinanceCategory.ApartmentRent, rentDue, string.Format("Apartment arrears for {0}", definition.DisplayName));
            message = string.Format("Settled apartment rent for {0}.", definition.DisplayName);
            return true;
        }

        public bool TryPurchaseCommercialVehicle(VehicleDefinition cargoDefinition, VehicleDefinition tractorDefinition, ref float balance, out OwnedCommercialVehiclePersistenceEntry vehicle, out string message)
        {
            vehicle = null;
            message = string.Empty;

            string officeReason;
            if (!CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            if (!TryNormalizeCommercialSelection(ref cargoDefinition, ref tractorDefinition, "purchase", out message))
            {
                return false;
            }

            var poweredDefinition = tractorDefinition ?? cargoDefinition;
            var cargoRecordDefinition = cargoDefinition ?? tractorDefinition;
            var hasSeparateCargoVehicle = cargoDefinition != null && cargoDefinition.IsTrailer && tractorDefinition != null;
            var purchasePrice = Math.Max(0f, cargoDefinition != null ? cargoDefinition.Price : 0f) + Math.Max(0f, tractorDefinition != null ? tractorDefinition.Price : 0f);
            if (balance < purchasePrice)
            {
                message = string.Format("Need {0} to purchase this commercial vehicle.", ModFormatting.FormatMoney(purchasePrice));
                return false;
            }

            balance -= purchasePrice;
            RecordFinanceExpense(
                CompanyFinanceCategory.OtherExpense,
                purchasePrice,
                string.Format(
                    "Purchased commercial vehicle {0}",
                    BuildCommercialDisplayName(
                        poweredDefinition != null ? poweredDefinition.DisplayName : string.Empty,
                        cargoDefinition != null ? cargoDefinition.DisplayName : string.Empty,
                        hasSeparateCargoVehicle)));
            vehicle = new OwnedCommercialVehiclePersistenceEntry
            {
                AssetId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                DisplayName = BuildCommercialDisplayName(
                    poweredDefinition != null ? poweredDefinition.DisplayName : string.Empty,
                    cargoDefinition != null ? cargoDefinition.DisplayName : string.Empty,
                    hasSeparateCargoVehicle),
                PoweredModelName = poweredDefinition != null ? poweredDefinition.ModelName : string.Empty,
                CargoModelName = cargoRecordDefinition != null ? cargoRecordDefinition.ModelName : string.Empty,
                HasSeparateCargoVehicle = hasSeparateCargoVehicle,
                PurchasePrice = purchasePrice,
                AssignedOfficeId = _state.ActiveOfficeId,
                IsRental = false,
                DailyRent = 0f,
                LastChargedDayIndex = -1,
                InActiveGarage = GetActiveCommercialGarageVehicles().Count() < GetActiveOfficeCapacity(),
                IsDeployed = false,
                PoweredPosition = ActiveOffice != null ? ActiveOffice.SpawnPosition : Vector3.Zero,
                PoweredHeading = ActiveOffice != null ? ActiveOffice.SpawnHeading : 0f,
                CargoType = cargoDefinition != null ? cargoDefinition.CargoType : VehicleCargoType.Unknown,
                CapacityTons = cargoDefinition != null ? Math.Max(0f, cargoDefinition.CapacityTons) : 0f,
                Commodity = string.Empty,
                WeightTons = 0f,
                CargoCondition = 0f,
                TotalLostTons = 0f,
                SourceIndustryId = string.Empty,
                SourceDistrictName = string.Empty,
                CurrentFuelLiters = 0f,
                MaintenanceCondition = 1f,
                LastMaintenanceWeekIndex = GetWeekIndex(GetTrackedCurrentInGameMinute()),
                LastInspectionWeekIndex = GetWeekIndex(GetTrackedCurrentInGameMinute()),
                InspectionOverdueWeeks = 0,
                LifetimeMaintenanceCost = 0f,
            };

            _state.CommercialVehicles.Add(vehicle);
            NormalizeCommercialGarageAssignments();
            message = vehicle.InActiveGarage
                ? string.Format("Purchased {0} for {1}. Assigned to active office garage.", vehicle.DisplayName, ModFormatting.FormatMoney(purchasePrice))
                : string.Format("Purchased {0} for {1}. Office garage is full, so it was moved to reserve.", vehicle.DisplayName, ModFormatting.FormatMoney(purchasePrice));
            return true;
        }

        public bool TryRentCommercialVehicle(VehicleDefinition cargoDefinition, VehicleDefinition tractorDefinition, ref float balance, int currentInGameMinute, out OwnedCommercialVehiclePersistenceEntry vehicle, out string message)
        {
            vehicle = null;
            message = string.Empty;

            string officeReason;
            if (!CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            if (!TryNormalizeCommercialSelection(ref cargoDefinition, ref tractorDefinition, "rental", out message))
            {
                return false;
            }

            var poweredDefinition = tractorDefinition ?? cargoDefinition;
            var cargoRecordDefinition = cargoDefinition ?? tractorDefinition;
            var hasSeparateCargoVehicle = cargoDefinition != null && cargoDefinition.IsTrailer && tractorDefinition != null;
            var dailyRent = Math.Max(0f, cargoDefinition != null ? cargoDefinition.DailyRent : 0f) + Math.Max(0f, tractorDefinition != null ? tractorDefinition.DailyRent : 0f);
            if (dailyRent <= 0.001f)
            {
                message = "Rental requires a positive dailyRent on the selected vehicle or truck.";
                return false;
            }
            vehicle = new OwnedCommercialVehiclePersistenceEntry
            {
                AssetId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                DisplayName = BuildCommercialDisplayName(
                    poweredDefinition != null ? poweredDefinition.DisplayName : string.Empty,
                    cargoDefinition != null ? cargoDefinition.DisplayName : string.Empty,
                    hasSeparateCargoVehicle),
                PoweredModelName = poweredDefinition != null ? poweredDefinition.ModelName : string.Empty,
                CargoModelName = cargoRecordDefinition != null ? cargoRecordDefinition.ModelName : string.Empty,
                HasSeparateCargoVehicle = hasSeparateCargoVehicle,
                PurchasePrice = 0f,
                AssignedOfficeId = _state.ActiveOfficeId,
                IsRental = true,
                DailyRent = dailyRent,
                LastChargedDayIndex = GetDayIndex(currentInGameMinute),
                InActiveGarage = GetActiveCommercialGarageVehicles().Count() < GetActiveOfficeCapacity(),
                IsDeployed = false,
                PoweredPosition = ActiveOffice != null ? ActiveOffice.SpawnPosition : Vector3.Zero,
                PoweredHeading = ActiveOffice != null ? ActiveOffice.SpawnHeading : 0f,
                CargoType = cargoDefinition != null ? cargoDefinition.CargoType : VehicleCargoType.Unknown,
                CapacityTons = cargoDefinition != null ? Math.Max(0f, cargoDefinition.CapacityTons) : 0f,
                Commodity = string.Empty,
                WeightTons = 0f,
                CargoCondition = 0f,
                TotalLostTons = 0f,
                SourceIndustryId = string.Empty,
                SourceDistrictName = string.Empty,
                CurrentFuelLiters = 0f,
            };

            _state.CommercialVehicles.Add(vehicle);
            NormalizeCommercialGarageAssignments();
            message = vehicle.InActiveGarage
                ? string.Format(
                    "Rented {0} for {1}/day. No upfront cost charged.",
                    vehicle.DisplayName,
                    ModFormatting.FormatMoney(dailyRent))
                : string.Format(
                    "Rented {0} for {1}/day. No upfront cost charged. Office garage is full, so it was moved to reserve.",
                    vehicle.DisplayName,
                    ModFormatting.FormatMoney(dailyRent));
            return true;
        }

        public bool TrySellCommercialVehicle(string assetId, FleetManager fleetManager, VehicleFuelSystem fuelSystem, ref float balance, out string message)
        {
            message = string.Empty;
            var vehicle = GetCommercialVehicle(assetId);
            if (vehicle == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            if (vehicle.IsRental)
            {
                message = "Use End Rent for rented vehicles.";
                return false;
            }

            if (vehicle.IsDeployed)
            {
                TryStoreCommercialVehicle(assetId, fleetManager, fuelSystem, out _);
            }

            var refund = Math.Max(0f, vehicle.PurchasePrice * CommercialVehicleSaleRefundRatio);
            refund = CalculateCommercialVehicleSaleRefund(vehicle);
            balance += refund;
            RecordFinanceIncome(CompanyFinanceCategory.OtherIncome, refund, string.Format("Sold commercial vehicle {0}", vehicle.DisplayName));
            _state.CommercialVehicles.Remove(vehicle);
            NormalizeCommercialGarageAssignments();
            message = refund > 0.001f
                ? string.Format("Sold {0} for {1}.", vehicle.DisplayName, ModFormatting.FormatMoney(refund))
                : string.Format("Removed {0} from the garage roster.", vehicle.DisplayName);
            return true;
        }

        public bool TryEndCommercialVehicleRental(string assetId, FleetManager fleetManager, VehicleFuelSystem fuelSystem, ref float balance, out string message)
        {
            message = string.Empty;
            var vehicle = GetCommercialVehicle(assetId);
            if (vehicle == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            if (!vehicle.IsRental)
            {
                message = "This vehicle is company-owned, not rented.";
                return false;
            }

            if (vehicle.IsDeployed)
            {
                TryStoreCommercialVehicle(assetId, fleetManager, fuelSystem, out _);
            }

            _state.CommercialVehicles.Remove(vehicle);
            NormalizeCommercialGarageAssignments();
            message = string.Format("Ended rental for {0}.", vehicle.DisplayName);
            return true;
        }

        public bool TrySetCommercialVehicleActive(string assetId, out string message)
        {
            message = string.Empty;
            var vehicle = GetCommercialVehicle(assetId);
            if (vehicle == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            string officeReason;
            if (!CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            if (vehicle.InActiveGarage)
            {
                message = string.Format("{0} is already in the active garage.", vehicle.DisplayName);
                return false;
            }

            if (GetActiveCommercialGarageVehicles().Count() >= GetActiveOfficeCapacity())
            {
                message = "Active office garage is full. Move another vehicle to reserve first.";
                return false;
            }

            vehicle.AssignedOfficeId = _state.ActiveOfficeId;
            vehicle.InActiveGarage = true;
            NormalizeCommercialGarageAssignments();
            message = string.Format("Moved {0} into the active garage.", vehicle.DisplayName);
            return true;
        }

        public bool TrySetCommercialVehicleReserve(string assetId, FleetManager fleetManager, VehicleFuelSystem fuelSystem, out string message)
        {
            message = string.Empty;
            var vehicle = GetCommercialVehicle(assetId);
            if (vehicle == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            if (!vehicle.InActiveGarage)
            {
                message = string.Format("{0} is already in reserve.", vehicle.DisplayName);
                return false;
            }

            if (vehicle.IsDeployed)
            {
                TryStoreCommercialVehicle(assetId, fleetManager, fuelSystem, out _);
            }

            vehicle.InActiveGarage = false;
            message = string.Format("Moved {0} to reserve.", vehicle.DisplayName);
            return true;
        }

        public bool TryDeployCommercialVehicle(string assetId, FleetManager fleetManager, VehicleFuelSystem fuelSystem, Func<Vector3, Vector3> getGroundPosition, Vector3 spawnPosition, float spawnHeading, out string message)
        {
            var vehicle = GetCommercialVehicle(assetId);
            if (vehicle == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            return TrySpawnCommercialVehicle(vehicle, fleetManager, fuelSystem, getGroundPosition, spawnPosition, spawnHeading, out message);
        }

        public bool TryStoreCommercialVehicle(string assetId, FleetManager fleetManager, VehicleFuelSystem fuelSystem, out string message)
        {
            var entry = GetCommercialVehicle(assetId);
            if (entry == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            return CaptureAndStoreCommercialVehicle(entry, fleetManager, fuelSystem, true, out message);
        }

        public bool TryPurchasePersonalVehicle(DealershipVehicleDefinition definition, ref float balance, out OwnedPersonalVehiclePersistenceEntry vehicle, out string message)
        {
            vehicle = null;
            message = string.Empty;

            string apartmentReason;
            if (!CanUseApartmentSystems(out apartmentReason))
            {
                message = apartmentReason;
                return false;
            }

            if (definition == null)
            {
                message = "No personal vehicle selected.";
                return false;
            }

            if (balance < definition.Price)
            {
                message = string.Format("Need {0} to purchase this personal vehicle.", ModFormatting.FormatMoney(definition.Price));
                return false;
            }

            balance -= definition.Price;
            RecordFinanceExpense(CompanyFinanceCategory.OtherExpense, definition.Price, string.Format("Purchased personal vehicle {0}", definition.DisplayName));
            vehicle = new OwnedPersonalVehiclePersistenceEntry
            {
                AssetId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                DisplayName = definition.DisplayName,
                ModelName = definition.ModelName,
                Category = definition.Category,
                PurchasePrice = definition.Price,
                AssignedApartmentId = _state.ActiveApartmentId,
                IsDeployed = false,
                Position = ActiveApartment != null ? ActiveApartment.GaragePosition : Vector3.Zero,
                Heading = 0f,
            };

            _state.PersonalVehicles.Add(vehicle);
            message = string.Format("Purchased {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(definition.Price));
            return true;
        }

        public bool TryDeployPersonalVehicle(string assetId, Vector3 spawnPosition, float spawnHeading, out string message)
        {
            var vehicle = GetPersonalVehicle(assetId);
            if (vehicle == null)
            {
                message = "Personal vehicle record not found.";
                return false;
            }

            return TrySpawnPersonalVehicle(vehicle, spawnPosition, spawnHeading, true, out message);
        }

        public bool TryStorePersonalVehicle(string assetId, out string message)
        {
            var entry = GetPersonalVehicle(assetId);
            if (entry == null)
            {
                message = "Personal vehicle record not found.";
                return false;
            }

            PersonalVehicleRuntimeState runtime;
            if (!_personalRuntime.TryGetValue(entry.AssetId, out runtime))
            {
                entry.IsDeployed = false;
                message = string.Format("{0} is already stored.", entry.DisplayName);
                return false;
            }

            var vehicle = Entity.FromHandle(runtime.VehicleHandle) as Vehicle;
            if (vehicle != null && vehicle.Exists())
            {
                entry.Appearance = CaptureVehicleAppearance(vehicle);
                entry.Position = vehicle.Position;
                entry.Heading = vehicle.Heading;
                vehicle.Delete();
            }

            entry.IsDeployed = false;
            _personalRuntime.Remove(entry.AssetId);
            message = string.Format("Stored {0}.", entry.DisplayName);
            return true;
        }

        public IEnumerable<OwnedCommercialVehiclePersistenceEntry> GetActiveCommercialGarageVehicles()
        {
            return _state.CommercialVehicles
                .Where(entry => entry != null && entry.InActiveGarage)
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<OwnedCommercialVehiclePersistenceEntry> GetReserveCommercialVehicles()
        {
            return _state.CommercialVehicles
                .Where(entry => entry != null && !entry.InActiveGarage)
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<OwnedPersonalVehiclePersistenceEntry> GetOwnedPersonalVehicles()
        {
            return _state.PersonalVehicles
                .Where(entry => entry != null)
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        public OfficeDefinition GetOfficeDefinition(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
            {
                return null;
            }

            OfficeDefinition definition;
            return _officeDefinitionsById.TryGetValue(officeId.Trim(), out definition)
                ? definition
                : null;
        }

        public InteriorDefinition GetInteriorDefinition(string interiorId)
        {
            if (string.IsNullOrWhiteSpace(interiorId))
            {
                return null;
            }

            InteriorDefinition definition;
            return _interiorDefinitionsById.TryGetValue(interiorId.Trim(), out definition)
                ? definition
                : null;
        }

        public DealershipVehicleDefinition GetPersonalVehicleDefinition(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return null;
            }

            DealershipVehicleDefinition definition;
            return _personalVehicleDefinitionsById.TryGetValue(vehicleId.Trim(), out definition)
                ? definition
                : null;
        }

        public OfficeOwnershipPersistenceEntry GetOfficeState(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
            {
                return null;
            }

            return _state.Offices.FirstOrDefault(entry => entry != null && string.Equals(entry.OfficeId, officeId, StringComparison.OrdinalIgnoreCase));
        }

        public OfficeObjectDefinition GetOfficeObjectDefinition(int definitionId)
        {
            OfficeObjectDefinition definition;
            return _officeObjectDefinitionsById.TryGetValue(definitionId, out definition)
                ? definition
                : null;
        }

        public IReadOnlyList<OfficeObjectPersistenceEntry> GetOfficeObjects(string officeId, bool includeUnplaced = true)
        {
            if (string.IsNullOrWhiteSpace(officeId))
            {
                return Array.Empty<OfficeObjectPersistenceEntry>();
            }

            return _state.OfficeObjects
                .Where(entry => entry != null
                    && string.Equals(entry.OfficeId, officeId, StringComparison.OrdinalIgnoreCase)
                    && (includeUnplaced || entry.IsPlaced))
                .OrderBy(entry => entry.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public int GetOfficeObjectCount(string officeId, int definitionId, bool includeUnplaced = true)
        {
            if (string.IsNullOrWhiteSpace(officeId) || definitionId <= 0)
            {
                return 0;
            }

            return _state.OfficeObjects.Count(entry => entry != null
                && string.Equals(entry.OfficeId, officeId, StringComparison.OrdinalIgnoreCase)
                && entry.DefinitionId == definitionId
                && (includeUnplaced || entry.IsPlaced));
        }

        public bool HasOfficeObjectFunction(string officeId, OfficeObjectFunction function)
        {
            return GetOfficeObjects(officeId, false)
                .Select(entry => GetOfficeObjectDefinition(entry.DefinitionId))
                .Any(definition => definition != null && definition.Function == function);
        }

        public bool HasAnyOfficeObjectFunction(OfficeObjectFunction function, bool placedOnly = true)
        {
            return _state.OfficeObjects
                .Where(entry => entry != null && (!placedOnly || entry.IsPlaced))
                .Select(entry => GetOfficeObjectDefinition(entry.DefinitionId))
                .Any(definition => definition != null && definition.Function == function);
        }

        public float GetOfficeObjectFunctionCapacity(string officeId, OfficeObjectFunction function)
        {
            return GetOfficeObjects(officeId, false)
                .Select(entry => GetOfficeObjectDefinition(entry.DefinitionId))
                .Where(definition => definition != null && definition.Function == function)
                .Sum(definition => Math.Max(0f, definition.Capacity));
        }

        public float GetOfficeObjectStoredResourceAmount(string officeId, OfficeObjectFunction function)
        {
            return GetOfficeObjects(officeId, false)
                .Where(entry =>
                {
                    var definition = GetOfficeObjectDefinition(entry.DefinitionId);
                    return definition != null && definition.Function == function;
                })
                .Sum(entry => Math.Max(0f, entry.StoredResourceAmount));
        }

        public OfficeObjectPersistenceEntry GetFirstPlacedOfficeObjectByFunction(string officeId, OfficeObjectFunction function)
        {
            return GetOfficeObjects(officeId, false)
                .FirstOrDefault(entry =>
                {
                    var definition = GetOfficeObjectDefinition(entry.DefinitionId);
                    return definition != null && definition.Function == function;
                });
        }

        public bool TryPurchaseOfficeObject(string officeId, int definitionId, ref float balance, out OfficeObjectPersistenceEntry purchasedEntry, out string message)
        {
            purchasedEntry = null;
            message = string.Empty;

            var officeDefinition = GetOfficeDefinition(officeId);
            var officeState = GetOfficeState(officeId);
            if (officeDefinition == null || officeState == null || (!officeState.IsOwned && !officeState.IsRented))
            {
                message = "Acquire the office before purchasing objects.";
                return false;
            }

            if (officeState.IsAccessSuspended || officeState.OutstandingRent > 0.01f)
            {
                message = string.Format("{0} is unavailable until office arrears are settled.", officeDefinition.DisplayName);
                return false;
            }

            var definition = GetOfficeObjectDefinition(definitionId);
            if (definition == null)
            {
                message = "Office object definition unavailable.";
                return false;
            }

            if (definition.Function == OfficeObjectFunction.Headquarters)
            {
                if (!officeState.IsOwned)
                {
                    message = "Landmark HQ modules can only be installed in an owned office.";
                    return false;
                }

                if (HasAnyOfficeObjectFunction(OfficeObjectFunction.Headquarters, false))
                {
                    message = "The company already has a Landmark HQ project in progress.";
                    return false;
                }
            }

            if (definition.PerOfficeLimit > 0 && GetOfficeObjectCount(officeId, definitionId) >= definition.PerOfficeLimit)
            {
                message = string.Format("{0} limit reached for this office.", definition.DisplayName);
                return false;
            }

            if (balance < definition.Price)
            {
                message = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.Price), definition.DisplayName);
                return false;
            }

            balance -= definition.Price;
            RecordFinanceExpense(CompanyFinanceCategory.OtherExpense, definition.Price, string.Format("Purchased office object {0}", definition.DisplayName));
            purchasedEntry = new OfficeObjectPersistenceEntry
            {
                InstanceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                OfficeId = officeId,
                DefinitionId = definitionId,
                IsPlaced = false,
                Position = Vector3.Zero,
                Rotation = Vector3.Zero,
                StoredResourceAmount = 0f,
            };

            _state.OfficeObjects.Add(purchasedEntry);
            message = string.Format("Purchased {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(definition.Price));
            return true;
        }

        public bool TryPlaceOfficeObject(string instanceId, Vector3 position, Vector3 rotation, out OfficeObjectPersistenceEntry placedEntry, out string message)
        {
            placedEntry = null;
            message = string.Empty;

            var entry = GetOfficeObject(instanceId);
            if (entry == null)
            {
                message = "Office object record not found.";
                return false;
            }

            entry.Position = position;
            entry.Rotation = rotation;
            entry.IsPlaced = true;
            placedEntry = entry;

            var definition = GetOfficeObjectDefinition(entry.DefinitionId);
            message = string.Format("Placed {0}.", definition != null ? definition.DisplayName : "office object");
            return true;
        }

        public bool TryUpdateOfficeObjectStoredResourceAmount(string instanceId, float amount, out OfficeObjectPersistenceEntry updatedEntry)
        {
            updatedEntry = null;
            var entry = GetOfficeObject(instanceId);
            if (entry == null)
            {
                return false;
            }

            entry.StoredResourceAmount = Math.Max(0f, amount);
            updatedEntry = entry;
            return true;
        }

        public ApartmentOwnershipPersistenceEntry GetApartmentState(string interiorId)
        {
            if (string.IsNullOrWhiteSpace(interiorId))
            {
                return null;
            }

            return _state.Apartments.FirstOrDefault(entry => entry != null && string.Equals(entry.InteriorId, interiorId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsCommercialVehicleDeployed(string assetId)
        {
            var entry = GetCommercialVehicle(assetId);
            return entry != null && entry.IsDeployed;
        }

        public bool IsPersonalVehicleDeployed(string assetId)
        {
            var entry = GetPersonalVehicle(assetId);
            return entry != null && entry.IsDeployed;
        }

        public IReadOnlyList<CommercialVehicleMapBlipInfo> GetCommercialVehicleBlipInfos()
        {
            var results = new List<CommercialVehicleMapBlipInfo>();
            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var entry = _state.CommercialVehicles[i];
                if (entry == null || !entry.IsDeployed)
                {
                    continue;
                }

                CommercialVehicleRuntimeState runtime;
                if (!_commercialRuntime.TryGetValue(entry.AssetId, out runtime))
                {
                    continue;
                }

                var truck = Entity.FromHandle(runtime.TruckHandle) as Vehicle;
                if (truck == null || !truck.Exists())
                {
                    continue;
                }

                results.Add(new CommercialVehicleMapBlipInfo
                {
                    AssetId = entry.AssetId,
                    DisplayName = entry.DisplayName,
                    TruckHandle = truck.Handle,
                });
            }

            return results;
        }

        public IReadOnlyList<PersonalVehicleMapBlipInfo> GetPersonalVehicleBlipInfos()
        {
            var results = new List<PersonalVehicleMapBlipInfo>();
            for (int i = 0; i < _state.PersonalVehicles.Count; i++)
            {
                var entry = _state.PersonalVehicles[i];
                if (entry == null || !entry.IsDeployed)
                {
                    continue;
                }

                PersonalVehicleRuntimeState runtime;
                if (!_personalRuntime.TryGetValue(entry.AssetId, out runtime))
                {
                    continue;
                }

                var vehicle = Entity.FromHandle(runtime.VehicleHandle) as Vehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    entry.IsDeployed = false;
                    _personalRuntime.Remove(entry.AssetId);
                    continue;
                }

                results.Add(new PersonalVehicleMapBlipInfo
                {
                    AssetId = entry.AssetId,
                    DisplayName = entry.DisplayName,
                    VehicleHandle = vehicle.Handle,
                });
            }

            return results;
        }

        public bool TryResolveCommercialVehicleRecord(Vehicle vehicle, out OwnedCommercialVehiclePersistenceEntry entry)
        {
            entry = null;
            if (vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var candidate = _state.CommercialVehicles[i];
                if (candidate == null)
                {
                    continue;
                }

                CommercialVehicleRuntimeState runtime;
                if (!_commercialRuntime.TryGetValue(candidate.AssetId, out runtime))
                {
                    continue;
                }

                if (runtime.TruckHandle == vehicle.Handle || runtime.CargoHandle == vehicle.Handle)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private void CaptureAllRuntimeState(FleetManager fleetManager, VehicleFuelSystem fuelSystem)
        {
            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var entry = _state.CommercialVehicles[i];
                if (entry != null)
                {
                    CaptureAndStoreCommercialVehicle(entry, fleetManager, fuelSystem, false, out _);
                }
            }

            for (int i = _state.PersonalVehicles.Count - 1; i >= 0; i--)
            {
                var entry = _state.PersonalVehicles[i];
                if (entry == null)
                {
                    continue;
                }

                PersonalVehicleRuntimeState runtime;
                if (!_personalRuntime.TryGetValue(entry.AssetId, out runtime))
                {
                    continue;
                }

                var vehicle = Entity.FromHandle(runtime.VehicleHandle) as Vehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    entry.IsDeployed = false;
                    _personalRuntime.Remove(entry.AssetId);
                    continue;
                }

                entry.Appearance = CaptureVehicleAppearance(vehicle);
                entry.Position = vehicle.Position;
                entry.Heading = vehicle.Heading;
                entry.IsDeployed = true;
            }
        }

        private bool TrySpawnCommercialVehicle(
            OwnedCommercialVehiclePersistenceEntry entry,
            FleetManager fleetManager,
            VehicleFuelSystem fuelSystem,
            Func<Vector3, Vector3> getGroundPosition,
            Vector3 spawnPosition,
            float spawnHeading,
            out string message)
        {
            message = string.Empty;
            if (entry == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            if (!entry.InActiveGarage)
            {
                message = string.Format("{0} is in reserve. Move it into the active garage first.", entry.DisplayName);
                return false;
            }

            string officeReason;
            if (!CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            CaptureAndStoreCommercialVehicle(entry, fleetManager, fuelSystem, true, out _);

            var cargoDefinition = fleetManager.FindDefinitionByModelName(entry.CargoModelName);
            var tractorDefinition = entry.HasSeparateCargoVehicle
                ? fleetManager.FindDefinitionByModelName(entry.PoweredModelName)
                : null;
            if (cargoDefinition == null || (entry.HasSeparateCargoVehicle && tractorDefinition == null))
            {
                message = string.Format("Vehicle definitions for {0} are no longer available.", entry.DisplayName);
                return false;
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            var finalSpawnPosition = getGroundPosition != null ? getGroundPosition(spawnPosition) : spawnPosition;
            if (!fleetManager.SpawnSelectedVehicle(cargoDefinition, tractorDefinition, finalSpawnPosition, spawnHeading, out truck, out cargoVehicle, out message))
            {
                return false;
            }

            fleetManager.RegisterOwnedRig(truck, cargoVehicle);
            var poweredDefinition = tractorDefinition ?? cargoDefinition;
            if (fuelSystem != null && poweredDefinition != null && !poweredDefinition.IsTrailer)
            {
                fuelSystem.InitializeSpawnedVehicle(truck, entry.CurrentFuelLiters > 0.001f ? (float?)entry.CurrentFuelLiters : null);
            }

            var cargoState = fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState != null)
            {
                cargoState.CargoType = entry.CargoType != VehicleCargoType.Unknown ? entry.CargoType : cargoDefinition.CargoType;
                cargoState.CapacityTons = entry.CapacityTons > 0.001f ? entry.CapacityTons : Math.Max(0f, cargoDefinition.CapacityTons);
                cargoState.Commodity = entry.Commodity;
                cargoState.WeightTons = Math.Max(0f, Math.Min(cargoState.CapacityTons, entry.WeightTons));
                cargoState.CargoCondition = cargoState.WeightTons <= 0.001f
                    ? 0f
                    : Math.Max(0f, Math.Min(1f, entry.CargoCondition));
                cargoState.TotalLostTons = Math.Max(0f, entry.TotalLostTons);
                cargoState.SourceIndustryId = entry.SourceIndustryId;
                cargoState.SourceDistrictName = entry.SourceDistrictName;
                cargoState.PlayerContractId = entry.PlayerContractId ?? string.Empty;
                cargoState.PlayerContractDestinationIndustryId = entry.PlayerContractDestinationIndustryId ?? string.Empty;
            }

            ApplyCommercialVehicleMaintenanceState(truck, entry.MaintenanceCondition);
            if (cargoVehicle != null && cargoVehicle.Exists() && cargoVehicle.Handle != truck.Handle)
            {
                ApplyCommercialVehicleMaintenanceState(cargoVehicle, entry.MaintenanceCondition);
            }

            if (truck != null && truck.Exists())
            {
                if (entry.PoweredAppearance != null && entry.PoweredAppearance.HasData)
                {
                    ApplyVehicleAppearance(truck, entry.PoweredAppearance);
                }
                else
                {
                    entry.PoweredAppearance = CaptureVehicleAppearance(truck);
                }
            }

            if (cargoVehicle != null && cargoVehicle.Exists() && cargoVehicle.Handle != truck.Handle)
            {
                if (entry.CargoAppearance != null && entry.CargoAppearance.HasData)
                {
                    ApplyVehicleAppearance(cargoVehicle, entry.CargoAppearance);
                }
                else
                {
                    entry.CargoAppearance = CaptureVehicleAppearance(cargoVehicle);
                }
            }
            else
            {
                entry.CargoAppearance = null;
            }

            _commercialRuntime[entry.AssetId] = new CommercialVehicleRuntimeState
            {
                TruckHandle = truck != null && truck.Exists() ? truck.Handle : 0,
                CargoHandle = cargoVehicle != null && cargoVehicle.Exists() ? cargoVehicle.Handle : 0,
            };
            entry.PoweredPosition = truck != null && truck.Exists() ? truck.Position : finalSpawnPosition;
            entry.PoweredHeading = truck != null && truck.Exists() ? truck.Heading : spawnHeading;
            entry.IsDeployed = true;

            message = string.Format("Retrieved {0}.", entry.DisplayName);
            return true;
        }

        private bool CaptureAndStoreCommercialVehicle(OwnedCommercialVehiclePersistenceEntry entry, FleetManager fleetManager, VehicleFuelSystem fuelSystem, bool deleteVehicles, out string message)
        {
            message = string.Empty;
            if (entry == null)
            {
                message = "Commercial vehicle record not found.";
                return false;
            }

            CommercialVehicleRuntimeState runtime;
            if (!_commercialRuntime.TryGetValue(entry.AssetId, out runtime))
            {
                entry.IsDeployed = false;
                message = string.Format("{0} is already stored.", entry.DisplayName);
                return false;
            }

            var truck = Entity.FromHandle(runtime.TruckHandle) as Vehicle;
            var cargoVehicle = Entity.FromHandle(runtime.CargoHandle) as Vehicle;
            if (truck == null || !truck.Exists() || cargoVehicle == null || !cargoVehicle.Exists())
            {
                entry.IsDeployed = false;
                _commercialRuntime.Remove(entry.AssetId);
                message = string.Format("Stored {0}.", entry.DisplayName);
                return true;
            }

            entry.PoweredPosition = truck.Position;
            entry.PoweredHeading = truck.Heading;

            if (fleetManager != null)
            {
                var cargoState = fleetManager.GetOrCreateCargoState(cargoVehicle);
                if (cargoState != null)
                {
                    entry.CargoType = cargoState.CargoType;
                    entry.CapacityTons = cargoState.CapacityTons;
                    entry.Commodity = cargoState.Commodity;
                    entry.WeightTons = cargoState.WeightTons;
                    entry.CargoCondition = cargoState.CargoCondition;
                    entry.TotalLostTons = cargoState.TotalLostTons;
                    entry.SourceIndustryId = cargoState.SourceIndustryId;
                    entry.SourceDistrictName = cargoState.SourceDistrictName;
                    entry.PlayerContractId = cargoState.PlayerContractId;
                    entry.PlayerContractDestinationIndustryId = cargoState.PlayerContractDestinationIndustryId;
                }
            }

            if (fuelSystem != null)
            {
                var telemetry = fuelSystem.GetTelemetry(truck, cargoVehicle);
                entry.CurrentFuelLiters = telemetry != null ? telemetry.CurrentLiters : entry.CurrentFuelLiters;
            }

            entry.PoweredAppearance = CaptureVehicleAppearance(truck);
            entry.CargoAppearance = cargoVehicle != null && cargoVehicle.Exists() && cargoVehicle.Handle != truck.Handle
                ? CaptureVehicleAppearance(cargoVehicle)
                : null;

            if (!deleteVehicles)
            {
                entry.IsDeployed = true;
                message = string.Format("Captured {0}.", entry.DisplayName);
                return true;
            }

            if (deleteVehicles)
            {
                cargoVehicle.Delete();
                if (truck.Handle != cargoVehicle.Handle)
                {
                    truck.Delete();
                }
            }

            entry.IsDeployed = false;
            _commercialRuntime.Remove(entry.AssetId);
            message = string.Format("Stored {0}.", entry.DisplayName);
            return true;
        }

        private bool TrySpawnPersonalVehicle(OwnedPersonalVehiclePersistenceEntry entry, Vector3 spawnPosition, float spawnHeading, bool storeOtherVehiclesFirst, out string message)
        {
            message = string.Empty;
            if (entry == null || string.IsNullOrWhiteSpace(entry.ModelName))
            {
                message = "Personal vehicle record not found.";
                return false;
            }

            string apartmentReason;
            if (!CanUseApartmentSystems(out apartmentReason))
            {
                message = apartmentReason;
                return false;
            }

            if (storeOtherVehiclesFirst)
            {
                var otherIds = _personalRuntime.Keys.Where(key => !string.Equals(key, entry.AssetId, StringComparison.OrdinalIgnoreCase)).ToList();
                for (int i = 0; i < otherIds.Count; i++)
                {
                    TryStorePersonalVehicle(otherIds[i], out _);
                }
            }

            TryStorePersonalVehicle(entry.AssetId, out _);

            var model = new Model(entry.ModelName);
            if (!model.Request(1000))
            {
                message = string.Format("Could not load model {0}.", entry.ModelName);
                return false;
            }

            var vehicle = World.CreateVehicle(model, spawnPosition, spawnHeading);
            model.MarkAsNoLongerNeeded();
            if (vehicle == null || !vehicle.Exists())
            {
                message = string.Format("Failed to retrieve {0}.", entry.DisplayName);
                return false;
            }

            vehicle.IsPersistent = true;
            if (entry.Appearance != null && entry.Appearance.HasData)
            {
                ApplyVehicleAppearance(vehicle, entry.Appearance);
            }
            else
            {
                entry.Appearance = CaptureVehicleAppearance(vehicle);
            }

            entry.Position = vehicle.Position;
            entry.Heading = vehicle.Heading;
            entry.IsDeployed = true;
            _personalRuntime[entry.AssetId] = new PersonalVehicleRuntimeState
            {
                VehicleHandle = vehicle.Handle,
            };

            message = string.Format("Retrieved {0}.", entry.DisplayName);
            return true;
        }

        private void ProcessOfficeWeeklyCharge(OfficeDefinition definition, OfficeOwnershipPersistenceEntry state, int currentWeekIndex, ref float balance, List<string> messages)
        {
            if (definition == null || state == null || (!state.IsOwned && !state.IsRented))
            {
                return;
            }

            if (state.LastChargedWeekIndex < 0)
            {
                state.LastChargedWeekIndex = currentWeekIndex;
                return;
            }

            for (int weekIndex = state.LastChargedWeekIndex + 1; weekIndex <= currentWeekIndex; weekIndex++)
            {
                var weeklyRent = Math.Max(0f, definition.WeeklyOfficeRent);
                if (weeklyRent <= 0.01f)
                {
                    continue;
                }

                if (!state.IsAccessSuspended && state.OutstandingRent <= 0.01f && balance >= weeklyRent)
                {
                    balance -= weeklyRent;
                    RecordFinanceExpense(CompanyFinanceCategory.OfficeRent, weeklyRent, Math.Max(0, weekIndex * MinutesPerWeek), string.Format("Weekly office rent for {0}", definition.DisplayName));
                    messages.Add(string.Format("Paid weekly office rent for {0}: {1}.", definition.DisplayName, ModFormatting.FormatMoney(weeklyRent)));
                    continue;
                }

                state.OutstandingRent += weeklyRent;
                state.IsAccessSuspended = true;
                messages.Add(string.Format("Could not pay office rent for {0}. Outstanding balance is now {1}.", definition.DisplayName, ModFormatting.FormatMoney(state.OutstandingRent)));
            }

            state.LastChargedWeekIndex = currentWeekIndex;
        }

        private void ProcessApartmentWeeklyCharge(InteriorDefinition definition, ApartmentOwnershipPersistenceEntry state, int currentWeekIndex, ref float balance, List<string> messages)
        {
            if (definition == null || state == null)
            {
                return;
            }

            if (state.IsOwned)
            {
                state.IsRented = false;
                state.IsAccessSuspended = false;
                state.OutstandingRent = 0f;
                state.LastChargedWeekIndex = -1;
                return;
            }

            if (!IsRentalOnlyApartmentAccess(state))
            {
                state.LastChargedWeekIndex = -1;
                return;
            }

            if (state.LastChargedWeekIndex < 0)
            {
                state.LastChargedWeekIndex = currentWeekIndex;
                return;
            }

            for (int weekIndex = state.LastChargedWeekIndex + 1; weekIndex <= currentWeekIndex; weekIndex++)
            {
                var weeklyRent = Math.Max(0f, definition.InteriorWeeklyRent);
                if (weeklyRent <= 0.01f)
                {
                    continue;
                }

                if (!state.IsAccessSuspended && state.OutstandingRent <= 0.01f && balance >= weeklyRent)
                {
                    balance -= weeklyRent;
                    RecordFinanceExpense(CompanyFinanceCategory.ApartmentRent, weeklyRent, Math.Max(0, weekIndex * MinutesPerWeek), string.Format("Weekly apartment rent for {0}", definition.DisplayName));
                    messages.Add(string.Format("Paid weekly apartment rent for {0}: {1}.", definition.DisplayName, ModFormatting.FormatMoney(weeklyRent)));
                    continue;
                }

                state.OutstandingRent += weeklyRent;
                state.IsAccessSuspended = true;
                messages.Add(string.Format("Could not pay apartment rent for {0}. Outstanding balance is now {1}.", definition.DisplayName, ModFormatting.FormatMoney(state.OutstandingRent)));
            }

            state.LastChargedWeekIndex = currentWeekIndex;
        }

        private int GetActiveOfficeCapacity()
        {
            if (ActiveOffice == null)
            {
                return 0;
            }

            return _officeGarageLimitEnforced
                ? Math.Max(0, ActiveOffice.MaxCommercialVehicles)
                : int.MaxValue;
        }

        private void TransferCommercialVehiclesToActiveOffice()
        {
            if (string.IsNullOrWhiteSpace(_state.ActiveOfficeId))
            {
                return;
            }

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var entry = _state.CommercialVehicles[i];
                if (entry != null)
                {
                    entry.AssignedOfficeId = _state.ActiveOfficeId;
                }
            }

            NormalizeCommercialGarageAssignments();
        }

        private void NormalizeCommercialGarageAssignments()
        {
            var capacity = GetActiveOfficeCapacity();
            var ordered = _state.CommercialVehicles
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.InActiveGarage)
                .ThenByDescending(entry => entry.IsDeployed)
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].AssignedOfficeId = _state.ActiveOfficeId;
                ordered[i].InActiveGarage = i < capacity;
            }
        }

        private void ReassignPersonalVehiclesToActiveApartment()
        {
            var assignedApartmentId = _state.ActiveApartmentId ?? string.Empty;

            for (int i = 0; i < _state.PersonalVehicles.Count; i++)
            {
                var entry = _state.PersonalVehicles[i];
                if (entry != null)
                {
                    entry.AssignedApartmentId = assignedApartmentId;
                }
            }
        }

        private void EnsureValidSelections()
        {
            if (GetOfficeDefinition(_state.ActiveOfficeId) == null)
            {
                _state.ActiveOfficeId = string.Empty;
            }

            if (GetInteriorDefinition(_state.ActiveApartmentId) == null)
            {
                _state.ActiveApartmentId = string.Empty;
            }
        }

        private void NormalizeOfficeAccessContracts()
        {
            for (int i = 0; i < _state.Offices.Count; i++)
            {
                var state = _state.Offices[i];
                if (state != null && state.IsOwned && state.IsRented)
                {
                    state.IsRented = false;
                }
            }

            var retainedRentalOfficeId = ResolveRetainedRentalOfficeId();
            RelinquishOtherOfficeRentals(retainedRentalOfficeId, true);

            var activeOfficeState = GetOfficeState(_state.ActiveOfficeId);
            if (HasOfficeAccess(activeOfficeState))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(retainedRentalOfficeId))
            {
                _state.ActiveOfficeId = retainedRentalOfficeId;
                return;
            }

            var fallbackOfficeState = _state.Offices.FirstOrDefault(entry => entry != null && GetOfficeDefinition(entry.OfficeId) != null && HasOfficeAccess(entry));
            _state.ActiveOfficeId = fallbackOfficeState != null ? fallbackOfficeState.OfficeId : string.Empty;
        }

        private void NormalizeApartmentAccessContracts()
        {
            for (int i = 0; i < _state.Apartments.Count; i++)
            {
                var state = _state.Apartments[i];
                if (state == null)
                {
                    continue;
                }

                if (state.IsOwned)
                {
                    state.IsRented = false;
                    state.IsAccessSuspended = false;
                    state.OutstandingRent = 0f;
                    state.LastChargedWeekIndex = -1;
                    continue;
                }

                if (!state.IsRented)
                {
                    state.LastChargedWeekIndex = -1;
                }
            }

            var retainedRentalApartmentId = ResolveRetainedRentalApartmentId();
            RelinquishOtherApartmentRentals(retainedRentalApartmentId, true);

            var activeApartmentState = GetApartmentState(_state.ActiveApartmentId);
            if (HasApartmentAccess(activeApartmentState))
            {
                return;
            }

            var fallbackApartmentState = FindRetainedApartmentAccessState();
            _state.ActiveApartmentId = fallbackApartmentState != null ? fallbackApartmentState.InteriorId : string.Empty;
        }

        private void InitializeRentTracking(int currentInGameMinute)
        {
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            var currentDayIndex = GetDayIndex(currentInGameMinute);
            if (_state.LastCorporateOverheadWeekIndex < 0)
            {
                _state.LastCorporateOverheadWeekIndex = currentWeekIndex;
            }

            for (int i = 0; i < _state.Offices.Count; i++)
            {
                if (_state.Offices[i] != null && _state.Offices[i].LastChargedWeekIndex < 0)
                {
                    _state.Offices[i].LastChargedWeekIndex = currentWeekIndex;
                }
            }

            for (int i = 0; i < _state.Apartments.Count; i++)
            {
                var apartment = _state.Apartments[i];
                if (IsRentalOnlyApartmentAccess(apartment) && apartment.LastChargedWeekIndex < 0)
                {
                    apartment.LastChargedWeekIndex = currentWeekIndex;
                }
                else if (apartment != null && !IsRentalOnlyApartmentAccess(apartment))
                {
                    apartment.LastChargedWeekIndex = -1;
                }
            }

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var vehicle = _state.CommercialVehicles[i];
                if (vehicle != null && vehicle.IsRental && vehicle.LastChargedDayIndex < 0)
                {
                    vehicle.LastChargedDayIndex = currentDayIndex;
                }

                if (vehicle != null && !vehicle.IsRental)
                {
                    vehicle.MaintenanceCondition = NormalizeMaintenanceCondition(vehicle.MaintenanceCondition);
                    if (vehicle.LastMaintenanceWeekIndex < 0)
                    {
                        vehicle.LastMaintenanceWeekIndex = currentWeekIndex;
                    }

                    if (vehicle.LastInspectionWeekIndex < 0)
                    {
                        vehicle.LastInspectionWeekIndex = currentWeekIndex;
                    }
                }
            }
        }

        private void ProcessCorporateOverheadCharge(int currentWeekIndex, ref float balance, ICollection<string> messages)
        {
            var preview = BuildCorporateOverheadPreview(currentWeekIndex, GetTrackedCurrentInGameMinute());
            if (preview == null || preview.WeeksDue <= 0 || preview.AmountDue <= 0.01f)
            {
                return;
            }

            balance -= preview.AmountDue;
            _state.LastCorporateOverheadWeekIndex = currentWeekIndex;
            RecordFinanceExpense(
                CompanyFinanceCategory.CorporateOverhead,
                preview.AmountDue,
                preview.WeeksDue > 1
                    ? string.Format("Corporate overhead for {0} weeks", preview.WeeksDue)
                    : "Corporate overhead");

            if (messages != null)
            {
                messages.Add(string.Format(
                    "Corporate overhead billed {0}: {1} site{2}, {3} fleet vehicle{4}, {5} route crew{6}, {7} charter district{8}.",
                    ModFormatting.FormatMoney(preview.AmountDue),
                    preview.OwnedSiteCount,
                    preview.OwnedSiteCount == 1 ? string.Empty : "s",
                    preview.OwnedFleetCount,
                    preview.OwnedFleetCount == 1 ? string.Empty : "s",
                    preview.ActiveNpcCount,
                    preview.ActiveNpcCount == 1 ? string.Empty : "s",
                    preview.LicensedDistrictCount,
                    preview.LicensedDistrictCount == 1 ? string.Empty : "s"));
            }
        }

        private void ProcessFleetMaintenanceCharges(int currentWeekIndex, ref float balance, ICollection<string> messages)
        {
            var ownedVehicles = _state.CommercialVehicles
                .Where(vehicle => vehicle != null && !vehicle.IsRental)
                .ToList();
            if (ownedVehicles.Count == 0)
            {
                return;
            }

            float totalCharge = 0f;
            var servicedVehicleCount = 0;
            var overdueInspectionCount = 0;
            var atRiskVehicleCount = 0;

            for (int i = 0; i < ownedVehicles.Count; i++)
            {
                var vehicle = ownedVehicles[i];
                var elapsedWeeks = vehicle.LastMaintenanceWeekIndex < 0
                    ? 0
                    : Math.Max(0, currentWeekIndex - vehicle.LastMaintenanceWeekIndex);
                if (elapsedWeeks <= 0)
                {
                    continue;
                }

                var hasCoverage = HasVehicleMaintenanceCoverage(vehicle);
                var overdueWeeks = GetInspectionOverdueWeeks(vehicle, currentWeekIndex);
                var weeklyCharge = CalculateWeeklyMaintenanceCharge(vehicle, hasCoverage, overdueWeeks);
                var charge = weeklyCharge * elapsedWeeks;
                totalCharge += charge;
                vehicle.LifetimeMaintenanceCost += charge;
                vehicle.LastMaintenanceWeekIndex = currentWeekIndex;

                if (hasCoverage)
                {
                    servicedVehicleCount += 1;
                    vehicle.LastInspectionWeekIndex = currentWeekIndex;
                    vehicle.InspectionOverdueWeeks = 0;
                    vehicle.MaintenanceCondition = Math.Min(1f, NormalizeMaintenanceCondition(vehicle.MaintenanceCondition) + (FleetMaintenanceCoverageRecovery * elapsedWeeks));
                }
                else
                {
                    if (overdueWeeks > 0)
                    {
                        overdueInspectionCount += 1;
                    }

                    vehicle.InspectionOverdueWeeks = overdueWeeks;
                    var wear = (FleetMaintenanceWearPerWeek
                        + (vehicle.IsDeployed ? FleetMaintenanceDeployedWearBonus : 0f)
                        + (overdueWeeks > 0 ? FleetMaintenanceOverdueWearBonus : 0f)) * elapsedWeeks;
                    vehicle.MaintenanceCondition = Math.Max(
                        MinimumMaintenanceCondition,
                        NormalizeMaintenanceCondition(vehicle.MaintenanceCondition) - wear);
                }

                if (vehicle.MaintenanceCondition < 0.75f || vehicle.InspectionOverdueWeeks > 0)
                {
                    atRiskVehicleCount += 1;
                }
            }

            if (totalCharge <= 0.01f)
            {
                return;
            }

            balance -= totalCharge;
            RecordFinanceExpense(CompanyFinanceCategory.FleetMaintenance, totalCharge, "Fleet maintenance and inspection cycle");

            if (messages != null)
            {
                messages.Add(string.Format(
                    "Fleet maintenance billed {0}: {1}/{2} rigs serviced by Maintenance Bays, {3} inspection backlog, {4} at risk.",
                    ModFormatting.FormatMoney(totalCharge),
                    servicedVehicleCount,
                    ownedVehicles.Count,
                    overdueInspectionCount,
                    atRiskVehicleCount));
            }
        }

        private void ProcessCommercialVehicleDailyCharge(OwnedCommercialVehiclePersistenceEntry vehicle, int currentDayIndex, ref float balance, ICollection<string> messages)
        {
            if (vehicle == null || !vehicle.IsRental || vehicle.DailyRent <= 0.001f)
            {
                return;
            }

            var lastChargedDayIndex = vehicle.LastChargedDayIndex < 0 ? currentDayIndex : vehicle.LastChargedDayIndex;
            var elapsedDays = currentDayIndex - lastChargedDayIndex;
            if (elapsedDays <= 0)
            {
                return;
            }

            var charge = vehicle.DailyRent * elapsedDays;
            balance -= charge;
            vehicle.LastChargedDayIndex = currentDayIndex;
            RecordFinanceExpense(CompanyFinanceCategory.VehicleRent, charge, Math.Max(0, currentDayIndex * MinutesPerDay), string.Format("Commercial rental charge for {0}", vehicle.DisplayName));
            if (messages != null)
            {
                messages.Add(string.Format(
                    "Commercial rental charge: {0} billed {1} for {2} day{3}.",
                    vehicle.DisplayName,
                    ModFormatting.FormatMoney(charge),
                    elapsedDays,
                    elapsedDays == 1 ? string.Empty : "s"));
            }
        }

        private CorporateOverheadChargePreview BuildCorporateOverheadPreview(int currentWeekIndex, int currentInGameMinute)
        {
            var ownedSiteCount = Math.Max(0, _getOwnedSiteCount != null ? _getOwnedSiteCount() : 0);
            var ownedFleetCount = CountOwnedCommercialVehicles();
            var activeNpcCount = Math.Max(0, _getActiveNpcCount != null ? _getActiveNpcCount() : 0);
            var licensedDistrictCount = Math.Max(0, _getLicensedDistrictCount != null ? _getLicensedDistrictCount() : 0);
            var securedSupportSiteCount = Math.Max(0, _getSecuredSupportSiteCount != null ? _getSecuredSupportSiteCount() : 0);
            var activeCorridorCount = Math.Max(0, _getActiveCorridorCount != null ? _getActiveCorridorCount() : 0);

            var siteScale = Math.Max(0, ownedSiteCount - 2);
            var fleetScale = Math.Max(0, ownedFleetCount - 2);
            var npcScale = Math.Max(0, activeNpcCount - 1);
            var districtScale = Math.Max(0, licensedDistrictCount - 1);
            var supportScale = Math.Max(0, securedSupportSiteCount);
            var corridorScale = Math.Max(0, activeCorridorCount - 1);
            var scaleScore = siteScale + fleetScale + npcScale + districtScale + supportScale + corridorScale;
            if (scaleScore <= 0)
            {
                return new CorporateOverheadChargePreview
                {
                    DueInMinutes = Math.Max(0, ((currentWeekIndex + 1) * MinutesPerWeek) - currentInGameMinute),
                    WeeksDue = 0,
                };
            }

            var weeklyAmount = CorporateOverheadBaseCharge
                + (siteScale * CorporateOverheadPerOwnedSite)
                + (fleetScale * CorporateOverheadPerOwnedFleetVehicle)
                + (npcScale * CorporateOverheadPerNpcCrew)
                + (districtScale * CorporateOverheadPerLicensedDistrict)
                + (supportScale * CorporateOverheadPerSupportSite)
                + (corridorScale * CorporateOverheadPerCorridor)
                + (scaleScore * CorporateOverheadScaleCharge);
            var weeksDue = _state.LastCorporateOverheadWeekIndex < 0
                ? 0
                : Math.Max(0, currentWeekIndex - _state.LastCorporateOverheadWeekIndex);
            var dueInMinutes = weeksDue > 0
                ? 0
                : Math.Max(0, ((currentWeekIndex + 1) * MinutesPerWeek) - currentInGameMinute);

            return new CorporateOverheadChargePreview
            {
                WeeklyAmount = weeklyAmount,
                AmountDue = weeklyAmount * (weeksDue > 0 ? weeksDue : 1),
                DueInMinutes = dueInMinutes,
                WeeksDue = weeksDue,
                ScaleScore = scaleScore,
                OwnedSiteCount = ownedSiteCount,
                OwnedFleetCount = ownedFleetCount,
                ActiveNpcCount = activeNpcCount,
                LicensedDistrictCount = licensedDistrictCount,
                SecuredSupportSiteCount = securedSupportSiteCount,
                ActiveCorridorCount = activeCorridorCount,
            };
        }

        private FleetMaintenanceChargePreview BuildFleetMaintenancePreview(int currentWeekIndex, int currentInGameMinute)
        {
            var ownedVehicles = _state.CommercialVehicles
                .Where(vehicle => vehicle != null && !vehicle.IsRental)
                .ToList();
            if (ownedVehicles.Count == 0)
            {
                return new FleetMaintenanceChargePreview
                {
                    DueInMinutes = Math.Max(0, ((currentWeekIndex + 1) * MinutesPerWeek) - currentInGameMinute),
                };
            }

            float weeklyAmount = 0f;
            float amountDueNow = 0f;
            var coveredVehicleCount = 0;
            var overdueInspectionCount = 0;
            var atRiskVehicleCount = 0;
            float totalConditionPercent = 0f;

            for (int i = 0; i < ownedVehicles.Count; i++)
            {
                var vehicle = ownedVehicles[i];
                var normalizedCondition = NormalizeMaintenanceCondition(vehicle.MaintenanceCondition);
                var hasCoverage = HasVehicleMaintenanceCoverage(vehicle);
                var overdueWeeks = GetInspectionOverdueWeeks(vehicle, currentWeekIndex);
                var charge = CalculateWeeklyMaintenanceCharge(vehicle, hasCoverage, overdueWeeks);
                var elapsedWeeks = vehicle.LastMaintenanceWeekIndex < 0
                    ? 0
                    : Math.Max(0, currentWeekIndex - vehicle.LastMaintenanceWeekIndex);

                weeklyAmount += charge;
                if (elapsedWeeks > 0)
                {
                    amountDueNow += charge * elapsedWeeks;
                }

                if (hasCoverage)
                {
                    coveredVehicleCount += 1;
                }

                if (overdueWeeks > 0)
                {
                    overdueInspectionCount += 1;
                }

                if (normalizedCondition < 0.75f || overdueWeeks > 0)
                {
                    atRiskVehicleCount += 1;
                }

                totalConditionPercent += normalizedCondition * 100f;
            }

            return new FleetMaintenanceChargePreview
            {
                WeeklyAmount = weeklyAmount,
                AmountDue = amountDueNow > 0.01f ? amountDueNow : weeklyAmount,
                DueInMinutes = amountDueNow > 0.01f ? 0 : Math.Max(0, ((currentWeekIndex + 1) * MinutesPerWeek) - currentInGameMinute),
                VehicleCount = ownedVehicles.Count,
                CoveredVehicleCount = coveredVehicleCount,
                OverdueInspectionCount = overdueInspectionCount,
                AtRiskVehicleCount = atRiskVehicleCount,
                AverageConditionPercent = ownedVehicles.Count > 0 ? totalConditionPercent / ownedVehicles.Count : 0f,
            };
        }

        private int CountOwnedCommercialVehicles()
        {
            return _state.CommercialVehicles.Count(vehicle => vehicle != null && !vehicle.IsRental);
        }

        private bool HasVehicleMaintenanceCoverage(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            return vehicle != null
                && !vehicle.IsRental
                && vehicle.InActiveGarage
                && !vehicle.IsDeployed
                && !string.IsNullOrWhiteSpace(vehicle.AssignedOfficeId)
                && HasOfficeObjectFunction(vehicle.AssignedOfficeId, OfficeObjectFunction.Repair);
        }

        private static int GetInspectionOverdueWeeks(OwnedCommercialVehiclePersistenceEntry vehicle, int currentWeekIndex)
        {
            if (vehicle == null)
            {
                return 0;
            }

            var lastInspectionWeekIndex = vehicle.LastInspectionWeekIndex < 0 ? currentWeekIndex : vehicle.LastInspectionWeekIndex;
            return Math.Max(0, currentWeekIndex - lastInspectionWeekIndex - (FleetInspectionIntervalWeeks - 1));
        }

        private static float CalculateWeeklyMaintenanceCharge(OwnedCommercialVehiclePersistenceEntry vehicle, bool hasCoverage, int overdueInspectionWeeks)
        {
            if (vehicle == null)
            {
                return 0f;
            }

            var baseCharge = Math.Max(
                FleetMaintenanceMinimumWeeklyCharge,
                (Math.Max(0f, vehicle.PurchasePrice) * FleetMaintenancePurchaseRate) + (Math.Max(0f, vehicle.CapacityTons) * FleetMaintenanceCapacityRate));
            if (hasCoverage)
            {
                baseCharge *= FleetMaintenanceCoverageDiscount;
            }

            if (overdueInspectionWeeks > 0)
            {
                baseCharge += FleetMaintenanceInspectionSurcharge * overdueInspectionWeeks;
            }

            return Math.Max(0f, baseCharge);
        }

        private static float NormalizeMaintenanceCondition(float value)
        {
            if (value <= 0f)
            {
                return 1f;
            }

            return Math.Max(MinimumMaintenanceCondition, Math.Min(1f, value));
        }

        private int GetTrackedCurrentInGameMinute()
        {
            return _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
        }

        private static void ApplyCommercialVehicleMaintenanceState(Vehicle vehicle, float maintenanceCondition)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            var normalizedCondition = NormalizeMaintenanceCondition(maintenanceCondition);
            var targetEngineHealth = 450f + (normalizedCondition * 550f);
            var targetBodyHealth = 500f + (normalizedCondition * 500f);
            vehicle.EngineHealth = Math.Min(vehicle.EngineHealth, targetEngineHealth);
            vehicle.BodyHealth = Math.Min(vehicle.BodyHealth, targetBodyHealth);
        }

        private static float CalculateCommercialVehicleSaleRefund(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null || vehicle.PurchasePrice <= 0.01f)
            {
                return 0f;
            }

            var maintenanceRatio = 0.75f + (NormalizeMaintenanceCondition(vehicle.MaintenanceCondition) * 0.25f);
            var depreciationPenalty = Math.Min(0.20f, Math.Max(0f, vehicle.LifetimeMaintenanceCost) / Math.Max(1f, vehicle.PurchasePrice) * 0.18f);
            return Math.Max(0f, vehicle.PurchasePrice * CommercialVehicleSaleRefundRatio * maintenanceRatio * (1f - depreciationPenalty));
        }

        private OfficeOwnershipPersistenceEntry GetOrCreateOfficeState(string officeId)
        {
            var state = GetOfficeState(officeId);
            if (state != null)
            {
                return state;
            }

            state = new OfficeOwnershipPersistenceEntry
            {
                OfficeId = officeId,
                LastChargedWeekIndex = -1,
            };
            _state.Offices.Add(state);
            return state;
        }

        private OfficeOwnershipPersistenceEntry GetTransferableOfficeRental(string excludedOfficeId)
        {
            var activeRental = GetOfficeState(_state.ActiveOfficeId);
            if (IsRentalOnlyOfficeAccess(activeRental)
                && !string.Equals(activeRental.OfficeId, excludedOfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return activeRental;
            }

            return _state.Offices.FirstOrDefault(entry => IsRentalOnlyOfficeAccess(entry)
                && !string.Equals(entry.OfficeId, excludedOfficeId, StringComparison.OrdinalIgnoreCase));
        }

        private OfficeOwnershipPersistenceEntry FindOperationalFallbackOfficeState(string excludedOfficeId)
        {
            return _state.Offices
                .Where(entry => entry != null
                    && !string.Equals(entry.OfficeId, excludedOfficeId, StringComparison.OrdinalIgnoreCase)
                    && GetOfficeDefinition(entry.OfficeId) != null
                    && HasOperationalOfficeAccess(entry))
                .OrderByDescending(entry => entry.IsOwned)
                .ThenBy(entry => GetOfficeDisplayName(entry.OfficeId), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private List<string> RelinquishOtherOfficeRentals(string retainedOfficeId, bool clearOutstandingBalance)
        {
            var relinquishedOfficeNames = new List<string>();
            for (int i = 0; i < _state.Offices.Count; i++)
            {
                var state = _state.Offices[i];
                if (!IsRentalOnlyOfficeAccess(state)
                    || string.Equals(state.OfficeId, retainedOfficeId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                relinquishedOfficeNames.Add(GetOfficeDisplayName(state.OfficeId));
                ReleaseOfficeRental(state, clearOutstandingBalance);
            }

            return relinquishedOfficeNames;
        }

        private string ResolveRetainedRentalOfficeId()
        {
            var activeOfficeState = GetOfficeState(_state.ActiveOfficeId);
            if (IsRentalOnlyOfficeAccess(activeOfficeState))
            {
                return activeOfficeState.OfficeId;
            }

            var fallbackRentalState = _state.Offices.FirstOrDefault(entry => entry != null && GetOfficeDefinition(entry.OfficeId) != null && IsRentalOnlyOfficeAccess(entry));
            return fallbackRentalState != null ? fallbackRentalState.OfficeId : string.Empty;
        }

        private string GetOfficeDisplayName(string officeId)
        {
            var definition = GetOfficeDefinition(officeId);
            return definition != null ? definition.DisplayName : (officeId ?? string.Empty);
        }

        private static bool HasOfficeAccess(OfficeOwnershipPersistenceEntry state)
        {
            return state != null && (state.IsOwned || state.IsRented);
        }

        private static bool IsRentalOnlyOfficeAccess(OfficeOwnershipPersistenceEntry state)
        {
            return state != null && state.IsRented && !state.IsOwned;
        }

        private static bool HasOperationalOfficeAccess(OfficeOwnershipPersistenceEntry state)
        {
            return HasOfficeAccess(state) && !state.IsAccessSuspended && state.OutstandingRent <= 0.01f;
        }

        private static bool HasApartmentAccess(ApartmentOwnershipPersistenceEntry state)
        {
            return state != null && (state.IsOwned || state.IsRented);
        }

        private static bool IsRentalOnlyApartmentAccess(ApartmentOwnershipPersistenceEntry state)
        {
            return state != null && state.IsRented && !state.IsOwned;
        }

        private static bool HasOperationalApartmentAccess(ApartmentOwnershipPersistenceEntry state)
        {
            return HasApartmentAccess(state) && !state.IsAccessSuspended && state.OutstandingRent <= 0.01f;
        }

        private static void ReleaseOfficeRental(OfficeOwnershipPersistenceEntry state, bool clearOutstandingBalance)
        {
            if (!IsRentalOnlyOfficeAccess(state))
            {
                return;
            }

            state.IsRented = false;
            if (clearOutstandingBalance)
            {
                state.OutstandingRent = 0f;
                state.IsAccessSuspended = false;
            }
            else
            {
                state.IsAccessSuspended = state.OutstandingRent > 0.01f;
            }

            state.LastChargedWeekIndex = -1;
        }

        private ApartmentOwnershipPersistenceEntry GetTransferableApartmentRental(string excludedInteriorId)
        {
            var activeRental = GetApartmentState(_state.ActiveApartmentId);
            if (IsRentalOnlyApartmentAccess(activeRental)
                && !string.Equals(activeRental.InteriorId, excludedInteriorId, StringComparison.OrdinalIgnoreCase))
            {
                return activeRental;
            }

            return _state.Apartments.FirstOrDefault(entry => IsRentalOnlyApartmentAccess(entry)
                && !string.Equals(entry.InteriorId, excludedInteriorId, StringComparison.OrdinalIgnoreCase));
        }

        private ApartmentOwnershipPersistenceEntry FindOperationalFallbackApartmentState(string excludedInteriorId)
        {
            return _state.Apartments
                .Where(entry => entry != null
                    && !string.Equals(entry.InteriorId, excludedInteriorId, StringComparison.OrdinalIgnoreCase)
                    && GetInteriorDefinition(entry.InteriorId) != null
                    && HasOperationalApartmentAccess(entry))
                .OrderByDescending(entry => entry.IsOwned)
                .ThenBy(entry => GetApartmentDisplayName(entry.InteriorId), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private string ResolveRetainedRentalApartmentId()
        {
            var activeApartmentState = GetApartmentState(_state.ActiveApartmentId);
            if (IsRentalOnlyApartmentAccess(activeApartmentState))
            {
                return activeApartmentState.InteriorId;
            }

            var fallbackRentalState = _state.Apartments
                .Where(entry => entry != null && GetInteriorDefinition(entry.InteriorId) != null && IsRentalOnlyApartmentAccess(entry))
                .OrderBy(entry => entry.InteriorId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return fallbackRentalState != null ? fallbackRentalState.InteriorId : string.Empty;
        }

        private List<string> RelinquishOtherApartmentRentals(string retainedInteriorId, bool clearOutstandingBalance)
        {
            var relinquishedApartmentNames = new List<string>();
            for (int i = 0; i < _state.Apartments.Count; i++)
            {
                var state = _state.Apartments[i];
                if (!IsRentalOnlyApartmentAccess(state)
                    || string.Equals(state.InteriorId, retainedInteriorId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                relinquishedApartmentNames.Add(GetApartmentDisplayName(state.InteriorId));
                ReleaseApartmentRental(state, clearOutstandingBalance);
            }

            return relinquishedApartmentNames;
        }

        private ApartmentOwnershipPersistenceEntry FindRetainedApartmentAccessState()
        {
            return _state.Apartments
                .Where(entry => entry != null && GetInteriorDefinition(entry.InteriorId) != null && HasApartmentAccess(entry))
                .OrderByDescending(entry => entry.IsOwned)
                .ThenBy(entry => entry.InteriorId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private string GetApartmentDisplayName(string interiorId)
        {
            var definition = GetInteriorDefinition(interiorId);
            return definition != null ? definition.DisplayName : (interiorId ?? string.Empty);
        }

        private static void ReleaseApartmentRental(ApartmentOwnershipPersistenceEntry state, bool clearOutstandingBalance)
        {
            if (!IsRentalOnlyApartmentAccess(state))
            {
                return;
            }

            state.IsRented = false;
            if (clearOutstandingBalance)
            {
                state.OutstandingRent = 0f;
                state.IsAccessSuspended = false;
            }
            else
            {
                state.IsAccessSuspended = state.OutstandingRent > 0.01f;
            }

            state.LastChargedWeekIndex = -1;
        }

        private static void ReleaseApartmentOwnership(ApartmentOwnershipPersistenceEntry state)
        {
            if (state == null || !state.IsOwned)
            {
                return;
            }

            state.IsOwned = false;
            state.IsRented = false;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = -1;
        }

        private ApartmentOwnershipPersistenceEntry GetOrCreateApartmentState(string interiorId)
        {
            var state = GetApartmentState(interiorId);
            if (state != null)
            {
                return state;
            }

            state = new ApartmentOwnershipPersistenceEntry
            {
                InteriorId = interiorId,
                LastChargedWeekIndex = -1,
            };
            _state.Apartments.Add(state);
            return state;
        }

        private OfficeObjectPersistenceEntry GetOfficeObject(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            return _state.OfficeObjects.FirstOrDefault(entry => entry != null && string.Equals(entry.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
        }

        private OwnedCommercialVehiclePersistenceEntry GetCommercialVehicle(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return null;
            }

            return _state.CommercialVehicles.FirstOrDefault(entry => entry != null && string.Equals(entry.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        }

        private OwnedPersonalVehiclePersistenceEntry GetPersonalVehicle(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return null;
            }

            return _state.PersonalVehicles.FirstOrDefault(entry => entry != null && string.Equals(entry.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        }

        private void ClearRuntimeState()
        {
            var commercialIds = _commercialRuntime.Keys.ToList();
            for (int i = 0; i < commercialIds.Count; i++)
            {
                CommercialVehicleRuntimeState runtime;
                if (!_commercialRuntime.TryGetValue(commercialIds[i], out runtime))
                {
                    continue;
                }

                var truck = Entity.FromHandle(runtime.TruckHandle) as Vehicle;
                var cargoVehicle = Entity.FromHandle(runtime.CargoHandle) as Vehicle;
                if (cargoVehicle != null && cargoVehicle.Exists())
                {
                    cargoVehicle.Delete();
                }

                if (truck != null && truck.Exists() && (cargoVehicle == null || truck.Handle != cargoVehicle.Handle))
                {
                    truck.Delete();
                }
            }

            var personalIds = _personalRuntime.Keys.ToList();
            for (int i = 0; i < personalIds.Count; i++)
            {
                PersonalVehicleRuntimeState runtime;
                if (!_personalRuntime.TryGetValue(personalIds[i], out runtime))
                {
                    continue;
                }

                var vehicle = Entity.FromHandle(runtime.VehicleHandle) as Vehicle;
                if (vehicle != null && vehicle.Exists())
                {
                    vehicle.Delete();
                }
            }

            _commercialRuntime.Clear();
            _personalRuntime.Clear();
        }

        private void PruneInvalidOfficeObjects()
        {
            for (int i = _state.OfficeObjects.Count - 1; i >= 0; i--)
            {
                var entry = _state.OfficeObjects[i];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.OfficeId)
                    || GetOfficeDefinition(entry.OfficeId) == null
                    || GetOfficeObjectDefinition(entry.DefinitionId) == null)
                {
                    _state.OfficeObjects.RemoveAt(i);
                }
            }
        }

        private static PropertyOwnershipPersistenceSnapshot CloneSnapshot(PropertyOwnershipPersistenceSnapshot source)
        {
            var clone = new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = source != null ? source.ActiveOfficeId : string.Empty,
                ActiveApartmentId = source != null ? source.ActiveApartmentId : string.Empty,
                LastSuccessfulApartmentSleepMinute = source != null ? source.LastSuccessfulApartmentSleepMinute : -1,
            };

            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Offices.Count; i++)
            {
                var entry = source.Offices[i];
                if (entry == null)
                {
                    continue;
                }

                clone.Offices.Add(new OfficeOwnershipPersistenceEntry
                {
                    OfficeId = entry.OfficeId,
                    IsOwned = entry.IsOwned,
                    IsRented = entry.IsRented,
                    IsAccessSuspended = entry.IsAccessSuspended,
                    OutstandingRent = entry.OutstandingRent,
                    LastChargedWeekIndex = entry.LastChargedWeekIndex,
                });
            }

            for (int i = 0; i < source.OfficeObjects.Count; i++)
            {
                var entry = source.OfficeObjects[i];
                if (entry == null)
                {
                    continue;
                }

                clone.OfficeObjects.Add(new OfficeObjectPersistenceEntry
                {
                    InstanceId = entry.InstanceId,
                    OfficeId = entry.OfficeId,
                    DefinitionId = entry.DefinitionId,
                    IsPlaced = entry.IsPlaced,
                    Position = entry.Position,
                    Rotation = entry.Rotation,
                    StoredResourceAmount = entry.StoredResourceAmount,
                });
            }

            for (int i = 0; i < source.Apartments.Count; i++)
            {
                var entry = source.Apartments[i];
                if (entry == null)
                {
                    continue;
                }

                clone.Apartments.Add(new ApartmentOwnershipPersistenceEntry
                {
                    InteriorId = entry.InteriorId,
                    IsOwned = entry.IsOwned,
                    IsRented = entry.IsRented,
                    IsAccessSuspended = entry.IsAccessSuspended,
                    OutstandingRent = entry.OutstandingRent,
                    LastChargedWeekIndex = entry.LastChargedWeekIndex,
                });
            }

            for (int i = 0; i < source.CommercialVehicles.Count; i++)
            {
                var entry = source.CommercialVehicles[i];
                if (entry == null)
                {
                    continue;
                }

                clone.CommercialVehicles.Add(new OwnedCommercialVehiclePersistenceEntry
                {
                    AssetId = entry.AssetId,
                    DisplayName = entry.DisplayName,
                    PoweredModelName = entry.PoweredModelName,
                    CargoModelName = entry.CargoModelName,
                    HasSeparateCargoVehicle = entry.HasSeparateCargoVehicle,
                    PurchasePrice = entry.PurchasePrice,
                    AssignedOfficeId = entry.AssignedOfficeId,
                    IsRental = entry.IsRental,
                    DailyRent = entry.DailyRent,
                    LastChargedDayIndex = entry.LastChargedDayIndex,
                    InActiveGarage = entry.InActiveGarage,
                    IsDeployed = entry.IsDeployed,
                    PoweredPosition = entry.PoweredPosition,
                    PoweredHeading = entry.PoweredHeading,
                    CargoType = entry.CargoType,
                    CapacityTons = entry.CapacityTons,
                    Commodity = entry.Commodity,
                    WeightTons = entry.WeightTons,
                    CargoCondition = entry.CargoCondition,
                    TotalLostTons = entry.TotalLostTons,
                    SourceIndustryId = entry.SourceIndustryId,
                    SourceDistrictName = entry.SourceDistrictName,
                    CurrentFuelLiters = entry.CurrentFuelLiters,
                    MaintenanceCondition = entry.MaintenanceCondition,
                    LastMaintenanceWeekIndex = entry.LastMaintenanceWeekIndex,
                    LastInspectionWeekIndex = entry.LastInspectionWeekIndex,
                    InspectionOverdueWeeks = entry.InspectionOverdueWeeks,
                    LifetimeMaintenanceCost = entry.LifetimeMaintenanceCost,
                    PoweredAppearance = CloneVehicleAppearance(entry.PoweredAppearance),
                    CargoAppearance = CloneVehicleAppearance(entry.CargoAppearance),
                });
            }

            for (int i = 0; i < source.PersonalVehicles.Count; i++)
            {
                var entry = source.PersonalVehicles[i];
                if (entry == null)
                {
                    continue;
                }

                clone.PersonalVehicles.Add(new OwnedPersonalVehiclePersistenceEntry
                {
                    AssetId = entry.AssetId,
                    DisplayName = entry.DisplayName,
                    ModelName = entry.ModelName,
                    Category = entry.Category,
                    PurchasePrice = entry.PurchasePrice,
                    AssignedApartmentId = entry.AssignedApartmentId,
                    IsDeployed = entry.IsDeployed,
                    Position = entry.Position,
                    Heading = entry.Heading,
                    Appearance = CloneVehicleAppearance(entry.Appearance),
                });
            }

            return clone;
        }

        private static VehicleAppearancePersistenceSnapshot CaptureVehicleAppearance(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            var snapshot = new VehicleAppearancePersistenceSnapshot();
            var mods = vehicle.Mods;
            if (mods == null)
            {
                return null;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle.Handle, 0);
            }
            catch
            {
            }

            snapshot.ColorCombination = mods.ColorCombination;
            snapshot.LicensePlate = mods.LicensePlate;
            snapshot.LicensePlateStyle = mods.LicensePlateStyle;
            snapshot.WindowTint = mods.WindowTint;
            snapshot.Livery = mods.Livery;
            snapshot.WheelType = mods.WheelType;
            snapshot.PrimaryColor = mods.PrimaryColor;
            snapshot.SecondaryColor = mods.SecondaryColor;
            snapshot.PearlescentColor = mods.PearlescentColor;
            snapshot.RimColor = mods.RimColor;
            snapshot.DashboardColor = mods.DashboardColor;
            snapshot.TrimColor = mods.TrimColor;
            snapshot.CustomPrimaryColor = mods.IsPrimaryColorCustom ? (Color?)mods.CustomPrimaryColor : null;
            snapshot.CustomSecondaryColor = mods.IsSecondaryColorCustom ? (Color?)mods.CustomSecondaryColor : null;
            snapshot.NeonLightsColor = mods.NeonLightsColor;
            snapshot.TireSmokeColor = mods.TireSmokeColor;

            foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
            {
                try
                {
                    var mod = mods[modType];
                    if (mod != null && (mod.Index >= 0 || mod.Variation))
                    {
                        snapshot.Mods.Add(new VehicleModPersistenceEntry
                        {
                            Type = modType,
                            Index = mod.Index,
                            Variation = mod.Variation,
                        });
                    }
                }
                catch
                {
                }
            }

            foreach (VehicleToggleModType toggleModType in Enum.GetValues(typeof(VehicleToggleModType)))
            {
                try
                {
                    var toggleMod = mods[toggleModType];
                    if (toggleMod != null && toggleMod.IsInstalled)
                    {
                        snapshot.ToggleMods.Add(new VehicleToggleModPersistenceEntry
                        {
                            Type = toggleModType,
                            IsInstalled = true,
                        });
                    }
                }
                catch
                {
                }
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static void ApplyVehicleAppearance(Vehicle vehicle, VehicleAppearancePersistenceSnapshot snapshot)
        {
            if (vehicle == null || !vehicle.Exists() || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            var mods = vehicle.Mods;
            if (mods == null)
            {
                return;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle.Handle, 0);
            }
            catch
            {
            }

            try
            {
                mods.ColorCombination = snapshot.ColorCombination.HasValue
                    ? snapshot.ColorCombination.Value
                    : mods.ColorCombination;
            }
            catch
            {
            }

            try
            {
                mods.LicensePlate = snapshot.LicensePlate ?? string.Empty;
            }
            catch
            {
            }

            try
            {
                if (snapshot.LicensePlateStyle.HasValue)
                {
                    mods.LicensePlateStyle = snapshot.LicensePlateStyle.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.WindowTint.HasValue)
                {
                    mods.WindowTint = snapshot.WindowTint.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.WheelType.HasValue)
                {
                    mods.WheelType = snapshot.WheelType.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.PrimaryColor.HasValue)
                {
                    mods.PrimaryColor = snapshot.PrimaryColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.SecondaryColor.HasValue)
                {
                    mods.SecondaryColor = snapshot.SecondaryColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.PearlescentColor.HasValue)
                {
                    mods.PearlescentColor = snapshot.PearlescentColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.RimColor.HasValue)
                {
                    mods.RimColor = snapshot.RimColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.DashboardColor.HasValue)
                {
                    mods.DashboardColor = snapshot.DashboardColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.TrimColor.HasValue)
                {
                    mods.TrimColor = snapshot.TrimColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.Livery.HasValue)
                {
                    mods.Livery = snapshot.Livery.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.CustomPrimaryColor.HasValue)
                {
                    mods.CustomPrimaryColor = snapshot.CustomPrimaryColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.CustomSecondaryColor.HasValue)
                {
                    mods.CustomSecondaryColor = snapshot.CustomSecondaryColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.NeonLightsColor.HasValue)
                {
                    mods.NeonLightsColor = snapshot.NeonLightsColor.Value;
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.TireSmokeColor.HasValue)
                {
                    mods.TireSmokeColor = snapshot.TireSmokeColor.Value;
                }
            }
            catch
            {
            }

            if (snapshot.Mods != null)
            {
                for (int i = 0; i < snapshot.Mods.Count; i++)
                {
                    var persistedMod = snapshot.Mods[i];
                    if (persistedMod == null)
                    {
                        continue;
                    }

                    try
                    {
                        var mod = mods[persistedMod.Type];
                        if (mod == null)
                        {
                            continue;
                        }

                        mod.Index = persistedMod.Index;
                        mod.Variation = persistedMod.Variation;
                    }
                    catch
                    {
                    }
                }
            }

            if (snapshot.ToggleMods != null)
            {
                for (int i = 0; i < snapshot.ToggleMods.Count; i++)
                {
                    var persistedToggle = snapshot.ToggleMods[i];
                    if (persistedToggle == null)
                    {
                        continue;
                    }

                    try
                    {
                        var toggleMod = mods[persistedToggle.Type];
                        if (toggleMod == null)
                        {
                            continue;
                        }

                        toggleMod.IsInstalled = persistedToggle.IsInstalled;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static VehicleAppearancePersistenceSnapshot CloneVehicleAppearance(VehicleAppearancePersistenceSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new VehicleAppearancePersistenceSnapshot
            {
                ColorCombination = source.ColorCombination,
                LicensePlate = source.LicensePlate,
                LicensePlateStyle = source.LicensePlateStyle,
                WindowTint = source.WindowTint,
                Livery = source.Livery,
                WheelType = source.WheelType,
                PrimaryColor = source.PrimaryColor,
                SecondaryColor = source.SecondaryColor,
                PearlescentColor = source.PearlescentColor,
                RimColor = source.RimColor,
                DashboardColor = source.DashboardColor,
                TrimColor = source.TrimColor,
                CustomPrimaryColor = source.CustomPrimaryColor,
                CustomSecondaryColor = source.CustomSecondaryColor,
                NeonLightsColor = source.NeonLightsColor,
                TireSmokeColor = source.TireSmokeColor,
            };

            if (source.Mods != null)
            {
                for (int i = 0; i < source.Mods.Count; i++)
                {
                    var mod = source.Mods[i];
                    if (mod == null)
                    {
                        continue;
                    }

                    clone.Mods.Add(new VehicleModPersistenceEntry
                    {
                        Type = mod.Type,
                        Index = mod.Index,
                        Variation = mod.Variation,
                    });
                }
            }

            if (source.ToggleMods != null)
            {
                for (int i = 0; i < source.ToggleMods.Count; i++)
                {
                    var toggleMod = source.ToggleMods[i];
                    if (toggleMod == null)
                    {
                        continue;
                    }

                    clone.ToggleMods.Add(new VehicleToggleModPersistenceEntry
                    {
                        Type = toggleMod.Type,
                        IsInstalled = toggleMod.IsInstalled,
                    });
                }
            }

            return clone.HasData ? clone : null;
        }

        private static int GetWeekIndex(int currentInGameMinute)
        {
            return Math.Max(0, currentInGameMinute) / MinutesPerWeek;
        }

        private static int GetDayIndex(int currentInGameMinute)
        {
            return Math.Max(0, currentInGameMinute) / MinutesPerDay;
        }

        private void RecordFinanceExpense(CompanyFinanceCategory category, float amount, int inGameMinute, string description)
        {
            if (_financeTracker == null || amount <= 0f)
            {
                return;
            }

            _financeTracker.RecordExpense(category, amount, inGameMinute, description);
        }

        private void RecordFinanceExpense(CompanyFinanceCategory category, float amount, string description)
        {
            RecordFinanceExpense(category, amount, ResolveFinanceMinute(), description);
        }

        private void RecordFinanceIncome(CompanyFinanceCategory category, float amount, string description)
        {
            if (_financeTracker == null || amount <= 0f)
            {
                return;
            }

            _financeTracker.RecordIncome(category, amount, ResolveFinanceMinute(), description);
        }

        private int ResolveFinanceMinute()
        {
            return _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
        }

        private static string BuildCommercialDisplayName(string poweredDisplayName, string cargoDisplayName, bool hasSeparateCargoVehicle)
        {
            if (!hasSeparateCargoVehicle)
            {
                return string.IsNullOrWhiteSpace(cargoDisplayName) ? (poweredDisplayName ?? string.Empty) : cargoDisplayName;
            }

            return string.Format("{0} + {1}", poweredDisplayName ?? string.Empty, cargoDisplayName ?? string.Empty).Trim();
        }

        private static bool TryNormalizeCommercialSelection(ref VehicleDefinition cargoDefinition, ref VehicleDefinition tractorDefinition, string modeLabel, out string message)
        {
            message = string.Empty;

            if (cargoDefinition == null && tractorDefinition == null)
            {
                message = "Select a truck and/or trailer first.";
                return false;
            }

            if (tractorDefinition != null && !tractorDefinition.IsTractor)
            {
                message = "Selected truck is not a valid tractor unit.";
                return false;
            }

            if (cargoDefinition == null)
            {
                return true;
            }

            if (!cargoDefinition.IsTrailer)
            {
                tractorDefinition = null;
                return true;
            }

            if (tractorDefinition == null)
            {
                return true;
            }

            if (!tractorDefinition.IsTractor)
            {
                message = string.Format("Select a truck tractor for the trailer {0}.", modeLabel);
                return false;
            }

            return true;
        }

        private sealed class CommercialVehicleRuntimeState
        {
            public int TruckHandle { get; set; }

            public int CargoHandle { get; set; }
        }

        private sealed class PersonalVehicleRuntimeState
        {
            public int VehicleHandle { get; set; }
        }
    }
}
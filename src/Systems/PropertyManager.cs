using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA;
using GTA.Math;
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

    public sealed class PropertyManager
    {
        private const int MinutesPerWeek = 7 * 24 * 60;
        private const int MinutesPerDay = 24 * 60;
        private const float CommercialVehicleSaleRefundRatio = 0.5f;
        private const int CommercialRentalRefundDays = 2;

        private readonly List<OfficeDefinition> _officeDefinitions;
        private readonly Dictionary<string, OfficeDefinition> _officeDefinitionsById;
        private readonly List<InteriorDefinition> _interiorDefinitions;
        private readonly Dictionary<string, InteriorDefinition> _interiorDefinitionsById;
        private readonly List<DealershipVehicleDefinition> _personalVehicleDefinitions;
        private readonly Dictionary<string, DealershipVehicleDefinition> _personalVehicleDefinitionsById;
        private readonly Dictionary<string, CommercialVehicleRuntimeState> _commercialRuntime;
        private readonly Dictionary<string, PersonalVehicleRuntimeState> _personalRuntime;

        private PropertyOwnershipPersistenceSnapshot _state;

        public PropertyManager(ModConfig config)
        {
            _officeDefinitions = config != null && config.OfficeDefinitions != null
                ? config.OfficeDefinitions.OrderBy(x => x != null ? x.OfficePrice : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<OfficeDefinition>();
            _officeDefinitionsById = _officeDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.OfficeId))
                .ToDictionary(x => x.OfficeId, x => x, StringComparer.OrdinalIgnoreCase);
            _interiorDefinitions = config != null && config.InteriorDefinitions != null
                ? config.InteriorDefinitions.OrderBy(x => x != null ? x.InteriorPrice : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<InteriorDefinition>();
            _interiorDefinitionsById = _interiorDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.InteriorId))
                .ToDictionary(x => x.InteriorId, x => x, StringComparer.OrdinalIgnoreCase);
            _personalVehicleDefinitions = config != null && config.PersonalVehicleDefinitions != null
                ? config.PersonalVehicleDefinitions.OrderBy(x => x != null ? x.Price : 0f).ThenBy(x => x != null ? x.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<DealershipVehicleDefinition>();
            _personalVehicleDefinitionsById = _personalVehicleDefinitions
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.VehicleId))
                .ToDictionary(x => x.VehicleId, x => x, StringComparer.OrdinalIgnoreCase);
            _commercialRuntime = new Dictionary<string, CommercialVehicleRuntimeState>(StringComparer.OrdinalIgnoreCase);
            _personalRuntime = new Dictionary<string, PersonalVehicleRuntimeState>(StringComparer.OrdinalIgnoreCase);
            _state = new PropertyOwnershipPersistenceSnapshot();
        }

        public IReadOnlyList<OfficeDefinition> Offices
        {
            get { return _officeDefinitions; }
        }

        public IReadOnlyList<InteriorDefinition> Interiors
        {
            get { return _interiorDefinitions; }
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

        public OfficeDefinition ActiveOffice
        {
            get { return GetOfficeDefinition(_state.ActiveOfficeId); }
        }

        public InteriorDefinition ActiveApartment
        {
            get { return GetInteriorDefinition(_state.ActiveApartmentId); }
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

            return messages;
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
            if (apartment == null || apartmentState == null || !apartmentState.IsOwned)
            {
                reason = "Purchase and activate an apartment to access personal storage.";
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

            var upfrontRent = Math.Max(0f, definition.WeeklyOfficeRent);
            if (balance < upfrontRent)
            {
                message = string.Format("Need {0} to rent {1}.", ModFormatting.FormatMoney(upfrontRent), definition.DisplayName);
                return false;
            }

            balance -= upfrontRent;
            state.IsRented = true;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = GetWeekIndex(currentInGameMinute);

            if (string.IsNullOrWhiteSpace(_state.ActiveOfficeId))
            {
                _state.ActiveOfficeId = definition.OfficeId;
            }

            NormalizeCommercialGarageAssignments();
            message = string.Format("Rented {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(upfrontRent));
            return true;
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

            if (balance < definition.OfficePrice)
            {
                message = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.OfficePrice), definition.DisplayName);
                return false;
            }

            balance -= definition.OfficePrice;
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

            _state.ActiveOfficeId = definition.OfficeId;
            TransferCommercialVehiclesToActiveOffice();
            message = string.Format("Activated {0}. Commercial garage capacity is now {1}.", definition.DisplayName, Math.Max(0, definition.MaxCommercialVehicles));
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

            balance -= state.OutstandingRent;
            state.OutstandingRent = 0f;
            state.IsAccessSuspended = false;
            message = string.Format("Settled office rent for {0}.", definition.DisplayName);
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

            if (balance < definition.InteriorPrice)
            {
                message = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.InteriorPrice), definition.DisplayName);
                return false;
            }

            balance -= definition.InteriorPrice;
            state.IsOwned = true;
            state.IsAccessSuspended = false;
            state.OutstandingRent = 0f;
            state.LastChargedWeekIndex = GetWeekIndex(currentInGameMinute);
            if (string.IsNullOrWhiteSpace(_state.ActiveApartmentId))
            {
                _state.ActiveApartmentId = definition.InteriorId;
            }

            ReassignPersonalVehiclesToActiveApartment();
            message = string.Format("Purchased {0} for {1}.", definition.DisplayName, ModFormatting.FormatMoney(definition.InteriorPrice));
            return true;
        }

        public bool TryActivateApartment(string interiorId, out string message)
        {
            message = string.Empty;
            var definition = GetInteriorDefinition(interiorId);
            var state = GetApartmentState(interiorId);
            if (definition == null || state == null || !state.IsOwned)
            {
                message = "Purchase the apartment before activating it.";
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

            balance -= state.OutstandingRent;
            state.OutstandingRent = 0f;
            state.IsAccessSuspended = false;
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
                message = "Rental is not available for the selected vehicle.";
                return false;
            }

            var upfrontCost = dailyRent * (1f + CommercialRentalRefundDays);
            if (balance < upfrontCost)
            {
                message = string.Format(
                    "Need {0} to cover the first rental day plus a refundable deposit.",
                    ModFormatting.FormatMoney(upfrontCost));
                return false;
            }

            balance -= upfrontCost;
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
                    "Rented {0} for {1}/day. First day and deposit collected.",
                    vehicle.DisplayName,
                    ModFormatting.FormatMoney(dailyRent))
                : string.Format(
                    "Rented {0} for {1}/day. Office garage is full, so it was moved to reserve.",
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
            balance += refund;
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

            var refund = Math.Max(0f, vehicle.DailyRent * CommercialRentalRefundDays);
            balance += refund;
            _state.CommercialVehicles.Remove(vehicle);
            NormalizeCommercialGarageAssignments();
            message = refund > 0.001f
                ? string.Format("Ended rental for {0}. Refunded {1}.", vehicle.DisplayName, ModFormatting.FormatMoney(refund))
                : string.Format("Ended rental for {0}.", vehicle.DisplayName);
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
                }
            }

            if (fuelSystem != null)
            {
                var telemetry = fuelSystem.GetTelemetry(truck, cargoVehicle);
                entry.CurrentFuelLiters = telemetry != null ? telemetry.CurrentLiters : entry.CurrentFuelLiters;
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
            if (definition == null || state == null || !state.IsOwned)
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
                var weeklyRent = Math.Max(0f, definition.InteriorWeeklyRent);
                if (weeklyRent <= 0.01f)
                {
                    continue;
                }

                if (!state.IsAccessSuspended && state.OutstandingRent <= 0.01f && balance >= weeklyRent)
                {
                    balance -= weeklyRent;
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
            return ActiveOffice != null
                ? Math.Max(0, ActiveOffice.MaxCommercialVehicles)
                : 0;
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
            if (string.IsNullOrWhiteSpace(_state.ActiveApartmentId))
            {
                return;
            }

            for (int i = 0; i < _state.PersonalVehicles.Count; i++)
            {
                var entry = _state.PersonalVehicles[i];
                if (entry != null)
                {
                    entry.AssignedApartmentId = _state.ActiveApartmentId;
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

        private void InitializeRentTracking(int currentInGameMinute)
        {
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            var currentDayIndex = GetDayIndex(currentInGameMinute);
            for (int i = 0; i < _state.Offices.Count; i++)
            {
                if (_state.Offices[i] != null && _state.Offices[i].LastChargedWeekIndex < 0)
                {
                    _state.Offices[i].LastChargedWeekIndex = currentWeekIndex;
                }
            }

            for (int i = 0; i < _state.Apartments.Count; i++)
            {
                if (_state.Apartments[i] != null && _state.Apartments[i].LastChargedWeekIndex < 0)
                {
                    _state.Apartments[i].LastChargedWeekIndex = currentWeekIndex;
                }
            }

            for (int i = 0; i < _state.CommercialVehicles.Count; i++)
            {
                var vehicle = _state.CommercialVehicles[i];
                if (vehicle != null && vehicle.IsRental && vehicle.LastChargedDayIndex < 0)
                {
                    vehicle.LastChargedDayIndex = currentDayIndex;
                }
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

        private static PropertyOwnershipPersistenceSnapshot CloneSnapshot(PropertyOwnershipPersistenceSnapshot source)
        {
            var clone = new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = source != null ? source.ActiveOfficeId : string.Empty,
                ActiveApartmentId = source != null ? source.ActiveApartmentId : string.Empty,
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
                });
            }

            return clone;
        }

        private static int GetWeekIndex(int currentInGameMinute)
        {
            return Math.Max(0, currentInGameMinute) / MinutesPerWeek;
        }

        private static int GetDayIndex(int currentInGameMinute)
        {
            return Math.Max(0, currentInGameMinute) / MinutesPerDay;
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
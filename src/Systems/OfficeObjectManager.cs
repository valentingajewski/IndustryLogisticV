using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Config;
using LSOL.Domain;
using WinForms = System.Windows.Forms;

namespace LSOL.Systems
{
    public sealed class OfficeObjectManager
    {
        private const float PreviewForwardDistance = 2.85f;
        private const float PreviewRightDistance = 1.1f;
        private const float OfficeStreamingDistance = 180f;
        private const float OfficePlacementRadius = 20f;
        private const float PlacementRotationStep = 15f;
        private const float PlacementPadding = 0.2f;
        private const float HaulUnloadDistance = 6f;
        private const float FuelDeliveryPriceMultiplier = 1.05f;
        private const float ServiceArrivalDistance = 18f;
        private const int ServiceDriveRefreshIntervalMs = 4000;
        private const int ServiceTimeoutMs = 900000;
        private const int ServiceDriveStyle = 786603;
        private const string DefaultDriverModel = "s_m_m_trucker_01";
        private const string OfficeObjectHaulTruckModelName = "hauler";
        private const string OfficeObjectHaulTrailerModelName = "trflat";
        private const float OfficeObjectHaulCargoRotationZ = 90f;
        private static readonly Vector3 OfficeObjectHaulTrailerSpawnPosition = new Vector3(937.16f, -3155.35f, 5.90f);
        private static readonly Vector3 OfficeObjectHaulTruckSpawnPosition = new Vector3(858.12f, -3130f, 5.41f);
        private const float OfficeObjectHaulTrailerHeading = 180f;
        private const float OfficeObjectHaulTruckHeading = 270f;

        private enum FuelDeliveryPhase
        {
            Delivering = 0,
            Returning = 1,
        }

        private enum HaulDeliveryPhase
        {
            ReachTruck = 0,
            AttachTrailer = 1,
            DeliverToOffice = 2,
            UnloadAtOffice = 3,
        }

        private sealed class PlacementSession
        {
            public string InstanceId { get; set; }

            public string OfficeId { get; set; }

            public float Heading { get; set; }
        }

        private sealed class FuelDeliveryDispatch
        {
            public string OfficeId { get; set; }

            public string TankInstanceId { get; set; }

            public Industry SourceIndustry { get; set; }

            public Vector3 SourceSpawnPosition { get; set; }

            public Vector3 TargetPosition { get; set; }

            public int RequestedAtMs { get; set; }

            public int NextDriveTaskRefreshMs { get; set; }

            public float RequestedLiters { get; set; }

            public float RequestedPrice { get; set; }

            public FuelDeliveryPhase Phase { get; set; }

            public Ped Driver { get; set; }

            public Vehicle Truck { get; set; }

            public Vehicle CargoVehicle { get; set; }

            public Blip RouteBlip { get; set; }
        }

        private sealed class HaulDeliverySession
        {
            public string InstanceId { get; set; }

            public string OfficeId { get; set; }

            public Vehicle Truck { get; set; }

            public Vehicle Trailer { get; set; }

            public Prop Cargo { get; set; }

            public Blip RouteBlip { get; set; }

            public HaulDeliveryPhase Phase { get; set; }
        }

        private readonly PropertyManager _propertyManager;
        private readonly FleetManager _fleetManager;
        private readonly VehicleFuelSystem _vehicleFuelSystem;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Func<float> _getProfit;
        private readonly Action<float> _deductProfit;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Action<string> _showStatus;
        private readonly Action _onOfficeObjectPlaced;
        private readonly Dictionary<string, Prop> _spawnedProps;
        private PlacementSession _placement;
        private HaulDeliverySession _activeHaulDelivery;
        private FuelDeliveryDispatch _activeFuelDelivery;
        private Prop _previewProp;
        private int _previewDefinitionId;
        private bool _previewIsPlacement;
        private string _spawnedOfficeId;

        public OfficeObjectManager(
            PropertyManager propertyManager,
            FleetManager fleetManager,
            VehicleFuelSystem vehicleFuelSystem,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Func<float> getProfit,
            Action<float> deductProfit,
            CompanyFinanceTracker financeTracker,
            Func<int> getCurrentInGameMinute,
            Func<Vector3, Vector3> getGroundPosition,
            Action<string> showStatus,
            Action onOfficeObjectPlaced = null)
        {
            _propertyManager = propertyManager ?? throw new ArgumentNullException(nameof(propertyManager));
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _vehicleFuelSystem = vehicleFuelSystem ?? throw new ArgumentNullException(nameof(vehicleFuelSystem));
            _industryManager = industryManager ?? throw new ArgumentNullException(nameof(industryManager));
            _globalMarket = globalMarket ?? throw new ArgumentNullException(nameof(globalMarket));
            _getProfit = getProfit;
            _deductProfit = deductProfit;
            _financeTracker = financeTracker;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _getGroundPosition = getGroundPosition;
            _showStatus = showStatus;
            _onOfficeObjectPlaced = onOfficeObjectPlaced;
            _spawnedProps = new Dictionary<string, Prop>(StringComparer.OrdinalIgnoreCase);
            _previewDefinitionId = 0;
            _spawnedOfficeId = string.Empty;
        }

        public bool IsPlacementActive
        {
            get { return _placement != null; }
        }

        public bool HasActiveFuelDelivery
        {
            get { return _activeFuelDelivery != null; }
        }

        public string ActiveFuelDeliveryOfficeId
        {
            get { return _activeFuelDelivery != null ? _activeFuelDelivery.OfficeId ?? string.Empty : string.Empty; }
        }

        public void Update(Ped player, OfficeObjectDefinition previewDefinition, bool previewEnabled, int now)
        {
            SyncPlacedObjects(player);
            UpdateFuelDelivery(now);
            UpdateHaulDelivery(player);

            if (_placement != null)
            {
                UpdatePlacementPreview(player);
                return;
            }

            if (_activeHaulDelivery != null)
            {
                ClearPreviewProp();
                return;
            }

            UpdateMenuPreview(player, previewDefinition, previewEnabled);
        }

        public bool HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            if (_placement == null)
            {
                return false;
            }

            controls = controls ?? new ControlBindings();
            if (key == controls.MenuLeft)
            {
                _placement.Heading = NormalizeHeading(_placement.Heading - PlacementRotationStep);
                return true;
            }

            if (key == controls.MenuRight)
            {
                _placement.Heading = NormalizeHeading(_placement.Heading + PlacementRotationStep);
                return true;
            }

            if (key == controls.MenuSelect || key == WinForms.Keys.Enter)
            {
                ConfirmPlacement(Game.Player.Character);
                return true;
            }

            if (key == controls.MenuBack || key == WinForms.Keys.Escape)
            {
                CancelPlacement("Placement cancelled.");
                return true;
            }

            return false;
        }

        public bool TryStartPlacement(OfficeDefinition office, OfficeObjectPersistenceEntry entry, out string message)
        {
            message = string.Empty;
            if (_placement != null)
            {
                message = "Finish the current placement first.";
                return false;
            }

            if (_activeHaulDelivery != null)
            {
                message = "Finish the current office delivery first.";
                return false;
            }

            if (office == null || entry == null)
            {
                message = "No office object is available to place.";
                return false;
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Activate this office before placing objects.";
                return false;
            }

            var definition = _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId);
            if (definition == null)
            {
                message = "Office object definition unavailable.";
                return false;
            }

            if (definition.IsFunctional && !entry.IsPlaced)
            {
                return TryStartHaulDelivery(office, entry, definition, out message);
            }

            _placement = new PlacementSession
            {
                InstanceId = entry.InstanceId,
                OfficeId = office.OfficeId,
                Heading = entry.IsPlaced && Math.Abs(entry.Rotation.Z) > 0.01f
                    ? NormalizeHeading(entry.Rotation.Z)
                    : NormalizeHeading(Game.Player.Character != null && Game.Player.Character.Exists() ? Game.Player.Character.Heading : office.SpawnHeading),
            };

            ClearPreviewProp();
            message = string.Format("Placement mode: rotate with Left/Right, confirm with Enter, cancel with Back.");
            return true;
        }

        public bool TryRefuelPlayerVehicleAtOffice(Ped player, out string message)
        {
            message = string.Empty;

            OfficeDefinition office;
            OfficeObjectPersistenceEntry tankEntry;
            OfficeObjectDefinition tankDefinition;
            float storedLiters;
            if (!TryGetActiveOfficeTank(out office, out tankEntry, out tankDefinition, out storedLiters, out message))
            {
                return false;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || poweredVehicle == null
                || !poweredVehicle.Exists())
            {
                message = "Bring an owned company vehicle to the office to refuel it.";
                return false;
            }

            OwnedCommercialVehiclePersistenceEntry vehicleEntry;
            if (!_propertyManager.TryResolveCommercialVehicleRecord(poweredVehicle, out vehicleEntry)
                || vehicleEntry == null
                || !string.Equals(vehicleEntry.AssignedOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Only company vehicles assigned to the active office can use the diesel tank.";
                return false;
            }

            _vehicleFuelSystem.EnsureTrackedVehicle(poweredVehicle, vehicleEntry.CurrentFuelLiters > 0.001f ? (float?)vehicleEntry.CurrentFuelLiters : null);
            var telemetry = _vehicleFuelSystem.GetTelemetry(poweredVehicle, cargoVehicle);
            if (telemetry == null || telemetry.CapacityLiters <= 0.01f)
            {
                message = "No fueled company vehicle is available to refuel.";
                return false;
            }

            var litersNeeded = Math.Max(0f, telemetry.CapacityLiters - telemetry.CurrentLiters);
            if (litersNeeded <= 0.05f)
            {
                message = "The office vehicle is already full.";
                return false;
            }

            var litersToAdd = Math.Min(storedLiters, litersNeeded);
            var addedLiters = _vehicleFuelSystem.AddFuel(poweredVehicle, litersToAdd);
            if (addedLiters <= 0.05f)
            {
                message = "The office vehicle is already full.";
                return false;
            }

            _propertyManager.TryUpdateOfficeObjectStoredResourceAmount(tankEntry.InstanceId, Math.Max(0f, storedLiters - addedLiters), out tankEntry);
            message = string.Format("Refueled {0:0}L from the office diesel tank. Tank now holds {1:0}/{2:0}L.", addedLiters, Math.Max(0f, storedLiters - addedLiters), tankDefinition.Capacity);
            return true;
        }

        public bool TryRepairPlayerVehicleAtOffice(Ped player, out string message)
        {
            message = string.Empty;

            string officeReason;
            if (!_propertyManager.CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            var office = _propertyManager.ActiveOffice;
            if (office == null)
            {
                message = "No active office is available.";
                return false;
            }

            if (!_propertyManager.HasOfficeObjectFunction(office.OfficeId, OfficeObjectFunction.Repair))
            {
                message = "Install a Maintenance Bay at the active office first.";
                return false;
            }

            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                message = "Enter a company vehicle near the office to repair it.";
                return false;
            }

            if (!IsWithinOfficePlacementBounds(player.Position, office))
            {
                message = "Move closer to the active office yard to use the Maintenance Bay.";
                return false;
            }

            var currentVehicle = player.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
            {
                message = "No vehicle is available to repair.";
                return false;
            }

            OwnedCommercialVehiclePersistenceEntry vehicleEntry;
            if (!_propertyManager.TryResolveCommercialVehicleRecord(currentVehicle, out vehicleEntry)
                || vehicleEntry == null
                || !string.Equals(vehicleEntry.AssignedOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Only company vehicles assigned to the active office can use the Maintenance Bay.";
                return false;
            }

            currentVehicle.Repair();
            var trailer = ResolveTrailerForVehicle(currentVehicle);
            if (trailer != null && trailer.Exists())
            {
                trailer.Repair();
            }

            _propertyManager.RecordMaintenanceBayService(vehicleEntry, _getCurrentInGameMinute != null ? _getCurrentInGameMinute() : 0);

            message = trailer != null && trailer.Exists()
                ? "Maintenance Bay repaired the truck and trailer."
                : "Maintenance Bay repaired the truck.";
            return true;
        }

        public bool TryUnloadFuelCargoIntoOfficeTank(Ped player, out string message)
        {
            message = string.Empty;

            OfficeDefinition office;
            OfficeObjectPersistenceEntry tankEntry;
            OfficeObjectDefinition tankDefinition;
            float storedLiters;
            if (!TryGetActiveOfficeTank(out office, out tankEntry, out tankDefinition, out storedLiters, out message))
            {
                return false;
            }

            if (player == null || !player.Exists())
            {
                message = "Player unavailable.";
                return false;
            }

            if (!IsWithinOfficePlacementBounds(player.Position, office))
            {
                message = "Move closer to the active office yard to unload fuel.";
                return false;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || cargoVehicle == null
                || !cargoVehicle.Exists())
            {
                message = "Bring an owned company cargo vehicle to unload fuel.";
                return false;
            }

            OwnedCommercialVehiclePersistenceEntry vehicleEntry;
            if (!_propertyManager.TryResolveCommercialVehicleRecord(poweredVehicle, out vehicleEntry)
                || vehicleEntry == null
                || !string.Equals(vehicleEntry.AssignedOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Only company vehicles assigned to the active office can unload into the diesel tank.";
                return false;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || cargoState.IsEmpty || !string.Equals(CommodityCatalog.Normalize(cargoState.Commodity), "Fuel", StringComparison.OrdinalIgnoreCase))
            {
                message = "Load Fuel cargo before unloading it into the office tank.";
                return false;
            }

            var freeLiters = Math.Max(0f, tankDefinition.Capacity - storedLiters);
            if (freeLiters <= 0.05f)
            {
                message = "The office diesel tank is already full.";
                return false;
            }

            var transferableLiters = Math.Min(freeLiters, Math.Max(0f, cargoState.WeightTons * 1000f));
            if (transferableLiters <= 0.05f)
            {
                message = "No fuel cargo is available to unload.";
                return false;
            }

            var remainingWeight = Math.Max(0f, cargoState.WeightTons - (transferableLiters / 1000f));
            if (remainingWeight <= 0.001f)
            {
                cargoState.ClearCargo();
            }
            else
            {
                cargoState.WeightTons = remainingWeight;
                cargoState.CargoCondition = Math.Max(0f, Math.Min(1f, cargoState.CargoCondition));
            }

            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            _propertyManager.TryUpdateOfficeObjectStoredResourceAmount(tankEntry.InstanceId, storedLiters + transferableLiters, out tankEntry);
            message = string.Format("Unloaded {0:0}L of diesel into the office tank. Tank now holds {1:0}/{2:0}L.", transferableLiters, storedLiters + transferableLiters, tankDefinition.Capacity);
            return true;
        }

        public bool TryRequestOfficeFuelDelivery(int now, out string message)
        {
            message = string.Empty;
            if (_activeFuelDelivery != null)
            {
                message = "An office diesel delivery is already in transit.";
                return false;
            }

            OfficeDefinition office;
            OfficeObjectPersistenceEntry tankEntry;
            OfficeObjectDefinition tankDefinition;
            float storedLiters;
            if (!TryGetActiveOfficeTank(out office, out tankEntry, out tankDefinition, out storedLiters, out message))
            {
                return false;
            }

            var freeLiters = Math.Max(0f, tankDefinition.Capacity - storedLiters);
            if (freeLiters <= 0.05f)
            {
                message = "The office diesel tank is already full.";
                return false;
            }

            Industry sourceIndustry;
            if (!TryFindRefinery(out sourceIndustry, out message))
            {
                return false;
            }

            VehicleDefinition tankerTrailer;
            VehicleDefinition tractorDefinition;
            if (!TryResolveServiceRig(out tankerTrailer, out tractorDefinition, out message))
            {
                return false;
                }

            var availableSourceLiters = Math.Max(0f, sourceIndustry.GetStock("Fuel") * 1000f);
            if (availableSourceLiters <= 0.05f)
            {
                message = string.Format("{0} is out of diesel.", sourceIndustry.Name);
                return false;
            }

            var rigCapacityLiters = Math.Max(0f, tankerTrailer.CapacityTons * 1000f);
            var requestedLiters = Math.Min(freeLiters, availableSourceLiters);
            if (rigCapacityLiters > 0.05f)
            {
                requestedLiters = Math.Min(requestedLiters, rigCapacityLiters);
            }

            var pricePerLiter = Math.Max(0f, _globalMarket.GetUnitPrice("Fuel")) / 1000f;
            var affordableLiters = pricePerLiter <= 0.0001f || _getProfit == null
                ? requestedLiters
                : Math.Max(0f, (_getProfit() / (pricePerLiter * FuelDeliveryPriceMultiplier)));
            requestedLiters = Math.Min(requestedLiters, affordableLiters);
            if (requestedLiters <= 0.05f)
            {
                message = "Insufficient funds to request diesel delivery.";
                return false;
            }

            Vector3 spawnPosition;
            float spawnHeading;
            ResolveDispatchSpawn(sourceIndustry, office.SpawnPosition, out spawnPosition, out spawnHeading);

            Vehicle truck;
            Vehicle cargoVehicle;
            if (!_fleetManager.SpawnSelectedVehicle(tankerTrailer, tractorDefinition, spawnPosition, spawnHeading, out truck, out cargoVehicle, out message))
            {
                return false;
            }

            var driver = CreateDispatchDriver(truck);
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

                message = "Failed to dispatch the refinery delivery truck.";
                return false;
            }

            _activeFuelDelivery = new FuelDeliveryDispatch
            {
                OfficeId = office.OfficeId,
                TankInstanceId = tankEntry.InstanceId,
                SourceIndustry = sourceIndustry,
                SourceSpawnPosition = spawnPosition,
                TargetPosition = office.SpawnPosition,
                RequestedAtMs = now,
                NextDriveTaskRefreshMs = 0,
                RequestedLiters = requestedLiters,
                RequestedPrice = requestedLiters * pricePerLiter * FuelDeliveryPriceMultiplier,
                Phase = FuelDeliveryPhase.Delivering,
                Driver = driver,
                Truck = truck,
                CargoVehicle = cargoVehicle,
                RouteBlip = CreateDispatchBlip(truck, office, sourceIndustry),
            };

            EnsureDispatchDriveTask(_activeFuelDelivery, _activeFuelDelivery.TargetPosition, now);
            message = string.Format("Diesel delivery dispatched from {0}.", sourceIndustry.Name);
            return true;
        }

        public void Cleanup()
        {
            CancelPlacement();
            CleanupHaulDelivery();
            CleanupFuelDelivery();
            ClearPreviewProp();
            ClearSpawnedProps();
        }

        private bool TryStartHaulDelivery(OfficeDefinition office, OfficeObjectPersistenceEntry entry, OfficeObjectDefinition definition, out string message)
        {
            message = string.Empty;
            if (office == null || entry == null || definition == null)
            {
                message = "No office module is available to deliver.";
                return false;
            }

            Vehicle truck;
            if (!_fleetManager.TrySpawnVehicleByModelName(OfficeObjectHaulTruckModelName, OfficeObjectHaulTruckSpawnPosition, OfficeObjectHaulTruckHeading, out truck)
                || truck == null
                || !truck.Exists())
            {
                message = "Failed to stage the port haul truck.";
                return false;
            }

            Vehicle trailer;
            if (!_fleetManager.TrySpawnVehicleByModelName(OfficeObjectHaulTrailerModelName, OfficeObjectHaulTrailerSpawnPosition, OfficeObjectHaulTrailerHeading, out trailer)
                || trailer == null
                || !trailer.Exists())
            {
                DeleteVehicle(truck);
                message = "Failed to stage the port haul trailer.";
                return false;
            }

            truck.IsPersistent = true;
            trailer.IsPersistent = true;

            Prop cargo;
            if (!TryCreateHaulCargoProp(definition, trailer, out cargo))
            {
                DeleteVehicle(trailer);
                DeleteVehicle(truck);
                message = string.Format("Failed to load {0} onto the haul trailer.", definition.DisplayName);
                return false;
            }

            _activeHaulDelivery = new HaulDeliverySession
            {
                InstanceId = entry.InstanceId,
                OfficeId = office.OfficeId,
                Truck = truck,
                Trailer = trailer,
                Cargo = cargo,
                Phase = HaulDeliveryPhase.ReachTruck,
                RouteBlip = CreateHaulDeliveryBlip(OfficeObjectHaulTruckSpawnPosition, definition, office, HaulDeliveryPhase.ReachTruck),
            };

            ClearPreviewProp();
            return true;
        }

        private void ConfirmPlacement(Ped player)
        {
            if (_placement == null)
            {
                return;
            }

            OfficeDefinition office;
            var entry = GetPlacementEntry(out office);
            var definition = entry != null ? _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId) : null;
            if (office == null || entry == null || definition == null)
            {
                CancelPlacement("Office placement data is no longer available.");
                return;
            }

            Vector3 position;
            Vector3 rotation;
            string error;
            if (!TryComputePlacementTransform(player, office, definition, _placement.Heading, entry.InstanceId, out position, out rotation, out error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _showStatus?.Invoke(error);
                }

                return;
            }

            string message;
            OfficeObjectPersistenceEntry placedEntry;
            if (!_propertyManager.TryPlaceOfficeObject(entry.InstanceId, position, rotation, out placedEntry, out message))
            {
                _showStatus?.Invoke(message);
                return;
            }

            _showStatus?.Invoke(message);
            _placement = null;
            ClearPreviewProp();
            SyncPlacedObjects(player);
            _onOfficeObjectPlaced?.Invoke();
        }

        private void CancelPlacement(string message = null)
        {
            _placement = null;
            ClearPreviewProp();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _showStatus?.Invoke(message);
            }
        }

        private void UpdatePlacementPreview(Ped player)
        {
            if (_placement == null)
            {
                return;
            }

            OfficeDefinition office;
            var entry = GetPlacementEntry(out office);
            var definition = entry != null ? _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId) : null;
            if (office == null || entry == null || definition == null)
            {
                CancelPlacement("Office placement data is no longer available.");
                return;
            }

            Vector3 position;
            Vector3 rotation;
            string error;
            var canPlace = TryComputePlacementTransform(player, office, definition, _placement.Heading, entry.InstanceId, out position, out rotation, out error);

            var preview = EnsurePreviewProp(definition, true);
            if (preview == null || !preview.Exists())
            {
                return;
            }

            ApplyPreviewTransform(preview, position, rotation);
            Function.Call(Hash.SET_ENTITY_ALPHA, preview.Handle, canPlace ? 190 : 120, false);

            var prompt = canPlace
                ? string.Format("Placing {0}. Left/Right rotate, Enter confirm, Back cancel.", definition.DisplayName)
                : (string.IsNullOrWhiteSpace(error) ? "Move to a valid office placement spot." : error);
            Screen.ShowHelpTextThisFrame(prompt);
        }

        private void UpdateMenuPreview(Ped player, OfficeObjectDefinition previewDefinition, bool previewEnabled)
        {
            if (!previewEnabled || previewDefinition == null || player == null || !player.Exists())
            {
                ClearPreviewProp();
                return;
            }

            var preview = EnsurePreviewProp(previewDefinition, false);
            if (preview == null || !preview.Exists())
            {
                return;
            }

            Vector3 position;
            Vector3 rotation;
            ComputeMenuPreviewTransform(player, previewDefinition, out position, out rotation);
            ApplyPreviewTransform(preview, position, rotation);
            Function.Call(Hash.SET_ENTITY_ALPHA, preview.Handle, 180, false);
        }

        private void UpdateHaulDelivery(Ped player)
        {
            var delivery = _activeHaulDelivery;
            if (delivery == null)
            {
                return;
            }

            OfficeDefinition office;
            OfficeObjectPersistenceEntry entry;
            OfficeObjectDefinition definition;
            if (!TryGetHaulDeliveryContext(delivery, out office, out entry, out definition))
            {
                CancelHaulDelivery("Office module delivery data is no longer available.");
                return;
            }

            if (delivery.Truck == null || !delivery.Truck.Exists() || delivery.Trailer == null || !delivery.Trailer.Exists() || delivery.Cargo == null || !delivery.Cargo.Exists())
            {
                CancelHaulDelivery(string.Format("{0} delivery failed. Start the port haul again.", definition.DisplayName));
                return;
            }

            RefreshHaulDeliveryBlip(delivery, office, definition);

            switch (delivery.Phase)
            {
                case HaulDeliveryPhase.ReachTruck:
                    Screen.ShowHelpTextThisFrame(string.Format(
                        "Go to the port terminal to retreive your {0}",
                        definition.DisplayName));
                    if (IsPlayerUsingVehicle(player, delivery.Truck))
                    {
                        delivery.Phase = HaulDeliveryPhase.AttachTrailer;
                        RefreshHaulDeliveryBlip(delivery, office, definition);
                    }

                    break;

                case HaulDeliveryPhase.AttachTrailer:
                    if (IsTruckAttachedToTrailer(delivery.Truck, delivery.Trailer))
                    {
                        delivery.Phase = HaulDeliveryPhase.DeliverToOffice;
                        RefreshHaulDeliveryBlip(delivery, office, definition);
                        _showStatus?.Invoke(string.Format("Trailer attached. Deliver {0} to {1}.", definition.DisplayName, office.DisplayName));
                    }
                    else
                    {
                        Screen.ShowHelpTextThisFrame(string.Format(
                            "Attach the trailer to transport {3}.",
                            OfficeObjectHaulTrailerSpawnPosition.X,
                            OfficeObjectHaulTrailerSpawnPosition.Y,
                            OfficeObjectHaulTrailerSpawnPosition.Z,
                            definition.DisplayName));
                    }

                    break;

                case HaulDeliveryPhase.DeliverToOffice:
                    var trailerAttached = IsTruckAttachedToTrailer(delivery.Truck, delivery.Trailer);
                    var trailerInOffice = IsWithinOfficePlacementBounds(delivery.Trailer.Position, office);
                    if (trailerInOffice && !trailerAttached)
                    {
                        delivery.Phase = HaulDeliveryPhase.UnloadAtOffice;
                        RefreshHaulDeliveryBlip(delivery, office, definition);
                        _showStatus?.Invoke(string.Format("{0} arrived at the office. Walk to the cargo to unload it.", definition.DisplayName));
                        break;
                    }

                    if (!trailerAttached)
                    {
                        delivery.Phase = HaulDeliveryPhase.AttachTrailer;
                        RefreshHaulDeliveryBlip(delivery, office, definition);
                        _showStatus?.Invoke("Trailer detached before reaching the office. Hook it up again.");
                        break;
                    }

                    Screen.ShowHelpTextThisFrame(trailerInOffice
                        ? string.Format("Detach the trailer at {0}, exit the truck, and walk to the cargo to unload {1}.", office.DisplayName, definition.DisplayName)
                        : string.Format("Drive the trailer to {0} and bring {1} into the office yard.", office.DisplayName, definition.DisplayName));
                    break;

                case HaulDeliveryPhase.UnloadAtOffice:
                    if (!IsWithinOfficePlacementBounds(delivery.Trailer.Position, office))
                    {
                        delivery.Phase = HaulDeliveryPhase.DeliverToOffice;
                        RefreshHaulDeliveryBlip(delivery, office, definition);
                        _showStatus?.Invoke(string.Format("Bring the trailer carrying {0} back into the office yard.", definition.DisplayName));
                        break;
                    }

                    if (player == null || !player.Exists())
                    {
                        return;
                    }

                    if (IsPlayerUsingAnyVehicle(player))
                    {
                        Screen.ShowHelpTextThisFrame(string.Format("Exit the vehicle and approach {0} to unload it.", definition.DisplayName));
                        return;
                    }

                    if (player.Position.DistanceTo(delivery.Cargo.Position) <= HaulUnloadDistance)
                    {
                        StartPlacementFromHaul(player, office, entry, definition);
                        return;
                    }

                    Screen.ShowHelpTextThisFrame(string.Format("Walk to the trailer cargo to unload {0}, then move it manually into place.", definition.DisplayName));
                    break;
            }
        }

        private void SyncPlacedObjects(Ped player)
        {
            var office = _propertyManager.ActiveOffice;
            if (office == null || player == null || !player.Exists() || player.Position.DistanceToSquared(office.SpawnPosition) > OfficeStreamingDistance * OfficeStreamingDistance)
            {
                _spawnedOfficeId = string.Empty;
                ClearSpawnedProps();
                return;
            }

            var activeOfficeId = office.OfficeId ?? string.Empty;
            if (!string.Equals(_spawnedOfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase))
            {
                ClearSpawnedProps();
                _spawnedOfficeId = activeOfficeId;
            }

            var desiredEntries = _propertyManager.GetOfficeObjects(activeOfficeId, false);
            var desiredIds = new HashSet<string>(desiredEntries.Select(entry => entry.InstanceId), StringComparer.OrdinalIgnoreCase);

            var toRemove = _spawnedProps.Keys.Where(key => !desiredIds.Contains(key)).ToList();
            for (int i = 0; i < toRemove.Count; i++)
            {
                DeleteProp(_spawnedProps[toRemove[i]]);
                _spawnedProps.Remove(toRemove[i]);
            }

            for (int i = 0; i < desiredEntries.Count; i++)
            {
                var entry = desiredEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.InstanceId))
                {
                    continue;
                }

                var definition = _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId);
                if (definition == null)
                {
                    continue;
                }

                Prop existing;
                if (_spawnedProps.TryGetValue(entry.InstanceId, out existing) && existing != null && existing.Exists())
                {
                    continue;
                }

                var prop = CreatePlacedProp(definition, entry.Position, entry.Rotation);
                if (prop != null && prop.Exists())
                {
                    _spawnedProps[entry.InstanceId] = prop;
                }
            }
        }

        private void UpdateFuelDelivery(int now)
        {
            if (_activeFuelDelivery == null)
            {
                return;
            }

            if (now - _activeFuelDelivery.RequestedAtMs >= ServiceTimeoutMs)
            {
                CancelFuelDelivery("Office diesel delivery timed out before arriving.");
                return;
            }

            if (_activeFuelDelivery.Driver == null || !_activeFuelDelivery.Driver.Exists() || _activeFuelDelivery.Truck == null || !_activeFuelDelivery.Truck.Exists())
            {
                CancelFuelDelivery("Office diesel delivery failed before arriving.");
                return;
            }

            RefreshDispatchBlip(_activeFuelDelivery);

            if (_activeFuelDelivery.Phase == FuelDeliveryPhase.Returning)
            {
                if (_activeFuelDelivery.Truck.Position.DistanceTo(_activeFuelDelivery.SourceSpawnPosition) <= ServiceArrivalDistance)
                {
                    CleanupFuelDelivery();
                    return;
                }

                EnsureDispatchDriveTask(_activeFuelDelivery, _activeFuelDelivery.SourceSpawnPosition, now);
                return;
            }

            if (_activeFuelDelivery.Truck.Position.DistanceTo(_activeFuelDelivery.TargetPosition) > ServiceArrivalDistance)
            {
                EnsureDispatchDriveTask(_activeFuelDelivery, _activeFuelDelivery.TargetPosition, now);
                return;
            }

            ClearDispatchDriverTasks(_activeFuelDelivery.Driver);
            FinishFuelDelivery(now);
        }

        private void StartPlacementFromHaul(Ped player, OfficeDefinition office, OfficeObjectPersistenceEntry entry, OfficeObjectDefinition definition)
        {
            if (office == null || entry == null || definition == null)
            {
                CancelHaulDelivery("Office module delivery data is no longer available.");
                return;
            }

            CleanupHaulDelivery();
            _placement = new PlacementSession
            {
                InstanceId = entry.InstanceId,
                OfficeId = office.OfficeId,
                Heading = NormalizeHeading(player != null && player.Exists() ? player.Heading : office.SpawnHeading),
            };

            ClearPreviewProp();
            _showStatus?.Invoke(string.Format("{0} unloaded. Move it manually and press Enter to place it.", definition.DisplayName));
        }

        private bool TryGetHaulDeliveryContext(HaulDeliverySession delivery, out OfficeDefinition office, out OfficeObjectPersistenceEntry entry, out OfficeObjectDefinition definition)
        {
            office = null;
            entry = null;
            definition = null;
            if (delivery == null)
            {
                return false;
            }

            office = _propertyManager.GetOfficeDefinition(delivery.OfficeId);
            entry = _propertyManager.GetOfficeObjects(delivery.OfficeId, true)
                .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.InstanceId, delivery.InstanceId, StringComparison.OrdinalIgnoreCase));
            definition = entry != null ? _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId) : null;
            return office != null && entry != null && definition != null;
        }

        private Blip CreateHaulDeliveryBlip(Vector3 position, OfficeObjectDefinition definition, OfficeDefinition office, HaulDeliveryPhase phase)
        {
            var blip = World.CreateBlip(position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = ResolveHaulDeliveryBlipSprite(phase);
            blip.Color = BlipColor.Yellow;
            blip.Name = string.Format("Office Delivery: {0} -> {1}", definition != null ? definition.DisplayName : "module", office != null ? office.DisplayName : "office");
            blip.Scale = 0.85f;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private void RefreshHaulDeliveryBlip(HaulDeliverySession delivery, OfficeDefinition office, OfficeObjectDefinition definition)
        {
            if (delivery == null)
            {
                return;
            }

            Vector3 targetPosition;
            switch (delivery.Phase)
            {
                case HaulDeliveryPhase.ReachTruck:
                    targetPosition = delivery.Truck != null && delivery.Truck.Exists() ? delivery.Truck.Position : OfficeObjectHaulTruckSpawnPosition;
                    break;
                case HaulDeliveryPhase.AttachTrailer:
                case HaulDeliveryPhase.UnloadAtOffice:
                    targetPosition = delivery.Trailer != null && delivery.Trailer.Exists() ? delivery.Trailer.Position : OfficeObjectHaulTrailerSpawnPosition;
                    break;
                default:
                    targetPosition = office != null ? office.SpawnPosition : (delivery.Trailer != null && delivery.Trailer.Exists() ? delivery.Trailer.Position : OfficeObjectHaulTrailerSpawnPosition);
                    break;
            }

            if (delivery.RouteBlip == null || !delivery.RouteBlip.Exists())
            {
                delivery.RouteBlip = CreateHaulDeliveryBlip(targetPosition, definition, office, delivery.Phase);
                return;
            }

            delivery.RouteBlip.Position = targetPosition;
            delivery.RouteBlip.Sprite = ResolveHaulDeliveryBlipSprite(delivery.Phase);
        }

        private static BlipSprite ResolveHaulDeliveryBlipSprite(HaulDeliveryPhase phase)
        {
            switch (phase)
            {
                case HaulDeliveryPhase.AttachTrailer:
                case HaulDeliveryPhase.UnloadAtOffice:
                    return BlipSprite.Trailer;
                default:
                    return BlipSprite.Truck;
            }
        }

        private void CancelHaulDelivery(string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _showStatus?.Invoke(reason);
            }

            CleanupHaulDelivery();
        }

        private void CleanupHaulDelivery()
        {
            if (_activeHaulDelivery == null)
            {
                return;
            }

            DeleteBlip(_activeHaulDelivery.RouteBlip);
            if (_activeHaulDelivery.Cargo != null && _activeHaulDelivery.Cargo.Exists())
            {
                try
                {
                    Function.Call(Hash.DETACH_ENTITY, _activeHaulDelivery.Cargo.Handle, true, true);
                }
                catch
                {
                    // Best-effort detach before cleanup.
                }
            }

            DeleteProp(_activeHaulDelivery.Cargo);
            DeleteVehicle(_activeHaulDelivery.Trailer);
            DeleteVehicle(_activeHaulDelivery.Truck);
            _activeHaulDelivery = null;
        }

        private bool TryCreateHaulCargoProp(OfficeObjectDefinition definition, Vehicle trailer, out Prop cargo)
        {
            cargo = null;
            if (definition == null || trailer == null || !trailer.Exists())
            {
                return false;
            }

            var model = new Model(definition.ModelHash);
            model.Request(500);
            if (!model.IsLoaded)
            {
                return false;
            }

            cargo = World.CreateProp(model, OfficeObjectHaulTrailerSpawnPosition, true, false);
            model.MarkAsNoLongerNeeded();
            if (cargo == null || !cargo.Exists())
            {
                cargo = null;
                return false;
            }

            cargo.IsPersistent = true;
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, cargo.Handle, true);
            Function.Call(Hash.SET_ENTITY_COLLISION, cargo.Handle, false, false);

            Vector3 cargoMin;
            Vector3 cargoMax;
            Vector3 trailerMin;
            Vector3 trailerMax;
            var hasCargoDimensions = TryGetModelDimensions(model, out cargoMin, out cargoMax);
            var hasTrailerDimensions = TryGetModelDimensions(trailer.Model, out trailerMin, out trailerMax);
            var cargoBottomOffset = hasCargoDimensions ? -cargoMin.Z : 0.5f;
            var trailerDeckHeight = hasTrailerDimensions ? Math.Max(0.25f, trailerMax.Z) : 1.1f;

            Function.Call(
                Hash.ATTACH_ENTITY_TO_ENTITY,
                cargo.Handle,
                trailer.Handle,
                0,
                0f,
                0f,
                trailerDeckHeight + cargoBottomOffset,
                0f,
                0f,
                OfficeObjectHaulCargoRotationZ,
                false,
                false,
                false,
                false,
                2,
                true);
            return true;
        }

        private void FinishFuelDelivery(int now)
        {
            if (_activeFuelDelivery == null)
            {
                return;
            }

            var tankEntry = _propertyManager.GetOfficeObjects(_activeFuelDelivery.OfficeId, false)
                .FirstOrDefault(entry => entry != null && string.Equals(entry.InstanceId, _activeFuelDelivery.TankInstanceId, StringComparison.OrdinalIgnoreCase));
            var tankDefinition = tankEntry != null ? _propertyManager.GetOfficeObjectDefinition(tankEntry.DefinitionId) : null;
            if (tankEntry == null || tankDefinition == null)
            {
                CancelFuelDelivery("Office diesel delivery cancelled because the target tank is no longer available.");
                return;
            }

            var freeLiters = Math.Max(0f, tankDefinition.Capacity - tankEntry.StoredResourceAmount);
            if (freeLiters <= 0.05f)
            {
                CancelFuelDelivery("Office diesel delivery cancelled because the tank is already full.");
                return;
            }

            var removedTons = _activeFuelDelivery.SourceIndustry != null
                ? _activeFuelDelivery.SourceIndustry.RemoveOutput("Fuel", _activeFuelDelivery.RequestedLiters / 1000f)
                : 0f;
            var deliveredLiters = Math.Min(freeLiters, Math.Max(0f, removedTons * 1000f));
            if (deliveredLiters <= 0.05f)
            {
                BeginDispatchReturn(_activeFuelDelivery, now);
                _showStatus?.Invoke("Refinery delivery arrived empty because no diesel stock was available.");
                return;
            }

            var leftoverLiters = Math.Max(0f, (removedTons * 1000f) - deliveredLiters);
            if (leftoverLiters > 0.05f && _activeFuelDelivery.SourceIndustry != null)
            {
                _activeFuelDelivery.SourceIndustry.AddOutput("Fuel", leftoverLiters / 1000f);
            }

            _propertyManager.TryUpdateOfficeObjectStoredResourceAmount(tankEntry.InstanceId, tankEntry.StoredResourceAmount + deliveredLiters, out tankEntry);
            var deliveredPrice = _activeFuelDelivery.RequestedLiters <= 0.05f
                ? 0f
                : _activeFuelDelivery.RequestedPrice * (deliveredLiters / _activeFuelDelivery.RequestedLiters);
            if (deliveredPrice > 0f && _deductProfit != null)
            {
                _deductProfit(deliveredPrice);
                RecordFinanceExpense(
                    CompanyFinanceCategory.FuelPurchase,
                    deliveredPrice,
                    string.Format("Office fuel delivery for {0}", _activeFuelDelivery.OfficeId ?? "office"));
            }

            _showStatus?.Invoke(string.Format("Refinery delivery unloaded {0:0}L at the office tank for {1}.", deliveredLiters, ModFormatting.FormatMoney(deliveredPrice)));
            BeginDispatchReturn(_activeFuelDelivery, now);
        }

        private void RecordFinanceExpense(CompanyFinanceCategory category, float amount, string description)
        {
            if (_financeTracker == null || amount <= 0f)
            {
                return;
            }

            var inGameMinute = _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
            _financeTracker.RecordExpense(category, amount, inGameMinute, description);
        }

        private bool TryGetActiveOfficeTank(out OfficeDefinition office, out OfficeObjectPersistenceEntry tankEntry, out OfficeObjectDefinition tankDefinition, out float storedLiters, out string message)
        {
            office = null;
            tankEntry = null;
            tankDefinition = null;
            storedLiters = 0f;
            message = string.Empty;

            string officeReason;
            if (!_propertyManager.CanUseCommercialSystems(out officeReason))
            {
                message = officeReason;
                return false;
            }

            office = _propertyManager.ActiveOffice;
            if (office == null)
            {
                message = "No active office is available.";
                return false;
            }

            tankEntry = _propertyManager.GetFirstPlacedOfficeObjectByFunction(office.OfficeId, OfficeObjectFunction.Refuel);
            tankDefinition = tankEntry != null ? _propertyManager.GetOfficeObjectDefinition(tankEntry.DefinitionId) : null;
            if (tankEntry == null || tankDefinition == null)
            {
                message = "Install a Diesel Tank at the active office first.";
                return false;
            }

            storedLiters = Math.Max(0f, tankEntry.StoredResourceAmount);
            return true;
        }

        private bool TryFindRefinery(out Industry refinery, out string message)
        {
            refinery = _industryManager.Industries
                .Where(industry => industry != null && industry.HasOutputStock("Fuel"))
                .OrderBy(industry => string.Equals(industry.LegacyKey, "Refinery", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(industry => industry.Name != null && industry.Name.IndexOf("Refinery", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(industry => industry.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (refinery == null)
            {
                message = "No refinery with diesel stock is available.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryResolveServiceRig(out VehicleDefinition tankerTrailer, out VehicleDefinition tractorDefinition, out string message)
        {
            tankerTrailer = _fleetManager
                .GetSpawnableForCommodity("Fuel")
                .Where(definition => definition != null && definition.IsTrailer)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();
            tractorDefinition = _fleetManager
                .GetTractorDefinitions()
                .Where(definition => definition != null)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();

            if (tankerTrailer == null || tractorDefinition == null)
            {
                message = "No truck + tanker trailer setup is configured for refinery deliveries.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void ResolveDispatchSpawn(Industry sourceIndustry, Vector3 targetPosition, out Vector3 spawnPosition, out float spawnHeading)
        {
            spawnPosition = sourceIndustry != null && sourceIndustry.VehicleSpawnPosition.HasValue
                ? sourceIndustry.VehicleSpawnPosition.Value
                : (sourceIndustry != null ? sourceIndustry.Position : targetPosition);
            if (_getGroundPosition != null)
            {
                spawnPosition = _getGroundPosition(spawnPosition);
            }

            if (sourceIndustry != null && sourceIndustry.VehicleSpawnHeading.HasValue)
            {
                spawnHeading = sourceIndustry.VehicleSpawnHeading.Value;
                return;
            }

            var delta = targetPosition - spawnPosition;
            spawnHeading = delta.LengthSquared() <= 0.001f
                ? 0f
                : Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
        }

        private Ped CreateDispatchDriver(Vehicle truck)
        {
            if (truck == null || !truck.Exists())
            {
                return null;
            }

            var model = new Model(DefaultDriverModel);
            model.Request(1000);
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
            Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 0.85f);
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 0.2f);
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, truck.Handle, true, true, false);
            return driver;
        }

        private void EnsureDispatchDriveTask(FuelDeliveryDispatch dispatch, Vector3 targetPosition, int now)
        {
            if (dispatch == null || dispatch.Driver == null || !dispatch.Driver.Exists() || dispatch.Truck == null || !dispatch.Truck.Exists())
            {
                return;
            }

            if (now < dispatch.NextDriveTaskRefreshMs)
            {
                return;
            }

            Function.Call(
                Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                dispatch.Driver.Handle,
                dispatch.Truck.Handle,
                targetPosition.X,
                targetPosition.Y,
                targetPosition.Z,
                20f,
                ServiceDriveStyle,
                ServiceArrivalDistance * 0.5f);
            dispatch.NextDriveTaskRefreshMs = now + ServiceDriveRefreshIntervalMs;
        }

        private static void ClearDispatchDriverTasks(Ped driver)
        {
            if (driver == null || !driver.Exists())
            {
                return;
            }

            Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
        }

        private void BeginDispatchReturn(FuelDeliveryDispatch dispatch, int now)
        {
            if (dispatch == null)
            {
                return;
            }

            dispatch.Phase = FuelDeliveryPhase.Returning;
            dispatch.NextDriveTaskRefreshMs = 0;
            EnsureDispatchDriveTask(dispatch, dispatch.SourceSpawnPosition, now);
        }

        private Blip CreateDispatchBlip(Vehicle truck, OfficeDefinition office, Industry sourceIndustry)
        {
            if (truck == null || !truck.Exists())
            {
                return null;
            }

            var blip = World.CreateBlip(truck.Position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = BlipSprite.Truck;
            blip.Color = BlipColor.Yellow;
            blip.Name = string.Format("Office Fuel Delivery: {0}", office != null ? office.DisplayName : (sourceIndustry != null ? sourceIndustry.Name : "service"));
            blip.Scale = 0.85f;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private void RefreshDispatchBlip(FuelDeliveryDispatch dispatch)
        {
            if (dispatch == null)
            {
                return;
            }

            if (dispatch.Truck == null || !dispatch.Truck.Exists())
            {
                if (dispatch.RouteBlip != null && dispatch.RouteBlip.Exists())
                {
                    dispatch.RouteBlip.Delete();
                }

                dispatch.RouteBlip = null;
                return;
            }

            if (dispatch.RouteBlip == null || !dispatch.RouteBlip.Exists())
            {
                var office = _propertyManager.GetOfficeDefinition(dispatch.OfficeId);
                dispatch.RouteBlip = CreateDispatchBlip(dispatch.Truck, office, dispatch.SourceIndustry);
            }

            if (dispatch.RouteBlip != null && dispatch.RouteBlip.Exists())
            {
                dispatch.RouteBlip.Position = dispatch.Truck.Position;
            }
        }

        private void CancelFuelDelivery(string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _showStatus?.Invoke(reason);
            }

            CleanupFuelDelivery();
        }

        private void CleanupFuelDelivery()
        {
            if (_activeFuelDelivery == null)
            {
                return;
            }

            DeleteBlip(_activeFuelDelivery.RouteBlip);
            DeletePed(_activeFuelDelivery.Driver);
            DeleteVehicle(_activeFuelDelivery.CargoVehicle);
            DeleteVehicle(_activeFuelDelivery.Truck);
            _activeFuelDelivery = null;
        }

        private Prop EnsurePreviewProp(OfficeObjectDefinition definition, bool placementPreview)
        {
            if (definition == null)
            {
                ClearPreviewProp();
                return null;
            }

            if (_previewProp != null && _previewProp.Exists() && _previewDefinitionId == definition.ObjectId && _previewIsPlacement == placementPreview)
            {
                return _previewProp;
            }

            ClearPreviewProp();
            var model = new Model(definition.ModelHash);
            model.Request(500);
            if (!model.IsLoaded)
            {
                return null;
            }

            _previewProp = World.CreateProp(model, Vector3.Zero, true, false);
            if (_previewProp == null || !_previewProp.Exists())
            {
                _previewProp = null;
                return null;
            }

            _previewProp.IsPersistent = true;
            Function.Call(Hash.SET_ENTITY_COLLISION, _previewProp.Handle, false, false);
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, _previewProp.Handle, true);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, _previewProp.Handle, true);
            _previewDefinitionId = definition.ObjectId;
            _previewIsPlacement = placementPreview;
            return _previewProp;
        }

        private void ClearPreviewProp()
        {
            DeleteProp(_previewProp);
            _previewProp = null;
            _previewDefinitionId = 0;
            _previewIsPlacement = false;
        }

        private Prop CreatePlacedProp(OfficeObjectDefinition definition, Vector3 position, Vector3 rotation)
        {
            if (definition == null)
            {
                return null;
            }

            var model = new Model(definition.ModelHash);
            model.Request(500);
            if (!model.IsLoaded)
            {
                return null;
            }

            var prop = World.CreateProp(model, position, true, false);
            if (prop == null || !prop.Exists())
            {
                return null;
            }

            prop.IsPersistent = true;
            prop.Heading = rotation.Z;
            try
            {
                prop.Rotation = rotation;
            }
            catch
            {
                // Heading still covers the intended office placement rotation around the Z axis.
            }

            TryPlacePropOnGround(prop);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, prop.Handle, true);
            return prop;
        }

        private static void ApplyPreviewTransform(Prop prop, Vector3 position, Vector3 rotation)
        {
            if (prop == null || !prop.Exists())
            {
                return;
            }

            prop.Position = position;
            prop.Heading = rotation.Z;
            try
            {
                prop.Rotation = rotation;
            }
            catch
            {
                // Heading still covers the placement rotation around Z.
            }

            Function.Call(Hash.FREEZE_ENTITY_POSITION, prop.Handle, true);
        }

        private void ComputeMenuPreviewTransform(Ped player, OfficeObjectDefinition definition, out Vector3 position, out Vector3 rotation)
        {
            var heading = player != null && player.Exists() ? player.Heading : 0f;
            var forward = GetFlatDirectionFromHeading(heading);
            var right = new Vector3(-forward.Y, forward.X, 0f);
            position = (player != null && player.Exists() ? player.Position : Vector3.Zero)
                + (forward * PreviewForwardDistance)
                + (right * PreviewRightDistance);
            rotation = new Vector3(0f, 0f, NormalizeHeading(heading + 180f));
            position = SnapPlacementHeight(position, definition);
        }

        private bool TryComputePlacementTransform(Ped player, OfficeDefinition office, OfficeObjectDefinition definition, float heading, string instanceId, out Vector3 position, out Vector3 rotation, out string error)
        {
            position = office != null ? office.SpawnPosition : Vector3.Zero;
            rotation = new Vector3(0f, 0f, NormalizeHeading(heading));
            error = string.Empty;

            if (office == null || definition == null)
            {
                error = "No active office placement target is available.";
                return false;
            }

            if (player == null || !player.Exists())
            {
                error = "Player unavailable.";
                return false;
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                error = "Activate this office before placing objects.";
                return false;
            }

            var forward = GetFlatDirectionFromHeading(player.Heading);
            var distance = GetPlacementDistance(definition.Size);
            position = player.Position + (forward * distance);
            position = SnapPlacementHeight(position, definition, office);
            rotation = new Vector3(0f, 0f, NormalizeHeading(heading));

            if (!IsWithinOfficePlacementBounds(position, office))
            {
                error = string.Format("Keep {0} inside the active office yard.", definition.DisplayName);
                return false;
            }

            if (player.Position.DistanceTo(position) <= 0.9f)
            {
                error = "Step back slightly before placing the object.";
                return false;
            }

            var collisionRadius = GetCollisionRadius(definition.Size);
            var officeObjects = _propertyManager.GetOfficeObjects(office.OfficeId, false);
            for (int i = 0; i < officeObjects.Count; i++)
            {
                var other = officeObjects[i];
                if (other == null || string.Equals(other.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherDefinition = _propertyManager.GetOfficeObjectDefinition(other.DefinitionId);
                if (otherDefinition == null)
                {
                    continue;
                }

                var minDistance = collisionRadius + GetCollisionRadius(otherDefinition.Size) + PlacementPadding;
                if (other.Position.DistanceTo(position) < minDistance)
                {
                    error = string.Format("{0} overlaps another office object.", definition.DisplayName);
                    return false;
                }
            }

            return true;
        }

        private OfficeObjectPersistenceEntry GetPlacementEntry(out OfficeDefinition office)
        {
            office = null;
            if (_placement == null)
            {
                return null;
            }

            office = _propertyManager.GetOfficeDefinition(_placement.OfficeId);
            return _propertyManager.GetOfficeObjects(_placement.OfficeId, true)
                .FirstOrDefault(entry => entry != null && string.Equals(entry.InstanceId, _placement.InstanceId, StringComparison.OrdinalIgnoreCase));
        }

        private static float GetPlacementDistance(OfficeObjectSize size)
        {
            switch (size)
            {
                case OfficeObjectSize.Small:
                    return 2.2f;
                case OfficeObjectSize.Big:
                    return 4.2f;
                default:
                    return 3.1f;
            }
        }

        private static float GetCollisionRadius(OfficeObjectSize size)
        {
            switch (size)
            {
                case OfficeObjectSize.Small:
                    return 0.9f;
                case OfficeObjectSize.Big:
                    return 2.6f;
                default:
                    return 1.5f;
            }
        }

        private bool IsWithinOfficePlacementBounds(Vector3 position, OfficeDefinition office)
        {
            if (office == null)
            {
                return false;
            }

            return position.DistanceToSquared(office.SpawnPosition) <= OfficePlacementRadius * OfficePlacementRadius
                || position.DistanceToSquared(office.MarkerPosition) <= OfficePlacementRadius * OfficePlacementRadius;
        }

        private Vector3 SnapPlacementHeight(Vector3 position, OfficeObjectDefinition definition, OfficeDefinition office = null)
        {
            if (definition == null)
            {
                return ResolvePlacementGroundPosition(position, office);
            }

            var model = new Model(definition.ModelHash);
            Vector3 min;
            Vector3 max;
            if (!TryGetModelDimensions(model, out min, out max))
            {
                return ResolvePlacementGroundPosition(position, office);
            }

            var snapped = ResolvePlacementGroundPosition(position, office);
            return snapped + new Vector3(0f, 0f, -min.Z);
        }

        private Vector3 ResolvePlacementGroundPosition(Vector3 position, OfficeDefinition office)
        {
            var snapped = TryGetGroundPosition(position);
            if (office == null)
            {
                return snapped;
            }

            var officeGround = Math.Max(TryGetGroundPosition(office.SpawnPosition).Z, TryGetGroundPosition(office.MarkerPosition).Z);
            if (snapped.Z < officeGround)
            {
                snapped = new Vector3(snapped.X, snapped.Y, officeGround);
            }

            return snapped;
        }

        private Vector3 TryGetGroundPosition(Vector3 position)
        {
            var fallback = _getGroundPosition != null ? _getGroundPosition(position) : position;
            float groundZ;
            if (TryProbeGroundZ(position, out groundZ))
            {
                return new Vector3(position.X, position.Y, groundZ);
            }

            return fallback;
        }

        private static bool TryProbeGroundZ(Vector3 position, out float groundZ)
        {
            groundZ = position.Z;
            var sampleHeights = new[]
            {
                Math.Max(position.Z + 4f, 8f),
                Math.Max(position.Z + 25f, 40f),
                250f,
                1000f,
            };

            for (int i = 0; i < sampleHeights.Length; i++)
            {
                if (TryProbeGroundZAtHeight(position.X, position.Y, sampleHeights[i], out groundZ))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryProbeGroundZAtHeight(float x, float y, float z, out float groundZ)
        {
            groundZ = z;

            var groundArg = new OutputArgument();
            try
            {
                if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, x, y, z, groundArg, false, false))
                {
                    groundZ = groundArg.GetResult<float>();
                    return true;
                }
            }
            catch
            {
                // Fall back to the older native signature below.
            }

            groundArg = new OutputArgument();
            try
            {
                if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, x, y, z, groundArg, false))
                {
                    groundZ = groundArg.GetResult<float>();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryGetModelDimensions(Model model, out Vector3 min, out Vector3 max)
        {
            min = Vector3.Zero;
            max = Vector3.Zero;
            try
            {
                model.GetDimensions(out min, out max);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryPlacePropOnGround(Prop prop)
        {
            if (prop == null || !prop.Exists())
            {
                return;
            }

            try
            {
                Function.Call<bool>(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, prop.Handle);
            }
            catch
            {
                // Keep the computed position if the native fails.
            }
        }

        private void ClearSpawnedProps()
        {
            var keys = _spawnedProps.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                Prop prop;
                if (_spawnedProps.TryGetValue(keys[i], out prop))
                {
                    DeleteProp(prop);
                }
            }

            _spawnedProps.Clear();
        }

        private static Vehicle ResolveTrailerForVehicle(Vehicle currentVehicle)
        {
            if (currentVehicle == null || !currentVehicle.Exists())
            {
                return null;
            }

            var trailer = currentVehicle.TowedVehicle;
            if (trailer != null && trailer.Exists())
            {
                return trailer;
            }

            var trailerHandleArg = new OutputArgument();
            bool hasTrailer;
            try
            {
                hasTrailer = Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, currentVehicle.Handle, trailerHandleArg);
            }
            catch
            {
                return null;
            }

            if (!hasTrailer)
            {
                return null;
            }

            int trailerHandle;
            try
            {
                trailerHandle = trailerHandleArg.GetResult<int>();
            }
            catch
            {
                return null;
            }

            if (trailerHandle != 0)
            {
                return Entity.FromHandle(trailerHandle) as Vehicle;
            }

            return null;
        }

        private static bool IsTruckAttachedToTrailer(Vehicle truck, Vehicle trailer)
        {
            if (truck == null || !truck.Exists() || trailer == null || !trailer.Exists())
            {
                return false;
            }

            var attached = ResolveTrailerForVehicle(truck);
            return attached != null && attached.Exists() && attached.Handle == trailer.Handle;
        }

        private static bool IsPlayerUsingVehicle(Ped player, Vehicle vehicle)
        {
            return player != null
                && player.Exists()
                && vehicle != null
                && vehicle.Exists()
                && player.CurrentVehicle != null
                && player.CurrentVehicle.Exists()
                && player.CurrentVehicle.Handle == vehicle.Handle;
        }

        private static bool IsPlayerUsingAnyVehicle(Ped player)
        {
            return player != null && player.Exists() && player.CurrentVehicle != null && player.CurrentVehicle.Exists();
        }

        private static Vector3 GetFlatDirectionFromHeading(float heading)
        {
            var radians = heading * ((float)Math.PI / 180f);
            var direction = new Vector3(-(float)Math.Sin(radians), (float)Math.Cos(radians), 0f);
            return direction.LengthSquared() <= 0.0001f ? new Vector3(0f, 1f, 0f) : direction.Normalized;
        }

        private static float NormalizeHeading(float heading)
        {
            while (heading < 0f)
            {
                heading += 360f;
            }

            while (heading >= 360f)
            {
                heading -= 360f;
            }

            return heading;
        }

        private static void DeleteProp(Prop prop)
        {
            if (prop == null || !prop.Exists())
            {
                return;
            }

            try
            {
                prop.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void DeleteVehicle(Vehicle vehicle)
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
                // Best-effort cleanup.
            }
        }

        private static void DeletePed(Ped ped)
        {
            if (ped == null || !ped.Exists())
            {
                return;
            }

            try
            {
                ped.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void DeleteBlip(Blip blip)
        {
            if (blip == null || !blip.Exists())
            {
                return;
            }

            try
            {
                blip.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
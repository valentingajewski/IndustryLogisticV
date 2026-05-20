using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class IndustryRefuelService
    {
        private const float ServiceArrivalDistance = 18f;
        private const float VisibleDispatchApproachDistance = 150f;
        private const float VisibleReturnCleanupDistance = 180f;
        private const float OffscreenDispatchSpeedMps = 30f;
        private const int ServiceDriveRefreshIntervalMs = 4000;
        private const int ServiceTimeoutMs = 900000;
        private const int MaterializationRetryIntervalMs = 1500;
        private const int MaxMaterializationAttempts = 6;
        private const int ServiceDriveStyle = 786603;
        private const string DefaultDriverModel = "s_m_m_trucker_01";

        private enum RemoteRefuelDispatchPhase
        {
            OffscreenDelivering = 0,
            VisibleDelivering = 1,
            VisibleReturning = 2,
        }

        private sealed class RemoteRefuelDispatch
        {
            public Industry SourceIndustry { get; set; }

            public Vector3 SourceSpawnPosition { get; set; }

            public int RequestedAtMs { get; set; }

            public int NextMaterializationAttemptMs { get; set; }

            public int NextDriveTaskRefreshMs { get; set; }

            public int MaterializationAttemptCount { get; set; }

            public RemoteRefuelDispatchPhase Phase { get; set; }

            public int TargetVehicleHandle { get; set; }

            public VehicleDefinition TankerTrailerDefinition { get; set; }

            public VehicleDefinition TractorDefinition { get; set; }

            public Ped Driver { get; set; }

            public Vehicle Truck { get; set; }

            public Vehicle CargoVehicle { get; set; }

            public Blip RouteBlip { get; set; }

            public Vector3? ReturnCleanupAnchorPosition { get; set; }
        }

        private readonly FleetManager _fleetManager;
        private readonly VehicleFuelSystem _vehicleFuelSystem;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Func<float> _getProfit;
        private readonly Action<float> _deductProfit;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Action<string> _showStatus;
        private readonly float _interactionDistance;
        private RemoteRefuelDispatch _activeDispatch;

        public IndustryRefuelService(
            FleetManager fleetManager,
            VehicleFuelSystem vehicleFuelSystem,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Func<float> getProfit,
            Action<float> deductProfit,
            CompanyFinanceTracker financeTracker,
            Func<int> getCurrentInGameMinute,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<Vector3, Vector3> getGroundPosition,
            Action<string> showStatus,
            float interactionDistance)
        {
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _vehicleFuelSystem = vehicleFuelSystem ?? throw new ArgumentNullException(nameof(vehicleFuelSystem));
            _industryManager = industryManager ?? throw new ArgumentNullException(nameof(industryManager));
            _globalMarket = globalMarket ?? throw new ArgumentNullException(nameof(globalMarket));
            _getProfit = getProfit;
            _deductProfit = deductProfit;
            _financeTracker = financeTracker;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _getIndustryMarkerPosition = getIndustryMarkerPosition ?? throw new ArgumentNullException(nameof(getIndustryMarkerPosition));
            _getGroundPosition = getGroundPosition;
            _showStatus = showStatus;
            _interactionDistance = interactionDistance;
            _activeDispatch = null;
        }

        public bool HasActiveDispatch
        {
            get { return _activeDispatch != null; }
        }

        public bool TryRefuel(Industry industry, Ped player, out string message)
        {
            message = string.Empty;

            Vehicle cargoVehicle;
            Vehicle poweredVehicle;
            VehicleFuelTelemetry fuelTelemetry;
            if (!TryGetRefuelContext(industry, player, out cargoVehicle, out poweredVehicle, out fuelTelemetry, out message))
            {
                return false;
            }

            return TryRefuelVehicle(industry, poweredVehicle, cargoVehicle, fuelTelemetry, false, out message);
        }

        public bool TryRequestRemoteRefuel(Ped player, int now, out string message)
        {
            message = string.Empty;

            if (_activeDispatch != null)
            {
                message = "Refuel tanker already en route.";
                return false;
            }

            VehicleFuelTelemetry fuelTelemetry;
            if (!TryGetRemoteRefuelTarget(player, out fuelTelemetry, out message))
            {
                return false;
            }

            Industry sourceIndustry;
            if (!TryFindNearestGasStation(fuelTelemetry.PoweredVehicle.Position, out sourceIndustry, out message))
            {
                return false;
            }

            VehicleDefinition tankerTrailer;
            VehicleDefinition tractorDefinition;
            if (!TryResolveServiceRig(out tankerTrailer, out tractorDefinition, out message))
            {
                return false;
            }

            _activeDispatch = new RemoteRefuelDispatch
            {
                SourceIndustry = sourceIndustry,
                SourceSpawnPosition = ResolveDispatchSpawn(sourceIndustry, fuelTelemetry.PoweredVehicle.Position),
                RequestedAtMs = now,
                NextMaterializationAttemptMs = now,
                NextDriveTaskRefreshMs = 0,
                MaterializationAttemptCount = 0,
                Phase = RemoteRefuelDispatchPhase.OffscreenDelivering,
                TargetVehicleHandle = fuelTelemetry.PoweredVehicle.Handle,
                TankerTrailerDefinition = tankerTrailer,
                TractorDefinition = tractorDefinition,
                ReturnCleanupAnchorPosition = null,
            };

            if (HasCompletedOffscreenTravel(_activeDispatch, fuelTelemetry.PoweredVehicle.Position, now))
            {
                if (!TryMaterializeDispatch(_activeDispatch, fuelTelemetry.PoweredVehicle.Position, now))
                {
                    _activeDispatch.MaterializationAttemptCount++;
                    _activeDispatch.NextMaterializationAttemptMs = now + MaterializationRetryIntervalMs;
                }
            }

            message = string.Format("Refuel tanker dispatched from {0}.", sourceIndustry.Name);
            return true;
        }

        public bool Update(int now)
        {
            if (_activeDispatch == null)
            {
                return false;
            }

            if (now - _activeDispatch.RequestedAtMs >= ServiceTimeoutMs)
            {
                CancelActiveDispatch("Refuel tanker timed out before completing the dispatch.");
                return false;
            }

            var targetVehicle = Entity.FromHandle(_activeDispatch.TargetVehicleHandle) as Vehicle;
            if ((_activeDispatch.Phase == RemoteRefuelDispatchPhase.OffscreenDelivering
                    || _activeDispatch.Phase == RemoteRefuelDispatchPhase.VisibleDelivering)
                && (targetVehicle == null || !targetVehicle.Exists()))
            {
                CancelActiveDispatch("Refuel tanker cancelled because the target vehicle is no longer available.");
                return false;
            }

            if (_activeDispatch.Phase == RemoteRefuelDispatchPhase.OffscreenDelivering)
            {
                if (!HasCompletedOffscreenTravel(_activeDispatch, targetVehicle.Position, now)
                    || now < _activeDispatch.NextMaterializationAttemptMs)
                {
                    return false;
                }

                if (TryMaterializeDispatch(_activeDispatch, targetVehicle.Position, now))
                {
                    return false;
                }

                _activeDispatch.MaterializationAttemptCount++;
                if (_activeDispatch.MaterializationAttemptCount >= MaxMaterializationAttempts)
                {
                    CancelActiveDispatch("Refuel tanker failed before reaching the target.");
                    return false;
                }

                _activeDispatch.NextMaterializationAttemptMs = now + MaterializationRetryIntervalMs;
                return false;
            }

            if (_activeDispatch.Driver == null || !_activeDispatch.Driver.Exists() || _activeDispatch.Truck == null || !_activeDispatch.Truck.Exists())
            {
                CancelActiveDispatch("Refuel tanker failed before reaching the target.");
                return false;
            }

            RefreshDispatchBlip(_activeDispatch);

            if (_activeDispatch.Phase == RemoteRefuelDispatchPhase.VisibleReturning)
            {
                if (_activeDispatch.Truck.Position.DistanceTo(_activeDispatch.SourceSpawnPosition) <= ServiceArrivalDistance
                    || (_activeDispatch.ReturnCleanupAnchorPosition.HasValue
                        && _activeDispatch.Truck.Position.DistanceTo(_activeDispatch.ReturnCleanupAnchorPosition.Value) >= VisibleReturnCleanupDistance))
                {
                    CleanupDispatch();
                    return false;
                }

                EnsureDispatchDriveTask(_activeDispatch, _activeDispatch.SourceSpawnPosition, now);
                return false;
            }

            if (_activeDispatch.Truck.Position.DistanceTo(targetVehicle.Position) <= ServiceArrivalDistance)
            {
                ClearDispatchDriverTasks(_activeDispatch.Driver);

                _vehicleFuelSystem.EnsureTrackedVehicle(targetVehicle);
                var fuelTelemetry = _vehicleFuelSystem.GetTelemetry(targetVehicle);
                var message = string.Empty;
                var changed = fuelTelemetry != null && TryRefuelVehicle(_activeDispatch.SourceIndustry, targetVehicle, null, fuelTelemetry, true, out message);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _showStatus?.Invoke(changed
                        ? string.Format("Tanker service completed: {0}", message)
                        : string.Format("Tanker service failed: {0}", message));
                }

                    BeginDispatchReturn(_activeDispatch, targetVehicle.Position, now);
                return changed;
            }

            EnsureDispatchDriveTask(_activeDispatch, targetVehicle.Position, now);
            return false;
        }

        public void CancelActiveDispatch(string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _showStatus?.Invoke(reason);
            }

            CleanupDispatch();
        }

        private bool TryRefuelVehicle(Industry industry, Vehicle poweredVehicle, Vehicle cargoVehicle, VehicleFuelTelemetry fuelTelemetry, bool isRemoteService, out string message)
        {
            message = string.Empty;

            var litersNeeded = Math.Max(0f, fuelTelemetry.CapacityLiters - fuelTelemetry.CurrentLiters);
            if (litersNeeded <= 0.05f)
            {
                message = string.Format("Fuel tank already full ({0:0}/{1:0}L).", fuelTelemetry.CurrentLiters, fuelTelemetry.CapacityLiters);
                return false;
            }

            var availableStockLiters = Math.Max(0f, industry.GetStock("Fuel") * 1000f);
            if (availableStockLiters <= 0.05f)
            {
                message = "Station out of fuel.";
                return false;
            }

            var currentProfit = _getProfit != null ? _getProfit() : 0f;
            var pricePerTon = Math.Max(0f, _globalMarket.GetUnitPrice("Fuel"));
            var affordableLiters = industry.RefuelIsFree || pricePerTon <= 0.001f
                ? litersNeeded
                : Math.Max(0f, (currentProfit / pricePerTon) * 1000f);

            if (!industry.RefuelIsFree && affordableLiters <= 0.05f)
            {
                message = "Insufficient funds to refuel.";
                return false;
            }

            var litersToDispense = Math.Min(litersNeeded, availableStockLiters);
            if (!industry.RefuelIsFree)
            {
                litersToDispense = Math.Min(litersToDispense, affordableLiters);
            }

            if (litersToDispense <= 0.05f)
            {
                message = industry.RefuelIsFree ? "Station out of fuel." : "Insufficient funds to refuel.";
                return false;
            }

            float dispensedLiters;
            if (!_industryManager.TryDispenseFuel(industry, litersToDispense, out dispensedLiters) || dispensedLiters <= 0.05f)
            {
                message = "Station out of fuel.";
                return false;
            }

            var addedLiters = _vehicleFuelSystem.AddFuel(poweredVehicle, dispensedLiters);
            if (addedLiters <= 0.05f)
            {
                industry.AddInput("Fuel", dispensedLiters / 1000f);
                message = "Fuel tank already full.";
                return false;
            }

            var pricePaid = industry.RefuelIsFree ? 0f : _industryManager.ComputeFuelRefillPrice(addedLiters, _globalMarket);
            if (pricePaid > 0f && _deductProfit != null)
            {
                _deductProfit(pricePaid);
                RecordFinanceExpense(
                    CompanyFinanceCategory.FuelPurchase,
                    pricePaid,
                    string.Format(
                        "{0} refuel at {1}",
                        isRemoteService ? "Remote" : "Vehicle",
                        industry != null ? industry.Name : "station"));
            }

            var resultingTank = Math.Min(fuelTelemetry.CapacityLiters, fuelTelemetry.CurrentLiters + addedLiters);
            var notes = new List<string>();
            if (availableStockLiters + 0.05f < litersNeeded)
            {
                notes.Add("station out of fuel");
            }

            if (!industry.RefuelIsFree && affordableLiters + 0.05f < litersNeeded)
            {
                notes.Add("insufficient funds");
            }

            var priceText = industry.RefuelIsFree
                ? "Free at office station"
                : string.Format("Paid {0}", ModFormatting.FormatMoney(pricePaid));
            var noteText = notes.Count > 0
                ? string.Format(" | {0}", string.Join(" | ", notes))
                : string.Empty;

            message = string.Format(
                "Refueled {0:0}L. Tank {1:0}/{2:0}L | {3}{4}",
                addedLiters,
                resultingTank,
                fuelTelemetry.CapacityLiters,
                priceText,
                noteText);
            return true;
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

        private bool TryGetRefuelContext(Industry industry, Ped player, out Vehicle cargoVehicle, out Vehicle poweredVehicle, out VehicleFuelTelemetry fuelTelemetry, out string error)
        {
            cargoVehicle = null;
            poweredVehicle = null;
            fuelTelemetry = null;
            error = string.Empty;

            if (industry == null)
            {
                error = "No target industry.";
                return false;
            }

            if (!industry.IsGasStation)
            {
                error = "Refueling is only available at petrol stations.";
                return false;
            }

            if (player == null || !player.Exists())
            {
                error = "Player unavailable.";
                return false;
            }

            if (player.Position.DistanceTo(_getIndustryMarkerPosition(industry)) > _interactionDistance + 1.2f)
            {
                error = "Move closer to the petrol station marker.";
                return false;
            }

            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || poweredVehicle == null
                || !poweredVehicle.Exists())
            {
                error = "Bring a powered cargo vehicle close to the petrol station.";
                return false;
            }

            _vehicleFuelSystem.EnsureTrackedVehicle(poweredVehicle);
            fuelTelemetry = _vehicleFuelSystem.GetTelemetry(poweredVehicle, cargoVehicle);
            if (fuelTelemetry == null || fuelTelemetry.CapacityLiters <= 0.001f)
            {
                error = "No powered cargo vehicle with a fuel tank is in range.";
                return false;
            }

            return true;
        }

        private bool TryGetRemoteRefuelTarget(Ped player, out VehicleFuelTelemetry fuelTelemetry, out string error)
        {
            fuelTelemetry = null;
            error = string.Empty;

            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                error = "Enter a company vehicle to request refueling.";
                return false;
            }

            fuelTelemetry = _vehicleFuelSystem.GetActiveTelemetry(player);
            if (fuelTelemetry == null || fuelTelemetry.PoweredVehicle == null || !fuelTelemetry.PoweredVehicle.Exists() || fuelTelemetry.CapacityLiters <= 0.001f)
            {
                error = "No active company truck is available for remote refueling.";
                return false;
            }

            if (Math.Max(0f, fuelTelemetry.CapacityLiters - fuelTelemetry.CurrentLiters) <= 0.01f)
            {
                error = "The active company truck is already full.";
                return false;
            }

            return true;
        }

        private bool TryFindNearestGasStation(Vector3 targetPosition, out Industry sourceIndustry, out string error)
        {
            sourceIndustry = null;
            error = string.Empty;

            sourceIndustry = _industryManager.Industries
                .Where(industry => industry != null && industry.IsGasStation && industry.GetStock("Fuel") > 0.001f)
                .OrderBy(industry => industry.Position.DistanceTo(targetPosition))
                .FirstOrDefault();
            if (sourceIndustry == null)
            {
                error = "No stocked gas station is available for tanker dispatch.";
                return false;
            }

            return true;
        }

        private bool TryResolveServiceRig(out VehicleDefinition tankerTrailer, out VehicleDefinition tractorDefinition, out string error)
        {
            tankerTrailer = _fleetManager
                .GetSpawnableForCommodity("Fuel")
                .Where(definition => definition.IsTrailer)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();
            tractorDefinition = _fleetManager
                .GetTractorDefinitions()
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();

            if (tankerTrailer == null || tractorDefinition == null)
            {
                error = "No truck + tanker trailer setup is configured for fuel service.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Vector3 ResolveDispatchSpawn(Industry sourceIndustry, Vector3 targetPosition)
        {
            var spawnPosition = sourceIndustry != null && sourceIndustry.VehicleSpawnPosition.HasValue
                ? sourceIndustry.VehicleSpawnPosition.Value
                : (sourceIndustry != null ? sourceIndustry.Position : targetPosition);
            if (_getGroundPosition != null)
            {
                spawnPosition = _getGroundPosition(spawnPosition);
            }

            return spawnPosition;
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

        private void EnsureDispatchDriveTask(RemoteRefuelDispatch dispatch, Vector3 targetPosition, int now)
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

        private void ClearDispatchDriverTasks(Ped driver)
        {
            if (driver == null || !driver.Exists())
            {
                return;
            }

            Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
        }

        private bool HasCompletedOffscreenTravel(RemoteRefuelDispatch dispatch, Vector3 targetPosition, int now)
        {
            if (dispatch == null)
            {
                return false;
            }

            var offscreenDistance = Math.Max(0f, dispatch.SourceSpawnPosition.DistanceTo(targetPosition) - VisibleDispatchApproachDistance);
            var requiredTravelMs = OffscreenDispatchSpeedMps <= 0.01f
                ? 0f
                : (offscreenDistance / OffscreenDispatchSpeedMps) * 1000f;
            return now - dispatch.RequestedAtMs >= requiredTravelMs;
        }

        private bool TryMaterializeDispatch(RemoteRefuelDispatch dispatch, Vector3 targetPosition, int now)
        {
            if (dispatch == null || dispatch.TankerTrailerDefinition == null || dispatch.TractorDefinition == null)
            {
                return false;
            }

            Vector3 spawnPosition;
            float spawnHeading;
            ResolveVisibleDispatchSpawn(dispatch, targetPosition, dispatch.MaterializationAttemptCount, out spawnPosition, out spawnHeading);

            Vehicle truck;
            Vehicle cargoVehicle;
            string message;
            if (!_fleetManager.SpawnSelectedVehicle(
                dispatch.TankerTrailerDefinition,
                dispatch.TractorDefinition,
                spawnPosition,
                spawnHeading,
                out truck,
                out cargoVehicle,
                out message))
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

                return false;
            }

            dispatch.Driver = driver;
            dispatch.Truck = truck;
            dispatch.CargoVehicle = cargoVehicle;
            dispatch.RouteBlip = CreateDispatchBlip(truck, dispatch.SourceIndustry);
            dispatch.ReturnCleanupAnchorPosition = null;
            dispatch.NextDriveTaskRefreshMs = 0;
            dispatch.NextMaterializationAttemptMs = 0;
            dispatch.Phase = RemoteRefuelDispatchPhase.VisibleDelivering;
            EnsureDispatchDriveTask(dispatch, targetPosition, now);
            return true;
        }

        private void ResolveVisibleDispatchSpawn(RemoteRefuelDispatch dispatch, Vector3 targetPosition, int recoveryAttempt, out Vector3 spawnPosition, out float spawnHeading)
        {
            var sourcePosition = dispatch != null ? dispatch.SourceSpawnPosition : targetPosition;
            var approachDirection = targetPosition - sourcePosition;
            if (approachDirection.LengthSquared() <= 0.001f)
            {
                approachDirection = new Vector3(1f, 0f, 0f);
            }

            approachDirection.Normalize();
            var lateralDirection = new Vector3(-approachDirection.Y, approachDirection.X, 0f);
            var attempt = Math.Max(0, recoveryAttempt);
            var lateralPattern = attempt % 5;
            var lateralOffset = lateralPattern == 1
                ? 24f
                : lateralPattern == 2
                    ? -24f
                    : lateralPattern == 3
                        ? 42f
                        : lateralPattern == 4
                            ? -42f
                            : 0f;
            var approachDistance = VisibleDispatchApproachDistance + (attempt * 18f);
            var seedPosition = targetPosition - (approachDirection * approachDistance) + (lateralDirection * lateralOffset);
            spawnPosition = ResolveRoadSafePosition(seedPosition, targetPosition, attempt);

            var headingDelta = targetPosition - spawnPosition;
            spawnHeading = headingDelta.LengthSquared() <= 0.001f
                ? 0f
                : Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, headingDelta.X, headingDelta.Y);
        }

        private Vector3 ResolveRoadSafePosition(Vector3 seedPosition, Vector3 referencePosition, int recoveryAttempt)
        {
            Vector3 roadPosition;
            if (TryGetClosestVehicleNode(seedPosition, out roadPosition))
            {
                return roadPosition;
            }

            var direction = seedPosition - referencePosition;
            if (direction.LengthSquared() <= 0.001f)
            {
                direction = new Vector3(1f, 0f, 0f);
            }

            direction.Normalize();
            var offsetDistance = 16f + (Math.Max(0, recoveryAttempt) * 4f);
            return SnapRoutePosition(seedPosition - (direction * offsetDistance));
        }

        private bool TryGetClosestVehicleNode(Vector3 seedPosition, out Vector3 roadPosition)
        {
            roadPosition = Vector3.Zero;
            var nodeArg = new OutputArgument();
            try
            {
                if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE, seedPosition.X, seedPosition.Y, seedPosition.Z, nodeArg, 1, 3f, 0f))
                {
                    roadPosition = SnapRoutePosition(nodeArg.GetResult<Vector3>());
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private Vector3 SnapRoutePosition(Vector3 position)
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
            }

            return false;
        }

        private void BeginDispatchReturn(RemoteRefuelDispatch dispatch, Vector3 cleanupAnchorPosition, int now)
        {
            if (dispatch == null)
            {
                return;
            }

            dispatch.Phase = RemoteRefuelDispatchPhase.VisibleReturning;
            dispatch.ReturnCleanupAnchorPosition = cleanupAnchorPosition;
            dispatch.NextDriveTaskRefreshMs = 0;
            EnsureDispatchDriveTask(dispatch, dispatch.SourceSpawnPosition, now);
        }

        private Blip CreateDispatchBlip(Vehicle truck, Industry sourceIndustry)
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
            blip.Name = string.Format("Refuel Tanker: {0}", sourceIndustry != null ? sourceIndustry.Name : "service");
            blip.Scale = 0.85f;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private void RefreshDispatchBlip(RemoteRefuelDispatch dispatch)
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
                dispatch.RouteBlip = CreateDispatchBlip(dispatch.Truck, dispatch.SourceIndustry);
            }

            if (dispatch.RouteBlip != null && dispatch.RouteBlip.Exists())
            {
                dispatch.RouteBlip.Position = dispatch.Truck.Position;
            }
        }

        private void CleanupDispatch()
        {
            if (_activeDispatch == null)
            {
                return;
            }

            if (_activeDispatch.RouteBlip != null && _activeDispatch.RouteBlip.Exists())
            {
                _activeDispatch.RouteBlip.Delete();
            }

            if (_activeDispatch.Driver != null && _activeDispatch.Driver.Exists())
            {
                _activeDispatch.Driver.Delete();
            }

            if (_activeDispatch.CargoVehicle != null && _activeDispatch.CargoVehicle.Exists())
            {
                _activeDispatch.CargoVehicle.Delete();
            }

            if (_activeDispatch.Truck != null && _activeDispatch.Truck.Exists() && (_activeDispatch.CargoVehicle == null || _activeDispatch.Truck.Handle != _activeDispatch.CargoVehicle.Handle))
            {
                _activeDispatch.Truck.Delete();
            }

            _activeDispatch = null;
        }
    }
}
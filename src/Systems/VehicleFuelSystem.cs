using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class VehicleFuelTelemetry
    {
        public Vehicle PoweredVehicle { get; set; }

        public Vehicle CargoVehicle { get; set; }

        public VehicleDefinition PoweredVehicleDefinition { get; set; }

        public float CurrentLiters { get; set; }

        public float CapacityLiters { get; set; }

        public float FuelRatio { get; set; }

        public bool IsOutOfFuel { get; set; }

        public bool HasRangeEstimate { get; set; }

        public float EstimatedRangeMeters { get; set; }

        public float SmoothedLitersPerMeter { get; set; }

        public bool UsesSeparatePoweredVehicle
        {
            get
            {
                return PoweredVehicle != null
                    && PoweredVehicle.Exists()
                    && CargoVehicle != null
                    && CargoVehicle.Exists()
                    && PoweredVehicle.Handle != CargoVehicle.Handle;
            }
        }
    }

    public sealed class VehicleFuelSystem
    {
        private const float BaseThrottleConsumptionLitersPerSecond = 0.09f;
        private const float ReverseThrottleMultiplier = 0.9f;
        private const float TractorConsumptionMultiplier = 1.35f;
        private const float HeavyTruckConsumptionMultiplier = 1.15f;
        private const float MinimumThrottleInput = 0.04f;

        private readonly FleetManager _fleetManager;
        private readonly Action<string> _showStatus;
        private readonly Dictionary<int, VehicleFuelState> _fuelStates;

        private int _lastUpdateMs;

        public VehicleFuelSystem(FleetManager fleetManager, Action<string> showStatus)
        {
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _showStatus = showStatus;
            _fuelStates = new Dictionary<int, VehicleFuelState>();
            _lastUpdateMs = int.MinValue;
        }

        public bool DifficultyEnabled { get; private set; }

        public void SetDifficultyEnabled(bool enabled)
        {
            if (DifficultyEnabled == enabled)
            {
                return;
            }

            DifficultyEnabled = enabled;
            if (!enabled)
            {
                RestoreAllVehiclePower();
            }
        }

        public void InitializeSpawnedVehicle(Vehicle poweredVehicle, float? currentFuelLiters = null)
        {
            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return;
            }

            var resolvedFuelLiters = currentFuelLiters.HasValue
                ? Math.Max(0f, Math.Min(state.CapacityLiters, currentFuelLiters.Value))
                : state.CapacityLiters;
            state.CurrentFuelLiters = resolvedFuelLiters;
            state.OutOfFuelMessageShown = false;
            state.PowerCutApplied = false;
            ResetRangeTracking(state, poweredVehicle);
            ApplyPropulsionState(poweredVehicle, state);
        }

        public void EnsureTrackedVehicle(Vehicle poweredVehicle, float? currentFuelLiters = null)
        {
            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return;
            }

            if (currentFuelLiters.HasValue)
            {
                state.CurrentFuelLiters = Math.Max(0f, Math.Min(state.CapacityLiters, currentFuelLiters.Value));
                state.OutOfFuelMessageShown = state.CurrentFuelLiters <= 0.001f;

                if (poweredVehicle != null && poweredVehicle.Exists())
                {
                    state.LastObservedPosition = poweredVehicle.Position;
                    state.HasLastObservedPosition = true;
                }
            }
            else if (!state.HasLastObservedPosition && poweredVehicle != null && poweredVehicle.Exists())
            {
                state.LastObservedPosition = poweredVehicle.Position;
                state.HasLastObservedPosition = true;
            }

            ApplyPropulsionState(poweredVehicle, state);
        }

        public bool Update(Ped player, int gameTimeMs)
        {
            if (gameTimeMs < 0)
            {
                return false;
            }

            if (_lastUpdateMs == int.MinValue)
            {
                _lastUpdateMs = gameTimeMs;
                return false;
            }

            var deltaSeconds = Math.Max(0f, (gameTimeMs - _lastUpdateMs) / 1000f);
            _lastUpdateMs = gameTimeMs;

            if (player == null || !player.Exists())
            {
                return false;
            }

            Vehicle cargoVehicle;
            Vehicle poweredVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle))
            {
                return false;
            }

            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return false;
            }

            if (!DifficultyEnabled)
            {
                ApplyPropulsionState(poweredVehicle, state);
                return false;
            }

            var changed = false;
            var currentPosition = poweredVehicle.Position;
            var distanceMeters = GetDistanceSinceLastObservation(state, currentPosition);
            if (player.CurrentVehicle != null
                && player.CurrentVehicle.Exists()
                && player.CurrentVehicle.Handle == poweredVehicle.Handle
                && deltaSeconds > 0f)
            {
                var throttleInput = ResolveThrottleInput(poweredVehicle);
                if (throttleInput > MinimumThrottleInput)
                {
                    var before = state.CurrentFuelLiters;
                    var burnLiters = throttleInput * GetConsumptionMultiplier(state) * BaseThrottleConsumptionLitersPerSecond * deltaSeconds;
                    if (burnLiters > 0f)
                    {
                        state.CurrentFuelLiters = Math.Max(0f, state.CurrentFuelLiters - burnLiters);
                        var consumedLiters = Math.Max(0f, before - state.CurrentFuelLiters);
                        state.RangeEstimator.AddObservation(distanceMeters, consumedLiters, Math.Max(0f, poweredVehicle.Speed));
                        changed = consumedLiters > 0.001f;
                    }
                }
            }

            if (state.CurrentFuelLiters <= 0.001f)
            {
                state.CurrentFuelLiters = 0f;
                if (!state.OutOfFuelMessageShown)
                {
                    _showStatus?.Invoke("Truck out of fuel. Refuel at a petrol station.");
                    state.OutOfFuelMessageShown = true;
                }
            }

            ApplyPropulsionState(poweredVehicle, state);
            state.LastObservedPosition = currentPosition;
            state.HasLastObservedPosition = true;
            return changed;
        }

        public VehicleFuelTelemetry GetTelemetry(Vehicle poweredVehicle, Vehicle cargoVehicle = null)
        {
            var state = GetState(poweredVehicle);
            if (state == null)
            {
                return null;
            }

            var isOutOfFuel = state.CurrentFuelLiters <= 0.001f;
            var hasRangeEstimate = isOutOfFuel || state.RangeEstimator.HasEstimate;

            return new VehicleFuelTelemetry
            {
                PoweredVehicle = poweredVehicle,
                CargoVehicle = cargoVehicle,
                PoweredVehicleDefinition = state.Definition,
                CurrentLiters = state.CurrentFuelLiters,
                CapacityLiters = state.CapacityLiters,
                FuelRatio = state.CapacityLiters <= 0.001f
                    ? 0f
                    : ModMath.Clamp01(state.CurrentFuelLiters / state.CapacityLiters),
                IsOutOfFuel = isOutOfFuel,
                HasRangeEstimate = hasRangeEstimate,
                EstimatedRangeMeters = isOutOfFuel
                    ? 0f
                    : state.RangeEstimator.EstimateRemainingRangeMeters(state.CurrentFuelLiters),
                SmoothedLitersPerMeter = state.RangeEstimator.SmoothedLitersPerMeter,
            };
        }

        public VehicleFuelTelemetry GetActiveTelemetry(Ped player)
        {
            Vehicle cargoVehicle;
            var poweredVehicle = _fleetManager.ResolvePoweredVehicle(player, out cargoVehicle);
            EnsureTrackedVehicle(poweredVehicle);
            return GetTelemetry(poweredVehicle, cargoVehicle);
        }

        public float AddFuel(Vehicle poweredVehicle, float liters)
        {
            var state = GetState(poweredVehicle);
            if (state == null || liters <= 0f)
            {
                return 0f;
            }

            var availableSpace = Math.Max(0f, state.CapacityLiters - state.CurrentFuelLiters);
            if (availableSpace <= 0.001f)
            {
                return 0f;
            }

            var addedLiters = Math.Min(availableSpace, liters);
            state.CurrentFuelLiters += addedLiters;
            state.OutOfFuelMessageShown = false;
            if (poweredVehicle != null && poweredVehicle.Exists())
            {
                try
                {
                    SetEngineState(poweredVehicle, true);
                }
                catch
                {
                    // Engine restart is a best-effort quality-of-life step after refueling.
                }
            }

            ApplyPropulsionState(poweredVehicle, state);
            return addedLiters;
        }

        public float SetFuelLiters(Vehicle poweredVehicle, float liters)
        {
            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return 0f;
            }

            var clampedLiters = Math.Max(0f, Math.Min(state.CapacityLiters, liters));
            state.CurrentFuelLiters = clampedLiters;
            state.OutOfFuelMessageShown = clampedLiters <= 0.001f;

            if (poweredVehicle != null && poweredVehicle.Exists())
            {
                state.LastObservedPosition = poweredVehicle.Position;
                state.HasLastObservedPosition = true;
            }

            ApplyPropulsionState(poweredVehicle, state);
            return state.CurrentFuelLiters;
        }

        public void CleanupStates()
        {
            var removeHandles = new List<int>();
            foreach (var pair in _fuelStates)
            {
                var vehicle = Entity.FromHandle(pair.Key) as Vehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    removeHandles.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeHandles.Count; i++)
            {
                _fuelStates.Remove(removeHandles[i]);
            }
        }

        public void ClearAllStates()
        {
            RestoreAllVehiclePower();
            _fuelStates.Clear();
            _lastUpdateMs = int.MinValue;
        }

        private VehicleFuelState GetOrCreateState(Vehicle poweredVehicle)
        {
            if (poweredVehicle == null || !poweredVehicle.Exists())
            {
                return null;
            }

            VehicleFuelState state;
            if (_fuelStates.TryGetValue(poweredVehicle.Handle, out state))
            {
                return state;
            }

            var definition = _fleetManager.FindDefinition(poweredVehicle.Model);
            if (definition == null || definition.IsTrailer || definition.FuelCapacityLiters <= 0.001f)
            {
                return null;
            }

            state = new VehicleFuelState
            {
                Definition = definition,
                CapacityLiters = Math.Max(0f, definition.FuelCapacityLiters),
                CurrentFuelLiters = Math.Max(0f, definition.FuelCapacityLiters),
            };

            state.LastObservedPosition = poweredVehicle.Position;
            state.HasLastObservedPosition = true;

            _fuelStates[poweredVehicle.Handle] = state;
            return state;
        }

        private static float GetDistanceSinceLastObservation(VehicleFuelState state, Vector3 currentPosition)
        {
            if (state == null)
            {
                return 0f;
            }

            if (!state.HasLastObservedPosition)
            {
                state.LastObservedPosition = currentPosition;
                state.HasLastObservedPosition = true;
                return 0f;
            }

            return state.LastObservedPosition.DistanceTo(currentPosition);
        }

        private static void ResetRangeTracking(VehicleFuelState state, Vehicle poweredVehicle)
        {
            if (state == null)
            {
                return;
            }

            state.RangeEstimator.Reset();
            if (poweredVehicle != null && poweredVehicle.Exists())
            {
                state.LastObservedPosition = poweredVehicle.Position;
                state.HasLastObservedPosition = true;
                return;
            }

            state.LastObservedPosition = Vector3.Zero;
            state.HasLastObservedPosition = false;
        }

        private VehicleFuelState GetState(Vehicle poweredVehicle)
        {
            if (poweredVehicle == null || !poweredVehicle.Exists())
            {
                return null;
            }

            VehicleFuelState state;
            return _fuelStates.TryGetValue(poweredVehicle.Handle, out state)
                ? state
                : null;
        }

        private static float ResolveThrottleInput(Vehicle poweredVehicle)
        {
            if (poweredVehicle == null || !poweredVehicle.Exists())
            {
                return 0f;
            }

            var acceleratorInput = GetControlNormal(71);
            if (acceleratorInput > MinimumThrottleInput)
            {
                return acceleratorInput;
            }

            var reverseInput = GetControlNormal(72);
            if (reverseInput <= MinimumThrottleInput)
            {
                return 0f;
            }

            if (GetForwardSpeed(poweredVehicle) <= 0.5f)
            {
                return reverseInput * ReverseThrottleMultiplier;
            }

            return 0f;
        }

        private static float GetControlNormal(int controlId)
        {
            try
            {
                return Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, controlId);
            }
            catch
            {
                return 0f;
            }
        }

        private static float GetForwardSpeed(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return 0f;
            }

            try
            {
                var velocity = vehicle.Velocity;
                var forward = vehicle.ForwardVector;
                return (velocity.X * forward.X) + (velocity.Y * forward.Y) + (velocity.Z * forward.Z);
            }
            catch
            {
                return vehicle.Speed;
            }
        }

        private static float GetConsumptionMultiplier(VehicleFuelState state)
        {
            if (state == null || state.Definition == null)
            {
                return 1f;
            }

            if (state.Definition.IsTractor)
            {
                return TractorConsumptionMultiplier;
            }

            return state.Definition.CapacityTons >= 18f
                ? HeavyTruckConsumptionMultiplier
                : 1f;
        }

        private void ApplyPropulsionState(Vehicle poweredVehicle, VehicleFuelState state)
        {
            if (poweredVehicle == null || !poweredVehicle.Exists() || state == null)
            {
                return;
            }

            var shouldCutPower = DifficultyEnabled && state.CurrentFuelLiters <= 0.001f;
            if (!shouldCutPower && !state.PowerCutApplied)
            {
                return;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, poweredVehicle.Handle, shouldCutPower);
                SetEngineState(poweredVehicle, !shouldCutPower);
                state.PowerCutApplied = shouldCutPower;
            }
            catch
            {
                state.PowerCutApplied = shouldCutPower;
            }
        }

        private static void SetEngineState(Vehicle vehicle, bool enabled)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, enabled, true, true);
        }

        private void RestoreAllVehiclePower()
        {
            foreach (var pair in _fuelStates)
            {
                var vehicle = Entity.FromHandle(pair.Key) as Vehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    continue;
                }

                ApplyPropulsionState(vehicle, pair.Value);
            }
        }

        private sealed class VehicleFuelState
        {
            public VehicleDefinition Definition { get; set; }

            public float CapacityLiters { get; set; }

            public float CurrentFuelLiters { get; set; }

            public bool OutOfFuelMessageShown { get; set; }

            public bool PowerCutApplied { get; set; }

            public Vector3 LastObservedPosition { get; set; }

            public bool HasLastObservedPosition { get; set; }

            public VehicleFuelRangeEstimator RangeEstimator { get; } = new VehicleFuelRangeEstimator();
        }
    }

    internal sealed class VehicleFuelRangeEstimator
    {
        private const int MaxCommittedObservationCount = 24;
        internal const float MinimumObservationSpeedMetersPerSecond = 2.5f;
        internal const float MinimumCommittedObservationDistanceMeters = 20f;
        internal const float MinimumCommittedObservationFuelLiters = 0.02f;
        internal const float MinimumCalibrationDistanceMeters = 100f;
        internal const float MinimumCalibrationFuelLiters = 0.2f;
        private const float MaximumObservationDistanceMeters = 250f;

        private readonly Queue<RangeObservation> _observations = new Queue<RangeObservation>();

        private float _windowDistanceMeters;
        private float _windowFuelLiters;
        private float _pendingDistanceMeters;
        private float _pendingFuelLiters;

        public bool HasEstimate
        {
            get
            {
                return _windowDistanceMeters >= MinimumCalibrationDistanceMeters
                    && _windowFuelLiters >= MinimumCalibrationFuelLiters
                    && SmoothedLitersPerMeter > 0.000001f;
            }
        }

        public float SmoothedLitersPerMeter
        {
            get
            {
                return _windowDistanceMeters <= 0.001f
                    ? 0f
                    : _windowFuelLiters / _windowDistanceMeters;
            }
        }

        public void Reset()
        {
            _observations.Clear();
            _windowDistanceMeters = 0f;
            _windowFuelLiters = 0f;
            _pendingDistanceMeters = 0f;
            _pendingFuelLiters = 0f;
        }

        public void AddObservation(float distanceMeters, float fuelLitersConsumed, float speedMetersPerSecond)
        {
            if (speedMetersPerSecond < MinimumObservationSpeedMetersPerSecond
                || distanceMeters <= 0.001f
                || fuelLitersConsumed <= 0.0001f)
            {
                return;
            }

            if (distanceMeters > MaximumObservationDistanceMeters)
            {
                ResetPending();
                return;
            }

            _pendingDistanceMeters += distanceMeters;
            _pendingFuelLiters += fuelLitersConsumed;
            if (_pendingDistanceMeters < MinimumCommittedObservationDistanceMeters
                || _pendingFuelLiters < MinimumCommittedObservationFuelLiters)
            {
                return;
            }

            var observation = new RangeObservation
            {
                DistanceMeters = _pendingDistanceMeters,
                FuelLitersConsumed = _pendingFuelLiters,
            };

            _observations.Enqueue(observation);
            _windowDistanceMeters += observation.DistanceMeters;
            _windowFuelLiters += observation.FuelLitersConsumed;
            ResetPending();

            while (_observations.Count > MaxCommittedObservationCount)
            {
                var expired = _observations.Dequeue();
                _windowDistanceMeters = Math.Max(0f, _windowDistanceMeters - expired.DistanceMeters);
                _windowFuelLiters = Math.Max(0f, _windowFuelLiters - expired.FuelLitersConsumed);
            }
        }

        public float EstimateRemainingRangeMeters(float currentFuelLiters)
        {
            if (currentFuelLiters <= 0.001f)
            {
                return 0f;
            }

            var litersPerMeter = SmoothedLitersPerMeter;
            return !HasEstimate || litersPerMeter <= 0.000001f
                ? 0f
                : Math.Max(0f, currentFuelLiters / litersPerMeter);
        }

        private void ResetPending()
        {
            _pendingDistanceMeters = 0f;
            _pendingFuelLiters = 0f;
        }

        private sealed class RangeObservation
        {
            public float DistanceMeters { get; set; }

            public float FuelLitersConsumed { get; set; }
        }
    }
}
using System;
using System.Collections.Generic;
using GTA;
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
        private const float BaseThrottleConsumptionLitersPerSecond = 0.18f;
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

        public void InitializeSpawnedVehicle(Vehicle poweredVehicle)
        {
            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return;
            }

            state.CurrentFuelLiters = state.CapacityLiters;
            state.OutOfFuelMessageShown = false;
            ApplyPropulsionState(poweredVehicle, state);
        }

        public bool Update(Ped player, int gameTimeMs)
        {
            CleanupStates();

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
                        changed = Math.Abs(before - state.CurrentFuelLiters) > 0.001f;
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
            return changed;
        }

        public VehicleFuelTelemetry GetTelemetry(Vehicle poweredVehicle, Vehicle cargoVehicle = null)
        {
            var state = GetOrCreateState(poweredVehicle);
            if (state == null)
            {
                return null;
            }

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
                IsOutOfFuel = state.CurrentFuelLiters <= 0.001f,
            };
        }

        public VehicleFuelTelemetry GetActiveTelemetry(Ped player)
        {
            Vehicle cargoVehicle;
            var poweredVehicle = _fleetManager.ResolvePoweredVehicle(player, out cargoVehicle);
            return GetTelemetry(poweredVehicle, cargoVehicle);
        }

        public float AddFuel(Vehicle poweredVehicle, float liters)
        {
            var state = GetOrCreateState(poweredVehicle);
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

            _fuelStates[poweredVehicle.Handle] = state;
            return state;
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
        }
    }
}
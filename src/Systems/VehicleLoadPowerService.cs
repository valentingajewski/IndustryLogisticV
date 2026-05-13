using System;
using GTA;
using GTA.Native;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class VehicleLoadPowerService
    {
        private const float MinimumPowerMultiplier = 0.72f;
        private const float TractorMaxPenalty = 0.28f;
        private const float StraightTruckMaxPenalty = 0.22f;
        private const float TractorReferenceWeightTons = 28f;
        private const float StraightTruckReferenceWeightTons = 16f;
        private const float AbsoluteWeightBlend = 0.75f;

        private readonly FleetManager _fleetManager;
        private int _lastVehicleHandle;

        public VehicleLoadPowerService(FleetManager fleetManager)
        {
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
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
                RestoreTrackedVehicle();
            }
        }

        public void Update(Ped player)
        {
            if (!DifficultyEnabled)
            {
                RestoreTrackedVehicle();
                return;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || poweredVehicle == null
                || !poweredVehicle.Exists())
            {
                RestoreTrackedVehicle();
                return;
            }

            var definition = _fleetManager.FindDefinition(poweredVehicle.Model);
            if (definition == null || definition.IsTrailer)
            {
                RestoreTrackedVehicle();
                return;
            }

            var cargoState = cargoVehicle != null && cargoVehicle.Exists()
                ? _fleetManager.GetOrCreateCargoState(cargoVehicle)
                : null;
            var multiplier = ResolvePowerMultiplier(definition, cargoState);
            ApplyToVehicle(poweredVehicle, multiplier);
        }

        public void ClearAllStates()
        {
            RestoreTrackedVehicle();
        }

        private static float ResolvePowerMultiplier(VehicleDefinition definition, VehicleCargoState cargoState)
        {
            if (definition == null || cargoState == null || cargoState.WeightTons <= 0.01f)
            {
                return 1f;
            }

            var capacityRatio = cargoState.CapacityTons <= 0.01f
                ? 0f
                : ModMath.Clamp01(cargoState.WeightTons / cargoState.CapacityTons);
            var referenceWeight = definition.IsTractor ? TractorReferenceWeightTons : StraightTruckReferenceWeightTons;
            var absoluteRatio = ModMath.Clamp01(cargoState.WeightTons / referenceWeight);
            var loadFactor = Math.Max(capacityRatio, absoluteRatio * AbsoluteWeightBlend);
            var maxPenalty = definition.IsTractor ? TractorMaxPenalty : StraightTruckMaxPenalty;
            return Math.Max(MinimumPowerMultiplier, 1f - (loadFactor * maxPenalty));
        }

        private void ApplyToVehicle(Vehicle vehicle, float multiplier)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                RestoreTrackedVehicle();
                return;
            }

            if (_lastVehicleHandle != 0 && _lastVehicleHandle != vehicle.Handle)
            {
                RestoreTrackedVehicle();
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicle.Handle, multiplier);
                _lastVehicleHandle = vehicle.Handle;
            }
            catch
            {
                _lastVehicleHandle = vehicle.Handle;
            }
        }

        private void RestoreTrackedVehicle()
        {
            if (_lastVehicleHandle == 0)
            {
                return;
            }

            try
            {
                var vehicle = Entity.FromHandle(_lastVehicleHandle) as Vehicle;
                if (vehicle != null && vehicle.Exists())
                {
                    Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicle.Handle, 1f);
                }
            }
            catch
            {
            }

            _lastVehicleHandle = 0;
        }
    }
}
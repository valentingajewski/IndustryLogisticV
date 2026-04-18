using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public sealed class FleetManager
    {
        private readonly List<VehicleDefinition> _definitions;
        private readonly Dictionary<string, List<string>> _objectModels;
        private readonly Dictionary<int, VehicleCargoState> _cargoStates;
        private readonly Random _random;
        private readonly Model[] _emptyModelArray;

        public FleetManager(ModConfig config)
        {
            _definitions = config.VehicleDefinitions;
            _objectModels = config.ObjectModels;
            _cargoStates = new Dictionary<int, VehicleCargoState>();
            _random = new Random();
            _emptyModelArray = new Model[0];
        }

        public IReadOnlyList<VehicleDefinition> Definitions
        {
            get { return _definitions; }
        }

        public IEnumerable<VehicleDefinition> GetSpawnableForCargoType(VehicleCargoType cargoType)
        {
            return _definitions.Where(x =>
                x.IsEnabled &&
                x.CargoType == cargoType &&
                x.CargoType != VehicleCargoType.Trailer);
        }

        public List<VehicleDefinition> GetTractorDefinitions()
        {
            return _definitions
                .Where(x => x.IsEnabled && x.CargoType == VehicleCargoType.Trailer)
                .ToList();
        }

        public Vehicle ResolveCargoVehicle(Ped player, out Vehicle driverVehicle)
        {
            driverVehicle = null;
            if (player == null || !player.Exists())
            {
                return null;
            }

            var current = player.CurrentVehicle;
            if (current != null && current.Exists())
            {
                driverVehicle = current;
                if (current.TowedVehicle != null && current.TowedVehicle.Exists())
                {
                    return current.TowedVehicle;
                }

                return current;
            }

            var nearest = World.GetClosestVehicle(player.Position, 12f, _emptyModelArray);
            if (nearest != null && nearest.Exists())
            {
                driverVehicle = nearest;
                if (nearest.TowedVehicle != null && nearest.TowedVehicle.Exists())
                {
                    return nearest.TowedVehicle;
                }

                return nearest;
            }

            return null;
        }

        public VehicleCargoState GetOrCreateCargoState(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            VehicleCargoState state;
            if (_cargoStates.TryGetValue(vehicle.Handle, out state))
            {
                return state;
            }

            var definition = FindDefinition(vehicle.Model);
            var cargoType = definition != null ? definition.CargoType : VehicleCargoType.Unknown;
            var capacity = definition != null && definition.CapacityTons > 0f ? definition.CapacityTons : 8f;

            state = new VehicleCargoState(vehicle.Handle, cargoType, capacity);
            _cargoStates[vehicle.Handle] = state;
            return state;
        }

        public VehicleDefinition FindDefinition(Model model)
        {
            var hash = model.Hash;
            for (int i = 0; i < _definitions.Count; i++)
            {
                var candidate = _definitions[i];
                if (!candidate.IsEnabled)
                {
                    continue;
                }

                if (new Model(candidate.ModelName).Hash == hash)
                {
                    return candidate;
                }
            }

            return null;
        }

        public bool SpawnSelectedVehicle(
            VehicleDefinition selected,
            VehicleDefinition selectedTractor,
            Vector3 spawnPosition,
            float heading,
            out Vehicle truck,
            out Vehicle cargoVehicle,
            out string message)
        {
            truck = null;
            cargoVehicle = null;
            message = string.Empty;

            if (selected == null)
            {
                message = "No vehicle selected.";
                return false;
            }

            if (selected.IsTrailer)
            {
                var tractor = selectedTractor;
                if (tractor == null || !tractor.IsEnabled || tractor.CargoType != VehicleCargoType.Trailer)
                {
                    tractor = _definitions.FirstOrDefault(x => x.CargoType == VehicleCargoType.Trailer && x.IsEnabled);
                }

                if (tractor == null)
                {
                    message = "No truck tractor available in config.";
                    return false;
                }

                if (!TrySpawnVehicle(tractor, spawnPosition, heading, out truck))
                {
                    message = "Failed to spawn truck.";
                    return false;
                }

                var trailerOffset = HeadingToDirection(heading) * -13f;
                if (!TrySpawnVehicle(selected, spawnPosition + trailerOffset, heading, out cargoVehicle))
                {
                    truck.Delete();
                    message = "Failed to spawn trailer.";
                    return false;
                }

                truck.AttachToTrailer(cargoVehicle, 15f);
                if (truck.TowedVehicle == null || !truck.TowedVehicle.Exists())
                {
                    truck.AttachToTrailer(cargoVehicle, 20f);
                }

                var trailerState = GetOrCreateCargoState(cargoVehicle);
                trailerState.CargoType = selected.CargoType;
                trailerState.CapacityTons = Math.Max(1f, selected.CapacityTons);

                message = string.Format("Spawned {0} with {1}.", truck.DisplayName, cargoVehicle.DisplayName);
                return true;
            }

            if (!TrySpawnVehicle(selected, spawnPosition, heading, out truck))
            {
                message = "Failed to spawn vehicle.";
                return false;
            }

            cargoVehicle = truck;
            var stateForVehicle = GetOrCreateCargoState(cargoVehicle);
            stateForVehicle.CargoType = selected.CargoType;
            stateForVehicle.CapacityTons = Math.Max(1f, selected.CapacityTons);

            message = string.Format("Spawned {0}.", truck.DisplayName);
            return true;
        }

        public void ApplyCargoVisuals(Vehicle cargoVehicle, VehicleCargoState cargoState)
        {
            if (cargoVehicle == null || !cargoVehicle.Exists() || cargoState == null)
            {
                return;
            }

            ClearCargoVisuals(cargoState);
            if (cargoState.CargoType != VehicleCargoType.Crate || cargoState.IsEmpty)
            {
                return;
            }

            List<string> modelNames;
            if (!_objectModels.TryGetValue("Box", out modelNames) || modelNames.Count == 0)
            {
                return;
            }

            var count = (int)Math.Ceiling(cargoState.WeightTons / 2f);
            count = Math.Max(1, Math.Min(6, count));

            var rootBone = cargoVehicle.Bones.Root;
            for (int i = 0; i < count; i++)
            {
                var modelName = modelNames[i % modelNames.Count];
                var model = new Model(modelName);
                if (!TryRequestModel(model, 500))
                {
                    continue;
                }

                var prop = World.CreateProp(model, cargoVehicle.Position, true, false);
                model.MarkAsNoLongerNeeded();

                if (prop == null || !prop.Exists())
                {
                    continue;
                }

                var col = i % 3;
                var row = i / 3;
                var offset = new Vector3(-0.9f + (col * 0.9f), -1.2f + (row * 1.1f), 0.55f);
                prop.AttachTo(rootBone, offset, Vector3.Zero);
                cargoState.AttachedProps.Add(prop);
            }
        }

        public void ClearCargoVisuals(VehicleCargoState cargoState)
        {
            if (cargoState == null)
            {
                return;
            }

            for (int i = 0; i < cargoState.AttachedProps.Count; i++)
            {
                var prop = cargoState.AttachedProps[i];
                if (prop != null && prop.Exists())
                {
                    prop.Delete();
                }
            }

            cargoState.AttachedProps.Clear();
        }

        public void CleanupStates()
        {
            var remove = new List<int>();
            foreach (var pair in _cargoStates)
            {
                var entity = Entity.FromHandle(pair.Key);
                var vehicle = entity as Vehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    ClearCargoVisuals(pair.Value);
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _cargoStates.Remove(remove[i]);
            }
        }

        private bool TrySpawnVehicle(VehicleDefinition definition, Vector3 position, float heading, out Vehicle vehicle)
        {
            vehicle = null;
            var model = new Model(definition.ModelName);
            if (!TryRequestModel(model, 1000))
            {
                return false;
            }

            vehicle = World.CreateVehicle(model, position, heading);
            model.MarkAsNoLongerNeeded();
            return vehicle != null && vehicle.Exists();
        }

        private static bool TryRequestModel(Model model, int timeoutMs)
        {
            if (!model.IsInCdImage || !model.IsValid)
            {
                return false;
            }

            return model.Request(timeoutMs);
        }

        private static Vector3 HeadingToDirection(float heading)
        {
            var radians = heading * (float)Math.PI / 180f;
            return new Vector3((float)-Math.Sin(radians), (float)Math.Cos(radians), 0f);
        }
    }
}

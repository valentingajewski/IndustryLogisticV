using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
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
            if (cargoState.IsEmpty)
            {
                return;
            }

            if (cargoState.CargoType == VehicleCargoType.Loose)
            {
                // Loose cargo is rendered as a marker overlay during loading ticks.
                return;
            }

            if (cargoState.CargoType != VehicleCargoType.Crate)
            {
                return;
            }

            List<string> modelNames;
            if (!_objectModels.TryGetValue("Box", out modelNames) || modelNames.Count == 0)
            {
                return;
            }

            var count = ResolveCratePropCount(cargoVehicle, cargoState);
            Vector3 modelMin;
            Vector3 modelMax;
            float bedMinX;
            float bedMaxX;
            float bedRearY;
            float bedFrontY;
            if (!TryGetTruckBedBounds(cargoVehicle, out modelMin, out modelMax, out bedMinX, out bedMaxX, out bedRearY, out bedFrontY))
            {
                return;
            }

            var columns = ResolveCrateColumnCount(count);
            var rows = (int)Math.Ceiling((float)count / columns);

            var rootBone = cargoVehicle.Bones.Root;
            for (int i = 0; i < count; i++)
            {
                var modelName = modelNames[i % modelNames.Count];
                var model = new Model(modelName);
                if (!TryRequestModel(model, 500))
                {
                    continue;
                }

                Vector3 crateModelMin;
                Vector3 crateModelMax;
                model.GetDimensions(out crateModelMin, out crateModelMax);

                var halfWidth = Math.Max(0.05f, (crateModelMax.X - crateModelMin.X) * 0.5f);
                var halfLength = Math.Max(0.05f, (crateModelMax.Y - crateModelMin.Y) * 0.5f);
                var localSlot = ResolveCrateLocalSlot(i, columns, rows, bedMinX, bedMaxX, bedRearY, bedFrontY);
                localSlot = ConstrainCrateLocalSlotToBed(localSlot, bedMinX, bedMaxX, bedRearY, bedFrontY, halfWidth, halfLength);

                float floorLocalZ;
                if (!TryProbeTruckBedFloor(cargoVehicle, localSlot.X, localSlot.Y, modelMin.Z, modelMax.Z, out floorLocalZ))
                {
                    model.MarkAsNoLongerNeeded();
                    continue;
                }

                var spawnPosition = cargoVehicle.GetOffsetPosition(new Vector3(localSlot.X, localSlot.Y, floorLocalZ + 0.35f));
                var prop = World.CreateProp(model, spawnPosition, true, false);
                model.MarkAsNoLongerNeeded();

                if (prop == null || !prop.Exists())
                {
                    continue;
                }

                if (!IsEntityInTruckBed(cargoVehicle, prop, bedRearY, bedFrontY, bedMinX, bedMaxX))
                {
                    var fallbackSlot = ConstrainCrateLocalSlotToBed(
                        new Vector3((bedMinX + bedMaxX) * 0.5f, (bedRearY + bedFrontY) * 0.5f, 0f),
                        bedMinX,
                        bedMaxX,
                        bedRearY,
                        bedFrontY,
                        halfWidth,
                        halfLength);

                    float fallbackFloorLocalZ;
                    if (!TryProbeTruckBedFloor(cargoVehicle, fallbackSlot.X, fallbackSlot.Y, modelMin.Z, modelMax.Z, out fallbackFloorLocalZ))
                    {
                        fallbackFloorLocalZ = EstimateTruckBedFloorLocalZ(modelMin.Z, modelMax.Z);
                    }

                    localSlot = fallbackSlot;
                    floorLocalZ = fallbackFloorLocalZ;
                    prop.Position = cargoVehicle.GetOffsetPosition(new Vector3(localSlot.X, localSlot.Y, floorLocalZ + 0.35f));
                }

                Vector3 crateMin;
                Vector3 crateMax;
                prop.Model.GetDimensions(out crateMin, out crateMax);

                var offset = new Vector3(localSlot.X, localSlot.Y, floorLocalZ - crateMin.Z + 0.01f);
                prop.AttachTo(rootBone, offset, Vector3.Zero);
                cargoState.AttachedProps.Add(prop);
            }
        }

        private int ResolveCratePropCount(Vehicle cargoVehicle, VehicleCargoState cargoState)
        {
            var definition = FindDefinition(cargoVehicle.Model);
            if (definition != null)
            {
                // Keep class-based prop counts stable for gameplay readability.
                if (definition.SectionName.Equals("LightCommercialTruck", StringComparison.OrdinalIgnoreCase))
                {
                    return 2;
                }

                if (definition.SectionName.Equals("MediumCommercialTruck", StringComparison.OrdinalIgnoreCase))
                {
                    return 4;
                }
            }

            var count = (int)Math.Ceiling(cargoState.WeightTons / 2f);
            return Math.Max(1, Math.Min(6, count));
        }

        private static bool TryGetTruckBedBounds(
            Vehicle truck,
            out Vector3 modelMin,
            out Vector3 modelMax,
            out float bedMinX,
            out float bedMaxX,
            out float bedRearY,
            out float bedFrontY)
        {
            modelMin = Vector3.Zero;
            modelMax = Vector3.Zero;
            bedMinX = 0f;
            bedMaxX = 0f;
            bedRearY = 0f;
            bedFrontY = 0f;

            if (truck == null || !truck.Exists())
            {
                return false;
            }

            truck.Model.GetDimensions(out modelMin, out modelMax);

            var width = modelMax.X - modelMin.X;
            var length = modelMax.Y - modelMin.Y;
            if (width <= 0.05f || length <= 0.05f)
            {
                return false;
            }

            // Model dimensions include exterior body/mirrors; use a larger margin for inner bed width.
            var sideMargin = Math.Max(0.3f, width * 0.24f);
            bedMinX = modelMin.X + sideMargin;
            bedMaxX = modelMax.X - sideMargin;

            var rearMargin = Math.Max(0.15f, length * 0.05f);
            bedRearY = modelMin.Y + rearMargin;
            // Keep placement in rear cargo section to avoid cabin/roof area.
            bedFrontY = modelMin.Y + (length * 0.48f);
            bedFrontY = Math.Min(bedFrontY, modelMax.Y - rearMargin);

            if (bedMinX >= bedMaxX)
            {
                var centerX = (modelMin.X + modelMax.X) * 0.5f;
                var halfFallbackX = Math.Max(0.15f, width * 0.18f);
                bedMinX = centerX - halfFallbackX;
                bedMaxX = centerX + halfFallbackX;
            }

            if (bedRearY >= bedFrontY)
            {
                bedRearY = modelMin.Y + (length * 0.12f);
                bedFrontY = modelMin.Y + (length * 0.62f);
            }

            if (bedMinX >= bedMaxX || bedRearY >= bedFrontY)
            {
                return false;
            }

            return true;
        }

        private static int ResolveCrateColumnCount(int crateCount)
        {
            if (crateCount <= 1)
            {
                return 1;
            }

            if (crateCount <= 2)
            {
                return 2;
            }

            if (crateCount <= 4)
            {
                return 2;
            }

            return 3;
        }

        private static Vector3 ResolveCrateLocalSlot(int index, int columns, int rows, float bedMinX, float bedMaxX, float bedRearY, float bedFrontY)
        {
            var col = columns <= 0 ? 0 : index % columns;
            var row = columns <= 0 ? 0 : index / columns;

            var tx = columns <= 1 ? 0.5f : (float)col / (columns - 1);
            var ty = rows <= 1 ? 0.5f : (float)row / (rows - 1);

            return new Vector3(
                Lerp(bedMinX, bedMaxX, tx),
                Lerp(bedRearY, bedFrontY, ty),
                0f);
        }

        private static Vector3 ConstrainCrateLocalSlotToBed(
            Vector3 localSlot,
            float bedMinX,
            float bedMaxX,
            float bedRearY,
            float bedFrontY,
            float halfWidth,
            float halfLength)
        {
            const float wallPadding = 0.04f;

            var safeMinX = bedMinX + halfWidth + wallPadding;
            var safeMaxX = bedMaxX - halfWidth - wallPadding;
            var safeRearY = bedRearY + halfLength + wallPadding;
            var safeFrontY = bedFrontY - halfLength - wallPadding;

            var x = safeMinX <= safeMaxX
                ? Clamp(localSlot.X, safeMinX, safeMaxX)
                : (bedMinX + bedMaxX) * 0.5f;

            var y = safeRearY <= safeFrontY
                ? Clamp(localSlot.Y, safeRearY, safeFrontY)
                : (bedRearY + bedFrontY) * 0.5f;

            return new Vector3(x, y, localSlot.Z);
        }

        private static bool TryProbeTruckBedFloor(Vehicle truck, float localX, float localY, float modelMinZ, float modelMaxZ, out float floorLocalZ)
        {
            floorLocalZ = 0f;
            if (truck == null || !truck.Exists())
            {
                return false;
            }

            var height = Math.Max(0.1f, modelMaxZ - modelMinZ);
            var startLocalZ = modelMinZ + (height * 0.88f);
            var maxFloorZ = modelMinZ + (height * 0.94f);

            var start = truck.GetOffsetPosition(new Vector3(localX, localY, startLocalZ));
            var target = truck.GetOffsetPosition(new Vector3(localX, localY, modelMinZ - 0.9f));
            var hit = World.Raycast(start, target, IntersectFlags.Map | IntersectFlags.Vehicles | IntersectFlags.Objects, null);
            if (!hit.DidHit)
            {
                floorLocalZ = EstimateTruckBedFloorLocalZ(modelMinZ, modelMaxZ);
                return true;
            }

            var localHit = truck.GetPositionOffset(hit.HitPosition);
            if (localHit.Z < modelMinZ - 0.2f || localHit.Z > maxFloorZ)
            {
                floorLocalZ = EstimateTruckBedFloorLocalZ(modelMinZ, modelMaxZ);
                return true;
            }

            floorLocalZ = localHit.Z;
            return true;
        }

        private static bool IsEntityInTruckBed(Vehicle truck, Entity entity, float bedRearY, float bedFrontY, float bedMinX, float bedMaxX)
        {
            if (truck == null || !truck.Exists() || entity == null || !entity.Exists())
            {
                return false;
            }

            var localOffset = GetOffsetFromEntityInWorldCoords(truck, entity.Position);
            Vector3 entityMin;
            Vector3 entityMax;
            entity.Model.GetDimensions(out entityMin, out entityMax);

            var halfWidth = Math.Max(0.05f, (entityMax.X - entityMin.X) * 0.5f);
            var halfLength = Math.Max(0.05f, (entityMax.Y - entityMin.Y) * 0.5f);
            const float edgePadding = 0.02f;

            var safeHalfWidth = halfWidth * 0.78f;
            var safeHalfLength = halfLength * 0.78f;

            var insideY = (localOffset.Y - safeHalfLength) >= (bedRearY + edgePadding) && (localOffset.Y + safeHalfLength) <= (bedFrontY - edgePadding);
            var insideX = (localOffset.X - safeHalfWidth) >= (bedMinX + edgePadding) && (localOffset.X + safeHalfWidth) <= (bedMaxX - edgePadding);
            return insideY && insideX;
        }

        private static Vector3 GetOffsetFromEntityInWorldCoords(Entity entity, Vector3 worldCoords)
        {
            return entity.GetPositionOffset(worldCoords);
        }

        private static float Lerp(float start, float end, float t)
        {
            return start + ((end - start) * t);
        }

        private static float EstimateTruckBedFloorLocalZ(float modelMinZ, float modelMaxZ)
        {
            return modelMinZ + ((modelMaxZ - modelMinZ) * 0.5f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
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

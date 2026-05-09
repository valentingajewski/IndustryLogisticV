using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class FleetManager
    {
        private const string AlloySolidPropModel = "prop_pipes_01b";
        private const string MetalSolidPropModel = "prop_pipes_04a";
        private const string DefaultWoodPropModel = "prop_woodpile_01b";
        private const string DefaultLittleBoxPropModel = "prop_boxpile_07d";
        private const string DefaultTinyBoxPropModel = "prop_rub_boxpile_02";
        private readonly List<VehicleDefinition> _definitions;
        private readonly Dictionary<int, VehicleDefinition> _definitionsByModelHash;
        private readonly Dictionary<string, VehicleDefinition> _definitionsByModelName;
        private readonly Dictionary<string, List<string>> _objectModels;
        private readonly Dictionary<int, VehicleCargoState> _cargoStates;
        private readonly List<OwnedFleetRig> _ownedRigs;
        private readonly Random _random;
        private readonly Model[] _emptyModelArray;

        public FleetManager(ModConfig config)
        {
            _definitions = config.VehicleDefinitions;
            _definitionsByModelHash = BuildDefinitionLookup(_definitions);
            _definitionsByModelName = BuildDefinitionModelNameLookup(_definitions);
            _objectModels = config.ObjectModels;
            _cargoStates = new Dictionary<int, VehicleCargoState>();
            _ownedRigs = new List<OwnedFleetRig>();
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
                !x.IsTractor &&
                x.CargoType == cargoType);
        }

        public IEnumerable<VehicleDefinition> GetSpawnableForCommodity(string commodity)
        {
            return _definitions.Where(x =>
                x.IsEnabled &&
                !x.IsTractor &&
                CanDefinitionCarryCommodity(x, commodity));
        }

        public List<VehicleDefinition> GetTractorDefinitions()
        {
            return _definitions
                .Where(x => x.IsEnabled && x.IsTractor)
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
                var currentTrailer = ResolveAttachedTrailer(current);
                if (currentTrailer != null)
                {
                    return currentTrailer;
                }

                return current;
            }

            var nearest = World.GetClosestVehicle(player.Position, 12f, _emptyModelArray);
            if (nearest != null && nearest.Exists())
            {
                driverVehicle = nearest;
                var nearestTrailer = ResolveAttachedTrailer(nearest);
                if (nearestTrailer != null)
                {
                    return nearestTrailer;
                }

                return nearest;
            }

            return null;
        }

        public Vehicle ResolvePoweredVehicle(Ped player, out Vehicle cargoVehicle)
        {
            cargoVehicle = null;
            Vehicle driverVehicle;
            cargoVehicle = ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return null;
            }

            if (driverVehicle != null && driverVehicle.Exists() && driverVehicle.Handle != cargoVehicle.Handle)
            {
                return driverVehicle;
            }

            var towingVehicle = ResolveTowVehicle(cargoVehicle);
            if (towingVehicle != null)
            {
                return towingVehicle;
            }

            return cargoVehicle;
        }

        public bool TryResolveVehicleContext(Ped player, out Vehicle poweredVehicle, out Vehicle cargoVehicle)
        {
            cargoVehicle = null;
            poweredVehicle = ResolvePoweredVehicle(player, out cargoVehicle);
            return poweredVehicle != null && poweredVehicle.Exists() && cargoVehicle != null && cargoVehicle.Exists();
        }

        private static Vehicle ResolveAttachedTrailer(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            var towedVehicle = vehicle.TowedVehicle;
            if (towedVehicle != null && towedVehicle.Exists())
            {
                return towedVehicle;
            }

            var trailerHandleArg = new OutputArgument();
            bool hasTrailer;
            try
            {
                hasTrailer = Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, vehicle.Handle, trailerHandleArg);
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

            if (trailerHandle <= 0)
            {
                return null;
            }

            var trailerEntity = Entity.FromHandle(trailerHandle) as Vehicle;
            if (trailerEntity != null && trailerEntity.Exists())
            {
                return trailerEntity;
            }

            return null;
        }

        private static Vehicle ResolveTowVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            int attachedHandle;
            try
            {
                attachedHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, vehicle.Handle);
            }
            catch
            {
                return null;
            }

            if (attachedHandle <= 0 || attachedHandle == vehicle.Handle)
            {
                return null;
            }

            var attachedVehicle = Entity.FromHandle(attachedHandle) as Vehicle;
            return attachedVehicle != null && attachedVehicle.Exists()
                ? attachedVehicle
                : null;
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
            VehicleDefinition definition;
            return _definitionsByModelHash.TryGetValue(model.Hash, out definition)
                ? definition
                : null;
        }

        public VehicleDefinition FindDefinitionByModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            VehicleDefinition definition;
            return _definitionsByModelName.TryGetValue(modelName.Trim(), out definition)
                ? definition
                : null;
        }

        public bool TrySpawnVehicleByModelName(string modelName, Vector3 position, float heading, out Vehicle vehicle)
        {
            vehicle = null;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            var definition = FindDefinitionByModelName(modelName);
            if (definition != null)
            {
                return TrySpawnVehicle(definition, position, heading, out vehicle);
            }

            var model = new Model(modelName);
            if (!TryRequestModel(model, 1000))
            {
                return false;
            }

            vehicle = World.CreateVehicle(model, position, heading);
            model.MarkAsNoLongerNeeded();
            PlaceVehicleOnGround(vehicle);
            return vehicle != null && vehicle.Exists();
        }

        public bool TryAttachVehicleToTrailer(Vehicle truck, Vehicle trailer, float heading)
        {
            return TryAttachTruckToTrailer(truck, trailer, heading);
        }

        public void RegisterOwnedRig(Vehicle truck, Vehicle cargoVehicle)
        {
            if (truck == null || !truck.Exists())
            {
                return;
            }

            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                cargoVehicle = truck;
            }

            for (int i = 0; i < _ownedRigs.Count; i++)
            {
                if (_ownedRigs[i].Matches(truck.Handle, cargoVehicle.Handle))
                {
                    _ownedRigs[i].TruckHandle = truck.Handle;
                    _ownedRigs[i].CargoHandle = cargoVehicle.Handle;
                    truck.IsPersistent = true;
                    cargoVehicle.IsPersistent = true;
                    return;
                }
            }

            truck.IsPersistent = true;
            cargoVehicle.IsPersistent = true;
            _ownedRigs.Add(new OwnedFleetRig
            {
                TruckHandle = truck.Handle,
                CargoHandle = cargoVehicle.Handle,
            });
        }

        public void DespawnOwnedFleet()
        {
            CleanupOwnedRigs(true);
        }

        public OwnedFleetPersistenceSnapshot CreateOwnedFleetSnapshot(VehicleFuelSystem fuelSystem)
        {
            var snapshot = new OwnedFleetPersistenceSnapshot();

            for (int i = _ownedRigs.Count - 1; i >= 0; i--)
            {
                var rig = _ownedRigs[i];
                var truck = ResolveVehicleHandle(rig.TruckHandle);
                var cargoVehicle = ResolveVehicleHandle(rig.CargoHandle);
                if (truck == null || !truck.Exists() || cargoVehicle == null || !cargoVehicle.Exists())
                {
                    _ownedRigs.RemoveAt(i);
                    continue;
                }

                var truckDefinition = FindDefinition(truck.Model);
                var cargoDefinition = FindDefinition(cargoVehicle.Model);
                if (truckDefinition == null || cargoDefinition == null)
                {
                    continue;
                }

                var cargoState = GetOrCreateCargoState(cargoVehicle);
                if (cargoState == null)
                {
                    continue;
                }

                var fuelTelemetry = fuelSystem != null
                    ? fuelSystem.GetTelemetry(truck, cargoVehicle)
                    : null;

                snapshot.Vehicles.Add(new OwnedFleetVehicleSnapshot
                {
                    PoweredModelName = truckDefinition.ModelName,
                    CargoModelName = cargoDefinition.ModelName,
                    HasSeparateCargoVehicle = truck.Handle != cargoVehicle.Handle,
                    PoweredPosition = truck.Position,
                    PoweredHeading = truck.Heading,
                    CargoType = cargoState.CargoType,
                    CapacityTons = cargoState.CapacityTons,
                    Commodity = cargoState.Commodity,
                    WeightTons = cargoState.WeightTons,
                    CargoCondition = cargoState.CargoCondition,
                    TotalLostTons = cargoState.TotalLostTons,
                    SourceIndustryId = cargoState.SourceIndustryId,
                    SourceDistrictName = cargoState.SourceDistrictName,
                    CurrentFuelLiters = fuelTelemetry != null ? fuelTelemetry.CurrentLiters : 0f,
                });
            }

            return snapshot;
        }

        public int RestoreOwnedFleet(
            OwnedFleetPersistenceSnapshot snapshot,
            VehicleFuelSystem fuelSystem,
            Func<Vector3, Vector3> getGroundPosition)
        {
            if (snapshot == null || snapshot.Vehicles == null || snapshot.Vehicles.Count == 0)
            {
                return 0;
            }

            var restoredCount = 0;
            for (int i = 0; i < snapshot.Vehicles.Count; i++)
            {
                var entry = snapshot.Vehicles[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.PoweredModelName))
                {
                    continue;
                }

                var truckDefinition = FindDefinitionByModelName(entry.PoweredModelName);
                var cargoDefinition = entry.HasSeparateCargoVehicle
                    ? FindDefinitionByModelName(entry.CargoModelName)
                    : truckDefinition;
                var tractorDefinition = entry.HasSeparateCargoVehicle ? truckDefinition : null;
                if (truckDefinition == null || cargoDefinition == null)
                {
                    continue;
                }

                Vehicle truck;
                Vehicle cargoVehicle;
                string ignoredMessage;
                var spawnPosition = getGroundPosition != null
                    ? getGroundPosition(entry.PoweredPosition)
                    : entry.PoweredPosition;
                if (!SpawnSelectedVehicle(
                    cargoDefinition,
                    tractorDefinition,
                    spawnPosition,
                    entry.PoweredHeading,
                    out truck,
                    out cargoVehicle,
                    out ignoredMessage))
                {
                    continue;
                }

                RegisterOwnedRig(truck, cargoVehicle);

                var cargoState = GetOrCreateCargoState(cargoVehicle);
                if (cargoState != null)
                {
                    cargoState.CargoType = entry.CargoType;
                    cargoState.CapacityTons = Math.Max(1f, entry.CapacityTons > 0f ? entry.CapacityTons : cargoState.CapacityTons);
                    if (!string.IsNullOrWhiteSpace(entry.Commodity) && entry.WeightTons > 0.001f)
                    {
                        cargoState.Commodity = CommodityCatalog.Normalize(entry.Commodity);
                        cargoState.WeightTons = Math.Max(0f, entry.WeightTons);
                        cargoState.CargoCondition = Math.Max(0f, Math.Min(1f, entry.CargoCondition));
                        cargoState.TotalLostTons = Math.Max(0f, entry.TotalLostTons);
                        cargoState.SourceIndustryId = entry.SourceIndustryId ?? string.Empty;
                        cargoState.SourceDistrictName = entry.SourceDistrictName ?? string.Empty;
                        ApplyCargoVisuals(cargoVehicle, cargoState);
                    }
                    else
                    {
                        cargoState.ClearCargo();
                    }
                }

                if (fuelSystem != null)
                {
                    fuelSystem.InitializeSpawnedVehicle(truck, entry.CurrentFuelLiters);
                }

                restoredCount += 1;
            }

            return restoredCount;
        }

        public bool CanVehicleCarryCommodity(Vehicle vehicle, string commodity)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            return CanDefinitionCarryCommodity(FindDefinition(vehicle.Model), commodity);
        }

        public bool CanDefinitionCarryCommodity(VehicleDefinition definition, string commodity)
        {
            if (definition == null || string.IsNullOrWhiteSpace(commodity))
            {
                return false;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            if (definition.AcceptedCommodities != null && definition.AcceptedCommodities.Count > 0)
            {
                return definition.AcceptedCommodities.Contains(normalizedCommodity);
            }

            return definition.CargoType == CommodityCatalog.GetCargoTypeForCommodity(normalizedCommodity);
        }

        private static Dictionary<int, VehicleDefinition> BuildDefinitionLookup(List<VehicleDefinition> definitions)
        {
            var lookup = new Dictionary<int, VehicleDefinition>();
            if (definitions == null)
            {
                return lookup;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate == null || !candidate.IsEnabled || string.IsNullOrWhiteSpace(candidate.ModelName))
                {
                    continue;
                }

                var hash = new Model(candidate.ModelName).Hash;
                if (!lookup.ContainsKey(hash))
                {
                    lookup[hash] = candidate;
                }
            }

            return lookup;
        }

        private static Dictionary<string, VehicleDefinition> BuildDefinitionModelNameLookup(List<VehicleDefinition> definitions)
        {
            var lookup = new Dictionary<string, VehicleDefinition>(StringComparer.OrdinalIgnoreCase);
            if (definitions == null)
            {
                return lookup;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate == null || !candidate.IsEnabled || string.IsNullOrWhiteSpace(candidate.ModelName))
                {
                    continue;
                }

                if (!lookup.ContainsKey(candidate.ModelName.Trim()))
                {
                    lookup[candidate.ModelName.Trim()] = candidate;
                }
            }

            return lookup;
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
                if (tractor == null || !tractor.IsEnabled || !tractor.IsTractor)
                {
                    tractor = _definitions.FirstOrDefault(x => x.IsEnabled && x.IsTractor);
                }

                if (tractor == null)
                {
                    message = "No truck tractor available in config.";
                    return false;
                }

                if (!TrySpawnTrailerCombination(tractor, selected, spawnPosition, heading, out truck, out cargoVehicle))
                {
                    message = "Failed to connect trailer.";
                    return false;
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

        private bool TrySpawnTrailerCombination(
            VehicleDefinition tractor,
            VehicleDefinition trailerDefinition,
            Vector3 spawnPosition,
            float heading,
            out Vehicle truck,
            out Vehicle cargoVehicle)
        {
            truck = null;
            cargoVehicle = null;

            if (tractor == null || trailerDefinition == null)
            {
                return false;
            }

            var anchorCandidates = BuildTrailerSpawnAnchorCandidates(spawnPosition, heading);
            for (int anchorIndex = 0; anchorIndex < anchorCandidates.Length; anchorIndex++)
            {
                if (TrySpawnTrailerCombinationAtAnchor(tractor, trailerDefinition, anchorCandidates[anchorIndex], heading, out truck, out cargoVehicle))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySpawnTrailerCombinationAtAnchor(
            VehicleDefinition tractor,
            VehicleDefinition trailerDefinition,
            Vector3 anchorPosition,
            float heading,
            out Vehicle truck,
            out Vehicle cargoVehicle)
        {
            truck = null;
            cargoVehicle = null;

            if (!TrySpawnVehicle(tractor, anchorPosition, heading, out truck))
            {
                return false;
            }

            var direction = HeadingToDirection(heading);
            var trailerDistances = new[] { 18f, 22f };
            for (int distanceIndex = 0; distanceIndex < trailerDistances.Length; distanceIndex++)
            {
                var trailerPosition = anchorPosition - (direction * trailerDistances[distanceIndex]);
                if (!TrySpawnVehicle(trailerDefinition, trailerPosition, heading, out cargoVehicle))
                {
                    continue;
                }

                if (TryAttachTrailerAfterSpawn(truck, cargoVehicle, heading))
                {
                    return true;
                }

                cargoVehicle.Delete();
                cargoVehicle = null;
            }

            truck.Delete();
            truck = null;
            return false;
        }

        private bool TryAttachTrailerAfterSpawn(Vehicle truck, Vehicle trailer, float heading)
        {
            if (TryAttachTruckToTrailer(truck, trailer, heading))
            {
                return true;
            }

            var direction = HeadingToDirection(heading);
            var originalTruckPosition = truck.Position;
            var forwardOffsets = new[] { 6f, 10f, 14f };

            for (int i = 0; i < forwardOffsets.Length; i++)
            {
                truck.Position = originalTruckPosition + (direction * forwardOffsets[i]);
                truck.Heading = heading;
                PlaceVehicleOnGround(truck);

                if (TryAttachTruckToTrailer(truck, trailer, heading))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3[] BuildTrailerSpawnAnchorCandidates(Vector3 spawnPosition, float heading)
        {
            var direction = HeadingToDirection(heading);
            var right = new Vector3(direction.Y, -direction.X, 0f);
            return new[]
            {
                spawnPosition,
                spawnPosition + (direction * 8f),
                spawnPosition - (direction * 6f),
                spawnPosition + (right * 4f),
                spawnPosition - (right * 4f),
            };
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

            List<string> modelNames;
            int count;
            bool forceCenteredPlacement;
            if (!TryResolveCargoPropLayout(cargoVehicle, cargoState, out modelNames, out count, out forceCenteredPlacement))
            {
                return;
            }

            Vector3 modelMin;
            Vector3 modelMax;
            float bedMinX;
            float bedMaxX;
            float bedRearY;
            float bedFrontY;
            var definition = FindDefinition(cargoVehicle.Model);
            var useFullLengthBed = definition != null && definition.IsTrailer;
            if (!TryGetTruckBedBounds(cargoVehicle, useFullLengthBed, out modelMin, out modelMax, out bedMinX, out bedMaxX, out bedRearY, out bedFrontY))
            {
                return;
            }

            var columns = ResolveCrateColumnCount(count);
            var rows = (int)Math.Ceiling((float)count / columns);
            for (int i = 0; i < count; i++)
            {
                var modelName = modelNames[i % modelNames.Count];
                var model = new Model(modelName);
                if (!TryRequestModel(model, 500))
                {
                    continue;
                }

                var attachmentRotation = ResolveAttachedPropRotation(modelName);
                var attachmentOffset = ResolveAttachedPropOffset(modelName);

                Vector3 crateModelMin;
                Vector3 crateModelMax;
                model.GetDimensions(out crateModelMin, out crateModelMax);

                var halfWidth = Math.Max(0.05f, (crateModelMax.X - crateModelMin.X) * 0.5f);
                var halfLength = Math.Max(0.05f, (crateModelMax.Y - crateModelMin.Y) * 0.5f);
                var localSlot = ResolveCrateLocalSlot(i, columns, rows, bedMinX, bedMaxX, bedRearY, bedFrontY);
                if (forceCenteredPlacement && count == 1)
                {
                    localSlot = new Vector3(
                        (bedMinX + bedMaxX) * 0.5f,
                        (bedRearY + bedFrontY) * 0.5f,
                        localSlot.Z);
                }

                localSlot = ConstrainCrateLocalSlotToBed(localSlot, bedMinX, bedMaxX, bedRearY, bedFrontY, halfWidth, halfLength);

                var centerOffsetX = forceCenteredPlacement
                    ? (crateModelMin.X + crateModelMax.X) * 0.5f
                    : 0f;
                var centerOffsetY = forceCenteredPlacement
                    ? (crateModelMin.Y + crateModelMax.Y) * 0.5f
                    : 0f;
                var placementX = localSlot.X - centerOffsetX;
                var placementY = localSlot.Y - centerOffsetY;

                float floorLocalZ;
                if (!TryProbeTruckBedFloor(cargoVehicle, localSlot.X, localSlot.Y, modelMin.Z, modelMax.Z, out floorLocalZ))
                {
                    model.MarkAsNoLongerNeeded();
                    continue;
                }

                var spawnPosition = cargoVehicle.GetOffsetPosition(new Vector3(
                    placementX + attachmentOffset.X,
                    placementY + attachmentOffset.Y,
                    floorLocalZ + 0.35f + attachmentOffset.Z));
                var prop = World.CreateProp(model, spawnPosition, true, false);
                model.MarkAsNoLongerNeeded();

                if (prop == null || !prop.Exists())
                {
                    continue;
                }

                if (!IsEntityInTruckBed(cargoVehicle, prop, bedRearY, bedFrontY, bedMinX, bedMaxX))
                {
                    var fallbackLocalY = (bedRearY + bedFrontY) * 0.5f;

                    var fallbackSlot = ConstrainCrateLocalSlotToBed(
                        new Vector3((bedMinX + bedMaxX) * 0.5f, fallbackLocalY, 0f),
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
                    placementX = localSlot.X - centerOffsetX;
                    placementY = localSlot.Y - centerOffsetY;
                    prop.Position = cargoVehicle.GetOffsetPosition(new Vector3(
                        placementX + attachmentOffset.X,
                        placementY + attachmentOffset.Y,
                        floorLocalZ + 0.35f + attachmentOffset.Z));
                }

                Vector3 crateMin;
                Vector3 crateMax;
                prop.Model.GetDimensions(out crateMin, out crateMax);

                var offset = new Vector3(
                    placementX + attachmentOffset.X,
                    placementY + attachmentOffset.Y,
                    floorLocalZ - crateMin.Z + 0.01f + attachmentOffset.Z);
                prop.AttachTo(cargoVehicle, offset, attachmentRotation);
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

        private bool TryResolveCargoPropLayout(Vehicle cargoVehicle, VehicleCargoState cargoState, out List<string> modelNames, out int count, out bool forceCenteredPlacement)
        {
            modelNames = null;
            count = 0;
            forceCenteredPlacement = false;

            var cargoType = CommodityCatalog.ResolveCargoType(cargoState.CargoType, cargoState.Commodity);
            if (!CommodityCatalog.UsesAttachedPropVisual(cargoType))
            {
                return false;
            }

            var definition = FindDefinition(cargoVehicle.Model);
            modelNames = ResolveCargoPropModels(cargoState.Commodity, cargoType, definition);
            if (modelNames == null || modelNames.Count == 0)
            {
                return false;
            }

            forceCenteredPlacement = CommodityCatalog.UsesCenteredPropVisual(cargoType);
            count = forceCenteredPlacement ? 1 : ResolveCratePropCount(cargoVehicle, cargoState);
            return count > 0;
        }

        private List<string> ResolveCargoPropModels(string commodity, VehicleCargoType cargoType, VehicleDefinition definition)
        {
            var vehicleSpecificModels = ResolveVehicleSpecificPropModels(definition);
            if (vehicleSpecificModels != null)
            {
                return vehicleSpecificModels;
            }

            var normalized = CommodityCatalog.Normalize(commodity);
            List<string> modelNames;
            if (_objectModels.TryGetValue(normalized, out modelNames) && modelNames.Count > 0)
            {
                return modelNames;
            }

            if (cargoType == VehicleCargoType.Wood)
            {
                var woodModel = ResolveWoodPropModel();
                if (!string.IsNullOrWhiteSpace(woodModel))
                {
                    return new List<string> { woodModel };
                }
            }

            if (cargoType == VehicleCargoType.OpenHull)
            {
                var solidModel = ResolveSolidPropModel(commodity);
                if (!string.IsNullOrWhiteSpace(solidModel))
                {
                    return new List<string> { solidModel };
                }
            }

            if ((cargoType == VehicleCargoType.CraftedGoods || cargoType == VehicleCargoType.Refrigeration) &&
                _objectModels.TryGetValue("Box", out modelNames) &&
                modelNames.Count > 0)
            {
                return modelNames;
            }

            return null;
        }

        private List<string> ResolveVehicleSpecificPropModels(VehicleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.SectionName))
            {
                return null;
            }

            if (definition.SectionName.Equals("CommercialBigVans", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveNamedPropModels("LittleBox", DefaultLittleBoxPropModel);
            }

            if (definition.SectionName.Equals("CommercialVans", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveNamedPropModels("TinyBox", DefaultTinyBoxPropModel);
            }

            return null;
        }

        private List<string> ResolveNamedPropModels(string objectKey, string fallbackModelName)
        {
            List<string> modelNames;
            if (_objectModels.TryGetValue(objectKey, out modelNames) && modelNames.Count > 0)
            {
                return modelNames;
            }

            return new List<string> { fallbackModelName };
        }

        private static Vector3 ResolveAttachedPropRotation(string modelName)
        {
            return string.Equals(modelName, DefaultLittleBoxPropModel, StringComparison.OrdinalIgnoreCase)
                ? new Vector3(0f, 0f, 90f)
                : Vector3.Zero;
        }

        private static Vector3 ResolveAttachedPropOffset(string modelName)
        {
            if (string.Equals(modelName, DefaultLittleBoxPropModel, StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(0f, 0.4f, 0.05f);
            }

            if (string.Equals(modelName, DefaultTinyBoxPropModel, StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(-0.2f, 0f, 0.5f);
            }

            return Vector3.Zero;
        }

        private static string ResolveSolidPropModel(string commodity)
        {
            var normalized = CommodityCatalog.Normalize(commodity);
            if (normalized.Equals("Alloy", StringComparison.OrdinalIgnoreCase))
            {
                return AlloySolidPropModel;
            }

            if (normalized.Equals("Metal", StringComparison.OrdinalIgnoreCase))
            {
                return MetalSolidPropModel;
            }

            return null;
        }

        private string ResolveWoodPropModel()
        {
            List<string> modelNames;
            if (_objectModels.TryGetValue("Wood", out modelNames) && modelNames.Count > 0)
            {
                return modelNames[0];
            }

            if (_objectModels.TryGetValue("Lumber", out modelNames) && modelNames.Count > 0)
            {
                return modelNames[0];
            }

            return DefaultWoodPropModel;
        }

        private static bool TryGetTruckBedBounds(
            Vehicle truck,
            bool useFullLengthBed,
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
            if (useFullLengthBed)
            {
                bedFrontY = modelMax.Y - rearMargin;
            }
            else
            {
                // Keep placement in rear cargo section to avoid cabin/roof area.
                bedFrontY = modelMin.Y + (length * 0.48f);
                bedFrontY = Math.Min(bedFrontY, modelMax.Y - rearMargin);
            }

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
                bedFrontY = useFullLengthBed
                    ? modelMax.Y - (length * 0.08f)
                    : modelMin.Y + (length * 0.62f);
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
                if (prop == null)
                {
                    continue;
                }

                try
                {
                    if (!prop.Exists())
                    {
                        continue;
                    }

                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, prop.Handle, true, true);
                    prop.Delete();

                    if (prop.Exists())
                    {
                        prop.IsVisible = false;
                        var position = prop.Position;
                        prop.Position = new Vector3(position.X, position.Y, position.Z - 250f);
                        prop.Delete();
                    }
                }
                catch
                {
                    // Keep cleanup resilient: one bad prop handle must not block deleting remaining props.
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

            CleanupOwnedRigs(false);
        }

        public void ClearAllStates()
        {
            foreach (var pair in _cargoStates)
            {
                ClearCargoVisuals(pair.Value);
            }

            _cargoStates.Clear();
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
            PlaceVehicleOnGround(vehicle);
            return vehicle != null && vehicle.Exists();
        }

        private static void PlaceVehicleOnGround(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
            }
            catch
            {
                // Ground placement is a best effort step; spawning should continue if the native call fails.
            }
        }

        private static bool TryAttachTruckToTrailer(Vehicle truck, Vehicle trailer, float heading)
        {
            if (truck == null || !truck.Exists() || trailer == null || !trailer.Exists())
            {
                return false;
            }

            truck.Heading = heading;
            trailer.Heading = heading;
            PlaceVehicleOnGround(truck);
            PlaceVehicleOnGround(trailer);

            if (TryAttachTruckToTrailerNow(truck, trailer))
            {
                return true;
            }

            var truckPosition = truck.Position;
            var trailerPosition = trailer.Position;
            var direction = HeadingToDirection(heading);
            var baseSpacing = ResolveTrailerSpacing(truck, trailer);
            var candidateSpacings = new[]
            {
                baseSpacing,
                baseSpacing + 2f,
                Math.Max(12f, baseSpacing - 1.5f),
                baseSpacing + 4f,
                baseSpacing + 8f,
            };
            var candidateHeights = BuildTrailerAttachHeightCandidates(truckPosition.Z, trailerPosition.Z);

            for (int heightIndex = 0; heightIndex < candidateHeights.Length; heightIndex++)
            {
                for (int i = 0; i < candidateSpacings.Length; i++)
                {
                    var targetPosition = truckPosition - (direction * candidateSpacings[i]);
                    trailer.Position = new Vector3(targetPosition.X, targetPosition.Y, candidateHeights[heightIndex]);
                    trailer.Heading = heading;
                    PlaceVehicleOnGround(trailer);

                    if (TryAttachTruckToTrailerNow(truck, trailer))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float[] BuildTrailerAttachHeightCandidates(float truckZ, float trailerZ)
        {
            if (Math.Abs(truckZ - trailerZ) < 0.25f)
            {
                return new[]
                {
                    trailerZ,
                    trailerZ + 0.35f,
                    trailerZ - 0.35f,
                };
            }

            return new[]
            {
                truckZ,
                trailerZ,
                truckZ + 0.35f,
                trailerZ + 0.35f,
                Math.Min(truckZ, trailerZ) - 0.35f,
            };
        }

        private static bool TryAttachTruckToTrailerNow(Vehicle truck, Vehicle trailer)
        {
            if (truck == null || !truck.Exists() || trailer == null || !trailer.Exists())
            {
                return false;
            }

            try
            {
                Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, truck.Handle);
            }
            catch
            {
                // Ignore detach failures and still attempt a fresh attach.
            }

            truck.AttachToTrailer(trailer, 1f);

            var towedVehicle = truck.TowedVehicle;
            if (towedVehicle != null && towedVehicle.Exists() && towedVehicle.Handle == trailer.Handle)
            {
                return true;
            }

            var attachedTrailer = ResolveAttachedTrailer(truck);
            return attachedTrailer != null && attachedTrailer.Exists() && attachedTrailer.Handle == trailer.Handle;
        }

        private static float ResolveTrailerSpacing(Vehicle truck, Vehicle trailer)
        {
            Vector3 truckMin;
            Vector3 truckMax;
            Vector3 trailerMin;
            Vector3 trailerMax;
            if (!TryGetVehicleBounds(truck, out truckMin, out truckMax) || !TryGetVehicleBounds(trailer, out trailerMin, out trailerMax))
            {
                return 16f;
            }

            var truckRearExtent = Math.Max(1f, -truckMin.Y);
            var trailerFrontExtent = Math.Max(1f, trailerMax.Y);
            return Math.Max(13f, truckRearExtent + trailerFrontExtent + 1.25f);
        }

        private static bool TryGetVehicleBounds(Vehicle vehicle, out Vector3 modelMin, out Vector3 modelMax)
        {
            modelMin = Vector3.Zero;
            modelMax = Vector3.Zero;
            if (vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            vehicle.Model.GetDimensions(out modelMin, out modelMax);
            return (modelMax.X - modelMin.X) > 0.05f && (modelMax.Y - modelMin.Y) > 0.05f;
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

        private void CleanupOwnedRigs(bool deleteVehicles)
        {
            for (int i = _ownedRigs.Count - 1; i >= 0; i--)
            {
                var rig = _ownedRigs[i];
                var truck = ResolveVehicleHandle(rig.TruckHandle);
                var cargoVehicle = ResolveVehicleHandle(rig.CargoHandle);
                var hasTruck = truck != null && truck.Exists();
                var hasCargoVehicle = cargoVehicle != null && cargoVehicle.Exists();
                var singleVehicleRig = rig.TruckHandle == rig.CargoHandle;
                var isAlive = hasTruck && (singleVehicleRig || hasCargoVehicle);

                if (deleteVehicles)
                {
                    if (hasCargoVehicle)
                    {
                        cargoVehicle.Delete();
                    }

                    if (hasTruck && (!hasCargoVehicle || truck.Handle != cargoVehicle.Handle))
                    {
                        truck.Delete();
                    }

                    RemoveCargoState(rig.CargoHandle);
                    if (!singleVehicleRig)
                    {
                        RemoveCargoState(rig.TruckHandle);
                    }

                    _ownedRigs.RemoveAt(i);
                    continue;
                }

                if (!isAlive)
                {
                    _ownedRigs.RemoveAt(i);
                }
            }
        }

        private void RemoveCargoState(int vehicleHandle)
        {
            VehicleCargoState state;
            if (!_cargoStates.TryGetValue(vehicleHandle, out state))
            {
                return;
            }

            ClearCargoVisuals(state);
            _cargoStates.Remove(vehicleHandle);
        }

        private static Vehicle ResolveVehicleHandle(int handle)
        {
            return Entity.FromHandle(handle) as Vehicle;
        }

        private sealed class OwnedFleetRig
        {
            public int TruckHandle { get; set; }

            public int CargoHandle { get; set; }

            public bool Matches(int truckHandle, int cargoHandle)
            {
                return (TruckHandle == truckHandle && CargoHandle == cargoHandle)
                    || (TruckHandle == cargoHandle && CargoHandle == truckHandle)
                    || TruckHandle == truckHandle
                    || CargoHandle == cargoHandle;
            }
        }
    }
}

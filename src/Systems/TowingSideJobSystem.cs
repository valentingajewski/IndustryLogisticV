using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Config;
using LSOL.Domain;
using LSOL.UI;

namespace LSOL.Systems
{
    /// <summary>
    /// Persisted state for the towing side job. Follows the same section-based
    /// persistence pattern as the other systems (see IndustryPersistenceManager).
    /// Legacy saves without a towing section deserialize to <c>null</c> and load safely.
    /// </summary>
    public sealed class TowingPersistenceSnapshot
    {
        public TowingPersistenceSnapshot()
        {
            DamagedVehicles = new List<TowingDamagedVehicleSnapshot>();
            LastPointIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public int NextSpawnId { get; set; }

        public List<TowingDamagedVehicleSnapshot> DamagedVehicles { get; }

        /// <summary>
        /// Last authored JobCoordinates site used in each district. Kept so a replacement wreck never
        /// lands on the coordinates of the one that was just towed away, even across a reload.
        /// </summary>
        public Dictionary<string, int> LastPointIds { get; }

        public TowingActiveTowSnapshot ActiveTow { get; set; }

        public bool HasData
        {
            get
            {
                return (DamagedVehicles != null && DamagedVehicles.Count > 0)
                    || ActiveTow != null;
            }
        }
    }

    public sealed class TowingDamagedVehicleSnapshot
    {
        public int SpawnId { get; set; }

        /// <summary>Authored JobCoordinates JobPoint id of the site this wreck was placed on.</summary>
        public int PointId { get; set; }

        /// <summary>District the site belongs to; drives the per-district wreck rotation.</summary>
        public string DistrictName { get; set; }

        public string ModelName { get; set; }

        public string DisplayName { get; set; }

        public float WeightTons { get; set; }

        public Vector3 Position { get; set; }

        public float Heading { get; set; }

        public int SpawnedAtGameTime { get; set; }
    }

    public sealed class TowingActiveTowSnapshot
    {
        public string TowTruckModelName { get; set; }

        public int TowedSpawnId { get; set; }
    }

    public sealed class TowTruckDefinition
    {
        public string Name { get; set; }
        public string ModelName { get; set; }
        public int UnlockLevel { get; set; }
        public float TowCapacityTons { get; set; }
        public float Price { get; set; }
        public float DailyRent { get; set; }
        public float FuelCapacityLiters { get; set; }
    }

    /// <summary>
    /// Self-contained towing side job. Damaged vehicles sit on the curated JobCoordinates
    /// "DamagedVehicle" sites, one or two per district, and the player hooks one with a real tow
    /// truck (the Rockstar tow mechanic, no mod interaction key) and delivers it to a garage for
    /// cash + Towing XP. Every delivered wreck is replaced by another site of the same district, so
    /// the wrecks stay scattered across the map and never repeat the same coordinates twice in a row.
    ///
    /// Progression reuses the existing <see cref="PlayerSkillSystem"/>
    /// (<see cref="PlayerSkillId.Towing"/>) — no new progression store is created.
    /// </summary>
    internal sealed class TowingSideJobSystem
    {
        // Wrecks only spawn on the authored JobCoordinates "DamagedVehicle" sites now: the old
        // random road rolls piled every wreck around the player. One or two sites per district are
        // kept occupied, so wrecks stay scattered over the whole map and a district is never empty.
        private const int MinWrecksPerDistrict = 1;
        private const int MaxWrecksPerDistrict = 2;

        /// <summary>Hard ceiling on materialised wrecks: the map can never be flooded.</summary>
        private const int MaxActiveDamagedVehicles = 22;

        private const int SpawnRefreshIntervalMs = 20000;
        private const int InitialSpawnDelayMs = 20000;

        /// <summary>Wrecks materialised per refresh, so the pool grows outward instead of hitching.</summary>
        private const int MaxSpawnsPerRefresh = 3;

        /// <summary>
        /// Districts farther than this from the player are left empty for now. Nothing is created
        /// outside the streamed world; the pool reaches the rest of the map as the player travels.
        /// </summary>
        private const float SpawnMaxDistanceFromPlayer = 2500f;

        /// <summary>Never materialise a wreck in front of the player.</summary>
        private const float SpawnMinDistanceFromPlayer = 75f;

        /// <summary>
        /// A wreck nobody touched for this long moves to another site of its district, so no single
        /// spot becomes "the" towing address.
        /// </summary>
        private const int WreckRelocateAgeMs = 900000;

        /// <summary>Only wrecks this far away relocate: the player can never watch one move.</summary>
        private const float RelocateMinDistanceFromPlayer = 600f;

        /// <summary>Position tolerance used to re-attach a legacy saved wreck to its authored site.</summary>
        private const float RestorePointMatchDistance = 25f;

        /// <summary>
        /// How high above the authored Z the ground probe starts. The ground native only searches
        /// downwards, so the probe needs headroom: starting at the authored height itself finds
        /// nothing and used to leave every wreck hanging in the air.
        /// </summary>
        private const float GroundProbeHeight = 5f;

        /// <summary>How far the probed ground may sit below the authored Z before the reading is discarded.</summary>
        private const float GroundProbeMaxDrop = 60f;

        /// <summary>How far the probed ground may sit above the authored Z before the reading is discarded.</summary>
        private const float GroundProbeMaxRise = 15f;

        /// <summary>A wreck floating higher (or sunk deeper) than this above the road gets re-seated.</summary>
        private const float MaxWreckHeightAboveGround = 1.0f;

        /// <summary>A re-seat is only written when it moves the wreck by at least this much.</summary>
        private const float GroundReseatMinDelta = 0.05f;

        private const float DropoffDeliveryDistance = 15f;
        private const float DropoffMarkerDrawDistance = 400f;
        private const float TowPointInteractDistance = 6f;

        private readonly string _configDirectory;
        private readonly PlayerSkillSystem _skillSystem;
        private readonly Action<string> _showStatus;
        private readonly Action<float> _addProfit;
        private readonly Action _requestAutosave;

        // District bonus access, injected as callbacks so this system keeps no TerritoryManager
        // dependency and stays unit testable.
        private readonly Func<string, string, float> _getDistrictBonusExcludingJob;
        private readonly Func<string, string, float, DistrictBonusAward> _reportDistrictBonus;
        private readonly Random _random;

        private readonly List<TowTruckDefinition> _towTrucks;
        private readonly List<TowDropoffPoint> _dropoffs;
        private readonly List<TowableVehicleDefinition> _towables;
        private readonly List<DamagedVehicleDistrict> _damagedDistricts;
        private readonly List<DamagedVehicleInstance> _activeDamaged;
        private readonly List<Blip> _dropoffBlips;

        private DamagedVehicleInstance _towed;
        private int _nextSpawnId = 1;
        private int _nextRefreshGameTime;
        private int _nextBucketIndex;
        private bool _modMechanicsEnabled = true;
        private bool _jobEnabled = true;
        private bool _nearDropoffHint;
        private bool _detachHintShown;

        public TowingSideJobSystem(
            string configDirectory,
            IReadOnlyList<DealershipVehicleDefinition> dealershipCatalog,
            PlayerSkillSystem skillSystem,
            Action<string> showStatus,
            Action<float> addProfit,
            Action requestAutosave,
            Func<string, string, float> getDistrictBonusExcludingJob = null,
            Func<string, string, float, DistrictBonusAward> reportDistrictBonus = null)
        {
            _configDirectory = configDirectory ?? string.Empty;
            _skillSystem = skillSystem;
            _showStatus = showStatus;
            _addProfit = addProfit;
            _requestAutosave = requestAutosave;
            _getDistrictBonusExcludingJob = getDistrictBonusExcludingJob;
            _reportDistrictBonus = reportDistrictBonus;
            _random = new Random();

            _dropoffs = LoadDropoffs(_configDirectory);
            _towTrucks = LoadTowTrucks(_configDirectory);
            _towables = BuildTowableCatalog(dealershipCatalog);
            _damagedDistricts = LoadDamagedDistricts(_configDirectory, SideJobConfigLoader.LoadDistricts(_configDirectory));
            _activeDamaged = new List<DamagedVehicleInstance>();
            _dropoffBlips = new List<Blip>();
            _nextRefreshGameTime = Game.GameTime + InitialSpawnDelayMs;
        }

        public void Update(Ped player, int gameTime)
        {
            if (player == null || !player.Exists())
            {
                return;
            }

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                return;
            }

            UpdateDropoffHint(player);
            EnsureDropoffBlips();
            DrawDropoffMarkers(player);
            RefreshDamagedSet(player, gameTime);
            UpdateDamagedBlips();
            UpdateTowDetection();
            UpdateDelivery();
        }

        public void SetModMechanicsEnabled(bool enabled)
        {
            if (_modMechanicsEnabled == enabled)
            {
                return;
            }

            _modMechanicsEnabled = enabled;
            if (!enabled)
            {
                HideWorldVisuals();
            }
            else
            {
                _nextRefreshGameTime = Game.GameTime;
            }
        }

        public void SetJobEnabled(bool enabled)
        {
            if (_jobEnabled == enabled)
            {
                return;
            }

            _jobEnabled = enabled;
            if (!enabled)
            {
                HideWorldVisuals();
            }
            else
            {
                _nextRefreshGameTime = Game.GameTime;
            }
        }

        public IReadOnlyList<TowTruckDefinition> GetTowTrucks()
        {
            return _towTrucks;
        }

        public bool TryGetNearestTowPoint(Vector3 position, float maxDistance, out Vector3 spawnPosition, out string pointName, out float heading)
        {
            spawnPosition = Vector3.Zero;
            pointName = string.Empty;
            heading = 0f;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                return false;
            }

            TowDropoffPoint nearest = null;
            var nearestDistance = maxDistance;
            for (int i = 0; i < _dropoffs.Count; i++)
            {
                var point = _dropoffs[i];
                var distance = point.Position.DistanceTo(position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = point;
                }
            }

            if (nearest == null)
            {
                return false;
            }

            spawnPosition = nearest.Position;
            pointName = nearest.Name;
            heading = nearest.Heading;
            return true;
        }

        public bool TrySpawnTowTruck(string modelName, Ped player, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = "The towing side job is inactive.";
                return false;
            }

            var definition = _towTrucks.FirstOrDefault(t => t != null
                && string.Equals(t.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                message = "Unknown tow truck.";
                return false;
            }

            var towingLevel = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.Towing) : 0;
            if (towingLevel < definition.UnlockLevel)
            {
                message = string.Format("{0} requires Towing level {1}.", definition.Name, definition.UnlockLevel);
                return false;
            }

            var point = GetNearestDropoff(player.Position);
            if (point == null)
            {
                message = "No tow dropoff configured.";
                return false;
            }

            var vehicle = SpawnVehicle(definition.ModelName, point.Position, point.Heading, true);
            if (vehicle == null || !vehicle.Exists())
            {
                message = "Could not spawn the tow truck.";
                return false;
            }

            try
            {
                player.Task.WarpIntoVehicle(vehicle, VehicleSeat.Driver);
            }
            catch
            {
                try
                {
                    player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                }
                catch
                {
                    // Seating is best-effort; the vehicle is still spawned.
                }
            }

            message = string.Format("{0} ready at {1}.", definition.Name, point.Name);
            _requestAutosave?.Invoke();
            return true;
        }

        public void ResetState()
        {
            _towed = null;

            for (int i = _activeDamaged.Count - 1; i >= 0; i--)
            {
                DeleteDamagedInstance(_activeDamaged[i]);
            }

            _activeDamaged.Clear();

            _nextSpawnId = 1;
            _nextBucketIndex = 0;
            _nextRefreshGameTime = Game.GameTime + InitialSpawnDelayMs;
            _nearDropoffHint = false;
            _detachHintShown = false;

            for (int i = 0; i < _damagedDistricts.Count; i++)
            {
                _damagedDistricts[i].LastPointId = 0;
            }

            ClearGps();
            ClearDropoffBlips();
        }

        public TowingPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new TowingPersistenceSnapshot
            {
                NextSpawnId = Math.Max(1, _nextSpawnId),
            };

            foreach (var instance in _activeDamaged)
            {
                if (instance == null || string.IsNullOrWhiteSpace(instance.ModelName))
                {
                    continue;
                }

                snapshot.DamagedVehicles.Add(new TowingDamagedVehicleSnapshot
                {
                    SpawnId = instance.SpawnId,
                    PointId = instance.PointId,
                    DistrictName = instance.DistrictName,
                    ModelName = instance.ModelName,
                    DisplayName = instance.DisplayName,
                    WeightTons = instance.WeightTons,
                    Position = instance.SpawnPosition,
                    Heading = instance.SpawnHeading,
                    SpawnedAtGameTime = instance.SpawnedAtGameTime,
                });
            }

            for (int i = 0; i < _damagedDistricts.Count; i++)
            {
                var district = _damagedDistricts[i];
                if (district.LastPointId != 0)
                {
                    snapshot.LastPointIds[district.Name] = district.LastPointId;
                }
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(TowingPersistenceSnapshot snapshot)
        {
            ResetState();
            if (snapshot == null)
            {
                return;
            }

            _nextSpawnId = Math.Max(1, snapshot.NextSpawnId);

            if (snapshot.LastPointIds != null)
            {
                foreach (var pair in snapshot.LastPointIds)
                {
                    var lastDistrict = FindDistrict(pair.Key);
                    if (lastDistrict != null)
                    {
                        lastDistrict.LastPointId = pair.Value;
                    }
                }
            }

            if (snapshot.DamagedVehicles != null)
            {
                for (int i = 0; i < snapshot.DamagedVehicles.Count; i++)
                {
                    var entry = snapshot.DamagedVehicles[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ModelName))
                    {
                        continue;
                    }

                    RestoreDamagedVehicle(entry);
                }
            }
        }

        /// <summary>
        /// Keeps one or two wrecks per district alive. Districts are filled nearest-first and only a
        /// few at a time, so the pool spreads outward from the player instead of appearing at once.
        /// A wreck that has been ignored for a long time moves to another site of its district.
        /// </summary>
        private void RefreshDamagedSet(Ped player, int gameTime)
        {
            CleanupMissingEntities();

            if (gameTime < _nextRefreshGameTime)
            {
                return;
            }

            _nextRefreshGameTime = gameTime + SpawnRefreshIntervalMs + _random.Next(0, 10000);

            EnsureWrecksOnGround();
            RelocateAgedWrecks(player, gameTime);

            var spawned = 0;
            foreach (var district in OrderDistrictsByDistance(player.Position))
            {
                if (spawned >= MaxSpawnsPerRefresh || _activeDamaged.Count >= MaxActiveDamagedVehicles)
                {
                    break;
                }

                if (CountActiveInDistrict(district.Name) >= district.TargetCount)
                {
                    continue;
                }

                if (NearestPointDistance(district, player.Position) > SpawnMaxDistanceFromPlayer)
                {
                    // Out of streaming range: the district keeps its current wrecks and is filled when
                    // the player gets closer.
                    continue;
                }

                if (TrySpawnAtDistrictPoint(district, player.Position, gameTime))
                {
                    spawned += 1;
                }
            }
        }

        private void CleanupMissingEntities()
        {
            for (int i = _activeDamaged.Count - 1; i >= 0; i--)
            {
                var instance = _activeDamaged[i];
                if (instance == null || instance.IsTowed)
                {
                    continue;
                }

                if (instance.Vehicle == null || !instance.Vehicle.Exists())
                {
                    DeleteDamagedInstance(instance);
                }
            }
        }

        /// <summary>
        /// Moves at most one wreck per refresh to another site of its district. Sites the player is
        /// close enough to see are left alone, so a blip never jumps under the player's eyes.
        /// </summary>
        private void RelocateAgedWrecks(Ped player, int gameTime)
        {
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance == null || instance.IsTowed || instance.PointId == 0)
                {
                    continue;
                }

                if (gameTime - instance.SpawnedAtGameTime < WreckRelocateAgeMs)
                {
                    continue;
                }

                if (instance.Vehicle == null || !instance.Vehicle.Exists())
                {
                    continue;
                }

                if (instance.Vehicle.Position.DistanceTo(player.Position) < RelocateMinDistanceFromPlayer)
                {
                    // The player is around: keep it where it is and restart its timer.
                    instance.SpawnedAtGameTime = gameTime;
                    continue;
                }

                var district = FindDistrict(instance.DistrictName);
                if (district == null)
                {
                    instance.SpawnedAtGameTime = gameTime;
                    continue;
                }

                if (NearestPointDistance(district, player.Position) > SpawnMaxDistanceFromPlayer)
                {
                    // Same streaming rule as spawning: a relocation may not create an entity in a
                    // district the game has not loaded yet.
                    instance.SpawnedAtGameTime = gameTime;
                    continue;
                }

                DeleteDamagedInstance(instance);
                TrySpawnAtDistrictPoint(district, player.Position, gameTime);
                return;
            }
        }

        /// <summary>
        /// Spawns a wreck on one of the district's authored sites. The site that was used last time
        /// and the sites already occupied are skipped, so two consecutive wrecks never share
        /// coordinates, and sites too close to the player are skipped so nothing pops in front of them.
        /// </summary>
        private bool TrySpawnAtDistrictPoint(DamagedVehicleDistrict district, Vector3 playerPosition, int gameTime)
        {
            if (district == null)
            {
                return false;
            }

            var point = PickDistrictPoint(district, playerPosition);
            if (point == null)
            {
                return false;
            }

            var definition = PickBalancedTowable();
            if (definition == null)
            {
                return false;
            }

            var vehicle = SpawnDamagedVehicleEntity(definition.ModelName, point.Position, point.Heading);
            if (vehicle == null)
            {
                return false;
            }

            var instance = new DamagedVehicleInstance
            {
                SpawnId = _nextSpawnId,
                PointId = point.PointId,
                DistrictName = district.Name,
                ModelName = definition.ModelName,
                DisplayName = definition.DisplayName,
                WeightTons = definition.WeightTons,
                Vehicle = vehicle,
                SpawnPosition = point.Position,
                SpawnHeading = point.Heading,
                SpawnedAtGameTime = gameTime,
            };
            _nextSpawnId += 1;
            instance.Blip = CreateDamagedVehicleBlip(instance);
            _activeDamaged.Add(instance);
            district.LastPointId = point.PointId;
            return true;
        }

        private DamagedVehiclePoint PickDistrictPoint(DamagedVehicleDistrict district, Vector3 playerPosition)
        {
            var candidates = new List<DamagedVehiclePoint>();
            for (int i = 0; i < district.Points.Count; i++)
            {
                var point = district.Points[i];
                if (point.PointId == district.LastPointId || IsPointOccupied(point.PointId))
                {
                    continue;
                }

                if (SideJobConfigLoader.DistanceBetween(point.Position, playerPosition) < SpawnMinDistanceFromPlayer)
                {
                    continue;
                }

                candidates.Add(point);
            }

            if (candidates.Count == 0)
            {
                // Every free site of the district is under the player's feet right now; the next
                // refresh tries again.
                return null;
            }

            return candidates[_random.Next(candidates.Count)];
        }

        private bool IsPointOccupied(int pointId)
        {
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance != null && instance.PointId == pointId)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountActiveInDistrict(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0;
            }

            var count = 0;
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance != null && string.Equals(instance.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    count += 1;
                }
            }

            return count;
        }

        private DamagedVehicleDistrict FindDistrict(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return null;
            }

            for (int i = 0; i < _damagedDistricts.Count; i++)
            {
                var district = _damagedDistricts[i];
                if (string.Equals(district.Name, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    return district;
                }
            }

            return null;
        }

        private List<DamagedVehicleDistrict> OrderDistrictsByDistance(Vector3 position)
        {
            var ordered = new List<DamagedVehicleDistrict>(_damagedDistricts);
            ordered.Sort((left, right) =>
                NearestPointDistance(left, position).CompareTo(NearestPointDistance(right, position)));
            return ordered;
        }

        private static float NearestPointDistance(DamagedVehicleDistrict district, Vector3 position)
        {
            var nearest = float.MaxValue;
            for (int i = 0; i < district.Points.Count; i++)
            {
                var distance = SideJobConfigLoader.DistanceBetween(district.Points[i].Position, position);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private TowableVehicleDefinition PickBalancedTowable()
        {
            var light = _towables.Where(t => t.WeightTons <= 1.5f).ToList();
            var medium = _towables.Where(t => t.WeightTons > 1.5f && t.WeightTons <= 2.5f).ToList();
            var heavy = _towables.Where(t => t.WeightTons > 2.5f).ToList();
            var buckets = new[] { light, medium, heavy };

            for (int offset = 0; offset < buckets.Length; offset++)
            {
                var index = (_nextBucketIndex + offset) % buckets.Length;
                if (buckets[index].Count > 0)
                {
                    _nextBucketIndex = index + 1;
                    return buckets[index][_random.Next(buckets[index].Count)];
                }
            }

            return _towables.Count > 0 ? _towables[_random.Next(_towables.Count)] : null;
        }

        private void UpdateTowDetection()
        {
            if (_towed == null)
            {
                for (int i = 0; i < _activeDamaged.Count; i++)
                {
                    var instance = _activeDamaged[i];
                    if (instance == null || instance.IsTowed || instance.Vehicle == null || !instance.Vehicle.Exists())
                    {
                        continue;
                    }

                    if (GetAttachedToHandle(instance.Vehicle) == 0)
                    {
                        continue;
                    }

                    instance.IsTowed = true;
                    _towed = instance;

                    var dropoff = GetNearestDropoff(instance.Vehicle.Position);
                    if (dropoff != null)
                    {
                        SetGps(dropoff.Position);
                    }

                    _showStatus?.Invoke(string.Format(
                        "Towing {0} ({1:0.#}t). GPS set to {2}.",
                        instance.DisplayName,
                        instance.WeightTons,
                        dropoff != null ? dropoff.Name : "garage"));
                    _requestAutosave?.Invoke();
                    return;
                }

                return;
            }

            var towedVehicle = _towed.Vehicle;
            if (towedVehicle == null || !towedVehicle.Exists())
            {
                _towed.IsTowed = false;
                _towed = null;
                ClearGps();
                _showStatus?.Invoke("Tow interrupted: the towed vehicle was lost.");
                return;
            }

            if (GetAttachedToHandle(towedVehicle) != 0)
            {
                return;
            }

            var detachPoint = GetNearestDropoff(towedVehicle.Position);
            if (detachPoint == null || towedVehicle.Position.DistanceTo(detachPoint.Position) > DropoffDeliveryDistance)
            {
                _towed.IsTowed = false;
                _towed = null;
                ClearGps();
                _showStatus?.Invoke("The tow came loose.");
            }
        }

        private void UpdateDelivery()
        {
            if (_towed == null)
            {
                return;
            }

            var vehicle = _towed.Vehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            var dropoff = GetNearestDropoff(vehicle.Position);
            var onMarker = dropoff != null && vehicle.Position.DistanceTo(dropoff.Position) <= DropoffDeliveryDistance;
            if (!onMarker)
            {
                _detachHintShown = false;
                return;
            }

            if (GetAttachedToHandle(vehicle) == 0)
            {
                _detachHintShown = false;
                CompleteDelivery(dropoff);
                return;
            }

            if (!_detachHintShown)
            {
                _detachHintShown = true;
                _showStatus?.Invoke("On the marker. Detach the tow to deliver.");
            }
        }

        private void CompleteDelivery(TowDropoffPoint dropoff)
        {
            var delivered = _towed;
            if (delivered == null)
            {
                return;
            }

            var weight = Math.Max(1f, Math.Min(3f, delivered.WeightTons));
            var xp = 75f + (weight - 1f) * 37.5f;
            var baseCash = 500f + (weight - 1f) * 500f;
            var bonus = _skillSystem != null ? _skillSystem.GetBonusMultiplier(PlayerSkillId.Towing) : 1f;
            var districtName = dropoff != null ? dropoff.DistrictName : string.Empty;
            var cash = (float)Math.Round(baseCash * bonus * GetDistrictPayoutMultiplier(districtName));

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.Towing, xp, PlayerSkillXpSource.Player);
            }

            _addProfit?.Invoke(cash);

            _towed = null;
            DeleteDamagedInstance(delivered);
            ClearGps();

            _showStatus?.Invoke(string.Concat(
                string.Format(
                    "Delivered {0} ({1:0.#}t) to {2}: {3} + {4:0} Towing XP.",
                    delivered.DisplayName,
                    delivered.WeightTons,
                    dropoff != null ? dropoff.Name : "garage",
                    ModFormatting.FormatMoney(cash),
                    xp),
                ReportDistrictCompletion(districtName)));
            _requestAutosave?.Invoke();
        }

        /// <summary>
        /// District revenue multiplier for this job's own payouts. Uses the district bonus with the
        /// Towing pool excluded, so towing work can never boost its own payout.
        /// </summary>
        private float GetDistrictPayoutMultiplier(string districtName)
        {
            if (_getDistrictBonusExcludingJob == null || string.IsNullOrWhiteSpace(districtName))
            {
                return 1f;
            }

            try
            {
                var percent = Math.Max(0f, _getDistrictBonusExcludingJob(districtName, DistrictBonusCatalog.TowingJobId));
                return 1f + (percent / 100f);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Credits a finished tow to the dropoff garage's district and returns the status suffix
        /// describing the gain. Garages without a district tag contribute nothing and stay silent.
        /// </summary>
        private string ReportDistrictCompletion(string districtName)
        {
            if (_reportDistrictBonus == null || string.IsNullOrWhiteSpace(districtName))
            {
                return string.Empty;
            }

            try
            {
                var award = _reportDistrictBonus(DistrictBonusCatalog.TowingJobId, districtName, 1f);
                if (award == null || !award.Applied || award.AppliedPoints <= 0f)
                {
                    return string.Empty;
                }

                return string.Concat(
                    " ",
                    LocalizedText.FormatOrDefault(
                        "sidejob.bonus.districtBonus",
                        "District bonus {0} +{1:0.#}% ({2:0.#} / {3:0.#}%).",
                        ModFormatting.FormatDistrictName(districtName),
                        award.AppliedPoints,
                        award.TotalPercent,
                        award.CapPercent));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int GetAttachedToHandle(Entity entity)
        {
            if (entity == null || !entity.Exists())
            {
                return 0;
            }

            try
            {
                return Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, entity.Handle);
            }
            catch
            {
                return 0;
            }
        }

        private TowDropoffPoint GetNearestDropoff(Vector3 position)
        {
            TowDropoffPoint nearest = null;
            var nearestDistance = float.MaxValue;
            for (int i = 0; i < _dropoffs.Count; i++)
            {
                var point = _dropoffs[i];
                var distance = point.Position.DistanceTo(position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = point;
                }
            }

            return nearest;
        }

        private void UpdateDropoffHint(Ped player)
        {
            var point = GetNearestDropoff(player.Position);
            var near = point != null && player.Position.DistanceTo(point.Position) <= TowPointInteractDistance;
            if (near && !_nearDropoffHint)
            {
                _nearDropoffHint = true;
                _showStatus?.Invoke(string.Format("Tow dropoff: {0}. Press Interact for tow trucks.", point.Name));
            }
            else if (!near)
            {
                _nearDropoffHint = false;
            }
        }

        private void EnsureDropoffBlips()
        {
            if (_dropoffBlips.Count == _dropoffs.Count)
            {
                return;
            }

            ClearDropoffBlips();

            for (int i = 0; i < _dropoffs.Count; i++)
            {
                var point = _dropoffs[i];
                var blip = World.CreateBlip(point.Position);
                if (blip == null || !blip.Exists())
                {
                    continue;
                }

                // "radar_arena_work" maps to the Arena War workshop sprite; the
                // SHVDN BlipSprite enum exposes it as ArenaWorkshop (643).
                blip.Sprite = BlipSprite.ArenaWorkshop;
                blip.Color = BlipColor.Green;
                blip.Name = string.IsNullOrWhiteSpace(point.Name) ? "Tow Dropoff" : point.Name;
                blip.Scale = 0.9f;
                BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
                _dropoffBlips.Add(blip);
            }
        }

        private void ClearDropoffBlips()
        {
            for (int i = 0; i < _dropoffBlips.Count; i++)
            {
                var blip = _dropoffBlips[i];
                if (blip != null && blip.Exists())
                {
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

            _dropoffBlips.Clear();
        }

        private void ClearDamagedBlips()
        {
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance == null)
                {
                    continue;
                }

                if (instance.Blip != null && instance.Blip.Exists())
                {
                    try
                    {
                        instance.Blip.Delete();
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }

                instance.Blip = null;
            }
        }

        private void HideWorldVisuals()
        {
            _towed = null;

            for (int i = _activeDamaged.Count - 1; i >= 0; i--)
            {
                DeleteDamagedInstance(_activeDamaged[i]);
            }

            _activeDamaged.Clear();

            ClearGps();
            ClearDropoffBlips();
            _nearDropoffHint = false;
            _detachHintShown = false;
        }

        private void DrawDropoffMarkers(Ped player)
        {
            for (int i = 0; i < _dropoffs.Count; i++)
            {
                var point = _dropoffs[i];
                if (player.Position.DistanceTo(point.Position) > DropoffMarkerDrawDistance)
                {
                    continue;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    point.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(2f, 2f, 1.5f),
                    Color.FromArgb(180, 92, 208, 144),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }
        }

        private Blip CreateDamagedVehicleBlip(DamagedVehicleInstance instance)
        {
            if (instance == null || instance.Vehicle == null || !instance.Vehicle.Exists())
            {
                return null;
            }

            var blip = World.CreateBlip(instance.Vehicle.Position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = BlipSprite.TowTruck;
            blip.Color = BlipColor.Yellow;
            blip.Name = string.Format("Damaged {0} ({1:0.#}t)", instance.DisplayName, instance.WeightTons);
            blip.Scale = 0.85f;
            BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
            return blip;
        }

        private void UpdateDamagedBlips()
        {
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance == null)
                {
                    continue;
                }

                if (instance.IsTowed || instance.Vehicle == null || !instance.Vehicle.Exists())
                {
                    if (instance.Blip != null && instance.Blip.Exists())
                    {
                        try
                        {
                            instance.Blip.Delete();
                        }
                        catch
                        {
                            // Best-effort cleanup.
                        }
                    }

                    instance.Blip = null;
                    continue;
                }

                var blip = instance.Blip;
                if (blip == null || !blip.Exists())
                {
                    instance.Blip = CreateDamagedVehicleBlip(instance);
                }
                else
                {
                    blip.Position = instance.Vehicle.Position;
                }
            }
        }

        private void DeleteDamagedInstance(DamagedVehicleInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            // Remember the site the wreck leaves behind, whatever removed it (delivered, relocated or
            // cleaned up by the game), so the replacement never reuses the same coordinates twice in a
            // row in that district.
            if (instance.PointId != 0)
            {
                var district = FindDistrict(instance.DistrictName);
                if (district != null)
                {
                    district.LastPointId = instance.PointId;
                }
            }

            if (instance.Vehicle != null && instance.Vehicle.Exists())
            {
                try
                {
                    instance.Vehicle.Detach();
                }
                catch
                {
                    // Best-effort detach.
                }

                try
                {
                    instance.Vehicle.Delete();
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }

            if (instance.Blip != null && instance.Blip.Exists())
            {
                try
                {
                    instance.Blip.Delete();
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }

            instance.Vehicle = null;
            instance.Blip = null;
            _activeDamaged.Remove(instance);
        }

        private void RestoreDamagedVehicle(TowingDamagedVehicleSnapshot entry)
        {
            var vehicle = SpawnDamagedVehicleEntity(entry.ModelName, entry.Position, entry.Heading);
            if (vehicle == null)
            {
                return;
            }

            // Saves written before wrecks moved onto authored sites have no site id: re-attach them
            // by position so the district rotation still knows about them.
            var point = entry.PointId != 0 ? null : FindPointNear(entry.Position, RestorePointMatchDistance);
            var district = FindDistrict(entry.DistrictName);
            if (district == null && point != null)
            {
                district = FindDistrict(point.DistrictName);
            }

            var instance = new DamagedVehicleInstance
            {
                SpawnId = entry.SpawnId,
                PointId = entry.PointId != 0 ? entry.PointId : (point != null ? point.PointId : 0),
                DistrictName = district != null ? district.Name : (entry.DistrictName ?? string.Empty),
                ModelName = entry.ModelName,
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.ModelName : entry.DisplayName,
                WeightTons = entry.WeightTons,
                Vehicle = vehicle,
                SpawnPosition = entry.Position,
                SpawnHeading = entry.Heading,
                SpawnedAtGameTime = Game.GameTime,
            };
            instance.Blip = CreateDamagedVehicleBlip(instance);
            _activeDamaged.Add(instance);
        }

        /// <summary>
        /// Nearest authored wreck site to a saved position, used to re-attach legacy wrecks that were
        /// persisted without their site id.
        /// </summary>
        private DamagedVehiclePoint FindPointNear(Vector3 position, float maxDistance)
        {
            DamagedVehiclePoint nearest = null;
            var nearestDistance = maxDistance;
            for (int i = 0; i < _damagedDistricts.Count; i++)
            {
                var district = _damagedDistricts[i];
                for (int j = 0; j < district.Points.Count; j++)
                {
                    var point = district.Points[j];
                    var distance = SideJobConfigLoader.DistanceBetween(point.Position, position);
                    if (distance <= nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = point;
                    }
                }
            }

            return nearest;
        }

        private static Vehicle SpawnVehicle(string modelName, Vector3 position, float heading, bool persistent)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            var model = new Model(modelName);
            if (!model.Request(1500))
            {
                model.MarkAsNoLongerNeeded();
                return null;
            }

            Vehicle vehicle = null;
            try
            {
                vehicle = World.CreateVehicle(model, position, heading);
            }
            catch
            {
                vehicle = null;
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }

            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            vehicle.IsPersistent = persistent;
            return vehicle;
        }

        /// <summary>
        /// Resolves the road height a wreck must be created at. The authored Z is only a hint: it is
        /// used as the sanity band for the probe and as the fallback when no collision is found (the
        /// district may not be streamed yet).
        /// </summary>
        private static float ResolveWreckGroundZ(Vector3 authored)
        {
            // GET_GROUND_Z_FOR_3D_COORD only searches DOWNWARDS from the given Z, so the probe has to
            // start above the surface. Probing from the authored height itself finds nothing whenever
            // that height already sits on the road, which is how wrecks ended up hanging in the air.
            var probe = new Vector3(authored.X, authored.Y, authored.Z + GroundProbeHeight);
            try
            {
                float groundZ;
                if (World.GetGroundHeight(probe, out groundZ, GetGroundHeightMode.Normal)
                    && groundZ >= authored.Z - GroundProbeMaxDrop
                    && groundZ <= authored.Z + GroundProbeMaxRise)
                {
                    return groundZ;
                }
            }
            catch
            {
                // Fall through to the authored height.
            }

            return authored.Z;
        }

        /// <summary>
        /// Safety net for a wreck that could not be seated when it spawned (a district created just
        /// before it streamed in): anything still off the ground is put back, unless it is being
        /// towed, in which case it is supposed to be in the air.
        /// </summary>
        private void EnsureWrecksOnGround()
        {
            for (int i = 0; i < _activeDamaged.Count; i++)
            {
                var instance = _activeDamaged[i];
                if (instance == null || instance.IsTowed
                    || instance.Vehicle == null || !instance.Vehicle.Exists())
                {
                    continue;
                }

                if (GetAttachedToHandle(instance.Vehicle) != 0)
                {
                    // Already hooked onto a tow truck (possibly this very frame, before the tow
                    // detection below ran): it is meant to be off the ground.
                    continue;
                }

                float heightAboveGround;
                try
                {
                    heightAboveGround = Function.Call<float>(Hash.GET_ENTITY_HEIGHT_ABOVE_GROUND, instance.Vehicle.Handle);
                }
                catch
                {
                    continue;
                }

                if (Math.Abs(heightAboveGround) <= MaxWreckHeightAboveGround)
                {
                    continue;
                }

                var position = instance.Vehicle.Position;
                var groundZ = ResolveWreckGroundZ(instance.SpawnPosition);
                if (Math.Abs(groundZ - position.Z) < GroundReseatMinDelta)
                {
                    // The probe has nothing better to offer than where the wreck already is.
                    continue;
                }

                try
                {
                    // The wreck is frozen: this is a plain teleport, no physics pass, so it cannot be
                    // nudged or lifted.
                    instance.Vehicle.Position = new Vector3(position.X, position.Y, groundZ);
                }
                catch
                {
                    // Best-effort.
                }
            }
        }

        private static Vehicle SpawnDamagedVehicleEntity(string modelName, Vector3 position, float heading)
        {
            // The wreck is created at the resolved road height, not at the authored Z: it is frozen
            // right after creation, so it never gets the chance to fall onto the road, and anything
            // placed above the surface stays hanging in the air for good.
            var spawnPosition = new Vector3(position.X, position.Y, ResolveWreckGroundZ(position));

            var vehicle = SpawnVehicle(modelName, spawnPosition, heading, true);
            if (vehicle == null)
            {
                return null;
            }

            vehicle.EngineHealth = 300f;
            vehicle.BodyHealth = 600f;
            vehicle.IsEngineRunning = false;
            vehicle.LockStatus = VehicleLockStatus.CannotEnter;
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, false, true, true);

            Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle.Handle, true);
            return vehicle;
        }

        private static void SetGps(Vector3 position)
        {
            try
            {
                Function.Call(Hash.SET_NEW_WAYPOINT, position.X, position.Y);
            }
            catch
            {
                // Waypoint setup is best-effort.
            }
        }

        private static void ClearGps()
        {
            try
            {
                Function.Call(Hash.SET_WAYPOINT_OFF);
            }
            catch
            {
                // Waypoint cleanup is best-effort.
            }
        }

        private static List<TowableVehicleDefinition> BuildTowableCatalog(IReadOnlyList<DealershipVehicleDefinition> dealershipCatalog)
        {
            var result = new List<TowableVehicleDefinition>();
            if (dealershipCatalog == null)
            {
                return result;
            }

            for (int i = 0; i < dealershipCatalog.Count; i++)
            {
                var definition = dealershipCatalog[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ModelName))
                {
                    continue;
                }

                if (definition.VehicleWeightTons <= 0f || IsBikeVehicle(definition))
                {
                    continue;
                }

                result.Add(new TowableVehicleDefinition
                {
                    ModelName = definition.ModelName,
                    DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.ModelName : definition.DisplayName,
                    WeightTons = Math.Max(1f, Math.Min(3f, definition.VehicleWeightTons)),
                });
            }

            return result;
        }

        private static bool IsBikeVehicle(DealershipVehicleDefinition definition)
        {
            var category = (definition.Category ?? string.Empty).Trim();
            var model = (definition.ModelName ?? string.Empty).Trim();
            return category.IndexOf("Motorcycle", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("Bike", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("Bicycle", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("bmx", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("faggio", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("bati", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<TowTruckDefinition> LoadTowTrucks(string configDirectory)
        {
            var result = new List<TowTruckDefinition>();
            var document = SideJobConfigLoader.TryLoadDocument(
                SideJobConfigLoader.BuildSideJobsPath(configDirectory, "JobVehicles.xml"));
            if (document == null || document.Root == null)
            {
                return result;
            }

            foreach (var element in document.Root.Elements("JobVehicle"))
            {
                if (!string.Equals(SideJobConfigLoader.ReadAttribute(element, "type"), "TowTruck", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var modelName = SideJobConfigLoader.ReadAttribute(element, "model");
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    continue;
                }

                result.Add(new TowTruckDefinition
                {
                    Name = SideJobConfigLoader.ReadAttribute(element, "name", modelName),
                    ModelName = modelName,
                    UnlockLevel = Math.Max(0, SideJobConfigLoader.ReadIntAttribute(element, "unlockLevel", 0)),
                    TowCapacityTons = Math.Max(1f, SideJobConfigLoader.ReadFloatAttribute(element, "towCapacityTons", 3f)),
                    Price = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "dailyRent", 0f)),
                    FuelCapacityLiters = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "fuelCapacityLiters", 120f)),
                });
            }

            return result;
        }

        private static List<TowDropoffPoint> LoadDropoffs(string configDirectory)
        {
            var result = new List<TowDropoffPoint>();
            var document = SideJobConfigLoader.TryLoadDocument(
                SideJobConfigLoader.BuildSideJobsPath(configDirectory, "JobCoordinates.xml"));
            if (document == null || document.Root == null)
            {
                return result;
            }

            foreach (var element in document.Root.Elements("JobPoint"))
            {
                if (!string.Equals(SideJobConfigLoader.ReadAttribute(element, "job"), "TowTruck", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(SideJobConfigLoader.ReadAttribute(element, "function"), "TowDropoff", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var position = SideJobConfigLoader.ReadPosition(element);
                if (position == Vector3.Zero && !SideJobConfigLoader.HasAnyCoordinate(element))
                {
                    continue;
                }

                result.Add(new TowDropoffPoint
                {
                    Name = SideJobConfigLoader.ReadAttribute(element, "name", "Tow Dropoff"),
                    Position = position,
                    Heading = SideJobConfigLoader.ReadFloatAttribute(element, "heading", 0f),
                    DistrictName = SideJobConfigLoader.ReadAttribute(element, "district"),
                });
            }

            return result;
        }

        /// <summary>
        /// Loads the authored <c>DamagedVehicle</c> sites of JobCoordinates.xml, grouped by district.
        /// The authored <c>district="..."</c> is re-checked against Districts.xml: a site that really
        /// sits in another district is filed under that one, so the one or two wrecks a district keeps
        /// can never end up in the wrong part of the map.
        /// </summary>
        private List<DamagedVehicleDistrict> LoadDamagedDistricts(
            string configDirectory,
            IReadOnlyList<DistrictConfig> districts)
        {
            var result = new List<DamagedVehicleDistrict>();
            var document = SideJobConfigLoader.TryLoadDocument(
                SideJobConfigLoader.BuildSideJobsPath(configDirectory, "JobCoordinates.xml"));
            if (document == null || document.Root == null)
            {
                return result;
            }

            var byDistrict = new Dictionary<string, DamagedVehicleDistrict>(StringComparer.OrdinalIgnoreCase);
            var syntheticPointId = 0;

            foreach (var element in document.Root.Elements("JobPoint"))
            {
                if (!string.Equals(SideJobConfigLoader.ReadAttribute(element, "job"), "TowTruck", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(SideJobConfigLoader.ReadAttribute(element, "function"), "DamagedVehicle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!SideJobConfigLoader.HasAnyCoordinate(element))
                {
                    continue;
                }

                var position = SideJobConfigLoader.ReadPosition(element);
                var authored = SideJobConfigLoader.NormalizeDistrictName(SideJobConfigLoader.ReadAttribute(element, "district"));
                var resolved = SideJobConfigLoader.NormalizeDistrictName(
                    SideJobConfigLoader.ResolveDistrictName(position, districts));
                var districtName = resolved.Length > 0 ? resolved : authored;
                if (districtName.Length == 0)
                {
                    continue;
                }

                var pointId = SideJobConfigLoader.ReadIntAttribute(element, "id", 0);
                if (pointId <= 0)
                {
                    // The rotation memory needs a stable non-zero key even on an unlabelled point.
                    syntheticPointId -= 1;
                    pointId = syntheticPointId;
                }

                DamagedVehicleDistrict district;
                if (!byDistrict.TryGetValue(districtName, out district))
                {
                    district = new DamagedVehicleDistrict(districtName);
                    byDistrict[districtName] = district;
                    result.Add(district);
                }

                district.Points.Add(new DamagedVehiclePoint
                {
                    PointId = pointId,
                    Position = position,
                    Heading = SideJobConfigLoader.NormalizeHeading(SideJobConfigLoader.ReadFloatAttribute(element, "heading", 0f)),
                    AuthoredDistrictName = authored,
                    DistrictName = districtName,
                });
            }

            for (int i = 0; i < result.Count; i++)
            {
                // One or two wrecks per district, so the wrecks stay scattered across the map.
                result[i].TargetCount = _random.Next(MinWrecksPerDistrict, MaxWrecksPerDistrict + 1);
            }

            return result;
        }

        private sealed class TowDropoffPoint
        {
            public TowDropoffPoint()
            {
                DistrictName = string.Empty;
            }

            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public float Heading { get; set; }

            /// <summary>Canonical district of the garage, from district="..." on the JobPoint.</summary>
            public string DistrictName { get; set; }
        }

        private sealed class TowableVehicleDefinition
        {
            public string ModelName { get; set; }
            public string DisplayName { get; set; }
            public float WeightTons { get; set; }
        }

        /// <summary>One authored <c>DamagedVehicle</c> JobCoordinates site.</summary>
        private sealed class DamagedVehiclePoint
        {
            public int PointId { get; set; }
            public Vector3 Position { get; set; }
            public float Heading { get; set; }

            /// <summary>district="..." exactly as written in JobCoordinates.xml.</summary>
            public string AuthoredDistrictName { get; set; }

            /// <summary>District actually containing the site; equals the authored one unless the config was wrong.</summary>
            public string DistrictName { get; set; }
        }

        /// <summary>
        /// One district of wreck sites plus the rotation memory for it: the site used last is skipped
        /// on the next pick, so two consecutive wrecks never share coordinates.
        /// </summary>
        private sealed class DamagedVehicleDistrict
        {
            public DamagedVehicleDistrict(string name)
            {
                Name = name;
                Points = new List<DamagedVehiclePoint>();
            }

            public string Name { get; private set; }

            public List<DamagedVehiclePoint> Points { get; private set; }

            /// <summary>Wrecks this district keeps alive: one or two.</summary>
            public int TargetCount { get; set; }

            /// <summary>Site used last time, or 0 when the district has not spawned a wreck yet.</summary>
            public int LastPointId { get; set; }
        }

        private sealed class DamagedVehicleInstance
        {
            public int SpawnId { get; set; }
            public int PointId { get; set; }
            public string DistrictName { get; set; }
            public string ModelName { get; set; }
            public string DisplayName { get; set; }
            public float WeightTons { get; set; }
            public Vehicle Vehicle { get; set; }
            public Blip Blip { get; set; }
            public Vector3 SpawnPosition { get; set; }
            public float SpawnHeading { get; set; }
            public int SpawnedAtGameTime { get; set; }
            public bool IsTowed { get; set; }
        }
    }
}

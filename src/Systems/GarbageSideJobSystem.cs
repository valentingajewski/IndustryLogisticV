using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.UI;

namespace LSOL.Systems
{
    /// <summary>
    /// Persisted state for the garbage side job. Follows the same section-based
    /// persistence pattern as the other systems (see IndustryPersistenceManager).
    /// Legacy saves without a garbage section deserialize to <c>null</c> and load safely.
    /// </summary>
    public sealed class GarbagePersistenceSnapshot
    {
        public GarbagePersistenceSnapshot()
        {
            Bags = new List<GarbagePendingBagSnapshot>();
            OwnedTruckModels = new List<string>();
        }

        public string RouteId { get; set; }

        public int NextBagSpawnId { get; set; }

        public int OnBoardBags { get; set; }

        public float OnBoardTons { get; set; }

        public int RouteBagsCollected { get; set; }

        public string ActiveTruckModelName { get; set; }

        /// <summary>Bags of the active route that are still pending (not collected yet).</summary>
        public List<GarbagePendingBagSnapshot> Bags { get; }

        /// <summary>Model names of the trucks parked in the garbage job garage.</summary>
        public List<string> OwnedTruckModels { get; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RouteId)
                    || OnBoardBags > 0
                    || OnBoardTons > 0.001f
                    || RouteBagsCollected > 0
                    || (Bags != null && Bags.Count > 0)
                    || (OwnedTruckModels != null && OwnedTruckModels.Count > 0);
            }
        }
    }

    /// <summary>
    /// One not-yet-collected trash bag of the active route: its stable spawn id, the
    /// stop it belongs to, its world position and its rolled weight in tons.
    /// </summary>
    public sealed class GarbagePendingBagSnapshot
    {
        public int SpawnId { get; set; }

        public int StopIndex { get; set; }

        public Vector3 Position { get; set; }

        public float WeightTons { get; set; }

        public bool Collected { get; set; }
    }

    /// <summary>A garbage truck entry of JobVehicles.xml (type="Garbage").</summary>
    public sealed class GarbageTruckDefinition
    {
        public string Name { get; set; }
        public string ModelName { get; set; }
        public int UnlockLevel { get; set; }
        public float CapacityTons { get; set; }
        public float Price { get; set; }
        public float DailyRent { get; set; }
        public float FuelCapacityLiters { get; set; }
    }

    /// <summary>
    /// One collection stop of a garbage route. Bags live in alleys, driveways and back
    /// lots, so the truck cannot park there: the drive-to <see cref="Anchor"/> is derived
    /// from the bag centroid via the street graph and is what GPS/markers target.
    /// </summary>
    public sealed class GarbageStopDefinition
    {
        public GarbageStopDefinition()
        {
            BagPositions = new Vector3[0];
        }

        public Vector3[] BagPositions { get; set; }

        public Vector3 Anchor { get; set; }

        public float AnchorHeading { get; set; }

        public bool AnchorResolved { get; set; }

        public int BagCount
        {
            get { return BagPositions != null ? BagPositions.Length : 0; }
        }
    }

    /// <summary>A garbage collection route of GarbageRoute.xml.</summary>
    public sealed class GarbageRouteDefinition
    {
        public GarbageRouteDefinition()
        {
            Stops = new List<GarbageStopDefinition>();
        }

        public string RouteId { get; set; }

        public string DisplayName { get; set; }

        /// <summary>
        /// Canonical district of this route, taken from the district="..." beacon of
        /// GarbageRoute.xml (must match a district name in Districts.xml). Empty when the
        /// route is not tagged, in which case district-aware features are skipped.
        /// </summary>
        public string DistrictName { get; set; }

        public IReadOnlyList<GarbageStopDefinition> Stops { get; set; }

        public int StopCount
        {
            get { return Stops != null ? Stops.Count : 0; }
        }

        public int TotalBags
        {
            get
            {
                if (Stops == null)
                {
                    return 0;
                }

                var total = 0;
                for (int i = 0; i < Stops.Count; i++)
                {
                    var stop = Stops[i];
                    if (stop != null)
                    {
                        total += stop.BagCount;
                    }
                }

                return total;
            }
        }
    }

    /// <summary>
    /// Self-contained garbage side job. The La Puerta Recycling Center is both the job
    /// start point and the tip/dropoff point: the player spawns a trash truck there,
    /// picks a route, drives to each stop, walks up to the trash bags, presses Interact
    /// to carry them and presses Interact again next to the truck to throw them in, then
    /// tips the load back at the depot for cash and Garbage XP.
    ///
    /// Progression reuses the existing <see cref="PlayerSkillSystem"/>
    /// (<see cref="PlayerSkillId.Garbage"/>) — no new progression store is created.
    /// </summary>
    internal sealed class GarbageSideJobSystem
    {
        // Payout and XP tuning (see the locked design values for the job).
        public const float BagWeightMin = 0.1f;
        public const float BagWeightMax = 0.3f;
        public const float BagHandlingFee = 120f;
        public const float TippingFeePerTon = 150f;
        public const float RouteCompletionBonus = 1500f;
        public const float PerfectRouteBonus = 800f;
        public const float RouteCompletionXp = 250f;
        public const float BagXp = 12f;

        private const float BagSpawnRadius = 80f;
        private const float BagDespawnRadius = 140f;
        private const int MaxLiveBags = 12;
        private const int MaxBagSpawnsPerTick = 4;
        private const float BagPickupDistance = 2.0f;
        private const float ThrowInDistance = 6.0f;
        private const float TipDistance = 15f;
        private const float TipStationarySpeedMps = 1.0f;
        private const int TipCooldownMs = 4000;
        private const float DepotInteractHintDistance = 6f;
        private const float DepotMarkerDrawDistance = 400f;
        private const float StopMarkerDrawDistance = 250f;
        private const float BagArrowDrawDistance = 120f;
        private const float BagArrowHeight = 1.1f;
        private const int BagModelRequestTimeoutMs = 400;
        private const int CarryAnimTimeoutMs = 250;

        // Right-hand bone used to hold the bag. The reference trash job resolves its attach bone
        // with GET_PED_BONE_INDEX(ped, 57005); the resolved index is used, and the well-known
        // ph_r_hand bone (28422) is the fallback when the game cannot resolve 57005 on a ped.
        private const int ReferenceRightHandBoneId = 57005;
        private const int FallbackRightHandBoneId = 28422;

        // Extra drop applied to the carried bag, in metres, measured straight down in the world.
        // The grip offset below is expressed in the hand bone's local frame, and that frame is not
        // world aligned (it follows the wrist), so simply lowering the offset's own Z pushed the
        // bag sideways/backwards instead of down. The drop is therefore converted to the bone
        // frame at attach time with GET_PED_BONE_COORDS. Override with bagDrop="..." in
        // JobCoordinates.xml (0 disables the drop, so the bag's pivot sits on the palm again).
        private const float DefaultBagDropMeters = 0.22f;

        // Base-game animation set for taking, carrying and throwing a bin bag, all from
        // "anim@heists@narcotics@trash" (Heists update / Series A trash truck).
        // IMPORTANT: clips ending in "_bin_bag" are PROP tracks exported for synchronized scenes,
        // not ped tracks. Playing one on a ped fails silently (T/A-pose), so only ped clips are
        // used here: pickup (one-shot), idle (upper-body carry loop) and throw_b (one-shot).
        private const string CarryAnimDictionary = "anim@heists@narcotics@trash";
        private const string PickupAnimClip = "pickup";
        private const string CarryWalkAnimClip = "idle";
        private const string ThrowAnimClip = "throw_b";
        // Anim flag notes: 1 = loop, 2 = hold last frame, 16 = secondary (upper body only),
        // 32 = reorient when finished. Secondary MUST be set for every pose that has to coexist
        // with player locomotion, otherwise TASK_PLAY_ANIM becomes a full-body scripted task and
        // locks movement.
        private const int PickupAnimFlags = 0;   // one-shot lower-body take clip
        private const int CarryAnimFlags = 49;   // 1 + 16 + 32: looping upper-body carry pose
        private const int CarryHoldFallbackFlags = 50; // 2 + 16 + 32: upper-body take pose, holds
        private const int ThrowAnimFlags = 48;   // 16 + 32: one-shot upper-body release
        private const int PickupAttachAtMs = 700;     // hand reaches the bag mid take clip
        private const int PickupAnimHoldMs = 1400;    // take clip length before the carry loop
        private const int CarryProbeMs = 500;         // grace period before checking the carry clip
        private const int ThrowBagReleaseMs = 400;    // bag leaves the hand mid release clip
        private const int ThrowBagDeleteMs = 1200;    // flying bag is despawned after this
        private const int ThrowTaskClearDelayMs = 1500;
        private const float ThrownBagSpeedMps = 4.5f; // forward toss towards the truck
        private const float ThrownBagUpSpeedMps = 1.5f;

        private const string GarbageJobId = "Garbage";
        private const string GarbageDepotFunctionId = "GarbageDepot";

        private const string KeyInactive = "sidejob.garbage.inactive";
        private const string KeyTruckUnknown = "sidejob.garbage.truckUnknown";
        private const string KeyTruckLocked = "sidejob.garbage.truckLocked";
        private const string KeyDepotMissing = "sidejob.garbage.depotMissing";
        private const string KeySpawnFailed = "sidejob.garbage.spawnFailed";
        private const string KeyTruckReady = "sidejob.garbage.truckReady";
        private const string KeyDepotHint = "sidejob.garbage.depotHint";
        private const string KeyRouteUnknown = "sidejob.garbage.routeUnknown";
        private const string KeyRoutesMissing = "sidejob.garbage.routesMissing";
        private const string KeyRouteBusy = "sidejob.garbage.routeBusy";
        private const string KeyRouteStarted = "sidejob.garbage.routeStarted";
        private const string KeyRouteResumed = "sidejob.garbage.routeResumed";
        private const string KeyBagPickedUp = "sidejob.garbage.bagPickedUp";
        private const string KeyBagThrowHint = "sidejob.garbage.bagThrowHint";
        private const string KeyTruckFull = "sidejob.garbage.truckFull";
        private const string KeyThrowTooFar = "sidejob.garbage.throwTooFar";
        private const string KeyNeedTruck = "sidejob.garbage.needTruck";
        private const string KeyBagLoaded = "sidejob.garbage.bagLoaded";
        private const string KeyTipped = "sidejob.garbage.tipped";
        private const string KeyRouteComplete = "sidejob.garbage.routeComplete";
        private const string KeyTruckPurchased = "sidejob.garbage.truckPurchased";
        private const string KeyCannotAfford = "sidejob.garbage.cannotAfford";
        private const string KeyAlreadyOwned = "sidejob.garbage.alreadyOwned";
        private const string KeyNotOwned = "sidejob.garbage.notOwned";
        private const string KeyTruckAlreadyOut = "sidejob.garbage.truckOut";
        private const string KeyTruckStored = "sidejob.garbage.truckStored";
        private const string KeyNoTruckOut = "sidejob.garbage.noTruckOut";
        private const string KeyStoreBlocked = "sidejob.garbage.storeBlocked";
        private const string KeyDistrictBonus = "sidejob.bonus.districtBonus";

        private static readonly string[] BagModelCandidates =
        {
            "prop_rub_binbag_01",
            "prop_rub_binbag_03",
            "prop_rub_binbag_sd_01",
        };

        // The depot keeps the garbage truck sprite; the per-stop trash bag blips use the
        // classic yellow on-mission sprite (radar_on_mission, exposed as BlipSprite.OnMission).
        private static readonly BlipSprite DepotBlipSprite = ResolveDepotBlipSprite();

        /// <summary>
        /// Default grip transform for the carried bag, relative to the right-hand bone. Overridable
        /// per install with bagOffsetX/Y/Z and bagRotX/Y/Z on the Garbage JobPoint in
        /// JobCoordinates.xml, so the bag can be tuned onto the hand without rebuilding.
        /// </summary>
        private static readonly Vector3 DefaultHandAttachOffset = new Vector3(0.12f, 0f, -0.10f);
        private static readonly Vector3 DefaultHandAttachRotation = new Vector3(220f, 90f, 0f);

        private static string _resolvedBagModelName;

        private readonly string _configDirectory;
        private readonly PlayerSkillSystem _skillSystem;
        private readonly Action<string> _showStatus;
        private readonly Action<float> _addProfit;
        private readonly Action _requestAutosave;
        private readonly Func<float> _getCompanyBalance;
        private readonly Action<float, string> _deductProfit;

        // District bonus access. Injected as callbacks (not a TerritoryManager reference) so this
        // system stays self-contained and unit testable, like the other side job delegates.
        private readonly Func<string, string, float> _getDistrictBonusExcludingJob;
        private readonly Func<string, string, float, DistrictBonusAward> _reportDistrictBonus;
        private readonly Random _random;

        private readonly List<GarbageTruckDefinition> _trucks;
        private readonly List<GarbageRouteDefinition> _routes;
        private readonly List<GarbageStopRuntime> _stops;
        private readonly List<GarbageBagRuntime> _bags;
        private readonly List<string> _ownedTruckModels;

        // Not readonly: the depot entry can be re-read at runtime (TryReloadDepotConfiguration),
        // which is how the bag grip is tuned in the XML without restarting the game.
        private GarbageDepotPoint _depot;

        private GarbageRouteDefinition _activeRoute;
        private Vehicle _activeJobTruck;
        private string _activeTruckModelName = string.Empty;
        private float _activeTruckCapacityTons;
        private Blip _depotBlip;
        private GarbageBagRuntime _carriedBag;
        private int _nextBagSpawnId = 1;
        private int _onBoardBags;
        private float _onBoardTons;
        private int _routeBagsCollected;
        private float _routeTonsCollected;
        private float _routeCashEarned;
        private float _routeXpEarned;
        private int _nextTipAllowedGameTime;
        private bool _modMechanicsEnabled = true;
        private bool _jobEnabled = true;
        private bool _nearDepotHint;
        private bool _carryHintShown;
        private bool _pickupAnimPending;
        private int _pickupAnimDeadlineGameTime;
        private int _pickupAttachAtGameTime;
        private int _carryLoopAtGameTime;
        private int _carryLoopProbeAtGameTime;
        private int _carryTaskClearAtGameTime;
        private int _throwReleaseAtGameTime;
        private int _thrownBagDeleteAtGameTime;
        private GarbageBagRuntime _thrownBag;

        public GarbageSideJobSystem(
            string configDirectory,
            PlayerSkillSystem skillSystem,
            Action<string> showStatus,
            Action<float> addProfit,
            Action requestAutosave,
            Func<float> getCompanyBalance = null,
            Action<float, string> deductProfit = null,
            Func<string, string, float> getDistrictBonusExcludingJob = null,
            Func<string, string, float, DistrictBonusAward> reportDistrictBonus = null)
        {
            _configDirectory = configDirectory ?? string.Empty;
            _skillSystem = skillSystem;
            _showStatus = showStatus;
            _addProfit = addProfit;
            _requestAutosave = requestAutosave;
            _getCompanyBalance = getCompanyBalance;
            _deductProfit = deductProfit;
            _getDistrictBonusExcludingJob = getDistrictBonusExcludingJob;
            _reportDistrictBonus = reportDistrictBonus;
            _random = new Random();

            _trucks = LoadGarbageTrucks(_configDirectory);
            _routes = LoadRoutes(_configDirectory);
            _depot = LoadDepot(_configDirectory);
            _stops = new List<GarbageStopRuntime>();
            _bags = new List<GarbageBagRuntime>();
            _ownedTruckModels = new List<string>();
        }

        /// <summary>True while a collection route is in progress (with or without a truck).</summary>
        public bool HasActiveRoute
        {
            get { return _activeRoute != null; }
        }

        /// <summary>True while a garbage truck of this job is spawned in the world.</summary>
        public bool HasTruckOut
        {
            get { return _activeJobTruck != null && _activeJobTruck.Exists(); }
        }

        public IReadOnlyList<GarbageTruckDefinition> GetTrucks()
        {
            return _trucks;
        }

        public IReadOnlyList<GarbageRouteDefinition> GetRoutes()
        {
            return _routes;
        }

        /// <summary>Trucks bought by the player and parked in the garbage job garage.</summary>
        public IReadOnlyList<GarbageTruckDefinition> GetOwnedTrucks()
        {
            var result = new List<GarbageTruckDefinition>();
            for (int i = 0; i < _trucks.Count; i++)
            {
                var definition = _trucks[i];
                if (definition != null && OwnsTruck(definition.ModelName))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        public bool OwnsTruck(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            for (int i = 0; i < _ownedTruckModels.Count; i++)
            {
                if (string.Equals(_ownedTruckModels[i], modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the given model is the garbage truck currently taken out.</summary>
        public bool IsActiveTruckOut(string modelName)
        {
            return HasTruckOut
                && !string.IsNullOrWhiteSpace(modelName)
                && string.Equals(_activeTruckModelName, modelName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Buys a garbage truck from the job dealership. Bought trucks are parked in the job
        /// garage and are what the player takes out at the depot.
        /// </summary>
        public bool TryBuyGarbageTruck(string modelName, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The garbage side job is inactive.");
                return false;
            }

            var definition = FindTruck(modelName);
            if (definition == null)
            {
                message = Text(KeyTruckUnknown, "Unknown garbage truck.");
                return false;
            }

            if (OwnsTruck(definition.ModelName))
            {
                message = LocalizedText.FormatOrDefault(
                    KeyAlreadyOwned,
                    "You already own {0}.",
                    definition.Name);
                return false;
            }

            var garbageLevel = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.Garbage) : 0;
            if (garbageLevel < definition.UnlockLevel)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyTruckLocked,
                    "{0} requires Garbage level {1}.",
                    definition.Name,
                    definition.UnlockLevel);
                return false;
            }

            var price = Math.Max(0f, definition.Price);
            if (price > 0f && _getCompanyBalance != null && _getCompanyBalance() < price)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyCannotAfford,
                    "You cannot afford {0} ({1}).",
                    definition.Name,
                    ModFormatting.FormatMoney(price));
                return false;
            }

            _deductProfit?.Invoke(price, string.Format("Garbage truck: {0}", definition.Name));
            _ownedTruckModels.Add(definition.ModelName);

            message = LocalizedText.FormatOrDefault(
                KeyTruckPurchased,
                "Bought {0} for {1}. It is parked in the garbage garage.",
                definition.Name,
                ModFormatting.FormatMoney(price));

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>Parks the truck currently taken out back into the job garage.</summary>
        public bool TryStoreGarbageTruck(Ped player, out string message)
        {
            message = string.Empty;

            var truck = _activeJobTruck;
            if (truck == null || !truck.Exists())
            {
                _activeJobTruck = null;
                message = Text(KeyNoTruckOut, "No garbage truck is currently out.");
                return false;
            }

            try
            {
                if (player != null && player.Exists() && player.IsInVehicle(truck))
                {
                    message = Text(KeyStoreBlocked, "Get out of the truck before storing it.");
                    return false;
                }
            }
            catch
            {
                // Being unable to test the seat is not a reason to block storing.
            }

            var storedName = ResolveTruckName(_activeTruckModelName);
            try
            {
                truck.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }

            _activeJobTruck = null;
            _activeTruckModelName = string.Empty;
            _activeTruckCapacityTons = 0f;
            _onBoardBags = 0;
            _onBoardTons = 0f;
            _nextTipAllowedGameTime = 0;

            message = LocalizedText.FormatOrDefault(
                KeyTruckStored,
                "{0} stored in the garbage garage.",
                storedName);

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>
        /// Cancels the active garbage job: bags, blips, GPS and route progress are cleared. The
        /// parked truck and the trucks owned in the job garage are kept. Never throws.
        /// </summary>
        public void CancelCurrentJob()
        {
            try
            {
                ResetRouteState();
                _nearDepotHint = false;
                _carryHintShown = false;
            }
            catch
            {
                // Cancelling a job must never throw into the menu handler.
            }
        }

        /// <summary>
        /// Re-reads the garbage depot entry from JobCoordinates.xml so the bag grip, the throw-in
        /// distance and the depot position can be tuned in the XML without restarting the game.
        /// Routes, owned trucks and the running job are deliberately left untouched. Never throws.
        /// </summary>
        public bool TryReloadDepotConfiguration(out string message)
        {
            message = string.Empty;
            try
            {
                var reloaded = LoadDepot(_configDirectory);
                if (reloaded == null)
                {
                    message = "Garbage depot entry not found in LSOL_Config/SideJobs/JobCoordinates.xml.";
                    return false;
                }

                _depot = reloaded;
                ClearDepotBlip();
                EnsureDepotBlip();

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Garbage depot reloaded (bag drop {0:0.00} m, throw-in {1:0.0} m).",
                    GetBagDropMeters(),
                    GetThrowInDistance());
                return true;
            }
            catch
            {
                message = "Garbage depot reload failed.";
                return false;
            }
        }

        public bool TryGetNearestGarbageDepot(Vector3 position, float maxDistance, out Vector3 spawnPosition, out string depotName, out float heading)
        {
            spawnPosition = Vector3.Zero;
            depotName = string.Empty;
            heading = 0f;

            if (!_modMechanicsEnabled || !_jobEnabled || _depot == null)
            {
                return false;
            }

            if (position.DistanceTo(_depot.Position) > maxDistance)
            {
                return false;
            }

            spawnPosition = _depot.Position;
            depotName = _depot.Name;
            heading = _depot.Heading;
            return true;
        }

        /// <summary>
        /// Takes a truck out of the job garage: spawns it at the depot and puts the player in
        /// the driver seat. Only trucks bought into the garage can be taken out.
        /// </summary>
        public bool TrySpawnGarbageTruck(string modelName, Ped player, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The garbage side job is inactive.");
                return false;
            }

            var definition = FindTruck(modelName);
            if (definition == null)
            {
                message = Text(KeyTruckUnknown, "Unknown garbage truck.");
                return false;
            }

            if (!OwnsTruck(definition.ModelName))
            {
                message = LocalizedText.FormatOrDefault(
                    KeyNotOwned,
                    "{0} is not in your garbage garage. Buy it at the depot first.",
                    definition.Name);
                return false;
            }

            if (HasTruckOut)
            {
                message = Text(KeyTruckAlreadyOut, "A garbage truck is already out. Store it first.");
                return false;
            }

            var garbageLevel = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.Garbage) : 0;
            if (garbageLevel < definition.UnlockLevel)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyTruckLocked,
                    "{0} requires Garbage level {1}.",
                    definition.Name,
                    definition.UnlockLevel);
                return false;
            }

            if (_depot == null)
            {
                message = Text(KeyDepotMissing, "No garbage depot configured.");
                return false;
            }

            var vehicle = SpawnVehicle(definition.ModelName, _depot.Position, _depot.Heading, true);
            if (vehicle == null || !vehicle.Exists())
            {
                message = Text(KeySpawnFailed, "Could not spawn the garbage truck.");
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

            _activeJobTruck = vehicle;
            _activeTruckModelName = definition.ModelName ?? string.Empty;
            _activeTruckCapacityTons = definition.CapacityTons;
            _nextTipAllowedGameTime = 0;

            if (_activeRoute != null)
            {
                RefreshRouteGps();
                message = LocalizedText.FormatOrDefault(
                    KeyRouteResumed,
                    "Route {0} resumed. {1} stops, {2} bags remaining.",
                    _activeRoute.DisplayName,
                    _activeRoute.StopCount,
                    Math.Max(0, _activeRoute.TotalBags - _routeBagsCollected));
            }
            else
            {
                message = LocalizedText.FormatOrDefault(
                    KeyTruckReady,
                    "{0} ready at {1}.",
                    definition.Name,
                    _depot.Name);
            }

            _requestAutosave?.Invoke();
            return true;
        }

        public bool TryStartRoute(string routeId, Ped player, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The garbage side job is inactive.");
                return false;
            }

            var route = FindRoute(routeId);
            if (route == null)
            {
                message = string.IsNullOrWhiteSpace(routeId)
                    ? Text(KeyRoutesMissing, "No garbage routes configured.")
                    : Text(KeyRouteUnknown, "Unknown garbage route.");
                return false;
            }

            if (_activeRoute != null && string.Equals(_activeRoute.RouteId, route.RouteId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureStopBlips();
                RefreshRouteGps();
                message = LocalizedText.FormatOrDefault(
                    KeyRouteResumed,
                    "Route {0} resumed. {1} stops, {2} bags remaining.",
                    route.DisplayName,
                    route.StopCount,
                    Math.Max(0, route.TotalBags - _routeBagsCollected));
                return true;
            }

            if (_activeRoute != null && (_routeBagsCollected > 0 || _onBoardBags > 0 || _carriedBag != null))
            {
                message = Text(KeyRouteBusy, "Tip the current load and finish this route first.");
                return false;
            }

            ResetRouteState();
            _activeRoute = route;
            _nextBagSpawnId = 1;
            BuildRouteRuntime(route);
            ResolveStopAnchors();
            EnsureStopBlips();
            RefreshRouteGps();

            message = LocalizedText.FormatOrDefault(
                KeyRouteStarted,
                "Route {0} started. {1} stops, {2} bags.",
                route.DisplayName,
                route.StopCount,
                route.TotalBags);

            _requestAutosave?.Invoke();
            return true;
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

            try
            {
                EnsureDepotBlip();
                UpdateDepotHint(player);
                DrawDepotMarker(player);

                if (_activeRoute == null)
                {
                    return;
                }

                UpdateThrowSequence(player, gameTime);
                RefreshBagStreaming(player);
                UpdateStopBlips();
                DrawStopMarkers(player);
                DrawBagArrows(player);
                UpdateTipping(player, gameTime);
                TryCompleteRouteIfReady();
            }
            catch
            {
                // The job tick must never throw into the game loop.
            }
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
                _nextTipAllowedGameTime = 0;
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
                _nextTipAllowedGameTime = 0;
            }
        }

        public void ResetState()
        {
            ReleaseCarriedBag();
            DespawnAllBags();
            ClearGps();
            ClearDepotBlip();
            ClearStopBlips();

            _stops.Clear();
            _bags.Clear();

            _activeRoute = null;
            _activeJobTruck = null;
            _activeTruckModelName = string.Empty;
            _activeTruckCapacityTons = 0f;
            _nextBagSpawnId = 1;
            _onBoardBags = 0;
            _onBoardTons = 0f;
            _routeBagsCollected = 0;
            _routeTonsCollected = 0f;
            _routeCashEarned = 0f;
            _routeXpEarned = 0f;
            _nextTipAllowedGameTime = 0;
            _nearDepotHint = false;
            _carryHintShown = false;
            _pickupAnimPending = false;
            _carryLoopAtGameTime = 0;
            _ownedTruckModels.Clear();
        }

        public GarbagePersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new GarbagePersistenceSnapshot
            {
                RouteId = _activeRoute != null ? _activeRoute.RouteId : string.Empty,
                NextBagSpawnId = Math.Max(1, _nextBagSpawnId),
                OnBoardBags = Math.Max(0, _onBoardBags),
                OnBoardTons = Math.Max(0f, _onBoardTons),
                RouteBagsCollected = Math.Max(0, _routeBagsCollected),
                ActiveTruckModelName = _activeTruckModelName ?? string.Empty,
            };

            for (int i = 0; i < _ownedTruckModels.Count; i++)
            {
                var modelName = _ownedTruckModels[i];
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    snapshot.OwnedTruckModels.Add(modelName);
                }
            }

            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag == null || bag.Collected)
                {
                    continue;
                }

                snapshot.Bags.Add(new GarbagePendingBagSnapshot
                {
                    SpawnId = bag.SpawnId,
                    StopIndex = bag.StopIndex,
                    Position = bag.Position,
                    WeightTons = bag.WeightTons,
                    Collected = false,
                });
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(GarbagePersistenceSnapshot snapshot)
        {
            ResetState();
            if (snapshot == null)
            {
                return;
            }

            _activeTruckModelName = snapshot.ActiveTruckModelName ?? string.Empty;
            _activeTruckCapacityTons = ResolveCapacityForModel(_activeTruckModelName);

            if (snapshot.OwnedTruckModels != null)
            {
                for (int i = 0; i < snapshot.OwnedTruckModels.Count; i++)
                {
                    var modelName = snapshot.OwnedTruckModels[i];
                    if (string.IsNullOrWhiteSpace(modelName) || OwnsTruck(modelName))
                    {
                        continue;
                    }

                    // Drop garage entries whose truck no longer exists in JobVehicles.xml.
                    if (FindTruck(modelName) != null)
                    {
                        _ownedTruckModels.Add(modelName);
                    }
                }
            }
            _onBoardBags = Math.Max(0, snapshot.OnBoardBags);
            _onBoardTons = Math.Max(0f, snapshot.OnBoardTons);
            _routeBagsCollected = Math.Max(0, snapshot.RouteBagsCollected);
            _nextBagSpawnId = Math.Max(1, snapshot.NextBagSpawnId);

            var route = FindRoute(snapshot.RouteId);
            if (route == null)
            {
                return;
            }

            _activeRoute = route;
            BuildRouteRuntime(route);

            if (snapshot.Bags != null)
            {
                for (int i = 0; i < snapshot.Bags.Count; i++)
                {
                    var entry = snapshot.Bags[i];
                    if (entry == null || entry.Collected)
                    {
                        continue;
                    }

                    var bag = FindBag(entry.SpawnId);
                    if (bag == null)
                    {
                        continue;
                    }

                    bag.StopIndex = entry.StopIndex;
                    bag.Position = entry.Position;
                    bag.WeightTons = ClampBagWeight(entry.WeightTons);
                    bag.Collected = false;
                }
            }

            RecomputeStopProgress();
            ResolveStopAnchors();
            EnsureStopBlips();
            RefreshRouteGps();
        }

        /// <summary>
        /// Interact (E) entry point: picks up the nearest trash bag on foot, or throws the
        /// carried bag into the active job truck. Returns true when the key was consumed so
        /// the caller does not fall through to other interact handlers (e.g. the depot menu).
        /// </summary>
        public bool TryHandleInteract(Ped player)
        {
            try
            {
                if (!_modMechanicsEnabled || !_jobEnabled)
                {
                    return false;
                }

                if (player == null || !player.Exists())
                {
                    return false;
                }

                if (IsPlayerInAnyVehicle(player))
                {
                    return false;
                }

                // The take/throw sequence is still playing: swallow extra presses so the same bag
                // cannot be handled twice before it has left the hand.
                if (IsCarrySequenceBusy())
                {
                    return true;
                }

                if (_carriedBag != null)
                {
                    return HandleThrowIn(player);
                }

                return HandlePickup(player);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True while a take/throw clip is driving the bag, so Interact presses are ignored until
        /// the sequence finishes.
        /// </summary>
        private bool IsCarrySequenceBusy()
        {
            return _throwReleaseAtGameTime > 0 || _pickupAnimPending || _pickupAttachAtGameTime > 0;
        }

        private bool HandlePickup(Ped player)
        {
            if (_activeRoute == null)
            {
                return false;
            }

            var playerPosition = player.Position;
            GarbageBagRuntime nearest = null;
            var nearestDistance = BagPickupDistance;

            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag == null || bag.Collected || bag.PickedUp)
                {
                    continue;
                }

                var entity = bag.Entity;
                if (entity == null || !entity.Exists())
                {
                    continue;
                }

                var distance = entity.Position.DistanceTo(playerPosition);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = bag;
                }
            }

            if (nearest == null)
            {
                return false;
            }

            if (!BeginBagPickup(nearest))
            {
                return false;
            }

            _carriedBag = nearest;
            nearest.PickedUp = true;
            _carryHintShown = false;

            _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                KeyBagPickedUp,
                "Bag picked up ({0:0.0} t). Walk to the truck and press Interact.",
                nearest.WeightTons));
            return true;
        }

        private bool HandleThrowIn(Ped player)
        {
            var bag = _carriedBag;
            if (bag == null)
            {
                return false;
            }

            if (_activeJobTruck == null || !_activeJobTruck.Exists())
            {
                _showStatus?.Invoke(Text(
                    KeyNeedTruck,
                    "Take a garbage truck out of the depot garage before loading bags."));
                return true;
            }

            Vector3 rearPosition;
            try
            {
                rearPosition = GetTruckRearPosition(_activeJobTruck);
            }
            catch
            {
                rearPosition = _activeJobTruck.Position;
            }

            // Standing anywhere along the truck counts, not only dead behind the rear bumper.
            var rearDistance = player.Position.DistanceTo(rearPosition);
            var bodyDistance = player.Position.DistanceTo(_activeJobTruck.Position);
            var distance = Math.Min(rearDistance, bodyDistance);
            var maxDistance = GetThrowInDistance();
            if (distance > maxDistance)
            {
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyThrowTooFar,
                    "Move closer to the garbage truck ({0:0.0} m away).",
                    distance));
                return true;
            }

            if (_activeTruckCapacityTons <= 0f)
            {
                // Recovers the capacity when a save restored only the truck model name.
                _activeTruckCapacityTons = ResolveCapacityForModel(_activeTruckModelName);
            }

            if (!CanLoadBag(_onBoardTons, bag.WeightTons, _activeTruckCapacityTons))
            {
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyTruckFull,
                    "Truck full ({0:0.0}/{1:0.0} t). Tip the load at the Recycling Center.",
                    _onBoardTons,
                    _activeTruckCapacityTons));
                return true;
            }

            bag.Collected = true;
            bag.PickedUp = false;

            // The bag stays attached to the hand while the throw clip plays and is released (and
            // deleted) mid-animation, exactly like the original game sequence.
            BeginThrowAnimation(player);

            _onBoardBags += 1;
            _onBoardTons += bag.WeightTons;
            _routeBagsCollected += 1;
            _routeTonsCollected += bag.WeightTons;

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.Garbage, BagXp, PlayerSkillXpSource.Player);
            }

            _routeXpEarned += BagXp;

            var stop = GetStop(bag.StopIndex);
            if (stop != null)
            {
                stop.CollectedBags += 1;
            }

            _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                KeyBagLoaded,
                "Bag {0}/{1} at this stop. {2}/{3} on the route. {4:0.0} t / {5:0.0} t loaded.",
                stop != null ? stop.CollectedBags : 0,
                stop != null ? stop.TotalBags : 0,
                _routeBagsCollected,
                _activeRoute != null ? _activeRoute.TotalBags : 0,
                _onBoardTons,
                _activeTruckCapacityTons));

            _requestAutosave?.Invoke();
            return true;
        }

        private void ShowThrowHint()
        {
            if (_carryHintShown)
            {
                return;
            }

            _carryHintShown = true;
            _showStatus?.Invoke(Text(KeyBagThrowHint, "Carry the bag to the truck and press Interact to throw it in."));
        }

        private void UpdateTipping(Ped player, int gameTime)
        {
            if (_depot == null || _onBoardBags <= 0 || gameTime < _nextTipAllowedGameTime)
            {
                return;
            }

            var truck = _activeJobTruck;
            if (truck == null || !truck.Exists())
            {
                return;
            }

            if (!IsPlayerInActiveTruck(player))
            {
                return;
            }

            float speed;
            try
            {
                speed = truck.Speed;
            }
            catch
            {
                return;
            }

            if (speed >= TipStationarySpeedMps)
            {
                return;
            }

            if (truck.Position.DistanceTo(_depot.Position) > TipDistance)
            {
                return;
            }

            _nextTipAllowedGameTime = gameTime + TipCooldownMs;
            TipLoad();
        }

        private void TipLoad()
        {
            var bags = _onBoardBags;
            var tons = _onBoardTons;
            if (bags <= 0)
            {
                return;
            }

            var multiplier = _skillSystem != null ? _skillSystem.GetBonusMultiplier(PlayerSkillId.Garbage) : 1f;
            var cash = ComputeTipCash(bags, tons, multiplier * GetDistrictPayoutMultiplier(_activeRoute != null ? _activeRoute.DistrictName : null));

            _addProfit?.Invoke(cash);
            _routeCashEarned += cash;

            _onBoardBags = 0;
            _onBoardTons = 0;

            _requestAutosave?.Invoke();

            if (TryCompleteRouteIfReady())
            {
                // The route summary already reports the full run total, including this tip.
                return;
            }

            _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                KeyTipped,
                "Tipped {0} bags ({1:0.0} t): {2}.",
                bags,
                tons,
                ModFormatting.FormatMoney(cash)));

            RefreshRouteGps();
        }

        /// <summary>
        /// District revenue multiplier for this job's own payouts. Uses the district bonus with the
        /// Garbage pool excluded, so garbage work can never boost its own payout.
        /// </summary>
        private float GetDistrictPayoutMultiplier(string districtName)
        {
            if (_getDistrictBonusExcludingJob == null || string.IsNullOrWhiteSpace(districtName))
            {
                return 1f;
            }

            try
            {
                var percent = Math.Max(0f, _getDistrictBonusExcludingJob(districtName, DistrictBonusCatalog.GarbageJobId));
                return 1f + (percent / 100f);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Credits a finished route to the route's district and returns the status suffix describing
        /// the gain. Routes without a district tag contribute nothing and stay silent.
        /// </summary>
        private string ReportDistrictCompletion(GarbageRouteDefinition route)
        {
            if (_reportDistrictBonus == null || route == null || string.IsNullOrWhiteSpace(route.DistrictName))
            {
                return string.Empty;
            }

            try
            {
                var award = _reportDistrictBonus(DistrictBonusCatalog.GarbageJobId, route.DistrictName, 1f);
                if (award == null || !award.Applied || award.AppliedPoints <= 0f)
                {
                    return string.Empty;
                }

                return string.Concat(
                    " ",
                    LocalizedText.FormatOrDefault(
                        KeyDistrictBonus,
                        "District bonus {0} +{1:0.#}% ({2:0.#} / {3:0.#}%).",
                        ModFormatting.FormatDistrictName(route.DistrictName),
                        award.AppliedPoints,
                        award.TotalPercent,
                        award.CapPercent));
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TryCompleteRouteIfReady()
        {
            var route = _activeRoute;

            if (route == null)
            {
                return false;
            }

            if (_routeBagsCollected < route.TotalBags || _onBoardBags > 0 || _carriedBag != null)
            {
                return false;
            }

            // Route completion implies a perfect route: every bag on the route was collected.
            var districtMultiplier = GetDistrictPayoutMultiplier(route.DistrictName);
            var bonus = (float)Math.Round((RouteCompletionBonus + PerfectRouteBonus) * districtMultiplier);
            _addProfit?.Invoke(bonus);
            _routeCashEarned += bonus;

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.Garbage, RouteCompletionXp, PlayerSkillXpSource.Player);
            }

            _routeXpEarned += RouteCompletionXp;

            var routeName = route.DisplayName;
            var bags = _routeBagsCollected;
            var tons = _routeTonsCollected;
            var cash = _routeCashEarned;
            var xp = _routeXpEarned;

            // Credit the district once the route is genuinely finished, and fold the gain into the
            // same status line so the completion summary is not overwritten.
            var districtSuffix = ReportDistrictCompletion(route);

            _showStatus?.Invoke(string.Concat(
                LocalizedText.FormatOrDefault(
                    KeyRouteComplete,
                    "Route {0} complete: {1} bags, {2:0.0} t. Total {3} and {4:0} Garbage XP.",
                    routeName,
                    bags,
                    tons,
                    ModFormatting.FormatMoney(cash),
                    xp),
                districtSuffix));

            ResetRouteState();
            _requestAutosave?.Invoke();
            return true;
        }

        private void ResetRouteState()
        {
            ReleaseCarriedBag();
            DespawnAllBags();
            ClearGps();
            ClearStopBlips();

            _stops.Clear();
            _bags.Clear();
            _activeRoute = null;
            _nextBagSpawnId = 1;
            _onBoardBags = 0;
            _onBoardTons = 0f;
            _routeBagsCollected = 0;
            _routeTonsCollected = 0f;
            _routeCashEarned = 0f;
            _routeXpEarned = 0f;
            _carryHintShown = false;
            _pickupAnimPending = false;
            _carryLoopAtGameTime = 0;
            _carryTaskClearAtGameTime = 0;
            _throwReleaseAtGameTime = 0;
        }

        private void HideWorldVisuals()
        {
            ReleaseCarriedBag();
            DespawnAllBags();

            ClearGps();
            ClearDepotBlip();
            ClearStopBlips();

            _nearDepotHint = false;
            _carryHintShown = false;
            _pickupAnimPending = false;
            _carryLoopAtGameTime = 0;
            _carryTaskClearAtGameTime = 0;
            _throwReleaseAtGameTime = 0;
        }

        private void BuildRouteRuntime(GarbageRouteDefinition route)
        {
            _stops.Clear();
            _bags.Clear();

            if (route == null || route.Stops == null)
            {
                return;
            }

            for (int stopIndex = 0; stopIndex < route.Stops.Count; stopIndex++)
            {
                var definition = route.Stops[stopIndex];
                var runtime = new GarbageStopRuntime
                {
                    Index = stopIndex,
                    Definition = definition,
                    BlipName = string.Format("{0} - {1}", route.DisplayName, stopIndex + 1),
                };

                var positions = definition != null ? definition.BagPositions : null;
                if (positions != null)
                {
                    runtime.Centroid = ComputeCentroid(positions);
                    for (int i = 0; i < positions.Length; i++)
                    {
                        _bags.Add(new GarbageBagRuntime
                        {
                            SpawnId = _nextBagSpawnId,
                            StopIndex = stopIndex,
                            Position = positions[i],
                            WeightTons = RollBagWeight(_random),
                        });
                        _nextBagSpawnId += 1;
                    }
                }

                _stops.Add(runtime);
            }
        }

        private void ResolveStopAnchors()
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop == null || stop.Definition == null || stop.AnchorResolved)
                {
                    continue;
                }

                var centroid = stop.Centroid;
                if (centroid == Vector3.Zero && stop.Definition.BagPositions != null)
                {
                    centroid = ComputeCentroid(stop.Definition.BagPositions);
                    stop.Centroid = centroid;
                }

                var heading = 0f;
                var anchor = Vector3.Zero;
                try
                {
                    anchor = World.GetNextPositionOnStreetWithHeading(centroid, out heading, false);
                }
                catch
                {
                    anchor = Vector3.Zero;
                }

                if (anchor == Vector3.Zero)
                {
                    stop.Anchor = centroid;
                    stop.AnchorHeading = 0f;
                    stop.AnchorResolved = false;
                }
                else
                {
                    stop.Anchor = anchor;
                    stop.AnchorHeading = heading;
                    stop.AnchorResolved = true;
                }
            }
        }

        private void RecomputeStopProgress()
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop == null)
                {
                    continue;
                }

                stop.CollectedBags = 0;
                for (int b = 0; b < _bags.Count; b++)
                {
                    var bag = _bags[b];
                    if (bag != null && bag.StopIndex == stop.Index && bag.Collected)
                    {
                        stop.CollectedBags += 1;
                    }
                }
            }
        }

        private void RefreshBagStreaming(Ped player)
        {
            if (_activeRoute == null || player == null || !player.Exists())
            {
                return;
            }

            var playerPosition = player.Position;
            var liveCount = CountLiveBags();
            var spawnedThisTick = 0;

            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag == null || bag.Collected || bag.PickedUp || bag.Entity != null)
                {
                    continue;
                }

                if (liveCount >= MaxLiveBags || spawnedThisTick >= MaxBagSpawnsPerTick)
                {
                    break;
                }

                var stop = GetStop(bag.StopIndex);
                var anchor = stop != null && stop.AnchorResolved ? stop.Anchor : (stop != null ? stop.Centroid : bag.Position);
                if (playerPosition.DistanceTo(anchor) > BagSpawnRadius)
                {
                    continue;
                }

                if (SpawnBagEntity(bag))
                {
                    liveCount += 1;
                    spawnedThisTick += 1;
                }
            }

            EnforceLiveBagCap(playerPosition);
            CleanupMissingBagEntities();
        }

        private void EnforceLiveBagCap(Vector3 playerPosition)
        {
            while (true)
            {
                var live = CountLiveBags();
                if (live <= MaxLiveBags)
                {
                    return;
                }

                GarbageBagRuntime farthest = null;
                var farthestDistance = float.MinValue;
                for (int i = 0; i < _bags.Count; i++)
                {
                    var bag = _bags[i];
                    if (bag == null || bag.Entity == null || bag.Collected || bag.PickedUp || !bag.Entity.Exists())
                    {
                        continue;
                    }

                    var distance = bag.Entity.Position.DistanceTo(playerPosition);
                    if (distance > BagDespawnRadius && distance > farthestDistance)
                    {
                        farthestDistance = distance;
                        farthest = bag;
                    }
                }

                if (farthest == null)
                {
                    return;
                }

                DeleteBagEntity(farthest);
            }
        }

        private void CleanupMissingBagEntities()
        {
            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag == null || bag.Entity == null || bag.PickedUp)
                {
                    continue;
                }

                if (!bag.Entity.Exists())
                {
                    bag.Entity = null;
                }
            }
        }

        private int CountLiveBags()
        {
            var count = 0;
            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag != null && bag.Entity != null && bag.Entity.Exists())
                {
                    count += 1;
                }
            }

            return count;
        }

        private bool SpawnBagEntity(GarbageBagRuntime bag)
        {
            if (bag == null)
            {
                return false;
            }

            var modelName = ResolveBagModelName();
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            var model = new Model(modelName);
            Prop prop = null;
            try
            {
                if (!model.Request(BagModelRequestTimeoutMs))
                {
                    model.MarkAsNoLongerNeeded();
                    return false;
                }

                // SHVDN v3 has no World.CreateObject; CreateProp is the equivalent. The bag is
                // created on the ground (placeOnGround) so the configured alley coordinates,
                // which often sit above street level, never leave the bag floating in the air.
                prop = World.CreateProp(model, bag.Position, Vector3.Zero, true, true);
            }
            catch
            {
                prop = null;
            }
            finally
            {
                try
                {
                    model.MarkAsNoLongerNeeded();
                }
                catch
                {
                    // Model release is best-effort.
                }
            }

            if (prop == null || !prop.Exists())
            {
                return false;
            }

            try
            {
                // Same ground snap the industry output props use, then freeze so bags never
                // roll into traffic and no longer float above the pavement.
                Function.Call<bool>(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, prop.Handle);
                prop.IsPersistent = true;
                prop.IsPositionFrozen = true;
                prop.HasGravity = false;
            }
            catch
            {
                // Persistence/ground flags are best-effort.
            }

            bag.Entity = prop;
            return true;
        }

        private static string ResolveBagModelName()
        {
            if (!string.IsNullOrWhiteSpace(_resolvedBagModelName))
            {
                return _resolvedBagModelName;
            }

            for (int i = 0; i < BagModelCandidates.Length; i++)
            {
                var candidate = BagModelCandidates[i];
                try
                {
                    var model = new Model(candidate);
                    if (!model.IsValid)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                _resolvedBagModelName = candidate;
                return candidate;
            }

            System.Diagnostics.Debug.WriteLine(
                "GarbageSideJobSystem: no trash bag prop model resolved; skipping bag spawning.");
            return string.Empty;
        }

        private void DeleteBagEntity(GarbageBagRuntime bag)
        {
            if (bag == null)
            {
                return;
            }

            var entity = bag.Entity;
            bag.Entity = null;
            if (entity == null)
            {
                return;
            }

            try
            {
                entity.Detach();
            }
            catch
            {
                // Best-effort detach.
            }

            try
            {
                if (entity.Exists())
                {
                    entity.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private void DespawnAllBags()
        {
            for (int i = 0; i < _bags.Count; i++)
            {
                DeleteBagEntity(_bags[i]);
            }
        }

        /// <summary>
        /// Starts the take-bag sequence: the bag stays frozen on the ground while the one-shot
        /// take clip plays, and is pinned to the hand bone when the hand reaches it.
        /// </summary>
        private bool BeginBagPickup(GarbageBagRuntime bag)
        {
            var entity = bag != null ? bag.Entity : null;
            if (entity == null || !entity.Exists())
            {
                return false;
            }

            try
            {
                entity.IsPositionFrozen = true;
                entity.HasGravity = false;
            }
            catch
            {
                // Flag updates are best-effort.
            }

            BeginCarryAnimation();
            return true;
        }

        /// <summary>Pins the picked up bag to the right hand bone once the take clip reaches it.</summary>
        private void AttachCarriedBagToHand(Ped player)
        {
            var entity = _carriedBag != null ? _carriedBag.Entity : null;
            if (entity == null || !entity.Exists() || player == null || !player.Exists())
            {
                return;
            }

            try
            {
                entity.IsPositionFrozen = false;
                entity.HasGravity = false;

                var handOffset = GetHandAttachOffset();
                var handRotation = GetHandAttachRotation();
                var handBoneIndex = ResolveHandBoneIndex(player);

                // Lower the bag straight down in the world so it hangs from the palm instead of
                // being centred on it. Solved into the hand bone's own frame, otherwise the drop
                // only slid the bag along an unpredictable wrist axis.
                var dropMeters = GetBagDropMeters();
                if (dropMeters > 0f)
                {
                    Vector3 boneDrop;
                    if (TryConvertWorldOffsetToBoneOffset(
                        player,
                        handBoneIndex,
                        new Vector3(0f, 0f, -dropMeters),
                        out boneDrop))
                    {
                        handOffset = handOffset + boneDrop;
                    }
                }

                Function.Call(
                    Hash.ATTACH_ENTITY_TO_ENTITY,
                    entity.Handle,
                    player.Handle,
                    handBoneIndex,
                    handOffset.X,
                    handOffset.Y,
                    handOffset.Z,
                    handRotation.X,
                    handRotation.Y,
                    handRotation.Z,
                    true,
                    true,
                    false,
                    true,
                    1,
                    true);
            }
            catch
            {
                // The bag stays where it is when the attach fails; the weight is still counted.
            }
        }

        private Vector3 GetHandAttachOffset()
        {
            return _depot != null && _depot.HasBagAttachOverride ? _depot.BagAttachOffset : DefaultHandAttachOffset;
        }

        private Vector3 GetHandAttachRotation()
        {
            return _depot != null && _depot.HasBagAttachOverride ? _depot.BagAttachRotation : DefaultHandAttachRotation;
        }

        private float GetThrowInDistance()
        {
            return _depot != null && _depot.ThrowInDistance > 0f ? _depot.ThrowInDistance : ThrowInDistance;
        }

        private float GetBagDropMeters()
        {
            if (_depot != null && _depot.HasBagDropOverride)
            {
                return _depot.BagDropMeters;
            }

            return DefaultBagDropMeters;
        }

        /// <summary>
        /// Turns a world space displacement into an ATTACH_ENTITY_TO_ENTITY offset for the given
        /// ped bone. GET_PED_BONE_COORDS is probed on the three local axes so the bone's own
        /// orientation (and scale) is measured instead of assumed.
        /// </summary>
        private static bool TryConvertWorldOffsetToBoneOffset(Ped player, int boneIndex, Vector3 worldOffset, out Vector3 boneOffset)
        {
            boneOffset = worldOffset;
            try
            {
                var origin = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, player.Handle, boneIndex, 0f, 0f, 0f);
                var axisX = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, player.Handle, boneIndex, 1f, 0f, 0f) - origin;
                var axisY = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, player.Handle, boneIndex, 0f, 1f, 0f) - origin;
                var axisZ = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, player.Handle, boneIndex, 0f, 0f, 1f) - origin;

                var rowX = Vector3.Cross(axisY, axisZ);
                var determinant = Vector3.Dot(axisX, rowX);
                if (Math.Abs(determinant) < 0.0001f)
                {
                    return false;
                }

                var rowY = Vector3.Cross(axisZ, axisX);
                var rowZ = Vector3.Cross(axisX, axisY);
                boneOffset = new Vector3(
                    Vector3.Dot(rowX, worldOffset) / determinant,
                    Vector3.Dot(rowY, worldOffset) / determinant,
                    Vector3.Dot(rowZ, worldOffset) / determinant);
                return true;
            }
            catch
            {
                // A failed probe keeps the configured offset untouched.
                return false;
            }
        }

        /// <summary>
        /// Detaches (and deletes) the carried bag and puts it back into the route plan so it
        /// can be picked up again later. Never leaves a detached prop behind in the world.
        /// </summary>
        private void ReleaseCarriedBag()
        {
            var entity = _carriedBag != null ? _carriedBag.Entity : null;
            _pickupAnimPending = false;
            _pickupAttachAtGameTime = 0;
            _carryLoopAtGameTime = 0;
            _carryLoopProbeAtGameTime = 0;
            CleanupThrownBag();

            if (_carriedBag != null)
            {
                _carriedBag.PickedUp = false;
                _carriedBag.Entity = null;
                _carriedBag = null;
            }

            if (entity == null)
            {
                return;
            }

            try
            {
                entity.Detach();
            }
            catch
            {
                // Best-effort detach.
            }

            try
            {
                if (entity.Exists())
                {
                    entity.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        /// <summary>
        /// Starts the "take bag" sequence: the bag is pinned to the hand, then the base-game
        /// pick-up clip plays and the ped settles into the carry loop once the clip is done.
        /// </summary>
        private static int ResolveHandBoneIndex(Ped player)
        {
            try
            {
                var index = Function.Call<int>(Hash.GET_PED_BONE_INDEX, player.Handle, ReferenceRightHandBoneId);
                if (index > 0)
                {
                    return index;
                }
            }
            catch
            {
                // Fall through to the ph_r_hand bone.
            }

            return FallbackRightHandBoneId;
        }

        private void BeginCarryAnimation()
        {
            try
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, CarryAnimDictionary);
                _pickupAnimPending = true;
                _pickupAnimDeadlineGameTime = Game.GameTime + CarryAnimTimeoutMs;
            }
            catch
            {
                _pickupAnimPending = false;
            }
        }

        /// <summary>
        /// Plays the one-shot throw animation for the bag that was just thrown into the truck.
        /// The ped task is cleared again after a short delay so the ped returns to normal.
        /// </summary>
        private void BeginThrowAnimation(Ped player)
        {
            if (player == null || !player.Exists())
            {
                return;
            }

            try
            {
                Function.Call(
                    Hash.TASK_PLAY_ANIM,
                    player.Handle,
                    CarryAnimDictionary,
                    ThrowAnimClip,
                    8f,
                    -8f,
                    -1,
                    ThrowAnimFlags,
                    0f,
                    false,
                    false,
                    false);

                _carryTaskClearAtGameTime = Game.GameTime + ThrowTaskClearDelayMs;
                _throwReleaseAtGameTime = Game.GameTime + ThrowBagReleaseMs;
                _carryLoopAtGameTime = 0;
            }
            catch
            {
                _carryTaskClearAtGameTime = 0;
                _throwReleaseAtGameTime = 0;
            }
        }

        private void UpdateThrowSequence(Ped player, int gameTime)
        {
            if (_throwReleaseAtGameTime > 0 && gameTime >= _throwReleaseAtGameTime)
            {
                _throwReleaseAtGameTime = 0;
                ReleaseThrownBagFromHand(player);
            }

            if (_thrownBagDeleteAtGameTime > 0 && gameTime >= _thrownBagDeleteAtGameTime)
            {
                CleanupThrownBag();
            }

            if (_carryTaskClearAtGameTime > 0 && gameTime >= _carryTaskClearAtGameTime)
            {
                _carryTaskClearAtGameTime = 0;
                ClearCarryTask(player);
            }

            UpdateCarryLoopProbe(player, gameTime);
            UpdateCarryAnimations(player, gameTime);
        }

        /// <summary>
        /// Mid release clip the bag leaves the hand: it is detached, tossed towards the truck bed
        /// and despawned a moment later so the toss is visible.
        /// </summary>
        private void ReleaseThrownBagFromHand(Ped player)
        {
            var bag = _carriedBag;
            var entity = bag != null ? bag.Entity : null;
            _carriedBag = null;
            _thrownBag = bag;
            _carryHintShown = false;

            if (bag != null)
            {
                bag.PickedUp = false;
            }

            if (entity == null || !entity.Exists())
            {
                _thrownBag = null;
                return;
            }

            try
            {
                entity.Detach();
            }
            catch
            {
                // Best-effort detach.
            }

            try
            {
                var origin = entity.Position;
                var target = _activeJobTruck != null && _activeJobTruck.Exists()
                    ? GetTruckRearPosition(_activeJobTruck)
                    : origin + (player != null && player.Exists() ? player.ForwardVector : Vector3.Zero);

                var direction = target - origin;
                direction.Z = 0f;
                if (direction.Length() < 0.05f)
                {
                    direction = player != null && player.Exists() ? player.ForwardVector : new Vector3(0f, 1f, 0f);
                    direction.Z = 0f;
                }

                direction.Normalize();
                entity.HasGravity = true;
                Function.Call(
                    Hash.SET_ENTITY_VELOCITY,
                    entity.Handle,
                    direction.X * ThrownBagSpeedMps,
                    direction.Y * ThrownBagSpeedMps,
                    ThrownBagUpSpeedMps);
            }
            catch
            {
                // Toss momentum is cosmetic; the bag is despawned either way.
            }

            _thrownBagDeleteAtGameTime = Game.GameTime + ThrowBagDeleteMs;
        }

        private void CleanupThrownBag()
        {
            var bag = _thrownBag;
            _thrownBag = null;
            _thrownBagDeleteAtGameTime = 0;

            if (bag != null)
            {
                DeleteBagEntity(bag);
            }
        }

        /// <summary>
        /// Drives the bag animation: the take-bag clip plays once, then the looping carry clip
        /// takes over. Both are silently skipped when their dictionary is unavailable.
        /// </summary>
        private void UpdateCarryAnimations(Ped player, int gameTime)
        {
            if (_carriedBag == null)
            {
                return;
            }

            if (_pickupAnimPending)
            {
                try
                {
                    if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, CarryAnimDictionary))
                    {
                        Function.Call(
                            Hash.TASK_PLAY_ANIM,
                            player.Handle,
                            CarryAnimDictionary,
                            PickupAnimClip,
                            8f,
                            -8f,
                            -1,
                            PickupAnimFlags,
                            0f,
                            false,
                            false,
                            false);

                        _pickupAnimPending = false;
                        _pickupAttachAtGameTime = gameTime + PickupAttachAtMs;
                        _carryLoopAtGameTime = gameTime + PickupAnimHoldMs;
                    }
                    else if (gameTime >= _pickupAnimDeadlineGameTime)
                    {
                        // Dictionary never streamed in: hand the bag over without a pose.
                        _pickupAnimPending = false;
                        _pickupAttachAtGameTime = gameTime;
                    }
                }
                catch
                {
                    _pickupAnimPending = false;
                }
            }

            if (_pickupAttachAtGameTime > 0 && gameTime >= _pickupAttachAtGameTime)
            {
                _pickupAttachAtGameTime = 0;
                AttachCarriedBagToHand(player);
            }

            if (_carryLoopAtGameTime <= 0 || gameTime < _carryLoopAtGameTime)
            {
                return;
            }

            _carryLoopAtGameTime = 0;
            PlayCarryLoop(player, gameTime);
        }

        private void PlayCarryLoop(Ped player, int gameTime)
        {
            try
            {
                Function.Call(
                    Hash.TASK_PLAY_ANIM,
                    player.Handle,
                    CarryAnimDictionary,
                    CarryWalkAnimClip,
                    8f,
                    -8f,
                    -1,
                    CarryAnimFlags,
                    0f,
                    false,
                    false,
                    false);

                // Verify a beat later that the carry loop really started; when the clip is not
                // part of the dictionary the ped keeps the take pose instead of dropping the bag.
                _carryLoopProbeAtGameTime = gameTime + CarryProbeMs;
            }
            catch
            {
                _carryLoopProbeAtGameTime = 0;
            }
        }

        /// <summary>
        /// Falls back to the confirmed take-bag clip when the walking carry clip does not exist
        /// in the running game build, so the bag always stays in a held pose.
        /// </summary>
        private void UpdateCarryLoopProbe(Ped player, int gameTime)
        {
            if (_carryLoopProbeAtGameTime <= 0 || gameTime < _carryLoopProbeAtGameTime)
            {
                return;
            }

            _carryLoopProbeAtGameTime = 0;

            try
            {
                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player.Handle, CarryAnimDictionary, CarryWalkAnimClip, 3))
                {
                    return;
                }

                // The carry clip is missing on this build: hold the take pose instead.
                Function.Call(
                    Hash.TASK_PLAY_ANIM,
                    player.Handle,
                    CarryAnimDictionary,
                    PickupAnimClip,
                    8f,
                    -8f,
                    -1,
                    CarryHoldFallbackFlags,
                    0f,
                    false,
                    false,
                    false);
            }
            catch
            {
                // Animation is cosmetic only.
            }
        }

        private static void ClearCarryTask(Ped player)
        {
            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, player.Handle);
            }
            catch
            {
                // Task cleanup is best-effort.
            }
        }

        private void EnsureStopBlips()
        {
            if (_activeRoute == null)
            {
                return;
            }

            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop == null || stop.BagsCollected)
                {
                    continue;
                }

                if (stop.Blip != null && stop.Blip.Exists())
                {
                    continue;
                }

                stop.Blip = CreateStopBlip(stop);
            }
        }

        private void UpdateStopBlips()
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop == null)
                {
                    continue;
                }

                if (stop.Definition == null || stop.CollectedBags >= stop.TotalBags)
                {
                    ClearStopBlip(stop);
                    continue;
                }

                if (stop.Blip == null || !stop.Blip.Exists())
                {
                    stop.Blip = CreateStopBlip(stop);
                    continue;
                }

                var position = stop.AnchorResolved ? stop.Anchor : stop.Centroid;
                try
                {
                    stop.Blip.Position = position;
                }
                catch
                {
                    // Blip position refresh is best-effort.
                }
            }
        }

        private static Blip CreateStopBlip(GarbageStopRuntime stop)
        {
            if (stop == null)
            {
                return null;
            }

            try
            {
                var position = stop.AnchorResolved ? stop.Anchor : stop.Centroid;
                var blip = World.CreateBlip(position);
                if (blip == null || !blip.Exists())
                {
                    return null;
                }

                blip.Sprite = BlipSprite.OnMission;
                blip.Color = BlipColor.Yellow;
                blip.Name = stop.BlipName;
                blip.Scale = 0.85f;
                BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
                return blip;
            }
            catch
            {
                return null;
            }
        }

        private void ClearStopBlips()
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                ClearStopBlip(_stops[i]);
            }
        }

        private static void ClearStopBlip(GarbageStopRuntime stop)
        {
            if (stop == null || stop.Blip == null)
            {
                return;
            }

            try
            {
                if (stop.Blip.Exists())
                {
                    stop.Blip.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }

            stop.Blip = null;
        }

        private void EnsureDepotBlip()
        {
            if (_depot == null || _depotBlip != null)
            {
                return;
            }

            try
            {
                var blip = World.CreateBlip(_depot.Position);
                if (blip == null || !blip.Exists())
                {
                    return;
                }

                blip.Sprite = DepotBlipSprite;
                blip.Color = BlipColor.Green;
                blip.Name = string.IsNullOrWhiteSpace(_depot.Name) ? "Garbage Depot" : _depot.Name;
                blip.Scale = 0.9f;
                BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
                _depotBlip = blip;
            }
            catch
            {
                _depotBlip = null;
            }
        }

        private void ClearDepotBlip()
        {
            var blip = _depotBlip;
            _depotBlip = null;
            if (blip == null)
            {
                return;
            }

            try
            {
                if (blip.Exists())
                {
                    blip.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private void DrawDepotMarker(Ped player)
        {
            if (_depot == null || player.Position.DistanceTo(_depot.Position) > DepotMarkerDrawDistance)
            {
                return;
            }

            DrawGroundMarker(_depot.Position, Color.FromArgb(180, 92, 208, 144));
        }

        private void DrawStopMarkers(Ped player)
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop == null || !stop.AnchorResolved || stop.CollectedBags >= stop.TotalBags)
                {
                    continue;
                }

                if (player.Position.DistanceTo(stop.Anchor) > StopMarkerDrawDistance)
                {
                    continue;
                }

                DrawGroundMarker(stop.Anchor, Color.FromArgb(180, 92, 208, 144));
            }
        }

        private static void DrawGroundMarker(Vector3 position, Color color)
        {            try
            {
                World.DrawMarker(
                    MarkerType.Cylinder,
                    position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(2f, 2f, 1.5f),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }
            catch
            {
                // Marker drawing is best-effort.
            }
        }

        /// <summary>
        /// Floating arrow marker above every spawned trash bag so bags tucked into alleys and
        /// back lots are easy to spot from the street.
        /// </summary>
        private void DrawBagArrows(Ped player)
        {
            var drawDistanceSquared = BagArrowDrawDistance * BagArrowDrawDistance;
            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag == null || bag.Collected || bag.PickedUp)
                {
                    continue;
                }

                var entity = bag.Entity;
                if (entity == null || !entity.Exists())
                {
                    continue;
                }

                Vector3 position;
                try
                {
                    position = entity.Position + new Vector3(0f, 0f, BagArrowHeight);
                }
                catch
                {
                    continue;
                }

                if (player.Position.DistanceToSquared(position) > drawDistanceSquared)
                {
                    continue;
                }

                DrawArrowMarker(position);
            }
        }

        private static void DrawArrowMarker(Vector3 position)
        {
            try
            {
                // MarkerType.Arrow draws an arrow that points up by default; the 180 degree
                // rotation (plus a downward facing direction) turns it into an arrow that
                // points down at the bag lying on the ground.
                World.DrawMarker(
                    MarkerType.Arrow,
                    position,
                    new Vector3(0f, 0f, -1f),
                    new Vector3(180f, 0f, 0f),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    Color.FromArgb(220, 255, 214, 92),
                    false,
                    true,
                    false,
                    null,
                    null,
                    false);
            }
            catch
            {
                // Marker drawing is best-effort.
            }
        }

        private void UpdateDepotHint(Ped player)
        {
            if (_depot == null)
            {
                return;
            }

            var near = player.Position.DistanceTo(_depot.Position) <= DepotInteractHintDistance;
            if (near && !_nearDepotHint)
            {
                _nearDepotHint = true;
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyDepotHint,
                    "Garbage depot: {0}. Press Interact for trash trucks.",
                    _depot.Name));
            }
            else if (!near)
            {
                _nearDepotHint = false;
            }
        }

        private void RefreshRouteGps()
        {
            if (_activeRoute == null)
            {
                ClearGps();
                return;
            }

            var stop = GetNextUncollectedStop();
            if (stop == null)
            {
                ClearGps();
                return;
            }

            var position = stop.AnchorResolved ? stop.Anchor : stop.Centroid;
            if (position == Vector3.Zero)
            {
                ClearGps();
                return;
            }

            SetGps(position);
        }

        private GarbageStopRuntime GetNextUncollectedStop()
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop != null && stop.CollectedBags < stop.TotalBags)
                {
                    return stop;
                }
            }

            return null;
        }

        private GarbageStopRuntime GetStop(int index)
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                if (stop != null && stop.Index == index)
                {
                    return stop;
                }
            }

            return null;
        }

        private GarbageBagRuntime FindBag(int spawnId)
        {
            for (int i = 0; i < _bags.Count; i++)
            {
                var bag = _bags[i];
                if (bag != null && bag.SpawnId == spawnId)
                {
                    return bag;
                }
            }

            return null;
        }

        private GarbageTruckDefinition FindTruck(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            return _trucks.FirstOrDefault(t => t != null
                && string.Equals(t.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveTruckName(string modelName)
        {
            var definition = FindTruck(modelName);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Name))
            {
                return definition.Name;
            }

            return string.IsNullOrWhiteSpace(modelName) ? "Garbage truck" : modelName;
        }

        private GarbageRouteDefinition FindRoute(string routeId)
        {
            if (string.IsNullOrWhiteSpace(routeId))
            {
                return null;
            }

            return _routes.FirstOrDefault(r => r != null
                && string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase));
        }

        private float ResolveCapacityForModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return 0f;
            }

            var definition = _trucks.FirstOrDefault(t => t != null
                && string.Equals(t.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
            return definition != null ? definition.CapacityTons : 0f;
        }

        private static bool IsPlayerInAnyVehicle(Ped player)
        {
            try
            {
                return player.IsInVehicle();
            }
            catch
            {
                return false;
            }
        }

        private bool IsPlayerInActiveTruck(Ped player)
        {
            var truck = _activeJobTruck;
            if (truck == null || !truck.Exists())
            {
                return false;
            }

            try
            {
                return player.IsInVehicle(truck);
            }
            catch
            {
                return false;
            }
        }

        private static Vector3 GetTruckRearPosition(Vehicle truck)
        {
            if (truck == null || !truck.Exists())
            {
                return Vector3.Zero;
            }

            try
            {
                Vector3 minimum;
                Vector3 maximum;
                truck.Model.GetDimensions(out minimum, out maximum);
                if (minimum.Y < 0f)
                {
                    return truck.GetOffsetPosition(new Vector3(0f, minimum.Y, 0f));
                }
            }
            catch
            {
                // Dimension lookup is best-effort.
            }

            try
            {
                return truck.RearPosition;
            }
            catch
            {
                return truck.Position;
            }
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

        private static string Text(string key, string fallback)
        {
            return LocalizedText.GetOrDefault(key, fallback);
        }

        /// <summary>
        /// Average of a stop's bag positions. Pure maths so the geometry can be unit tested
        /// without a running game.
        /// </summary>
        internal static Vector3 ComputeCentroid(IReadOnlyList<Vector3> positions)
        {
            if (positions == null || positions.Count == 0)
            {
                return Vector3.Zero;
            }

            var x = 0f;
            var y = 0f;
            var z = 0f;
            for (int i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                x += position.X;
                y += position.Y;
                z += position.Z;
            }

            var count = positions.Count;
            return new Vector3(x / count, y / count, z / count);
        }

        /// <summary>
        /// Tip payout for a load: handling fee per bag plus a tipping fee per ton, scaled by
        /// the player's Garbage skill bonus multiplier.
        /// </summary>
        internal static float ComputeTipCash(int bags, float tons, float skillMultiplier)
        {
            var safeBags = Math.Max(0, bags);
            var safeTons = Math.Max(0f, tons);
            var multiplier = skillMultiplier > 0f ? skillMultiplier : 1f;
            var gross = safeBags * BagHandlingFee + safeTons * TippingFeePerTon;
            return (float)Math.Round(gross * multiplier);
        }

        /// <summary>Capacity gate: a bag is only accepted while it fits in the truck.</summary>
        internal static bool CanLoadBag(float onBoardTons, float bagTons, float capacityTons)
        {
            return onBoardTons + bagTons <= capacityTons;
        }

        /// <summary>Rolls one bag weight inside the locked [0.1, 0.3] tons band.</summary>
        internal static float RollBagWeight(Random random)
        {
            var roll = random != null ? random.NextDouble() : 0.5;
            return BagWeightMin + (float)(roll * (BagWeightMax - BagWeightMin));
        }

        internal static float ClampBagWeight(float weightTons)
        {
            if (weightTons <= 0f)
            {
                return BagWeightMin;
            }

            return Math.Max(BagWeightMin, Math.Min(BagWeightMax, weightTons));
        }

        internal static List<GarbageTruckDefinition> LoadGarbageTrucks(string configDirectory)
        {
            var result = new List<GarbageTruckDefinition>();
            foreach (var element in SideJobConfigLoader.EnumerateJobVehicles(configDirectory, GarbageJobId))
            {
                var modelName = ReadAttribute(element, "model");

                result.Add(new GarbageTruckDefinition
                {
                    Name = ReadAttribute(element, "name", modelName),
                    ModelName = modelName,
                    UnlockLevel = Math.Max(0, ReadIntAttribute(element, "unlockLevel", 0)),
                    CapacityTons = Math.Max(0.1f, ReadFloatAttribute(element, "garbageTons", 10f)),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, ReadFloatAttribute(element, "dailyRent", 0f)),
                    FuelCapacityLiters = Math.Max(0f, ReadFloatAttribute(element, "fuelCapacityLiters", 120f)),
                });
            }

            return result;
        }

        internal static List<GarbageRouteDefinition> LoadRoutes(string configDirectory)
        {
            var result = new List<GarbageRouteDefinition>();
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "SideJobs", "GarbageRoute.xml"));
            var root = document != null ? document.Root : null;
            if (root == null)
            {
                return result;
            }

            foreach (var routeElement in root.Elements("GarbageRoute"))
            {
                var routeId = ReadAttribute(routeElement, "id");
                if (string.IsNullOrWhiteSpace(routeId))
                {
                    continue;
                }

                var route = new GarbageRouteDefinition
                {
                    RouteId = routeId,
                    DisplayName = ReadAttribute(routeElement, "name", routeId),
                    DistrictName = ReadAttribute(routeElement, "district"),
                };

                var stops = new List<GarbageStopDefinition>();
                var stopsElement = routeElement.Element("Stops");
                if (stopsElement != null)
                {
                    foreach (var stopElement in stopsElement.Elements("StopBase"))
                    {
                        stops.Add(LoadStop(stopElement));
                    }
                }

                route.Stops = stops;
                result.Add(route);
            }

            return result;
        }

        private static GarbageStopDefinition LoadStop(XElement stopElement)
        {
            var positions = new List<Vector3>();
            if (stopElement != null)
            {
                var spawnElement = stopElement.Element("TrashBagsSpawnPosition");
                if (spawnElement != null)
                {
                    foreach (var vectorElement in spawnElement.Elements("Vector3"))
                    {
                        positions.Add(ReadVector3(vectorElement));
                    }
                }
            }

            return new GarbageStopDefinition
            {
                BagPositions = positions.ToArray(),
                Anchor = Vector3.Zero,
                AnchorHeading = 0f,
                AnchorResolved = false,
            };
        }

        internal static XDocument TryLoadDocument(string filePath)
        {
            return SideJobConfigLoader.TryLoadDocument(filePath);
        }

        /// <summary>
        /// Reads a Vector3 written with X/Y/Z ATTRIBUTES (the normalized GarbageRoute.xml
        /// shape), not nested coordinate elements.
        /// </summary>
        internal static Vector3 ReadVector3(XElement element)
        {
            if (element == null)
            {
                return Vector3.Zero;
            }

            return new Vector3(
                ReadFloatAttribute(element, "X", 0f),
                ReadFloatAttribute(element, "Y", 0f),
                ReadFloatAttribute(element, "Z", 0f));
        }

        private static GarbageDepotPoint LoadDepot(string configDirectory)
        {
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "SideJobs", "JobCoordinates.xml"));
            if (document == null || document.Root == null)
            {
                return null;
            }

            foreach (var element in document.Root.Elements("JobPoint"))
            {
                if (!string.Equals(ReadAttribute(element, "job"), GarbageJobId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ReadAttribute(element, "function"), GarbageDepotFunctionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (element.Attribute("x") == null && element.Attribute("y") == null && element.Attribute("z") == null)
                {
                    continue;
                }

                return new GarbageDepotPoint
                {
                    Name = ReadAttribute(element, "name", "Garbage Depot"),
                    Position = new Vector3(
                        ReadFloatAttribute(element, "x", 0f),
                        ReadFloatAttribute(element, "y", 0f),
                        ReadFloatAttribute(element, "z", 0f)),
                    Heading = ReadFloatAttribute(element, "heading", 0f),
                    HasBagAttachOverride = element.Attribute("bagOffsetX") != null
                        || element.Attribute("bagOffsetY") != null
                        || element.Attribute("bagOffsetZ") != null
                        || element.Attribute("bagRotX") != null
                        || element.Attribute("bagRotY") != null
                        || element.Attribute("bagRotZ") != null,
                    BagAttachOffset = new Vector3(
                        ReadFloatAttribute(element, "bagOffsetX", DefaultHandAttachOffset.X),
                        ReadFloatAttribute(element, "bagOffsetY", DefaultHandAttachOffset.Y),
                        ReadFloatAttribute(element, "bagOffsetZ", DefaultHandAttachOffset.Z)),
                    BagAttachRotation = new Vector3(
                        ReadFloatAttribute(element, "bagRotX", DefaultHandAttachRotation.X),
                        ReadFloatAttribute(element, "bagRotY", DefaultHandAttachRotation.Y),
                        ReadFloatAttribute(element, "bagRotZ", DefaultHandAttachRotation.Z)),
                    ThrowInDistance = ReadFloatAttribute(element, "throwDistance", 0f),
                    HasBagDropOverride = element.Attribute("bagDrop") != null,
                    BagDropMeters = ReadFloatAttribute(element, "bagDrop", DefaultBagDropMeters),
                };
            }

            return null;
        }

        private static string ReadAttribute(XElement element, string name, string fallback = null)
        {
            if (element == null || string.IsNullOrWhiteSpace(name))
            {
                return fallback ?? string.Empty;
            }

            var attribute = element.Attribute(name);
            return attribute != null
                ? (attribute.Value ?? fallback ?? string.Empty)
                : (fallback ?? string.Empty);
        }

        private static float ReadFloatAttribute(XElement element, string name, float fallback)
        {
            var raw = ReadAttribute(element, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            float parsed;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static int ReadIntAttribute(XElement element, string name, int fallback)
        {
            var raw = ReadAttribute(element, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        /// <summary>
        /// Resolves the depot blip sprite by name so a runtime that lacks a newer enum member
        /// degrades to the plain truck sprite instead of guessing a raw numeric value.
        /// </summary>
        private static BlipSprite ResolveDepotBlipSprite()
        {
            var candidates = new[] { "Trash", "GarbageTruck" };
            try
            {
                var enumType = typeof(BlipSprite);
                var available = new HashSet<string>(Enum.GetNames(enumType), StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    if (!available.Contains(candidate))
                    {
                        continue;
                    }

                    return (BlipSprite)Enum.Parse(enumType, candidate, true);
                }
            }
            catch
            {
                // Fall through to the base-game truck sprite.
            }

            return BlipSprite.Truck;
        }

        private sealed class GarbageDepotPoint
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public float Heading { get; set; }

            public bool HasBagAttachOverride { get; set; }
            public Vector3 BagAttachOffset { get; set; }
            public Vector3 BagAttachRotation { get; set; }
            public float ThrowInDistance { get; set; }

            /// <summary>True when bagDrop="..." is present, so 0 can mean "no drop at all".</summary>
            public bool HasBagDropOverride { get; set; }
            public float BagDropMeters { get; set; }
        }

        private sealed class GarbageStopRuntime
        {
            public int Index { get; set; }
            public GarbageStopDefinition Definition { get; set; }
            public Vector3 Centroid { get; set; }
            public Vector3 Anchor { get; set; }
            public float AnchorHeading { get; set; }
            public bool AnchorResolved { get; set; }
            public Blip Blip { get; set; }
            public string BlipName { get; set; }
            public int CollectedBags { get; set; }

            public int TotalBags
            {
                get { return Definition != null ? Definition.BagCount : 0; }
            }

            public bool BagsCollected
            {
                get { return TotalBags > 0 && CollectedBags >= TotalBags; }
            }
        }

        private sealed class GarbageBagRuntime
        {
            public int SpawnId { get; set; }
            public int StopIndex { get; set; }
            public Vector3 Position { get; set; }
            public float WeightTons { get; set; }
            public bool Collected { get; set; }
            public bool PickedUp { get; set; }
            public Prop Entity { get; set; }
        }
    }
}

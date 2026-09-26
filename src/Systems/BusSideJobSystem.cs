using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Config;
using LSOL.UI;

namespace LSOL.Systems
{
    /// <summary>Stop-by-stop state of the bus job. Driven from <c>BusSideJobSystem.Update</c>.</summary>
    public enum BusStopPhase
    {
        /// <summary>En route to the current station.</summary>
        Driving = 0,

        /// <summary>Stopped at the station with the doors still closed.</summary>
        Arrived = 1,

        /// <summary>Doors opened, waiting for the door animation to settle.</summary>
        DoorsOpen = 2,

        /// <summary>Passengers are getting off and on.</summary>
        Exchanging = 3,

        /// <summary>Exchange finished, doors closing before the line continues.</summary>
        Departing = 4,
    }

    /// <summary>
    /// Persisted state for the bus side job. Follows the same section-based persistence pattern as
    /// the other systems (see IndustryPersistenceManager). Legacy saves without a bus section
    /// deserialize to <c>null</c> and load safely.
    ///
    /// Ped and vehicle entities are deliberately never persisted: on restore the bus is spawned
    /// again at the depot and the waiting pool repopulates around the player.
    /// </summary>
    public sealed class BusPersistenceSnapshot
    {
        public BusPersistenceSnapshot()
        {
            OwnedBusModels = new List<string>();
            OnBoardRiders = new List<BusRiderSnapshot>();
            StationWaiting = new List<BusStationWaitingSnapshot>();
        }

        public string RouteId { get; set; }

        /// <summary>Index of the station the bus is currently serving (or driving to).</summary>
        public int CurrentStationIndex { get; set; }

        public int StationsServiced { get; set; }

        public int CompletedLoops { get; set; }

        public int RoutePassengersDelivered { get; set; }

        /// <summary>
        /// Passengers that could not board because the bus was full. A run with none is a perfect
        /// run and pays the perfect-route bonus.
        /// </summary>
        public int PassengersLeftBehind { get; set; }

        public float RouteCashEarned { get; set; }

        public float RouteXpEarned { get; set; }

        public bool DoorsOpen { get; set; }

        public string ActiveBusModelName { get; set; }

        /// <summary>Model names of the buses parked in the bus job garage.</summary>
        public List<string> OwnedBusModels { get; }

        /// <summary>Passengers currently on the bus, with their destination and seat.</summary>
        public List<BusRiderSnapshot> OnBoardRiders { get; }

        /// <summary>Still-to-be-served stations and the passengers waiting at them.</summary>
        public List<BusStationWaitingSnapshot> StationWaiting { get; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RouteId)
                    || StationsServiced > 0
                    || CompletedLoops > 0
                    || RoutePassengersDelivered > 0
                    || RouteCashEarned > 0.01f
                    || (OnBoardRiders != null && OnBoardRiders.Count > 0)
                    || (StationWaiting != null && StationWaiting.Count > 0)
                    || (OwnedBusModels != null && OwnedBusModels.Count > 0);
            }
        }
    }

    /// <summary>One passenger sitting on the bus at save time.</summary>
    public sealed class BusRiderSnapshot
    {
        public int RiderId { get; set; }

        public int BoardedStationIndex { get; set; }

        public int DestinationIndex { get; set; }

        /// <summary>Number of stations this passenger rides, which sets the fare.</summary>
        public int RiddenStops { get; set; }

        public int SeatIndex { get; set; }
    }

    /// <summary>One unserved route station: how many passengers are still waiting there.</summary>
    public sealed class BusStationWaitingSnapshot
    {
        public BusStationWaitingSnapshot()
        {
            RideOffsets = new List<int>();
        }

        public int StationIndex { get; set; }

        public int WaitingPassengers { get; set; }

        /// <summary>
        /// Ride length of every waiting passenger. Rolls happen when a passenger appears, not when
        /// they board, so they are persisted to keep a restored station faithful.
        /// </summary>
        public List<int> RideOffsets { get; }
    }

    /// <summary>A bus entry of JobVehicles.xml (type="Bus").</summary>
    public sealed class BusVehicleDefinition
    {
        public BusVehicleDefinition()
        {
            DoorIndices = new List<int>();
        }

        public string Name { get; set; }

        public string ModelName { get; set; }

        public int UnlockLevel { get; set; }

        /// <summary>Passenger seats (the driver is not counted), from the <c>seats</c> attribute.</summary>
        public int Seats { get; set; }

        public float Price { get; set; }

        public float DailyRent { get; set; }

        public float FuelCapacityLiters { get; set; }

        /// <summary>Model specific door indices from <c>doorIndices="0,2"</c>; empty means the default pair.</summary>
        public List<int> DoorIndices { get; }
    }

    /// <summary>
    /// One authored passenger waiting spot of a station: where a waiting passenger stands and which
    /// way they look. Exported from the reference transportation mod, whose stops store an explicit
    /// passenger position list ("Add psgr pos."), so the crowd lines up exactly where the stop
    /// expects it instead of being scattered around the station point.
    /// </summary>
    public sealed class BusPedSpawnPoint
    {
        public Vector3 Position { get; set; }

        public float Heading { get; set; }
    }

    /// <summary>
    /// One station of a bus route. The authored coordinate <em>is</em> the stop: GPS, the map blip,
    /// the ground marker and the arrival/door range all use it verbatim, with no street snapping.
    /// </summary>
    public sealed class BusStationDefinition
    {
        public BusStationDefinition()
        {
            ExportId = -1;
            ShelterModel = -1;
            PedSpawnPoints = new List<BusPedSpawnPoint>();
        }

        public int Index { get; set; }

        public string Name { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>Stop id of the exported catalogue comment (the reference mod's stop id), or -1.</summary>
        public int ExportId { get; set; }

        /// <summary>
        /// Authored passenger waiting spots (the "peds(n)" list of the exported comment). Empty for
        /// a station without export data, in which case the pedestrian layer falls back to the
        /// shelter position and finally to a small fan around the station point.
        /// </summary>
        public List<BusPedSpawnPoint> PedSpawnPoints { get; }

        /// <summary>Bus shelter prop model of the exported comment, or -1 when the stop has none.</summary>
        public int ShelterModel { get; set; }

        public Vector3 ShelterPosition { get; set; }

        public float ShelterHeading { get; set; }

        /// <summary>
        /// Facing of the bus when it stops here: the <c>heading</c> attribute when the XML sets one,
        /// otherwise the bearing towards the next station (see <see cref="BusSideJobSystem.LoadRoutes"/>).
        /// </summary>
        public float Heading { get; set; }

        /// <summary>True when the XML carried an explicit <c>heading</c>, which always wins.</summary>
        public bool HasHeadingOverride { get; set; }
    }

    /// <summary>A bus route of BusRoute.xml.</summary>
    public sealed class BusRouteDefinition
    {
        public BusRouteDefinition()
        {
            DistrictNames = new List<string>();
            Stations = new List<BusStationDefinition>();
        }

        public string RouteId { get; set; }

        /// <summary>Line number and termini, e.g. "45 Downtown LS - LSIA".</summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Canonical districts this route serves, taken from the districts="a,b,c" beacon of
        /// BusRoute.xml (each entry must match a district name in Districts.xml).
        /// </summary>
        public IReadOnlyList<string> DistrictNames { get; set; }

        public IReadOnlyList<BusStationDefinition> Stations { get; set; }

        public int StationCount
        {
            get { return Stations != null ? Stations.Count : 0; }
        }

        /// <summary>First tagged district; used as the fallback when a station cannot be resolved.</summary>
        public string PrimaryDistrictName
        {
            get { return DistrictNames != null && DistrictNames.Count > 0 ? DistrictNames[0] : string.Empty; }
        }

        /// <summary>
        /// True when the route ends at the station it started from: the XML repeats the origin stop
        /// as the last entry, so the loop can be driven again without a deadhead leg. Ride lengths
        /// wrap on those routes.
        /// </summary>
        public bool IsClosedLoop
        {
            get
            {
                var stations = Stations;
                if (stations == null || stations.Count < 2)
                {
                    return false;
                }

                var first = stations[0];
                var last = stations[stations.Count - 1];
                return first != null
                    && last != null
                    && !string.IsNullOrWhiteSpace(first.Name)
                    && string.Equals(first.Name, last.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Self-contained bus side job. The Downtown Bus Depot is both the job start point and the
    /// garage: the player takes a bus out there, picks a line, drives the station sequence with GPS,
    /// stops at each station, opens the bus doors with the dedicated door key to let passengers off
    /// and on, and is paid per fare (on alighting), per serviced station and once for the finished
    /// line.
    ///
    /// Progression reuses the existing <see cref="PlayerSkillSystem"/> (<see cref="PlayerSkillId.Bus"/>)
    /// — no new progression store is created. The waiting/boarding pedestrian layer is optional and
    /// is disabled on the Enhanced runtime, where ambient entity spawning is unsafe: the job then
    /// runs purely on the abstract passenger counts.
    ///
    /// Passenger handling follows the reference transportation mod: every stop of BusRoute.xml carries
    /// an exported comment with the stop's authored passenger waiting spots (its <c>PassengersPositions</c>)
    /// plus the bus shelter it belongs to, so waiting passengers stand exactly where the stop expects
    /// them. Boarding picks the lowest free passenger seat (the model's own capacity is respected),
    /// the configured entry doors are opened with the dedicated door key, and every scripted task is
    /// kept alive with SET_PED_KEEP_TASK (the reference mod's KeepTasks) so an ambient behaviour change
    /// cannot cancel a boarding or alighting passenger.
    /// </summary>
    internal sealed class BusSideJobSystem
    {
        // Payout and XP tuning (see the locked design values for the job).
        public const float BaseFare = 25f;
        public const float FarePerStation = 12f;
        public const float StationServiceFee = 60f;
        public const float RouteCompletionBonus = 2500f;
        public const float PerfectRouteBonus = 1200f;
        public const float RouteCompletionXp = 300f;
        public const float PassengerXp = 10f;
        public const int MinWaitingPassengers = 1;
        public const int MaxWaitingPassengers = 6;
        public const int MaxRideStops = 6;
        public const int MaxBoardPerStop = 4;

        /// <summary>Share of waiting passengers that ride a single stop, then two stops.</summary>
        public const double OneStopRideShare = 0.5;
        public const double TwoStopRideShare = 0.3;

        private static readonly int[] DefaultDoorIndices = { 0, 1 };

        // Station export comment grammar (see ApplyExportComment): "export id=10 | peds(6): (x,y,z,h) ...
        // | shelter: model=2142033519 at (x,y,z) h205.85".
        private static readonly Regex ExportIdPattern = new Regex(
            @"\bid\s*=\s*(?<id>\d+)",
            RegexOptions.CultureInvariant);

        private static readonly Regex PedSectionPattern = new Regex(
            @"peds\s*\(\s*\d+\s*\)\s*:(?<peds>.*?)(\|\s*shelter\s*:|$)",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private static readonly Regex PedSpawnPointPattern = new Regex(
            @"\(\s*(?<x>-?\d+(?:\.\d+)?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)\s*,\s*(?<z>-?\d+(?:\.\d+)?)\s*,\s*h\s*(?<h>-?\d+(?:\.\d+)?)\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex ShelterPattern = new Regex(
            @"(?:shelter\s*:\s*)?model\s*=\s*(?<model>\d+)\s+at\s*\(\s*(?<x>-?\d+(?:\.\d+)?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)\s*,\s*(?<z>-?\d+(?:\.\d+)?)\s*\)\s*h\s*(?<h>-?\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant);

        private const float ArriveDistance = 12f;

        /// <summary>
        /// Maximum distance at which the door key works. Deliberately a little more generous than the
        /// arrival radius so the player never stops just outside it and cannot open the doors.
        /// </summary>
        private const float DoorKeyMaxDistance = 15f;
        private const float StationarySpeedMps = 1.0f;
        private const float AutoCloseDoorSpeedMps = 2.0f;
        private const int DoorsOpenDwellMs = 1200;

        /// <summary>
        /// How long a stop may sit without any passenger moving or any task being retried before the
        /// exchange is closed. The valve is idle based (not wall-clock based) so a busy stop is never
        /// cut short while people are still walking in and out.
        /// </summary>
        private const int ExchangeIdleTimeoutMs = 30000;

        /// <summary>Absolute ceiling of a single stop: a line can never hang on one exchange.</summary>
        private const int ExchangeHardTimeoutMs = 120000;

        /// <summary>
        /// Window a passenger gets to walk out of the bus (rear seats must reach the front door)
        /// before the leave task is retried instead of the ped being placed on the sidewalk.
        /// </summary>
        private const int AlightTimeoutMs = 12000;

        /// <summary>Leave-task attempts per rider (first try plus retries) before the ped is placed outside.</summary>
        private const int AlightMaxAttempts = 3;

        /// <summary>Task window the game gets to walk a boarding passenger into the reserved seat.</summary>
        private const int BoardTimeoutMs = 12000;

        /// <summary>Extra window a blocked boarding passenger gets before the passenger gives up.</summary>
        private const int BoardRetryMs = 10000;

        /// <summary>Enter-task attempts per boarding passenger (first try plus retries).</summary>
        private const int BoardMaxAttempts = 3;

        /// <summary>
        /// Distance inside which seating a passenger is invisible: a ped already standing at the bus
        /// steps in, while one still on the sidewalk would visibly teleport across the street.
        /// </summary>
        private const float WarpToSeatDistance = 5f;

        private const int AlightedPedDespawnMs = 5000;
        private const int DepartingDwellMs = 1500;
        private const float DepotInteractHintDistance = 6f;
        private const float DepotMarkerDrawDistance = 400f;
        private const float StationMarkerDrawDistance = 250f;
        private const float DoorHintDistance = 30f;
        private const int VehicleModelRequestTimeoutMs = 1500;

        // Pedestrian layer anti-crowding knobs. Real milliseconds (Game.GameTime), like the other
        // side job cooldowns.
        private const float PedSpawnRadius = 180f;
        private const float PedDespawnRadius = 300f;

        /// <summary>
        /// Live waiting peds per stop. The catalogue authors four to nine real passenger spots per
        /// stop, so this is no longer an anti-crowding guess: it matches the highest waiting roll
        /// (<see cref="MaxWaitingPassengers"/>), which keeps the visible crowd in step with the number
        /// of passengers that can actually board.
        /// </summary>
        private const int MaxWaitingPerStation = 6;

        private const int MaxAmbientWaitingPeds = 12;
        private const int WaitingRefillCooldownMs = 120000;
        private const int WaitingLifetimeMs = 180000;
        private const int PedModelRequestTimeoutMs = 500;

        private const string BusJobId = "Bus";
        private const string BusDepotFunctionId = "BusDepot";

        private const string KeyInactive = "sidejob.bus.inactive";
        private const string KeyBusUnknown = "sidejob.bus.busUnknown";
        private const string KeyBusLocked = "sidejob.bus.busLocked";
        private const string KeyDepotMissing = "sidejob.bus.depotMissing";
        private const string KeySpawnFailed = "sidejob.bus.spawnFailed";
        private const string KeyBusReady = "sidejob.bus.busReady";
        private const string KeyDepotHint = "sidejob.bus.depotHint";
        private const string KeyRouteUnknown = "sidejob.bus.routeUnknown";
        private const string KeyRoutesMissing = "sidejob.bus.routesMissing";
        private const string KeyRouteBusy = "sidejob.bus.routeBusy";
        private const string KeyRouteStarted = "sidejob.bus.routeStarted";
        private const string KeyRouteResumed = "sidejob.bus.routeResumed";
        private const string KeyNeedBus = "sidejob.bus.needBus";
        private const string KeyNeedDoors = "sidejob.bus.needDoors";
        private const string KeyDoorsOpen = "sidejob.bus.doorsOpen";
        private const string KeyDoorsClosed = "sidejob.bus.doorsClosed";
        private const string KeyDoorsAutoClosed = "sidejob.bus.doorsAutoClosed";
        private const string KeyArrived = "sidejob.bus.arrived";
        private const string KeyStationServiced = "sidejob.bus.stationServiced";
        private const string KeyStationSkipped = "sidejob.bus.stationSkipped";
        private const string KeyFarePaid = "sidejob.bus.farePaid";
        private const string KeyBusFull = "sidejob.bus.busFull";
        private const string KeyRouteComplete = "sidejob.bus.routeComplete";
        private const string KeyBusPurchased = "sidejob.bus.busPurchased";
        private const string KeyCannotAfford = "sidejob.bus.cannotAfford";
        private const string KeyAlreadyOwned = "sidejob.bus.alreadyOwned";
        private const string KeyNotOwned = "sidejob.bus.notOwned";
        private const string KeyBusAlreadyOut = "sidejob.bus.busOut";
        private const string KeyBusStored = "sidejob.bus.busStored";
        private const string KeyNoBusOut = "sidejob.bus.noBusOut";
        private const string KeyStoreBlocked = "sidejob.bus.storeBlocked";
        private const string KeyDistrictBonus = "sidejob.bonus.districtBonus";

        private static readonly string[] PedModelCandidates =
        {
            "a_m_y_business_01",
            "a_f_y_business_01",
            "a_m_m_business_01",
            "a_m_y_hipster_01",
            "a_f_y_hipster_01",
            "a_m_y_tourist_01",
            "a_f_y_tourist_01",
            "a_m_m_farmer_01",
        };

        private static readonly string[] WaitingScenarioNames =
        {
            "WORLD_HUMAN_STAND_IMPATIENT",
            "WORLD_HUMAN_STAND_MOBILE",
            "WORLD_HUMAN_SMOKING",
        };

        // The depot keeps the classic bus sprite, resolved by name so a runtime without the member
        // degrades to the plain truck sprite instead of guessing a raw numeric value.
        private static readonly BlipSprite DepotBlipSprite = ResolveDepotBlipSprite();
        private static readonly BlipSprite StationBlipSprite = ResolveStationBlipSprite();

        private readonly string _configDirectory;
        private readonly string _doorKeyLabel;
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

        private readonly List<BusVehicleDefinition> _buses;
        private readonly List<BusRouteDefinition> _routes;
        private readonly List<DistrictConfig> _districts;
        private readonly List<BusStationRuntime> _stations;
        private readonly List<BusRiderRuntime> _onBoard;
        private readonly List<string> _ownedBusModels;
        private readonly List<BusRiderRuntime> _alighters;
        private readonly List<BusWaitingPassengerRuntime> _boardingQueue;
        private readonly List<BusAmbientPedRuntime> _ambientPeds;
        private readonly HashSet<string> _resolvedPedModels;

        // Not readonly: the depot entry can be re-read at runtime (TryReloadDepotConfiguration),
        // which is how the door indices and the station stop radius are tuned without rebuilding.
        private BusDepotPoint _depot;

        private BusRouteDefinition _activeRoute;
        private Vehicle _activeJobBus;
        private string _activeBusModelName = string.Empty;
        private int _activeBusSeats;
        private Blip _depotBlip;
        private Blip _stationBlip;
        private BusStopPhase _phase = BusStopPhase.Driving;
        private int _currentStationIndex;
        private int _stationsServiced;
        private int _completedLoops;
        private int _passengersDelivered;
        private int _passengersLeftBehind;
        private int _boardedThisStop;
        private int _alightedThisStop;
        private float _faresThisStop;
        private float _routeCashEarned;
        private float _routeXpEarned;
        private int _nextRiderId = 1;
        private int _phaseDeadlineGameTime;
        private int _exchangeStartedGameTime;
        private int _exchangeLastProgressGameTime;
        private bool _doorsOpen;
        private bool _modMechanicsEnabled = true;
        private bool _jobEnabled = true;
        private bool _pedLayerEnabled = true;
        private bool _nearDepotHint;
        private bool _arrivedHintShown;
        private bool _doorHintShown;
        private bool _autoCloseHintShown;
        private bool _exchangeAnnounced;
        private bool _needBusHintShown;

        public BusSideJobSystem(
            string configDirectory,
            PlayerSkillSystem skillSystem,
            Action<string> showStatus,
            Action<float> addProfit,
            Action requestAutosave,
            Func<float> getCompanyBalance = null,
            Action<float, string> deductProfit = null,
            Func<string, string, float> getDistrictBonusExcludingJob = null,
            Func<string, string, float, DistrictBonusAward> reportDistrictBonus = null,
            string doorKeyLabel = "B",
            bool pedLayerEnabled = true,
            Random random = null)
        {
            _configDirectory = configDirectory ?? string.Empty;
            _doorKeyLabel = string.IsNullOrWhiteSpace(doorKeyLabel) ? "B" : doorKeyLabel;
            _skillSystem = skillSystem;
            _showStatus = showStatus;
            _addProfit = addProfit;
            _requestAutosave = requestAutosave;
            _getCompanyBalance = getCompanyBalance;
            _deductProfit = deductProfit;
            _getDistrictBonusExcludingJob = getDistrictBonusExcludingJob;
            _reportDistrictBonus = reportDistrictBonus;
            // Injectable so tests can pin the roll sequence.
            _random = random ?? new Random();

            _buses = LoadBusVehicles(_configDirectory);
            _routes = LoadRoutes(_configDirectory);
            _districts = LoadDistricts(_configDirectory);
            _depot = LoadDepot(_configDirectory);

            _stations = new List<BusStationRuntime>();
            _onBoard = new List<BusRiderRuntime>();
            _ownedBusModels = new List<string>();
            _alighters = new List<BusRiderRuntime>();
            _boardingQueue = new List<BusWaitingPassengerRuntime>();
            _ambientPeds = new List<BusAmbientPedRuntime>();
            _resolvedPedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // The waiting crowd materialises on BOTH runtimes. It used to be gated off on the Enhanced
            // one as a precaution, which silently removed every passenger from the stops without any
            // in-game hint. Core.xml can still switch the whole layer off with
            // <Controls busPedLayer="false" />, in which case the job runs on the abstract counts.
            _pedLayerEnabled = pedLayerEnabled && LoadPedLayerEnabled(_configDirectory);
        }

        /// <summary>True while a bus line is in progress (with or without a bus).</summary>
        public bool HasActiveRoute
        {
            get { return _activeRoute != null; }
        }

        /// <summary>True while a bus of this job is spawned in the world.</summary>
        public bool HasBusOut
        {
            get { return _activeJobBus != null && _activeJobBus.Exists(); }
        }

        /// <summary>True while the doors of the active bus are open for a station stop.</summary>
        public bool AreDoorsOpen
        {
            get { return _doorsOpen; }
        }

        /// <summary>Current stop phase of the active line.</summary>
        public BusStopPhase Phase
        {
            get { return _phase; }
        }

        /// <summary>True when the waiting/boarding pedestrian layer is materialised in the world.</summary>
        public bool IsPedLayerActive
        {
            get { return _pedLayerEnabled; }
        }

        public IReadOnlyList<BusVehicleDefinition> GetBuses()
        {
            return _buses;
        }

        public IReadOnlyList<BusRouteDefinition> GetRoutes()
        {
            return _routes;
        }

        /// <summary>Buses bought by the player and parked in the bus job garage.</summary>
        public IReadOnlyList<BusVehicleDefinition> GetOwnedBuses()
        {
            var result = new List<BusVehicleDefinition>();
            for (int i = 0; i < _buses.Count; i++)
            {
                var definition = _buses[i];
                if (definition != null && OwnsBus(definition.ModelName))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        public bool OwnsBus(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            for (int i = 0; i < _ownedBusModels.Count; i++)
            {
                if (string.Equals(_ownedBusModels[i], modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the given model is the bus currently taken out.</summary>
        public bool IsActiveBusOut(string modelName)
        {
            return HasBusOut
                && !string.IsNullOrWhiteSpace(modelName)
                && string.Equals(_activeBusModelName, modelName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Buys a bus from the job dealership. Bought buses are parked in the job garage and are
        /// what the player takes out at the depot.
        /// </summary>
        public bool TryBuyBus(string modelName, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The bus side job is inactive.");
                return false;
            }

            var definition = FindBus(modelName);
            if (definition == null)
            {
                message = Text(KeyBusUnknown, "Unknown bus.");
                return false;
            }

            if (OwnsBus(definition.ModelName))
            {
                message = LocalizedText.FormatOrDefault(
                    KeyAlreadyOwned,
                    "You already own {0}.",
                    definition.Name);
                return false;
            }

            var busLevel = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.Bus) : 0;
            if (busLevel < definition.UnlockLevel)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyBusLocked,
                    "{0} requires Bus level {1}.",
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

            _deductProfit?.Invoke(price, string.Format("Bus: {0}", definition.Name));
            _ownedBusModels.Add(definition.ModelName);

            message = LocalizedText.FormatOrDefault(
                KeyBusPurchased,
                "Bought {0} for {1}. It is parked in the bus garage.",
                definition.Name,
                ModFormatting.FormatMoney(price));

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>
        /// Takes a bus out of the job garage: spawns it at the depot and puts the player in the
        /// driver seat. Only buses bought into the garage can be taken out.
        /// </summary>
        public bool TrySpawnBus(string modelName, Ped player, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The bus side job is inactive.");
                return false;
            }

            var definition = FindBus(modelName);
            if (definition == null)
            {
                message = Text(KeyBusUnknown, "Unknown bus.");
                return false;
            }

            if (!OwnsBus(definition.ModelName))
            {
                message = LocalizedText.FormatOrDefault(
                    KeyNotOwned,
                    "{0} is not in your bus garage. Buy it at the depot first.",
                    definition.Name);
                return false;
            }

            if (HasBusOut)
            {
                message = Text(KeyBusAlreadyOut, "A bus is already out. Store it first.");
                return false;
            }

            var busLevel = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.Bus) : 0;
            if (busLevel < definition.UnlockLevel)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyBusLocked,
                    "{0} requires Bus level {1}.",
                    definition.Name,
                    definition.UnlockLevel);
                return false;
            }

            if (_depot == null)
            {
                message = Text(KeyDepotMissing, "No bus depot configured.");
                return false;
            }

            var vehicle = SpawnVehicle(definition.ModelName, _depot.Position, _depot.Heading, true);
            if (vehicle == null || !vehicle.Exists())
            {
                message = Text(KeySpawnFailed, "Could not spawn the bus.");
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

            _activeJobBus = vehicle;
            _activeBusModelName = definition.ModelName ?? string.Empty;
            _activeBusSeats = ResolveSeatCount(vehicle, definition);
            _doorsOpen = false;
            ApplyDoorState(vehicle, false, GetDoorIndicesForModel(_activeBusModelName));

            if (_activeRoute != null)
            {
                // A restored route is re-aimed at the station it was left on.
                _phase = BusStopPhase.Driving;
                _phaseDeadlineGameTime = 0;
                RefreshStationTarget();
                message = LocalizedText.FormatOrDefault(
                    KeyRouteResumed,
                    "Route {0} resumed. {1} stations remaining.",
                    _activeRoute.DisplayName,
                    RemainingStationCount());
            }
            else
            {
                message = LocalizedText.FormatOrDefault(
                    KeyBusReady,
                    "{0} ready at {1}.",
                    definition.Name,
                    _depot.Name);
            }

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>Parks the bus currently taken out back into the job garage.</summary>
        public bool TryStoreBus(Ped player, out string message)
        {
            message = string.Empty;

            var bus = _activeJobBus;
            if (bus == null || !bus.Exists())
            {
                _activeJobBus = null;
                message = Text(KeyNoBusOut, "No bus is currently out.");
                return false;
            }

            try
            {
                if (player != null && player.Exists() && player.IsInVehicle(bus))
                {
                    message = Text(KeyStoreBlocked, "Get out of the bus before storing it.");
                    return false;
                }
            }
            catch
            {
                // Being unable to test the seat is not a reason to block storing.
            }

            var storedName = ResolveBusName(_activeBusModelName);
            ClearStationPassengers();
            try
            {
                bus.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }

            _activeJobBus = null;
            _activeBusModelName = string.Empty;
            _activeBusSeats = 0;
            _doorsOpen = false;
            _onBoard.Clear();
            _phase = BusStopPhase.Driving;
            _phaseDeadlineGameTime = 0;

            message = LocalizedText.FormatOrDefault(
                KeyBusStored,
                "{0} stored in the bus garage.",
                storedName);

            _requestAutosave?.Invoke();
            return true;
        }

        public bool TryStartRoute(string routeId, Ped player, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The bus side job is inactive.");
                return false;
            }

            var route = FindRoute(routeId);
            if (route == null)
            {
                message = string.IsNullOrWhiteSpace(routeId)
                    ? Text(KeyRoutesMissing, "No bus routes configured.")
                    : Text(KeyRouteUnknown, "Unknown bus route.");
                return false;
            }

            if (_activeRoute != null && string.Equals(_activeRoute.RouteId, route.RouteId, StringComparison.OrdinalIgnoreCase))
            {
                _phase = BusStopPhase.Driving;
                _phaseDeadlineGameTime = 0;
                RefreshStationTarget();
                message = LocalizedText.FormatOrDefault(
                    KeyRouteResumed,
                    "Route {0} resumed. {1} stations remaining.",
                    route.DisplayName,
                    RemainingStationCount());
                return true;
            }

            if (_activeRoute != null && (_stationsServiced > 0 || _onBoard.Count > 0))
            {
                message = Text(KeyRouteBusy, "Finish the current line before starting another one.");
                return false;
            }

            ResetRouteState();
            _activeRoute = route;
            _currentStationIndex = 0;
            _phase = BusStopPhase.Driving;
            BuildRouteRuntime(route);
            RefreshStationTarget();

            message = LocalizedText.FormatOrDefault(
                KeyRouteStarted,
                "Route {0} started. {1} stations.",
                route.DisplayName,
                route.StationCount);

            _requestAutosave?.Invoke();
            return true;
        }

        public bool TryGetNearestBusDepot(Vector3 position, float maxDistance, out Vector3 spawnPosition, out string depotName, out float heading)
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
        /// Door key entry point: opens or closes the entry doors of the bus the player is driving.
        /// Passengers only get on and off while the doors are open, so this is the interaction that
        /// services a station. Returns true when the key was consumed so the caller does not fall
        /// through to other key handlers.
        ///
        /// Refuses unless the bus is stopped (or nearly stopped) right at the current station, which
        /// keeps the doors from being opened in traffic.
        /// </summary>
        public bool TryHandleDoorKey(Ped player)
        {
            try
            {
                if (!_modMechanicsEnabled || !_jobEnabled)
                {
                    return false;
                }

                var bus = _activeJobBus;
                if (bus == null || !bus.Exists() || !IsDriverOfActiveBus(player))
                {
                    // Consume nothing: the key belongs to other systems when the player is not in
                    // the job bus.
                    return false;
                }

                if (!IsStoppedAtCurrentStation(bus, DoorKeyMaxDistance))
                {
                    if (_doorsOpen)
                    {
                        _doorsOpen = false;
                        ApplyDoorState(bus, false, GetDoorIndicesForModel(_activeBusModelName));
                        _showStatus?.Invoke(Text(KeyDoorsClosed, "Bus doors closed."));
                        return true;
                    }

                    _showStatus?.Invoke(Text(
                        KeyNeedDoors,
                        "Stop at the station before opening the bus doors."));
                    return true;
                }

                _doorsOpen = !_doorsOpen;
                ApplyDoorState(bus, _doorsOpen, GetDoorIndicesForModel(_activeBusModelName));

                if (_doorsOpen && _phase == BusStopPhase.Arrived)
                {
                    _phase = BusStopPhase.DoorsOpen;
                    _phaseDeadlineGameTime = Game.GameTime + DoorsOpenDwellMs;
                }
                else if (!_doorsOpen && (_phase == BusStopPhase.DoorsOpen || _phase == BusStopPhase.Exchanging))
                {
                    // The player closed the doors mid-stop: finish the exchange bookkeeping.
                    FinishExchange();
                }

                _doorHintShown = false;
                _autoCloseHintShown = false;

                _showStatus?.Invoke(_doorsOpen
                    ? Text(KeyDoorsOpen, "Bus doors open.")
                    : Text(KeyDoorsClosed, "Bus doors closed."));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Debug helper for the F9 menu: reports the door indices the current bus model really has,
        /// the configured indices and the seat capacity, while the player sits in a bus.
        /// </summary>
        public string BuildDoorProbeReport(Ped player)
        {
            Vehicle bus = null;
            try
            {
                if (player != null && player.Exists())
                {
                    bus = player.CurrentVehicle;
                }
            }
            catch
            {
                bus = null;
            }

            if (bus == null || !bus.Exists())
            {
                return "Bus probe: sit in a bus to read its doors and seat capacity.";
            }

            var presentDoors = new List<int>();
            try
            {
                foreach (VehicleDoorIndex door in Enum.GetValues(typeof(VehicleDoorIndex)))
                {
                    if (bus.Doors.Contains(door))
                    {
                        presentDoors.Add((int)door);
                    }
                }
            }
            catch
            {
                // Door enumeration is best-effort.
            }

            var configuredDoors = GetDoorIndicesForModel(ResolveModelName(bus));
            var maxPassengers = -1;
            try
            {
                maxPassengers = Function.Call<int>(Hash.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS, bus.Handle);
            }
            catch
            {
                // Native probe is best-effort.
            }

            string modelName;
            try
            {
                modelName = ResolveModelName(bus);
            }
            catch
            {
                modelName = "unknown";
            }

            var capacity = -1;
            try
            {
                capacity = bus.PassengerCapacity;
            }
            catch
            {
                capacity = -1;
            }

            var station = GetCurrentStation();
            var waitingAtStation = station != null && station.Waiting != null ? station.Waiting.Count : 0;

            return string.Format(
                CultureInfo.InvariantCulture,
                "Bus probe ({0}): doors present [{1}] | configured [{2}] | seats (XML/game) {3}/{4} | max passengers {5} | waiting crowd {6} | live peds {7}/{8} | waiting at this station {9}",
                modelName,
                string.Join(",", presentDoors.Select(index => index.ToString(CultureInfo.InvariantCulture))),
                string.Join(",", configuredDoors.Select(index => index.ToString(CultureInfo.InvariantCulture))),
                _activeBusSeats,
                capacity,
                maxPassengers,
                _pedLayerEnabled ? "on" : "off (busPedLayer=false in Core.xml)",
                CountLiveAmbientPeds(),
                MaxAmbientWaitingPeds,
                waitingAtStation);
        }

        /// <summary>
        /// Cancels the active bus job: stations, blips, GPS, waiting peds and fare progress are
        /// cleared. The parked bus and the buses owned in the job garage are kept. Never throws.
        /// </summary>
        public void CancelCurrentJob()
        {
            try
            {
                ResetRouteState();
                _nearDepotHint = false;
                _arrivedHintShown = false;
                _doorHintShown = false;
                _autoCloseHintShown = false;
            }
            catch
            {
                // Cancelling a job must never throw into the menu handler.
            }
        }

        /// <summary>
        /// Re-reads the depot entry from JobCoordinates.xml and the bus definitions from
        /// JobVehicles.xml, so the station stop radius, the door indices and the seat counts can be
        /// tuned in the XML without restarting the game. Routes, bus ownership and the running job
        /// are deliberately left untouched. Never throws.
        /// </summary>
        public bool TryReloadDepotConfiguration(out string message)
        {
            message = string.Empty;
            try
            {
                var reloaded = LoadDepot(_configDirectory);
                if (reloaded == null)
                {
                    message = "Bus depot entry not found in LSOL_Config/SideJobs/JobCoordinates.xml.";
                    return false;
                }

                _depot = reloaded;

                // Door indices and seat counts live on the vehicle entries, so refresh those too.
                var reloadedBuses = LoadBusVehicles(_configDirectory);
                if (reloadedBuses.Count > 0)
                {
                    _buses.Clear();
                    _buses.AddRange(reloadedBuses);
                    if (!string.IsNullOrWhiteSpace(_activeBusModelName))
                    {
                        var definition = FindBus(_activeBusModelName);
                        if (definition != null)
                        {
                            _activeBusSeats = ClampSeatsToVehicle(_activeJobBus, definition.Seats);
                        }
                    }
                }

                ClearDepotBlip();
                EnsureDepotBlip();
                EnsureStationBlip();

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Bus depot reloaded (station stop radius {0:0.0} m, doors [{1}]).",
                    GetStationStopDistance(),
                    string.Join(",", GetDoorIndicesForModel(_activeBusModelName).Select(index => index.ToString(CultureInfo.InvariantCulture))));
                return true;
            }
            catch
            {
                message = "Bus depot reload failed.";
                return false;
            }
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
                if (_activeRoute == null)
                {
                    // Nothing is running: only refresh the depot so the player can find it.
                    EnsureDepotBlip();
                    UpdateDepotHint(player);
                    DrawDepotMarker(player);
                    return;
                }

                EnsureStationBlip();
                DrawStationMarker(player);
                UpdateWaitingPool(player, gameTime);
                UpdateDoorAutoClose(player);
                UpdateStationPhase(player, gameTime);
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
        }

        /// <summary>
        /// Turns the waiting/boarding pedestrian layer on or off at runtime. Switching it off retires
        /// every ped the job spawned; the job keeps running on the abstract passenger counts. The
        /// <c>busPedLayer</c> attribute of Core.xml only sets the starting state, this setter is
        /// authoritative afterwards.
        /// </summary>
        public void SetPedLayerEnabled(bool enabled)
        {
            if (_pedLayerEnabled == enabled)
            {
                return;
            }

            _pedLayerEnabled = enabled;
            if (!enabled)
            {
                ClearAmbientPeds();
            }
        }

        public void ResetState()
        {
            ResetRouteState();
            ClearDepotBlip();

            _activeJobBus = null;
            _activeBusModelName = string.Empty;
            _activeBusSeats = 0;
            _doorsOpen = false;
            _ownedBusModels.Clear();
        }

        public BusPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new BusPersistenceSnapshot
            {
                RouteId = _activeRoute != null ? _activeRoute.RouteId : string.Empty,
                CurrentStationIndex = Math.Max(0, _currentStationIndex),
                StationsServiced = Math.Max(0, _stationsServiced),
                CompletedLoops = Math.Max(0, _completedLoops),
                RoutePassengersDelivered = Math.Max(0, _passengersDelivered),
                PassengersLeftBehind = Math.Max(0, _passengersLeftBehind),
                RouteCashEarned = Math.Max(0f, _routeCashEarned),
                RouteXpEarned = Math.Max(0f, _routeXpEarned),
                DoorsOpen = _doorsOpen,
                ActiveBusModelName = _activeBusModelName ?? string.Empty,
            };

            for (int i = 0; i < _ownedBusModels.Count; i++)
            {
                var modelName = _ownedBusModels[i];
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    snapshot.OwnedBusModels.Add(modelName);
                }
            }

            for (int i = 0; i < _onBoard.Count; i++)
            {
                var rider = _onBoard[i];
                if (rider == null)
                {
                    continue;
                }

                snapshot.OnBoardRiders.Add(new BusRiderSnapshot
                {
                    RiderId = rider.RiderId,
                    BoardedStationIndex = rider.BoardedStationIndex,
                    DestinationIndex = rider.DestinationIndex,
                    RiddenStops = rider.RiddenStops,
                    SeatIndex = rider.SeatIndex,
                });
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station == null || station.Definition == null || station.Serviced)
                {
                    continue;
                }

                var entry = new BusStationWaitingSnapshot
                {
                    StationIndex = station.Definition.Index,
                    WaitingPassengers = station.Waiting.Count,
                };

                for (int w = 0; w < station.Waiting.Count; w++)
                {
                    entry.RideOffsets.Add(station.Waiting[w].RiddenStops);
                }

                snapshot.StationWaiting.Add(entry);
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(BusPersistenceSnapshot snapshot)
        {
            ResetState();
            if (snapshot == null)
            {
                return;
            }

            _activeBusModelName = snapshot.ActiveBusModelName ?? string.Empty;
            _activeBusSeats = ResolveSeatCountForModel(_activeBusModelName);

            if (snapshot.OwnedBusModels != null)
            {
                for (int i = 0; i < snapshot.OwnedBusModels.Count; i++)
                {
                    var modelName = snapshot.OwnedBusModels[i];
                    if (string.IsNullOrWhiteSpace(modelName) || OwnsBus(modelName))
                    {
                        continue;
                    }

                    // Drop garage entries whose bus no longer exists in JobVehicles.xml.
                    if (FindBus(modelName) != null)
                    {
                        _ownedBusModels.Add(modelName);
                    }
                }
            }

            _passengersDelivered = Math.Max(0, snapshot.RoutePassengersDelivered);
            _passengersLeftBehind = Math.Max(0, snapshot.PassengersLeftBehind);
            _completedLoops = Math.Max(0, snapshot.CompletedLoops);
            _routeCashEarned = Math.Max(0f, snapshot.RouteCashEarned);
            _routeXpEarned = Math.Max(0f, snapshot.RouteXpEarned);
            _doorsOpen = snapshot.DoorsOpen;

            var route = FindRoute(snapshot.RouteId);
            if (route == null)
            {
                return;
            }

            _activeRoute = route;
            BuildRouteRuntime(route);

            var currentStationIndex = Math.Max(0, Math.Min(route.StationCount, snapshot.CurrentStationIndex));
            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station == null)
                {
                    continue;
                }

                if (i < currentStationIndex)
                {
                    // Stations before the saved cursor are already served; nobody waits there.
                    station.Serviced = true;
                    station.Waiting.Clear();
                }
            }

            if (snapshot.StationWaiting != null)
            {
                for (int i = 0; i < snapshot.StationWaiting.Count; i++)
                {
                    var entry = snapshot.StationWaiting[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var station = GetStation(entry.StationIndex);
                    if (station == null || station.Serviced)
                    {
                        continue;
                    }

                    station.Waiting.Clear();
                    var count = Math.Max(0, entry.WaitingPassengers);
                    for (int w = 0; w < count; w++)
                    {
                        var riddenStops = entry.RideOffsets != null && w < entry.RideOffsets.Count
                            ? entry.RideOffsets[w]
                            : RollRideOffset(_random);
                        station.Waiting.Add(new BusWaitingPassengerRuntime
                        {
                            RiddenStops = Math.Max(1, riddenStops),
                        });
                    }
                }
            }

            if (snapshot.OnBoardRiders != null)
            {
                for (int i = 0; i < snapshot.OnBoardRiders.Count; i++)
                {
                    var entry = snapshot.OnBoardRiders[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    if (entry.DestinationIndex < 0 || entry.DestinationIndex >= route.StationCount)
                    {
                        continue;
                    }

                    _onBoard.Add(new BusRiderRuntime
                    {
                        RiderId = entry.RiderId > 0 ? entry.RiderId : _nextRiderId++,
                        BoardedStationIndex = Math.Max(0, entry.BoardedStationIndex),
                        DestinationIndex = entry.DestinationIndex,
                        RiddenStops = Math.Max(1, entry.RiddenStops),
                        SeatIndex = Math.Max(0, entry.SeatIndex),
                    });
                }
            }

            if (_nextRiderId <= 0)
            {
                _nextRiderId = 1;
            }

            _currentStationIndex = currentStationIndex;
            _stationsServiced = Math.Max(0, Math.Min(route.StationCount, snapshot.StationsServiced));
            _phase = BusStopPhase.Driving;
            _phaseDeadlineGameTime = 0;

            RefreshStationTarget();
        }

        // ---------------------------------------------------------------- stop state machine

        private void UpdateStationPhase(Ped player, int gameTime)
        {
            if (!HasBusOut)
            {
                if (!_needBusHintShown)
                {
                    _needBusHintShown = true;
                    _showStatus?.Invoke(Text(
                        KeyNeedBus,
                        "Take a bus out of the depot garage before working a line."));
                }

                return;
            }

            _needBusHintShown = false;

            switch (_phase)
            {
                case BusStopPhase.Driving:
                    UpdateDriving(player, gameTime);
                    break;
                case BusStopPhase.Arrived:
                    UpdateArrived(player, gameTime);
                    break;
                case BusStopPhase.DoorsOpen:
                    UpdateDoorsOpen(player, gameTime);
                    break;
                case BusStopPhase.Exchanging:
                    UpdateExchanging(player, gameTime);
                    break;
                case BusStopPhase.Departing:
                    UpdateDeparting(player, gameTime);
                    break;
            }
        }

        private void UpdateDriving(Ped player, int gameTime)
        {
            var station = GetCurrentStation();
            var bus = _activeJobBus;
            if (station == null || station.Definition == null)
            {
                // The cursor ran past the last station: the line is ready to be closed out.
                if (_activeRoute != null && _stationsServiced >= _activeRoute.StationCount)
                {
                    TryCompleteRoute();
                }

                return;
            }

            if (station.Serviced)
            {
                // Never service a stop twice: skip forward to the next unserved one.
                AdvanceStationCursor();
                return;
            }

            if (bus == null || !bus.Exists() || !IsDriverOfActiveBus(player))
            {
                return;
            }

            if (bus.Position.DistanceTo(station.Definition.Position) > GetStationStopDistance())
            {
                return;
            }

            float speed;
            try
            {
                speed = bus.Speed;
            }
            catch
            {
                return;
            }

            if (speed >= StationarySpeedMps)
            {
                return;
            }

            _phase = BusStopPhase.Arrived;
            _arrivedHintShown = false;
            _doorHintShown = false;
            _autoCloseHintShown = false;
            _exchangeAnnounced = false;
        }

        private void UpdateArrived(Ped player, int gameTime)
        {
            var station = GetCurrentStation();
            var bus = _activeJobBus;
            if (station == null || station.Definition == null || bus == null || !bus.Exists() || !IsDriverOfActiveBus(player))
            {
                _phase = BusStopPhase.Driving;
                return;
            }

            if (HasLeftStation(bus, station))
            {
                _phase = BusStopPhase.Driving;
                _arrivedHintShown = false;
                _doorHintShown = false;
                return;
            }

            if (_doorsOpen)
            {
                _phase = BusStopPhase.DoorsOpen;
                _phaseDeadlineGameTime = gameTime + DoorsOpenDwellMs;
                return;
            }

            if (!_arrivedHintShown)
            {
                _arrivedHintShown = true;
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyArrived,
                    "Station {0}/{1}: {2}. Press {3} to open the bus doors.",
                    _currentStationIndex + 1,
                    _activeRoute.StationCount,
                    station.Definition.Name,
                    _doorKeyLabel));
            }

            if (!_doorHintShown)
            {
                _doorHintShown = true;
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyNeedDoors,
                    "Stop at the station and press {0} to open the bus doors.",
                    _doorKeyLabel));
            }

            ShowDoorKeyHelpText();
        }

        private void UpdateDoorsOpen(Ped player, int gameTime)
        {
            var bus = _activeJobBus;
            var station = GetCurrentStation();
            if (bus == null || !bus.Exists() || station == null || !IsDriverOfActiveBus(player))
            {
                _phase = BusStopPhase.Driving;
                return;
            }

            if (!_doorsOpen)
            {
                _phase = BusStopPhase.Arrived;
                return;
            }

            if (gameTime < _phaseDeadlineGameTime)
            {
                return;
            }

            BeginExchange(gameTime);
        }

        private void UpdateExchanging(Ped player, int gameTime)
        {
            var bus = _activeJobBus;
            var station = GetCurrentStation();
            if (bus == null || !bus.Exists() || station == null || station.Definition == null)
            {
                _phase = BusStopPhase.Driving;
                return;
            }

            if (!_doorsOpen)
            {
                // Doors were closed: finish what is already queued and move on.
                FinishExchange();
                return;
            }

            if (!_exchangeAnnounced)
            {
                _exchangeAnnounced = true;
                _exchangeStartedGameTime = gameTime;
                _exchangeLastProgressGameTime = gameTime;
            }

            var alightingDone = ProcessAlighting(bus, station, gameTime);
            var boardingDone = ProcessBoarding(bus, station, gameTime);
            if (alightingDone && boardingDone)
            {
                FinishExchange();
                return;
            }

            // The valve is idle based: a stop is closed when nothing moved and no task was retried for
            // ExchangeIdleTimeoutMs (a blocked ped, a closed door, a vanished seat), while a stop where
            // people keep walking in and out is allowed to run. ExchangeHardTimeoutMs is the absolute
            // ceiling that guarantees a line can never hang.
            var idleFor = gameTime - _exchangeLastProgressGameTime;
            var elapsed = gameTime - _exchangeStartedGameTime;
            if (ShouldCloseExchange(idleFor, elapsed))
            {
                ForceFinishExchange(bus, station);
            }
        }

        private void UpdateDeparting(Ped player, int gameTime)
        {
            if (gameTime < _phaseDeadlineGameTime)
            {
                return;
            }

            var bus = _activeJobBus;
            if (bus != null && bus.Exists())
            {
                _doorsOpen = false;
                ApplyDoorState(bus, false, GetDoorIndicesForModel(_activeBusModelName));
            }

            _phase = BusStopPhase.Driving;
            _arrivedHintShown = false;
            _doorHintShown = false;
            _autoCloseHintShown = false;
            _exchangeAnnounced = false;

            if (_stationsServiced >= (_activeRoute != null ? _activeRoute.StationCount : 0))
            {
                TryCompleteRoute();
                return;
            }

            RefreshStationTarget();
            _requestAutosave?.Invoke();
        }

        /// <summary>
        /// Starts the stop exchange: everybody whose destination is this station is queued to leave,
        /// then the waiting passengers board while seats remain.
        /// </summary>
        private void BeginExchange(int gameTime)
        {
            var station = GetCurrentStation();
            if (station == null || station.Definition == null)
            {
                return;
            }

            if (station.Serviced)
            {
                // A stop is only ever served once, however many times the bus rolls back onto it.
                AdvanceStationCursor();
                return;
            }

            _phase = BusStopPhase.Exchanging;
            _exchangeStartedGameTime = gameTime;
            _exchangeLastProgressGameTime = gameTime;
            _exchangeAnnounced = true;
            _boardedThisStop = 0;
            _alightedThisStop = 0;
            _faresThisStop = 0f;
            _alighters.Clear();
            _boardingQueue.Clear();

            var stationIndex = station.Definition.Index;
            var route = _activeRoute;
            var isTerminus = route != null && stationIndex >= route.StationCount - 1;
            for (int i = 0; i < _onBoard.Count; i++)
            {
                var rider = _onBoard[i];
                if (rider == null)
                {
                    continue;
                }

                // The terminus is the end of the line: everybody still on board gets off there, so a
                // loop never finishes with passengers aboard.
                if (isTerminus || rider.DestinationIndex == stationIndex)
                {
                    _alighters.Add(rider);
                }
            }

            for (int i = 0; i < station.Waiting.Count; i++)
            {
                var waiting = station.Waiting[i];
                if (waiting != null)
                {
                    _boardingQueue.Add(waiting);
                }
            }

            station.Waiting.Clear();

            // With the pedestrian layer off the exchange resolves on the same tick.
            if (!_pedLayerEnabled)
            {
                ResolveExchangeWithoutPeds(station);
            }
        }

        /// <summary>
        /// Abstract (no-ped) exchange: alighters leave and boarders take seats immediately, paying
        /// the same fares the pedestrian layer would pay.
        /// </summary>
        private void ResolveExchangeWithoutPeds(BusStationRuntime station)
        {
            var bus = _activeJobBus;
            for (int i = _alighters.Count - 1; i >= 0; i--)
            {
                var rider = _alighters[i];
                _onBoard.Remove(rider);
                PayFare(rider, station, bus);
            }

            _alighters.Clear();

            for (int i = 0; i < _boardingQueue.Count && _boardedThisStop < MaxBoardPerStop; i++)
            {
                var seat = FindFreeSeat();
                if (seat < 0)
                {
                    break;
                }

                _onBoard.Add(CreateRider(_boardingQueue[i], station.Definition.Index, seat));
                _boardedThisStop += 1;
            }

            // Whoever is still queued could not fit: that costs the perfect-route bonus.
            _passengersLeftBehind += Math.Max(0, _boardingQueue.Count - _boardedThisStop);
            _boardingQueue.Clear();
        }

        /// <summary>
        /// Processes the alighting queue. Returns true when no passenger is left to get off.
        /// </summary>
        private bool ProcessAlighting(Vehicle bus, BusStationRuntime station, int gameTime)
        {
            for (int i = _alighters.Count - 1; i >= 0; i--)
            {
                var rider = _alighters[i];
                if (rider == null)
                {
                    _alighters.RemoveAt(i);
                    continue;
                }

                if (!_pedLayerEnabled)
                {
                    CompleteAlighting(rider, station, bus);
                    _alighters.RemoveAt(i);
                    continue;
                }

                var ped = rider.Ped;
                if (ped == null || !ped.Exists())
                {
                    // The ped entity is gone (despawned or never materialised): the passenger still
                    // counts as delivered.
                    CompleteAlighting(rider, station, bus);
                    _alighters.RemoveAt(i);
                    continue;
                }

                if (rider.StateDeadlineGameTime <= 0)
                {
                    rider.StateDeadlineGameTime = gameTime + AlightTimeoutMs;
                    IssueLeaveVehicleTask(ped, bus);
                    continue;
                }

                var stillInside = IsPedInVehicle(ped, bus);
                if (!stillInside)
                {
                    ReleasePedAtStop(ped, station);
                    CompleteAlighting(rider, station, bus);
                    _alighters.RemoveAt(i);
                    continue;
                }

                if (gameTime < rider.StateDeadlineGameTime)
                {
                    continue;
                }

                // Still on board after the window: passengers in the rear of the bus have to walk to
                // the front door, and ambient behaviour can drop the leave task. Retrying keeps the
                // exit animation instead of yanking the ped out in plain sight of the player.
                rider.AlightAttempts += 1;
                if (rider.AlightAttempts < AlightMaxAttempts)
                {
                    rider.StateDeadlineGameTime = gameTime + AlightTimeoutMs;
                    MarkExchangeProgress();
                    IssueLeaveVehicleTask(ped, bus);
                    continue;
                }

                // Every attempt is spent (the seat path is genuinely blocked): the rider is delivered
                // and stepped out so the line can never stall.
                ReleasePedAtStop(ped, station, true);
                CompleteAlighting(rider, station, bus);
                _alighters.RemoveAt(i);
            }

            return _alighters.Count == 0;
        }

        /// <summary>
        /// Asks a rider to leave the bus through the open door. The task is re-issued on every retry so
        /// a leave that the game dropped still plays its animation.
        /// </summary>
        private static void IssueLeaveVehicleTask(Ped ped, Vehicle bus)
        {
            if (ped == null || bus == null)
            {
                return;
            }

            KeepPedOnTask(ped, true);
            try
            {
                ped.Task.LeaveVehicle(bus, false);
            }
            catch
            {
                // Best-effort: the retry loop or the last-resort placement still resolves the stop.
            }
        }

        /// <summary>
        /// Delivers a rider at this stop: the fare is paid, the rider leaves the on-board list and the
        /// stop counts as having progressed.
        /// </summary>
        private void CompleteAlighting(BusRiderRuntime rider, BusStationRuntime station, Vehicle bus)
        {
            if (rider == null)
            {
                return;
            }

            _onBoard.Remove(rider);
            PayFare(rider, station, bus);
            MarkExchangeProgress();
        }

        /// <summary>
        /// Processes the boarding queue. Returns true when the queue is drained, the per-stop cap is
        /// reached or no seat is free.
        /// </summary>
        private bool ProcessBoarding(Vehicle bus, BusStationRuntime station, int gameTime)
        {
            if (_boardingQueue.Count == 0)
            {
                return true;
            }

            var waiting = _boardingQueue[0];
            if (waiting == null)
            {
                _boardingQueue.RemoveAt(0);
                return false;
            }

            var ped = waiting.Ped;
            var tracked = ped != null && ped.Exists();

            // A boarding passenger that is already walking to their reserved seat is advanced first:
            // the seat search below would otherwise treat that reservation as a full bus.
            if (tracked && waiting.BoardingDeadlineGameTime > 0)
            {
                if (IsPedInVehicle(ped, bus))
                {
                    BoardQueuedPassenger(waiting, station, waiting.BoardingSeatIndex);
                    return false;
                }

                if (gameTime < waiting.BoardingDeadlineGameTime)
                {
                    return false;
                }

                // The window is over. A ped that is still walking (blocked by traffic, by another
                // passenger or by a closed door, or simply mid-animation) gets another window so the
                // boarding animation is kept instead of being yanked in from the sidewalk.
                waiting.BoardAttempts += 1;
                if (waiting.BoardAttempts < BoardMaxAttempts)
                {
                    waiting.BoardingDeadlineGameTime = gameTime + BoardRetryMs;
                    MarkExchangeProgress();
                    IssueEnterVehicleTask(ped, bus, waiting.BoardingSeatIndex);
                    return false;
                }

                // Attempts spent. Seating a ped that already stands at the bus is a single step and shows
                // no teleport, so that is the only case where the boarding animation is skipped.
                if (TrySeatPedAtBus(ped, bus, waiting.BoardingSeatIndex))
                {
                    BoardQueuedPassenger(waiting, station, waiting.BoardingSeatIndex);
                    return false;
                }

                // Still on the sidewalk: the passenger gives up and walks off (counted as left behind,
                // which costs the perfect-route bonus), so the stop still closes on its own.
                ReleasePedAtStop(ped, station);
                _passengersLeftBehind += 1;
                _boardingQueue.RemoveAt(0);
                MarkExchangeProgress();
                return false;
            }

            // No seat left or the per-stop cap reached: the rest of the queue stays for the report
            // and counts as left behind, which costs the perfect-route bonus.
            if (_boardedThisStop >= MaxBoardPerStop)
            {
                return true;
            }

            var seat = FindFreeSeat();
            if (seat < 0)
            {
                return true;
            }

            if (!_pedLayerEnabled || !tracked)
            {
                // No pedestrian layer (or the ped entity vanished): the passenger boards abstractly so
                // the seat is not wasted and the fare curve stays identical.
                BoardQueuedPassenger(waiting, station, seat);
                return false;
            }

            waiting.BoardingDeadlineGameTime = gameTime + BoardTimeoutMs;
            waiting.BoardingSeatIndex = seat;
            IssueEnterVehicleTask(ped, bus, seat);

            return false;
        }

        /// <summary>
        /// Walks a passenger into their reserved seat. The task is re-issued on every retry (a blocked
        /// ped otherwise stands at the door until the stop closes) and the seat stays reserved meanwhile.
        /// </summary>
        private static void IssueEnterVehicleTask(Ped ped, Vehicle bus, int seatIndex)
        {
            if (ped == null || bus == null || seatIndex < 0)
            {
                return;
            }

            KeepPedOnTask(ped, true);
            try
            {
                ped.Task.EnterVehicle(bus, (VehicleSeat)seatIndex, BoardTimeoutMs, 1.0f, EnterVehicleFlags.None);
            }
            catch
            {
                // Best-effort: the retry loop or the give-up path still resolves the passenger.
            }
        }

        /// <summary>
        /// Seats a passenger without an animation, which is only invisible when the ped already stands
        /// at the bus. Returns false when the ped is still away from the vehicle, in which case the
        /// caller keeps the animation path.
        /// </summary>
        private static bool TrySeatPedAtBus(Ped ped, Vehicle bus, int seatIndex)
        {
            if (ped == null || bus == null || seatIndex < 0)
            {
                return false;
            }

            try
            {
                if (!IsAtBusForSeating(ped.Position, bus.Position))
                {
                    return false;
                }

                Function.Call(Hash.SET_PED_INTO_VEHICLE, ped.Handle, bus.Handle, seatIndex);
                return IsPedInVehicle(ped, bus);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether a stop exchange must be closed: when nothing moved for <see cref="ExchangeIdleTimeoutMs"/>
        /// (blocked ped, closed door, vanished seat) or when the absolute ceiling
        /// <see cref="ExchangeHardTimeoutMs"/> is reached. A stop where people keep walking in and out is
        /// never cut short by its own duration.
        /// </summary>
        internal static bool ShouldCloseExchange(int idleForMs, int elapsedMs)
        {
            return idleForMs > ExchangeIdleTimeoutMs || elapsedMs > ExchangeHardTimeoutMs;
        }

        /// <summary>
        /// Whether a passenger may be seated without playing the boarding animation: only a ped that
        /// already stands at the bus, where stepping in is not visible as a teleport.
        /// </summary>
        internal static bool IsAtBusForSeating(Vector3 pedPosition, Vector3 busPosition)
        {
            return pedPosition.DistanceTo(busPosition) <= WarpToSeatDistance;
        }

        /// <summary>Seats a queued passenger and removes them from the boarding queue.</summary>
        private void BoardQueuedPassenger(BusWaitingPassengerRuntime waiting, BusStationRuntime station, int seatIndex)
        {
            var rider = CreateRider(waiting, station.Definition.Index, seatIndex);
            rider.Ped = waiting != null ? waiting.Ped : null;
            _onBoard.Add(rider);
            _boardedThisStop += 1;
            MarkExchangeProgress();

            if (waiting != null)
            {
                _boardingQueue.Remove(waiting);
            }
        }

        /// <summary>Ends the stop: reports it, queues the departure and advances the cursor.</summary>
        private void FinishExchange()
        {
            var station = GetCurrentStation();
            if (station == null || station.Definition == null)
            {
                _phase = BusStopPhase.Driving;
                return;
            }

            station.Serviced = true;
            _stationsServiced += 1;

            var leftBehind = _boardingQueue.Count;
            _passengersLeftBehind += leftBehind;

            _alighters.Clear();
            _boardingQueue.Clear();

            // Per-station service fee, scaled like every other payout of the job.
            var fee = (float)Math.Round(StationServiceFee * GetPayoutMultiplier(station.DistrictName));
            if (fee > 0f)
            {
                _addProfit?.Invoke(fee);
                _routeCashEarned += fee;
            }

            var summary = LocalizedText.FormatOrDefault(
                KeyStationServiced,
                "Station {0}/{1} served: {2} boarded, {3} off, {4} in fares. {5} on board.",
                _currentStationIndex + 1,
                _activeRoute != null ? _activeRoute.StationCount : 0,
                _boardedThisStop,
                _alightedThisStop,
                ModFormatting.FormatMoney(_faresThisStop),
                _onBoard.Count);

            if (leftBehind > 0)
            {
                summary = string.Concat(
                    summary,
                    " ",
                    LocalizedText.FormatOrDefault(
                        KeyBusFull,
                        "{0} passenger(s) left behind: the bus is full.",
                        leftBehind));
            }

            _showStatus?.Invoke(summary);

            ClearStationBlip();
            _phase = BusStopPhase.Departing;
            _phaseDeadlineGameTime = Game.GameTime + DepartingDwellMs;
            _requestAutosave?.Invoke();
        }

        /// <summary>
        /// Marks that the exchange moved (a passenger boarded, alighted or gave up), which restarts the
        /// idle valve of <see cref="ExchangeIdleTimeoutMs"/>. Retries count as progress too: a ped that
        /// is still being worked on is not a stalled stop.
        /// </summary>
        private void MarkExchangeProgress()
        {
            _exchangeLastProgressGameTime = Game.GameTime;
        }

        /// <summary>Safety valve: never let a stalled ped task block the line forever.</summary>
        private void ForceFinishExchange(Vehicle bus, BusStationRuntime station)
        {
            for (int i = _alighters.Count - 1; i >= 0; i--)
            {
                var rider = _alighters[i];
                if (rider == null)
                {
                    _alighters.RemoveAt(i);
                    continue;
                }

                if (rider.Ped != null && rider.Ped.Exists())
                {
                    // Last resort: a rider whose leave attempts are exhausted is stepped out so the
                    // bus can drive on.
                    ReleasePedAtStop(rider.Ped, station, true);
                }

                _onBoard.Remove(rider);
                _alighters.RemoveAt(i);
                PayFare(rider, station, bus);
            }

            for (int i = 0; i < _boardingQueue.Count; i++)
            {
                var waiting = _boardingQueue[i];
                if (waiting != null && waiting.Ped != null && waiting.Ped.Exists())
                {
                    // Waiting passengers who never made it in walk off instead of vanishing in front of
                    // the player; the pool despawns them a moment later.
                    ReleasePedAtStop(waiting.Ped, station);
                }
            }

            _boardingQueue.Clear();
            _showStatus?.Invoke(Text(KeyStationSkipped, "Station exchange timed out; the line continues."));
            FinishExchange();
        }

        private bool TryCompleteRoute()
        {
            var route = _activeRoute;
            if (route == null)
            {
                return false;
            }

            if (_stationsServiced < route.StationCount || _onBoard.Count > 0)
            {
                return false;
            }

            var multiplier = GetPayoutMultiplier(route.PrimaryDistrictName);
            var perfect = _passengersLeftBehind <= 0;
            var bonus = (float)Math.Round((RouteCompletionBonus + (perfect ? PerfectRouteBonus : 0f)) * multiplier);
            _addProfit?.Invoke(bonus);
            _routeCashEarned += bonus;

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.Bus, RouteCompletionXp, PlayerSkillXpSource.Player);
            }

            _routeXpEarned += RouteCompletionXp;
            _completedLoops += 1;

            var routeName = route.DisplayName;
            var passengers = _passengersDelivered;
            var cash = _routeCashEarned;
            var xp = _routeXpEarned;

            // Credit the district once the line is genuinely finished, and fold the gain into the
            // same status line so the completion summary is not overwritten.
            var districtSuffix = ReportDistrictCompletion(route);

            _showStatus?.Invoke(string.Concat(
                LocalizedText.FormatOrDefault(
                    KeyRouteComplete,
                    "Route {0} complete: {1} passengers carried. Total {2} and {3:0} Bus XP.",
                    routeName,
                    passengers,
                    ModFormatting.FormatMoney(cash),
                    xp),
                districtSuffix));

            ResetRouteState();
            _requestAutosave?.Invoke();
            return true;
        }

        private void ResetRouteState()
        {
            ClearGps();
            ClearStationBlip();

            _stations.Clear();
            _alighters.Clear();
            _boardingQueue.Clear();

            _activeRoute = null;
            _currentStationIndex = 0;
            _stationsServiced = 0;
            _passengersDelivered = 0;
            _passengersLeftBehind = 0;
            _routeCashEarned = 0f;
            _routeXpEarned = 0f;
            _boardedThisStop = 0;
            _alightedThisStop = 0;
            _faresThisStop = 0f;
            _phase = BusStopPhase.Driving;
            _phaseDeadlineGameTime = 0;
            _exchangeStartedGameTime = 0;
            _arrivedHintShown = false;
            _doorHintShown = false;
            _autoCloseHintShown = false;
            _exchangeAnnounced = false;

            // Waiting peds belong to the cancelled line.
            for (int i = 0; i < _onBoard.Count; i++)
            {
                DeletePed(_onBoard[i] != null ? _onBoard[i].Ped : null);
            }

            _onBoard.Clear();
            ClearAmbientPeds();
        }

        private void HideWorldVisuals()
        {
            ClearGps();
            ClearDepotBlip();
            ClearStationBlip();
            ClearAmbientPeds();

            _nearDepotHint = false;
            _arrivedHintShown = false;
            _doorHintShown = false;
        }

        // ---------------------------------------------------------------- route runtime

        private void BuildRouteRuntime(BusRouteDefinition route)
        {
            _stations.Clear();
            _onBoard.Clear();
            _alighters.Clear();
            _boardingQueue.Clear();

            if (route == null || route.Stations == null)
            {
                return;
            }

            var isLoop = route.IsClosedLoop;
            for (int stationIndex = 0; stationIndex < route.Stations.Count; stationIndex++)
            {
                var definition = route.Stations[stationIndex];
                var isTerminus = stationIndex >= route.Stations.Count - 1;

                var runtime = new BusStationRuntime
                {
                    Index = stationIndex,
                    Definition = definition,
                    BlipName = string.Format("{0} - {1}", route.DisplayName, stationIndex + 1),
                    DistrictName = ResolveStationDistrict(definition, route),
                };

                // Nobody waits at the terminus: the run ends there, so boarded passengers would have
                // to get off again immediately.
                if (!isTerminus)
                {
                    var waiting = RollWaitingPassengers(_random);
                    if (definition != null && definition.PedSpawnPoints.Count > 0)
                    {
                        // Never ask for more passengers than the stop has authored waiting spots.
                        waiting = Math.Min(waiting, definition.PedSpawnPoints.Count);
                    }

                    for (int i = 0; i < waiting; i++)
                    {
                        runtime.Waiting.Add(new BusWaitingPassengerRuntime
                        {
                            RiddenStops = ResolveRiddenStops(RollRideOffset(_random), stationIndex, route, isLoop),
                        });
                    }
                }

                _stations.Add(runtime);
            }
        }

        private string ResolveStationDistrict(BusStationDefinition definition, BusRouteDefinition route)
        {
            var fallback = route != null ? route.PrimaryDistrictName : string.Empty;
            if (definition == null)
            {
                return fallback;
            }

            var resolved = ResolveDistrictName(definition.Position, _districts);
            return string.IsNullOrWhiteSpace(resolved) ? fallback : resolved;
        }

        private BusStationRuntime GetCurrentStation()
        {
            if (_currentStationIndex < 0 || _currentStationIndex >= _stations.Count)
            {
                return null;
            }

            return _stations[_currentStationIndex];
        }

        /// <summary>
        /// Skips every station that was already served and re-aims GPS/blip at the next one, or closes
        /// the line out when the cursor runs past the terminus.
        /// </summary>
        private void AdvanceStationCursor()
        {
            while (_currentStationIndex < _stations.Count)
            {
                var station = _stations[_currentStationIndex];
                if (station == null || !station.Serviced)
                {
                    break;
                }

                _currentStationIndex += 1;
            }

            _phase = BusStopPhase.Driving;
            _arrivedHintShown = false;
            _doorHintShown = false;
            _autoCloseHintShown = false;
            _exchangeAnnounced = false;

            if (_activeRoute != null && _stationsServiced >= _activeRoute.StationCount)
            {
                TryCompleteRoute();
                return;
            }

            RefreshStationTarget();
        }

        private BusStationRuntime GetStation(int index)
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station != null && station.Index == index)
                {
                    return station;
                }
            }

            return null;
        }

        private int RemainingStationCount()
        {
            return Math.Max(0, (_activeRoute != null ? _activeRoute.StationCount : 0) - _stationsServiced);
        }

        /// <summary>Re-aims GPS and the single station blip at the station being driven to.</summary>
        private void RefreshStationTarget()
        {
            var station = GetCurrentStation();
            var definition = station != null ? station.Definition : null;
            if (definition == null)
            {
                ClearGps();
                ClearStationBlip();
                return;
            }

            var position = definition.Position;
            if (position == Vector3.Zero)
            {
                ClearGps();
                ClearStationBlip();
                return;
            }

            SetGps(position);
            EnsureStationBlip();
        }

        private bool HasLeftStation(Vehicle bus, BusStationRuntime station)
        {
            if (bus == null || station == null || station.Definition == null)
            {
                return true;
            }

            if (bus.Position.DistanceTo(station.Definition.Position) > GetStationStopDistance() + 6f)
            {
                return true;
            }

            try
            {
                return bus.Speed >= StationarySpeedMps;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStoppedAtCurrentStation(Vehicle bus, float maxDistance)
        {
            var station = GetCurrentStation();
            if (station == null || station.Definition == null || bus == null || !bus.Exists())
            {
                return false;
            }

            if (bus.Position.DistanceTo(station.Definition.Position) > maxDistance)
            {
                return false;
            }

            try
            {
                return bus.Speed < StationarySpeedMps;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- fares and districts

        /// <summary>Lowest free passenger seat, or -1 when the bus is full.</summary>
        private int FindFreeSeat()
        {
            var bus = _activeJobBus;
            for (int seat = 0; seat < _activeBusSeats; seat++)
            {
                var occupied = false;
                for (int i = 0; i < _onBoard.Count; i++)
                {
                    if (_onBoard[i] != null && _onBoard[i].SeatIndex == seat)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (occupied)
                {
                    continue;
                }

                // A seat being walked to by a boarding passenger is reserved until they arrive.
                for (int i = 0; i < _boardingQueue.Count; i++)
                {
                    var waiting = _boardingQueue[i];
                    if (waiting != null && waiting.BoardingDeadlineGameTime > 0 && waiting.BoardingSeatIndex == seat)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (occupied)
                {
                    continue;
                }

                if (bus != null && bus.Exists() && _pedLayerEnabled)
                {
                    try
                    {
                        if (!bus.IsSeatFree((VehicleSeat)seat))
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        // Seat probing is best-effort; the bookkeeping above still applies.
                    }
                }

                return seat;
            }

            return -1;
        }

        private BusRiderRuntime CreateRider(BusWaitingPassengerRuntime waiting, int stationIndex, int seatIndex)
        {
            var route = _activeRoute;
            var riddenStops = waiting != null && waiting.RiddenStops > 0 ? waiting.RiddenStops : 1;
            var destination = route != null
                ? ResolveDestinationIndex(stationIndex, riddenStops, route.StationCount, route.IsClosedLoop)
                : stationIndex;

            return new BusRiderRuntime
            {
                RiderId = _nextRiderId++,
                BoardedStationIndex = stationIndex,
                DestinationIndex = destination,
                RiddenStops = riddenStops,
                SeatIndex = seatIndex,
            };
        }

        /// <summary>
        /// Pays one alighting passenger. The fare is settled on alighting (not boarding), scaled by
        /// the skill bonus and by the district bonus of the station the passenger leaves at.
        /// </summary>
        private void PayFare(BusRiderRuntime rider, BusStationRuntime station, Vehicle bus)
        {
            if (rider == null)
            {
                return;
            }

            _passengersDelivered += 1;
            _alightedThisStop += 1;

            var districtName = station != null ? station.DistrictName : string.Empty;
            var fare = ComputeFare(rider.RiddenStops, GetPayoutMultiplier(districtName));
            if (fare > 0f)
            {
                _addProfit?.Invoke(fare);
                _routeCashEarned += fare;
                _faresThisStop += fare;
            }

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.Bus, PassengerXp, PlayerSkillXpSource.Player);
                _routeXpEarned += PassengerXp;
            }

            if (bus != null && bus.Exists() && _pedLayerEnabled && fare > 0f)
            {
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyFarePaid,
                    "Fare {0} collected ({1} stop(s)).",
                    ModFormatting.FormatMoney(fare),
                    rider.RiddenStops));
            }
        }

        /// <summary>Skill bonus times the district bonus of the given district (Bus pool excluded).</summary>
        private float GetPayoutMultiplier(string districtName)
        {
            var skillMultiplier = _skillSystem != null ? _skillSystem.GetBonusMultiplier(PlayerSkillId.Bus) : 1f;
            return skillMultiplier * GetDistrictPayoutMultiplier(districtName);
        }

        private float GetDistrictPayoutMultiplier(string districtName)
        {
            if (_getDistrictBonusExcludingJob == null || string.IsNullOrWhiteSpace(districtName))
            {
                return 1f;
            }

            try
            {
                var percent = Math.Max(0f, _getDistrictBonusExcludingJob(districtName, DistrictBonusCatalog.BusJobId));
                return 1f + (percent / 100f);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Credits a finished line to its primary district and returns the status suffix describing
        /// the gain. Routes without a district tag contribute nothing and stay silent.
        /// </summary>
        private string ReportDistrictCompletion(BusRouteDefinition route)
        {
            if (_reportDistrictBonus == null || route == null || string.IsNullOrWhiteSpace(route.PrimaryDistrictName))
            {
                return string.Empty;
            }

            try
            {
                var award = _reportDistrictBonus(DistrictBonusCatalog.BusJobId, route.PrimaryDistrictName, 1f);
                if (award == null || !award.Applied || award.AppliedPoints <= 0f)
                {
                    return string.Empty;
                }

                return string.Concat(
                    " ",
                    LocalizedText.FormatOrDefault(
                        KeyDistrictBonus,
                        "District bonus {0} +{1:0.#}% ({2:0.#} / {3:0.#}%).",
                        ModFormatting.FormatDistrictName(route.PrimaryDistrictName),
                        award.AppliedPoints,
                        award.TotalPercent,
                        award.CapPercent));
            }
            catch
            {
                return string.Empty;
            }
        }

        // ---------------------------------------------------------------- pedestrian layer

        private void UpdateWaitingPool(Ped player, int gameTime)
        {
            if (!_pedLayerEnabled)
            {
                return;
            }

            var playerPosition = player.Position;

            // Retire peds that lingered, wandered off, or that the player left behind.
            for (int i = _ambientPeds.Count - 1; i >= 0; i--)
            {
                var ambient = _ambientPeds[i];
                var ped = ambient != null ? ambient.Ped : null;
                if (ambient == null || ped == null || !ped.Exists())
                {
                    _ambientPeds.RemoveAt(i);
                    continue;
                }

                var owner = FindStationOwningPed(ped);

                if (!ambient.Released && gameTime >= ambient.WanderAtGameTime)
                {
                    // A waiting passenger only waits so long before they give up and walk off.
                    ambient.Released = true;
                    try
                    {
                        ped.Task.Wander(3f, false);
                    }
                    catch
                    {
                        // Wandering is cosmetic.
                    }
                }

                var retire = gameTime >= ambient.DespawnAtGameTime
                    || playerPosition.DistanceTo(ped.Position) > PedDespawnRadius;
                if (!retire)
                {
                    continue;
                }

                DeletePed(ped);
                _ambientPeds.RemoveAt(i);

                if (owner != null)
                {
                    owner.NextRefillGameTime = gameTime + WaitingRefillCooldownMs;
                }
            }

            // Materialise the waiting passengers of nearby stations.
            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station == null || station.Definition == null)
                {
                    continue;
                }

                if (station.Serviced || station.Waiting.Count == 0)
                {
                    continue;
                }

                if (gameTime < station.NextRefillGameTime)
                {
                    continue;
                }

                if (playerPosition.DistanceTo(station.Definition.Position) > PedSpawnRadius)
                {
                    continue;
                }

                if (CountLiveAmbientPeds() >= MaxAmbientWaitingPeds)
                {
                    return;
                }

                SpawnWaitingPeds(station, gameTime);
            }
        }

        private void SpawnWaitingPeds(BusStationRuntime station, int gameTime)
        {
            var pending = new List<BusWaitingPassengerRuntime>();
            for (int i = 0; i < station.Waiting.Count; i++)
            {
                var waiting = station.Waiting[i];
                if (waiting != null && waiting.Ped == null)
                {
                    pending.Add(waiting);
                }
            }

            var slot = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                if (slot >= MaxWaitingPerStation || CountLiveAmbientPeds() >= MaxAmbientWaitingPeds)
                {
                    break;
                }

                var ped = CreateWaitingPed(station, slot);
                if (ped == null)
                {
                    break;
                }

                pending[i].Ped = ped;
                pending[i].SpawnedAtGameTime = gameTime;
                _ambientPeds.Add(new BusAmbientPedRuntime
                {
                    Ped = ped,
                    WanderAtGameTime = gameTime + WaitingLifetimeMs,
                    DespawnAtGameTime = gameTime + WaitingLifetimeMs + AlightedPedDespawnMs,
                });
                slot += 1;
            }
        }

        private Ped CreateWaitingPed(BusStationRuntime station, int slot)
        {
            var modelName = ResolvePedModelName();
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            Vector3 position;
            float heading;
            if (!TryGetWaitingSpot(station.Definition, slot, out position, out heading))
            {
                // Legacy catalogue without export data: fan the crowd out next to the station point.
                position = station.Definition.Position + new Vector3(
                    (slot - 1) * 1.6f,
                    ((slot % 2) == 0 ? 1f : -1f) * 1.2f,
                    0f);
                heading = station.Definition.Heading;
            }

            var model = new Model(modelName);
            Ped ped = null;
            try
            {
                if (!model.Request(PedModelRequestTimeoutMs))
                {
                    return null;
                }

                ped = World.CreatePed(model, position, heading);
            }
            catch
            {
                ped = null;
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

            if (ped == null || !ped.Exists())
            {
                return null;
            }

            try
            {
                ped.IsPersistent = true;
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
                var scenario = WaitingScenarioNames[slot % WaitingScenarioNames.Length];
                ped.Task.StartScenarioInPlace(scenario, 0, true);
            }
            catch
            {
                // Scenario and flags are cosmetic.
            }

            // Same as the reference mod's KeepTasks: the scripted scenario and every boarding task
            // must survive ambient behaviour changes.
            KeepPedOnTask(ped, true);
            return ped;
        }

        /// <summary>
        /// Waiting spot of one passenger: the authored export position first, then the shelter the
        /// stop declares, and finally nothing (the caller falls back to the station point).
        /// </summary>
        internal static bool TryGetWaitingSpot(BusStationDefinition definition, int slot, out Vector3 position, out float heading)
        {
            position = Vector3.Zero;
            heading = 0f;

            if (definition == null)
            {
                return false;
            }

            if (definition.PedSpawnPoints.Count > 0)
            {
                var point = definition.PedSpawnPoints[Math.Abs(slot) % definition.PedSpawnPoints.Count];
                if (point != null)
                {
                    position = point.Position;
                    heading = point.Heading;
                    return true;
                }
            }

            if (definition.ShelterModel > 0 && definition.ShelterPosition != Vector3.Zero)
            {
                // Wait in a loose rank in front of the shelter.
                var column = Math.Abs(slot) % 3;
                var row = (Math.Abs(slot) / 3) % 3;
                position = definition.ShelterPosition + new Vector3(
                    (column - 1) * 1.1f,
                    (row - 1) * 1.1f,
                    0f);
                heading = definition.ShelterHeading;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Keeps a scripted ped on its task (the SET_PED_KEEP_TASK native, which is what the reference
        /// transportation mod's KeepTasks flag does). The managed Ped.AlwaysKeepTask property is
        /// obsolete and only covers peds marked as no longer needed, so the native is used directly.
        /// </summary>
        private static void KeepPedOnTask(Ped ped, bool keep)
        {
            if (ped == null)
            {
                return;
            }

            try
            {
                Function.Call(Hash.SET_PED_KEEP_TASK, ped.Handle, keep);
            }
            catch
            {
                // Task persistence is best-effort.
            }
        }

        /// <summary>
        /// Releases a passenger the job is done with: the ped wanders off from the stop and is cleaned up
        /// by the pool tick. <paramref name="forceOut"/> is the last resort for a ped that could not leave
        /// the bus on its own — it is placed on the sidewalk, which is the only case where a passenger
        /// moves without its animation.
        /// </summary>
        private void ReleasePedAtStop(Ped ped, BusStationRuntime station, bool forceOut = false)
        {
            if (ped == null || !ped.Exists())
            {
                return;
            }

            KeepPedOnTask(ped, false);

            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);
            }
            catch
            {
                // Task cleanup is best-effort.
            }

            if (forceOut && station != null && station.Definition != null)
            {
                try
                {
                    ped.Position = station.Definition.Position + new Vector3(0f, 1.2f, 0f);
                }
                catch
                {
                    // The ped is deleted by the pool tick if it cannot be moved.
                }
            }

            try
            {
                ped.Task.Wander(3f, false);
            }
            catch
            {
                // Wandering is cosmetic.
            }

            // The ped walks away from the stop and is despawned a moment later so people never pile
            // up on the sidewalk.
            _ambientPeds.Add(new BusAmbientPedRuntime
            {
                Ped = ped,
                WanderAtGameTime = 0,
                DespawnAtGameTime = Game.GameTime + AlightedPedDespawnMs,
                Released = true,
            });
        }

        private BusStationRuntime FindStationOwningPed(Ped ped)
        {
            if (ped == null)
            {
                return null;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station == null)
                {
                    continue;
                }

                for (int w = 0; w < station.Waiting.Count; w++)
                {
                    var waiting = station.Waiting[w];
                    if (waiting != null && waiting.Ped == ped)
                    {
                        return station;
                    }
                }
            }

            return null;
        }

        private int CountLiveAmbientPeds()
        {
            var count = 0;
            for (int i = 0; i < _ambientPeds.Count; i++)
            {
                var ambient = _ambientPeds[i];
                if (ambient != null && ambient.Ped != null && ambient.Ped.Exists())
                {
                    count += 1;
                }
            }

            return count;
        }

        private void ClearAmbientPeds()
        {
            for (int i = 0; i < _ambientPeds.Count; i++)
            {
                var ambient = _ambientPeds[i];
                if (ambient != null)
                {
                    DeletePed(ambient.Ped);
                }
            }

            _ambientPeds.Clear();
        }

        private void ClearStationPassengers()
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station == null)
                {
                    continue;
                }

                station.Waiting.Clear();
            }

            ClearAmbientPeds();
        }

        private string ResolvePedModelName()
        {
            for (int i = 0; i < PedModelCandidates.Length; i++)
            {
                var candidate = PedModelCandidates[i];
                if (_resolvedPedModels.Contains(candidate))
                {
                    return candidate;
                }

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

                _resolvedPedModels.Add(candidate);
                return candidate;
            }

            return string.Empty;
        }

        // ---------------------------------------------------------------- door helpers

        private IReadOnlyList<int> GetDoorIndicesForModel(string modelName)
        {
            var definition = FindBus(modelName);
            if (definition != null && definition.DoorIndices.Count > 0)
            {
                return definition.DoorIndices;
            }

            return DefaultDoorIndices;
        }

        /// <summary>Opens or closes the entry doors. Never throws.</summary>
        private static void ApplyDoorState(Vehicle bus, bool open, IReadOnlyList<int> doorIndices)
        {
            if (bus == null || !bus.Exists() || doorIndices == null)
            {
                return;
            }

            for (int i = 0; i < doorIndices.Count; i++)
            {
                var doorIndex = (VehicleDoorIndex)doorIndices[i];
                try
                {
                    if (!bus.Doors.Contains(doorIndex))
                    {
                        continue;
                    }

                    var door = bus.Doors[doorIndex];
                    if (open)
                    {
                        door.Open(false, false);
                    }
                    else
                    {
                        door.Close(false);
                    }
                }
                catch
                {
                    // Door state is cosmetic; boarding is gated by _doorsOpen bookkeeping.
                }
            }
        }

        private void UpdateDoorAutoClose(Ped player)
        {
            if (!_doorsOpen)
            {
                return;
            }

            var bus = _activeJobBus;
            if (bus == null || !bus.Exists())
            {
                _doorsOpen = false;
                return;
            }

            float speed;
            try
            {
                speed = bus.Speed;
            }
            catch
            {
                return;
            }

            if (speed <= AutoCloseDoorSpeedMps)
            {
                return;
            }

            _doorsOpen = false;
            ApplyDoorState(bus, false, GetDoorIndicesForModel(_activeBusModelName));

            if (!_autoCloseHintShown)
            {
                _autoCloseHintShown = true;
                _showStatus?.Invoke(Text(KeyDoorsAutoClosed, "Bus doors closed: the bus is moving."));
            }

            if (_phase == BusStopPhase.Arrived || _phase == BusStopPhase.DoorsOpen)
            {
                _phase = BusStopPhase.Driving;
            }
        }

        private void ShowDoorKeyHelpText()
        {
            try
            {
                var text = LocalizedText.FormatOrDefault(
                    KeyNeedDoors,
                    "Press {0} to open the bus doors.",
                    _doorKeyLabel);
                Screen.ShowHelpTextThisFrame(text);
            }
            catch
            {
                // Help text is cosmetic.
            }
        }

        // ---------------------------------------------------------------- world visuals

        /// <summary>
        /// One blip for the station currently being driven to. Routes carry up to 33 stations, so a
        /// blip per station would flood the map: only the current target is shown.
        /// </summary>
        private void EnsureStationBlip()
        {
            var station = GetCurrentStation();
            var definition = station != null ? station.Definition : null;
            if (definition == null)
            {
                ClearStationBlip();
                return;
            }

            var position = definition.Position;
            if (position == Vector3.Zero)
            {
                ClearStationBlip();
                return;
            }

            if (_stationBlip != null && _stationBlip.Exists())
            {
                try
                {
                    _stationBlip.Position = position;
                    _stationBlip.Name = station.BlipName;
                }
                catch
                {
                    // Blip refresh is best-effort.
                }

                return;
            }

            try
            {
                var blip = World.CreateBlip(position);
                if (blip == null || !blip.Exists())
                {
                    return;
                }

                blip.Sprite = StationBlipSprite;
                blip.Color = BlipColor.Yellow;
                blip.Name = station.BlipName;
                blip.Scale = 0.9f;
                BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
                _stationBlip = blip;
            }
            catch
            {
                _stationBlip = null;
            }
        }

        private void ClearStationBlip()
        {
            var blip = _stationBlip;
            _stationBlip = null;
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
                blip.Name = string.IsNullOrWhiteSpace(_depot.Name) ? "Bus Depot" : _depot.Name;
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

        private void DrawStationMarker(Ped player)
        {
            var station = GetCurrentStation();
            var definition = station != null ? station.Definition : null;
            if (definition == null || definition.Position == Vector3.Zero)
            {
                return;
            }

            if (player.Position.DistanceTo(definition.Position) > StationMarkerDrawDistance)
            {
                return;
            }

            DrawGroundMarker(definition.Position, Color.FromArgb(200, 92, 208, 144));
        }

        private static void DrawGroundMarker(Vector3 position, Color color)
        {
            try
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
                    "Bus depot: {0}. Press Interact for buses and lines.",
                    _depot.Name));
            }
            else if (!near)
            {
                _nearDepotHint = false;
            }
        }

        // ---------------------------------------------------------------- vehicle and ped plumbing

        private bool IsDriverOfActiveBus(Ped player)
        {
            var bus = _activeJobBus;
            if (bus == null || !bus.Exists() || player == null || !player.Exists())
            {
                return false;
            }

            try
            {
                if (!player.IsInVehicle(bus))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            try
            {
                var driver = bus.Driver;
                if (driver != null && driver.Exists())
                {
                    return driver.Handle == player.Handle;
                }
            }
            catch
            {
                // Fall through to the seat check.
            }

            try
            {
                return player.SeatIndex == VehicleSeat.Driver;
            }
            catch
            {
                // Being unable to read the seat is not fatal: sitting in the bus is enough.
                return true;
            }
        }

        private static bool IsPedInVehicle(Ped ped, Vehicle vehicle)
        {
            if (ped == null || !ped.Exists() || vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            try
            {
                return ped.IsInVehicle(vehicle);
            }
            catch
            {
                return false;
            }
        }

        private static void DeletePed(Ped ped)
        {
            if (ped == null)
            {
                return;
            }

            try
            {
                if (ped.Exists())
                {
                    ped.Delete();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static string ResolveModelName(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return string.Empty;
            }

            try
            {
                return vehicle.Model.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Model seat count, clamped by the vehicle's own passenger capacity.</summary>
        private static int ResolveSeatCount(Vehicle vehicle, BusVehicleDefinition definition)
        {
            var xmlSeats = definition != null ? Math.Max(1, definition.Seats) : 1;
            return ClampSeatsToVehicle(vehicle, xmlSeats);
        }

        private static int ClampSeatsToVehicle(Vehicle vehicle, int seats)
        {
            var requested = Math.Max(1, seats);
            if (vehicle == null || !vehicle.Exists())
            {
                return requested;
            }

            try
            {
                var capacity = vehicle.PassengerCapacity;
                if (capacity <= 0)
                {
                    capacity = Function.Call<int>(Hash.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS, vehicle.Handle);
                }

                if (capacity <= 0)
                {
                    capacity = Function.Call<int>(Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS, vehicle.Model.Hash);
                }

                if (capacity > 0)
                {
                    return Math.Max(1, Math.Min(requested, capacity));
                }
            }
            catch
            {
                // Fall through to the configured value.
            }

            return requested;
        }

        private int ResolveSeatCountForModel(string modelName)
        {
            var definition = FindBus(modelName);
            return definition != null ? Math.Max(1, definition.Seats) : 0;
        }

        private BusVehicleDefinition FindBus(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            return _buses.FirstOrDefault(b => b != null
                && string.Equals(b.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveBusName(string modelName)
        {
            var definition = FindBus(modelName);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Name))
            {
                return definition.Name;
            }

            return string.IsNullOrWhiteSpace(modelName) ? "Bus" : modelName;
        }

        private BusRouteDefinition FindRoute(string routeId)
        {
            if (string.IsNullOrWhiteSpace(routeId))
            {
                return null;
            }

            return _routes.FirstOrDefault(r => r != null
                && string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase));
        }

        private float GetStationStopDistance()
        {
            // "stationStopDistance" on the Bus depot JobPoint tunes both the arrival radius and the
            // door range, which is what a short shuttle hop between adjacent stops needs.
            return _depot != null && _depot.StationStopDistance > 0f ? _depot.StationStopDistance : ArriveDistance;
        }

        private static Vehicle SpawnVehicle(string modelName, Vector3 position, float heading, bool persistent)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            var model = new Model(modelName);
            if (!model.Request(VehicleModelRequestTimeoutMs))
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

        // ---------------------------------------------------------------- pure helpers

        /// <summary>
        /// Seats a boarding wave can fill: never more than the passengers waiting, the seats still
        /// free, or the per-stop boarding cap.
        /// </summary>
        internal static int AllocateSeats(int waitingPassengers, int freeSeats, int maxPerStop)
        {
            if (waitingPassengers <= 0 || freeSeats <= 0 || maxPerStop <= 0)
            {
                return 0;
            }

            return Math.Min(Math.Min(waitingPassengers, freeSeats), maxPerStop);
        }

        /// <summary>
        /// Ride length of a newly appeared passenger: one stop half of the time, two stops three
        /// times out of ten, and three or more for the rest. Never zero.
        /// </summary>
        internal static int RollRideOffset(Random random)
        {
            var roll = random != null ? random.NextDouble() : 0.5;
            if (roll < OneStopRideShare)
            {
                return 1;
            }

            if (roll < OneStopRideShare + TwoStopRideShare)
            {
                return 2;
            }

            var extraStops = MaxRideStops - 2;
            var extra = random != null && extraStops > 0 ? random.Next(extraStops) : 0;
            return 3 + extra;
        }

        /// <summary>
        /// Ride offsets beyond the end of the line wrap on closed loops (the XML repeats the origin
        /// station as the last one, so the loop has one fewer distinct stop) and clamp on open lines.
        /// </summary>
        internal static int ResolveRiddenStops(int rideOffset, int stationIndex, BusRouteDefinition route, bool isClosedLoop)
        {
            var offset = Math.Max(1, rideOffset);
            if (route == null)
            {
                return offset;
            }

            var distinctStops = isClosedLoop ? Math.Max(1, route.StationCount - 1) : route.StationCount;
            var remaining = Math.Max(1, distinctStops - (stationIndex % distinctStops));
            return Math.Min(offset, Math.Max(1, remaining));
        }

        /// <summary>
        /// Destination of a passenger boarding at <paramref name="boardedIndex"/>, wrapping modulo
        /// the distinct stops on a closed loop and clamping on an open line.
        /// </summary>
        internal static int ResolveDestinationIndex(int boardedIndex, int riddenStops, int stationCount, bool isClosedLoop)
        {
            if (stationCount <= 1)
            {
                return Math.Max(0, stationCount - 1);
            }

            var offset = Math.Max(1, riddenStops);
            if (!isClosedLoop)
            {
                return Math.Min(stationCount - 1, Math.Max(0, boardedIndex) + offset);
            }

            var distinctStops = Math.Max(1, stationCount - 1);
            var start = ((boardedIndex % distinctStops) + distinctStops) % distinctStops;
            return (start + offset) % distinctStops;
        }

        /// <summary>Rolls the number of passengers waiting at one station.</summary>
        internal static int RollWaitingPassengers(Random random)
        {
            var span = Math.Max(1, MaxWaitingPassengers - MinWaitingPassengers + 1);
            var roll = random != null ? random.Next(span) : 0;
            return MinWaitingPassengers + roll;
        }

        /// <summary>
        /// One passenger's fare: a flat base fare plus a per-station rate for the ride, scaled by the
        /// skill and district multipliers and rounded to whole currency units.
        /// </summary>
        internal static float ComputeFare(int stationsRidden, float multiplier)
        {
            var ridden = Math.Max(0, stationsRidden);
            var scale = multiplier > 0f ? multiplier : 1f;
            var gross = BaseFare + FarePerStation * ridden;
            return (float)Math.Round(gross * scale);
        }

        /// <summary>
        /// GTA heading (degrees) from one point to another: 0 = north (+Y), 90 = west (-X),
        /// 180 = south, 270 = east (+X).
        /// </summary>
        internal static float ComputeBearingDegrees(float fromX, float fromY, float toX, float toY)
        {
            var deltaX = toX - fromX;
            var deltaY = toY - fromY;
            if (Math.Abs(deltaX) < 0.0001f && Math.Abs(deltaY) < 0.0001f)
            {
                return 0f;
            }

            var heading = (float)(Math.Atan2(-deltaX, deltaY) * 180.0 / Math.PI);
            return NormalizeHeading(heading);
        }

        /// <summary>Wraps any heading into the [0, 360) range; invalid input becomes 0.</summary>
        internal static float NormalizeHeading(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading))
            {
                return 0f;
            }

            var normalized = heading % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }

            return normalized;
        }

        /// <summary>
        /// Resolves the district of a world point: exact polygon containment first (the ray-cast used
        /// by the territory system), then the nearest polygon centroid, because the shipped district
        /// map has coverage gaps (Downtown/South Los Santos seam) and a self-intersecting Vinewood
        /// quad. When several polygons claim the point the nearest centroid wins.
        /// </summary>
        internal static string ResolveDistrictName(Vector3 position, IReadOnlyList<DistrictConfig> districts)
        {
            return SideJobConfigLoader.ResolveDistrictName(position, districts);
        }

        private static float DistanceSquared(Vector2 left, Vector2 right)
        {
            return SideJobConfigLoader.DistanceSquared(left, right);
        }

        // ---------------------------------------------------------------- config loaders

        internal static List<BusVehicleDefinition> LoadBusVehicles(string configDirectory)
        {
            var result = new List<BusVehicleDefinition>();
            foreach (var element in SideJobConfigLoader.EnumerateJobVehicles(configDirectory, BusJobId))
            {
                var modelName = ReadAttribute(element, "model");
                var definition = new BusVehicleDefinition
                {
                    Name = ReadAttribute(element, "name", modelName),
                    ModelName = modelName,
                    UnlockLevel = Math.Max(0, ReadIntAttribute(element, "unlockLevel", 0)),
                    Seats = Math.Max(1, ReadIntAttribute(element, "seats", 1)),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, ReadFloatAttribute(element, "dailyRent", 0f)),
                    FuelCapacityLiters = Math.Max(0f, ReadFloatAttribute(element, "fuelCapacityLiters", 120f)),
                };

                var rawDoorIndices = ReadAttribute(element, "doorIndices");
                if (!string.IsNullOrWhiteSpace(rawDoorIndices))
                {
                    var parts = rawDoorIndices.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var parsed = -1;
                        int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
                        if (parsed >= 0 && !definition.DoorIndices.Contains(parsed))
                        {
                            definition.DoorIndices.Add(parsed);
                        }
                    }
                }

                result.Add(definition);
            }

            return result;
        }

        /// <summary>
        /// Loads BusRoute.xml. Station order comes from the <c>index</c> attribute; the runtime index
        /// is always the contiguous document position so the route length, the destination indices
        /// and the GPS cursor stay consistent. A station without a <c>heading</c> attribute is faced
        /// towards the next station (the previous station for the last one), which keeps the shipped
        /// heading-less XML loadable.
        /// </summary>
        internal static List<BusRouteDefinition> LoadRoutes(string configDirectory)
        {
            var result = new List<BusRouteDefinition>();
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "SideJobs", "BusRoute.xml"));
            var root = document != null ? document.Root : null;
            if (root == null)
            {
                return result;
            }

            foreach (var routeElement in root.Elements("BusRoute"))
            {
                var routeId = ReadAttribute(routeElement, "id");
                if (string.IsNullOrWhiteSpace(routeId))
                {
                    continue;
                }

                var route = new BusRouteDefinition
                {
                    RouteId = routeId,
                    DisplayName = ReadAttribute(routeElement, "name", routeId),
                    DistrictNames = ReadDistricts(routeElement),
                };

                var stations = routeElement
                    .Elements("Station")
                    .Select(LoadStation)
                    .OrderBy(station => station.Index)
                    .ToList();

                for (int i = 0; i < stations.Count; i++)
                {
                    stations[i].Index = i;
                }

                ApplyDerivedHeadings(stations);
                route.Stations = stations;
                result.Add(route);
            }

            return result;
        }

        /// <summary>
        /// Loads the district polygons from Districts.xml. Kept local so the bus job can resolve the
        /// district of each station without a TerritoryManager dependency.
        /// </summary>
        internal static List<DistrictConfig> LoadDistricts(string configDirectory)
        {
            return SideJobConfigLoader.LoadDistricts(configDirectory);
        }

        private static BusStationDefinition LoadStation(XElement stationElement)
        {
            var station = new BusStationDefinition
            {
                Index = ReadIntAttribute(stationElement, "index", 0),
                Name = ReadAttribute(stationElement, "name"),
                Position = ReadPosition(stationElement),
                Heading = ReadFloatAttribute(stationElement, "heading", 0f),
                HasHeadingOverride = stationElement != null && stationElement.Attribute("heading") != null,
            };

            // Primary shape: real <Ped> / <Shelter> children (and a <Peds> wrapper for convenience).
            LoadStationExportElements(station, stationElement);

            // Legacy shapes: the export line in a comment, in plain text, or as station attributes.
            // Whatever the elements already provided wins, so both shapes can coexist.
            ApplyExportData(station, stationElement);
            return station;
        }

        /// <summary>
        /// Reads the passenger waiting spots and the shelter of a station from its child elements:
        /// <c>&lt;Ped x=".." y=".." z=".." heading=".." /&gt;</c> and
        /// <c>&lt;Shelter model=".." x=".." y=".." z=".." heading=".." /&gt;</c>.
        /// </summary>
        private static void LoadStationExportElements(BusStationDefinition station, XElement stationElement)
        {
            if (station == null || stationElement == null)
            {
                return;
            }

            foreach (var child in stationElement.Elements())
            {
                var name = child.Name.LocalName;

                if (IsPedElementName(name))
                {
                    AddPedSpawnPoint(station, child);
                    continue;
                }

                if (string.Equals(name, "Peds", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Passengers", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ped in child.Elements())
                    {
                        if (IsPedElementName(ped.Name.LocalName))
                        {
                            AddPedSpawnPoint(station, ped);
                        }
                    }

                    continue;
                }

                if (string.Equals(name, "Shelter", StringComparison.OrdinalIgnoreCase) && station.ShelterModel < 0)
                {
                    station.ShelterModel = ReadIntAttribute(child, "model", -1);
                    station.ShelterPosition = ReadPosition(child);
                    station.ShelterHeading = NormalizeHeading(ReadHeading(child));
                }
            }
        }

        private static bool IsPedElementName(string localName)
        {
            return string.Equals(localName, "Ped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localName, "Passenger", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localName, "Stand", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPedSpawnPoint(BusStationDefinition station, XElement pedElement)
        {
            if (station == null || pedElement == null)
            {
                return;
            }

            station.PedSpawnPoints.Add(new BusPedSpawnPoint
            {
                Position = ReadPosition(pedElement),
                Heading = NormalizeHeading(ReadHeading(pedElement)),
            });
        }

        /// <summary>Heading of an element, accepting both <c>heading</c> and the short <c>h</c>.</summary>
        private static float ReadHeading(XElement element)
        {
            if (element == null)
            {
                return 0f;
            }

            if (element.Attribute("heading") != null)
            {
                return ReadFloatAttribute(element, "heading", 0f);
            }

            return ReadFloatAttribute(element, "h", 0f);
        }

        /// <summary>
        /// Reads the station's export data. Three shapes are accepted and all end up in the same
        /// parsers:
        ///   1. the shipped one, a comment right after the station:
        ///      <c>export id=10 | peds(6): (x,y,z,h) (x,y,z,h) | shelter: model=... at (x,y,z) h...</c>
        ///   2. the same line as plain text after the station (what removing the comment markers
        ///      produces) or inside a following &lt;Peds&gt;/&lt;Passengers&gt;/&lt;Export&gt; element;
        ///   3. inline attributes on the station: <c>exportId="10" peds="(x,y,z,h) (x,y,z,h)"</c> and
        ///      <c>shelter="model=... at (x,y,z) h..."</c>.
        /// A station with none of them keeps empty lists and the legacy fallback placement is used.
        /// </summary>
        internal static void ApplyExportData(BusStationDefinition station, XElement stationElement)
        {
            if (station == null)
            {
                return;
            }

            // Shape 3: inline attributes win, because they are the most explicit declaration.
            var inlineExportId = ReadAttribute(stationElement, "exportId");
            int parsedInlineId;
            if (!string.IsNullOrWhiteSpace(inlineExportId) && int.TryParse(inlineExportId, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInlineId))
            {
                station.ExportId = parsedInlineId;
            }

            ApplyExportText(station, ReadAttribute(stationElement, "peds"));
            ApplyExportText(station, ReadAttribute(stationElement, "shelter"));

            // Shapes 1 and 2.
            ApplyExportText(station, ReadExportText(stationElement));
        }

        private static void ApplyExportText(BusStationDefinition station, string text)
        {
            if (station == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int exportId;
            if (TryParseExportId(text, out exportId) && station.ExportId < 0)
            {
                station.ExportId = exportId;
            }

            var points = ParsePedSpawnPoints(text);
            if (station.PedSpawnPoints.Count == 0)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    station.PedSpawnPoints.Add(points[i]);
                }
            }

            int shelterModel;
            Vector3 shelterPosition;
            float shelterHeading;
            if (station.ShelterModel < 0 && TryParseShelter(text, out shelterModel, out shelterPosition, out shelterHeading))
            {
                station.ShelterModel = shelterModel;
                station.ShelterPosition = shelterPosition;
                station.ShelterHeading = shelterHeading;
            }
        }

        /// <summary>
        /// Export data that documents a station: the sibling comment right after the station element
        /// (whitespace in between is expected) or a non-whitespace text node in the same place, which
        /// is what uncommenting the shipped comment leaves behind. Anything else means the station
        /// carries no exported data next to it.
        /// </summary>
        internal static string ReadExportText(XElement stationElement)
        {
            if (stationElement == null)
            {
                return string.Empty;
            }

            var node = stationElement.NextNode;
            while (node != null)
            {
                var comment = node as XComment;
                if (comment != null)
                {
                    return comment.Value ?? string.Empty;
                }

                var text = node as XText;
                if (text != null)
                {
                    if (!string.IsNullOrWhiteSpace(text.Value))
                    {
                        return text.Value;
                    }

                    // Indentation between the station and its export line.
                    node = node.NextNode;
                    continue;
                }

                // A following element: accept it only when it is clearly the export wrapper.
                var element = node as XElement;
                if (element != null && IsExportElementName(element.Name.LocalName))
                {
                    return element.Value ?? string.Empty;
                }

                return string.Empty;
            }

            return string.Empty;
        }

        private static bool IsExportElementName(string localName)
        {
            return string.Equals(localName, "Peds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localName, "Passengers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localName, "Export", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parses the "export id=n" beacon of a station comment.</summary>
        internal static bool TryParseExportId(string commentText, out int exportId)
        {
            exportId = -1;
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return false;
            }

            var match = ExportIdPattern.Match(commentText);
            if (!match.Success)
            {
                return false;
            }

            return int.TryParse(
                match.Groups["id"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out exportId);
        }

        /// <summary>
        /// Parses the passenger waiting spots out of exported text: either the "peds(n): (x,y,z,h) ..."
        /// list of a full export line, or a bare "(x,y,z,h) (x,y,z,h)" tuple list such as the value of
        /// a <c>peds="..."</c> attribute. A tuple only matches when it carries the heading, so a
        /// shelter coordinate or any other parenthesised number group is ignored.
        /// </summary>
        internal static List<BusPedSpawnPoint> ParsePedSpawnPoints(string commentText)
        {
            var result = new List<BusPedSpawnPoint>();
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return result;
            }

            var section = PedSectionPattern.Match(commentText);
            var pedText = section.Success ? section.Groups["peds"].Value : commentText;

            foreach (Match match in PedSpawnPointPattern.Matches(pedText))
            {
                float x;
                float y;
                float z;
                float heading;
                if (!TryParseFloat(match.Groups["x"].Value, out x)
                    || !TryParseFloat(match.Groups["y"].Value, out y)
                    || !TryParseFloat(match.Groups["z"].Value, out z)
                    || !TryParseFloat(match.Groups["h"].Value, out heading))
                {
                    continue;
                }

                result.Add(new BusPedSpawnPoint
                {
                    Position = new Vector3(x, y, z),
                    Heading = NormalizeHeading(heading),
                });
            }

            return result;
        }

        /// <summary>Parses the optional "shelter: model=n at (x,y,z) h=n" part of a station comment.</summary>
        internal static bool TryParseShelter(string commentText, out int model, out Vector3 position, out float heading)
        {
            model = -1;
            position = Vector3.Zero;
            heading = 0f;
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return false;
            }

            var match = ShelterPattern.Match(commentText);
            if (!match.Success)
            {
                return false;
            }

            int parsedModel;
            if (!int.TryParse(match.Groups["model"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedModel))
            {
                return false;
            }

            float x;
            float y;
            float z;
            float parsedHeading;
            if (!TryParseFloat(match.Groups["x"].Value, out x)
                || !TryParseFloat(match.Groups["y"].Value, out y)
                || !TryParseFloat(match.Groups["z"].Value, out z)
                || !TryParseFloat(match.Groups["h"].Value, out parsedHeading))
            {
                return false;
            }

            model = parsedModel;
            position = new Vector3(x, y, z);
            heading = NormalizeHeading(parsedHeading);
            return true;
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Fills in the heading of every station that has no explicit <c>heading</c> attribute: the
        /// bus is faced towards the next station, and the last station keeps the direction the route
        /// was travelling in.
        /// </summary>
        private static void ApplyDerivedHeadings(List<BusStationDefinition> stations)
        {
            if (stations == null)
            {
                return;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                if (station == null || station.HasHeadingOverride)
                {
                    continue;
                }

                if (i + 1 < stations.Count)
                {
                    var next = stations[i + 1];
                    station.Heading = next != null
                        ? ComputeBearingDegrees(station.Position.X, station.Position.Y, next.Position.X, next.Position.Y)
                        : 0f;
                    continue;
                }

                var previous = i > 0 ? stations[i - 1] : null;
                station.Heading = previous != null
                    ? ComputeBearingDegrees(previous.Position.X, previous.Position.Y, station.Position.X, station.Position.Y)
                    : 0f;
            }
        }

        /// <summary>
        /// Reads the districts="a,b,c" beacon. Entries are trimmed, de-duplicated and normalized to
        /// the canonical district spelling (no spaces), so a hand-edited list still matches
        /// Districts.xml. A missing attribute yields an empty list and keeps the route loadable.
        /// </summary>
        internal static List<string> ReadDistricts(XElement element)
        {
            var result = new List<string>();
            var raw = ReadAttribute(element, "districts");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var trimmed = NormalizeDistrictName(parts[i]);
                if (trimmed.Length == 0 || result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(trimmed);
            }

            return result;
        }

        private static string NormalizeDistrictName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                var character = raw[i];
                if (!char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        internal static Vector3 ReadPosition(XElement element)
        {
            return SideJobConfigLoader.ReadPosition(element);
        }

        /// <summary>
        /// Whether the waiting crowd may be materialised, from <c>&lt;Controls busPedLayer="true" /&gt;</c>
        /// of Core.xml. Absent or unparseable means on: the crowd is part of the job, so it takes an
        /// explicit <c>false</c> to switch it off.
        /// </summary>
        internal static bool LoadPedLayerEnabled(string configDirectory)
        {
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "Core.xml"));
            var controls = document != null && document.Root != null ? document.Root.Element("Controls") : null;
            var raw = ReadAttribute(controls, "busPedLayer");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            bool enabled;
            return !bool.TryParse(raw.Trim(), out enabled) || enabled;
        }

        private static BusDepotPoint LoadDepot(string configDirectory)
        {
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "SideJobs", "JobCoordinates.xml"));
            if (document == null || document.Root == null)
            {
                return null;
            }

            foreach (var element in document.Root.Elements("JobPoint"))
            {
                if (!string.Equals(ReadAttribute(element, "job"), BusJobId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ReadAttribute(element, "function"), BusDepotFunctionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (element.Attribute("x") == null && element.Attribute("y") == null && element.Attribute("z") == null)
                {
                    continue;
                }

                return new BusDepotPoint
                {
                    Name = ReadAttribute(element, "name", "Bus Depot"),
                    Position = new Vector3(
                        ReadFloatAttribute(element, "x", 0f),
                        ReadFloatAttribute(element, "y", 0f),
                        ReadFloatAttribute(element, "z", 0f)),
                    Heading = ReadFloatAttribute(element, "heading", 0f),
                    StationStopDistance = ReadFloatAttribute(element, "stationStopDistance", 0f),
                };
            }

            return null;
        }

        internal static XDocument TryLoadDocument(string filePath)
        {
            return SideJobConfigLoader.TryLoadDocument(filePath);
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
        /// Resolves a blip sprite by name so a runtime that lacks a newer enum member degrades
        /// instead of guessing a raw numeric value.
        /// </summary>
        private static BlipSprite ResolveBlipSprite(string[] candidates, BlipSprite fallback)
        {
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
                // Fall through to the fallback sprite.
            }

            return fallback;
        }

        private static BlipSprite ResolveDepotBlipSprite()
        {
            return ResolveBlipSprite(new[] { "Bus", "Truck" }, BlipSprite.Truck);
        }

        private static BlipSprite ResolveStationBlipSprite()
        {
            return ResolveBlipSprite(new[] { "BusStop", "OnMission" }, BlipSprite.OnMission);
        }

        private sealed class BusDepotPoint
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public float Heading { get; set; }

            /// <summary>Optional stationStopDistance="..." override; 0 keeps the default radius.</summary>
            public float StationStopDistance { get; set; }
        }

        private sealed class BusStationRuntime
        {
            public int Index { get; set; }
            public BusStationDefinition Definition { get; set; }
            public string BlipName { get; set; }

            /// <summary>District of the station point, used to scale the fares paid there.</summary>
            public string DistrictName { get; set; }

            public List<BusWaitingPassengerRuntime> Waiting { get; } = new List<BusWaitingPassengerRuntime>();

            /// <summary>Cooldown before this stop lets a new crowd appear after the bus left.</summary>
            public int NextRefillGameTime { get; set; }

            public bool Serviced { get; set; }
        }

        private sealed class BusWaitingPassengerRuntime
        {
            /// <summary>Ride length rolled when the passenger appears, never zero.</summary>
            public int RiddenStops { get; set; }

            public Ped Ped { get; set; }

            public int SpawnedAtGameTime { get; set; }

            public int BoardingDeadlineGameTime { get; set; }

            /// <summary>Enter-task attempts spent by this passenger (see <see cref="BoardMaxAttempts"/>).</summary>
            public int BoardAttempts { get; set; }

            public int BoardingSeatIndex { get; set; } = -1;
        }

        private sealed class BusAmbientPedRuntime
        {
            public Ped Ped { get; set; }

            /// <summary>When the ped stops waiting and wanders off (0 = already wandering).</summary>
            public int WanderAtGameTime { get; set; }

            public int DespawnAtGameTime { get; set; }

            public bool Released { get; set; }
        }

        private sealed class BusRiderRuntime
        {
            public int RiderId { get; set; }
            public int BoardedStationIndex { get; set; }
            public int DestinationIndex { get; set; }
            public int RiddenStops { get; set; }
            public int SeatIndex { get; set; }
            public Ped Ped { get; set; }
            public int StateDeadlineGameTime { get; set; }

            /// <summary>Leave-task attempts spent by this rider (see <see cref="AlightMaxAttempts"/>).</summary>
            public int AlightAttempts { get; set; }
        }
    }
}

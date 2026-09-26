using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Config;
using LSOL.UI;

namespace LSOL.Systems
{
    /// <summary>Lifecycle of the food delivery run the player is currently working.</summary>
    public enum FoodDeliveryRunPhase
    {
        /// <summary>No run: the player is not carrying meals.</summary>
        Idle = 0,

        /// <summary>Meals loaded, driving to the current customer.</summary>
        EnRoute = 1,

        /// <summary>Stopped at the customer, dropping the meal off.</summary>
        Arrived = 2,

        /// <summary>Drop-off settled; the next customer is rolled after a short beat.</summary>
        Paid = 3,
    }

    /// <summary>
    /// Restaurant information the script layer resolves from the industry catalog. The side job never
    /// holds an IndustryManager reference: everything it needs about a restaurant arrives through this
    /// object plus the two ingredient delegates.
    /// </summary>
    public sealed class FoodDeliveryRestaurantInfo
    {
        public FoodDeliveryRestaurantInfo()
        {
            SiteKey = string.Empty;
            Name = string.Empty;
            District = string.Empty;
            ProductCommodity = string.Empty;
            ProductBasePrice = 0f;
            PositionResolved = false;
        }

        /// <summary>The Sites.xml legacyKey of the restaurant.</summary>
        public string SiteKey { get; set; }

        public string Name { get; set; }

        /// <summary>Canonical district of the restaurant (the district a completed run credits).</summary>
        public string District { get; set; }

        /// <summary>The single finished good the restaurant produces: Pizza or Burger.</summary>
        public string ProductCommodity { get; set; }

        /// <summary>Live marker position of the site, when the catalog could resolve it.</summary>
        public Vector3 Position { get; set; }

        public float Heading { get; set; }

        /// <summary>Base price of <see cref="ProductCommodity"/>, used by the fare formula.</summary>
        public float ProductBasePrice { get; set; }

        /// <summary>True when the player owns the restaurant for gameplay (ingredients are free).</summary>
        public bool IsOwned { get; set; }

        public bool PositionResolved { get; set; }

        /// <summary>The site's VehicleSpawn: where a delivery vehicle is taken out.</summary>
        public Vector3 SpawnPosition { get; set; }

        public float SpawnHeading { get; set; }

        /// <summary>False when the site defines no VehicleSpawn and the marker is used instead.</summary>
        public bool SpawnResolved { get; set; }
    }

    /// <summary>Persisted state of the food delivery side job. Legacy saves deserialize to null.</summary>
    public sealed class FoodDeliveryPersistenceSnapshot
    {
        public FoodDeliveryPersistenceSnapshot()
        {
            OwnedVehicleModels = new List<string>();
            Orders = new List<FoodDeliveryOrderSnapshot>();
        }

        public string ActiveVehicleModelName { get; set; }

        public List<string> OwnedVehicleModels { get; }

        /// <summary>The restaurant the run was started from (Sites.xml legacyKey).</summary>
        public string ActiveRestaurantKey { get; set; }

        /// <summary>The restaurant's finished good (Pizza or Burger).</summary>
        public string ProductCommodity { get; set; }

        public int LoadedMeals { get; set; }

        public int MealsDelivered { get; set; }

        public int RunsCompleted { get; set; }

        public int RunsAbandoned { get; set; }

        public float RouteCashEarned { get; set; }

        public float RouteXpEarned { get; set; }

        /// <summary>Fares earned by the current run, used by the perfect-run tip.</summary>
        public float RunFareEarned { get; set; }

        /// <summary>Game time the current run's meals spoil; 0 when nothing is loaded.</summary>
        public int SpoiledAtGameTime { get; set; }

        public int ActiveOrderId { get; set; }

        public int NextOrderId { get; set; } = 1;

        /// <summary>The order being worked at save time, if any (never more than one).</summary>
        public List<FoodDeliveryOrderSnapshot> Orders { get; }

        /// <summary>The district route the current run follows (empty when none is chosen yet).</summary>
        public string RouteDistrict { get; set; }

        /// <summary>Seed of the run's stop shuffle; 0 means the authored order was kept.</summary>
        public int RouteSeed { get; set; }

        /// <summary>How many stops of the route order this run already consumed.</summary>
        public int RouteStopsDelivered { get; set; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ActiveVehicleModelName)
                    || (OwnedVehicleModels != null && OwnedVehicleModels.Count > 0)
                    || !string.IsNullOrWhiteSpace(ActiveRestaurantKey)
                    || LoadedMeals > 0
                    || MealsDelivered > 0
                    || RunsCompleted > 0
                    || RunsAbandoned > 0
                    || RouteCashEarned > 0.01f
                    || (Orders != null && Orders.Count > 0);
            }
        }
    }

    /// <summary>One in-progress customer order. Never carries entities.</summary>
    public sealed class FoodDeliveryOrderSnapshot
    {
        public int OrderId { get; set; }

        public string CustomerName { get; set; }

        public string CustomerDistrict { get; set; }

        public Vector3 CustomerPosition { get; set; }

        public int DistanceBand { get; set; }

        public float DistanceMeters { get; set; }

        /// <summary>What this order paid when it was delivered.</summary>
        public float CashEarned { get; set; }
    }

    /// <summary>A JobVehicles.xml entry of type="FoodDelivery".</summary>
    public sealed class FoodDeliveryVehicleDefinition
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ModelName { get; set; }

        public int UnlockLevel { get; set; }

        /// <summary>Max meals per run (the mealCapacity attribute). The only authority on the cap.</summary>
        public int MealCapacity { get; set; }

        public float Price { get; set; }

        public float DailyRent { get; set; }

        public float FuelCapacityLiters { get; set; }
    }

    /// <summary>A JobPoint of job="FoodDelivery" function="CustomerDropoff": one address of the shared pool.</summary>
    public sealed class FoodDeliveryCustomerDefinition
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>Canonical district from the district="..." attribute (empty when absent).</summary>
        public string DistrictName { get; set; }
    }

    /// <summary>
    /// One selectable delivery route: every authored CustomerDropoff address of a single district, in the
    /// order they appear in JobCoordinates.xml. Routes are derived from that file, so adding addresses - or
    /// a whole new district - needs no code change and no route is ever capped or filtered.
    /// </summary>
    public sealed class FoodDeliveryRoute
    {
        public FoodDeliveryRoute(string districtName)
        {
            DistrictName = districtName ?? string.Empty;
            DisplayName = string.Empty;
            Stops = new List<FoodDeliveryCustomerDefinition>();
        }

        /// <summary>Canonical district key (a Districts.xml name); empty for the unassigned group.</summary>
        public string DistrictName { get; set; }

        /// <summary>Player-facing district name.</summary>
        public string DisplayName { get; set; }

        /// <summary>The district's drop-off addresses, in authored order.</summary>
        public List<FoodDeliveryCustomerDefinition> Stops { get; }

        public int StopCount
        {
            get { return Stops.Count; }
        }

        /// <summary>Distance from the restaurant being worked to the closest address of the route.</summary>
        public float NearestDistanceMeters { get; set; }
    }

    /// <summary>One customer drop-off the player is driving to.</summary>
    public sealed class FoodDeliveryOrder
    {
        public int OrderId { get; set; }

        public string CustomerName { get; set; }

        public string CustomerDistrict { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>Kept for legacy saves. Always true now: every address is an authored coordinate.</summary>
        public bool AnchorResolved { get; set; }

        /// <summary>
        /// Session bookkeeping: key of the authored stop this order came from, so the address is never
        /// served twice in the same session. Never persisted.
        /// </summary>
        public string StopKey { get; set; }

        /// <summary>0 short, 1 medium, 2 long.</summary>
        public int DistanceBand { get; set; }

        public float DistanceMeters { get; set; }

        public float MealValue { get; set; }

        public float CashEarned { get; set; }
    }

    /// <summary>
    /// Self-contained Food Delivery side job. Restaurants are Sites.xml sites (role="Restaurant") that
    /// turn ProcessedFood/Meat into Pizza or Burger; each RestaurantPickup JobPoint is both the job
    /// garage and the meal loading point.
    ///
    /// A run loads min(vehicle mealCapacity, restaurant stock) meals and drops them one customer at a
    /// time. Meals are drawn from the restaurant's INPUT tank as INGREDIENTS, never from the finished
    /// output, so a run never waits for a production cycle and the input stock seeded by
    /// IndustryManager.SeedIndustryStartingState (startingTankRatio) always feeds the job.
    ///
    /// Money model (deliberately single-sourced): the player earns a delivery fee per meal (finished
    /// good base price share plus distance from the restaurant) plus a completion bonus and a
    /// perfect-run tip. An UNOWNED restaurant additionally charges a wholesale price per meal, so
    /// buying the restaurant is strictly better. The goods revenue itself stays with the industry.
    ///
    /// A completed run pays XP, cash and district bonus. The district credited is the district of the
    /// ROUTE the player chose - the addresses that were actually served - falling back to the
    /// restaurant's district when no route is active. Progression reuses <see cref="PlayerSkillSystem"/>.
    /// </summary>
    internal sealed class FoodDeliverySideJobSystem
    {
        // ---------------------------------------------------------------- tuning

        /// <summary>Share of the finished good's base price paid as the delivery fee for one meal.</summary>
        public const float MealValueFactor = 0.005f;

        public const float PerKilometer = 30f;

        /// <summary>Paid once when every loaded meal has been delivered.</summary>
        public const float RunCompletionBonus = 250f;

        /// <summary>Extra share of the run's fares paid when nothing spoiled.</summary>
        public const float PerfectRunTipShare = 0.15f;

        public const float OrderXp = 15f;
        public const float RunCompletionXp = 150f;

        /// <summary>Ingredient tons drawn per meal (ProcessedFood + Meat = 5 kg of food).</summary>
        public const float IngredientProcessedFoodPerMeal = 0.0035f;

        public const float IngredientMeatPerMeal = 0.0015f;

        /// <summary>Charged per meal when the restaurant is not owned by the player.</summary>
        public const float WholesaleMealCost = 12f;

        /// <summary>Used when the catalog could not supply the product's base price.</summary>
        public const float DefaultProductBasePrice = 1200f;

        /// <summary>
        /// Distance thresholds, in metres, used only to LABEL how far a drop-off is from the restaurant
        /// (telemetry and the saved order). Nothing is generated or filtered by them any more: every stop
        /// is an authored JobCoordinates address.
        /// </summary>
        public const float MediumBandMinMeters = 1200f;
        public const float LongBandMinMeters = 2400f;

        public const float ArriveDistance = 20f;
        public const float StationarySpeedMps = 1.0f;

        public const int PlayerAwayTimeoutMs = 30000;
        public const int OrderStallTimeoutMs = 90000;
        public const int PaidDwellMs = 1500;

        /// <summary>
        /// Real milliseconds before the loaded meals go cold. At the default game time scale this is
        /// roughly a third of an in-game day, so a long detour costs the tip but never the run.
        /// </summary>
        public const int SpoilageMs = 480000;

        public const float TargetMarkerDrawDistance = 250f;

        /// <summary>Height of the floating drop-off arrow above the address, in metres.</summary>
        public const float TargetMarkerHeight = 1.2f;

        /// <summary>How close the player must be before the drop-off customer is spawned.</summary>
        public const float DropoffNpcSpawnDistance = 150f;

        /// <summary>
        /// How far from a restaurant the job looks for the player when a caller gives no restaurant key
        /// (a vehicle take-out with no page context). Routes are never filtered by this.
        /// </summary>
        public const float DefaultServiceRadius = 5000f;

        public const int DefaultMealCapacity = 5;

        private const int VehicleModelRequestTimeoutMs = 1500;

        private const string JobId = DistrictBonusCatalog.FoodDeliveryJobId;
        private const string CustomerFunctionId = "CustomerDropoff";

        private const string KeyInactive = "sidejob.fooddelivery.inactive";
        private const string KeyVehicleUnknown = "sidejob.fooddelivery.vehicleUnknown";
        private const string KeyVehicleLocked = "sidejob.fooddelivery.vehicleLocked";
        private const string KeyRestaurantMissing = "sidejob.fooddelivery.restaurantMissing";
        private const string KeySpawnFailed = "sidejob.fooddelivery.spawnFailed";
        private const string KeyVehicleReady = "sidejob.fooddelivery.vehicleReady";
        private const string KeyNeedVehicle = "sidejob.fooddelivery.needVehicle";
        private const string KeyAlreadyOnRun = "sidejob.fooddelivery.alreadyOnRun";
        private const string KeyNoMeals = "sidejob.fooddelivery.noMeals";
        private const string KeyMealsLoaded = "sidejob.fooddelivery.mealsLoaded";
        private const string KeyRouteStarted = "sidejob.fooddelivery.routeStarted";
        private const string KeyRouteMissing = "sidejob.fooddelivery.routeMissing";
        private const string KeyUnassignedRoute = "sidejob.fooddelivery.routeUnassigned";
        private const string KeyMealsPurchased = "sidejob.fooddelivery.mealsPurchased";
        private const string KeyCannotAfford = "sidejob.fooddelivery.cannotAfford";
        private const string KeyCannotCarry = "sidejob.fooddelivery.cannotCarry";
        private const string KeyEnRoute = "sidejob.fooddelivery.enRoute";
        private const string KeyArrived = "sidejob.fooddelivery.arrived";
        private const string KeyMealDelivered = "sidejob.fooddelivery.mealDelivered";
        private const string KeyRunComplete = "sidejob.fooddelivery.runComplete";
        private const string KeyRunAbandoned = "sidejob.fooddelivery.runAbandoned";
        private const string KeySpoiled = "sidejob.fooddelivery.spoiled";
        private const string KeyVehiclePurchased = "sidejob.fooddelivery.vehiclePurchased";
        private const string KeyAlreadyOwned = "sidejob.fooddelivery.alreadyOwned";
        private const string KeyNotOwned = "sidejob.fooddelivery.notOwned";
        private const string KeyVehicleAlreadyOut = "sidejob.fooddelivery.vehicleOut";
        private const string KeyVehicleStored = "sidejob.fooddelivery.vehicleStored";
        private const string KeyNoVehicleOut = "sidejob.fooddelivery.noVehicleOut";
        private const string KeyStoreBlocked = "sidejob.fooddelivery.storeBlocked";
        private const string KeyCustomer = "sidejob.fooddelivery.customer";
        private const string KeyDistrictBonus = "sidejob.bonus.districtBonus";

        private static readonly BlipSprite TargetBlipSprite =
            ResolveBlipSprite(new[] { "OnMission", "Waypoint" }, BlipSprite.OnMission);

        /// <summary>Radar icon of the delivery vehicle while it is parked somewhere in the world.</summary>
        private static readonly BlipSprite ActiveVehicleBlipSprite = BlipSprite.Truck;

        // ---------------------------------------------------------------- state

        private readonly string _configDirectory;
        private readonly PlayerSkillSystem _skillSystem;
        private readonly Action<string> _showStatus;
        private readonly Action<float> _addProfit;
        private readonly Action _requestAutosave;
        private readonly Func<float> _getCompanyBalance;
        private readonly Action<float, string> _deductProfit;
        private readonly Func<string, string, float> _getDistrictBonusExcludingJob;
        private readonly Func<string, string, float, DistrictBonusAward> _reportDistrictBonus;
        private readonly Func<IReadOnlyList<FoodDeliveryRestaurantInfo>> _getRestaurants;
        private readonly Func<string, string, float> _getRestaurantIngredientStock;
        private readonly Func<string, string, float, float> _drawRestaurantIngredient;
        private readonly Random _random;

        private readonly List<FoodDeliveryVehicleDefinition> _vehicles;
        private readonly List<FoodDeliveryRestaurantInfo> _restaurants;
        private readonly List<FoodDeliveryCustomerDefinition> _customers;
        private readonly List<DistrictConfig> _districts;
        private readonly List<string> _ownedVehicleModels;
        private readonly List<FoodDeliveryCustomerDefinition> _routeStops;
        private readonly HashSet<string> _sessionDeliveredStopKeys;

        private string _routeDistrict = string.Empty;
        private int _routeIndex;
        private int _routeSeed;
        private Ped _dropoffPed;
        private Vehicle _activeJobVehicle;
        private string _activeVehicleModelName = string.Empty;
        private int _activeMealCapacity;
        private FoodDeliveryOrder _activeOrder;
        private FoodDeliveryRestaurantInfo _activeRestaurant;
        private string _activeRestaurantKey = string.Empty;
        private string _productCommodity = string.Empty;
        private Blip _targetBlip;
        private Blip _activeVehicleBlip;
        private FoodDeliveryRunPhase _phase = FoodDeliveryRunPhase.Idle;
        private int _nextOrderId = 1;
        private int _loadedMeals;
        private int _mealsDelivered;
        private int _runsCompleted;
        private int _runsAbandoned;
        private float _routeCashEarned;
        private float _routeXpEarned;
        private float _runFareEarned;
        private int _spoiledAtGameTime;
        private bool _runSpoiled;
        private bool _spoiledHintShown;
        private int _phaseDeadlineGameTime;
        private int _awaySinceGameTime;
        private bool _modMechanicsEnabled = true;
        private bool _jobEnabled = true;
        private bool _arrivedHintShown;

        public FoodDeliverySideJobSystem(
            string configDirectory,
            PlayerSkillSystem skillSystem,
            Action<string> showStatus,
            Action<float> addProfit,
            Action requestAutosave,
            Func<float> getCompanyBalance = null,
            Action<float, string> deductProfit = null,
            Func<string, string, float> getDistrictBonusExcludingJob = null,
            Func<string, string, float, DistrictBonusAward> reportDistrictBonus = null,
            Func<IReadOnlyList<FoodDeliveryRestaurantInfo>> getRestaurants = null,
            Func<string, string, float> getRestaurantIngredientStock = null,
            Func<string, string, float, float> drawRestaurantIngredient = null,
            Random random = null)
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
            _getRestaurants = getRestaurants;
            _getRestaurantIngredientStock = getRestaurantIngredientStock;
            _drawRestaurantIngredient = drawRestaurantIngredient;
            // Injectable so tests can pin the roll sequence.
            _random = random ?? new Random();

            _vehicles = LoadVehicles(_configDirectory);
            _restaurants = new List<FoodDeliveryRestaurantInfo>();
            _customers = LoadCustomers(_configDirectory);
            _districts = SideJobConfigLoader.LoadDistricts(_configDirectory);

            _ownedVehicleModels = new List<string>();
            _routeStops = new List<FoodDeliveryCustomerDefinition>();
            _sessionDeliveredStopKeys = new HashSet<string>(StringComparer.Ordinal);

            ResolveCustomerDistricts();
            RefreshRestaurants();
        }

        /// <summary>
        /// Re-reads the restaurant list from the industry catalog. Restaurants are ordinary Sites.xml
        /// industries (role="Restaurant"), so the site is the only place a restaurant is defined and
        /// adding one there is enough. Call this before rendering the delivery pages so ownership and
        /// the product are current.
        /// </summary>
        public void RefreshRestaurants()
        {
            _restaurants.Clear();

            if (_getRestaurants == null)
            {
                return;
            }

            try
            {
                var live = _getRestaurants();
                if (live == null)
                {
                    return;
                }

                for (int i = 0; i < live.Count; i++)
                {
                    var restaurant = live[i];
                    if (restaurant != null && !string.IsNullOrWhiteSpace(restaurant.SiteKey))
                    {
                        _restaurants.Add(restaurant);
                    }
                }
            }
            catch
            {
                // A failed catalog read must never break the tick.
            }
        }

        // ---------------------------------------------------------------- read-only surface

        public bool HasVehicleOut
        {
            get { return _activeJobVehicle != null && _activeJobVehicle.Exists(); }
        }

        public bool HasActiveRun
        {
            get { return _loadedMeals > 0 || _activeOrder != null; }
        }

        public FoodDeliveryRunPhase Phase
        {
            get { return _phase; }
        }

        public int LoadedMeals
        {
            get { return _loadedMeals; }
        }

        public int MealsDelivered
        {
            get { return _mealsDelivered; }
        }

        public FoodDeliveryOrder ActiveOrder
        {
            get { return _activeOrder; }
        }

        public string ActiveRestaurantKey
        {
            get { return _activeRestaurantKey; }
        }

        public string ProductCommodity
        {
            get { return _productCommodity; }
        }

        public IReadOnlyList<FoodDeliveryRestaurantInfo> GetRestaurants()
        {
            return _restaurants;
        }

        public IReadOnlyList<FoodDeliveryCustomerDefinition> GetCustomers()
        {
            return _customers;
        }

        /// <summary>
        /// Every delivery route of the job: one per district that has authored CustomerDropoff addresses,
        /// in file order and with every address kept. Pass a restaurant key to fill in how far each route
        /// starts from that restaurant, which is what the tablet picker shows.
        /// </summary>
        public IReadOnlyList<FoodDeliveryRoute> GetRoutes(string restaurantKey = null)
        {
            var routes = BuildRoutes(_customers);
            var definition = FindRestaurant(restaurantKey);
            var info = definition != null ? ResolveRestaurant(definition) : null;
            var origin = info != null ? info.Position : Vector3.Zero;

            for (int i = 0; i < routes.Count; i++)
            {
                routes[i].NearestDistanceMeters = ComputeNearestDistance(routes[i], origin);
            }

            return routes;
        }

        /// <summary>The district route the current run follows; empty when the player has not chosen one.</summary>
        public string RouteDistrict
        {
            get { return _routeDistrict; }
        }

        /// <summary>Stops queued for the current run (0 when no route is active).</summary>
        public int RouteStopCount
        {
            get { return _routeStops.Count; }
        }

        /// <summary>The addresses queued for the current run, in the order they will be served.</summary>
        internal IReadOnlyList<FoodDeliveryCustomerDefinition> QueuedStops
        {
            get { return _routeStops; }
        }

        /// <summary>Stops of the active route order this run already consumed.</summary>
        public int RouteStopsDelivered
        {
            get { return _routeIndex; }
        }

        /// <summary>The first restaurant of the catalog: the menu entries' default hub.</summary>
        public FoodDeliveryRestaurantInfo GetGarageRestaurant()
        {
            return _restaurants.Count > 0 ? _restaurants[0] : null;
        }

        public IReadOnlyList<FoodDeliveryVehicleDefinition> GetVehicles()
        {
            return _vehicles;
        }

        public IReadOnlyList<FoodDeliveryVehicleDefinition> GetOwnedVehicles()
        {
            var result = new List<FoodDeliveryVehicleDefinition>();
            for (int i = 0; i < _vehicles.Count; i++)
            {
                var definition = _vehicles[i];
                if (definition != null && OwnsVehicle(definition.ModelName))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        public bool OwnsVehicle(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            for (int i = 0; i < _ownedVehicleModels.Count; i++)
            {
                if (string.Equals(_ownedVehicleModels[i], modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsActiveVehicleOut(string modelName)
        {
            return HasVehicleOut
                && !string.IsNullOrWhiteSpace(modelName)
                && string.Equals(_activeVehicleModelName, modelName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Display name of the vehicle currently taken out (empty when none is).</summary>
        public string ActiveVehicleDisplayName
        {
            get
            {
                if (!HasVehicleOut)
                {
                    return string.Empty;
                }

                var definition = FindVehicle(_activeVehicleModelName);
                return definition != null ? definition.Name : _activeVehicleModelName;
            }
        }

        /// <summary>Meals the active vehicle can carry (0 when no vehicle is out).</summary>
        public int ActiveMealCapacity
        {
            get { return HasVehicleOut ? _activeMealCapacity : 0; }
        }

        // ---------------------------------------------------------------- garage

        /// <summary>
        /// Buys a delivery vehicle for the job. Bought vehicles are parked in the restaurant garage and
        /// are what the player takes out to work runs.
        /// </summary>
        public bool TryBuyVehicle(string modelName, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The food delivery side job is inactive.");
                return false;
            }

            var definition = FindVehicle(modelName);
            if (definition == null)
            {
                message = Text(KeyVehicleUnknown, "Unknown delivery vehicle.");
                return false;
            }

            if (OwnsVehicle(definition.ModelName))
            {
                message = Text(KeyAlreadyOwned, "That vehicle is already parked in the garage.");
                return false;
            }

            var level = _skillSystem != null ? _skillSystem.GetLevel(PlayerSkillId.FoodDelivery) : 0;
            if (definition.UnlockLevel > level)
            {
                message = LocalizedText.FormatOrDefault(
                    KeyVehicleLocked,
                    "Requires Food Delivery level {0}.",
                    definition.UnlockLevel);
                return false;
            }

            var price = Math.Max(0f, definition.Price);
            if (price > 0f)
            {
                var balance = _getCompanyBalance != null ? _getCompanyBalance() : 0f;
                if (balance < price)
                {
                    message = LocalizedText.FormatOrDefault(
                        KeyCannotAfford,
                        "Not enough money: {0} needed.",
                        ModFormatting.FormatMoney(price));
                    return false;
                }
            }

            if (price > 0f && _deductProfit != null)
            {
                _deductProfit(price, string.Format(CultureInfo.InvariantCulture, "Delivery vehicle: {0}", definition.Name));
            }

            _ownedVehicleModels.Add(definition.ModelName);
            message = LocalizedText.FormatOrDefault(
                KeyVehiclePurchased,
                "{0} bought and parked in the garage.",
                definition.Name);

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>
        /// Takes an owned vehicle out of the job garage. The vehicle always appears on the delivery pad
        /// of the restaurant the caller is working from - or, when no key is given, of the restaurant
        /// the player is standing at - never on a fixed site.
        /// </summary>
        public bool TrySpawnVehicle(string modelName, Ped player, out string message, string restaurantKey = null)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The food delivery side job is inactive.");
                return false;
            }

            if (HasVehicleOut)
            {
                message = Text(KeyVehicleAlreadyOut, "A delivery vehicle is already out. Park it first.");
                return false;
            }

            var definition = FindVehicle(modelName);
            if (definition == null)
            {
                message = Text(KeyVehicleUnknown, "Unknown delivery vehicle.");
                return false;
            }

            if (!OwnsVehicle(definition.ModelName))
            {
                message = Text(KeyNotOwned, "Buy that vehicle before taking it out.");
                return false;
            }

            // Spawn at the restaurant the player is working from, not at a fixed site.
            var info = ResolveVehicleSpawnRestaurant(restaurantKey, player);
            if (info == null)
            {
                message = Text(KeyRestaurantMissing, "No restaurant site found. Add one with role=\"Restaurant\" in LSOL_Config/Sites.xml.");
                return false;
            }

            // Prefer the site's VehicleSpawn pad so the vehicle does not appear inside the restaurant.
            var spawnPosition = info.SpawnResolved ? info.SpawnPosition : info.Position;
            var spawnHeading = info.SpawnResolved ? info.SpawnHeading : info.Heading;

            if (spawnPosition == Vector3.Zero)
            {
                message = Text(KeyRestaurantMissing, "No restaurant site found. Add one with role=\"Restaurant\" in LSOL_Config/Sites.xml.");
                return false;
            }

            var vehicle = SpawnVehicle(definition.ModelName, spawnPosition, spawnHeading, true);
            if (vehicle == null)
            {
                message = LocalizedText.FormatOrDefault(KeySpawnFailed, "Could not spawn {0}.", definition.Name);
                return false;
            }

            _activeJobVehicle = vehicle;
            _activeVehicleModelName = definition.ModelName;
            _activeMealCapacity = Math.Max(1, definition.MealCapacity);

            message = LocalizedText.FormatOrDefault(
                KeyVehicleReady,
                "{0} ready at {1}.",
                definition.Name,
                string.IsNullOrWhiteSpace(info.Name) ? info.SiteKey : info.Name);

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>Parks the active vehicle back in the garage. Refuses while a run is loaded.</summary>
        public bool TryStoreVehicle(Ped player, out string message)
        {
            message = string.Empty;

            if (!HasVehicleOut)
            {
                message = Text(KeyNoVehicleOut, "No delivery vehicle is out.");
                return false;
            }

            if (_loadedMeals > 0)
            {
                message = Text(KeyCannotCarry, "Deliver or drop the meals you are carrying first.");
                return false;
            }

            try
            {
                if (player != null && player.Exists() && player.IsInVehicle(_activeJobVehicle))
                {
                    message = Text(KeyStoreBlocked, "Get out of the vehicle before parking it.");
                    return false;
                }
            }
            catch
            {
                // Fall through: a failed seat check must not block parking.
            }

            try
            {
                if (_activeJobVehicle != null && _activeJobVehicle.Exists())
                {
                    _activeJobVehicle.Delete();
                }
            }
            catch
            {
                // Best-effort delete.
            }

            _activeJobVehicle = null;
            _activeVehicleModelName = string.Empty;
            _activeMealCapacity = 0;
            ClearActiveVehicleBlip();
            message = Text(KeyVehicleStored, "Delivery vehicle parked in the garage.");
            _requestAutosave?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- UI actions
        //
        // The tablet pages call these instead of the Try* methods so the UI never has to know about
        // out-parameters, the player ped or the status line. Every one of them reports through the same
        // status channel the world hints use.

        /// <summary>
        /// Takes an owned vehicle out of the garage and reports the result. The restaurant key comes
        /// from the tablet page that asked for it, so the vehicle appears on that restaurant's pad.
        /// </summary>
        public bool TakeOutVehicle(string modelName, string restaurantKey = null)
        {
            string message;
            var result = TrySpawnVehicle(modelName, TryGetPlayerPed(), out message, restaurantKey);
            Report(message);
            return result;
        }

        /// <summary>Parks the active vehicle in the garage and reports the result.</summary>
        public bool ParkVehicle()
        {
            string message;
            var result = TryStoreVehicle(TryGetPlayerPed(), out message);
            Report(message);
            return result;
        }

        /// <summary>Buys a delivery vehicle and reports the result.</summary>
        public bool BuyVehicle(string modelName)
        {
            string message;
            var result = TryBuyVehicle(modelName, out message);
            Report(message);
            return result;
        }

        /// <summary>Loads a run at the restaurant and reports the result. Pass a district to pick the route.</summary>
        public bool StartRun(string restaurantKey, string routeDistrict = null)
        {
            string message;
            var result = TryStartRun(restaurantKey, routeDistrict, out message);
            Report(message);
            return result;
        }

        /// <summary>Re-reads the address pool and vehicle definitions and reports the result.</summary>
        public void ReloadConfiguration()
        {
            string message;
            TryReloadConfiguration(out message);
            Report(message);
        }

        private void Report(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _showStatus?.Invoke(message);
            }
        }

        private static Ped TryGetPlayerPed()
        {
            try
            {
                return Game.Player.Character;
            }
            catch
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>
        /// Loads a run: draws the meals as ingredients from the restaurant's input tank and queues the
        /// route's drop-offs. Refuses when the vehicle is missing, a run is loaded, the chosen route has
        /// no authored address, or the restaurant has no ingredient stock and the player cannot pay
        /// wholesale for it.
        /// </summary>
        public bool TryStartRun(string restaurantKey, out string message)
        {
            return TryStartRun(restaurantKey, null, out message);
        }

        /// <summary>
        /// Loads a run on the given district route. Every stop is an authored JobCoordinates address used
        /// verbatim; when the vehicle cannot carry the whole district the stop order is shuffled, so a
        /// small vehicle works a different slice of the route each run.
        /// </summary>
        public bool TryStartRun(string restaurantKey, string routeDistrict, out string message)
        {
            message = string.Empty;

            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                message = Text(KeyInactive, "The food delivery side job is inactive.");
                return false;
            }

            if (!HasVehicleOut)
            {
                message = Text(KeyNeedVehicle, "Take a delivery vehicle out of the garage before starting a run.");
                return false;
            }

            if (_loadedMeals > 0)
            {
                message = Text(KeyAlreadyOnRun, "Finish the run you are already on before loading more meals.");
                return false;
            }

            var definition = FindRestaurant(restaurantKey);
            if (definition == null)
            {
                message = Text(KeyRestaurantMissing, "No restaurant found in LSOL_Config/SideJobs/JobCoordinates.xml.");
                return false;
            }

            var info = ResolveRestaurant(definition);
            var capacity = Math.Max(1, _activeMealCapacity);

            // Queue the route before anything is charged: the load is capped by the addresses this
            // session has not served yet, so an address is never delivered twice.
            QueueRouteStops(routeDistrict, info);
            if (_routeStops.Count == 0)
            {
                message = Text(
                    KeyRouteMissing,
                    "That delivery route has no drop-off addresses. Add CustomerDropoff points for it in JobCoordinates.xml.");
                return false;
            }

            // How many meals the restaurant can actually supply right now.
            var available = ResolveAvailableMeals(info, capacity);
            if (available <= 0)
            {
                message = Text(KeyNoMeals, "The restaurant has no ingredients left. Deliver ProcessedFood or Meat first.");
                return false;
            }

            var meals = Math.Min(capacity, Math.Min(available, _routeStops.Count));

            // An unowned restaurant charges wholesale per meal: the player is taking an owner's stock.
            var wholesale = 0f;
            if (!info.IsOwned)
            {
                wholesale = meals * Math.Max(0f, WholesaleMealCost);
                if (wholesale > 0f && _deductProfit != null)
                {
                    var balance = _getCompanyBalance != null ? _getCompanyBalance() : 0f;
                    if (balance < wholesale)
                    {
                        message = LocalizedText.FormatOrDefault(
                            KeyCannotAfford,
                            "Not enough money: {0} needed.",
                            ModFormatting.FormatMoney(wholesale));
                        return false;
                    }
                }
            }

            var drawn = DrawIngredients(definition.SiteKey, meals);
            if (drawn <= 0)
            {
                message = Text(KeyNoMeals, "The restaurant has no ingredients left. Deliver ProcessedFood or Meat first.");
                return false;
            }

            meals = Math.Min(meals, drawn);

            if (wholesale > 0f && _deductProfit != null)
            {
                _deductProfit(wholesale, string.Format(CultureInfo.InvariantCulture, "Restaurant meals: {0}", meals));
                message = LocalizedText.FormatOrDefault(
                    KeyMealsPurchased,
                    "{0} meal(s) bought from {1}.",
                    meals,
                    string.IsNullOrWhiteSpace(info.Name) ? definition.Name : info.Name);
            }

            _activeRestaurant = info;
            _activeRestaurantKey = definition.SiteKey ?? string.Empty;
            _productCommodity = info.ProductCommodity ?? string.Empty;
            _loadedMeals = meals;
            _mealsDelivered = 0;
            _runFareEarned = 0f;
            _runSpoiled = false;
            _spoiledHintShown = false;
            _awaySinceGameTime = 0;
            _arrivedHintShown = false;

            var gameTime = GetGameTimeSafe();
            _spoiledAtGameTime = gameTime + SpoilageMs;

            if (!RollNextOrder(out var orderMessage))
            {
                // No usable address: refund the load so the player is not stuck carrying meals.
                _loadedMeals = 0;
                _activeRestaurant = null;
                _activeRestaurantKey = string.Empty;
                _routeStops.Clear();
                _routeDistrict = string.Empty;
                _routeIndex = 0;
                _routeSeed = 0;
                message = string.IsNullOrWhiteSpace(orderMessage) ? Text(KeyNoMeals, "No customer could be found near this restaurant.") : orderMessage;
                return false;
            }

            _phase = FoodDeliveryRunPhase.EnRoute;
            _phaseDeadlineGameTime = gameTime + OrderStallTimeoutMs;

            if (string.IsNullOrWhiteSpace(message))
            {
                message = LocalizedText.FormatOrDefault(
                    KeyRouteStarted,
                    "{0} meal(s) loaded from {1}. Route: {2} ({3} drop-off(s)).",
                    meals,
                    string.IsNullOrWhiteSpace(info.Name) ? definition.Name : info.Name,
                    DescribeRoute(_routeDistrict),
                    _routeStops.Count);
            }

            _requestAutosave?.Invoke();
            return true;
        }

        /// <summary>Drops the run without pay. Keeps the vehicle, the garage and anything earned.</summary>
        public void CancelCurrentJob()
        {
            try
            {
                AbandonRun("sidejob.fooddelivery.cancelReason", "the job was cancelled");
                _arrivedHintShown = false;
            }
            catch
            {
                // Cancelling a job must never throw into the menu handler.
            }
        }

        /// <summary>
        /// Re-reads the FoodDelivery entries of JobCoordinates.xml and JobVehicles.xml so the
        /// restaurants, the customer pool and the meal capacities can be tuned without a rebuild. The
        /// garage, the money and the running job are left untouched. Never throws.
        /// </summary>
        public bool TryReloadConfiguration(out string message)
        {
            message = string.Empty;
            try
            {
                RefreshRestaurants();
                if (_restaurants.Count == 0)
                {
                    message = "No restaurant site found. Add one with role=\"Restaurant\" in LSOL_Config/Sites.xml.";
                    return false;
                }

                var reloadedCustomers = LoadCustomers(_configDirectory);
                if (reloadedCustomers.Count > 0)
                {
                    _customers.Clear();
                    _customers.AddRange(reloadedCustomers);
                    ResolveCustomerDistricts();

                    // The addresses are new objects now: the session bookkeeping cannot follow them.
                    _sessionDeliveredStopKeys.Clear();
                }

                var reloadedVehicles = LoadVehicles(_configDirectory);
                if (reloadedVehicles.Count > 0)
                {
                    _vehicles.Clear();
                    _vehicles.AddRange(reloadedVehicles);
                    if (!string.IsNullOrWhiteSpace(_activeVehicleModelName))
                    {
                        var definition = FindVehicle(_activeVehicleModelName);
                        if (definition != null)
                        {
                            _activeMealCapacity = Math.Max(1, definition.MealCapacity);
                        }
                    }
                }

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Delivery restaurants reloaded ({0} restaurant(s), {1} address(es)).",
                    _restaurants.Count,
                    _customers.Count);
                return true;
            }
            catch
            {
                message = "Restaurant reload failed.";
                return false;
            }
        }

        // ---------------------------------------------------------------- lifecycle

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

        public void ResetState()
        {
            // Silent reset: loading a save must never push a status line about the previous run.
            ResetActiveRunSilently();

            // A loaded save starts a fresh delivery cycle: no address counts as already served.
            _sessionDeliveredStopKeys.Clear();

            ClearTargetBlip();
            ClearActiveVehicleBlip();
            ClearGps();

            _activeJobVehicle = null;
            _activeVehicleModelName = string.Empty;
            _activeMealCapacity = 0;
            _ownedVehicleModels.Clear();
            _nextOrderId = 1;
            _runsCompleted = 0;
            _runsAbandoned = 0;
            _routeCashEarned = 0f;
            _routeXpEarned = 0f;
            _runFareEarned = 0f;
            _spoiledAtGameTime = 0;
            _phase = FoodDeliveryRunPhase.Idle;
            _arrivedHintShown = false;
        }

        public void Update(Ped player, int gameTime)
        {
            if (!_modMechanicsEnabled || !_jobEnabled)
            {
                return;
            }

            if (player != null && player.Exists())
            {
                DrawTargetMarker(player);
            }

            UpdateActiveVehicleBlip(player);
            EnsureDropoffNpc(player);

            UpdateSpoilage(gameTime);

            switch (_phase)
            {
                case FoodDeliveryRunPhase.EnRoute:
                    UpdateEnRoute(player, gameTime);
                    break;
                case FoodDeliveryRunPhase.Arrived:
                    UpdateArrived(gameTime);
                    break;
                case FoodDeliveryRunPhase.Paid:
                    UpdatePaid(gameTime);
                    break;
                default:
                    UpdateIdle(player);
                    break;
            }
        }

        private void UpdateIdle(Ped player)
        {
            if (_loadedMeals > 0)
            {
                // A loaded run with no order should never linger, but recover instead of soft-locking.
                if (!RollNextOrder(out _))
                {
                    if (IsRouteExhausted())
                    {
                        // Every address of the route was served, so the run closes instead of failing.
                        CompleteRun();
                        return;
                    }

                    AbandonRun("sidejob.fooddelivery.noCustomerReason", "no customer could be found");
                    return;
                }

                _phase = FoodDeliveryRunPhase.EnRoute;
                return;
            }

            if (player == null || !player.Exists() || HasVehicleOut)
            {
                return;
            }
        }

        private void UpdateEnRoute(Ped player, int gameTime)
        {
            var order = _activeOrder;
            var vehicle = _activeJobVehicle;
            if (order == null || _loadedMeals <= 0)
            {
                _phase = FoodDeliveryRunPhase.Idle;
                return;
            }

            if (vehicle == null || !vehicle.Exists())
            {
                AbandonRun("sidejob.fooddelivery.vehicleGoneReason", "the delivery vehicle is gone");
                return;
            }

            if (!IsDriverOfActiveVehicle(player))
            {
                RegisterAwayFromVehicle(gameTime);
                return;
            }

            _awaySinceGameTime = 0;

            float speed;
            try
            {
                speed = vehicle.Speed;
            }
            catch
            {
                return;
            }

            if (!IsStoppedWithinRange(vehicle.Position.DistanceTo(order.Position), speed, ArriveDistance))
            {
                // A stalled drop-off (blocked, wrecked, or the player parked somewhere else) gives up
                // instead of pinning the run forever.
                if (gameTime > _phaseDeadlineGameTime)
                {
                    AbandonRun("sidejob.fooddelivery.stalledReason", "the delivery stalled");
                }

                return;
            }

            _phase = FoodDeliveryRunPhase.Arrived;
            _arrivedHintShown = false;
            _phaseDeadlineGameTime = gameTime + OrderStallTimeoutMs;
        }

        private void UpdateArrived(int gameTime)
        {
            var order = _activeOrder;
            if (order == null || _loadedMeals <= 0)
            {
                _phase = FoodDeliveryRunPhase.Idle;
                return;
            }

            if (!_arrivedHintShown)
            {
                _arrivedHintShown = true;
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyArrived,
                    "Arrived at {0}.",
                    order.CustomerName));
            }

            CompleteOrder(gameTime);
        }

        private void UpdatePaid(int gameTime)
        {
            if (gameTime < _phaseDeadlineGameTime)
            {
                return;
            }

            if (_loadedMeals <= 0)
            {
                CompleteRun();
                return;
            }

            if (!RollNextOrder(out _))
            {
                if (IsRouteExhausted())
                {
                    // Every address of the route was served, so the run closes instead of failing.
                    CompleteRun();
                    return;
                }

                AbandonRun("sidejob.fooddelivery.noCustomerReason", "no customer could be found");
                return;
            }

            _phase = FoodDeliveryRunPhase.EnRoute;
            _phaseDeadlineGameTime = gameTime + OrderStallTimeoutMs;
            _arrivedHintShown = false;
        }

        /// <summary>
        /// Settles one drop-off: fare for one meal (product base price share plus distance from the
        /// restaurant), scaled by the skill and the district bonus, then the next customer is rolled.
        /// </summary>
        private void CompleteOrder(int gameTime)
        {
            var order = _activeOrder;
            if (order == null)
            {
                return;
            }

            var districtName = ResolveOrderDistrict(order);
            var multiplier = GetPayoutMultiplier(districtName);
            var basePrice = ResolveProductBasePrice();
            var mealValue = ComputeMealFare(basePrice, order.DistanceMeters / 1000f, 1f, multiplier);
            mealValue = (float)Math.Round(mealValue);

            if (mealValue > 0f)
            {
                _addProfit?.Invoke(mealValue);
                _routeCashEarned += mealValue;
                _runFareEarned += mealValue;
            }

            order.CashEarned = mealValue;
            _mealsDelivered += 1;
            _loadedMeals = Math.Max(0, _loadedMeals - 1);

            if (_skillSystem != null)
            {
                _skillSystem.AddXp(PlayerSkillId.FoodDelivery, OrderXp, PlayerSkillXpSource.Player);
                _routeXpEarned += OrderXp;
            }

            var suffix = ReportDistrictCompletion(districtName, 0f);
            var summary = LocalizedText.FormatOrDefault(
                KeyMealDelivered,
                "Delivered to {0} ({1}){2}",
                order.CustomerName,
                FormatDistance(order.DistanceMeters),
                suffix);

            _showStatus?.Invoke(summary);

            // The customer reacts to the hand-over, then is handed back to the game.
            PlayDropoffThanks();
            ReleaseDropoffNpc();

            // The session must never send a second meal to the same address.
            MarkStopDelivered(order);

            ClearTargetBlip();
            ClearGps();

            _activeOrder = null;
            _awaySinceGameTime = 0;
            _arrivedHintShown = false;

            if (_loadedMeals <= 0)
            {
                CompleteRun();
                return;
            }

            _phase = FoodDeliveryRunPhase.Paid;
            _phaseDeadlineGameTime = gameTime + PaidDwellMs;

            _requestAutosave?.Invoke();
        }

        /// <summary>
        /// Pays the completed run: completion bonus, perfect-run tip, XP and the district bonus pool of
        /// the district the player chose to deliver in.
        /// </summary>
        private void CompleteRun()
        {
            var delivered = _mealsDelivered;
            var capacity = Math.Max(1, _activeMealCapacity);

            // The chosen route decides which district is credited: that is where the meals were served,
            // and it is not necessarily the district the restaurant sits in.
            var districtName = ResolveCreditDistrict(
                _routeDistrict,
                _activeRestaurant != null ? _activeRestaurant.District : string.Empty);

            var bonus = RunCompletionBonus;
            var tip = _runSpoiled ? 0f : (float)Math.Round(_runFareEarned * PerfectRunTipShare);
            var total = bonus + tip;

            if (total > 0f)
            {
                _addProfit?.Invoke(total);
                _routeCashEarned += total;
            }

            _runsCompleted += 1;

            if (_skillSystem != null)
            {
                var xp = RunCompletionXp + (OrderXp * Math.Max(0, delivered));
                _skillSystem.AddXp(PlayerSkillId.FoodDelivery, xp, PlayerSkillXpSource.Player);
                _routeXpEarned += xp;
            }

            // The pool credit is a complete-run unit: a full load is worth 1.0, a partial load scales.
            var units = delivered / (float)capacity;
            var districtSuffix = ReportDistrictCompletion(districtName, units);

            var summary = LocalizedText.FormatOrDefault(
                KeyRunComplete,
                "Run complete: {0} meal(s) delivered, {1}{2}",
                delivered,
                ModFormatting.FormatMoney(total),
                tip > 0f
                    ? string.Format(CultureInfo.InvariantCulture, " incl. tip {0}", ModFormatting.FormatMoney(tip))
                    : string.Empty);

            _showStatus?.Invoke(string.Concat(summary, districtSuffix));

            ResetActiveRunSilently();
            ClearTargetBlip();
            ClearGps();
            _phase = FoodDeliveryRunPhase.Idle;
            _requestAutosave?.Invoke();
        }

        private void AbandonRun(string reasonKey, string reasonFallback)
        {
            if (_loadedMeals <= 0 && _activeOrder == null)
            {
                _phase = FoodDeliveryRunPhase.Idle;
                return;
            }

            var lost = _loadedMeals;
            _runsAbandoned += 1;

            _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                KeyRunAbandoned,
                "Run abandoned ({0}): {1} meal(s) lost.",
                Text(reasonKey, reasonFallback),
                lost));

            ResetActiveRunSilently();
            ClearTargetBlip();
            ClearGps();
            _phase = FoodDeliveryRunPhase.Idle;
        }

        private void ResetActiveRunSilently()
        {
            _activeOrder = null;
            ReleaseDropoffNpc();
            _activeRestaurant = null;
            _activeRestaurantKey = string.Empty;
            _productCommodity = string.Empty;
            _routeStops.Clear();
            _routeDistrict = string.Empty;
            _routeIndex = 0;
            _routeSeed = 0;
            _loadedMeals = 0;
            _mealsDelivered = 0;
            _runFareEarned = 0f;
            _runSpoiled = false;
            _spoiledHintShown = false;
            _spoiledAtGameTime = 0;
            _phaseDeadlineGameTime = 0;
            _awaySinceGameTime = 0;
            _arrivedHintShown = false;
        }

        private void RegisterAwayFromVehicle(int gameTime)
        {
            if (_awaySinceGameTime <= 0)
            {
                _awaySinceGameTime = gameTime;
                return;
            }

            if (HasAbandonedRun(gameTime, _awaySinceGameTime, PlayerAwayTimeoutMs))
            {
                AbandonRun("sidejob.fooddelivery.awayReason", "you left the delivery vehicle");
            }
        }

        private void UpdateSpoilage(int gameTime)
        {
            if (_loadedMeals <= 0 || _spoiledAtGameTime <= 0 || _runSpoiled)
            {
                return;
            }

            if (!IsSpoiled(gameTime, _spoiledAtGameTime))
            {
                return;
            }

            _runSpoiled = true;
            if (!_spoiledHintShown)
            {
                _spoiledHintShown = true;
                _showStatus?.Invoke(Text(
                    KeySpoiled,
                    "The meals went cold: the perfect-run tip is lost, the run can still be finished."));
            }
        }

        // ---------------------------------------------------------------- routes

        /// <summary>
        /// Groups every authored drop-off by district, in file order: one route per district with every
        /// address kept. There is no cap and no filtering, so adding points - or a whole new district - to
        /// JobCoordinates.xml is all that is needed.
        /// </summary>
        internal static List<FoodDeliveryRoute> BuildRoutes(IReadOnlyList<FoodDeliveryCustomerDefinition> customers)
        {
            var routes = new List<FoodDeliveryRoute>();
            if (customers == null)
            {
                return routes;
            }

            for (int i = 0; i < customers.Count; i++)
            {
                var customer = customers[i];
                if (customer == null || customer.Position == Vector3.Zero)
                {
                    continue;
                }

                var districtName = customer.DistrictName ?? string.Empty;
                var route = FindRouteByName(routes, districtName);
                if (route == null)
                {
                    route = new FoodDeliveryRoute(districtName);
                    routes.Add(route);
                }

                route.Stops.Add(customer);
            }

            for (int i = 0; i < routes.Count; i++)
            {
                routes[i].DisplayName = DescribeRoute(routes[i].DistrictName);
            }

            return routes;
        }

        internal static FoodDeliveryRoute FindRouteByName(IReadOnlyList<FoodDeliveryRoute> routes, string districtName)
        {
            if (routes == null)
            {
                return null;
            }

            var key = districtName ?? string.Empty;
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                if (route != null && string.Equals(route.DistrictName ?? string.Empty, key, StringComparison.OrdinalIgnoreCase))
                {
                    return route;
                }
            }

            return null;
        }

        /// <summary>
        /// Key of one authored address. The JobPoint id is used when there is one, otherwise the exact
        /// coordinate, so the same address always maps to the same key.
        /// </summary>
        internal static string GetStopKey(FoodDeliveryCustomerDefinition stop)
        {
            if (stop == null)
            {
                return string.Empty;
            }

            if (stop.Id > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "id:{0}", stop.Id);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "pos:{0:R};{1:R};{2:R}",
                stop.Position.X,
                stop.Position.Y,
                stop.Position.Z);
        }

        /// <summary>
        /// Addresses this run may still serve: every distinct authored stop of the route, minus the ones
        /// the session already delivered. A run never repeats an address, and when the whole route has
        /// been covered the cycle restarts (reported through <paramref name="cycleReset"/>) so the player
        /// can keep working. When the queue still outnumbers the meals it is shuffled, as before.
        /// </summary>
        internal static List<FoodDeliveryCustomerDefinition> SelectRouteQueue(
            IReadOnlyList<FoodDeliveryCustomerDefinition> routeStops,
            ISet<string> deliveredKeys,
            int capacity,
            Random random,
            out int seed,
            out bool cycleReset)
        {
            seed = 0;
            cycleReset = false;

            var distinct = new List<FoodDeliveryCustomerDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (routeStops != null)
            {
                for (int i = 0; i < routeStops.Count; i++)
                {
                    var stop = routeStops[i];
                    if (stop == null || stop.Position == Vector3.Zero)
                    {
                        continue;
                    }

                    // The same address authored twice is still one address.
                    if (seen.Add(GetStopKey(stop)))
                    {
                        distinct.Add(stop);
                    }
                }
            }

            var hasDelivered = deliveredKeys != null && deliveredKeys.Count > 0;
            var remaining = new List<FoodDeliveryCustomerDefinition>();
            if (hasDelivered)
            {
                for (int i = 0; i < distinct.Count; i++)
                {
                    if (!deliveredKeys.Contains(GetStopKey(distinct[i])))
                    {
                        remaining.Add(distinct[i]);
                    }
                }
            }

            // Still-served addresses first; when the session already covered the whole route, a new cycle.
            var queue = hasDelivered && remaining.Count > 0 ? remaining : distinct;
            cycleReset = hasDelivered && remaining.Count == 0 && distinct.Count > 0;

            if (queue.Count > Math.Max(1, capacity))
            {
                seed = (random ?? new Random()).Next(1, int.MaxValue);
                ShuffleStops(queue, new Random(seed));
            }

            return queue;
        }

        /// <summary>The route's stops in authored order, or in the shuffled order of the given seed.</summary>
        internal static List<FoodDeliveryCustomerDefinition> StopsForSeed(FoodDeliveryRoute route, int seed)
        {
            var stops = new List<FoodDeliveryCustomerDefinition>();
            if (route == null)
            {
                return stops;
            }

            stops.AddRange(route.Stops);
            if (seed > 0)
            {
                ShuffleStops(stops, new Random(seed));
            }

            return stops;
        }

        internal static void ShuffleStops(List<FoodDeliveryCustomerDefinition> stops, Random random)
        {
            if (stops == null || stops.Count < 2 || random == null)
            {
                return;
            }

            for (int i = stops.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var swap = stops[i];
                stops[i] = stops[j];
                stops[j] = swap;
            }
        }

        private static string DescribeRoute(string districtName)
        {
            return string.IsNullOrWhiteSpace(districtName)
                ? Text(KeyUnassignedRoute, "Unassigned")
                : ModFormatting.FormatDistrictName(districtName);
        }

        private static float ComputeNearestDistance(FoodDeliveryRoute route, Vector3 origin)
        {
            if (route == null || route.Stops.Count == 0 || origin == Vector3.Zero)
            {
                return -1f;
            }

            var best = float.MaxValue;
            for (int i = 0; i < route.Stops.Count; i++)
            {
                var stop = route.Stops[i];
                if (stop == null || stop.Position == Vector3.Zero)
                {
                    continue;
                }

                var meters = SideJobConfigLoader.DistanceBetween(origin, stop.Position);
                if (meters < best)
                {
                    best = meters;
                }
            }

            return best == float.MaxValue ? -1f : best;
        }

        private FoodDeliveryRoute FindRoute(string districtName)
        {
            return FindRouteByName(BuildRoutes(_customers), districtName);
        }

        /// <summary>
        /// Route used when the caller did not choose one: the route whose closest address is nearest to the
        /// restaurant, so a run started without the picker still delivers where the player is standing.
        /// </summary>
        private FoodDeliveryRoute ResolveDefaultRoute(FoodDeliveryRestaurantInfo info)
        {
            var routes = BuildRoutes(_customers);
            if (routes.Count == 0)
            {
                return null;
            }

            var origin = info != null ? info.Position : Vector3.Zero;
            if (origin == Vector3.Zero)
            {
                return routes[0];
            }

            FoodDeliveryRoute best = routes[0];
            var bestDistance = float.MaxValue;
            for (int i = 0; i < routes.Count; i++)
            {
                var distance = ComputeNearestDistance(routes[i], origin);
                if (distance >= 0f && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = routes[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Queues this run's stops: the route's still-unserved addresses, shuffled when they outnumber the
        /// meals. The session filter is what guarantees an address is never delivered twice. Internal so
        /// the tests can drive a whole delivery session without a game world.
        /// </summary>
        internal void QueueRouteStops(string routeDistrict, FoodDeliveryRestaurantInfo info)
        {
            _routeStops.Clear();
            _routeDistrict = string.Empty;
            _routeSeed = 0;
            _routeIndex = 0;

            var route = FindRoute(routeDistrict) ?? ResolveDefaultRoute(info);
            if (route == null)
            {
                return;
            }

            int seed;
            bool cycleReset;
            var queue = SelectRouteQueue(
                route.Stops,
                _sessionDeliveredStopKeys,
                Math.Max(1, _activeMealCapacity),
                _random,
                out seed,
                out cycleReset);

            if (cycleReset)
            {
                // The whole district has been served: start a new cycle for it.
                for (int i = 0; i < route.Stops.Count; i++)
                {
                    _sessionDeliveredStopKeys.Remove(GetStopKey(route.Stops[i]));
                }
            }

            _routeDistrict = route.DistrictName ?? string.Empty;
            _routeSeed = seed;
            _routeStops.AddRange(queue);
        }

        /// <summary>Marks an address as served for the rest of the session.</summary>
        internal void MarkStopDelivered(FoodDeliveryOrder order)
        {
            var key = order != null ? order.StopKey : null;
            if (string.IsNullOrWhiteSpace(key))
            {
                // A restored order carries no key: match the saved coordinate back to its address.
                key = FindStopKeyByPosition(order != null ? order.Position : Vector3.Zero, 1.5f);
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                _sessionDeliveredStopKeys.Add(key);
            }
        }

        /// <summary>Key of the authored address sitting on (or within <paramref name="tolerance"/> m of) a point.</summary>
        private string FindStopKeyByPosition(Vector3 position, float tolerance)
        {
            if (_customers.Count == 0 || (position == Vector3.Zero && tolerance <= 0f))
            {
                return string.Empty;
            }

            var bestKey = string.Empty;
            var bestDistance = tolerance;

            for (int i = 0; i < _customers.Count; i++)
            {
                var stop = _customers[i];
                if (stop == null || stop.Position == Vector3.Zero)
                {
                    continue;
                }

                var distance = SideJobConfigLoader.DistanceBetween(position, stop.Position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestKey = GetStopKey(stop);
                }
            }

            return bestKey;
        }

        /// <summary>True when every address queued for this run has been served.</summary>
        private bool IsRouteExhausted()
        {
            return _routeStops.Count > 0 && _routeIndex >= _routeStops.Count;
        }

        /// <summary>Rebuilds the run's stop queue after a load, from the persisted route and shuffle seed.</summary>
        private void RestoreRoute(string routeDistrict, int seed, int stopsDelivered)
        {
            _routeStops.Clear();
            _routeDistrict = string.Empty;
            _routeSeed = 0;
            _routeIndex = 0;

            var route = FindRoute(routeDistrict);
            if (route == null)
            {
                return;
            }

            _routeDistrict = route.DistrictName ?? string.Empty;
            _routeSeed = Math.Max(0, seed);
            _routeStops.AddRange(StopsForSeed(route, _routeSeed));

            if (_routeStops.Count > 0)
            {
                _routeIndex = Math.Max(0, stopsDelivered) % _routeStops.Count;
            }
        }

        /// <summary>
        /// The next stop of the active route, or null once every address of this run has been served. The
        /// queue never wraps: an address is served at most once per session.
        /// </summary>
        private FoodDeliveryCustomerDefinition TakeNextRouteStop()
        {
            if (_routeStops.Count == 0 || _routeIndex >= _routeStops.Count)
            {
                return null;
            }

            return _routeStops[_routeIndex++];
        }

        // ---------------------------------------------------------------- orders

        /// <summary>
        /// Rolls the next customer from the active route: the next authored JobCoordinates address, used
        /// exactly as written. Nothing is snapped or generated any more, so the marker, the blip and the
        /// GPS all sit on the coordinate that was authored.
        /// </summary>
        private bool RollNextOrder(out string message)
        {
            message = string.Empty;

            var info = _activeRestaurant;
            var origin = info != null ? info.Position : Vector3.Zero;
            if (origin == Vector3.Zero)
            {
                return false;
            }

            if (_routeStops.Count == 0)
            {
                // No route chosen yet (a save written before routes existed): pick the closest one.
                QueueRouteStops(string.Empty, info);
            }

            var stop = TakeNextRouteStop();
            if (stop == null || stop.Position == Vector3.Zero)
            {
                message = Text(
                    KeyRouteMissing,
                    "That delivery route has no drop-off addresses. Add CustomerDropoff points for it in JobCoordinates.xml.");
                return false;
            }

            var position = stop.Position;
            var meters = SideJobConfigLoader.DistanceBetween(origin, position);
            var districtName = string.IsNullOrWhiteSpace(stop.DistrictName)
                ? ResolveDistrict(position)
                : stop.DistrictName;

            _activeOrder = new FoodDeliveryOrder
            {
                OrderId = _nextOrderId++,
                CustomerName = ResolveCustomerLabel(stop),
                CustomerDistrict = SideJobConfigLoader.NormalizeDistrictName(districtName),
                Position = position,
                AnchorResolved = true,
                StopKey = GetStopKey(stop),
                DistanceBand = ResolveDistanceBand(meters),
                DistanceMeters = meters,
                MealValue = 0f,
            };

            EnsureTargetBlip();

            if (_phase != FoodDeliveryRunPhase.Paid)
            {
                _showStatus?.Invoke(LocalizedText.FormatOrDefault(
                    KeyEnRoute,
                    "Deliver to {0} ({1}).",
                    _activeOrder.CustomerName,
                    FormatDistance(meters)));
            }

            return true;
        }

        /// <summary>Friendly name for the authored point: the file name, or a generic customer label.</summary>
        private static string ResolveCustomerLabel(FoodDeliveryCustomerDefinition stop)
        {
            var name = stop != null ? stop.Name : null;
            if (string.IsNullOrWhiteSpace(name)
                || string.Equals(name, "Delivery Point", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return Text(KeyCustomer, "Customer");
            }

            return name;
        }

        /// <summary>
        /// District credited by a completed run: the chosen route's district, which is the district whose
        /// addresses were served. Falls back to the restaurant's district when no route is active (a save
        /// written before routes existed).
        /// </summary>
        internal static string ResolveCreditDistrict(string routeDistrict, string restaurantDistrict)
        {
            return string.IsNullOrWhiteSpace(routeDistrict)
                ? restaurantDistrict ?? string.Empty
                : routeDistrict;
        }

        private string ResolveOrderDistrict(FoodDeliveryOrder order)
        {
            if (order == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(order.CustomerDistrict))
            {
                return order.CustomerDistrict;
            }

            return ResolveDistrict(order.Position);
        }

        // ---------------------------------------------------------------- restaurant access

        /// <summary>
        /// Identity: restaurants arrive already resolved from the industry catalog, so there is no
        /// per-key resolution step left to do here.
        /// </summary>
        private static FoodDeliveryRestaurantInfo ResolveRestaurant(FoodDeliveryRestaurantInfo info)
        {
            return info;
        }

        /// <summary>Meals the restaurant can supply right now, from its ingredient stock.</summary>
        private int ResolveAvailableMeals(FoodDeliveryRestaurantInfo info, int capacity)
        {
            if (_getRestaurantIngredientStock == null || info == null || string.IsNullOrWhiteSpace(info.SiteKey))
            {
                // No stock plumbing (tests, or a legacy install): the load is bounded by the vehicle.
                return capacity;
            }

            var processedFood = SafeStock(info.SiteKey, "ProcessedFood");
            var meat = SafeStock(info.SiteKey, "Meat");

            var byProcessedFood = IngredientProcessedFoodPerMeal > 0f
                ? (int)Math.Floor(processedFood / IngredientProcessedFoodPerMeal)
                : capacity;
            var byMeat = IngredientMeatPerMeal > 0f
                ? (int)Math.Floor(meat / IngredientMeatPerMeal)
                : capacity;

            return Math.Max(0, Math.Min(capacity, Math.Min(byProcessedFood, byMeat)));
        }

        private float SafeStock(string siteKey, string commodity)
        {
            try
            {
                var stock = _getRestaurantIngredientStock(siteKey, commodity);
                return Math.Max(0f, stock);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Draws the ingredient tons for the requested meals and reports how many meals the restaurant
        /// could actually supply. A commodity the site does not accept removes nothing, which is the
        /// natural gate for a restaurant that is not configured for the job.
        /// </summary>
        private int DrawIngredients(string siteKey, int meals)
        {
            if (_drawRestaurantIngredient == null || string.IsNullOrWhiteSpace(siteKey) || meals <= 0)
            {
                return meals;
            }

            try
            {
                var processedFood = _drawRestaurantIngredient(siteKey, "ProcessedFood", meals * IngredientProcessedFoodPerMeal);
                var meat = _drawRestaurantIngredient(siteKey, "Meat", meals * IngredientMeatPerMeal);

                var byProcessedFood = IngredientProcessedFoodPerMeal > 0f
                    ? (int)Math.Floor(Math.Max(0f, processedFood) / IngredientProcessedFoodPerMeal)
                    : meals;
                var byMeat = IngredientMeatPerMeal > 0f
                    ? (int)Math.Floor(Math.Max(0f, meat) / IngredientMeatPerMeal)
                    : meals;

                return Math.Max(0, Math.Min(meals, Math.Min(byProcessedFood, byMeat)));
            }
            catch
            {
                return 0;
            }
        }

        private float ResolveProductBasePrice()
        {
            if (_activeRestaurant != null && _activeRestaurant.ProductBasePrice > 0f)
            {
                return _activeRestaurant.ProductBasePrice;
            }

            return DefaultProductBasePrice;
        }

        // ---------------------------------------------------------------- payouts

        private float GetPayoutMultiplier(string districtName)
        {
            var skillMultiplier = _skillSystem != null
                ? _skillSystem.GetBonusMultiplier(PlayerSkillId.FoodDelivery)
                : 1f;

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
                var percent = Math.Max(0f, _getDistrictBonusExcludingJob(districtName, JobId));
                return 1f + (percent / 100f);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Credits the restaurant's district and returns the status suffix. A drop-off passes 0 units
        /// (the pool is only topped up by a completed run); the completion passes the run's units.
        /// </summary>
        private string ReportDistrictCompletion(string districtName, float units)
        {
            if (_reportDistrictBonus == null || string.IsNullOrWhiteSpace(districtName) || units <= 0f)
            {
                return string.Empty;
            }

            try
            {
                var award = _reportDistrictBonus(JobId, districtName, units);
                if (award == null || !award.Applied || award.AppliedPoints <= 0f)
                {
                    return string.Empty;
                }

                return string.Concat(
                    " ",
                    LocalizedText.FormatOrDefault(
                        KeyDistrictBonus,
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

        // ---------------------------------------------------------------- visuals

        private void EnsureTargetBlip()
        {
            var position = Vector3.Zero;
            var name = string.Empty;

            if (_activeOrder != null)
            {
                position = _activeOrder.Position;
                name = _activeOrder.CustomerName;
            }

            if (position == Vector3.Zero)
            {
                ClearTargetBlip();
                ClearGps();
                return;
            }

            SetGps(position);

            if (_targetBlip != null && _targetBlip.Exists())
            {
                try
                {
                    _targetBlip.Position = position;
                    _targetBlip.Name = name;
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

                blip.Sprite = TargetBlipSprite;
                blip.Color = BlipColor.Yellow;
                blip.Name = name;
                blip.Scale = 0.9f;
                BlipLifecycleManager.ApplyStandardNearbyVisibility(blip);
                _targetBlip = blip;
            }
            catch
            {
                _targetBlip = null;
            }
        }

        private void ClearTargetBlip()
        {
            var blip = _targetBlip;
            _targetBlip = null;
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

        /// <summary>
        /// Keeps the delivery vehicle on the radar so the player can always find the scooter or truck
        /// they left behind, and takes the blip away while they are sitting in it.
        /// </summary>
        private void UpdateActiveVehicleBlip(Ped player)
        {
            var vehicle = _activeJobVehicle;
            if (vehicle == null || !vehicle.Exists() || PlayerIsInsideVehicle(player, vehicle))
            {
                ClearActiveVehicleBlip();
                return;
            }

            Vector3 position;
            try
            {
                position = vehicle.Position;
            }
            catch
            {
                return;
            }

            var blip = _activeVehicleBlip;
            if (blip == null || !blip.Exists())
            {
                try
                {
                    blip = World.CreateBlip(position);
                    if (blip == null || !blip.Exists())
                    {
                        _activeVehicleBlip = null;
                        return;
                    }

                    blip.Sprite = ActiveVehicleBlipSprite;
                    blip.Color = BlipColor.Yellow;
                    blip.Name = ActiveVehicleDisplayName;
                    blip.Scale = 0.85f;
                    BlipLifecycleManager.ApplyAlwaysVisibleVisibility(blip);
                    _activeVehicleBlip = blip;
                }
                catch
                {
                    _activeVehicleBlip = null;
                    return;
                }
            }

            try
            {
                blip.Position = position;
            }
            catch
            {
                // Blip refresh is best-effort.
            }
        }

        private static bool PlayerIsInsideVehicle(Ped player, Vehicle vehicle)
        {
            if (player == null || !player.Exists() || vehicle == null)
            {
                return false;
            }

            try
            {
                return player.IsInVehicle(vehicle);
            }
            catch
            {
                return false;
            }
        }

        private void ClearActiveVehicleBlip()
        {
            var blip = _activeVehicleBlip;
            _activeVehicleBlip = null;
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

        /// <summary>
        /// Draws the drop-off marker: a downward arrow hovering above the authored address, floating up
        /// and down so it reads as "here, above this spot" from the street. Same marker the garbage job
        /// uses for its bags.
        /// </summary>
        private void DrawTargetMarker(Ped player)
        {
            var order = _activeOrder;
            if (order == null)
            {
                return;
            }

            if (player.Position.DistanceTo(order.Position) > TargetMarkerDrawDistance)
            {
                return;
            }

            DrawDownArrowMarker(order.Position, Color.FromArgb(220, 240, 200, 80), TargetMarkerHeight);
        }

        private static void DrawDownArrowMarker(Vector3 position, Color color, float height)
        {
            try
            {
                // MarkerType.Arrow points up by default: the 180 degree rotation plus the downward
                // direction flip it into an arrow pointing down at the address, and bobUpAndDown makes it
                // hover in the air instead of sitting flat on the ground.
                World.DrawMarker(
                    MarkerType.Arrow,
                    position + new Vector3(0f, 0f, height),
                    new Vector3(0f, 0f, -1f),
                    new Vector3(180f, 0f, 0f),
                    new Vector3(0.6f, 0.6f, 0.6f),
                    color,
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

        private void HideWorldVisuals()
        {
            ClearTargetBlip();
            ClearActiveVehicleBlip();
            ReleaseDropoffNpc();
            ClearGps();
        }

        // ---------------------------------------------------------------- customer npc

        /// <summary>
        /// Spawns the customer standing at the drop-off once the player is close enough to see them and
        /// keeps exactly one customer alive at a time. Customers are ambient peds: never persisted, never
        /// owned by the job, released as soon as the meal changes hands.
        /// </summary>
        private void EnsureDropoffNpc(Ped player)
        {
            var order = _activeOrder;
            if (order == null || player == null || !player.Exists())
            {
                return;
            }

            if (_dropoffPed != null)
            {
                if (_dropoffPed.Exists() && !IsPedDead(_dropoffPed))
                {
                    return;
                }

                // The customer is gone or dead: hand them back and put a fresh one at the door.
                ReleaseDropoffNpc();
            }

            try
            {
                if (player.Position.DistanceTo(order.Position) > DropoffNpcSpawnDistance)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            Ped ped = null;
            try
            {
                ped = World.CreateRandomPed(order.Position);
            }
            catch
            {
                ped = null;
            }

            if (ped == null || !ped.Exists())
            {
                return;
            }

            try
            {
                ped.IsPersistent = false;
                ped.BlockPermanentEvents = true;
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            }
            catch
            {
                // A plain pedestrian still works if the blocking call fails.
            }

            PlayWaitingScenario(ped);
            _dropoffPed = ped;
        }

        /// <summary>Makes the customer look like someone waiting for their food.</summary>
        private static void PlayWaitingScenario(Ped ped)
        {
            if (ped == null || !ped.Exists())
            {
                return;
            }

            var scenarios = new[] { "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_STAND_IMPATIENT" };
            for (int i = 0; i < scenarios.Length; i++)
            {
                try
                {
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, scenarios[i], 0, true);
                    return;
                }
                catch
                {
                    // Try the next scenario.
                }
            }
        }

        private static bool IsPedDead(Ped ped)
        {
            try
            {
                return ped.IsDead;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Makes the customer say something nice about the delivery. Best-effort: the game silently
        /// ignores an unavailable speech name, so the candidates are walked until one registers.
        /// </summary>
        private void PlayDropoffThanks()
        {
            var ped = _dropoffPed;
            if (ped == null || !ped.Exists())
            {
                return;
            }

            var speeches = new[] { "GENERIC_THANKS", "THANKS", "GENERIC_HI" };
            for (int i = 0; i < speeches.Length; i++)
            {
                try
                {
                    ped.PlayAmbientSpeech(speeches[i], SpeechModifier.ForceNormal);
                    if (ped.IsAmbientSpeechPlaying)
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }
        }

        /// <summary>Hands the customer back to the game so it despawns naturally.</summary>
        private void ReleaseDropoffNpc()
        {
            var ped = _dropoffPed;
            _dropoffPed = null;
            if (ped == null)
            {
                return;
            }

            try
            {
                if (ped.Exists())
                {
                    ped.MarkAsNoLongerNeeded();
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        // ---------------------------------------------------------------- helpers

        private bool IsDriverOfActiveVehicle(Ped player)
        {
            var vehicle = _activeJobVehicle;
            if (player == null || !player.Exists() || vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            try
            {
                if (!player.IsInVehicle(vehicle))
                {
                    return false;
                }

                return vehicle.Driver == player;
            }
            catch
            {
                return false;
            }
        }

        private string ResolveDistrict(Vector3 position)
        {
            return SideJobConfigLoader.ResolveDistrictName(position, _districts);
        }

        private void ResolveCustomerDistricts()
        {
            for (int i = 0; i < _customers.Count; i++)
            {
                var customer = _customers[i];
                if (customer == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(customer.DistrictName))
                {
                    customer.DistrictName = ResolveDistrict(customer.Position);
                }
                else
                {
                    customer.DistrictName = SideJobConfigLoader.NormalizeDistrictName(customer.DistrictName);
                }
            }
        }

        private FoodDeliveryVehicleDefinition FindVehicle(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            for (int i = 0; i < _vehicles.Count; i++)
            {
                var definition = _vehicles[i];
                if (definition != null && string.Equals(definition.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private FoodDeliveryRestaurantInfo FindRestaurant(string siteKey)
        {
            if (string.IsNullOrWhiteSpace(siteKey))
            {
                return null;
            }

            for (int i = 0; i < _restaurants.Count; i++)
            {
                var restaurant = _restaurants[i];
                if (restaurant != null && string.Equals(restaurant.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
                {
                    return restaurant;
                }
            }

            return null;
        }

        /// <summary>
        /// Restaurant whose delivery pad receives the taken-out vehicle: the one the caller asked for
        /// (the restaurant whose tablet page is open), then the restaurant the player is standing at,
        /// then the job garage. Internal so the tests can pin the choice without a game world.
        /// </summary>
        internal FoodDeliveryRestaurantInfo ResolveVehicleSpawnRestaurant(string restaurantKey, Ped player)
        {
            var requested = FindRestaurant(restaurantKey);
            if (requested != null)
            {
                return ResolveRestaurant(requested);
            }

            var nearest = FindNearestRestaurantToPlayer(player);
            if (nearest != null)
            {
                return ResolveRestaurant(nearest);
            }

            var garage = GetGarageRestaurant();
            return garage != null ? ResolveRestaurant(garage) : null;
        }

        /// <summary>
        /// Restaurant closest to the player, inside the delivery service radius, or null when the player
        /// is nowhere near a restaurant. Only a fallback for callers that have no restaurant key.
        /// </summary>
        private FoodDeliveryRestaurantInfo FindNearestRestaurantToPlayer(Ped player)
        {
            if (player == null || !player.Exists())
            {
                return null;
            }

            Vector3 playerPosition;
            try
            {
                playerPosition = player.Position;
            }
            catch
            {
                return null;
            }

            FoodDeliveryRestaurantInfo best = null;
            var bestDistance = DefaultServiceRadius;

            for (int i = 0; i < _restaurants.Count; i++)
            {
                var definition = _restaurants[i];
                if (definition == null)
                {
                    continue;
                }

                var info = ResolveRestaurant(definition);
                var target = info.PositionResolved ? info.Position : definition.Position;
                if (target == Vector3.Zero)
                {
                    continue;
                }

                float distance;
                try
                {
                    distance = playerPosition.DistanceTo(target);
                }
                catch
                {
                    continue;
                }

                if (distance > bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = definition;
            }

            return best;
        }

        // ---------------------------------------------------------------- persistence

        public FoodDeliveryPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new FoodDeliveryPersistenceSnapshot
            {
                ActiveVehicleModelName = _activeVehicleModelName ?? string.Empty,
                ActiveRestaurantKey = _activeRestaurantKey ?? string.Empty,
                ProductCommodity = _productCommodity ?? string.Empty,
                LoadedMeals = Math.Max(0, _loadedMeals),
                MealsDelivered = Math.Max(0, _mealsDelivered),
                RunsCompleted = Math.Max(0, _runsCompleted),
                RunsAbandoned = Math.Max(0, _runsAbandoned),
                RouteCashEarned = Math.Max(0f, _routeCashEarned),
                RouteXpEarned = Math.Max(0f, _routeXpEarned),
                RunFareEarned = Math.Max(0f, _runFareEarned),
                SpoiledAtGameTime = Math.Max(0, _spoiledAtGameTime),
                NextOrderId = Math.Max(1, _nextOrderId),
                RouteDistrict = _routeDistrict ?? string.Empty,
                RouteSeed = Math.Max(0, _routeSeed),
                RouteStopsDelivered = Math.Max(0, _routeIndex),
            };

            for (int i = 0; i < _ownedVehicleModels.Count; i++)
            {
                var modelName = _ownedVehicleModels[i];
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    snapshot.OwnedVehicleModels.Add(modelName);
                }
            }

            var order = _activeOrder;
            if (order != null && order.OrderId > 0)
            {
                snapshot.ActiveOrderId = order.OrderId;
                snapshot.Orders.Add(new FoodDeliveryOrderSnapshot
                {
                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName ?? string.Empty,
                    CustomerDistrict = order.CustomerDistrict ?? string.Empty,
                    CustomerPosition = order.Position,
                    DistanceBand = order.DistanceBand,
                    DistanceMeters = order.DistanceMeters,
                    CashEarned = Math.Max(0f, order.CashEarned),
                });
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(FoodDeliveryPersistenceSnapshot snapshot)
        {
            ResetState();
            if (snapshot == null)
            {
                return;
            }

            _activeVehicleModelName = snapshot.ActiveVehicleModelName ?? string.Empty;

            if (snapshot.OwnedVehicleModels != null)
            {
                for (int i = 0; i < snapshot.OwnedVehicleModels.Count; i++)
                {
                    var modelName = snapshot.OwnedVehicleModels[i];
                    if (string.IsNullOrWhiteSpace(modelName) || OwnsVehicle(modelName))
                    {
                        continue;
                    }

                    // Drop garage entries whose vehicle no longer exists in JobVehicles.xml.
                    if (FindVehicle(modelName) != null)
                    {
                        _ownedVehicleModels.Add(modelName);
                    }
                }
            }

            var vehicleDefinition = FindVehicle(_activeVehicleModelName);
            _activeMealCapacity = vehicleDefinition != null
                ? Math.Max(1, vehicleDefinition.MealCapacity)
                : DefaultMealCapacity;

            _runsCompleted = Math.Max(0, snapshot.RunsCompleted);
            _runsAbandoned = Math.Max(0, snapshot.RunsAbandoned);
            _routeCashEarned = Math.Max(0f, snapshot.RouteCashEarned);
            _routeXpEarned = Math.Max(0f, snapshot.RouteXpEarned);
            _nextOrderId = Math.Max(1, snapshot.NextOrderId);

            var restaurantKey = snapshot.ActiveRestaurantKey ?? string.Empty;
            var restaurantDefinition = FindRestaurant(restaurantKey);

            var orderEntry = snapshot.Orders != null && snapshot.Orders.Count > 0
                ? snapshot.Orders[0]
                : null;

            // A run whose restaurant disappeared from the config is dropped instead of restored broken.
            if (restaurantDefinition == null || orderEntry == null || snapshot.ActiveOrderId <= 0)
            {
                return;
            }

            _activeRestaurant = ResolveRestaurant(restaurantDefinition);
            _activeRestaurantKey = restaurantKey;
            _productCommodity = snapshot.ProductCommodity ?? string.Empty;
            _loadedMeals = Math.Max(1, snapshot.LoadedMeals);
            _mealsDelivered = Math.Max(0, snapshot.MealsDelivered);
            _runFareEarned = Math.Max(0f, snapshot.RunFareEarned);
            _spoiledAtGameTime = Math.Max(0, snapshot.SpoiledAtGameTime);

            var gameTime = GetGameTimeSafe();
            _runSpoiled = _spoiledAtGameTime > 0 && gameTime >= _spoiledAtGameTime;

            // Rebuild the very route order the player was part-way through.
            RestoreRoute(snapshot.RouteDistrict, snapshot.RouteSeed, snapshot.RouteStopsDelivered);

            _activeOrder = new FoodDeliveryOrder
            {
                OrderId = snapshot.ActiveOrderId,
                CustomerName = orderEntry.CustomerName ?? string.Empty,
                CustomerDistrict = orderEntry.CustomerDistrict ?? string.Empty,
                Position = orderEntry.CustomerPosition,
                DistanceBand = orderEntry.DistanceBand,
                DistanceMeters = orderEntry.DistanceMeters,
                AnchorResolved = true,
                StopKey = FindStopKeyByPosition(orderEntry.CustomerPosition, 1.5f),
                CashEarned = Math.Max(0f, orderEntry.CashEarned),
            };

            _phase = FoodDeliveryRunPhase.EnRoute;
            _phaseDeadlineGameTime = gameTime + OrderStallTimeoutMs;
            _awaySinceGameTime = 0;
        }

        // ---------------------------------------------------------------- config loaders

        internal static List<FoodDeliveryVehicleDefinition> LoadVehicles(string configDirectory)
        {
            var result = new List<FoodDeliveryVehicleDefinition>();
            foreach (var element in SideJobConfigLoader.EnumerateJobVehicles(configDirectory, JobId))
            {
                var modelName = SideJobConfigLoader.ReadAttribute(element, "model");
                result.Add(new FoodDeliveryVehicleDefinition
                {
                    Id = SideJobConfigLoader.ReadIntAttribute(element, "id", 0),
                    Name = SideJobConfigLoader.ReadAttribute(element, "name", modelName),
                    ModelName = modelName,
                    UnlockLevel = Math.Max(0, SideJobConfigLoader.ReadIntAttribute(element, "unlockLevel", 0)),
                    MealCapacity = Math.Max(1, SideJobConfigLoader.ReadIntAttribute(element, "mealCapacity", DefaultMealCapacity)),
                    Price = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "dailyRent", 0f)),
                    FuelCapacityLiters = Math.Max(0f, SideJobConfigLoader.ReadFloatAttribute(element, "fuelCapacityLiters", 30f)),
                });
            }

            return result;
        }

        /// <summary>Loads the shared CustomerDropoff address pool.</summary>
        internal static List<FoodDeliveryCustomerDefinition> LoadCustomers(string configDirectory)
        {
            var result = new List<FoodDeliveryCustomerDefinition>();
            var document = SideJobConfigLoader.TryLoadDocument(
                SideJobConfigLoader.BuildSideJobsPath(configDirectory, "JobCoordinates.xml"));
            var root = document != null ? document.Root : null;
            if (root == null)
            {
                return result;
            }

            foreach (var element in root.Elements("JobPoint"))
            {
                if (!string.Equals(SideJobConfigLoader.ReadAttribute(element, "job"), JobId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(SideJobConfigLoader.ReadAttribute(element, "function"), CustomerFunctionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!SideJobConfigLoader.HasAnyCoordinate(element))
                {
                    continue;
                }

                var position = SideJobConfigLoader.ReadPosition(element);
                if (position == Vector3.Zero)
                {
                    continue;
                }

                result.Add(new FoodDeliveryCustomerDefinition
                {
                    Id = SideJobConfigLoader.ReadIntAttribute(element, "id", 0),
                    Name = SideJobConfigLoader.ReadAttribute(element, "name", "Customer"),
                    Position = position,
                    DistrictName = SideJobConfigLoader.NormalizeDistrictName(
                        SideJobConfigLoader.ReadAttribute(element, "district")),
                });
            }

            return result;
        }

        private static BlipSprite ResolveBlipSprite(string[] candidates, BlipSprite fallback)
        {
            try
            {
                var available = new HashSet<string>(Enum.GetNames(typeof(BlipSprite)), StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (available.Contains(candidates[i]))
                    {
                        return (BlipSprite)Enum.Parse(typeof(BlipSprite), candidates[i], true);
                    }
                }
            }
            catch
            {
                // Fall through to the safe default.
            }

            return fallback;
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
                try
                {
                    model.MarkAsNoLongerNeeded();
                }
                catch
                {
                    // Model release is best-effort.
                }
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
        /// The game clock, read defensively: Game.GameTime goes through a native, so a caller that runs
        /// outside the game loop (menus, persistence, unit tests) must not be able to throw.
        /// </summary>
        private static int GetGameTimeSafe()
        {
            try
            {
                return Game.GameTime;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatDistance(float meters)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                meters >= 1000f ? "{0:0.0} km" : "{0:0} m",
                meters >= 1000f ? meters / 1000f : meters);
        }

        // ---------------------------------------------------------------- testable maths

        /// <summary>
        /// Delivery fee for one meal from one restaurant: a share of the finished good's base price plus
        /// a per-kilometre term for the distance between the restaurant and the customer.
        /// </summary>
        internal static float ComputeMealFare(float productBasePrice, float kilometers, float meals, float multiplier)
        {
            var basePrice = productBasePrice > 0f ? productBasePrice : DefaultProductBasePrice;
            var distance = Math.Max(0f, kilometers) * PerKilometer;
            var value = (basePrice * MealValueFactor) + distance;
            return value * Math.Max(0f, meals) * Math.Max(0f, multiplier > 0f ? multiplier : 1f);
        }

        /// <summary>Ingredient tons a run of the given meals draws from the restaurant.</summary>
        internal static float GetIngredientTons(int meals, out float processedFoodTons, out float meatTons)
        {
            var count = Math.Max(0, meals);
            processedFoodTons = count * IngredientProcessedFoodPerMeal;
            meatTons = count * IngredientMeatPerMeal;
            return processedFoodTons + meatTons;
        }

        /// <summary>Wholesale cost of loading the given meals when the restaurant is not owned.</summary>
        internal static float ComputeWholesaleCost(int meals, bool isOwned)
        {
            return isOwned ? 0f : Math.Max(0, meals) * WholesaleMealCost;
        }

        /// <summary>District units credited by a completed run: a full load is worth 1.0.</summary>
        internal static float GetRunContributionUnits(int mealsDelivered, int mealCapacity)
        {
            var delivered = Math.Max(0, mealsDelivered);
            var capacity = Math.Max(1, mealCapacity);
            return (float)delivered / capacity;
        }

        /// <summary>
        /// Band label for an authored distance: 0 short, 1 medium, 2 long. Purely descriptive - the band
        /// no longer selects, generates or filters anything.
        /// </summary>
        internal static int ResolveDistanceBand(float meters)
        {
            if (meters < MediumBandMinMeters)
            {
                return 0;
            }

            return meters < LongBandMinMeters ? 1 : 2;
        }

        internal static bool IsStoppedWithinRange(float distanceMeters, float speedMps, float arriveDistance)
        {
            return distanceMeters <= arriveDistance && speedMps <= StationarySpeedMps;
        }

        internal static bool HasAbandonedRun(int gameTime, int awaySinceGameTime, int playerAwayTimeoutMs)
        {
            return awaySinceGameTime > 0
                && gameTime > awaySinceGameTime
                && gameTime - awaySinceGameTime >= playerAwayTimeoutMs;
        }

        internal static bool IsSpoiled(int gameTime, int spoiledAtGameTime)
        {
            return spoiledAtGameTime > 0 && gameTime >= spoiledAtGameTime;
        }
    }
}

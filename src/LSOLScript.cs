using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using OfficeMenuItem = LSOL.UI.MenuItem;
using WinForms = System.Windows.Forms;

namespace LSOL
{
    public sealed partial class LSOLScript : Script
    {
        private const string MessagePrefix = "~y~[LSOL]~s~ ";
        private const float IndustryMarkerDrawDistance = 180f;
        private const float IndustryInteractionDistance = 4.8f;
        private const float OfficeInteractionDistance = 3.8f;
        private const float CargoRigMaxBodyHealth = 1000f;
        private const float CargoDamageGraceHealth = 40f;
        private const float CargoConditionLossPerDamageRatio = 0.75f;
        private const float CargoLossPerDamageRatio = 0.35f;
        private const float IndustryObjectDeletionRadius = 100f;
        private const float IndustryObjectDeletionActivationRange = 250f;
        private const int IndustryObjectDeletionSweepIntervalMs = 5000;
        private const float DebugFillTons = 1000000f;
        private const string SavegamesDirectoryName = "LSOLSaves";
        private const int MaxSaveNameLength = 40;
        private const float DefaultStartingBalance = 20000f;
        private const int DefaultNpcRouteLimit = 5;
        private const int MinNpcRouteLimit = 0;
        private const int MaxNpcRouteLimit = 10;
        private const int AlertRuleEvaluationIntervalMs = 15000;
        private const int AlertRuleNotificationCooldownMs = 180000;
        private const int AlertRuleGlobalCooldownMs = 45000;
        private const int AlertRuleWarmupMs = 15000;
        private const string PhantomCommercialVehicleId = "20";
        private const string PhantomCommercialModelName = "phantom3";
        private const string PhantomRoadVeteranUnlockMessage = "Phantom unlocks after earning Road Veteran (100 deliveries).";
        private static readonly float[] DebugResourceAmountOptionsTons = { 1f, 5f, 10f, 25f, 50f, 100f, 250f, 500f, 1000f };
        private static readonly float[] DebugMoneyAmountOptions = { 1000f, 5000f, 10000f, 25000f, 50000f, 100000f, 500000f, 1000000f };
        private static readonly float[] DebugDistrictReputationAmountOptions = { 5f, 10f, 25f, 50f, 100f, 250f };
        private static readonly string[] DebugDistrictStateOptions = { "Unknown", "Emerging", "Established", "Dominant" };
        private static readonly float[] StartingBalanceOptions = BuildStartingBalanceOptions();
        private static readonly ModLanguage[] SelectableLanguages =
        {
            ModLanguage.English,
            ModLanguage.French,
            ModLanguage.Italian,
            ModLanguage.Spanish,
            ModLanguage.Russian,
            ModLanguage.Japanese,
            ModLanguage.Chinese,
            ModLanguage.Hindi,
            ModLanguage.Portuguese,
            ModLanguage.Turkish,
        };
        private static readonly ColorblindMode[] ColorblindModeOptions =
        {
            ColorblindMode.Off,
            ColorblindMode.Deuteranopia,
            ColorblindMode.Protanopia,
            ColorblindMode.Tritanopia,
        };

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly ModConfig _config;
        private readonly LsolAddonCatalog _addonCatalog;
        private readonly string _configDirectory;
        private readonly DifficultySettingsTemplateStore _difficultyTemplateStore;
        private readonly string _defaultIndustryStatePath;
        private readonly string _savegamesDirectoryPath;
        private readonly ControlBindings _controls;
        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly PropertyManager _propertyManager;
        private readonly CompanyFinanceTracker _financeTracker;
        private readonly BankLoanManager _bankLoanManager;
        private readonly PlayerSuccessTracker _playerSuccessTracker;
        private readonly VehicleFuelSystem _vehicleFuelSystem;
        private readonly VehicleLoadPowerService _vehicleLoadPowerService;
        private readonly GlobalMarketManager _globalMarket;
        private readonly TerritoryManager _territoryManager;
        private readonly SpecialMissionManager _specialMissionManager;
        private readonly IndustryOutputPropManager _industryOutputPropManager;

        private readonly LemonMenu _officeMenu;
        private readonly LemonMenu _vehicleCargoMenu;
        private readonly SimpleMenu _upgradeMenu;
        private readonly LemonMenu _modControlMenu;
        private readonly LemonMenu _savingOptionsMenu;
        private readonly LemonMenu _newSaveSetupMenu;
        private readonly LemonMenu _saveSlotsMenu;
        private readonly LemonMenu _industryPurchaseMenu;
        private readonly LemonMenu _difficultyMenu;
        private readonly LemonMenu _difficultyActionsMenu;
        private readonly LemonMenu _difficultyTemplateMenu;
        private readonly LemonMenu _optionsMenu;
        private readonly LemonMenu _notificationsMenu;
        private readonly LemonMenu _debugMenu;
        private readonly LemonMenu _debugMissionMenu;
        private readonly DebugMenuProvider _debugMenuProvider;
        private readonly BlipLifecycleManager _blipLifecycleManager;
        private readonly CargoTransferController _cargoTransferController;
        private readonly NpcLogisticsManager _npcLogisticsManager;
        private readonly PlayerContractsManager _playerContractsManager;
        private readonly IndustryRefuelService _industryRefuelService;
        private readonly OfficeObjectManager _officeObjectManager;
        private readonly NpcLogisticsController _npcLogisticsController;
        private readonly TabletStateStore _tabletStateStore;
        private readonly TabletShellController _tabletShellController;
        private readonly CompanyMapController _companyMapController;
        private readonly VehicleSpawnController _vehicleSpawnController;
        private readonly WorkerSpawnController _workerSpawnController;
        private readonly Dictionary<string, Blip> _commercialVehicleBlips;
        private readonly Dictionary<string, Blip> _personalVehicleBlips;
        private readonly Dictionary<string, int> _alertRuleLastShownByKey;

        private readonly Dictionary<WinForms.Keys, int> _keyCooldownUntil;
        private readonly HashSet<WinForms.Keys> _heldKeys;

        private readonly Vector3 _mainOfficeMarkerSeed;
        private readonly Vector3 _vehicleSpawnMarkerSeed;

        private string _industryStatePath;
        private Industry _nearestIndustry;
        private Industry _menuIndustry;
        private Industry _pendingIndustryPurchaseIndustry;
        private VehicleCargoMenuContext _vehicleCargoMenuContext;

        private int _selectedDebugResourceIndex;
        private int _selectedDebugResourceAmountIndex;
        private int _selectedDebugMoneyAmountIndex;
        private int _selectedDebugDistrictIndex;
        private int _selectedDebugDistrictReputationAmountIndex;
        private int _selectedDebugDistrictStateIndex;
        private int _selectedStartingBalanceIndex;
        private int _lastIndustryTickMs;
        private int _lastNearestProbeMs;
        private int _lastBlipRefreshMs;
        private int _lastAlertRuleEvaluationMs;
        private int _nextAlertRuleAllowedAtMs;
        private int _statusMessageUntil;

        private string _pendingSaveName;
        private string _pendingDeleteSavePath;
        private string _statusMessage;

        private float _profit;
        private float _currentStartingBalance;
        private float _lastPlayerSuccessBalanceSync;
        private GameModMode _gameModMode;
        private IndustryPurchaseMenuReturnTarget _industryPurchaseMenuReturnTarget;
        private SaveSlotMenuAction _saveSlotMenuAction;
        private DifficultyProfileTarget _activeDifficultyProfileTarget;

        private bool _modMechanicsEnabled;
        private bool _industryPersistenceEnabled;
        private bool _hasPlayerSuccessBalanceSync;
        private bool _difficultySettingsLocked;
        private bool _cargoDamageDifficultyEnabled;
        private bool _pendingCargoDamageDifficultyEnabled;
        private bool _industryPricingDifficultyEnabled;
        private bool _pendingIndustryPricingDifficultyEnabled;
        private bool _licensingDifficultyEnabled;
        private bool _pendingLicensingDifficultyEnabled;
        private bool _corridorRestrictionDifficultyEnabled;
        private bool _pendingCorridorRestrictionDifficultyEnabled;
        private bool _reputationDifficultyEnabled;
        private bool _pendingReputationDifficultyEnabled;
        private bool _officeGarageLimitDifficultyEnabled;
        private bool _pendingOfficeGarageLimitDifficultyEnabled;
        private bool _officeNpcLimitDifficultyEnabled;
        private bool _pendingOfficeNpcLimitDifficultyEnabled;
        private ModLanguage _language;
        private EconomyDifficultyPreset _economyDifficultyPreset;
        private EconomyDifficultyPreset _pendingEconomyDifficultyPreset;
        private ColorblindMode _colorblindMode;
        private bool _useMetricSpeedDisplay;
        private NpcWeeklyWageDifficulty _npcWeeklyWageDifficulty;
        private NpcWeeklyWageDifficulty _pendingNpcWeeklyWageDifficulty;
        private int _npcRouteLimit;
        private int _pendingNpcRouteLimit;
        private bool _vehicleFuelDifficultyEnabled;
        private bool _pendingVehicleFuelDifficultyEnabled;
        private bool _cargoWeightPowerDifficultyEnabled;
        private bool _pendingCargoWeightPowerDifficultyEnabled;
        private bool _cruiseControlEnabled;
        private float _cruiseControlTargetSpeedMps;
        private bool _isConstructing;
        private int _lastIndustryObjectDeletionSweepMs;
        private AlertRulesPersistenceSnapshot _alertRules;

        private OwnedFleetPersistenceSnapshot _pendingOwnedFleetRestore;
        private PropertyOwnershipPersistenceSnapshot _pendingPropertyRestore;
        private SpecialMissionPersistenceSnapshot _pendingSpecialMissionRestore;

        public LSOLScript()
        {
            _isConstructing = true;
            _configDirectory = ResolveConfigDirectory();
            _defaultIndustryStatePath = ResolveIndustryStatePath();
            _savegamesDirectoryPath = ResolveSavegamesDirectoryPath();
            _industryStatePath = _defaultIndustryStatePath;
            _difficultyTemplateStore = new DifficultySettingsTemplateStore(Path.Combine(_configDirectory, "DifficultyTemplates.xml"));
            _addonCatalog = LsolAddonCatalog.Load(_configDirectory);
            _config = ModConfig.Load(_configDirectory, _addonCatalog);
            _controls = _config.Controls ?? new ControlBindings();
            _industryManager = new IndustryManager(_config);
            _fleetManager = new FleetManager(_config);
            _propertyManager = new PropertyManager(_config);
            _financeTracker = new CompanyFinanceTracker();
            _bankLoanManager = new BankLoanManager(_config.BankDefinitions, _financeTracker);
            _propertyManager.ConfigureFinanceTracking(_financeTracker, GetCurrentInGameWeekMinute);
            _vehicleFuelSystem = new VehicleFuelSystem(_fleetManager, message => ShowStatus(message));
            _vehicleLoadPowerService = new VehicleLoadPowerService(_fleetManager);
            _globalMarket = new GlobalMarketManager(Game.GameTime, _config.CommodityBasePrices);
            _industryManager.ConfigureMarketPressure(_globalMarket);
            _territoryManager = new TerritoryManager(_config, _industryManager);
            _globalMarket.ConfigureShockContext(GetCurrentInGameWeekMinute, () => _territoryManager != null ? _territoryManager.DistrictStates : Enumerable.Empty<TerritoryDistrictState>());
            _industryManager.ConfigureStoragePressure(_territoryManager, _financeTracker, GetCurrentInGameWeekMinute, message => ShowStatus(message, 4500));
            _specialMissionManager = new SpecialMissionManager(
                _configDirectory,
                _industryManager,
                _territoryManager,
                _fleetManager,
                _globalMarket,
                () => _propertyManager != null ? _propertyManager.CommercialVehicles : Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                amount => AddProfit(CompanyFinanceCategory.MissionReward, amount, "Mission reward"),
                ShowStatus,
                () =>
                {
                    if (_tabletStateStore != null)
                    {
                        _tabletStateStore.MarkAllDirty();
                    }
                },
                GetCurrentInGameWeekMinute,
                HandlePlayerSuccessMissionCompleted,
                _addonCatalog);
            _industryOutputPropManager = new IndustryOutputPropManager(_industryManager.Industries);

            _mainOfficeMarkerSeed = _config.MainOfficePosition;
            _vehicleSpawnMarkerSeed = _config.VehicleSpawnPosition;
            _blipLifecycleManager = new BlipLifecycleManager(
                _industryManager,
                () => _propertyManager != null ? _propertyManager.Offices : new OfficeDefinition[0],
                () => _propertyManager != null ? _propertyManager.ActiveOfficeId : string.Empty,
                () => _propertyManager != null ? _propertyManager.Interiors : new InteriorDefinition[0],
                () => _propertyManager != null ? _propertyManager.ActiveApartmentId : string.Empty,
                () => _propertyManager != null ? _propertyManager.Motels : new MotelDefinition[0],
                () =>
                {
                    string reason;
                    return _propertyManager != null && _propertyManager.CanUseApartmentSystems(out reason);
                },
                () => _bankLoanManager != null ? _bankLoanManager.Banks : Array.Empty<BankDefinition>(),
                ResolveOfficeBlipSeed,
                CommercialDealershipMarker,
                PersonalDealershipMarker,
                GetGroundPosition,
                GetIndustryMarkerPosition,
                IsPetrolServiceStation,
                _territoryManager);
            _playerContractsManager = new PlayerContractsManager(
                _industryManager,
                _fleetManager,
                _propertyManager,
                _globalMarket,
                GetGroundPosition,
                () => Game.Player.Character,
                GetCurrentInGameWeekMinute,
                message => ShowStatus(message),
                _territoryManager,
                () =>
                {
                    if (_tabletStateStore != null)
                    {
                        _tabletStateStore.MarkNetworkDirty();
                        _tabletStateStore.MarkCargoDirty();
                    }
                },
                null,
                _financeTracker);
            _cargoTransferController = new CargoTransferController(
                _fleetManager,
                _industryManager,
                _globalMarket,
                message => ShowStatus(message),
                _playerContractsManager,
                _territoryManager,
                industry => _industryOutputPropManager.RefreshIndustry(industry));
            var cargoFilterOrder = _config.CargoTypes != null && _config.CargoTypes.Count > 0
                ? _config.CargoTypes
                : new List<VehicleCargoType>
                {
                    VehicleCargoType.Aggregates,
                    VehicleCargoType.OpenHull,
                    VehicleCargoType.Wood,
                    VehicleCargoType.CraftedGoods,
                    VehicleCargoType.Liquid,
                    VehicleCargoType.DryBulk,
                    VehicleCargoType.Refrigeration,
                    VehicleCargoType.Recyclable,
                    VehicleCargoType.Vehicles,
                };
            var defaultCargoFilter = cargoFilterOrder.Contains(VehicleCargoType.CraftedGoods)
                ? VehicleCargoType.CraftedGoods
                : (cargoFilterOrder.Count > 0 ? cargoFilterOrder[0] : VehicleCargoType.Aggregates);
            _vehicleSpawnController = new VehicleSpawnController(
                _fleetManager,
                _vehicleSpawnMarkerSeed,
                _config.VehicleSpawnHeading,
                cargoFilterOrder,
                defaultCargoFilter,
                definition => IsCommercialDealershipVehicleAvailableToPlayer(definition));
            _workerSpawnController = new WorkerSpawnController(_config.WorkerModels);
            _commercialVehicleBlips = new Dictionary<string, Blip>(StringComparer.OrdinalIgnoreCase);
            _personalVehicleBlips = new Dictionary<string, Blip>(StringComparer.OrdinalIgnoreCase);
            _alertRuleLastShownByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _npcLogisticsManager = new NpcLogisticsManager(
                _configDirectory,
                _industryManager,
                _fleetManager,
                _globalMarket,
                GetGroundPosition,
                () => _profit,
                DeductProfit,
                AddProfit,
                message => ShowStatus(message),
                _territoryManager,
                () => _propertyManager != null ? _propertyManager.CommercialVehicles : Array.Empty<OwnedCommercialVehiclePersistenceEntry>(),
                () => _propertyManager != null ? _propertyManager.ActiveOfficeId : string.Empty,
                () => _propertyManager != null ? _propertyManager.ActiveOffice : null,
                _financeTracker,
                GetCurrentInGameWeekMinute,
                HandlePlayerSuccessNpcContractsChanged,
                RecordNpcSuccessDeliveryProgress);
            _propertyManager.ConfigureEconomicPressure(
                () => _industryManager != null
                    ? _industryManager.Industries.Count(industry => industry != null && industry.IsOwned && !industry.IsStarterHeadquarters)
                    : 0,
                () => _npcLogisticsManager != null && _npcLogisticsManager.Contracts != null
                    ? _npcLogisticsManager.Contracts.Count(contract => contract != null)
                    : 0,
                () => _territoryManager != null && _territoryManager.DistrictStates != null
                    ? _territoryManager.DistrictStates.Count(state => state != null && (state.LicenseStatus == DistrictLicenseStatus.Active || state.LicenseStatus == DistrictLicenseStatus.Probation))
                    : 0,
                GetSecuredSupportSiteCount,
                () => _territoryManager != null ? _territoryManager.GetActiveCorridorCount() : 0);
            _industryRefuelService = new IndustryRefuelService(
                _fleetManager,
                _vehicleFuelSystem,
                _industryManager,
                _globalMarket,
                () => _profit,
                DeductProfit,
                _financeTracker,
                GetCurrentInGameWeekMinute,
                GetIndustryMarkerPosition,
                GetGroundPosition,
                message => ShowStatus(message, 4500),
                IndustryInteractionDistance);
            _officeObjectManager = new OfficeObjectManager(
                _propertyManager,
                _fleetManager,
                _vehicleFuelSystem,
                _industryManager,
                _globalMarket,
                () => _profit,
                DeductProfit,
                _financeTracker,
                GetCurrentInGameWeekMinute,
                GetGroundPosition,
                message => ShowStatus(message, 4500),
                () =>
                {
                    ReevaluatePlayerSuccesses(true);
                    if (_tabletStateStore != null)
                    {
                        _tabletStateStore.MarkAllDirty();
                    }
                });

            _officeMenu = new LemonMenu("Office")
            {
                Subtitle = "Manage workers and fleet deployment",
                AlignRight = true,
            };
            _vehicleCargoMenu = new LemonMenu("Vehicle & Cargo Type")
            {
                Subtitle = "Choose cargo filter, vehicle, and spawn",
                AlignRight = true,
            };
            _upgradeMenu = new SimpleMenu("Industry Upgrades")
            {
                Subtitle = "Invest profits into modules",
            };
            _modControlMenu = new LemonMenu("Game Mod Control")
            {
                Subtitle = "Activate mechanics and configure gameplay",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _savingOptionsMenu = new LemonMenu("Saving Options")
            {
                Subtitle = "Create, load, delete, and save named games",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _newSaveSetupMenu = new LemonMenu("Difficulty Settings")
            {
                Subtitle = "Configure a new save before starting",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _saveSlotsMenu = new LemonMenu("Save Slots")
            {
                Subtitle = "Choose a saved game profile",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _industryPurchaseMenu = new LemonMenu("Buy Industry")
            {
                Subtitle = "Confirm the industry purchase",
                AlignRight = true,
            };
            _difficultyMenu = new LemonMenu("Difficulty Settings")
            {
                Subtitle = "Enable or disable challenge options",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _difficultyActionsMenu = new LemonMenu("Difficulty Actions")
            {
                Subtitle = "Templates",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _difficultyTemplateMenu = new LemonMenu("Load Difficulty Template")
            {
                Subtitle = "Choose a saved difficulty profile",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _optionsMenu = new LemonMenu("Options")
            {
                Subtitle = "Language and accessibility settings",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _notificationsMenu = new LemonMenu("Notifications")
            {
                Subtitle = "Control gameplay alerts",
                AlignRight = true,
                Theme = LemonMenuTheme.Default,
            };
            _debugMenu = new LemonMenu("Debug")
            {
                Subtitle = "Runtime industry and vehicle tools",
                AlignRight = true,
                MaxVisibleItems = 10,
                Theme = LemonMenuTheme.Default,
            };
            _debugMissionMenu = new LemonMenu("Trigger Missions")
            {
                Subtitle = "Force-start loaded community contracts",
                AlignRight = true,
                MaxVisibleItems = 10,
                Theme = LemonMenuTheme.Default,
            };
            _debugMenuProvider = new DebugMenuProvider();
            InitializePropertyMenus();
            InitializeBankingMenus();
            _npcLogisticsController = new NpcLogisticsController(
                _controls,
                _npcLogisticsManager,
                OpenOfficeMenu,
                GetNpcHiringBlockedReason,
                message => ShowStatus(message));
            _companyMapController = new CompanyMapController(
                _controls,
                _territoryManager,
                _industryManager,
                CloseAllMenus,
                () => _profit,
                AcquireDistrictLicenseFromOffice,
                SecureSupportSiteFromOffice,
                AssignSupportCrewFromOffice,
                HireSupportStaffFromOffice,
                SetDepotSpecializationFromOffice,
                message => ShowStatus(message),
                () => _tabletStateStore != null ? _tabletStateStore.GetRoutePlannerOverlaySnapshot() : new RoutePlannerOverlaySnapshot());
            _tabletStateStore = new TabletStateStore(
                _industryManager,
                _fleetManager,
                _vehicleFuelSystem,
                _globalMarket,
                _npcLogisticsManager,
                _playerContractsManager,
                _propertyManager,
                _bankLoanManager,
                _financeTracker,
                _territoryManager,
                GetCurrentInGameWeekMinute,
                () => Game.Player.Character,
                () => _nearestIndustry,
                () => _profit,
                () => _vehicleSpawnController.SelectedFilter,
                GetIndustryMarkerPosition,
                () => _cargoTransferController.HasPendingTransfer,
                GetActiveStatusMessage,
                () => _territoryManager != null ? _territoryManager.GetControlledDistrictCount() : 0,
                () => _territoryManager != null ? _territoryManager.GetActiveCorridorCount() : 0,
                GetSecuredSupportSiteCount,
                () => _territoryManager != null ? _territoryManager.DistrictStates : Enumerable.Empty<TerritoryDistrictState>(),
                () => _territoryManager != null ? _territoryManager.GetOperationsSummary() : new TerritoryOperationsSummary(),
                currentMinute => _territoryManager != null ? _territoryManager.GetRemainingOperationsChargeMinutes(currentMinute) : (7 * 24 * 60));
            _playerSuccessTracker = new PlayerSuccessTracker(
                _industryManager,
                _propertyManager,
                _npcLogisticsManager,
                _territoryManager,
                _specialMissionManager,
                _bankLoanManager,
                _financeTracker,
                ShowStatus);
            _territoryManager.ConfigureEndgameContext(
                () => _playerSuccessTracker != null ? _playerSuccessTracker.GetEndgameSummary() : new CompanyEndgameSummary(),
                () => _npcLogisticsManager != null ? _npcLogisticsManager.GetDistrictCompetitionSummaries() : Array.Empty<NpcDistrictCompetitionSummary>(),
                () => _npcLogisticsManager != null ? _npcLogisticsManager.GetCorridorCompetitionSummaries() : Array.Empty<NpcCorridorCompetitionSummary>());
            _tabletShellController = new TabletShellController(_controls, _tabletStateStore);
            _tabletShellController.RegisterApp(new HomeTabletApp(OpenCompanyMapMenuFromTablet, OpenCompanyDistrictViewFromTablet, OpenCompanyDepotViewFromTablet, _specialMissionManager, _playerSuccessTracker));
            _tabletShellController.RegisterApp(new BudgetTabletApp());
            _tabletShellController.RegisterApp(new PropertyPortfolioTabletApp(CreatePropertyPortfolioTabletActions()));
            _tabletShellController.RegisterApp(new AnalyticsTabletApp(OpenAnalyticsRoutePlannerMapFromTablet, OpenNpcPlannerDraftFromTablet));
            _tabletShellController.RegisterApp(new SuccessesTabletApp(_playerSuccessTracker));
            _tabletShellController.RegisterApp(new SpecialMissionsTabletApp(_specialMissionManager));
            _tabletShellController.RegisterApp(new NetworkTabletApp(IndustryInteractionDistance, PurchaseContractorPermitFromTablet, AddIndustryGpsRouteFromTablet, ClearGpsRouteFromTablet, HandleCompanyServiceRefuelRequested, HandleCompanyServiceRepairRequested, ToggleServiceSiteOperatorFromTablet, message => ShowStatus(message), OpenNetworkRoutePlannerMapFromTablet, OpenNpcPlannerDraftFromTablet));
            _tabletShellController.RegisterApp(new IndustryTabletApp(
                IndustryInteractionDistance,
                HandleTabletLoadRequested,
                HandleTabletLoadCommodityRequested,
                HandleTabletUnloadRequested,
                HandleTabletUnloadModeRequested,
                HandleTabletRefuelRequested,
                HandleTabletUpgradeModuleRequested,
                HandleTabletVehicleSpawnerRequested,
                PurchaseIndustryFromTablet));

            _keyCooldownUntil = new Dictionary<WinForms.Keys, int>();
            _heldKeys = new HashSet<WinForms.Keys>();
            _alertRules = new AlertRulesPersistenceSnapshot();
            ResetAlertRuleRuntimeState(Game.GameTime, true);

            _gameModMode = GameModMode.Fun;
            _vehicleCargoMenuContext = VehicleCargoMenuContext.Office;
            _profit = DefaultStartingBalance;
            _currentStartingBalance = DefaultStartingBalance;
            _modMechanicsEnabled = false;
            _industryPersistenceEnabled = true;
            _difficultySettingsLocked = false;
            _language = ModLanguage.English;
            _colorblindMode = ColorblindMode.Off;
            _useMetricSpeedDisplay = false;
            _activeDifficultyProfileTarget = DifficultyProfileTarget.Live;
            SetLiveDifficultyProfile(DifficultySettingsProfile.CreateDefault());
            SyncPendingDifficultyProfileFromLive();
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            _pendingSaveName = string.Empty;
            _industryPurchaseMenuReturnTarget = IndustryPurchaseMenuReturnTarget.None;
            _saveSlotMenuAction = SaveSlotMenuAction.Load;
            _playerSuccessTracker.ResetForNewSave(_profit);
            SyncPlayerSuccessBalance(false);
            ApplyPresentationSettings(false);
            ApplyDifficultySettingsToSystems();

            if (_industryPersistenceEnabled)
            {
                TryLoadIndustryPersistence(false);
            }

            RebuildOfficeMenuItems();
            RebuildModControlMenuItems();
            RebuildDifficultyMenuItems();
            RebuildOptionsMenuItems();
            RebuildDebugMenuItems();

            Tick += OnTick;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Aborted += OnAborted;

            _isConstructing = false;

            Notification.PostTicker(PrefixMessage(Text(ModTextKey.DetailLoadedSuccessfully)), false, false);
        }

        private bool AnyMenuOpen
        {
            get
            {
                return _officeMenu.IsOpen
                    || (_bankMenu != null && _bankMenu.IsOpen)
                    || _vehicleCargoMenu.IsOpen
                    || _upgradeMenu.IsOpen
                    || _modControlMenu.IsOpen
                    || _savingOptionsMenu.IsOpen
                    || _newSaveSetupMenu.IsOpen
                    || _saveSlotsMenu.IsOpen
                    || _industryPurchaseMenu.IsOpen
                    || _difficultyMenu.IsOpen
                    || _difficultyActionsMenu.IsOpen
                    || _difficultyTemplateMenu.IsOpen
                    || _optionsMenu.IsOpen
                    || _notificationsMenu.IsOpen
                    || _debugMenu.IsOpen
                    || _debugMissionMenu.IsOpen
                    || HasPropertyMenuOpen()
                    || _npcLogisticsController.AnyMenuOpen
                    || _companyMapController.AnyMenuOpen
                    || (_officeObjectManager != null && _officeObjectManager.IsPlacementActive)
                    || _tabletShellController.IsOpen;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            RestorePendingWorldState();

            var gameTime = Game.GameTime;
            UpdateApartmentSleepTransition(player, gameTime);
            SweepIndustryObjectDeletions(player, gameTime);

            if (_lastIndustryTickMs == 0)
            {
                _lastIndustryTickMs = gameTime;
            }

            if (!_modMechanicsEnabled)
            {
                DrawOpenMenus();

                return;
            }

            var elapsed = gameTime - _lastIndustryTickMs;
            if (elapsed >= 1000)
            {
                _lastIndustryTickMs = gameTime;
                _globalMarket.Update(gameTime);
                var shockAnnouncement = _globalMarket.ConsumePendingShockAnnouncement();
                if (!string.IsNullOrWhiteSpace(shockAnnouncement))
                {
                    ShowStatus(shockAnnouncement, 4500);
                }

                _industryManager.Update(elapsed / 60000f, _config.OmegaMultiplier);
                _industryOutputPropManager.Update(player.Position);
                _fleetManager.CleanupStates();
                _vehicleFuelSystem.CleanupStates();
                _territoryManager.EvaluateFinancialPressure(_profit, gameTime, message => ShowStatus(message, 4500));
                _tabletStateStore.MarkMarketDirty();
                _tabletStateStore.MarkNetworkDirty();
            }

            if (gameTime - _lastNearestProbeMs >= 250)
            {
                _lastNearestProbeMs = gameTime;
                _nearestIndustry = _industryManager.GetNearestIndustry(player.Position, 40f);
                _tabletStateStore.MarkNearestIndustryDirty();
                _tabletStateStore.MarkCargoDirty();
            }

            if (gameTime - _lastBlipRefreshMs >= 1000)
            {
                _lastBlipRefreshMs = gameTime;
                RefreshBlipPositions();
            }

            _npcLogisticsManager.Update(gameTime, GetCurrentInGameWeekMinute());
            _playerContractsManager.Update(gameTime, GetCurrentInGameWeekMinute());
            if (_industryRefuelService.Update(gameTime))
            {
                _tabletStateStore.MarkCargoDirty();
                _tabletStateStore.MarkNetworkDirty();
            }
            ProcessPropertyWeeklyCharges();
            ProcessBankLoanRepayments();
            ProcessTerritoryWeeklyCharges();
            ProcessTerritoryWeeklyMaintenance();
            _tabletStateStore.CaptureHistory(gameTime);

            if (_vehicleFuelSystem.Update(player, gameTime))
            {
                _tabletStateStore.MarkCargoDirty();
            }

            EvaluateAmbientAlertRules(gameTime);

            _vehicleLoadPowerService.Update(player);
            UpdateCruiseControl(player);

            _specialMissionManager.Update(player, gameTime);
            _officeObjectManager.Update(
                player,
                GetSelectedOfficeObjectPreviewDefinition(),
                _officeObjectsMenu != null && _officeObjectsMenu.IsOpen,
                gameTime);
            UpdatePersonalDealershipPreview();
            SyncPlayerSuccessBalance(true, true);

            DrawMarkers(player);
            _cargoTransferController.Update(gameTime, DrawProgressBar);
            var drewCargoOverview = UpdateCargoOverviewAndIntegrity(player, gameTime);
            if (!drewCargoOverview)
            {
                DrawActiveVehicleFuelHud(player);
            }

            DrawOpenMenus();
            DrawTabletShell();
        }

        private void OnKeyDown(object sender, WinForms.KeyEventArgs e)
        {
            if (_heldKeys.Contains(e.KeyCode))
            {
                return;
            }

            _heldKeys.Add(e.KeyCode);

            if (IsDebugMenuHotkey(e))
            {
                if (!CanHandleKeyPress(e.KeyCode))
                {
                    return;
                }

                ToggleDebugMenu();
                return;
            }

            if (!CanHandleKeyPress(e.KeyCode))
            {
                return;
            }

            if (e.KeyCode == _controls.OpenModMenu)
            {
                ToggleModControlMenu();
                return;
            }

            if (e.KeyCode == _controls.OpenDebugMenu)
            {
                ToggleDebugMenu();
                return;
            }

            if (e.KeyCode == _controls.ToggleDashboard)
            {
                if (_modMechanicsEnabled)
                {
                    OpenTabletNetworkApp();
                }

                return;
            }

            if (HandleTabletShellKey(e.KeyCode))
            {
                return;
            }

            if (HandleMenuKey(e.KeyCode))
            {
                return;
            }

            if (_officeObjectManager.HandleKey(e.KeyCode, _controls))
            {
                return;
            }

            if (!_modMechanicsEnabled)
            {
                return;
            }

            if (e.KeyCode == _controls.Interact)
            {
                var player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    if (HandlePropertyInteraction(player))
                    {
                        return;
                    }

                    if (HandleBankInteraction(player))
                    {
                        return;
                    }

                    var nearbyIndustry = GetIndustryInInteractionRange(player.Position);
                    if (nearbyIndustry != null)
                    {
                        _nearestIndustry = nearbyIndustry;
                        TryOpenIndustryTablet();
                        return;
                    }

                    if (_specialMissionManager.HandleInteract(player))
                    {
                        return;
                    }
                }

                return;
            }

        }

        private void OnKeyUp(object sender, WinForms.KeyEventArgs e)
        {
            _heldKeys.Remove(e.KeyCode);
        }

        private static bool IsDebugMenuHotkey(WinForms.KeyEventArgs e)
        {
            return System.Diagnostics.Debugger.IsAttached
                && e != null
                && e.KeyCode == WinForms.Keys.W
                && e.Alt;
        }

        private static bool IsVirtualKeyDown(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        private bool HandleTabletShellKey(WinForms.Keys key)
        {
            return _tabletShellController != null && _tabletShellController.HandleKey(key);
        }

        private bool HandleMenuKey(WinForms.Keys key)
        {
            if (_industryPurchaseMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    CancelIndustryPurchase();
                    return true;
                }

                _industryPurchaseMenu.HandleKey(key, _controls);
                return true;
            }

            if (_saveSlotsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToSavingOptionsMenu();
                    return true;
                }

                _saveSlotsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_difficultyTemplateMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToDifficultyActionsMenu();
                    return true;
                }

                _difficultyTemplateMenu.HandleKey(key, _controls);
                return true;
            }

            if (_difficultyActionsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToDifficultySourceMenu();
                    return true;
                }

                _difficultyActionsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_newSaveSetupMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    CancelNewSaveSetup();
                    return true;
                }

                _newSaveSetupMenu.HandleKey(key, _controls);
                return true;
            }

            if (_savingOptionsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToModControlMenu();
                    return true;
                }

                _savingOptionsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_optionsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToModControlMenu();
                    return true;
                }

                _optionsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_notificationsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToModControlMenu();
                    return true;
                }

                _notificationsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_debugMissionMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToDebugMenu();
                    return true;
                }

                _debugMissionMenu.HandleKey(key, _controls);
                return true;
            }

            if (_debugMenu.IsOpen)
            {
                _debugMenu.HandleKey(key, _controls);
                return true;
            }

            if (_modControlMenu.IsOpen)
            {
                _modControlMenu.HandleKey(key, _controls);
                return true;
            }

            if (_difficultyMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToModControlMenu();
                    return true;
                }

                _difficultyMenu.HandleKey(key, _controls);
                return true;
            }

            if (_npcLogisticsController.AnyMenuOpen)
            {
                _npcLogisticsController.HandleKey(key);
                return true;
            }

            if (_companyMapController.AnyMenuOpen)
            {
                _companyMapController.HandleKey(key);
                return true;
            }

            if (HandlePropertyMenuKey(key))
            {
                return true;
            }

            if (_bankMenu != null && _bankMenu.IsOpen)
            {
                HandleBankMenuKey(key);
                return true;
            }

            if (_officeMenu.IsOpen)
            {
                _officeMenu.HandleKey(key, _controls);
                return true;
            }

            if (_vehicleCargoMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnFromVehicleCargoMenu();
                    return true;
                }

                _vehicleCargoMenu.HandleKey(key, _controls);
                return true;
            }

            if (_upgradeMenu.IsOpen)
            {
                _upgradeMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        private bool CanHandleKeyPress(WinForms.Keys key)
        {
            if (IsApartmentSleepTransitionActive)
            {
                return false;
            }

            var now = Game.GameTime;
            var cooldownMs = AnyMenuOpen ? 95 : 220;

            if (key == _controls.Interact)
            {
                cooldownMs = 320;
            }

            int until;
            if (_keyCooldownUntil.TryGetValue(key, out until) && now < until)
            {
                return false;
            }

            _keyCooldownUntil[key] = now + cooldownMs;
            return true;
        }

        private void DrawOpenMenus()
        {
            _modControlMenu.Draw();
            _savingOptionsMenu.Draw();
            _newSaveSetupMenu.Draw();
            _saveSlotsMenu.Draw();
            _industryPurchaseMenu.Draw();
            _difficultyMenu.Draw();
            _difficultyActionsMenu.Draw();
            _difficultyTemplateMenu.Draw();
            _optionsMenu.Draw();
            _notificationsMenu.Draw();
            _officeMenu.Draw();
            DrawBankMenu();
            _vehicleCargoMenu.Draw();
            _debugMenu.Draw();
            _debugMissionMenu.Draw();
            DrawPropertyMenus();
            _npcLogisticsController.Draw();
            _companyMapController.Draw();

            if (_modControlMenu.IsOpen || _savingOptionsMenu.IsOpen || _newSaveSetupMenu.IsOpen || _saveSlotsMenu.IsOpen || _industryPurchaseMenu.IsOpen || _difficultyMenu.IsOpen || _difficultyActionsMenu.IsOpen || _difficultyTemplateMenu.IsOpen || _optionsMenu.IsOpen || _notificationsMenu.IsOpen || _officeMenu.IsOpen || (_bankMenu != null && _bankMenu.IsOpen) || _vehicleCargoMenu.IsOpen || _debugMenu.IsOpen || _debugMissionMenu.IsOpen || HasPropertyMenuOpen() || _npcLogisticsController.AnyMenuOpen || _companyMapController.AnyMenuOpen)
            {
                return;
            }

            if (_upgradeMenu.IsOpen)
            {
                _upgradeMenu.Draw();
            }
        }

        private void DrawTabletShell()
        {
            _tabletShellController.Draw();
        }

        private void DrawMarkers(Ped player)
        {
            var playerPos = player.Position;
            var canShowPrompts = !AnyMenuOpen;
            var promptShown = false;

            DrawPropertyMarkers(player, canShowPrompts, ref promptShown);
            DrawBankMarkers(player, canShowPrompts, ref promptShown);

            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var industry = _industryManager.Industries[i];
                var markerPos = GetIndustryMarkerPosition(industry);

                if (playerPos.DistanceToSquared(markerPos) > drawDistanceSq)
                {
                    continue;
                }

                var color = industry.IsSink
                    ? Color.FromArgb(190, 252, 102, 88)
                    : Color.FromArgb(185, 86, 232, 140);

                World.DrawMarker(
                    MarkerType.Cylinder,
                    markerPos,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(_config.MarkerRadius, _config.MarkerRadius, _config.MarkerHeight),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && _nearestIndustry == industry && playerPos.DistanceTo(markerPos) <= IndustryInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format(
                        "Press {0} to open the industry tablet.",
                        KeyName(_controls.Interact))));
                    promptShown = true;
                }
            }
        }

        private bool UpdateCargoOverviewAndIntegrity(Ped player, int now)
        {
            Vehicle cargoVehicle;
            Vehicle driverVehicle;
            VehicleCargoState cargoState;
            if (!TryGetActiveCargoContext(player, out cargoVehicle, out driverVehicle, out cargoState))
            {
                return false;
            }

            if (_cargoDamageDifficultyEnabled)
            {
                UpdateCargoDamageAndLoss(cargoVehicle, driverVehicle, cargoState, now);
            }
            else
            {
                SyncCargoDamageTracking(cargoVehicle, driverVehicle, cargoState);
            }

            var fuelTelemetry = _vehicleFuelSystem.GetTelemetry(
                driverVehicle != null && driverVehicle.Exists() ? driverVehicle : cargoVehicle,
                cargoVehicle);
            DrawCargoOverview(cargoState, fuelTelemetry);
            return true;
        }

        private bool TryGetActiveCargoContext(Ped player, out Vehicle cargoVehicle, out Vehicle driverVehicle, out VehicleCargoState cargoState)
        {
            cargoVehicle = null;
            driverVehicle = null;
            cargoState = null;

            if (player == null || !player.Exists())
            {
                return false;
            }

            cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return false;
            }

            cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null)
            {
                return false;
            }

            if (!cargoState.IsEmpty)
            {
                return true;
            }

            if (driverVehicle == null || !driverVehicle.Exists() || player.CurrentVehicle != driverVehicle)
            {
                return false;
            }

            var definition = _fleetManager.FindDefinition(cargoVehicle.Model);
            return definition != null && !definition.IsTractor;
        }

        private void SyncCargoDamageTracking(Vehicle cargoVehicle, Vehicle driverVehicle, VehicleCargoState cargoState)
        {
            if (cargoVehicle == null || !cargoVehicle.Exists() || cargoState == null || cargoState.IsEmpty)
            {
                return;
            }

            var currentRigHealth = GetActiveRigBodyHealth(driverVehicle, cargoVehicle);
            if (currentRigHealth <= 0.001f)
            {
                cargoState.LastTrackedRigHealth = 0f;
                cargoState.LastTrackedRigSpeed = 0f;
                return;
            }

            cargoState.LastTrackedRigHealth = currentRigHealth;
            cargoState.LastTrackedRigSpeed = GetActiveRigSpeed(driverVehicle, cargoVehicle);
        }

        private void UpdateCargoDamageAndLoss(Vehicle cargoVehicle, Vehicle driverVehicle, VehicleCargoState cargoState, int now)
        {
            if (cargoVehicle == null || !cargoVehicle.Exists() || cargoState == null || cargoState.IsEmpty)
            {
                return;
            }

            var currentRigHealth = GetActiveRigBodyHealth(driverVehicle, cargoVehicle);
            var currentRigSpeed = GetActiveRigSpeed(driverVehicle, cargoVehicle);
            if (currentRigHealth <= 0.001f)
            {
                return;
            }

            if (cargoState.LastTrackedRigHealth <= 0.001f)
            {
                cargoState.LastTrackedRigHealth = currentRigHealth;
                cargoState.LastTrackedRigSpeed = currentRigSpeed;
                return;
            }

            var previousEffectiveDamage = GetEffectiveCargoDamage(cargoState.LastTrackedRigHealth);
            var currentEffectiveDamage = GetEffectiveCargoDamage(currentRigHealth);
            var previousRigSpeed = cargoState.LastTrackedRigSpeed;
            cargoState.LastTrackedRigHealth = currentRigHealth;
            cargoState.LastTrackedRigSpeed = currentRigSpeed;

            var damageDelta = currentEffectiveDamage - previousEffectiveDamage;
            var damageRatioDelta = damageDelta <= 0.05f
                ? 0f
                : ModMath.Clamp01(damageDelta / CargoRigMaxBodyHealth);

            var collisionDamageRatio = 0f;
            if (HasRigCollision(driverVehicle, cargoVehicle) && previousRigSpeed > 0.001f)
            {
                var speedDrop = Math.Max(0f, previousRigSpeed - currentRigSpeed);
                if (speedDrop >= 1.1f)
                {
                    collisionDamageRatio = ModMath.Clamp01(speedDrop / 70f);
                }
            }

            damageRatioDelta = Math.Max(damageRatioDelta, collisionDamageRatio);
            if (damageRatioDelta <= 0.0001f)
            {
                return;
            }

            cargoState.CargoCondition = Math.Max(0f, cargoState.CargoCondition - (damageRatioDelta * CargoConditionLossPerDamageRatio));

            var cargoLossFactor = GetCargoLossFactor(cargoState.CargoType);
            var lostTons = Math.Min(
                cargoState.WeightTons,
                cargoState.WeightTons * damageRatioDelta * CargoLossPerDamageRatio * cargoLossFactor);

            if (lostTons <= 0.0001f)
            {
                return;
            }

            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - lostTons);
            cargoState.TotalLostTons += lostTons;

            if (cargoState.WeightTons <= 0.001f)
            {
                ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                return;
            }

            if (CommodityCatalog.UsesAttachedPropVisual(cargoState.CargoType) ||
                CommodityCatalog.UsesAttachedPropVisual(cargoState.Commodity))
            {
                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            }
        }

        private void DrawCargoOverview(VehicleCargoState cargoState, VehicleFuelTelemetry fuelTelemetry)
        {
            if (cargoState == null)
            {
                return;
            }

            var resolution = Screen.MainWindowResolution;
            var fuelDisplay = VehicleFuelHudFormatter.BuildDisplay(fuelTelemetry, _useMetricSpeedDisplay, _vehicleFuelDifficultyEnabled);
            var hasFuelSecondaryLine = !string.IsNullOrWhiteSpace(fuelDisplay.SecondaryLabel);
            var x = resolution.Width * 0.18f;
            var y = resolution.Height * 0.812f;
            var width = resolution.Width * 0.1075f;
            var height = resolution.Height * (hasFuelSecondaryLine ? 0.128f : 0.112f);
            var isEmpty = cargoState.IsEmpty;
            var quantityRatio = cargoState.FillRatio;
            var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
            var quantityColor = isEmpty
                ? Color.FromArgb(186, 122, 140, 156)
                : ResolveCargoOverviewAccent(cargoState.CargoType);
            var fuelColor = ResolveVehicleFuelColor(fuelDisplay.Severity);
            var conditionColor = isEmpty
                ? Color.FromArgb(186, 122, 140, 156)
                : ResolveCargoConditionColor(conditionRatio);
            var commodityLabel = isEmpty ? "No cargo loaded" : cargoState.Commodity;
            var quantityLabel = isEmpty
                ? "Qty 0% | Empty"
                : string.Format("Qty {0:0}% | {1:0.0}t", quantityRatio * 100f, cargoState.WeightTons);
            var conditionLabel = isEmpty
                ? "Cond n/a"
                : string.Format("Cargo Condition {0} | {1:0}%", GetCargoConditionLabel(conditionRatio), conditionRatio * 100f);
            var contentX = x + 8f;
            var titleY = y + 5f;
            var commodityY = y + (height * 0.17f);
            var quantityTextY = y + (height * 0.33f);
            var quantityBarY = y + (height * 0.44f);
            var fuelTextY = y + (height * 0.56f);
            var fuelDetailY = y + (height * 0.64f);
            var fuelBarY = y + (height * (hasFuelSecondaryLine ? 0.72f : 0.67f));
            var conditionTextY = y + (height * (hasFuelSecondaryLine ? 0.80f : 0.79f));
            var conditionBarY = y + (height * 0.90f);
            var barWidth = width - 16f;
            var barHeight = Math.Max(4f, height * 0.055f);

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, Color.FromArgb(92, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(204, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 4f, quantityColor);

            DrawHudText(
                resolution,
                "CARGO",
                contentX,
                titleY,
                0.30f,
                Color.FromArgb(248, 239, 247, 255),
                GTA.UI.Font.ChaletComprimeCologne);

            DrawHudText(
                resolution,
                commodityLabel,
                contentX,
                commodityY,
                0.19f,
                Color.FromArgb(232, 220, 229, 239),
                GTA.UI.Font.ChaletLondon);

            DrawHudText(
                resolution,
                quantityLabel,
                contentX,
                quantityTextY,
                0.18f,
                Color.FromArgb(224, 214, 223, 233),
                GTA.UI.Font.ChaletLondon);

            DrawCompactLoadingBar(resolution, contentX, quantityBarY, barWidth, barHeight, quantityRatio, quantityColor);

            DrawHudText(
                resolution,
                fuelDisplay.PrimaryLabel,
                contentX,
                fuelTextY,
                0.18f,
                fuelColor,
                GTA.UI.Font.ChaletLondon);

            if (hasFuelSecondaryLine)
            {
                DrawHudText(
                    resolution,
                    fuelDisplay.SecondaryLabel,
                    contentX,
                    fuelDetailY,
                    0.17f,
                    Color.FromArgb(220, 214, 223, 233),
                    GTA.UI.Font.ChaletLondon);
            }

            DrawCompactLoadingBar(resolution, contentX, fuelBarY, barWidth, barHeight, fuelDisplay.FuelRatio, fuelColor);

            DrawHudText(
                resolution,
                conditionLabel,
                contentX,
                conditionTextY,
                0.18f,
                conditionColor,
                GTA.UI.Font.ChaletLondon);

            DrawCompactLoadingBar(resolution, contentX, conditionBarY, barWidth, barHeight, conditionRatio, conditionColor);

        }

        private void DrawActiveVehicleFuelHud(Ped player)
        {
            if (!_vehicleFuelDifficultyEnabled || player == null || !player.Exists())
            {
                return;
            }

            var currentVehicle = player.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
            {
                return;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle))
            {
                return;
            }

            if (currentVehicle.Handle != poweredVehicle.Handle && currentVehicle.Handle != cargoVehicle.Handle)
            {
                return;
            }

            var fuelTelemetry = _vehicleFuelSystem.GetTelemetry(poweredVehicle, cargoVehicle);
            if (fuelTelemetry == null || fuelTelemetry.CapacityLiters <= 0.001f)
            {
                return;
            }

            var fuelDisplay = VehicleFuelHudFormatter.BuildDisplay(fuelTelemetry, _useMetricSpeedDisplay, includeRangeEstimate: true);
            var accentColor = ResolveVehicleFuelColor(fuelDisplay.Severity);
            var resolution = Screen.MainWindowResolution;
            var x = resolution.Width * 0.18f;
            var y = resolution.Height * 0.852f;
            var width = resolution.Width * 0.1075f;
            var height = resolution.Height * 0.076f;
            var contentX = x + 8f;
            var titleY = y + 5f;
            var primaryY = y + (height * 0.30f);
            var secondaryY = y + (height * 0.52f);
            var barY = y + (height * 0.76f);
            var barWidth = width - 16f;
            var barHeight = Math.Max(4f, height * 0.08f);

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, Color.FromArgb(92, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(204, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 4f, accentColor);

            DrawHudText(
                resolution,
                "FUEL",
                contentX,
                titleY,
                0.30f,
                Color.FromArgb(248, 239, 247, 255),
                GTA.UI.Font.ChaletComprimeCologne);

            DrawHudText(
                resolution,
                fuelDisplay.PrimaryLabel,
                contentX,
                primaryY,
                0.18f,
                accentColor,
                GTA.UI.Font.ChaletLondon);

            DrawHudText(
                resolution,
                fuelDisplay.SecondaryLabel,
                contentX,
                secondaryY,
                0.17f,
                Color.FromArgb(220, 214, 223, 233),
                GTA.UI.Font.ChaletLondon);

            DrawCompactLoadingBar(resolution, contentX, barY, barWidth, barHeight, fuelDisplay.FuelRatio, accentColor);
        }

        private static void DrawCompactLoadingBar(Size resolution, float x, float y, float width, float height, float ratio, Color fillColor)
        {
            ratio = ModMath.Clamp01(ratio);
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(165, 6, 10, 15));

            var innerHeight = Math.Max(2f, height - 2f);
            var innerWidth = Math.Max(0f, (width - 2f) * ratio);
            if (innerWidth <= 0f)
            {
                return;
            }

            DrawRect(resolution.Width, resolution.Height, x + 1f, y + 1f, innerWidth, innerHeight, fillColor);
        }

        private static void DrawHudText(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font)
        {
            var coords = ToScriptTextCoords(resolution, x, y);
            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_CENTRE, false);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, coords.X / 1280f, coords.Y / 720f, 0);
        }

        private static float GetActiveRigBodyHealth(Vehicle driverVehicle, Vehicle cargoVehicle)
        {
            var best = CargoRigMaxBodyHealth;
            var found = false;

            if (driverVehicle != null && driverVehicle.Exists())
            {
                best = Math.Min(best, ClampVehicleBodyHealth(driverVehicle.BodyHealth));
                found = true;
            }

            if (cargoVehicle != null && cargoVehicle.Exists())
            {
                best = Math.Min(best, ClampVehicleBodyHealth(cargoVehicle.BodyHealth));
                found = true;
            }

            return found ? best : 0f;
        }

        private static float GetActiveRigSpeed(Vehicle driverVehicle, Vehicle cargoVehicle)
        {
            var best = 0f;

            if (driverVehicle != null && driverVehicle.Exists())
            {
                best = Math.Max(best, GetEntitySpeed(driverVehicle));
            }

            if (cargoVehicle != null && cargoVehicle.Exists())
            {
                best = Math.Max(best, GetEntitySpeed(cargoVehicle));
            }

            return best;
        }

        private static bool HasRigCollision(Vehicle driverVehicle, Vehicle cargoVehicle)
        {
            return HasEntityCollision(driverVehicle)
                || HasEntityCollision(cargoVehicle);
        }

        private static bool HasEntityCollision(Entity entity)
        {
            if (entity == null || !entity.Exists())
            {
                return false;
            }

            try
            {
                return Function.Call<bool>(Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, entity.Handle);
            }
            catch
            {
                return false;
            }
        }

        private static float GetEntitySpeed(Entity entity)
        {
            if (entity == null || !entity.Exists())
            {
                return 0f;
            }

            try
            {
                return Math.Max(0f, Function.Call<float>(Hash.GET_ENTITY_SPEED, entity.Handle));
            }
            catch
            {
                return 0f;
            }
        }

        private static float GetEffectiveCargoDamage(float rigBodyHealth)
        {
            return Math.Max(0f, CargoRigMaxBodyHealth - ClampVehicleBodyHealth(rigBodyHealth) - CargoDamageGraceHealth);
        }

        private static float ClampVehicleBodyHealth(float bodyHealth)
        {
            if (bodyHealth <= 0f)
            {
                return 0f;
            }

            if (bodyHealth >= CargoRigMaxBodyHealth)
            {
                return CargoRigMaxBodyHealth;
            }

            return bodyHealth;
        }

        private static float GetCargoLossFactor(VehicleCargoType cargoType)
        {
            switch (cargoType)
            {
                case VehicleCargoType.Liquid:
                    return 1.25f;
                case VehicleCargoType.Aggregates:
                    return 0.95f;
                case VehicleCargoType.DryBulk:
                case VehicleCargoType.Recyclable:
                    return 0.85f;
                case VehicleCargoType.CraftedGoods:
                    return 0.6f;
                case VehicleCargoType.Refrigeration:
                    return 0.55f;
                case VehicleCargoType.OpenHull:
                    return 0.45f;
                case VehicleCargoType.Wood:
                    return 0.5f;
                case VehicleCargoType.Vehicles:
                    return 0.35f;
                default:
                    return 0.8f;
            }
        }

        private static string GetCargoConditionLabel(float conditionRatio)
        {
            if (conditionRatio >= 0.9f)
            {
                return "Excellent";
            }

            if (conditionRatio >= 0.75f)
            {
                return "Good";
            }

            if (conditionRatio >= 0.55f)
            {
                return "Worn";
            }

            if (conditionRatio >= 0.35f)
            {
                return "Poor";
            }

            return "Critical";
        }

        private static Color ResolveCargoConditionColor(float conditionRatio)
        {
            if (conditionRatio >= 0.75f)
            {
                return Color.FromArgb(228, 108, 196, 142);
            }

            if (conditionRatio >= 0.5f)
            {
                return Color.FromArgb(228, 214, 188, 96);
            }

            if (conditionRatio >= 0.3f)
            {
                return Color.FromArgb(228, 226, 148, 82);
            }

            return Color.FromArgb(228, 214, 92, 92);
        }

        private static Color ResolveVehicleFuelColor(VehicleFuelHudSeverity severity)
        {
            switch (severity)
            {
                case VehicleFuelHudSeverity.Healthy:
                    return Color.FromArgb(220, 116, 202, 138);
                case VehicleFuelHudSeverity.Watch:
                    return Color.FromArgb(224, 220, 180, 80);
                case VehicleFuelHudSeverity.Urgent:
                case VehicleFuelHudSeverity.Empty:
                    return Color.FromArgb(224, 214, 92, 78);
                default:
                    return Color.FromArgb(186, 122, 140, 156);
            }
        }

        private static Color ResolveCargoOverviewAccent(VehicleCargoType cargoType)
        {
            switch (cargoType)
            {
                case VehicleCargoType.Liquid:
                    return Color.FromArgb(228, 88, 150, 214);
                case VehicleCargoType.Aggregates:
                    return Color.FromArgb(228, 168, 144, 92);
                case VehicleCargoType.CraftedGoods:
                    return Color.FromArgb(228, 118, 188, 138);
                case VehicleCargoType.OpenHull:
                    return Color.FromArgb(228, 188, 176, 98);
                case VehicleCargoType.Wood:
                    return Color.FromArgb(228, 152, 118, 76);
                case VehicleCargoType.DryBulk:
                    return Color.FromArgb(228, 197, 152, 82);
                case VehicleCargoType.Refrigeration:
                    return Color.FromArgb(228, 104, 194, 224);
                case VehicleCargoType.Recyclable:
                    return Color.FromArgb(228, 102, 180, 166);
                case VehicleCargoType.Vehicles:
                    return Color.FromArgb(228, 188, 176, 98);
                default:
                    return Color.FromArgb(228, 138, 154, 196);
            }
        }

        private void OpenOfficeMenu()
        {
            var player = Game.Player.Character;
            var office = player != null && player.Exists()
                ? GetOfficeInInteractionRange(player.Position)
                : null;
            if (office == null)
            {
                office = _propertyManager.ActiveOffice ?? _propertyManager.Offices.FirstOrDefault();
            }

            OpenOfficeMenuFor(office);
        }

        private void ToggleModControlMenu()
        {
            if (_modControlMenu.IsOpen)
            {
                _modControlMenu.Close();
                return;
            }

            CloseAllMenus();
            RebuildModControlMenuItems();
            _modControlMenu.Open();
        }

        private void CloseNonOfficeMenus()
        {
            CloseOverviewMenus();
            _companyMapController.Close();
            _npcLogisticsController.Close();
            ClosePropertyMenus();
            _vehicleCargoMenu.Close();
            _upgradeMenu.Close();
            _modControlMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            _industryPurchaseMenu.Close();
            _difficultyMenu.Close();
            _difficultyActionsMenu.Close();
            _difficultyTemplateMenu.Close();
            _debugMenu.Close();
            _debugMissionMenu.Close();
            CloseBankMenu();
            CloseIndustryTablet();
        }

        private void CloseAllMenus()
        {
            CloseOverviewMenus();
            _companyMapController.Close();
            _npcLogisticsController.Close();
            ClosePropertyMenus();
            _modControlMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            _industryPurchaseMenu.Close();
            _difficultyMenu.Close();
            _difficultyActionsMenu.Close();
            _difficultyTemplateMenu.Close();
            _optionsMenu.Close();
            _notificationsMenu.Close();
            _debugMenu.Close();
            _debugMissionMenu.Close();
            _officeMenu.Close();
            CloseBankMenu();
            _vehicleCargoMenu.Close();
            _upgradeMenu.Close();
            CloseIndustryTablet();
        }

        private void RebuildModControlMenuItems()
        {
            _modControlMenu.Title = Text(ModTextKey.MenuGameModControlTitle);
            _modControlMenu.Subtitle = Text(ModTextKey.MenuGameModControlSubtitle);

            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentActivationCaption,
                    OnLeft = ToggleMechanicsFromMenu,
                    OnRight = ToggleMechanicsFromMenu,
                    OnActivate = ToggleMechanicsFromMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowSavingOptions),
                    DetailFactory = CurrentSavingOptionsDetail,
                    OnActivate = OpenSavingOptionsMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowDifficultySettings),
                    DetailFactory = CurrentDifficultySettingsDetail,
                    OnActivate = OpenDifficultyMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowOptions),
                    DetailFactory = CurrentOptionsDetail,
                    OnActivate = OpenOptionsMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Notifications",
                    DetailFactory = CurrentNotificationsDetail,
                    OnActivate = OpenNotificationsMenu,
                },
            };

            if (ShouldShowCruiseControlMenuItem())
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = CurrentCruiseControlCaption,
                    DetailFactory = CurrentCruiseControlDetail,
                    OnActivate = ToggleCruiseControlFromMenu,
                });
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonClose),
                OnActivate = () => _modControlMenu.Close(),
            });

            _modControlMenu.SetItems(items.ToArray());
        }

        private bool ShouldShowCruiseControlMenuItem()
        {
            Vehicle vehicle;
            return TryGetCruiseControlVehicle(Game.Player.Character, out vehicle);
        }

        private void ToggleCruiseControlFromMenu()
        {
            if (_cruiseControlEnabled)
            {
                DisableCruiseControl(true);
                return;
            }

            Vehicle vehicle;
            if (!TryGetCruiseControlVehicle(Game.Player.Character, out vehicle))
            {
                ShowStatus("Drive a vehicle before enabling cruise control.", 3000);
                return;
            }

            var targetSpeed = GetCruiseControlForwardSpeed(vehicle);
            if (targetSpeed < 1.5f)
            {
                ShowStatus("Accelerate before enabling cruise control.", 3000);
                return;
            }

            _cruiseControlEnabled = true;
            _cruiseControlTargetSpeedMps = targetSpeed;
            if (_modControlMenu.IsOpen)
            {
                RebuildModControlMenuItems();
            }

            ShowStatus(string.Format("Cruise control set to {0}.", FormatCruiseControlSpeed(_cruiseControlTargetSpeedMps)), 3000);
        }

        private void DisableCruiseControl(bool showStatus)
        {
            var wasEnabled = _cruiseControlEnabled;
            _cruiseControlEnabled = false;
            _cruiseControlTargetSpeedMps = 0f;

            if (_modControlMenu.IsOpen)
            {
                RebuildModControlMenuItems();
            }

            if (showStatus && wasEnabled)
            {
                ShowStatus("Cruise control disabled.", 2500);
            }
        }

        private void UpdateCruiseControl(Ped player)
        {
            if (!_cruiseControlEnabled)
            {
                return;
            }

            Vehicle vehicle;
            if (!TryGetCruiseControlVehicle(player, out vehicle))
            {
                DisableCruiseControl(false);
                return;
            }

            if (GetCruiseControlInput(72) > 0.04f)
            {
                DisableCruiseControl(true);
                return;
            }

            if (_cruiseControlTargetSpeedMps <= 0.1f)
            {
                DisableCruiseControl(false);
                return;
            }

            try
            {
                var currentForwardSpeed = GetCruiseControlForwardSpeed(vehicle);
                if (Math.Abs(currentForwardSpeed - _cruiseControlTargetSpeedMps) > 0.75f)
                {
                    Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, vehicle.Handle, _cruiseControlTargetSpeedMps);
                }
            }
            catch
            {
                DisableCruiseControl(false);
            }
        }

        private static bool TryGetCruiseControlVehicle(Ped player, out Vehicle vehicle)
        {
            vehicle = null;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                return false;
            }

            vehicle = player.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            try
            {
                var driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);
                return driver != null && driver.Exists() && driver.Handle == player.Handle;
            }
            catch
            {
                return false;
            }
        }

        private static float GetCruiseControlInput(int controlId)
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

        private static float GetCruiseControlForwardSpeed(Vehicle vehicle)
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

        private string FormatCruiseControlSpeed(float speedMetersPerSecond)
        {
            var speed = Math.Max(0f, speedMetersPerSecond);
            return _useMetricSpeedDisplay
                ? string.Format("{0:0} km/h", speed * 3.6f)
                : string.Format("{0:0} mph", speed * 2.2369363f);
        }

        private void RebuildSavingOptionsMenuItems()
        {
            _savingOptionsMenu.Title = Text(ModTextKey.MenuSavingOptionsTitle);
            _savingOptionsMenu.Subtitle = Text(ModTextKey.MenuSavingOptionsSubtitle, GetCurrentSaveLabel());
            _savingOptionsMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowCreateNewSave),
                    DetailFactory = () => Text(ModTextKey.DetailCreateNewSave, "<name>", SavegamesDirectoryName),
                    OnActivate = PromptForNewSave,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowLoadSave),
                    DetailFactory = () => Text(ModTextKey.DetailLoadSave),
                    OnActivate = () => OpenSaveSlotsMenu(SaveSlotMenuAction.Load),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowDeleteSave),
                    DetailFactory = () => Text(ModTextKey.DetailDeleteSave),
                    OnActivate = () => OpenSaveSlotsMenu(SaveSlotMenuAction.Delete),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowSaveGame),
                    DetailFactory = CurrentSaveGameDetail,
                    OnActivate = SaveCurrentNamedGame,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    OnActivate = ReturnToModControlMenu,
                },
            });
        }

        private string CurrentCruiseControlCaption()
        {
            return Text(
                ModTextKey.RowCruiseControl,
                _cruiseControlEnabled
                    ? FormatCruiseControlSpeed(_cruiseControlTargetSpeedMps)
                    : Text(ModTextKey.CommonOff));
        }

        private string CurrentCruiseControlDetail()
        {
            return Text(ModTextKey.DetailCruiseControl);
        }

        private void RebuildNewSaveSetupMenuItems()
        {
            _newSaveSetupMenu.Title = Text(ModTextKey.MenuDifficultyTitle);
            _newSaveSetupMenu.Subtitle = Text(ModTextKey.MenuNewSaveSetupSubtitle, _pendingSaveName);
            var items = new List<OfficeMenuItem>();
            items.AddRange(BuildDifficultyMenuRootItems(DifficultyProfileTarget.Pending));
            items.AddRange(BuildDifficultyMenuCoreItems(DifficultyProfileTarget.Pending));
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.RowCreateSave),
                DetailFactory = () => Text(ModTextKey.DetailCreateSave, _pendingSaveName),
                OnActivate = FinalizeNewSave,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = CancelNewSaveSetup,
            });
            _newSaveSetupMenu.SetItems(items);
        }

        private void RebuildSaveSlotsMenuItems()
        {
            var entries = GetAvailableNamedSaves();
            var items = new List<OfficeMenuItem>();

            _saveSlotsMenu.Title = _saveSlotMenuAction == SaveSlotMenuAction.Load
                ? Text(ModTextKey.MenuSaveSlotsLoadTitle)
                : Text(ModTextKey.MenuSaveSlotsDeleteTitle);
            _saveSlotsMenu.Subtitle = entries.Count == 1
                ? Text(ModTextKey.MenuSaveSlotsSingle)
                : Text(ModTextKey.MenuSaveSlotsMany, entries.Count);

            if (entries.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowNoSavesFound),
                    DetailFactory = () => Text(ModTextKey.DetailNoSavesFound, SavegamesDirectoryName),
                });
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var detail = BuildSaveSlotDetail(entry);
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => entry.DisplayName,
                        DetailFactory = () => detail,
                        OnActivate = _saveSlotMenuAction == SaveSlotMenuAction.Load
                            ? (Action)(() => LoadNamedSave(entry))
                            : (() => ConfirmOrDeleteNamedSave(entry)),
                    });
                }
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = ReturnToSavingOptionsMenu,
            });

            _saveSlotsMenu.SetItems(items);
        }

        private void RebuildOptionsMenuItems()
        {
            _optionsMenu.Title = Text(ModTextKey.MenuOptionsTitle);
            _optionsMenu.Subtitle = Text(ModTextKey.MenuOptionsSubtitle);
            _optionsMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentLanguageCaption,
                    DetailFactory = CurrentLanguageDetail,
                    OnLeft = () => ChangeLanguage(-1),
                    OnRight = () => ChangeLanguage(1),
                    OnActivate = () => ChangeLanguage(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentSpeedUnitCaption,
                    DetailFactory = CurrentSpeedUnitDetail,
                    OnLeft = () => ChangeSpeedUnit(-1),
                    OnRight = () => ChangeSpeedUnit(1),
                    OnActivate = () => ChangeSpeedUnit(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentColorblindModeCaption,
                    DetailFactory = CurrentColorblindModeDetail,
                    OnLeft = () => ChangeColorblindMode(-1),
                    OnRight = () => ChangeColorblindMode(1),
                    OnActivate = () => ChangeColorblindMode(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    OnActivate = ReturnToModControlMenu,
                },
            });
        }

        private void RebuildNotificationsMenuItems()
        {
            _notificationsMenu.Title = "Notifications";
            _notificationsMenu.Subtitle = "Alert rules and route notices";
            _notificationsMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentRentAlertCaption,
                    DetailFactory = CurrentRentAlertDetail,
                    OnLeft = () => CycleRentAlertLeadTime(-1),
                    OnRight = () => CycleRentAlertLeadTime(1),
                    OnActivate = () => CycleRentAlertLeadTime(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentContractAlertCaption,
                    DetailFactory = CurrentContractAlertDetail,
                    OnLeft = () => CycleContractAlertLeadTime(-1),
                    OnRight = () => CycleContractAlertLeadTime(1),
                    OnActivate = () => CycleContractAlertLeadTime(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentFleetAlertCaption,
                    DetailFactory = CurrentFleetAlertDetail,
                    OnLeft = () => CycleFleetAlertMode(-1),
                    OnRight = () => CycleFleetAlertMode(1),
                    OnActivate = () => CycleFleetAlertMode(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentTerritoryAlertCaption,
                    DetailFactory = CurrentTerritoryAlertDetail,
                    OnLeft = () => CycleTerritoryAlertMode(-1),
                    OnRight = () => CycleTerritoryAlertMode(1),
                    OnActivate = () => CycleTerritoryAlertMode(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Office NPC deliveries",
                    DetailFactory = () => "Show loading and unloading alerts for hired office logistics routes.",
                    CheckboxStateFactory = () => _npcLogisticsManager.OfficeDeliveryNotificationsEnabled,
                    OnActivate = ToggleOfficeNpcDeliveryNotifications,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    OnActivate = ReturnToModControlMenu,
                },
            });
        }

        private void OpenSavingOptionsMenu()
        {
            _modControlMenu.Close();
            RebuildSavingOptionsMenuItems();
            _savingOptionsMenu.Open();
        }

        private void OpenOptionsMenu()
        {
            _modControlMenu.Close();
            RebuildOptionsMenuItems();
            _optionsMenu.Open();
        }

        private void OpenNotificationsMenu()
        {
            _modControlMenu.Close();
            RebuildNotificationsMenuItems();
            _notificationsMenu.Open();
        }

        private void OpenSaveSlotsMenu(SaveSlotMenuAction action)
        {
            _saveSlotMenuAction = action;
            _pendingDeleteSavePath = null;
            _savingOptionsMenu.Close();
            RebuildSaveSlotsMenuItems();
            _saveSlotsMenu.Open();
        }

        private void ReturnToModControlMenu()
        {
            _difficultyMenu.Close();
            _difficultyActionsMenu.Close();
            _difficultyTemplateMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            _optionsMenu.Close();
            _notificationsMenu.Close();
            RebuildModControlMenuItems();
            _modControlMenu.Open();
        }

        private void ReturnToSavingOptionsMenu()
        {
            _pendingDeleteSavePath = null;
            _difficultyActionsMenu.Close();
            _difficultyTemplateMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            RebuildModControlMenuItems();
            RebuildSavingOptionsMenuItems();
            _savingOptionsMenu.Open();
        }

        private void CancelNewSaveSetup()
        {
            _pendingSaveName = string.Empty;
            ReturnToSavingOptionsMenu();
        }

        private void RebuildIndustryPurchaseMenuItems()
        {
            var industry = _pendingIndustryPurchaseIndustry;
            if (industry == null)
            {
                _industryPurchaseMenu.Title = "Buy Industry";
                _industryPurchaseMenu.Subtitle = "No industry selected";
                _industryPurchaseMenu.SetItems(new[]
                {
                    new OfficeMenuItem
                    {
                        CaptionFactory = () => "Close",
                        OnActivate = CancelIndustryPurchase,
                    },
                });
                return;
            }

            _industryPurchaseMenu.Title = "Buy Industry";
            _industryPurchaseMenu.Subtitle = string.Format("Buy {0} for {1}?", industry.Name, ModFormatting.FormatMoney(industry.IndustryPrice));
            _industryPurchaseMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Yes",
                    DetailFactory = () => GetIndustryPurchasePromptDetail(industry),
                    OnActivate = ConfirmIndustryPurchase,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "No",
                    DetailFactory = () => "Return to the previous page.",
                    OnActivate = CancelIndustryPurchase,
                },
            });
        }

        private string GetIndustryPurchasePromptDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var detail = string.Format("Deduct {0} and unlock upgrades.", ModFormatting.FormatMoney(industry.IndustryPrice));
            if (industry.IndustryOwnerCut > 0f)
            {
                detail += string.Format(" Removes the {0:0}% owner cut.", industry.IndustryOwnerCut * 100f);
            }

            if (_profit < industry.IndustryPrice)
            {
                detail += string.Format(" Need {0} more.", ModFormatting.FormatMoney(industry.IndustryPrice - _profit));
            }

            return detail;
        }

        private void OpenIndustryPurchaseMenu(Industry industry, IndustryPurchaseMenuReturnTarget returnTarget)
        {
            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            if (!_industryManager.RequiresIndustryPurchase(industry))
            {
                ShowStatus(_industryManager.IsIndustryOwnedForGameplay(industry)
                    ? string.Format("{0} is already owned.", industry.Name)
                    : "Industry pricing is disabled for this save.");
                return;
            }

            _pendingIndustryPurchaseIndustry = industry;
            _industryPurchaseMenuReturnTarget = returnTarget;
            _upgradeMenu.Close();
            CloseIndustryTablet();
            RebuildIndustryPurchaseMenuItems();
            _industryPurchaseMenu.Open();
        }

        private void ConfirmIndustryPurchase()
        {
            var industry = _pendingIndustryPurchaseIndustry;
            if (industry == null)
            {
                CancelIndustryPurchase();
                return;
            }

            float cost;
            string result;
            var balanceBefore = _profit;
            if (!industry.TryPurchase(ref _profit, out cost, out result))
            {
                ShowStatus(result);
                RebuildIndustryPurchaseMenuItems();
                return;
            }

            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Purchased industry {0}", industry.Name));

            result = AppendIndustryPurchasePermitGrantResult(industry, result);
            _territoryManager.OnIndustryAccessChanged(industry);
            _blipLifecycleManager.Refresh();
            _tabletStateStore.MarkBalanceDirty();
            _tabletStateStore.MarkNetworkDirty();
            ReevaluatePlayerSuccesses(true);

            ShowStatus(result, 4000);
            ReturnFromIndustryPurchaseMenu();
        }

        private void CancelIndustryPurchase()
        {
            ReturnFromIndustryPurchaseMenu();
        }

        private void ReturnFromIndustryPurchaseMenu()
        {
            var returnTarget = _industryPurchaseMenuReturnTarget;
            var industry = _pendingIndustryPurchaseIndustry;

            _industryPurchaseMenu.Close();
            _industryPurchaseMenuReturnTarget = IndustryPurchaseMenuReturnTarget.None;
            _pendingIndustryPurchaseIndustry = null;

            if (industry == null)
            {
                return;
            }

            if (returnTarget == IndustryPurchaseMenuReturnTarget.Tablet)
            {
                TryReopenIndustryTablet(industry);
                return;
            }

            if (returnTarget == IndustryPurchaseMenuReturnTarget.UpgradeMenu)
            {
                _menuIndustry = industry;
                RebuildUpgradeMenuItems();
                _upgradeMenu.Open();
            }
        }

        private bool TryReopenIndustryTablet(Industry industry)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || industry == null)
            {
                return false;
            }

            _menuIndustry = industry;
            if (player.Position.DistanceTo(GetIndustryMarkerPosition(industry)) > IndustryInteractionDistance)
            {
                return false;
            }

            _tabletStateStore.MarkAllDirty();
            _tabletShellController.OpenIndustry(industry);
            return true;
        }

        private void RebuildDifficultyMenuItems()
        {
            _difficultyMenu.Title = Text(ModTextKey.MenuDifficultyTitle);
            _difficultyMenu.Subtitle = _difficultySettingsLocked
                ? Text(ModTextKey.MenuDifficultyLockedSubtitle)
                : Text(ModTextKey.MenuDifficultySubtitle);

            var items = new List<OfficeMenuItem>();
            items.AddRange(BuildDifficultyMenuRootItems(DifficultyProfileTarget.Live));
            items.AddRange(BuildDifficultyMenuCoreItems(DifficultyProfileTarget.Live));
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = ReturnToModControlMenu,
            });
            _difficultyMenu.SetItems(items);
        }

        private void OpenDifficultyMenu()
        {
            _modControlMenu.Close();
            RebuildDifficultyMenuItems();
            _difficultyMenu.Open();
        }

        private void ToggleDebugMenu()
        {
            if (_debugMenu.IsOpen || _debugMissionMenu.IsOpen)
            {
                _debugMenu.Close();
                _debugMissionMenu.Close();
                return;
            }

            CloseAllMenus();
            RebuildDebugMenuItems();
            _debugMenu.Open();
        }

        private void OpenDebugMissionMenu()
        {
            _debugMenu.Close();
            RebuildDebugMissionMenuItems();
            _debugMissionMenu.Open();
        }

        private void ReturnToDebugMenu()
        {
            _debugMissionMenu.Close();
            RebuildDebugMenuItems();
            _debugMenu.Open();
        }

        private void CloseOverviewMenus()
        {
            _tabletShellController.Close();
        }

        private void OpenTabletNetworkApp()
        {
            CloseAllMenus();
            _tabletStateStore.MarkAllDirty();
            _tabletShellController.OpenHome();
        }

        private string Text(string key)
        {
            return ModLocalization.Service.Get(key);
        }

        private string Text(string key, params object[] args)
        {
            return ModLocalization.Service.Format(key, args);
        }

        private void ApplyPresentationSettings(bool rebuildMenus)
        {
            ModLocalization.Service.SetLanguage(_language);
            AccessibilityTheme.Service.SetMode(_colorblindMode);

            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            if (!rebuildMenus)
            {
                return;
            }

            RebuildModControlMenuItems();
            RebuildSavingOptionsMenuItems();
            RebuildNewSaveSetupMenuItems();
            RebuildSaveSlotsMenuItems();
            RebuildDifficultyMenuItems();
            RebuildOptionsMenuItems();
        }

        private string FormatColoredToggle(bool enabled)
        {
            return enabled
                ? string.Format("~g~{0}~s~", Text(ModTextKey.CommonOn))
                : string.Format("~r~{0}~s~", Text(ModTextKey.CommonOff));
        }

        private string CurrentActivationCaption()
        {
            return Text(ModTextKey.RowActivate, FormatColoredToggle(_modMechanicsEnabled));
        }

        private string CurrentSavingOptionsDetail()
        {
            return Text(ModTextKey.DetailSavingOptions, GetCurrentSaveLabel());
        }

        private string CurrentDifficultySettingsDetail()
        {
            var profile = CaptureLiveDifficultyProfile();
            return _difficultySettingsLocked
                ? Text(
                    ModTextKey.DetailDifficultyLocked,
                    FormatEconomyDifficultyPreset(profile.EconomyDifficultyPreset),
                    FormatWeeklyWageDifficulty(profile.NpcWeeklyWageDifficulty),
                    FormatNpcRouteLimitValue(profile.NpcRouteLimit),
                    DifficultySettingsSummaryFormatter.BuildBooleanCount(profile))
                : Text(
                    ModTextKey.DetailDifficultyUnlocked,
                    FormatEconomyDifficultyPreset(profile.EconomyDifficultyPreset),
                    FormatWeeklyWageDifficulty(profile.NpcWeeklyWageDifficulty),
                    FormatNpcRouteLimitValue(profile.NpcRouteLimit),
                    DifficultySettingsSummaryFormatter.BuildBooleanCount(profile));
        }

        private string CurrentOptionsDetail()
        {
            return Text(ModTextKey.DetailOptions);
        }

        private string CurrentNotificationsDetail()
        {
            var alertRules = EnsureAlertRules();
            return string.Format(
                "Rent {0} | Contracts {1} | Fleet {2} | Territory {3} | Office NPC deliveries {4}",
                FormatAlertLeadTimeLabel(alertRules.RentLeadTime),
                FormatAlertLeadTimeLabel(alertRules.ContractLeadTime),
                FormatFleetAlertModeLabel(alertRules.FleetMode),
                FormatTerritoryAlertModeLabel(alertRules.TerritoryMode),
                _npcLogisticsManager.OfficeDeliveryNotificationsEnabled ? Text(ModTextKey.CommonOn) : Text(ModTextKey.CommonOff));
        }

        private string CurrentSaveGameDetail()
        {
            NamedSaveEntry activeSave;
            if (!TryGetActiveNamedSave(out activeSave))
            {
                return Text(ModTextKey.DetailNeedNamedSave);
            }

            return Text(ModTextKey.DetailSaveGame, activeSave.DisplayName);
        }

        private string CurrentStartingBalanceCaption()
        {
            return Text(ModTextKey.RowStartingBalance, ModFormatting.FormatMoney(GetSelectedStartingBalance()));
        }

        private string CurrentVehicleFuelSettingCaption()
        {
            return string.Format("{0}: {1}", Text(ModTextKey.RowVehicleFuel), FormatColoredToggle(_vehicleFuelDifficultyEnabled));
        }

        private string CurrentNpcWeeklyWageDifficultyCaption()
        {
            return Text(ModTextKey.RowNpcWeeklyWages, FormatWeeklyWageDifficulty(_npcWeeklyWageDifficulty));
        }

        private string CurrentEconomyDifficultyPresetCaption()
        {
            return Text(ModTextKey.RowEconomyPreset, FormatEconomyDifficultyPreset(_economyDifficultyPreset));
        }

        private string CurrentEconomyDifficultyPresetDetail()
        {
            return BuildEconomyDifficultyPresetDetail(_economyDifficultyPreset);
        }

        private string CurrentNpcWeeklyWageDifficultyDetail()
        {
            return BuildNpcWeeklyWageDifficultyDetail(_npcWeeklyWageDifficulty);
        }

        private string CurrentPendingEconomyDifficultyPresetCaption()
        {
            return Text(ModTextKey.RowEconomyPreset, FormatEconomyDifficultyPreset(_pendingEconomyDifficultyPreset));
        }

        private string CurrentPendingEconomyDifficultyPresetDetail()
        {
            return BuildEconomyDifficultyPresetDetail(_pendingEconomyDifficultyPreset);
        }

        private string CurrentPendingNpcWeeklyWageDifficultyCaption()
        {
            return Text(ModTextKey.RowNpcWeeklyWages, FormatWeeklyWageDifficulty(_pendingNpcWeeklyWageDifficulty));
        }

        private string CurrentPendingNpcWeeklyWageDifficultyDetail()
        {
            return BuildNpcWeeklyWageDifficultyDetail(_pendingNpcWeeklyWageDifficulty);
        }

        private string CurrentLanguageCaption()
        {
            return Text(ModTextKey.RowLanguage, GetLanguageDisplayName(_language));
        }

        private string CurrentLanguageDetail()
        {
            return _language == ModLanguage.English
                ? Text(ModTextKey.DetailLanguageEnglishFallback)
                : Text(ModTextKey.DetailLanguage);
        }

        private string CurrentSpeedUnitCaption()
        {
            return Text(ModTextKey.RowSpeedUnit, GetSpeedUnitDisplayName(_useMetricSpeedDisplay));
        }

        private string CurrentSpeedUnitDetail()
        {
            return Text(ModTextKey.DetailSpeedUnit);
        }

        private string CurrentColorblindModeCaption()
        {
            return Text(ModTextKey.RowColorblindMode, GetColorblindModeDisplayName(_colorblindMode));
        }

        private string CurrentColorblindModeDetail()
        {
            return Text(ModTextKey.DetailColorblindMode);
        }

        private string CurrentNpcRouteLimitCaption()
        {
            return Text(ModTextKey.RowNpcRouteLimit, FormatNpcRouteLimitValue(_npcRouteLimit));
        }

        private string CurrentPendingNpcRouteLimitCaption()
        {
            return Text(ModTextKey.RowNpcRouteLimit, FormatNpcRouteLimitValue(_pendingNpcRouteLimit));
        }

        private string CurrentNpcRouteLimitDetail()
        {
            return Text(ModTextKey.DetailNpcRouteLimit);
        }

        private string CurrentOfficeNpcLimitDetail()
        {
            return BuildOfficeNpcLimitDetail(_officeNpcLimitDifficultyEnabled);
        }

        private string CurrentPendingOfficeNpcLimitDetail()
        {
            return BuildOfficeNpcLimitDetail(_pendingOfficeNpcLimitDifficultyEnabled);
        }

        private string BuildOfficeNpcLimitDetail(bool enabled)
        {
            return enabled
            ? Text(ModTextKey.DetailOfficeNpcLimitEnabled)
            : Text(ModTextKey.DetailOfficeNpcLimitDisabled);
        }

        private void ToggleMechanicsFromMenu()
        {
            SetModMechanicsEnabled(!_modMechanicsEnabled, true);
            RebuildModControlMenuItems();
        }

        private void ChangeGameModMode(int delta)
        {
            var next = ((int)_gameModMode + delta + 2) % 2;
            _gameModMode = (GameModMode)next;
        }

        private void ChangeStartingBalanceSelection(int delta)
        {
            if (StartingBalanceOptions.Length == 0)
            {
                return;
            }

            var count = StartingBalanceOptions.Length;
            _selectedStartingBalanceIndex = (_selectedStartingBalanceIndex + delta + count) % count;
        }

        private void ChangeLanguage(int delta)
        {
            if (SelectableLanguages.Length == 0)
            {
                return;
            }

            var currentIndex = Array.IndexOf(SelectableLanguages, _language);
            var nextIndex = currentIndex < 0
                ? (delta < 0 ? SelectableLanguages.Length - 1 : 0)
                : (currentIndex + delta + SelectableLanguages.Length) % SelectableLanguages.Length;
            _language = SelectableLanguages[nextIndex];
            ApplyPresentationSettings(true);
            ShowStatus(Text(ModTextKey.DetailLanguageChanged, GetLanguageDisplayName(_language)));
        }

        private void ChangeSpeedUnit(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            _useMetricSpeedDisplay = !_useMetricSpeedDisplay;
            RebuildModControlMenuItems();
            RebuildOptionsMenuItems();
            ShowStatus(Text(ModTextKey.DetailSpeedUnitChanged, GetSpeedUnitDisplayName(_useMetricSpeedDisplay)));
        }

        private void ChangeColorblindMode(int delta)
        {
            if (ColorblindModeOptions.Length == 0)
            {
                return;
            }

            var currentIndex = Array.IndexOf(ColorblindModeOptions, _colorblindMode);
            var nextIndex = currentIndex < 0
                ? 0
                : (currentIndex + delta + ColorblindModeOptions.Length) % ColorblindModeOptions.Length;
            _colorblindMode = ColorblindModeOptions[nextIndex];
            ApplyPresentationSettings(true);
            ShowStatus(Text(ModTextKey.DetailColorblindChanged, GetColorblindModeDisplayName(_colorblindMode)));
        }

        private void ToggleOfficeNpcDeliveryNotifications()
        {
            _npcLogisticsManager.SetOfficeDeliveryNotificationsEnabled(!_npcLogisticsManager.OfficeDeliveryNotificationsEnabled);
            RebuildNotificationsMenuItems();
        }

        private void TogglePendingVehicleFuelSetting()
        {
            _pendingVehicleFuelDifficultyEnabled = !_pendingVehicleFuelDifficultyEnabled;
        }

        private void TogglePendingCargoWeightPowerSetting()
        {
            _pendingCargoWeightPowerDifficultyEnabled = !_pendingCargoWeightPowerDifficultyEnabled;
        }

        private void TogglePendingCargoDamageSetting()
        {
            _pendingCargoDamageDifficultyEnabled = !_pendingCargoDamageDifficultyEnabled;
        }

        private void TogglePendingIndustryPricingSetting()
        {
            _pendingIndustryPricingDifficultyEnabled = !_pendingIndustryPricingDifficultyEnabled;
        }

        private void TogglePendingLicensingSetting()
        {
            _pendingLicensingDifficultyEnabled = !_pendingLicensingDifficultyEnabled;
        }

        private void TogglePendingCorridorRestrictionSetting()
        {
            _pendingCorridorRestrictionDifficultyEnabled = !_pendingCorridorRestrictionDifficultyEnabled;
        }

        private void TogglePendingReputationSetting()
        {
            _pendingReputationDifficultyEnabled = !_pendingReputationDifficultyEnabled;
        }

        private void TogglePendingOfficeGarageLimitSetting()
        {
            _pendingOfficeGarageLimitDifficultyEnabled = !_pendingOfficeGarageLimitDifficultyEnabled;
        }

        private void TogglePendingOfficeNpcLimitSetting()
        {
            _pendingOfficeNpcLimitDifficultyEnabled = !_pendingOfficeNpcLimitDifficultyEnabled;
        }

        private void ChangePendingNpcRouteLimit(int delta)
        {
            _pendingNpcRouteLimit = ClampNpcRouteLimit(_pendingNpcRouteLimit + delta);
        }

        private void ChangePendingEconomyDifficultyPreset(int delta)
        {
            _pendingEconomyDifficultyPreset = OffsetEconomyDifficultyPreset(_pendingEconomyDifficultyPreset, delta);
        }

        private void ChangePendingNpcWeeklyWageDifficulty(int delta)
        {
            _pendingNpcWeeklyWageDifficulty = OffsetWeeklyWageDifficulty(_pendingNpcWeeklyWageDifficulty, delta);
        }

        private void ToggleVehicleFuelSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _vehicleFuelDifficultyEnabled = !_vehicleFuelDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleCargoWeightPowerSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _cargoWeightPowerDifficultyEnabled = !_cargoWeightPowerDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleCargoDamageSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _cargoDamageDifficultyEnabled = !_cargoDamageDifficultyEnabled;
        }

        private void ToggleIndustryPricingSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _industryPricingDifficultyEnabled = !_industryPricingDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleLicensingSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _licensingDifficultyEnabled = !_licensingDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleCorridorRestrictionSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _corridorRestrictionDifficultyEnabled = !_corridorRestrictionDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleReputationSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _reputationDifficultyEnabled = !_reputationDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleOfficeGarageLimitSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _officeGarageLimitDifficultyEnabled = !_officeGarageLimitDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ToggleOfficeNpcLimitSetting()
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _officeNpcLimitDifficultyEnabled = !_officeNpcLimitDifficultyEnabled;
            ApplyDifficultySettingsToSystems();
        }

        private void ChangeNpcRouteLimit(int delta)
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _npcRouteLimit = ClampNpcRouteLimit(_npcRouteLimit + delta);
            ApplyDifficultySettingsToSystems();
        }

        private void ChangeNpcWeeklyWageDifficulty(int delta)
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _npcWeeklyWageDifficulty = OffsetWeeklyWageDifficulty(_npcWeeklyWageDifficulty, delta);
            ApplyDifficultySettingsToSystems();
        }

        private void ChangeEconomyDifficultyPreset(int delta)
        {
            if (_difficultySettingsLocked)
            {
                ShowDifficultySettingsLockedStatus();
                return;
            }

            _economyDifficultyPreset = OffsetEconomyDifficultyPreset(_economyDifficultyPreset, delta);
            ApplyDifficultySettingsToSystems();
        }

        private void ShowDifficultySettingsLockedStatus()
        {
            ShowStatus(Text(ModTextKey.DetailDifficultySettingsLockedStatus));
        }

        private void ToggleIndustryPersistenceFromMenu()
        {
            _industryPersistenceEnabled = !_industryPersistenceEnabled;
            if (_industryPersistenceEnabled)
            {
                var restored = TryLoadIndustryPersistence(true);
                if (!restored)
                {
                    ShowStatus(Text(ModTextKey.DetailPersistenceEnabled));
                }
            }
            else
            {
                ShowStatus(Text(ModTextKey.DetailPersistenceDisabled));
            }

            RebuildModControlMenuItems();
        }

        private void ApplyDifficultySettingsToSystems()
        {
            _industryManager.SetIndustryPricingDifficultyEnabled(_industryPricingDifficultyEnabled);
            _industryManager.SetLicensingDifficultyEnabled(_licensingDifficultyEnabled);
            _industryManager.SetEconomyDifficultyPreset(_economyDifficultyPreset);
            _propertyManager.SetOfficeGarageLimitEnforced(_officeGarageLimitDifficultyEnabled);
            _vehicleFuelSystem.SetDifficultyEnabled(_vehicleFuelDifficultyEnabled);
            _vehicleLoadPowerService.SetDifficultyEnabled(_cargoWeightPowerDifficultyEnabled);
            _npcLogisticsManager.SetRouteLimit(_npcRouteLimit);
            _npcLogisticsManager.SetWeeklyWageDifficulty(_npcWeeklyWageDifficulty);
            _territoryManager.SetReputationEnabled(_reputationDifficultyEnabled);
            _territoryManager.SetCorridorRestrictionEnabled(_corridorRestrictionDifficultyEnabled);
            _territoryManager.RefreshState();
            if (_modMechanicsEnabled)
            {
                _blipLifecycleManager.Refresh();
            }

            if (_upgradeMenu.IsOpen)
            {
                RebuildUpgradeMenuItems();
            }
        }

        private string BuildNpcWeeklyWageDifficultyDetail(NpcWeeklyWageDifficulty difficulty)
        {
            var rookie = _npcLogisticsManager.DriverTiers.FirstOrDefault(tier => string.Equals(tier.Id, "Rookie", StringComparison.OrdinalIgnoreCase));
            var professional = _npcLogisticsManager.DriverTiers.FirstOrDefault(tier => string.Equals(tier.Id, "Professional", StringComparison.OrdinalIgnoreCase));
            var veteran = _npcLogisticsManager.DriverTiers.FirstOrDefault(tier => string.Equals(tier.Id, "Veteran", StringComparison.OrdinalIgnoreCase));

            return string.Format(
                Text(ModTextKey.DetailNpcWeeklyPayroll),
                rookie != null ? ModFormatting.FormatMoney(rookie.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f),
                professional != null ? ModFormatting.FormatMoney(professional.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f),
                veteran != null ? ModFormatting.FormatMoney(veteran.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f));
        }

        private string FormatWeeklyWageDifficulty(NpcWeeklyWageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcWeeklyWageDifficulty.Casual:
                    return Text(ModTextKey.ValueDifficultyCasual);
                case NpcWeeklyWageDifficulty.Hardcore:
                    return Text(ModTextKey.ValueDifficultyHardcore);
                default:
                    return Text(ModTextKey.ValueDifficultyStandard);
            }
        }

        private string BuildEconomyDifficultyPresetDetail(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return Text(ModTextKey.DetailEconomyPresetCasual);
                case EconomyDifficultyPreset.Hardcore:
                    return Text(ModTextKey.DetailEconomyPresetHardcore);
                case EconomyDifficultyPreset.Impossible:
                    return Text(ModTextKey.DetailEconomyPresetImpossible);
                default:
                    return Text(ModTextKey.DetailEconomyPresetStandard);
            }
        }

        private string FormatEconomyDifficultyPreset(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return Text(ModTextKey.ValueDifficultyCasual);
                case EconomyDifficultyPreset.Hardcore:
                    return Text(ModTextKey.ValueDifficultyHardcore);
                case EconomyDifficultyPreset.Impossible:
                    return Text(ModTextKey.ValueDifficultyImpossible);
                default:
                    return Text(ModTextKey.ValueDifficultyStandard);
            }
        }

        private string GetLanguageDisplayName(ModLanguage language)
        {
            switch (language)
            {
                case ModLanguage.French:
                    return Text(ModTextKey.ValueLanguageFrench);
                case ModLanguage.Italian:
                    return Text(ModTextKey.ValueLanguageItalian);
                case ModLanguage.Spanish:
                    return Text(ModTextKey.ValueLanguageSpanish);
                case ModLanguage.Russian:
                    return Text(ModTextKey.ValueLanguageRussian);
                case ModLanguage.Japanese:
                    return Text(ModTextKey.ValueLanguageJapanese);
                case ModLanguage.Chinese:
                    return Text(ModTextKey.ValueLanguageChinese);
                case ModLanguage.Hindi:
                    return Text(ModTextKey.ValueLanguageHindi);
                case ModLanguage.Portuguese:
                    return Text(ModTextKey.ValueLanguagePortuguese);
                case ModLanguage.Turkish:
                    return Text(ModTextKey.ValueLanguageTurkish);
                default:
                    return Text(ModTextKey.ValueLanguageEnglishFallback);
            }
        }

        private string GetColorblindModeDisplayName(ColorblindMode mode)
        {
            switch (mode)
            {
                case ColorblindMode.Deuteranopia:
                    return Text(ModTextKey.ValueColorblindDeuteranopia);
                case ColorblindMode.Protanopia:
                    return Text(ModTextKey.ValueColorblindProtanopia);
                case ColorblindMode.Tritanopia:
                    return Text(ModTextKey.ValueColorblindTritanopia);
                default:
                    return Text(ModTextKey.ValueColorblindOff);
            }
        }

        private string GetSpeedUnitDisplayName(bool useMetric)
        {
            return Text(useMetric ? ModTextKey.ValueUnitMetric : ModTextKey.ValueUnitImperial);
        }

        private static int ClampNpcRouteLimit(int value)
        {
            return Math.Max(MinNpcRouteLimit, Math.Min(MaxNpcRouteLimit, value));
        }

        private static string FormatNpcRouteLimitValue(int routeLimit)
        {
            return ClampNpcRouteLimit(routeLimit).ToString();
        }

        private static NpcWeeklyWageDifficulty OffsetWeeklyWageDifficulty(NpcWeeklyWageDifficulty current, int delta)
        {
            const int count = 3;
            var next = ((int)current + delta + count) % count;
            return (NpcWeeklyWageDifficulty)next;
        }

        private static EconomyDifficultyPreset OffsetEconomyDifficultyPreset(EconomyDifficultyPreset current, int delta)
        {
            var count = Enum.GetValues(typeof(EconomyDifficultyPreset)).Length;
            var next = ((int)current + delta + count) % count;
            return (EconomyDifficultyPreset)next;
        }

        private string PurchaseContractorPermitFromOverview(Industry industry)
        {
            if (industry == null)
            {
                return "No industry selected.";
            }

            if (!_licensingDifficultyEnabled)
            {
                return "Licensing system is disabled for this save.";
            }

            float cost;
            string result;
            var hadPermit = industry.HasContractorPermit;
            var balanceBefore = _profit;
            industry.TryPurchaseContractorPermit(ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.PermitOrLicence, string.Format("Contractor permit for {0}", industry.Name));
            if (!hadPermit && industry.HasContractorPermit)
            {
                _territoryManager.OnIndustryAccessChanged(industry);
                _blipLifecycleManager.Refresh();
            }

            _tabletStateStore.MarkBalanceDirty();
            _tabletStateStore.MarkNetworkDirty();

            return result;
        }

        private string PurchaseContractorPermitFromTablet(Industry industry)
        {
            var result = PurchaseContractorPermitFromOverview(industry);
            if (!string.IsNullOrWhiteSpace(result))
            {
                ShowStatus(result);
            }

            return result;
        }

        private string PurchaseIndustryFromTablet(Industry industry)
        {
            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return "No industry selected.";
            }

            if (!_industryManager.RequiresIndustryPurchase(industry))
            {
                var message = _industryManager.IsIndustryOwnedForGameplay(industry)
                    ? string.Format("{0} is already owned.", industry.Name)
                    : "Industry pricing is disabled for this save.";
                ShowStatus(message);
                return message;
            }

            float cost;
            string result;
            var balanceBefore = _profit;
            if (!industry.TryPurchase(ref _profit, out cost, out result))
            {
                ShowStatus(result);
                _tabletStateStore.MarkBalanceDirty();
                _tabletStateStore.MarkNetworkDirty();
                return result;
            }

            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Purchased industry {0}", industry.Name));

            result = AppendIndustryPurchasePermitGrantResult(industry, result);
            _territoryManager.OnIndustryAccessChanged(industry);
            _blipLifecycleManager.Refresh();
            _tabletStateStore.MarkBalanceDirty();
            _tabletStateStore.MarkNetworkDirty();
            ReevaluatePlayerSuccesses(true);
            ShowStatus(result, 4000);
            return result;
        }

        private void AddIndustryGpsRouteFromTablet(Industry industry)
        {
            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            var markerPosition = GetIndustryMarkerPosition(industry);
            Function.Call(Hash.SET_NEW_WAYPOINT, markerPosition.X, markerPosition.Y);
            ShowStatus(string.Format("GPS route added to {0}.", industry.Name), 4000);
        }

        private void ClearGpsRouteFromTablet()
        {
            Function.Call(Hash.SET_WAYPOINT_OFF);
            ShowStatus("GPS route cleared.", 4000);
        }

        private void SetModMechanicsEnabled(bool enabled, bool keepControlMenuOpen)
        {
            if (_modMechanicsEnabled == enabled)
            {
                return;
            }

            _modMechanicsEnabled = enabled;

            if (enabled)
            {
                _lastIndustryTickMs = Game.GameTime;
                _lastNearestProbeMs = 0;
                _lastBlipRefreshMs = 0;
                CreateMapBlips();
                ShowStatus(Text(ModTextKey.DetailMechanicsEnabled));
                return;
            }

            _cargoTransferController.ClearState();
            _industryOutputPropManager.DestroyAll();
            CloseOverviewMenus();
            _companyMapController.Close();
            _officeMenu.Close();
            _upgradeMenu.Close();
            _difficultyMenu.Close();
            _industryPurchaseMenu.Close();
            _debugMenu.Close();
            CloseIndustryTablet();
            DestroyMapBlips();
            _lastIndustryTickMs = Game.GameTime;

            if (!keepControlMenuOpen)
            {
                _modControlMenu.Close();
            }

            ShowStatus(Text(ModTextKey.DetailMechanicsDisabled));
        }

        private void RebuildDebugMenuItems()
        {
            _debugMenuProvider.PopulateRootMenu(_debugMenu, new DebugMenuCallbacks
            {
                IndustryCaption = CurrentDebugIndustryCaption,
                IndustryDetail = CurrentDebugIndustryDetail,
                ResourceCaption = CurrentDebugResourceCaption,
                ResourceDetail = CurrentDebugResourceDetail,
                ResourceAmountCaption = CurrentDebugResourceAmountCaption,
                SelectPreviousResourceAmount = () => ChangeDebugResourceAmountSelection(-1),
                SelectNextResourceAmount = () => ChangeDebugResourceAmountSelection(1),
                SelectPreviousResource = () => ChangeDebugResourceSelection(-1),
                SelectNextResource = () => ChangeDebugResourceSelection(1),
                MoneyAmountCaption = CurrentDebugMoneyAmountCaption,
                SelectPreviousMoneyAmount = () => ChangeDebugMoneyAmountSelection(-1),
                SelectNextMoneyAmount = () => ChangeDebugMoneyAmountSelection(1),
                DistrictCaption = CurrentDebugDistrictCaption,
                DistrictDetail = CurrentDebugDistrictDetail,
                SelectPreviousDistrict = () => ChangeDebugDistrictSelection(-1),
                SelectNextDistrict = () => ChangeDebugDistrictSelection(1),
                DistrictReputationAmountCaption = CurrentDebugDistrictReputationAmountCaption,
                SelectPreviousDistrictReputationAmount = () => ChangeDebugDistrictReputationAmountSelection(-1),
                SelectNextDistrictReputationAmount = () => ChangeDebugDistrictReputationAmountSelection(1),
                DistrictStateCaption = CurrentDebugDistrictStateCaption,
                SelectPreviousDistrictState = () => ChangeDebugDistrictStateSelection(-1),
                SelectNextDistrictState = () => ChangeDebugDistrictStateSelection(1),
                MissionBoardDetail = CurrentDebugMissionBoardDetail,
                OpenMissionMenu = OpenDebugMissionMenu,
                SelectedMoneyAmount = GetSelectedDebugMoneyAmount,
                SelectedDistrictState = GetSelectedDebugDistrictState,
                SelectedDistrictReputationAmount = GetSelectedDebugDistrictReputationAmount,
                AddMoney = AddDebugMoney,
                ApplyDistrictStateToAll = ApplySelectedDebugDistrictStateToAll,
                IncreaseDistrictReputation = () => AdjustDebugDistrictReputation(1f),
                DecreaseDistrictReputation = () => AdjustDebugDistrictReputation(-1f),
                AddSelectedResourceToNearbyIndustry = AddSelectedDebugResourceToNearbyIndustry,
                DeleteResolvedVehicleCargo = DeleteResolvedVehicleCargo,
                DeleteCurrentVehicle = DeleteCurrentVehicle,
                FillNearbyIndustryInputs = FillNearbyIndustryInputs,
                EmptyNearbyIndustryInputs = EmptyNearbyIndustryInputs,
                FillNearbyIndustryOutputs = FillNearbyIndustryOutputs,
                EmptyNearbyIndustryOutputs = EmptyNearbyIndustryOutputs,
                MultiplyNearbyIndustryProductionRate = MultiplyNearbyIndustryProductionRate,
                CloseMenu = () => _debugMenu.Close(),
            });
        }

        private void RebuildDebugMissionMenuItems()
        {
            _debugMenuProvider.PopulateMissionMenu(_debugMissionMenu, new DebugMissionMenuCallbacks
            {
                Definitions = _specialMissionManager != null ? _specialMissionManager.Definitions : Enumerable.Empty<SpecialMissionDefinition>(),
                Listings = _specialMissionManager != null ? _specialMissionManager.GetMissionListings() : Enumerable.Empty<SpecialMissionListing>(),
                MissionCaptionFactory = BuildDebugMissionCaption,
                MissionDetailFactory = BuildDebugMissionDetail,
                TriggerMission = TriggerDebugMission,
                ReturnToDebugMenu = ReturnToDebugMenu,
            });
        }

        private string CurrentDebugMissionBoardDetail()
        {
            if (_specialMissionManager == null)
            {
                return "Mission manager unavailable.";
            }

            var warningCount = _specialMissionManager.Catalog != null
                ? _specialMissionManager.Catalog.ValidationMessages.Count
                : 0;
            var missionCount = _specialMissionManager.Definitions.Count();
            if (missionCount <= 0)
            {
                return warningCount > 0
                    ? string.Format("No mission board entries available. Grow district presence or add XML mission packs to scripts/LSOL_Config/missions or scripts/LSOL_Addons/*/content/missions. {0} validation warning(s).", warningCount)
                    : "No mission board entries available. Grow district presence or add XML mission packs to scripts/LSOL_Config/missions or scripts/LSOL_Addons/*/content/missions.";
            }

            if (_specialMissionManager.HasActiveMission)
            {
                return warningCount > 0
                    ? string.Format("{0} mission board entries available | Active: {1} | {2} validation warning(s).", missionCount, _specialMissionManager.ActiveMissionName, warningCount)
                    : string.Format("{0} mission board entries available | Active: {1}.", missionCount, _specialMissionManager.ActiveMissionName);
            }

            return warningCount > 0
                ? string.Format("{0} mission board entries available | Force-start any mission | {1} validation warning(s).", missionCount, warningCount)
                : string.Format("{0} mission board entries available. Force-start any mission regardless of unlock or cooldown.", missionCount);
        }

        private static string BuildDebugMissionCaption(SpecialMissionDefinition definition, SpecialMissionListing listing)
        {
            if (definition == null)
            {
                return "Unavailable mission";
            }

            return listing != null && listing.IsActive
                ? string.Format("{0} ~y~[LIVE]~s~", definition.Name)
                : definition.Name;
        }

        private static string BuildDebugMissionDetail(SpecialMissionDefinition definition, SpecialMissionListing listing)
        {
            if (definition == null)
            {
                return "Mission definition unavailable.";
            }

            var status = listing != null && listing.IsActive
                ? "Currently active. Selecting here restarts the mission."
                : listing != null && !listing.CanAccept && !string.IsNullOrWhiteSpace(listing.AvailabilityDetail)
                    ? string.Format("{0} Selecting here ignores that restriction.", listing.AvailabilityDetail)
                    : "Ready for testing. Selecting here ignores unlock and cooldown checks.";
            return string.Format("{0} | Reward {1} | {2}", definition.Category, ModFormatting.FormatMoney(definition.Reward), status);
        }

        private void TriggerDebugMission(string missionId)
        {
            if (_specialMissionManager == null)
            {
                ShowStatus("Mission manager unavailable.");
                return;
            }

            string detail;
            if (_specialMissionManager.TryForceStartMission(missionId, out detail))
            {
                CloseAllMenus();
                ShowStatus(detail, 4500);
                return;
            }

            ShowStatus(detail, 4500);
            RebuildDebugMissionMenuItems();
        }

        private string CurrentDebugIndustryCaption()
        {
            var industry = GetDebugNearbyIndustry();
            return industry == null
                ? "Nearby Industry: none"
                : string.Format("Nearby Industry: {0}", industry.Name);
        }

        private string CurrentDebugIndustryDetail()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                return "Move within an industry marker to target it.";
            }

            return string.Format(
                "Rate {0:0.0} cyc/h | Inputs {1} | Outputs {2}",
                industry.ProductionRate,
                industry.Inputs.Count,
                industry.Outputs.Count);
        }

        private string CurrentDebugResourceCaption()
        {
            var resource = GetSelectedDebugResource(GetDebugNearbyIndustry());
            return string.IsNullOrWhiteSpace(resource)
                ? "Resource: < none >"
                : string.Format("Resource: < {0} >", resource);
        }

        private string CurrentDebugResourceDetail()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                return "Move next to an industry to change the resource target.";
            }

            var resource = GetSelectedDebugResource(industry);
            if (string.IsNullOrWhiteSpace(resource))
            {
                return "No input or output resource is available for this industry.";
            }

            var currentStock = resource.Equals("Omega", StringComparison.OrdinalIgnoreCase)
                ? industry.OmegaStorage
                : industry.GetStock(resource);
            return string.Format("Current stock: {0:0.0}t", currentStock);
        }

        private string CurrentDebugResourceAmountCaption()
        {
            return string.Format("Amount: < {0:0.0}t >", GetSelectedDebugResourceAmountTons());
        }

        private void RebuildOfficeMenuItems()
        {
            _officeMenu.Title = "Office Menu";
            _officeMenu.Subtitle = BuildOfficeMenuSubtitle();
            _officeMenu.SetItems(BuildOfficeMenuItems());
        }

        private string CurrentNpcHiringDetail()
        {
            var routeCount = _npcLogisticsManager.Contracts.Count;
            if (_npcRouteLimit <= 0)
            {
                return routeCount > 0
                    ? string.Format("Hiring new NPCs is disabled. {0} active logistics route{1} can still be managed.", routeCount, routeCount == 1 ? string.Empty : "s")
                    : "Hiring new NPCs is disabled in Options.";
            }

            var hireBlockedReason = GetNpcHiringBlockedReason();
            if (!string.IsNullOrWhiteSpace(hireBlockedReason))
            {
                return hireBlockedReason;
            }

            if (_officeNpcLimitDifficultyEnabled)
            {
                var capacity = GetActiveOfficeNpcCapacity();
                return string.Format(
                    "{0} hired NPC{1} active | Capacity {2}. Open the tablet-style dispatch manager.",
                    routeCount,
                    routeCount == 1 ? string.Empty : "s",
                    Math.Max(0, capacity));
            }

            return routeCount == 1
                ? "1 active logistics route. Open the tablet-style NPC manager."
                : string.Format("{0} active logistics routes. Open the tablet-style NPC manager.", routeCount);
        }

        private string GetNpcHiringBlockedReason()
        {
            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                return reason;
            }

            if (!_officeNpcLimitDifficultyEnabled)
            {
                return string.Empty;
            }

            var office = _propertyManager.ActiveOffice;
            if (office == null)
            {
                return "Activate an office before hiring NPCs.";
            }

            var capacity = GetActiveOfficeNpcCapacity();
            if (capacity <= 0)
            {
                return "Install an NPC operations module at the active office to hire NPCs.";
            }

            var hiredNpcCount = _npcLogisticsManager.Contracts.Count;
            if (hiredNpcCount >= capacity)
            {
                return string.Format("NPC operations capacity reached: {0}/{1} hired NPCs.", hiredNpcCount, capacity);
            }

            return string.Empty;
        }

        private int GetActiveOfficeNpcCapacity()
        {
            var office = _propertyManager.ActiveOffice;
            if (office == null)
            {
                return 0;
            }

            return (int)Math.Floor(_propertyManager.GetOfficeObjectFunctionCapacity(office.OfficeId, OfficeObjectFunction.Npc) + 0.001f);
        }

        private string CurrentCompanyMapDetail()
        {
            var controlledDistricts = _territoryManager.GetControlledDistrictCount();
            var corridorCount = _territoryManager.GetActiveCorridorCount();
            var securedSupportSites = _territoryManager.GetDepotIndustries().Count(industry =>
            {
                var siteState = _territoryManager.GetSiteState(industry);
                return siteState != null && siteState.ControlLevel != TerritoryControlLevel.None;
            });

            return string.Format(
                "{0} districts anchored | {1} corridors active | {2} depots or yards secured.",
                controlledDistricts,
                corridorCount,
                securedSupportSites);
        }

        private void OpenNpcHiringMenu()
        {
            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            if (_npcRouteLimit <= 0 && _npcLogisticsManager.Contracts.Count == 0)
            {
                ShowStatus("Hiring NPC is disabled in Options.");
                return;
            }

            _officeMenu.Close();
            _npcLogisticsController.OpenRootMenu();
        }

        private void OpenCompanyMapMenu()
        {
            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            CloseAllMenus();
            _companyMapController.OpenNetworkView();
        }

        private void OpenCompanyMapMenuFromTablet()
        {
            CloseAllMenus();
            _companyMapController.OpenNetworkView(ReturnToTabletHomeFromCompanyMap);
        }

        private void OpenNetworkRoutePlannerMapFromTablet()
        {
            CloseAllMenus();
            _companyMapController.OpenNetworkView(() => ReturnToTabletRouteFromCompanyMap(TabletAppIds.Network, "planner"));
        }

        private void OpenAnalyticsRoutePlannerMapFromTablet()
        {
            CloseAllMenus();
            _companyMapController.OpenNetworkView(() => ReturnToTabletRouteFromCompanyMap(TabletAppIds.Analytics, "route-planner"));
        }

        private void OpenCompanyDistrictViewFromTablet()
        {
            CloseAllMenus();
            _companyMapController.OpenDistrictView(ReturnToTabletHomeFromCompanyMap);
        }

        private void OpenCompanyDepotViewFromTablet()
        {
            CloseAllMenus();
            _companyMapController.OpenDepotView(ReturnToTabletHomeFromCompanyMap);
        }

        private void ReturnToTabletHomeFromCompanyMap()
        {
            _tabletStateStore.MarkAllDirty();
            _tabletShellController.OpenHome();
        }

        private void ReturnToTabletRouteFromCompanyMap(string appId, string pageId, object payload = null)
        {
            _tabletStateStore.MarkAllDirty();
            _tabletShellController.Navigate(appId, pageId, payload);
        }

        private void OpenNpcPlannerDraftFromTablet(NpcLogisticsRouteDefinition routeDefinition)
        {
            if (routeDefinition == null)
            {
                ShowStatus("Planner lane unavailable.");
                return;
            }

            CloseAllMenus();
            _npcLogisticsController.OpenPlannerDraft(routeDefinition);
        }

        private int GetSecuredSupportSiteCount()
        {
            if (_territoryManager == null)
            {
                return 0;
            }

            return _territoryManager.GetDepotIndustries().Count(industry =>
            {
                var siteState = _territoryManager.GetSiteState(industry);
                return siteState != null && siteState.ControlLevel != TerritoryControlLevel.None;
            });
        }

        private void AddProfit(float amount)
        {
            AddProfit(CompanyFinanceCategory.OtherIncome, amount, string.Empty);
        }

        private void AddProfit(
            CompanyFinanceCategory category,
            float amount,
            string description,
            int routeContractId = 0,
            string routeLabel = null,
            string playerContractId = null,
            string shipperKey = null,
            string districtName = null)
        {
            if (amount <= 0f)
            {
                return;
            }

            _profit += amount;
            _financeTracker.RecordIncome(category, amount, GetCurrentInGameWeekMinute(), description, routeContractId, routeLabel, playerContractId, shipperKey, districtName);
            SyncPlayerSuccessBalance();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }
        }

        private void DeductProfit(float amount)
        {
            DeductProfit(CompanyFinanceCategory.OtherExpense, amount, string.Empty);
        }

        private void DeductProfit(CompanyFinanceCategory category, float amount, string description, int routeContractId = 0, string routeLabel = null)
        {
            if (amount <= 0f)
            {
                return;
            }

            _profit -= amount;
            _financeTracker.RecordExpense(category, amount, GetCurrentInGameWeekMinute(), description, routeContractId, routeLabel);
            SyncPlayerSuccessBalance();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }
        }

        private void RecordTrackedBalanceDelta(float previousBalance, CompanyFinanceCategory expenseCategory, string expenseDescription, CompanyFinanceCategory incomeCategory = CompanyFinanceCategory.OtherIncome, string incomeDescription = null)
        {
            var delta = _profit - previousBalance;
            if (Math.Abs(delta) <= 0.001f)
            {
                return;
            }

            if (delta > 0f)
            {
                _financeTracker.RecordIncome(incomeCategory, delta, GetCurrentInGameWeekMinute(), incomeDescription ?? string.Empty);
            }
            else
            {
                _financeTracker.RecordExpense(expenseCategory, Math.Abs(delta), GetCurrentInGameWeekMinute(), expenseDescription ?? string.Empty);
            }

            SyncPlayerSuccessBalance();

            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }
        }

        private void SyncPlayerSuccessBalance(bool notifyUnlocks = true, bool markBalanceDirty = false)
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            var balanceChanged = !_hasPlayerSuccessBalanceSync || Math.Abs(_profit - _lastPlayerSuccessBalanceSync) > 0.001f;
            _playerSuccessTracker.UpdateCompanyBalance(_profit, notifyUnlocks);
            _lastPlayerSuccessBalanceSync = _profit;
            _hasPlayerSuccessBalanceSync = true;

            if (balanceChanged && markBalanceDirty && _tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }
        }

        private void ReevaluatePlayerSuccesses(bool notifyUnlocks = true)
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            SyncPlayerSuccessBalance(notifyUnlocks);
            _playerSuccessTracker.ReevaluateCurrentState(notifyUnlocks);
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }
        }

        private void RecordPlayerSuccessDeliveryProgress(
            Industry destinationIndustry,
            string commodity,
            float deliveredTons,
            string sourceIndustryId,
            string sourceDistrictName,
            bool completedDelivery,
            bool isCleanDelivery)
        {
            if (_playerSuccessTracker == null)
            {
                if (_specialMissionManager == null)
                {
                    return;
                }
            }

            if (_playerSuccessTracker != null)
            {
                _playerSuccessTracker.RecordDeliveryProgress(commodity, deliveredTons, completedDelivery, isCleanDelivery);
            }

            if (_specialMissionManager != null)
            {
                _specialMissionManager.NotifyPlayerDelivery(
                    destinationIndustry,
                    commodity,
                    deliveredTons,
                    sourceIndustryId,
                    sourceDistrictName,
                    completedDelivery,
                    isCleanDelivery);
            }

            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }
        }

        private void RecordNpcSuccessDeliveryProgress(string commodity, float deliveredTons, bool completedDelivery, bool isCleanDelivery)
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            _playerSuccessTracker.RecordDeliveryProgress(
                commodity,
                deliveredTons,
                completedDelivery,
                isCleanDelivery,
                DeliveryProgressSource.Npc);
        }

        private void HandlePlayerSuccessNpcContractsChanged()
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            _playerSuccessTracker.NotifyNpcContractsChanged();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }
        }

        private void HandlePlayerSuccessMissionCompleted()
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            _playerSuccessTracker.RecordSpecialMissionCompleted();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }
        }

        private void RecordPlayerSuccessEmergencyServiceUsage()
        {
            if (_playerSuccessTracker == null)
            {
                return;
            }

            _playerSuccessTracker.RecordEmergencyServiceUsage();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }
        }

        private string AcquireDistrictLicenseFromOffice(string districtName)
        {
            var balanceBefore = _profit;
            float cost;
            string result;
            _territoryManager.TryAcquireDistrictLicense(districtName, ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.PermitOrLicence, string.Format("District charter for {0}", string.IsNullOrWhiteSpace(districtName) ? "district" : districtName));
            _blipLifecycleManager.Refresh();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            return result;
        }

        private string SecureSupportSiteFromOffice(Industry industry)
        {
            var balanceBefore = _profit;
            float cost;
            string result;
            _territoryManager.TryAcquireDepot(industry, ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Secured support site at {0}", industry != null ? industry.Name : "support site"));
            _blipLifecycleManager.Refresh();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            return result;
        }

        private string AssignSupportCrewFromOffice(Industry industry)
        {
            var balanceBefore = _profit;
            float cost;
            string result;
            _territoryManager.TryAssignCrew(industry, ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Assigned support crew at {0}", industry != null ? industry.Name : "support site"));
            _blipLifecycleManager.Refresh();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            return result;
        }

        private string HireSupportStaffFromOffice(Industry industry, DepotStaffRole staffRole)
        {
            var balanceBefore = _profit;
            float cost;
            string result;
            _territoryManager.TryHireDepotStaff(industry, staffRole, ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Hired {0} staff at {1}", staffRole, industry != null ? industry.Name : "support site"));
            _blipLifecycleManager.Refresh();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            return result;
        }

        private string ToggleServiceSiteOperatorFromTablet(Industry industry)
        {
            if (_territoryManager == null)
            {
                return "Territory manager unavailable.";
            }

            var assignOperator = !_territoryManager.HasServiceSiteOperatorAssigned(industry);
            string result;
            var changed = _territoryManager.TrySetServiceSiteOperatorAssigned(industry, assignOperator, out result);
            if (changed)
            {
                if (_tabletStateStore != null)
                {
                    _tabletStateStore.MarkAllDirty();
                }

                RebuildOfficeMenuItems();
            }

            return result;
        }

        private string SetDepotSpecializationFromOffice(Industry industry, DepotSpecialization specialization)
        {
            var balanceBefore = _profit;
            float cost;
            string result;
            _territoryManager.TrySetDepotSpecialization(industry, specialization, ref _profit, out cost, out result);
            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Depot specialization at {0}", industry != null ? industry.Name : "support site"));
            _blipLifecycleManager.Refresh();
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            return result;
        }

        private void RebuildVehicleCargoMenuItems()
        {
            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Cargo Filter: < {0} >", _vehicleSpawnController.SelectedFilter.ToDisplayName()),
                    OnLeft = () => ChangeFilter(-1),
                    OnRight = () => ChangeFilter(1),
                    OnActivate = RefreshFilteredVehicles,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Cargo / Trailer: < {0} >", _vehicleSpawnController.CurrentVehicleCaption),
                    DetailFactory = _vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership
                        ? (Func<string>)BuildCommercialDealershipVehicleSelectionDetail
                        : null,
                    OnLeft = () => ChangeVehicleSelection(-1),
                    OnRight = () => ChangeVehicleSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Truck: < {0} >", _vehicleSpawnController.CurrentTractorCaption),
                    DetailFactory = _vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership
                        ? (Func<string>)BuildCommercialDealershipTruckSelectionDetail
                        : null,
                    OnLeft = () => ChangeTractorSelection(-1),
                    OnRight = () => ChangeTractorSelection(1),
                },
            };

            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = CurrentCommercialDealershipAcquisitionCaption,
                    DetailFactory = BuildCommercialDealershipAcquisitionModeDetail,
                    OnLeft = () => ChangeCommercialDealershipAcquisitionMode(-1),
                    OnRight = () => ChangeCommercialDealershipAcquisitionMode(1),
                    OnActivate = () => ChangeCommercialDealershipAcquisitionMode(1),
                });
            }

            items.Add(new OfficeMenuItem
            {
                IsSeparator = true,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = CurrentVehicleSpawnerActionCaption,
                DetailFactory = CurrentVehicleSpawnerActionDetail,
                OnActivate = SpawnSelectedVehicle,
            });

            _vehicleCargoMenu.SetItems(items);
        }

        private void OpenVehicleCargoMenu()
        {
            OpenVehicleCargoMenu(VehicleCargoMenuContext.Office);
        }

        private void OpenIndustryVehicleSpawnerMenu()
        {
            OpenVehicleCargoMenu(VehicleCargoMenuContext.Industry);
        }

        private void OpenVehicleCargoMenu(VehicleCargoMenuContext context)
        {
            _vehicleCargoMenuContext = context;
            if (context == VehicleCargoMenuContext.Industry)
            {
                CloseIndustryTablet();
            }
            else if (context == VehicleCargoMenuContext.CommercialDealership)
            {
                _vehicleCargoMenu.Title = "Trucks Dealership";
                _vehicleCargoMenu.Subtitle = "Purchase trucks and/or trailers";
            }
            else
            {
                _vehicleCargoMenu.Title = "Vehicle & Cargo Type";
                _vehicleCargoMenu.Subtitle = "Choose cargo filter, vehicle, and spawn";
                _officeMenu.Close();
            }

            _vehicleSpawnController.RefreshFilteredVehicles();
            RebuildVehicleCargoMenuItems();
            _vehicleCargoMenu.Open();
        }

        private void ReturnToOfficeMenu()
        {
            _vehicleCargoMenu.Close();
            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private void ReturnFromVehicleCargoMenu()
        {
            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.Industry)
            {
                ReturnToIndustryTablet();
                return;
            }

            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership)
            {
                _vehicleCargoMenu.Close();
                return;
            }

            ReturnToOfficeMenu();
        }

        private void ReturnToIndustryTablet()
        {
            _vehicleCargoMenu.Close();

            var player = Game.Player.Character;
            if (player == null || !player.Exists() || _menuIndustry == null)
            {
                return;
            }

            if (player.Position.DistanceTo(GetIndustryMarkerPosition(_menuIndustry)) > IndustryInteractionDistance)
            {
                ShowStatus("Move closer to the industry marker to reopen operations.");
                return;
            }

            _tabletStateStore.MarkAllDirty();
            _tabletShellController.OpenIndustry(_menuIndustry);
        }

        private string CurrentVehicleSpawnerSelectionDetail()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            var selectedTruck = selectedVehicle != null && !selectedVehicle.IsTrailer
                ? null
                : _vehicleSpawnController.SelectedTractorDefinition;
            return string.Format(
                "{0} | Cargo / Trailer {1} | Truck {2}",
                _vehicleSpawnController.SelectedFilter.ToDisplayName(),
                _vehicleSpawnController.CurrentVehicleCaption,
                selectedTruck != null ? _vehicleSpawnController.CurrentTractorCaption : "None");
        }

        private string CurrentVehicleSpawnerActionDetail()
        {
            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership)
            {
                return BuildCommercialDealershipPurchaseDetail();
            }

            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.Industry)
            {
                return _vehicleSpawnController.HasAnySelection
                    ? "Spawn the selected truck, trailer, or combined rig at this industry pad."
                    : "Select a truck and/or trailer first.";
            }

            return _vehicleSpawnController.HasAnySelection
                ? "Spawn the selected truck, trailer, or combined rig at the office lot."
                : "Select a truck and/or trailer first.";
        }

        private bool IsCommercialDealershipVehicleAvailableToPlayer(VehicleDefinition definition)
        {
            return !IsCommercialDealershipPhantomLocked(definition);
        }

        private bool IsCommercialDealershipPhantomLocked(VehicleDefinition definition)
        {
            return _vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership
                && IsPhantomCommercialVehicle(definition)
                && (_playerSuccessTracker == null || !_playerSuccessTracker.HasRoadVeteranUnlocked);
        }

        private static bool IsPhantomCommercialVehicle(VehicleDefinition definition)
        {
            return definition != null
                && (string.Equals(definition.ModelName, PhantomCommercialModelName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(definition.Id, PhantomCommercialVehicleId, StringComparison.OrdinalIgnoreCase));
        }

        private string CurrentIndustryVehicleSpawnerDetail()
        {
            if (_menuIndustry == null)
            {
                return "No target industry selected.";
            }

            if (!_menuIndustry.VehicleSpawnPosition.HasValue)
            {
                return "No vehicle spawn configured for this industry.";
            }

            return CurrentVehicleSpawnerSelectionDetail();
        }

        private void ChangeWorkerIndex(int delta)
        {
            _workerSpawnController.ChangeSelection(delta);
        }

        private void ApplyWorkerModel()
        {
            _workerSpawnController.ApplySelectedWorkerModel(message => ShowStatus(message), Wait);
        }

        private void ChangeFilter(int delta)
        {
            _vehicleSpawnController.ChangeFilter(delta);
            RefreshVehicleSelectionMenus();
        }

        private void RefreshFilteredVehicles()
        {
            _vehicleSpawnController.RefreshFilteredVehicles();
            RefreshVehicleSelectionMenus();
        }

        private void ChangeVehicleSelection(int delta)
        {
            _vehicleSpawnController.ChangeVehicleSelection(delta);
            RefreshVehicleSelectionMenus();
        }

        private void ChangeTractorSelection(int delta)
        {
            _vehicleSpawnController.ChangeTractorSelection(delta);
            RefreshVehicleSelectionMenus();
        }

        private void RefreshVehicleSelectionMenus()
        {
            if (_vehicleCargoMenu != null && _vehicleCargoMenu.IsOpen)
            {
                RebuildVehicleCargoMenuItems();
            }

            if (_officeMenu != null)
            {
                RebuildOfficeMenuItems();
            }
        }

        private void ChangeDebugResourceSelection(int delta)
        {
            var resourceOptions = GetDebugResourceOptions(GetDebugNearbyIndustry());
            if (resourceOptions.Count == 0)
            {
                _selectedDebugResourceIndex = 0;
                return;
            }

            _selectedDebugResourceIndex = (_selectedDebugResourceIndex + delta + resourceOptions.Count) % resourceOptions.Count;
        }

        private string CurrentDebugMoneyAmountCaption()
        {
            return string.Format("Money amount: < {0} >", ModFormatting.FormatMoney(GetSelectedDebugMoneyAmount()));
        }

        private string CurrentDebugDistrictCaption()
        {
            var district = GetSelectedDebugDistrict();
            return string.IsNullOrWhiteSpace(district)
                ? "District: < none >"
                : string.Format("District: < {0} >", district);
        }

        private string CurrentDebugDistrictDetail()
        {
            if (_territoryManager == null)
            {
                return "Territory manager unavailable.";
            }

            var districtName = GetSelectedDebugDistrict();
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return "No district data is loaded.";
            }

            var district = _territoryManager.GetDistrictState(districtName);
            if (district == null)
            {
                return "Selected district state is unavailable.";
            }

            var debugOffset = _territoryManager.GetDistrictReputationDebugOffset(districtName);
            return string.Format(
                "{0} | Influence {1:0}% | Reputation {2:0.0} | Debug offset {3:+0.0;-0.0;0.0}",
                string.IsNullOrWhiteSpace(district.ReputationLabel) ? "Unknown" : district.ReputationLabel,
                district.InfluenceRatio * 100f,
                district.ReputationScore,
                debugOffset);
        }

        private string CurrentDebugDistrictReputationAmountCaption()
        {
            return string.Format("District rep amount: < {0:0.#} >", GetSelectedDebugDistrictReputationAmount());
        }

        private string CurrentDebugDistrictStateCaption()
        {
            return string.Format("All districts state: < {0} >", GetSelectedDebugDistrictState());
        }

        private void ChangeDebugResourceAmountSelection(int delta)
        {
            _selectedDebugResourceAmountIndex = (_selectedDebugResourceAmountIndex + delta + DebugResourceAmountOptionsTons.Length) % DebugResourceAmountOptionsTons.Length;
        }

        private void ChangeDebugMoneyAmountSelection(int delta)
        {
            _selectedDebugMoneyAmountIndex = (_selectedDebugMoneyAmountIndex + delta + DebugMoneyAmountOptions.Length) % DebugMoneyAmountOptions.Length;
        }

        private void ChangeDebugDistrictSelection(int delta)
        {
            var districtOptions = GetDebugDistrictOptions();
            if (districtOptions.Count == 0)
            {
                _selectedDebugDistrictIndex = 0;
                return;
            }

            _selectedDebugDistrictIndex = (_selectedDebugDistrictIndex + delta + districtOptions.Count) % districtOptions.Count;
        }

        private void ChangeDebugDistrictReputationAmountSelection(int delta)
        {
            _selectedDebugDistrictReputationAmountIndex = (_selectedDebugDistrictReputationAmountIndex + delta + DebugDistrictReputationAmountOptions.Length) % DebugDistrictReputationAmountOptions.Length;
        }

        private void ChangeDebugDistrictStateSelection(int delta)
        {
            _selectedDebugDistrictStateIndex = (_selectedDebugDistrictStateIndex + delta + DebugDistrictStateOptions.Length) % DebugDistrictStateOptions.Length;
        }

        private void AdjustDebugDistrictReputation(float direction)
        {
            if (_territoryManager == null)
            {
                ShowStatus("Territory manager unavailable.");
                return;
            }

            var districtName = GetSelectedDebugDistrict();
            if (string.IsNullOrWhiteSpace(districtName))
            {
                ShowStatus("No district selected.");
                return;
            }

            var amount = GetSelectedDebugDistrictReputationAmount();
            if (amount <= 0.001f)
            {
                ShowStatus("Select a valid district reputation amount first.");
                return;
            }

            _territoryManager.AdjustDistrictReputationDebug(districtName, amount * direction);
            _tabletStateStore.MarkNetworkDirty();

            var district = _territoryManager.GetDistrictState(districtName);
            var label = district != null && !string.IsNullOrWhiteSpace(district.ReputationLabel)
                ? district.ReputationLabel
                : "Unknown";
            ShowStatus(string.Format(
                "{0} reputation {1}{2:0.#}. New label: {3}.",
                districtName,
                direction >= 0f ? "+" : string.Empty,
                amount * direction,
                label));
        }

        private void ApplySelectedDebugDistrictStateToAll()
        {
            if (_territoryManager == null)
            {
                ShowStatus("Territory manager unavailable.");
                return;
            }

            var districtState = GetSelectedDebugDistrictState();
            if (string.IsNullOrWhiteSpace(districtState))
            {
                ShowStatus("Select a valid district state first.");
                return;
            }

            var updatedDistrictCount = _territoryManager.ApplyDistrictReputationDebugStateToAll(districtState);
            if (updatedDistrictCount <= 0)
            {
                ShowStatus("No district data is loaded.");
                return;
            }

            _tabletStateStore.MarkNetworkDirty();
            ShowStatus(string.Format(
                "Applied {0} to {1} district{2}.",
                districtState,
                updatedDistrictCount,
                updatedDistrictCount == 1 ? string.Empty : "s"));
        }

        private void AddSelectedDebugResourceToNearbyIndustry()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to add resources.");
                return;
            }

            var resource = GetSelectedDebugResource(industry);
            if (string.IsNullOrWhiteSpace(resource))
            {
                ShowStatus("No resource available for this industry.");
                return;
            }

            var added = AddDebugResource(industry, resource, GetSelectedDebugResourceAmountTons());
            if (added <= 0.001f)
            {
                ShowStatus(string.Format("Could not add {0}; storage is full or unsupported.", resource));
                return;
            }

            ShowStatus(string.Format("Added {0:0.0}t {1} to {2}.", added, resource, industry.Name));
        }

        private void DeleteResolvedVehicleCargo()
        {
            CancelPendingTransferForDebug();

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                ShowStatus("No cargo vehicle found nearby.");
                return;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            ClearCargoStateAndVisuals(cargoVehicle, cargoState);
            ShowStatus(string.Format("Cleared cargo from {0}.", cargoVehicle.DisplayName));
        }

        private void DeleteCurrentVehicle()
        {
            CancelPendingTransferForDebug();

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            var currentVehicle = player.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
            {
                ShowStatus("Enter a vehicle to delete it.");
                return;
            }

            var attachedTrailer = currentVehicle.TowedVehicle;
            var hadAttachedTrailer = attachedTrailer != null && attachedTrailer.Exists();
            if (attachedTrailer != null && attachedTrailer.Exists())
            {
                var trailerState = _fleetManager.GetOrCreateCargoState(attachedTrailer);
                ClearCargoStateAndVisuals(attachedTrailer, trailerState);
                attachedTrailer.Delete();
            }

            var currentCargoState = _fleetManager.GetOrCreateCargoState(currentVehicle);
            ClearCargoStateAndVisuals(currentVehicle, currentCargoState);
            currentVehicle.Delete();

            ShowStatus(hadAttachedTrailer
                ? "Deleted current vehicle and its trailer."
                : "Deleted current vehicle.");
        }

        private void FillNearbyIndustryInputs()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to fill its inputs.");
                return;
            }

            var inputCommodities = industry.GetSortedInputs();
            if (industry.SupportsOmegaBoost && !inputCommodities.Any(x => x.Equals("Omega", StringComparison.OrdinalIgnoreCase)))
            {
                inputCommodities.Add("Omega");
            }

            if (inputCommodities.Count == 0)
            {
                ShowStatus("This industry has no input buffers.");
                return;
            }

            float totalAdded = 0f;
            for (int i = 0; i < inputCommodities.Count; i++)
            {
                totalAdded += industry.AddInput(inputCommodities[i], DebugFillTons);
            }

            ShowStatus(totalAdded <= 0.001f
                ? "Nearby industry inputs are already full."
                : string.Format("Filled {0} input buffers on {1}.", inputCommodities.Count, industry.Name));
        }

        private void FillNearbyIndustryOutputs()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to fill its outputs.");
                return;
            }

            var outputCommodities = industry.GetSortedOutputs();
            if (outputCommodities.Count == 0)
            {
                ShowStatus("This industry has no output buffers.");
                return;
            }

            float totalAdded = 0f;
            for (int i = 0; i < outputCommodities.Count; i++)
            {
                totalAdded += industry.AddOutput(outputCommodities[i], DebugFillTons);
            }

            ShowStatus(totalAdded <= 0.001f
                ? "Nearby industry outputs are already full."
                : string.Format("Filled {0} output buffers on {1}.", outputCommodities.Count, industry.Name));
        }

        private void EmptyNearbyIndustryInputs()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to empty its inputs.");
                return;
            }

            if (industry.Inputs.Count == 0 && !industry.SupportsOmegaBoost)
            {
                ShowStatus("This industry has no input buffers.");
                return;
            }

            var removed = industry.ClearInputs();
            ShowStatus(removed <= 0.001f
                ? "Nearby industry inputs are already empty."
                : string.Format("Emptied {0:0.0}t from input buffers on {1}.", removed, industry.Name));
        }

        private void EmptyNearbyIndustryOutputs()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to empty its outputs.");
                return;
            }

            if (industry.Outputs.Count == 0)
            {
                ShowStatus("This industry has no output buffers.");
                return;
            }

            var removed = industry.ClearOutputs();
            ShowStatus(removed <= 0.001f
                ? "Nearby industry outputs are already empty."
                : string.Format("Emptied {0:0.0}t from output buffers on {1}.", removed, industry.Name));
        }

        private void MultiplyNearbyIndustryProductionRate()
        {
            var industry = GetDebugNearbyIndustry();
            if (industry == null)
            {
                ShowStatus("Move next to an industry to change its production rate.");
                return;
            }

            industry.SetProductionRate(industry.ProductionRate * 1000f);
            ShowStatus(string.Format("{0} production rate is now {1:0.0} cyc/h.", industry.Name, industry.ProductionRate));
        }

        private void SpawnSelectedVehicle()
        {
            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership)
            {
                var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
                var selectedTractor = selectedVehicle != null && !selectedVehicle.IsTrailer
                    ? null
                    : _vehicleSpawnController.SelectedTractorDefinition;
                if (IsCommercialDealershipPhantomLocked(selectedVehicle) || IsCommercialDealershipPhantomLocked(selectedTractor))
                {
                    _vehicleSpawnController.RefreshFilteredVehicles();
                    RefreshVehicleSelectionMenus();
                    ShowStatus(PhantomRoadVeteranUnlockMessage);
                    return;
                }

                string purchaseMessage;
                var acquired = IsCommercialDealershipRentMode
                    ? _propertyManager.TryRentCommercialVehicle(
                        selectedVehicle,
                        selectedTractor,
                        ref _profit,
                        GetCurrentInGameWeekMinute(),
                        out _,
                        out purchaseMessage)
                    : _propertyManager.TryPurchaseCommercialVehicle(
                        selectedVehicle,
                        selectedTractor,
                        ref _profit,
                        out _,
                        out purchaseMessage);
                if (acquired)
                {
                    _tabletStateStore.MarkBalanceDirty();
                    ReevaluatePlayerSuccesses(true);
                }

                ShowStatus(purchaseMessage);
                return;
            }

            Vector3 spawnPosition;
            float spawnHeading;
            string error;
            if (!TryGetCurrentVehicleSpawnParameters(out spawnPosition, out spawnHeading, out error))
            {
                ShowStatus(error);
                return;
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            string message;
            if (!_vehicleSpawnController.SpawnSelectedVehicle(GetGroundPosition, spawnPosition, spawnHeading, out truck, out cargoVehicle, out message))
            {
                ShowStatus(message);
                return;
            }

            _fleetManager.RegisterOwnedRig(truck, cargoVehicle);
            var poweredDefinition = _vehicleSpawnController.SelectedVehicleDefinition != null && _vehicleSpawnController.SelectedVehicleDefinition.IsTrailer
                ? _vehicleSpawnController.SelectedTractorDefinition
                : (_vehicleSpawnController.SelectedVehicleDefinition ?? _vehicleSpawnController.SelectedTractorDefinition);
            if (poweredDefinition != null && !poweredDefinition.IsTrailer)
            {
                _vehicleFuelSystem.InitializeSpawnedVehicle(truck);
            }
            _tabletStateStore.MarkCargoDirty();

            ShowStatus(message);
        }

        private void TryOpenIndustryTablet()
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (_nearestIndustry == null || player.Position.DistanceTo(GetIndustryMarkerPosition(_nearestIndustry)) > IndustryInteractionDistance)
            {
                ShowStatus("No industry marker in range.");
                return;
            }

            _menuIndustry = _nearestIndustry;
            CloseAllMenus();
            _tabletStateStore.MarkAllDirty();
            _tabletShellController.OpenIndustry(_nearestIndustry);
        }

        private bool TryGetIndustryTabletContext(Industry industry, out Vehicle cargoVehicle, out VehicleCargoState cargoState, out string error)
        {
            cargoVehicle = null;
            cargoState = null;
            error = string.Empty;

            if (industry == null)
            {
                error = "No target industry.";
                return false;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                error = "Player unavailable.";
                return false;
            }

            if (player.Position.DistanceTo(GetIndustryMarkerPosition(industry)) > IndustryInteractionDistance + 1.2f)
            {
                error = "Move closer to the industry marker.";
                return false;
            }

            Vehicle driverVehicle;
            cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                error = "Bring a cargo vehicle close to the industry.";
                return false;
            }

            cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null)
            {
                error = "Unable to initialize cargo state.";
                return false;
            }

            return true;
        }

        private void HandleTabletLoadRequested(Industry industry)
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryTabletContext(industry, out cargoVehicle, out cargoState, out error))
            {
                ShowStatus(error);
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = _vehicleSpawnController.SelectedFilter;
            }

            var products = _industryManager.GetLoadableOutputs(industry, cargoType);
            if (products.Count == 0)
            {
                ShowStatus("No compatible product available to load.");
                return;
            }

            var selectedProduct = !cargoState.IsEmpty
                ? products.FirstOrDefault(product => CommoditiesMatch(product, cargoState.Commodity))
                : GetPreferredOreCommodity(products);
            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                ShowStatus(string.Format("Vehicle already carries {0}. This site cannot top it up.", cargoState.Commodity));
                return;
            }

            if (!CanLoadSelectedCommodity(cargoState, selectedProduct, out error))
            {
                ShowStatus(error);
                return;
            }

            StartTabletLoadTransfer(industry, cargoVehicle, cargoState, cargoType, selectedProduct);
        }

        private void HandleTabletLoadCommodityRequested(Industry industry, string selectedProduct)
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                ShowStatus("No product selected for loading.");
                return;
            }

            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryTabletContext(industry, out cargoVehicle, out cargoState, out error))
            {
                ShowStatus(error);
                return;
            }

            if (!CanLoadSelectedCommodity(cargoState, selectedProduct, out error))
            {
                ShowStatus(error);
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            StartTabletLoadTransfer(industry, cargoVehicle, cargoState, cargoType, selectedProduct);
        }

        private static bool CommoditiesMatch(string left, string right)
        {
            return string.Equals(
                CommodityCatalog.Normalize(left),
                CommodityCatalog.Normalize(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanLoadSelectedCommodity(VehicleCargoState cargoState, string selectedProduct, out string message)
        {
            message = string.Empty;
            if (cargoState == null)
            {
                message = "No cargo hold is available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                message = "No product selected for loading.";
                return false;
            }

            if (cargoState.IsEmpty)
            {
                return true;
            }

            if (cargoState.FreeCapacityTons <= 0.001f)
            {
                message = "Vehicle cargo is already full.";
                return false;
            }

            if (!CommoditiesMatch(cargoState.Commodity, selectedProduct))
            {
                message = string.Format("Vehicle already carries {0}. Mixed cargo is not supported.", cargoState.Commodity);
                return false;
            }

            return true;
        }

        private void HandleTabletUnloadRequested(Industry industry)
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryTabletContext(industry, out cargoVehicle, out cargoState, out error))
            {
                ShowStatus(error);
                return;
            }

            if (cargoState.IsEmpty)
            {
                ShowStatus("Vehicle is empty.");
                return;
            }

            if (CargoTransferController.IndustryHasMultipleInputs(industry))
            {
                ShowStatus("Select unload mode from the tablet menu.");
                return;
            }

            var omegaOnly = CargoTransferController.IsOmegaOnlyUnloadIndustry(industry);
            if (omegaOnly && !cargoState.Commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(string.Format("Vehicle cargo is {0}. Omega cargo required.", cargoState.Commodity));
                return;
            }

            StartTabletUnloadTransfer(industry, cargoVehicle, cargoState, omegaOnly);
        }

        private void HandleTabletUnloadModeRequested(Industry industry, bool omegaOnly)
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryTabletContext(industry, out cargoVehicle, out cargoState, out error))
            {
                ShowStatus(error);
                return;
            }

            if (cargoState.IsEmpty)
            {
                ShowStatus("Vehicle is empty.");
                return;
            }

            if (omegaOnly && (industry == null || !industry.SupportsOmegaBoost))
            {
                ShowStatus("Omega unload is not available for this industry.");
                return;
            }

            StartTabletUnloadTransfer(industry, cargoVehicle, cargoState, omegaOnly);
        }

        private void HandleTabletRefuelRequested(Industry industry)
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            _cargoTransferController.StartTimedTransfer(
                "Refueling 0%...",
                2200,
                () =>
                {
                    CloseIndustryTablet();
                },
                () =>
                {
                    string message;
                    if (!_industryRefuelService.TryRefuel(industry, Game.Player.Character, out message))
                    {
                        ShowStatus(message);
                        return;
                    }

                    _tabletStateStore.MarkCargoDirty();
                    _tabletStateStore.MarkNetworkDirty();
                    ShowStatus(message, 4500);
                },
                progress => string.Format("Refueling {0:0}%...", ModMath.Clamp01(progress) * 100f));
        }

        private void HandleCompanyServiceRefuelRequested()
        {
            var player = Game.Player.Character;
            string message;
            if (!_industryRefuelService.TryRequestRemoteRefuel(player, Game.GameTime, out message))
            {
                ShowStatus(message);
                return;
            }

            _tabletStateStore.MarkNetworkDirty();
            RecordPlayerSuccessEmergencyServiceUsage();
            ShowStatus(message, 4500);
        }

        private void HandleCompanyServiceRepairRequested()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                ShowStatus("Enter a vehicle to request repairs.");
                return;
            }

            var currentVehicle = player.CurrentVehicle;
            if (currentVehicle == null || !currentVehicle.Exists())
            {
                ShowStatus("No vehicle available to repair.");
                return;
            }

            currentVehicle.Repair();
            var trailer = currentVehicle.TowedVehicle;
            if (trailer != null && trailer.Exists())
            {
                trailer.Repair();
            }

            ShowStatus(trailer != null && trailer.Exists()
                ? "Remote repair completed for the truck and trailer."
                : "Remote repair completed.", 4500);
            RecordPlayerSuccessEmergencyServiceUsage();
        }

        private void StartTabletLoadTransfer(Industry industry, Vehicle cargoVehicle, VehicleCargoState cargoState, VehicleCargoType cargoType, string selectedProduct)
        {
            _cargoTransferController.StartTabletLoadTransfer(
                industry,
                cargoVehicle,
                cargoState,
                cargoType,
                selectedProduct,
                () =>
                {
                    CloseIndustryTablet();
                });
        }

        private void StartTabletUnloadTransfer(Industry industry, Vehicle cargoVehicle, VehicleCargoState cargoState, bool omegaOnly)
        {
            var deliveryDescription = string.Format(
                "Player delivery of {0} to {1}",
                cargoState != null ? cargoState.Commodity : "cargo",
                industry != null ? industry.Name : "destination");
            _cargoTransferController.StartTabletUnloadTransfer(
                industry,
                cargoVehicle,
                cargoState,
                omegaOnly,
                () =>
                {
                    CloseIndustryTablet();
                },
                (amount, payoutContext) => AddProfit(
                    payoutContext != null ? payoutContext.Category : (cargoState != null && !string.IsNullOrWhiteSpace(cargoState.PlayerContractId)
                        ? CompanyFinanceCategory.PlayerContract
                        : CompanyFinanceCategory.PlayerDelivery),
                    amount,
                    payoutContext != null && !string.IsNullOrWhiteSpace(payoutContext.Description)
                        ? payoutContext.Description
                        : (cargoState != null && !string.IsNullOrWhiteSpace(cargoState.PlayerContractId)
                            ? string.Format(
                                "Contract delivery of {0} to {1}",
                                cargoState.Commodity ?? "cargo",
                                industry != null ? industry.Name : "destination")
                            : deliveryDescription),
                    payoutContext != null ? payoutContext.RouteContractId : 0,
                    payoutContext != null ? payoutContext.RouteLabel : null,
                    payoutContext != null ? payoutContext.PlayerContractId : null,
                    payoutContext != null ? payoutContext.ShipperKey : null,
                    payoutContext != null ? payoutContext.DistrictName : null),
                RecordPlayerSuccessDeliveryProgress);
        }

        private void HandleTabletUpgradeModuleRequested(Industry industry, IndustryUpgradeModule module)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            if (player.Position.DistanceTo(GetIndustryMarkerPosition(industry)) > IndustryInteractionDistance + 2.4f)
            {
                ShowStatus("Move closer to an industry to manage upgrades.");
                return;
            }

            if (_industryManager.RequiresIndustryPurchase(industry))
            {
                OpenIndustryPurchaseMenu(industry, IndustryPurchaseMenuReturnTarget.Tablet);
                return;
            }

            float cost;
            string result;
            var balanceBefore = _profit;
            if (!industry.TryUpgradeModule(module, ref _profit, out cost, out result))
            {
                ShowStatus(result);
                return;
            }

            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Upgraded {0} at {1}", module, industry.Name));

            industry.ClampBuffersToCapacity();
            ShowStatus(result);

            if (_upgradeMenu.IsOpen && _menuIndustry == industry)
            {
                RebuildUpgradeMenuItems();
            }
        }

        private void HandleTabletIndustryPurchaseRequested(Industry industry)
        {
            OpenIndustryPurchaseMenu(industry, IndustryPurchaseMenuReturnTarget.Tablet);
        }

        private void HandleTabletVehicleSpawnerRequested(Industry industry)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            if (player.Position.DistanceTo(GetIndustryMarkerPosition(industry)) > IndustryInteractionDistance + 2.4f)
            {
                ShowStatus("Move closer to an industry to open the vehicle spawner.");
                return;
            }

            string reason;
            if (!_territoryManager.CanSpawnCompanyVehicleAt(industry, out reason))
            {
                ShowStatus(reason);
                return;
            }

            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            _menuIndustry = industry;
            OpenIndustryCommercialGarageMenu();
        }

        private static string GetPreferredOreCommodity(List<string> products)
        {
            for (int i = 0; i < products.Count; i++)
            {
                if (products[i].Equals("Ore", StringComparison.OrdinalIgnoreCase))
                {
                    return products[i];
                }
            }

            return products[0];
        }

        private bool TryGetCurrentVehicleSpawnParameters(out Vector3 spawnPosition, out float spawnHeading, out string error)
        {
            spawnPosition = Vector3.Zero;
            spawnHeading = 0f;
            error = string.Empty;

            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.Industry)
            {
                if (_menuIndustry == null)
                {
                    error = "No target industry selected.";
                    return false;
                }

                if (!_menuIndustry.VehicleSpawnPosition.HasValue)
                {
                    error = "No vehicle spawn configured for this industry.";
                    return false;
                }

                spawnPosition = _menuIndustry.VehicleSpawnPosition.Value;
                spawnHeading = _menuIndustry.VehicleSpawnHeading ?? _config.VehicleSpawnHeading;
                return true;
            }

            var activeOffice = _propertyManager.ActiveOffice;
            if (activeOffice == null)
            {
                error = "No active office selected.";
                return false;
            }

            spawnPosition = activeOffice.SpawnPosition;
            spawnHeading = activeOffice.SpawnHeading;
            return true;
        }

        private void TryOpenUpgradeMenu()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (player.CurrentVehicle != null && player.CurrentVehicle.Exists())
            {
                ShowStatus("Exit your vehicle to open upgrade modules.");
                return;
            }

            if (_nearestIndustry == null || player.Position.DistanceTo(GetIndustryMarkerPosition(_nearestIndustry)) > IndustryInteractionDistance)
            {
                ShowStatus("Move closer to an industry to manage upgrades.");
                return;
            }

            _menuIndustry = _nearestIndustry;

            if (_industryManager.RequiresIndustryPurchase(_menuIndustry))
            {
                OpenIndustryPurchaseMenu(_menuIndustry, IndustryPurchaseMenuReturnTarget.UpgradeMenu);
                return;
            }

            CloseIndustryTablet();
            _officeMenu.Close();
            RebuildUpgradeMenuItems();
            _upgradeMenu.Open();
        }

        private void RebuildUpgradeMenuItems()
        {
            if (_menuIndustry == null)
            {
                _upgradeMenu.SetItems(new[]
                {
                    new OfficeMenuItem { CaptionFactory = () => "No industry selected." },
                    new OfficeMenuItem { CaptionFactory = () => "Close", OnActivate = () => _upgradeMenu.Close() },
                });
                return;
            }

            _upgradeMenu.Title = "Industry Upgrades";
            _upgradeMenu.Subtitle = _menuIndustry.Name;

            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Profit Balance: ${0:0}", _profit),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => BuildIndustryPermitCaption(_menuIndustry),
                    DetailFactory = () => BuildIndustryPermitDetail(_menuIndustry),
                    OnActivate = () => HandleIndustryPermitAction(_menuIndustry),
                },
            };

            if (_industryManager.RequiresIndustryPurchase(_menuIndustry))
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Buy industry: {0}", ModFormatting.FormatMoney(_menuIndustry.IndustryPrice)),
                    DetailFactory = () => "Purchase this site to unlock its upgrade modules.",
                    OnActivate = () => OpenIndustryPurchaseMenu(_menuIndustry, IndustryPurchaseMenuReturnTarget.UpgradeMenu),
                });

                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _upgradeMenu.Close(),
                });

                _upgradeMenu.SetItems(items);
                return;
            }

            AddUpgradeMenuItemIfAvailable(items, _menuIndustry, IndustryUpgradeModule.Production, "Production Module");
            AddUpgradeMenuItemIfAvailable(items, _menuIndustry, IndustryUpgradeModule.InputStorage, "Input Storage Module");
            AddUpgradeMenuItemIfAvailable(items, _menuIndustry, IndustryUpgradeModule.OutputStorage, "Output Storage Module");
            AddUpgradeMenuItemIfAvailable(items, _menuIndustry, IndustryUpgradeModule.OmegaStorage, "Omega Tank Module");

            if (items.Count == 1)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No upgrade modules available for this industry.",
                });
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Close",
                OnActivate = () => _upgradeMenu.Close(),
            });

            _upgradeMenu.SetItems(items);
        }

        private void AddUpgradeMenuItemIfAvailable(List<OfficeMenuItem> items, Industry industry, IndustryUpgradeModule module, string label)
        {
            if (items == null || industry == null)
            {
                return;
            }

            if (industry.GetUpgradeCost(module) <= 0f)
            {
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => GetUpgradeCaption(industry, module, label),
                OnActivate = () => TryApplyUpgradeModule(module),
            });
        }

        private static string GetUpgradeCaption(Industry industry, IndustryUpgradeModule module, string label)
        {
            var level = industry.GetUpgradeLevel(module);
            var cost = industry.GetUpgradeCost(module);
            if (cost <= 0f)
            {
                return string.Format("{0}: Unavailable", label);
            }

            return string.Format("{0} Lv.{1} -> ${2:0}", label, level, cost);
        }

        private void TryApplyUpgradeModule(IndustryUpgradeModule module)
        {
            if (_menuIndustry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            if (_industryManager.RequiresIndustryPurchase(_menuIndustry))
            {
                OpenIndustryPurchaseMenu(_menuIndustry, IndustryPurchaseMenuReturnTarget.UpgradeMenu);
                return;
            }

            float cost;
            string result;
            var balanceBefore = _profit;
            if (!_menuIndustry.TryUpgradeModule(module, ref _profit, out cost, out result))
            {
                ShowStatus(result);
                return;
            }

            RecordTrackedBalanceDelta(balanceBefore, CompanyFinanceCategory.OtherExpense, string.Format("Upgraded {0} at {1}", module, _menuIndustry.Name));

            _menuIndustry.ClampBuffersToCapacity();
            ShowStatus(result);
            RebuildUpgradeMenuItems();
        }

        private string AppendIndustryPurchasePermitGrantResult(Industry industry, string purchaseResult)
        {
            if (industry == null || industry.HasContractorPermit || !industry.RequiresContractorPermit)
            {
                return purchaseResult;
            }

            industry.SetContractorPermitOwned(true);
            if (string.IsNullOrWhiteSpace(purchaseResult))
            {
                return string.Format("Contractor permit granted for {0}.", industry.Name);
            }

            return string.Format("{0} Contractor permit granted.", purchaseResult.TrimEnd('.', ' '));
        }

        private string BuildIndustryPermitCaption(Industry industry)
        {
            if (industry == null)
            {
                return "Permit: n/a";
            }

            if (!industry.RequiresContractorPermit)
            {
                return "Permit: Open";
            }

            if (!_licensingDifficultyEnabled && !industry.HasContractorPermit)
            {
                return "Permit: Disabled";
            }

            return industry.HasContractorPermit
                ? "Permit: Owned"
                : string.Format("Permit: {0}", ModFormatting.FormatMoney(industry.IndustryLicencePrice));
        }

        private string BuildIndustryPermitDetail(Industry industry)
        {
            if (industry == null)
            {
                return "No industry selected.";
            }

            if (!industry.RequiresContractorPermit)
            {
                return "No contractor permit is required for this site.";
            }

            if (!_licensingDifficultyEnabled && !industry.HasContractorPermit)
            {
                return "Licensing difficulty is disabled for this save, so permit access is already open.";
            }

            if (industry.HasContractorPermit)
            {
                return "Contractor permit already unlocked for this site.";
            }

            if (_industryManager.RequiresIndustryPurchase(industry))
            {
                return "Purchase this permit directly, or buy the industry to unlock it automatically.";
            }

            return string.Format("Purchase the contractor permit for {0}.", ModFormatting.FormatMoney(industry.IndustryLicencePrice));
        }

        private void HandleIndustryPermitAction(Industry industry)
        {
            if (industry == null)
            {
                ShowStatus("No industry selected.");
                return;
            }

            if (!industry.RequiresContractorPermit)
            {
                ShowStatus(string.Format("{0} does not require a contractor permit.", industry.Name));
                return;
            }

            if (!_licensingDifficultyEnabled && !industry.HasContractorPermit)
            {
                ShowStatus("Licensing system is disabled for this save.");
                return;
            }

            if (industry.HasContractorPermit)
            {
                ShowStatus(string.Format("Contractor permit already purchased for {0}.", industry.Name));
                return;
            }

            ShowStatus(PurchaseContractorPermitFromOverview(industry));
            RebuildUpgradeMenuItems();
        }

        private void ClearCargoStateAndVisuals(Vehicle cargoVehicle, VehicleCargoState cargoState)
        {
            _cargoTransferController.ClearCargoStateAndVisuals(cargoVehicle, cargoState);
        }

        private void DrawProgressBar(string label, float progress)
        {
            var res = Screen.MainWindowResolution;
            var width = res.Width * 0.36f;
            var height = res.Height * 0.045f;
            var x = (res.Width - width) * 0.5f;
            var y = res.Height * 0.88f;

            DrawRect(res.Width, res.Height, x + 6f, y + 6f, width, height, Color.FromArgb(95, 0, 0, 0));
            DrawRect(res.Width, res.Height, x, y, width, height, Color.FromArgb(192, 14, 20, 30));
            DrawRect(res.Width, res.Height, x, y, width, 4f, Color.FromArgb(236, 219, 165, 57));
            DrawRect(res.Width, res.Height, x + 2f, y + 2f, (width - 4f) * progress, height - 4f, Color.FromArgb(236, 219, 165, 57));

            new TextElement(
                    label,
                    ToScriptTextCoords(res, x + 10f, y - 21f),
                    0.33f,
                    Color.White,
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();
        }

        private void DrawPanel(List<string> lines, float xNormalized, float yNormalized, float widthNormalized, Color bgColor, Color accentColor)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            var res = Screen.MainWindowResolution;
            var x = res.Width * xNormalized;
            var y = res.Height * yNormalized;
            var width = res.Width * widthNormalized;
            var lineHeight = res.Height * 0.028f;
            var height = lineHeight * (lines.Count + 1.2f);

            DrawRect(res.Width, res.Height, x + 6f, y + 6f, width, height, Color.FromArgb(95, 0, 0, 0));
            DrawRect(res.Width, res.Height, x, y, width, height, bgColor);
            DrawRect(res.Width, res.Height, x, y, width, 4f, accentColor);
            DrawRect(res.Width, res.Height, x, y + 4f, width, 1f, Color.FromArgb(180, 255, 255, 255));

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = y + 8f + (lineHeight * i);
                var color = i == 0 ? Color.FromArgb(250, 240, 248, 255) : Color.FromArgb(235, 226, 232, 241);
                var scale = i == 0 ? 0.33f : 0.295f;
                var font = i == 0 ? GTA.UI.Font.ChaletComprimeCologne : GTA.UI.Font.ChaletLondon;

                new TextElement(
                        lines[i],
                        ToScriptTextCoords(res, x + 10f, lineY),
                        scale,
                        color,
                        font,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();
            }
        }

        private void CloseIndustryTablet()
        {
            _tabletShellController.Close();
        }

        private void CreateMapBlips()
        {
            _blipLifecycleManager.Create();
            RefreshCommercialVehicleBlips();
            RefreshPersonalVehicleBlips();
        }

        private void RefreshBlipPositions()
        {
            _blipLifecycleManager.Refresh();
            RefreshCommercialVehicleBlips();
            RefreshPersonalVehicleBlips();
        }

        private void DestroyMapBlips()
        {
            DestroyCommercialVehicleBlips();
            DestroyPersonalVehicleBlips();
            _blipLifecycleManager.Destroy();
        }

        private void RefreshCommercialVehicleBlips()
        {
            var visibleAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var player = Game.Player.Character;
            var playerVehicleHandle = player != null && player.Exists() && player.CurrentVehicle != null && player.CurrentVehicle.Exists()
                ? player.CurrentVehicle.Handle
                : 0;
            var vehicleInfos = _propertyManager != null ? _propertyManager.GetCommercialVehicleBlipInfos() : null;
            if (vehicleInfos != null)
            {
                for (int i = 0; i < vehicleInfos.Count; i++)
                {
                    var info = vehicleInfos[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.AssetId))
                    {
                        continue;
                    }

                    var truck = Entity.FromHandle(info.TruckHandle) as Vehicle;
                    if (truck == null || !truck.Exists())
                    {
                        continue;
                    }

                    if (playerVehicleHandle != 0 && truck.Handle == playerVehicleHandle)
                    {
                        continue;
                    }

                    visibleAssetIds.Add(info.AssetId);
                    Blip blip;
                    if (!_commercialVehicleBlips.TryGetValue(info.AssetId, out blip) || blip == null || !blip.Exists())
                    {
                        blip = World.CreateBlip(truck.Position);
                        if (blip == null || !blip.Exists())
                        {
                            continue;
                        }

                        blip.Sprite = BlipSprite.Truck;
                        blip.Color = BlipColor.Blue;
                        blip.Scale = 0.85f;
                        blip.IsShortRange = false;
                        blip.IsHiddenOnLegend = false;
                        _commercialVehicleBlips[info.AssetId] = blip;
                    }

                    blip.Position = truck.Position;
                    blip.Name = string.Format("Office Truck: {0}", info.DisplayName ?? string.Empty);
                }
            }

            var staleAssetIds = _commercialVehicleBlips.Keys
                .Where(assetId => !visibleAssetIds.Contains(assetId))
                .ToList();
            for (int i = 0; i < staleAssetIds.Count; i++)
            {
                Blip blip;
                if (_commercialVehicleBlips.TryGetValue(staleAssetIds[i], out blip) && blip != null && blip.Exists())
                {
                    blip.Delete();
                }

                _commercialVehicleBlips.Remove(staleAssetIds[i]);
            }
        }

        private void DestroyCommercialVehicleBlips()
        {
            var assetIds = _commercialVehicleBlips.Keys.ToList();
            for (int i = 0; i < assetIds.Count; i++)
            {
                Blip blip;
                if (_commercialVehicleBlips.TryGetValue(assetIds[i], out blip) && blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            _commercialVehicleBlips.Clear();
        }

        private void RefreshPersonalVehicleBlips()
        {
            var visibleAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var player = Game.Player.Character;
            var playerVehicleHandle = player != null && player.Exists() && player.CurrentVehicle != null && player.CurrentVehicle.Exists()
                ? player.CurrentVehicle.Handle
                : 0;
            var vehicleInfos = _propertyManager != null ? _propertyManager.GetPersonalVehicleBlipInfos() : null;
            if (vehicleInfos != null)
            {
                for (int i = 0; i < vehicleInfos.Count; i++)
                {
                    var info = vehicleInfos[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.AssetId))
                    {
                        continue;
                    }

                    var vehicle = Entity.FromHandle(info.VehicleHandle) as Vehicle;
                    if (vehicle == null || !vehicle.Exists())
                    {
                        continue;
                    }

                    if (playerVehicleHandle != 0 && vehicle.Handle == playerVehicleHandle)
                    {
                        continue;
                    }

                    visibleAssetIds.Add(info.AssetId);
                    Blip blip;
                    if (!_personalVehicleBlips.TryGetValue(info.AssetId, out blip) || blip == null || !blip.Exists())
                    {
                        blip = World.CreateBlip(vehicle.Position);
                        if (blip == null || !blip.Exists())
                        {
                            continue;
                        }

                        blip.Sprite = BlipSprite.PersonalVehicleCar;
                        blip.Color = BlipColor.Blue;
                        blip.Scale = 0.85f;
                        blip.IsShortRange = false;
                        blip.IsHiddenOnLegend = false;
                        _personalVehicleBlips[info.AssetId] = blip;
                    }

                    blip.Position = vehicle.Position;
                    blip.Name = string.Format("Personal Vehicle: {0}", info.DisplayName ?? string.Empty);
                }
            }

            var staleAssetIds = _personalVehicleBlips.Keys
                .Where(assetId => !visibleAssetIds.Contains(assetId))
                .ToList();
            for (int i = 0; i < staleAssetIds.Count; i++)
            {
                Blip blip;
                if (_personalVehicleBlips.TryGetValue(staleAssetIds[i], out blip) && blip != null && blip.Exists())
                {
                    blip.Delete();
                }

                _personalVehicleBlips.Remove(staleAssetIds[i]);
            }
        }

        private void DestroyPersonalVehicleBlips()
        {
            var assetIds = _personalVehicleBlips.Keys.ToList();
            for (int i = 0; i < assetIds.Count; i++)
            {
                Blip blip;
                if (_personalVehicleBlips.TryGetValue(assetIds[i], out blip) && blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            _personalVehicleBlips.Clear();
        }

        private Vector3 ResolveOfficeBlipSeed()
        {
            var activeOffice = _propertyManager != null ? _propertyManager.ActiveOffice : null;
            if (activeOffice != null)
            {
                return activeOffice.MarkerPosition;
            }

            var fallbackOffice = _propertyManager != null ? _propertyManager.Offices.FirstOrDefault() : null;
            return fallbackOffice != null ? fallbackOffice.MarkerPosition : _mainOfficeMarkerSeed;
        }

        private bool IsNearMainOffice(Vector3 position)
        {
            return position.DistanceTo(GetGroundPosition(_mainOfficeMarkerSeed)) <= OfficeInteractionDistance;
        }

        private Industry GetIndustryInInteractionRange(Vector3 position)
        {
            if (_nearestIndustry != null && position.DistanceTo(GetIndustryMarkerPosition(_nearestIndustry)) <= IndustryInteractionDistance)
            {
                return _nearestIndustry;
            }

            var nearest = _industryManager.GetNearestIndustry(position, IndustryInteractionDistance + 1.0f);
            if (nearest == null)
            {
                return null;
            }

            return position.DistanceTo(GetIndustryMarkerPosition(nearest)) <= IndustryInteractionDistance
                ? nearest
                : null;
        }

        private static Vector3 GetIndustryMarkerPosition(Industry industry)
        {
            if (industry == null)
            {
                return Vector3.Zero;
            }

            return industry.Position;
        }

        private static PointF ToScriptTextCoords(Size resolution, float x, float y)
        {
            const float scriptWidth = 1280f;
            const float scriptHeight = 720f;
            return new PointF(
                x * (scriptWidth / resolution.Width),
                y * (scriptHeight / resolution.Height));
        }

        private static int GetCurrentInGameWeekMinute()
        {
            var year = Math.Max(2000, Function.Call<int>(Hash.GET_CLOCK_YEAR));
            var month = Math.Max(1, Math.Min(12, Function.Call<int>(Hash.GET_CLOCK_MONTH) + 1));
            var day = Math.Max(1, Function.Call<int>(Hash.GET_CLOCK_DAY_OF_MONTH));
            var hours = Math.Max(0, Function.Call<int>(Hash.GET_CLOCK_HOURS)) % 24;
            var minutes = Math.Max(0, Function.Call<int>(Hash.GET_CLOCK_MINUTES)) % 60;
            var clampedDay = Math.Min(day, DateTime.DaysInMonth(year, month));
            var clockDate = new DateTime(year, month, clampedDay, hours, minutes, 0, DateTimeKind.Unspecified);
            var epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return (int)(clockDate - epoch).TotalMinutes;
        }

        private static void DrawRect(float screenWidth, float screenHeight, float x, float y, float width, float height, Color color)
        {
            var centerX = (x + (width * 0.5f)) / screenWidth;
            var centerY = (y + (height * 0.5f)) / screenHeight;
            var normalizedW = width / screenWidth;
            var normalizedH = height / screenHeight;

            Function.Call(Hash.DRAW_RECT, centerX, centerY, normalizedW, normalizedH, color.R, color.G, color.B, color.A);
        }

        private static Vector3 GetGroundPosition(Vector3 input)
        {
            return input;
        }

        private Industry GetDebugNearbyIndustry()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return null;
            }

            var nearbyIndustry = GetIndustryInInteractionRange(player.Position);
            if (nearbyIndustry != null)
            {
                _nearestIndustry = nearbyIndustry;
            }

            return nearbyIndustry;
        }

        private List<string> GetDebugResourceOptions(Industry industry)
        {
            var resources = new List<string>();
            if (industry == null)
            {
                return resources;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inputs = industry.GetSortedInputs();
            for (int i = 0; i < inputs.Count; i++)
            {
                if (seen.Add(inputs[i]))
                {
                    resources.Add(inputs[i]);
                }
            }

            if (industry.SupportsOmegaBoost && seen.Add("Omega"))
            {
                resources.Add("Omega");
            }

            var outputs = industry.GetSortedOutputs();
            for (int i = 0; i < outputs.Count; i++)
            {
                if (seen.Add(outputs[i]))
                {
                    resources.Add(outputs[i]);
                }
            }

            return resources;
        }

        private string GetSelectedDebugResource(Industry industry)
        {
            var resourceOptions = GetDebugResourceOptions(industry);
            if (resourceOptions.Count == 0)
            {
                _selectedDebugResourceIndex = 0;
                return string.Empty;
            }

            if (_selectedDebugResourceIndex >= resourceOptions.Count)
            {
                _selectedDebugResourceIndex = resourceOptions.Count - 1;
            }
            else if (_selectedDebugResourceIndex < 0)
            {
                _selectedDebugResourceIndex = 0;
            }

            return resourceOptions[_selectedDebugResourceIndex];
        }
        private void AddDebugMoney()
        {
            var amount = GetSelectedDebugMoneyAmount();
            if (amount <= 0f)
            {
                ShowStatus("Select a valid money amount first.");
                return;
            }

            AddProfit(CompanyFinanceCategory.OtherIncome, amount, "Debug grant");
            ShowStatus(string.Format("Added {0}. Balance is now {1}.", ModFormatting.FormatMoney(amount), ModFormatting.FormatMoney(_profit)));
        }

        private float GetSelectedDebugResourceAmountTons()
        {
            if (_selectedDebugResourceAmountIndex < 0 || _selectedDebugResourceAmountIndex >= DebugResourceAmountOptionsTons.Length)
            {
                _selectedDebugResourceAmountIndex = 0;
            }

            return DebugResourceAmountOptionsTons[_selectedDebugResourceAmountIndex];
        }

        private float GetSelectedDebugMoneyAmount()
        {
            if (_selectedDebugMoneyAmountIndex < 0 || _selectedDebugMoneyAmountIndex >= DebugMoneyAmountOptions.Length)
            {
                _selectedDebugMoneyAmountIndex = 0;
            }

            return DebugMoneyAmountOptions[_selectedDebugMoneyAmountIndex];
        }

        private float GetSelectedDebugDistrictReputationAmount()
        {
            if (_selectedDebugDistrictReputationAmountIndex < 0 || _selectedDebugDistrictReputationAmountIndex >= DebugDistrictReputationAmountOptions.Length)
            {
                _selectedDebugDistrictReputationAmountIndex = 0;
            }

            return DebugDistrictReputationAmountOptions[_selectedDebugDistrictReputationAmountIndex];
        }

        private string GetSelectedDebugDistrictState()
        {
            if (_selectedDebugDistrictStateIndex < 0 || _selectedDebugDistrictStateIndex >= DebugDistrictStateOptions.Length)
            {
                _selectedDebugDistrictStateIndex = 0;
            }

            return DebugDistrictStateOptions[_selectedDebugDistrictStateIndex];
        }

        private List<string> GetDebugDistrictOptions()
        {
            return _territoryManager != null
                ? _territoryManager.DistrictStates
                    .Where(district => district != null && !string.IsNullOrWhiteSpace(district.DistrictName))
                    .OrderBy(district => district.DistrictName, StringComparer.OrdinalIgnoreCase)
                    .Select(district => district.DistrictName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
        }

        private string GetSelectedDebugDistrict()
        {
            var districtOptions = GetDebugDistrictOptions();
            if (districtOptions.Count == 0)
            {
                _selectedDebugDistrictIndex = 0;
                return string.Empty;
            }

            if (_selectedDebugDistrictIndex >= districtOptions.Count)
            {
                _selectedDebugDistrictIndex = districtOptions.Count - 1;
            }
            else if (_selectedDebugDistrictIndex < 0)
            {
                _selectedDebugDistrictIndex = 0;
            }

            return districtOptions[_selectedDebugDistrictIndex];
        }

        private void CancelPendingTransferForDebug()
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                _cargoTransferController.ClearState();
            }
        }

        private static float AddDebugResource(Industry industry, string resource, float tons)
        {
            if (industry == null || string.IsNullOrWhiteSpace(resource) || tons <= 0f)
            {
                return 0f;
            }

            return industry.Outputs.Contains(resource)
                ? industry.AddOutput(resource, tons)
                : industry.AddInput(resource, tons);
        }

        private static float[] BuildStartingBalanceOptions()
        {
            var values = new List<float>();
            for (int amount = -5000; amount <= 100000; amount += 5000)
            {
                values.Add(amount);
            }

            return values.ToArray();
        }

        private void ShowStatus(string message, int durationMs = 3000)
        {
            var prefixed = PrefixMessage(message);
            _statusMessage = prefixed;
            _statusMessageUntil = Game.GameTime + durationMs;
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkStatusDirty();
            }

            Notification.PostTicker(prefixed, false, false);
        }

        private void SweepIndustryObjectDeletions(Ped player, int gameTime)
        {
            if (player == null || !player.Exists() || _industryManager == null || gameTime - _lastIndustryObjectDeletionSweepMs < IndustryObjectDeletionSweepIntervalMs)
            {
                return;
            }

            _lastIndustryObjectDeletionSweepMs = gameTime;
            var playerPosition = player.Position;
            var activationRangeSquared = IndustryObjectDeletionActivationRange * IndustryObjectDeletionActivationRange;
            var industries = _industryManager.Industries;
            for (int i = 0; i < industries.Count; i++)
            {
                var industry = industries[i];
                if (industry == null || industry.ObjectToDeleteModelHashes == null || industry.ObjectToDeleteModelHashes.Count == 0)
                {
                    continue;
                }

                if (industry.Position.DistanceToSquared(playerPosition) > activationRangeSquared)
                {
                    continue;
                }

                DeleteConfiguredIndustryObjects(industry);
            }
        }

        private static void DeleteConfiguredIndustryObjects(Industry industry)
        {
            if (industry == null || industry.ObjectToDeleteModelHashes == null || industry.ObjectToDeleteModelHashes.Count == 0)
            {
                return;
            }

            Prop[] nearbyProps;
            try
            {
                nearbyProps = World.GetNearbyProps(industry.Position, IndustryObjectDeletionRadius);
            }
            catch
            {
                return;
            }

            if (nearbyProps == null || nearbyProps.Length == 0)
            {
                return;
            }

            var hashesToDelete = new HashSet<int>(industry.ObjectToDeleteModelHashes);
            for (int i = 0; i < nearbyProps.Length; i++)
            {
                var prop = nearbyProps[i];
                if (prop == null)
                {
                    continue;
                }

                try
                {
                    if (!prop.Exists() || prop.IsPersistent || !hashesToDelete.Contains(prop.Model.Hash))
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
                    // Keep deletion resilient: one bad streamed prop should not block the remaining cleanup pass.
                }
            }
        }

        private string GetActiveStatusMessage()
        {
            return !string.IsNullOrWhiteSpace(_statusMessage) && Game.GameTime <= _statusMessageUntil
                ? _statusMessage
                : string.Empty;
        }

        private static string PrefixMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return MessagePrefix.TrimEnd();
            }

            if (message.IndexOf("[LSOL]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return message;
            }

            return MessagePrefix + message;
        }

        private static string JoinSet(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "None";
            }

            return string.Join(",", values.OrderBy(x => x));
        }

        private static string KeyName(WinForms.Keys key)
        {
            return key == WinForms.Keys.Back ? "Backspace" : key.ToString();
        }

        private static bool IsPetrolServiceStation(Industry industry)
        {
            return industry != null && industry.IsGasStation;
        }

        private void OnAborted(object sender, EventArgs e)
        {
            TrySaveIndustryPersistence();
            CancelApartmentSleepTransition();
            _specialMissionManager.Shutdown();
            _npcLogisticsManager.ClearAll();
            _playerContractsManager.ClearAll();
            CancelActiveRefuelDispatchForShutdown();
            DestroyMapBlips();
            _cargoTransferController.ClearState();
            _industryOutputPropManager.DestroyAll();
            _officeObjectManager.Cleanup();
            CloseAllMenus();
            _heldKeys.Clear();
        }

        private enum VehicleCargoMenuContext
        {
            Office = 0,
            Industry = 1,
            CommercialDealership = 2,
        }

        private enum GameModMode
        {
            Fun = 0,
            Career = 1,
        }

        private enum SaveSlotMenuAction
        {
            Load = 0,
            Delete = 1,
        }

        private enum IndustryPurchaseMenuReturnTarget
        {
            None = 0,
            Tablet = 1,
            UpgradeMenu = 2,
        }

        private sealed class NamedSaveEntry
        {
            public NamedSaveEntry(string displayName, string filePath, string loadPath = null)
            {
                DisplayName = displayName;
                FilePath = filePath;
                LoadPath = string.IsNullOrWhiteSpace(loadPath) ? filePath : loadPath;
            }

            public string DisplayName { get; private set; }

            public string FilePath { get; private set; }

            public string LoadPath { get; private set; }
        }

    }
}

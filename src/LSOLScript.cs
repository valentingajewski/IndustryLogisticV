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
        private const int VkRControl = 0xA3;
        private const float DebugFillTons = 1000000f;
        private const string SavegamesDirectoryName = "LSOLSaves";
        private const int MaxSaveNameLength = 40;
        private const float DefaultStartingBalance = 20000f;
        private static readonly float[] DebugResourceAmountOptionsTons = { 1f, 5f, 10f, 25f, 50f, 100f, 250f, 500f, 1000f };
        private static readonly float[] DebugMoneyAmountOptions = { 1000f, 5000f, 10000f, 25000f, 50000f, 100000f, 500000f, 1000000f };
        private static readonly float[] StartingBalanceOptions = BuildStartingBalanceOptions();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly ModConfig _config;
        private readonly string _configPath;
        private readonly string _defaultIndustryStatePath;
        private readonly string _savegamesDirectoryPath;
        private readonly ControlBindings _controls;
        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly GlobalMarketManager _globalMarket;

        private readonly LemonMenu _officeMenu;
        private readonly LemonMenu _vehicleCargoMenu;
        private readonly SimpleMenu _upgradeMenu;
        private readonly LemonMenu _modControlMenu;
        private readonly LemonMenu _savingOptionsMenu;
        private readonly LemonMenu _newSaveSetupMenu;
        private readonly LemonMenu _saveSlotsMenu;
        private readonly LemonMenu _industryPurchaseMenu;
        private readonly LemonMenu _difficultyMenu;
        private readonly LemonMenu _debugMenu;
        private readonly BarrierInteractionHandler _barrierInteractionHandler;
        private readonly BlipLifecycleManager _blipLifecycleManager;
        private readonly CargoTransferController _cargoTransferController;
        private readonly NpcLogisticsManager _npcLogisticsManager;
        private readonly NpcLogisticsController _npcLogisticsController;
        private readonly OverviewMenuController _overviewMenuController;
        private readonly VehicleSpawnController _vehicleSpawnController;
        private readonly WorkerSpawnController _workerSpawnController;
        private readonly IndustryTabletController _industryTabletController;

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
        private int _selectedStartingBalanceIndex;
        private int _lastIndustryTickMs;
        private int _lastNearestProbeMs;
        private int _lastBlipRefreshMs;
        private int _statusMessageUntil;

        private string _pendingSaveName;
        private string _statusMessage;

        private float _profit;
        private float _currentStartingBalance;
        private GameModMode _gameModMode;
        private IndustryPurchaseMenuReturnTarget _industryPurchaseMenuReturnTarget;
        private SaveSlotMenuAction _saveSlotMenuAction;

        private bool _showContext;
        private bool _modMechanicsEnabled;
        private bool _industryPersistenceEnabled;
        private bool _difficultySettingsLocked;
        private bool _cargoDamageDifficultyEnabled;
        private bool _pendingCargoDamageDifficultyEnabled;
        private bool _industryPricingDifficultyEnabled;
        private bool _pendingIndustryPricingDifficultyEnabled;
        private bool _licensingDifficultyEnabled;
        private bool _pendingLicensingDifficultyEnabled;
        private EconomyDifficultyPreset _economyDifficultyPreset;
        private EconomyDifficultyPreset _pendingEconomyDifficultyPreset;
        private NpcWeeklyWageDifficulty _npcWeeklyWageDifficulty;
        private NpcWeeklyWageDifficulty _pendingNpcWeeklyWageDifficulty;
        private bool _vehicleFuelDifficultyEnabled;
        private bool _pendingVehicleFuelDifficultyEnabled;

        public LSOLScript()
        {
            _configPath = ResolveConfigPath();
            _defaultIndustryStatePath = ResolveIndustryStatePath(_configPath);
            _savegamesDirectoryPath = ResolveSavegamesDirectoryPath(_configPath);
            _industryStatePath = _defaultIndustryStatePath;
            _config = ModConfig.Load(_configPath);
            _controls = _config.Controls ?? new ControlBindings();
            _industryManager = new IndustryManager(_config);
            _fleetManager = new FleetManager(_config);
            _globalMarket = new GlobalMarketManager(Game.GameTime);

            _mainOfficeMarkerSeed = _config.MainOfficePosition;
            _vehicleSpawnMarkerSeed = _config.VehicleSpawnPosition;
            _barrierInteractionHandler = new BarrierInteractionHandler();
            _blipLifecycleManager = new BlipLifecycleManager(
                _industryManager,
                _mainOfficeMarkerSeed,
                _vehicleSpawnMarkerSeed,
                GetGroundPosition,
                GetIndustryMarkerPosition,
                IsPetrolServiceStation);
            _cargoTransferController = new CargoTransferController(
                _fleetManager,
                _industryManager,
                _globalMarket,
                message => ShowStatus(message));
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
                defaultCargoFilter);
            _workerSpawnController = new WorkerSpawnController(_config.WorkerModels);
            _npcLogisticsManager = new NpcLogisticsManager(
                _configPath,
                _industryManager,
                _fleetManager,
                _globalMarket,
                GetGroundPosition,
                () => _profit,
                DeductProfit,
                amount => _profit += amount,
                message => ShowStatus(message));

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
            };
            _savingOptionsMenu = new LemonMenu("Saving Options")
            {
                Subtitle = "Create, load, delete, and save named games",
                AlignRight = true,
            };
            _newSaveSetupMenu = new LemonMenu("Difficulty Settings")
            {
                Subtitle = "Configure a new save before starting",
                AlignRight = true,
            };
            _saveSlotsMenu = new LemonMenu("Save Slots")
            {
                Subtitle = "Choose a saved game profile",
                AlignRight = true,
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
            };
            _debugMenu = new LemonMenu("Debug")
            {
                Subtitle = "Runtime industry and vehicle tools",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _npcLogisticsController = new NpcLogisticsController(
                _controls,
                _npcLogisticsManager,
                OpenOfficeMenu,
                message => ShowStatus(message));
            _overviewMenuController = new OverviewMenuController(
                _controls,
                _industryManager,
                CloseAllMenus,
                () => _licensingDifficultyEnabled,
                () => _profit,
                PurchaseContractorPermitFromOverview,
                message => ShowStatus(message));
            _industryTabletController = new IndustryTabletController(
                _fleetManager,
                _industryManager,
                _globalMarket,
                GetIndustryMarkerPosition);
            _industryTabletController.LoadRequested += HandleTabletLoadRequested;
            _industryTabletController.LoadCommodityRequested += HandleTabletLoadCommodityRequested;
            _industryTabletController.UnloadRequested += HandleTabletUnloadRequested;
            _industryTabletController.UnloadModeRequested += HandleTabletUnloadModeRequested;
            _industryTabletController.IndustryPurchaseRequested += HandleTabletIndustryPurchaseRequested;
            _industryTabletController.UpgradeModuleRequested += HandleTabletUpgradeModuleRequested;
            _industryTabletController.VehicleSpawnerRequested += HandleTabletVehicleSpawnerRequested;

            _keyCooldownUntil = new Dictionary<WinForms.Keys, int>();
            _heldKeys = new HashSet<WinForms.Keys>();

            _gameModMode = GameModMode.Fun;
            _vehicleCargoMenuContext = VehicleCargoMenuContext.Office;
            _profit = DefaultStartingBalance;
            _currentStartingBalance = DefaultStartingBalance;
            _modMechanicsEnabled = false;
            _industryPersistenceEnabled = true;
            _difficultySettingsLocked = false;
            _cargoDamageDifficultyEnabled = true;
            _industryPricingDifficultyEnabled = false;
            _licensingDifficultyEnabled = false;
            _economyDifficultyPreset = EconomyDifficultyPreset.Standard;
            _npcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard;
            _vehicleFuelDifficultyEnabled = false;
            _pendingCargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled;
            _pendingIndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled;
            _pendingLicensingDifficultyEnabled = _licensingDifficultyEnabled;
            _pendingEconomyDifficultyPreset = _economyDifficultyPreset;
            _pendingNpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty;
            _pendingVehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled;
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            _pendingSaveName = string.Empty;
            _industryPurchaseMenuReturnTarget = IndustryPurchaseMenuReturnTarget.None;
            _saveSlotMenuAction = SaveSlotMenuAction.Load;
            ApplyDifficultySettingsToSystems();

            if (_industryPersistenceEnabled)
            {
                TryLoadIndustryPersistence(false);
            }

            RebuildOfficeMenuItems();
            RebuildModControlMenuItems();
            RebuildDifficultyMenuItems();
            RebuildDebugMenuItems();

            Tick += OnTick;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Aborted += OnAborted;

            Notification.PostTicker(MessagePrefix + "Loaded successfully.", false, false);
        }

        private bool AnyMenuOpen
        {
            get
            {
                return _officeMenu.IsOpen
                    || _vehicleCargoMenu.IsOpen
                    || _upgradeMenu.IsOpen
                    || _modControlMenu.IsOpen
                    || _savingOptionsMenu.IsOpen
                    || _newSaveSetupMenu.IsOpen
                    || _saveSlotsMenu.IsOpen
                    || _industryPurchaseMenu.IsOpen
                    || _difficultyMenu.IsOpen
                    || _debugMenu.IsOpen
                    || _npcLogisticsController.AnyMenuOpen
                    || _overviewMenuController.AnyMenuOpen
                    || _industryTabletController.IsOpen;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            var gameTime = Game.GameTime;
            if (_lastIndustryTickMs == 0)
            {
                _lastIndustryTickMs = gameTime;
            }

            if (!_modMechanicsEnabled)
            {
                DrawOpenMenus();

                if (!string.IsNullOrWhiteSpace(_statusMessage) && gameTime <= _statusMessageUntil)
                {
                    Screen.ShowSubtitle(_statusMessage, 1);
                }

                return;
            }

            var elapsed = gameTime - _lastIndustryTickMs;
            if (elapsed >= 1000)
            {
                _lastIndustryTickMs = gameTime;
                _globalMarket.Update(gameTime);
                _industryManager.Update(elapsed / 60000f, _config.OmegaMultiplier);
                _fleetManager.CleanupStates();
            }

            if (gameTime - _lastNearestProbeMs >= 250)
            {
                _lastNearestProbeMs = gameTime;
                _nearestIndustry = _industryManager.GetNearestIndustry(player.Position, 40f);
            }

            if (gameTime - _lastBlipRefreshMs >= 6000)
            {
                _lastBlipRefreshMs = gameTime;
                _blipLifecycleManager.Refresh();
            }

            _npcLogisticsManager.Update(gameTime, GetCurrentInGameWeekMinute());

            DrawMarkers(player);
            _cargoTransferController.Update(gameTime, DrawProgressBar);
            DrawOpenMenus();
            DrawIndustryTablet(player);
            UpdateCargoOverviewAndIntegrity(player, gameTime);

            if (_showContext)
            {
                DrawContextPanel(player);
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage) && gameTime <= _statusMessageUntil)
            {
                Screen.ShowSubtitle(_statusMessage, 1);
            }
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

            if (e.KeyCode == _controls.ToggleDashboard)
            {
                if (_modMechanicsEnabled)
                {
                    ToggleOverviewMenu();
                }

                return;
            }

            if (HandleOverviewMenuKey(e.KeyCode))
            {
                return;
            }

            if (HandleTabletKey(e.KeyCode))
            {
                return;
            }

            if (HandleMenuKey(e.KeyCode))
            {
                return;
            }

            if (!_modMechanicsEnabled)
            {
                return;
            }

            if (e.KeyCode == _controls.ToggleContext)
            {
                _showContext = !_showContext;
                return;
            }

            if (e.KeyCode == _controls.GateInteract)
            {
                var player = Game.Player.Character;
                if (player != null && player.Exists() && TryOpenNearbyBarrier(player))
                {
                    return;
                }
            }

            if (e.KeyCode == _controls.Interact)
            {
                var player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    if (IsNearMainOffice(player.Position))
                    {
                        OpenOfficeMenu();
                        return;
                    }

                    var nearbyIndustry = GetIndustryInInteractionRange(player.Position);
                    if (nearbyIndustry != null)
                    {
                        _nearestIndustry = nearbyIndustry;
                        TryOpenIndustryTablet();
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
            return e != null
                && e.KeyCode == WinForms.Keys.W
                && e.Alt;
        }

        private static bool IsVirtualKeyDown(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        private bool HandleTabletKey(WinForms.Keys key)
        {
            if (!_industryTabletController.IsOpen)
            {
                return false;
            }

            if (_industryTabletController.HandleKey(key, _controls))
            {
                return true;
            }

            if (key == _controls.Interact || key == _controls.MenuBack || key == WinForms.Keys.Escape)
            {
                CloseIndustryTablet();
                return true;
            }

            return true;
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
                _difficultyMenu.HandleKey(key, _controls);
                return true;
            }

            if (_npcLogisticsController.AnyMenuOpen)
            {
                _npcLogisticsController.HandleKey(key);
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

        private bool HandleOverviewMenuKey(WinForms.Keys key)
        {
            return _overviewMenuController.HandleKey(key);
        }

        private bool CanHandleKeyPress(WinForms.Keys key)
        {
            var now = Game.GameTime;
            var cooldownMs = AnyMenuOpen ? 95 : 220;

            if (key == _controls.Interact || key == _controls.GateInteract)
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
            _officeMenu.Draw();
            _vehicleCargoMenu.Draw();
            _debugMenu.Draw();
            _npcLogisticsController.Draw();

            _overviewMenuController.Draw();
            if (_overviewMenuController.AnyMenuOpen)
            {
                return;
            }

            if (_modControlMenu.IsOpen || _savingOptionsMenu.IsOpen || _newSaveSetupMenu.IsOpen || _saveSlotsMenu.IsOpen || _industryPurchaseMenu.IsOpen || _difficultyMenu.IsOpen || _officeMenu.IsOpen || _vehicleCargoMenu.IsOpen || _debugMenu.IsOpen || _npcLogisticsController.AnyMenuOpen)
            {
                return;
            }

            if (_upgradeMenu.IsOpen)
            {
                _upgradeMenu.Draw();
            }
        }

        private void DrawIndustryTablet(Ped player)
        {
            _industryTabletController.Draw(
                player,
                _profit,
                _vehicleSpawnController.SelectedFilter,
                IndustryInteractionDistance,
                message => ShowStatus(message));
        }

        private void DrawMarkers(Ped player)
        {
            var playerPos = player.Position;
            var officePos = GetGroundPosition(_mainOfficeMarkerSeed);
            var canShowPrompts = !AnyMenuOpen;
            var promptShown = false;

            if (playerPos.DistanceToSquared(officePos) <= IndustryMarkerDrawDistance * IndustryMarkerDrawDistance)
            {
                World.DrawMarker(
                    MarkerType.Cylinder,
                    officePos,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(_config.MarkerRadius * 1.45f, _config.MarkerRadius * 1.45f, _config.MarkerHeight),
                    Color.FromArgb(200, 52, 170, 238),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && IsNearMainOffice(playerPos))
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to open Logistics Main Office.", KeyName(_controls.Interact))));
                    promptShown = true;
                }
            }

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

            Prop nearestBarrier;
            if (canShowPrompts && !promptShown && TryGetNearestBarrier(playerPos, out nearestBarrier))
            {
                Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format(
                    "Press {0} to open nearby gate/door.",
                    KeyName(_controls.GateInteract))));
            }
        }

        private void DrawContextPanel(Ped player)
        {
            var lines = new List<string> { "F6 CONTEXT" };

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle != null && cargoVehicle.Exists())
            {
                var state = _fleetManager.GetOrCreateCargoState(cargoVehicle);
                lines.Add(string.Format("Vehicle: {0}", cargoVehicle.DisplayName));
                lines.Add(string.Format("Cargo type: {0}", state.CargoType.ToDisplayName()));
                lines.Add(string.Format("Current cargo: {0}", state.IsEmpty ? "Empty" : state.Commodity));
                lines.Add(string.Format("Weight: {0:0.0}/{1:0.0}t", state.WeightTons, state.CapacityTons));
            }
            else
            {
                lines.Add("Veh: none nearby");
            }

            lines.Add(string.Empty);

            if (_nearestIndustry != null && player.Position.DistanceTo(GetIndustryMarkerPosition(_nearestIndustry)) <= 40f)
            {
                lines.Add(string.Format("Industry: {0}", _nearestIndustry.Name));
                lines.Add(string.Format("Inputs: {0}", JoinSet(_nearestIndustry.Inputs)));
                lines.Add(string.Format("Outputs: {0}", JoinSet(_nearestIndustry.Outputs)));
                lines.Add(string.Format("Rate: {0:0.0} t/h", _nearestIndustry.CurrentOutputPerHourTons));
                lines.Add(string.Format("Utilization: {0:0}%", _nearestIndustry.LastUtilizationPercent));
                lines.Add(string.Format("Omega: {0:0.0}/{1:0.0}t", _nearestIndustry.OmegaStorage, _nearestIndustry.OmegaCapacityTons));
            }

            else
            {
                lines.Add("Industry: none in range");
            }

            DrawPanel(lines, 0.73f, 0.08f, 0.245f, Color.FromArgb(196, 10, 15, 24), Color.FromArgb(235, 219, 165, 57));
        }

        private void UpdateCargoOverviewAndIntegrity(Ped player, int now)
        {
            Vehicle cargoVehicle;
            Vehicle driverVehicle;
            VehicleCargoState cargoState;
            if (!TryGetActiveCargoContext(player, out cargoVehicle, out driverVehicle, out cargoState))
            {
                return;
            }

            if (_cargoDamageDifficultyEnabled)
            {
                UpdateCargoDamageAndLoss(cargoVehicle, driverVehicle, cargoState, now);
            }
            else
            {
                SyncCargoDamageTracking(cargoVehicle, driverVehicle, cargoState);
            }

            if (cargoState.IsEmpty)
            {
                return;
            }

            DrawCargoOverview(cargoState);
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
            return cargoState != null && !cargoState.IsEmpty;
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

        private void DrawCargoOverview(VehicleCargoState cargoState)
        {
            if (cargoState == null || cargoState.IsEmpty)
            {
                return;
            }

            var resolution = Screen.MainWindowResolution;
            var x = resolution.Width * 0.18f;
            var y = resolution.Height * 0.845f;
            var width = resolution.Width * 0.1075f;
            var height = resolution.Height * 0.085f;
            var quantityRatio = cargoState.FillRatio;
            var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
            var quantityColor = ResolveCargoOverviewAccent(cargoState.CargoType);
            var conditionColor = ResolveCargoConditionColor(conditionRatio);
                var contentX = x + 8f;
                var titleY = y + 5f;
                var commodityY = y + (height * 0.22f);
                var quantityTextY = y + (height * 0.43f);
                var quantityBarY = y + (height * 0.60f);
                var conditionTextY = y + (height * 0.72f);
                var conditionBarY = y + (height * 0.86f);
                var barWidth = width - 16f;
                var barHeight = Math.Max(4f, height * 0.08f);

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, Color.FromArgb(92, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(204, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 4f, quantityColor);

            new TextElement(
                    "CARGO",
                    ToScriptTextCoords(resolution, contentX, titleY),
                    0.30f,
                    Color.FromArgb(248, 239, 247, 255),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            new TextElement(
                    cargoState.Commodity,
                    ToScriptTextCoords(resolution, contentX, commodityY),
                    0.19f,
                    Color.FromArgb(232, 220, 229, 239),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            new TextElement(
                    string.Format("Qty {0:0}% | {1:0.0}t", quantityRatio * 100f, cargoState.WeightTons),
                    ToScriptTextCoords(resolution, contentX, quantityTextY),
                    0.18f,
                    Color.FromArgb(224, 214, 223, 233),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            DrawCompactLoadingBar(resolution, contentX, quantityBarY, barWidth, barHeight, quantityRatio, quantityColor);

            new TextElement(
                    string.Format("Cond {0} | {1:0}%", GetCargoConditionLabel(conditionRatio), conditionRatio * 100f),
                    ToScriptTextCoords(resolution, contentX, conditionTextY),
                    0.18f,
                    conditionColor,
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            DrawCompactLoadingBar(resolution, contentX, conditionBarY, barWidth, barHeight, conditionRatio, conditionColor);

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
            CloseIndustryTablet();
            CloseNonOfficeMenus();
            RebuildOfficeMenuItems();
            _officeMenu.Open();
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
            _npcLogisticsController.Close();
            _vehicleCargoMenu.Close();
            _upgradeMenu.Close();
            _modControlMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            _industryPurchaseMenu.Close();
            _difficultyMenu.Close();
            _debugMenu.Close();
            CloseIndustryTablet();
        }

        private void CloseAllMenus()
        {
            CloseOverviewMenus();
            _npcLogisticsController.Close();
            _modControlMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            _industryPurchaseMenu.Close();
            _difficultyMenu.Close();
            _debugMenu.Close();
            _officeMenu.Close();
            _vehicleCargoMenu.Close();
            _upgradeMenu.Close();
            CloseIndustryTablet();
        }

        private void RebuildModControlMenuItems()
        {
            _modControlMenu.Title = "Game Mod Control";
            _modControlMenu.Subtitle = "Activate mechanics and configure gameplay";

            _modControlMenu.SetItems(new[]
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
                    CaptionFactory = () => "Saving Options",
                    DetailFactory = CurrentSavingOptionsDetail,
                    OnActivate = OpenSavingOptionsMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Difficulty settings",
                    DetailFactory = CurrentDifficultySettingsDetail,
                    OnActivate = OpenDifficultyMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _modControlMenu.Close(),
                },
            });
        }

        private void RebuildSavingOptionsMenuItems()
        {
            _savingOptionsMenu.Title = "Saving Options";
            _savingOptionsMenu.Subtitle = string.Format("Active save: {0}", GetCurrentSaveLabel());
            _savingOptionsMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Create new save",
                    DetailFactory = () => string.Format("Creates {0}.state.ini in {1}", "<name>", SavegamesDirectoryName),
                    OnActivate = PromptForNewSave,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Load save",
                    DetailFactory = () => "Choose from all created savegames.",
                    OnActivate = () => OpenSaveSlotsMenu(SaveSlotMenuAction.Load),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Delete save",
                    DetailFactory = () => "Delete one of your created savegames.",
                    OnActivate = () => OpenSaveSlotsMenu(SaveSlotMenuAction.Delete),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Save game",
                    DetailFactory = CurrentSaveGameDetail,
                    OnActivate = SaveCurrentNamedGame,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = ReturnToModControlMenu,
                },
            });
        }

        private void RebuildNewSaveSetupMenuItems()
        {
            _newSaveSetupMenu.Title = "Difficulty Settings";
            _newSaveSetupMenu.Subtitle = string.Format("Configure '{0}' before starting", _pendingSaveName);
            _newSaveSetupMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentStartingBalanceCaption,
                    DetailFactory = () => "Choose the opening balance for the new save.",
                    OnLeft = () => ChangeStartingBalanceSelection(-1),
                    OnRight = () => ChangeStartingBalanceSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentPendingEconomyDifficultyPresetCaption,
                    DetailFactory = CurrentPendingEconomyDifficultyPresetDetail,
                    OnLeft = () => ChangePendingEconomyDifficultyPreset(-1),
                    OnRight = () => ChangePendingEconomyDifficultyPreset(1),
                    OnActivate = () => ChangePendingEconomyDifficultyPreset(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentPendingNpcWeeklyWageDifficultyCaption,
                    DetailFactory = CurrentPendingNpcWeeklyWageDifficultyDetail,
                    OnLeft = () => ChangePendingNpcWeeklyWageDifficulty(-1),
                    OnRight = () => ChangePendingNpcWeeklyWageDifficulty(1),
                    OnActivate = () => ChangePendingNpcWeeklyWageDifficulty(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Vehicle fuel",
                    DetailFactory = () => "Enable vehicle fuel usage for this save.",
                    CheckboxStateFactory = () => _pendingVehicleFuelDifficultyEnabled,
                    OnActivate = TogglePendingVehicleFuelSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Cargo damage",
                    DetailFactory = () => "Enable cargo loss and condition damage for this save.",
                    CheckboxStateFactory = () => _pendingCargoDamageDifficultyEnabled,
                    OnActivate = TogglePendingCargoDamageSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Industry price mechanic",
                    DetailFactory = () => "Require industry purchases and apply owner cut until bought.",
                    CheckboxStateFactory = () => _pendingIndustryPricingDifficultyEnabled,
                    OnActivate = TogglePendingIndustryPricingSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Licensing system",
                    DetailFactory = () => "Require contractor permits for industry cargo transport.",
                    CheckboxStateFactory = () => _pendingLicensingDifficultyEnabled,
                    OnActivate = TogglePendingLicensingSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Create save",
                    DetailFactory = () => string.Format("Starts a fresh game as {0}.state.ini", _pendingSaveName),
                    OnActivate = FinalizeNewSave,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = CancelNewSaveSetup,
                },
            });
        }

        private void RebuildSaveSlotsMenuItems()
        {
            var entries = GetAvailableNamedSaves();
            var items = new List<OfficeMenuItem>();

            _saveSlotsMenu.Title = _saveSlotMenuAction == SaveSlotMenuAction.Load ? "Load save" : "Delete save";
            _saveSlotsMenu.Subtitle = entries.Count == 1 ? "1 savegame found" : string.Format("{0} savegames found", entries.Count);

            if (entries.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No saves found",
                    DetailFactory = () => string.Format("Create a save in {0} first.", SavegamesDirectoryName),
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
                            : (() => DeleteNamedSave(entry)),
                    });
                }
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = ReturnToSavingOptionsMenu,
            });

            _saveSlotsMenu.SetItems(items);
        }

        private void OpenSavingOptionsMenu()
        {
            _modControlMenu.Close();
            RebuildSavingOptionsMenuItems();
            _savingOptionsMenu.Open();
        }

        private void OpenSaveSlotsMenu(SaveSlotMenuAction action)
        {
            _saveSlotMenuAction = action;
            _savingOptionsMenu.Close();
            RebuildSaveSlotsMenuItems();
            _saveSlotsMenu.Open();
        }

        private void ReturnToModControlMenu()
        {
            _difficultyMenu.Close();
            _savingOptionsMenu.Close();
            _newSaveSetupMenu.Close();
            _saveSlotsMenu.Close();
            RebuildModControlMenuItems();
            _modControlMenu.Open();
        }

        private void ReturnToSavingOptionsMenu()
        {
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
            if (!industry.TryPurchase(ref _profit, out cost, out result))
            {
                ShowStatus(result);
                RebuildIndustryPurchaseMenuItems();
                return;
            }

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
            return _industryTabletController.TryOpen(
                player,
                industry,
                IndustryInteractionDistance,
                () => { },
                message => ShowStatus(message));
        }

        private void RebuildDifficultyMenuItems()
        {
            _difficultyMenu.Title = "Difficulty Settings";
            _difficultyMenu.Subtitle = _difficultySettingsLocked
                ? "Locked by the active save. Values are read-only."
                : "Enable or disable challenge options";

            _difficultyMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentEconomyDifficultyPresetCaption,
                    DetailFactory = CurrentEconomyDifficultyPresetDetail,
                    OnLeft = () => ChangeEconomyDifficultyPreset(-1),
                    OnRight = () => ChangeEconomyDifficultyPreset(1),
                    OnActivate = () => ChangeEconomyDifficultyPreset(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentNpcWeeklyWageDifficultyCaption,
                    DetailFactory = CurrentNpcWeeklyWageDifficultyDetail,
                    OnLeft = () => ChangeNpcWeeklyWageDifficulty(-1),
                    OnRight = () => ChangeNpcWeeklyWageDifficulty(1),
                    OnActivate = () => ChangeNpcWeeklyWageDifficulty(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Vehicle fuel",
                    DetailFactory = () => "Enable vehicle fuel usage for cargo operations.",
                    CheckboxStateFactory = () => _vehicleFuelDifficultyEnabled,
                    OnActivate = ToggleVehicleFuelSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Cargo damage",
                    DetailFactory = () => "Enable cargo loss and condition damage from collisions.",
                    CheckboxStateFactory = () => _cargoDamageDifficultyEnabled,
                    OnActivate = ToggleCargoDamageSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Industry price mechanic",
                    DetailFactory = () => "Require industry purchases and owner-cut payouts until bought.",
                    CheckboxStateFactory = () => _industryPricingDifficultyEnabled,
                    OnActivate = ToggleIndustryPricingSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Licensing system",
                    DetailFactory = () => "Require contractor permits before transporting cargo to or from industries.",
                    CheckboxStateFactory = () => _licensingDifficultyEnabled,
                    OnActivate = ToggleLicensingSetting,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = ReturnToModControlMenu,
                },
            });
        }

        private void OpenDifficultyMenu()
        {
            _modControlMenu.Close();
            RebuildDifficultyMenuItems();
            _difficultyMenu.Open();
        }

        private void ToggleDebugMenu()
        {
            if (_debugMenu.IsOpen)
            {
                _debugMenu.Close();
                return;
            }

            CloseAllMenus();
            RebuildDebugMenuItems();
            _debugMenu.Open();
        }

        private void ToggleOverviewMenu()
        {
            _overviewMenuController.Toggle();
        }

        private void CloseOverviewMenus()
        {
            _overviewMenuController.Close();
        }

        private string CurrentActivationCaption()
        {
            return string.Format("Activate: {0}", _modMechanicsEnabled ? "~g~On~s~" : "~r~Off~s~");
        }
        private string CurrentSavingOptionsDetail()
        {
            return string.Format("Create, load, delete, or save named games. Active: {0}.", GetCurrentSaveLabel());
        }

        private string CurrentDifficultySettingsDetail()
        {
            return _difficultySettingsLocked
                ? string.Format(
                    "Locked by the active save. Economy: {0} | NPC wages: {1}. Create a new save to change these settings.",
                    FormatEconomyDifficultyPreset(_economyDifficultyPreset),
                    FormatWeeklyWageDifficulty(_npcWeeklyWageDifficulty))
                : string.Format(
                    "Change the economy preset, vehicle fuel, cargo damage, industry pricing, licensing, and NPC wages. Economy: {0} | wages: {1}.",
                    FormatEconomyDifficultyPreset(_economyDifficultyPreset),
                    FormatWeeklyWageDifficulty(_npcWeeklyWageDifficulty));
        }

        private string CurrentSaveGameDetail()
        {
            NamedSaveEntry activeSave;
            if (!TryGetActiveNamedSave(out activeSave))
            {
                return "Create or load a named save first.";
            }

            return string.Format("Writes current progress to {0}.state.ini.", activeSave.DisplayName);
        }

        private string CurrentStartingBalanceCaption()
        {
            return string.Format("Starting balance: {0}", ModFormatting.FormatMoney(GetSelectedStartingBalance()));
        }

        private string CurrentVehicleFuelSettingCaption()
        {
            return string.Format("Vehicle fuel: {0}", _vehicleFuelDifficultyEnabled ? "~g~On~s~" : "~r~Off~s~");
        }

        private string CurrentNpcWeeklyWageDifficultyCaption()
        {
            return string.Format("NPC weekly wages: < {0} >", FormatWeeklyWageDifficulty(_npcWeeklyWageDifficulty));
        }

        private string CurrentEconomyDifficultyPresetCaption()
        {
            return string.Format("Economy preset: < {0} >", FormatEconomyDifficultyPreset(_economyDifficultyPreset));
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
            return string.Format("Economy preset: < {0} >", FormatEconomyDifficultyPreset(_pendingEconomyDifficultyPreset));
        }

        private string CurrentPendingEconomyDifficultyPresetDetail()
        {
            return BuildEconomyDifficultyPresetDetail(_pendingEconomyDifficultyPreset);
        }

        private string CurrentPendingNpcWeeklyWageDifficultyCaption()
        {
            return string.Format("NPC weekly wages: < {0} >", FormatWeeklyWageDifficulty(_pendingNpcWeeklyWageDifficulty));
        }

        private string CurrentPendingNpcWeeklyWageDifficultyDetail()
        {
            return BuildNpcWeeklyWageDifficultyDetail(_pendingNpcWeeklyWageDifficulty);
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

        private void TogglePendingVehicleFuelSetting()
        {
            _pendingVehicleFuelDifficultyEnabled = !_pendingVehicleFuelDifficultyEnabled;
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
            ShowStatus("Difficulty settings are sealed for this save. Create a new save to change them.");
        }

        private void ToggleIndustryPersistenceFromMenu()
        {
            _industryPersistenceEnabled = !_industryPersistenceEnabled;
            if (_industryPersistenceEnabled)
            {
                var restored = TryLoadIndustryPersistence(true);
                if (!restored)
                {
                    ShowStatus("Industry persistence enabled.");
                }
            }
            else
            {
                ShowStatus("Industry persistence disabled.");
            }

            RebuildModControlMenuItems();
        }

        private void ApplyDifficultySettingsToSystems()
        {
            _industryManager.SetIndustryPricingDifficultyEnabled(_industryPricingDifficultyEnabled);
            _industryManager.SetLicensingDifficultyEnabled(_licensingDifficultyEnabled);
            _industryManager.SetEconomyDifficultyPreset(_economyDifficultyPreset);
            _npcLogisticsManager.SetWeeklyWageDifficulty(_npcWeeklyWageDifficulty);

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
                "Weekly NPC payroll only. Rookie {0} | Pro {1} | Veteran {2}.",
                rookie != null ? ModFormatting.FormatMoney(rookie.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f),
                professional != null ? ModFormatting.FormatMoney(professional.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f),
                veteran != null ? ModFormatting.FormatMoney(veteran.GetWeeklyWage(difficulty)) : ModFormatting.FormatMoney(0f));
        }

        private static string FormatWeeklyWageDifficulty(NpcWeeklyWageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcWeeklyWageDifficulty.Casual:
                    return "Casual";
                case NpcWeeklyWageDifficulty.Hardcore:
                    return "Hardcore";
                default:
                    return "Standard";
            }
        }

        private string BuildEconomyDifficultyPresetDetail(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return "Industry price $200,000 | licence $8,000 | 180t input | 150t output | base 40 cyc/h. OmegaFactory x1.75, RecyclingCenter x3.00.";
                case EconomyDifficultyPreset.Hardcore:
                    return "Industry price $800,000 | licence $18,000 | 80t input | 70t output | base 24 cyc/h. OmegaFactory x1.25, RecyclingCenter x2.00.";
                default:
                    return "Industry price $450,000 | licence $13,000 | 120t input | 100t output | base 32 cyc/h. OmegaFactory x1.50, RecyclingCenter x2.50.";
            }
        }

        private static string FormatEconomyDifficultyPreset(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return "Casual";
                case EconomyDifficultyPreset.Hardcore:
                    return "Hardcore";
                default:
                    return "Standard";
            }
        }

        private static NpcWeeklyWageDifficulty OffsetWeeklyWageDifficulty(NpcWeeklyWageDifficulty current, int delta)
        {
            const int count = 3;
            var next = ((int)current + delta + count) % count;
            return (NpcWeeklyWageDifficulty)next;
        }

        private static EconomyDifficultyPreset OffsetEconomyDifficultyPreset(EconomyDifficultyPreset current, int delta)
        {
            const int count = 3;
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
            industry.TryPurchaseContractorPermit(ref _profit, out cost, out result);
            return result;
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
                ShowStatus("Mod mechanics enabled.");
                return;
            }

            _cargoTransferController.ClearState();
            _showContext = false;
            CloseOverviewMenus();
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

            ShowStatus("Mod mechanics disabled.");
        }

        private void RebuildDebugMenuItems()
        {
            _debugMenu.Title = "Debug";
            _debugMenu.Subtitle = "ALT + RCTRL + C";

            _debugMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentDebugIndustryCaption,
                    DetailFactory = CurrentDebugIndustryDetail,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentDebugResourceCaption,
                    DetailFactory = CurrentDebugResourceDetail,
                    OnLeft = () => ChangeDebugResourceSelection(-1),
                    OnRight = () => ChangeDebugResourceSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentDebugResourceAmountCaption,
                    DetailFactory = () => "Used by the add-resource action.",
                    OnLeft = () => ChangeDebugResourceAmountSelection(-1),
                    OnRight = () => ChangeDebugResourceAmountSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentDebugMoneyAmountCaption,
                    DetailFactory = () => "Used by the add-money action.",
                    OnLeft = () => ChangeDebugMoneyAmountSelection(-1),
                    OnRight = () => ChangeDebugMoneyAmountSelection(1),
                },
                new OfficeMenuItem
                {
                    IsSeparator = true,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Add money",
                    DetailFactory = () => string.Format("Adds {0} to your current balance.", ModFormatting.FormatMoney(GetSelectedDebugMoneyAmount())),
                    OnActivate = AddDebugMoney,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Add selected resource",
                    DetailFactory = () => "Adds the selected tonnage to the highlighted nearby industry resource.",
                    OnActivate = AddSelectedDebugResourceToNearbyIndustry,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Delete vehicle cargo",
                    DetailFactory = () => "Clears cargo and visuals from your current or nearest cargo vehicle.",
                    OnActivate = DeleteResolvedVehicleCargo,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Delete current vehicle",
                    DetailFactory = () => "Deletes your current vehicle and its attached trailer if present.",
                    OnActivate = DeleteCurrentVehicle,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Fill all inputs",
                    DetailFactory = () => "Fills every accepted input buffer for the nearby industry.",
                    OnActivate = FillNearbyIndustryInputs,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Empty all inputs",
                    DetailFactory = () => "Clears every accepted input buffer for the nearby industry.",
                    OnActivate = EmptyNearbyIndustryInputs,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Fill all outputs",
                    DetailFactory = () => "Fills every output buffer for the nearby industry.",
                    OnActivate = FillNearbyIndustryOutputs,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Empty all outputs",
                    DetailFactory = () => "Clears every output buffer for the nearby industry.",
                    OnActivate = EmptyNearbyIndustryOutputs,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Boost production x1000",
                    DetailFactory = () => "Multiplies the nearby industry's production rate by 1000.",
                    OnActivate = MultiplyNearbyIndustryProductionRate,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _debugMenu.Close(),
                },
            });
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
            _officeMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Profit Balance: ${0:0}", _profit),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Worker Model: < {0} >", _workerSpawnController.SelectedWorkerDisplayName),
                    OnLeft = () => ChangeWorkerIndex(-1),
                    OnRight = () => ChangeWorkerIndex(1),
                    OnActivate = ApplyWorkerModel,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Vehicle & Cargo Type",
                    DetailFactory = CurrentVehicleSpawnerSelectionDetail,
                    OnActivate = OpenVehicleCargoMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Hire NPC",
                    DetailFactory = CurrentNpcHiringDetail,
                    OnActivate = OpenNpcHiringMenu,
                },
            });
        }

        private string CurrentNpcHiringDetail()
        {
            var routeCount = _npcLogisticsManager.Contracts.Count;
            return routeCount == 1
                ? "1 active logistics route. Open the tablet-style NPC manager."
                : string.Format("{0} active logistics routes. Open the tablet-style NPC manager.", routeCount);
        }

        private void OpenNpcHiringMenu()
        {
            _officeMenu.Close();
            _npcLogisticsController.OpenRootMenu();
        }

        private void DeductProfit(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _profit -= amount;
        }

        private void RebuildVehicleCargoMenuItems()
        {
            _vehicleCargoMenu.SetItems(new[]
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
                    CaptionFactory = () => string.Format("Vehicle: < {0} >", _vehicleSpawnController.CurrentVehicleCaption),
                    OnLeft = () => ChangeVehicleSelection(-1),
                    OnRight = () => ChangeVehicleSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Truck: < {0} >", _vehicleSpawnController.CurrentTractorCaption),
                    OnLeft = () => ChangeTractorSelection(-1),
                    OnRight = () => ChangeTractorSelection(1),
                },
                new OfficeMenuItem
                {
                    IsSeparator = true,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "~b~Spawn Vehicle~s~",
                    DetailFactory = CurrentVehicleSpawnerActionDetail,
                    OnActivate = SpawnSelectedVehicle,
                },
            });
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
            else
            {
                _officeMenu.Close();
            }

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

            _industryTabletController.TryOpen(
                player,
                _menuIndustry,
                IndustryInteractionDistance,
                () => { },
                message => ShowStatus(message));
        }

        private string CurrentVehicleSpawnerSelectionDetail()
        {
            return string.Format(
                "{0} | {1}",
                _vehicleSpawnController.SelectedFilter.ToDisplayName(),
                _vehicleSpawnController.CurrentVehicleCaption);
        }

        private string CurrentVehicleSpawnerActionDetail()
        {
            if (_vehicleCargoMenuContext == VehicleCargoMenuContext.Industry)
            {
                return "Spawn the selected fleet vehicle at this industry pad.";
            }

            return "Spawn the selected fleet vehicle at the office lot.";
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

        private void ChangeDebugResourceAmountSelection(int delta)
        {
            _selectedDebugResourceAmountIndex = (_selectedDebugResourceAmountIndex + delta + DebugResourceAmountOptionsTons.Length) % DebugResourceAmountOptionsTons.Length;
        }

        private void ChangeDebugMoneyAmountSelection(int delta)
        {
            _selectedDebugMoneyAmountIndex = (_selectedDebugMoneyAmountIndex + delta + DebugMoneyAmountOptions.Length) % DebugMoneyAmountOptions.Length;
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

            _industryTabletController.TryOpen(
                player,
                _nearestIndustry,
                IndustryInteractionDistance,
                () =>
                {
                    _menuIndustry = _nearestIndustry;
                    _officeMenu.Close();
                    _upgradeMenu.Close();
                },
                message => ShowStatus(message));
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

            if (!cargoState.IsEmpty)
            {
                ShowStatus("Vehicle already carries cargo. Unload first.");
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

            var selectedProduct = GetPreferredOreCommodity(products);
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

            if (!cargoState.IsEmpty)
            {
                ShowStatus("Vehicle already carries cargo. Unload first.");
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            StartTabletLoadTransfer(industry, cargoVehicle, cargoState, cargoType, selectedProduct);
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
                ShowStatus(string.Format("Vehicle cargo is {0}. Omega fluid required.", cargoState.Commodity));
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
            _cargoTransferController.StartTabletUnloadTransfer(
                industry,
                cargoVehicle,
                cargoState,
                omegaOnly,
                () =>
                {
                    CloseIndustryTablet();
                },
                amount => _profit += amount);
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
            if (!industry.TryUpgradeModule(module, ref _profit, out cost, out result))
            {
                ShowStatus(result);
                return;
            }

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

            _menuIndustry = industry;
            OpenIndustryVehicleSpawnerMenu();
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

            spawnPosition = _config.VehicleSpawnPosition;
            spawnHeading = _config.VehicleSpawnHeading;
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
            if (!_menuIndustry.TryUpgradeModule(module, ref _profit, out cost, out result))
            {
                ShowStatus(result);
                return;
            }

            _menuIndustry.ClampBuffersToCapacity();
            ShowStatus(result);
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
            _industryTabletController.Close();
        }

        private void CreateMapBlips()
        {
            _blipLifecycleManager.Create();
        }

        private void RefreshBlipPositions()
        {
            _blipLifecycleManager.Refresh();
        }

        private void DestroyMapBlips()
        {
            _blipLifecycleManager.Destroy();
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

            _profit += amount;
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
            Notification.PostTicker(prefixed, false, false);
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

        private bool TryGetNearestBarrier(Vector3 playerPos, out Prop nearestBarrier)
        {
            return _barrierInteractionHandler.TryGetNearestBarrier(playerPos, out nearestBarrier);
        }

        private bool TryOpenNearbyBarrier(Ped player)
        {
            return _barrierInteractionHandler.TryOpenNearbyBarrier(player);
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
            _npcLogisticsManager.ClearAll();
            DestroyMapBlips();
            _cargoTransferController.ClearState();
            _barrierInteractionHandler.ClearState();
            CloseAllMenus();
            _industryTabletController.LoadRequested -= HandleTabletLoadRequested;
            _industryTabletController.LoadCommodityRequested -= HandleTabletLoadCommodityRequested;
            _industryTabletController.UnloadRequested -= HandleTabletUnloadRequested;
            _industryTabletController.UnloadModeRequested -= HandleTabletUnloadModeRequested;
            _industryTabletController.IndustryPurchaseRequested -= HandleTabletIndustryPurchaseRequested;
            _industryTabletController.UpgradeModuleRequested -= HandleTabletUpgradeModuleRequested;
            _industryTabletController.VehicleSpawnerRequested -= HandleTabletVehicleSpawnerRequested;
            _heldKeys.Clear();
        }

        private enum VehicleCargoMenuContext
        {
            Office = 0,
            Industry = 1,
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
            public NamedSaveEntry(string displayName, string filePath)
            {
                DisplayName = displayName;
                FilePath = filePath;
            }

            public string DisplayName { get; private set; }

            public string FilePath { get; private set; }
        }

    }
}

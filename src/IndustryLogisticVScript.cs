using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;
using IndustryLogisticV.Systems;
using IndustryLogisticV.UI;
using OfficeMenuItem = IndustryLogisticV.UI.MenuItem;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV
{
    public sealed class IndustryLogisticVScript : Script
    {
        private const string MessagePrefix = "~y~[IndustryLogisticV]~s~ ";
        private const float IndustryMarkerDrawDistance = 180f;
        private const float IndustryInteractionDistance = 4.8f;
        private const float OfficeInteractionDistance = 3.8f;
        private const float CargoRigMaxBodyHealth = 1000f;
        private const float CargoDamageGraceHealth = 40f;
        private const float CargoConditionLossPerDamageRatio = 0.75f;
        private const float CargoLossPerDamageRatio = 0.35f;

        private readonly ModConfig _config;
        private readonly string _configPath;
        private readonly string _industryStatePath;
        private readonly ControlBindings _controls;
        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly GlobalMarketManager _globalMarket;

        private readonly LemonMenu _officeMenu;
        private readonly SimpleMenu _industryMenu;
        private readonly SimpleMenu _upgradeMenu;
        private readonly LemonMenu _modControlMenu;
        private readonly LemonMenu _difficultyMenu;
        private readonly BarrierInteractionHandler _barrierInteractionHandler;
        private readonly BlipLifecycleManager _blipLifecycleManager;
        private readonly CargoTransferController _cargoTransferController;
        private readonly OverviewMenuController _overviewMenuController;
        private readonly VehicleSpawnController _vehicleSpawnController;
        private readonly WorkerSpawnController _workerSpawnController;
        private readonly IndustryTabletController _industryTabletController;

        private readonly Dictionary<WinForms.Keys, int> _keyCooldownUntil;
        private readonly HashSet<WinForms.Keys> _heldKeys;

        private readonly Vector3 _mainOfficeMarkerSeed;
        private readonly Vector3 _vehicleSpawnMarkerSeed;

        private List<string> _industryTransferProducts;

        private Industry _nearestIndustry;
        private Industry _menuIndustry;

        private int _selectedIndustryProductIndex;
        private int _lastIndustryTickMs;
        private int _lastNearestProbeMs;
        private int _lastBlipRefreshMs;
        private int _statusMessageUntil;

        private string _statusMessage;

        private float _profit;
        private GameModMode _gameModMode;
        private IndustryTransferMode _industryTransferMode;

        private bool _showContext;
        private bool _modMechanicsEnabled;
        private bool _industryPersistenceEnabled;
        private bool _cargoDamageDifficultyEnabled;
        private bool _vehicleFuelDifficultyEnabled;

        public IndustryLogisticVScript()
        {
            _configPath = ResolveConfigPath();
            _industryStatePath = ResolveIndustryStatePath(_configPath);
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
            _vehicleSpawnController = new VehicleSpawnController(
                _fleetManager,
                _vehicleSpawnMarkerSeed,
                _config.VehicleSpawnHeading,
                new[] { VehicleCargoType.Loose, VehicleCargoType.Crate, VehicleCargoType.Solid, VehicleCargoType.Fluid },
                VehicleCargoType.Crate);
            _workerSpawnController = new WorkerSpawnController(_config.WorkerModels);

            _officeMenu = new LemonMenu("Office")
            {
                Subtitle = "Manage workers and fleet deployment",
                AlignRight = true,
            };
            _industryMenu = new SimpleMenu("Industry Transfer")
            {
                Subtitle = "Load and unload cargo",
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
            _difficultyMenu = new LemonMenu("Difficulty Settings")
            {
                Subtitle = "Enable or disable challenge options",
                AlignRight = true,
            };
            _overviewMenuController = new OverviewMenuController(_controls, _industryManager, CloseAllMenus);
            _industryTabletController = new IndustryTabletController(
                _fleetManager,
                _industryManager,
                _globalMarket,
                GetIndustryMarkerPosition);
            _industryTabletController.LoadRequested += HandleTabletLoadRequested;
            _industryTabletController.LoadCommodityRequested += HandleTabletLoadCommodityRequested;
            _industryTabletController.UnloadRequested += HandleTabletUnloadRequested;
            _industryTabletController.UnloadModeRequested += HandleTabletUnloadModeRequested;
            _industryTabletController.UpgradeModuleRequested += HandleTabletUpgradeModuleRequested;

            _keyCooldownUntil = new Dictionary<WinForms.Keys, int>();
            _heldKeys = new HashSet<WinForms.Keys>();

            _industryTransferProducts = new List<string>();
            _gameModMode = GameModMode.Fun;
            _industryTransferMode = IndustryTransferMode.Load;
            _profit = 20000f;
            _modMechanicsEnabled = false;
            _industryPersistenceEnabled = true;
            _cargoDamageDifficultyEnabled = true;
            _vehicleFuelDifficultyEnabled = false;

            if (_industryPersistenceEnabled)
            {
                TryLoadIndustryPersistence(false);
            }

            RebuildOfficeMenuItems();
            RebuildModControlMenuItems();
            RebuildDifficultyMenuItems();

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
                    || _industryMenu.IsOpen
                    || _upgradeMenu.IsOpen
                    || _modControlMenu.IsOpen
                    || _difficultyMenu.IsOpen
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

            if (_officeMenu.IsOpen)
            {
                _officeMenu.HandleKey(key, _controls);
                return true;
            }

            if (_industryMenu.IsOpen)
            {
                _industryMenu.HandleKey(key, _controls);
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
            _difficultyMenu.Draw();
            _officeMenu.Draw();

            _overviewMenuController.Draw();
            if (_overviewMenuController.AnyMenuOpen)
            {
                return;
            }

            if (_modControlMenu.IsOpen || _difficultyMenu.IsOpen || _officeMenu.IsOpen)
            {
                return;
            }

            if (_industryMenu.IsOpen)
            {
                _industryMenu.Draw();
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
                        "Press {0} for opening the industry menu.",
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
                lines.Add(string.Format("Cargo type: {0}", state.CargoType));
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
                : Clamp01(damageDelta / CargoRigMaxBodyHealth);

            var collisionDamageRatio = 0f;
            if (HasRigCollision(driverVehicle, cargoVehicle) && previousRigSpeed > 0.001f)
            {
                var speedDrop = Math.Max(0f, previousRigSpeed - currentRigSpeed);
                if (speedDrop >= 1.1f)
                {
                    collisionDamageRatio = Clamp01(speedDrop / 70f);
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

            if (cargoState.CargoType == VehicleCargoType.Crate || cargoState.CargoType == VehicleCargoType.Solid)
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
            var conditionRatio = Clamp01(cargoState.CargoCondition);
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
            ratio = Clamp01(ratio);
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
                case VehicleCargoType.Fluid:
                    return 1.25f;
                case VehicleCargoType.Loose:
                    return 0.95f;
                case VehicleCargoType.Crate:
                    return 0.6f;
                case VehicleCargoType.Solid:
                    return 0.45f;
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
                case VehicleCargoType.Fluid:
                    return Color.FromArgb(228, 88, 150, 214);
                case VehicleCargoType.Loose:
                    return Color.FromArgb(228, 168, 144, 92);
                case VehicleCargoType.Crate:
                    return Color.FromArgb(228, 118, 188, 138);
                case VehicleCargoType.Solid:
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
            _industryMenu.Close();
            _upgradeMenu.Close();
            _modControlMenu.Close();
            _difficultyMenu.Close();
            CloseIndustryTablet();
        }

        private void CloseAllMenus()
        {
            CloseOverviewMenus();
            _modControlMenu.Close();
            _difficultyMenu.Close();
            _officeMenu.Close();
            _industryMenu.Close();
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
                    CaptionFactory = CurrentIndustryPersistenceCaption,
                    OnLeft = ToggleIndustryPersistenceFromMenu,
                    OnRight = ToggleIndustryPersistenceFromMenu,
                    OnActivate = ToggleIndustryPersistenceFromMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentGameModeCaption,
                    OnLeft = () => ChangeGameModMode(-1),
                    OnRight = () => ChangeGameModMode(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Difficulty settings",
                    OnActivate = OpenDifficultyMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _modControlMenu.Close(),
                },
            });
        }

        private void RebuildDifficultyMenuItems()
        {
            _difficultyMenu.Title = "Difficulty Settings";
            _difficultyMenu.Subtitle = "Enable or disable challenge options";

            _difficultyMenu.SetItems(new[]
            {
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
                    CaptionFactory = () => "Back",
                    OnActivate = () =>
                    {
                        _difficultyMenu.Close();
                        RebuildModControlMenuItems();
                        _modControlMenu.Open();
                    },
                },
            });
        }

        private void OpenDifficultyMenu()
        {
            _modControlMenu.Close();
            RebuildDifficultyMenuItems();
            _difficultyMenu.Open();
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

        private string CurrentGameModeCaption()
        {
            return string.Format("Game mod: {0}", _gameModMode == GameModMode.Fun ? "Fun" : "Career");
        }

        private string CurrentIndustryPersistenceCaption()
        {
            return string.Format("Industry persistence: {0}", _industryPersistenceEnabled ? "~g~On~s~" : "~r~Off~s~");
        }

        private string CurrentVehicleFuelSettingCaption()
        {
            return string.Format("Vehicle fuel: {0}", _vehicleFuelDifficultyEnabled ? "~g~On~s~" : "~r~Off~s~");
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

        private void ToggleVehicleFuelSetting()
        {
            _vehicleFuelDifficultyEnabled = !_vehicleFuelDifficultyEnabled;
        }

        private void ToggleCargoDamageSetting()
        {
            _cargoDamageDifficultyEnabled = !_cargoDamageDifficultyEnabled;
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

        private bool TryLoadIndustryPersistence(bool notifyWhenNoData)
        {
            try
            {
                var restoredCount = IndustryPersistenceManager.Load(_industryStatePath, _industryManager.Industries);
                if (restoredCount > 0)
                {
                    ShowStatus(string.Format("Loaded saved industry state for {0} nodes.", restoredCount), 4000);
                    return true;
                }

                if (notifyWhenNoData)
                {
                    ShowStatus("No saved industry state found yet.");
                }
            }
            catch (Exception)
            {
                ShowStatus("Failed to load industry persistence data.");
            }

            return false;
        }

        private void TrySaveIndustryPersistence()
        {
            if (!_industryPersistenceEnabled)
            {
                return;
            }

            try
            {
                IndustryPersistenceManager.Save(_industryStatePath, _industryManager.Industries);
            }
            catch (Exception)
            {
                ShowStatus("Failed to save industry persistence data.");
            }
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
            _industryMenu.Close();
            _upgradeMenu.Close();
            _difficultyMenu.Close();
            CloseIndustryTablet();
            DestroyMapBlips();
            _lastIndustryTickMs = Game.GameTime;

            if (!keepControlMenuOpen)
            {
                _modControlMenu.Close();
            }

            ShowStatus("Mod mechanics disabled.");
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
                    CaptionFactory = () => string.Format("Worker Model: {0}", _workerSpawnController.SelectedWorkerDisplayName),
                    OnLeft = () => ChangeWorkerIndex(-1),
                    OnRight = () => ChangeWorkerIndex(1),
                    OnActivate = ApplyWorkerModel,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Cargo Filter: {0}", _vehicleSpawnController.SelectedFilter),
                    OnLeft = () => ChangeFilter(-1),
                    OnRight = () => ChangeFilter(1),
                    OnActivate = RefreshFilteredVehicles,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => _vehicleSpawnController.CurrentVehicleCaption,
                    OnLeft = () => ChangeVehicleSelection(-1),
                    OnRight = () => ChangeVehicleSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => _vehicleSpawnController.CurrentTractorCaption,
                    OnLeft = () => ChangeTractorSelection(-1),
                    OnRight = () => ChangeTractorSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Spawn Vehicle"),
                    OnActivate = SpawnSelectedVehicle,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _officeMenu.Close(),
                },
            });
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
            RebuildOfficeMenuItems();
        }

        private void RefreshFilteredVehicles()
        {
            _vehicleSpawnController.RefreshFilteredVehicles();
        }

        private void ChangeVehicleSelection(int delta)
        {
            _vehicleSpawnController.ChangeVehicleSelection(delta);
            RebuildOfficeMenuItems();
        }

        private void ChangeTractorSelection(int delta)
        {
            _vehicleSpawnController.ChangeTractorSelection(delta);
        }

        private void SpawnSelectedVehicle()
        {
            Vehicle truck;
            Vehicle cargoVehicle;
            string message;
            if (!_vehicleSpawnController.SpawnSelectedVehicle(GetGroundPosition, out truck, out cargoVehicle, out message))
            {
                ShowStatus(message);
                return;
            }

            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                player.SetIntoVehicle(truck, VehicleSeat.Driver);
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
                    _industryMenu.Close();
                    _upgradeMenu.Close();
                },
                message => ShowStatus(message));
        }

        private void TryOpenIndustryTransferMenu()
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

            if (player.CurrentVehicle != null && player.CurrentVehicle.Exists())
            {
                ShowStatus("Exit your vehicle to open the industry terminal.");
                return;
            }

            if (_nearestIndustry == null || player.Position.DistanceTo(GetIndustryMarkerPosition(_nearestIndustry)) > IndustryInteractionDistance)
            {
                ShowStatus("No industry marker in range.");
                return;
            }

            _menuIndustry = _nearestIndustry;
            _industryTransferMode = DetermineDefaultTransferMode();
            _selectedIndustryProductIndex = 0;

            _officeMenu.Close();
            _upgradeMenu.Close();
            RebuildIndustryMenuItems();
            _industryMenu.Open();
        }

        private IndustryTransferMode DetermineDefaultTransferMode()
        {
            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(Game.Player.Character, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return IndustryTransferMode.Load;
            }

            var state = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (state == null || state.IsEmpty)
            {
                return IndustryTransferMode.Load;
            }

            if (_menuIndustry != null && _menuIndustry.AcceptsCommodity(state.Commodity))
            {
                return IndustryTransferMode.Unload;
            }

            return IndustryTransferMode.Load;
        }

        private bool TryGetIndustryMenuContext(out Industry industry, out Vehicle cargoVehicle, out VehicleCargoState cargoState, out string error)
        {
            industry = _menuIndustry;
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

            if (player.CurrentVehicle != null && player.CurrentVehicle.Exists())
            {
                error = "Exit your vehicle to use the terminal.";
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
                error = "Bring a vehicle close to the industry.";
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
                    _industryMenu.Close();
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
                    _industryMenu.Close();
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

        private void RebuildIndustryMenuItems()
        {
            Industry industry;
            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryMenuContext(out industry, out cargoVehicle, out cargoState, out error))
            {
                _industryMenu.Title = "Industry Transfer";
                _industryMenu.Subtitle = error;
                _industryMenu.SetItems(new[]
                {
                    new OfficeMenuItem { CaptionFactory = () => "Close", OnActivate = () => _industryMenu.Close() },
                });
                return;
            }

            RefreshIndustryTransferProducts(industry, cargoState);

            _industryMenu.Title = "Industry Transfer";
            _industryMenu.Subtitle = string.Format("{0} | Vehicle: {1}", industry.Name, cargoVehicle.DisplayName);
            _industryMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Cargo: {0}", cargoState.IsEmpty ? "Empty" : string.Format("{0:0.0}t {1}", cargoState.WeightTons, cargoState.Commodity)),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentIndustryActionCaption,
                    OnLeft = () => ChangeIndustryTransferMode(-1),
                    OnRight = () => ChangeIndustryTransferMode(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentIndustryProductCaption,
                    OnLeft = () => ChangeIndustryProductSelection(-1),
                    OnRight = () => ChangeIndustryProductSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentIndustryTransferPreviewCaption,
                    OnActivate = ExecuteIndustryTransferFromMenu,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _industryMenu.Close(),
                },
            });
        }

        private string CurrentIndustryActionCaption()
        {
            return string.Format("Action: {0}", _industryTransferMode == IndustryTransferMode.Load ? "Load" : "Unload");
        }

        private string CurrentIndustryProductCaption()
        {
            if (_industryTransferProducts.Count == 0)
            {
                return "Product: none available";
            }

            return string.Format("Product: {0}", _industryTransferProducts[_selectedIndustryProductIndex]);
        }

        private string CurrentIndustryTransferPreviewCaption()
        {
            Industry industry;
            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryMenuContext(out industry, out cargoVehicle, out cargoState, out error))
            {
                return "Transfer unavailable";
            }

            if (_industryTransferMode == IndustryTransferMode.Unload)
            {
                if (cargoState.IsEmpty)
                {
                    return "Unload: vehicle empty";
                }

                return string.Format("Confirm Unload ({0:0.0}t)", cargoState.WeightTons);
            }

            if (_industryTransferProducts.Count == 0)
            {
                return "Load: no compatible product";
            }

            var requested = Math.Max(0.5f, cargoState.FreeCapacityTons);
            return string.Format("Confirm Load ({0:0.0}t max)", requested);
        }

        private void ChangeIndustryTransferMode(int delta)
        {
            _industryTransferMode = _industryTransferMode == IndustryTransferMode.Load
                ? IndustryTransferMode.Unload
                : IndustryTransferMode.Load;

            _selectedIndustryProductIndex = 0;
            RebuildIndustryMenuItems();
        }

        private void ChangeIndustryProductSelection(int delta)
        {
            if (_industryTransferProducts.Count == 0)
            {
                return;
            }

            _selectedIndustryProductIndex = (_selectedIndustryProductIndex + delta + _industryTransferProducts.Count) % _industryTransferProducts.Count;
        }

        private void RefreshIndustryTransferProducts(Industry industry, VehicleCargoState cargoState)
        {
            _industryTransferProducts.Clear();

            if (_industryTransferMode == IndustryTransferMode.Load)
            {
                if (!cargoState.IsEmpty)
                {
                    _selectedIndustryProductIndex = 0;
                    return;
                }

                var cargoType = cargoState.CargoType;
                if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
                {
                    cargoType = _vehicleSpawnController.SelectedFilter;
                }

                _industryTransferProducts = _industryManager.GetLoadableOutputs(industry, cargoType);
            }
            else
            {
                if (!cargoState.IsEmpty && industry.AcceptsCommodity(cargoState.Commodity))
                {
                    _industryTransferProducts.Add(cargoState.Commodity);
                }
            }

            if (_selectedIndustryProductIndex >= _industryTransferProducts.Count)
            {
                _selectedIndustryProductIndex = Math.Max(0, _industryTransferProducts.Count - 1);
            }
        }

        private void ExecuteIndustryTransferFromMenu()
        {
            if (_cargoTransferController.HasPendingTransfer)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            Industry industry;
            Vehicle cargoVehicle;
            VehicleCargoState cargoState;
            string error;
            if (!TryGetIndustryMenuContext(out industry, out cargoVehicle, out cargoState, out error))
            {
                ShowStatus(error);
                RebuildIndustryMenuItems();
                return;
            }

            if (_industryTransferMode == IndustryTransferMode.Unload)
            {
                if (cargoState.IsEmpty)
                {
                    ShowStatus("Vehicle is empty.");
                    return;
                }

                if (!industry.AcceptsCommodity(cargoState.Commodity))
                {
                    ShowStatus(string.Format("Can't unload {0} in this industry", cargoState.CargoType));
                    return;
                }

                _cargoTransferController.StartMenuUnloadTransfer(
                    industry,
                    cargoVehicle,
                    cargoState,
                    () => _industryMenu.Close(),
                    amount => _profit += amount);

                return;
            }

            if (!cargoState.IsEmpty)
            {
                ShowStatus("Vehicle already carries cargo. Unload first.");
                return;
            }

            if (_industryTransferProducts.Count == 0)
            {
                ShowStatus("No compatible product available to load.");
                return;
            }

            var selectedProduct = _industryTransferProducts[_selectedIndustryProductIndex];
            _cargoTransferController.StartMenuLoadTransfer(
                industry,
                cargoVehicle,
                cargoState,
                selectedProduct,
                () => _industryMenu.Close());
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
            CloseIndustryTablet();
            _officeMenu.Close();
            _industryMenu.Close();
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

            if (ShouldUseConfiguredZForMarker(industry.Name))
            {
                return new Vector3(industry.Position.X, industry.Position.Y, industry.Position.Z + 0.05f);
            }

            return GetGroundPosition(industry.Position);
        }

        private static bool ShouldUseConfiguredZForMarker(string markerName)
        {
            if (string.IsNullOrWhiteSpace(markerName))
            {
                return false;
            }

            var normalized = markerName.Trim();
            if (MarkerConstants.PreserveConfiguredZMarkerNames.Contains(normalized))
            {
                return true;
            }

            return normalized.IndexOf("Marina Dr", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("Marina Drive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PointF ToScriptTextCoords(Size resolution, float x, float y)
        {
            const float scriptWidth = 1280f;
            const float scriptHeight = 720f;
            return new PointF(
                x * (scriptWidth / resolution.Width),
                y * (scriptHeight / resolution.Height));
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
            float z;
            if (World.GetGroundHeight(new Vector3(input.X, input.Y, 1000f), out z, GetGroundHeightMode.ConsiderWaterAsGroundNoWaves))
            {
                return new Vector3(input.X, input.Y, z + 0.05f);
            }

            if (World.GetGroundHeight(new Vector3(input.X, input.Y, 1000f), out z, GetGroundHeightMode.ConsiderWaterAsGround))
            {
                return new Vector3(input.X, input.Y, z + 0.05f);
            }

            if (World.GetGroundHeight(new Vector3(input.X, input.Y, input.Z + 50f), out z, GetGroundHeightMode.Normal))
            {
                return new Vector3(input.X, input.Y, z + 0.05f);
            }

            return new Vector3(input.X, input.Y, input.Z + 0.05f);
        }

        private string ResolveConfigPath()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(assemblyDir, "IndustryLogisticV.ini"),
                Path.Combine(BaseDirectory, "IndustryLogisticV.ini"),
                Path.Combine(BaseDirectory, "scripts", "IndustryLogisticV.ini"),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        private string ResolveIndustryStatePath(string configPath)
        {
            var configDirectory = string.IsNullOrWhiteSpace(configPath)
                ? string.Empty
                : Path.GetDirectoryName(configPath) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                return Path.Combine(configDirectory, "IndustryLogisticV.state.ini");
            }

            return Path.Combine(BaseDirectory, "IndustryLogisticV.state.ini");
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

            if (message.IndexOf("[IndustryLogisticV]", StringComparison.OrdinalIgnoreCase) >= 0)
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
            if (industry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(industry.Id) && industry.Id.IndexOf("Petrol Station", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return industry.IsSink && industry.Inputs.Count == 1 && industry.Inputs.Contains("Fuel");
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }

        private void OnAborted(object sender, EventArgs e)
        {
            TrySaveIndustryPersistence();
            DestroyMapBlips();
            _cargoTransferController.ClearState();
            _barrierInteractionHandler.ClearState();
            CloseAllMenus();
            _industryTabletController.LoadRequested -= HandleTabletLoadRequested;
            _industryTabletController.LoadCommodityRequested -= HandleTabletLoadCommodityRequested;
            _industryTabletController.UnloadRequested -= HandleTabletUnloadRequested;
            _industryTabletController.UnloadModeRequested -= HandleTabletUnloadModeRequested;
            _industryTabletController.UpgradeModuleRequested -= HandleTabletUpgradeModuleRequested;
            _heldKeys.Clear();
        }

        private enum IndustryTransferMode
        {
            Load = 0,
            Unload = 1,
        }

        private enum GameModMode
        {
            Fun = 0,
            Career = 1,
        }

    }
}

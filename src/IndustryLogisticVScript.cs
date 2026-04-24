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

        private readonly ModConfig _config;
        private readonly ControlBindings _controls;
        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly GlobalMarketManager _globalMarket;

        private readonly SimpleMenu _officeMenu;
        private readonly SimpleMenu _industryMenu;
        private readonly SimpleMenu _upgradeMenu;
        private readonly IndustryTabletUi _industryTablet;

        private readonly List<Blip> _industryBlips;
        private readonly List<VehicleCargoType> _filterOrder;
        private readonly List<VehicleDefinition> _tractorVehicles;
        private readonly Dictionary<WinForms.Keys, int> _keyCooldownUntil;
        private readonly HashSet<WinForms.Keys> _heldKeys;
        private readonly Dictionary<string, string> _workerNameOverrides;

        private readonly Vector3 _mainOfficeMarkerSeed;
        private readonly Vector3 _vehicleSpawnMarkerSeed;

        private Blip _officeBlip;
        private Blip _vehicleSpawnBlip;

        private List<VehicleDefinition> _filteredVehicles;
        private List<string> _industryTransferProducts;

        private Industry _nearestIndustry;
        private Industry _menuIndustry;

        private int _workerIndex;
        private int _selectedVehicleIndex;
        private int _selectedTractorIndex;
        private int _selectedIndustryProductIndex;
        private int _dashboardViewButtonIndex;
        private int _dashboardPage;
        private int _lastIndustryTickMs;
        private int _lastNearestProbeMs;
        private int _lastBlipRefreshMs;
        private int _statusMessageUntil;

        private string _statusMessage;

        private float _profit;
        private VehicleCargoType _selectedFilter;
        private DashboardOverviewMode _dashboardView;
        private IndustryTransferMode _industryTransferMode;

        private bool _showDashboard;
        private bool _showContext;

        private PendingTransfer _pendingTransfer;

        public IndustryLogisticVScript()
        {
            var configPath = ResolveConfigPath();
            _config = ModConfig.Load(configPath);
            _controls = _config.Controls ?? new ControlBindings();
            _industryManager = new IndustryManager(_config);
            _fleetManager = new FleetManager(_config);
            _globalMarket = new GlobalMarketManager(Game.GameTime);

            _mainOfficeMarkerSeed = _config.MainOfficePosition;
            _vehicleSpawnMarkerSeed = _config.VehicleSpawnPosition;

            _officeMenu = new SimpleMenu("Industrial Logistics Office")
            {
                Subtitle = "Manage workers and fleet deployment",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.6f,
                TabletAlignRight = true,
            };
            _industryMenu = new SimpleMenu("Industry Transfer")
            {
                Subtitle = "Load and unload cargo",
            };
            _upgradeMenu = new SimpleMenu("Industry Upgrades")
            {
                Subtitle = "Invest profits into modules",
            };
            _industryTablet = new IndustryTabletUi();
            _industryTablet.LoadRequested += HandleTabletLoadRequested;
            _industryTablet.LoadCommodityRequested += HandleTabletLoadCommodityRequested;
            _industryTablet.UnloadRequested += HandleTabletUnloadRequested;
            _industryTablet.UnloadModeRequested += HandleTabletUnloadModeRequested;
            _industryTablet.UpgradeModuleRequested += HandleTabletUpgradeModuleRequested;

            _industryBlips = new List<Blip>();
            _filterOrder = new List<VehicleCargoType> { VehicleCargoType.Loose, VehicleCargoType.Crate, VehicleCargoType.Fluid };
            _tractorVehicles = _fleetManager.GetTractorDefinitions();
            _keyCooldownUntil = new Dictionary<WinForms.Keys, int>();
            _heldKeys = new HashSet<WinForms.Keys>();
            _workerNameOverrides = CreateWorkerNameOverrides();

            _selectedFilter = VehicleCargoType.Crate;
            _filteredVehicles = new List<VehicleDefinition>();
            _industryTransferProducts = new List<string>();
            _dashboardView = DashboardOverviewMode.Industries;
            _dashboardViewButtonIndex = 1;
            _industryTransferMode = IndustryTransferMode.Load;
            _profit = 20000f;

            RefreshFilteredVehicles();
            RebuildOfficeMenuItems();
            CreateMapBlips();

            Tick += OnTick;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Aborted += OnAborted;

            Notification.PostTicker(MessagePrefix + "Loaded successfully.", false, false);
        }

        private bool AnyMenuOpen
        {
            get { return _officeMenu.IsOpen || _industryMenu.IsOpen || _upgradeMenu.IsOpen || _industryTablet.IsOpen; }
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
                RefreshBlipPositions();
            }

            DrawMarkers(player);
            UpdateTransfer(gameTime);
            DrawOpenMenus();
            DrawIndustryTablet(player);

            if (_showDashboard)
            {
                try
                {
                    DrawDashboard(player);
                }
                catch (Exception)
                {
                    _showDashboard = false;
                    ShowStatus("Dashboard draw error detected. Reopen with F8.");
                }
            }

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

            if (HandleTabletKey(e.KeyCode))
            {
                return;
            }

            if (HandleMenuKey(e.KeyCode))
            {
                return;
            }

            if (e.KeyCode == _controls.ToggleDashboard)
            {
                _showDashboard = !_showDashboard;
                return;
            }

            if (e.KeyCode == _controls.ToggleContext)
            {
                _showContext = !_showContext;
                return;
            }

            if (_showDashboard && HandleDashboardKey(e.KeyCode))
            {
                return;
            }

            if (e.KeyCode == _controls.Interact)
            {
                var player = Game.Player.Character;
                if (player != null && player.Exists() && IsNearMainOffice(player.Position))
                {
                    OpenOfficeMenu();
                    return;
                }

                if (player != null && player.Exists())
                {
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
            if (!_industryTablet.IsOpen)
            {
                return false;
            }

            if (_industryTablet.HandleKey(key, _controls))
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

        private bool HandleDashboardKey(WinForms.Keys key)
        {
            if (!_showDashboard)
            {
                return false;
            }

            if (key == _controls.MenuLeft)
            {
                _dashboardViewButtonIndex = _dashboardViewButtonIndex <= 0 ? 1 : 0;
                return true;
            }

            if (key == _controls.MenuRight)
            {
                _dashboardViewButtonIndex = (_dashboardViewButtonIndex + 1) % 2;
                return true;
            }

            if (key == _controls.MenuSelect)
            {
                var selectedView = _dashboardViewButtonIndex == 0
                    ? DashboardOverviewMode.PetrolStations
                    : DashboardOverviewMode.Industries;

                if (_dashboardView != selectedView)
                {
                    _dashboardView = selectedView;
                    _dashboardPage = 0;
                }

                return true;
            }

            if (key == _controls.DashboardPageDown)
            {
                _dashboardPage += 1;
                return true;
            }

            if (key == _controls.DashboardPageUp)
            {
                _dashboardPage = Math.Max(0, _dashboardPage - 1);
                return true;
            }

            return false;
        }

        private bool CanHandleKeyPress(WinForms.Keys key)
        {
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
            if (_officeMenu.IsOpen)
            {
                _officeMenu.Draw();
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
            if (!_industryTablet.IsOpen)
            {
                return;
            }

            if (player == null || !player.Exists())
            {
                CloseIndustryTablet();
                return;
            }

            var industry = _industryTablet.ActiveIndustry;
            if (industry == null)
            {
                CloseIndustryTablet();
                return;
            }

            if (player.Position.DistanceTo(GetGroundPosition(industry.Position)) > IndustryInteractionDistance + 2.4f)
            {
                ShowStatus("Tablet signal lost. Move closer to the industry marker.");
                CloseIndustryTablet();
                return;
            }

            _industryTablet.UpdateProfitBalance(_profit);
            UpdateTabletLoadOptions(player, industry);
            _industryTablet.DrawAndHandleInput();
        }

        private void UpdateTabletLoadOptions(Ped player, Industry industry)
        {
            if (!_industryTablet.IsOpen || industry == null || player == null || !player.Exists())
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || !cargoState.IsEmpty)
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = _selectedFilter;
            }

            _industryTablet.SetLoadOptions(_industryManager.GetLoadableOutputs(industry, cargoType));
        }

        private void DrawMarkers(Ped player)
        {
            var playerPos = player.Position;
            var officePos = GetGroundPosition(_mainOfficeMarkerSeed);
            var canShowPrompts = !AnyMenuOpen;

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
                }
            }

            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var industry = _industryManager.Industries[i];
                var markerPos = GetGroundPosition(industry.Position);

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
                }
            }
        }

        private void DrawDashboard(Ped player)
        {
            var filtered = _industryManager.Industries
                .Where(x => _dashboardView == DashboardOverviewMode.PetrolStations
                    ? IsPetrolServiceStation(x)
                    : !IsPetrolServiceStation(x))
                .OrderBy(x => x.Position.DistanceToSquared(player.Position))
                .ToList();

            const int pageSize = 10;
            var pageCount = Math.Max(1, (int)Math.Ceiling(filtered.Count / (float)pageSize));
            if (_dashboardPage >= pageCount)
            {
                _dashboardPage = pageCount - 1;
            }
            if (_dashboardPage < 0)
            {
                _dashboardPage = 0;
            }

            var resolution = Screen.MainWindowResolution;
            var x = resolution.Width * 0.04f;
            var y = resolution.Height * 0.08f;
            var width = resolution.Width * 0.92f;
            var height = resolution.Height * 0.78f;
            var topHeaderHeight = resolution.Height * 0.14f;
            var tableHeaderHeight = resolution.Height * 0.042f;
            var rowHeight = resolution.Height * 0.052f;
            var loadingBarHeight = Math.Max(6f, rowHeight * 0.2f);
            var title = _dashboardView == DashboardOverviewMode.PetrolStations
                ? "PETROL SERVICE STATION OVERVIEW"
                : "INDUSTRY NETWORK OVERVIEW";

            DrawRect(resolution.Width, resolution.Height, x + 7f, y + 7f, width, height, Color.FromArgb(90, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(204, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 5f, Color.FromArgb(238, 227, 170, 58));
            DrawRect(resolution.Width, resolution.Height, x, y + 5f, width, topHeaderHeight - 5f, Color.FromArgb(182, 14, 20, 28));

            DrawDashboardText(
                resolution,
                title,
                x + 12f,
                y + 10f,
                0.47f,
                Color.FromArgb(250, 244, 250, 255),
                GTA.UI.Font.ChaletComprimeCologne);

            DrawDashboardText(
                resolution,
                string.Format("Profit ${0:0}   Market x{1:0.00}", _profit, _globalMarket.PriceMultiplier),
                x + 13f,
                y + 43f,
                0.30f,
                Color.FromArgb(235, 224, 232, 240),
                GTA.UI.Font.ChaletLondon);

            DrawDashboardText(
                resolution,
                string.Format(
                    "Page {0}/{1}   {2}/{3} pages",
                    _dashboardPage + 1,
                    pageCount,
                    KeyName(_controls.DashboardPageUp),
                    KeyName(_controls.DashboardPageDown)),
                x + (width * 0.71f),
                y + 43f,
                0.28f,
                Color.FromArgb(232, 221, 228, 236),
                GTA.UI.Font.ChaletLondon);

            var buttonY = y + topHeaderHeight - 36f;
            var buttonHeight = 36f;
            var buttonWidth = width * 0.28f;
            DrawDashboardModeButton(
                resolution,
                x + 12f,
                buttonY,
                buttonWidth,
                buttonHeight,
                "PETROL SERVICE STATIONS",
                _dashboardViewButtonIndex == 0,
                _dashboardView == DashboardOverviewMode.PetrolStations);
            DrawDashboardModeButton(
                resolution,
                x + 24f + buttonWidth,
                buttonY,
                buttonWidth,
                buttonHeight,
                "INDUSTRIES OVERVIEW",
                _dashboardViewButtonIndex == 1,
                _dashboardView == DashboardOverviewMode.Industries);

            var tableY = y + topHeaderHeight;
            DrawRect(resolution.Width, resolution.Height, x, tableY, width, tableHeaderHeight, Color.FromArgb(210, 24, 30, 41));

            var pageStart = _dashboardPage * pageSize;
            var maxIndex = Math.Min(filtered.Count, pageStart + pageSize);
            var rowTop = tableY + tableHeaderHeight;

            if (_dashboardView == DashboardOverviewMode.PetrolStations)
            {
                var colIndexX = x + 14f;
                var colNameX = x + (width * 0.06f);
                var colFuelX = x + (width * 0.53f);
                var colCapX = x + (width * 0.67f);
                var colFillX = x + (width * 0.79f);
                var barX = x + (width * 0.88f);
                var barWidth = width * 0.10f;

                DrawTableHeaderText(resolution, "#", colIndexX, tableY + 7f);
                DrawTableHeaderText(resolution, "STATION", colNameX, tableY + 7f);
                DrawTableHeaderText(resolution, "FUEL", colFuelX, tableY + 7f);
                DrawTableHeaderText(resolution, "CAP", colCapX, tableY + 7f);
                DrawTableHeaderText(resolution, "FILL", colFillX, tableY + 7f);

                for (int i = pageStart; i < maxIndex; i++)
                {
                    var row = i - pageStart;
                    var industry = filtered[i];
                    var lineY = rowTop + (row * rowHeight);
                    var rowColor = row % 2 == 0
                        ? Color.FromArgb(112, 16, 22, 31)
                        : Color.FromArgb(92, 12, 17, 24);
                    DrawRect(resolution.Width, resolution.Height, x, lineY, width, rowHeight, rowColor);

                    var fuelStock = industry.GetInputStockTotal();
                    var fuelCapacity = Math.Max(1f, industry.InputCapacityTons);
                    var fillRatio = Clamp01(fuelStock / fuelCapacity);
                    var fillColor = fillRatio >= 0.75f
                        ? Color.FromArgb(230, 86, 191, 113)
                        : (fillRatio >= 0.35f
                            ? Color.FromArgb(230, 223, 177, 76)
                            : Color.FromArgb(230, 208, 88, 74));

                    var name = industry.Name;
                    if (name.Length > 34)
                    {
                        name = name.Substring(0, 31) + "...";
                    }

                    DrawTableRowText(resolution, (i + 1).ToString(), colIndexX, lineY + 8f, Color.FromArgb(230, 230, 236, 244));
                    DrawTableRowText(resolution, name, colNameX, lineY + 8f, Color.FromArgb(240, 236, 242, 248));
                    DrawTableRowText(resolution, string.Format("{0:0.0}t", fuelStock), colFuelX, lineY + 8f, Color.FromArgb(226, 219, 229, 239));
                    DrawTableRowText(resolution, string.Format("{0:0.0}t", fuelCapacity), colCapX, lineY + 8f, Color.FromArgb(226, 219, 229, 239));
                    DrawTableRowText(resolution, string.Format("{0:0}%", fillRatio * 100f), colFillX, lineY + 8f, Color.FromArgb(232, 227, 234, 242));

                    var barY = lineY + ((rowHeight - loadingBarHeight) * 0.5f);
                    DrawCompactLoadingBar(resolution, barX, barY, barWidth, loadingBarHeight, fillRatio, fillColor);
                }
            }
            else
            {
                var colIndexX = x + 14f;
                var colNameX = x + (width * 0.06f);
                var colStockX = x + (width * 0.41f);
                var colRateX = x + (width * 0.56f);
                var colUtilX = x + (width * 0.66f);
                var colFillX = x + (width * 0.75f);
                var colOmegaX = x + (width * 0.82f);
                var fillBarX = x + (width * 0.87f);
                var fillBarWidth = width * 0.055f;
                var omegaBarX = x + (width * 0.93f);
                var omegaBarWidth = width * 0.055f;

                DrawTableHeaderText(resolution, "#", colIndexX, tableY + 7f);
                DrawTableHeaderText(resolution, "INDUSTRY", colNameX, tableY + 7f);
                DrawTableHeaderText(resolution, "STOCK", colStockX, tableY + 7f);
                DrawTableHeaderText(resolution, "RATE/H", colRateX, tableY + 7f);
                DrawTableHeaderText(resolution, "UTIL", colUtilX, tableY + 7f);
                DrawTableHeaderText(resolution, "FILL", colFillX, tableY + 7f);
                DrawTableHeaderText(resolution, "OMEGA", colOmegaX, tableY + 7f);

                for (int i = pageStart; i < maxIndex; i++)
                {
                    var row = i - pageStart;
                    var industry = filtered[i];
                    var lineY = rowTop + (row * rowHeight);
                    var rowColor = row % 2 == 0
                        ? Color.FromArgb(112, 16, 22, 31)
                        : Color.FromArgb(92, 12, 17, 24);
                    DrawRect(resolution.Width, resolution.Height, x, lineY, width, rowHeight, rowColor);

                    var totalStock = industry.GetInputStockTotal() + industry.GetOutputStockTotal();
                    var totalCapacity = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
                    var fillRatio = Clamp01(totalStock / totalCapacity);
                    var util = Math.Max(0f, Math.Min(100f, industry.LastUtilizationPercent));
                    var omegaRatio = industry.SupportsOmegaBoost
                        ? Clamp01(industry.OmegaStorage / Math.Max(1f, industry.OmegaCapacityTons))
                        : 0f;

                    var name = industry.Name;
                    if (name.Length > 29)
                    {
                        name = name.Substring(0, 26) + "...";
                    }

                    if (industry.HasOmegaBoost)
                    {
                        name += "  OMEGA";
                    }

                    var fillColor = fillRatio >= 0.75f
                        ? Color.FromArgb(230, 86, 191, 113)
                        : (fillRatio >= 0.35f
                            ? Color.FromArgb(230, 223, 177, 76)
                            : Color.FromArgb(230, 208, 88, 74));
                    var omegaColor = industry.SupportsOmegaBoost
                        ? Color.FromArgb(230, 112, 164, 236)
                        : Color.FromArgb(160, 74, 82, 94);

                    DrawTableRowText(resolution, (i + 1).ToString(), colIndexX, lineY + 8f, Color.FromArgb(230, 230, 236, 244));
                    DrawTableRowText(resolution, name, colNameX, lineY + 8f, Color.FromArgb(240, 236, 242, 248));
                    DrawTableRowText(resolution, string.Format("{0:0.0}/{1:0.0}t", totalStock, totalCapacity), colStockX, lineY + 8f, Color.FromArgb(226, 219, 229, 239));
                    DrawTableRowText(resolution, string.Format("{0:0.0}", industry.CurrentOutputPerHourTons), colRateX, lineY + 8f, Color.FromArgb(226, 219, 229, 239));
                    DrawTableRowText(resolution, string.Format("{0:0}%", util), colUtilX, lineY + 8f, Color.FromArgb(232, 227, 234, 242));
                    DrawTableRowText(resolution, string.Format("{0:0}%", fillRatio * 100f), colFillX, lineY + 8f, Color.FromArgb(232, 227, 234, 242));
                    DrawTableRowText(
                        resolution,
                        industry.SupportsOmegaBoost ? string.Format("{0:0}%", omegaRatio * 100f) : "-",
                        colOmegaX,
                        lineY + 8f,
                        Color.FromArgb(232, 227, 234, 242));

                    var barY = lineY + ((rowHeight - loadingBarHeight) * 0.5f);
                    DrawCompactLoadingBar(resolution, fillBarX, barY, fillBarWidth, loadingBarHeight, fillRatio, fillColor);
                    DrawCompactLoadingBar(resolution, omegaBarX, barY, omegaBarWidth, loadingBarHeight, omegaRatio, omegaColor);
                }
            }

            if (filtered.Count == 0)
            {
                DrawRect(resolution.Width, resolution.Height, x, rowTop, width, rowHeight, Color.FromArgb(96, 12, 17, 24));
                DrawTableRowText(
                    resolution,
                    "No entries in this overview.",
                    x + 16f,
                    rowTop + 8f,
                    Color.FromArgb(224, 214, 223, 233));
            }

            var footerY = y + height - (resolution.Height * 0.04f);
            DrawRect(resolution.Width, resolution.Height, x, footerY, width, resolution.Height * 0.032f, Color.FromArgb(178, 16, 22, 30));
            DrawDashboardText(
                resolution,
                string.Format(
                    "{0}/{1} select tab   {2} open tab   {3}/{4} page   {5} close dashboard",
                    KeyName(_controls.MenuLeft),
                    KeyName(_controls.MenuRight),
                    KeyName(_controls.MenuSelect),
                    KeyName(_controls.DashboardPageUp),
                    KeyName(_controls.DashboardPageDown),
                    KeyName(_controls.ToggleDashboard)),
                x + 12f,
                footerY + 4f,
                0.27f,
                Color.FromArgb(230, 222, 230, 238),
                GTA.UI.Font.ChaletLondon);
        }

        private static void DrawDashboardModeButton(Size resolution, float x, float y, float width, float height, string label, bool focused, bool active)
        {
            var fillColor = active
                ? Color.FromArgb(212, 88, 125, 150)
                : (focused ? Color.FromArgb(190, 61, 82, 100) : Color.FromArgb(162, 34, 47, 60));

            DrawRect(resolution.Width, resolution.Height, x, y, width, height, fillColor);
            if (focused)
            {
                DrawRect(resolution.Width, resolution.Height, x + 1f, y + 1f, 4f, height - 2f, Color.FromArgb(244, 252, 246, 220));
            }

            DrawDashboardText(
                resolution,
                label,
                x + 10f,
                y + 5f,
                0.275f,
                active || focused ? Color.FromArgb(246, 247, 251, 255) : Color.FromArgb(226, 220, 230, 239),
                GTA.UI.Font.ChaletLondon);
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

            if (_nearestIndustry != null && player.Position.DistanceTo(GetGroundPosition(_nearestIndustry.Position)) <= 40f)
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

        private void OpenOfficeMenu()
        {
            CloseIndustryTablet();
            CloseNonOfficeMenus();
            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private void CloseNonOfficeMenus()
        {
            _industryMenu.Close();
            _upgradeMenu.Close();
            CloseIndustryTablet();
        }

        private void CloseAllMenus()
        {
            _officeMenu.Close();
            _industryMenu.Close();
            _upgradeMenu.Close();
            CloseIndustryTablet();
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
                    CaptionFactory = () => string.Format("Worker Model: < {0} >", GetWorkerDisplayName(_config.WorkerModels[_workerIndex])),
                    OnLeft = () => ChangeWorkerIndex(-1),
                    OnRight = () => ChangeWorkerIndex(1),
                    OnActivate = ApplyWorkerModel,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Cargo Filter: < {0} >", _selectedFilter),
                    OnLeft = () => ChangeFilter(-1),
                    OnRight = () => ChangeFilter(1),
                    OnActivate = RefreshFilteredVehicles,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentVehicleCaption,
                    OnLeft = () => ChangeVehicleSelection(-1),
                    OnRight = () => ChangeVehicleSelection(1),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = CurrentTractorCaption,
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
            var count = _config.WorkerModels.Count;
            if (count == 0)
            {
                return;
            }

            _workerIndex = (_workerIndex + delta + count) % count;
        }

        private void ApplyWorkerModel()
        {
            if (_config.WorkerModels.Count == 0)
            {
                ShowStatus("No worker models configured.");
                return;
            }

            var modelName = _config.WorkerModels[_workerIndex];
            var model = new Model(modelName);
            if (!model.IsInCdImage || !model.IsValid || !model.Request(1000))
            {
                ShowStatus("Failed to request worker model.");
                return;
            }

            if (Game.Player.Character != null && Game.Player.Character.Exists() && Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.Exists())
            {
                ShowStatus("Exit your vehicle before changing character model.");
                model.MarkAsNoLongerNeeded();
                return;
            }

            var changed = Game.Player.ChangeModel(model);
            if (!changed)
            {
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player.Handle, model.Hash);
                Wait(0);
                var refreshed = Game.Player.Character;
                changed = refreshed != null && refreshed.Exists() && refreshed.Model.Hash == model.Hash;
            }

            if (changed)
            {
                var refreshed = Game.Player.Character;
                if (refreshed != null && refreshed.Exists())
                {
                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, refreshed.Handle);
                }
            }

            model.MarkAsNoLongerNeeded();

            ShowStatus(changed
                ? string.Format("Worker switched to {0}", GetWorkerDisplayName(modelName))
                : "Model switch failed.");
        }

        private void ChangeFilter(int delta)
        {
            var index = _filterOrder.IndexOf(_selectedFilter);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + _filterOrder.Count) % _filterOrder.Count;
            _selectedFilter = _filterOrder[index];
            RefreshFilteredVehicles();
            RebuildOfficeMenuItems();
        }

        private void RefreshFilteredVehicles()
        {
            _filteredVehicles = _fleetManager.GetSpawnableForCargoType(_selectedFilter).ToList();
            _selectedVehicleIndex = 0;
        }

        private void ChangeVehicleSelection(int delta)
        {
            if (_filteredVehicles.Count == 0)
            {
                return;
            }

            _selectedVehicleIndex = (_selectedVehicleIndex + delta + _filteredVehicles.Count) % _filteredVehicles.Count;
            RebuildOfficeMenuItems();
        }

        private void ChangeTractorSelection(int delta)
        {
            if (_tractorVehicles.Count == 0)
            {
                return;
            }

            _selectedTractorIndex = (_selectedTractorIndex + delta + _tractorVehicles.Count) % _tractorVehicles.Count;
        }

        private string CurrentVehicleCaption()
        {
            if (_filteredVehicles.Count == 0)
            {
                return "Vehicle: < none for this cargo filter >";
            }

            return string.Format("Vehicle: < {0} >", _filteredVehicles[_selectedVehicleIndex]);
        }

        private string CurrentTractorCaption()
        {
            if (_filteredVehicles.Count == 0)
            {
                return "Trailer Truck: < n/a >";
            }

            if (!_filteredVehicles[_selectedVehicleIndex].IsTrailer)
            {
                return "Trailer Truck: < auto (not needed) >";
            }

            if (_tractorVehicles.Count == 0)
            {
                return "Trailer Truck: < unavailable >";
            }

            return string.Format("Trailer Truck: < {0} >", _tractorVehicles[_selectedTractorIndex].ModelName);
        }

        private void SpawnSelectedVehicle()
        {
            if (_filteredVehicles.Count == 0)
            {
                ShowStatus("No vehicle available in this cargo filter.");
                return;
            }

            var selected = _filteredVehicles[_selectedVehicleIndex];
            VehicleDefinition tractor = null;
            if (selected.IsTrailer && _tractorVehicles.Count > 0)
            {
                tractor = _tractorVehicles[_selectedTractorIndex];
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            string message;
            if (!_fleetManager.SpawnSelectedVehicle(
                    selected,
                    tractor,
                    GetGroundPosition(_vehicleSpawnMarkerSeed),
                    _config.VehicleSpawnHeading,
                    out truck,
                    out cargoVehicle,
                    out message))
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
            if (_pendingTransfer != null)
            {
                ShowStatus("Transfer already in progress.");
                return;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (_nearestIndustry == null || player.Position.DistanceTo(GetGroundPosition(_nearestIndustry.Position)) > IndustryInteractionDistance)
            {
                ShowStatus("No industry marker in range.");
                return;
            }

            _menuIndustry = _nearestIndustry;
            _officeMenu.Close();
            _industryMenu.Close();
            _upgradeMenu.Close();

            _industryTablet.Open(_nearestIndustry);
        }

        private void TryOpenIndustryTransferMenu()
        {
            if (_pendingTransfer != null)
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

            if (_nearestIndustry == null || player.Position.DistanceTo(GetGroundPosition(_nearestIndustry.Position)) > IndustryInteractionDistance)
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

            if (player.Position.DistanceTo(GetGroundPosition(industry.Position)) > IndustryInteractionDistance + 1.2f)
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

            if (player.Position.DistanceTo(GetGroundPosition(industry.Position)) > IndustryInteractionDistance + 1.2f)
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
            if (_pendingTransfer != null)
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
                cargoType = _selectedFilter;
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
            if (_pendingTransfer != null)
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
            if (_pendingTransfer != null)
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

            if (IndustryHasMultipleInputs(industry))
            {
                ShowStatus("Select unload mode from the tablet menu.");
                return;
            }

            if (!cargoState.Commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(string.Format("Vehicle cargo is {0}. Omega fluid required.", cargoState.Commodity));
                return;
            }

            StartTabletUnloadTransfer(industry, cargoVehicle, cargoState, true);
        }

        private void HandleTabletUnloadModeRequested(Industry industry, bool omegaOnly)
        {
            if (_pendingTransfer != null)
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
            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                ShowStatus("No product selected for loading.");
                return;
            }

            var requestedCapacity = Math.Max(0.5f, cargoState.FreeCapacityTons);
            var targetLoadTons = ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                ShowStatus("Loading failed: product unavailable.");
                return;
            }

            var shouldAnimateCrateDoors = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct) == VehicleCargoType.Crate;
            var usesLooseVisual = IsLooseVisualCommodity(selectedProduct);

            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            if (usesLooseVisual)
            {
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            _industryMenu.Close();
            CloseIndustryTablet();
            StartTransfer(
                BuildLoadingTransferLabel(0f, targetLoadTons, selectedProduct),
                2600,
                () =>
                {
                    try
                    {
                        float loaded;
                        if (!_industryManager.TryLoadCommodity(industry, cargoType, selectedProduct, targetLoadTons, out loaded))
                        {
                            if (usesLooseVisual)
                            {
                                _fleetManager.ClearCargoVisuals(cargoState);
                            }

                            ShowStatus("Loading failed: product unavailable.");
                            return;
                        }

                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        ShowStatus(string.Format("Loaded {0:0.0}t {1}.", loaded, selectedProduct));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                },
                progress =>
                {
                    if (_pendingTransfer == null)
                    {
                        return;
                    }

                    var currentTons = targetLoadTons * Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
        }

        private void StartTabletUnloadTransfer(Industry industry, Vehicle cargoVehicle, VehicleCargoState cargoState, bool omegaOnly)
        {
            if (cargoState.IsEmpty)
            {
                ShowStatus("Vehicle is empty.");
                return;
            }

            if (omegaOnly && !cargoState.Commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(string.Format("Vehicle cargo is {0}. Omega fluid required.", cargoState.Commodity));
                return;
            }

            if (!industry.AcceptsCommodity(cargoState.Commodity))
            {
                ShowStatus(string.Format("This industry does not accept {0}.", cargoState.Commodity));
                return;
            }

            var tonsToUnload = cargoState.WeightTons;
            var commodity = cargoState.Commodity;
            var shouldAnimateCrateDoors = cargoState.CargoType == VehicleCargoType.Crate
                || CommodityCatalog.GetCargoTypeForCommodity(commodity) == VehicleCargoType.Crate;

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            _industryMenu.Close();
            CloseIndustryTablet();
            StartTransfer(
                string.Format("Unloading {0:0.0}t {1}...", tonsToUnload, commodity),
                2800,
                () =>
                {
                    try
                    {
                        float accepted;
                        if (!_industryManager.TryUnload(industry, commodity, tonsToUnload, out accepted))
                        {
                            ShowStatus("Unloading failed: destination storage full.");
                            return;
                        }

                        var revenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                        _profit += revenue;

                        cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                        if (cargoState.WeightTons <= 0.001f)
                        {
                            cargoState.ClearCargo();
                            _fleetManager.ClearCargoVisuals(cargoState);
                        }
                        else
                        {
                            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        }

                        ShowStatus(string.Format("Unloaded {0:0.0}t {1}. Profit +${2:0}", accepted, commodity, revenue));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                });
        }

        private static bool IndustryHasMultipleInputs(Industry industry)
        {
            return industry != null
                && industry.SupportsOmegaBoost
                && industry.Inputs != null
                && industry.Inputs.Count > 1;
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

            if (player.Position.DistanceTo(GetGroundPosition(industry.Position)) > IndustryInteractionDistance + 2.4f)
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
                    cargoType = _selectedFilter;
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
            if (_pendingTransfer != null)
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

                var tonsToUnload = cargoState.WeightTons;
                var commodity = cargoState.Commodity;
                var shouldAnimateCrateDoors = cargoState.CargoType == VehicleCargoType.Crate;

                if (shouldAnimateCrateDoors)
                {
                    SetRearCargoDoors(cargoVehicle, true);
                }

                _industryMenu.Close();
                StartTransfer(
                    string.Format("Unloading {0:0.0}t {1}...", tonsToUnload, commodity),
                    2800,
                    () =>
                    {
                        try
                        {
                            float accepted;
                            if (!_industryManager.TryUnload(industry, commodity, tonsToUnload, out accepted))
                            {
                                ShowStatus("Unloading failed: destination storage full.");
                                return;
                            }

                            var revenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                            _profit += revenue;

                            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                            if (cargoState.WeightTons <= 0.001f)
                            {
                                cargoState.ClearCargo();
                                _fleetManager.ClearCargoVisuals(cargoState);
                            }
                            else
                            {
                                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                            }

                            ShowStatus(string.Format("Unloaded {0:0.0}t {1}. Profit +${2:0}", accepted, commodity, revenue));
                        }
                        finally
                        {
                            if (shouldAnimateCrateDoors)
                            {
                                SetRearCargoDoors(cargoVehicle, false);
                            }
                        }
                    });

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
            var requestedCapacity = Math.Max(0.5f, cargoState.FreeCapacityTons);
            var targetLoadTons = ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                ShowStatus("Loading failed: product unavailable.");
                return;
            }

            var cargoType = cargoState.CargoType;
            var shouldAnimateCrateDoorsOnLoad = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct) == VehicleCargoType.Crate;
            var usesLooseVisual = IsLooseVisualCommodity(selectedProduct);
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            if (shouldAnimateCrateDoorsOnLoad)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            if (usesLooseVisual)
            {
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            _industryMenu.Close();
            StartTransfer(
                BuildLoadingTransferLabel(0f, targetLoadTons, selectedProduct),
                2600,
                () =>
                {
                    try
                    {
                        float loaded;
                        if (!_industryManager.TryLoadCommodity(industry, cargoType, selectedProduct, targetLoadTons, out loaded))
                        {
                            if (usesLooseVisual)
                            {
                                _fleetManager.ClearCargoVisuals(cargoState);
                            }

                            ShowStatus("Loading failed: product unavailable.");
                            return;
                        }

                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        ShowStatus(string.Format("Loaded {0:0.0}t {1}.", loaded, selectedProduct));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoorsOnLoad)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                },
                progress =>
                {
                    if (_pendingTransfer == null)
                    {
                        return;
                    }

                    var currentTons = targetLoadTons * Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
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

            if (_nearestIndustry == null || player.Position.DistanceTo(GetGroundPosition(_nearestIndustry.Position)) > IndustryInteractionDistance)
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

            _upgradeMenu.SetItems(new[]
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Profit Balance: ${0:0}", _profit),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => GetUpgradeCaption(_menuIndustry, IndustryUpgradeModule.Production, "Production Module"),
                    OnActivate = () => TryApplyUpgradeModule(IndustryUpgradeModule.Production),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => GetUpgradeCaption(_menuIndustry, IndustryUpgradeModule.InputStorage, "Input Storage Module"),
                    OnActivate = () => TryApplyUpgradeModule(IndustryUpgradeModule.InputStorage),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => GetUpgradeCaption(_menuIndustry, IndustryUpgradeModule.OutputStorage, "Output Storage Module"),
                    OnActivate = () => TryApplyUpgradeModule(IndustryUpgradeModule.OutputStorage),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => GetUpgradeCaption(_menuIndustry, IndustryUpgradeModule.OmegaStorage, "Omega Tank Module"),
                    OnActivate = () => TryApplyUpgradeModule(IndustryUpgradeModule.OmegaStorage),
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = () => _upgradeMenu.Close(),
                },
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

        private void StartTransfer(string label, int durationMs, Action complete, Action<float> onProgress = null)
        {
            _pendingTransfer = new PendingTransfer
            {
                Label = label,
                DurationMs = durationMs,
                StartMs = Game.GameTime,
                OnComplete = complete,
                OnProgress = onProgress,
            };
        }

        private void UpdateTransfer(int now)
        {
            if (_pendingTransfer == null)
            {
                return;
            }

            var elapsed = now - _pendingTransfer.StartMs;
            var progress = Math.Min(1f, elapsed / (float)_pendingTransfer.DurationMs);

            try
            {
                _pendingTransfer.OnProgress?.Invoke(progress);
            }
            catch (Exception)
            {
                // Disable progress callback after first failure so transfer can still complete safely.
                _pendingTransfer.OnProgress = null;
                ShowStatus("Transfer visual callback failed. Continuing without preview.");
            }

            DrawProgressBar(_pendingTransfer.Label, progress);

            if (elapsed < _pendingTransfer.DurationMs)
            {
                return;
            }

            var completed = _pendingTransfer;
            _pendingTransfer = null;

            try
            {
                completed.OnComplete?.Invoke();
            }
            catch (Exception)
            {
                ShowStatus("Transfer completion failed.");
            }
        }

        private static bool IsLooseVisualCommodity(string commodity)
        {
            var normalized = CommodityCatalog.Normalize(commodity);
            return normalized.Equals("Ore", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Coal", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Recyclable", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Recyclables", StringComparison.OrdinalIgnoreCase);
        }

        private static float ResolveLoadTargetTons(Industry industry, string commodity, float requestedTons)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || requestedTons <= 0f)
            {
                return 0f;
            }

            var available = Math.Max(0f, industry.GetStock(commodity));
            return Math.Min(requestedTons, available);
        }

        private static string BuildLoadingTransferLabel(float currentTons, float targetTons, string commodity)
        {
            return string.Format(
                "Loading {0:0.0}/{1:0.0}t {2}...",
                Math.Max(0f, currentTons),
                Math.Max(0f, targetTons),
                commodity ?? string.Empty);
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

        private static void DrawTableHeaderText(Size resolution, string text, float x, float y)
        {
            DrawDashboardText(
                resolution,
                text,
                x,
                y,
                0.28f,
                Color.FromArgb(233, 227, 233, 241),
                GTA.UI.Font.ChaletLondon);
        }

        private static void DrawTableRowText(Size resolution, string text, float x, float y, Color color)
        {
            DrawDashboardText(
                resolution,
                text,
                x,
                y,
                0.295f,
                color,
                GTA.UI.Font.ChaletLondon);
        }

        private static void DrawDashboardText(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font)
        {
            var normalizedX = x / resolution.Width;
            var normalizedY = y / resolution.Height;

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_PROPORTIONAL, true);
            Function.Call(Hash.SET_TEXT_CENTRE, false);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, normalizedX, normalizedY, 0);
        }

        private void CloseIndustryTablet()
        {
            if (!_industryTablet.IsOpen)
            {
                return;
            }

            _industryTablet.Close();
        }

        private void CreateMapBlips()
        {
            DestroyMapBlips();

            _officeBlip = CreateStaticBlip(GetGroundPosition(_mainOfficeMarkerSeed), BlipSprite.Office, BlipColor.Blue, "Logistics Office", 1.0f);
            _vehicleSpawnBlip = CreateStaticBlip(GetGroundPosition(_vehicleSpawnMarkerSeed), BlipSprite.Garage2, BlipColor.White, "Vehicle Spawn", 0.9f);

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var industry = _industryManager.Industries[i];
                var isPetrolStation = IsPetrolServiceStation(industry);
                var sprite = ResolveIndustryBlipSprite(industry, isPetrolStation);
                var color = isPetrolStation
                    ? BlipColor.Yellow
                    : (industry.IsSink ? BlipColor.Yellow : BlipColor.Green);
                var blip = CreateStaticBlip(GetGroundPosition(industry.Position), sprite, color, industry.Name, 0.85f);
                if (blip != null && blip.Exists())
                {
                    _industryBlips.Add(blip);
                }
            }
        }

        private void RefreshBlipPositions()
        {
            if (_officeBlip != null && _officeBlip.Exists())
            {
                _officeBlip.Position = GetGroundPosition(_mainOfficeMarkerSeed);
            }

            if (_vehicleSpawnBlip != null && _vehicleSpawnBlip.Exists())
            {
                _vehicleSpawnBlip.Position = GetGroundPosition(_vehicleSpawnMarkerSeed);
            }

            var count = Math.Min(_industryBlips.Count, _industryManager.Industries.Count);
            for (int i = 0; i < count; i++)
            {
                var blip = _industryBlips[i];
                if (blip == null || !blip.Exists())
                {
                    continue;
                }

                blip.Position = GetGroundPosition(_industryManager.Industries[i].Position);
            }
        }

        private void DestroyMapBlips()
        {
            if (_officeBlip != null && _officeBlip.Exists())
            {
                _officeBlip.Delete();
            }

            if (_vehicleSpawnBlip != null && _vehicleSpawnBlip.Exists())
            {
                _vehicleSpawnBlip.Delete();
            }

            for (int i = 0; i < _industryBlips.Count; i++)
            {
                var blip = _industryBlips[i];
                if (blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            _industryBlips.Clear();
            _officeBlip = null;
            _vehicleSpawnBlip = null;
        }

        private static Blip CreateStaticBlip(Vector3 position, BlipSprite sprite, BlipColor color, string name, float scale)
        {
            var blip = World.CreateBlip(position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = sprite;
            blip.Color = color;
            blip.Name = name;
            blip.Scale = scale;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private bool IsNearMainOffice(Vector3 position)
        {
            return position.DistanceTo(GetGroundPosition(_mainOfficeMarkerSeed)) <= OfficeInteractionDistance;
        }

        private Industry GetIndustryInInteractionRange(Vector3 position)
        {
            if (_nearestIndustry != null && position.DistanceTo(GetGroundPosition(_nearestIndustry.Position)) <= IndustryInteractionDistance)
            {
                return _nearestIndustry;
            }

            var nearest = _industryManager.GetNearestIndustry(position, IndustryInteractionDistance + 1.0f);
            if (nearest == null)
            {
                return null;
            }

            return position.DistanceTo(GetGroundPosition(nearest.Position)) <= IndustryInteractionDistance
                ? nearest
                : null;
        }

        private static BlipSprite ResolveIndustryBlipSprite(Industry industry, bool isPetrolStation)
        {
            if (isPetrolStation)
            {
                return BlipSprite.JerryCan;
            }

            return industry != null && industry.IsSink
                ? BlipSprite.Store
                : BlipSprite.Warehouse;
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

        private string GetWorkerDisplayName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "Unknown Worker";
            }

            string overrideName;
            if (_workerNameOverrides.TryGetValue(modelName, out overrideName))
            {
                return overrideName;
            }

            var cleaned = modelName.Replace("_", " ").Trim();
            if (cleaned.Length == 0)
            {
                return "Worker";
            }

            var words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (words.Count >= 3 && (words[0].Length <= 2 || words[0].Equals("mp", StringComparison.OrdinalIgnoreCase)))
            {
                words = words.Skip(2).ToList();
            }

            if (words.Count > 0)
            {
                int parsed;
                if (int.TryParse(words[words.Count - 1], out parsed))
                {
                    words.RemoveAt(words.Count - 1);
                }
            }

            if (words.Count == 0)
            {
                words.Add("Worker");
            }

            for (int i = 0; i < words.Count; i++)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : string.Empty);
            }

            return string.Join(" ", words);
        }

        private static Dictionary<string, string> CreateWorkerNameOverrides()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "s_m_m_dockwork_01", "Dock Worker" },
                { "s_m_y_construct_01", "Construction Worker" },
                { "s_m_m_trucker_01", "Long-Haul Trucker" },
                { "mp_m_freemode_01", "Freemode Male" },
                { "mp_f_freemode_01", "Freemode Female" },
            };
        }

        private static void SetRearCargoDoors(Vehicle vehicle, bool open)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            ToggleDoor(vehicle, VehicleDoorIndex.BackLeftDoor, open);
            ToggleDoor(vehicle, VehicleDoorIndex.BackRightDoor, open);
            ToggleDoor(vehicle, VehicleDoorIndex.Trunk, open);
        }

        private static void ToggleDoor(Vehicle vehicle, VehicleDoorIndex doorIndex, bool open)
        {
            if (!vehicle.Doors.Contains(doorIndex))
            {
                return;
            }

            var door = vehicle.Doors[doorIndex];
            if (open)
            {
                door.Open(false, false);
            }
            else
            {
                door.Close(false);
            }
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
            DestroyMapBlips();
            _pendingTransfer = null;
            CloseAllMenus();
            _industryTablet.LoadRequested -= HandleTabletLoadRequested;
            _industryTablet.LoadCommodityRequested -= HandleTabletLoadCommodityRequested;
            _industryTablet.UnloadRequested -= HandleTabletUnloadRequested;
            _industryTablet.UnloadModeRequested -= HandleTabletUnloadModeRequested;
            _industryTablet.UpgradeModuleRequested -= HandleTabletUpgradeModuleRequested;
            _heldKeys.Clear();
        }

        private sealed class PendingTransfer
        {
            public string Label { get; set; }
            public int StartMs { get; set; }
            public int DurationMs { get; set; }
            public Action OnComplete { get; set; }
            public Action<float> OnProgress { get; set; }
        }

        private enum IndustryTransferMode
        {
            Load = 0,
            Unload = 1,
        }

        private enum DashboardOverviewMode
        {
            PetrolStations = 0,
            Industries = 1,
        }
    }
}

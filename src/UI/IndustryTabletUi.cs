using System;
using System.Collections.Generic;
using System.Drawing;
using LSOL;
using LSOL.Config;
using GTA;
using GTA.UI;
using LSOL.Domain;
using LemonUI.Elements;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    public sealed class IndustryTabletUi
    {
        private enum TabletPage
        {
            Main = 0,
            Upgrades = 1,
            LoadSelection = 2,
            UnloadSelection = 3,
            Statistics = 4,
        }

        private const float UiFallbackWidth = 1280f;
        private const float UiFallbackHeight = 720f;
        private const float MenuBackgroundWidth = 784f;
        private const float MenuBackgroundHeight = 536f;

        private readonly ScaledRectangle _menuBackground;
        private readonly ScaledRectangle _loadButton;
        private readonly ScaledRectangle _unloadButton;
        private readonly ScaledRectangle _statsButton;
        private readonly ScaledRectangle _vehicleSpawnerButton;
        private readonly ScaledRectangle _upgradeButton;
        private readonly ScaledRectangle _statsPanel;
        private readonly List<ScaledRectangle> _upgradeModuleButtons;
        private readonly List<IndustryUpgradeModule> _availableUpgradeModules;
        private readonly List<string> _loadOptions;
        private readonly Dictionary<string, string> _loadOptionSubtitles;
        private readonly IndustryStatisticsSnapshotCache _statisticsSnapshotCache;

        private Industry _industry;
        private float _frameX;
        private float _frameY;
        private float _profitBalance;
        private float _industryPrice;
        private float _industryOwnerCut;
        private int _selectedMainIndex;
        private int _selectedUpgradeIndex;
        private int _selectedLoadOptionIndex;
        private int _selectedUnloadOptionIndex;
        private int _statsScrollIndex;
        private TabletPage _currentPage;
        private bool _isIndustryOwnedForGameplay;
        private bool _requiresIndustryPurchase;
        private bool _upgradeModulesDirty;

        public IndustryTabletUi()
        {
            _menuBackground = new ScaledRectangle(new PointF(0f, 0f), new SizeF(MenuBackgroundWidth, MenuBackgroundHeight))
            {
                Color = Color.FromArgb(230, 8, 12, 18),
            };

            _loadButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _unloadButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _statsButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _vehicleSpawnerButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _upgradeButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _statsPanel = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 88f));

            _upgradeModuleButtons = new List<ScaledRectangle>();
            for (int i = 0; i < 5; i++)
            {
                var moduleButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 58f))
                {
                    Color = Color.FromArgb(0, 0, 0, 0),
                };

                _upgradeModuleButtons.Add(moduleButton);
            }

            _selectedMainIndex = 0;
            _selectedUpgradeIndex = 0;
            _selectedLoadOptionIndex = 0;
            _selectedUnloadOptionIndex = 0;
            _statsScrollIndex = 0;
            _currentPage = TabletPage.Main;
            _availableUpgradeModules = new List<IndustryUpgradeModule>();
            _loadOptions = new List<string>();
            _loadOptionSubtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _statisticsSnapshotCache = new IndustryStatisticsSnapshotCache();
            _upgradeModulesDirty = true;
            UpdateLayout();
        }

        public bool IsOpen { get; private set; }

        public Industry ActiveIndustry
        {
            get { return _industry; }
        }

        public event Action<Industry> LoadRequested;

        public event Action<Industry, string> LoadCommodityRequested;

        public event Action<Industry> UnloadRequested;

        public event Action<Industry, bool> UnloadModeRequested;

        public event Action<Industry> IndustryPurchaseRequested;

        public event Action<Industry, IndustryUpgradeModule> UpgradeModuleRequested;

        public event Action<Industry> VehicleSpawnerRequested;

        public void SetLoadOptions(List<string> loadOptions, Dictionary<string, string> loadOptionSubtitles = null)
        {
            _loadOptions.Clear();
            _loadOptionSubtitles.Clear();
            if (loadOptions != null)
            {
                for (int i = 0; i < loadOptions.Count; i++)
                {
                    var entry = loadOptions[i];
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    _loadOptions.Add(entry.Trim());
                }
            }

            if (loadOptionSubtitles != null)
            {
                foreach (var entry in loadOptionSubtitles)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        continue;
                    }

                    _loadOptionSubtitles[entry.Key.Trim()] = entry.Value.Trim();
                }
            }

            var maxIndex = Math.Max(0, _loadOptions.Count);
            if (_selectedLoadOptionIndex > maxIndex)
            {
                _selectedLoadOptionIndex = maxIndex;
            }
        }

        public void UpdateProfitBalance(float profitBalance)
        {
            _profitBalance = Math.Max(0f, profitBalance);
        }

        public void UpdateOwnershipState(bool isIndustryOwnedForGameplay, bool requiresIndustryPurchase, float industryPrice, float industryOwnerCut)
        {
            if (_requiresIndustryPurchase != requiresIndustryPurchase)
            {
                _upgradeModulesDirty = true;
            }

            _isIndustryOwnedForGameplay = isIndustryOwnedForGameplay;
            _requiresIndustryPurchase = requiresIndustryPurchase;
            _industryPrice = Math.Max(0f, industryPrice);
            _industryOwnerCut = ModMath.Clamp01(industryOwnerCut);
        }

        public bool HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            if (!IsOpen || _industry == null)
            {
                return false;
            }

            if (controls == null)
            {
                controls = new ControlBindings();
            }

            if (key == controls.Interact)
            {
                return false;
            }

            if (key == controls.MenuBack || key == WinForms.Keys.Escape)
            {
                if (_currentPage == TabletPage.Upgrades)
                {
                    _currentPage = TabletPage.Main;
                    _selectedMainIndex = 4;
                    return true;
                }

                if (_currentPage == TabletPage.LoadSelection)
                {
                    _currentPage = TabletPage.Main;
                    _selectedMainIndex = 0;
                    return true;
                }

                if (_currentPage == TabletPage.UnloadSelection)
                {
                    _currentPage = TabletPage.Main;
                    _selectedMainIndex = 1;
                    return true;
                }

                if (_currentPage == TabletPage.Statistics)
                {
                    _currentPage = TabletPage.Main;
                    _selectedMainIndex = 2;
                    return true;
                }

                return false;
            }

            if (key == controls.MenuUp)
            {
                MoveSelection(-1);
                return true;
            }

            if (key == controls.MenuDown)
            {
                MoveSelection(1);
                return true;
            }

            if (key == controls.MenuSelect)
            {
                ActivateSelection();
                return true;
            }

            if (key == controls.MenuLeft || key == controls.MenuRight)
            {
                return true;
            }

            return true;
        }

        private void MoveSelection(int delta)
        {
            if (_currentPage == TabletPage.Main)
            {
                const int count = 5;
                _selectedMainIndex = (_selectedMainIndex + delta + count) % count;
                return;
            }

            if (_currentPage == TabletPage.Upgrades)
            {
                var upgradeCount = GetUpgradeSelectionCount();
                _selectedUpgradeIndex = (_selectedUpgradeIndex + delta + upgradeCount) % upgradeCount;
                return;
            }

            if (_currentPage == TabletPage.LoadSelection)
            {
                var loadChoiceCount = Math.Max(1, _loadOptions.Count + 1);
                _selectedLoadOptionIndex = (_selectedLoadOptionIndex + delta + loadChoiceCount) % loadChoiceCount;
                return;
            }

            if (_currentPage == TabletPage.UnloadSelection)
            {
                const int unloadChoiceCount = 3;
                _selectedUnloadOptionIndex = (_selectedUnloadOptionIndex + delta + unloadChoiceCount) % unloadChoiceCount;
                return;
            }

            MoveStatsSelection(delta);
        }

        private void ActivateSelection()
        {
            if (_industry == null)
            {
                return;
            }

            if (_currentPage == TabletPage.Main)
            {
                if (_selectedMainIndex == 0)
                {
                    if (_loadOptions.Count > 1)
                    {
                        _currentPage = TabletPage.LoadSelection;
                        _selectedLoadOptionIndex = 0;
                        return;
                    }

                    if (_loadOptions.Count == 1)
                    {
                        LoadCommodityRequested?.Invoke(_industry, _loadOptions[0]);
                        return;
                    }

                    LoadRequested?.Invoke(_industry);
                    return;
                }

                if (_selectedMainIndex == 1)
                {
                    var canChooseOmegaUnloadMode = _industry.SupportsOmegaBoost
                        && _industry.Inputs != null
                        && _industry.Inputs.Count > 1;
                    if (canChooseOmegaUnloadMode)
                    {
                        _currentPage = TabletPage.UnloadSelection;
                        _selectedUnloadOptionIndex = 0;
                        return;
                    }

                    UnloadRequested?.Invoke(_industry);
                    return;
                }

                if (_selectedMainIndex == 2)
                {
                    _currentPage = TabletPage.Statistics;
                    _statsScrollIndex = 0;
                    return;
                }

                if (_selectedMainIndex == 3)
                {
                    VehicleSpawnerRequested?.Invoke(_industry);
                    return;
                }

                if (_selectedMainIndex == 4)
                {
                    if (_requiresIndustryPurchase)
                    {
                        IndustryPurchaseRequested?.Invoke(_industry);
                        return;
                    }

                    _currentPage = TabletPage.Upgrades;
                    _selectedUpgradeIndex = 0;
                }

                return;
            }

            if (_currentPage == TabletPage.LoadSelection)
            {
                if (_selectedLoadOptionIndex >= _loadOptions.Count)
                {
                    _currentPage = TabletPage.Main;
                    _selectedMainIndex = 0;
                    return;
                }

                LoadCommodityRequested?.Invoke(_industry, _loadOptions[_selectedLoadOptionIndex]);
                return;
            }

            if (_currentPage == TabletPage.UnloadSelection)
            {
                if (_selectedUnloadOptionIndex == 0)
                {
                    UnloadModeRequested?.Invoke(_industry, false);
                    return;
                }

                if (_selectedUnloadOptionIndex == 1)
                {
                    UnloadModeRequested?.Invoke(_industry, true);
                    return;
                }

                _currentPage = TabletPage.Main;
                _selectedMainIndex = 1;
                return;
            }

            if (_currentPage == TabletPage.Statistics)
            {
                _currentPage = TabletPage.Main;
                _selectedMainIndex = 2;
                return;
            }

            if (_currentPage != TabletPage.Upgrades)
            {
                return;
            }

            var availableModules = GetAvailableUpgradeModules();
            if (_selectedUpgradeIndex >= availableModules.Count)
            {
                _currentPage = TabletPage.Main;
                _selectedMainIndex = 4;
                return;
            }

            var module = availableModules[_selectedUpgradeIndex];
            _upgradeModulesDirty = true;
            UpgradeModuleRequested?.Invoke(_industry, module);
        }

        public void Open(Industry industry)
        {
            if (industry == null)
            {
                return;
            }

            _industry = industry;
            IsOpen = true;
            _selectedMainIndex = 0;
            _selectedUpgradeIndex = 0;
            _selectedLoadOptionIndex = 0;
            _selectedUnloadOptionIndex = 0;
            _statsScrollIndex = 0;
            _currentPage = TabletPage.Main;
            _upgradeModulesDirty = true;
            _statisticsSnapshotCache.Invalidate();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _industry = null;
            IsOpen = false;
            _currentPage = TabletPage.Main;
            _selectedMainIndex = 0;
            _selectedUpgradeIndex = 0;
            _selectedLoadOptionIndex = 0;
            _selectedUnloadOptionIndex = 0;
            _statsScrollIndex = 0;
            _loadOptions.Clear();
            _loadOptionSubtitles.Clear();
            _availableUpgradeModules.Clear();
            _upgradeModulesDirty = true;
            _statisticsSnapshotCache.Invalidate();
            HideUpgradeModuleButtons();
            HideMainButtons();
        }

        public void DrawAndHandleInput()
        {
            if (!IsOpen || _industry == null)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            UpdateLayout();
            _menuBackground.Draw();

            var industryName = BuildIndustryLabel(_industry.Name);
            var ownershipLabel = _isIndustryOwnedForGameplay ? "[OWNED]" : "[NOT OWNED]";
            var ownershipColor = _isIndustryOwnedForGameplay
                ? palette.Get(ModColorRole.AccentTeal, 232)
                : palette.Get(ModColorRole.AccentOrange, 232);

            DrawText(
                string.Format("{0} MENU", industryName),
                _frameX + 78f,
                _frameY + 62f,
                0.53f,
                palette.Get(ModColorRole.TextPrimary, 236),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                ownershipLabel,
                ResolveOwnershipLabelX(industryName),
                _frameY + 67f,
                0.30f,
                ownershipColor,
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                BuildIndustrySubtitle(),
                _frameX + 80f,
                _frameY + 88f,
                0.31f,
                palette.Get(ModColorRole.TextSecondary, 222),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (_currentPage == TabletPage.Main)
            {
                HideUpgradeModuleButtons();

                var hasMultipleInputs = _industry.Inputs != null && _industry.Inputs.Count > 1;
                var canChooseOmegaUnloadMode = _industry.SupportsOmegaBoost && hasMultipleInputs;
                var isOmegaOnlyUnload = _industry.SupportsOmegaBoost && !hasMultipleInputs;
                var unloadTitle = isOmegaOnlyUnload ? "UNLOAD OMEGA" : "UNLOAD CARGO";
                var unloadSubtitle = canChooseOmegaUnloadMode
                    ? "Choose Omega or truck cargo in-tablet"
                    : (isOmegaOnlyUnload
                        ? "Deliver Omega boost cargo from your active cargo vehicle"
                        : "Deliver current truck cargo to this industry");
                var loadSubtitle = _loadOptions.Count > 1
                    ? "Choose resource in-tablet after pressing load"
                    : "Initiate cargo load from local production stockpile";

                DrawButton(
                    _loadButton,
                    "LOAD CARGO TRUCK",
                    loadSubtitle,
                    palette.Get(ModColorRole.AccentBlue, 170),
                    palette.Get(ModColorRole.AccentBlue, 206),
                    _selectedMainIndex == 0);

                DrawButton(
                    _unloadButton,
                    unloadTitle,
                    unloadSubtitle,
                    palette.Get(ModColorRole.AccentPurple, 170),
                    palette.Get(ModColorRole.AccentPurple, 210),
                    _selectedMainIndex == 1);

                DrawButton(
                    _statsButton,
                    string.Format("VIEW {0} STATISTICS", industryName),
                    "Open statistics page with input/output loading bars",
                    palette.Get(ModColorRole.AccentTeal, 170),
                    palette.Get(ModColorRole.AccentTeal, 205),
                    _selectedMainIndex == 2);

                DrawButton(
                    _vehicleSpawnerButton,
                    "VEHICLE SPAWNER",
                    "Open fleet selection at this industry's spawn pad",
                    palette.Get(ModColorRole.AccentOrange, 170),
                    palette.Get(ModColorRole.AccentOrange, 210),
                    _selectedMainIndex == 3);

                DrawButton(
                    _upgradeButton,
                    GetUpgradeActionTitle(),
                    GetUpgradeActionSubtitle(),
                    palette.Get(ModColorRole.AccentGold, 170),
                    palette.Get(ModColorRole.AccentGold, 212),
                    _selectedMainIndex == 4);

                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                DrawText(
                    "Arrow Up/Down to navigate | Enter to select | E or Backspace or Esc to close",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    palette.Get(ModColorRole.TextMuted, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
            }
            else if (_currentPage == TabletPage.LoadSelection)
            {
                HideMainButtons();
                HideUpgradeModuleButtons();
                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                DrawText(
                    "SELECT LOAD RESOURCE",
                    _frameX + 80f,
                    _frameY + 116f,
                    0.39f,
                    palette.Get(ModColorRole.TextPrimary, 236),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    _loadOptions.Count > 0
                        ? "Choose the product to load into your truck"
                        : "No loadable product available right now",
                    _frameX + 82f,
                    _frameY + 143f,
                    0.30f,
                    palette.Get(ModColorRole.TextSecondary, 224),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                for (int i = 0; i < _upgradeModuleButtons.Count; i++)
                {
                    if (i < _loadOptions.Count)
                    {
                        DrawUpgradeModuleButton(
                            i,
                            string.Format("LOAD {0}", _loadOptions[i].ToUpperInvariant()),
                            GetLoadOptionSubtitle(_loadOptions[i]),
                            _selectedLoadOptionIndex == i);
                    }
                    else if (i == _loadOptions.Count)
                    {
                        DrawUpgradeModuleButton(
                            i,
                            "BACK TO OPERATIONS",
                            "Return to load/unload controls",
                            _selectedLoadOptionIndex == i);
                    }
                    else
                    {
                        _upgradeModuleButtons[i].Color = Color.FromArgb(0, 0, 0, 0);
                    }
                }

                DrawText(
                    "Arrow Up/Down to navigate | Enter to select | Backspace or Esc to return",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    palette.Get(ModColorRole.TextMuted, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
            }
            else if (_currentPage == TabletPage.UnloadSelection)
            {
                HideMainButtons();
                HideUpgradeModuleButtons();
                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                DrawText(
                    "SELECT UNLOAD MODE",
                    _frameX + 80f,
                    _frameY + 116f,
                    0.39f,
                    palette.Get(ModColorRole.TextPrimary, 236),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    "Choose whether to unload truck cargo or Omega only",
                    _frameX + 82f,
                    _frameY + 143f,
                    0.30f,
                    palette.Get(ModColorRole.TextSecondary, 224),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawUpgradeModuleButton(
                    0,
                    "UNLOAD TRUCK CARGO",
                    "Deliver your current cargo vehicle commodity",
                    _selectedUnloadOptionIndex == 0);

                DrawUpgradeModuleButton(
                    1,
                    "UNLOAD OMEGA",
                    "Only unload if your active cargo vehicle carries Omega cargo",
                    _selectedUnloadOptionIndex == 1);

                DrawUpgradeModuleButton(
                    2,
                    "BACK TO OPERATIONS",
                    "Return to load/unload controls",
                    _selectedUnloadOptionIndex == 2);

                for (int i = 3; i < _upgradeModuleButtons.Count; i++)
                {
                    _upgradeModuleButtons[i].Color = Color.FromArgb(0, 0, 0, 0);
                }

                DrawText(
                    "Arrow Up/Down to navigate | Enter to select | Backspace or Esc to return",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    palette.Get(ModColorRole.TextMuted, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
            }
            else if (_currentPage == TabletPage.Statistics)
            {
                HideMainButtons();
                HideUpgradeModuleButtons();
                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                var statistics = _statisticsSnapshotCache.GetSnapshot(_industry);
                IndustryStatisticsPanelRenderer.DrawPageContent(_industry, statistics, _statsScrollIndex, _frameX, _frameY, "Arrow Up/Down to scroll | Enter or Backspace or Esc to return");
            }
            else
            {
                HideMainButtons();
                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                var availableModules = GetAvailableUpgradeModules();
                var selectionCount = Math.Max(1, availableModules.Count + 1);
                if (_selectedUpgradeIndex >= selectionCount)
                {
                    _selectedUpgradeIndex = selectionCount - 1;
                }

                DrawText(
                    "UPGRADE MODULES",
                    _frameX + 80f,
                    _frameY + 116f,
                    0.39f,
                    palette.Get(ModColorRole.TextPrimary, 236),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    string.Format("Profit Balance: {0}", ModFormatting.FormatMoney(_profitBalance)),
                    _frameX + 82f,
                    _frameY + 143f,
                    0.30f,
                    palette.Get(ModColorRole.TextSecondary, 224),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                for (int i = 0; i < _upgradeModuleButtons.Count; i++)
                {
                    if (i < availableModules.Count)
                    {
                        var module = availableModules[i];
                        DrawUpgradeModuleButton(
                            i,
                            GetUpgradeModuleTitle(module),
                            GetUpgradeModuleSubtitle(module),
                            _selectedUpgradeIndex == i);
                    }
                    else if (i == availableModules.Count)
                    {
                        DrawUpgradeModuleButton(
                            i,
                            "BACK TO OPERATIONS",
                            "Return to load/unload controls",
                            _selectedUpgradeIndex == i);
                    }
                    else
                    {
                        _upgradeModuleButtons[i].Color = Color.FromArgb(0, 0, 0, 0);
                    }
                }

                DrawText(
                    "Arrow Up/Down to navigate | Enter to buy | Backspace or Esc to return",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    palette.Get(ModColorRole.TextMuted, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
            }
        }

        private string GetUpgradeModuleSubtitle(IndustryUpgradeModule module)
        {
            if (_industry == null)
            {
                return "No industry selected";
            }

            var level = _industry.GetUpgradeLevel(module);
            var cost = _industry.GetUpgradeCost(module);

            if (cost <= 0f)
            {
                return "Unavailable for this industry";
            }

            if (_profitBalance < cost)
            {
                return string.Format("Lv.{0} -> {1} | Need {2} more", level, ModFormatting.FormatMoney(cost), ModFormatting.FormatMoney(cost - _profitBalance));
            }

            return string.Format("Lv.{0} -> {1} | Press Enter to purchase", level, ModFormatting.FormatMoney(cost));
        }

        private int GetUpgradeSelectionCount()
        {
            return Math.Max(1, GetAvailableUpgradeModules().Count + 1);
        }

        private List<IndustryUpgradeModule> GetAvailableUpgradeModules()
        {
            if (!_upgradeModulesDirty)
            {
                return _availableUpgradeModules;
            }

            _availableUpgradeModules.Clear();
            if (_industry == null)
            {
                _upgradeModulesDirty = false;
                return _availableUpgradeModules;
            }

            var candidates = new[]
            {
                IndustryUpgradeModule.Production,
                IndustryUpgradeModule.InputStorage,
                IndustryUpgradeModule.OutputStorage,
                IndustryUpgradeModule.OmegaStorage,
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var module = candidates[i];
                if (_industry.GetUpgradeCost(module) > 0f)
                {
                    _availableUpgradeModules.Add(module);
                }
            }

            _upgradeModulesDirty = false;
            return _availableUpgradeModules;
        }

        private static string GetUpgradeModuleTitle(IndustryUpgradeModule module)
        {
            if (module == IndustryUpgradeModule.Production)
            {
                return "PRODUCTION MODULE";
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                return "INPUT STORAGE MODULE";
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return "OUTPUT STORAGE MODULE";
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                return "OMEGA TANK MODULE";
            }

            return "MODULE";
        }

        private string GetLoadOptionSubtitle(string commodity)
        {
            if (string.IsNullOrWhiteSpace(commodity))
            {
                return "Start loading this resource";
            }

            string subtitle;
            if (_loadOptionSubtitles.TryGetValue(commodity.Trim(), out subtitle) && !string.IsNullOrWhiteSpace(subtitle))
            {
                return subtitle;
            }

            return "Start loading this resource";
        }

        private string BuildIndustrySubtitle()
        {
            if (_requiresIndustryPurchase)
            {
                return string.Format(
                    "Purchase for {0} to unlock upgrades and remove the {1} owner cut",
                    ModFormatting.FormatMoney(_industryPrice),
                    ModFormatting.FormatPercent(_industryOwnerCut * 100f));
            }

            return "Industry Operations Interface";
        }

        private string GetUpgradeActionTitle()
        {
            return _requiresIndustryPurchase ? "BUY INDUSTRY" : "OPEN UPGRADES";
        }

        private string GetUpgradeActionSubtitle()
        {
            if (_requiresIndustryPurchase)
            {
                return string.Format("Price {0} | Unlock upgrades at this site", ModFormatting.FormatMoney(_industryPrice));
            }

            return "Switch to module upgrades in this industry";
        }

        private void DrawUpgradeModuleButton(int index, string title, string subtitle, bool selected)
        {
            if (index < 0 || index >= _upgradeModuleButtons.Count)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            var rectangle = _upgradeModuleButtons[index];
            var idle = index == 4 ? palette.Get(ModColorRole.AccentGold, 160) : palette.Get(ModColorRole.BackgroundCard, 160);
            var active = index == 4 ? palette.Get(ModColorRole.AccentGold, 212) : palette.Get(ModColorRole.BackgroundCardSelected, 210);
            rectangle.Color = selected ? active : idle;
            rectangle.Draw();

            DrawText(
                title,
                rectangle.Position.X + 20f,
                rectangle.Position.Y + 14f,
                0.40f,
                palette.Get(ModColorRole.TextPrimary, 238),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                subtitle,
                rectangle.Position.X + 22f,
                rectangle.Position.Y + 39f,
                0.265f,
                palette.Get(ModColorRole.TextSecondary, 220),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        private void DrawButton(ScaledRectangle rectangle, string title, string subtitle, Color idleColor, Color hoverColor, bool selected)
        {
            var palette = AccessibilityTheme.Service.Palette;
            rectangle.Color = selected ? hoverColor : idleColor;
            rectangle.Draw();

            DrawText(
                title,
                rectangle.Position.X + 20f,
                rectangle.Position.Y + 18f,
                0.46f,
                palette.Get(ModColorRole.TextPrimary, 238),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                subtitle,
                rectangle.Position.X + 22f,
                rectangle.Position.Y + 49f,
                0.285f,
                palette.Get(ModColorRole.TextSecondary, 220),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        private void HideMainButtons()
        {
            _loadButton.Color = Color.FromArgb(0, 0, 0, 0);
            _unloadButton.Color = Color.FromArgb(0, 0, 0, 0);
            _statsButton.Color = Color.FromArgb(0, 0, 0, 0);
            _vehicleSpawnerButton.Color = Color.FromArgb(0, 0, 0, 0);
            _upgradeButton.Color = Color.FromArgb(0, 0, 0, 0);
        }

        private void HideUpgradeModuleButtons()
        {
            for (int i = 0; i < _upgradeModuleButtons.Count; i++)
            {
                _upgradeModuleButtons[i].Color = Color.FromArgb(0, 0, 0, 0);
            }
        }

        private void MoveStatsSelection(int delta)
        {
            _statsScrollIndex = IndustryStatisticsPanelRenderer.MoveScrollIndex(_statisticsSnapshotCache.GetSnapshot(_industry), _statsScrollIndex, delta);
        }

        private static void DrawLoadingBar(float x, float y, float width, float height, float ratio, Color backgroundColor, Color fillColor)
        {
            var back = new ScaledRectangle(new PointF(x, y), new SizeF(width, height))
            {
                Color = backgroundColor,
            };
            back.Draw();

            var innerHeight = Math.Max(2f, height - 4f);
            var innerWidth = Math.Max(2f, (width - 4f) * ModMath.Clamp01(ratio));

            var fill = new ScaledRectangle(new PointF(x + 2f, y + 2f), new SizeF(innerWidth, innerHeight))
            {
                Color = fillColor,
            };
            fill.Draw();
        }

        private static string BuildIndustryLabel(string industryName)
        {
            if (string.IsNullOrWhiteSpace(industryName))
            {
                return "INDUSTRY";
            }

            var label = industryName.Trim().ToUpperInvariant();
            if (label.Length <= 24)
            {
                return label;
            }

            return label.Substring(0, 24);
        }

        private float ResolveOwnershipLabelX(string industryName)
        {
            var nameLength = string.IsNullOrWhiteSpace(industryName) ? 0 : industryName.Length;
            return Math.Min(_frameX + 628f, _frameX + 92f + (nameLength * 13.5f));
        }

        private static void DrawText(string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float wrap)
        {
            var entry = new ScaledText(new PointF(x, y), text ?? string.Empty, scale, font)
            {
                Alignment = alignment,
                Color = color,
                Shadow = false,
                Outline = false,
            };

            if (wrap > 0f)
            {
                entry.WordWrap = wrap;
            }

            entry.Draw();
        }

        private static float GetUiWidth()
        {
            float width;
            float height;
            GetUiSize(out width, out height);
            return width;
        }

        private static float GetUiHeight()
        {
            float width;
            float height;
            GetUiSize(out width, out height);
            return height;
        }

        private static void GetUiSize(out float width, out float height)
        {
            width = UiFallbackWidth;
            height = UiFallbackHeight;
        }

        private void UpdateLayout()
        {
            var uiWidth = GetUiWidth();
            var uiHeight = GetUiHeight();

            const float buttonSpacing = 74f;

            const float buttonsCenterX = 0f;
            const float buttonsCenterY = 0f;
            var mainButtonsHeight = _loadButton.Size.Height + (buttonSpacing * 4f);
            var buttonX = (uiWidth * 0.5f) + buttonsCenterX - (_loadButton.Size.Width * 0.5f);
            var buttonTopY = (uiHeight * 0.5f) + buttonsCenterY - (mainButtonsHeight * 0.5f);

            // Keep text and sub-panels aligned to the centered button column.
            _frameX = buttonX - 72f;
            _frameY = buttonTopY - 112f;
            _menuBackground.Position = new PointF(_frameX, _frameY);

            _loadButton.Position = new PointF(buttonX, buttonTopY);
            _unloadButton.Position = new PointF(buttonX, buttonTopY + buttonSpacing);
            _statsButton.Position = new PointF(buttonX, buttonTopY + (buttonSpacing * 2f));
            _vehicleSpawnerButton.Position = new PointF(buttonX, buttonTopY + (buttonSpacing * 3f));
            _upgradeButton.Position = new PointF(buttonX, buttonTopY + (buttonSpacing * 4f));

            _statsPanel.Position = new PointF(buttonX, buttonTopY + 296f);

            var moduleTopY = buttonTopY + 62f;
            const float moduleSpacing = 62f;
            for (int i = 0; i < _upgradeModuleButtons.Count; i++)
            {
                _upgradeModuleButtons[i].Position = new PointF(buttonX, moduleTopY + (i * moduleSpacing));
            }
        }
    }
}

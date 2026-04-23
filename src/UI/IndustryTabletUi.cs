using System;
using System.Collections.Generic;
using System.Drawing;
using IndustryLogisticV.Config;
using GTA;
using GTA.UI;
using IndustryLogisticV.Domain;
using LemonUI.Elements;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.UI
{
    public sealed class IndustryTabletUi
    {
        private enum TabletPage
        {
            Main = 0,
            Upgrades = 1,
            LoadSelection = 2,
            UnloadSelection = 3,
        }

        private const float UiFallbackWidth = 1280f;
        private const float UiFallbackHeight = 720f;
        private const float MenuBackgroundWidth = 784f;
        private const float MenuBackgroundHeight = 536f;

        private readonly ScaledRectangle _menuBackground;
        private readonly ScaledRectangle _loadButton;
        private readonly ScaledRectangle _unloadButton;
        private readonly ScaledRectangle _statsButton;
        private readonly ScaledRectangle _upgradeButton;
        private readonly ScaledRectangle _statsPanel;
        private readonly List<ScaledRectangle> _upgradeModuleButtons;
        private readonly List<string> _loadOptions;

        private Industry _industry;
        private bool _showStatsPanel;
        private float _frameX;
        private float _frameY;
        private float _profitBalance;
        private int _selectedMainIndex;
        private int _selectedUpgradeIndex;
        private int _selectedLoadOptionIndex;
        private int _selectedUnloadOptionIndex;
        private TabletPage _currentPage;

        public IndustryTabletUi()
        {
            _menuBackground = new ScaledRectangle(new PointF(0f, 0f), new SizeF(MenuBackgroundWidth, MenuBackgroundHeight))
            {
                Color = Color.FromArgb(204, 8, 12, 18),
            };

            _loadButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _unloadButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
            _statsButton = new ScaledRectangle(new PointF(0f, 0f), new SizeF(640f, 68f));
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
            _currentPage = TabletPage.Main;
            _loadOptions = new List<string>();
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

        public event Action<Industry, IndustryUpgradeModule> UpgradeModuleRequested;

        public void SetLoadOptions(List<string> loadOptions)
        {
            _loadOptions.Clear();
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
                    _selectedMainIndex = 3;
                    _showStatsPanel = false;
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
                const int count = 4;
                _selectedMainIndex = (_selectedMainIndex + delta + count) % count;
                return;
            }

            if (_currentPage == TabletPage.Upgrades)
            {
                const int upgradeCount = 5;
                _selectedUpgradeIndex = (_selectedUpgradeIndex + delta + upgradeCount) % upgradeCount;
                return;
            }

            if (_currentPage == TabletPage.LoadSelection)
            {
                var loadChoiceCount = Math.Max(1, _loadOptions.Count + 1);
                _selectedLoadOptionIndex = (_selectedLoadOptionIndex + delta + loadChoiceCount) % loadChoiceCount;
                return;
            }

            const int unloadChoiceCount = 3;
            _selectedUnloadOptionIndex = (_selectedUnloadOptionIndex + delta + unloadChoiceCount) % unloadChoiceCount;
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
                        _showStatsPanel = false;
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
                    var hasMultipleInputs = _industry.Inputs != null && _industry.Inputs.Count > 1;
                    if (hasMultipleInputs)
                    {
                        _currentPage = TabletPage.UnloadSelection;
                        _selectedUnloadOptionIndex = 0;
                        _showStatsPanel = false;
                        return;
                    }

                    UnloadRequested?.Invoke(_industry);
                    return;
                }

                if (_selectedMainIndex == 2)
                {
                    _showStatsPanel = !_showStatsPanel;
                    return;
                }

                if (_selectedMainIndex == 3)
                {
                    _currentPage = TabletPage.Upgrades;
                    _selectedUpgradeIndex = 0;
                    _showStatsPanel = false;
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

            if (_selectedUpgradeIndex == 4)
            {
                _currentPage = TabletPage.Main;
                _selectedMainIndex = 3;
                return;
            }

            var module = (IndustryUpgradeModule)_selectedUpgradeIndex;
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
            _showStatsPanel = false;
            _selectedMainIndex = 0;
            _selectedUpgradeIndex = 0;
            _selectedLoadOptionIndex = 0;
            _selectedUnloadOptionIndex = 0;
            _currentPage = TabletPage.Main;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _industry = null;
            IsOpen = false;
            _showStatsPanel = false;
            _currentPage = TabletPage.Main;
            _selectedMainIndex = 0;
            _selectedUpgradeIndex = 0;
            _selectedLoadOptionIndex = 0;
            _selectedUnloadOptionIndex = 0;
            _loadOptions.Clear();
            HideUpgradeModuleButtons();
            HideMainButtons();
        }

        public void DrawAndHandleInput()
        {
            if (!IsOpen || _industry == null)
            {
                return;
            }

            UpdateLayout();
            _menuBackground.Draw();

            var industryName = BuildIndustryLabel(_industry.Name);
            var stockpile = _industry.GetInputStockTotal() + _industry.GetOutputStockTotal();
            var totalCapacity = Math.Max(1f, _industry.InputCapacityTons + _industry.OutputCapacityTons);
            var stockRatio = Clamp01(stockpile / totalCapacity);
            var utilizationRatio = Clamp01(_industry.LastUtilizationPercent / 100f);
            var omegaRatio = Clamp01(_industry.OmegaStorage / Math.Max(1f, _industry.OmegaCapacityTons));

            DrawText(
                string.Format("{0} MENU", industryName),
                _frameX + 78f,
                _frameY + 62f,
                0.53f,
                Color.FromArgb(236, 242, 246, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                "Industry Operations Interface",
                _frameX + 80f,
                _frameY + 88f,
                0.31f,
                Color.FromArgb(222, 214, 225, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (_currentPage == TabletPage.Main)
            {
                HideUpgradeModuleButtons();

                var hasMultipleInputs = _industry.Inputs != null && _industry.Inputs.Count > 1;
                var unloadTitle = hasMultipleInputs ? "UNLOAD CARGO" : "UNLOAD OMEGA FLUID";
                var unloadSubtitle = hasMultipleInputs
                    ? "Choose Omega or truck cargo in-tablet"
                    : "Deliver Omega boost fluid from your tanker";
                var loadSubtitle = _loadOptions.Count > 1
                    ? "Choose resource in-tablet after pressing load"
                    : "Initiate cargo load from local production stockpile";

                DrawButton(
                    _loadButton,
                    "LOAD CARGO TRUCK",
                    loadSubtitle,
                    Color.FromArgb(170, 45, 62, 74),
                    Color.FromArgb(206, 88, 125, 150),
                    _selectedMainIndex == 0);

                DrawButton(
                    _unloadButton,
                    unloadTitle,
                    unloadSubtitle,
                    Color.FromArgb(170, 56, 45, 61),
                    Color.FromArgb(210, 132, 86, 158),
                    _selectedMainIndex == 1);

                DrawButton(
                    _statsButton,
                    string.Format("VIEW {0} STATISTICS", industryName),
                    "Display live stockpile, output and utilization graph",
                    _showStatsPanel ? Color.FromArgb(195, 74, 95, 58) : Color.FromArgb(170, 46, 66, 50),
                    _showStatsPanel ? Color.FromArgb(226, 120, 172, 98) : Color.FromArgb(205, 85, 124, 94),
                    _selectedMainIndex == 2);

                DrawButton(
                    _upgradeButton,
                    "OPEN UPGRADES",
                    "Switch to module upgrades in this industry",
                    Color.FromArgb(170, 58, 51, 86),
                    Color.FromArgb(212, 124, 104, 178),
                    _selectedMainIndex == 3);

                if (_showStatsPanel)
                {
                    _statsPanel.Color = Color.FromArgb(185, 20, 30, 40);
                    _statsPanel.Draw();
                    DrawStatsText(stockpile, totalCapacity, stockRatio, utilizationRatio, omegaRatio);
                }
                else
                {
                    _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);
                }

                DrawText(
                    "Arrow Up/Down to navigate | Enter to select | E or Backspace or Esc to close",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    Color.FromArgb(214, 195, 206, 218),
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
                    Color.FromArgb(236, 234, 242, 252),
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
                    Color.FromArgb(224, 214, 226, 236),
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
                            "Start loading this resource",
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
                    Color.FromArgb(214, 195, 206, 218),
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
                    Color.FromArgb(236, 234, 242, 252),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    "Choose whether to unload truck cargo or Omega only",
                    _frameX + 82f,
                    _frameY + 143f,
                    0.30f,
                    Color.FromArgb(224, 214, 226, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawUpgradeModuleButton(
                    0,
                    "UNLOAD TRUCK CARGO",
                    "Deliver your current tanker commodity",
                    _selectedUnloadOptionIndex == 0);

                DrawUpgradeModuleButton(
                    1,
                    "UNLOAD OMEGA",
                    "Only unload if your tanker carries Omega",
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
                    Color.FromArgb(214, 195, 206, 218),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
            }
            else
            {
                HideMainButtons();
                _statsPanel.Color = Color.FromArgb(0, 0, 0, 0);

                DrawText(
                    "UPGRADE MODULES",
                    _frameX + 80f,
                    _frameY + 116f,
                    0.39f,
                    Color.FromArgb(236, 234, 242, 252),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    string.Format("Profit Balance: ${0:0}", _profitBalance),
                    _frameX + 82f,
                    _frameY + 143f,
                    0.30f,
                    Color.FromArgb(224, 214, 226, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawUpgradeModuleButton(0, "PRODUCTION MODULE", GetUpgradeModuleSubtitle(IndustryUpgradeModule.Production), _selectedUpgradeIndex == 0);
                DrawUpgradeModuleButton(1, "INPUT STORAGE MODULE", GetUpgradeModuleSubtitle(IndustryUpgradeModule.InputStorage), _selectedUpgradeIndex == 1);
                DrawUpgradeModuleButton(2, "OUTPUT STORAGE MODULE", GetUpgradeModuleSubtitle(IndustryUpgradeModule.OutputStorage), _selectedUpgradeIndex == 2);
                DrawUpgradeModuleButton(3, "OMEGA TANK MODULE", GetUpgradeModuleSubtitle(IndustryUpgradeModule.OmegaStorage), _selectedUpgradeIndex == 3);
                DrawUpgradeModuleButton(4, "BACK TO OPERATIONS", "Return to load/unload controls", _selectedUpgradeIndex == 4);

                DrawText(
                    "Arrow Up/Down to navigate | Enter to buy | Backspace or Esc to return",
                    _frameX + 80f,
                    _frameY + 515f,
                    0.28f,
                    Color.FromArgb(214, 195, 206, 218),
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
                return string.Format("Lv.{0} -> ${1:0} | Need ${2:0} more", level, cost, cost - _profitBalance);
            }

            return string.Format("Lv.{0} -> ${1:0} | Press Enter to purchase", level, cost);
        }

        private void DrawUpgradeModuleButton(int index, string title, string subtitle, bool selected)
        {
            if (index < 0 || index >= _upgradeModuleButtons.Count)
            {
                return;
            }

            var rectangle = _upgradeModuleButtons[index];
            var idle = index == 4 ? Color.FromArgb(160, 66, 58, 82) : Color.FromArgb(160, 46, 60, 76);
            var active = index == 4 ? Color.FromArgb(212, 124, 104, 178) : Color.FromArgb(210, 92, 126, 158);
            rectangle.Color = selected ? active : idle;
            rectangle.Draw();

            DrawText(
                title,
                rectangle.Position.X + 20f,
                rectangle.Position.Y + 14f,
                0.40f,
                Color.FromArgb(238, 245, 249, 255),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                subtitle,
                rectangle.Position.X + 22f,
                rectangle.Position.Y + 39f,
                0.265f,
                Color.FromArgb(220, 222, 231, 240),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        private void DrawButton(ScaledRectangle rectangle, string title, string subtitle, Color idleColor, Color hoverColor, bool selected)
        {
            rectangle.Color = selected ? hoverColor : idleColor;
            rectangle.Draw();

            DrawText(
                title,
                rectangle.Position.X + 20f,
                rectangle.Position.Y + 18f,
                0.46f,
                Color.FromArgb(238, 245, 249, 255),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                subtitle,
                rectangle.Position.X + 22f,
                rectangle.Position.Y + 49f,
                0.285f,
                Color.FromArgb(220, 222, 231, 240),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        private void HideMainButtons()
        {
            _loadButton.Color = Color.FromArgb(0, 0, 0, 0);
            _unloadButton.Color = Color.FromArgb(0, 0, 0, 0);
            _statsButton.Color = Color.FromArgb(0, 0, 0, 0);
            _upgradeButton.Color = Color.FromArgb(0, 0, 0, 0);
        }

        private void HideUpgradeModuleButtons()
        {
            for (int i = 0; i < _upgradeModuleButtons.Count; i++)
            {
                _upgradeModuleButtons[i].Color = Color.FromArgb(0, 0, 0, 0);
            }
        }

        private void DrawStatsText(float stockpile, float totalCapacity, float stockRatio, float utilizationRatio, float omegaRatio)
        {
            DrawText(
                string.Format("Omega Fill: {0:0}%", omegaRatio * 100f),
                _frameX + 88f,
                _frameY + 436f,
                0.30f,
                Color.FromArgb(220, 214, 223, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawLoadingBar(
                _frameX + 300f,
                _frameY + 444f,
                190f,
                12f,
                omegaRatio,
                Color.FromArgb(170, 28, 40, 54),
                Color.FromArgb(230, 112, 164, 236));

            DrawText(
                string.Format("Stockpile: {0:0.0}/{1:0.0} t", stockpile, totalCapacity),
                _frameX + 88f,
                _frameY + 458f,
                0.30f,
                Color.FromArgb(220, 214, 223, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawLoadingBar(
                _frameX + 300f,
                _frameY + 466f,
                190f,
                12f,
                stockRatio,
                Color.FromArgb(170, 28, 40, 54),
                Color.FromArgb(230, 214, 188, 96));

            DrawText(
                string.Format("Output: {0:0.0} t/h", _industry.CurrentOutputPerHourTons),
                _frameX + 88f,
                _frameY + 482f,
                0.30f,
                Color.FromArgb(220, 214, 223, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        private static void DrawLoadingBar(float x, float y, float width, float height, float ratio, Color backgroundColor, Color fillColor)
        {
            var back = new ScaledRectangle(new PointF(x, y), new SizeF(width, height))
            {
                Color = backgroundColor,
            };
            back.Draw();

            var innerHeight = Math.Max(2f, height - 4f);
            var innerWidth = Math.Max(2f, (width - 4f) * Clamp01(ratio));

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
            var mainButtonsHeight = _loadButton.Size.Height + (buttonSpacing * 3f);
            var buttonX = (uiWidth * 0.5f) + buttonsCenterX - (_loadButton.Size.Width * 0.5f);
            var buttonTopY = (uiHeight * 0.5f) + buttonsCenterY - (mainButtonsHeight * 0.5f);

            // Keep text and sub-panels aligned to the centered button column.
            _frameX = buttonX - 72f;
            _frameY = buttonTopY - 112f;
            _menuBackground.Position = new PointF(_frameX, _frameY);

            _loadButton.Position = new PointF(buttonX, buttonTopY);
            _unloadButton.Position = new PointF(buttonX, buttonTopY + buttonSpacing);
            _statsButton.Position = new PointF(buttonX, buttonTopY + (buttonSpacing * 2f));
            _upgradeButton.Position = new PointF(buttonX, buttonTopY + (buttonSpacing * 3f));

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

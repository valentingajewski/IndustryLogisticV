using System;
using System.Collections.Generic;
using System.Drawing;
using LSOL.Config;
using GTA.Native;
using GTA.UI;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    public sealed class SimpleMenuTabletPanelContext
    {
        public Size Resolution { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int SelectedIndex { get; set; }
        public int FirstVisibleIndex { get; set; }
        public IReadOnlyList<MenuItem> Items { get; set; }
        public IReadOnlyList<MenuItem> VisibleItems { get; set; }
    }

    public enum SimpleMenuTheme
    {
        Classic = 0,
        Tablet = 1,
    }

    public enum SimpleMenuTabletLayout
    {
        List = 0,
        Dashboard = 1,
    }

    public sealed class MenuItem
    {
        public Func<string> CaptionFactory { get; set; }
        public Func<string> CaptionAccentFactory { get; set; }
        public Func<string> CaptionSuffixFactory { get; set; }
        public Func<Color?> CaptionAccentColorFactory { get; set; }
        public Func<string> DetailFactory { get; set; }
        public Func<string> IconLabelFactory { get; set; }
        public Func<float?> ProgressRatioFactory { get; set; }
        public Func<bool> CheckboxStateFactory { get; set; }
        public Action OnActivate { get; set; }
        public Action OnLeft { get; set; }
        public Action OnRight { get; set; }
        public bool IsSeparator { get; set; }
        public Color? IdleBackgroundColor { get; set; }
        public Color? SelectedBackgroundColor { get; set; }
        public Color? ProgressBarColor { get; set; }
    }

    public sealed class SimpleMenu
    {
        private readonly List<MenuItem> _items;
        private int _firstVisibleIndex;

        public SimpleMenu(string title)
        {
            Title = title;
            Subtitle = string.Empty;
            _items = new List<MenuItem>();
            _firstVisibleIndex = 0;
            SelectedIndex = 0;
            Theme = SimpleMenuTheme.Classic;
            TabletLayout = SimpleMenuTabletLayout.List;
            TabletWidthScale = 1f;
            TabletAlignRight = false;
            TabletCaptionScale = 0.305f;
            TabletDetailScale = 0.235f;
            TabletCaptionOffsetY = 8f;
            TabletDetailOffsetY = 31f;
            TabletMinRowHeight = 0f;
            TabletMinProgressRowHeight = 0f;
            TabletDashboardSidebarCount = 0;
            TabletDashboardTileColumns = 5;
            TabletBottomPanelHeight = 0f;
            MaxVisibleItems = 0;
        }

        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Func<string> HeaderRightTextFactory { get; set; }
        public Func<string> FooterTextFactory { get; set; }
        public SimpleMenuTheme Theme { get; set; }
        public SimpleMenuTabletLayout TabletLayout { get; set; }
        public float TabletWidthScale { get; set; }
        public bool TabletAlignRight { get; set; }
        public float TabletCaptionScale { get; set; }
        public float TabletDetailScale { get; set; }
        public float TabletCaptionOffsetY { get; set; }
        public float TabletDetailOffsetY { get; set; }
        public float TabletMinRowHeight { get; set; }
        public float TabletMinProgressRowHeight { get; set; }
        public int TabletDashboardSidebarCount { get; set; }
        public int TabletDashboardTileColumns { get; set; }
        public float TabletBottomPanelHeight { get; set; }
        public Action<SimpleMenuTabletPanelContext> TabletContentRenderer { get; set; }
        public Action<SimpleMenuTabletPanelContext> TabletBottomPanelRenderer { get; set; }
        public Action TabletSelectAction { get; set; }
        public int MaxVisibleItems { get; set; }
        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; }

        public void SetItems(IEnumerable<MenuItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
            if (SelectedIndex >= _items.Count)
            {
                SelectedIndex = Math.Max(0, _items.Count - 1);
            }

            EnsureSelectionVisible();
        }

        public void SetSelectedIndex(int selectedIndex)
        {
            if (_items.Count == 0)
            {
                SelectedIndex = 0;
                _firstVisibleIndex = 0;
                return;
            }

            SelectedIndex = Math.Max(0, Math.Min(_items.Count - 1, selectedIndex));
            EnsureSelectionVisible();
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            if (!IsOpen || _items.Count == 0)
            {
                return;
            }

            if (controls == null)
            {
                controls = new ControlBindings();
            }

            if (Theme == SimpleMenuTheme.Tablet && TabletLayout == SimpleMenuTabletLayout.Dashboard)
            {
                if (TryHandleDashboardNavigation(key, controls))
                {
                    return;
                }
            }

            if (key == controls.MenuUp)
            {
                SelectedIndex = SelectedIndex <= 0 ? _items.Count - 1 : SelectedIndex - 1;
                EnsureSelectionVisible();
                return;
            }

            if (key == controls.MenuDown)
            {
                SelectedIndex = (SelectedIndex + 1) % _items.Count;
                EnsureSelectionVisible();
                return;
            }

            var selected = _items[SelectedIndex];
            if (key == controls.MenuLeft)
            {
                selected.OnLeft?.Invoke();
                return;
            }

            if (key == controls.MenuRight)
            {
                selected.OnRight?.Invoke();
                return;
            }

            if (key == controls.MenuSelect)
            {
                if (selected.OnActivate != null)
                {
                    selected.OnActivate();
                    return;
                }

                TabletSelectAction?.Invoke();
                return;
            }

            if (key == controls.MenuBack || key == WinForms.Keys.Escape)
            {
                Close();
            }
        }

        public void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Theme == SimpleMenuTheme.Tablet)
            {
                DrawTabletTheme();
                return;
            }

            DrawClassicTheme();
        }

        private void DrawClassicTheme()
        {
            var palette = AccessibilityTheme.Service.Palette;
            var resolution = GTA.UI.Screen.MainWindowResolution;
            var x = resolution.Width * 0.56f;
            var y = resolution.Height * 0.15f;
            var width = resolution.Width * 0.38f;
            var lineHeight = resolution.Height * 0.038f;
            var visibleItems = GetVisibleItems();
            var contentHeight = ComputeContentHeight(lineHeight, visibleItems);
            var headerHeight = lineHeight * 1.55f;
            var footerHeight = lineHeight * 0.86f;
            var height = contentHeight + headerHeight + footerHeight + 10f;

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, palette.Get(ModColorRole.BackgroundOuter, 95));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, palette.Get(ModColorRole.BackgroundOuter, 206));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 5f, palette.Get(ModColorRole.AccentGold, 238));
            DrawRect(resolution.Width, resolution.Height, x, y + 5f, width, headerHeight - 5f, palette.Get(ModColorRole.BackgroundHeader, 194));

            DrawTextBlock(
                resolution,
                Title,
                x + 12f,
                y + 8f,
                0.40f,
                palette.Get(ModColorRole.TextPrimary),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                14f);

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                DrawTextBlock(
                    resolution,
                    Subtitle,
                    x + 12f,
                    y + 30f,
                    0.27f,
                    palette.Get(ModColorRole.TextSecondary, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    12f);
            }

            var rowY = y + headerHeight;
            for (int i = 0; i < visibleItems.Count; i++)
            {
                var itemIndex = _firstVisibleIndex + i;
                var item = visibleItems[i];
                var detail = GetDetailText(item);
                var hasProgressBar = HasProgressBar(item);
                var rowHeight = GetRowHeight(lineHeight, detail, hasProgressBar);
                var idleRowColor = item.IdleBackgroundColor ?? (itemIndex % 2 == 0 ? palette.Get(ModColorRole.TextPrimary, 34) : Color.Empty);
                if (idleRowColor != Color.Empty)
                {
                    DrawRect(resolution.Width, resolution.Height, x, rowY, width, rowHeight, idleRowColor);
                }

                if (itemIndex == SelectedIndex)
                {
                    var selectedRowColor = item.SelectedBackgroundColor ?? palette.Get(ModColorRole.AccentGold, 220);
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, width - 4f, rowHeight - 4f, selectedRowColor);
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, 5f, rowHeight - 4f, palette.Get(ModColorRole.Highlight, 240));
                }

                var color = itemIndex == SelectedIndex ? palette.Get(ModColorRole.TextPrimary) : palette.Get(ModColorRole.TextSecondary, 235);
                DrawMenuItemCaption(
                    resolution,
                    item,
                    x + 11f,
                    rowY + 6f,
                    0.30f,
                    color,
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    14f);

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    var detailColor = itemIndex == SelectedIndex
                        ? palette.Get(ModColorRole.TextPrimary, 232)
                        : palette.Get(ModColorRole.TextMuted, 214);
                    DrawTextBlock(
                        resolution,
                        detail,
                        x + 11f,
                        rowY + 28f,
                        0.225f,
                        detailColor,
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        12f);
                }

                DrawProgressBarIfNeeded(
                    resolution,
                    item,
                    x + 11f,
                    rowY + rowHeight - 11f,
                    width - 22f,
                    6f,
                    itemIndex == SelectedIndex);

                rowY += rowHeight;
            }

            var footerY = y + headerHeight + contentHeight;
            DrawRect(resolution.Width, resolution.Height, x, footerY, width, footerHeight, palette.Get(ModColorRole.BackgroundPanel, 185));
            var footerText = GetFooterText("Navigate | Edit | Select | Close");
            DrawTextBlock(
                resolution,
                footerText,
                x + 12f,
                footerY + 4f,
                0.255f,
                palette.Get(ModColorRole.TextSecondary, 228),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                12f);
        }

        private void DrawTabletTheme()
        {
            if (TabletLayout == SimpleMenuTabletLayout.Dashboard)
            {
                DrawTabletDashboardTheme();
                return;
            }

            DrawTabletListTheme();
        }

        private void DrawTabletListTheme()
        {
            var palette = AccessibilityTheme.Service.Palette;
            var resolution = GTA.UI.Screen.MainWindowResolution;
            var scale = Math.Max(0.25f, TabletWidthScale);
            var lineHeight = resolution.Height * 0.043f;
            var headerHeight = lineHeight * 1.95f;
            var footerHeight = lineHeight * 0.88f;
            var screenWidth = resolution.Width * 0.56f * scale;
            var screenHeight = resolution.Height * 0.58f;
            var bottomPanelHeight = TabletBottomPanelRenderer != null && TabletBottomPanelHeight > 0f
                ? TabletBottomPanelHeight
                : 0f;
            var contentHeight = GetTabletListContentHeight(screenHeight, lineHeight, headerHeight, footerHeight, bottomPanelHeight);
            var visibleItems = GetVisibleItems(lineHeight, contentHeight);
            var bodyX = (resolution.Width - (screenWidth + 58f)) * 0.5f;
            var bodyY = resolution.Height * 0.08f;
            var screenX = bodyX + 29f;
            var screenY = bodyY + 34f;
            var bodyWidth = screenWidth + 58f;
            var bodyHeight = screenHeight + 82f;

            DrawTabletDeviceFrame(resolution, bodyX, bodyY, bodyWidth, bodyHeight, screenX, screenY, screenWidth, screenHeight);
            DrawTabletWallpaper(resolution, screenX, screenY, screenWidth, screenHeight);
            DrawRect(resolution.Width, resolution.Height, screenX, screenY, screenWidth, headerHeight + 8f, palette.Get(ModColorRole.BackgroundOuter, 94));

            DrawTextBlock(
                resolution,
                Title,
                screenX + 20f,
                screenY + 16f,
                0.43f,
                palette.Get(ModColorRole.TextPrimary, 236),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                14f);

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                DrawTextBlock(
                    resolution,
                    Subtitle,
                    screenX + 20f,
                    screenY + 42f,
                    0.275f,
                    palette.Get(ModColorRole.TextSecondary, 222),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    12f);
            }

            var headerRightText = GetHeaderRightText();
            if (!string.IsNullOrWhiteSpace(headerRightText))
            {
                DrawTextBlock(
                    resolution,
                    headerRightText,
                    screenX + screenWidth - 20f,
                    screenY + 18f,
                    0.255f,
                    palette.Get(ModColorRole.TextSecondary, 230),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Right,
                    12f);
            }

            var customContentRenderer = TabletContentRenderer;
            if (customContentRenderer != null)
            {
                customContentRenderer(new SimpleMenuTabletPanelContext
                {
                    Resolution = resolution,
                    X = screenX + 18f,
                    Y = screenY + headerHeight + 12f,
                    Width = screenWidth - 36f,
                    Height = contentHeight,
                    SelectedIndex = SelectedIndex,
                    FirstVisibleIndex = _firstVisibleIndex,
                    Items = _items,
                    VisibleItems = visibleItems,
                });
            }
            else
            {
                var rowY = screenY + headerHeight + 12f;
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    var itemIndex = _firstVisibleIndex + i;
                    var item = visibleItems[i];
                    var detail = GetDetailText(item);
                    var rowHeight = GetItemRowHeight(lineHeight, item);

                    var selected = itemIndex == SelectedIndex;
                    var idleColor = item.IdleBackgroundColor ?? palette.Get(ModColorRole.BackgroundCard, 142);
                    var activeColor = item.SelectedBackgroundColor ?? palette.Get(ModColorRole.BackgroundCardSelected, 218);
                    var cardX = screenX + 18f;
                    var cardWidth = screenWidth - 36f;
                    DrawRect(
                        resolution.Width,
                        resolution.Height,
                        cardX + 4f,
                        rowY + 6f,
                        cardWidth,
                        rowHeight - 4f,
                        palette.Get(ModColorRole.BackgroundOuter, 56));
                    DrawRect(
                        resolution.Width,
                        resolution.Height,
                        cardX,
                        rowY,
                        cardWidth,
                        rowHeight - 2f,
                        palette.Get(ModColorRole.BackgroundPanel, selected ? 210 : 170));
                    DrawRect(
                        resolution.Width,
                        resolution.Height,
                        cardX + 2f,
                        rowY + 2f,
                        cardWidth - 4f,
                        rowHeight - 6f,
                        selected ? activeColor : idleColor);
                    DrawRect(
                        resolution.Width,
                        resolution.Height,
                        cardX + 2f,
                        rowY + 2f,
                        6f,
                        rowHeight - 6f,
                        selected ? palette.Get(ModColorRole.Highlight, 236) : palette.Get(ModColorRole.TextMuted, 188));

                    var color = selected
                        ? palette.Get(ModColorRole.TextPrimary, 238)
                        : palette.Get(ModColorRole.TextSecondary, 220);
                    DrawMenuItemCaption(
                        resolution,
                        item,
                        cardX + 24f,
                        rowY + TabletCaptionOffsetY,
                        TabletCaptionScale,
                        color,
                        GTA.UI.Font.ChaletComprimeCologne,
                        Alignment.Left,
                        18f);

                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        var detailColor = selected
                            ? palette.Get(ModColorRole.TextPrimary, 225)
                            : palette.Get(ModColorRole.TextMuted, 205);
                        DrawTextBlock(
                            resolution,
                            detail,
                            cardX + 24f,
                            rowY + TabletDetailOffsetY,
                            TabletDetailScale,
                            detailColor,
                            GTA.UI.Font.ChaletLondon,
                            Alignment.Left,
                            16f);
                    }

                    DrawProgressBarIfNeeded(
                        resolution,
                        item,
                        cardX + 24f,
                        rowY + rowHeight - 12f,
                        cardWidth - 48f,
                        7f,
                        selected);

                    rowY += rowHeight;
                }

                if (bottomPanelHeight > 0f)
                {
                    var panelY = screenY + screenHeight - footerHeight - 22f - bottomPanelHeight;
                    DrawTabletBottomPanel(
                        resolution,
                        screenX + 18f,
                        panelY,
                        screenWidth - 36f,
                        bottomPanelHeight,
                        visibleItems);
                }
            }

            var footerText = customContentRenderer != null
                ? (FooterTextFactory != null ? FooterTextFactory() ?? string.Empty : string.Empty)
                : GetFooterText("Arrow Up/Down to navigate | Enter to select | Backspace/Esc to close", visibleItems.Count);
            if (string.IsNullOrWhiteSpace(footerText))
            {
                footerText = "Arrow Up/Down to navigate | Enter to select | Backspace/Esc to close";
            }
            DrawRect(
                resolution.Width,
                resolution.Height,
                screenX + 18f,
                screenY + screenHeight - footerHeight - 12f,
                screenWidth - 36f,
                footerHeight,
                Color.FromArgb(84, 9, 14, 24));
            DrawTextBlock(
                resolution,
                footerText,
                screenX + 24f,
                screenY + screenHeight - footerHeight - 6f,
                0.235f,
                Color.FromArgb(214, 195, 206, 218),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                14f);
        }

        private void DrawTabletDashboardTheme()
        {
            var resolution = GTA.UI.Screen.MainWindowResolution;
            var scale = Math.Max(0.26f, TabletWidthScale);
            var screenWidth = resolution.Width * 0.56f * scale;
            var screenHeight = resolution.Height * 0.58f;
            var bodyX = (resolution.Width - (screenWidth + 58f)) * 0.5f;
            var bodyY = resolution.Height * 0.08f;
            var screenX = bodyX + 29f;
            var screenY = bodyY + 34f;
            var bodyWidth = screenWidth + 58f;
            var bodyHeight = screenHeight + 82f;
            var sidebarCount = Math.Max(0, Math.Min(TabletDashboardSidebarCount, _items.Count));
            var tileColumns = Math.Max(1, TabletDashboardTileColumns);
            var gap = 14f;
            var sidebarWidth = Math.Max(148f, screenWidth * 0.22f);
            var rightAreaX = screenX + sidebarWidth + 18f;
            var rightAreaWidth = screenWidth - sidebarWidth - 30f;
            var tileSize = Math.Min(76f, (rightAreaWidth - ((tileColumns - 1) * gap)) / tileColumns);
            var headerY = screenY + 12f;
            var bottomPanelHeight = TabletBottomPanelRenderer != null && TabletBottomPanelHeight > 0f
                ? TabletBottomPanelHeight
                : 140f;

            DrawTabletDeviceFrame(resolution, bodyX, bodyY, bodyWidth, bodyHeight, screenX, screenY, screenWidth, screenHeight);
            DrawTabletWallpaper(resolution, screenX, screenY, screenWidth, screenHeight);
            DrawRect(resolution.Width, resolution.Height, screenX, screenY, screenWidth, 38f, Color.FromArgb(62, 5, 8, 18));

            DrawTextBlock(
                resolution,
                Title,
                screenX + 16f,
                headerY,
                0.27f,
                Color.FromArgb(228, 238, 244, 250),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                14f);
            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                DrawTextBlock(
                    resolution,
                    Subtitle,
                    screenX + 16f,
                    headerY + 16f,
                    0.185f,
                    Color.FromArgb(198, 197, 208, 221),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    12f);
            }

            var headerRightText = GetHeaderRightText();
            if (!string.IsNullOrWhiteSpace(headerRightText))
            {
                DrawTextBlock(
                    resolution,
                    headerRightText,
                    screenX + screenWidth - 18f,
                    headerY + 2f,
                    0.23f,
                    Color.FromArgb(222, 209, 223, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Right,
                    12f);
            }

            var widgetTopY = screenY + 52f;
            var widgetAvailableHeight = screenHeight - 118f;
            var primaryWidgetHeight = Math.Min(132f, Math.Max(116f, widgetAvailableHeight * 0.28f));
            var secondaryWidgetHeight = Math.Max(130f, widgetAvailableHeight - primaryWidgetHeight - gap);

            for (int i = 0; i < sidebarCount; i++)
            {
                var item = _items[i];
                var widgetHeight = i == 0 ? primaryWidgetHeight : secondaryWidgetHeight;
                var widgetY = i == 0 ? widgetTopY : widgetTopY + primaryWidgetHeight + gap;
                DrawDashboardWidget(
                    resolution,
                    item,
                    screenX + 16f,
                    widgetY,
                    sidebarWidth - 8f,
                    widgetHeight,
                    i == SelectedIndex,
                    i == 0);
            }

            var tilesStartIndex = sidebarCount;
            var tileCount = Math.Max(0, _items.Count - sidebarCount);
            var tileStartY = screenY + 36f;
            for (int i = 0; i < tileCount; i++)
            {
                var itemIndex = tilesStartIndex + i;
                var column = i % tileColumns;
                var row = i / tileColumns;
                var tileX = rightAreaX + (column * (tileSize + gap));
                var tileY = tileStartY + (row * (tileSize + 32f));
                DrawDashboardTile(
                    resolution,
                    _items[itemIndex],
                    tileX,
                    tileY,
                    tileSize,
                    itemIndex == SelectedIndex);
            }

            if (_items.Count > 0)
            {
                var panelY = screenY + screenHeight - bottomPanelHeight - 36f;
                if (TabletBottomPanelRenderer != null && TabletBottomPanelHeight > 0f)
                {
                    DrawTabletBottomPanel(
                        resolution,
                        rightAreaX,
                        panelY,
                        rightAreaWidth,
                        bottomPanelHeight,
                        new List<MenuItem>(_items));
                }
                else
                {
                    var selectedItem = _items[Math.Max(0, Math.Min(SelectedIndex, _items.Count - 1))];
                    DrawDashboardDetailCard(
                        resolution,
                        selectedItem,
                        rightAreaX,
                        panelY,
                        rightAreaWidth,
                        bottomPanelHeight);
                }
            }

            DrawRect(
                resolution.Width,
                resolution.Height,
                screenX + 18f,
                screenY + screenHeight - 28f,
                screenWidth - 36f,
                20f,
                Color.FromArgb(38, 4, 7, 18));
            DrawTextBlock(
                resolution,
                GetFooterText("Arrow Keys Navigate | Enter Select | Backspace/Esc Close"),
                screenX + 24f,
                screenY + screenHeight - 26f,
                0.205f,
                Color.FromArgb(198, 187, 199, 214),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                12f);
        }

        private bool TryHandleDashboardNavigation(WinForms.Keys key, ControlBindings controls)
        {
            if (_items.Count == 0 || controls == null)
            {
                return false;
            }

            var sidebarCount = Math.Max(0, Math.Min(TabletDashboardSidebarCount, _items.Count));
            var tileCount = Math.Max(0, _items.Count - sidebarCount);
            var tileColumns = Math.Max(1, TabletDashboardTileColumns);

            if (SelectedIndex < sidebarCount)
            {
                if (key == controls.MenuDown)
                {
                    if (SelectedIndex + 1 < sidebarCount)
                    {
                        SelectedIndex += 1;
                    }
                    else if (tileCount > 0)
                    {
                        SelectedIndex = sidebarCount;
                    }
                    else
                    {
                        SelectedIndex = 0;
                    }

                    EnsureSelectionVisible();
                    return true;
                }

                if (key == controls.MenuUp)
                {
                    SelectedIndex = SelectedIndex <= 0 ? _items.Count - 1 : SelectedIndex - 1;
                    EnsureSelectionVisible();
                    return true;
                }

                if (key == controls.MenuRight && tileCount > 0)
                {
                    SelectedIndex = sidebarCount;
                    EnsureSelectionVisible();
                    return true;
                }

                if (key == controls.MenuLeft)
                {
                    SelectedIndex = _items.Count - 1;
                    EnsureSelectionVisible();
                    return true;
                }

                return false;
            }

            if (tileCount <= 0)
            {
                return false;
            }

            var relativeIndex = SelectedIndex - sidebarCount;
            if (key == controls.MenuRight)
            {
                relativeIndex = (relativeIndex + 1) % tileCount;
                SelectedIndex = sidebarCount + relativeIndex;
                EnsureSelectionVisible();
                return true;
            }

            if (key == controls.MenuLeft)
            {
                relativeIndex = (relativeIndex - 1 + tileCount) % tileCount;
                SelectedIndex = sidebarCount + relativeIndex;
                EnsureSelectionVisible();
                return true;
            }

            if (key == controls.MenuDown)
            {
                var nextIndex = relativeIndex + tileColumns;
                if (nextIndex < tileCount)
                {
                    SelectedIndex = sidebarCount + nextIndex;
                }
                else
                {
                    SelectedIndex = sidebarCount + (relativeIndex % tileColumns);
                    if (SelectedIndex >= _items.Count)
                    {
                        SelectedIndex = _items.Count - 1;
                    }
                }

                EnsureSelectionVisible();
                return true;
            }

            if (key == controls.MenuUp)
            {
                var nextIndex = relativeIndex - tileColumns;
                if (nextIndex >= 0)
                {
                    SelectedIndex = sidebarCount + nextIndex;
                }
                else if (sidebarCount > 0)
                {
                    SelectedIndex = Math.Min(sidebarCount - 1, relativeIndex);
                }
                else
                {
                    SelectedIndex = sidebarCount + Math.Max(0, tileCount - 1);
                }

                EnsureSelectionVisible();
                return true;
            }

            return false;
        }

        private string GetHeaderRightText()
        {
            return HeaderRightTextFactory != null
                ? HeaderRightTextFactory() ?? string.Empty
                : string.Empty;
        }

        private string GetFooterText(string defaultText, int visibleItemCount = -1)
        {
            var footerText = FooterTextFactory != null
                ? FooterTextFactory() ?? string.Empty
                : string.Empty;

            return BuildFooterText(string.IsNullOrWhiteSpace(footerText) ? defaultText : footerText, visibleItemCount);
        }

        private float ComputeContentHeight(float lineHeight, List<MenuItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return lineHeight;
            }

            float total = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                total += GetRowHeight(lineHeight, GetDetailText(items[i]), HasProgressBar(items[i]));
            }

            return total;
        }

        private List<MenuItem> GetVisibleItems(float lineHeight = 0f, float availableContentHeight = 0f)
        {
            if (_items.Count == 0)
            {
                return new List<MenuItem>();
            }

            if (Theme == SimpleMenuTheme.Tablet && TabletLayout == SimpleMenuTabletLayout.Dashboard)
            {
                _firstVisibleIndex = 0;
                return new List<MenuItem>(_items);
            }

            if (Theme == SimpleMenuTheme.Tablet && TabletLayout == SimpleMenuTabletLayout.List && lineHeight > 0f && availableContentHeight > 0f)
            {
                _firstVisibleIndex = Math.Max(0, Math.Min(_firstVisibleIndex, _items.Count - 1));
                var dynamicVisibleCount = GetVisibleItemCount(_firstVisibleIndex, lineHeight, availableContentHeight);
                return _items.GetRange(_firstVisibleIndex, Math.Min(Math.Max(1, dynamicVisibleCount), _items.Count - _firstVisibleIndex));
            }

            if (MaxVisibleItems <= 0 || _items.Count <= MaxVisibleItems)
            {
                _firstVisibleIndex = 0;
                return new List<MenuItem>(_items);
            }

            var visibleCount = Math.Max(1, MaxVisibleItems);
            var maxFirst = Math.Max(0, _items.Count - visibleCount);
            if (_firstVisibleIndex > maxFirst)
            {
                _firstVisibleIndex = maxFirst;
            }

            return _items.GetRange(_firstVisibleIndex, Math.Min(visibleCount, _items.Count - _firstVisibleIndex));
        }

        private void EnsureSelectionVisible()
        {
            if (Theme == SimpleMenuTheme.Tablet && TabletLayout == SimpleMenuTabletLayout.List)
            {
                if (_items.Count == 0)
                {
                    _firstVisibleIndex = 0;
                    return;
                }

                var resolution = GTA.UI.Screen.MainWindowResolution;
                var lineHeight = resolution.Height * 0.043f;
                var screenHeight = resolution.Height * 0.58f;
                var headerHeight = lineHeight * 1.95f;
                var footerHeight = lineHeight * 0.88f;
                var availableContentHeight = GetTabletListContentHeight(screenHeight, lineHeight, headerHeight, footerHeight);

                _firstVisibleIndex = Math.Max(0, Math.Min(_firstVisibleIndex, _items.Count - 1));
                if (SelectedIndex < _firstVisibleIndex)
                {
                    _firstVisibleIndex = SelectedIndex;
                }

                while (true)
                {
                    var dynamicVisibleCount = GetVisibleItemCount(_firstVisibleIndex, lineHeight, availableContentHeight);
                    if (dynamicVisibleCount <= 0)
                    {
                        _firstVisibleIndex = Math.Max(0, Math.Min(SelectedIndex, _items.Count - 1));
                        return;
                    }

                    var dynamicLastVisibleIndex = _firstVisibleIndex + dynamicVisibleCount - 1;
                    if (SelectedIndex <= dynamicLastVisibleIndex)
                    {
                        return;
                    }

                    _firstVisibleIndex += 1;
                }
            }

            if (MaxVisibleItems <= 0 || _items.Count <= MaxVisibleItems)
            {
                _firstVisibleIndex = 0;
                return;
            }

            var visibleCount = Math.Max(1, MaxVisibleItems);
            if (SelectedIndex < _firstVisibleIndex)
            {
                _firstVisibleIndex = SelectedIndex;
                return;
            }

            var lastVisibleIndex = _firstVisibleIndex + visibleCount - 1;
            if (SelectedIndex > lastVisibleIndex)
            {
                _firstVisibleIndex = SelectedIndex - visibleCount + 1;
            }
        }

        private string BuildFooterText(string baseText, int visibleItemCount = -1)
        {
            if (_items.Count == 0)
            {
                return baseText;
            }

            if (visibleItemCount <= 0)
            {
                visibleItemCount = MaxVisibleItems > 0 ? Math.Max(1, MaxVisibleItems) : _items.Count;
            }

            if (_firstVisibleIndex <= 0 && visibleItemCount >= _items.Count)
            {
                return baseText;
            }

            var start = _firstVisibleIndex + 1;
            var end = Math.Min(_items.Count, _firstVisibleIndex + Math.Max(1, visibleItemCount));
            return string.Format("{0} | {1}-{2}/{3}", baseText, start, end, _items.Count);
        }

        private float GetItemRowHeight(float baseLineHeight, MenuItem item)
        {
            var detail = GetDetailText(item);
            var hasProgressBar = HasProgressBar(item);
            var rowHeight = GetRowHeight(baseLineHeight, detail, hasProgressBar);
            if (TabletMinRowHeight > 0f)
            {
                var minimumRowHeight = hasProgressBar && TabletMinProgressRowHeight > 0f
                    ? TabletMinProgressRowHeight
                    : TabletMinRowHeight;
                rowHeight = Math.Max(rowHeight, minimumRowHeight);
            }

            return rowHeight;
        }

        private int GetVisibleItemCount(int startIndex, float lineHeight, float availableContentHeight)
        {
            if (_items.Count == 0 || startIndex < 0 || startIndex >= _items.Count)
            {
                return 0;
            }

            var maxVisibleCount = MaxVisibleItems > 0 ? MaxVisibleItems : _items.Count;
            var usedHeight = 0f;
            var visibleCount = 0;

            for (int i = startIndex; i < _items.Count && visibleCount < maxVisibleCount; i++)
            {
                var rowHeight = GetItemRowHeight(lineHeight, _items[i]);
                if (visibleCount > 0 && usedHeight + rowHeight > availableContentHeight)
                {
                    break;
                }

                usedHeight += rowHeight;
                visibleCount += 1;
            }

            return Math.Max(1, visibleCount);
        }

        private void DrawTabletBottomPanel(Size resolution, float x, float y, float width, float height, IReadOnlyList<MenuItem> visibleItems)
        {
            DrawRect(resolution.Width, resolution.Height, x + 4f, y + 6f, width, height, Color.FromArgb(46, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(146, 7, 10, 18));
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 2f, width - 4f, height - 4f, Color.FromArgb(108, 12, 18, 28));

            var renderer = TabletBottomPanelRenderer;
            if (renderer == null)
            {
                return;
            }

            renderer(new SimpleMenuTabletPanelContext
            {
                Resolution = resolution,
                X = x + 2f,
                Y = y + 2f,
                Width = width - 4f,
                Height = height - 4f,
                SelectedIndex = SelectedIndex,
                FirstVisibleIndex = _firstVisibleIndex,
                Items = _items,
                VisibleItems = visibleItems ?? Array.Empty<MenuItem>(),
            });
        }

        private static float GetTabletListContentHeight(float screenHeight, float lineHeight, float headerHeight, float footerHeight, float bottomPanelHeight = 0f)
        {
            var panelSpacing = bottomPanelHeight > 0f ? 18f : 0f;
            return Math.Max(lineHeight, screenHeight - headerHeight - footerHeight - 24f - bottomPanelHeight - panelSpacing);
        }

        private static float GetRowHeight(float baseLineHeight, string detail, bool hasProgressBar)
        {
            var detailLineCount = GetLineCount(detail);
            if (detailLineCount <= 0)
            {
                return hasProgressBar ? baseLineHeight * 1.55f : baseLineHeight * 1.12f;
            }

            var heightMultiplier = 1.20f + (detailLineCount * 0.58f);
            if (hasProgressBar)
            {
                heightMultiplier += 0.32f;
            }

            return baseLineHeight * heightMultiplier;
        }

        private static string GetDetailText(MenuItem item)
        {
            if (item == null || item.DetailFactory == null)
            {
                return string.Empty;
            }

            return item.DetailFactory() ?? string.Empty;
        }

        private static bool HasProgressBar(MenuItem item)
        {
            return item != null && item.ProgressRatioFactory != null;
        }

        private static float GetProgressRatio(MenuItem item)
        {
            if (item == null || item.ProgressRatioFactory == null)
            {
                return 0f;
            }

            var ratio = item.ProgressRatioFactory() ?? 0f;
            return Math.Max(0f, Math.Min(1f, ratio));
        }

        private static void DrawProgressBarIfNeeded(Size resolution, MenuItem item, float x, float y, float width, float height, bool selected)
        {
            if (!HasProgressBar(item))
            {
                return; 
            }

            var palette = AccessibilityTheme.Service.Palette;
            var ratio = GetProgressRatio(item);
            var fillColor = item.ProgressBarColor ?? (selected ? palette.Get(ModColorRole.ProgressSelected, 228) : palette.Get(ModColorRole.ProgressIdle, 218));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, palette.Get(ModColorRole.BackgroundPanel, 158));
            DrawRect(resolution.Width, resolution.Height, x + 1f, y + 1f, Math.Max(0f, (width - 2f) * ratio), Math.Max(1f, height - 2f), fillColor);
            DrawRect(resolution.Width, resolution.Height, x, y, width, 1f, palette.Get(ModColorRole.Highlight, 192));
        }

        private static void DrawTabletDeviceFrame(Size resolution, float bodyX, float bodyY, float bodyWidth, float bodyHeight, float screenX, float screenY, float screenWidth, float screenHeight)
        {
            DrawRect(resolution.Width, resolution.Height, bodyX + 10f, bodyY + 10f, bodyWidth, bodyHeight, Color.FromArgb(72, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, bodyX, bodyY, bodyWidth, bodyHeight, Color.FromArgb(228, 20, 22, 28));
            DrawRect(resolution.Width, resolution.Height, bodyX + 2f, bodyY + 2f, bodyWidth - 4f, bodyHeight - 4f, Color.FromArgb(242, 32, 34, 42));
            DrawRect(resolution.Width, resolution.Height, bodyX + 4f, bodyY + 4f, bodyWidth - 8f, bodyHeight - 8f, Color.FromArgb(224, 11, 13, 18));
            DrawRect(resolution.Width, resolution.Height, screenX, screenY, screenWidth, screenHeight, Color.FromArgb(255, 6, 10, 18));
            DrawRect(resolution.Width, resolution.Height, screenX, screenY, screenWidth, 20f, Color.FromArgb(44, 255, 255, 255));
            DrawRect(resolution.Width, resolution.Height, bodyX + (bodyWidth * 0.5f) - 3f, bodyY + 12f, 6f, 6f, Color.FromArgb(132, 42, 52, 70));
            DrawRect(resolution.Width, resolution.Height, bodyX + (bodyWidth * 0.5f), bodyY + bodyHeight - 18f, 44f, 4f, Color.FromArgb(162, 176, 182, 193));
            DrawRect(resolution.Width, resolution.Height, bodyX + bodyWidth - 10f, bodyY + 52f, 3f, 18f, Color.FromArgb(128, 124, 129, 138));
        }

        private static void DrawTabletWallpaper(Size resolution, float x, float y, float width, float height)
        {
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(255, 14, 16, 20));
            DrawRect(resolution.Width, resolution.Height, x, y + (height * 0.76f), width, height * 0.24f, Color.FromArgb(88, 38, 44, 52));
        }

        private static void DrawDashboardWidget(Size resolution, MenuItem item, float x, float y, float width, float height, bool selected, bool compact)
        {
            var palette = AccessibilityTheme.Service.Palette;
            var idleColor = item != null && item.IdleBackgroundColor.HasValue ? item.IdleBackgroundColor.Value : palette.Get(ModColorRole.BackgroundCard, 176);
            var activeColor = item != null && item.SelectedBackgroundColor.HasValue ? item.SelectedBackgroundColor.Value : palette.Get(ModColorRole.BackgroundCardSelected, 212);
            DrawRect(resolution.Width, resolution.Height, x + 4f, y + 6f, width, height, palette.Get(ModColorRole.BackgroundOuter, 52));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, palette.Get(ModColorRole.BackgroundOuter, 176));
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 2f, width - 4f, height - 4f, selected ? activeColor : idleColor);

            var eyebrow = GetIconLabelText(item);
            if (!string.IsNullOrWhiteSpace(eyebrow))
            {
                DrawTextBlock(
                    resolution,
                    eyebrow,
                    x + 14f,
                    y + 14f,
                    0.19f,
                    compact ? palette.Get(ModColorRole.AccentOrange, 236) : palette.Get(ModColorRole.TextSecondary, 216),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    12f,
                    width - 28f);
            }

            var caption = item != null && item.CaptionFactory != null ? item.CaptionFactory() : string.Empty;
            DrawTextBlock(
                resolution,
                caption,
                x + 14f,
                y + (compact ? 34f : 40f),
                compact ? 0.56f : 0.33f,
                palette.Get(ModColorRole.TextPrimary, 238),
                compact ? GTA.UI.Font.ChaletComprimeCologne : GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                compact ? 18f : 16f,
                width - 28f);

            var detail = GetDetailText(item);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                DrawTextBlock(
                    resolution,
                    detail,
                    x + 14f,
                    y + (compact ? 78f : 70f),
                    compact ? 0.20f : 0.21f,
                    palette.Get(ModColorRole.TextSecondary, 212),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    14f,
                    width - 28f);
            }
        }

        private static void DrawDashboardTile(Size resolution, MenuItem item, float x, float y, float size, bool selected)
        {
            var palette = AccessibilityTheme.Service.Palette;
            var idleColor = item != null && item.IdleBackgroundColor.HasValue ? item.IdleBackgroundColor.Value : palette.Get(ModColorRole.BackgroundCard, 172);
            var activeColor = item != null && item.SelectedBackgroundColor.HasValue ? item.SelectedBackgroundColor.Value : palette.Get(ModColorRole.BackgroundCardSelected, 228);
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 4f, size, size, palette.Get(ModColorRole.BackgroundOuter, 58));
            DrawRect(resolution.Width, resolution.Height, x, y, size, size, palette.Get(ModColorRole.BackgroundPanel, 150));
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 2f, size - 4f, size - 4f, selected ? activeColor : idleColor);

            var iconText = GetIconLabelText(item);
            DrawTextBlock(
                resolution,
                iconText,
                x + (size * 0.5f),
                y + (size * 0.28f),
                0.36f,
                palette.Get(ModColorRole.TextPrimary, 244),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Center,
                14f,
                size - 10f);

            var caption = item != null && item.CaptionFactory != null ? item.CaptionFactory() : string.Empty;
            DrawTextBlock(
                resolution,
                caption,
                x + (size * 0.5f),
                y + size + 8f,
                0.18f,
                palette.Get(ModColorRole.TextPrimary, 224),
                GTA.UI.Font.ChaletLondon,
                Alignment.Center,
                12f,
                size - 8f);
        }

        private static void DrawDashboardDetailCard(Size resolution, MenuItem item, float x, float y, float width, float height)
        {
            var palette = AccessibilityTheme.Service.Palette;
            DrawRect(resolution.Width, resolution.Height, x + 4f, y + 6f, width, height, palette.Get(ModColorRole.BackgroundOuter, 46));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, palette.Get(ModColorRole.BackgroundOuter, 146));
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 2f, width - 4f, height - 4f, palette.Get(ModColorRole.BackgroundPanel, 108));

            var caption = item != null && item.CaptionFactory != null ? item.CaptionFactory() : string.Empty;
            var detail = GetDetailText(item);
            DrawTextBlock(
                resolution,
                caption,
                x + 18f,
                y + 18f,
                0.34f,
                palette.Get(ModColorRole.TextPrimary, 236),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                16f,
                width - 36f);
            DrawTextBlock(
                resolution,
                string.IsNullOrWhiteSpace(detail) ? "Select an app to inspect its current context." : detail,
                x + 18f,
                y + 48f,
                0.215f,
                palette.Get(ModColorRole.TextSecondary, 210),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                14f,
                width - 36f);
        }

        private static void DrawTextBlock(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float lineSpacing, float maxWidth = float.NaN)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var lines = text.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                DrawHudTextLine(
                    resolution,
                    lines[i],
                    x,
                    y + (i * lineSpacing),
                    scale,
                    color,
                    font,
                    alignment,
                    maxWidth);
            }
        }

        private static void DrawMenuItemCaption(Size resolution, MenuItem item, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float lineSpacing, float maxWidth = float.NaN)
        {
            var caption = item != null && item.CaptionFactory != null ? item.CaptionFactory() : string.Empty;
            var accent = item != null && item.CaptionAccentFactory != null ? item.CaptionAccentFactory() : string.Empty;
            var suffix = item != null && item.CaptionSuffixFactory != null ? item.CaptionSuffixFactory() : string.Empty;

            if (string.IsNullOrWhiteSpace(accent)
                || alignment != Alignment.Left
                || (!float.IsNaN(maxWidth) && maxWidth > 0f)
                || ContainsLineBreak(caption)
                || ContainsLineBreak(accent)
                || ContainsLineBreak(suffix))
            {
                DrawTextBlock(
                    resolution,
                    string.Concat(caption ?? string.Empty, accent ?? string.Empty, suffix ?? string.Empty),
                    x,
                    y,
                    scale,
                    color,
                    font,
                    alignment,
                    lineSpacing,
                    maxWidth);
                return;
            }

            var accentColor = item != null && item.CaptionAccentColorFactory != null
                ? item.CaptionAccentColorFactory()
                : null;
            var currentX = x;

            if (!string.IsNullOrEmpty(caption))
            {
                DrawHudTextLine(
                    resolution,
                    caption,
                    currentX,
                    y,
                    scale,
                    color,
                    font,
                    alignment,
                    maxWidth);
                currentX += MeasureHudTextWidthPixels(resolution, caption, scale, font);
            }

            DrawHudTextLine(
                resolution,
                accent,
                currentX,
                y,
                scale,
                accentColor ?? color,
                font,
                alignment,
                maxWidth);
            currentX += MeasureHudTextWidthPixels(resolution, accent, scale, font);

            if (!string.IsNullOrEmpty(suffix))
            {
                DrawHudTextLine(
                    resolution,
                    suffix,
                    currentX,
                    y,
                    scale,
                    color,
                    font,
                    alignment,
                    maxWidth);
            }
        }

        private static void DrawHudTextLine(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float maxWidth = float.NaN)
        {
            var coords = ToScriptTextCoords(resolution, x, y);
            var normalizedX = coords.X / 1280f;
            var normalizedY = coords.Y / 720f;
            var wrapStart = 0f;
            var wrapEnd = alignment == Alignment.Right ? normalizedX : 1f;
            if (!float.IsNaN(maxWidth) && maxWidth > 0f)
            {
                if (alignment == Alignment.Right)
                {
                    wrapStart = Math.Max(0f, ToScriptTextCoords(resolution, x - maxWidth, y).X / 1280f);
                }
                else if (alignment == Alignment.Center)
                {
                    wrapStart = Math.Max(0f, ToScriptTextCoords(resolution, x - (maxWidth * 0.5f), y).X / 1280f);
                    wrapEnd = Math.Min(1f, ToScriptTextCoords(resolution, x + (maxWidth * 0.5f), y).X / 1280f);
                }
                else
                {
                    wrapEnd = Math.Min(1f, ToScriptTextCoords(resolution, x + maxWidth, y).X / 1280f);
                }
            }

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_CENTRE, alignment == Alignment.Center);
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, alignment == Alignment.Right);
            Function.Call(Hash.SET_TEXT_WRAP, wrapStart, wrapEnd);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, normalizedX, normalizedY, 0);
        }

        private static float MeasureHudTextWidthPixels(Size resolution, string text, float scale, GTA.UI.Font font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            var normalizedWidth = Function.Call<float>(Hash.END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, true);
            return Math.Max(0f, normalizedWidth * resolution.Width);
        }

        private static bool ContainsLineBreak(string text)
        {
            return !string.IsNullOrEmpty(text)
                && (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0);
        }

        private static int GetLineCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return text.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.None).Length;
        }

        private static string GetIconLabelText(MenuItem item)
        {
            if (item != null && item.IconLabelFactory != null)
            {
                var explicitLabel = item.IconLabelFactory();
                if (!string.IsNullOrWhiteSpace(explicitLabel))
                {
                    return explicitLabel.Trim();
                }
            }

            var caption = item != null && item.CaptionFactory != null ? item.CaptionFactory() : string.Empty;
            return BuildMonogram(caption);
        }

        private static string BuildMonogram(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "APP";
            }

            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
            }

            var trimmed = parts.Length == 1 ? parts[0] : text.Trim();
            return trimmed.Length <= 3
                ? trimmed.ToUpperInvariant()
                : trimmed.Substring(0, 3).ToUpperInvariant();
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
    }
}

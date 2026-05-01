using System;
using System.Collections.Generic;
using System.Drawing;
using IndustryLogisticV.Config;
using GTA.Native;
using GTA.UI;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.UI
{
    public enum SimpleMenuTheme
    {
        Classic = 0,
        Tablet = 1,
    }

    public sealed class MenuItem
    {
        public Func<string> CaptionFactory { get; set; }
        public Func<string> DetailFactory { get; set; }
        public Func<float?> ProgressRatioFactory { get; set; }
        public Action OnActivate { get; set; }
        public Action OnLeft { get; set; }
        public Action OnRight { get; set; }
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
            TabletWidthScale = 1f;
            TabletAlignRight = false;
            TabletCaptionScale = 0.305f;
            TabletDetailScale = 0.235f;
            TabletCaptionOffsetY = 8f;
            TabletDetailOffsetY = 31f;
            TabletMinRowHeight = 0f;
            TabletMinProgressRowHeight = 0f;
            MaxVisibleItems = 0;
        }

        public string Title { get; set; }
        public string Subtitle { get; set; }
        public SimpleMenuTheme Theme { get; set; }
        public float TabletWidthScale { get; set; }
        public bool TabletAlignRight { get; set; }
        public float TabletCaptionScale { get; set; }
        public float TabletDetailScale { get; set; }
        public float TabletCaptionOffsetY { get; set; }
        public float TabletDetailOffsetY { get; set; }
        public float TabletMinRowHeight { get; set; }
        public float TabletMinProgressRowHeight { get; set; }
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
                selected.OnActivate?.Invoke();
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

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, Color.FromArgb(95, 8, 10, 16));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(206, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 5f, Color.FromArgb(238, 227, 170, 58));
            DrawRect(resolution.Width, resolution.Height, x, y + 5f, width, headerHeight - 5f, Color.FromArgb(194, 17, 24, 34));

            new TextElement(
                    Title,
                    ToScriptTextCoords(resolution, x + 12f, y + 8f),
                    0.40f,
                    Color.White,
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                new TextElement(
                        Subtitle,
                        ToScriptTextCoords(resolution, x + 12f, y + 30f),
                        0.27f,
                        Color.FromArgb(236, 224, 232, 238),
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();
            }

            var rowY = y + headerHeight;
            for (int i = 0; i < visibleItems.Count; i++)
            {
                var itemIndex = _firstVisibleIndex + i;
                var item = visibleItems[i];
                var detail = GetDetailText(item);
                var hasProgressBar = HasProgressBar(item);
                var rowHeight = GetRowHeight(lineHeight, detail, hasProgressBar);
                var idleRowColor = item.IdleBackgroundColor ?? (itemIndex % 2 == 0 ? Color.FromArgb(34, 255, 255, 255) : Color.Empty);
                if (idleRowColor != Color.Empty)
                {
                    DrawRect(resolution.Width, resolution.Height, x, rowY, width, rowHeight, idleRowColor);
                }

                if (itemIndex == SelectedIndex)
                {
                    var selectedRowColor = item.SelectedBackgroundColor ?? Color.FromArgb(220, 212, 164, 72);
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, width - 4f, rowHeight - 4f, selectedRowColor);
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, 5f, rowHeight - 4f, Color.FromArgb(240, 252, 246, 220));
                }

                var captionFactory = item.CaptionFactory;
                var caption = captionFactory != null ? captionFactory() : string.Empty;
                var color = itemIndex == SelectedIndex ? Color.White : Color.FromArgb(235, 220, 230, 240);
                new TextElement(
                        caption,
                        ToScriptTextCoords(resolution, x + 11f, rowY + 6f),
                        0.30f,
                        color,
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    var detailColor = itemIndex == SelectedIndex
                        ? Color.FromArgb(232, 244, 248, 252)
                        : Color.FromArgb(214, 205, 216, 228);
                    new TextElement(
                            detail,
                            ToScriptTextCoords(resolution, x + 11f, rowY + 28f),
                            0.225f,
                            detailColor,
                            GTA.UI.Font.ChaletLondon,
                            Alignment.Left,
                            true,
                            false)
                        .Draw();
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
            DrawRect(resolution.Width, resolution.Height, x, footerY, width, footerHeight, Color.FromArgb(185, 14, 20, 28));
            var footerText = BuildFooterText("Navigate | Edit | Select | Close");
            new TextElement(
                    footerText,
                    ToScriptTextCoords(resolution, x + 12f, footerY + 4f),
                    0.255f,
                    Color.FromArgb(228, 214, 223, 233),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();
        }

        private void DrawTabletTheme()
        {
            var resolution = GTA.UI.Screen.MainWindowResolution;
            var y = resolution.Height * 0.14f;
            var scale = Math.Max(0.25f, TabletWidthScale);
            var width = resolution.Width * 0.60f * scale;
            var sideMargin = resolution.Width * 0.035f;
            var x = TabletAlignRight
                ? resolution.Width - width - sideMargin
                : (resolution.Width - width) * 0.5f;
            var lineHeight = resolution.Height * 0.043f;
            var visibleItems = GetVisibleItems();
            var contentHeight = ComputeContentHeight(lineHeight, visibleItems);
            var headerHeight = lineHeight * 1.62f;
            var footerHeight = lineHeight * 0.82f;
            var height = contentHeight + headerHeight + footerHeight + 12f;

            DrawRect(resolution.Width, resolution.Height, x + 7f, y + 7f, width, height, Color.FromArgb(98, 8, 10, 16));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(204, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y + 4f, width, headerHeight - 4f, Color.FromArgb(156, 20, 30, 40));

            new TextElement(
                    Title,
                    ToScriptTextCoords(resolution, x + 14f, y + 9f),
                    0.41f,
                    Color.FromArgb(236, 242, 246, 252),
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                new TextElement(
                        Subtitle,
                        ToScriptTextCoords(resolution, x + 14f, y + 33f),
                        0.275f,
                        Color.FromArgb(222, 214, 225, 236),
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();
            }

            var rowY = y + headerHeight;
            for (int i = 0; i < visibleItems.Count; i++)
            {
                var itemIndex = _firstVisibleIndex + i;
                var item = visibleItems[i];
                var detail = GetDetailText(item);
                var hasProgressBar = HasProgressBar(item);
                var rowHeight = GetRowHeight(lineHeight, detail, hasProgressBar);
                if (TabletMinRowHeight > 0f)
                {
                    var minimumRowHeight = hasProgressBar && TabletMinProgressRowHeight > 0f
                        ? TabletMinProgressRowHeight
                        : TabletMinRowHeight;
                    rowHeight = Math.Max(rowHeight, minimumRowHeight);
                }

                var selected = itemIndex == SelectedIndex;
                var idleColor = item.IdleBackgroundColor ?? Color.FromArgb(160, 46, 60, 76);
                var activeColor = item.SelectedBackgroundColor ?? Color.FromArgb(210, 92, 126, 158);
                DrawRect(
                    resolution.Width,
                    resolution.Height,
                    x + 12f,
                    rowY + 3f,
                    width - 24f,
                    rowHeight - 6f,
                    selected ? activeColor : idleColor);

                var captionFactory = item.CaptionFactory;
                var caption = captionFactory != null ? captionFactory() : string.Empty;
                var color = selected
                    ? Color.FromArgb(238, 245, 249, 255)
                    : Color.FromArgb(220, 222, 231, 240);
                new TextElement(
                        caption,
                    ToScriptTextCoords(resolution, x + 28f, rowY + TabletCaptionOffsetY),
                    TabletCaptionScale,
                        color,
                        GTA.UI.Font.ChaletComprimeCologne,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    var detailColor = selected
                        ? Color.FromArgb(225, 241, 247, 252)
                        : Color.FromArgb(205, 204, 216, 228);
                    new TextElement(
                            detail,
                            ToScriptTextCoords(resolution, x + 28f, rowY + TabletDetailOffsetY),
                            TabletDetailScale,
                            detailColor,
                            GTA.UI.Font.ChaletLondon,
                            Alignment.Left,
                            true,
                            false)
                        .Draw();
                }

                DrawProgressBarIfNeeded(
                    resolution,
                    item,
                    x + 28f,
                    rowY + rowHeight - 12f,
                    width - 56f,
                    7f,
                    selected);

                rowY += rowHeight;
            }

            var footerY = y + headerHeight + contentHeight;
            DrawRect(resolution.Width, resolution.Height, x, footerY, width, footerHeight, Color.FromArgb(148, 16, 24, 34));
            var footerText = BuildFooterText("Arrow Up/Down to navigate | Enter to select | Backspace/Esc to close");
            new TextElement(
                    footerText,
                    ToScriptTextCoords(resolution, x + 14f, footerY + 5f),
                    0.245f,
                    Color.FromArgb(214, 195, 206, 218),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();
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

        private List<MenuItem> GetVisibleItems()
        {
            if (_items.Count == 0)
            {
                return new List<MenuItem>();
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

        private string BuildFooterText(string baseText)
        {
            if (MaxVisibleItems <= 0 || _items.Count <= MaxVisibleItems)
            {
                return baseText;
            }

            var start = _firstVisibleIndex + 1;
            var end = Math.Min(_items.Count, _firstVisibleIndex + Math.Max(1, MaxVisibleItems));
            return string.Format("{0} | {1}-{2}/{3}", baseText, start, end, _items.Count);
        }

        private static float GetRowHeight(float baseLineHeight, string detail, bool hasProgressBar)
        {
            if (!string.IsNullOrWhiteSpace(detail) && hasProgressBar)
            {
                return baseLineHeight * 2.28f;
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return baseLineHeight * 1.88f;
            }

            if (hasProgressBar)
            {
                return baseLineHeight * 1.55f;
            }

            return baseLineHeight;
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

            var ratio = GetProgressRatio(item);
            var fillColor = item.ProgressBarColor ?? (selected ? Color.FromArgb(228, 244, 200, 96) : Color.FromArgb(218, 88, 156, 220));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(158, 11, 17, 24));
            DrawRect(resolution.Width, resolution.Height, x + 1f, y + 1f, Math.Max(0f, (width - 2f) * ratio), Math.Max(1f, height - 2f), fillColor);
            DrawRect(resolution.Width, resolution.Height, x, y, width, 1f, Color.FromArgb(192, 255, 255, 255));
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

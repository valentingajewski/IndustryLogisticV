using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.Native;
using GTA.UI;
using UiFont = GTA.UI.Font;

namespace LSOL.UI
{
    internal sealed class TabletBarEntry
    {
        public string Label { get; set; }

        public float Value { get; set; }

        public string ValueText { get; set; }

        public Color FillColor { get; set; }
    }

    internal sealed class TabletMetricBarEntry
    {
        public string Label { get; set; }

        public float Ratio { get; set; }

        public string ValueText { get; set; }

        public Color FillColor { get; set; }
    }

    internal static class TabletChartRenderer
    {
        public static void DrawMessagePanel(SimpleMenuTabletPanelContext panel, string title, string subtitle, string message)
        {
            if (panel == null)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            DrawPanelHeading(panel, title, subtitle, string.Empty);
            DrawTextBlock(
                panel.Resolution,
                message,
                panel.X + 14f,
                panel.Y + 58f,
                0.225f,
                palette.Get(ModColorRole.TextSecondary, 206),
                UiFont.ChaletLondon,
                Alignment.Left,
                14f);
        }

        public static void DrawHistoryPanel(
            SimpleMenuTabletPanelContext panel,
            string title,
            string subtitle,
            IReadOnlyList<float> values,
            Color accent,
            Func<float, string> formatter = null)
        {
            if (panel == null)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            formatter = formatter ?? (value => value.ToString("0.0"));
            var latestText = values != null && values.Count > 0
                ? formatter(values[values.Count - 1])
                : string.Empty;
            DrawPanelHeading(panel, title, subtitle, latestText);

            if (values == null || values.Count < 2)
            {
                DrawTextBlock(
                    panel.Resolution,
                    "History builds as gameplay runs. Keep the save active for a few samples.",
                    panel.X + 14f,
                    panel.Y + 58f,
                    0.21f,
                    palette.Get(ModColorRole.TextSecondary, 198),
                    UiFont.ChaletLondon,
                    Alignment.Left,
                    14f);
                return;
            }

            var chartX = panel.X + 14f;
            var chartY = panel.Y + 54f;
            var chartWidth = Math.Max(32f, panel.Width - 28f);
            var chartHeight = Math.Max(28f, panel.Height - 66f);

            var minimum = values[0];
            var maximum = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                minimum = Math.Min(minimum, values[i]);
                maximum = Math.Max(maximum, values[i]);
            }

            if (Math.Abs(maximum - minimum) < 0.001f)
            {
                maximum += 1f;
                minimum = Math.Max(0f, minimum - 1f);
            }

            for (int i = 0; i < 4; i++)
            {
                var rowY = chartY + ((chartHeight / 3f) * i);
                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    chartX,
                    rowY,
                    chartWidth,
                    1f,
                        palette.Get(ModColorRole.ChartGrid, 34));
            }

            DrawTextBlock(
                panel.Resolution,
                formatter(maximum),
                chartX + chartWidth,
                chartY - 8f,
                0.17f,
                palette.Get(ModColorRole.TextMuted, 154),
                UiFont.ChaletLondon,
                Alignment.Right,
                10f);
            DrawTextBlock(
                panel.Resolution,
                formatter(minimum),
                chartX + chartWidth,
                chartY + chartHeight - 8f,
                0.17f,
                palette.Get(ModColorRole.TextMuted, 154),
                UiFont.ChaletLondon,
                Alignment.Right,
                10f);

            var pointCount = values.Count;
            var lastPointX = chartX;
            var lastPointY = chartY + chartHeight;
            for (int i = 0; i < pointCount; i++)
            {
                var ratio = pointCount == 1 ? 0f : (float)i / (pointCount - 1);
                var normalized = (values[i] - minimum) / Math.Max(0.001f, maximum - minimum);
                var pointX = chartX + (chartWidth * ratio);
                var pointY = chartY + chartHeight - (chartHeight * normalized);

                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    pointX - 1f,
                    pointY,
                    2f,
                    chartY + chartHeight - pointY,
                    Color.FromArgb(24, accent.R, accent.G, accent.B));

                if (i > 0)
                {
                    DrawPointLine(panel.Resolution, lastPointX, lastPointY, pointX, pointY, accent);
                }

                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    pointX - 2f,
                    pointY - 2f,
                    4f,
                    4f,
                    Color.FromArgb(226, accent.R, accent.G, accent.B));

                lastPointX = pointX;
                lastPointY = pointY;
            }
        }

        public static void DrawComparisonBarsPanel(
            SimpleMenuTabletPanelContext panel,
            string title,
            string subtitle,
            IReadOnlyList<TabletBarEntry> entries,
            int highlightIndex = -1)
        {
            if (panel == null)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            DrawPanelHeading(panel, title, subtitle, string.Empty);
            if (entries == null || entries.Count == 0)
            {
                DrawTextBlock(
                    panel.Resolution,
                    "No comparison data is currently available.",
                    panel.X + 14f,
                    panel.Y + 58f,
                    0.21f,
                    palette.Get(ModColorRole.TextSecondary, 198),
                    UiFont.ChaletLondon,
                    Alignment.Left,
                    14f);
                return;
            }

            var count = Math.Min(entries.Count, 5);
            var maxValue = 0.001f;
            for (int i = 0; i < count; i++)
            {
                maxValue = Math.Max(maxValue, Math.Max(0f, entries[i].Value));
            }

            var rowHeight = Math.Max(18f, (panel.Height - 64f) / count);
            var labelWidth = Math.Min(132f, panel.Width * 0.34f);
            var valueWidth = 72f;
            var barWidth = Math.Max(26f, panel.Width - labelWidth - valueWidth - 40f);

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i] ?? new TabletBarEntry();
                var rowY = panel.Y + 50f + (rowHeight * i);
                var selected = i == highlightIndex;
                var fillColor = entry.FillColor == default(Color)
                    ? palette.Get(ModColorRole.AccentBlue, 208)
                    : entry.FillColor;
                var trackColor = selected
                    ? palette.Get(ModColorRole.Highlight, 74)
                    : palette.Get(ModColorRole.ChartFill, 42);
                var textColor = selected
                    ? palette.Get(ModColorRole.TextPrimary, 232)
                    : palette.Get(ModColorRole.TextSecondary, 204);
                var fillWidth = barWidth * (Math.Max(0f, entry.Value) / maxValue);

                DrawTextBlock(
                    panel.Resolution,
                    entry.Label ?? string.Empty,
                    panel.X + 14f,
                    rowY,
                    0.20f,
                    textColor,
                    UiFont.ChaletLondon,
                    Alignment.Left,
                    12f);
                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    panel.X + labelWidth,
                    rowY + 6f,
                    barWidth,
                    8f,
                    trackColor);
                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    panel.X + labelWidth,
                    rowY + 6f,
                    Math.Max(4f, fillWidth),
                    8f,
                    Color.FromArgb(selected ? 230 : 198, fillColor.R, fillColor.G, fillColor.B));
                DrawTextBlock(
                    panel.Resolution,
                    entry.ValueText ?? string.Empty,
                    panel.X + panel.Width - 12f,
                    rowY - 1f,
                    0.19f,
                    textColor,
                    UiFont.ChaletLondon,
                    Alignment.Right,
                    12f);
            }
        }

        public static void DrawMetricBarsPanel(
            SimpleMenuTabletPanelContext panel,
            string title,
            string subtitle,
            IReadOnlyList<TabletMetricBarEntry> entries)
        {
            if (panel == null)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            DrawPanelHeading(panel, title, subtitle, string.Empty);
            if (entries == null || entries.Count == 0)
            {
                DrawTextBlock(
                    panel.Resolution,
                    "No route metrics are available for the current selection.",
                    panel.X + 14f,
                    panel.Y + 58f,
                    0.21f,
                    palette.Get(ModColorRole.TextSecondary, 198),
                    UiFont.ChaletLondon,
                    Alignment.Left,
                    14f);
                return;
            }

            var rowHeight = Math.Max(22f, (panel.Height - 66f) / entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i] ?? new TabletMetricBarEntry();
                var ratio = Clamp01(entry.Ratio);
                var fillColor = entry.FillColor == default(Color)
                    ? palette.Get(ModColorRole.AccentBlue, 212)
                    : entry.FillColor;
                var rowY = panel.Y + 50f + (rowHeight * i);
                var barX = panel.X + 14f;
                var barY = rowY + 12f;
                var barWidth = panel.Width - 28f;

                DrawTextBlock(
                    panel.Resolution,
                    entry.Label ?? string.Empty,
                    barX,
                    rowY - 2f,
                    0.205f,
                    palette.Get(ModColorRole.TextPrimary, 224),
                    UiFont.ChaletLondon,
                    Alignment.Left,
                    12f);
                DrawTextBlock(
                    panel.Resolution,
                    entry.ValueText ?? string.Empty,
                    panel.X + panel.Width - 12f,
                    rowY - 2f,
                    0.195f,
                    palette.Get(ModColorRole.TextSecondary, 208),
                    UiFont.ChaletLondon,
                    Alignment.Right,
                    12f);
                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    barX,
                    barY,
                    barWidth,
                    9f,
                    palette.Get(ModColorRole.ChartFill, 40));
                DrawRect(
                    panel.Resolution.Width,
                    panel.Resolution.Height,
                    barX,
                    barY,
                    Math.Max(5f, barWidth * ratio),
                    9f,
                    Color.FromArgb(222, fillColor.R, fillColor.G, fillColor.B));
            }
        }

        private static void DrawPanelHeading(SimpleMenuTabletPanelContext panel, string title, string subtitle, string rightText)
        {
            var palette = AccessibilityTheme.Service.Palette;
            DrawTextBlock(
                panel.Resolution,
                title,
                panel.X + 14f,
                panel.Y + 10f,
                0.31f,
                palette.Get(ModColorRole.TextPrimary, 236),
                UiFont.ChaletComprimeCologne,
                Alignment.Left,
                16f);
            DrawTextBlock(
                panel.Resolution,
                subtitle,
                panel.X + 14f,
                panel.Y + 34f,
                0.19f,
                palette.Get(ModColorRole.TextSecondary, 196),
                UiFont.ChaletLondon,
                Alignment.Left,
                12f);
            if (!string.IsNullOrWhiteSpace(rightText))
            {
                DrawTextBlock(
                    panel.Resolution,
                    rightText,
                    panel.X + panel.Width - 12f,
                    panel.Y + 12f,
                    0.22f,
                    palette.Get(ModColorRole.TextPrimary, 222),
                    UiFont.ChaletLondon,
                    Alignment.Right,
                    12f);
            }
        }

        private static void DrawPointLine(Size resolution, float x0, float y0, float x1, float y1, Color color)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var steps = Math.Max(1, (int)(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 3f));
            for (int i = 0; i <= steps; i++)
            {
                var ratio = i / (float)steps;
                var pointX = x0 + (dx * ratio);
                var pointY = y0 + (dy * ratio);
                DrawRect(
                    resolution.Width,
                    resolution.Height,
                    pointX - 1f,
                    pointY - 1f,
                    2f,
                    2f,
                    Color.FromArgb(192, color.R, color.G, color.B));
            }
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static void DrawTextBlock(Size resolution, string text, float x, float y, float scale, Color color, UiFont font, Alignment alignment, float lineSpacing)
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
                    alignment);
            }
        }

        private static void DrawHudTextLine(Size resolution, string text, float x, float y, float scale, Color color, UiFont font, Alignment alignment)
        {
            var coords = ToScriptTextCoords(resolution, x, y);
            var normalizedX = coords.X / 1280f;
            var normalizedY = coords.Y / 720f;
            var wrapEnd = alignment == Alignment.Right ? normalizedX : 1f;

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_CENTRE, alignment == Alignment.Center);
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, alignment == Alignment.Right);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, wrapEnd);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, normalizedX, normalizedY, 0);
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
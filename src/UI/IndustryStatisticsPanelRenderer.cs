using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.Native;
using LSOL;
using GTA.UI;
using LSOL.Domain;
using LemonUI.Elements;

namespace LSOL.UI
{
    internal static class IndustryStatisticsPanelRenderer
    {
        private const float UiFallbackWidth = 1280f;
        private const float UiFallbackHeight = 720f;
        private const float MenuBackgroundWidth = 784f;
        private const float MenuBackgroundHeight = 536f;
        private const int VisibleRows = 6;

        public static void DrawStandalone(Industry industry, IndustryStatisticsSnapshot snapshot, int scrollIndex, string footerText)
        {
            if (industry == null)
            {
                return;
            }

            float frameX;
            float frameY;
            GetFramePosition(out frameX, out frameY);

            var menuBackground = new ScaledRectangle(new PointF(frameX, frameY), new SizeF(MenuBackgroundWidth, MenuBackgroundHeight))
            {
                Color = Color.FromArgb(230, 8, 12, 18),
            };
            menuBackground.Draw();

            DrawText(
                BuildMenuTitle(industry.Name),
                frameX + 78f,
                frameY + 62f,
                0.53f,
                Color.FromArgb(236, 242, 246, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                BuildOperationsSubtitle(industry),
                frameX + 80f,
                frameY + 88f,
                0.31f,
                Color.FromArgb(222, 214, 225, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawPageContent(industry, snapshot, scrollIndex, frameX, frameY, footerText);
        }

        public static void DrawPageContent(Industry industry, IndustryStatisticsSnapshot snapshot, int scrollIndex, float frameX, float frameY, string footerText)
        {
            if (industry == null)
            {
                return;
            }

            DrawText(
                BuildStatisticsHeading(industry),
                frameX + 80f,
                frameY + 116f,
                0.39f,
                Color.FromArgb(236, 234, 242, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                BuildCommodityBreakdownSubtitle(industry),
                frameX + 82f,
                frameY + 143f,
                0.30f,
                Color.FromArgb(224, 214, 226, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawIndustryStatistics(industry, snapshot, ClampScrollIndex(snapshot, scrollIndex), frameX, frameY);

            DrawText(
                string.IsNullOrWhiteSpace(footerText)
                    ? "Arrow Up/Down to scroll | Enter or Backspace or Esc to return"
                    : footerText,
                frameX + 80f,
                frameY + 515f,
                0.28f,
                Color.FromArgb(214, 195, 206, 218),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);
        }

        public static int MoveScrollIndex(IndustryStatisticsSnapshot snapshot, int currentScrollIndex, int delta)
        {
            return ClampScrollIndex(snapshot, currentScrollIndex + delta);
        }

        public static int ClampScrollIndex(IndustryStatisticsSnapshot snapshot, int requestedScrollIndex)
        {
            var entryCount = snapshot == null || snapshot.Entries == null ? 0 : snapshot.Entries.Count;
            var maxScroll = Math.Max(0, entryCount - VisibleRows);
            return Math.Max(0, Math.Min(maxScroll, requestedScrollIndex));
        }

        public static int GetScrollSlotCount(IndustryStatisticsSnapshot snapshot)
        {
            return ClampScrollIndex(snapshot, int.MaxValue) + 1;
        }

        public static string BuildMenuTitle(string industryName)
        {
            return string.Format("{0} MENU", BuildIndustryLabel(industryName));
        }

        public static string BuildOperationsSubtitle(Industry industry)
        {
            if (industry != null && industry.SiteRole == SiteRole.Warehouse)
            {
                return "Warehouse Operations Interface";
            }

            if (industry != null && industry.IsStore)
            {
                return "Store Operations Interface";
            }

            return "Industry Operations Interface";
        }

        public static string BuildStatisticsHeading(Industry industry)
        {
            if (industry != null && industry.SiteRole == SiteRole.Warehouse)
            {
                return "WAREHOUSE STATISTICS";
            }

            if (industry != null && industry.IsStore)
            {
                return "STORE STATISTICS";
            }

            return "INDUSTRY STATISTICS";
        }

        public static string BuildCommodityBreakdownSubtitle(Industry industry)
        {
            return industry != null && industry.SiteRole == SiteRole.Warehouse
                ? "Stored commodities and accepted capacity by commodity"
                : "Input and output stock by commodity";
        }

        public static void DrawTabletBody(Industry industry, IndustryStatisticsSnapshot snapshot, int scrollIndex, SimpleMenuTabletPanelContext panel)
        {
            if (industry == null || panel == null)
            {
                return;
            }

            var resolution = panel.Resolution;
            var entries = snapshot == null ? null : snapshot.Entries;
            var stockpile = snapshot == null ? 0f : snapshot.Stockpile;
            var totalCapacity = snapshot == null ? 1f : snapshot.TotalCapacity;
            var stockRatio = snapshot == null ? 0f : snapshot.StockRatio;
            var utilizationRatio = snapshot == null ? 0f : snapshot.UtilizationRatio;
            var clampedScrollIndex = ClampScrollIndex(snapshot, scrollIndex);
            var visibleCount = entries == null ? 0 : Math.Min(VisibleRows, entries.Count - clampedScrollIndex);
            var statsWidth = Math.Min(Math.Max(420f, panel.Width - 220f), 620f);
            var statsHeight = Math.Min(Math.Max(232f, 170f + (visibleCount * 36f)), Math.Max(232f, panel.Height - 18f));
            var statsX = panel.X + Math.Max(0f, (panel.Width - statsWidth) * 0.5f);
            var statsY = panel.Y + Math.Max(8f, (panel.Height - statsHeight) * 0.5f);
            var contentX = statsX + 18f;
            var contentY = statsY + 16f;
            var barWidth = Math.Min(290f, Math.Max(130f, statsWidth - 286f));
            var barX = statsX + statsWidth - barWidth - 18f;

            DrawPixelRect(resolution, statsX, statsY, statsWidth, statsHeight, Color.FromArgb(162, 8, 12, 18));

            DrawPixelText(
                resolution,
                BuildStatisticsHeading(industry),
                contentX,
                contentY,
                0.39f,
                Color.FromArgb(236, 234, 242, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawPixelText(
                resolution,
                BuildCommodityBreakdownSubtitle(industry),
                contentX + 2f,
                contentY + 24f,
                0.30f,
                Color.FromArgb(224, 214, 226, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawPixelText(
                resolution,
                string.Format("Total Stockpile: {0}", ModFormatting.FormatRatio(stockpile, totalCapacity, " t")),
                contentX,
                contentY + 52f,
                0.275f,
                Color.FromArgb(220, 214, 223, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawPixelLoadingBar(
                resolution,
                barX,
                contentY + 60f,
                barWidth,
                11f,
                stockRatio,
                Color.FromArgb(170, 28, 40, 54),
                Color.FromArgb(230, 214, 188, 96));

            DrawPixelText(
                resolution,
                BuildSummaryLine(industry, snapshot),
                contentX,
                contentY + 74f,
                0.25f,
                Color.FromArgb(214, 205, 217, 228),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (entries == null || entries.Count == 0)
            {
                DrawPixelText(
                    resolution,
                    industry != null && industry.SiteRole == SiteRole.Warehouse
                        ? "No accepted storage commodities are configured for this warehouse."
                        : "No input/output commodities configured for this industry.",
                    contentX,
                    contentY + 138f,
                    0.29f,
                    Color.FromArgb(224, 214, 226, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
                return;
            }

            DrawPixelText(
                resolution,
                industry != null && industry.SiteRole == SiteRole.Warehouse
                    ? "Stored warehouse commodities and their current fill ratios"
                    : "IN = input storage | OUT = output storage",
                contentX,
                contentY + 112f,
                0.24f,
                AccessibilityTheme.Service.Palette.Get(ModColorRole.TextMuted, 206),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (entries.Count > VisibleRows)
            {
                DrawPixelText(
                    resolution,
                    string.Format("{0}-{1}/{2}", clampedScrollIndex + 1, clampedScrollIndex + visibleCount, entries.Count),
                    statsX + statsWidth - 18f,
                    contentY + 112f,
                    0.24f,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.TextMuted, 206),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Right,
                    0f);
            }

            var listTopY = contentY + 126f;
            for (int i = 0; i < visibleCount; i++)
            {
                var entry = entries[clampedScrollIndex + i];
                var rowY = listTopY + (i * 35f);
                var titleColor = entry.IsInput
                    ? AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, 226)
                    : AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGold, 226);
                var fillColor = entry.IsInput
                    ? AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, 228)
                    : AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGold, 228);

                DrawPixelText(
                    resolution,
                    string.Format("{0} {1}", entry.IsInput ? "IN" : "OUT", entry.Commodity.ToUpperInvariant()),
                    contentX,
                    rowY,
                    0.27f,
                    titleColor,
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawPixelText(
                    resolution,
                    ModFormatting.FormatRatio(entry.Stock, entry.Capacity, " t"),
                    contentX,
                    rowY + 14f,
                    0.235f,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.TextSecondary, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawPixelLoadingBar(
                    resolution,
                    barX,
                    rowY + 13f,
                    barWidth,
                    10f,
                    entry.Ratio,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.BackgroundHeader, 170),
                    fillColor);
            }
        }

        private static string BuildSummaryLine(Industry industry, IndustryStatisticsSnapshot snapshot)
        {
            var utilizationRatio = snapshot == null ? 0f : snapshot.UtilizationRatio;
            if (industry != null && industry.SiteRole == SiteRole.Warehouse)
            {
                var commodityCount = snapshot != null && snapshot.Entries != null ? snapshot.Entries.Count : 0;
                return string.Format("Fill Ratio: {0} | Accepted Commodities: {1}", ModFormatting.FormatPercent(utilizationRatio * 100f), commodityCount);
            }

            return string.Format("Utilization: {0} | Output: {1}", ModFormatting.FormatPercent(utilizationRatio * 100f), ModFormatting.FormatRatePerHour(industry != null ? industry.CurrentOutputPerHourTons : 0f, "t"));
        }

        private static void DrawIndustryStatistics(Industry industry, IndustryStatisticsSnapshot snapshot, int scrollIndex, float frameX, float frameY)
        {
            var entries = snapshot == null ? null : snapshot.Entries;
            var stockpile = snapshot == null ? 0f : snapshot.Stockpile;
            var totalCapacity = snapshot == null ? 1f : snapshot.TotalCapacity;
            var stockRatio = snapshot == null ? 0f : snapshot.StockRatio;
            var utilizationRatio = snapshot == null ? 0f : snapshot.UtilizationRatio;

            DrawText(
                string.Format("Total Stockpile: {0}", ModFormatting.FormatRatio(stockpile, totalCapacity, " t")),
                frameX + 84f,
                frameY + 170f,
                0.275f,
                AccessibilityTheme.Service.Palette.Get(ModColorRole.TextSecondary, 220),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawLoadingBar(
                frameX + 336f,
                frameY + 178f,
                274f,
                11f,
                stockRatio,
                AccessibilityTheme.Service.Palette.Get(ModColorRole.BackgroundHeader, 170),
                AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGold, 230));

            DrawText(
                BuildSummaryLine(industry, snapshot),
                frameX + 84f,
                frameY + 192f,
                0.25f,
                AccessibilityTheme.Service.Palette.Get(ModColorRole.TextSecondary, 214),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (entries == null || entries.Count == 0)
            {
                DrawText(
                    industry != null && industry.SiteRole == SiteRole.Warehouse
                        ? "No accepted storage commodities are configured for this warehouse."
                        : "No input/output commodities configured for this industry.",
                    frameX + 84f,
                    frameY + 266f,
                    0.29f,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.TextSecondary, 224),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
                return;
            }

            var visibleCount = Math.Min(VisibleRows, entries.Count - scrollIndex);
            var listTopY = frameY + 246f;

            DrawText(
                industry != null && industry.SiteRole == SiteRole.Warehouse
                    ? "Stored warehouse commodities and their current fill ratios"
                    : "IN = input storage | OUT = output storage",
                frameX + 84f,
                frameY + 232f,
                0.24f,
                AccessibilityTheme.Service.Palette.Get(ModColorRole.TextMuted, 206),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            for (int i = 0; i < visibleCount; i++)
            {
                var entry = entries[scrollIndex + i];
                var rowY = listTopY + (i * 43f);
                var titleColor = entry.IsInput
                    ? AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, 226)
                    : AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGold, 226);
                var fillColor = entry.IsInput
                    ? AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentTeal, 228)
                    : AccessibilityTheme.Service.Palette.Get(ModColorRole.AccentGold, 228);

                DrawText(
                    string.Format("{0} {1}", entry.IsInput ? "IN" : "OUT", entry.Commodity.ToUpperInvariant()),
                    frameX + 84f,
                    rowY,
                    0.27f,
                    titleColor,
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    0f);

                DrawText(
                    ModFormatting.FormatRatio(entry.Stock, entry.Capacity, " t"),
                    frameX + 84f,
                    rowY + 14f,
                    0.235f,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.TextSecondary, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawLoadingBar(
                    frameX + 336f,
                    rowY + 14f,
                    274f,
                    11f,
                    entry.Ratio,
                    AccessibilityTheme.Service.Palette.Get(ModColorRole.BackgroundHeader, 170),
                    fillColor);
            }

            if (entries.Count > VisibleRows)
            {
                DrawText(
                    string.Format("{0}-{1}/{2}", scrollIndex + 1, scrollIndex + visibleCount, entries.Count),
                    frameX + 690f,
                    frameY + 232f,
                    0.24f,
                    Color.FromArgb(206, 193, 206, 219),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Right,
                    0f);
            }
        }

        private static void GetFramePosition(out float frameX, out float frameY)
        {
            const float buttonSpacing = 74f;
            const float buttonWidth = 640f;
            const float buttonHeight = 68f;

            var mainButtonsHeight = buttonHeight + (buttonSpacing * 3f);
            var buttonX = (UiFallbackWidth * 0.5f) - (buttonWidth * 0.5f);
            var buttonTopY = (UiFallbackHeight * 0.5f) - (mainButtonsHeight * 0.5f);

            frameX = buttonX - 72f;
            frameY = buttonTopY - 112f;
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

        private static void DrawPixelText(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float wrap)
        {
            var entry = new TextElement(
                text ?? string.Empty,
                ToScriptTextCoords(resolution, x, y),
                scale,
                color,
                font,
                alignment,
                true,
                false);

            entry.Draw();
        }

        private static void DrawPixelLoadingBar(Size resolution, float x, float y, float width, float height, float ratio, Color backgroundColor, Color fillColor)
        {
            DrawPixelRect(resolution, x, y, width, height, backgroundColor);

            var innerHeight = Math.Max(2f, height - 4f);
            var innerWidth = Math.Max(2f, (width - 4f) * ModMath.Clamp01(ratio));
            DrawPixelRect(resolution, x + 2f, y + 2f, innerWidth, innerHeight, fillColor);
        }

        private static PointF ToScriptTextCoords(Size resolution, float x, float y)
        {
            return new PointF(
                x * (UiFallbackWidth / Math.Max(1f, resolution.Width)),
                y * (UiFallbackHeight / Math.Max(1f, resolution.Height)));
        }

        private static void DrawPixelRect(Size resolution, float x, float y, float width, float height, Color color)
        {
            var centerX = (x + (width * 0.5f)) / Math.Max(1f, resolution.Width);
            var centerY = (y + (height * 0.5f)) / Math.Max(1f, resolution.Height);
            var normalizedW = width / Math.Max(1f, resolution.Width);
            var normalizedH = height / Math.Max(1f, resolution.Height);
            Function.Call(Hash.DRAW_RECT, centerX, centerY, normalizedW, normalizedH, color.R, color.G, color.B, color.A);
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

    }
}
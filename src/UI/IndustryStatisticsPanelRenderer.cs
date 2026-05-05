using System;
using System.Collections.Generic;
using System.Drawing;
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
                string.Format("{0} MENU", BuildIndustryLabel(industry.Name)),
                frameX + 78f,
                frameY + 62f,
                0.53f,
                Color.FromArgb(236, 242, 246, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                "Industry Operations Interface",
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
                "INDUSTRY STATISTICS",
                frameX + 80f,
                frameY + 116f,
                0.39f,
                Color.FromArgb(236, 234, 242, 252),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left,
                0f);

            DrawText(
                "Input and output stock by commodity",
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

        private static void DrawIndustryStatistics(Industry industry, IndustryStatisticsSnapshot snapshot, int scrollIndex, float frameX, float frameY)
        {
            var entries = snapshot == null ? null : snapshot.Entries;
            var stockpile = snapshot == null ? 0f : snapshot.Stockpile;
            var totalCapacity = snapshot == null ? 1f : snapshot.TotalCapacity;
            var stockRatio = snapshot == null ? 0f : snapshot.StockRatio;
            var utilizationRatio = snapshot == null ? 0f : snapshot.UtilizationRatio;

            DrawText(
                string.Format("Total Stockpile: {0:0.0}/{1:0.0} t", stockpile, totalCapacity),
                frameX + 84f,
                frameY + 170f,
                0.275f,
                Color.FromArgb(220, 214, 223, 236),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            DrawLoadingBar(
                frameX + 336f,
                frameY + 178f,
                274f,
                11f,
                stockRatio,
                Color.FromArgb(170, 28, 40, 54),
                Color.FromArgb(230, 214, 188, 96));

            DrawText(
                string.Format("Utilization: {0:0}% | Output: {1:0.0} t/h", utilizationRatio * 100f, industry.CurrentOutputPerHourTons),
                frameX + 84f,
                frameY + 192f,
                0.25f,
                Color.FromArgb(214, 205, 217, 228),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            if (entries == null || entries.Count == 0)
            {
                DrawText(
                    "No input/output commodities configured for this industry.",
                    frameX + 84f,
                    frameY + 266f,
                    0.29f,
                    Color.FromArgb(224, 214, 226, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);
                return;
            }

            var visibleCount = Math.Min(VisibleRows, entries.Count - scrollIndex);
            var listTopY = frameY + 246f;

            DrawText(
                "IN = input storage | OUT = output storage",
                frameX + 84f,
                frameY + 232f,
                0.24f,
                Color.FromArgb(206, 193, 206, 219),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                0f);

            for (int i = 0; i < visibleCount; i++)
            {
                var entry = entries[scrollIndex + i];
                var rowY = listTopY + (i * 43f);
                var titleColor = entry.IsInput
                    ? Color.FromArgb(226, 132, 206, 184)
                    : Color.FromArgb(226, 223, 196, 128);
                var fillColor = entry.IsInput
                    ? Color.FromArgb(228, 98, 170, 148)
                    : Color.FromArgb(228, 214, 188, 96);

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
                    string.Format("{0:0.0}/{1:0.0} t", entry.Stock, entry.Capacity),
                    frameX + 84f,
                    rowY + 14f,
                    0.235f,
                    Color.FromArgb(214, 205, 217, 228),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    0f);

                DrawLoadingBar(
                    frameX + 336f,
                    rowY + 14f,
                    274f,
                    11f,
                    entry.Ratio,
                    Color.FromArgb(170, 28, 40, 54),
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
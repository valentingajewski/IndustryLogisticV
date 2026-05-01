using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.UI;
using IndustryLogisticV.Domain;
using LemonUI.Elements;

namespace IndustryLogisticV.UI
{
    internal static class IndustryStatisticsPanelRenderer
    {
        private const float UiFallbackWidth = 1280f;
        private const float UiFallbackHeight = 720f;
        private const float MenuBackgroundWidth = 784f;
        private const float MenuBackgroundHeight = 536f;
        private const int VisibleRows = 6;

        public static void DrawStandalone(Industry industry, int scrollIndex, string footerText)
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

            DrawPageContent(industry, scrollIndex, frameX, frameY, footerText);
        }

        public static void DrawPageContent(Industry industry, int scrollIndex, float frameX, float frameY, string footerText)
        {
            if (industry == null)
            {
                return;
            }

            var stockpile = industry.GetInputStockTotal() + industry.GetOutputStockTotal();
            var totalCapacity = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
            var stockRatio = Clamp01(stockpile / totalCapacity);
            var utilizationRatio = Clamp01(industry.LastUtilizationPercent / 100f);

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

            DrawIndustryStatistics(industry, ClampScrollIndex(industry, scrollIndex), frameX, frameY, stockpile, totalCapacity, stockRatio, utilizationRatio);

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

        public static int MoveScrollIndex(Industry industry, int currentScrollIndex, int delta)
        {
            return ClampScrollIndex(industry, currentScrollIndex + delta);
        }

        public static int ClampScrollIndex(Industry industry, int requestedScrollIndex)
        {
            var maxScroll = Math.Max(0, BuildCommodityStatsEntries(industry).Count - VisibleRows);
            return Math.Max(0, Math.Min(maxScroll, requestedScrollIndex));
        }

        private static void DrawIndustryStatistics(Industry industry, int scrollIndex, float frameX, float frameY, float stockpile, float totalCapacity, float stockRatio, float utilizationRatio)
        {
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

            var entries = BuildCommodityStatsEntries(industry);
            if (entries.Count == 0)
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

        private static List<CommodityStatEntry> BuildCommodityStatsEntries(Industry industry)
        {
            var entries = new List<CommodityStatEntry>();
            if (industry == null)
            {
                return entries;
            }

            var inputs = industry.GetSortedInputs();
            for (int i = 0; i < inputs.Count; i++)
            {
                var commodity = inputs[i];
                var stock = GetCommodityStockForStats(industry, commodity, true);
                var capacity = GetCommodityCapacityForStats(industry, commodity, true);
                entries.Add(new CommodityStatEntry
                {
                    Commodity = commodity,
                    Stock = stock,
                    Capacity = capacity,
                    Ratio = Clamp01(stock / Math.Max(0.01f, capacity)),
                    IsInput = true,
                });
            }

            var outputs = industry.GetSortedOutputs();
            for (int i = 0; i < outputs.Count; i++)
            {
                var commodity = outputs[i];
                var stock = GetCommodityStockForStats(industry, commodity, false);
                var capacity = GetCommodityCapacityForStats(industry, commodity, false);
                entries.Add(new CommodityStatEntry
                {
                    Commodity = commodity,
                    Stock = stock,
                    Capacity = capacity,
                    Ratio = Clamp01(stock / Math.Max(0.01f, capacity)),
                    IsInput = false,
                });
            }

            return entries;
        }

        private static float GetCommodityCapacityForStats(Industry industry, string commodity, bool isInput)
        {
            if (industry == null)
            {
                return 0.01f;
            }

            if (IsOmegaInputStat(industry, commodity, isInput))
            {
                return Math.Max(0.01f, industry.OmegaCapacityTons);
            }

            var stock = GetCommodityStockForStats(industry, commodity, isInput);
            var freeSpace = Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            var capacity = stock + freeSpace;

            if (capacity <= 0.001f)
            {
                var bucketCount = isInput
                    ? Math.Max(1, industry.Inputs.Count)
                    : Math.Max(1, industry.Outputs.Count);
                var totalCapacity = isInput
                    ? Math.Max(1f, industry.InputCapacityTons)
                    : Math.Max(1f, industry.OutputCapacityTons);
                capacity = totalCapacity / bucketCount;
            }

            return Math.Max(0.01f, capacity);
        }

        private static float GetCommodityStockForStats(Industry industry, string commodity, bool isInput)
        {
            if (industry == null)
            {
                return 0f;
            }

            if (IsOmegaInputStat(industry, commodity, isInput))
            {
                return Math.Max(0f, industry.OmegaStorage);
            }

            return Math.Max(0f, industry.GetStock(commodity));
        }

        private static bool IsOmegaInputStat(Industry industry, string commodity, bool isInput)
        {
            return isInput
                && industry != null
                && industry.SupportsOmegaBoost
                && !string.IsNullOrWhiteSpace(commodity)
                && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase);
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
    }
}
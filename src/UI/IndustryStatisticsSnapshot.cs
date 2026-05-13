using System;
using System.Collections.Generic;
using GTA;
using LSOL.Domain;

namespace LSOL.UI
{
    internal sealed class IndustryStatisticsSnapshot
    {
        private readonly List<CommodityStatEntry> _entries;

        public IndustryStatisticsSnapshot()
        {
            _entries = new List<CommodityStatEntry>();
            TotalCapacity = 1f;
        }

        public IList<CommodityStatEntry> Entries
        {
            get { return _entries; }
        }

        public float Stockpile { get; private set; }

        public float TotalCapacity { get; private set; }

        public float StockRatio { get; private set; }

        public float UtilizationRatio { get; private set; }

        public void Update(Industry industry)
        {
            _entries.Clear();
            if (industry == null)
            {
                Stockpile = 0f;
                TotalCapacity = 1f;
                StockRatio = 0f;
                UtilizationRatio = 0f;
                return;
            }

            Stockpile = industry.GetInputStockTotal() + industry.GetOutputStockTotal();
            TotalCapacity = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
            StockRatio = ModMath.Clamp01(Stockpile / TotalCapacity);
            UtilizationRatio = industry.SiteRole == SiteRole.Warehouse
                ? StockRatio
                : ModMath.Clamp01(industry.LastUtilizationPercent / 100f);

            AppendEntries(industry, industry.SortedAcceptedInputs, true);
            AppendEntries(industry, industry.SortedOutputs, false);
        }

        private void AppendEntries(Industry industry, IReadOnlyList<string> commodities, bool isInput)
        {
            if (industry == null || commodities == null)
            {
                return;
            }

            for (int i = 0; i < commodities.Count; i++)
            {
                var commodity = commodities[i];
                var stock = GetCommodityStock(industry, commodity, isInput);
                var capacity = GetCommodityCapacity(industry, commodity, isInput, stock);
                _entries.Add(new CommodityStatEntry
                {
                    Commodity = commodity,
                    Stock = stock,
                    Capacity = capacity,
                    Ratio = ModMath.Clamp01(stock / Math.Max(0.01f, capacity)),
                    IsInput = isInput,
                });
            }
        }

        private static float GetCommodityCapacity(Industry industry, string commodity, bool isInput, float stock)
        {
            if (industry == null)
            {
                return 0.01f;
            }

            if (IsOmegaInputStat(industry, commodity, isInput))
            {
                return Math.Max(0.01f, industry.OmegaCapacityTons);
            }

            var freeSpace = Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            var capacity = stock + freeSpace;

            if (capacity <= 0.001f)
            {
                var bucketCount = isInput
                    ? Math.Max(1, industry.Inputs.Count + industry.OptionalInputs.Count)
                    : Math.Max(1, industry.Outputs.Count);
                var totalCapacity = isInput
                    ? Math.Max(1f, industry.InputCapacityTons)
                    : Math.Max(1f, industry.OutputCapacityTons);
                capacity = totalCapacity / bucketCount;
            }

            return Math.Max(0.01f, capacity);
        }

        private static float GetCommodityStock(Industry industry, string commodity, bool isInput)
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
    }

    internal sealed class IndustryStatisticsSnapshotCache
    {
        private readonly IndustryStatisticsSnapshot _snapshot;
        private readonly int _refreshIntervalMs;
        private Industry _cachedIndustry;
        private int _lastRefreshMs;

        public IndustryStatisticsSnapshotCache(int refreshIntervalMs = 250)
        {
            _snapshot = new IndustryStatisticsSnapshot();
            _refreshIntervalMs = Math.Max(0, refreshIntervalMs);
            _lastRefreshMs = int.MinValue;
        }

        public IndustryStatisticsSnapshot GetSnapshot(Industry industry)
        {
            if (industry == null)
            {
                _cachedIndustry = null;
                _snapshot.Update(null);
                return _snapshot;
            }

            var now = Game.GameTime;
            if (!ReferenceEquals(_cachedIndustry, industry) || now - _lastRefreshMs >= _refreshIntervalMs)
            {
                _cachedIndustry = industry;
                _lastRefreshMs = now;
                _snapshot.Update(industry);
            }

            return _snapshot;
        }

        public void Invalidate()
        {
            _cachedIndustry = null;
            _lastRefreshMs = int.MinValue;
        }
    }
}
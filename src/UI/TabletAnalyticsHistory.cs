using System;
using System.Collections.Generic;

namespace LSOL.UI
{
    public enum TabletGraphTimeframe
    {
        FiveMinutes = 0,
        TenMinutes = 1,
        ThirtyMinutes = 2,
        OneHour = 3,
        FourHours = 4,
        OneDay = 5,
    }

    public static class TabletGraphTimeframeExtensions
    {
        public static string ToDisplayLabel(this TabletGraphTimeframe timeframe)
        {
            switch (timeframe)
            {
                case TabletGraphTimeframe.FiveMinutes:
                    return "5 min";
                case TabletGraphTimeframe.TenMinutes:
                    return "10 min";
                case TabletGraphTimeframe.ThirtyMinutes:
                    return "30 min";
                case TabletGraphTimeframe.OneHour:
                    return "1 h";
                case TabletGraphTimeframe.FourHours:
                    return "4 h";
                case TabletGraphTimeframe.OneDay:
                    return "1 day";
                default:
                    return "30 min";
            }
        }
    }

    public sealed class TabletTimeframeHistoryPersistence
    {
        public TabletTimeframeHistoryPersistence()
        {
            Values = new List<float>();
        }

        public TabletGraphTimeframe Timeframe { get; set; }

        public List<float> Values { get; private set; }

        public int PendingSampleCount { get; set; }

        public float PendingSum { get; set; }

        public bool HasData
        {
            get
            {
                return Values.Count > 0 || PendingSampleCount > 0 || Math.Abs(PendingSum) > 0.001f;
            }
        }
    }

    public sealed class TabletTimeSeriesPersistence
    {
        public TabletTimeSeriesPersistence()
        {
            Timeframes = new List<TabletTimeframeHistoryPersistence>();
        }

        public List<TabletTimeframeHistoryPersistence> Timeframes { get; private set; }

        public bool HasData
        {
            get
            {
                for (int i = 0; i < Timeframes.Count; i++)
                {
                    if (Timeframes[i] != null && Timeframes[i].HasData)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public sealed class TabletNamedTimeSeriesPersistence
    {
        public string Key { get; set; }

        public TabletTimeSeriesPersistence Series { get; set; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Key)
                    && Series != null
                    && Series.HasData;
            }
        }
    }

    public sealed class TabletAnalyticsPersistenceSnapshot
    {
        public TabletAnalyticsPersistenceSnapshot()
        {
            SelectedGraphTimeframe = TabletGraphTimeframe.ThirtyMinutes;
            SelectedTrendCommodity = string.Empty;
            CommodityPriceHistories = new List<TabletNamedTimeSeriesPersistence>();
            SiteUtilizationHistories = new List<TabletNamedTimeSeriesPersistence>();
            SiteStorageHistories = new List<TabletNamedTimeSeriesPersistence>();
        }

        public TabletGraphTimeframe SelectedGraphTimeframe { get; set; }

        public string SelectedTrendCommodity { get; set; }

        public TabletTimeSeriesPersistence ProfitHistory { get; set; }

        public List<TabletNamedTimeSeriesPersistence> CommodityPriceHistories { get; private set; }

        public List<TabletNamedTimeSeriesPersistence> SiteUtilizationHistories { get; private set; }

        public List<TabletNamedTimeSeriesPersistence> SiteStorageHistories { get; private set; }

        public bool HasData
        {
            get
            {
                if (ProfitHistory != null && ProfitHistory.HasData)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(SelectedTrendCommodity))
                {
                    return true;
                }

                return HasSeriesData(CommodityPriceHistories)
                    || HasSeriesData(SiteUtilizationHistories)
                    || HasSeriesData(SiteStorageHistories);
            }
        }

        private static bool HasSeriesData(List<TabletNamedTimeSeriesPersistence> entries)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].HasData)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class TabletGraphTimeframeCatalog
    {
        public const int PointCapacity = 20;

        private static readonly TabletGraphTimeframe[] OrderedTimeframes =
        {
            TabletGraphTimeframe.FiveMinutes,
            TabletGraphTimeframe.TenMinutes,
            TabletGraphTimeframe.ThirtyMinutes,
            TabletGraphTimeframe.OneHour,
            TabletGraphTimeframe.FourHours,
            TabletGraphTimeframe.OneDay,
        };

        public static IReadOnlyList<TabletGraphTimeframe> All
        {
            get { return OrderedTimeframes; }
        }

        public static TabletGraphTimeframe GetDefault()
        {
            return TabletGraphTimeframe.ThirtyMinutes;
        }

        public static int GetSamplesPerPoint(TabletGraphTimeframe timeframe)
        {
            switch (timeframe)
            {
                case TabletGraphTimeframe.FiveMinutes:
                    return 1;
                case TabletGraphTimeframe.TenMinutes:
                    return 2;
                case TabletGraphTimeframe.ThirtyMinutes:
                    return 6;
                case TabletGraphTimeframe.OneHour:
                    return 12;
                case TabletGraphTimeframe.FourHours:
                    return 48;
                case TabletGraphTimeframe.OneDay:
                    return 288;
                default:
                    return 6;
            }
        }

        public static TabletGraphTimeframe Cycle(TabletGraphTimeframe current, int delta)
        {
            var currentIndex = 0;
            for (int i = 0; i < OrderedTimeframes.Length; i++)
            {
                if (OrderedTimeframes[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (delta == 0)
            {
                return OrderedTimeframes[currentIndex];
            }

            var nextIndex = currentIndex + delta;
            while (nextIndex < 0)
            {
                nextIndex += OrderedTimeframes.Length;
            }

            while (nextIndex >= OrderedTimeframes.Length)
            {
                nextIndex -= OrderedTimeframes.Length;
            }

            return OrderedTimeframes[nextIndex];
        }
    }

    internal sealed class TabletPersistentAnalyticsSeries
    {
        private readonly Dictionary<TabletGraphTimeframe, TabletTimeframeAccumulator> _states;

        public TabletPersistentAnalyticsSeries()
        {
            _states = new Dictionary<TabletGraphTimeframe, TabletTimeframeAccumulator>();
            for (int i = 0; i < TabletGraphTimeframeCatalog.All.Count; i++)
            {
                var timeframe = TabletGraphTimeframeCatalog.All[i];
                _states[timeframe] = new TabletTimeframeAccumulator(
                    TabletGraphTimeframeCatalog.GetSamplesPerPoint(timeframe),
                    TabletGraphTimeframeCatalog.PointCapacity);
            }
        }

        public void AddSample(float value)
        {
            foreach (var pair in _states)
            {
                pair.Value.AddSample(value);
            }
        }

        public IReadOnlyList<float> GetValues(TabletGraphTimeframe timeframe, bool includePending = true)
        {
            TabletTimeframeAccumulator state;
            return _states.TryGetValue(timeframe, out state)
                ? state.GetValues(includePending)
                : Array.Empty<float>();
        }

        public void Clear()
        {
            foreach (var pair in _states)
            {
                pair.Value.Clear();
            }
        }

        public void Restore(TabletTimeSeriesPersistence persistence)
        {
            Clear();
            if (persistence == null || persistence.Timeframes == null)
            {
                return;
            }

            for (int i = 0; i < persistence.Timeframes.Count; i++)
            {
                var timeframe = persistence.Timeframes[i];
                if (timeframe == null)
                {
                    continue;
                }

                TabletTimeframeAccumulator state;
                if (_states.TryGetValue(timeframe.Timeframe, out state))
                {
                    state.Restore(timeframe);
                }
            }
        }

        public TabletTimeSeriesPersistence CreatePersistence()
        {
            var persistence = new TabletTimeSeriesPersistence();
            for (int i = 0; i < TabletGraphTimeframeCatalog.All.Count; i++)
            {
                var timeframe = TabletGraphTimeframeCatalog.All[i];
                TabletTimeframeAccumulator state;
                if (_states.TryGetValue(timeframe, out state))
                {
                    persistence.Timeframes.Add(state.CreatePersistence(timeframe));
                }
            }

            return persistence;
        }
    }

    internal sealed class TabletTimeframeAccumulator
    {
        private readonly int _samplesPerPoint;
        private readonly int _capacity;
        private readonly List<float> _values;

        public TabletTimeframeAccumulator(int samplesPerPoint, int capacity)
        {
            _samplesPerPoint = Math.Max(1, samplesPerPoint);
            _capacity = Math.Max(2, capacity);
            _values = new List<float>(_capacity);
        }

        public int PendingSampleCount { get; private set; }

        public float PendingSum { get; private set; }

        public void AddSample(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
            }

            PendingSampleCount += 1;
            PendingSum += value;
            if (PendingSampleCount >= _samplesPerPoint)
            {
                AppendValue(PendingSum / PendingSampleCount);
                PendingSampleCount = 0;
                PendingSum = 0f;
            }
        }

        public IReadOnlyList<float> GetValues(bool includePending)
        {
            if (!includePending || PendingSampleCount <= 0)
            {
                return _values.Count == 0 ? Array.Empty<float>() : _values.ToArray();
            }

            var values = new List<float>(_values.Count + 1);
            values.AddRange(_values);
            values.Add(PendingSum / PendingSampleCount);
            return values.ToArray();
        }

        public void Clear()
        {
            _values.Clear();
            PendingSampleCount = 0;
            PendingSum = 0f;
        }

        public void Restore(TabletTimeframeHistoryPersistence persistence)
        {
            Clear();
            if (persistence == null)
            {
                return;
            }

            if (persistence.Values != null)
            {
                var startIndex = Math.Max(0, persistence.Values.Count - _capacity);
                for (int i = startIndex; i < persistence.Values.Count; i++)
                {
                    AppendValue(persistence.Values[i]);
                }
            }

            PendingSampleCount = Math.Max(0, persistence.PendingSampleCount);
            PendingSum = float.IsNaN(persistence.PendingSum) || float.IsInfinity(persistence.PendingSum)
                ? 0f
                : persistence.PendingSum;
            if (PendingSampleCount == 0)
            {
                PendingSum = 0f;
            }
        }

        public TabletTimeframeHistoryPersistence CreatePersistence(TabletGraphTimeframe timeframe)
        {
            var persistence = new TabletTimeframeHistoryPersistence
            {
                Timeframe = timeframe,
                PendingSampleCount = PendingSampleCount,
                PendingSum = PendingSum,
            };

            for (int i = 0; i < _values.Count; i++)
            {
                persistence.Values.Add(_values[i]);
            }

            return persistence;
        }

        private void AppendValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
            }

            if (_values.Count >= _capacity)
            {
                _values.RemoveAt(0);
            }

            _values.Add(value);
        }
    }
}
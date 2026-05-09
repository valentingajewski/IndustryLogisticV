using System;
using System.Collections.Generic;

namespace LSOL.Systems
{
    public sealed class GlobalMarketManager
    {
        private const string DefaultCommodityKey = "__default__";
        private const float DefaultPriceMultiplier = 1f;
        private const float ScarcityIncreaseStep = 0.05f;
        private const float DeliveryPressureStep = 0.10f;
        private const int ScarcityIncreaseIntervalMs = 600000;

        private static readonly Dictionary<string, float> BasePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coal", 300f },
            { "Ore", 380f },
            { "Oil", 450f },
            { "Plastic", 900f },
            { "Fuel", 750f },
            { "Metal", 1200f },
            { "Alloy", 1500f },
            { "Processors", 2600f },
            { "ProcessedFood", 900f },
            { "Medicine", 4200f },
            { "TV", 6500f },
            { "Computer", 8200f },
            { "Omega", 12000f },
            { "Recyclable", 500f },
        };

        private readonly Dictionary<string, float> _basePrices;
        private readonly Dictionary<string, CommodityMarketState> _commodityStates;
        private int _lastUpdatedGameTimeMs;

        public GlobalMarketManager(int startGameTimeMs)
        {
            _basePrices = new Dictionary<string, float>(BasePrices, StringComparer.OrdinalIgnoreCase);
            _commodityStates = new Dictionary<string, CommodityMarketState>(StringComparer.OrdinalIgnoreCase);
            _lastUpdatedGameTimeMs = startGameTimeMs;

            foreach (var commodity in _basePrices.Keys)
            {
                EnsureCommodityState(commodity);
            }

            foreach (var commodity in Domain.CommodityCatalog.GetKnownCommodities())
            {
                EnsureCommodityState(commodity);
            }
        }

        public float PriceMultiplier
        {
            get
            {
                if (_commodityStates.Count == 0)
                {
                    return DefaultPriceMultiplier;
                }

                float total = 0f;
                var count = 0;
                foreach (var pair in _commodityStates)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    total += pair.Value.PriceMultiplier;
                    count += 1;
                }

                return count > 0
                    ? total / count
                    : DefaultPriceMultiplier;
            }
        }

        public void Update(int gameTimeMs)
        {
            foreach (var pair in _commodityStates)
            {
                UpdateCommodityState(pair.Value, gameTimeMs);
            }

            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public void RegisterDelivery(int gameTimeMs)
        {
            foreach (var pair in _commodityStates)
            {
                ApplyDeliveryPressure(pair.Value, gameTimeMs);
            }

            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public void RegisterDelivery(string commodity, int gameTimeMs)
        {
            ApplyDeliveryPressure(EnsureCommodityState(commodity), gameTimeMs);
            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public float GetPriceMultiplier(string commodity)
        {
            return EnsureCommodityState(commodity).PriceMultiplier;
        }

        public float GetUnitPrice(string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            float basePrice;
            if (!_basePrices.TryGetValue(commodity ?? string.Empty, out basePrice))
            {
                basePrice = 400f;
            }

            return basePrice * GetPriceMultiplier(commodity);
        }

        private CommodityMarketState EnsureCommodityState(string commodity)
        {
            var key = NormalizeCommodityKey(commodity);
            CommodityMarketState state;
            if (_commodityStates.TryGetValue(key, out state))
            {
                return state;
            }

            state = new CommodityMarketState
            {
                PriceMultiplier = DefaultPriceMultiplier,
                NextScarcityIncreaseAtMs = _lastUpdatedGameTimeMs + ScarcityIncreaseIntervalMs,
            };

            _commodityStates[key] = state;
            return state;
        }

        private static void ApplyDeliveryPressure(CommodityMarketState state, int gameTimeMs)
        {
            if (state == null)
            {
                return;
            }

            state.PriceMultiplier = Math.Max(DefaultPriceMultiplier, state.PriceMultiplier - DeliveryPressureStep);
            state.NextScarcityIncreaseAtMs = gameTimeMs + ScarcityIncreaseIntervalMs;
        }

        private static void UpdateCommodityState(CommodityMarketState state, int gameTimeMs)
        {
            if (state == null)
            {
                return;
            }

            while (gameTimeMs >= state.NextScarcityIncreaseAtMs)
            {
                state.PriceMultiplier += ScarcityIncreaseStep;
                state.NextScarcityIncreaseAtMs += ScarcityIncreaseIntervalMs;
            }
        }

        private static string NormalizeCommodityKey(string commodity)
        {
            var normalized = Domain.CommodityCatalog.Normalize(commodity);
            return string.IsNullOrWhiteSpace(normalized)
                ? DefaultCommodityKey
                : normalized;
        }

        private sealed class CommodityMarketState
        {
            public float PriceMultiplier { get; set; }

            public int NextScarcityIncreaseAtMs { get; set; }
        }
    }
}

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
            { "Coal", 250f },
            { "Gravel", 260f },
            { "Ore", 320f },
            { "Water", 220f },
            { "Wood", 320f },
            { "Crops", 340f },
            { "Oil", 380f },
            { "Livestock", 420f },
            { "Recyclable", 180f },
            { "Fuel", 700f },
            { "Cement", 450f },
            { "Lumber", 520f },
            { "Paper", 550f },
            { "Plastic", 800f },
            { "Alcohol", 650f },
            { "Chemicals", 750f },
            { "Fabric", 700f },
            { "ProcessedFood", 850f },
            { "Meat", 900f },
            { "LiquidFertilizer", 600f },
            { "Steel", 900f },
            { "Alloy", 1100f },
            { "Metal", 850f },
            { "Bricks", 650f },
            { "Concrete", 700f },
            { "Beam", 950f },
            { "MechanicalParts", 1100f },
            { "Electronic", 1400f },
            { "Clothes", 1200f },
            { "Furniture", 1500f },
            { "Medicine", 2400f },
            { "TV", 3000f },
            { "Computer", 3800f },
            { "Vehicles", 5000f },
            { "Omega", 7000f },
        };

        private readonly Dictionary<string, float> _basePrices;
        private readonly Dictionary<string, CommodityMarketState> _commodityStates;
        private int _lastUpdatedGameTimeMs;

        public GlobalMarketManager(int startGameTimeMs, IReadOnlyDictionary<string, float> commodityBasePrices = null)
        {
            _basePrices = new Dictionary<string, float>(BasePrices, StringComparer.OrdinalIgnoreCase);
            if (commodityBasePrices != null)
            {
                foreach (var pair in commodityBasePrices)
                {
                    var commodity = Domain.CommodityCatalog.Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity) || pair.Value <= 0f)
                    {
                        continue;
                    }

                    _basePrices[commodity] = pair.Value;
                }
            }

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

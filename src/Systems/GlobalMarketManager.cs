using System;
using System.Collections.Generic;

namespace LSOL.Systems
{
    public sealed class GlobalMarketManager
    {
        private static readonly Dictionary<string, float> BasePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coal", 300f },
            { "Ore", 380f },
            { "Oil", 450f },
            { "Plastic", 900f },
            { "Fuel", 750f },
            { "Metal", 1200f },
            { "Alloy", 1500f },
            { "Electronic", 2600f },
            { "ProcessedFood", 900f },
            { "Medicine", 4200f },
            { "TV", 6500f },
            { "Computer", 8200f },
            { "Omega", 12000f },
            { "Recyclable", 500f },
        };

        private readonly Dictionary<string, float> _basePrices;
        private int _nextScarcityIncreaseAtMs;

        public GlobalMarketManager(int startGameTimeMs)
        {
            PriceMultiplier = 1f;
            _nextScarcityIncreaseAtMs = startGameTimeMs + 600000;
            _basePrices = new Dictionary<string, float>(BasePrices, StringComparer.OrdinalIgnoreCase);
        }

        public float PriceMultiplier { get; private set; }

        public void Update(int gameTimeMs)
        {
            while (gameTimeMs >= _nextScarcityIncreaseAtMs)
            {
                PriceMultiplier += 0.05f;
                _nextScarcityIncreaseAtMs += 600000;
            }
        }

        public void RegisterDelivery(int gameTimeMs)
        {
            PriceMultiplier = Math.Max(1f, PriceMultiplier - 0.10f);
            _nextScarcityIncreaseAtMs = gameTimeMs + 600000;
        }

        public float GetUnitPrice(string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            float basePrice;
            if (!_basePrices.TryGetValue(commodity ?? string.Empty, out basePrice))
            {
                basePrice = 400f;
            }

            return basePrice * PriceMultiplier;
        }
    }
}

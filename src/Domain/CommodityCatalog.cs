using System;
using System.Collections.Generic;

namespace IndustryLogisticV.Domain
{
    public static class CommodityCatalog
    {
        private static readonly Dictionary<string, string> CommodityAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Iron", "Ore" },
            { "Processors", "Electronic" },
            { "Vehicle", "Vehicles" },
            { "LiquidFertilizer", "LiquidFertiliser" },
            { "Recyclables", "Recyclable" },
        };

        private static Dictionary<string, VehicleCargoType> CargoTypesByCommodity = CreateDefaultCargoTypeMap();

        public static void Configure(IEnumerable<KeyValuePair<string, VehicleCargoType>> commodityCargoTypes)
        {
            var configured = CreateDefaultCargoTypeMap();
            if (commodityCargoTypes != null)
            {
                foreach (var pair in commodityCargoTypes)
                {
                    var commodity = Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    configured[commodity] = pair.Value;
                }
            }

            CargoTypesByCommodity = configured;
        }

        public static string Normalize(string commodity)
        {
            var normalized = (commodity ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            string alias;
            if (CommodityAliases.TryGetValue(normalized, out alias))
            {
                return alias;
            }

            return normalized;
        }

        public static VehicleCargoType GetCargoTypeForCommodity(string commodity)
        {
            var normalized = Normalize(commodity);
            VehicleCargoType cargoType;
            if (CargoTypesByCommodity.TryGetValue(normalized, out cargoType))
            {
                return cargoType;
            }

            return VehicleCargoType.Aggregates;
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, VehicleCargoType> CreateDefaultCargoTypeMap()
        {
            return new Dictionary<string, VehicleCargoType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Oil", VehicleCargoType.Liquid },
                { "Fuel", VehicleCargoType.Liquid },
                { "Omega", VehicleCargoType.Liquid },
                { "TV", VehicleCargoType.CraftedGoods },
                { "Computer", VehicleCargoType.CraftedGoods },
                { "Electronic", VehicleCargoType.CraftedGoods },
                { "Alloy", VehicleCargoType.OpenHull },
                { "Metal", VehicleCargoType.OpenHull },
            };
        }
    }
}

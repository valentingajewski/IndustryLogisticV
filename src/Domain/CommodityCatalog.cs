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

        private static Dictionary<string, VehicleCargoType> _cargoTypesByCommodity = CreateDefaultCargoTypeMap();
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

            _cargoTypesByCommodity = configured.Count > 0
                ? configured
                : CreateDefaultCargoTypeMap();
        }

        public static void Configure(IEnumerable<Config.ResourceGroupConfig> resourceGroups)
        {
            var configured = CreateDefaultCargoTypeMap();
            if (resourceGroups != null)
            {
                foreach (var group in resourceGroups)
                {
                    if (group == null || group.CargoType == VehicleCargoType.Unknown || group.Commodities == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < group.Commodities.Count; i++)
                    {
                        var commodity = Normalize(group.Commodities[i]);
                        if (string.IsNullOrWhiteSpace(commodity))
                        {
                            continue;
                        }

                        configured[commodity] = group.CargoType;
                    }
                }
            }

            _cargoTypesByCommodity = configured.Count > 0
                ? configured
                : CreateDefaultCargoTypeMap();
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
            if (_cargoTypesByCommodity.TryGetValue(normalized, out cargoType))
            {
                return cargoType;
            }

            return VehicleCargoType.Aggregates;
        }

        public static bool RequiresCommodityResolution(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer;
        }

        public static VehicleCargoType ResolveCargoType(VehicleCargoType cargoType, string commodity)
        {
            return RequiresCommodityResolution(cargoType)
                ? GetCargoTypeForCommodity(commodity)
                : cargoType;
        }

        public static bool UsesDoorAnimation(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.CraftedGoods
                || cargoType == VehicleCargoType.Refrigeration;
        }

        public static bool UsesDoorAnimation(string commodity)
        {
            return UsesDoorAnimation(GetCargoTypeForCommodity(commodity));
        }

        public static bool UsesLooseVisual(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.Aggregates
                || cargoType == VehicleCargoType.DryBulk
                || cargoType == VehicleCargoType.Recyclable;
        }

        public static bool UsesLooseVisual(string commodity)
        {
            return UsesLooseVisual(GetCargoTypeForCommodity(commodity));
        }

        public static bool UsesCenteredPropVisual(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.OpenHull
                || cargoType == VehicleCargoType.Wood
                || cargoType == VehicleCargoType.Vehicles;
        }

        public static bool UsesAttachedPropVisual(VehicleCargoType cargoType)
        {
            return !RequiresCommodityResolution(cargoType)
                && cargoType != VehicleCargoType.Liquid
                && !UsesLooseVisual(cargoType);
        }

        public static bool UsesAttachedPropVisual(string commodity)
        {
            return UsesAttachedPropVisual(GetCargoTypeForCommodity(commodity));
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, VehicleCargoType> CreateDefaultCargoTypeMap()
        {
            return new Dictionary<string, VehicleCargoType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Coal", VehicleCargoType.Aggregates },
                { "Gravel", VehicleCargoType.Aggregates },
                { "Ore", VehicleCargoType.Aggregates },
                { "Crops", VehicleCargoType.Aggregates },
                { "Bricks", VehicleCargoType.OpenHull },
                { "Lumber", VehicleCargoType.OpenHull },
                { "Steel", VehicleCargoType.OpenHull },
                { "Oil", VehicleCargoType.Liquid },
                { "Fuel", VehicleCargoType.Liquid },
                { "LiquidFertiliser", VehicleCargoType.Liquid },
                { "Omega", VehicleCargoType.Liquid },
                { "Water", VehicleCargoType.Liquid },
                { "Asphalt", VehicleCargoType.DryBulk },
                { "Cement", VehicleCargoType.DryBulk },
                { "Concrete", VehicleCargoType.DryBulk },
                { "Livestock", VehicleCargoType.CraftedGoods },
                { "Alcohol", VehicleCargoType.CraftedGoods },
                { "Chemicals", VehicleCargoType.CraftedGoods },
                { "Clothes", VehicleCargoType.CraftedGoods },
                { "Fabric", VehicleCargoType.CraftedGoods },
                { "Food", VehicleCargoType.CraftedGoods },
                { "ProcessedFood", VehicleCargoType.CraftedGoods },
                { "Plastic", VehicleCargoType.CraftedGoods },
                { "Paper", VehicleCargoType.CraftedGoods },
                { "Medicine", VehicleCargoType.CraftedGoods },
                { "MechanicalParts", VehicleCargoType.CraftedGoods },
                { "TV", VehicleCargoType.CraftedGoods },
                { "Computer", VehicleCargoType.CraftedGoods },
                { "Electronic", VehicleCargoType.CraftedGoods },
                { "Furniture", VehicleCargoType.CraftedGoods },
                { "Alloy", VehicleCargoType.OpenHull },
                { "Metal", VehicleCargoType.OpenHull },
                { "Beam", VehicleCargoType.OpenHull },
                { "Wood", VehicleCargoType.Wood },
                { "Meat", VehicleCargoType.Refrigeration },
                { "Recyclable", VehicleCargoType.Recyclable },
                { "Vehicles", VehicleCargoType.Vehicles },
            };
        }
    }
}

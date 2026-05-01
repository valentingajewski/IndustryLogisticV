using System;
using System.Collections.Generic;

namespace IndustryLogisticV.Domain
{
    public static class CommodityCatalog
    {
        private static readonly Dictionary<string, VehicleCargoType> CargoTypesByCommodity = new Dictionary<string, VehicleCargoType>(StringComparer.OrdinalIgnoreCase)
        {
            { "Oil", VehicleCargoType.Fluid },
            { "Fuel", VehicleCargoType.Fluid },
            { "Omega", VehicleCargoType.Fluid },
            { "TV", VehicleCargoType.Crate },
            { "Computer", VehicleCargoType.Crate },
            { "Electronic", VehicleCargoType.Crate },
            { "Alloy", VehicleCargoType.Solid },
            { "Metal", VehicleCargoType.Solid },
        };

        public static string Normalize(string commodity)
        {
            return (commodity ?? string.Empty).Trim();
        }

        public static VehicleCargoType GetCargoTypeForCommodity(string commodity)
        {
            var normalized = Normalize(commodity);
            VehicleCargoType cargoType;
            if (CargoTypesByCommodity.TryGetValue(normalized, out cargoType))
            {
                return cargoType;
            }

            return VehicleCargoType.Loose;
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}

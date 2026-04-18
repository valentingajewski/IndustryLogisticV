using System;

namespace IndustryLogisticV.Domain
{
    public static class CommodityCatalog
    {
        public static string Normalize(string commodity)
        {
            return (commodity ?? string.Empty).Trim();
        }

        public static VehicleCargoType GetCargoTypeForCommodity(string commodity)
        {
            var normalized = Normalize(commodity);
            if (normalized.Equals("Oil", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Fuel", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Fluid;
            }

            if (normalized.Equals("TV", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Computer", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Electronic", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Crate;
            }

            return VehicleCargoType.Loose;
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}

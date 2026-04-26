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

            if (normalized.Equals("Alloy", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Metal", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Solid;
            }

            return VehicleCargoType.Loose;
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}

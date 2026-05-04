namespace LSOL.Domain
{
    public enum VehicleCargoType
    {
        Unknown = 0,
        Aggregates = 1,
        OpenHull = 2,
        Wood = 3,
        CraftedGoods = 4,
        Liquid = 5,
        DryBulk = 6,
        Refrigeration = 7,
        Recyclable = 8,
        Vehicles = 9,
        Trailer = 10,

        Loose = Aggregates,
        Crate = CraftedGoods,
        Fluid = Liquid,
        Solid = OpenHull,
    }

    public static class VehicleCargoTypeExtensions
    {
        public static string ToDisplayName(this VehicleCargoType cargoType)
        {
            switch (cargoType)
            {
                case VehicleCargoType.Aggregates:
                    return "Aggregates";
                case VehicleCargoType.OpenHull:
                    return "OpenHull";
                case VehicleCargoType.Wood:
                    return "Wood";
                case VehicleCargoType.CraftedGoods:
                    return "CraftedGoods";
                case VehicleCargoType.Liquid:
                    return "Liquid";
                case VehicleCargoType.DryBulk:
                    return "DryBulk";
                case VehicleCargoType.Refrigeration:
                    return "Refrigeration";
                case VehicleCargoType.Recyclable:
                    return "Recyclable";
                case VehicleCargoType.Vehicles:
                    return "Vehicles";
                case VehicleCargoType.Trailer:
                    return "Trailer";
                default:
                    return "Unknown";
            }
        }
    }
}

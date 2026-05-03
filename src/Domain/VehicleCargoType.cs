namespace IndustryLogisticV.Domain
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
    }
}

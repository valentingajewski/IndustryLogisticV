namespace LSOL.Domain
{
    public sealed class DealershipVehicleDefinition
    {
        public string VehicleId { get; set; }

        public string DisplayName { get; set; }

        public string ModelName { get; set; }

        public string Category { get; set; }

        public float Price { get; set; }

        /// <summary>
        /// Physical towable weight in tons (1-3 for non-bike vehicles). Bikes carry
        /// no weight and are excluded from the towing side job's damaged-vehicle spawns.
        /// </summary>
        public float VehicleWeightTons { get; set; }
    }
}
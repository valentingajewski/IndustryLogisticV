using GTA;

namespace LSOL.Domain
{
    public sealed class VehicleDefinition
    {
        public string SectionName { get; set; }
        public string ModelName { get; set; }
        public VehicleCargoType CargoType { get; set; }
        public float CapacityTons { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTrailer { get; set; }
        public bool IsTractor { get; set; }

        public Model Model => new Model(ModelName);

        public override string ToString()
        {
            return string.Format("{0} ({1}, {2:0.0}t)", ModelName, CargoType.ToDisplayName(), CapacityTons);
        }
    }
}

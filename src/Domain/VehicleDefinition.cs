using System.Collections.Generic;
using GTA;

namespace LSOL.Domain
{
    public sealed class VehicleDefinition
    {
        public string Id { get; set; }
        public string SectionName { get; set; }
        public string DisplayName { get; set; }
        public string ModelName { get; set; }
        public VehicleCargoType CargoType { get; set; }
        public HashSet<string> AcceptedCommodities { get; set; }
        public float CapacityTons { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTrailer { get; set; }
        public bool IsTractor { get; set; }

        public Model Model => new Model(ModelName);

        public override string ToString()
        {
            var label = string.IsNullOrWhiteSpace(DisplayName) ? ModelName : DisplayName;
            return string.Format("{0} ({1}, {2:0.0}t)", label, CargoType.ToDisplayName(), CapacityTons);
        }
    }
}

using System.Collections.Generic;
using GTA;

namespace IndustryLogisticV.Domain
{
    public sealed class VehicleCargoState
    {
        public VehicleCargoState(int vehicleHandle, VehicleCargoType cargoType, float capacityTons)
        {
            VehicleHandle = vehicleHandle;
            CargoType = cargoType;
            CapacityTons = capacityTons;
            AttachedProps = new List<Prop>();
        }

        public int VehicleHandle { get; }
        public VehicleCargoType CargoType { get; set; }
        public float CapacityTons { get; set; }
        public string Commodity { get; set; }
        public float WeightTons { get; set; }
        public List<Prop> AttachedProps { get; }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(Commodity) || WeightTons <= 0.0001f;
            }
        }

        public float FreeCapacityTons
        {
            get
            {
                var free = CapacityTons - WeightTons;
                return free < 0f ? 0f : free;
            }
        }

        public void ClearCargo()
        {
            Commodity = string.Empty;
            WeightTons = 0f;
        }
    }
}

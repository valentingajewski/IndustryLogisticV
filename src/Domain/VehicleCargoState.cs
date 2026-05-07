using System.Collections.Generic;
using GTA;

namespace LSOL.Domain
{
    public sealed class VehicleCargoState
    {
        public VehicleCargoState(int vehicleHandle, VehicleCargoType cargoType, float capacityTons)
        {
            VehicleHandle = vehicleHandle;
            CargoType = cargoType;
            CapacityTons = capacityTons;
            CargoCondition = 1f;
            AttachedProps = new List<Prop>();
        }

        public int VehicleHandle { get; }
        public VehicleCargoType CargoType { get; set; }
        public float CapacityTons { get; set; }
        public string Commodity { get; set; }
        public float WeightTons { get; set; }
        public float CargoCondition { get; set; }
        public float TotalLostTons { get; set; }
        public float LastTrackedRigHealth { get; set; }
        public float LastTrackedRigSpeed { get; set; }
        public string SourceIndustryId { get; set; }
        public string SourceDistrictName { get; set; }
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

        public float FillRatio
        {
            get
            {
                if (CapacityTons <= 0.0001f)
                {
                    return 0f;
                }

                var ratio = WeightTons / CapacityTons;
                if (ratio < 0f)
                {
                    return 0f;
                }

                return ratio > 1f ? 1f : ratio;
            }
        }

        public void ClearCargo()
        {
            Commodity = string.Empty;
            WeightTons = 0f;
            CargoCondition = 1f;
            TotalLostTons = 0f;
            LastTrackedRigHealth = 0f;
            LastTrackedRigSpeed = 0f;
            SourceIndustryId = string.Empty;
            SourceDistrictName = string.Empty;
        }
    }
}

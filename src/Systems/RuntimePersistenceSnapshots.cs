using System.Collections.Generic;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class OwnedFleetPersistenceSnapshot
    {
        public OwnedFleetPersistenceSnapshot()
        {
            Vehicles = new List<OwnedFleetVehicleSnapshot>();
        }

        public List<OwnedFleetVehicleSnapshot> Vehicles { get; }

        public bool HasData
        {
            get { return Vehicles.Count > 0; }
        }
    }

    public sealed class OwnedFleetVehicleSnapshot
    {
        public string PoweredModelName { get; set; }

        public string CargoModelName { get; set; }

        public bool HasSeparateCargoVehicle { get; set; }

        public Vector3 PoweredPosition { get; set; }

        public float PoweredHeading { get; set; }

        public VehicleCargoType CargoType { get; set; }

        public float CapacityTons { get; set; }

        public string Commodity { get; set; }

        public float WeightTons { get; set; }

        public float CargoCondition { get; set; }

        public float TotalLostTons { get; set; }

        public string SourceIndustryId { get; set; }

        public string SourceDistrictName { get; set; }

        public float CurrentFuelLiters { get; set; }
    }

    public sealed class NpcLogisticsPersistenceSnapshot
    {
        public NpcLogisticsPersistenceSnapshot()
        {
            Contracts = new List<NpcLogisticsContractSnapshot>();
        }

        public List<NpcLogisticsContractSnapshot> Contracts { get; }

        public bool HasData
        {
            get { return Contracts.Count > 0; }
        }
    }

    public sealed class NpcLogisticsContractSnapshot
    {
        public int Id { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public string Commodity { get; set; }

        public string TierId { get; set; }

        public float ContractCost { get; set; }

        public int PayrollElapsedInGameMinutes { get; set; }

        public int CompletedPayrollCycles { get; set; }

        public float TotalWeeklyWagesPaid { get; set; }

        public int CompletedDeliveries { get; set; }

        public float TotalDeliveredTons { get; set; }

        public float TotalProfitEarned { get; set; }

        public float LastJourneyLossRatio { get; set; }
    }
}
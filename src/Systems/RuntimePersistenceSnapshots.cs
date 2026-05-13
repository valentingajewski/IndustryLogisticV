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
            WorldJobs = new List<NpcWorldLogisticsJobSnapshot>();
        }

        public List<NpcLogisticsContractSnapshot> Contracts { get; }

        public List<NpcWorldLogisticsJobSnapshot> WorldJobs { get; }

        public NpcWorldDispatchPolicy DispatchPolicy { get; set; } = NpcWorldDispatchPolicy.Balanced;

        public string PriorityCommodity { get; set; }

        public string PriorityDistrict { get; set; }

        public bool PremiumDispatchEnabled { get; set; }

        public int LastWorldEvaluationClockMinute { get; set; } = -1;

        public int CompletedWorldDispatches { get; set; }

        public bool HasData
        {
            get
            {
                return Contracts.Count > 0
                    || WorldJobs.Count > 0
                    || !string.IsNullOrWhiteSpace(PriorityCommodity)
                    || !string.IsNullOrWhiteSpace(PriorityDistrict)
                    || PremiumDispatchEnabled
                    || CompletedWorldDispatches > 0;
            }
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

    public sealed class NpcWorldLogisticsJobSnapshot
    {
        public int Id { get; set; }

        public NpcWorldJobType Type { get; set; }

        public NpcWorldJobPhase Phase { get; set; }

        public string Commodity { get; set; }

        public string SourceLabel { get; set; }

        public string DestinationLabel { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public float Tons { get; set; }

        public int RemainingInGameMinutes { get; set; }

        public int TotalInGameMinutes { get; set; }

        public int CreatedClockMinute { get; set; }

        public bool IsSpotOpportunity { get; set; }

        public bool UsesPremiumDispatch { get; set; }

        public bool IsPriorityMatch { get; set; }

        public bool HasVisibleConvoy { get; set; }

        public bool IsRivalJob { get; set; }

        public int BackhaulDepth { get; set; }

        public string StatusText { get; set; }
    }
}
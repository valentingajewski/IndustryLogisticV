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

        public string PlayerContractId { get; set; }

        public string PlayerContractDestinationIndustryId { get; set; }

        public float CurrentFuelLiters { get; set; }

        public float MaintenanceCondition { get; set; } = 1f;

        public int LastMaintenanceWeekIndex { get; set; } = -1;

        public int LastInspectionWeekIndex { get; set; } = -1;

        public int InspectionOverdueWeeks { get; set; }

        public float LifetimeMaintenanceCost { get; set; }
    }

    public sealed class NpcLogisticsPersistenceSnapshot
    {
        public NpcLogisticsPersistenceSnapshot()
        {
            Contracts = new List<NpcLogisticsContractSnapshot>();
            WorldJobs = new List<NpcWorldLogisticsJobSnapshot>();
            Carriers = new List<NpcCarrierNetworkSnapshot>();
        }

        public List<NpcLogisticsContractSnapshot> Contracts { get; }

        public List<NpcWorldLogisticsJobSnapshot> WorldJobs { get; }

        public List<NpcCarrierNetworkSnapshot> Carriers { get; }

        public NpcWorldDispatchPolicy DispatchPolicy { get; set; } = NpcWorldDispatchPolicy.Balanced;

        public string PriorityCommodity { get; set; }

        public string PriorityDistrict { get; set; }

        public bool PremiumDispatchEnabled { get; set; }

        public bool OfficeDeliveryNotificationsEnabled { get; set; } = true;

        public int LastWorldEvaluationClockMinute { get; set; } = -1;

        public int CompletedWorldDispatches { get; set; }

        public bool HasData
        {
            get
            {
                return Contracts.Count > 0
                    || WorldJobs.Count > 0
                    || Carriers.Count > 0
                    || !string.IsNullOrWhiteSpace(PriorityCommodity)
                    || !string.IsNullOrWhiteSpace(PriorityDistrict)
                    || PremiumDispatchEnabled
                    || !OfficeDeliveryNotificationsEnabled
                    || CompletedWorldDispatches > 0;
            }
        }
    }

    public sealed class NpcLogisticsContractSnapshot
    {
        public NpcLogisticsContractSnapshot()
        {
            Routes = new List<NpcLogisticsRouteSnapshot>();
        }

        public int Id { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public string Commodity { get; set; }

        public string TierId { get; set; }

        public string AssignedVehicleAssetId { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public int OriginTriggerThresholdPercent { get; set; }

        public int DestinationTriggerThresholdPercent { get; set; } = 100;

        public int CurrentRouteIndex { get; set; }

        public List<NpcLogisticsRouteSnapshot> Routes { get; }

        public float ContractCost { get; set; }

        public int PayrollElapsedInGameMinutes { get; set; }

        public int CompletedPayrollCycles { get; set; }

        public float TotalWeeklyWagesPaid { get; set; }

        public int CompletedDeliveries { get; set; }

        public float TotalDeliveredTons { get; set; }

        public float TotalProfitEarned { get; set; }

        public float LastJourneyLossRatio { get; set; }
    }

    public sealed class NpcLogisticsRouteSnapshot
    {
        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public string Commodity { get; set; }

        public string AssignedVehicleAssetId { get; set; }

        public string AssignedVehicleDisplayName { get; set; }

        public int OriginTriggerThresholdPercent { get; set; }

        public int DestinationTriggerThresholdPercent { get; set; } = 100;
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

        public string CarrierId { get; set; }

        public int BackhaulDepth { get; set; }

        public string StatusText { get; set; }
    }

    public sealed class NpcCarrierNetworkSnapshot
    {
        public NpcCarrierNetworkSnapshot()
        {
            PreferredCommodityFamilies = new List<string>();
            PreferredDistricts = new List<string>();
            PreferredCorridors = new List<string>();
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string HomeDistrict { get; set; }

        public List<string> PreferredCommodityFamilies { get; }

        public List<string> PreferredDistricts { get; }

        public List<string> PreferredCorridors { get; }

        public float Strength { get; set; } = 0.35f;

        public float GrowthMomentum { get; set; }

        public float DeclinePressure { get; set; }

        public bool IsDormant { get; set; }

        public int DormantWeekCount { get; set; }

        public int LastActiveWeekIndex { get; set; } = -1;

        public int LastExpansionWeekIndex { get; set; } = -1;

        public int VisualSeed { get; set; }
    }

    public enum AlertLeadTimeMode
    {
        Off = 0,
        DueNow = 1,
        Within60Minutes = 2,
        Within180Minutes = 3,
        WithinDay = 4,
    }

    public enum FleetAlertMode
    {
        Off = 0,
        CriticalOnly = 1,
        WatchAndCritical = 2,
    }

    public enum TerritoryAlertMode
    {
        Off = 0,
        ChargesOnly = 1,
        ChargesAndRisk = 2,
    }

    public sealed class AlertRulesPersistenceSnapshot
    {
        public const AlertLeadTimeMode DefaultRentLeadTime = AlertLeadTimeMode.Within180Minutes;
        public const AlertLeadTimeMode DefaultContractLeadTime = AlertLeadTimeMode.Within60Minutes;
        public const FleetAlertMode DefaultFleetMode = FleetAlertMode.CriticalOnly;
        public const TerritoryAlertMode DefaultTerritoryMode = TerritoryAlertMode.ChargesAndRisk;

        public AlertLeadTimeMode RentLeadTime { get; set; } = DefaultRentLeadTime;

        public AlertLeadTimeMode ContractLeadTime { get; set; } = DefaultContractLeadTime;

        public FleetAlertMode FleetMode { get; set; } = DefaultFleetMode;

        public TerritoryAlertMode TerritoryMode { get; set; } = DefaultTerritoryMode;

        public bool HasData
        {
            get
            {
                return RentLeadTime != DefaultRentLeadTime
                    || ContractLeadTime != DefaultContractLeadTime
                    || FleetMode != DefaultFleetMode
                    || TerritoryMode != DefaultTerritoryMode;
            }
        }
    }
}
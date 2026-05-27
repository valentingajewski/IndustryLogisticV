using GTA.Math;

namespace LSOL.Domain
{
    public enum OfficeFacilityAnchorType
    {
        None = 0,
        ReceptionDesk = 1,
        DispatchDesk = 2,
        Boardroom = 3,
        MaintenanceDesk = 4,
        FuelDesk = 5,
        BreakRoom = 6,
        WorkerFallback = 7,
    }

    public enum OfficeObjectPlacementContext
    {
        Yard = 0,
        Room = 1,
        Either = 2,
    }

    public enum OfficeFacilityInteractionType
    {
        None = 0,
        OfficeSummary = 1,
        HireNpc = 2,
        RepairVehicle = 3,
        FuelManagement = 4,
        HeadquartersStatus = 5,
    }

    public enum OfficeAmbientStaffRole
    {
        None = 0,
        Receptionist = 1,
        Dispatcher = 2,
        Mechanic = 3,
        SupportWorker = 4,
        Security = 5,
        Manager = 6,
        AdminClerk = 7,
    }

    public sealed class OfficeFacilityAnchorDefinition
    {
        public string AnchorId { get; set; }

        public OfficeFacilityAnchorType AnchorType { get; set; }

        public string Label { get; set; }

        public Vector3 Position { get; set; }

        public float Heading { get; set; }

        public float InteractionRadius { get; set; } = 2.2f;

        public string ScenarioName { get; set; }
    }
}
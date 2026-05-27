namespace LSOL.Domain
{
    public enum OfficeObjectSize
    {
        Small = 0,
        Medium = 1,
        Big = 2,
    }

    public enum OfficeObjectFunction
    {
        Decorative = 0,
        Refuel = 1,
        Repair = 2,
        Npc = 3,
        Headquarters = 4,
    }

    public sealed class OfficeObjectDefinition
    {
        public int ObjectId { get; set; }

        public string ModelName { get; set; }

        public int ModelHash { get; set; }

        public string DisplayName { get; set; }

        public OfficeObjectSize Size { get; set; }

        public OfficeObjectFunction Function { get; set; }

        public string ResourceType { get; set; }

        public float Capacity { get; set; }

        public OfficeObjectPlacementContext PlacementContext { get; set; }

        public OfficeFacilityAnchorType AnchorType { get; set; }

        public OfficeFacilityInteractionType InteractionType { get; set; }

        public OfficeAmbientStaffRole AmbientStaffRole { get; set; }

        public int AmbientStaffCount { get; set; }

        public bool RequiresOwnedOffice { get; set; }

        public string AmbientScenarioName { get; set; }

        public int PerOfficeLimit { get; set; }

        public float Price { get; set; }

        public bool UsesFacilityAnchor
        {
            get { return AnchorType != OfficeFacilityAnchorType.None && PlacementContext != OfficeObjectPlacementContext.Yard; }
        }

        public bool RequiresRoomAnchor
        {
            get { return AnchorType != OfficeFacilityAnchorType.None && PlacementContext == OfficeObjectPlacementContext.Room; }
        }

        public bool IsFunctional
        {
            get { return Function != OfficeObjectFunction.Decorative; }
        }
    }
}
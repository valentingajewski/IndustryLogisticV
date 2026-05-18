using System;
using System.Collections.Generic;
using GTA.Math;

namespace LSOL.Domain
{
    public enum SpecialMissionType
    {
        Unknown = 0,
        HandlerContainerTransfer = 1,
        TrailerDelivery = 2,
        DynamicCargoDelivery = 3,
    }

    public enum GeneratedContractFamily
    {
        None = 0,
        CrisisRelief = 1,
        WeeklyTender = 2,
        PriorityLinehaul = 3,
    }

    public enum DistrictCrisisType
    {
        None = 0,
        FuelShortage = 1,
        ConstructionSurge = 2,
        SupplyDisruption = 3,
        EmergencyRestock = 4,
    }

    public sealed class SpecialMissionDefinition
    {
        public SpecialMissionDefinition()
        {
            Category = string.Empty;
            Name = string.Empty;
            Summary = string.Empty;
            Description = string.Empty;
            SourceIndustryId = string.Empty;
            DestinationIndustryId = string.Empty;
            Commodity = string.Empty;
            CrisisEventId = string.Empty;
            CrisisDistrictName = string.Empty;
            EligibilitySummary = string.Empty;
            Unlock = new SpecialMissionUnlockRequirement();
            Vehicles = new Dictionary<string, SpecialMissionVehicleSpawn>(StringComparer.OrdinalIgnoreCase);
            Props = new Dictionary<string, SpecialMissionPropSpawn>(StringComparer.OrdinalIgnoreCase);
            Zones = new Dictionary<string, SpecialMissionZone>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; set; }

        public SpecialMissionType Type { get; set; }

        public string Category { get; set; }

        public string Name { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public float Reward { get; set; }

        public bool Repeatable { get; set; }

        public bool IsGenerated { get; set; }

        public GeneratedContractFamily ContractFamily { get; set; }

        public bool IsTender { get; set; }

        public int RepeatCooldownInGameMinutes { get; set; }

        public int RepeatCooldownInGameMonths { get; set; }

        public int AvailabilityDelayInGameMinutes { get; set; }

        public int AvailabilityDelayInGameMonths { get; set; }

        public int PostedAtInGameMinute { get; set; }

        public int AvailableUntilInGameMinute { get; set; }

        public string SourceIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public string Commodity { get; set; }

        public float TargetTons { get; set; }

        public VehicleCargoType RequiredCargoType { get; set; }

        public float MinimumVehicleCapacityTons { get; set; }

        public string CrisisEventId { get; set; }

        public DistrictCrisisType CrisisType { get; set; }

        public string CrisisDistrictName { get; set; }

        public string EligibilitySummary { get; set; }

        public SpecialMissionUnlockRequirement Unlock { get; set; }

        public Dictionary<string, SpecialMissionVehicleSpawn> Vehicles { get; }

        public Dictionary<string, SpecialMissionPropSpawn> Props { get; }

        public Dictionary<string, SpecialMissionZone> Zones { get; }

        public bool TryGetVehicle(string roleId, out SpecialMissionVehicleSpawn spawn)
        {
            spawn = null;
            return !string.IsNullOrWhiteSpace(roleId)
                && Vehicles.TryGetValue(roleId.Trim(), out spawn)
                && spawn != null;
        }

        public bool TryGetProp(string roleId, out SpecialMissionPropSpawn spawn)
        {
            spawn = null;
            return !string.IsNullOrWhiteSpace(roleId)
                && Props.TryGetValue(roleId.Trim(), out spawn)
                && spawn != null;
        }

        public bool TryGetZone(string zoneId, out SpecialMissionZone zone)
        {
            zone = null;
            return !string.IsNullOrWhiteSpace(zoneId)
                && Zones.TryGetValue(zoneId.Trim(), out zone)
                && zone != null;
        }

        public bool HasRepeatCooldown
        {
            get { return RepeatCooldownInGameMinutes > 0 || RepeatCooldownInGameMonths > 0; }
        }

        public bool HasAvailabilityDelay
        {
            get { return AvailabilityDelayInGameMinutes > 0 || AvailabilityDelayInGameMonths > 0; }
        }

        public bool HasAvailabilityWindow
        {
            get { return AvailableUntilInGameMinute > 0; }
        }
    }

    public sealed class SpecialMissionUnlockRequirement
    {
        public SpecialMissionUnlockRequirement()
        {
            DistrictNames = new List<string>();
            MinInfluenceRatio = 0f;
        }

        public List<string> DistrictNames { get; }

        public float MinInfluenceRatio { get; set; }
    }

    public sealed class SpecialMissionVehicleSpawn
    {
        public SpecialMissionVehicleSpawn()
        {
            ModelName = string.Empty;
        }

        public string RoleId { get; set; }

        public string ModelName { get; set; }

        public Vector3 Position { get; set; }

        public float Heading { get; set; }

        public bool Required { get; set; } = true;
    }

    public sealed class SpecialMissionPropSpawn
    {
        public SpecialMissionPropSpawn()
        {
            RoleId = string.Empty;
            ModelName = string.Empty;
            AttachTargetRoleId = string.Empty;
            AttachOffset = Vector3.Zero;
            AttachRotation = Vector3.Zero;
        }

        public string RoleId { get; set; }

        public string ModelName { get; set; }

        public int ModelHash { get; set; }

        public Vector3 Position { get; set; }

        public float Heading { get; set; }

        public string AttachTargetRoleId { get; set; }

        public Vector3 AttachOffset { get; set; }

        public Vector3 AttachRotation { get; set; }

        public bool Required { get; set; } = true;

        public bool HasModelHash
        {
            get { return ModelHash != 0; }
        }
    }

    public sealed class SpecialMissionZone
    {
        public SpecialMissionZone()
        {
            ZoneId = string.Empty;
            Radius = 8f;
        }

        public string ZoneId { get; set; }

        public Vector3 Position { get; set; }

        public float Radius { get; set; }
    }
}
using System.Collections.Generic;
using GTA.Math;

namespace IndustryLogisticV.Config
{
    public sealed class IndustryConfig
    {
        public string Id { get; set; }
        public ExternalLocationKind LocationKind { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public Vector3? VehicleSpawnPosition { get; set; }
        public float? VehicleSpawnHeading { get; set; }
        public HashSet<string> Inputs { get; set; }
        public HashSet<string> Outputs { get; set; }
        public float InputCapacityTons { get; set; }
        public float OutputCapacityTons { get; set; }
        public float ProductionRate { get; set; }
        public float StartingTankRatio { get; set; }
        public string Density { get; set; }
        public float IndustryPrice { get; set; }
        public float IndustryOwnerCut { get; set; }
        public bool IsOwned { get; set; }
    }
}

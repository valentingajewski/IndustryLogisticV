using GTA.Math;

namespace LSOL.Domain
{
    public sealed class InteriorDefinition
    {
        public string InteriorId { get; set; }

        public string InteriorName { get; set; }

        public string InteriorIgName { get; set; }

        public Vector3 InteriorPosition { get; set; }

        public string InteriorType { get; set; }

        public float InteriorPrice { get; set; }

        public float InteriorWeeklyRent { get; set; }

        public Vector3 ExteriorPosition { get; set; }

        public Vector3 GaragePosition { get; set; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(InteriorName) ? (InteriorId ?? string.Empty) : InteriorName; }
        }
    }
}
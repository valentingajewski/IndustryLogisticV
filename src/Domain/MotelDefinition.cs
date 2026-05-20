using GTA.Math;

namespace LSOL.Domain
{
    public sealed class MotelDefinition
    {
        public string MotelId { get; set; }

        public string MotelName { get; set; }

        public string MotelIgName { get; set; }

        public string MotelType { get; set; }

        public float RestPrice { get; set; }

        public Vector3 ExteriorPosition { get; set; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(MotelName) ? (MotelId ?? string.Empty) : MotelName; }
        }
    }
}
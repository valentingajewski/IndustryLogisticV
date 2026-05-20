using GTA.Math;

namespace LSOL.Domain
{
    public sealed class VehicleObjectLayoutDefinition
    {
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string ObjectKey { get; set; }
        public Vector3 CenterOffset { get; set; }
        public int MaxLine { get; set; }
        public int MaxRow { get; set; }
        public bool IsEnabled { get; set; }

        public bool HasUsableGrid
        {
            get { return MaxLine > 0 && MaxRow > 0; }
        }
    }
}
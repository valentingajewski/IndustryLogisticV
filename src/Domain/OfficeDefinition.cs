using System.Collections.Generic;
using GTA.Math;

namespace LSOL.Domain
{
    public sealed class OfficeDefinition
    {
        public string OfficeId { get; set; }

        public string LegacyKey { get; set; }

        public string SiteName { get; set; }

        public string DistrictName { get; set; }

        public Vector3 MarkerPosition { get; set; }

        public Vector3 SpawnPosition { get; set; }

        public float SpawnHeading { get; set; }

        public Vector3? GatePosition { get; set; }

        public Vector3? WorkerPosition { get; set; }

        public List<OfficeFacilityAnchorDefinition> FacilityAnchors { get; set; } = new List<OfficeFacilityAnchorDefinition>();

        public float OfficePrice { get; set; }

        public float WeeklyOfficeRent { get; set; }

        public int MaxCommercialVehicles { get; set; }

        public string Description { get; set; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(SiteName) ? (OfficeId ?? string.Empty) : SiteName; }
        }
    }
}
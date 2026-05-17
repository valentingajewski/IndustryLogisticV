using LSOL.Config;
using LSOL.Domain;

namespace LSOL.UI
{
    internal static class TabletLocationFilters
    {
        public static bool IsProductionIndustry(Industry industry)
        {
            return industry != null
                && industry.LocationKind == ExternalLocationKind.Industry
                && industry.SiteRole != SiteRole.Warehouse
                && industry.SiteRole != SiteRole.ConstructionSiteSink;
        }

        public static bool IsConstructionSite(Industry industry)
        {
            return industry != null
                && industry.LocationKind == ExternalLocationKind.Industry
                && industry.SiteRole == SiteRole.ConstructionSiteSink;
        }

        public static bool IsWarehouse(Industry industry)
        {
            return industry != null
                && industry.LocationKind == ExternalLocationKind.Industry
                && industry.SiteRole == SiteRole.Warehouse;
        }

        public static bool IsStorageTrackedSite(Industry industry)
        {
            return industry != null
                && industry.SiteRole != SiteRole.ConstructionSiteSink
                && (industry.SiteRole == SiteRole.Warehouse || industry.LocationKind == ExternalLocationKind.Industry);
        }
    }
}
using System;
using System.Collections.Generic;
using LSOL.Domain;

namespace LSOL.Systems
{
    internal static class NpcLogisticsPersistenceMapper
    {
        internal static NpcLogisticsContractSnapshot CreateContractSnapshotOrNull(NpcLogisticsContract contract)
        {
            var primaryRoute = contract != null && contract.Routes.Count > 0
                ? contract.Routes[0]
                : null;
            if (contract == null || primaryRoute == null || primaryRoute.OriginIndustry == null || primaryRoute.DestinationIndustry == null || contract.Tier == null)
            {
                return null;
            }

            var snapshot = new NpcLogisticsContractSnapshot
            {
                Id = contract.Id,
                OriginIndustryId = primaryRoute.OriginIndustry.Id,
                DestinationIndustryId = primaryRoute.DestinationIndustry.Id,
                Commodity = primaryRoute.Commodity,
                TierId = contract.Tier.Id,
                AssignedVehicleAssetId = contract.AssignedVehicleAssetId,
                AssignedVehicleDisplayName = contract.AssignedVehicleDisplayName,
                OriginTriggerThresholdPercent = primaryRoute.OriginTriggerThresholdPercent,
                DestinationTriggerThresholdPercent = primaryRoute.DestinationTriggerThresholdPercent,
                CurrentRouteIndex = contract.CurrentRouteIndex,
                ContractCost = contract.ContractCost,
                PayrollElapsedInGameMinutes = contract.PayrollElapsedInGameMinutes,
                CompletedPayrollCycles = contract.CompletedPayrollCycles,
                TotalWeeklyWagesPaid = contract.TotalWeeklyWagesPaid,
                CompletedDeliveries = contract.CompletedDeliveries,
                TotalDeliveredTons = contract.TotalDeliveredTons,
                TotalProfitEarned = contract.TotalProfitEarned,
                LastJourneyLossRatio = contract.LastJourneyLossRatio,
            };

            for (int routeIndex = 0; routeIndex < contract.Routes.Count; routeIndex++)
            {
                var route = contract.Routes[routeIndex];
                if (route == null || route.OriginIndustry == null || route.DestinationIndustry == null || string.IsNullOrWhiteSpace(route.Commodity))
                {
                    continue;
                }

                snapshot.Routes.Add(new NpcLogisticsRouteSnapshot
                {
                    OriginIndustryId = route.OriginIndustry.Id,
                    DestinationIndustryId = route.DestinationIndustry.Id,
                    Commodity = route.Commodity,
                    AssignedVehicleAssetId = route.AssignedVehicleAssetId,
                    AssignedVehicleDisplayName = route.AssignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = route.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = route.DestinationTriggerThresholdPercent,
                });
            }

            return snapshot;
        }

        internal static List<NpcLogisticsRouteDefinition> CreateRouteDefinitionsFromSnapshot(
            NpcLogisticsContractSnapshot snapshot,
            Industry legacyOriginIndustry,
            Industry legacyDestinationIndustry,
            Func<string, Industry> findIndustryById,
            Func<string, string> normalizeCommodity,
            Func<int, int, int> clampTriggerPercent)
        {
            var routeDefinitions = new List<NpcLogisticsRouteDefinition>();
            if (snapshot == null || findIndustryById == null || normalizeCommodity == null || clampTriggerPercent == null)
            {
                return routeDefinitions;
            }

            if (snapshot.Routes != null && snapshot.Routes.Count > 0)
            {
                for (int routeIndex = 0; routeIndex < snapshot.Routes.Count; routeIndex++)
                {
                    var routeEntry = snapshot.Routes[routeIndex];
                    if (routeEntry == null)
                    {
                        continue;
                    }

                    AddRouteDefinitionIfValid(
                        routeDefinitions,
                        findIndustryById(routeEntry.OriginIndustryId),
                        findIndustryById(routeEntry.DestinationIndustryId),
                        normalizeCommodity(routeEntry.Commodity),
                        ResolveRouteValue(routeEntry.AssignedVehicleAssetId, snapshot.AssignedVehicleAssetId),
                        ResolveRouteValue(routeEntry.AssignedVehicleDisplayName, snapshot.AssignedVehicleDisplayName),
                        routeEntry.OriginTriggerThresholdPercent,
                        routeEntry.DestinationTriggerThresholdPercent,
                        clampTriggerPercent);
                }
            }

            if (routeDefinitions.Count == 0)
            {
                AddRouteDefinitionIfValid(
                    routeDefinitions,
                    legacyOriginIndustry,
                    legacyDestinationIndustry,
                    normalizeCommodity(snapshot.Commodity),
                    snapshot.AssignedVehicleAssetId,
                    snapshot.AssignedVehicleDisplayName,
                    snapshot.OriginTriggerThresholdPercent,
                    snapshot.DestinationTriggerThresholdPercent,
                    clampTriggerPercent);
            }

            return routeDefinitions;
        }

        private static void AddRouteDefinitionIfValid(
            IList<NpcLogisticsRouteDefinition> routeDefinitions,
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            string assignedVehicleAssetId,
            string assignedVehicleDisplayName,
            int originTriggerThresholdPercent,
            int destinationTriggerThresholdPercent,
            Func<int, int, int> clampTriggerPercent)
        {
            if (routeDefinitions == null
                || originIndustry == null
                || destinationIndustry == null
                || string.IsNullOrWhiteSpace(commodity)
                || clampTriggerPercent == null)
            {
                return;
            }

            routeDefinitions.Add(new NpcLogisticsRouteDefinition
            {
                OriginIndustry = originIndustry,
                DestinationIndustry = destinationIndustry,
                Commodity = commodity,
                AssignedVehicleAssetId = assignedVehicleAssetId,
                AssignedVehicleDisplayName = assignedVehicleDisplayName,
                OriginTriggerThresholdPercent = clampTriggerPercent(originTriggerThresholdPercent, 0),
                DestinationTriggerThresholdPercent = clampTriggerPercent(destinationTriggerThresholdPercent, 100),
            });
        }

        private static string ResolveRouteValue(string preferredValue, string fallbackValue)
        {
            return string.IsNullOrWhiteSpace(preferredValue)
                ? fallbackValue
                : preferredValue;
        }
    }
}
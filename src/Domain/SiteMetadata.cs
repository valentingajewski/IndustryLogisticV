using System;

namespace LSOL.Domain
{
    public enum SiteRole
    {
        Unknown = 0,
        StarterHQ = 1,
        RawProducer = 2,
        ProcessingPlant = 3,
        ManufacturingPlant = 4,
        RecyclingHub = 5,
        SpecialPlant = 6,
        Warehouse = 7,
        ConstructionSiteSink = 8,
        StoreSink = 9,
        FuelSink = 10,
        FleetYard = 11,
        Depot = 12,
        StorageYard = 13,
        TruckYard = 14,
    }

    public enum SiteOwnershipTier
    {
        Unknown = 0,
        Starter = 1,
        Local = 2,
        Regional = 3,
        Expansion = 4,
        Core = 5,
        Monopoly = 6,
    }

    public static class SiteMetadataParser
    {
        public static SiteRole ParseRole(string raw)
        {
            var normalized = Normalize(raw);
            if (normalized.Length == 0)
            {
                return SiteRole.Unknown;
            }

            switch (normalized)
            {
                case "starterhq":
                case "mainoffice":
                    return SiteRole.StarterHQ;
                case "rawproducer":
                case "mine":
                    return SiteRole.RawProducer;
                case "processingplant":
                    return SiteRole.ProcessingPlant;
                case "manufacturingplant":
                    return SiteRole.ManufacturingPlant;
                case "recyclinghub":
                    return SiteRole.RecyclingHub;
                case "specialplant":
                    return SiteRole.SpecialPlant;
                case "warehouse":
                    return SiteRole.Warehouse;
                case "constructionsitesink":
                case "construction":
                    return SiteRole.ConstructionSiteSink;
                case "storesink":
                case "store":
                    return SiteRole.StoreSink;
                case "fuelsink":
                case "gasstation":
                case "petrolstation":
                    return SiteRole.FuelSink;
                case "fleetyard":
                    return SiteRole.FleetYard;
                case "depot":
                    return SiteRole.Depot;
                case "storageyard":
                    return SiteRole.StorageYard;
                case "truckyard":
                    return SiteRole.TruckYard;
                default:
                    return SiteRole.Unknown;
            }
        }

        public static SiteOwnershipTier ParseOwnershipTier(string raw)
        {
            var normalized = Normalize(raw);
            if (normalized.Length == 0)
            {
                return SiteOwnershipTier.Unknown;
            }

            switch (normalized)
            {
                case "starter":
                    return SiteOwnershipTier.Starter;
                case "local":
                    return SiteOwnershipTier.Local;
                case "regional":
                    return SiteOwnershipTier.Regional;
                case "expansion":
                    return SiteOwnershipTier.Expansion;
                case "core":
                    return SiteOwnershipTier.Core;
                case "monopoly":
                case "strategic":
                    return SiteOwnershipTier.Monopoly;
                default:
                    return SiteOwnershipTier.Unknown;
            }
        }

        public static bool IsDepotLike(SiteRole role)
        {
            return role == SiteRole.StarterHQ
                || role == SiteRole.Warehouse
                || role == SiteRole.FleetYard
                || role == SiteRole.Depot
                || role == SiteRole.StorageYard
                || role == SiteRole.TruckYard;
        }

        public static bool GrantsStarterAccess(SiteRole role, SiteOwnershipTier ownershipTier)
        {
            return role == SiteRole.StarterHQ || ownershipTier == SiteOwnershipTier.Starter;
        }

        private static string Normalize(string raw)
        {
            return (raw ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
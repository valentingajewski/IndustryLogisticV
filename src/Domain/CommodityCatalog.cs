using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Domain
{
    public static class CommodityCatalog
    {
        private static readonly Dictionary<string, string> CommodityAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Iron", "Ore" },
            { "Processor", "Electronic" },
            { "Processors", "Electronic" },
            { "Food", "ProcessedFood" },
            { "Vehicle", "Vehicles" },
            { "Liquid Fertilizer", "LiquidFertilizer" },
            { "LiquidFertiliser", "LiquidFertilizer" },
            { "Medecine", "Medicine" },
            { "Recyclables", "Recyclable" },
        };

        private static Dictionary<string, VehicleCargoType> _cargoTypesByCommodity = CreateDefaultCargoTypeMap();
        private static Dictionary<string, CommodityEconomySemantics> _economySemanticsByCommodity = CreateDefaultEconomySemanticsMap(CreateDefaultCargoTypeMap(), null);

        public static void Configure(IEnumerable<KeyValuePair<string, VehicleCargoType>> commodityCargoTypes)
        {
            var configured = CreateDefaultCargoTypeMap();
            if (commodityCargoTypes != null)
            {
                foreach (var pair in commodityCargoTypes)
                {
                    var commodity = Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    configured[commodity] = pair.Value;
                }
            }

            _cargoTypesByCommodity = configured.Count > 0
                ? configured
                : CreateDefaultCargoTypeMap();
            _economySemanticsByCommodity = CreateDefaultEconomySemanticsMap(_cargoTypesByCommodity, null);
        }

        public static void Configure(IEnumerable<Config.ResourceGroupConfig> resourceGroups)
        {
            Configure(resourceGroups, null);
        }

        public static void Configure(IEnumerable<Config.ResourceGroupConfig> resourceGroups, IEnumerable<Config.ExternalResourceConfig> resources)
        {
            var configured = CreateDefaultCargoTypeMap();
            if (resourceGroups != null)
            {
                foreach (var group in resourceGroups)
                {
                    if (group == null || group.CargoType == VehicleCargoType.Unknown || group.Commodities == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < group.Commodities.Count; i++)
                    {
                        var commodity = Normalize(group.Commodities[i]);
                        if (string.IsNullOrWhiteSpace(commodity))
                        {
                            continue;
                        }

                        configured[commodity] = group.CargoType;
                    }
                }
            }

            _cargoTypesByCommodity = configured.Count > 0
                ? configured
                : CreateDefaultCargoTypeMap();
            _economySemanticsByCommodity = CreateDefaultEconomySemanticsMap(_cargoTypesByCommodity, resources);
        }

        public static string Normalize(string commodity)
        {
            var normalized = (commodity ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }
            string alias;
            if (CommodityAliases.TryGetValue(normalized, out alias))
            {
                return alias;
            }

            return normalized;
        }

        public static VehicleCargoType GetCargoTypeForCommodity(string commodity)
        {
            var normalized = Normalize(commodity);
            VehicleCargoType cargoType;
            if (_cargoTypesByCommodity.TryGetValue(normalized, out cargoType))
            {
                return cargoType;
            }

            return VehicleCargoType.Aggregates;
        }

        public static bool IsKnownCommodity(string commodity)
        {
            var normalized = Normalize(commodity);
            return normalized.Length > 0 && _cargoTypesByCommodity.ContainsKey(normalized);
        }

        public static bool RequiresCommodityResolution(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer;
        }

        public static VehicleCargoType ResolveCargoType(VehicleCargoType cargoType, string commodity)
        {
            return RequiresCommodityResolution(cargoType)
                ? GetCargoTypeForCommodity(commodity)
                : cargoType;
        }

        public static bool UsesDoorAnimation(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.CraftedGoods
                || cargoType == VehicleCargoType.Refrigeration;
        }

        public static bool UsesDoorAnimation(string commodity)
        {
            return UsesDoorAnimation(GetCargoTypeForCommodity(commodity));
        }

        public static bool UsesLooseVisual(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.Aggregates
                || cargoType == VehicleCargoType.DryBulk
                || cargoType == VehicleCargoType.Recyclable;
        }

        public static bool UsesLooseVisual(string commodity)
        {
            return UsesLooseVisual(GetCargoTypeForCommodity(commodity));
        }

        public static bool UsesCenteredPropVisual(VehicleCargoType cargoType)
        {
            return cargoType == VehicleCargoType.OpenHull
                || cargoType == VehicleCargoType.Vehicles;
        }

        public static bool UsesAttachedPropVisual(VehicleCargoType cargoType)
        {
            return !RequiresCommodityResolution(cargoType)
                && cargoType != VehicleCargoType.Liquid
                && cargoType != VehicleCargoType.Wood
                && !UsesLooseVisual(cargoType);
        }

        public static bool UsesAttachedPropVisual(string commodity)
        {
            return UsesAttachedPropVisual(GetCargoTypeForCommodity(commodity));
        }

        public static bool IsSameCommodity(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        public static CommodityEconomySemantics GetEconomySemantics(string commodity)
        {
            commodity = Normalize(commodity);
            CommodityEconomySemantics semantics;
            if (!string.IsNullOrWhiteSpace(commodity) && _economySemanticsByCommodity.TryGetValue(commodity, out semantics) && semantics != null)
            {
                return semantics;
            }

            return CreateDefaultEconomySemantics(commodity, GetCargoTypeForCommodity(commodity));
        }

        public static float GetSubstituteAffinity(string preferredCommodity, string candidateCommodity)
        {
            preferredCommodity = Normalize(preferredCommodity);
            candidateCommodity = Normalize(candidateCommodity);
            if (string.IsNullOrWhiteSpace(preferredCommodity) || string.IsNullOrWhiteSpace(candidateCommodity))
            {
                return 0f;
            }

            if (string.Equals(preferredCommodity, candidateCommodity, StringComparison.OrdinalIgnoreCase))
            {
                return 1f;
            }

            var preferredSemantics = GetEconomySemantics(preferredCommodity);
            var candidateSemantics = GetEconomySemantics(candidateCommodity);
            var affinity = 0f;

            if (preferredSemantics.Substitutes != null)
            {
                float configuredAffinity;
                if (preferredSemantics.Substitutes.TryGetValue(candidateCommodity, out configuredAffinity))
                {
                    affinity = Math.Max(affinity, configuredAffinity);
                }
            }

            if (candidateSemantics.Substitutes != null)
            {
                float configuredAffinity;
                if (candidateSemantics.Substitutes.TryGetValue(preferredCommodity, out configuredAffinity))
                {
                    affinity = Math.Max(affinity, configuredAffinity);
                }
            }

            if (affinity <= 0f
                && !string.IsNullOrWhiteSpace(preferredSemantics.SubstituteFamily)
                && string.Equals(preferredSemantics.SubstituteFamily, candidateSemantics.SubstituteFamily, StringComparison.OrdinalIgnoreCase))
            {
                affinity = 0.42f;
            }

            return Math.Max(0f, Math.Min(1f, affinity));
        }

        public static float GetEventResponseAffinity(string preferredCommodity, string candidateCommodity)
        {
            preferredCommodity = Normalize(preferredCommodity);
            candidateCommodity = Normalize(candidateCommodity);
            if (string.IsNullOrWhiteSpace(preferredCommodity) || string.IsNullOrWhiteSpace(candidateCommodity))
            {
                return 0f;
            }

            if (string.Equals(preferredCommodity, candidateCommodity, StringComparison.OrdinalIgnoreCase))
            {
                return 1f;
            }

            var preferredSemantics = GetEconomySemantics(preferredCommodity);
            var candidateSemantics = GetEconomySemantics(candidateCommodity);
            var affinity = GetSubstituteAffinity(preferredCommodity, candidateCommodity);
            if (affinity <= 0f && SharesDemandClass(preferredSemantics, candidateSemantics))
            {
                affinity = 0.58f;
            }

            if (affinity <= 0f
                && !string.IsNullOrWhiteSpace(preferredSemantics.SubstituteFamily)
                && string.Equals(preferredSemantics.SubstituteFamily, candidateSemantics.SubstituteFamily, StringComparison.OrdinalIgnoreCase))
            {
                affinity = 0.35f;
            }

            if (affinity <= 0f)
            {
                return 0f;
            }

            var eventAffinity = candidateSemantics.EventResponseAffinity > 0f
                ? candidateSemantics.EventResponseAffinity
                : 1f;
            return Math.Max(0f, Math.Min(1f, affinity * (0.70f + (eventAffinity * 0.30f))));
        }

        public static IReadOnlyList<CommodityAffinityEntry> GetEventResponseCommodityCandidates(string preferredCommodity, int maxCount = 5)
        {
            preferredCommodity = Normalize(preferredCommodity);
            if (string.IsNullOrWhiteSpace(preferredCommodity))
            {
                return Array.Empty<CommodityAffinityEntry>();
            }

            var results = new List<CommodityAffinityEntry>
            {
                new CommodityAffinityEntry(preferredCommodity, 1f),
            };

            foreach (var commodity in GetKnownCommodities())
            {
                if (string.Equals(commodity, preferredCommodity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var affinity = GetEventResponseAffinity(preferredCommodity, commodity);
                if (affinity <= 0.15f)
                {
                    continue;
                }

                results.Add(new CommodityAffinityEntry(commodity, affinity));
            }

            var ordered = results
                .OrderByDescending(entry => entry.Affinity)
                .ThenBy(entry => entry.Commodity, StringComparer.OrdinalIgnoreCase);
            if (maxCount > 0)
            {
                return ordered.Take(maxCount).ToArray();
            }

            return ordered.ToArray();
        }

        public static IReadOnlyList<string> GetKnownCommodities()
        {
            return _cargoTypesByCommodity.Keys
                .Concat(_economySemanticsByCommodity.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool SharesDemandClass(CommodityEconomySemantics left, CommodityEconomySemantics right)
        {
            if (left == null || right == null || left.DemandClasses == null || right.DemandClasses == null)
            {
                return false;
            }

            return left.DemandClasses.Any(right.DemandClasses.Contains);
        }

        private static Dictionary<string, CommodityEconomySemantics> CreateDefaultEconomySemanticsMap(
            IReadOnlyDictionary<string, VehicleCargoType> cargoTypes,
            IEnumerable<Config.ExternalResourceConfig> resources)
        {
            var result = new Dictionary<string, CommodityEconomySemantics>(StringComparer.OrdinalIgnoreCase);
            if (cargoTypes != null)
            {
                foreach (var pair in cargoTypes)
                {
                    result[pair.Key] = CreateDefaultEconomySemantics(pair.Key, pair.Value);
                }
            }

            if (resources == null)
            {
                return result;
            }

            foreach (var resource in resources.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Commodity)))
            {
                var commodity = Normalize(resource.Commodity);
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                CommodityEconomySemantics semantics;
                if (!result.TryGetValue(commodity, out semantics) || semantics == null)
                {
                    semantics = CreateDefaultEconomySemantics(commodity, resource.CargoType);
                    result[commodity] = semantics;
                }

                if (resource.EconomySemantics != null)
                {
                    ApplyConfiguredSemantics(semantics, resource.EconomySemantics);
                }
            }

            return result;
        }

        private static CommodityEconomySemantics CreateDefaultEconomySemantics(string commodity, VehicleCargoType cargoType)
        {
            var sinkElasticity = 0.55f;
            var scarcitySensitivity = 1f;
            var sinkPreferenceWeight = 1f;
            var eventResponseAffinity = 0.75f;
            var volatility = 1f;
            var perishability = 0f;

            switch (cargoType)
            {
                case VehicleCargoType.Liquid:
                    sinkElasticity = 0.42f;
                    scarcitySensitivity = 1.12f;
                    sinkPreferenceWeight = 1.08f;
                    eventResponseAffinity = 0.88f;
                    volatility = 1.05f;
                    break;
                case VehicleCargoType.DryBulk:
                case VehicleCargoType.Aggregates:
                case VehicleCargoType.OpenHull:
                case VehicleCargoType.Wood:
                    sinkElasticity = 0.48f;
                    scarcitySensitivity = 1.04f;
                    sinkPreferenceWeight = 1.02f;
                    eventResponseAffinity = 0.84f;
                    volatility = 0.96f;
                    break;
                case VehicleCargoType.Refrigeration:
                    sinkElasticity = 0.32f;
                    scarcitySensitivity = 1.16f;
                    sinkPreferenceWeight = 1.12f;
                    eventResponseAffinity = 0.94f;
                    volatility = 1.12f;
                    perishability = 0.85f;
                    break;
                case VehicleCargoType.Vehicles:
                    sinkElasticity = 0.70f;
                    scarcitySensitivity = 1.18f;
                    sinkPreferenceWeight = 0.86f;
                    eventResponseAffinity = 0.72f;
                    volatility = 1.18f;
                    break;
                case VehicleCargoType.CraftedGoods:
                    sinkElasticity = 0.62f;
                    scarcitySensitivity = 0.96f;
                    sinkPreferenceWeight = 1f;
                    eventResponseAffinity = 0.82f;
                    volatility = 1.08f;
                    break;
            }

            return CommodityEconomySemantics.CreateResolved(
                sinkElasticity,
                scarcitySensitivity,
                sinkPreferenceWeight,
                eventResponseAffinity,
                volatility,
                perishability,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<KeyValuePair<string, float>>());
        }

        private static void ApplyConfiguredSemantics(CommodityEconomySemantics target, CommodityEconomySemantics source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(source.SubstituteFamily))
            {
                target.SubstituteFamily = source.SubstituteFamily.Trim();
            }

            if (source.DemandClasses != null && source.DemandClasses.Count > 0)
            {
                target.DemandClasses = new HashSet<string>(source.DemandClasses, StringComparer.OrdinalIgnoreCase);
            }

            if (source.Substitutes != null)
            {
                foreach (var pair in source.Substitutes)
                {
                    var commodity = Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity) || pair.Value <= 0f)
                    {
                        continue;
                    }

                    target.Substitutes[commodity] = Math.Max(0f, Math.Min(1f, pair.Value));
                }
            }

            if (source.SinkElasticity >= 0f)
            {
                target.SinkElasticity = Math.Max(0.01f, source.SinkElasticity);
            }

            if (source.ScarcitySensitivity >= 0f)
            {
                target.ScarcitySensitivity = Math.Max(0.01f, source.ScarcitySensitivity);
            }

            if (source.SinkPreferenceWeight >= 0f)
            {
                target.SinkPreferenceWeight = Math.Max(0.01f, source.SinkPreferenceWeight);
            }

            if (source.EventResponseAffinity >= 0f)
            {
                target.EventResponseAffinity = Math.Max(0.01f, source.EventResponseAffinity);
            }

            if (source.Volatility >= 0f)
            {
                target.Volatility = Math.Max(0.01f, source.Volatility);
            }

            if (source.Perishability >= 0f)
            {
                target.Perishability = Math.Max(0f, Math.Min(1f, source.Perishability));
            }
        }

        private static Dictionary<string, VehicleCargoType> CreateDefaultCargoTypeMap()
        {
            return new Dictionary<string, VehicleCargoType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Coal", VehicleCargoType.Aggregates },
                { "Gravel", VehicleCargoType.Aggregates },
                { "Ore", VehicleCargoType.Aggregates },
                { "Crops", VehicleCargoType.Aggregates },
                { "Bricks", VehicleCargoType.OpenHull },
                { "Lumber", VehicleCargoType.OpenHull },
                { "Steel", VehicleCargoType.OpenHull },
                { "Oil", VehicleCargoType.Liquid },
                { "Fuel", VehicleCargoType.Liquid },
                { "LiquidFertilizer", VehicleCargoType.Liquid },
                { "Water", VehicleCargoType.Liquid },
                { "Asphalt", VehicleCargoType.DryBulk },
                { "Cement", VehicleCargoType.DryBulk },
                { "Concrete", VehicleCargoType.DryBulk },
                { "Livestock", VehicleCargoType.CraftedGoods },
                { "Alcohol", VehicleCargoType.CraftedGoods },
                { "Chemicals", VehicleCargoType.CraftedGoods },
                { "Clothes", VehicleCargoType.CraftedGoods },
                { "Fabric", VehicleCargoType.CraftedGoods },
                { "Food", VehicleCargoType.CraftedGoods },
                { "ProcessedFood", VehicleCargoType.CraftedGoods },
                { "Plastic", VehicleCargoType.CraftedGoods },
                { "Paper", VehicleCargoType.CraftedGoods },
                { "Medicine", VehicleCargoType.CraftedGoods },
                { "MechanicalParts", VehicleCargoType.CraftedGoods },
                { "Omega", VehicleCargoType.CraftedGoods },
                { "TV", VehicleCargoType.CraftedGoods },
                { "Computer", VehicleCargoType.CraftedGoods },
                { "Electronic", VehicleCargoType.CraftedGoods },
                { "Furniture", VehicleCargoType.CraftedGoods },
                { "Alloy", VehicleCargoType.OpenHull },
                { "Metal", VehicleCargoType.OpenHull },
                { "Beam", VehicleCargoType.OpenHull },
                { "Wood", VehicleCargoType.Wood },
                { "Meat", VehicleCargoType.Refrigeration },
                { "Recyclable", VehicleCargoType.Recyclable },
                { "HeavyMachinery", VehicleCargoType.Vehicles },
                { "Vehicles", VehicleCargoType.Vehicles },
            };
        }
    }
}

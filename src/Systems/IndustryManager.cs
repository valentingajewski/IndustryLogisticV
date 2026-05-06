using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class IndustryManager
    {
        private const float NearestIndustryCellSize = 160f;

        private readonly List<Industry> _industries;
        private readonly Dictionary<long, List<Industry>> _industriesBySpatialCell;
        private readonly Dictionary<string, IndustryConfig> _baseIndustryConfigs;
        private readonly Dictionary<string, IndustryConfig> _defaultIndustryConfigs;
        private readonly Dictionary<string, float> _petrolStationDrainRatePerMinuteByIndustryId;
        private readonly float _industryOmegaCapacityMultiplier;
        private EconomyDifficultyPreset _economyPreset;
        private bool _industryPricingDifficultyEnabled;
        private bool _licensingDifficultyEnabled;

        public IndustryManager(ModConfig config)
        {
            _industries = new List<Industry>();
            _industriesBySpatialCell = new Dictionary<long, List<Industry>>();
            _baseIndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase);
            _defaultIndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase);
            _petrolStationDrainRatePerMinuteByIndustryId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _industryOmegaCapacityMultiplier = config == null ? 0.2f : config.IndustryOmegaCapacityMultiplier;
            _economyPreset = EconomyDifficultyPreset.Standard;
            foreach (var pair in config.IndustryConfigs)
            {
                var industryConfig = pair.Value;
                var baseConfig = CloneIndustryConfig(industryConfig);
                if (baseConfig == null || string.IsNullOrWhiteSpace(baseConfig.Id))
                {
                    continue;
                }

                _baseIndustryConfigs[baseConfig.Id] = baseConfig;

                var runtimeConfig = BuildEffectiveIndustryConfig(baseConfig, _economyPreset);
                _defaultIndustryConfigs[runtimeConfig.Id] = CloneIndustryConfig(runtimeConfig);

                var supportsOmegaBoost = ShouldUseOmegaBoost(runtimeConfig);
                var recipes = RecipeRegistry.BuildRecipes(runtimeConfig, supportsOmegaBoost);
                var industry = new Industry(runtimeConfig, recipes, supportsOmegaBoost, config.IndustryOmegaCapacityMultiplier);
                SeedInitialOutput(industry);
                SeedStartingTank(industry, runtimeConfig);
                _industries.Add(industry);
                AddIndustryToSpatialIndex(industry);

                float drainRatePerMinute;
                if (TryGetPetrolStationDrainRatePerMinute(runtimeConfig, out drainRatePerMinute))
                {
                    _petrolStationDrainRatePerMinuteByIndustryId[runtimeConfig.Id] = drainRatePerMinute;
                }
            }
        }

        public IReadOnlyList<Industry> Industries
        {
            get { return _industries; }
        }

        public bool IndustryPricingDifficultyEnabled
        {
            get { return _industryPricingDifficultyEnabled; }
        }

        public bool LicensingDifficultyEnabled
        {
            get { return _licensingDifficultyEnabled; }
        }

        public void SetIndustryPricingDifficultyEnabled(bool enabled)
        {
            _industryPricingDifficultyEnabled = enabled;
        }

        public void SetLicensingDifficultyEnabled(bool enabled)
        {
            _licensingDifficultyEnabled = enabled;
        }

        public void SetEconomyDifficultyPreset(EconomyDifficultyPreset preset)
        {
            _economyPreset = preset;

            _defaultIndustryConfigs.Clear();
            foreach (var pair in _baseIndustryConfigs)
            {
                var effectiveConfig = BuildEffectiveIndustryConfig(pair.Value, preset);
                _defaultIndustryConfigs[pair.Key] = CloneIndustryConfig(effectiveConfig);
            }

            for (int i = 0; i < _industries.Count; i++)
            {
                var industry = _industries[i];
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                IndustryConfig effectiveConfig;
                if (!_defaultIndustryConfigs.TryGetValue(industry.Id, out effectiveConfig) || effectiveConfig == null)
                {
                    continue;
                }

                var liveBuffers = new Dictionary<string, float>(industry.BufferStorage, StringComparer.OrdinalIgnoreCase);
                var productionRate = ApplyProductionModuleLevels(effectiveConfig.ProductionRate, industry.ProductionModuleLevel);
                var inputCapacityTons = ApplyStorageModuleLevels(effectiveConfig.InputCapacityTons, industry.InputStorageModuleLevel);
                var outputCapacityTons = ApplyStorageModuleLevels(effectiveConfig.OutputCapacityTons, industry.OutputStorageModuleLevel);
                var omegaCapacityTons = ApplyOmegaStorageModuleLevels(
                    Math.Max(1f, effectiveConfig.InputCapacityTons * Math.Max(0.01f, _industryOmegaCapacityMultiplier)),
                    industry.OmegaStorageModuleLevel);

                industry.ApplyPersistentState(
                    liveBuffers,
                    industry.OmegaStorage,
                    productionRate,
                    inputCapacityTons,
                    outputCapacityTons,
                    omegaCapacityTons,
                    industry.ProductionModuleLevel,
                    industry.InputStorageModuleLevel,
                    industry.OutputStorageModuleLevel,
                    industry.OmegaStorageModuleLevel,
                    industry.IsOwned,
                    industry.HasContractorPermit,
                    effectiveConfig.IndustryPrice,
                    effectiveConfig.IndustryLicencePrice);
            }
        }

        public bool IsIndustryOwnedForGameplay(Industry industry)
        {
            if (industry == null)
            {
                return false;
            }

            return !_industryPricingDifficultyEnabled || !industry.RequiresPurchase || industry.IsOwned;
        }

        public bool RequiresIndustryPurchase(Industry industry)
        {
            return industry != null && _industryPricingDifficultyEnabled && industry.RequiresPurchase && !industry.IsOwned;
        }

        public bool HasContractorPermitForGameplay(Industry industry)
        {
            if (industry == null)
            {
                return false;
            }

            return !_licensingDifficultyEnabled || !industry.RequiresContractorPermit || industry.HasContractorPermit;
        }

        public bool RequiresContractorPermit(Industry industry)
        {
            return industry != null && _licensingDifficultyEnabled && industry.RequiresContractorPermit && !industry.HasContractorPermit;
        }

        public void ResetIndustriesToDefaults()
        {
            for (int i = 0; i < _industries.Count; i++)
            {
                var industry = _industries[i];
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                IndustryConfig defaultConfig;
                if (!_defaultIndustryConfigs.TryGetValue(industry.Id, out defaultConfig) || defaultConfig == null)
                {
                    continue;
                }

                var emptyBuffers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in industry.Inputs)
                {
                    emptyBuffers[input] = 0f;
                }

                foreach (var output in industry.Outputs)
                {
                    emptyBuffers[output] = 0f;
                }

                var omegaCapacityTons = Math.Max(1f, defaultConfig.InputCapacityTons * Math.Max(0.01f, _industryOmegaCapacityMultiplier));
                industry.ApplyPersistentState(
                    emptyBuffers,
                    0f,
                    defaultConfig.ProductionRate,
                    defaultConfig.InputCapacityTons,
                    defaultConfig.OutputCapacityTons,
                    omegaCapacityTons,
                    0,
                    0,
                    0,
                    0,
                    defaultConfig.IsOwned,
                    defaultConfig.HasContractorPermit,
                    defaultConfig.IndustryPrice,
                    defaultConfig.IndustryLicencePrice);

                SeedInitialOutput(industry);
                SeedStartingTank(industry, defaultConfig);
            }
        }

        public void Update(float deltaMinutes, float omegaMultiplier)
        {
            for (int i = 0; i < _industries.Count; i++)
            {
                var industry = _industries[i];
                industry.Update(deltaMinutes, omegaMultiplier);

                float drainRatePerMinute;
                if (_petrolStationDrainRatePerMinuteByIndustryId.TryGetValue(industry.Id, out drainRatePerMinute))
                {
                    DrainPetrolStationFuel(industry, deltaMinutes, drainRatePerMinute);
                }
            }
        }

        public Industry GetNearestIndustry(Vector3 position, float maxDistance)
        {
            if (maxDistance <= 0f || _industries.Count == 0)
            {
                return null;
            }

            Industry nearest = null;
            var maxDistanceSq = maxDistance * maxDistance;
            var bestSq = maxDistanceSq;
            var pos2 = new Vector2(position.X, position.Y);
            var cellRadius = Math.Max(0, (int)Math.Ceiling(maxDistance / NearestIndustryCellSize));
            var cellX = GetSpatialCellCoordinate(position.X);
            var cellY = GetSpatialCellCoordinate(position.Y);

            for (int x = cellX - cellRadius; x <= cellX + cellRadius; x++)
            {
                for (int y = cellY - cellRadius; y <= cellY + cellRadius; y++)
                {
                    List<Industry> candidates;
                    if (!_industriesBySpatialCell.TryGetValue(BuildSpatialCellKey(x, y), out candidates))
                    {
                        continue;
                    }

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        var cand2 = new Vector2(candidate.Position.X, candidate.Position.Y);
                        var distSq = pos2.DistanceToSquared(cand2);
                        if (distSq < bestSq)
                        {
                            bestSq = distSq;
                            nearest = candidate;
                        }
                    }
                }
            }

            return nearest;
        }

        private void AddIndustryToSpatialIndex(Industry industry)
        {
            if (industry == null)
            {
                return;
            }

            var key = BuildSpatialCellKey(GetSpatialCellCoordinate(industry.Position.X), GetSpatialCellCoordinate(industry.Position.Y));
            List<Industry> bucket;
            if (!_industriesBySpatialCell.TryGetValue(key, out bucket))
            {
                bucket = new List<Industry>();
                _industriesBySpatialCell[key] = bucket;
            }

            bucket.Add(industry);
        }

        private static int GetSpatialCellCoordinate(float value)
        {
            return (int)Math.Floor(value / NearestIndustryCellSize);
        }

        private static long BuildSpatialCellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        public bool TryGetLoadOffer(Industry industry, VehicleCargoType cargoType, float requestedTons, out string commodity, out float tons)
        {
            commodity = string.Empty;
            tons = 0f;
            if (industry == null || requestedTons <= 0f || industry.Outputs.Count == 0)
            {
                return false;
            }

            foreach (var output in industry.Outputs)
            {
                var typeForCommodity = CommodityCatalog.GetCargoTypeForCommodity(output);
                if (cargoType != VehicleCargoType.Unknown && cargoType != VehicleCargoType.Trailer && typeForCommodity != cargoType)
                {
                    continue;
                }

                var available = industry.GetStock(output);
                if (available <= 0.001f)
                {
                    continue;
                }

                var chosen = Math.Min(available, requestedTons);
                var removed = industry.RemoveOutput(output, chosen);
                if (removed <= 0f)
                {
                    continue;
                }

                commodity = output;
                tons = removed;
                return true;
            }

            return false;
        }

        public List<string> GetLoadableOutputs(Industry industry, VehicleCargoType cargoType)
        {
            var result = new List<string>();
            PopulateLoadableOutputs(industry, cargoType, result);
            return result;
        }

        public void PopulateLoadableOutputs(Industry industry, VehicleCargoType cargoType, List<string> result)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            if (industry == null || industry.Outputs.Count == 0)
            {
                return;
            }

            var outputs = industry.SortedOutputs;
            for (int i = 0; i < outputs.Count; i++)
            {
                var output = outputs[i];
                if (industry.GetStock(output) <= 0.001f)
                {
                    continue;
                }

                var typeForCommodity = CommodityCatalog.GetCargoTypeForCommodity(output);
                if (cargoType != VehicleCargoType.Unknown &&
                    cargoType != VehicleCargoType.Trailer &&
                    typeForCommodity != cargoType)
                {
                    continue;
                }

                result.Add(output);
            }
        }

        public bool TryLoadCommodity(Industry industry, VehicleCargoType cargoType, string commodity, float requestedTons, out float loadedTons)
        {
            loadedTons = 0f;
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || requestedTons <= 0f)
            {
                return false;
            }

            var normalized = CommodityCatalog.Normalize(commodity);
            if (!industry.Outputs.Contains(normalized))
            {
                return false;
            }

            var typeForCommodity = CommodityCatalog.GetCargoTypeForCommodity(normalized);
            if (cargoType != VehicleCargoType.Unknown &&
                cargoType != VehicleCargoType.Trailer &&
                cargoType != typeForCommodity)
            {
                return false;
            }

            var available = industry.GetStock(normalized);
            if (available <= 0.001f)
            {
                return false;
            }

            var transfer = Math.Min(available, requestedTons);
            loadedTons = industry.RemoveOutput(normalized, transfer);
            return loadedTons > 0.001f;
        }

        public bool TryUnload(Industry industry, string commodity, float tons, out float acceptedTons)
        {
            acceptedTons = 0f;
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || tons <= 0f)
            {
                return false;
            }

            acceptedTons = industry.AddInput(commodity, tons);
            return acceptedTons > 0f;
        }

        public float ComputeDeliveryProfit(Industry industry, string commodity, float deliveredTons, GlobalMarketManager market, int gameTimeMs)
        {
            if (industry == null || deliveredTons <= 0f)
            {
                return 0f;
            }

            var unitPrice = market.GetUnitPrice(commodity);
            float payout;
            if (industry.IsSink)
            {
                if (commodity.Equals("TV", StringComparison.OrdinalIgnoreCase) ||
                    commodity.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                {
                    market.RegisterDelivery(gameTimeMs);
                }

                payout = deliveredTons * unitPrice;
            }
            else
            {
                payout = deliveredTons * unitPrice * 0.15f;
            }

            if (RequiresIndustryPurchase(industry))
            {
                payout *= Math.Max(0f, 1f - Math.Max(0f, Math.Min(1f, industry.IndustryOwnerCut)));
            }

            return payout;
        }

        private static bool ShouldUseOmegaBoost(IndustryConfig config)
        {
            if (!config.Inputs.Contains("Omega"))
            {
                return false;
            }

            if (config.Id.Equals("OmegaFactory", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static void SeedInitialOutput(Industry industry)
        {
            if (industry.Outputs.Count == 0)
            {
                return;
            }

            var perOutputSeed = Math.Min(20f, industry.OutputCapacityTons / Math.Max(1, industry.Outputs.Count) * 0.35f);
            foreach (var output in industry.Outputs)
            {
                industry.SeedOutput(output, perOutputSeed);
            }
        }

        private static void SeedStartingTank(Industry industry, IndustryConfig config)
        {
            if (industry == null || config == null)
            {
                return;
            }

            var startingRatio = Math.Max(0f, Math.Min(1f, config.StartingTankRatio));
            if (startingRatio <= 0f || !industry.Inputs.Contains("Fuel"))
            {
                return;
            }

            var seedTons = industry.InputCapacityTons * startingRatio;
            if (seedTons <= 0f)
            {
                return;
            }

            industry.AddInput("Fuel", seedTons);
        }

        private static bool TryGetPetrolStationDrainRatePerMinute(IndustryConfig config, out float drainRatePerMinute)
        {
            drainRatePerMinute = 0f;
            if (config == null)
            {
                return false;
            }

            if (config.Outputs.Count != 0 || config.Inputs.Count != 1 || !config.Inputs.Contains("Fuel"))
            {
                return false;
            }

            // Ported from oil_mod density emptying rates, converted from L/s to tons/min.
            // Conversion uses 1000L ~= 1t so: tons/min = liters/second * 60 / 1000.
            const float litersPerSecondToTonsPerMinute = 0.06f;
            var density = (config.Density ?? "medium").Trim().ToLowerInvariant();
            float litersPerSecond;

            if (density == "very low" || density == "verylow")
            {
                litersPerSecond = 0.35f;
            }
            else if (density == "low")
            {
                litersPerSecond = 0.8f;
            }
            else if (density == "high")
            {
                litersPerSecond = 4.0f;
            }
            else
            {
                litersPerSecond = 2.25f;
            }

            drainRatePerMinute = litersPerSecond * litersPerSecondToTonsPerMinute;
            return drainRatePerMinute > 0f;
        }

        private static IndustryConfig CloneIndustryConfig(IndustryConfig source)
        {
            if (source == null)
            {
                return null;
            }

            return new IndustryConfig
            {
                Id = source.Id,
                LocationKind = source.LocationKind,
                Name = source.Name,
                Position = source.Position,
                VehicleSpawnPosition = source.VehicleSpawnPosition,
                VehicleSpawnHeading = source.VehicleSpawnHeading,
                Inputs = new HashSet<string>(source.Inputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(source.Outputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                FactoryProductionRatio = source.FactoryProductionRatio,
                InputCapacityTons = source.InputCapacityTons,
                OutputCapacityTons = source.OutputCapacityTons,
                ProductionRate = source.ProductionRate,
                StartingTankRatio = source.StartingTankRatio,
                Density = source.Density,
                IndustryPrice = source.IndustryPrice,
                IndustryLicencePrice = source.IndustryLicencePrice,
                IndustryOwnerCut = source.IndustryOwnerCut,
                IsOwned = source.IsOwned,
                HasContractorPermit = source.HasContractorPermit,
            };
        }

        private static IndustryConfig BuildEffectiveIndustryConfig(IndustryConfig baseConfig, EconomyDifficultyPreset preset)
        {
            var effectiveConfig = CloneIndustryConfig(baseConfig);
            if (effectiveConfig == null || effectiveConfig.LocationKind != ExternalLocationKind.Industry)
            {
                return effectiveConfig;
            }

            var presetValues = GetEconomyPresetValues(preset);
            var factoryRatio = Math.Max(0.1f, effectiveConfig.FactoryProductionRatio <= 0f ? 1f : effectiveConfig.FactoryProductionRatio);
            var productionMultiplier = ResolveFactoryProductionMultiplier(effectiveConfig.Id, preset);

            effectiveConfig.ProductionRate = Math.Max(1f, presetValues.IndustryProductionRate * factoryRatio * productionMultiplier);
            effectiveConfig.InputCapacityTons = ConvertRawCapacityToTons(presetValues.IndustryInputCapacityRaw);
            effectiveConfig.OutputCapacityTons = ConvertRawCapacityToTons(presetValues.IndustryOutputCapacityRaw);
            effectiveConfig.IndustryPrice = presetValues.IndustryPrice;
            effectiveConfig.IndustryLicencePrice = presetValues.IndustryLicencePrice;
            effectiveConfig.IsOwned = effectiveConfig.IndustryPrice <= 0f;
            effectiveConfig.HasContractorPermit = effectiveConfig.IndustryLicencePrice <= 0f;
            return effectiveConfig;
        }

        private static EconomyPresetValues GetEconomyPresetValues(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return new EconomyPresetValues(40f, 8000f, 200000f, 180000f, 150000f);
                case EconomyDifficultyPreset.Hardcore:
                    return new EconomyPresetValues(24f, 18000f, 800000f, 80000f, 70000f);
                default:
                    return new EconomyPresetValues(32f, 13000f, 450000f, 120000f, 100000f);
            }
        }

        private static float ResolveFactoryProductionMultiplier(string industryId, EconomyDifficultyPreset preset)
        {
            if (string.Equals(industryId, "OmegaFactory", StringComparison.OrdinalIgnoreCase))
            {
                switch (preset)
                {
                    case EconomyDifficultyPreset.Casual:
                        return 1.75f;
                    case EconomyDifficultyPreset.Hardcore:
                        return 1.25f;
                    default:
                        return 1.5f;
                }
            }

            if (string.Equals(industryId, "RecyclingCenter", StringComparison.OrdinalIgnoreCase))
            {
                switch (preset)
                {
                    case EconomyDifficultyPreset.Casual:
                        return 3f;
                    case EconomyDifficultyPreset.Hardcore:
                        return 2f;
                    default:
                        return 2.5f;
                }
            }

            return 1f;
        }

        private static float ConvertRawCapacityToTons(float rawCapacity)
        {
            return Math.Max(1f, rawCapacity / 1000f);
        }

        private static float ApplyProductionModuleLevels(float productionRate, int moduleLevel)
        {
            var effectiveProductionRate = Math.Max(1f, productionRate);
            for (var level = 0; level < Math.Max(0, moduleLevel); level++)
            {
                effectiveProductionRate += Math.Max(2f, effectiveProductionRate * 0.12f);
            }

            return effectiveProductionRate;
        }

        private static float ApplyStorageModuleLevels(float capacityTons, int moduleLevel)
        {
            var effectiveCapacityTons = Math.Max(1f, capacityTons);
            for (var level = 0; level < Math.Max(0, moduleLevel); level++)
            {
                effectiveCapacityTons += Math.Max(5f, effectiveCapacityTons * 0.18f);
            }

            return effectiveCapacityTons;
        }

        private static float ApplyOmegaStorageModuleLevels(float omegaCapacityTons, int moduleLevel)
        {
            var effectiveOmegaCapacityTons = Math.Max(1f, omegaCapacityTons);
            for (var level = 0; level < Math.Max(0, moduleLevel); level++)
            {
                effectiveOmegaCapacityTons += Math.Max(2f, effectiveOmegaCapacityTons * 0.20f);
            }

            return effectiveOmegaCapacityTons;
        }

        private static void DrainPetrolStationFuel(Industry industry, float deltaMinutes, float drainRatePerMinute)
        {
            if (industry == null || deltaMinutes <= 0f || drainRatePerMinute <= 0f)
            {
                return;
            }

            var currentFuel = Math.Max(0f, industry.GetStock("Fuel"));
            if (currentFuel <= 0.0001f)
            {
                return;
            }

            var consumed = drainRatePerMinute * deltaMinutes;
            if (consumed <= 0f)
            {
                return;
            }

            industry.BufferStorage["Fuel"] = Math.Max(0f, currentFuel - consumed);
        }

    }

    public enum EconomyDifficultyPreset
    {
        Casual = 0,
        Standard = 1,
        Hardcore = 2,
    }

    internal struct EconomyPresetValues
    {
        public EconomyPresetValues(
            float industryProductionRate,
            float industryLicencePrice,
            float industryPrice,
            float industryInputCapacityRaw,
            float industryOutputCapacityRaw)
        {
            IndustryProductionRate = industryProductionRate;
            IndustryLicencePrice = industryLicencePrice;
            IndustryPrice = industryPrice;
            IndustryInputCapacityRaw = industryInputCapacityRaw;
            IndustryOutputCapacityRaw = industryOutputCapacityRaw;
        }

        public float IndustryProductionRate { get; }
        public float IndustryLicencePrice { get; }
        public float IndustryPrice { get; }
        public float IndustryInputCapacityRaw { get; }
        public float IndustryOutputCapacityRaw { get; }
    }
}

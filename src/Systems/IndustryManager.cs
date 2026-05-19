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
        private const float NonSinkDeliveryPayoutMultiplier = 0.35f;
        private const float MinimumWarehouseStorageCondition = 0.55f;
        private const float WarehouseLossStatusThreshold = 1500f;

        private static readonly HashSet<string> SpoilageSensitiveCommodities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProcessedFood",
            "Meat",
            "Medicine",
            "Alcohol",
        };

        private static readonly HashSet<string> SecuritySensitiveCommodities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Electronic",
            "TV",
            "Computer",
            "Omega",
            "Vehicles",
            "MechanicalParts",
        };

        private readonly List<Industry> _industries;
        private readonly Dictionary<long, List<Industry>> _industriesBySpatialCell;
        private readonly Dictionary<string, IndustryConfig> _baseIndustryConfigs;
        private readonly Dictionary<string, IndustryConfig> _defaultIndustryConfigs;
        private readonly Dictionary<string, float> _sinkDrainRatePerMinuteByIndustryId;
        private readonly float _industryOmegaCapacityMultiplier;
        private GlobalMarketManager _globalMarket;
        private TerritoryManager _territoryManager;
        private CompanyFinanceTracker _financeTracker;
        private Func<int> _getCurrentInGameMinute;
        private Action<string> _notifyStoragePressure;
        private EconomyDifficultyPreset _economyPreset;
        private bool _industryPricingDifficultyEnabled;
        private bool _licensingDifficultyEnabled;

        public IndustryManager(ModConfig config)
        {
            _industries = new List<Industry>();
            _industriesBySpatialCell = new Dictionary<long, List<Industry>>();
            _baseIndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase);
            _defaultIndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase);
            _sinkDrainRatePerMinuteByIndustryId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
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
                var recipes = BuildRecipes(runtimeConfig, supportsOmegaBoost);
                var industry = new Industry(runtimeConfig, recipes, supportsOmegaBoost, config.IndustryOmegaCapacityMultiplier);
                SeedIndustryStartingState(industry, runtimeConfig);
                _industries.Add(industry);
                AddIndustryToSpatialIndex(industry);

                float drainRatePerMinute;
                if (TryGetSinkDrainRatePerMinute(runtimeConfig, out drainRatePerMinute))
                {
                    _sinkDrainRatePerMinuteByIndustryId[runtimeConfig.Id] = drainRatePerMinute;
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

        public void ConfigureMarketPressure(GlobalMarketManager globalMarket)
        {
            _globalMarket = globalMarket;
        }

        public void ConfigureStoragePressure(TerritoryManager territoryManager, CompanyFinanceTracker financeTracker, Func<int> getCurrentInGameMinute, Action<string> notifyStoragePressure)
        {
            _territoryManager = territoryManager;
            _financeTracker = financeTracker;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _notifyStoragePressure = notifyStoragePressure;
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

            _sinkDrainRatePerMinuteByIndustryId.Clear();

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

                float drainRatePerMinute;
                if (TryGetSinkDrainRatePerMinute(effectiveConfig, out drainRatePerMinute))
                {
                    _sinkDrainRatePerMinuteByIndustryId[industry.Id] = drainRatePerMinute;
                }

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
                    effectiveConfig.IndustryLicencePrice,
                    effectiveConfig.EmptyingRate,
                    effectiveConfig.IndustryOwnerCut,
                    effectiveConfig.DeliveryPayoutMultiplier,
                    effectiveConfig.WeeklyPassiveIncome);
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

                foreach (var optionalInput in industry.OptionalInputs)
                {
                    emptyBuffers[optionalInput] = 0f;
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
                    defaultConfig.IndustryLicencePrice,
                    defaultConfig.EmptyingRate,
                    defaultConfig.IndustryOwnerCut,
                    defaultConfig.DeliveryPayoutMultiplier,
                    defaultConfig.WeeklyPassiveIncome);
                industry.ApplyStoragePressureState(1f, -1, 0f, 0f);

                SeedIndustryStartingState(industry, defaultConfig);
            }
        }

        public void Update(float deltaMinutes, float omegaMultiplier)
        {
            var currentDayIndex = _getCurrentInGameMinute != null
                ? GetDayIndex(_getCurrentInGameMinute())
                : -1;

            for (int i = 0; i < _industries.Count; i++)
            {
                var industry = _industries[i];
                industry.Update(deltaMinutes, omegaMultiplier);

                float drainRatePerMinute;
                if (_sinkDrainRatePerMinuteByIndustryId.TryGetValue(industry.Id, out drainRatePerMinute))
                {
                    DrainSinkStock(industry, deltaMinutes, drainRatePerMinute);
                }

                if (currentDayIndex >= 0)
                {
                    ProcessWarehouseStoragePressure(industry, currentDayIndex);
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

        public bool TryDispenseFuel(Industry industry, float requestedLiters, out float dispensedLiters)
        {
            dispensedLiters = 0f;
            if (industry == null || requestedLiters <= 0f || !industry.AcceptsCommodity("Fuel"))
            {
                return false;
            }

            var removedTons = industry.RemoveInput("Fuel", requestedLiters / 1000f);
            dispensedLiters = Math.Max(0f, removedTons * 1000f);
            return dispensedLiters > 0.01f;
        }

        public float ComputeFuelRefillPrice(float liters, GlobalMarketManager market)
        {
            if (liters <= 0f || market == null)
            {
                return 0f;
            }

            return Math.Max(0f, market.GetUnitPrice("Fuel")) * (liters / 1000f);
        }

        public float ComputeDeliveryProfit(Industry industry, string commodity, float deliveredTons, GlobalMarketManager market, int gameTimeMs)
        {
            if (industry == null || deliveredTons <= 0f)
            {
                return 0f;
            }

            if (industry.IsWarehouse)
            {
                return 0f;
            }

            commodity = CommodityCatalog.Normalize(commodity);
            var unitPrice = market.GetUnitPrice(commodity);
            float payout;
            if (industry.IsSink)
            {
                if (market != null)
                {
                    market.RegisterDelivery(commodity, gameTimeMs);
                }

                payout = deliveredTons * unitPrice;
            }
            else
            {
                payout = deliveredTons * unitPrice * NonSinkDeliveryPayoutMultiplier;
            }

            payout *= Math.Max(0f, industry.DeliveryPayoutMultiplier <= 0f ? 1f : industry.DeliveryPayoutMultiplier);

            if (RequiresIndustryPurchase(industry))
            {
                payout *= Math.Max(0f, 1f - Math.Max(0f, Math.Min(1f, industry.IndustryOwnerCut)));
            }

            return payout;
        }

        private static bool ShouldUseOmegaBoost(IndustryConfig config)
        {
            if (config == null)
            {
                return false;
            }

            var usesOmegaBoost = config.Inputs.Contains("Omega")
                || (config.BoostInputs != null && config.BoostInputs.Contains("Omega"))
                || ((config.BoostInputs == null || config.BoostInputs.Count == 0)
                    && config.OptionalInputs != null
                    && config.OptionalInputs.Contains("Omega"));
            if (!usesOmegaBoost)
            {
                return false;
            }

            if (config.Id.Equals("OmegaFactory", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static void SeedIndustryStartingState(Industry industry, IndustryConfig config)
        {
            if (industry == null || config == null)
            {
                return;
            }

            var startingRatio = Math.Max(0f, Math.Min(1f, config.StartingTankRatio));
            if (!config.IsCsvBacked)
            {
                SeedLegacyIndustryStartingState(industry, startingRatio);
                return;
            }

            if (startingRatio <= 0f)
            {
                return;
            }

            if (industry.Inputs.Count == 0 && industry.OptionalInputs.Count == 0 && industry.Outputs.Count > 0)
            {
                var perOutputSeed = industry.OutputCapacityTons * startingRatio / Math.Max(1, industry.Outputs.Count);
                foreach (var output in industry.Outputs)
                {
                    industry.SeedOutput(output, perOutputSeed);
                }

                return;
            }

            var acceptedInputs = industry.Inputs
                .Concat(industry.OptionalInputs)
                .Concat(industry.BoostInputs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (acceptedInputs.Count > 0)
            {
                var perInputSeed = industry.InputCapacityTons * startingRatio / acceptedInputs.Count;
                for (int i = 0; i < acceptedInputs.Count; i++)
                {
                    industry.AddInput(acceptedInputs[i], perInputSeed);
                }
            }

            if (industry.Outputs.Count > 0 && industry.Inputs.Count > 0)
            {
                var perOutputSeed = Math.Min(
                    industry.OutputCapacityTons * 0.15f / Math.Max(1, industry.Outputs.Count),
                    industry.OutputCapacityTons / Math.Max(1, industry.Outputs.Count));
                if (perOutputSeed > 0f)
                {
                    foreach (var output in industry.Outputs)
                    {
                        industry.SeedOutput(output, perOutputSeed);
                    }
                }
            }
        }

        private static void SeedLegacyIndustryStartingState(Industry industry, float startingRatio)
        {
            if (industry.Outputs.Count > 0)
            {
                var perOutputSeed = Math.Min(20f, industry.OutputCapacityTons / Math.Max(1, industry.Outputs.Count) * 0.35f);
                foreach (var output in industry.Outputs)
                {
                    industry.SeedOutput(output, perOutputSeed);
                }
            }

            if (startingRatio <= 0f || !industry.AcceptsCommodity("Fuel"))
            {
                return;
            }

            var seedTons = industry.InputCapacityTons * startingRatio;
            if (seedTons > 0f)
            {
                industry.AddInput("Fuel", seedTons);
            }
        }

        private static bool TryGetSinkDrainRatePerMinute(IndustryConfig config, out float drainRatePerMinute)
        {
            drainRatePerMinute = 0f;
            if (config == null)
            {
                return false;
            }

            if (config.Outputs.Count != 0
                || (config.Inputs.Count == 0
                    && config.OptionalInputs.Count == 0
                    && (config.BoostInputs == null || config.BoostInputs.Count == 0)))
            {
                return false;
            }

            var configuredDrainRate = config.HasConfiguredEmptyingRate
                ? Math.Max(0f, config.EmptyingRate)
                : ResolveLegacyDensityDrainRate(config.Density);

            // CSV sink rates are authored directly as tons/min so service sinks drain visibly.
            // Legacy INI density values keep the previous liters/second semantics.
            const float litersPerSecondToTonsPerMinute = 0.06f;
            drainRatePerMinute = config.IsCsvBacked
                ? configuredDrainRate
                : configuredDrainRate * litersPerSecondToTonsPerMinute;

            return drainRatePerMinute > 0f;
        }

        private static float ResolveLegacyDensityDrainRate(string density)
        {
            var normalized = (density ?? "medium").Trim().ToLowerInvariant();
            if (normalized == "very low" || normalized == "verylow")
            {
                return 0.35f;
            }

            if (normalized == "low")
            {
                return 0.8f;
            }

            if (normalized == "high")
            {
                return 4f;
            }

            return 2.25f;
        }

        private static List<ProductionRecipe> BuildRecipes(IndustryConfig config, bool supportsOmegaBoost)
        {
            var recipes = new List<ProductionRecipe>();
            if (config == null || config.SiteRole == SiteRole.Warehouse || config.Outputs == null || config.Outputs.Count == 0)
            {
                return recipes;
            }

            var boostInputs = GetEffectiveBoostInputs(config);
            var weightedInputs = ClonePositiveWeights(config.RecipeInputWeights);
            if (weightedInputs.Count == 0)
            {
                var fallbackInputs = config.Inputs
                    .Concat(config.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var input in fallbackInputs)
                {
                    if (boostInputs.Contains(input))
                    {
                        continue;
                    }

                    if (supportsOmegaBoost && input.Equals("Omega", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    weightedInputs[input] = 1f;
                }
            }
            else
            {
                foreach (var boostInput in boostInputs)
                {
                    weightedInputs.Remove(boostInput);
                }

                if (supportsOmegaBoost)
                {
                    weightedInputs.Remove("Omega");
                }
            }

            var weightedOutputs = ClonePositiveWeights(config.RecipeOutputWeights);
            if (weightedOutputs.Count == 0)
            {
                foreach (var output in config.Outputs)
                {
                    weightedOutputs[output] = 1f;
                }
            }

            if (weightedInputs.Count == 0)
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    weightedOutputs));
                return recipes;
            }

            recipes.Add(new ProductionRecipe(weightedInputs, weightedOutputs));
            return recipes;
        }

        private static IndustryConfig CloneIndustryConfig(IndustryConfig source)
        {
            if (source == null)
            {
                return null;
            }

            return new IndustryConfig
            {
                CatalogId = source.CatalogId,
                Id = source.Id,
                LegacyKey = source.LegacyKey,
                LocationKind = source.LocationKind,
                SiteRole = source.SiteRole,
                OwnershipTier = source.OwnershipTier,
                DistrictName = source.DistrictName,
                Name = source.Name,
                Company = source.Company,
                Position = source.Position,
                VehicleSpawnPosition = source.VehicleSpawnPosition,
                VehicleSpawnHeading = source.VehicleSpawnHeading,
                SpawnedVehiclePosition = source.SpawnedVehiclePosition,
                SpawnedVehicleHeading = source.SpawnedVehicleHeading,
                GatePosition = source.GatePosition,
                BarrierModelHash = source.BarrierModelHash,
                WorkerPosition = source.WorkerPosition,
                DisplayObjectModelHash = source.DisplayObjectModelHash,
                DisplayObjectsAtGroundLevel = source.DisplayObjectsAtGroundLevel,
                MaxDisplayObjectLine = source.MaxDisplayObjectLine,
                MaxDisplayObjectRow = source.MaxDisplayObjectRow,
                ObjectToDeleteModelHashes = source.ObjectToDeleteModelHashes != null ? new List<int>(source.ObjectToDeleteModelHashes) : new List<int>(),
                Inputs = new HashSet<string>(source.Inputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                OptionalInputs = new HashSet<string>(source.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                BoostInputs = new HashSet<string>(source.BoostInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                Outputs = new HashSet<string>(source.Outputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                RecipeInputWeights = ClonePositiveWeights(source.RecipeInputWeights),
                RecipeOutputWeights = ClonePositiveWeights(source.RecipeOutputWeights),
                InputCapacityWeights = ClonePositiveWeights(source.InputCapacityWeights),
                OutputCapacityWeights = ClonePositiveWeights(source.OutputCapacityWeights),
                FactoryProductionRatio = source.FactoryProductionRatio,
                InputCapacityTons = source.InputCapacityTons,
                OutputCapacityTons = source.OutputCapacityTons,
                ProductionRate = source.ProductionRate,
                StartingTankRatio = source.StartingTankRatio,
                Density = source.Density,
                EmptyingRate = source.EmptyingRate,
                WeeklyPassiveIncome = source.WeeklyPassiveIncome,
                HasConfiguredEmptyingRate = source.HasConfiguredEmptyingRate,
                RefuelIsFree = source.RefuelIsFree,
                IndustryPrice = source.IndustryPrice,
                IndustryLicencePrice = source.IndustryLicencePrice,
                IndustryOwnerCut = source.IndustryOwnerCut,
                DeliveryPayoutMultiplier = source.DeliveryPayoutMultiplier,
                IsOwned = source.IsOwned,
                HasContractorPermit = source.HasContractorPermit,
                IsCsvBacked = source.IsCsvBacked,
                CasualEconomy = CloneSiteEconomy(source.CasualEconomy),
                StandardEconomy = CloneSiteEconomy(source.StandardEconomy),
                HardcoreEconomy = CloneSiteEconomy(source.HardcoreEconomy),
            };
        }

        private static IndustryConfig BuildEffectiveIndustryConfig(IndustryConfig baseConfig, EconomyDifficultyPreset preset)
        {
            var effectiveConfig = CloneIndustryConfig(baseConfig);
            if (effectiveConfig == null)
            {
                return effectiveConfig;
            }

            if (effectiveConfig.IsCsvBacked)
            {
                var sitePreset = ResolveAuthoredSitePresetValues(effectiveConfig, preset);
                if (sitePreset == null)
                {
                    sitePreset = SiteEconomyPresetValues.Create(0f, 0f, 0f, effectiveConfig.InputCapacityTons, effectiveConfig.OutputCapacityTons, effectiveConfig.FactoryProductionRatio, false);
                }

                var productionRatio = sitePreset.ProductionRatio > 0f
                    ? sitePreset.ProductionRatio
                    : ResolveFactoryProductionMultiplier(effectiveConfig.LegacyKey ?? effectiveConfig.Id, preset);

                effectiveConfig.FactoryProductionRatio = Math.Max(0.1f, productionRatio <= 0f ? 1f : productionRatio);
                if (effectiveConfig.Outputs.Count > 0)
                {
                    effectiveConfig.ProductionRate = sitePreset.ProductionRate > 0f
                        ? Math.Max(1f, sitePreset.ProductionRate * effectiveConfig.FactoryProductionRatio)
                        : effectiveConfig.ProductionRate;
                }

                effectiveConfig.InputCapacityTons = Math.Max(1f, sitePreset.InputCapacityTons > 0f ? sitePreset.InputCapacityTons : effectiveConfig.InputCapacityTons);
                effectiveConfig.OutputCapacityTons = effectiveConfig.Outputs.Count == 0
                    ? Math.Max(1f, sitePreset.InputCapacityTons > 0f ? sitePreset.InputCapacityTons : effectiveConfig.OutputCapacityTons)
                    : Math.Max(1f, sitePreset.OutputCapacityTons > 0f ? sitePreset.OutputCapacityTons : effectiveConfig.OutputCapacityTons);
                effectiveConfig.IndustryPrice = Math.Max(0f, sitePreset.PurchasePrice);
                effectiveConfig.IndustryLicencePrice = sitePreset.PermitRequired ? Math.Max(0f, sitePreset.LicencePrice) : 0f;
                effectiveConfig.EmptyingRate = ApplySinkEmptyingRateForPreset(effectiveConfig, preset);
                effectiveConfig.WeeklyPassiveIncome = ApplyWeeklyPassiveIncomeForPreset(effectiveConfig, preset);
                effectiveConfig.IndustryOwnerCut = ApplyIndustryOwnerCutForPreset(effectiveConfig.IndustryOwnerCut, preset);
                effectiveConfig.DeliveryPayoutMultiplier = Math.Max(0f, effectiveConfig.DeliveryPayoutMultiplier <= 0f ? 1f : effectiveConfig.DeliveryPayoutMultiplier);
                var hasStarterOwnership = SiteMetadataParser.GrantsStarterOwnership(effectiveConfig.SiteRole);
                var hasStarterPermitAccess = SiteMetadataParser.GrantsStarterPermitAccess(effectiveConfig.SiteRole, effectiveConfig.OwnershipTier);
                effectiveConfig.IsOwned = hasStarterOwnership || effectiveConfig.IsOwned;
                effectiveConfig.HasContractorPermit = hasStarterPermitAccess || !sitePreset.PermitRequired || effectiveConfig.IndustryLicencePrice <= 0f;
                return effectiveConfig;
            }

            var presetValues = GetEconomyPresetValues(preset);
            var factoryRatio = Math.Max(0.1f, effectiveConfig.FactoryProductionRatio <= 0f ? 1f : effectiveConfig.FactoryProductionRatio);
            var productionMultiplier = ResolveFactoryProductionMultiplier(effectiveConfig.LegacyKey ?? effectiveConfig.Id, preset);

            effectiveConfig.ProductionRate = Math.Max(1f, presetValues.IndustryProductionRate * factoryRatio * productionMultiplier);
            effectiveConfig.InputCapacityTons = ConvertRawCapacityToTons(presetValues.IndustryInputCapacityRaw);
            effectiveConfig.OutputCapacityTons = ConvertRawCapacityToTons(presetValues.IndustryOutputCapacityRaw);
            effectiveConfig.IndustryPrice = presetValues.IndustryPrice;
            effectiveConfig.IndustryLicencePrice = presetValues.IndustryLicencePrice;
            effectiveConfig.IsOwned = effectiveConfig.IndustryPrice <= 0f;
            effectiveConfig.HasContractorPermit = effectiveConfig.IndustryLicencePrice <= 0f;
            return effectiveConfig;
        }

        private static SiteEconomyPresetValues ResolveSitePresetValues(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return CloneSiteEconomy(config.CasualEconomy ?? config.StandardEconomy ?? config.HardcoreEconomy);
                case EconomyDifficultyPreset.Hardcore:
                    return CloneSiteEconomy(config.HardcoreEconomy ?? config.StandardEconomy ?? config.CasualEconomy);
                default:
                    return CloneSiteEconomy(config.StandardEconomy ?? config.CasualEconomy ?? config.HardcoreEconomy);
            }
        }

        private static SiteEconomyPresetValues ResolveAuthoredSitePresetValues(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            var authoredStandard = CloneSiteEconomy(config != null
                ? (config.StandardEconomy ?? config.CasualEconomy ?? config.HardcoreEconomy)
                : null);
            if (authoredStandard == null)
            {
                return ResolveSitePresetValues(config, preset);
            }

            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return SiteEconomyPresetValues.Create(
                        authoredStandard.ProductionRate * 1.20f,
                        authoredStandard.LicencePrice * 0.70f,
                        authoredStandard.PurchasePrice * 0.75f,
                        authoredStandard.InputCapacityTons * 1.30f,
                        authoredStandard.OutputCapacityTons * 1.30f,
                        authoredStandard.ProductionRatio,
                        authoredStandard.PermitRequired);
                case EconomyDifficultyPreset.Hardcore:
                    return SiteEconomyPresetValues.Create(
                        authoredStandard.ProductionRate * 0.85f,
                        authoredStandard.LicencePrice * 1.35f,
                        authoredStandard.PurchasePrice * 1.40f,
                        authoredStandard.InputCapacityTons * 0.80f,
                        authoredStandard.OutputCapacityTons * 0.80f,
                        authoredStandard.ProductionRatio,
                        authoredStandard.PermitRequired);
                default:
                    return authoredStandard;
            }
        }

        private static SiteEconomyPresetValues CloneSiteEconomy(SiteEconomyPresetValues source)
        {
            if (source == null)
            {
                return null;
            }

            return SiteEconomyPresetValues.Create(
                source.ProductionRate,
                source.LicencePrice,
                source.PurchasePrice,
                source.InputCapacityTons,
                source.OutputCapacityTons,
                source.ProductionRatio,
                source.PermitRequired);
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

        private static HashSet<string> GetEffectiveBoostInputs(IndustryConfig config)
        {
            if (config == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (config.BoostInputs != null && config.BoostInputs.Count > 0)
            {
                return new HashSet<string>(config.BoostInputs, StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(config.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, float> ClonePositiveWeights(IEnumerable<KeyValuePair<string, float>> source)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                var commodity = CommodityCatalog.Normalize(pair.Key);
                if (string.IsNullOrWhiteSpace(commodity) || pair.Value <= 0f)
                {
                    continue;
                }

                result[commodity] = pair.Value;
            }

            return result;
        }

        private static float ApplyIndustryOwnerCutForPreset(float ownerCut, EconomyDifficultyPreset preset)
        {
            var baseOwnerCut = Math.Max(0f, Math.Min(1f, ownerCut));
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return Math.Max(0.15f, baseOwnerCut - 0.05f);
                case EconomyDifficultyPreset.Hardcore:
                    return Math.Min(0.45f, baseOwnerCut + 0.05f);
                default:
                    return baseOwnerCut;
            }
        }

        private static float ApplySinkEmptyingRateForPreset(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            if (config == null || config.Outputs.Count != 0)
            {
                return config != null ? config.EmptyingRate : 0f;
            }

            var multiplier = 1f;
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    multiplier = 0.90f;
                    break;
                case EconomyDifficultyPreset.Hardcore:
                    multiplier = 1.15f;
                    break;
            }

            return Math.Max(0f, config.EmptyingRate * multiplier);
        }

        private static float ApplyWeeklyPassiveIncomeForPreset(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            if (config == null)
            {
                return 0f;
            }

            var multiplier = 1f;
            if (config.Outputs.Count == 0)
            {
                switch (preset)
                {
                    case EconomyDifficultyPreset.Casual:
                        multiplier = 0.90f;
                        break;
                    case EconomyDifficultyPreset.Hardcore:
                        multiplier = 1.15f;
                        break;
                }
            }

            return Math.Max(0f, config.WeeklyPassiveIncome * multiplier);
        }

        private static float ApplyProductionModuleLevels(float productionRate, int moduleLevel)
        {
            if (productionRate <= 0f)
            {
                return 0f;
            }

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

        private void DrainSinkStock(Industry industry, float deltaMinutes, float drainRatePerMinute)
        {
            if (industry == null || deltaMinutes <= 0f || drainRatePerMinute <= 0f)
            {
                return;
            }

            var acceptedInputs = industry.Inputs
                .Concat(industry.OptionalInputs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(commodity => industry.GetStock(commodity) > 0.0001f)
                .ToList();
            if (acceptedInputs.Count == 0)
            {
                return;
            }

            var consumed = drainRatePerMinute * deltaMinutes;
            if (consumed <= 0f)
            {
                return;
            }

            var totalWeight = 0f;
            var commodityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < acceptedInputs.Count; i++)
            {
                var commodity = acceptedInputs[i];
                var weight = _globalMarket != null
                    ? _globalMarket.GetSinkDemandMultiplier(industry.DistrictName, commodity)
                    : 1f;
                weight = Math.Max(0.1f, weight);
                commodityWeights[commodity] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.001f)
            {
                totalWeight = acceptedInputs.Count;
            }

            for (int i = 0; i < acceptedInputs.Count; i++)
            {
                var commodity = acceptedInputs[i];
                float weight;
                if (!commodityWeights.TryGetValue(commodity, out weight))
                {
                    weight = 1f;
                }

                var perCommodityConsumption = consumed * (weight / totalWeight);
                industry.BufferStorage[commodity] = Math.Max(0f, industry.GetStock(commodity) - perCommodityConsumption);
            }
        }

        private void ProcessWarehouseStoragePressure(Industry industry, int currentDayIndex)
        {
            if (industry == null || !industry.IsWarehouse || !IsIndustryOwnedForGameplay(industry))
            {
                return;
            }

            if (industry.LastStoragePressureDayIndex < 0)
            {
                industry.ApplyStoragePressureState(industry.StorageCondition, currentDayIndex, industry.LifetimeStorageLossTons, industry.LifetimeStorageLossValue);
                return;
            }

            var elapsedDays = currentDayIndex - industry.LastStoragePressureDayIndex;
            if (elapsedDays <= 0)
            {
                return;
            }

            var districtState = GetDistrictState(industry.DistrictName);
            var storageCondition = ClampWarehouseStorageCondition(industry.StorageCondition);
            float totalLostTons = 0f;
            float totalLostValue = 0f;

            for (int day = 0; day < elapsedDays; day++)
            {
                float totalStock = 0f;
                foreach (var pair in industry.BufferStorage)
                {
                    totalStock += Math.Max(0f, pair.Value);
                }

                var totalCapacity = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
                var fillRatio = Math.Max(0f, Math.Min(1f, totalStock / totalCapacity));
                var trackedCommodities = industry.BufferStorage.Keys
                    .Select(CommodityCatalog.Normalize)
                    .Where(commodity => IsTrackedStorageCommodity(commodity) && industry.GetStock(commodity) > 0.01f)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var stabilityScore = GetWarehouseStabilityScore(industry, districtState);
                var storagePressure = Math.Max(0f, fillRatio - 0.30f);
                var degradation = trackedCommodities.Count > 0
                    ? (0.015f + (storagePressure * 0.045f) + (districtState != null && districtState.LicenseStatus == DistrictLicenseStatus.Suspended ? 0.02f : 0f))
                        * Math.Max(0.35f, 1f - stabilityScore)
                    : 0f;
                var recovery = trackedCommodities.Count == 0 || fillRatio < 0.12f
                    ? 0.02f + Math.Min(0.04f, stabilityScore * 0.05f)
                    : 0f;
                storageCondition = ClampWarehouseStorageCondition(storageCondition - degradation + recovery);

                for (int commodityIndex = 0; commodityIndex < trackedCommodities.Count; commodityIndex++)
                {
                    var commodity = trackedCommodities[commodityIndex];
                    var currentTons = Math.Max(0f, industry.GetStock(commodity));
                    if (currentTons <= 0.01f)
                    {
                        continue;
                    }

                    var lossRatio = ComputeWarehouseLossRatio(commodity, storagePressure, storageCondition, stabilityScore);
                    var lostTons = Math.Min(currentTons, currentTons * lossRatio);
                    if (lostTons <= 0.001f)
                    {
                        continue;
                    }

                    industry.BufferStorage[commodity] = Math.Max(0f, currentTons - lostTons);
                    totalLostTons += lostTons;
                    totalLostValue += lostTons * (_globalMarket != null ? _globalMarket.GetUnitPrice(commodity) : 0f);
                }
            }

            industry.RecordStoragePressure(currentDayIndex, storageCondition, totalLostTons, totalLostValue);

            if (totalLostValue > 0.01f && _financeTracker != null)
            {
                var currentMinute = _getCurrentInGameMinute != null
                    ? Math.Max(0, _getCurrentInGameMinute())
                    : currentDayIndex * 24 * 60;
                _financeTracker.RecordExpense(
                    CompanyFinanceCategory.InventoryLoss,
                    totalLostValue,
                    currentMinute,
                    string.Format("Warehouse spoilage and shrinkage at {0}", industry.Name));
            }

            if (totalLostValue >= WarehouseLossStatusThreshold && _notifyStoragePressure != null)
            {
                _notifyStoragePressure(string.Format("{0} lost {1} to warehouse spoilage and shrinkage.", industry.Name, ModFormatting.FormatMoney(totalLostValue)));
            }
        }

        private TerritoryDistrictState GetDistrictState(string districtName)
        {
            return _territoryManager != null && _territoryManager.DistrictStates != null
                ? _territoryManager.DistrictStates.FirstOrDefault(state => state != null && string.Equals(state.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private static bool IsTrackedStorageCommodity(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            return !string.IsNullOrWhiteSpace(commodity)
                && (SpoilageSensitiveCommodities.Contains(commodity) || SecuritySensitiveCommodities.Contains(commodity));
        }

        private static float ComputeWarehouseLossRatio(string commodity, float storagePressure, float storageCondition, float stabilityScore)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            var conditionPenalty = 1f + ((1f - storageCondition) * 1.8f);
            if (SpoilageSensitiveCommodities.Contains(commodity))
            {
                return (0.0008f + (storagePressure * 0.0035f))
                    * conditionPenalty
                    * Math.Max(0.35f, 1f - (stabilityScore * 0.60f));
            }

            if (SecuritySensitiveCommodities.Contains(commodity))
            {
                return (0.00035f + (storagePressure * 0.0018f))
                    * conditionPenalty
                    * Math.Max(0.40f, 1f - stabilityScore);
            }

            return 0f;
        }

        private static float GetWarehouseStabilityScore(Industry industry, TerritoryDistrictState districtState)
        {
            var score = 0f;
            if (industry != null)
            {
                score += Math.Min(0.18f, Math.Max(0, industry.OutputStorageModuleLevel) * 0.06f);
                score += Math.Min(0.10f, Math.Max(0, industry.InputStorageModuleLevel) * 0.03f);
            }

            if (districtState != null)
            {
                if (districtState.LicenseStatus == DistrictLicenseStatus.Active)
                {
                    score += 0.10f;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    score += 0.05f;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Suspended)
                {
                    score -= 0.05f;
                }

                score += Math.Min(0.12f, Math.Max(0f, districtState.InfluenceRatio) * 0.12f);
                score += Math.Min(0.10f, Math.Max(0, districtState.ControlledDepots) * 0.03f);
                score += Math.Min(0.08f, Math.Max(0, districtState.RouteRights) * 0.015f);
            }

            return Math.Max(0f, Math.Min(0.60f, score));
        }

        private static float ClampWarehouseStorageCondition(float storageCondition)
        {
            if (storageCondition <= 0f)
            {
                return 1f;
            }

            return Math.Max(MinimumWarehouseStorageCondition, Math.Min(1f, storageCondition));
        }

        private static int GetDayIndex(int currentInGameMinute)
        {
            if (currentInGameMinute <= 0)
            {
                return 0;
            }

            return currentInGameMinute / (24 * 60);
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

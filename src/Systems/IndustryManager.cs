using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    internal sealed class WarehouseStoragePressureDayResult
    {
        public WarehouseStoragePressureDayResult()
        {
            CommodityLossTons = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, float> CommodityLossTons { get; private set; }

        public float StorageTons { get; set; }

        public float TotalCapacityTons { get; set; }

        public float FillRatio { get; set; }

        public float StabilityScore { get; set; }

        public float StoragePressure { get; set; }

        public float CurrentStorageCondition { get; set; }

        public float NextStorageCondition { get; set; }

        public float InventoryValue { get; set; }

        public float AdjustedInventoryValue { get; set; }

        public float SpoilageSensitiveTons { get; set; }

        public float ShrinkageSensitiveTons { get; set; }

        public float SpoilageTons { get; set; }

        public float SpoilageValue { get; set; }

        public float ShrinkageTons { get; set; }

        public float ShrinkageValue { get; set; }

        public WarehouseLossClass DominantLossClass { get; set; }

        public string DominantCommodity { get; set; }
    }

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
                ConfigureIndustryEconomics(industry);
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
                industry.ApplyStorageLossTelemetryState(-1, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

                SeedIndustryStartingState(industry, defaultConfig);
            }
        }

        public WarehouseStorageRiskSnapshot GetWarehouseStorageRiskSnapshot(Industry industry)
        {
            if (industry == null || !industry.IsWarehouse)
            {
                return null;
            }

            var dayResult = EvaluateWarehouseStoragePressureDay(
                industry,
                GetDistrictState(industry.DistrictName),
                industry.BufferStorage,
                ClampWarehouseStorageCondition(industry.StorageCondition));

            return new WarehouseStorageRiskSnapshot
            {
                Industry = industry,
                StorageTons = dayResult.StorageTons,
                TotalCapacityTons = dayResult.TotalCapacityTons,
                FillRatio = dayResult.FillRatio,
                StorageCondition = dayResult.CurrentStorageCondition,
                ProjectedNextCondition = dayResult.NextStorageCondition,
                StabilityScore = dayResult.StabilityScore,
                StoragePressure = dayResult.StoragePressure,
                InventoryValue = dayResult.InventoryValue,
                AdjustedInventoryValue = dayResult.AdjustedInventoryValue,
                ProjectedAdjustedInventoryValue = dayResult.InventoryValue * dayResult.NextStorageCondition,
                SpoilageSensitiveTons = dayResult.SpoilageSensitiveTons,
                ShrinkageSensitiveTons = dayResult.ShrinkageSensitiveTons,
                LastDaySpoilageTons = industry.LastDaySpoilageTons,
                LastDaySpoilageValue = industry.LastDaySpoilageValue,
                LastDayShrinkageTons = industry.LastDayShrinkageTons,
                LastDayShrinkageValue = industry.LastDayShrinkageValue,
                CurrentWeekSpoilageTons = industry.CurrentWeekSpoilageTons,
                CurrentWeekSpoilageValue = industry.CurrentWeekSpoilageValue,
                CurrentWeekShrinkageTons = industry.CurrentWeekShrinkageTons,
                CurrentWeekShrinkageValue = industry.CurrentWeekShrinkageValue,
                ProjectedSpoilageTons = dayResult.SpoilageTons,
                ProjectedSpoilageValue = dayResult.SpoilageValue,
                ProjectedShrinkageTons = dayResult.ShrinkageTons,
                ProjectedShrinkageValue = dayResult.ShrinkageValue,
                DominantLossClass = ResolveDominantLossClass(industry, dayResult),
                DominantCommodity = dayResult.DominantCommodity ?? string.Empty,
                HasSensitiveExposure = (dayResult.SpoilageSensitiveTons + dayResult.ShrinkageSensitiveTons) > 0.01f,
            };
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
                    market.RegisterDelivery(commodity, deliveredTons, gameTimeMs);
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
            if (!config.UsesAuthoredSiteSemantics)
            {
                SeedLegacyFactoryStartingState(industry, startingRatio);
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

        private static void SeedLegacyFactoryStartingState(Industry industry, float startingRatio)
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
                : ResolveLegacyFactoryDensityDrainRate(config.Density);

            // Authored site configs carry direct tons/min drain values.
            // Legacy factory density values preserve the older liters/second semantics.
            const float litersPerSecondToTonsPerMinute = 0.06f;
            drainRatePerMinute = config.UsesAuthoredSiteSemantics
                ? configuredDrainRate
                : configuredDrainRate * litersPerSecondToTonsPerMinute;

            return drainRatePerMinute > 0f;
        }

        private static float ResolveLegacyFactoryDensityDrainRate(string density)
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

            if (config.RecipeVariants != null && config.RecipeVariants.Count > 0)
            {
                foreach (var variant in config.RecipeVariants.Where(x => x != null))
                {
                    var recipe = BuildVariantRecipe(variant, supportsOmegaBoost);
                    if (recipe != null)
                    {
                        recipes.Add(recipe);
                    }
                }

                if (recipes.Count > 0)
                {
                    return recipes;
                }
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

        private static ProductionRecipe BuildVariantRecipe(IndustryRecipeVariantConfig variant, bool supportsOmegaBoost)
        {
            if (variant == null)
            {
                return null;
            }

            var boostInputs = CloneCommoditySet(variant.BoostInputs);
            var weightedInputs = ClonePositiveWeights(variant.RecipeInputWeights);
            if (weightedInputs.Count == 0)
            {
                var fallbackInputs = CloneCommoditySet(variant.Inputs)
                    .Concat(CloneCommoditySet(variant.OptionalInputs))
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

            var weightedOutputs = ClonePositiveWeights(variant.RecipeOutputWeights);
            if (weightedOutputs.Count == 0)
            {
                var outputs = CloneCommoditySet(variant.Outputs);
                foreach (var output in outputs)
                {
                    weightedOutputs[output] = 1f;
                }
            }

            if (weightedOutputs.Count == 0)
            {
                return null;
            }

            return new ProductionRecipe(
                weightedInputs,
                weightedOutputs,
                variant.Id,
                string.IsNullOrWhiteSpace(variant.DisplayName) ? variant.Id : variant.DisplayName,
                variant.SelectionPriority,
                ClonePositiveWeights(variant.OptionalInputWeights),
                CloneCommoditySet(variant.OptionalInputs),
                boostInputs);
        }

        private static HashSet<string> CloneCommoditySet(IEnumerable<string> source)
        {
            return source == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(source.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CommodityCatalog.Normalize), StringComparer.OrdinalIgnoreCase);
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
                RecipeVariants = CloneRecipeVariants(source.RecipeVariants),
                InputCapacityWeights = ClonePositiveWeights(source.InputCapacityWeights),
                OutputCapacityWeights = ClonePositiveWeights(source.OutputCapacityWeights),
                SinkPreferenceWeights = ClonePositiveWeights(source.SinkPreferenceWeights),
                SinkElasticityMultiplier = Math.Max(0.05f, source.SinkElasticityMultiplier <= 0f ? 1f : source.SinkElasticityMultiplier),
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
                UsesAuthoredSiteSemantics = source.UsesAuthoredSiteSemantics,
                CasualEconomy = CloneSiteEconomy(source.CasualEconomy),
                StandardEconomy = CloneSiteEconomy(source.StandardEconomy),
                HardcoreEconomy = CloneSiteEconomy(source.HardcoreEconomy),
                ImpossibleEconomy = CloneSiteEconomy(source.ImpossibleEconomy),
            };
        }

        private static List<IndustryRecipeVariantConfig> CloneRecipeVariants(IEnumerable<IndustryRecipeVariantConfig> source)
        {
            if (source == null)
            {
                return new List<IndustryRecipeVariantConfig>();
            }

            return source
                .Where(variant => variant != null)
                .Select(variant => new IndustryRecipeVariantConfig
                {
                    Id = variant.Id,
                    DisplayName = variant.DisplayName,
                    SelectionPriority = variant.SelectionPriority,
                    Inputs = CloneCommoditySet(variant.Inputs),
                    OptionalInputs = CloneCommoditySet(variant.OptionalInputs),
                    BoostInputs = CloneCommoditySet(variant.BoostInputs),
                    Outputs = CloneCommoditySet(variant.Outputs),
                    RecipeInputWeights = ClonePositiveWeights(variant.RecipeInputWeights),
                    RecipeOutputWeights = ClonePositiveWeights(variant.RecipeOutputWeights),
                    OptionalInputWeights = ClonePositiveWeights(variant.OptionalInputWeights),
                    InputCapacityWeights = ClonePositiveWeights(variant.InputCapacityWeights),
                    OutputCapacityWeights = ClonePositiveWeights(variant.OutputCapacityWeights),
                })
                .ToList();
        }

        private static IndustryConfig BuildEffectiveIndustryConfig(IndustryConfig baseConfig, EconomyDifficultyPreset preset)
        {
            var effectiveConfig = CloneIndustryConfig(baseConfig);
            if (effectiveConfig == null)
            {
                return effectiveConfig;
            }

            if (effectiveConfig.UsesAuthoredSiteSemantics)
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
                    return CloneSiteEconomy(config.CasualEconomy ?? config.StandardEconomy ?? config.HardcoreEconomy ?? config.ImpossibleEconomy);
                case EconomyDifficultyPreset.Hardcore:
                    return CloneSiteEconomy(config.HardcoreEconomy ?? config.StandardEconomy ?? config.CasualEconomy ?? config.ImpossibleEconomy);
                case EconomyDifficultyPreset.Impossible:
                    return CloneSiteEconomy(config.ImpossibleEconomy ?? config.HardcoreEconomy ?? config.StandardEconomy ?? config.CasualEconomy);
                default:
                    return CloneSiteEconomy(config.StandardEconomy ?? config.CasualEconomy ?? config.HardcoreEconomy ?? config.ImpossibleEconomy);
            }
        }

        private static SiteEconomyPresetValues ResolveAuthoredSitePresetValues(IndustryConfig config, EconomyDifficultyPreset preset)
        {
            var authoredImpossible = CloneSiteEconomy(config != null ? config.ImpossibleEconomy : null);
            var authoredStandard = CloneSiteEconomy(config != null
                ? (config.StandardEconomy ?? config.CasualEconomy ?? config.HardcoreEconomy ?? config.ImpossibleEconomy)
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
                case EconomyDifficultyPreset.Impossible:
                    if (authoredImpossible != null)
                    {
                        return authoredImpossible;
                    }

                    return SiteEconomyPresetValues.Create(
                        authoredStandard.ProductionRate * 0.70f,
                        authoredStandard.LicencePrice * 1.70f,
                        authoredStandard.PurchasePrice * 1.80f,
                        authoredStandard.InputCapacityTons * 0.65f,
                        authoredStandard.OutputCapacityTons * 0.65f,
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
                case EconomyDifficultyPreset.Impossible:
                    return new EconomyPresetValues(17f, 25000f, 1400000f, 55000f, 50000f);
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
                    case EconomyDifficultyPreset.Impossible:
                        return 1.10f;
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
                    case EconomyDifficultyPreset.Impossible:
                        return 1.75f;
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
                case EconomyDifficultyPreset.Impossible:
                    return Math.Min(0.55f, baseOwnerCut + 0.10f);
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
                case EconomyDifficultyPreset.Impossible:
                    multiplier = 1.30f;
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
                    case EconomyDifficultyPreset.Impossible:
                        multiplier = 1.30f;
                        break;
                }
            }

            return ServiceSiteEconomyPolicy.NormalizeWeeklyPassiveIncome(
                config.SiteRole,
                config.RefuelIsFree,
                config.WeeklyPassiveIncome * multiplier);
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

        private void ConfigureIndustryEconomics(Industry industry)
        {
            if (industry == null)
            {
                return;
            }

            industry.ConfigureRecipeSelectionEconomics(ComputeRecipeEconomicScore);
        }

        private void DrainSinkStock(Industry industry, float deltaMinutes, float drainRatePerMinute)
        {
            if (industry == null || deltaMinutes <= 0f || drainRatePerMinute <= 0f)
            {
                return;
            }

            var acceptedInputs = industry.SortedAcceptedInputs
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

            var remainingConsumption = consumed;
            var availableInputs = new List<string>(acceptedInputs);
            var maxPasses = Math.Max(2, acceptedInputs.Count * 3);

            for (var pass = 0; pass < maxPasses && remainingConsumption > 0.0001f && availableInputs.Count > 0; pass++)
            {
                var commodityWeights = BuildElasticSinkDemandWeights(industry, availableInputs, remainingConsumption);
                var totalWeight = commodityWeights.Values.Sum();
                if (totalWeight <= 0.001f)
                {
                    totalWeight = availableInputs.Count;
                }

                var remainingAtPassStart = remainingConsumption;
                for (int i = 0; i < availableInputs.Count && remainingConsumption > 0.0001f; i++)
                {
                    var commodity = availableInputs[i];
                    var currentStock = industry.GetStock(commodity);
                    if (currentStock <= 0.0001f)
                    {
                        continue;
                    }

                    float weight;
                    if (!commodityWeights.TryGetValue(commodity, out weight) || weight <= 0f)
                    {
                        weight = 1f;
                    }

                    var desiredShare = remainingAtPassStart * (weight / Math.Max(0.001f, totalWeight));
                    var removed = industry.RemoveInput(commodity, Math.Min(currentStock, desiredShare));
                    if (removed <= 0.0001f)
                    {
                        continue;
                    }

                    if (_globalMarket != null)
                    {
                        _globalMarket.RegisterSinkDemand(commodity, removed);
                    }

                    remainingConsumption = Math.Max(0f, remainingConsumption - removed);
                }

                availableInputs = availableInputs
                    .Where(commodity => industry.GetStock(commodity) > 0.0001f)
                    .ToList();
                if (Math.Abs(remainingAtPassStart - remainingConsumption) <= 0.0001f)
                {
                    break;
                }
            }
        }

        private Dictionary<string, float> BuildElasticSinkDemandWeights(Industry industry, IReadOnlyList<string> commodities, float targetConsumption)
        {
            var weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (industry == null || commodities == null || commodities.Count == 0)
            {
                return weights;
            }

            var expectedSlice = Math.Max(0.05f, targetConsumption / Math.Max(1, commodities.Count));
            for (int i = 0; i < commodities.Count; i++)
            {
                var commodity = commodities[i];
                var stock = industry.GetStock(commodity);
                if (stock <= 0.0001f)
                {
                    continue;
                }

                var semantics = CommodityCatalog.GetEconomySemantics(commodity);
                var sitePreference = industry.GetSinkPreferenceWeight(commodity);
                var roleBias = ResolveSinkRoleBias(industry, semantics);
                var marketDemand = _globalMarket != null
                    ? _globalMarket.GetSinkDemandMultiplier(industry.DistrictName, commodity)
                    : 1f;
                var pricePressure = _globalMarket != null
                    ? _globalMarket.GetPricePressure(commodity)
                    : 0f;
                var elasticity = Math.Max(0.05f, semantics.SinkElasticity * industry.SinkElasticityMultiplier);
                var pricePenalty = 1f / (1f + (pricePressure * elasticity * 0.90f));
                var availabilityFactor = 0.55f + Math.Min(1.25f, (float)Math.Sqrt(stock / expectedSlice));
                var substitutePenalty = ResolveSinkSubstitutePenalty(commodities, commodity, pricePressure, elasticity);

                weights[commodity] = Math.Max(
                    0.05f,
                    semantics.SinkPreferenceWeight
                    * sitePreference
                    * roleBias
                    * marketDemand
                    * pricePenalty
                    * availabilityFactor
                    * substitutePenalty);
            }

            return weights;
        }

        private static float ResolveSinkRoleBias(Industry industry, CommodityEconomySemantics semantics)
        {
            if (industry == null || semantics == null)
            {
                return 1f;
            }

            var bias = 1f;
            if (industry.IsConstructionSink)
            {
                if (string.Equals(semantics.SubstituteFamily, "ConstructionMaterials", StringComparison.OrdinalIgnoreCase))
                {
                    bias += 0.35f;
                }
                else if (semantics.DemandClasses != null && semantics.DemandClasses.Contains("ConstructionRelief"))
                {
                    bias += 0.15f;
                }
            }
            else if (industry.IsGasStation)
            {
                if (semantics.DemandClasses != null && semantics.DemandClasses.Contains("FuelRelief"))
                {
                    bias += 0.45f;
                }
            }
            else if (industry.IsStore)
            {
                if (semantics.DemandClasses != null && semantics.DemandClasses.Contains("EmergencyRelief"))
                {
                    bias += 0.20f;
                }

                if (semantics.Perishability > 0.40f)
                {
                    bias += 0.05f;
                }
            }

            return bias;
        }

        private float ResolveSinkSubstitutePenalty(IReadOnlyList<string> commodities, string commodity, float commodityPricePressure, float elasticity)
        {
            if (_globalMarket == null || commodities == null || commodities.Count <= 1)
            {
                return 1f;
            }

            var bestAlternativeAdvantage = 0f;
            for (int i = 0; i < commodities.Count; i++)
            {
                var alternative = commodities[i];
                if (string.Equals(alternative, commodity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var affinity = CommodityCatalog.GetSubstituteAffinity(commodity, alternative);
                if (affinity <= 0.15f)
                {
                    continue;
                }

                var alternativePressure = _globalMarket.GetPricePressure(alternative);
                var alternativeAdvantage = affinity * Math.Max(0f, commodityPricePressure - alternativePressure + 0.05f);
                if (alternativeAdvantage > bestAlternativeAdvantage)
                {
                    bestAlternativeAdvantage = alternativeAdvantage;
                }
            }

            if (bestAlternativeAdvantage <= 0f)
            {
                return 1f;
            }

            return 1f / (1f + (bestAlternativeAdvantage * elasticity * 1.35f));
        }

        private float ComputeRecipeEconomicScore(Industry industry, ProductionRecipe recipe)
        {
            if (_globalMarket == null || industry == null || recipe == null)
            {
                return 0f;
            }

            float outputScore = 0f;
            foreach (var pair in recipe.OutputsTons)
            {
                var semantics = CommodityCatalog.GetEconomySemantics(pair.Key);
                var demandFactor = 1f + Math.Min(0.90f, _globalMarket.GetPricePressure(pair.Key) * (0.70f + (semantics.EventResponseAffinity * 0.20f)));
                outputScore += pair.Value * _globalMarket.GetUnitPrice(pair.Key) * demandFactor;
            }

            float inputPenalty = 0f;
            foreach (var pair in recipe.InputsTons)
            {
                var semantics = CommodityCatalog.GetEconomySemantics(pair.Key);
                var stockCoverage = Math.Min(1.5f, industry.GetStock(pair.Key) / Math.Max(0.10f, pair.Value));
                var substituteCoverage = ResolveIndustrySubstituteCoverage(industry, pair.Key);
                var scarcityFactor = 1f + (_globalMarket.GetPricePressure(pair.Key) * (0.80f + (semantics.ScarcitySensitivity * 0.30f)));
                scarcityFactor += Math.Max(0f, 0.75f - stockCoverage) * 0.30f;
                scarcityFactor += Math.Max(0f, 0.40f - substituteCoverage) * 0.20f;
                inputPenalty += pair.Value * _globalMarket.GetUnitPrice(pair.Key) * scarcityFactor;
            }

            return (outputScore / 35f) - (inputPenalty / 60f);
        }

        private static float ResolveIndustrySubstituteCoverage(Industry industry, string commodity)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            var bestCoverage = 0f;
            for (int i = 0; i < industry.SortedAcceptedInputs.Count; i++)
            {
                var candidate = industry.SortedAcceptedInputs[i];
                if (string.Equals(candidate, commodity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stock = industry.GetStock(candidate);
                if (stock <= 0.05f)
                {
                    continue;
                }

                var affinity = CommodityCatalog.GetSubstituteAffinity(commodity, candidate);
                if (affinity <= 0.15f)
                {
                    continue;
                }

                bestCoverage = Math.Max(bestCoverage, affinity * Math.Min(1f, stock));
            }

            return Math.Max(0f, Math.Min(1f, bestCoverage));
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
            float totalSpoilageValue = 0f;
            float totalShrinkageValue = 0f;

            for (int day = 0; day < elapsedDays; day++)
            {
                var simulatedDayIndex = industry.LastStoragePressureDayIndex + day + 1;
                var dayResult = EvaluateWarehouseStoragePressureDay(industry, districtState, industry.BufferStorage, storageCondition);
                storageCondition = dayResult.NextStorageCondition;

                foreach (var pair in dayResult.CommodityLossTons)
                {
                    var currentTons = Math.Max(0f, industry.GetStock(pair.Key));
                    industry.BufferStorage[pair.Key] = Math.Max(0f, currentTons - pair.Value);
                }

                totalLostTons += dayResult.SpoilageTons + dayResult.ShrinkageTons;
                totalLostValue += dayResult.SpoilageValue + dayResult.ShrinkageValue;
                totalSpoilageValue += dayResult.SpoilageValue;
                totalShrinkageValue += dayResult.ShrinkageValue;
                industry.RecordStorageLossDay(
                    GetWeekIndexFromDay(simulatedDayIndex),
                    dayResult.SpoilageTons,
                    dayResult.SpoilageValue,
                    dayResult.ShrinkageTons,
                    dayResult.ShrinkageValue);
            }

            industry.RecordStoragePressure(currentDayIndex, storageCondition, totalLostTons, totalLostValue);

            if (totalSpoilageValue > 0.01f && _financeTracker != null)
            {
                var currentMinute = _getCurrentInGameMinute != null
                    ? Math.Max(0, _getCurrentInGameMinute())
                    : currentDayIndex * 24 * 60;
                _financeTracker.RecordExpense(
                    CompanyFinanceCategory.WarehouseSpoilage,
                    totalSpoilageValue,
                    currentMinute,
                    string.Format("Warehouse spoilage at {0}", industry.Name));
            }

            if (totalShrinkageValue > 0.01f && _financeTracker != null)
            {
                var currentMinute = _getCurrentInGameMinute != null
                    ? Math.Max(0, _getCurrentInGameMinute())
                    : currentDayIndex * 24 * 60;
                _financeTracker.RecordExpense(
                    CompanyFinanceCategory.WarehouseShrinkage,
                    totalShrinkageValue,
                    currentMinute,
                    string.Format("Warehouse shrinkage at {0}", industry.Name));
            }

            if (totalLostValue >= WarehouseLossStatusThreshold && _notifyStoragePressure != null)
            {
                _notifyStoragePressure(string.Format(
                    "{0} lost {1} to warehouse spoilage ({2}) and shrinkage ({3}).",
                    industry.Name,
                    ModFormatting.FormatMoney(totalLostValue),
                    ModFormatting.FormatMoney(totalSpoilageValue),
                    ModFormatting.FormatMoney(totalShrinkageValue)));
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
            return GetWarehouseLossClass(commodity) != WarehouseLossClass.None;
        }

        private static float ComputeWarehouseLossRatio(string commodity, float storagePressure, float storageCondition, float stabilityScore)
        {
            return ComputeWarehouseLossRatio(GetWarehouseLossClass(commodity), storagePressure, storageCondition, stabilityScore);
        }

        private static float ComputeWarehouseLossRatio(WarehouseLossClass lossClass, float storagePressure, float storageCondition, float stabilityScore)
        {
            if (lossClass == WarehouseLossClass.None)
            {
                return 0f;
            }

            var conditionPenalty = 1f + ((1f - storageCondition) * 1.8f);
            if (lossClass == WarehouseLossClass.Spoilage)
            {
                return (0.0008f + (storagePressure * 0.0035f))
                    * conditionPenalty
                    * Math.Max(0.35f, 1f - (stabilityScore * 0.60f));
            }

            if (lossClass == WarehouseLossClass.Shrinkage)
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

        private WarehouseStoragePressureDayResult EvaluateWarehouseStoragePressureDay(
            Industry industry,
            TerritoryDistrictState districtState,
            IReadOnlyDictionary<string, float> storage,
            float storageCondition)
        {
            var result = new WarehouseStoragePressureDayResult
            {
                CurrentStorageCondition = ClampWarehouseStorageCondition(storageCondition),
            };

            if (industry == null || storage == null)
            {
                result.NextStorageCondition = result.CurrentStorageCondition;
                return result;
            }

            foreach (var pair in storage)
            {
                var tons = Math.Max(0f, pair.Value);
                if (tons <= 0f)
                {
                    continue;
                }

                result.StorageTons += tons;
                var normalizedCommodity = CommodityCatalog.Normalize(pair.Key);
                var lossClass = GetWarehouseLossClass(normalizedCommodity);
                if (lossClass == WarehouseLossClass.Spoilage)
                {
                    result.SpoilageSensitiveTons += tons;
                }
                else if (lossClass == WarehouseLossClass.Shrinkage)
                {
                    result.ShrinkageSensitiveTons += tons;
                }

                var unitPrice = GetCommodityUnitPrice(normalizedCommodity);
                result.InventoryValue += tons * unitPrice;
            }

            result.TotalCapacityTons = Math.Max(1f, industry.InputCapacityTons + industry.OutputCapacityTons);
            result.FillRatio = Math.Max(0f, Math.Min(1f, result.StorageTons / result.TotalCapacityTons));
            result.StoragePressure = Math.Max(0f, result.FillRatio - 0.30f);
            result.StabilityScore = GetWarehouseStabilityScore(industry, districtState);

            var hasSensitiveExposure = (result.SpoilageSensitiveTons + result.ShrinkageSensitiveTons) > 0.01f;
            var degradation = hasSensitiveExposure
                ? (0.015f + (result.StoragePressure * 0.045f) + (districtState != null && districtState.LicenseStatus == DistrictLicenseStatus.Suspended ? 0.02f : 0f))
                    * Math.Max(0.35f, 1f - result.StabilityScore)
                : 0f;
            var recovery = !hasSensitiveExposure || result.FillRatio < 0.12f
                ? 0.02f + Math.Min(0.04f, result.StabilityScore * 0.05f)
                : 0f;
            result.NextStorageCondition = ClampWarehouseStorageCondition(result.CurrentStorageCondition - degradation + recovery);
            result.AdjustedInventoryValue = result.InventoryValue * result.CurrentStorageCondition;

            var highestLossValue = 0f;
            foreach (var pair in storage)
            {
                var commodity = CommodityCatalog.Normalize(pair.Key);
                var currentTons = Math.Max(0f, pair.Value);
                if (currentTons <= 0.01f)
                {
                    continue;
                }

                var lossClass = GetWarehouseLossClass(commodity);
                if (lossClass == WarehouseLossClass.None)
                {
                    continue;
                }

                var lossRatio = ComputeWarehouseLossRatio(lossClass, result.StoragePressure, result.NextStorageCondition, result.StabilityScore);
                var lostTons = Math.Min(currentTons, currentTons * lossRatio);
                if (lostTons <= 0.001f)
                {
                    continue;
                }

                var lostValue = lostTons * GetCommodityUnitPrice(commodity);
                result.CommodityLossTons[commodity] = lostTons;
                if (lossClass == WarehouseLossClass.Spoilage)
                {
                    result.SpoilageTons += lostTons;
                    result.SpoilageValue += lostValue;
                }
                else if (lossClass == WarehouseLossClass.Shrinkage)
                {
                    result.ShrinkageTons += lostTons;
                    result.ShrinkageValue += lostValue;
                }

                if (lostValue > highestLossValue)
                {
                    highestLossValue = lostValue;
                    result.DominantLossClass = lossClass;
                    result.DominantCommodity = commodity;
                }
            }

            return result;
        }

        private float GetCommodityUnitPrice(string commodity)
        {
            return _globalMarket != null
                ? Math.Max(0f, _globalMarket.GetUnitPrice(commodity))
                : 0f;
        }

        private static WarehouseLossClass GetWarehouseLossClass(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity))
            {
                return WarehouseLossClass.None;
            }

            if (SpoilageSensitiveCommodities.Contains(commodity))
            {
                return WarehouseLossClass.Spoilage;
            }

            if (SecuritySensitiveCommodities.Contains(commodity))
            {
                return WarehouseLossClass.Shrinkage;
            }

            return WarehouseLossClass.None;
        }

        private static WarehouseLossClass ResolveDominantLossClass(Industry industry, WarehouseStoragePressureDayResult dayResult)
        {
            if (industry == null)
            {
                return dayResult != null ? dayResult.DominantLossClass : WarehouseLossClass.None;
            }

            var currentWeekSpoilage = Math.Max(0f, industry.CurrentWeekSpoilageValue);
            var currentWeekShrinkage = Math.Max(0f, industry.CurrentWeekShrinkageValue);
            if (currentWeekSpoilage > 0.01f || currentWeekShrinkage > 0.01f)
            {
                return currentWeekSpoilage >= currentWeekShrinkage
                    ? WarehouseLossClass.Spoilage
                    : WarehouseLossClass.Shrinkage;
            }

            return dayResult != null ? dayResult.DominantLossClass : WarehouseLossClass.None;
        }

        private static int GetWeekIndexFromDay(int dayIndex)
        {
            return dayIndex < 0 ? -1 : dayIndex / 7;
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
        Impossible = 3,
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

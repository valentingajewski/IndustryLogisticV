using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public sealed class IndustryManager
    {
        private readonly List<Industry> _industries;

        public IndustryManager(ModConfig config)
        {
            _industries = new List<Industry>();
            foreach (var pair in config.IndustryConfigs)
            {
                var industryConfig = pair.Value;
                var groundedConfig = new IndustryConfig
                {
                    Id = industryConfig.Id,
                    Name = industryConfig.Name,
                    Position = GetGroundedPosition(industryConfig.Position),
                    Inputs = new HashSet<string>(industryConfig.Inputs, StringComparer.OrdinalIgnoreCase),
                    Outputs = new HashSet<string>(industryConfig.Outputs, StringComparer.OrdinalIgnoreCase),
                    InputCapacityTons = industryConfig.InputCapacityTons,
                    OutputCapacityTons = industryConfig.OutputCapacityTons,
                    ProductionRate = industryConfig.ProductionRate,
                    StartingTankRatio = industryConfig.StartingTankRatio,
                };

                var supportsOmegaBoost = ShouldUseOmegaBoost(groundedConfig);
                var recipes = BuildRecipes(groundedConfig, supportsOmegaBoost);
                var industry = new Industry(groundedConfig, recipes, supportsOmegaBoost, config.IndustryOmegaCapacityMultiplier);
                SeedInitialOutput(industry);
                SeedStartingTank(industry, groundedConfig);
                _industries.Add(industry);
            }
        }

        public IReadOnlyList<Industry> Industries
        {
            get { return _industries; }
        }

        public void Update(float deltaMinutes, float omegaMultiplier)
        {
            for (int i = 0; i < _industries.Count; i++)
            {
                _industries[i].Update(deltaMinutes, omegaMultiplier);
            }
        }

        public Industry GetNearestIndustry(Vector3 position, float maxDistance)
        {
            Industry nearest = null;
            var maxDistanceSq = maxDistance * maxDistance;
            var bestSq = maxDistanceSq;
            var pos2 = new Vector2(position.X, position.Y);

            for (int i = 0; i < _industries.Count; i++)
            {
                var candidate = _industries[i];
                var cand2 = new Vector2(candidate.Position.X, candidate.Position.Y);
                var distSq = pos2.DistanceToSquared(cand2);
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    nearest = candidate;
                }
            }

            return nearest;
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
            if (industry == null || industry.Outputs.Count == 0)
            {
                return result;
            }

            foreach (var output in industry.Outputs.OrderBy(x => x))
            {
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

            return result;
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
            if (industry.IsSink)
            {
                if (commodity.Equals("TV", StringComparison.OrdinalIgnoreCase) ||
                    commodity.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                {
                    market.RegisterDelivery(gameTimeMs);
                }

                return deliveredTons * unitPrice;
            }

            return deliveredTons * unitPrice * 0.15f;
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

        private static List<ProductionRecipe> BuildRecipes(IndustryConfig config, bool supportsOmegaBoost)
        {
            var recipes = new List<ProductionRecipe>();
            var id = config.Id ?? string.Empty;

            if (id.Equals("RecyclingCenter", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Recyclable", 1f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Metal", 0.25f },
                        { "Plastic", 0.25f },
                        { "Alloy", 0.25f },
                    }));
                return recipes;
            }

            if (id.Equals("OmegaFactory", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Coal", 2f },
                        { "Ore", 2f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Omega", 1f },
                    }));
                return recipes;
            }

            if (id.Equals("SmeltingFactory", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Ore", 2f },
                        { "Coal", 1f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Metal", 1f },
                        { "Alloy", 1f },
                    }));
                return recipes;
            }

            if (id.Equals("Refinery", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Oil", 2f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Plastic", 1f },
                        { "Fuel", 1f },
                    }));
                return recipes;
            }

            if (id.Equals("ProcessorFactory", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Plastic", 1f },
                        { "Alloy", 1f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Electronic", 1f },
                    }));
                return recipes;
            }

            if (id.Equals("ConsumerElectronicsFactory", StringComparison.OrdinalIgnoreCase))
            {
                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Electronic", 2f },
                    },
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "TV", 1f },
                        { "Computer", 1f },
                        { "Recyclable", 0.25f },
                    }));
                return recipes;
            }

            if (config.Outputs.Count == 0)
            {
                return recipes;
            }

            var effectiveInputs = config.Inputs
                .Where(x => !supportsOmegaBoost || !x.Equals("Omega", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (effectiveInputs.Count == 0)
            {
                var outputOnly = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var output in config.Outputs)
                {
                    outputOnly[output] = 1f;
                }

                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    outputOnly));
                return recipes;
            }

            var genericInputs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < effectiveInputs.Count; i++)
            {
                genericInputs[effectiveInputs[i]] = 1f;
            }

            var genericOutputs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var output in config.Outputs)
            {
                genericOutputs[output] = 1f;
            }

            recipes.Add(new ProductionRecipe(genericInputs, genericOutputs));
            return recipes;
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

        private static Vector3 GetGroundedPosition(Vector3 position)
        {
            float z;
            if (World.GetGroundHeight(new Vector3(position.X, position.Y, position.Z + 50f), out z, GetGroundHeightMode.ConsiderWaterAsGround))
            {
                return new Vector3(position.X, position.Y, z + 0.05f);
            }

            return position;
        }
    }
}

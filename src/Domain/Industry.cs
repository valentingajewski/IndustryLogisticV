using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Math;
using IndustryLogisticV.Config;

namespace IndustryLogisticV.Domain
{
    public sealed class Industry
    {
        private readonly List<ProductionRecipe> _recipes;
        private readonly bool _supportsOmegaBoost;

        public Industry(IndustryConfig config, List<ProductionRecipe> recipes, bool supportsOmegaBoost, float omegaCapacityMultiplier)
        {
            Id = config.Id;
            Name = config.Name;
            Position = config.Position;
            Inputs = new HashSet<string>(config.Inputs, StringComparer.OrdinalIgnoreCase);
            Outputs = new HashSet<string>(config.Outputs, StringComparer.OrdinalIgnoreCase);
            BufferStorage = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            ProductionRate = Math.Max(1f, config.ProductionRate);
            InputCapacityTons = Math.Max(1f, config.InputCapacityTons);
            OutputCapacityTons = Math.Max(1f, config.OutputCapacityTons);
            OmegaCapacityTons = Math.Max(1f, InputCapacityTons * Math.Max(0.01f, omegaCapacityMultiplier));

            _recipes = recipes ?? new List<ProductionRecipe>();
            _supportsOmegaBoost = supportsOmegaBoost;

            foreach (var input in Inputs)
            {
                if (!BufferStorage.ContainsKey(input))
                {
                    BufferStorage[input] = 0f;
                }
            }

            foreach (var output in Outputs)
            {
                if (!BufferStorage.ContainsKey(output))
                {
                    BufferStorage[output] = 0f;
                }
            }
        }

        public string Id { get; }
        public string Name { get; }
        public Vector3 Position { get; }
        public HashSet<string> Inputs { get; }
        public HashSet<string> Outputs { get; }
        public Dictionary<string, float> BufferStorage { get; }
        public float ProductionRate { get; private set; }
        public bool HasOmegaBoost { get; private set; }
        public float OmegaStorage { get; private set; }
        public float InputCapacityTons { get; private set; }
        public float OutputCapacityTons { get; private set; }
        public float OmegaCapacityTons { get; private set; }
        public float LastUtilizationPercent { get; private set; }
        public float CurrentOutputPerHourTons { get; private set; }
        public int ProductionModuleLevel { get; private set; }
        public int InputStorageModuleLevel { get; private set; }
        public int OutputStorageModuleLevel { get; private set; }
        public int OmegaStorageModuleLevel { get; private set; }

        public int UpgradeLevel
        {
            get
            {
                return ProductionModuleLevel + InputStorageModuleLevel + OutputStorageModuleLevel + OmegaStorageModuleLevel;
            }
        }

        public bool IsSink
        {
            get { return Outputs.Count == 0; }
        }

        public bool SupportsOmegaBoost
        {
            get { return _supportsOmegaBoost; }
        }

        public float GetStock(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            float value;
            if (BufferStorage.TryGetValue(commodity, out value))
            {
                return value;
            }

            return 0f;
        }

        public float GetInputStockTotal()
        {
            float total = 0f;
            foreach (var input in Inputs)
            {
                total += GetStock(input);
            }

            return total;
        }

        public float GetOutputStockTotal()
        {
            float total = 0f;
            foreach (var output in Outputs)
            {
                total += GetStock(output);
            }

            return total;
        }

        public bool AcceptsCommodity(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (_supportsOmegaBoost && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Inputs.Contains(commodity);
        }

        public bool ProducesCommodity(string commodity)
        {
            return Outputs.Contains(CommodityCatalog.Normalize(commodity));
        }

        public float AddInput(string commodity, float tons)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (tons <= 0f)
            {
                return 0f;
            }

            if (_supportsOmegaBoost && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                var omegaSpace = OmegaCapacityTons - OmegaStorage;
                if (omegaSpace <= 0f)
                {
                    return 0f;
                }

                var addedOmega = Math.Min(omegaSpace, tons);
                OmegaStorage += addedOmega;
                HasOmegaBoost = OmegaStorage > 0.0001f;
                return addedOmega;
            }

            if (!Inputs.Contains(commodity))
            {
                return 0f;
            }

            var capacity = GetCommodityCapacityTons(commodity);
            var current = GetStock(commodity);
            var free = capacity - current;
            if (free <= 0f)
            {
                return 0f;
            }

            var added = Math.Min(free, tons);
            BufferStorage[commodity] = current + added;
            return added;
        }

        public float AddOutput(string commodity, float tons)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (tons <= 0f || !Outputs.Contains(commodity))
            {
                return 0f;
            }

            var capacity = GetCommodityCapacityTons(commodity);
            var current = GetStock(commodity);
            var free = capacity - current;
            if (free <= 0f)
            {
                return 0f;
            }

            var added = Math.Min(free, tons);
            BufferStorage[commodity] = current + added;
            return added;
        }

        public float RemoveOutput(string commodity, float tons)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (tons <= 0f || !Outputs.Contains(commodity))
            {
                return 0f;
            }

            var current = GetStock(commodity);
            if (current <= 0f)
            {
                return 0f;
            }

            var removed = Math.Min(current, tons);
            BufferStorage[commodity] = current - removed;
            return removed;
        }

        public void SeedOutput(string commodity, float tons)
        {
            AddOutput(commodity, tons);
        }

        public void Update(float deltaMinutes, float omegaMultiplier)
        {
            if (deltaMinutes <= 0f || _recipes.Count == 0)
            {
                LastUtilizationPercent = 0f;
                CurrentOutputPerHourTons = 0f;
                return;
            }

            var baseCyclesBudget = ProductionRate * (deltaMinutes / 60f);
            if (baseCyclesBudget <= 0f)
            {
                LastUtilizationPercent = 0f;
                CurrentOutputPerHourTons = 0f;
                return;
            }

            var isBoosted = _supportsOmegaBoost && OmegaStorage > 0.0001f;
            HasOmegaBoost = isBoosted;
            var effectiveCyclesBudget = baseCyclesBudget * (isBoosted ? Math.Max(1f, omegaMultiplier) : 1f);

            float cyclesUsed = 0f;
            float outputProducedTons = 0f;
            float remainingBudget = effectiveCyclesBudget;

            for (int i = 0; i < _recipes.Count; i++)
            {
                if (remainingBudget <= 0f)
                {
                    break;
                }

                var recipe = _recipes[i];
                var maxCyclesByInput = recipe.GetMaxCyclesFromInputs(BufferStorage);
                var maxCyclesByOutput = GetMaxCyclesFromOutputCapacity(recipe);

                var cycles = Math.Min(remainingBudget, Math.Min(maxCyclesByInput, maxCyclesByOutput));
                if (cycles <= 0f)
                {
                    continue;
                }

                foreach (var input in recipe.InputsTons)
                {
                    var current = GetStock(input.Key);
                    BufferStorage[input.Key] = Math.Max(0f, current - (input.Value * cycles));
                }

                foreach (var output in recipe.OutputsTons)
                {
                    var current = GetStock(output.Key);
                    BufferStorage[output.Key] = current + (output.Value * cycles);
                    outputProducedTons += output.Value * cycles;
                }

                cyclesUsed += cycles;
                remainingBudget -= cycles;
            }

            if (isBoosted && cyclesUsed > 0f)
            {
                var omegaDrain = cyclesUsed * 0.25f;
                OmegaStorage = Math.Max(0f, OmegaStorage - omegaDrain);
                HasOmegaBoost = OmegaStorage > 0.0001f;
            }

            LastUtilizationPercent = baseCyclesBudget <= 0f ? 0f : (cyclesUsed / baseCyclesBudget) * 100f;
            CurrentOutputPerHourTons = outputProducedTons <= 0f ? 0f : outputProducedTons * (60f / deltaMinutes);
        }

        public float GetUpgradeCost(IndustryUpgradeModule module)
        {
            if (module == IndustryUpgradeModule.Production)
            {
                return 5000f * (ProductionModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                return 3500f * (InputStorageModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return 3500f * (OutputStorageModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                if (!_supportsOmegaBoost)
                {
                    return -1f;
                }

                return 4500f * (OmegaStorageModuleLevel + 1);
            }

            return -1f;
        }

        public int GetUpgradeLevel(IndustryUpgradeModule module)
        {
            if (module == IndustryUpgradeModule.Production)
            {
                return ProductionModuleLevel;
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                return InputStorageModuleLevel;
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return OutputStorageModuleLevel;
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                return OmegaStorageModuleLevel;
            }

            return 0;
        }

        public bool TryUpgradeModule(IndustryUpgradeModule module, ref float profit, out float cost, out string result)
        {
            result = string.Empty;
            cost = GetUpgradeCost(module);
            if (cost <= 0f)
            {
                result = "This module is not available for this industry.";
                return false;
            }

            if (profit < cost)
            {
                result = string.Format("Not enough profit. Cost: ${0:0}", cost);
                return false;
            }

            profit -= cost;

            if (module == IndustryUpgradeModule.Production)
            {
                ProductionModuleLevel += 1;
                ProductionRate += Math.Max(2f, ProductionRate * 0.12f);
                result = string.Format("Production module upgraded to Lv.{0}.", ProductionModuleLevel);
                return true;
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                InputStorageModuleLevel += 1;
                InputCapacityTons += Math.Max(5f, InputCapacityTons * 0.18f);
                result = string.Format("Input storage module upgraded to Lv.{0}.", InputStorageModuleLevel);
                return true;
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                OutputStorageModuleLevel += 1;
                OutputCapacityTons += Math.Max(5f, OutputCapacityTons * 0.18f);
                result = string.Format("Output storage module upgraded to Lv.{0}.", OutputStorageModuleLevel);
                return true;
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                OmegaStorageModuleLevel += 1;
                OmegaCapacityTons += Math.Max(2f, OmegaCapacityTons * 0.20f);
                result = string.Format("Omega storage module upgraded to Lv.{0}.", OmegaStorageModuleLevel);
                return true;
            }

            result = "Unknown upgrade module.";
            profit += cost;
            return false;
        }

        public bool TryUpgrade(ref float profit, out float cost)
        {
            string ignored;
            return TryUpgradeModule(IndustryUpgradeModule.Production, ref profit, out cost, out ignored);
        }

        public float GetMaxTransferTonsForCommodity(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            var capacity = GetCommodityCapacityTons(commodity);
            var current = GetStock(commodity);
            return Math.Max(0f, capacity - current);
        }

        public List<string> GetSortedOutputs()
        {
            return Outputs.OrderBy(x => x).ToList();
        }

        public List<string> GetSortedInputs()
        {
            return Inputs.OrderBy(x => x).ToList();
        }

        public bool HasOutputStock(string commodity)
        {
            return GetStock(commodity) > 0.001f;
        }

        public bool HasInputFreeSpace(string commodity)
        {
            return GetMaxTransferTonsForCommodity(commodity) > 0.001f;
        }

        public void ClampBuffersToCapacity()
        {
            var keys = BufferStorage.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var cap = GetCommodityCapacityTons(key);
                BufferStorage[key] = Math.Max(0f, Math.Min(BufferStorage[key], cap));
            }

            OmegaStorage = Math.Max(0f, Math.Min(OmegaStorage, OmegaCapacityTons));
            HasOmegaBoost = OmegaStorage > 0.0001f;
        }

        private float GetMaxCyclesFromOutputCapacity(ProductionRecipe recipe)
        {
            if (recipe.OutputsTons.Count == 0)
            {
                return float.MaxValue;
            }

            float max = float.MaxValue;
            foreach (var pair in recipe.OutputsTons)
            {
                if (pair.Value <= 0f)
                {
                    continue;
                }

                var current = GetStock(pair.Key);
                var capacity = GetCommodityCapacityTons(pair.Key);
                var free = capacity - current;
                var cycles = free <= 0f ? 0f : free / pair.Value;
                if (cycles < max)
                {
                    max = cycles;
                }
            }

            return max;
        }

        private float GetCommodityCapacityTons(string commodity)
        {
            var inCount = Math.Max(1, Inputs.Count);
            var outCount = Math.Max(1, Outputs.Count);
            var inputShare = Inputs.Contains(commodity) ? InputCapacityTons / inCount : 0f;
            var outputShare = Outputs.Contains(commodity) ? OutputCapacityTons / outCount : 0f;

            var max = Math.Max(inputShare, outputShare);
            if (max <= 0f)
            {
                max = Math.Max(InputCapacityTons, OutputCapacityTons);
            }

            return max;
        }
    }
}

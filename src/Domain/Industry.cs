using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Math;
using LSOL.Config;

namespace LSOL.Domain
{
    public sealed class Industry
    {
        private readonly List<ProductionRecipe> _recipes;
        private readonly bool _supportsOmegaBoost;
        private readonly Dictionary<string, float> _inputCapacityWeights;
        private readonly Dictionary<string, float> _outputCapacityWeights;
        private readonly List<string> _sortedInputs;
        private readonly List<string> _sortedOptionalInputs;
        private readonly List<string> _sortedBoostInputs;
        private readonly List<string> _sortedAcceptedInputs;
        private readonly List<string> _sortedOutputs;

        public Industry(IndustryConfig config, List<ProductionRecipe> recipes, bool supportsOmegaBoost, float omegaCapacityMultiplier)
        {
            CatalogId = config.CatalogId;
            Id = config.Id;
            LegacyKey = string.IsNullOrWhiteSpace(config.LegacyKey) ? config.Id : config.LegacyKey;
            LocationKind = config.LocationKind;
            SiteRole = config.SiteRole;
            OwnershipTier = config.OwnershipTier;
            DistrictName = config.DistrictName ?? string.Empty;
            Name = config.Name;
            Company = config.Company ?? string.Empty;
            Position = config.Position;
            VehicleSpawnPosition = config.VehicleSpawnPosition;
            VehicleSpawnHeading = config.VehicleSpawnHeading;
            SpawnedVehiclePosition = config.SpawnedVehiclePosition;
            SpawnedVehicleHeading = config.SpawnedVehicleHeading;
            GatePosition = config.GatePosition;
            BarrierModelHash = config.BarrierModelHash;
            WorkerPosition = config.WorkerPosition;
            DisplayObjectModelHash = config.DisplayObjectModelHash;
            DisplayObjectsAtGroundLevel = config.DisplayObjectsAtGroundLevel;
            MaxDisplayObjectLine = config.MaxDisplayObjectLine;
            MaxDisplayObjectRow = config.MaxDisplayObjectRow;
            ObjectToDeleteModelHashes = (config.ObjectToDeleteModelHashes ?? new List<int>()).AsReadOnly();
            Inputs = new HashSet<string>(config.Inputs, StringComparer.OrdinalIgnoreCase);
            OptionalInputs = new HashSet<string>(config.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            BoostInputs = new HashSet<string>(config.BoostInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            Outputs = new HashSet<string>(config.Outputs, StringComparer.OrdinalIgnoreCase);
            BufferStorage = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            ProductionRate = NormalizeProductionRate(config.ProductionRate);
            InputCapacityTons = Math.Max(1f, config.InputCapacityTons);
            OutputCapacityTons = Math.Max(1f, config.OutputCapacityTons);
            OmegaCapacityTons = Math.Max(1f, InputCapacityTons * Math.Max(0.01f, omegaCapacityMultiplier));
            EmptyingRate = Math.Max(0f, config.EmptyingRate);
            HasConfiguredEmptyingRate = config.HasConfiguredEmptyingRate;
            RefuelIsFree = config.RefuelIsFree;
            IndustryPrice = Math.Max(0f, config.IndustryPrice);
            IndustryLicencePrice = Math.Max(0f, config.IndustryLicencePrice);
            IndustryOwnerCut = Math.Max(0f, Math.Min(1f, config.IndustryOwnerCut));
            DeliveryPayoutMultiplier = Math.Max(0f, config.DeliveryPayoutMultiplier <= 0f ? 1f : config.DeliveryPayoutMultiplier);
            IsOwned = config.IsOwned || HasStarterOwnership;
            HasContractorPermit = config.HasContractorPermit || IndustryLicencePrice <= 0f || HasStarterPermitAccess;

            _recipes = recipes ?? new List<ProductionRecipe>();
            _supportsOmegaBoost = supportsOmegaBoost;
            _inputCapacityWeights = CloneCommodityWeightMap(config.InputCapacityWeights);
            _outputCapacityWeights = CloneCommodityWeightMap(config.OutputCapacityWeights);
            _sortedInputs = Inputs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            _sortedOptionalInputs = OptionalInputs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            _sortedBoostInputs = BoostInputs.Count > 0
                ? BoostInputs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>(_sortedOptionalInputs);
            _sortedAcceptedInputs = Inputs
                .Concat(OptionalInputs)
                .Concat(BoostInputs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _sortedOutputs = Outputs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

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

            foreach (var optionalInput in OptionalInputs)
            {
                if (!BufferStorage.ContainsKey(optionalInput))
                {
                    BufferStorage[optionalInput] = 0f;
                }
            }

            foreach (var boostInput in BoostInputs)
            {
                if (!BufferStorage.ContainsKey(boostInput))
                {
                    BufferStorage[boostInput] = 0f;
                }
            }
        }

        public string CatalogId { get; }
        public string Id { get; }
        public string LegacyKey { get; }
        public ExternalLocationKind LocationKind { get; }
        public SiteRole SiteRole { get; }
        public SiteOwnershipTier OwnershipTier { get; }
        public string DistrictName { get; }
        public string Name { get; }
        public string Company { get; }
        public Vector3 Position { get; }
        public Vector3? VehicleSpawnPosition { get; }
        public float? VehicleSpawnHeading { get; }
        public Vector3? SpawnedVehiclePosition { get; }
        public float? SpawnedVehicleHeading { get; }
        public Vector3? GatePosition { get; }
        public int? BarrierModelHash { get; }
        public Vector3? WorkerPosition { get; }
        public int? DisplayObjectModelHash { get; }
        public bool DisplayObjectsAtGroundLevel { get; }
        public int? MaxDisplayObjectLine { get; }
        public int? MaxDisplayObjectRow { get; }
        public IReadOnlyList<int> ObjectToDeleteModelHashes { get; }
        public HashSet<string> Inputs { get; }
        public HashSet<string> OptionalInputs { get; }
        public HashSet<string> BoostInputs { get; }
        public HashSet<string> Outputs { get; }
        public Dictionary<string, float> BufferStorage { get; }
        public IReadOnlyList<string> SortedInputs
        {
            get { return _sortedInputs; }
        }

        public IReadOnlyList<string> SortedOptionalInputs
        {
            get { return _sortedOptionalInputs; }
        }

        public IReadOnlyList<string> SortedAcceptedInputs
        {
            get { return _sortedAcceptedInputs; }
        }

        public IReadOnlyList<string> SortedOutputs
        {
            get { return _sortedOutputs; }
        }

        public float ProductionRate { get; private set; }
        public bool HasOmegaBoost { get; private set; }
        public float OmegaStorage { get; private set; }
        public float InputCapacityTons { get; private set; }
        public float OutputCapacityTons { get; private set; }
        public float OmegaCapacityTons { get; private set; }
        public float EmptyingRate { get; private set; }
        public bool HasConfiguredEmptyingRate { get; }
        public bool RefuelIsFree { get; }
        public float IndustryPrice { get; private set; }
        public float IndustryLicencePrice { get; private set; }
        public float IndustryOwnerCut { get; private set; }
        public float DeliveryPayoutMultiplier { get; private set; }
        public bool IsOwned { get; private set; }
        public bool HasContractorPermit { get; private set; }
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

        public bool IsStore
        {
            get { return LocationKind == ExternalLocationKind.Store; }
        }

        public bool IsGasStation
        {
            get { return LocationKind == ExternalLocationKind.GasStation; }
        }

        public bool IsDepotLike
        {
            get { return SiteMetadataParser.IsDepotLike(SiteRole); }
        }

        public bool IsStarterHeadquarters
        {
            get { return SiteRole == SiteRole.StarterHQ; }
        }

        public bool HasStarterOwnership
        {
            get { return SiteMetadataParser.GrantsStarterOwnership(SiteRole); }
        }

        public bool HasStarterPermitAccess
        {
            get { return SiteMetadataParser.GrantsStarterPermitAccess(SiteRole, OwnershipTier); }
        }

        public bool IsConstructionSink
        {
            get { return SiteRole == SiteRole.ConstructionSiteSink; }
        }

        public bool IsWarehouse
        {
            get { return SiteRole == SiteRole.Warehouse; }
        }

        public bool SupportsOmegaBoost
        {
            get { return _supportsOmegaBoost; }
        }

        public bool RequiresPurchase
        {
            get { return IndustryPrice > 0f; }
        }

        public bool RequiresContractorPermit
        {
            get { return IndustryLicencePrice > 0f; }
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
            foreach (var input in _sortedAcceptedInputs)
            {
                if (_supportsOmegaBoost && input.Equals("Omega", StringComparison.OrdinalIgnoreCase))
                {
                    total += OmegaStorage;
                    continue;
                }

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

            return Inputs.Contains(commodity) || OptionalInputs.Contains(commodity) || BoostInputs.Contains(commodity);
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

            if (!Inputs.Contains(commodity) && !OptionalInputs.Contains(commodity) && !BoostInputs.Contains(commodity))
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

        public float RemoveInput(string commodity, float tons)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (tons <= 0f)
            {
                return 0f;
            }

            if (_supportsOmegaBoost && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                if (OmegaStorage <= 0f)
                {
                    return 0f;
                }

                var removedOmega = Math.Min(OmegaStorage, tons);
                OmegaStorage = Math.Max(0f, OmegaStorage - removedOmega);
                HasOmegaBoost = OmegaStorage > 0.0001f;
                return removedOmega;
            }

            if (!Inputs.Contains(commodity) && !OptionalInputs.Contains(commodity) && !BoostInputs.Contains(commodity))
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

        public float ClearInputs()
        {
            float removed = 0f;
            foreach (var acceptedInput in _sortedAcceptedInputs)
            {
                if (_supportsOmegaBoost && acceptedInput.Equals("Omega", StringComparison.OrdinalIgnoreCase))
                {
                    removed += OmegaStorage;
                    OmegaStorage = 0f;
                    HasOmegaBoost = false;
                    continue;
                }

                removed += GetStock(acceptedInput);
                BufferStorage[acceptedInput] = 0f;
            }

            return removed;
        }

        public float ClearOutputs()
        {
            float removed = 0f;
            foreach (var output in Outputs)
            {
                removed += GetStock(output);
                BufferStorage[output] = 0f;
            }

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

            var optionalBoostMultiplier = ResolveOptionalInputBoostMultiplier();

            var isBoosted = _supportsOmegaBoost && OmegaStorage > 0.0001f;
            HasOmegaBoost = isBoosted;
            var effectiveCyclesBudget = baseCyclesBudget * optionalBoostMultiplier * (isBoosted ? Math.Max(1f, omegaMultiplier) : 1f);

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

            ConsumeOptionalInputs(cyclesUsed);

            LastUtilizationPercent = baseCyclesBudget <= 0f ? 0f : (cyclesUsed / baseCyclesBudget) * 100f;
            CurrentOutputPerHourTons = outputProducedTons <= 0f ? 0f : outputProducedTons * (60f / deltaMinutes);
        }

        public float GetUpgradeCost(IndustryUpgradeModule module)
        {
            if (IsWarehouse)
            {
                return -1f;
            }

            var isInputOnlySink = Outputs.Count == 0 && Inputs.Count > 0;

            if (module == IndustryUpgradeModule.Production)
            {
                if (isInputOnlySink)
                {
                    return -1f;
                }

                return 5000f * (ProductionModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.InputStorage)
            {
                if (isInputOnlySink)
                {
                    return -1f;
                }

                return 3500f * (InputStorageModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.OutputStorage)
            {
                return 3500f * (OutputStorageModuleLevel + 1);
            }

            if (module == IndustryUpgradeModule.OmegaStorage)
            {
                if (!_supportsOmegaBoost || isInputOnlySink)
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
            if (RequiresPurchase && !IsOwned)
            {
                cost = IndustryPrice;
                result = string.Format("Purchase {0} before buying upgrade modules.", Name);
                return false;
            }

            cost = GetUpgradeCost(module);
            if (cost <= 0f)
            {
                result = "This module is not available for this industry.";
                return false;
            }

            if (profit < cost)
            {
                result = string.Format("Not enough profit. Cost: {0}", ModFormatting.FormatMoney(cost));
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

        public bool TryPurchase(ref float profit, out float cost, out string result)
        {
            cost = IndustryPrice;
            result = string.Empty;

            if (!RequiresPurchase)
            {
                SetOwned(true);
                result = "This industry does not require purchase.";
                return true;
            }

            if (IsOwned)
            {
                result = "This industry is already owned.";
                return false;
            }

            if (profit < cost)
            {
                result = string.Format("Not enough profit. Cost: {0}", ModFormatting.FormatMoney(cost));
                return false;
            }

            profit -= cost;
            SetOwned(true);
            result = string.Format("Purchased {0} for {1}.", Name, ModFormatting.FormatMoney(cost));
            return true;
        }

        public bool TryPurchaseContractorPermit(ref float profit, out float cost, out string result)
        {
            cost = IndustryLicencePrice;
            result = string.Empty;

            if (!RequiresContractorPermit)
            {
                SetContractorPermitOwned(true);
                result = string.Format("{0} does not require a contractor permit.", Name);
                return true;
            }

            if (HasContractorPermit)
            {
                result = string.Format("Contractor permit already purchased for {0}.", Name);
                return false;
            }

            if (profit < cost)
            {
                result = string.Format("Not enough profit. Permit cost: {0}", ModFormatting.FormatMoney(cost));
                return false;
            }

            profit -= cost;
            SetContractorPermitOwned(true);
            result = string.Format("Purchased contractor permit for {0} for {1}.", Name, ModFormatting.FormatMoney(cost));
            return true;
        }

        public void SetOwned(bool isOwned)
        {
            IsOwned = isOwned || HasStarterOwnership;
        }

        public void SetContractorPermitOwned(bool hasContractorPermit)
        {
            HasContractorPermit = hasContractorPermit || !RequiresContractorPermit || HasStarterPermitAccess;
        }

        public void SetProductionRate(float productionRate)
        {
            ProductionRate = NormalizeProductionRate(productionRate);
        }

        public float GetMaxTransferTonsForCommodity(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (_supportsOmegaBoost && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0f, OmegaCapacityTons - OmegaStorage);
            }

            var capacity = GetCommodityCapacityTons(commodity);
            var current = GetStock(commodity);
            return Math.Max(0f, capacity - current);
        }

        public List<string> GetSortedOutputs()
        {
            return new List<string>(_sortedOutputs);
        }

        public List<string> GetSortedInputs()
        {
            return new List<string>(_sortedInputs);
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

        public string GetPrimaryConversionDescription()
        {
            if (IsWarehouse)
            {
                return "Storage site only.";
            }

            if (_recipes.Count == 0)
            {
                return "No production recipe.";
            }

            var recipe = _recipes[0];
            return string.Format(
                "{0} -> {1} | {2}",
                FormatCommodityFlow(recipe.InputsTons, "Passive source"),
                FormatCommodityFlow(recipe.OutputsTons, "No output"),
                ModFormatting.FormatRatePerHour(ProductionRate, "cyc"));
        }

        public string GetProductionWarning()
        {
            if (IsWarehouse)
            {
                return string.Empty;
            }

            if (_recipes.Count == 0)
            {
                return string.Empty;
            }

            ProductionRecipe blockingRecipe = null;
            List<string> blockingInputs = null;

            for (int i = 0; i < _recipes.Count; i++)
            {
                var recipe = _recipes[i];
                if (recipe == null || GetMaxCyclesFromOutputCapacity(recipe) <= 0.0001f)
                {
                    continue;
                }

                var missingInputs = recipe.InputsTons
                    .Where(x => x.Value > 0f && GetStock(x.Key) + 0.0001f < x.Value)
                    .Select(x => CommodityCatalog.Normalize(x.Key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (missingInputs.Count == 0)
                {
                    return string.Empty;
                }

                if (blockingRecipe == null || blockingInputs == null || missingInputs.Count < blockingInputs.Count)
                {
                    blockingRecipe = recipe;
                    blockingInputs = missingInputs;
                }
            }

            if (blockingRecipe == null || blockingInputs == null || blockingInputs.Count == 0)
            {
                return string.Empty;
            }

            return string.Format(
                "Missing {0} to produce {1}",
                FormatCommodityNames(blockingInputs),
                FormatCommodityNames(blockingRecipe.OutputsTons.Keys));
        }

        public void ApplyPersistentState(
            Dictionary<string, float> bufferStorage,
            float omegaStorage,
            float productionRate,
            float inputCapacityTons,
            float outputCapacityTons,
            float omegaCapacityTons,
            int productionModuleLevel,
            int inputStorageModuleLevel,
            int outputStorageModuleLevel,
            int omegaStorageModuleLevel,
            bool isOwned = false,
            bool hasContractorPermit = false,
            float? industryPrice = null,
            float? industryLicencePrice = null,
            float? emptyingRate = null,
            float? industryOwnerCut = null,
            float? deliveryPayoutMultiplier = null)
        {
            foreach (var key in BufferStorage.Keys.ToList())
            {
                BufferStorage[key] = 0f;
            }

            if (bufferStorage != null)
            {
                foreach (var pair in bufferStorage)
                {
                    var commodity = CommodityCatalog.Normalize(pair.Key);
                    if (!BufferStorage.ContainsKey(commodity))
                    {
                        continue;
                    }

                    BufferStorage[commodity] = Math.Max(0f, pair.Value);
                }
            }

            ProductionRate = NormalizeProductionRate(productionRate);
            InputCapacityTons = Math.Max(1f, inputCapacityTons);
            OutputCapacityTons = Math.Max(1f, outputCapacityTons);
            OmegaCapacityTons = Math.Max(1f, omegaCapacityTons);

            ProductionModuleLevel = Math.Max(0, productionModuleLevel);
            InputStorageModuleLevel = Math.Max(0, inputStorageModuleLevel);
            OutputStorageModuleLevel = Math.Max(0, outputStorageModuleLevel);
            OmegaStorageModuleLevel = Math.Max(0, omegaStorageModuleLevel);

            if (industryPrice.HasValue)
            {
                IndustryPrice = Math.Max(0f, industryPrice.Value);
            }

            if (industryLicencePrice.HasValue)
            {
                IndustryLicencePrice = Math.Max(0f, industryLicencePrice.Value);
            }

            if (emptyingRate.HasValue)
            {
                EmptyingRate = Math.Max(0f, emptyingRate.Value);
            }

            if (industryOwnerCut.HasValue)
            {
                IndustryOwnerCut = Math.Max(0f, Math.Min(1f, industryOwnerCut.Value));
            }

            if (deliveryPayoutMultiplier.HasValue)
            {
                DeliveryPayoutMultiplier = Math.Max(0f, deliveryPayoutMultiplier.Value <= 0f ? 1f : deliveryPayoutMultiplier.Value);
            }

            OmegaStorage = Math.Max(0f, omegaStorage);
            SetOwned(isOwned);
            SetContractorPermitOwned(hasContractorPermit);
            ClampBuffersToCapacity();
            LastUtilizationPercent = 0f;
            CurrentOutputPerHourTons = 0f;
        }

        private float NormalizeProductionRate(float productionRate)
        {
            return IsWarehouse
                ? Math.Max(0f, productionRate)
                : Math.Max(1f, productionRate);
        }

        private float ResolveOptionalInputBoostMultiplier()
        {
            if (_sortedBoostInputs.Count == 0)
            {
                return 1f;
            }

            var multiplier = 1f;
            for (int i = 0; i < _sortedBoostInputs.Count; i++)
            {
                var boostInput = _sortedBoostInputs[i];
                var stock = _supportsOmegaBoost && boostInput.Equals("Omega", StringComparison.OrdinalIgnoreCase)
                    ? OmegaStorage
                    : GetStock(boostInput);
                if (stock > 0.05f)
                {
                    multiplier += 0.12f;
                }
            }

            return multiplier;
        }

        private void ConsumeOptionalInputs(float cyclesUsed)
        {
            if (cyclesUsed <= 0f || _sortedBoostInputs.Count == 0)
            {
                return;
            }

            var perInputConsumption = cyclesUsed * 0.15f;
            if (perInputConsumption <= 0f)
            {
                return;
            }

            for (int i = 0; i < _sortedBoostInputs.Count; i++)
            {
                var boostInput = _sortedBoostInputs[i];
                RemoveInput(boostInput, perInputConsumption);
            }
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

        private static string FormatCommodityFlow(Dictionary<string, float> tonsByCommodity, string emptyLabel)
        {
            if (tonsByCommodity == null || tonsByCommodity.Count == 0)
            {
                return emptyLabel;
            }

            return string.Join(
                " + ",
                tonsByCommodity
                    .OrderBy(x => CommodityCatalog.Normalize(x.Key))
                    .Select(x => string.Format("{0} {1}", ModFormatting.FormatNumber(x.Value), CommodityCatalog.Normalize(x.Key))));
        }

        private static string FormatCommodityNames(IEnumerable<string> commodities)
        {
            if (commodities == null)
            {
                return "resource";
            }

            var names = commodities
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(CommodityCatalog.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (names.Count == 0)
            {
                return "resource";
            }

            return string.Join("/", names);
        }

        private float GetCommodityCapacityTons(string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (_supportsOmegaBoost && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0f, OmegaCapacityTons);
            }

            var inputShare = _sortedAcceptedInputs.Contains(commodity, StringComparer.OrdinalIgnoreCase)
                ? ResolveWeightedCapacityShare(commodity, _sortedAcceptedInputs, InputCapacityTons, _inputCapacityWeights)
                : 0f;
            var outputShare = Outputs.Contains(commodity)
                ? ResolveWeightedCapacityShare(commodity, _sortedOutputs, OutputCapacityTons, _outputCapacityWeights)
                : 0f;

            var max = Math.Max(inputShare, outputShare);
            if (max <= 0f)
            {
                max = Math.Max(InputCapacityTons, OutputCapacityTons);
            }

            return max;
        }

        private static float ResolveWeightedCapacityShare(string commodity, IEnumerable<string> commodities, float totalCapacity, Dictionary<string, float> weights)
        {
            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            var bucketCommodities = (commodities ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(CommodityCatalog.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (bucketCommodities.Count == 0 || !bucketCommodities.Contains(normalizedCommodity, StringComparer.OrdinalIgnoreCase))
            {
                return 0f;
            }

            if (weights == null || weights.Count == 0)
            {
                return totalCapacity / Math.Max(1, bucketCommodities.Count);
            }

            var totalWeight = 0f;
            for (int i = 0; i < bucketCommodities.Count; i++)
            {
                totalWeight += ResolveWeight(weights, bucketCommodities[i]);
            }

            if (totalWeight <= 0f)
            {
                return totalCapacity / Math.Max(1, bucketCommodities.Count);
            }

            return totalCapacity * (ResolveWeight(weights, normalizedCommodity) / totalWeight);
        }

        private static float ResolveWeight(Dictionary<string, float> weights, string commodity)
        {
            if (weights == null)
            {
                return 1f;
            }

            float configuredWeight;
            return weights.TryGetValue(commodity, out configuredWeight) && configuredWeight > 0f
                ? configuredWeight
                : 1f;
        }

        private static Dictionary<string, float> CloneCommodityWeightMap(IEnumerable<KeyValuePair<string, float>> source)
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
    }
}

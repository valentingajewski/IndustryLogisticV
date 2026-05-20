using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    public sealed class ModConfig
    {
        public float OmegaMultiplier { get; private set; }
        public float IndustryOmegaCapacityMultiplier { get; private set; }
        public float MarkerRadius { get; private set; }
        public float MarkerHeight { get; private set; }
        public Vector3 MainOfficePosition { get; private set; }
        public Vector3 VehicleSpawnPosition { get; private set; }
        public float VehicleSpawnHeading { get; private set; }
        public ControlBindings Controls { get; private set; }
        public Dictionary<string, float> CommodityBasePrices { get; private set; }
        public Dictionary<string, IndustryConfig> IndustryConfigs { get; private set; }
        public List<VehicleDefinition> VehicleDefinitions { get; private set; }
        public List<VehicleObjectLayoutDefinition> VehicleObjectLayouts { get; private set; }
        public List<BankDefinition> BankDefinitions { get; private set; }
        public List<OfficeDefinition> OfficeDefinitions { get; private set; }
        public List<OfficeObjectDefinition> OfficeObjectDefinitions { get; private set; }
        public List<InteriorDefinition> InteriorDefinitions { get; private set; }
        public List<MotelDefinition> MotelDefinitions { get; private set; }
        public List<DealershipVehicleDefinition> PersonalVehicleDefinitions { get; private set; }
        public ExternalConfigCatalog ExternalCatalog { get; private set; }
        public List<VehicleCargoType> CargoTypes { get; private set; }
        public Dictionary<string, List<string>> ObjectModels { get; private set; }
        public List<string> WorkerModels { get; private set; }
        public Dictionary<string, DistrictConfig> DistrictConfigs { get; private set; }
        public List<string> ValidationMessages { get; private set; }

        public static ModConfig Load(string configDirectory, LsolAddonCatalog addonCatalog = null)
        {
            var coreValidationMessages = new List<string>();
            var coreConfig = XmlConfigImport.LoadCoreConfig(configDirectory, coreValidationMessages);
            var externalCatalog = ExternalConfigCatalog.Load(configDirectory, addonCatalog);
            CommodityCatalog.Configure(externalCatalog.ResourceGroups);
            externalCatalog.ValidationMessages.InsertRange(0, coreValidationMessages);

            var config = new ModConfig
            {
                OmegaMultiplier = Math.Max(1f, coreConfig.OmegaMultiplier),
                IndustryOmegaCapacityMultiplier = Math.Max(0.01f, coreConfig.IndustryOmegaCapacityMultiplier),
                MarkerRadius = Math.Max(0.2f, coreConfig.MarkerRadius),
                MarkerHeight = Math.Max(0.5f, coreConfig.MarkerHeight),
                MainOfficePosition = coreConfig.MainOfficePosition,
                VehicleSpawnPosition = coreConfig.VehicleSpawnPosition,
                VehicleSpawnHeading = coreConfig.VehicleSpawnHeading,
                Controls = coreConfig.Controls ?? new ControlBindings(),
                CommodityBasePrices = new Dictionary<string, float>(externalCatalog.CommodityBasePrices, StringComparer.OrdinalIgnoreCase),
                ExternalCatalog = externalCatalog,
                IndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase),
                VehicleDefinitions = new List<VehicleDefinition>(),
                VehicleObjectLayouts = new List<VehicleObjectLayoutDefinition>(),
                BankDefinitions = new List<BankDefinition>(),
                OfficeDefinitions = new List<OfficeDefinition>(),
                OfficeObjectDefinitions = new List<OfficeObjectDefinition>(),
                InteriorDefinitions = new List<InteriorDefinition>(),
                MotelDefinitions = new List<MotelDefinition>(),
                PersonalVehicleDefinitions = new List<DealershipVehicleDefinition>(),
                CargoTypes = new List<VehicleCargoType>(),
                ObjectModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
                WorkerModels = new List<string>(),
                DistrictConfigs = new Dictionary<string, DistrictConfig>(externalCatalog.Districts, StringComparer.OrdinalIgnoreCase),
                ValidationMessages = new List<string>(externalCatalog.ValidationMessages),
            };

            config.CargoTypes.AddRange(ResolveCargoTypeOrder(externalCatalog));

            if (externalCatalog.Locations.Count > 0)
            {
                MergeExternalLocations(coreConfig, externalCatalog, config);
            }

            if (externalCatalog.VehicleDefinitions.Count > 0)
            {
                config.VehicleDefinitions.AddRange(CloneVehicleDefinitions(externalCatalog.VehicleDefinitions));
            }

            if (externalCatalog.VehicleObjectLayouts.Count > 0)
            {
                config.VehicleObjectLayouts.AddRange(CloneVehicleObjectLayouts(externalCatalog.VehicleObjectLayouts));
            }

            if (externalCatalog.BankDefinitions.Count > 0)
            {
                config.BankDefinitions.AddRange(CloneBankDefinitions(externalCatalog.BankDefinitions));
            }

            if (externalCatalog.OfficeDefinitions.Count > 0)
            {
                config.OfficeDefinitions.AddRange(CloneOfficeDefinitions(externalCatalog.OfficeDefinitions));
            }

            if (externalCatalog.OfficeObjectDefinitions.Count > 0)
            {
                config.OfficeObjectDefinitions.AddRange(CloneOfficeObjectDefinitions(externalCatalog.OfficeObjectDefinitions));
            }

            if (externalCatalog.InteriorDefinitions.Count > 0)
            {
                config.InteriorDefinitions.AddRange(CloneInteriorDefinitions(externalCatalog.InteriorDefinitions));
            }

            if (externalCatalog.MotelDefinitions.Count > 0)
            {
                config.MotelDefinitions.AddRange(CloneMotelDefinitions(externalCatalog.MotelDefinitions));
            }

            if (externalCatalog.PersonalVehicleDefinitions.Count > 0)
            {
                config.PersonalVehicleDefinitions.AddRange(CloneDealershipVehicleDefinitions(externalCatalog.PersonalVehicleDefinitions));
            }

            MergeExternalObjects(externalCatalog, config);
            config.WorkerModels.AddRange(coreConfig.WorkerModels);

            if (config.CargoTypes.Count == 0)
            {
                config.CargoTypes.AddRange(BuildFallbackCargoTypes(config));
            }

            return config;
        }

        private static void MergeExternalLocations(CoreXmlConfig coreConfig, ExternalConfigCatalog externalCatalog, ModConfig config)
        {
            if (externalCatalog == null || config == null)
            {
                return;
            }

            foreach (var pair in externalCatalog.Locations)
            {
                var location = pair.Value;
                if (location == null || !location.Enabled)
                {
                    continue;
                }

                config.IndustryConfigs[location.Id] = BuildIndustryConfigFromExternalLocation(coreConfig, location);
            }
        }

        private static IndustryConfig BuildIndustryConfigFromExternalLocation(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            var standardValues = location.StandardEconomy ?? SiteEconomyPresetValues.Create(0f, 0f, location.IndustryPrice, 0f, 0f, location.FactoryProductionRatio, false);
            var productionRatio = Math.Max(0.1f, standardValues.ProductionRatio > 0f ? standardValues.ProductionRatio : (location.FactoryProductionRatio > 0f ? location.FactoryProductionRatio : 1f));
            var productionRate = standardValues.ProductionRate > 0f
                ? standardValues.ProductionRate * productionRatio
                : ResolveProductionRate(coreConfig, location);
            var inputCapacityTons = standardValues.InputCapacityTons > 0f
                ? standardValues.InputCapacityTons
                : ResolveLocationInputCapacityTons(coreConfig, location);
            var outputCapacityTons = standardValues.OutputCapacityTons > 0f
                ? standardValues.OutputCapacityTons
                : ResolveLocationOutputCapacityTons(coreConfig, location);
            var licencePrice = standardValues.PermitRequired ? Math.Max(0f, standardValues.LicencePrice) : 0f;
            var purchasePrice = Math.Max(0f, standardValues.PurchasePrice);
            var hasStarterOwnership = SiteMetadataParser.GrantsStarterOwnership(location.SiteRole);
            var hasStarterPermitAccess = SiteMetadataParser.GrantsStarterPermitAccess(location.SiteRole, location.OwnershipTier);
            var inputs = new HashSet<string>(location.Inputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var optionalInputs = new HashSet<string>(location.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var boostInputs = new HashSet<string>(location.BoostInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var outputs = new HashSet<string>(location.Outputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var recipeInputWeights = CloneCommodityWeightMap(location.RecipeInputWeights);
            var recipeOutputWeights = CloneCommodityWeightMap(location.RecipeOutputWeights);
            var inputCapacityWeights = CloneCommodityWeightMap(
                location.InputCapacityWeights != null && location.InputCapacityWeights.Count > 0
                    ? location.InputCapacityWeights
                    : location.RecipeInputWeights);
            var outputCapacityWeights = CloneCommodityWeightMap(
                location.OutputCapacityWeights != null && location.OutputCapacityWeights.Count > 0
                    ? location.OutputCapacityWeights
                    : location.RecipeOutputWeights);

            if (location.SiteRole == SiteRole.Warehouse)
            {
                outputs.UnionWith(inputs);
                productionRate = 0f;
            }

            return new IndustryConfig
            {
                CatalogId = location.CatalogId,
                Id = location.Id,
                LegacyKey = location.LegacyKey ?? location.Id,
                LocationKind = location.Kind,
                SiteRole = location.SiteRole,
                OwnershipTier = location.OwnershipTier,
                DistrictName = location.DistrictName,
                Name = location.Name,
                Company = location.Company,
                Position = location.Position,
                VehicleSpawnPosition = location.VehicleSpawnPosition,
                VehicleSpawnHeading = location.VehicleSpawnHeading,
                SpawnedVehiclePosition = location.SpawnedVehiclePosition,
                SpawnedVehicleHeading = location.SpawnedVehicleHeading,
                GatePosition = location.GatePosition,
                BarrierModelHash = location.BarrierModelHash,
                WorkerPosition = location.WorkerPosition,
                DisplayObjectModelHash = location.DisplayObjectModelHash,
                DisplayObjectsAtGroundLevel = location.DisplayObjectsAtGroundLevel,
                MaxDisplayObjectLine = location.MaxSpawnedVehiclesLine,
                MaxDisplayObjectRow = location.MaxSpawnedVehiclesRow,
                ObjectToDeleteModelHashes = ParseObjectModelHashes(location.ObjectToDelete),
                Inputs = inputs,
                OptionalInputs = optionalInputs,
                BoostInputs = boostInputs,
                Outputs = outputs,
                RecipeInputWeights = recipeInputWeights,
                RecipeOutputWeights = recipeOutputWeights,
                InputCapacityWeights = inputCapacityWeights,
                OutputCapacityWeights = outputCapacityWeights,
                FactoryProductionRatio = productionRatio,
                InputCapacityTons = inputCapacityTons,
                OutputCapacityTons = outputCapacityTons,
                ProductionRate = productionRate,
                StartingTankRatio = location.StartingTankRatio,
                Density = location.Density,
                EmptyingRate = location.EmptyingRate,
                WeeklyPassiveIncome = Math.Max(0f, location.WeeklyPassiveIncome),
                HasConfiguredEmptyingRate = location.HasConfiguredEmptyingRate,
                RefuelIsFree = location.RefuelIsFree,
                IndustryPrice = purchasePrice,
                IndustryLicencePrice = licencePrice,
                IndustryOwnerCut = Math.Max(0f, Math.Min(1f, location.IndustryOwnerCut)),
                DeliveryPayoutMultiplier = Math.Max(0f, location.DeliveryPayoutMultiplier <= 0f ? 1f : location.DeliveryPayoutMultiplier),
                IsOwned = hasStarterOwnership,
                HasContractorPermit = hasStarterPermitAccess || !standardValues.PermitRequired || licencePrice <= 0f,
                IsCsvBacked = true,
                CasualEconomy = location.CasualEconomy,
                StandardEconomy = location.StandardEconomy,
                HardcoreEconomy = location.HardcoreEconomy,
            };
        }

        private static float ResolveLocationInputCapacityTons(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            return Math.Max(10f, ResolveInputCapacityTons(coreConfig, location));
        }

        private static float ResolveLocationOutputCapacityTons(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            return Math.Max(10f, ResolveOutputCapacityTons(coreConfig, location));
        }

        private static float ResolveInputCapacityTons(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            switch (location.Kind)
            {
                case ExternalLocationKind.Industry:
                    return 50f;
                case ExternalLocationKind.Store:
                    return ResolveDensityProfileCapacityTons(coreConfig != null ? coreConfig.StoreDensityProfile : null, location.Density, 60000f);
                case ExternalLocationKind.GasStation:
                    return ResolveDensityProfileCapacityTons(coreConfig != null ? coreConfig.GasStationDensityProfile : null, location.Density, 60000f);
                default:
                    return 50f;
            }
        }

        private static float ResolveOutputCapacityTons(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            if (location.Kind != ExternalLocationKind.Industry || location.Outputs.Count == 0)
            {
                return 10f;
            }

            return 50f;
        }

        private static float ResolveProductionRate(CoreXmlConfig coreConfig, ExternalLocationConfig location)
        {
            if (location != null && location.SiteRole == SiteRole.Warehouse)
            {
                return 0f;
            }

            var ratio = location != null && location.Kind == ExternalLocationKind.Industry
                ? Math.Max(0.1f, location.FactoryProductionRatio > 0f ? location.FactoryProductionRatio : 1f)
                : 1f;
            return Math.Max(1f, 30f * ratio);
        }

        private static float ResolveDensityProfileCapacityTons(XmlDensityProfileConfig profile, string density, float defaultCapacityRaw)
        {
            var rawCapacity = profile != null
                ? profile.GetCapacity(density)
                : defaultCapacityRaw;
            return Math.Max(10f, rawCapacity / 1000f);
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

        private static List<int> ParseObjectModelHashes(string raw)
        {
            var hashes = new List<int>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return hashes;
            }

            var seen = new HashSet<int>();
            var tokens = raw.Split(new[] { ',', ';', '|', '\t', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                int hash;
                if (!TryParseObjectModelHash(token, out hash) || !seen.Add(hash))
                {
                    continue;
                }

                hashes.Add(hash);
            }

            return hashes;
        }

        private static bool TryParseObjectModelHash(string token, out int hash)
        {
            hash = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = token.Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                uint hexValue;
                if (uint.TryParse(normalized.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hexValue))
                {
                    hash = unchecked((int)hexValue);
                    return true;
                }
            }

            int signedValue;
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out signedValue))
            {
                hash = signedValue;
                return true;
            }

            uint unsignedValue;
            if (uint.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out unsignedValue))
            {
                hash = unchecked((int)unsignedValue);
                return true;
            }

            return false;
        }

        private static List<VehicleCargoType> BuildFallbackCargoTypes(ModConfig config)
        {
            return config.VehicleDefinitions
                .Select(x => x.CargoType)
                .Where(x => x != VehicleCargoType.Unknown && x != VehicleCargoType.Trailer)
                .Distinct()
                .ToList();
        }

        private static IEnumerable<VehicleDefinition> CloneVehicleDefinitions(IEnumerable<VehicleDefinition> source)
        {
            if (source == null)
            {
                return new VehicleDefinition[0];
            }

            return source
                .Where(x => x != null)
                .Select(x => new VehicleDefinition
                {
                    Id = x.Id,
                    SectionName = x.SectionName,
                    DisplayName = x.DisplayName,
                    ModelName = x.ModelName,
                    CargoType = x.CargoType,
                    AcceptedCommodities = x.AcceptedCommodities == null
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(x.AcceptedCommodities, StringComparer.OrdinalIgnoreCase),
                    CapacityTons = x.CapacityTons,
                    FuelCapacityLiters = x.FuelCapacityLiters,
                    Price = x.Price,
                    DailyRent = x.DailyRent,
                    IsEnabled = x.IsEnabled,
                    IsTrailer = x.IsTrailer,
                    IsTractor = x.IsTractor,
                })
                .ToList();
        }

        private static IEnumerable<VehicleObjectLayoutDefinition> CloneVehicleObjectLayouts(IEnumerable<VehicleObjectLayoutDefinition> source)
        {
            if (source == null)
            {
                return new VehicleObjectLayoutDefinition[0];
            }

            return source
                .Where(x => x != null)
                .Select(x => new VehicleObjectLayoutDefinition
                {
                    ModelName = x.ModelName,
                    DisplayName = x.DisplayName,
                    ObjectKey = x.ObjectKey,
                    CenterOffset = x.CenterOffset,
                    MaxLine = x.MaxLine,
                    MaxRow = x.MaxRow,
                    IsEnabled = x.IsEnabled,
                })
                .ToList();
        }

        private static IEnumerable<BankDefinition> CloneBankDefinitions(IEnumerable<BankDefinition> source)
        {
            return source == null
                ? new BankDefinition[0]
                : source
                    .Where(x => x != null)
                    .Select(x => new BankDefinition
                    {
                        BankId = x.BankId,
                        Name = x.Name,
                        Position = x.Position,
                        LoanAmountMaxLimit = x.LoanAmountMaxLimit,
                        LoanInterestMin = x.LoanInterestMin,
                        LoanInterestMax = x.LoanInterestMax,
                    })
                    .ToList();
        }

            private static IEnumerable<OfficeDefinition> CloneOfficeDefinitions(IEnumerable<OfficeDefinition> source)
            {
                return source == null
                    ? new OfficeDefinition[0]
                    : source
                        .Where(x => x != null)
                        .Select(x => new OfficeDefinition
                        {
                            OfficeId = x.OfficeId,
                            LegacyKey = x.LegacyKey,
                            SiteName = x.SiteName,
                            DistrictName = x.DistrictName,
                            MarkerPosition = x.MarkerPosition,
                            SpawnPosition = x.SpawnPosition,
                            SpawnHeading = x.SpawnHeading,
                            GatePosition = x.GatePosition,
                            BarrierModelHash = x.BarrierModelHash,
                            WorkerPosition = x.WorkerPosition,
                            OfficePrice = x.OfficePrice,
                            WeeklyOfficeRent = x.WeeklyOfficeRent,
                            MaxCommercialVehicles = x.MaxCommercialVehicles,
                            Description = x.Description,
                        })
                        .ToList();
            }

            private static IEnumerable<OfficeObjectDefinition> CloneOfficeObjectDefinitions(IEnumerable<OfficeObjectDefinition> source)
            {
                return source == null
                    ? new OfficeObjectDefinition[0]
                    : source
                        .Where(x => x != null)
                        .Select(x => new OfficeObjectDefinition
                        {
                            ObjectId = x.ObjectId,
                            ModelName = x.ModelName,
                            ModelHash = x.ModelHash,
                            DisplayName = x.DisplayName,
                            Size = x.Size,
                            Function = x.Function,
                            ResourceType = x.ResourceType,
                            Capacity = x.Capacity,
                            PerOfficeLimit = x.PerOfficeLimit,
                            Price = x.Price,
                        })
                        .ToList();
            }

            private static IEnumerable<InteriorDefinition> CloneInteriorDefinitions(IEnumerable<InteriorDefinition> source)
            {
                return source == null
                    ? new InteriorDefinition[0]
                    : source
                        .Where(x => x != null)
                        .Select(x => new InteriorDefinition
                        {
                            InteriorId = x.InteriorId,
                            InteriorName = x.InteriorName,
                            InteriorIgName = x.InteriorIgName,
                            InteriorPosition = x.InteriorPosition,
                            InteriorType = x.InteriorType,
                            InteriorPrice = x.InteriorPrice,
                            InteriorWeeklyRent = x.InteriorWeeklyRent,
                            ExteriorPosition = x.ExteriorPosition,
                            GaragePosition = x.GaragePosition,
                        })
                        .ToList();
            }

            private static IEnumerable<MotelDefinition> CloneMotelDefinitions(IEnumerable<MotelDefinition> source)
            {
                return source == null
                    ? new MotelDefinition[0]
                    : source
                        .Where(x => x != null)
                        .Select(x => new MotelDefinition
                        {
                            MotelId = x.MotelId,
                            MotelName = x.MotelName,
                            MotelIgName = x.MotelIgName,
                            MotelType = x.MotelType,
                            RestPrice = x.RestPrice,
                            ExteriorPosition = x.ExteriorPosition,
                        })
                        .ToList();
            }

            private static IEnumerable<DealershipVehicleDefinition> CloneDealershipVehicleDefinitions(IEnumerable<DealershipVehicleDefinition> source)
            {
                return source == null
                    ? new DealershipVehicleDefinition[0]
                    : source
                        .Where(x => x != null)
                        .Select(x => new DealershipVehicleDefinition
                        {
                            VehicleId = x.VehicleId,
                            DisplayName = x.DisplayName,
                            ModelName = x.ModelName,
                            Category = x.Category,
                            Price = x.Price,
                        })
                        .ToList();
            }

        private static void MergeExternalObjects(ExternalConfigCatalog externalCatalog, ModConfig config)
        {
            if (externalCatalog == null || config == null)
            {
                return;
            }

            foreach (var pair in externalCatalog.ObjectModels)
            {
                if (pair.Value == null || pair.Value.Count == 0)
                {
                    continue;
                }

                config.ObjectModels[pair.Key] = new List<string>(pair.Value);
            }

            if (!config.ObjectModels.ContainsKey("Box"))
            {
                config.ObjectModels["Box"] = new List<string> { "prop_boxpile_05a" };
            }
        }

        private static List<VehicleCargoType> ResolveCargoTypeOrder(ExternalConfigCatalog externalCatalog)
        {
            var preferredOrder = new[]
            {
                VehicleCargoType.Aggregates,
                VehicleCargoType.OpenHull,
                VehicleCargoType.Wood,
                VehicleCargoType.CraftedGoods,
                VehicleCargoType.Liquid,
                VehicleCargoType.DryBulk,
                VehicleCargoType.Refrigeration,
                VehicleCargoType.Recyclable,
                VehicleCargoType.Vehicles,
            };

            var available = externalCatalog != null && externalCatalog.CargoTypesInOrder.Count > 0
                ? new HashSet<VehicleCargoType>(externalCatalog.CargoTypesInOrder)
                : new HashSet<VehicleCargoType>(preferredOrder);

            var result = new List<VehicleCargoType>();
            for (int i = 0; i < preferredOrder.Length; i++)
            {
                if (available.Contains(preferredOrder[i]))
                {
                    result.Add(preferredOrder[i]);
                }
            }

            return result;
        }
    }
}

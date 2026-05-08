using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    public sealed class ModConfig
    {
        private static readonly string[] ReservedSections =
        {
            "Global",
            "General",
            "Controls",
            "DensityProfiles",
            "GasStationDensityProfiles",
            "StoreDensityProfiles",
            "Markers",
            "MainOffice",
            "VehicleSpawn",
            "Objects",
            "Workers",
        };

        public float OmegaMultiplier { get; private set; }
        public float IndustryOmegaCapacityMultiplier { get; private set; }
        public float MarkerRadius { get; private set; }
        public float MarkerHeight { get; private set; }
        public Vector3 MainOfficePosition { get; private set; }
        public Vector3 VehicleSpawnPosition { get; private set; }
        public float VehicleSpawnHeading { get; private set; }
        public ControlBindings Controls { get; private set; }
        public Dictionary<string, IndustryConfig> IndustryConfigs { get; private set; }
        public List<VehicleDefinition> VehicleDefinitions { get; private set; }
        public ExternalConfigCatalog ExternalCatalog { get; private set; }
        public List<VehicleCargoType> CargoTypes { get; private set; }
        public Dictionary<string, List<string>> ObjectModels { get; private set; }
        public List<string> WorkerModels { get; private set; }
        public Dictionary<string, DistrictConfig> DistrictConfigs { get; private set; }
        public List<string> ValidationMessages { get; private set; }

        public static ModConfig Load(string path)
        {
            var ini = IniFile.Load(path);
            var configDirectory = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "configs");
            var externalCatalog = ExternalConfigCatalog.Load(configDirectory);
            CommodityCatalog.Configure(externalCatalog.ResourceGroups);

            var config = new ModConfig
            {
                OmegaMultiplier = Math.Max(1f, ini.GetFloat("General", "OmegaMultiplier", 2f)),
                IndustryOmegaCapacityMultiplier = Math.Max(0.01f, ini.GetFloat("General", "IndustryOmegaCapacityMultiplier", 0.2f)),
                MarkerRadius = Math.Max(0.2f, ini.GetFloat("Markers", "MarkerRadius", 1.0f)),
                MarkerHeight = Math.Max(0.5f, ini.GetFloat("Markers", "MarkerHeight", 1.0f)),
                MainOfficePosition = ini.GetVector3("MainOffice", "Coordinates", new Vector3(-333.33f, -2778.94f, 5.15f)),
                VehicleSpawnPosition = ini.GetVector3("VehicleSpawn", "Coordinates", new Vector3(-360.04f, -2763.21f, 6f)),
                VehicleSpawnHeading = ini.GetFloat("VehicleSpawn", "VehicleSpawnHeading", ini.GetFloat("VehicleSpawn", "Heading", 230f)),
                Controls = ParseControls(ini),
                ExternalCatalog = externalCatalog,
                IndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase),
                VehicleDefinitions = new List<VehicleDefinition>(),
                CargoTypes = new List<VehicleCargoType>(),
                ObjectModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
                WorkerModels = new List<string>(),
                DistrictConfigs = new Dictionary<string, DistrictConfig>(externalCatalog.Districts, StringComparer.OrdinalIgnoreCase),
                ValidationMessages = new List<string>(externalCatalog.ValidationMessages),
            };

            config.CargoTypes.AddRange(ResolveCargoTypeOrder(externalCatalog));

            if (externalCatalog.Locations.Count > 0)
            {
                MergeExternalLocations(ini, externalCatalog, config);
            }
            else
            {
                ParseIndustries(ini, config);
            }

            if (externalCatalog.VehicleDefinitions.Count > 0)
            {
                config.VehicleDefinitions.AddRange(CloneVehicleDefinitions(externalCatalog.VehicleDefinitions));
            }
            else
            {
                ParseVehicles(ini, config);
            }

            ParseObjects(ini, config);
            MergeExternalObjects(externalCatalog, config);
            ParseWorkers(ini, config);

            if (config.CargoTypes.Count == 0)
            {
                config.CargoTypes.AddRange(BuildFallbackCargoTypes(config));
            }

            return config;
        }

        private static void ParseIndustries(IniFile legacyIni, ExternalConfigCatalog externalCatalog, ModConfig config)
        {
            foreach (var pair in externalCatalog.Locations)
            {
                var location = pair.Value;
                if (location == null || !location.Enabled)
                {
                    continue;
                }

                var standardValues = location.StandardEconomy ?? SiteEconomyPresetValues.Create(0f, 0f, location.IndustryPrice, 0f, 0f, location.FactoryProductionRatio, false);
                var ratio = Math.Max(0.1f, standardValues.ProductionRatio > 0f ? standardValues.ProductionRatio : location.FactoryProductionRatio);
                var industryPrice = Math.Max(0f, standardValues.PurchasePrice);
                var industryLicencePrice = standardValues.PermitRequired ? Math.Max(0f, standardValues.LicencePrice) : 0f;
                var industryOwnerCut = Math.Max(0f, Math.Min(1f, location.IndustryOwnerCut));
                var hasStarterAccess = SiteMetadataParser.GrantsStarterAccess(location.SiteRole, location.OwnershipTier);

                config.IndustryConfigs[pair.Key] = new IndustryConfig
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
                    GatePosition = location.GatePosition,
                    BarrierModelHash = location.BarrierModelHash,
                    WorkerPosition = location.WorkerPosition,
                    DisplayObjectModelHash = location.DisplayObjectModelHash,
                    MaxDisplayObjectLine = location.MaxSpawnedVehiclesLine,
                    MaxDisplayObjectRow = location.MaxSpawnedVehiclesRow,
                    Inputs = new HashSet<string>(location.Inputs, StringComparer.OrdinalIgnoreCase),
                    OptionalInputs = new HashSet<string>(location.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                    Outputs = new HashSet<string>(location.Outputs, StringComparer.OrdinalIgnoreCase),
                    FactoryProductionRatio = ratio,
                    InputCapacityTons = standardValues.InputCapacityTons > 0f ? standardValues.InputCapacityTons : Math.Max(10f, ResolveInputCapacityTons(legacyIni, location)),
                    OutputCapacityTons = standardValues.OutputCapacityTons > 0f ? standardValues.OutputCapacityTons : Math.Max(10f, ResolveOutputCapacityTons(legacyIni, location)),
                    ProductionRate = standardValues.ProductionRate > 0f ? standardValues.ProductionRate * ratio : ResolveProductionRate(legacyIni, location),
                    StartingTankRatio = location.StartingTankRatio,
                    Density = location.Density,
                    EmptyingRate = location.EmptyingRate,
                    HasConfiguredEmptyingRate = location.HasConfiguredEmptyingRate,
                    RefuelIsFree = location.RefuelIsFree,
                    IndustryPrice = industryPrice,
                    IndustryLicencePrice = industryLicencePrice,
                    IndustryOwnerCut = industryOwnerCut,
                    IsOwned = hasStarterAccess || industryPrice <= 0f,
                    HasContractorPermit = hasStarterAccess || !standardValues.PermitRequired || industryLicencePrice <= 0f,
                    IsCsvBacked = true,
                    CasualEconomy = location.CasualEconomy,
                    StandardEconomy = location.StandardEconomy,
                    HardcoreEconomy = location.HardcoreEconomy,
                };
            }
        }

        private static void ParseIndustries(IniFile ini, ModConfig config)
        {
            foreach (var section in ini.Sections)
            {
                if (ReservedSections.Contains(section, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ini.HasKey(section, "Inputs"))
                {
                    continue;
                }

                if (ini.HasKey(section, "Enabled") && !ini.GetBool(section, "Enabled", true))
                {
                    continue;
                }

                var name = ini.GetString(section, "Name", section);
                var position = ini.GetVector3(section, "Coordinates", Vector3.Zero);
                var inputs = new HashSet<string>(
                    ini.GetStringList(section, "Inputs")
                        .Where(x => !x.Equals("None", StringComparison.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);

                var outputs = new HashSet<string>(
                    ini.GetStringList(section, "Outputs")
                        .Where(x => !x.Equals("None", StringComparison.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);

                var inputCapRaw = ini.GetFloat(section, "IndustryInputCapacity", 50000f);
                var outputCapRaw = ini.GetFloat(section, "IndustryOutputCapacity", 50000f);
                var inputCapTons = inputCapRaw / 1000f;
                var outputCapTons = outputCapRaw / 1000f;
                var startingTankRatio = Math.Max(0f, Math.Min(1f, ini.GetFloat(section, "StartingTank", 0f)));
                var density = ini.GetString(section, "Density", "medium");
                var industryPrice = Math.Max(0f, ini.GetFloat(section, "IndustryPrice", 0f));
                var industryLicencePrice = Math.Max(0f, ini.GetFloat(section, "IndustryLicencePrice", 0f));
                var industryOwnerCut = Math.Max(0f, Math.Min(1f, ini.GetFloat(section, "IndustryOwnerCut", 0.5f)));

                var productionRate = ResolveConfiguredProductionRate(ini, section, 30f);

                config.IndustryConfigs[section] = new IndustryConfig
                {
                    CatalogId = section,
                    Id = section,
                    LegacyKey = section,
                    LocationKind = InferLocationKind(section, inputs, outputs),
                    SiteRole = InferLegacySiteRole(section, inputs, outputs),
                    OwnershipTier = SiteOwnershipTier.Unknown,
                    DistrictName = string.Empty,
                    Name = name,
                    Company = string.Empty,
                    Position = position,
                    VehicleSpawnPosition = ini.HasKey(section, "VehicleSpawningCoordinates")
                        ? (Vector3?)ini.GetVector3(section, "VehicleSpawningCoordinates", Vector3.Zero)
                        : null,
                    VehicleSpawnHeading = ini.HasKey(section, "VehicleSpawningHeading")
                        ? (float?)ini.GetFloat(section, "VehicleSpawningHeading", 0f)
                        : null,
                    Inputs = inputs,
                    OptionalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Outputs = outputs,
                    FactoryProductionRatio = Math.Max(0.1f, ini.GetFloat(section, "FactoryProductionRatio", 1f)),
                    InputCapacityTons = Math.Max(10f, inputCapTons),
                    OutputCapacityTons = Math.Max(10f, outputCapTons),
                    ProductionRate = productionRate,
                    StartingTankRatio = startingTankRatio,
                    Density = density,
                    EmptyingRate = 0f,
                    HasConfiguredEmptyingRate = false,
                    RefuelIsFree = false,
                    IndustryPrice = industryPrice,
                    IndustryLicencePrice = industryLicencePrice,
                    IndustryOwnerCut = industryOwnerCut,
                    IsOwned = industryPrice <= 0f,
                    HasContractorPermit = industryLicencePrice <= 0f,
                    IsCsvBacked = false,
                };
            }
        }

        private static void MergeExternalLocations(IniFile ini, ExternalConfigCatalog externalCatalog, ModConfig config)
        {
            if (ini == null || externalCatalog == null || config == null)
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

                config.IndustryConfigs[location.Id] = BuildIndustryConfigFromExternalLocation(ini, location);
            }
        }

        private static IndustryConfig BuildIndustryConfigFromExternalLocation(IniFile ini, ExternalLocationConfig location)
        {
            var standardValues = location.StandardEconomy ?? SiteEconomyPresetValues.Create(0f, 0f, location.IndustryPrice, 0f, 0f, location.FactoryProductionRatio, false);
            var productionRatio = Math.Max(0.1f, standardValues.ProductionRatio > 0f ? standardValues.ProductionRatio : (location.FactoryProductionRatio > 0f ? location.FactoryProductionRatio : 1f));
            var productionRate = standardValues.ProductionRate > 0f
                ? standardValues.ProductionRate * productionRatio
                : ResolveProductionRate(ini, location);
            var inputCapacityTons = standardValues.InputCapacityTons > 0f
                ? standardValues.InputCapacityTons
                : ResolveLocationInputCapacityTons(ini, location);
            var outputCapacityTons = standardValues.OutputCapacityTons > 0f
                ? standardValues.OutputCapacityTons
                : ResolveLocationOutputCapacityTons(ini, location);
            var licencePrice = standardValues.PermitRequired ? Math.Max(0f, standardValues.LicencePrice) : 0f;
            var purchasePrice = Math.Max(0f, standardValues.PurchasePrice);
            var hasStarterAccess = SiteMetadataParser.GrantsStarterAccess(location.SiteRole, location.OwnershipTier);
            var inputs = new HashSet<string>(location.Inputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var outputs = new HashSet<string>(location.Outputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

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
                GatePosition = location.GatePosition,
                BarrierModelHash = location.BarrierModelHash,
                WorkerPosition = location.WorkerPosition,
                DisplayObjectModelHash = location.DisplayObjectModelHash,
                MaxDisplayObjectLine = location.MaxSpawnedVehiclesLine,
                MaxDisplayObjectRow = location.MaxSpawnedVehiclesRow,
                Inputs = inputs,
                OptionalInputs = new HashSet<string>(location.OptionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                Outputs = outputs,
                FactoryProductionRatio = productionRatio,
                InputCapacityTons = inputCapacityTons,
                OutputCapacityTons = outputCapacityTons,
                ProductionRate = productionRate,
                StartingTankRatio = location.StartingTankRatio,
                Density = location.Density,
                EmptyingRate = location.EmptyingRate,
                HasConfiguredEmptyingRate = location.HasConfiguredEmptyingRate,
                RefuelIsFree = location.RefuelIsFree,
                IndustryPrice = purchasePrice,
                IndustryLicencePrice = licencePrice,
                IndustryOwnerCut = Math.Max(0f, Math.Min(1f, location.IndustryOwnerCut)),
                IsOwned = hasStarterAccess || purchasePrice <= 0f,
                HasContractorPermit = hasStarterAccess || !standardValues.PermitRequired || licencePrice <= 0f,
                IsCsvBacked = true,
                CasualEconomy = location.CasualEconomy,
                StandardEconomy = location.StandardEconomy,
                HardcoreEconomy = location.HardcoreEconomy,
            };
        }

        private static float ResolveLocationInputCapacityTons(IniFile ini, ExternalLocationConfig location)
        {
            return Math.Max(10f, ResolveInputCapacityTons(ini, location));
        }

        private static float ResolveLocationOutputCapacityTons(IniFile ini, ExternalLocationConfig location)
        {
            return Math.Max(10f, ResolveOutputCapacityTons(ini, location));
        }

        private static float ResolveConfiguredProductionRate(IniFile ini, string section, float defaultRate)
        {
            var productionRate = ini.GetFloat(section, "ProductionRate", float.NaN);
            if (float.IsNaN(productionRate))
            {
                var block = ini.GetSection(section);
                foreach (var pair in block)
                {
                    if (!pair.Key.EndsWith("ProductionRate", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    float parsed;
                    if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed > 0f)
                    {
                        productionRate = parsed;
                        break;
                    }

                    if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) && parsed > 0f)
                    {
                        productionRate = parsed;
                        break;
                    }
                }
            }

            if (float.IsNaN(productionRate) || productionRate <= 0f)
            {
                return defaultRate;
            }

            return productionRate;
        }

        private static void ParseVehicles(IniFile ini, ModConfig config)
        {
            foreach (var section in ini.Sections)
            {
                if (!ini.HasKey(section, "ModelName") || !ini.HasKey(section, "VehicleCargoType"))
                {
                    continue;
                }

                var enabled = ini.GetBool(section, "Enabled", true);
                if (!enabled)
                {
                    continue;
                }

                var rawModels = ini.GetStringList(section, "ModelName");
                var configuredCargoType = ParseCargoType(ini.GetString(section, "VehicleCargoType", "Unknown"));
                var capacityRaw = ini.GetFloat(section, "VehicleCapacity", 10000f);
                var capacityTons = capacityRaw / 1000f;
                var isTrailerSection = section.IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0;

                for (int i = 0; i < rawModels.Count; i++)
                {
                    var modelName = rawModels[i];
                    var cargoType = NormalizeVehicleCargoType(section, modelName, configuredCargoType);
                    var isTractor = IsTractorDefinition(section, modelName, cargoType);
                    var isTrailer = isTrailerSection && !isTractor;
                    config.VehicleDefinitions.Add(new VehicleDefinition
                    {
                        SectionName = section,
                        ModelName = modelName,
                        CargoType = cargoType,
                        CapacityTons = Math.Max(0f, capacityTons),
                        FuelCapacityLiters = ResolveLegacyFuelCapacityLiters(ini, section, modelName, cargoType, Math.Max(0f, capacityTons), isTractor, isTrailer),
                        IsEnabled = true,
                        IsTrailer = isTrailer,
                        IsTractor = isTractor,
                    });
                }
            }

            EnsureDefaultOpenHullTrailer(config);
        }

        private static void EnsureDefaultOpenHullTrailer(ModConfig config)
        {
            if (config == null)
            {
                return;
            }

            var hasOpenHullTrailer = config.VehicleDefinitions.Any(x =>
                x != null &&
                x.IsEnabled &&
                x.IsTrailer &&
                x.CargoType == VehicleCargoType.OpenHull);

            if (hasOpenHullTrailer)
            {
                return;
            }

            config.VehicleDefinitions.Add(new VehicleDefinition
            {
                SectionName = "OpenHullTrailers",
                ModelName = "trflat",
                CargoType = VehicleCargoType.OpenHull,
                CapacityTons = 30f,
                FuelCapacityLiters = 0f,
                IsEnabled = true,
                IsTrailer = true,
                IsTractor = false,
            });
        }

        private static VehicleCargoType NormalizeVehicleCargoType(string section, string modelName, VehicleCargoType cargoType)
        {
            if (string.Equals(modelName, "trailerlogs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(section, "LogsTrailer", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Wood;
            }

            return cargoType;
        }

        private static bool IsTractorDefinition(string section, string modelName, VehicleCargoType cargoType)
        {
            if (string.Equals(modelName, "trailerlogs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(section, "LogsTrailer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return cargoType == VehicleCargoType.Trailer ||
                section.Equals("Trucks", StringComparison.OrdinalIgnoreCase) ||
                section.IndexOf("Tractor", StringComparison.OrdinalIgnoreCase) >= 0;
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
                    IsEnabled = x.IsEnabled,
                    IsTrailer = x.IsTrailer,
                    IsTractor = x.IsTractor,
                })
                .ToList();
        }

        private static float ResolveLegacyFuelCapacityLiters(IniFile ini, string section, string modelName, VehicleCargoType cargoType, float capacityTons, bool isTractor, bool isTrailer)
        {
            if (isTrailer || cargoType == VehicleCargoType.Trailer)
            {
                return 0f;
            }

            var configuredValue = ResolveFuelCapacityOverride(ini, section, modelName);
            if (configuredValue > 0f)
            {
                return configuredValue;
            }

            if (isTractor)
            {
                return 400f;
            }

            if (capacityTons >= 18f)
            {
                return 200f;
            }

            if (capacityTons >= 8f)
            {
                return 150f;
            }

            return 90f;
        }

        private static float ResolveFuelCapacityOverride(IniFile ini, string section, string modelName)
        {
            if (ini == null)
            {
                return 0f;
            }

            var aliases = new[]
            {
                "VehicleFuelCapacity",
                "FuelCapacity",
            };

            for (int i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (ini.HasKey(section, alias))
                {
                    return Math.Max(0f, ini.GetFloat(section, alias, 0f));
                }

                if (!string.IsNullOrWhiteSpace(modelName) && ini.HasKey(modelName, alias))
                {
                    return Math.Max(0f, ini.GetFloat(modelName, alias, 0f));
                }
            }

            return 0f;
        }

        private static void ParseObjects(IniFile ini, ModConfig config)
        {
            var block = ini.GetSection("Objects");
            foreach (var pair in block)
            {
                var list = pair.Value
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (list.Count > 0)
                {
                    config.ObjectModels[pair.Key] = list;
                }
            }

            if (!config.ObjectModels.ContainsKey("Box"))
            {
                config.ObjectModels["Box"] = new List<string> { "prop_boxpile_05a" };
            }
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

        private static void ParseWorkers(IniFile ini, ModConfig config)
        {
            var configured = ini.GetStringList("Workers", "Models");
            if (configured.Count > 0)
            {
                config.WorkerModels.AddRange(configured);
                return;
            }

            config.WorkerModels.Add("s_m_m_dockwork_01");
            config.WorkerModels.Add("s_m_y_construct_01");
            config.WorkerModels.Add("s_m_m_trucker_01");
        }

        private static ControlBindings ParseControls(IniFile ini)
        {
            var controls = new ControlBindings();
            controls.ToggleDashboard = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "ToggleDashboard", controls.ToggleDashboard.ToString()),
                controls.ToggleDashboard);
            controls.ToggleContext = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "ToggleContext", controls.ToggleContext.ToString()),
                controls.ToggleContext);
            controls.OpenModMenu = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "OpenModMenu", controls.OpenModMenu.ToString()),
                controls.OpenModMenu);
            controls.Interact = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "Interact", controls.Interact.ToString()),
                controls.Interact);
            controls.GateInteract = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "GateInteract", controls.GateInteract.ToString()),
                controls.GateInteract);
            controls.OpenUpgrade = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "OpenUpgrade", controls.OpenUpgrade.ToString()),
                controls.OpenUpgrade);

            controls.MenuUp = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuUp", controls.MenuUp.ToString()),
                controls.MenuUp);
            controls.MenuDown = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuDown", controls.MenuDown.ToString()),
                controls.MenuDown);
            controls.MenuLeft = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuLeft", controls.MenuLeft.ToString()),
                controls.MenuLeft);
            controls.MenuRight = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuRight", controls.MenuRight.ToString()),
                controls.MenuRight);
            controls.MenuSelect = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuSelect", controls.MenuSelect.ToString()),
                controls.MenuSelect);
            controls.MenuBack = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "MenuBack", "Backspace"),
                controls.MenuBack);

            return controls;
        }

        private static VehicleCargoType ParseCargoType(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
            if (normalized.Length == 0)
            {
                return VehicleCargoType.Unknown;
            }

            if (normalized.Equals("Aggregates", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Loose", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Aggregates;
            }

            if (normalized.Equals("OpenHull", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Solid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.OpenHull;
            }

            if (normalized.Equals("Wood", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Wood;
            }

            if (normalized.Equals("CraftedGoods", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Crate", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.CraftedGoods;
            }

            if (normalized.Equals("Liquid", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Fluid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Liquid;
            }

            if (normalized.Equals("DryBulk", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.DryBulk;
            }

            if (normalized.Equals("Refrigeration", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Refrigeration;
            }

            if (normalized.Equals("Recyclable", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Recyclables", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Recyclable;
            }

            if (normalized.Equals("Vehicles", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Vehicles;
            }

            if (normalized.Equals("Trailer", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Trailer;
            }

                return VehicleCargoType.Unknown;
        }

        private static float ResolveInputCapacityTons(IniFile legacyIni, ExternalLocationConfig location)
        {
            float rawCapacity;
            switch (location.Kind)
            {
                case ExternalLocationKind.Industry:
                    rawCapacity = legacyIni.GetFloat(location.Id, "IndustryInputCapacity", 50000f);
                    break;
                case ExternalLocationKind.Store:
                    rawCapacity = legacyIni.GetFloat(
                        location.Id,
                        "StoreInputCapacity",
                        ResolveDensityProfileCapacityRaw(legacyIni, "StoreDensityProfiles", location.Density, 60000f));
                    break;
                case ExternalLocationKind.GasStation:
                    rawCapacity = ResolveDensityProfileCapacityRaw(legacyIni, "GasStationDensityProfiles", location.Density, 60000f);
                    break;
                default:
                    rawCapacity = 50000f;
                    break;
            }

            return rawCapacity / 1000f;
        }

        private static float ResolveOutputCapacityTons(IniFile legacyIni, ExternalLocationConfig location)
        {
            if (location.Kind != ExternalLocationKind.Industry || location.Outputs.Count == 0)
            {
                return 10f;
            }

            return legacyIni.GetFloat(location.Id, "IndustryOutputCapacity", 50000f) / 1000f;
        }

        private static float ResolveProductionRate(IniFile legacyIni, ExternalLocationConfig location)
        {
            if (location != null && location.SiteRole == SiteRole.Warehouse)
            {
                return 0f;
            }

            var productionRate = legacyIni.GetFloat(location.Id, "ProductionRate", float.NaN);
            if (float.IsNaN(productionRate))
            {
                productionRate = legacyIni.GetFloat(location.Id, "IndustryProductionRate", float.NaN);
            }

            if (float.IsNaN(productionRate))
            {
                var block = legacyIni.GetSection(location.Id);
                foreach (var pair in block)
                {
                    if (!pair.Key.EndsWith("ProductionRate", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    float parsed;
                    if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed > 0f)
                    {
                        productionRate = parsed;
                        break;
                    }

                    if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) && parsed > 0f)
                    {
                        productionRate = parsed;
                        break;
                    }
                }
            }

            if (float.IsNaN(productionRate) || productionRate <= 0f)
            {
                productionRate = 30f;
            }

            var ratio = location.Kind == ExternalLocationKind.Industry
                ? Math.Max(0.1f, location.FactoryProductionRatio)
                : 1f;

            return Math.Max(1f, productionRate * ratio);
        }

        private static float ResolveDensityProfileCapacityRaw(IniFile legacyIni, string section, string density, float defaultCapacity)
        {
            var densityKey = NormalizeDensityKey(density);
            return legacyIni.GetFloat(section, densityKey + "Capacity", defaultCapacity);
        }

        private static string NormalizeDensityKey(string density)
        {
            var normalized = (density ?? string.Empty).Trim().Replace(" ", string.Empty).ToLowerInvariant();
            if (normalized == "verylow")
            {
                return "VeryLow";
            }

            if (normalized == "low")
            {
                return "Low";
            }

            if (normalized == "high")
            {
                return "High";
            }

            if (normalized == "veryhigh")
            {
                return "VeryHigh";
            }

            return "Medium";
        }

        private static ExternalLocationKind InferLocationKind(string section, HashSet<string> inputs, HashSet<string> outputs)
        {
            if (!string.IsNullOrWhiteSpace(section) && section.IndexOf("Petrol Station", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ExternalLocationKind.GasStation;
            }

            var hasFuelOnlyInput = inputs != null && inputs.Count == 1 && inputs.Contains("Fuel");
            if ((outputs == null || outputs.Count == 0) && hasFuelOnlyInput)
            {
                return ExternalLocationKind.GasStation;
            }

            if (!string.IsNullOrWhiteSpace(section) && section.StartsWith("Store", StringComparison.OrdinalIgnoreCase))
            {
                return ExternalLocationKind.Store;
            }

            if ((outputs == null || outputs.Count == 0) && inputs != null && inputs.Count > 0)
            {
                return ExternalLocationKind.Store;
            }

            return ExternalLocationKind.Industry;
        }

        private static SiteRole InferLegacySiteRole(string section, HashSet<string> inputs, HashSet<string> outputs)
        {
            if (string.Equals(section, "MainOffice", StringComparison.OrdinalIgnoreCase))
            {
                return SiteRole.StarterHQ;
            }

            var locationKind = InferLocationKind(section, inputs, outputs);
            if (locationKind == ExternalLocationKind.Store)
            {
                return SiteRole.StoreSink;
            }

            if (locationKind == ExternalLocationKind.GasStation)
            {
                return SiteRole.FuelSink;
            }

            if (string.Equals(section, "RecyclingCenter", StringComparison.OrdinalIgnoreCase))
            {
                return SiteRole.RecyclingHub;
            }

            if (string.Equals(section, "OmegaFactory", StringComparison.OrdinalIgnoreCase))
            {
                return SiteRole.SpecialPlant;
            }

            if (outputs == null || outputs.Count == 0)
            {
                return SiteRole.Warehouse;
            }

            if (inputs == null || inputs.Count == 0)
            {
                return SiteRole.RawProducer;
            }

            return SiteRole.ProcessingPlant;
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

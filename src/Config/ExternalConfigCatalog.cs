using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA.Math;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Config
{
    public sealed class ExternalConfigCatalog
    {
        public ExternalConfigCatalog()
        {
            Locations = new Dictionary<string, ExternalLocationConfig>(StringComparer.OrdinalIgnoreCase);
            ResourceGroups = new List<ResourceGroupConfig>();
            ResourcesByCommodity = new Dictionary<string, ExternalResourceConfig>(StringComparer.OrdinalIgnoreCase);
            ObjectModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, ExternalLocationConfig> Locations { get; }
        public List<ResourceGroupConfig> ResourceGroups { get; }
        public Dictionary<string, ExternalResourceConfig> ResourcesByCommodity { get; }
        public Dictionary<string, List<string>> ObjectModels { get; }

        public IReadOnlyList<VehicleCargoType> CargoTypesInOrder
        {
            get
            {
                var result = new List<VehicleCargoType>();
                for (int i = 0; i < ResourceGroups.Count; i++)
                {
                    var cargoType = ResourceGroups[i].CargoType;
                    if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer || result.Contains(cargoType))
                    {
                        continue;
                    }

                    result.Add(cargoType);
                }

                return result;
            }
        }

        public static ExternalConfigCatalog Load(string configDirectory)
        {
            var catalog = new ExternalConfigCatalog();

            ParseLocations(LoadConfigIni(configDirectory, "Industries.ini"), ExternalLocationKind.Industry, "Industries.ini", catalog);
            ParseLocations(LoadConfigIni(configDirectory, "Stores.ini"), ExternalLocationKind.Store, "Stores.ini", catalog);
            ParseLocations(LoadConfigIni(configDirectory, "GasStations.ini"), ExternalLocationKind.GasStation, "GasStations.ini", catalog);
            ParseResources(LoadConfigIni(configDirectory, "Resources.ini"), catalog);
            ParseObjects(LoadConfigIni(configDirectory, "Objects.ini"), catalog);

            return catalog;
        }

        private static IniFile LoadConfigIni(string configDirectory, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                var filePath = Path.Combine(configDirectory, fileName);
                if (File.Exists(filePath))
                {
                    return IniFile.Load(filePath);
                }
            }

            string content;
            if (TryReadEmbeddedConfig(fileName, out content))
            {
                return IniFile.LoadFromString(content);
            }

            return IniFile.LoadFromString(string.Empty);
        }

        private static bool TryReadEmbeddedConfig(string fileName, out string content)
        {
            content = null;
            var resourceName = "IndustryLogisticV.Configs." + fileName;
            using (var stream = typeof(ExternalConfigCatalog).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return false;
                }

                using (var reader = new StreamReader(stream))
                {
                    content = reader.ReadToEnd();
                    return true;
                }
            }
        }

        private static void ParseLocations(IniFile ini, ExternalLocationKind kind, string sourceFileName, ExternalConfigCatalog catalog)
        {
            foreach (var section in ini.Sections)
            {
                if (section.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var location = new ExternalLocationConfig
                {
                    Id = section,
                    Kind = kind,
                    SourceFileName = sourceFileName,
                    Enabled = ini.GetBool(section, "Enabled", true),
                    Name = ini.GetString(section, "Name", section),
                    Company = ini.GetString(section, "Company", string.Empty),
                    Position = ini.GetVector3(section, "Coordinates", Vector3.Zero),
                    VehicleSpawnPosition = GetOptionalVector3(ini, section, "VehicleSpawningCoordinates"),
                    VehicleSpawnHeading = GetOptionalFloat(ini, section, "VehicleSpawningHeading"),
                    FactoryDoorPosition = GetOptionalVector3(ini, section, "FactoryDoorCoordinates"),
                    FactoryProductionRatio = ini.GetFloat(section, "FactoryProductionRatio", 1f),
                    Inputs = GetCommoditySet(ini.GetStringList(section, "Inputs")),
                    Outputs = GetCommoditySet(ini.GetStringList(section, "Outputs")),
                    Density = ini.GetString(section, "Density", "medium"),
                    StartingTankRatio = Math.Max(0f, Math.Min(1f, ini.GetFloat(section, "StartingTank", 0f))),
                    SpawnedVehicleModel = ini.GetString(section, "SpawnedVehicleModel", string.Empty),
                    SpawnedVehiclePosition = GetOptionalVector3(ini, section, "SpawnedVehicleCoordinates"),
                    SpawnedVehicleHeading = GetOptionalFloat(ini, section, "SpawnedVehicleHeading"),
                    MaxSpawnedVehiclesLine = GetOptionalInt(ini, section, "MaxSpawnedVehiclesLine"),
                    MaxSpawnedVehiclesRow = GetOptionalInt(ini, section, "MaxSpawnedVehiclesRow"),
                    ObjectToDelete = ini.GetString(section, "ObjectToDelete", string.Empty),
                };

                catalog.Locations[section] = location;
            }
        }

        private static void ParseResources(IniFile ini, ExternalConfigCatalog catalog)
        {
            var block = ini.GetSection("Global");
            foreach (var pair in block)
            {
                var cargoType = ParseCargoType(pair.Key);
                if (cargoType == VehicleCargoType.Unknown)
                {
                    continue;
                }

                var commodities = pair.Value
                    .Split(',')
                    .Select(CommodityCatalog.Normalize)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                catalog.ResourceGroups.Add(new ResourceGroupConfig
                {
                    Name = pair.Key,
                    CargoType = cargoType,
                    Commodities = commodities,
                });

                for (int i = 0; i < commodities.Count; i++)
                {
                    var commodity = commodities[i];
                    catalog.ResourcesByCommodity[commodity] = new ExternalResourceConfig
                    {
                        Commodity = commodity,
                        GroupName = pair.Key,
                        CargoType = cargoType,
                    };
                }
            }
        }

        private static void ParseObjects(IniFile ini, ExternalConfigCatalog catalog)
        {
            var block = ini.GetSection("Global");
            foreach (var pair in block)
            {
                var modelNames = pair.Value
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (modelNames.Count == 0)
                {
                    continue;
                }

                catalog.ObjectModels[pair.Key] = modelNames;
            }
        }

        private static HashSet<string> GetCommoditySet(List<string> commodities)
        {
            return new HashSet<string>(
                commodities
                    .Select(CommodityCatalog.Normalize)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("None", StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static Vector3? GetOptionalVector3(IniFile ini, string section, string key)
        {
            if (!ini.HasKey(section, key))
            {
                return null;
            }

            return ini.GetVector3(section, key, Vector3.Zero);
        }

        private static float? GetOptionalFloat(IniFile ini, string section, string key)
        {
            if (!ini.HasKey(section, key))
            {
                return null;
            }

            return ini.GetFloat(section, key, 0f);
        }

        private static int? GetOptionalInt(IniFile ini, string section, string key)
        {
            if (!ini.HasKey(section, key))
            {
                return null;
            }

            return (int)Math.Round(ini.GetFloat(section, key, 0f));
        }

        private static VehicleCargoType ParseCargoType(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
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
    }

    public enum ExternalLocationKind
    {
        Industry = 0,
        Store = 1,
        GasStation = 2,
    }

    public sealed class ExternalLocationConfig
    {
        public string Id { get; set; }
        public ExternalLocationKind Kind { get; set; }
        public string SourceFileName { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Company { get; set; }
        public Vector3 Position { get; set; }
        public Vector3? VehicleSpawnPosition { get; set; }
        public float? VehicleSpawnHeading { get; set; }
        public Vector3? FactoryDoorPosition { get; set; }
        public float FactoryProductionRatio { get; set; }
        public HashSet<string> Inputs { get; set; }
        public HashSet<string> Outputs { get; set; }
        public string Density { get; set; }
        public float StartingTankRatio { get; set; }
        public string SpawnedVehicleModel { get; set; }
        public Vector3? SpawnedVehiclePosition { get; set; }
        public float? SpawnedVehicleHeading { get; set; }
        public int? MaxSpawnedVehiclesLine { get; set; }
        public int? MaxSpawnedVehiclesRow { get; set; }
        public string ObjectToDelete { get; set; }
    }

    public sealed class ResourceGroupConfig
    {
        public string Name { get; set; }
        public VehicleCargoType CargoType { get; set; }
        public List<string> Commodities { get; set; }
    }

    public sealed class ExternalResourceConfig
    {
        public string Commodity { get; set; }
        public string GroupName { get; set; }
        public VehicleCargoType CargoType { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    public sealed class ExternalConfigCatalog
    {
        public ExternalConfigCatalog()
        {
            Locations = new Dictionary<string, ExternalLocationConfig>(StringComparer.OrdinalIgnoreCase);
            Districts = new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase);
            ResourceGroups = new List<ResourceGroupConfig>();
            ResourcesByCommodity = new Dictionary<string, ExternalResourceConfig>(StringComparer.OrdinalIgnoreCase);
            CommodityBasePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            ObjectModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            VehicleDefinitions = new List<VehicleDefinition>();
            BankDefinitions = new List<BankDefinition>();
            OfficeDefinitions = new List<OfficeDefinition>();
            OfficeObjectDefinitions = new List<OfficeObjectDefinition>();
            InteriorDefinitions = new List<InteriorDefinition>();
            PersonalVehicleDefinitions = new List<DealershipVehicleDefinition>();
            ValidationMessages = new List<string>();
        }

        public Dictionary<string, ExternalLocationConfig> Locations { get; }
        public Dictionary<string, DistrictConfig> Districts { get; }
        public List<ResourceGroupConfig> ResourceGroups { get; }
        public Dictionary<string, ExternalResourceConfig> ResourcesByCommodity { get; }
        public Dictionary<string, float> CommodityBasePrices { get; }
        public Dictionary<string, List<string>> ObjectModels { get; }
        public List<VehicleDefinition> VehicleDefinitions { get; }
        public List<BankDefinition> BankDefinitions { get; }
        public List<OfficeDefinition> OfficeDefinitions { get; }
        public List<OfficeObjectDefinition> OfficeObjectDefinitions { get; }
        public List<InteriorDefinition> InteriorDefinitions { get; }
        public List<DealershipVehicleDefinition> PersonalVehicleDefinitions { get; }
        public List<string> ValidationMessages { get; }

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

        public static ExternalConfigCatalog Load(string configDirectory, LsolAddonCatalog addonCatalog = null)
        {
            var catalog = new ExternalConfigCatalog();
            XmlConfigImport.TryPopulateResources(configDirectory, catalog);
            CommodityCatalog.Configure(catalog.ResourceGroups);
            XmlConfigImport.TryPopulateObjects(configDirectory, catalog);
            XmlConfigImport.TryPopulateDistricts(configDirectory, catalog);
            XmlConfigImport.TryPopulateSites(configDirectory, catalog);
            XmlConfigImport.TryPopulateVehicles(configDirectory, catalog);
            XmlConfigImport.TryPopulateBanks(configDirectory, catalog);
            XmlConfigImport.TryPopulateOffices(configDirectory, catalog);
            XmlConfigImport.TryPopulateOfficeObjects(configDirectory, catalog);
            XmlConfigImport.TryPopulateInteriors(configDirectory, catalog);
            XmlConfigImport.TryPopulateDealershipVehicles(configDirectory, catalog);

            if (addonCatalog != null)
            {
                catalog.ValidationMessages.AddRange(addonCatalog.ValidationMessages);
                XmlConfigImport.TryPopulateAddonResources(addonCatalog, catalog);
                CommodityCatalog.Configure(catalog.ResourceGroups);
                XmlConfigImport.TryPopulateAddonSites(addonCatalog, catalog);
                XmlConfigImport.TryPopulateAddonVehicles(addonCatalog, catalog);
                XmlConfigImport.TryPopulateAddonOfficeObjects(addonCatalog, catalog);
            }

            return catalog;
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
        public string CatalogId { get; set; }
        public string Id { get; set; }
        public string LegacyKey { get; set; }
        public ExternalLocationKind Kind { get; set; }
        public SiteRole SiteRole { get; set; }
        public SiteOwnershipTier OwnershipTier { get; set; }
        public string SourceFileName { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string DistrictName { get; set; }
        public string Company { get; set; }
        public Vector3 Position { get; set; }
        public Vector3? VehicleSpawnPosition { get; set; }
        public float? VehicleSpawnHeading { get; set; }
        public Vector3? GatePosition { get; set; }
        public int? BarrierModelHash { get; set; }
        public Vector3? WorkerPosition { get; set; }
        public Vector3? FactoryDoorPosition { get; set; }
        public float FactoryProductionRatio { get; set; }
        public HashSet<string> Inputs { get; set; }
        public HashSet<string> OptionalInputs { get; set; }
        public HashSet<string> BoostInputs { get; set; }
        public HashSet<string> Outputs { get; set; }
        public Dictionary<string, float> RecipeInputWeights { get; set; }
        public Dictionary<string, float> RecipeOutputWeights { get; set; }
        public Dictionary<string, float> InputCapacityWeights { get; set; }
        public Dictionary<string, float> OutputCapacityWeights { get; set; }
        public string Density { get; set; }
        public float StartingTankRatio { get; set; }
        public float EmptyingRate { get; set; }
        public float WeeklyPassiveIncome { get; set; }
        public bool HasConfiguredEmptyingRate { get; set; }
        public bool RefuelIsFree { get; set; }
        public float IndustryPrice { get; set; }
        public float IndustryOwnerCut { get; set; }
        public float DeliveryPayoutMultiplier { get; set; }
        public string SpawnedVehicleModel { get; set; }
        public Vector3? SpawnedVehiclePosition { get; set; }
        public float? SpawnedVehicleHeading { get; set; }
        public bool DisplayObjectsAtGroundLevel { get; set; }
        public int? MaxSpawnedVehiclesLine { get; set; }
        public int? MaxSpawnedVehiclesRow { get; set; }
        public int? DisplayObjectModelHash { get; set; }
        public string ObjectToDelete { get; set; }
        public SiteEconomyPresetValues CasualEconomy { get; set; }
        public SiteEconomyPresetValues StandardEconomy { get; set; }
        public SiteEconomyPresetValues HardcoreEconomy { get; set; }
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
        public float BasePrice { get; set; }
    }
}
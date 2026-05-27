using System.Collections.Generic;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    public sealed class IndustryConfig
    {
        public string CatalogId { get; set; }
        public string Id { get; set; }
        public string LegacyKey { get; set; }
        public ExternalLocationKind LocationKind { get; set; }
        public SiteRole SiteRole { get; set; }
        public SiteOwnershipTier OwnershipTier { get; set; }
        public string DistrictName { get; set; }
        public string Name { get; set; }
        public string Company { get; set; }
        public Vector3 Position { get; set; }
        public Vector3? VehicleSpawnPosition { get; set; }
        public float? VehicleSpawnHeading { get; set; }
        public Vector3? SpawnedVehiclePosition { get; set; }
        public float? SpawnedVehicleHeading { get; set; }
        public Vector3? GatePosition { get; set; }
        public Vector3? WorkerPosition { get; set; }
        public int? DisplayObjectModelHash { get; set; }
        public bool DisplayObjectsAtGroundLevel { get; set; }
        public int? MaxDisplayObjectLine { get; set; }
        public int? MaxDisplayObjectRow { get; set; }
        public List<int> ObjectToDeleteModelHashes { get; set; }
        public HashSet<string> Inputs { get; set; }
        public HashSet<string> OptionalInputs { get; set; }
        public HashSet<string> BoostInputs { get; set; }
        public HashSet<string> Outputs { get; set; }
        public Dictionary<string, float> RecipeInputWeights { get; set; }
        public Dictionary<string, float> RecipeOutputWeights { get; set; }
        public List<IndustryRecipeVariantConfig> RecipeVariants { get; set; }
        public Dictionary<string, float> InputCapacityWeights { get; set; }
        public Dictionary<string, float> OutputCapacityWeights { get; set; }
        public Dictionary<string, float> SinkPreferenceWeights { get; set; }
        public float SinkElasticityMultiplier { get; set; } = 1f;
        public float FactoryProductionRatio { get; set; }
        public float InputCapacityTons { get; set; }
        public float OutputCapacityTons { get; set; }
        public float ProductionRate { get; set; }
        public float StartingTankRatio { get; set; }
        public string Density { get; set; }
        public float EmptyingRate { get; set; }
        public float WeeklyPassiveIncome { get; set; }
        public bool HasConfiguredEmptyingRate { get; set; }
        public bool RefuelIsFree { get; set; }
        public float IndustryPrice { get; set; }
        public float IndustryLicencePrice { get; set; }
        public float IndustryOwnerCut { get; set; }
        public float DeliveryPayoutMultiplier { get; set; }
        public bool IsOwned { get; set; }
        public bool HasContractorPermit { get; set; }
        // Authored site configs from the external location catalog carry their own
        // starting-state, sink-drain, and per-preset economy semantics.
        public bool UsesAuthoredSiteSemantics { get; set; }
        public SiteEconomyPresetValues CasualEconomy { get; set; }
        public SiteEconomyPresetValues StandardEconomy { get; set; }
        public SiteEconomyPresetValues HardcoreEconomy { get; set; }
        public SiteEconomyPresetValues ImpossibleEconomy { get; set; }
    }

    public sealed class SiteEconomyPresetValues
    {
        public float ProductionRate { get; set; }
        public float LicencePrice { get; set; }
        public float PurchasePrice { get; set; }
        public float InputCapacityTons { get; set; }
        public float OutputCapacityTons { get; set; }
        public float ProductionRatio { get; set; }
        public bool PermitRequired { get; set; }

        public static SiteEconomyPresetValues Create(float productionRate, float licencePrice, float purchasePrice, float inputCapacityTons, float outputCapacityTons, float productionRatio, bool permitRequired)
        {
            return new SiteEconomyPresetValues
            {
                ProductionRate = productionRate,
                LicencePrice = licencePrice,
                PurchasePrice = purchasePrice,
                InputCapacityTons = inputCapacityTons,
                OutputCapacityTons = outputCapacityTons,
                ProductionRatio = productionRatio,
                PermitRequired = permitRequired,
            };
        }
    }

    public sealed class IndustryRecipeVariantConfig
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public int SelectionPriority { get; set; }

        public HashSet<string> Inputs { get; set; }

        public HashSet<string> OptionalInputs { get; set; }

        public HashSet<string> BoostInputs { get; set; }

        public HashSet<string> Outputs { get; set; }

        public Dictionary<string, float> RecipeInputWeights { get; set; }

        public Dictionary<string, float> RecipeOutputWeights { get; set; }

        public Dictionary<string, float> OptionalInputWeights { get; set; }

        public Dictionary<string, float> InputCapacityWeights { get; set; }

        public Dictionary<string, float> OutputCapacityWeights { get; set; }
    }
}

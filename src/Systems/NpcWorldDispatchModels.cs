using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum NpcWorldDispatchPolicy
    {
        Balanced = 0,
        OverflowRescue = 1,
        ShortageRelief = 2,
        MarketPriority = 3,
    }

    public enum NpcWorldJobType
    {
        OverflowRescue = 0,
        ShortageRelief = 1,
        ExternalImport = 2,
        ExternalExport = 3,
        WarehouseBalancing = 4,
        ServiceRun = 5,
        RivalFreight = 6,
    }

    public enum NpcWorldJobPhase
    {
        Listed = 0,
        Traveling = 1,
        Completed = 2,
        Cancelled = 3,
    }

    public sealed class NpcWorldDispatchOverview
    {
        public bool Enabled { get; set; }

        public int ActiveJobCount { get; set; }

        public int ListedOpportunityCount { get; set; }

        public int RivalJobCount { get; set; }

        public int VisibleConvoyCount { get; set; }

        public int CompletedDispatchCount { get; set; }

        public NpcWorldDispatchPolicy DispatchPolicy { get; set; }

        public string PriorityCommodity { get; set; }

        public string PriorityDistrict { get; set; }

        public bool PremiumDispatchEnabled { get; set; }

        public string DispatchHeadline { get; set; }

        public string DispatchDetail { get; set; }
    }

    public sealed class NpcWorldJobSummary
    {
        public int Id { get; set; }

        public NpcWorldJobType Type { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public string Commodity { get; set; }

        public string SourceLabel { get; set; }

        public string DestinationLabel { get; set; }

        public float Tons { get; set; }

        public int RemainingInGameMinutes { get; set; }

        public bool IsSpotOpportunity { get; set; }

        public bool UsesPremiumDispatch { get; set; }

        public bool IsRivalJob { get; set; }

        public bool HasVisibleConvoy { get; set; }
    }

    internal sealed class NpcWorldLogisticsJob
    {
        public int Id { get; set; }

        public NpcWorldJobType Type { get; set; }

        public NpcWorldJobPhase Phase { get; set; }

        public string Commodity { get; set; }

        public string SourceLabel { get; set; }

        public string DestinationLabel { get; set; }

        public string OriginIndustryId { get; set; }

        public string DestinationIndustryId { get; set; }

        public float Tons { get; set; }

        public int RemainingInGameMinutes { get; set; }

        public int TotalInGameMinutes { get; set; }

        public int CreatedClockMinute { get; set; }

        public bool IsSpotOpportunity { get; set; }

        public bool UsesPremiumDispatch { get; set; }

        public bool IsPriorityMatch { get; set; }

        public bool HasVisibleConvoy { get; set; }

        public bool IsRivalJob { get; set; }

        public int BackhaulDepth { get; set; }

        public string StatusText { get; set; }

        public NpcLogisticsContract VisualRoute { get; set; }
    }

    internal sealed class NpcWorldJobCandidate
    {
        public NpcWorldJobType Type { get; set; }

        public Industry OriginIndustry { get; set; }

        public Industry DestinationIndustry { get; set; }

        public string Commodity { get; set; }

        public string SourceLabel { get; set; }

        public string DestinationLabel { get; set; }

        public float Tons { get; set; }

        public float Score { get; set; }

        public bool IsSpotOpportunity { get; set; }

        public bool IsPriorityMatch { get; set; }

        public bool UsesPremiumDispatch { get; set; }

        public bool HasVisibleConvoy { get; set; }

        public bool IsRivalJob { get; set; }

        public int ListingLeadTimeMinutes { get; set; }

        public int BackhaulDepth { get; set; }
    }

    internal sealed class NpcWorldDispatchConfig
    {
        public bool Enabled { get; set; } = true;

        public int EvaluationIntervalMinutes { get; set; } = 180;

        public int MaxActiveJobs { get; set; } = 8;

        public float OverflowThreshold { get; set; } = 0.82f;

        public float ShortageThreshold { get; set; } = 0.20f;

        public float WarehouseOverflowThreshold { get; set; } = 0.75f;

        public float ServiceShortageThreshold { get; set; } = 0.18f;

        public float MinDispatchTons { get; set; } = 2.5f;

        public float MaxDispatchTons { get; set; } = 14f;

        public int ListingLeadTimeMinutes { get; set; } = 120;

        public int BaseTravelMinutes { get; set; } = 240;

        public float VisualSpawnChance { get; set; } = 1f;

        public float RivalJobChance { get; set; } = 0.20f;

        public float SpotOpportunityChance { get; set; } = 0.30f;

        public float BackhaulSearchRadius { get; set; } = 2500f;

        public float DistrictSupportWeight { get; set; } = 0.35f;

        public float PriorityScoreBonus { get; set; } = 0.30f;

        public float PremiumDispatchScoreBonus { get; set; } = 0.20f;

        public float PremiumDispatchCostMultiplier { get; set; } = 1.35f;

        public float ExternalImportPremiumMultiplier { get; set; } = 1.15f;

        public float ExternalExportDiscountMultiplier { get; set; } = 0.70f;

        public IReadOnlyList<string> ServiceCommodities { get; set; } = new[] { "Fuel", "Water", "LiquidFertilizer" };
    }

    internal static class NpcWorldDispatchConfigLoader
    {
        public static NpcWorldDispatchConfig Load(string configDirectory)
        {
            var config = new NpcWorldDispatchConfig();
            var filePath = Path.Combine(configDirectory ?? string.Empty, "WorldNpcLogistics.xml");
            if (!File.Exists(filePath))
            {
                return config;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(filePath, LoadOptions.None);
            }
            catch
            {
                return config;
            }

            var root = document.Root;
            if (root == null)
            {
                return config;
            }

            config.Enabled = ReadBoolAttribute(root, "enabled", config.Enabled);
            config.EvaluationIntervalMinutes = Math.Max(30, ReadIntAttribute(root, "evaluationIntervalMinutes", config.EvaluationIntervalMinutes));
            config.MaxActiveJobs = Math.Max(1, ReadIntAttribute(root, "maxActiveJobs", config.MaxActiveJobs));
            config.OverflowThreshold = Clamp01(ReadFloatAttribute(root, "overflowThreshold", config.OverflowThreshold));
            config.ShortageThreshold = Clamp01(ReadFloatAttribute(root, "shortageThreshold", config.ShortageThreshold));
            config.WarehouseOverflowThreshold = Clamp01(ReadFloatAttribute(root, "warehouseOverflowThreshold", config.WarehouseOverflowThreshold));
            config.ServiceShortageThreshold = Clamp01(ReadFloatAttribute(root, "serviceShortageThreshold", config.ServiceShortageThreshold));
            config.MinDispatchTons = Math.Max(0.5f, ReadFloatAttribute(root, "minDispatchTons", config.MinDispatchTons));
            config.MaxDispatchTons = Math.Max(config.MinDispatchTons, ReadFloatAttribute(root, "maxDispatchTons", config.MaxDispatchTons));
            config.ListingLeadTimeMinutes = Math.Max(15, ReadIntAttribute(root, "listingLeadTimeMinutes", config.ListingLeadTimeMinutes));
            config.BaseTravelMinutes = Math.Max(30, ReadIntAttribute(root, "baseTravelMinutes", config.BaseTravelMinutes));
            config.VisualSpawnChance = Clamp01(ReadFloatAttribute(root, "visualSpawnChance", config.VisualSpawnChance));
            config.RivalJobChance = Clamp01(ReadFloatAttribute(root, "rivalJobChance", config.RivalJobChance));
            config.SpotOpportunityChance = Clamp01(ReadFloatAttribute(root, "spotOpportunityChance", config.SpotOpportunityChance));
            config.BackhaulSearchRadius = Math.Max(500f, ReadFloatAttribute(root, "backhaulSearchRadius", config.BackhaulSearchRadius));
            config.DistrictSupportWeight = Math.Max(0f, ReadFloatAttribute(root, "districtSupportWeight", config.DistrictSupportWeight));
            config.PriorityScoreBonus = Math.Max(0f, ReadFloatAttribute(root, "priorityScoreBonus", config.PriorityScoreBonus));
            config.PremiumDispatchScoreBonus = Math.Max(0f, ReadFloatAttribute(root, "premiumDispatchScoreBonus", config.PremiumDispatchScoreBonus));
            config.PremiumDispatchCostMultiplier = Math.Max(1f, ReadFloatAttribute(root, "premiumDispatchCostMultiplier", config.PremiumDispatchCostMultiplier));
            config.ExternalImportPremiumMultiplier = Math.Max(1f, ReadFloatAttribute(root, "externalImportPremiumMultiplier", config.ExternalImportPremiumMultiplier));
            config.ExternalExportDiscountMultiplier = Math.Max(0.1f, Math.Min(1f, ReadFloatAttribute(root, "externalExportDiscountMultiplier", config.ExternalExportDiscountMultiplier)));

            var configuredServiceCommodities = ReadAttribute(root, "serviceCommodities")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CommodityCatalog.Normalize)
                .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (configuredServiceCommodities.Length > 0)
            {
                config.ServiceCommodities = configuredServiceCommodities;
            }

            return config;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static int ParseInt(string raw, int fallback)
        {
            int parsed;
            return int.TryParse((raw ?? string.Empty).Trim(), out parsed)
                ? parsed
                : fallback;
        }

        private static string ReadAttribute(XElement element, string name, string fallback = "")
        {
            return element != null && element.Attribute(name) != null
                ? (element.Attribute(name).Value ?? string.Empty).Trim()
                : fallback;
        }

        private static float ReadFloatAttribute(XElement element, string name, float fallback)
        {
            float parsed;
            return float.TryParse(ReadAttribute(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                || float.TryParse(ReadAttribute(element, name), NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static int ReadIntAttribute(XElement element, string name, int fallback)
        {
            int parsed;
            return int.TryParse(ReadAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBoolAttribute(XElement element, string name, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ReadAttribute(element, name), out parsed)
                ? parsed
                : fallback;
        }
    }
}
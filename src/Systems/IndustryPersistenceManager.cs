using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public static class IndustryPersistenceManager
    {
        public static int Load(string filePath, IEnumerable<Industry> industries)
        {
            return LoadWithMetadata(filePath, industries).RestoredCount;
        }

        public static int Load(string filePath, IEnumerable<Industry> industries, TerritoryManager territoryManager)
        {
            return LoadWithMetadata(filePath, industries, territoryManager).RestoredCount;
        }

        public static IndustryPersistenceLoadResult LoadWithMetadata(string filePath, IEnumerable<Industry> industries)
        {
            return LoadWithMetadata(filePath, industries, null);
        }

        public static IndustryPersistenceLoadResult LoadWithMetadata(string filePath, IEnumerable<Industry> industries, TerritoryManager territoryManager)
        {
            var result = new IndustryPersistenceLoadResult
            {
                Metadata = new IndustryPersistenceMetadata(),
            };

            if (string.IsNullOrWhiteSpace(filePath) || industries == null || !File.Exists(filePath))
            {
                return result;
            }

            var ini = IniFile.Load(filePath);
            result.Metadata = ReadMetadata(ini);
            var territorySnapshot = territoryManager != null
                ? ReadTerritorySnapshot(ini)
                : null;

            var restoredCount = 0;

            foreach (var industry in industries)
            {
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                var section = BuildIndustrySectionName(industry.Id);
                if (!ini.HasSection(section))
                {
                    continue;
                }

                var productionRate = ini.GetFloat(section, "ProductionRate", industry.ProductionRate);
                var inputCapacityTons = ini.GetFloat(section, "InputCapacityTons", industry.InputCapacityTons);
                var outputCapacityTons = ini.GetFloat(section, "OutputCapacityTons", industry.OutputCapacityTons);
                var omegaCapacityTons = ini.GetFloat(section, "OmegaCapacityTons", industry.OmegaCapacityTons);
                var omegaStorage = ini.GetFloat(section, "OmegaStorage", industry.OmegaStorage);

                var productionModuleLevel = ParseInt(ini.GetString(section, "ProductionModuleLevel", industry.ProductionModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.ProductionModuleLevel);
                var inputStorageModuleLevel = ParseInt(ini.GetString(section, "InputStorageModuleLevel", industry.InputStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.InputStorageModuleLevel);
                var outputStorageModuleLevel = ParseInt(ini.GetString(section, "OutputStorageModuleLevel", industry.OutputStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.OutputStorageModuleLevel);
                var omegaStorageModuleLevel = ParseInt(ini.GetString(section, "OmegaStorageModuleLevel", industry.OmegaStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.OmegaStorageModuleLevel);
                var isOwned = ini.GetBool(section, "IsOwned", industry.IsOwned);
                var hasContractorPermit = ini.GetBool(section, "HasContractorPermit", industry.HasContractorPermit);

                var bufferStorage = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                var block = ini.GetSection(section);
                foreach (var pair in block)
                {
                    if (pair.Key == null || !pair.Key.StartsWith("Buffer.", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var commodity = pair.Key.Substring("Buffer.".Length).Trim();
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    var value = ParseFloat(pair.Value, 0f);
                    bufferStorage[commodity] = Math.Max(0f, value);
                }

                industry.ApplyPersistentState(
                    bufferStorage,
                    omegaStorage,
                    productionRate,
                    inputCapacityTons,
                    outputCapacityTons,
                    omegaCapacityTons,
                    productionModuleLevel,
                    inputStorageModuleLevel,
                    outputStorageModuleLevel,
                    omegaStorageModuleLevel,
                    isOwned,
                    hasContractorPermit);

                restoredCount += 1;
            }

            if (territoryManager != null && (restoredCount > 0 || (result.Metadata != null && result.Metadata.HasGameplayMetadata) || HasTerritoryData(territorySnapshot)))
            {
                territoryManager.ApplySnapshot(territorySnapshot);
            }

            result.RestoredCount = restoredCount;
            return result;
        }

        public static void Save(string filePath, IEnumerable<Industry> industries)
        {
            Save(filePath, industries, null);
        }

        public static void Save(string filePath, IEnumerable<Industry> industries, IndustryPersistenceMetadata metadata)
        {
            Save(filePath, industries, metadata, null);
        }

        public static void Save(string filePath, IEnumerable<Industry> industries, IndustryPersistenceMetadata metadata, TerritoryPersistenceSnapshot territorySnapshot)
        {
            if (string.IsNullOrWhiteSpace(filePath) || industries == null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("[Meta]");
                writer.WriteLine("Version={0}", metadata != null || territorySnapshot != null ? 6 : 1);
                writer.WriteLine("SavedAtUtc={0}", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                if (metadata != null)
                {
                    writer.WriteLine("Profit={0}", FormatFloat(metadata.Profit));
                    writer.WriteLine("StartingBalance={0}", FormatFloat(metadata.StartingBalance));
                    writer.WriteLine("VehicleFuelDifficultyEnabled={0}", metadata.VehicleFuelDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("CargoDamageDifficultyEnabled={0}", metadata.CargoDamageDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("IndustryPricingDifficultyEnabled={0}", metadata.IndustryPricingDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("LicensingDifficultyEnabled={0}", metadata.LicensingDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("EconomyDifficultyPreset={0}", metadata.EconomyDifficultyPreset);
                    writer.WriteLine("NpcWeeklyWageDifficulty={0}", metadata.NpcWeeklyWageDifficulty);
                    writer.WriteLine("DifficultySettingsLocked={0}", metadata.DifficultySettingsLocked ? "true" : "false");
                }
                writer.WriteLine();

                foreach (var industry in industries.OrderBy(x => x != null ? x.Id : string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                    {
                        continue;
                    }

                    writer.WriteLine("[{0}]", BuildIndustrySectionName(industry.Id));
                    writer.WriteLine("Name={0}", industry.Name ?? string.Empty);
                    writer.WriteLine("ProductionRate={0}", FormatFloat(industry.ProductionRate));
                    writer.WriteLine("InputCapacityTons={0}", FormatFloat(industry.InputCapacityTons));
                    writer.WriteLine("OutputCapacityTons={0}", FormatFloat(industry.OutputCapacityTons));
                    writer.WriteLine("OmegaCapacityTons={0}", FormatFloat(industry.OmegaCapacityTons));
                    writer.WriteLine("OmegaStorage={0}", FormatFloat(industry.OmegaStorage));
                    writer.WriteLine("ProductionModuleLevel={0}", industry.ProductionModuleLevel);
                    writer.WriteLine("InputStorageModuleLevel={0}", industry.InputStorageModuleLevel);
                    writer.WriteLine("OutputStorageModuleLevel={0}", industry.OutputStorageModuleLevel);
                    writer.WriteLine("OmegaStorageModuleLevel={0}", industry.OmegaStorageModuleLevel);
                    writer.WriteLine("IsOwned={0}", industry.IsOwned ? "true" : "false");
                    writer.WriteLine("HasContractorPermit={0}", industry.HasContractorPermit ? "true" : "false");

                    foreach (var stock in industry.BufferStorage.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var commodity = CommodityCatalog.Normalize(stock.Key);
                        if (string.IsNullOrWhiteSpace(commodity))
                        {
                            continue;
                        }

                        writer.WriteLine("Buffer.{0}={1}", commodity, FormatFloat(Math.Max(0f, stock.Value)));
                    }

                    writer.WriteLine();
                }

                if (territorySnapshot != null)
                {
                    WriteTerritorySnapshot(writer, territorySnapshot);
                }
            }
        }

        private static IndustryPersistenceMetadata ReadMetadata(IniFile ini)
        {
            var metadata = new IndustryPersistenceMetadata();
            if (ini == null || !ini.HasSection("Meta"))
            {
                return metadata;
            }

            metadata.HasGameplayMetadata =
                ini.HasKey("Meta", "Profit") ||
                ini.HasKey("Meta", "StartingBalance") ||
                ini.HasKey("Meta", "VehicleFuelDifficultyEnabled") ||
                ini.HasKey("Meta", "CargoDamageDifficultyEnabled") ||
                ini.HasKey("Meta", "IndustryPricingDifficultyEnabled") ||
                ini.HasKey("Meta", "LicensingDifficultyEnabled") ||
                ini.HasKey("Meta", "EconomyDifficultyPreset") ||
                ini.HasKey("Meta", "NpcWeeklyWageDifficulty") ||
                ini.HasKey("Meta", "DifficultySettingsLocked");

            metadata.StartingBalance = ini.GetFloat("Meta", "StartingBalance", 0f);
            metadata.Profit = ini.GetFloat("Meta", "Profit", metadata.StartingBalance);
            metadata.VehicleFuelDifficultyEnabled = ini.GetBool("Meta", "VehicleFuelDifficultyEnabled", false);
            metadata.CargoDamageDifficultyEnabled = ini.GetBool("Meta", "CargoDamageDifficultyEnabled", true);
            metadata.IndustryPricingDifficultyEnabled = ini.GetBool("Meta", "IndustryPricingDifficultyEnabled", false);
            metadata.LicensingDifficultyEnabled = ini.GetBool("Meta", "LicensingDifficultyEnabled", false);
            metadata.EconomyDifficultyPreset = ParseEconomyDifficultyPreset(
                ini.GetString("Meta", "EconomyDifficultyPreset", EconomyDifficultyPreset.Standard.ToString()),
                EconomyDifficultyPreset.Standard);
            metadata.NpcWeeklyWageDifficulty = ParseNpcWeeklyWageDifficulty(
                ini.GetString("Meta", "NpcWeeklyWageDifficulty", NpcWeeklyWageDifficulty.Standard.ToString()),
                NpcWeeklyWageDifficulty.Standard);
            metadata.DifficultySettingsLocked = ini.GetBool("Meta", "DifficultySettingsLocked", false);
            return metadata;
        }

        private static EconomyDifficultyPreset ParseEconomyDifficultyPreset(string raw, EconomyDifficultyPreset fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            EconomyDifficultyPreset parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static NpcWeeklyWageDifficulty ParseNpcWeeklyWageDifficulty(string raw, NpcWeeklyWageDifficulty fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            NpcWeeklyWageDifficulty parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static string BuildIndustrySectionName(string industryId)
        {
            return "Industry:" + (industryId ?? string.Empty).Trim();
        }

        private static void WriteTerritorySnapshot(StreamWriter writer, TerritoryPersistenceSnapshot territorySnapshot)
        {
            if (writer == null || territorySnapshot == null)
            {
                return;
            }

            foreach (var site in territorySnapshot.Sites.OrderBy(x => x.SiteId, StringComparer.OrdinalIgnoreCase))
            {
                if (site == null || string.IsNullOrWhiteSpace(site.SiteId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritorySiteSectionName(site.SiteId));
                writer.WriteLine("ControlLevel={0}", site.ControlLevel);
                writer.WriteLine("CrewAssigned={0}", site.CrewAssigned ? "true" : "false");
                writer.WriteLine("LoadRuns={0}", site.LoadRuns);
                writer.WriteLine("UnloadRuns={0}", site.UnloadRuns);
                writer.WriteLine("TotalDeliveries={0}", site.TotalDeliveries);
                writer.WriteLine("TotalDeliveredTons={0}", FormatFloat(site.TotalDeliveredTons));
                writer.WriteLine("TotalLoadedTons={0}", FormatFloat(site.TotalLoadedTons));
                writer.WriteLine("FranchiseLevel={0}", site.FranchiseLevel);
                writer.WriteLine("LoaderCount={0}", site.LoaderCount);
                writer.WriteLine("MechanicCount={0}", site.MechanicCount);
                writer.WriteLine("GuardCount={0}", site.GuardCount);
                writer.WriteLine("ManagerCount={0}", site.ManagerCount);
                writer.WriteLine("Repossessions={0}", site.Repossessions);
                writer.WriteLine("NpcLoads={0}", site.NpcLoads);
                writer.WriteLine("NpcDeliveries={0}", site.NpcDeliveries);
                writer.WriteLine("LastCommodity={0}", site.LastCommodity ?? string.Empty);
                writer.WriteLine();
            }

            foreach (var corridor in territorySnapshot.Corridors.OrderBy(x => BuildTerritoryCorridorSectionName(x.DistrictA, x.DistrictB), StringComparer.OrdinalIgnoreCase))
            {
                if (corridor == null || string.IsNullOrWhiteSpace(corridor.DistrictA) || string.IsNullOrWhiteSpace(corridor.DistrictB))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritoryCorridorSectionName(corridor.DistrictA, corridor.DistrictB));
                writer.WriteLine("DistrictA={0}", corridor.DistrictA);
                writer.WriteLine("DistrictB={0}", corridor.DistrictB);
                writer.WriteLine("DeliveryCount={0}", corridor.DeliveryCount);
                writer.WriteLine("TotalDeliveredTons={0}", FormatFloat(corridor.TotalDeliveredTons));
                writer.WriteLine("RightLevel={0}", corridor.RightLevel);
                writer.WriteLine();
            }
        }

        private static TerritoryPersistenceSnapshot ReadTerritorySnapshot(IniFile ini)
        {
            var snapshot = new TerritoryPersistenceSnapshot();
            if (ini == null)
            {
                return snapshot;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("TerritorySite:", StringComparison.OrdinalIgnoreCase))
                {
                    var siteId = section.Substring("TerritorySite:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(siteId))
                    {
                        continue;
                    }

                    snapshot.Sites.Add(new TerritorySiteSnapshot
                    {
                        SiteId = siteId,
                        ControlLevel = ParseTerritoryControlLevel(ini.GetString(section, "ControlLevel", TerritoryControlLevel.None.ToString())),
                        CrewAssigned = ini.GetBool(section, "CrewAssigned", false),
                        LoadRuns = ParseInt(ini.GetString(section, "LoadRuns", "0"), 0),
                        UnloadRuns = ParseInt(ini.GetString(section, "UnloadRuns", "0"), 0),
                        TotalDeliveries = ParseInt(ini.GetString(section, "TotalDeliveries", "0"), 0),
                        TotalDeliveredTons = ini.GetFloat(section, "TotalDeliveredTons", 0f),
                        TotalLoadedTons = ini.GetFloat(section, "TotalLoadedTons", 0f),
                        FranchiseLevel = ParseTerritoryFranchiseLevel(ini.GetString(section, "FranchiseLevel", TerritoryFranchiseLevel.None.ToString())),
                        LoaderCount = ParseInt(ini.GetString(section, "LoaderCount", "0"), 0),
                        MechanicCount = ParseInt(ini.GetString(section, "MechanicCount", "0"), 0),
                        GuardCount = ParseInt(ini.GetString(section, "GuardCount", "0"), 0),
                        ManagerCount = ParseInt(ini.GetString(section, "ManagerCount", "0"), 0),
                        Repossessions = ParseInt(ini.GetString(section, "Repossessions", "0"), 0),
                        NpcLoads = ParseInt(ini.GetString(section, "NpcLoads", "0"), 0),
                        NpcDeliveries = ParseInt(ini.GetString(section, "NpcDeliveries", "0"), 0),
                        LastCommodity = ini.GetString(section, "LastCommodity", string.Empty),
                    });

                    continue;
                }

                if (section.StartsWith("TerritoryCorridor:", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.Corridors.Add(new TerritoryCorridorSnapshot
                    {
                        DistrictA = ini.GetString(section, "DistrictA", string.Empty),
                        DistrictB = ini.GetString(section, "DistrictB", string.Empty),
                        DeliveryCount = ParseInt(ini.GetString(section, "DeliveryCount", "0"), 0),
                        TotalDeliveredTons = ini.GetFloat(section, "TotalDeliveredTons", 0f),
                        RightLevel = ParseCorridorRightLevel(ini.GetString(section, "RightLevel", CorridorRightLevel.None.ToString())),
                    });
                }
            }

            return snapshot;
        }

        private static bool HasTerritoryData(TerritoryPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && ((snapshot.Sites != null && snapshot.Sites.Count > 0)
                    || (snapshot.Corridors != null && snapshot.Corridors.Count > 0));
        }

        private static string BuildTerritorySiteSectionName(string siteId)
        {
            return "TerritorySite:" + (siteId ?? string.Empty).Trim();
        }

        private static string BuildTerritoryCorridorSectionName(string districtA, string districtB)
        {
            var left = districtA ?? string.Empty;
            var right = districtB ?? string.Empty;
            if (StringComparer.OrdinalIgnoreCase.Compare(left, right) > 0)
            {
                var swap = left;
                left = right;
                right = swap;
            }

            return "TerritoryCorridor:" + left + "|" + right;
        }

        private static TerritoryControlLevel ParseTerritoryControlLevel(string raw)
        {
            TerritoryControlLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : TerritoryControlLevel.None;
        }

        private static TerritoryFranchiseLevel ParseTerritoryFranchiseLevel(string raw)
        {
            TerritoryFranchiseLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : TerritoryFranchiseLevel.None;
        }

        private static CorridorRightLevel ParseCorridorRightLevel(string raw)
        {
            CorridorRightLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : CorridorRightLevel.None;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            float parsed;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static int ParseInt(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

    public sealed class IndustryPersistenceLoadResult
    {
        public int RestoredCount { get; set; }
        public IndustryPersistenceMetadata Metadata { get; set; }
    }

    public sealed class IndustryPersistenceMetadata
    {
        public bool HasGameplayMetadata { get; set; }
        public float Profit { get; set; }
        public float StartingBalance { get; set; }
        public bool VehicleFuelDifficultyEnabled { get; set; }
        public bool CargoDamageDifficultyEnabled { get; set; }
        public bool IndustryPricingDifficultyEnabled { get; set; }
        public bool LicensingDifficultyEnabled { get; set; }
        public EconomyDifficultyPreset EconomyDifficultyPreset { get; set; } = EconomyDifficultyPreset.Standard;
        public NpcWeeklyWageDifficulty NpcWeeklyWageDifficulty { get; set; } = NpcWeeklyWageDifficulty.Standard;
        public bool DifficultySettingsLocked { get; set; }
    }
}

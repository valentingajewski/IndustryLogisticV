using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public static class IndustryPersistenceManager
    {
        public static int Load(string filePath, IEnumerable<Industry> industries)
        {
            return LoadWithMetadata(filePath, industries).RestoredCount;
        }

        public static IndustryPersistenceLoadResult LoadWithMetadata(string filePath, IEnumerable<Industry> industries)
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
                    isOwned);

                restoredCount += 1;
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
                writer.WriteLine("Version={0}", metadata != null ? 2 : 1);
                writer.WriteLine("SavedAtUtc={0}", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                if (metadata != null)
                {
                    writer.WriteLine("Profit={0}", FormatFloat(metadata.Profit));
                    writer.WriteLine("StartingBalance={0}", FormatFloat(metadata.StartingBalance));
                    writer.WriteLine("VehicleFuelDifficultyEnabled={0}", metadata.VehicleFuelDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("CargoDamageDifficultyEnabled={0}", metadata.CargoDamageDifficultyEnabled ? "true" : "false");
                    writer.WriteLine("IndustryPricingDifficultyEnabled={0}", metadata.IndustryPricingDifficultyEnabled ? "true" : "false");
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
                ini.HasKey("Meta", "DifficultySettingsLocked");

            metadata.StartingBalance = ini.GetFloat("Meta", "StartingBalance", 0f);
            metadata.Profit = ini.GetFloat("Meta", "Profit", metadata.StartingBalance);
            metadata.VehicleFuelDifficultyEnabled = ini.GetBool("Meta", "VehicleFuelDifficultyEnabled", false);
            metadata.CargoDamageDifficultyEnabled = ini.GetBool("Meta", "CargoDamageDifficultyEnabled", true);
            metadata.IndustryPricingDifficultyEnabled = ini.GetBool("Meta", "IndustryPricingDifficultyEnabled", false);
            metadata.DifficultySettingsLocked = ini.GetBool("Meta", "DifficultySettingsLocked", false);
            return metadata;
        }

        private static string BuildIndustrySectionName(string industryId)
        {
            return "Industry:" + (industryId ?? string.Empty).Trim();
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
        public bool DifficultySettingsLocked { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace LSOL.Systems
{
    internal enum DifficultyTemplateSaveResult
    {
        Created = 0,
        Overwritten = 1,
    }

    internal sealed class DifficultySettingsTemplateEntry
    {
        public DifficultySettingsTemplateEntry(string name, DifficultySettingsProfile profile)
        {
            Name = NormalizeName(name);
            Profile = profile != null ? profile.Clone() : DifficultySettingsProfile.CreateDefault();
        }

        public string Name { get; private set; }

        public DifficultySettingsProfile Profile { get; private set; }

        internal static string NormalizeName(string name)
        {
            var normalized = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Template name is required.", nameof(name));
            }

            return normalized;
        }
    }

    internal sealed class DifficultySettingsTemplateStore
    {
        private const string RootElementName = "DifficultyTemplates";
        private const string TemplateElementName = "Template";

        public DifficultySettingsTemplateStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Template path is required.", nameof(filePath));
            }

            FilePath = filePath;
        }

        public string FilePath { get; private set; }

        public List<DifficultySettingsTemplateEntry> LoadTemplates()
        {
            if (!File.Exists(FilePath))
            {
                return new List<DifficultySettingsTemplateEntry>();
            }

            var document = XDocument.Load(FilePath, LoadOptions.None);
            var root = document.Root;
            if (root == null || !string.Equals(root.Name.LocalName, RootElementName, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Difficulty template file is not a valid LSOL difficulty templates document.");
            }

            var templatesByName = new Dictionary<string, DifficultySettingsTemplateEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in root.Elements(TemplateElementName))
            {
                var nameAttribute = element.Attribute("name");
                var name = DifficultySettingsTemplateEntry.NormalizeName(nameAttribute != null ? nameAttribute.Value : string.Empty);
                templatesByName[name] = new DifficultySettingsTemplateEntry(name, ReadProfile(element));
            }

            return templatesByName
                .Values
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public DifficultyTemplateSaveResult SaveTemplate(string name, DifficultySettingsProfile profile)
        {
            var normalizedName = DifficultySettingsTemplateEntry.NormalizeName(name);
            var templates = LoadTemplates();
            var existing = templates.FirstOrDefault(entry => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            var result = existing == null
                ? DifficultyTemplateSaveResult.Created
                : DifficultyTemplateSaveResult.Overwritten;

            if (existing != null)
            {
                templates.Remove(existing);
            }

            templates.Add(new DifficultySettingsTemplateEntry(normalizedName, profile));
            WriteTemplates(templates);
            return result;
        }

        private void WriteTemplates(IEnumerable<DifficultySettingsTemplateEntry> templates)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new XDocument(
                new XElement(
                    RootElementName,
                    templates
                        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(entry => WriteTemplate(entry))));
            document.Save(FilePath);
        }

        private static XElement WriteTemplate(DifficultySettingsTemplateEntry entry)
        {
            var profile = entry != null && entry.Profile != null
                ? entry.Profile
                : DifficultySettingsProfile.CreateDefault();

            return new XElement(
                TemplateElementName,
                new XAttribute("name", entry != null ? entry.Name : string.Empty),
                new XElement(nameof(DifficultySettingsProfile.EconomyDifficultyPreset), profile.EconomyDifficultyPreset.ToString()),
                new XElement(nameof(DifficultySettingsProfile.NpcWeeklyWageDifficulty), profile.NpcWeeklyWageDifficulty.ToString()),
                new XElement(nameof(DifficultySettingsProfile.VehicleFuelDifficultyEnabled), profile.VehicleFuelDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.CargoWeightPowerDifficultyEnabled), profile.CargoWeightPowerDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.CargoDamageDifficultyEnabled), profile.CargoDamageDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.IndustryPricingDifficultyEnabled), profile.IndustryPricingDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.LicensingDifficultyEnabled), profile.LicensingDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.CorridorRestrictionDifficultyEnabled), profile.CorridorRestrictionDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.ReputationDifficultyEnabled), profile.ReputationDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.OfficeGarageLimitDifficultyEnabled), profile.OfficeGarageLimitDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.OfficeNpcLimitDifficultyEnabled), profile.OfficeNpcLimitDifficultyEnabled ? "true" : "false"),
                new XElement(nameof(DifficultySettingsProfile.NpcRouteLimit), profile.NpcRouteLimit.ToString(CultureInfo.InvariantCulture)));
        }

        private static DifficultySettingsProfile ReadProfile(XElement element)
        {
            var defaults = DifficultySettingsProfile.CreateDefault();
            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = ReadEnum(element, nameof(DifficultySettingsProfile.EconomyDifficultyPreset), defaults.EconomyDifficultyPreset),
                NpcWeeklyWageDifficulty = ReadEnum(element, nameof(DifficultySettingsProfile.NpcWeeklyWageDifficulty), defaults.NpcWeeklyWageDifficulty),
                VehicleFuelDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.VehicleFuelDifficultyEnabled), defaults.VehicleFuelDifficultyEnabled),
                CargoWeightPowerDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.CargoWeightPowerDifficultyEnabled), defaults.CargoWeightPowerDifficultyEnabled),
                CargoDamageDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.CargoDamageDifficultyEnabled), defaults.CargoDamageDifficultyEnabled),
                IndustryPricingDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.IndustryPricingDifficultyEnabled), defaults.IndustryPricingDifficultyEnabled),
                LicensingDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.LicensingDifficultyEnabled), defaults.LicensingDifficultyEnabled),
                CorridorRestrictionDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.CorridorRestrictionDifficultyEnabled), defaults.CorridorRestrictionDifficultyEnabled),
                ReputationDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.ReputationDifficultyEnabled), defaults.ReputationDifficultyEnabled),
                OfficeGarageLimitDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.OfficeGarageLimitDifficultyEnabled), defaults.OfficeGarageLimitDifficultyEnabled),
                OfficeNpcLimitDifficultyEnabled = ReadBool(element, nameof(DifficultySettingsProfile.OfficeNpcLimitDifficultyEnabled), defaults.OfficeNpcLimitDifficultyEnabled),
                NpcRouteLimit = ReadInt(element, nameof(DifficultySettingsProfile.NpcRouteLimit), defaults.NpcRouteLimit),
            };
        }

        private static TEnum ReadEnum<TEnum>(XElement element, string name, TEnum fallback)
            where TEnum : struct
        {
            var value = ReadElementValue(element, name);
            TEnum parsed;
            return Enum.TryParse(value, true, out parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBool(XElement element, string name, bool fallback)
        {
            var value = ReadElementValue(element, name);
            bool parsed;
            return bool.TryParse(value, out parsed)
                ? parsed
                : fallback;
        }

        private static int ReadInt(XElement element, string name, int fallback)
        {
            var value = ReadElementValue(element, name);
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static string ReadElementValue(XElement element, string name)
        {
            if (element == null || string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var child = element.Element(name);
            return child != null ? child.Value : string.Empty;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    public sealed class SpecialMissionCatalog
    {
        private const int InGameMinutesPerWeek = 7 * 24 * 60;

        public SpecialMissionCatalog()
        {
            Definitions = new List<SpecialMissionDefinition>();
            ValidationMessages = new List<string>();
        }

        public List<SpecialMissionDefinition> Definitions { get; }

        public List<string> ValidationMessages { get; }

        public static string ResolveMissionDirectory(string configDirectory)
        {
            return string.IsNullOrWhiteSpace(configDirectory)
                ? string.Empty
                : Path.Combine(configDirectory, "missions");
        }

        public static SpecialMissionCatalog Load(string configDirectory, LsolAddonCatalog addonCatalog = null)
        {
            var catalog = new SpecialMissionCatalog();
            var knownMissionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LoadMissionDirectory(ResolveMissionDirectory(configDirectory), "LSOL_Config/missions", catalog, knownMissionIds);

            if (addonCatalog != null)
            {
                catalog.ValidationMessages.AddRange(addonCatalog.ValidationMessages);
                foreach (var package in addonCatalog.GetPackagesForCapability(LsolAddonCatalog.CapabilityMissions))
                {
                    string contentDirectory;
                    if (!package.TryGetContentDirectory(LsolAddonCatalog.CapabilityMissions, out contentDirectory))
                    {
                        continue;
                    }

                    LoadMissionDirectory(contentDirectory, string.Format("LSOL_Addons/{0}", package.DisplayLabel), catalog, knownMissionIds);
                }
            }

            return catalog;
        }

        private static void LoadMissionDirectory(string missionDirectory, string sourceLabel, SpecialMissionCatalog catalog, ISet<string> knownMissionIds)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(missionDirectory) || !Directory.Exists(missionDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(missionDirectory, "*.xml", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < files.Length; i++)
            {
                LoadDefinition(files[i], sourceLabel, catalog, knownMissionIds);
            }
        }

        private static void LoadDefinition(string filePath, string sourceLabel, SpecialMissionCatalog catalog, ISet<string> knownMissionIds)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(filePath, LoadOptions.None);
            }
            catch (Exception ex)
            {
                catalog.ValidationMessages.Add(string.Format("{0}: could not load mission XML: {1}", Path.GetFileName(filePath), ex.Message));
                return;
            }

            if (document.Root == null)
            {
                catalog.ValidationMessages.Add(string.Format("{0}: mission XML is empty.", Path.GetFileName(filePath)));
                return;
            }

            var mission = document.Root;
            var definition = new SpecialMissionDefinition
            {
                Id = NormalizeId(ReadAttribute(mission, "id", Path.GetFileNameWithoutExtension(filePath))),
                Type = ParseMissionType(ReadAttribute(mission, "type")),
                Category = ReadAttribute(mission, "category", "Special Mission"),
                Name = ReadAttribute(mission, "name", Path.GetFileNameWithoutExtension(filePath)),
                Summary = ReadAttribute(mission, "summary"),
                Description = ReadAttribute(mission, "description"),
                Reward = Math.Max(0f, ReadFloatAttribute(mission, "reward", 0f)),
                Repeatable = ReadBoolAttribute(mission, "repeatable", true),
                RepeatCooldownInGameMinutes = ParseRepeatCooldownInGameMinutes(mission),
                RepeatCooldownInGameMonths = Math.Max(0, ReadIntAttribute(mission, "repeatCooldownInGameMonths", 0)),
                AvailabilityDelayInGameMinutes = ParseAvailabilityDelayInGameMinutes(mission),
                AvailabilityDelayInGameMonths = Math.Max(0, ReadIntAttribute(mission, "availabilityDelayInGameMonths", 0)),
            };

            PopulateUnlockRequirement(mission.Element("Unlock"), definition.Unlock);
            PopulateVehicleSpawns(mission.Element("Vehicles"), definition);
            PopulatePropSpawns(mission.Element("Props"), definition);
            PopulateZones(mission.Element("Zones"), definition);
            ValidateDefinition(filePath, definition, catalog.ValidationMessages);

            if (!string.IsNullOrWhiteSpace(definition.Id))
            {
                if (knownMissionIds != null && !knownMissionIds.Add(definition.Id))
                {
                    catalog.ValidationMessages.Add(string.Format("{0}: mission id '{1}' from {2} duplicates an existing mission and was skipped.", Path.GetFileName(filePath), definition.Id, sourceLabel));
                    return;
                }

                catalog.Definitions.Add(definition);
            }
        }

        private static void PopulateUnlockRequirement(XElement element, SpecialMissionUnlockRequirement unlock)
        {
            if (element == null || unlock == null)
            {
                return;
            }

            unlock.MinInfluenceRatio = Clamp01(ReadFloatAttribute(element, "minInfluenceRatio", 0f));

            foreach (var district in SplitCsv(ReadAttribute(element, "districts")))
            {
                if (!unlock.DistrictNames.Contains(district, StringComparer.OrdinalIgnoreCase))
                {
                    unlock.DistrictNames.Add(district);
                }
            }

            foreach (var district in element.Elements("District"))
            {
                var districtName = ReadAttribute(district, "name");
                if (string.IsNullOrWhiteSpace(districtName))
                {
                    continue;
                }

                if (!unlock.DistrictNames.Contains(districtName, StringComparer.OrdinalIgnoreCase))
                {
                    unlock.DistrictNames.Add(districtName.Trim());
                }
            }
        }

        private static void PopulateVehicleSpawns(XElement element, SpecialMissionDefinition definition)
        {
            if (element == null || definition == null)
            {
                return;
            }

            foreach (var vehicle in element.Elements("Vehicle"))
            {
                var roleId = ReadAttribute(vehicle, "role");
                if (string.IsNullOrWhiteSpace(roleId))
                {
                    continue;
                }

                definition.Vehicles[roleId] = new SpecialMissionVehicleSpawn
                {
                    RoleId = roleId,
                    ModelName = ReadAttribute(vehicle, "model"),
                    Position = ReadVector3(vehicle, Vector3.Zero),
                    Heading = ReadFloatAttribute(vehicle, "heading", 0f),
                    Required = ReadBoolAttribute(vehicle, "required", true),
                };
            }
        }

        private static void PopulatePropSpawns(XElement element, SpecialMissionDefinition definition)
        {
            if (element == null || definition == null)
            {
                return;
            }

            foreach (var prop in element.Elements("Prop"))
            {
                var roleId = ReadAttribute(prop, "role");
                if (string.IsNullOrWhiteSpace(roleId))
                {
                    continue;
                }

                definition.Props[roleId] = new SpecialMissionPropSpawn
                {
                    RoleId = roleId,
                    ModelName = ReadAttribute(prop, "model"),
                    ModelHash = ReadIntAttribute(prop, "modelHash", 0),
                    Position = ReadVector3(prop, Vector3.Zero),
                    Heading = ReadFloatAttribute(prop, "heading", 0f),
                    AttachTargetRoleId = ReadAttribute(prop, "attachTargetRole"),
                    AttachOffset = ReadVector3(ReadAttribute(prop, "attachOffset"), Vector3.Zero),
                    AttachRotation = ReadVector3(ReadAttribute(prop, "attachRotation"), Vector3.Zero),
                    Required = ReadBoolAttribute(prop, "required", true),
                };
            }
        }

        private static void PopulateZones(XElement element, SpecialMissionDefinition definition)
        {
            if (element == null || definition == null)
            {
                return;
            }

            foreach (var zone in element.Elements("Zone"))
            {
                var zoneId = ReadAttribute(zone, "id");
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    continue;
                }

                definition.Zones[zoneId] = new SpecialMissionZone
                {
                    ZoneId = zoneId,
                    Position = ReadVector3(zone, Vector3.Zero),
                    Radius = Math.Max(1f, ReadFloatAttribute(zone, "radius", 8f)),
                };
            }
        }

        private static void ValidateDefinition(string filePath, SpecialMissionDefinition definition, ICollection<string> messages)
        {
            if (definition == null || messages == null)
            {
                return;
            }

            var label = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                messages.Add(string.Format("{0}: mission id is missing.", label));
            }

            if (definition.Type == SpecialMissionType.Unknown)
            {
                messages.Add(string.Format("{0}: mission type is missing or unsupported.", label));
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                messages.Add(string.Format("{0}: mission name is missing.", label));
            }

            if (definition.Type == SpecialMissionType.HandlerContainerTransfer)
            {
                ValidateRole(definition, label, messages, "DockHandler");
                ValidateRole(definition, label, messages, "DockTug");
                ValidateRole(definition, label, messages, "Trailer");
                ValidateRole(definition, label, messages, "Truck");
                ValidateProp(definition, label, messages, "Container");
                ValidateZone(definition, label, messages, "LoadingBay");
                ValidateZone(definition, label, messages, "TransferBay");
                ValidateZone(definition, label, messages, "Destination");
            }
            else if (definition.Type == SpecialMissionType.TrailerDelivery)
            {
                ValidateRole(definition, label, messages, "Trailer");
                ValidateZone(definition, label, messages, "Destination");
            }
        }

        private static void ValidateRole(SpecialMissionDefinition definition, string label, ICollection<string> messages, string roleId)
        {
            SpecialMissionVehicleSpawn spawn;
            if (!definition.TryGetVehicle(roleId, out spawn) || spawn.Position == Vector3.Zero || string.IsNullOrWhiteSpace(spawn.ModelName))
            {
                messages.Add(string.Format("{0}: required vehicle role '{1}' is incomplete.", label, roleId));
            }
        }

        private static void ValidateProp(SpecialMissionDefinition definition, string label, ICollection<string> messages, string roleId)
        {
            SpecialMissionPropSpawn spawn;
            if (!definition.TryGetProp(roleId, out spawn) || spawn.Position == Vector3.Zero || (!spawn.HasModelHash && string.IsNullOrWhiteSpace(spawn.ModelName)))
            {
                messages.Add(string.Format("{0}: required prop role '{1}' is incomplete.", label, roleId));
            }
        }

        private static void ValidateZone(SpecialMissionDefinition definition, string label, ICollection<string> messages, string zoneId)
        {
            SpecialMissionZone zone;
            if (!definition.TryGetZone(zoneId, out zone) || zone.Position == Vector3.Zero)
            {
                messages.Add(string.Format("{0}: required zone '{1}' is incomplete.", label, zoneId));
            }
        }

        private static string NormalizeId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : raw.Trim();
        }

        private static int ParseRepeatCooldownInGameMinutes(XElement mission)
        {
            if (mission == null)
            {
                return 0;
            }

            var cooldownWeeks = Math.Max(0f, ReadFloatAttribute(mission, "repeatCooldownInGameWeeks", 0f));
            if (cooldownWeeks > 0.001f)
            {
                return Math.Max(0, (int)Math.Round(cooldownWeeks * InGameMinutesPerWeek));
            }

            return Math.Max(0, ReadIntAttribute(mission, "repeatCooldownInGameMinutes", 0));
        }

        private static int ParseAvailabilityDelayInGameMinutes(XElement mission)
        {
            if (mission == null)
            {
                return 0;
            }

            var availabilityWeeks = Math.Max(0f, ReadFloatAttribute(mission, "availabilityDelayInGameWeeks", 0f));
            if (availabilityWeeks > 0.001f)
            {
                return Math.Max(0, (int)Math.Round(availabilityWeeks * InGameMinutesPerWeek));
            }

            return Math.Max(0, ReadIntAttribute(mission, "availabilityDelayInGameMinutes", 0));
        }

        private static SpecialMissionType ParseMissionType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return SpecialMissionType.Unknown;
            }

            var normalized = raw.Trim();
            if (normalized.Equals("handler_container_transfer", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("HandlerContainerTransfer", StringComparison.OrdinalIgnoreCase))
            {
                return SpecialMissionType.HandlerContainerTransfer;
            }

            if (normalized.Equals("trailer_delivery", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("TrailerDelivery", StringComparison.OrdinalIgnoreCase))
            {
                return SpecialMissionType.TrailerDelivery;
            }

            SpecialMissionType parsed;
            return Enum.TryParse(normalized, true, out parsed)
                ? parsed
                : SpecialMissionType.Unknown;
        }

        private static int ParseInt(string raw, int defaultValue)
        {
            int parsed;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
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

        private static Vector3 ReadVector3(XElement element, Vector3 fallback)
        {
            float x;
            float y;
            float z;
            if (element == null
                || !float.TryParse(ReadAttribute(element, "x"), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                || !float.TryParse(ReadAttribute(element, "y"), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                || !float.TryParse(ReadAttribute(element, "z"), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return fallback;
            }

            return new Vector3(x, y, z);
        }

        private static Vector3 ReadVector3(string raw, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return fallback;
            }

            float x;
            float y;
            float z;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                || !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return fallback;
            }

            return new Vector3(x, y, z);
        }

        private static IEnumerable<string> SplitCsv(string raw)
        {
            return (raw ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
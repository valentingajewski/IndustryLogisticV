using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public static string ResolveMissionDirectory(string configPath)
        {
            var configDirectory = string.IsNullOrWhiteSpace(configPath)
                ? string.Empty
                : Path.GetDirectoryName(configPath) ?? string.Empty;
            return string.IsNullOrWhiteSpace(configDirectory)
                ? string.Empty
                : Path.Combine(configDirectory, "missions");
        }

        public static SpecialMissionCatalog Load(string configPath)
        {
            var catalog = new SpecialMissionCatalog();
            var missionDirectory = ResolveMissionDirectory(configPath);
            if (string.IsNullOrWhiteSpace(missionDirectory) || !Directory.Exists(missionDirectory))
            {
                return catalog;
            }

            var files = Directory.GetFiles(missionDirectory, "*.ini", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < files.Length; i++)
            {
                LoadDefinition(files[i], catalog);
            }

            return catalog;
        }

        private static void LoadDefinition(string filePath, SpecialMissionCatalog catalog)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var ini = IniFile.Load(filePath);
            var definition = new SpecialMissionDefinition
            {
                Id = NormalizeId(ini.GetString("Meta", "Id", Path.GetFileNameWithoutExtension(filePath))),
                Type = ParseMissionType(ini.GetString("Meta", "Type", string.Empty)),
                Category = ini.GetString("Meta", "Category", "Special Mission"),
                Name = ini.GetString("Meta", "Name", Path.GetFileNameWithoutExtension(filePath)),
                Summary = ini.GetString("Meta", "Summary", string.Empty),
                Description = ini.GetString("Meta", "Description", string.Empty),
                Reward = Math.Max(0f, ini.GetFloat("Meta", "Reward", 0f)),
                Repeatable = ini.GetBool("Meta", "Repeatable", true),
                RepeatCooldownInGameMinutes = ParseRepeatCooldownInGameMinutes(ini),
                RepeatCooldownInGameMonths = Math.Max(0, ParseInt(ini.GetString("Meta", "RepeatCooldownInGameMonths", string.Empty), 0)),
            };

            PopulateUnlockRequirement(ini, definition.Unlock);
            PopulateVehicleSpawns(ini, definition);
            PopulatePropSpawns(ini, definition);
            PopulateZones(ini, definition);
            ValidateDefinition(filePath, definition, catalog.ValidationMessages);

            if (!string.IsNullOrWhiteSpace(definition.Id))
            {
                catalog.Definitions.Add(definition);
            }
        }

        private static void PopulateUnlockRequirement(IniFile ini, SpecialMissionUnlockRequirement unlock)
        {
            if (ini == null || unlock == null)
            {
                return;
            }

            unlock.MinInfluenceRatio = Clamp01(ini.GetFloat("Unlock", "MinInfluenceRatio", 0f));
            var districts = ini.GetStringList("Unlock", "Districts");
            for (int i = 0; i < districts.Count; i++)
            {
                var districtName = districts[i];
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

        private static void PopulateVehicleSpawns(IniFile ini, SpecialMissionDefinition definition)
        {
            if (ini == null || definition == null)
            {
                return;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("Vehicle.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var roleId = section.Substring("Vehicle.".Length).Trim();
                if (string.IsNullOrWhiteSpace(roleId))
                {
                    continue;
                }

                definition.Vehicles[roleId] = new SpecialMissionVehicleSpawn
                {
                    RoleId = roleId,
                    ModelName = ini.GetString(section, "Model", string.Empty),
                    Position = ini.GetVector3(section, "Position", Vector3.Zero),
                    Heading = ini.GetFloat(section, "Heading", 0f),
                    Required = ini.GetBool(section, "Required", true),
                };
            }
        }

        private static void PopulatePropSpawns(IniFile ini, SpecialMissionDefinition definition)
        {
            if (ini == null || definition == null)
            {
                return;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("Prop.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var roleId = section.Substring("Prop.".Length).Trim();
                if (string.IsNullOrWhiteSpace(roleId))
                {
                    continue;
                }

                definition.Props[roleId] = new SpecialMissionPropSpawn
                {
                    RoleId = roleId,
                    ModelName = ini.GetString(section, "Model", string.Empty),
                    ModelHash = ParseInt(ini.GetString(section, "ModelHash", string.Empty), 0),
                    Position = ini.GetVector3(section, "Position", Vector3.Zero),
                    Heading = ini.GetFloat(section, "Heading", 0f),
                    AttachTargetRoleId = ini.GetString(section, "AttachTargetRole", string.Empty),
                    AttachOffset = ini.GetVector3(section, "AttachOffset", Vector3.Zero),
                    AttachRotation = ini.GetVector3(section, "AttachRotation", Vector3.Zero),
                    Required = ini.GetBool(section, "Required", true),
                };
            }
        }

        private static void PopulateZones(IniFile ini, SpecialMissionDefinition definition)
        {
            if (ini == null || definition == null)
            {
                return;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("Zone.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var zoneId = section.Substring("Zone.".Length).Trim();
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    continue;
                }

                definition.Zones[zoneId] = new SpecialMissionZone
                {
                    ZoneId = zoneId,
                    Position = ini.GetVector3(section, "Position", Vector3.Zero),
                    Radius = Math.Max(1f, ini.GetFloat(section, "Radius", 8f)),
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

        private static int ParseRepeatCooldownInGameMinutes(IniFile ini)
        {
            if (ini == null)
            {
                return 0;
            }

            var cooldownWeeks = Math.Max(0f, ini.GetFloat("Meta", "RepeatCooldownInGameWeeks", 0f));
            if (cooldownWeeks > 0.001f)
            {
                return Math.Max(0, (int)Math.Round(cooldownWeeks * InGameMinutesPerWeek));
            }

            return Math.Max(0, ParseInt(ini.GetString("Meta", "RepeatCooldownInGameMinutes", string.Empty), 0));
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
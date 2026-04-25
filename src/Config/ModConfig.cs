using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA.Math;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Config
{
    public sealed class ModConfig
    {
        private static readonly string[] ReservedSections =
        {
            "Global",
            "General",
            "Controls",
            "DensityProfiles",
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
        public Dictionary<string, List<string>> ObjectModels { get; private set; }
        public List<string> WorkerModels { get; private set; }

        public static ModConfig Load(string path)
        {
            var ini = IniFile.Load(path);
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
                IndustryConfigs = new Dictionary<string, IndustryConfig>(StringComparer.OrdinalIgnoreCase),
                VehicleDefinitions = new List<VehicleDefinition>(),
                ObjectModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
                WorkerModels = new List<string>(),
            };

            ParseIndustries(ini, config);
            ParseVehicles(ini, config);
            ParseObjects(ini, config);
            ParseWorkers(ini, config);

            return config;
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

                float productionRate = ini.GetFloat(section, "ProductionRate", float.NaN);
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
                    productionRate = 30f;
                }

                config.IndustryConfigs[section] = new IndustryConfig
                {
                    Id = section,
                    Name = name,
                    Position = position,
                    Inputs = inputs,
                    Outputs = outputs,
                    InputCapacityTons = Math.Max(10f, inputCapTons),
                    OutputCapacityTons = Math.Max(10f, outputCapTons),
                    ProductionRate = productionRate,
                    StartingTankRatio = startingTankRatio,
                    Density = density,
                };
            }
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
                var cargoType = ParseCargoType(ini.GetString(section, "VehicleCargoType", "Unknown"));
                var capacityRaw = ini.GetFloat(section, "VehicleCapacity", 10000f);
                var capacityTons = capacityRaw / 1000f;
                var isTrailerSection = section.IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0;

                for (int i = 0; i < rawModels.Count; i++)
                {
                    config.VehicleDefinitions.Add(new VehicleDefinition
                    {
                        SectionName = section,
                        ModelName = rawModels[i],
                        CargoType = cargoType,
                        CapacityTons = Math.Max(0f, capacityTons),
                        IsEnabled = true,
                        IsTrailer = isTrailerSection || cargoType == VehicleCargoType.Trailer,
                    });
                }
            }

            EnsureDefaultSolidTrailer(config);
        }

        private static void EnsureDefaultSolidTrailer(ModConfig config)
        {
            if (config == null)
            {
                return;
            }

            var hasSolidTrailer = config.VehicleDefinitions.Any(x =>
                x != null &&
                x.IsEnabled &&
                x.IsTrailer &&
                x.CargoType == VehicleCargoType.Solid);

            if (hasSolidTrailer)
            {
                return;
            }

            config.VehicleDefinitions.Add(new VehicleDefinition
            {
                SectionName = "SolidTrailers",
                ModelName = "trflat",
                CargoType = VehicleCargoType.Solid,
                CapacityTons = 30f,
                IsEnabled = true,
                IsTrailer = true,
            });
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
                config.ObjectModels["Box"] = new List<string> { "v_serv_abox_02" };
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
            controls.Interact = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "Interact", controls.Interact.ToString()),
                controls.Interact);
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

            controls.DashboardPageUp = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "DashboardPageUp", controls.DashboardPageUp.ToString()),
                controls.DashboardPageUp);
            controls.DashboardPageDown = ControlBindings.ParseOrDefault(
                ini.GetString("Controls", "DashboardPageDown", controls.DashboardPageDown.ToString()),
                controls.DashboardPageDown);

            return controls;
        }

        private static VehicleCargoType ParseCargoType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return VehicleCargoType.Unknown;
            }

            if (raw.Equals("Loose", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Loose;
            }

            if (raw.Equals("Crate", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Crate;
            }

            if (raw.Equals("Fluid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Fluid;
            }

            if (raw.Equals("Trailer", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Trailer;
            }

            if (raw.Equals("Solid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Solid;
            }

            return VehicleCargoType.Unknown;
        }
    }
}

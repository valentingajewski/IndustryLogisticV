using System.Collections.Generic;
using GTA.Math;

namespace LSOL.Config
{
    internal sealed class CoreXmlConfig
    {
        public CoreXmlConfig()
        {
            Controls = new ControlBindings();
            StoreDensityProfile = XmlDensityProfileConfig.CreateDefault();
            GasStationDensityProfile = XmlDensityProfileConfig.CreateDefault();
            WorkerModels = new List<string>
            {
                "s_m_m_dockwork_01",
                "s_m_y_construct_01",
                "s_m_m_trucker_01",
            };
        }

        public float OmegaMultiplier { get; set; } = 2f;

        public float IndustryOmegaCapacityMultiplier { get; set; } = 0.2f;

        public float MarkerRadius { get; set; } = 1.0f;

        public float MarkerHeight { get; set; } = 1.0f;

        public Vector3 MainOfficePosition { get; set; } = new Vector3(-333.33f, -2778.94f, 5.15f);

        public Vector3 VehicleSpawnPosition { get; set; } = new Vector3(-360.04f, -2763.21f, 6f);

        public float VehicleSpawnHeading { get; set; } = 230f;

        public ControlBindings Controls { get; }

        public XmlDensityProfileConfig StoreDensityProfile { get; }

        public XmlDensityProfileConfig GasStationDensityProfile { get; }

        public List<string> WorkerModels { get; }
    }

    internal sealed class XmlDensityProfileConfig
    {
        public float VeryLowCapacity { get; set; } = 5000f;

        public float LowCapacity { get; set; } = 10000f;

        public float MediumCapacity { get; set; } = 60000f;

        public float HighCapacity { get; set; } = 100000f;

        public float VeryLowEmptyingRate { get; set; } = 0.35f;

        public float LowEmptyingRate { get; set; } = 0.8f;

        public float MediumEmptyingRate { get; set; } = 2.25f;

        public float HighEmptyingRate { get; set; } = 4f;

        public float GetCapacity(string density)
        {
            switch (NormalizeDensityKey(density))
            {
                case "VeryLow":
                    return VeryLowCapacity;
                case "Low":
                    return LowCapacity;
                case "High":
                    return HighCapacity;
                default:
                    return MediumCapacity;
            }
        }

        public float GetEmptyingRate(string density)
        {
            switch (NormalizeDensityKey(density))
            {
                case "VeryLow":
                    return VeryLowEmptyingRate;
                case "Low":
                    return LowEmptyingRate;
                case "High":
                    return HighEmptyingRate;
                default:
                    return MediumEmptyingRate;
            }
        }

        public static XmlDensityProfileConfig CreateDefault()
        {
            return new XmlDensityProfileConfig();
        }

        private static string NormalizeDensityKey(string density)
        {
            var normalized = (density ?? string.Empty).Trim().Replace(" ", string.Empty).ToLowerInvariant();
            if (normalized == "verylow")
            {
                return "VeryLow";
            }

            if (normalized == "low")
            {
                return "Low";
            }

            if (normalized == "high")
            {
                return "High";
            }

            return "Medium";
        }
    }
}
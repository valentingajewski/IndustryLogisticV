using System;
using System.Collections.Generic;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class DifficultySettingsSummaryFormatter
    {
        public static string BuildPreviewLine(DifficultySettingsProfile profile, bool locked)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            var parts = new List<string>
            {
                string.Format("Difficulty {0} preset", FormatEconomyDifficultyPreset(profile.EconomyDifficultyPreset)),
                string.Format("Wages {0}", FormatNpcWeeklyWageDifficulty(profile.NpcWeeklyWageDifficulty)),
                string.Format("Routes {0}", Math.Max(0, profile.NpcRouteLimit)),
                string.Format("Modules {0}", Math.Max(1, profile.MaxModuleLimitPerSite)),
                string.Format("Challenges {0}/{1}", profile.CountEnabledBooleanSettings(), DifficultySettingsCatalog.BooleanSettingCount),
            };

            if (locked)
            {
                parts.Add("Locked");
            }

            return string.Join(" | ", parts.ToArray());
        }

        public static string BuildBooleanCount(DifficultySettingsProfile profile)
        {
            if (profile == null)
            {
                return string.Format("0/{0}", DifficultySettingsCatalog.BooleanSettingCount);
            }

            return string.Format("{0}/{1}", profile.CountEnabledBooleanSettings(), DifficultySettingsCatalog.BooleanSettingCount);
        }

        public static List<string> BuildPreviewFlags(DifficultySettingsProfile profile)
        {
            var flags = new List<string>();
            if (profile == null)
            {
                return flags;
            }

            if (profile.VehicleFuelDifficultyEnabled)
            {
                flags.Add("fuel");
            }

            if (profile.CargoWeightPowerDifficultyEnabled)
            {
                flags.Add("haul");
            }

            if (!profile.CargoDamageDifficultyEnabled)
            {
                flags.Add("damage off");
            }

            if (profile.IndustryPricingDifficultyEnabled)
            {
                flags.Add("pricing");
            }

            if (profile.LicensingDifficultyEnabled)
            {
                flags.Add("licensing");
            }

            if (!profile.CorridorRestrictionDifficultyEnabled)
            {
                flags.Add("corridors off");
            }

            if (!profile.ReputationDifficultyEnabled)
            {
                flags.Add("reputation off");
            }

            if (!profile.OfficeGarageLimitDifficultyEnabled)
            {
                flags.Add("garage cap off");
            }

            if (profile.OfficeNpcLimitDifficultyEnabled)
            {
                flags.Add("office NPC cap");
            }

            return flags;
        }

        private static string FormatEconomyDifficultyPreset(EconomyDifficultyPreset preset)
        {
            switch (preset)
            {
                case EconomyDifficultyPreset.Casual:
                    return "Casual";
                case EconomyDifficultyPreset.Hardcore:
                    return "Hardcore";
                case EconomyDifficultyPreset.Impossible:
                    return "Impossible";
                default:
                    return "Standard";
            }
        }

        private static string FormatNpcWeeklyWageDifficulty(NpcWeeklyWageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcWeeklyWageDifficulty.Casual:
                    return "Casual";
                case NpcWeeklyWageDifficulty.Hardcore:
                    return "Hardcore";
                default:
                    return "Standard";
            }
        }
    }
}
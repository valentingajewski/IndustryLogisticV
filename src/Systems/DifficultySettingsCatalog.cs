using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace LSOL.Systems
{
    internal enum DifficultySettingKind
    {
        EconomyDifficultyPreset = 0,
        NpcWeeklyWageDifficulty = 1,
        VehicleFuelDifficultyEnabled = 2,
        CargoWeightPowerDifficultyEnabled = 3,
        CargoDamageDifficultyEnabled = 4,
        IndustryPricingDifficultyEnabled = 5,
        LicensingDifficultyEnabled = 6,
        CorridorRestrictionDifficultyEnabled = 7,
        ReputationDifficultyEnabled = 8,
        OfficeGarageLimitDifficultyEnabled = 9,
        OfficeNpcLimitDifficultyEnabled = 10,
        NpcRouteLimit = 11,
    }

    internal sealed class DifficultySettingDescriptor
    {
        public DifficultySettingDescriptor(DifficultySettingKind kind, bool isBooleanToggle, bool includedInBooleanBulkActions)
        {
            Kind = kind;
            IsBooleanToggle = isBooleanToggle;
            IncludedInBooleanBulkActions = includedInBooleanBulkActions;
        }

        public DifficultySettingKind Kind { get; private set; }

        public bool IsBooleanToggle { get; private set; }

        public bool IncludedInBooleanBulkActions { get; private set; }
    }

    internal static class DifficultySettingsCatalog
    {
        private static readonly ReadOnlyCollection<DifficultySettingDescriptor> AllSettingsInternal = Array.AsReadOnly(new[]
        {
            new DifficultySettingDescriptor(DifficultySettingKind.EconomyDifficultyPreset, false, false),
            new DifficultySettingDescriptor(DifficultySettingKind.NpcWeeklyWageDifficulty, false, false),
            new DifficultySettingDescriptor(DifficultySettingKind.VehicleFuelDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.CargoWeightPowerDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.CargoDamageDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.IndustryPricingDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.LicensingDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.CorridorRestrictionDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.ReputationDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.OfficeGarageLimitDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.OfficeNpcLimitDifficultyEnabled, true, true),
            new DifficultySettingDescriptor(DifficultySettingKind.NpcRouteLimit, false, false),
        });

        private static readonly ReadOnlyCollection<DifficultySettingDescriptor> BooleanBulkSettingsInternal = Array.AsReadOnly(
            AllSettingsInternal
                .Where(descriptor => descriptor.IncludedInBooleanBulkActions)
                .ToArray());

        public static ReadOnlyCollection<DifficultySettingDescriptor> AllSettings
        {
            get { return AllSettingsInternal; }
        }

        public static ReadOnlyCollection<DifficultySettingDescriptor> BooleanBulkSettings
        {
            get { return BooleanBulkSettingsInternal; }
        }

        public static int BooleanSettingCount
        {
            get { return BooleanBulkSettingsInternal.Count; }
        }
    }
}
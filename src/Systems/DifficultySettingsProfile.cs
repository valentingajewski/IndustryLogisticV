using System;

namespace LSOL.Systems
{
    internal sealed class DifficultySettingsProfile : IEquatable<DifficultySettingsProfile>
    {
        public static DifficultySettingsProfile CreateDefault()
        {
            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = EconomyDifficultyPreset.Standard,
                NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard,
                VehicleFuelDifficultyEnabled = false,
                CargoWeightPowerDifficultyEnabled = false,
                CargoDamageDifficultyEnabled = true,
                IndustryPricingDifficultyEnabled = false,
                LicensingDifficultyEnabled = false,
                CorridorRestrictionDifficultyEnabled = true,
                ReputationDifficultyEnabled = true,
                OfficeGarageLimitDifficultyEnabled = true,
                OfficeNpcLimitDifficultyEnabled = false,
                NpcRouteLimit = 5,
                MaxModuleLimitPerSite = 10,
            };
        }

        public static DifficultySettingsProfile FromMetadata(IndustryPersistenceMetadata metadata)
        {
            if (metadata == null)
            {
                return CreateDefault();
            }

            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = metadata.EconomyDifficultyPreset,
                NpcWeeklyWageDifficulty = metadata.NpcWeeklyWageDifficulty,
                VehicleFuelDifficultyEnabled = metadata.VehicleFuelDifficultyEnabled,
                CargoWeightPowerDifficultyEnabled = metadata.CargoWeightPowerDifficultyEnabled,
                CargoDamageDifficultyEnabled = metadata.CargoDamageDifficultyEnabled,
                IndustryPricingDifficultyEnabled = metadata.IndustryPricingDifficultyEnabled,
                LicensingDifficultyEnabled = metadata.LicensingDifficultyEnabled,
                CorridorRestrictionDifficultyEnabled = metadata.CorridorRestrictionDifficultyEnabled,
                ReputationDifficultyEnabled = metadata.ReputationDifficultyEnabled,
                OfficeGarageLimitDifficultyEnabled = metadata.OfficeGarageLimitDifficultyEnabled,
                OfficeNpcLimitDifficultyEnabled = metadata.OfficeNpcLimitDifficultyEnabled,
                NpcRouteLimit = metadata.NpcRouteLimit,
                MaxModuleLimitPerSite = metadata.MaxModuleLimitPerSite,
            };
        }

        public EconomyDifficultyPreset EconomyDifficultyPreset { get; set; }

        public NpcWeeklyWageDifficulty NpcWeeklyWageDifficulty { get; set; }

        public bool VehicleFuelDifficultyEnabled { get; set; }

        public bool CargoWeightPowerDifficultyEnabled { get; set; }

        public bool CargoDamageDifficultyEnabled { get; set; }

        public bool IndustryPricingDifficultyEnabled { get; set; }

        public bool LicensingDifficultyEnabled { get; set; }

        public bool CorridorRestrictionDifficultyEnabled { get; set; }

        public bool ReputationDifficultyEnabled { get; set; }

        public bool OfficeGarageLimitDifficultyEnabled { get; set; }

        public bool OfficeNpcLimitDifficultyEnabled { get; set; }

        public int NpcRouteLimit { get; set; }

        public int MaxModuleLimitPerSite { get; set; }

        public DifficultySettingsProfile Clone()
        {
            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = EconomyDifficultyPreset,
                NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty,
                VehicleFuelDifficultyEnabled = VehicleFuelDifficultyEnabled,
                CargoWeightPowerDifficultyEnabled = CargoWeightPowerDifficultyEnabled,
                CargoDamageDifficultyEnabled = CargoDamageDifficultyEnabled,
                IndustryPricingDifficultyEnabled = IndustryPricingDifficultyEnabled,
                LicensingDifficultyEnabled = LicensingDifficultyEnabled,
                CorridorRestrictionDifficultyEnabled = CorridorRestrictionDifficultyEnabled,
                ReputationDifficultyEnabled = ReputationDifficultyEnabled,
                OfficeGarageLimitDifficultyEnabled = OfficeGarageLimitDifficultyEnabled,
                OfficeNpcLimitDifficultyEnabled = OfficeNpcLimitDifficultyEnabled,
                NpcRouteLimit = NpcRouteLimit,
                MaxModuleLimitPerSite = MaxModuleLimitPerSite,
            };
        }

        public void ApplyToMetadata(IndustryPersistenceMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            metadata.EconomyDifficultyPreset = EconomyDifficultyPreset;
            metadata.NpcWeeklyWageDifficulty = NpcWeeklyWageDifficulty;
            metadata.VehicleFuelDifficultyEnabled = VehicleFuelDifficultyEnabled;
            metadata.CargoWeightPowerDifficultyEnabled = CargoWeightPowerDifficultyEnabled;
            metadata.CargoDamageDifficultyEnabled = CargoDamageDifficultyEnabled;
            metadata.IndustryPricingDifficultyEnabled = IndustryPricingDifficultyEnabled;
            metadata.LicensingDifficultyEnabled = LicensingDifficultyEnabled;
            metadata.CorridorRestrictionDifficultyEnabled = CorridorRestrictionDifficultyEnabled;
            metadata.ReputationDifficultyEnabled = ReputationDifficultyEnabled;
            metadata.OfficeGarageLimitDifficultyEnabled = OfficeGarageLimitDifficultyEnabled;
            metadata.OfficeNpcLimitDifficultyEnabled = OfficeNpcLimitDifficultyEnabled;
            metadata.NpcRouteLimit = NpcRouteLimit;
            metadata.MaxModuleLimitPerSite = MaxModuleLimitPerSite;
        }

        public void ApplyBooleanBulkState(bool enabled)
        {
            VehicleFuelDifficultyEnabled = enabled;
            CargoWeightPowerDifficultyEnabled = enabled;
            CargoDamageDifficultyEnabled = enabled;
            IndustryPricingDifficultyEnabled = enabled;
            LicensingDifficultyEnabled = enabled;
            CorridorRestrictionDifficultyEnabled = enabled;
            ReputationDifficultyEnabled = enabled;
            OfficeGarageLimitDifficultyEnabled = enabled;
            OfficeNpcLimitDifficultyEnabled = enabled;
        }

        public int CountEnabledBooleanSettings()
        {
            var count = 0;
            count += VehicleFuelDifficultyEnabled ? 1 : 0;
            count += CargoWeightPowerDifficultyEnabled ? 1 : 0;
            count += CargoDamageDifficultyEnabled ? 1 : 0;
            count += IndustryPricingDifficultyEnabled ? 1 : 0;
            count += LicensingDifficultyEnabled ? 1 : 0;
            count += CorridorRestrictionDifficultyEnabled ? 1 : 0;
            count += ReputationDifficultyEnabled ? 1 : 0;
            count += OfficeGarageLimitDifficultyEnabled ? 1 : 0;
            count += OfficeNpcLimitDifficultyEnabled ? 1 : 0;
            return count;
        }

        public bool Equals(DifficultySettingsProfile other)
        {
            return other != null
                && EconomyDifficultyPreset == other.EconomyDifficultyPreset
                && NpcWeeklyWageDifficulty == other.NpcWeeklyWageDifficulty
                && VehicleFuelDifficultyEnabled == other.VehicleFuelDifficultyEnabled
                && CargoWeightPowerDifficultyEnabled == other.CargoWeightPowerDifficultyEnabled
                && CargoDamageDifficultyEnabled == other.CargoDamageDifficultyEnabled
                && IndustryPricingDifficultyEnabled == other.IndustryPricingDifficultyEnabled
                && LicensingDifficultyEnabled == other.LicensingDifficultyEnabled
                && CorridorRestrictionDifficultyEnabled == other.CorridorRestrictionDifficultyEnabled
                && ReputationDifficultyEnabled == other.ReputationDifficultyEnabled
                && OfficeGarageLimitDifficultyEnabled == other.OfficeGarageLimitDifficultyEnabled
                && OfficeNpcLimitDifficultyEnabled == other.OfficeNpcLimitDifficultyEnabled
                && NpcRouteLimit == other.NpcRouteLimit
                && MaxModuleLimitPerSite == other.MaxModuleLimitPerSite;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DifficultySettingsProfile);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)EconomyDifficultyPreset;
                hashCode = (hashCode * 397) ^ (int)NpcWeeklyWageDifficulty;
                hashCode = (hashCode * 397) ^ VehicleFuelDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ CargoWeightPowerDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ CargoDamageDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ IndustryPricingDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ LicensingDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ CorridorRestrictionDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ ReputationDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ OfficeGarageLimitDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ OfficeNpcLimitDifficultyEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ NpcRouteLimit;
                hashCode = (hashCode * 397) ^ MaxModuleLimitPerSite;
                return hashCode;
            }
        }
    }
}
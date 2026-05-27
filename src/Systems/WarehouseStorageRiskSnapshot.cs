using System;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum WarehouseLossClass
    {
        None = 0,
        Spoilage = 1,
        Shrinkage = 2,
    }

    public sealed class WarehouseStorageRiskSnapshot
    {
        public Industry Industry { get; set; }

        public float StorageTons { get; set; }

        public float TotalCapacityTons { get; set; }

        public float FillRatio { get; set; }

        public float StorageCondition { get; set; }

        public float ProjectedNextCondition { get; set; }

        public float StabilityScore { get; set; }

        public float StoragePressure { get; set; }

        public float InventoryValue { get; set; }

        public float AdjustedInventoryValue { get; set; }

        public float ProjectedAdjustedInventoryValue { get; set; }

        public float SpoilageSensitiveTons { get; set; }

        public float ShrinkageSensitiveTons { get; set; }

        public float LastDaySpoilageTons { get; set; }

        public float LastDaySpoilageValue { get; set; }

        public float LastDayShrinkageTons { get; set; }

        public float LastDayShrinkageValue { get; set; }

        public float CurrentWeekSpoilageTons { get; set; }

        public float CurrentWeekSpoilageValue { get; set; }

        public float CurrentWeekShrinkageTons { get; set; }

        public float CurrentWeekShrinkageValue { get; set; }

        public float ProjectedSpoilageTons { get; set; }

        public float ProjectedSpoilageValue { get; set; }

        public float ProjectedShrinkageTons { get; set; }

        public float ProjectedShrinkageValue { get; set; }

        public WarehouseLossClass DominantLossClass { get; set; }

        public string DominantCommodity { get; set; }

        public bool HasSensitiveExposure { get; set; }

        public float ConditionValueLoss
        {
            get { return Math.Max(0f, InventoryValue - AdjustedInventoryValue); }
        }

        public float ProjectedConditionValueLoss
        {
            get { return Math.Max(0f, InventoryValue - ProjectedAdjustedInventoryValue); }
        }

        public float LastDayTotalLossTons
        {
            get { return LastDaySpoilageTons + LastDayShrinkageTons; }
        }

        public float LastDayTotalLossValue
        {
            get { return LastDaySpoilageValue + LastDayShrinkageValue; }
        }

        public float CurrentWeekTotalLossTons
        {
            get { return CurrentWeekSpoilageTons + CurrentWeekShrinkageTons; }
        }

        public float CurrentWeekTotalLossValue
        {
            get { return CurrentWeekSpoilageValue + CurrentWeekShrinkageValue; }
        }

        public float ProjectedNextDayLossTons
        {
            get { return ProjectedSpoilageTons + ProjectedShrinkageTons; }
        }

        public float ProjectedNextDayLossValue
        {
            get { return ProjectedSpoilageValue + ProjectedShrinkageValue; }
        }
    }
}
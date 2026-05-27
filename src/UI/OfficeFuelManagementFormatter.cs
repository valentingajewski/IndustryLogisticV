using System;

namespace LSOL.UI
{
    internal sealed class OfficeFuelManagementSnapshot
    {
        public OfficeFuelManagementSnapshot()
        {
            VehicleName = string.Empty;
        }

        public bool HasSelectedOffice { get; set; }

        public bool OfficeIsActive { get; set; }

        public bool HasTankInstalled { get; set; }

        public bool HasActiveDeliveryForOffice { get; set; }

        public float StoredLiters { get; set; }

        public float CapacityLiters { get; set; }

        public float FreeLiters { get; set; }

        public float SpotReplacementRatePerLiter { get; set; }

        public float DeliveredRatePerLiter { get; set; }

        public float EstimatedFillCost { get; set; }

        public bool HasEligibleOfficeVehicle { get; set; }

        public string VehicleName { get; set; }

        public bool VehicleAlreadyFull { get; set; }

        public float VehicleFuelNeededLiters { get; set; }
    }

    internal static class OfficeFuelManagementFormatter
    {
        private const float FullThresholdLiters = 0.05f;
        private const float EmptyThresholdLiters = 0.05f;
        private const float LowFuelRatioThreshold = 0.25f;

        public static string BuildOfficeMenuSummary(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.HasSelectedOffice)
            {
                return "No office selected.";
            }

            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to review diesel storage and refinery refill state.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return "Install a Diesel Tank to store fuel, refuel office trucks, and request refinery delivery.";
            }

            return string.Format(
                "Diesel tank {0} stored | {1} free | Refill {2}",
                FormatStorage(snapshot.StoredLiters, snapshot.CapacityLiters),
                ModFormatting.FormatLiters(Math.Max(0f, snapshot.FreeLiters)),
                BuildRefillState(snapshot));
        }

        public static string BuildTankStatusDetail(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.HasSelectedOffice)
            {
                return "No office selected.";
            }

            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to review diesel storage, refill state, and delivery pricing.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return "Install a Diesel Tank first.";
            }

            return string.Format(
                "{0} stored | {1} free | Refill state: {2}",
                FormatStorage(snapshot.StoredLiters, snapshot.CapacityLiters),
                ModFormatting.FormatLiters(Math.Max(0f, snapshot.FreeLiters)),
                BuildRefillState(snapshot));
        }

        public static string BuildPricingDetail(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.HasSelectedOffice)
            {
                return "No office selected.";
            }

            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to review replacement-rate diesel pricing.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return string.Format(
                    "Replacement-rate spot {0} | Delivered {1} | Install a Diesel Tank to estimate refill cost.",
                    FormatMoneyPerLiter(snapshot.SpotReplacementRatePerLiter),
                    FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter));
            }

            if (snapshot.FreeLiters <= FullThresholdLiters)
            {
                return string.Format(
                    "Replacement-rate spot {0} | Delivered {1} | Tank already full.",
                    FormatMoneyPerLiter(snapshot.SpotReplacementRatePerLiter),
                    FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter));
            }

            return string.Format(
                "Replacement-rate spot {0} | Delivered {1} | Fill remaining about {2}",
                FormatMoneyPerLiter(snapshot.SpotReplacementRatePerLiter),
                FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter),
                ModFormatting.FormatMoney(Math.Max(0f, snapshot.EstimatedFillCost)));
        }

        public static string BuildVehicleRefuelDetail(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to refuel company vehicles from its diesel tank.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return "Install a Diesel Tank first.";
            }

            if (!snapshot.HasEligibleOfficeVehicle)
            {
                return string.Format(
                    "Tank holds {0} | Bring an active office-assigned company vehicle to refuel.",
                    FormatStorage(snapshot.StoredLiters, snapshot.CapacityLiters));
            }

            if (snapshot.VehicleAlreadyFull)
            {
                return string.Format(
                    "Current office rig already full | Tank holds {0}.",
                    FormatStorage(snapshot.StoredLiters, snapshot.CapacityLiters));
            }

            var shortfall = Math.Max(0f, snapshot.VehicleFuelNeededLiters - snapshot.StoredLiters);
            if (shortfall <= FullThresholdLiters)
            {
                return string.Format(
                    "Tank covers the current rig | {0} available after this refill.",
                    ModFormatting.FormatLiters(Math.Max(0f, snapshot.StoredLiters - snapshot.VehicleFuelNeededLiters)));
            }

            return string.Format(
                "Current rig needs {0} | Tank short by {1}.",
                ModFormatting.FormatLiters(Math.Max(0f, snapshot.VehicleFuelNeededLiters)),
                ModFormatting.FormatLiters(shortfall));
        }

        public static string BuildFuelUnloadDetail(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to unload fuel cargo into its diesel tank.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return "Install a Diesel Tank first.";
            }

            if (snapshot.FreeLiters <= FullThresholdLiters)
            {
                return "0.00L free in tank | Tank already full.";
            }

            return string.Format(
                "{0} free in tank | Office unload updates storage immediately.",
                ModFormatting.FormatLiters(Math.Max(0f, snapshot.FreeLiters)));
        }

        public static string BuildFuelDeliveryDetail(OfficeFuelManagementSnapshot snapshot)
        {
            snapshot = snapshot ?? new OfficeFuelManagementSnapshot();
            if (!snapshot.OfficeIsActive)
            {
                return "Activate this office to request refinery diesel delivery.";
            }

            if (!snapshot.HasTankInstalled)
            {
                return "Install a Diesel Tank first.";
            }

            if (snapshot.HasActiveDeliveryForOffice)
            {
                return string.Format(
                    "Refill state: tanker already en route | {0} free in tank | Delivered about {1}.",
                    ModFormatting.FormatLiters(Math.Max(0f, snapshot.FreeLiters)),
                    FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter));
            }

            if (snapshot.FreeLiters <= FullThresholdLiters)
            {
                return string.Format(
                    "Refill state: full | Delivered about {0} | No refill needed.",
                    FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter));
            }

            return string.Format(
                "Up to {0} | Delivered about {1} | Estimated fill cost {2} at current market.",
                ModFormatting.FormatLiters(Math.Max(0f, snapshot.FreeLiters)),
                FormatMoneyPerLiter(snapshot.DeliveredRatePerLiter),
                ModFormatting.FormatMoney(Math.Max(0f, snapshot.EstimatedFillCost)));
        }

        private static string BuildRefillState(OfficeFuelManagementSnapshot snapshot)
        {
            if (snapshot.HasActiveDeliveryForOffice)
            {
                return "tanker en route";
            }

            if (snapshot.FreeLiters <= FullThresholdLiters)
            {
                return "full";
            }

            if (snapshot.StoredLiters <= EmptyThresholdLiters)
            {
                return "empty";
            }

            var ratio = snapshot.CapacityLiters <= FullThresholdLiters
                ? 0f
                : Math.Max(0f, Math.Min(1f, snapshot.StoredLiters / snapshot.CapacityLiters));
            return ratio < LowFuelRatioThreshold ? "low" : "ready";
        }

        private static string FormatStorage(float storedLiters, float capacityLiters)
        {
            return string.Format(
                "{0}/{1}L",
                ModFormatting.FormatNumber(Math.Max(0f, storedLiters)),
                ModFormatting.FormatNumber(Math.Max(0f, capacityLiters)));
        }

        private static string FormatMoneyPerLiter(float amount)
        {
            return string.Concat(ModFormatting.FormatMoney(Math.Max(0f, amount)), "/L");
        }
    }
}
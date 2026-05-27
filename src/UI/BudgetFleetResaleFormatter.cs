using System;
using LSOL;

namespace LSOL.UI
{
    internal static class BudgetFleetResaleFormatter
    {
        public static string BuildRootTileDetail(TabletFleetResaleSummary summary)
        {
            summary = summary ?? new TabletFleetResaleSummary();
            return summary.OwnedVehicleCount <= 0
                ? "Fleet resale n/a"
                : string.Format("Fleet resale {0} | Dep {1}", ModFormatting.FormatMoney(summary.EstimatedResaleValue), ModFormatting.FormatMoney(summary.TotalDepreciationLoss));
        }

        public static string BuildOverviewRecoveryDetail(TabletFleetResaleSummary summary)
        {
            summary = summary ?? new TabletFleetResaleSummary();
            if (summary.OwnedVehicleCount <= 0)
            {
                return "No owned commercial fleet is currently tracked.";
            }

            return string.Format(
                "Owned {0} | Purchase basis {1} | Resale {2} | Recovery {3:0.0}%",
                summary.OwnedVehicleCount,
                ModFormatting.FormatMoney(summary.PurchaseBasis),
                ModFormatting.FormatMoney(summary.EstimatedResaleValue),
                summary.RecoveryPercentOfPurchase);
        }

        public static string BuildOverviewDepreciationDetail(TabletFleetResaleSummary summary)
        {
            summary = summary ?? new TabletFleetResaleSummary();
            if (summary.OwnedVehicleCount <= 0)
            {
                return "No fleet depreciation signal available without owned vehicles.";
            }

            if (string.IsNullOrWhiteSpace(summary.WeakestVehicleName))
            {
                return string.Format("Depreciation vs purchase basis {0}", ModFormatting.FormatMoney(summary.TotalDepreciationLoss));
            }

            return string.Format(
                "Depreciation {0} | Weakest recovery {1}: {2:0.0}%",
                ModFormatting.FormatMoney(summary.TotalDepreciationLoss),
                summary.WeakestVehicleName,
                summary.WeakestVehicleRecoveryPercent);
        }
    }
}

using System;
using System.Collections.Generic;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class TabletPropertyPortfolioSummary
    {
        public TabletPropertyPortfolioSummary()
        {
            Offices = Array.Empty<TabletPropertyOfficeEntry>();
            Apartments = Array.Empty<TabletPropertyApartmentEntry>();
            Motels = Array.Empty<TabletPropertyMotelEntry>();
        }

        public IReadOnlyList<TabletPropertyOfficeEntry> Offices { get; set; }

        public IReadOnlyList<TabletPropertyApartmentEntry> Apartments { get; set; }

        public IReadOnlyList<TabletPropertyMotelEntry> Motels { get; set; }

        public int OwnedOfficeCount { get; set; }

        public int RentedOfficeCount { get; set; }

        public int OwnedApartmentCount { get; set; }

        public int RentedApartmentCount { get; set; }

        public float TotalArrears { get; set; }

        public float UpcomingWeeklyRent { get; set; }
    }

    internal sealed class TabletPropertyOfficeEntry
    {
        public string OfficeId { get; set; }

        public string DisplayName { get; set; }

        public string DistrictName { get; set; }

        public string StatusLabel { get; set; }

        public bool IsOwned { get; set; }

        public bool IsRented { get; set; }

        public bool IsAccessSuspended { get; set; }

        public bool IsActive { get; set; }

        public bool HasArrears { get; set; }

        public float ArrearsAmount { get; set; }

        public float WeeklyRent { get; set; }

        public float PurchasePrice { get; set; }

        public int DueInMinutes { get; set; }

        public string BillDetail { get; set; }

        public int ActiveGarageVehicleCount { get; set; }

        public int ReserveVehicleCount { get; set; }

        public string AssignmentSummary { get; set; }
    }

    internal sealed class TabletPropertyApartmentEntry
    {
        public string InteriorId { get; set; }

        public string DisplayName { get; set; }

        public string InteriorType { get; set; }

        public string InteriorIgName { get; set; }

        public string StatusLabel { get; set; }

        public bool IsOwned { get; set; }

        public bool IsRented { get; set; }

        public bool IsAccessSuspended { get; set; }

        public bool IsActive { get; set; }

        public bool HasArrears { get; set; }

        public float ArrearsAmount { get; set; }

        public float WeeklyRent { get; set; }

        public float PurchasePrice { get; set; }

        public int DueInMinutes { get; set; }

        public string BillDetail { get; set; }
    }

    internal sealed class TabletPropertyMotelEntry
    {
        public string MotelId { get; set; }

        public string DisplayName { get; set; }

        public string MotelType { get; set; }

        public string MotelIgName { get; set; }

        public float NightlyRestPrice { get; set; }
    }

    internal sealed class TabletPropertyBillEntry
    {
        public string PropertyId { get; set; }

        public CompanyFinanceCategory Category { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public float Amount { get; set; }

        public int DueInMinutes { get; set; }
    }
}
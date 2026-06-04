using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;

namespace LSOL.UI
{
    internal sealed class PropertyPortfolioTabletActions
    {
        public Action<string> RentOffice { get; set; }

        public Action<string> TransferOfficeRental { get; set; }

        public Action<string> PurchaseOffice { get; set; }

        public Action<string> ActivateOffice { get; set; }

        public Action<string> RelinquishOfficeRental { get; set; }

        public Action<string> PayOfficeArrears { get; set; }

        public Action<string> PurchaseApartment { get; set; }

        public Action<string> RentApartment { get; set; }

        public Action<string> ActivateApartment { get; set; }

        public Action<string> CancelApartmentRental { get; set; }

        public Action<string> SellApartment { get; set; }

        public Action<string> PayApartmentArrears { get; set; }

        public Action<string> RestAtMotel { get; set; }
    }

    internal sealed class PropertyPortfolioTabletApp : ITabletApp
    {
        private static readonly Color WarningIdle = Color.FromArgb(182, 82, 48, 40);
        private static readonly Color WarningActive = Color.FromArgb(224, 162, 102, 84);
        private static readonly Color OwnedIdle = Color.FromArgb(180, 40, 58, 46);
        private static readonly Color OwnedActive = Color.FromArgb(224, 102, 142, 118);
        private static readonly Color RentalIdle = Color.FromArgb(180, 40, 50, 62);
        private static readonly Color RentalActive = Color.FromArgb(224, 94, 126, 148);
        private static readonly Color AvailableIdle = Color.FromArgb(176, 38, 42, 46);
        private static readonly Color AvailableActive = Color.FromArgb(218, 90, 102, 112);

        private readonly PropertyPortfolioTabletActions _actions;

        public PropertyPortfolioTabletApp(PropertyPortfolioTabletActions actions)
        {
            _actions = actions ?? new PropertyPortfolioTabletActions();
        }

        public string AppId
        {
            get { return TabletAppIds.PropertyPortfolio; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            TabletShellPage page;
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "offices":
                    page = BuildOfficesPage(context);
                    break;
                case "office-detail":
                    page = BuildOfficeDetailPage(context, route != null ? route.Payload as string : null);
                    break;
                case "apartments":
                    page = BuildApartmentsPage(context);
                    break;
                case "apartment-detail":
                    page = BuildApartmentDetailPage(context, route != null ? route.Payload as string : null);
                    break;
                case "motels":
                    page = BuildMotelsPage(context);
                    break;
                case "motel-detail":
                    page = BuildMotelDetailPage(context, route != null ? route.Payload as string : null);
                    break;
                default:
                    page = BuildRootPage(context);
                    break;
            }

            ApplyPageStyle(page);
            return page;
        }

        private static void ApplyPageStyle(TabletShellPage page)
        {
            if (page == null)
            {
                return;
            }

            page.CaptionScale = 0.44f;
            page.DetailScale = 0.275f;
        }

        private TabletShellPage BuildRootPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var items = new List<MenuItem>();
            var arrearsCount = summary.Offices.Count(entry => entry.HasArrears) + summary.Apartments.Count(entry => entry.HasArrears);
            var controlledOffices = summary.Offices.Count(entry => entry.IsOwned || entry.IsRented);
            var controlledApartments = summary.Apartments.Count(entry => entry.IsOwned || entry.IsRented);
            var activeOffice = summary.Offices.FirstOrDefault(entry => entry.IsActive);
            var activeApartment = summary.Apartments.FirstOrDefault(entry => entry.IsActive);
            var cheapestMotel = summary.Motels.OrderBy(entry => entry.NightlyRestPrice).FirstOrDefault();

            if (summary.TotalArrears > 0.01f)
            {
                items.Add(TabletUiHelpers.CreateActionItem(
                    "Due Now",
                    string.Format(
                        "{0} blocked propert{1}\nArrears {2}",
                        arrearsCount,
                        arrearsCount == 1 ? "y" : "ies",
                        ModFormatting.FormatMoney(summary.TotalArrears)),
                    () => context.Push(
                        TabletAppIds.PropertyPortfolio,
                        summary.Offices.Any(entry => entry.HasArrears) ? "offices" : "apartments"),
                    WarningIdle,
                    WarningActive,
                    null,
                    "DUE"));
            }

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyOffices),
                string.Format(
                    "{0} controlled / {1} total\n{2}",
                    controlledOffices,
                    summary.Offices.Count,
                    BuildOfficeRootDetail(summary, activeOffice)),
                () => context.Push(TabletAppIds.PropertyPortfolio, "offices"),
                OwnedIdle,
                OwnedActive,
                null,
                "OFF"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyApartments),
                string.Format(
                    "{0} controlled / {1} total\n{2}",
                    controlledApartments,
                    summary.Apartments.Count,
                    BuildApartmentRootDetail(summary, activeApartment)),
                () => context.Push(TabletAppIds.PropertyPortfolio, "apartments"),
                RentalIdle,
                RentalActive,
                null,
                "APT"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyMotels),
                cheapestMotel != null
                    ? string.Format(
                        "{0} nightly locations\nCheapest {1} at {2}",
                        summary.Motels.Count,
                        ModFormatting.FormatMoney(cheapestMotel.NightlyRestPrice),
                        cheapestMotel.DisplayName)
                    : "No motel rest locations are configured.\nMotels remain rest-only.",
                () => context.Push(TabletAppIds.PropertyPortfolio, "motels"),
                Color.FromArgb(180, 54, 44, 58),
                Color.FromArgb(224, 130, 108, 144),
                null,
                "MTL"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyActiveOffice),
                activeOffice != null
                    ? string.Format("{0}\n{1}", activeOffice.DisplayName, activeOffice.AssignmentSummary)
                    : "No office is active.\nActivate an owned or rented office to anchor company logistics.",
                () => context.Push(
                    TabletAppIds.PropertyPortfolio,
                    activeOffice != null ? "office-detail" : "offices",
                    activeOffice != null ? activeOffice.OfficeId : null),
                Color.FromArgb(180, 46, 56, 50),
                Color.FromArgb(224, 110, 132, 114),
                null,
                "HQ"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyActiveApartment),
                activeApartment != null
                    ? string.Format("{0}\n{1}", activeApartment.DisplayName, activeApartment.IsOwned ? "Owned residence ready." : activeApartment.BillDetail)
                    : "No residence is active.\nActivate an apartment to anchor personal storage and sleep.",
                () => context.Push(
                    TabletAppIds.PropertyPortfolio,
                    activeApartment != null ? "apartment-detail" : "apartments",
                    activeApartment != null ? activeApartment.InteriorId : null),
                Color.FromArgb(180, 44, 50, 62),
                Color.FromArgb(224, 102, 120, 148),
                null,
                "HOME"));
            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.Get(ModTextKey.TabletPropertyWeeklyRentOutlook),
                string.Format(
                    "Upcoming rent {0}\nOffice dues and rental residences only",
                    ModFormatting.FormatMoney(summary.UpcomingWeeklyRent)),
                () => context.Push(TabletAppIds.PropertyPortfolio, summary.Offices.Count > 0 ? "offices" : "apartments"),
                Color.FromArgb(180, 44, 54, 46),
                Color.FromArgb(224, 106, 134, 112),
                null,
                "BIL"));
            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Property Portfolio",
                Subtitle = "Offices, residences, motel rest stops, rent, arrears, and active assignments",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                FooterText = "Arrow Keys Navigate | Enter Open or Manage | Backspace/Esc Back",
                WidthScale = 0.95f,
                Layout = SimpleMenuTabletLayout.Dashboard,
                DashboardSidebarCount = 0,
                DashboardTileColumns = 3,
                Items = items,
            };
        }

        private TabletShellPage BuildOfficesPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Office Portfolio",
                    string.Format(
                        "{0} owned | {1} rented | {2} arrears due now",
                        summary.OwnedOfficeCount,
                        summary.RentedOfficeCount,
                        ModFormatting.FormatMoney(summary.Offices.Sum(entry => entry.ArrearsAmount))))
            };

            if (summary.Offices.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No offices configured", "Office definitions are not available in the current configuration."));
            }
            else
            {
                items.AddRange(summary.Offices.Select(office => BuildOfficeListItem(context, office)));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Property Portfolio.", () => context.GoBack(), "BACK"));
            return new TabletShellPage
            {
                Title = "Offices",
                Subtitle = "Review access, arrears, and the active company garage anchor",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildOfficeDetailPage(TabletShellContext context, string officeId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var office = summary.Offices.FirstOrDefault(entry => string.Equals(entry.OfficeId, officeId, StringComparison.OrdinalIgnoreCase));
            if (office == null)
            {
                return BuildUnavailablePage(snapshot, "Office portfolio entry unavailable.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                BuildStatusItem(office.DisplayName, office.StatusLabel, office.IsActive, office.HasArrears, office.BillDetail),
                TabletUiHelpers.CreateInfoItem(
                    "Pricing",
                    string.Format(
                        "Rent {0} | Buy {1}",
                        ModFormatting.FormatMoney(office.WeeklyRent),
                        ModFormatting.FormatMoney(office.PurchasePrice))),
                TabletUiHelpers.CreateInfoItem(
                    "Assignment",
                    office.AssignmentSummary),
                TabletUiHelpers.CreateInfoItem(
                    "Marker Operations",
                    "Garage, office objects, fuel management, and repairs remain on the office marker menu. Use a placed Construction Site Cabin for Hire NPC.")
            };

            var hasTransferRental = summary.Offices.Any(entry => !string.Equals(entry.OfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase) && entry.IsRented && !entry.IsOwned);
            if (!office.IsOwned && !office.IsRented)
            {
                items.Add(BuildActionItem(
                    hasTransferRental ? "Transfer Office Rental" : "Rent Office",
                    hasTransferRental
                        ? string.Format("Move your current rental to this office for {0}.", ModFormatting.FormatMoney(office.WeeklyRent))
                        : string.Format("Rent access for {0} per week.", ModFormatting.FormatMoney(office.WeeklyRent)),
                    hasTransferRental ? _actions.TransferOfficeRental : _actions.RentOffice,
                    office.OfficeId,
                    context,
                    office.HasArrears));
            }

            if (!office.IsOwned && !office.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Purchase Office",
                    string.Format("Purchase permanent access for {0}.", ModFormatting.FormatMoney(office.PurchasePrice)),
                    _actions.PurchaseOffice,
                    office.OfficeId,
                    context,
                    false));
            }

            if (office.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Pay Office Arrears",
                    string.Format("Settle {0} to restore office access.", ModFormatting.FormatMoney(office.ArrearsAmount)),
                    _actions.PayOfficeArrears,
                    office.OfficeId,
                    context,
                    true));
            }

            if (office.IsRented && !office.IsOwned)
            {
                items.Add(BuildActionItem(
                    "Relinquish Rental",
                    office.IsActive
                        ? "Close this rental and move company operations to another office you already control."
                        : "Close this inactive rental to stop future weekly charges.",
                    _actions.RelinquishOfficeRental,
                    office.OfficeId,
                    context,
                    false));
            }

            if ((office.IsOwned || office.IsRented) && !office.IsActive && !office.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Activate Office",
                    "Make this office the active commercial garage and assignment anchor.",
                    _actions.ActivateOffice,
                    office.OfficeId,
                    context,
                    false));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Offices.", () => context.GoBack(), "BACK"));
            return new TabletShellPage
            {
                Title = "Office Detail",
                Subtitle = string.IsNullOrWhiteSpace(office.DistrictName)
                    ? office.DisplayName
                    : string.Format("{0} | {1}", office.DisplayName, office.DistrictName),
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 7,
                Items = items,
            };
        }

        private TabletShellPage BuildApartmentsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Residence Portfolio",
                    string.Format(
                        "{0} owned | {1} rented | {2} arrears due now",
                        summary.OwnedApartmentCount,
                        summary.RentedApartmentCount,
                        ModFormatting.FormatMoney(summary.Apartments.Sum(entry => entry.ArrearsAmount))))
            };

            if (summary.Apartments.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No apartments configured", "Apartment definitions are not available in the current configuration."));
            }
            else
            {
                items.AddRange(summary.Apartments.Select(apartment => BuildApartmentListItem(context, apartment)));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Property Portfolio.", () => context.GoBack(), "BACK"));
            return new TabletShellPage
            {
                Title = "Apartments",
                Subtitle = "Review residence access, active home selection, and rent arrears",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.94f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildApartmentDetailPage(TabletShellContext context, string interiorId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var apartment = summary.Apartments.FirstOrDefault(entry => string.Equals(entry.InteriorId, interiorId, StringComparison.OrdinalIgnoreCase));
            if (apartment == null)
            {
                return BuildUnavailablePage(snapshot, "Apartment portfolio entry unavailable.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                BuildStatusItem(apartment.DisplayName, apartment.StatusLabel, apartment.IsActive, apartment.HasArrears, apartment.BillDetail),
                TabletUiHelpers.CreateInfoItem(
                    "Pricing",
                    string.Format(
                        "Rent {0} | Buy {1}",
                        ModFormatting.FormatMoney(apartment.WeeklyRent),
                        ModFormatting.FormatMoney(apartment.PurchasePrice))),
                TabletUiHelpers.CreateInfoItem(
                    "Residence Type",
                    string.IsNullOrWhiteSpace(apartment.InteriorIgName)
                        ? apartment.InteriorType
                        : string.Format("{0} | {1}", apartment.InteriorType, apartment.InteriorIgName)),
                TabletUiHelpers.CreateInfoItem(
                    "Marker Operations",
                    "Enter, exit, garage access, and sleeping remain on the apartment marker menu.")
            };

            if (!apartment.IsOwned && !apartment.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Purchase Apartment",
                    string.Format("Purchase permanent access for {0}.", ModFormatting.FormatMoney(apartment.PurchasePrice)),
                    _actions.PurchaseApartment,
                    apartment.InteriorId,
                    context,
                    false));
            }

            if (!apartment.IsOwned && !apartment.IsRented)
            {
                items.Add(BuildActionItem(
                    "Rent Apartment",
                    string.Format("Rent access for {0} per week.", ModFormatting.FormatMoney(apartment.WeeklyRent)),
                    _actions.RentApartment,
                    apartment.InteriorId,
                    context,
                    false));
            }

            if (apartment.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Pay Apartment Arrears",
                    string.Format("Settle {0} to restore residence access.", ModFormatting.FormatMoney(apartment.ArrearsAmount)),
                    _actions.PayApartmentArrears,
                    apartment.InteriorId,
                    context,
                    true));
            }

            if (apartment.IsRented && !apartment.IsOwned)
            {
                items.Add(BuildActionItem(
                    "Cancel Rental",
                    apartment.IsActive
                        ? "Cancel this rental and move your active residence to another apartment you already control."
                        : "Cancel this inactive rental to stop future weekly charges.",
                    _actions.CancelApartmentRental,
                    apartment.InteriorId,
                    context,
                    false));
            }

            if (apartment.IsOwned)
            {
                items.Add(BuildActionItem(
                    "Sell Apartment",
                    "Sell this owned residence and fall back to another apartment if needed.",
                    _actions.SellApartment,
                    apartment.InteriorId,
                    context,
                    false));
            }

            if ((apartment.IsOwned || apartment.IsRented) && !apartment.IsActive && !apartment.HasArrears)
            {
                items.Add(BuildActionItem(
                    "Activate Apartment",
                    "Make this apartment the active residence for storage and spawn fallback.",
                    _actions.ActivateApartment,
                    apartment.InteriorId,
                    context,
                    false));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Apartments.", () => context.GoBack(), "BACK"));
            return new TabletShellPage
            {
                Title = "Apartment Detail",
                Subtitle = apartment.DisplayName,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 7,
                Items = items,
            };
        }

        private TabletShellPage BuildMotelsPage(TabletShellContext context)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    "Motel Stops",
                    "Motels stay as nightly rest locations only. They do not become owned, rented, activated, or billed properties.")
            };

            if (summary.Motels.Count == 0)
            {
                items.Add(TabletUiHelpers.CreateInfoItem("No motels configured", "No quick-rest motel locations are available in the current configuration."));
            }
            else
            {
                items.AddRange(summary.Motels.Select(motel => BuildMotelListItem(context, motel)));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to Property Portfolio.", () => context.GoBack(), "BACK"));
            return new TabletShellPage
            {
                Title = "Motels",
                Subtitle = "Nightly rest-only locations surfaced inside the property portfolio",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.92f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private TabletShellPage BuildMotelDetailPage(TabletShellContext context, string motelId)
        {
            var snapshot = context.Snapshot ?? new TabletStateSnapshot();
            var summary = context.StateStore.GetPropertyPortfolioSummary() ?? new TabletPropertyPortfolioSummary();
            var motel = summary.Motels.FirstOrDefault(entry => string.Equals(entry.MotelId, motelId, StringComparison.OrdinalIgnoreCase));
            if (motel == null)
            {
                return BuildUnavailablePage(snapshot, "Motel entry unavailable.", () => context.GoBack());
            }

            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateBannerItem(
                    motel.DisplayName,
                    string.IsNullOrWhiteSpace(motel.MotelIgName)
                        ? string.Format("{0} | Rest-only stop", motel.MotelType)
                        : string.Format("{0} | {1} | Rest-only stop", motel.MotelType, motel.MotelIgName)),
                TabletUiHelpers.CreateInfoItem(
                    "Nightly Price",
                    string.Format("Buy a room for the night for {0}.", ModFormatting.FormatMoney(motel.NightlyRestPrice))),
                TabletUiHelpers.CreateInfoItem(
                    "Motel Rules",
                    "No ownership, no weekly rent, no arrears, and no activation state. This is only a quick-rest shortcut."),
                BuildActionItem(
                    "Quick Rest",
                    string.Format("Pay {0} and use the existing motel rest flow.", ModFormatting.FormatMoney(motel.NightlyRestPrice)),
                    _actions.RestAtMotel,
                    motel.MotelId,
                    context,
                    false),
                TabletUiHelpers.CreateNavigationItem("Back", "Return to Motels.", () => context.GoBack(), "BACK"),
            };

            return new TabletShellPage
            {
                Title = "Motel Detail",
                Subtitle = motel.DisplayName,
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.90f,
                MaxVisibleItems = 6,
                Items = items,
            };
        }

        private MenuItem BuildOfficeListItem(TabletShellContext context, TabletPropertyOfficeEntry office)
        {
            return TabletUiHelpers.CreateActionItem(
                BuildOfficeCaption(office),
                string.Format(
                    "{0} | {1} | {2}",
                    string.IsNullOrWhiteSpace(office.DistrictName) ? "No district" : office.DistrictName,
                    office.BillDetail,
                    office.AssignmentSummary),
                () => context.Push(TabletAppIds.PropertyPortfolio, "office-detail", office.OfficeId),
                GetItemIdleColor(office.HasArrears, office.IsOwned, office.IsRented),
                GetItemActiveColor(office.HasArrears, office.IsOwned, office.IsRented),
                null,
                office.IsActive ? "ACT" : office.HasArrears ? "DUE" : office.IsOwned ? "OWN" : office.IsRented ? "RNT" : "AVL");
        }

        private MenuItem BuildApartmentListItem(TabletShellContext context, TabletPropertyApartmentEntry apartment)
        {
            var typeDetail = string.IsNullOrWhiteSpace(apartment.InteriorIgName)
                ? apartment.InteriorType
                : string.Format("{0} | {1}", apartment.InteriorType, apartment.InteriorIgName);
            return TabletUiHelpers.CreateActionItem(
                BuildApartmentCaption(apartment),
                string.Format("{0} | {1}", typeDetail, apartment.BillDetail),
                () => context.Push(TabletAppIds.PropertyPortfolio, "apartment-detail", apartment.InteriorId),
                GetItemIdleColor(apartment.HasArrears, apartment.IsOwned, apartment.IsRented),
                GetItemActiveColor(apartment.HasArrears, apartment.IsOwned, apartment.IsRented),
                null,
                apartment.IsActive ? "ACT" : apartment.HasArrears ? "DUE" : apartment.IsOwned ? "OWN" : apartment.IsRented ? "RNT" : "AVL");
        }

        private MenuItem BuildMotelListItem(TabletShellContext context, TabletPropertyMotelEntry motel)
        {
            var typeLabel = string.IsNullOrWhiteSpace(motel.MotelType) ? "Motel" : motel.MotelType;
            var igLabel = string.IsNullOrWhiteSpace(motel.MotelIgName) ? string.Empty : string.Format(" | {0}", motel.MotelIgName);
            return TabletUiHelpers.CreateActionItem(
                motel.DisplayName,
                string.Format("{0}{1} | Nightly rest {2}", typeLabel, igLabel, ModFormatting.FormatMoney(motel.NightlyRestPrice)),
                () => context.Push(TabletAppIds.PropertyPortfolio, "motel-detail", motel.MotelId),
                Color.FromArgb(176, 52, 44, 58),
                Color.FromArgb(220, 126, 104, 142),
                null,
                "REST");
        }

        private MenuItem BuildStatusItem(string displayName, string statusLabel, bool isActive, bool hasArrears, string detail)
        {
            var caption = string.Format(
                "{0} | {1}{2}",
                displayName,
                statusLabel,
                isActive ? " | Active" : string.Empty);
            return hasArrears
                ? TabletUiHelpers.CreateActionItem(caption, detail, null, WarningIdle, WarningActive, null, "DUE")
                : TabletUiHelpers.CreateBannerItem(caption, detail);
        }

        private MenuItem BuildActionItem(
            string caption,
            string detail,
            Action<string> action,
            string propertyId,
            TabletShellContext context,
            bool warning)
        {
            return TabletUiHelpers.CreateActionItem(
                caption,
                detail,
                () => ExecuteAction(action, propertyId, context),
                warning ? WarningIdle : null,
                warning ? WarningActive : null,
                null,
                warning ? "DUE" : null);
        }

        private static void ExecuteAction(Action<string> action, string propertyId, TabletShellContext context)
        {
            if (action == null || string.IsNullOrWhiteSpace(propertyId))
            {
                return;
            }

            action(propertyId);
            if (context != null)
            {
                context.Refresh();
            }
        }

        private static TabletShellPage BuildUnavailablePage(TabletStateSnapshot snapshot, string message, Action goBack)
        {
            return new TabletShellPage
            {
                Title = "Property Portfolio",
                Subtitle = "Unavailable",
                HeaderRightText = TabletUiHelpers.BuildBalanceChrome(snapshot),
                WidthScale = 0.88f,
                Items = new[]
                {
                    TabletUiHelpers.CreateInfoItem("Unavailable", message),
                    TabletUiHelpers.CreateNavigationItem("Back", "Return to the previous page.", goBack, "BACK"),
                },
            };
        }

        private static string BuildOfficeRootDetail(TabletPropertyPortfolioSummary summary, TabletPropertyOfficeEntry activeOffice)
        {
            if (activeOffice == null)
            {
                return summary.TotalArrears > 0.01f
                    ? string.Format("No active office | Arrears {0}", ModFormatting.FormatMoney(summary.Offices.Sum(entry => entry.ArrearsAmount)))
                    : "No active office selected.";
            }

            return string.Format("Active: {0} | {1}", activeOffice.DisplayName, activeOffice.AssignmentSummary);
        }

        private static string BuildApartmentRootDetail(TabletPropertyPortfolioSummary summary, TabletPropertyApartmentEntry activeApartment)
        {
            if (activeApartment == null)
            {
                return summary.Apartments.Any(entry => entry.HasArrears)
                    ? string.Format("No active residence | Arrears {0}", ModFormatting.FormatMoney(summary.Apartments.Sum(entry => entry.ArrearsAmount)))
                    : "No active residence selected.";
            }

            return string.Format("Active: {0} | {1}", activeApartment.DisplayName, activeApartment.BillDetail);
        }

        private static string BuildOfficeCaption(TabletPropertyOfficeEntry office)
        {
            if (office.IsActive)
            {
                return string.Format("{0} ~g~ACTIVE~s~", office.DisplayName);
            }

            if (office.HasArrears)
            {
                return string.Format("{0} ~r~ARREARS~s~", office.DisplayName);
            }

            return office.DisplayName;
        }

        private static string BuildApartmentCaption(TabletPropertyApartmentEntry apartment)
        {
            if (apartment.IsActive)
            {
                return string.Format("{0} ~g~ACTIVE~s~", apartment.DisplayName);
            }

            if (apartment.HasArrears)
            {
                return string.Format("{0} ~r~ARREARS~s~", apartment.DisplayName);
            }

            return apartment.DisplayName;
        }

        private static Color GetItemIdleColor(bool hasArrears, bool isOwned, bool isRented)
        {
            if (hasArrears)
            {
                return WarningIdle;
            }

            if (isOwned)
            {
                return OwnedIdle;
            }

            if (isRented)
            {
                return RentalIdle;
            }

            return AvailableIdle;
        }

        private static Color GetItemActiveColor(bool hasArrears, bool isOwned, bool isRented)
        {
            if (hasArrears)
            {
                return WarningActive;
            }

            if (isOwned)
            {
                return OwnedActive;
            }

            if (isRented)
            {
                return RentalActive;
            }

            return AvailableActive;
        }
    }
}
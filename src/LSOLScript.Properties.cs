using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using OfficeMenuItem = LSOL.UI.MenuItem;
using WinForms = System.Windows.Forms;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private const int ApartmentSleepHours = 8;
        private const int ApartmentSleepCooldownMinutes = 16 * 60;
        private const int ApartmentSleepFadeDurationMs = 650;
        private const int ApartmentSleepFadeSafetyBufferMs = 250;
        private const int ApartmentSleepBlackoutDurationMs = 2000;
        private const int PersonalDealershipCategoryListStartIndex = 1;
        private const int PersonalDealershipVehicleListStartIndex = 1;
        private const float ApartmentExteriorMarkerScale = 1.3f;
        private const float ApartmentInteriorMarkerScale = 1.2f;
        private const float ApartmentGarageMarkerScale = 1.3f;
        private const float MotelExteriorMarkerScale = 1.3f;
        private const float DealershipInteractionDistance = 4.6f;
        private const float PersonalDealershipVehiclePadHeading = 70f;
        private const string PersonalDealershipAllCategory = "All";
        private const string PersonalDealershipUncategorizedCategory = "Uncategorized";

        private static readonly Vector3 CommercialDealershipMarker = new Vector3(-979.56f, -2232.48f, 8.86f);
        private static readonly Vector3 PersonalDealershipMarker = new Vector3(-38.68f, -1109.47f, 26.44f);
        private static readonly Vector3 PersonalDealershipVehiclePadPosition = new Vector3(-42.77f, -1112.92f, 26.44f);

        private LemonMenu _commercialGarageMenu;
        private LemonMenu _commercialGarageActionMenu;
        private LemonMenu _officeObjectsMenu;
        private LemonMenu _officeObjectPurchaseMenu;
        private LemonMenu _officeFuelManagementMenu;
        private LemonMenu _apartmentMenu;
        private LemonMenu _apartmentInteriorMenu;
        private LemonMenu _motelMenu;
        private LemonMenu _personalGarageMenu;
        private LemonMenu _personalDealershipMenu;
        private Vehicle _personalDealershipPreviewVehicle;
        private OfficeDefinition _menuOffice;
        private InteriorDefinition _menuApartment;
        private MotelDefinition _menuMotel;
        private CommercialGarageMenuContext _commercialGarageMenuContext;
        private OwnedCommercialVehiclePersistenceEntry _selectedCommercialGarageVehicle;
        private CommercialDealershipAcquisitionMode _commercialDealershipAcquisitionMode;
        private List<OfficeObjectDefinition> _officeObjectPreviewSlots;
        private List<string> _personalDealershipCategories;
        private List<DealershipVehicleDefinition> _personalDealershipVisibleVehicles;
        private string _activePersonalDealershipCategory;
        private string _personalDealershipPreviewModelName;
        private string _personalDealershipSelectedVehicleModelName;
        private OfficeObjectDefinition _pendingOfficeObjectPurchaseDefinition;
        private ApartmentSleepTransitionPhase _apartmentSleepTransitionPhase;
        private int _apartmentSleepTransitionPhaseStartedAt;
        private bool _apartmentSleepClockApplied;
        private int _selectedPersonalDealershipCategoryIndex;

        private enum ApartmentSleepTransitionPhase
        {
            None = 0,
            FadingOut = 1,
            HoldingBlack = 2,
            FadingIn = 3,
        }

        [Flags]
        private enum PropertyUiRefreshFlags
        {
            None = 0,
            Balance = 1,
            Network = 2,
            OfficeMenu = 4,
            ApartmentMenu = 8,
            CommercialGarage = 16,
            AllTablet = 32,
            PlayerSuccesses = 64,
        }

        private struct PropertyActionResult
        {
            public PropertyActionResult(bool succeeded, string message)
            {
                Succeeded = succeeded;
                Message = message ?? string.Empty;
            }

            public bool Succeeded { get; }

            public string Message { get; }
        }

        private PropertyPortfolioTabletActions CreatePropertyPortfolioTabletActions()
        {
            return new PropertyPortfolioTabletActions
            {
                RentOffice = RentOfficeById,
                TransferOfficeRental = TransferOfficeRentalById,
                PurchaseOffice = PurchaseOfficeById,
                ActivateOffice = ActivateOfficeById,
                RelinquishOfficeRental = RelinquishOfficeRentalById,
                PayOfficeArrears = PayOfficeArrearsById,
                PurchaseApartment = PurchaseApartmentById,
                RentApartment = RentApartmentById,
                ActivateApartment = ActivateApartmentById,
                CancelApartmentRental = CancelApartmentRentalById,
                SellApartment = SellApartmentById,
                PayApartmentArrears = PayApartmentArrearsById,
                RestAtMotel = RestAtMotelById,
            };
        }

        private void InitializePropertyMenus()
        {
            _commercialGarageMenu = new LemonMenu("Commercial Garage")
            {
                Subtitle = "Retrieve, store, and swap vehicles",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _commercialGarageActionMenu = new LemonMenu("Vehicle")
            {
                Subtitle = "Retrieve, store, reserve, or close the contract",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeObjectsMenu = new LemonMenu("Office Objects")
            {
                Subtitle = "Buy decorative or useful props",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeObjectPurchaseMenu = new LemonMenu("Confirm Purchase")
            {
                Subtitle = "Review price, office limits, and placement requirements",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeFuelManagementMenu = new LemonMenu("Fuel Management")
            {
                Subtitle = "Refuel vehicles or manage diesel deliveries",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _apartmentMenu = new LemonMenu("Apartment")
            {
                Subtitle = "Manage residence access and personal storage",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _apartmentInteriorMenu = new LemonMenu("Apartment")
            {
                Subtitle = "Sleep or leave the active residence",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _motelMenu = new LemonMenu("Motel")
            {
                Subtitle = "Pay for a room and rest for the night",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _personalGarageMenu = new LemonMenu("Personal Garage")
            {
                Subtitle = "Retrieve and store owned personal vehicles",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _personalDealershipMenu = new LemonMenu("Vehicle Dealership")
            {
                Subtitle = "Purchase personal vehicles for the active residence",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _commercialGarageMenuContext = CommercialGarageMenuContext.Office;
            _commercialDealershipAcquisitionMode = CommercialDealershipAcquisitionMode.Purchase;
            _officeObjectPreviewSlots = new List<OfficeObjectDefinition>();
            _personalDealershipCategories = new List<string>();
            _personalDealershipVisibleVehicles = new List<DealershipVehicleDefinition>();
        }

        private bool HasPropertyMenuOpen()
        {
            return (_commercialGarageMenu != null && _commercialGarageMenu.IsOpen)
                || (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
                || (_officeObjectsMenu != null && _officeObjectsMenu.IsOpen)
                || (_officeObjectPurchaseMenu != null && _officeObjectPurchaseMenu.IsOpen)
                || (_officeFuelManagementMenu != null && _officeFuelManagementMenu.IsOpen)
                || (_apartmentMenu != null && _apartmentMenu.IsOpen)
                || (_apartmentInteriorMenu != null && _apartmentInteriorMenu.IsOpen)
                || (_motelMenu != null && _motelMenu.IsOpen)
                || (_personalGarageMenu != null && _personalGarageMenu.IsOpen)
                || (_personalDealershipMenu != null && _personalDealershipMenu.IsOpen);
        }

        private void DrawPropertyMenus()
        {
            if (_commercialGarageMenu != null)
            {
                _commercialGarageMenu.Draw();
            }

            if (_commercialGarageActionMenu != null)
            {
                _commercialGarageActionMenu.Draw();
            }

            if (_officeObjectsMenu != null)
            {
                _officeObjectsMenu.Draw();
            }

            if (_officeObjectPurchaseMenu != null)
            {
                _officeObjectPurchaseMenu.Draw();
            }

            if (_officeFuelManagementMenu != null)
            {
                _officeFuelManagementMenu.Draw();
            }

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Draw();
            }

            if (_apartmentInteriorMenu != null)
            {
                _apartmentInteriorMenu.Draw();
            }

            if (_motelMenu != null)
            {
                _motelMenu.Draw();
            }

            if (_personalGarageMenu != null)
            {
                _personalGarageMenu.Draw();
            }

            if (_personalDealershipMenu != null)
            {
                _personalDealershipMenu.Draw();
            }
        }

        private void ClosePropertyMenus()
        {
            if (_commercialGarageMenu != null)
            {
                _commercialGarageMenu.Close();
            }

            if (_commercialGarageActionMenu != null)
            {
                _commercialGarageActionMenu.Close();
            }

            if (_officeObjectsMenu != null)
            {
                _officeObjectsMenu.Close();
            }

            if (_officeObjectPurchaseMenu != null)
            {
                _officeObjectPurchaseMenu.Close();
            }

            if (_officeFuelManagementMenu != null)
            {
                _officeFuelManagementMenu.Close();
            }

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Close();
            }

            if (_apartmentInteriorMenu != null)
            {
                _apartmentInteriorMenu.Close();
            }

            if (_motelMenu != null)
            {
                _motelMenu.Close();
            }

            if (_personalGarageMenu != null)
            {
                _personalGarageMenu.Close();
            }

            if (_personalDealershipMenu != null)
            {
                _personalDealershipMenu.Close();
            }

            ClearPersonalDealershipPreviewVehicle();
        }

        private bool HandlePropertyMenuKey(WinForms.Keys key)
        {
            if (_personalDealershipMenu != null && _personalDealershipMenu.IsOpen)
            {
                var menuBackKey = _controls != null ? _controls.MenuBack : WinForms.Keys.Back;
                if (!string.IsNullOrWhiteSpace(_activePersonalDealershipCategory)
                    && (key == WinForms.Keys.Back || key == menuBackKey))
                {
                    ReturnToPersonalDealershipCategories();
                    return true;
                }

                _personalDealershipMenu.HandleKey(key, _controls);
                if (!_personalDealershipMenu.IsOpen)
                {
                    ClearPersonalDealershipPreviewVehicle();
                }

                return true;
            }

            if (_personalGarageMenu != null && _personalGarageMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    _personalGarageMenu.Close();
                    return true;
                }

                _personalGarageMenu.HandleKey(key, _controls);
                return true;
            }

            if (_commercialGarageMenu != null && _commercialGarageMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnFromCommercialGarageMenu();
                    return true;
                }

                _commercialGarageMenu.HandleKey(key, _controls);
                return true;
            }

            if (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToCommercialGarageMenu();
                    return true;
                }

                _commercialGarageActionMenu.HandleKey(key, _controls);
                return true;
            }

            if (_officeObjectPurchaseMenu != null && _officeObjectPurchaseMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToOfficeObjectsMenu();
                    return true;
                }

                _officeObjectPurchaseMenu.HandleKey(key, _controls);
                return true;
            }

            if (_officeObjectsMenu != null && _officeObjectsMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToOfficeMenuFromObjects();
                    return true;
                }

                _officeObjectsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_officeFuelManagementMenu != null && _officeFuelManagementMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToOfficeMenuFromFuelManagement();
                    return true;
                }

                _officeFuelManagementMenu.HandleKey(key, _controls);
                return true;
            }

            if (_apartmentMenu != null && _apartmentMenu.IsOpen)
            {
                _apartmentMenu.HandleKey(key, _controls);
                return true;
            }

            if (_apartmentInteriorMenu != null && _apartmentInteriorMenu.IsOpen)
            {
                _apartmentInteriorMenu.HandleKey(key, _controls);
                return true;
            }

            if (_motelMenu != null && _motelMenu.IsOpen)
            {
                _motelMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        private void ProcessPropertyWeeklyCharges()
        {
            var balanceBefore = _profit;
            var updatedBalance = _profit;
            var messages = _propertyManager.ProcessWeeklyCharges(GetCurrentInGameWeekMinute(), ref updatedBalance);
            if (Math.Abs(updatedBalance - _profit) > 0.001f)
            {
                _profit = updatedBalance;
                _tabletStateStore.MarkBalanceDirty();
            }

            if (messages.Count > 0)
            {
                ShowStatus(messages[messages.Count - 1], 5000);
                RebuildOfficeMenuItems();
                RebuildApartmentMenuItems();
            }
        }

        private void ProcessTerritoryWeeklyCharges()
        {
            if (_territoryManager == null)
            {
                return;
            }

            var charge = _territoryManager.ProcessWeeklyOperationsCharges(GetCurrentInGameWeekMinute());
            if (charge == null || charge.TotalAmount <= 0.01f)
            {
                return;
            }

            DeductProfit(
                CompanyFinanceCategory.TerritoryOperations,
                charge.TotalAmount,
                charge.ChargeCount > 1
                    ? string.Format("Territory operations for {0} weeks", charge.ChargeCount)
                    : "Weekly territory operations");

            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            var summary = charge.Summary ?? new TerritoryOperationsSummary();
            ShowStatus(
                string.Format(
                    "Territory operations billed {0}: {1} district{2}, {3} corridor{4}, {5} support site{6}, {7} premium franchise{8}.",
                    ModFormatting.FormatMoney(charge.TotalAmount),
                    summary.ChargedDistrictCount,
                    summary.ChargedDistrictCount == 1 ? string.Empty : "s",
                    summary.ActiveCorridorCount,
                    summary.ActiveCorridorCount == 1 ? string.Empty : "s",
                    summary.SupportSiteCount,
                    summary.SupportSiteCount == 1 ? string.Empty : "s",
                    summary.PremiumFranchiseCount,
                    summary.PremiumFranchiseCount == 1 ? string.Empty : "s"),
                5000);
        }

        private void ProcessTerritoryWeeklyMaintenance()
        {
            if (_territoryManager == null)
            {
                return;
            }

            var result = _territoryManager.ProcessWeeklyMaintenance(GetCurrentInGameWeekMinute());
            if (result == null || result.ProcessedWeekCount <= 0)
            {
                return;
            }

            if (result.ServiceSinkStaffingExpense > 0.01f)
            {
                DeductProfit(
                    CompanyFinanceCategory.ServiceSiteStaffing,
                    result.ServiceSinkStaffingExpense,
                    BuildServiceSiteStaffingFinanceDescription(result));
            }

            if (result.ServiceSinkPassiveIncome > 0.01f)
            {
                AddProfit(
                    CompanyFinanceCategory.IndustryIncome,
                    result.ServiceSinkPassiveIncome,
                    BuildServiceSinkPassiveIncomeFinanceDescription(result));
            }

            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            RebuildOfficeMenuItems();
            var summaryMessage = result.Messages.Count > 0 ? result.Messages[result.Messages.Count - 1] : string.Empty;
            var staffingMessage = BuildServiceSiteStaffingStatus(result);
            var passiveIncomeMessage = BuildServiceSinkPassiveIncomeStatus(result);
            var statusSegments = new List<string>();
            if (!string.IsNullOrWhiteSpace(staffingMessage))
            {
                statusSegments.Add(staffingMessage);
            }

            if (!string.IsNullOrWhiteSpace(passiveIncomeMessage))
            {
                statusSegments.Add(passiveIncomeMessage);
            }

            if (!string.IsNullOrWhiteSpace(summaryMessage))
            {
                statusSegments.Add(summaryMessage);
            }

            if (statusSegments.Count > 0)
            {
                ShowStatus(string.Join(" ", statusSegments.ToArray()), 5000);
            }
        }

        private static string BuildServiceSinkPassiveIncomeFinanceDescription(TerritoryWeeklyMaintenanceResult result)
        {
            if (result != null && result.ProcessedWeekCount > 1)
            {
                return string.Format("Passive income from staffed and supplied stores and gas stations ({0} weeks)", result.ProcessedWeekCount);
            }

            return "Passive income from staffed and supplied stores and gas stations";
        }

        private static string BuildServiceSiteStaffingFinanceDescription(TerritoryWeeklyMaintenanceResult result)
        {
            if (result != null && result.ProcessedWeekCount > 1)
            {
                return string.Format("Weekly site staffing for owned stores and gas stations ({0} weeks)", result.ProcessedWeekCount);
            }

            return "Weekly site staffing for owned stores and gas stations";
        }

        private static string BuildServiceSinkPassiveIncomeStatus(TerritoryWeeklyMaintenanceResult result)
        {
            if (result == null || result.ServiceSinkPassiveIncome <= 0.01f)
            {
                return string.Empty;
            }

            if (result.ProcessedWeekCount > 1)
            {
                return string.Format(
                    "Staffed and supplied stores and gas stations generated {0} in passive income over {1} weeks.",
                    ModFormatting.FormatMoney(result.ServiceSinkPassiveIncome),
                    result.ProcessedWeekCount);
            }

            return string.Format(
                "Staffed and supplied stores and gas stations generated {0} in passive income.",
                ModFormatting.FormatMoney(result.ServiceSinkPassiveIncome));
        }

        private static string BuildServiceSiteStaffingStatus(TerritoryWeeklyMaintenanceResult result)
        {
            if (result == null || result.ServiceSinkStaffingExpense <= 0.01f)
            {
                return string.Empty;
            }

            if (result.ProcessedWeekCount > 1)
            {
                return string.Format(
                    "Store and gas-station payroll billed {0} over {1} weeks.",
                    ModFormatting.FormatMoney(result.ServiceSinkStaffingExpense),
                    result.ProcessedWeekCount);
            }

            return string.Format(
                "Store and gas-station payroll billed {0}.",
                ModFormatting.FormatMoney(result.ServiceSinkStaffingExpense));
        }

        private void DrawPropertyMarkers(Ped player, bool canShowPrompts, ref bool promptShown)
        {
            var playerPos = player.Position;
            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            var apartmentExteriorInteractionDistance = GetApartmentExteriorInteractionDistance();
            var apartmentInteriorInteractionDistance = GetApartmentInteriorInteractionDistance();
            var apartmentGarageInteractionDistance = GetApartmentGarageInteractionDistance();
            var motelExteriorInteractionDistance = GetMotelExteriorInteractionDistance();
            var activeOffice = _propertyManager.ActiveOffice;
            var activeApartment = _propertyManager.ActiveApartment;
            Vector3 activeApartmentGaragePosition;
            var canUseActiveApartmentGarage = TryGetActiveApartmentGaragePosition(out activeApartmentGaragePosition);

            OfficeObjectManager.FacilityInteractionContext facilityInteraction;
            if (canShowPrompts
                && !promptShown
                && _officeObjectManager != null
                && _officeObjectManager.TryGetNearbyFacilityInteraction(player, out facilityInteraction))
            {
                Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to {1}.", KeyName(_controls.Interact), facilityInteraction.PromptDescription)));
                promptShown = true;
            }

            for (int i = 0; i < _propertyManager.Offices.Count; i++)
            {
                var office = _propertyManager.Offices[i];
                if (office == null || playerPos.DistanceToSquared(office.MarkerPosition) > drawDistanceSq)
                {
                    continue;
                }

                var isActive = activeOffice != null && string.Equals(activeOffice.OfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase);
                World.DrawMarker(
                    MarkerType.Cylinder,
                    office.MarkerPosition,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(_config.MarkerRadius * 1.4f, _config.MarkerRadius * 1.4f, _config.MarkerHeight),
                    isActive ? Color.FromArgb(210, 98, 208, 132) : Color.FromArgb(200, 52, 170, 238),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(office.MarkerPosition) <= OfficeInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to manage {1}.", KeyName(_controls.Interact), office.DisplayName)));
                    promptShown = true;
                }
            }

            if (canUseActiveApartmentGarage && playerPos.DistanceToSquared(activeApartmentGaragePosition) <= drawDistanceSq)
            {
                World.DrawMarker(
                    MarkerType.Cylinder,
                    activeApartmentGaragePosition,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(apartmentGarageInteractionDistance, apartmentGarageInteractionDistance, _config.MarkerHeight),
                    Color.FromArgb(205, 98, 176, 220),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(activeApartmentGaragePosition) <= apartmentGarageInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to manage your personal garage.", KeyName(_controls.Interact))));
                    promptShown = true;
                }
            }

            for (int i = 0; i < _propertyManager.Interiors.Count; i++)
            {
                var apartment = _propertyManager.Interiors[i];
                if (apartment == null || playerPos.DistanceToSquared(apartment.ExteriorPosition) > drawDistanceSq)
                {
                    continue;
                }

                var isActive = activeApartment != null && string.Equals(activeApartment.InteriorId, apartment.InteriorId, StringComparison.OrdinalIgnoreCase);
                World.DrawMarker(
                    MarkerType.Cylinder,
                    apartment.ExteriorPosition,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(apartmentExteriorInteractionDistance, apartmentExteriorInteractionDistance, _config.MarkerHeight),
                    isActive ? Color.FromArgb(210, 220, 188, 84) : Color.FromArgb(205, 188, 134, 82),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(apartment.ExteriorPosition) <= apartmentExteriorInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to manage {1}.", KeyName(_controls.Interact), apartment.DisplayName)));
                    promptShown = true;
                }
            }

            for (int i = 0; i < _propertyManager.Motels.Count; i++)
            {
                var motel = _propertyManager.Motels[i];
                if (motel == null || playerPos.DistanceToSquared(motel.ExteriorPosition) > drawDistanceSq)
                {
                    continue;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    motel.ExteriorPosition,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(motelExteriorInteractionDistance, motelExteriorInteractionDistance, _config.MarkerHeight),
                    Color.FromArgb(205, 214, 164, 90),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(motel.ExteriorPosition) <= motelExteriorInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to rest at {1}.", KeyName(_controls.Interact), motel.DisplayName)));
                    promptShown = true;
                }
            }

            if (activeApartment != null && playerPos.DistanceToSquared(activeApartment.InteriorPosition) <= drawDistanceSq)
            {
                World.DrawMarker(
                    MarkerType.Cylinder,
                    activeApartment.InteriorPosition,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(apartmentInteriorInteractionDistance, apartmentInteriorInteractionDistance, _config.MarkerHeight),
                    Color.FromArgb(205, 234, 196, 110),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(activeApartment.InteriorPosition) <= apartmentInteriorInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} for apartment options.", KeyName(_controls.Interact))));
                    promptShown = true;
                }
            }

            DrawDealershipMarker(playerPos, CommercialDealershipMarker, Color.FromArgb(205, 94, 174, 220), canShowPrompts, ref promptShown, "browse the commercial dealership");
            DrawDealershipMarker(playerPos, PersonalDealershipMarker, Color.FromArgb(205, 228, 156, 82), canShowPrompts, ref promptShown, "browse the personal vehicle dealership");
        }

        private void DrawDealershipMarker(Vector3 playerPosition, Vector3 markerPosition, Color color, bool canShowPrompts, ref bool promptShown, string promptDescription)
        {
            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            if (playerPosition.DistanceToSquared(markerPosition) > drawDistanceSq)
            {
                return;
            }

            World.DrawMarker(
                MarkerType.Cylinder,
                markerPosition,
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(_config.MarkerRadius * 1.25f, _config.MarkerRadius * 1.25f, _config.MarkerHeight),
                color,
                false,
                false,
                false,
                null,
                null,
                false);

            if (canShowPrompts && !promptShown && playerPosition.DistanceTo(markerPosition) <= DealershipInteractionDistance)
            {
                Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to {1}.", KeyName(_controls.Interact), promptDescription)));
                promptShown = true;
            }
        }

        private bool HandlePropertyInteraction(Ped player)
        {
            var activeApartment = _propertyManager.ActiveApartment;
            if (activeApartment != null && player.Position.DistanceTo(activeApartment.InteriorPosition) <= GetApartmentInteriorInteractionDistance())
            {
                OpenActiveApartmentInteriorMenu();
                return true;
            }

            Vector3 activeApartmentGaragePosition;
            if (TryGetActiveApartmentGaragePosition(out activeApartmentGaragePosition)
                && player.Position.DistanceTo(activeApartmentGaragePosition) <= GetApartmentGarageInteractionDistance())
            {
                OpenPersonalGarageMenu();
                return true;
            }

            OfficeObjectManager.FacilityInteractionContext facilityInteraction;
            if (_officeObjectManager != null
                && _officeObjectManager.TryGetNearbyFacilityInteraction(player, out facilityInteraction)
                && HandleOfficeFacilityInteraction(facilityInteraction))
            {
                return true;
            }

            var office = GetOfficeInInteractionRange(player.Position);
            if (office != null)
            {
                OpenOfficeMenuFor(office);
                return true;
            }

            var apartment = GetApartmentInInteractionRange(player.Position);
            if (apartment != null)
            {
                OpenApartmentMenuFor(apartment);
                return true;
            }

            var motel = GetMotelInInteractionRange(player.Position);
            if (motel != null)
            {
                OpenMotelMenuFor(motel);
                return true;
            }

            if (IsNearCommercialDealership(player.Position))
            {
                OpenCommercialDealershipMenu();
                return true;
            }

            if (IsNearPersonalDealership(player.Position))
            {
                OpenPersonalDealershipMenu();
                return true;
            }

            return false;
        }

        private bool HandleOfficeFacilityInteraction(OfficeObjectManager.FacilityInteractionContext interaction)
        {
            if (interaction == null || string.IsNullOrWhiteSpace(interaction.OfficeId))
            {
                return false;
            }

            var office = _propertyManager.GetOfficeDefinition(interaction.OfficeId);
            if (office == null)
            {
                return false;
            }

            _menuOffice = office;
            switch (interaction.InteractionType)
            {
                case OfficeFacilityInteractionType.OfficeSummary:
                    OpenOfficeMenuFor(office);
                    return true;
                case OfficeFacilityInteractionType.HireNpc:
                    OpenNpcHiringMenu();
                    return true;
                case OfficeFacilityInteractionType.RepairVehicle:
                    RepairVehicleAtOffice();
                    return true;
                case OfficeFacilityInteractionType.FuelManagement:
                    OpenOfficeFuelManagementMenu();
                    return true;
                case OfficeFacilityInteractionType.HeadquartersStatus:
                    ShowStatus(BuildHeadquartersFacilityStatus(office));
                    return true;
                default:
                    return false;
            }
        }

        private string BuildHeadquartersFacilityStatus(OfficeDefinition office)
        {
            var summary = _playerSuccessTracker != null
                ? _playerSuccessTracker.GetEndgameSummary()
                : new CompanyEndgameSummary();
            var headquartersInstalled = office != null && _propertyManager.HasOfficeObjectFunction(office.OfficeId, OfficeObjectFunction.Headquarters);
            var opportunity = !string.IsNullOrWhiteSpace(summary.PrimaryOpportunitySummary)
                ? summary.PrimaryOpportunitySummary
                : (summary.DoctrineLead != null ? summary.DoctrineLead.ReasonSummary ?? string.Empty : string.Empty);
            return string.Format(
                "{0} | {1} | Prestige {2:0}/{3:0}\n{4} | {5}",
                office != null ? office.DisplayName : "Headquarters",
                TabletEndgameStatusFormatter.BuildStatusCaption(summary),
                Math.Max(0f, summary.PrestigeScore),
                Math.Max(Math.Max(0f, summary.PrestigeScore), Math.Max(0f, summary.HighestPrestigeScore)),
                headquartersInstalled ? "Landmark annex online" : "Landmark annex pending",
                opportunity);
        }

        private OfficeDefinition GetOfficeInInteractionRange(Vector3 position)
        {
            for (int i = 0; i < _propertyManager.Offices.Count; i++)
            {
                var office = _propertyManager.Offices[i];
                if (office != null && position.DistanceTo(office.MarkerPosition) <= OfficeInteractionDistance)
                {
                    return office;
                }
            }

            return null;
        }

        private InteriorDefinition GetApartmentInInteractionRange(Vector3 position)
        {
            var apartmentExteriorInteractionDistance = GetApartmentExteriorInteractionDistance();
            for (int i = 0; i < _propertyManager.Interiors.Count; i++)
            {
                var apartment = _propertyManager.Interiors[i];
                if (apartment != null && position.DistanceTo(apartment.ExteriorPosition) <= apartmentExteriorInteractionDistance)
                {
                    return apartment;
                }
            }

            return null;
        }

        private float GetApartmentExteriorInteractionDistance()
        {
            return _config.MarkerRadius * ApartmentExteriorMarkerScale;
        }

        private MotelDefinition GetMotelInInteractionRange(Vector3 position)
        {
            var motelExteriorInteractionDistance = GetMotelExteriorInteractionDistance();
            for (int i = 0; i < _propertyManager.Motels.Count; i++)
            {
                var motel = _propertyManager.Motels[i];
                if (motel != null && position.DistanceTo(motel.ExteriorPosition) <= motelExteriorInteractionDistance)
                {
                    return motel;
                }
            }

            return null;
        }

        private float GetMotelExteriorInteractionDistance()
        {
            return _config.MarkerRadius * MotelExteriorMarkerScale;
        }

        private float GetApartmentInteriorInteractionDistance()
        {
            return _config.MarkerRadius * ApartmentInteriorMarkerScale;
        }

        private float GetApartmentGarageInteractionDistance()
        {
            return _config.MarkerRadius * ApartmentGarageMarkerScale;
        }

        private bool TryGetActiveApartmentGaragePosition(out Vector3 garagePosition)
        {
            garagePosition = Vector3.Zero;
            if (_propertyManager == null)
            {
                return false;
            }

            string reason;
            if (!_propertyManager.CanUseApartmentSystems(out reason))
            {
                return false;
            }

            var apartment = _propertyManager.ActiveApartment;
            if (apartment == null || apartment.GaragePosition == Vector3.Zero)
            {
                return false;
            }

            garagePosition = apartment.GaragePosition;
            return true;
        }

        private bool IsNearCommercialDealership(Vector3 position)
        {
            return position.DistanceTo(CommercialDealershipMarker) <= DealershipInteractionDistance;
        }

        private bool IsNearPersonalDealership(Vector3 position)
        {
            return position.DistanceTo(PersonalDealershipMarker) <= DealershipInteractionDistance;
        }

        private void OpenOfficeMenuFor(OfficeDefinition office)
        {
            _menuOffice = office;
            CloseIndustryTablet();
            CloseNonOfficeMenus();
            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private string BuildOfficeMenuSubtitle()
        {
            if (_menuOffice == null)
            {
                return "Manage office access and commercial operations";
            }

            var officeState = _propertyManager.GetOfficeState(_menuOffice.OfficeId);
            var stateLabel = officeState == null || (!officeState.IsOwned && !officeState.IsRented)
                ? "Available"
                : officeState.IsAccessSuspended || officeState.OutstandingRent > 0.01f
                    ? string.Format("Arrears {0}", ModFormatting.FormatMoney(officeState.OutstandingRent))
                    : officeState.IsOwned
                        ? "Owned"
                        : "Rented";
            return string.Format("{0} | Rent {1} | Buy {2}", stateLabel, ModFormatting.FormatMoney(_menuOffice.WeeklyOfficeRent), ModFormatting.FormatMoney(_menuOffice.OfficePrice));
        }

        private IEnumerable<OfficeMenuItem> BuildOfficeMenuItems()
        {
            var items = new List<OfficeMenuItem>();
            var office = _menuOffice;
            var officeState = office != null ? _propertyManager.GetOfficeState(office.OfficeId) : null;
            var transferSourceState = office != null
                ? _propertyManager.Offices
                    .Where(candidate => candidate != null && !string.Equals(candidate.OfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => _propertyManager.GetOfficeState(candidate.OfficeId))
                    .FirstOrDefault(state => state != null && state.IsRented && !state.IsOwned)
                : null;
            var isActiveOffice = office != null && string.Equals(_propertyManager.ActiveOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase);
            var isOwned = officeState != null && officeState.IsOwned;
            var hasAccess = office != null && officeState != null && (officeState.IsOwned || officeState.IsRented);
            var hasArrears = officeState != null && officeState.OutstandingRent > 0.01f;
            var isRentalOnly = officeState != null && officeState.IsRented && !officeState.IsOwned;

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_profit)),
                DetailFactory = () => office != null
                    ? string.Format("{0} vehicle slots | {1}", BuildOfficeGarageCapacityLabel(office), office.DistrictName)
                    : "No office selected.",
            });

            if (office == null)
            {
                return items;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => office.DisplayName,
                DetailFactory = () => string.Format("Weekly rent {0} | Purchase {1}", ModFormatting.FormatMoney(office.WeeklyOfficeRent), ModFormatting.FormatMoney(office.OfficePrice)),
            });

            if (!hasAccess)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => transferSourceState != null ? "Transfer Office Rental" : "Rent Office",
                    DetailFactory = () => transferSourceState != null
                        ? string.Format("Pay {0} to move your current rental contract to this office.", ModFormatting.FormatMoney(office.WeeklyOfficeRent))
                        : string.Format("Pay {0} to unlock access at this office.", ModFormatting.FormatMoney(office.WeeklyOfficeRent)),
                    OnActivate = transferSourceState != null ? (Action)TransferSelectedOfficeRental : RentSelectedOffice,
                });
            }

            if (!isOwned && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Purchase Office",
                    DetailFactory = () => string.Format("Pay {0} to own this office permanently.", ModFormatting.FormatMoney(office.OfficePrice)),
                    OnActivate = PurchaseSelectedOffice,
                });
            }

            if (hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Settle Office Arrears",
                    DetailFactory = () => string.Format("Outstanding balance: {0}", ModFormatting.FormatMoney(officeState.OutstandingRent)),
                    OnActivate = PaySelectedOfficeArrears,
                });
            }

            if (!hasAccess)
            {
                return items;
            }

            if (isRentalOnly)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Relinquish Rental",
                    DetailFactory = () => isActiveOffice
                        ? "Close this rental and move operations to another available office you already control."
                        : "Close this inactive rental to stop future weekly charges.",
                    OnActivate = RelinquishSelectedOfficeRental,
                });
            }

            if (!isActiveOffice && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Activate Office",
                    DetailFactory = () => string.Format("Make {0} the active commercial garage.", office.DisplayName),
                    OnActivate = ActivateSelectedOffice,
                });
            }
            else if (!hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Worker Model: < {0} >", _workerSpawnController.SelectedWorkerDisplayName),
                    OnLeft = () => ChangeWorkerIndex(-1),
                    OnRight = () => ChangeWorkerIndex(1),
                    OnActivate = ApplyWorkerModel,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Garage",
                    DetailFactory = BuildCommercialGarageSummary,
                    OnActivate = OpenCommercialGarageMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Objects",
                    DetailFactory = BuildOfficeObjectsSummary,
                    OnActivate = OpenOfficeObjectsMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Fuel Management",
                    DetailFactory = BuildOfficeFuelManagementSummary,
                    OnActivate = OpenOfficeFuelManagementMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Repair Vehicle",
                    DetailFactory = BuildOfficeRepairDetail,
                    OnActivate = RepairVehicleAtOffice,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Hire NPC",
                    DetailFactory = CurrentNpcHiringDetail,
                    OnActivate = OpenNpcHiringMenu,
                });
            }

            return items;
        }

        private string BuildCommercialGarageSummary()
        {
            var activeCount = _propertyManager.GetActiveCommercialGarageVehicles().Count();
            var reserveCount = _propertyManager.GetReserveCommercialVehicles().Count();
            var activeOffice = _propertyManager.ActiveOffice;
            var capacity = activeOffice != null ? BuildOfficeGarageCapacityLabel(activeOffice) : "0";
            return string.Format("Active {0}/{1} | Reserve {2}", activeCount, capacity, reserveCount);
        }

        private string BuildOfficeGarageCapacityLabel(OfficeDefinition office)
        {
            if (office == null)
            {
                return "0";
            }

            return _officeGarageLimitDifficultyEnabled
                ? Math.Max(0, office.MaxCommercialVehicles).ToString()
                : "Unlimited";
        }

        private string BuildOfficeObjectsSummary()
        {
            if (_menuOffice == null)
            {
                return "No office selected.";
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to buy and place objects here.";
            }

            var objects = _propertyManager.GetOfficeObjects(_menuOffice.OfficeId, true);
            var placed = objects.Count(entry => entry != null && entry.IsPlaced);
            var pending = objects.Count - placed;
            var placedDefinitions = objects
                .Where(entry => entry != null && entry.IsPlaced)
                .Select(entry => _propertyManager.GetOfficeObjectDefinition(entry.DefinitionId))
                .Where(definition => definition != null)
                .ToList();
            var interactiveFacilities = placedDefinitions.Count(definition => definition.InteractionType != OfficeFacilityInteractionType.None);
            var staffedPosts = placedDefinitions.Sum(definition => definition.AmbientStaffRole == OfficeAmbientStaffRole.None ? 0 : Math.Max(1, definition.AmbientStaffCount));
            var catalogCount = _propertyManager.OfficeObjectCatalog.Count;
            return string.Format("Catalog {0} | Placed {1} | Pending placement {2} | Facilities {3} | Staff {4}", catalogCount, placed, pending, interactiveFacilities, staffedPosts);
        }

        private string BuildOfficeFuelManagementSummary()
        {
            return OfficeFuelManagementFormatter.BuildOfficeMenuSummary(BuildOfficeFuelManagementSnapshot());
        }

        private string BuildOfficeVehicleRefuelDetail()
        {
            return OfficeFuelManagementFormatter.BuildVehicleRefuelDetail(BuildOfficeFuelManagementSnapshot());
        }

        private string BuildOfficeFuelUnloadDetail()
        {
            return OfficeFuelManagementFormatter.BuildFuelUnloadDetail(BuildOfficeFuelManagementSnapshot());
        }

        private string BuildOfficeFuelDeliveryDetail()
        {
            return OfficeFuelManagementFormatter.BuildFuelDeliveryDetail(BuildOfficeFuelManagementSnapshot());
        }

        private string BuildOfficeFuelManagementStatusDetail()
        {
            return OfficeFuelManagementFormatter.BuildTankStatusDetail(BuildOfficeFuelManagementSnapshot());
        }

        private string BuildOfficeFuelManagementPricingDetail()
        {
            return OfficeFuelManagementFormatter.BuildPricingDetail(BuildOfficeFuelManagementSnapshot());
        }

        private OfficeFuelManagementSnapshot BuildOfficeFuelManagementSnapshot()
        {
            var snapshot = new OfficeFuelManagementSnapshot
            {
                HasSelectedOffice = _menuOffice != null,
            };

            if (_menuOffice == null)
            {
                return snapshot;
            }

            snapshot.OfficeIsActive = string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase);
            var spotRatePerLiter = _globalMarket != null
                ? Math.Max(0f, _globalMarket.GetUnitPrice("Fuel")) / 1000f
                : 0f;
            snapshot.SpotReplacementRatePerLiter = spotRatePerLiter;
            snapshot.DeliveredRatePerLiter = spotRatePerLiter * (_officeObjectManager != null ? _officeObjectManager.FuelDeliveryRateMultiplier : 1.05f);

            if (!snapshot.OfficeIsActive)
            {
                return snapshot;
            }

            var tank = _propertyManager.GetFirstPlacedOfficeObjectByFunction(_menuOffice.OfficeId, OfficeObjectFunction.Refuel);
            var definition = tank != null ? _propertyManager.GetOfficeObjectDefinition(tank.DefinitionId) : null;
            if (tank == null || definition == null)
            {
                return snapshot;
            }

            snapshot.HasTankInstalled = true;
            snapshot.StoredLiters = Math.Max(0f, tank.StoredResourceAmount);
            snapshot.CapacityLiters = Math.Max(0f, definition.Capacity);
            snapshot.FreeLiters = Math.Max(0f, snapshot.CapacityLiters - snapshot.StoredLiters);
            snapshot.EstimatedFillCost = snapshot.FreeLiters * snapshot.DeliveredRatePerLiter;
            snapshot.HasActiveDeliveryForOffice = _officeObjectManager != null
                && _officeObjectManager.HasActiveFuelDelivery
                && string.Equals(_officeObjectManager.ActiveFuelDeliveryOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase);

            var player = Game.Player != null ? Game.Player.Character : null;
            if (player == null || !player.Exists())
            {
                return snapshot;
            }

            Vehicle poweredVehicle;
            Vehicle cargoVehicle;
            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || poweredVehicle == null
                || !poweredVehicle.Exists())
            {
                return snapshot;
            }

            OwnedCommercialVehiclePersistenceEntry vehicleEntry;
            if (!_propertyManager.TryResolveCommercialVehicleRecord(poweredVehicle, out vehicleEntry)
                || vehicleEntry == null
                || !string.Equals(vehicleEntry.AssignedOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return snapshot;
            }

            _vehicleFuelSystem.EnsureTrackedVehicle(poweredVehicle, vehicleEntry.CurrentFuelLiters > 0.001f ? (float?)vehicleEntry.CurrentFuelLiters : null);
            var telemetry = _vehicleFuelSystem.GetTelemetry(poweredVehicle, cargoVehicle);
            if (telemetry == null || telemetry.CapacityLiters <= 0.01f)
            {
                return snapshot;
            }

            snapshot.HasEligibleOfficeVehicle = true;
            snapshot.VehicleName = poweredVehicle.DisplayName;
            snapshot.VehicleFuelNeededLiters = Math.Max(0f, telemetry.CapacityLiters - telemetry.CurrentLiters);
            snapshot.VehicleAlreadyFull = snapshot.VehicleFuelNeededLiters <= 0.05f;
            return snapshot;
        }

        private string BuildOfficeRepairDetail()
        {
            if (_menuOffice == null || !string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to use its Maintenance Bay.";
            }

            return _propertyManager.HasOfficeObjectFunction(_menuOffice.OfficeId, OfficeObjectFunction.Repair)
                ? "Repair the active office truck and trailer for free, either from the office menu or the maintenance desk."
                : "Install a repair facility first.";
        }

        private void OpenOfficeObjectsMenu()
        {
            if (_menuOffice == null)
            {
                return;
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus("Activate this office before buying or placing office objects.");
                return;
            }

            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            _officeMenu.Close();
            RebuildOfficeObjectMenuItems();
            _officeObjectsMenu.Open();
        }

        private void OpenOfficeFuelManagementMenu()
        {
            if (_menuOffice == null)
            {
                return;
            }

            _officeMenu.Close();
            RebuildOfficeFuelManagementMenuItems();
            _officeFuelManagementMenu.Open();
        }

        private void RebuildOfficeFuelManagementMenuItems()
        {
            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Diesel Tank Status",
                    DetailFactory = BuildOfficeFuelManagementStatusDetail,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Implied Rate",
                    DetailFactory = BuildOfficeFuelManagementPricingDetail,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Refuel Vehicle",
                    DetailFactory = BuildOfficeVehicleRefuelDetail,
                    OnActivate = RefuelVehicleFromOfficeTank,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Unload Fuel Cargo",
                    DetailFactory = BuildOfficeFuelUnloadDetail,
                    OnActivate = UnloadFuelCargoIntoOfficeTank,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Request Diesel Delivery",
                    DetailFactory = BuildOfficeFuelDeliveryDetail,
                    OnActivate = RequestOfficeFuelDelivery,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    DetailFactory = () => "Return to the office menu.",
                    OnActivate = ReturnToOfficeMenuFromFuelManagement,
                },
            };

            _officeFuelManagementMenu.Title = "Fuel Management";
            _officeFuelManagementMenu.Subtitle = "Manage refueling operations";
            _officeFuelManagementMenu.SetItems(items);
        }

        private void RebuildOfficeObjectMenuItems()
        {
            _officeObjectPreviewSlots.Clear();

            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = BuildOfficeObjectsSummary,
                    DetailFactory = () => _menuOffice != null
                        ? string.Format("{0} catalog entries available for {1}.", _propertyManager.OfficeObjectCatalog.Count, _menuOffice.DisplayName)
                        : "No office selected.",
                }
            };
            _officeObjectPreviewSlots.Add(null);

            var catalog = _propertyManager.OfficeObjectCatalog;
            if (catalog.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No office objects configured",
                    DetailFactory = () => "OfficeObjects.xml is empty or failed to load.",
                });
                _officeObjectPreviewSlots.Add(null);
            }
            else
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    var definition = catalog[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildOfficeObjectCaption(definition),
                        DetailFactory = () => BuildOfficeObjectDetail(definition),
                        OnActivate = () => HandleOfficeObjectSelection(definition),
                    });
                    _officeObjectPreviewSlots.Add(definition);
                }
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = ReturnToOfficeMenuFromObjects,
            });
            _officeObjectPreviewSlots.Add(null);

            _officeObjectsMenu.Title = "Office Objects";
            _officeObjectsMenu.Subtitle = "Buy modules or decorative props";
            _officeObjectsMenu.SetItems(items);
        }

        private string BuildOfficeObjectCaption(OfficeObjectDefinition definition)
        {
            return OfficeObjectCatalogFormatter.BuildCaption(definition);
        }

        private string BuildOfficeObjectDetail(OfficeObjectDefinition definition)
        {
            if (definition == null || _menuOffice == null)
            {
                return string.Empty;
            }

            var objects = _propertyManager.GetOfficeObjects(_menuOffice.OfficeId, true)
                .Where(entry => entry != null && entry.DefinitionId == definition.ObjectId)
                .ToList();
            var placed = objects.Count(entry => entry.IsPlaced);
            var pending = objects.Count - placed;

            return OfficeObjectCatalogFormatter.BuildDetail(definition, placed, pending);
        }

        private void HandleOfficeObjectSelection(OfficeObjectDefinition definition)
        {
            if (definition == null || _menuOffice == null)
            {
                return;
            }

            var pendingEntry = _propertyManager.GetOfficeObjects(_menuOffice.OfficeId, true)
                .FirstOrDefault(entry => entry != null && !entry.IsPlaced && entry.DefinitionId == definition.ObjectId);
            if (pendingEntry != null)
            {
                StartOfficeObjectPlacement(pendingEntry);
                return;
            }

            _pendingOfficeObjectPurchaseDefinition = definition;
            RebuildOfficeObjectPurchaseMenuItems();
            _officeObjectsMenu.Close();
            _officeObjectPurchaseMenu.Open();
        }

        private void RebuildOfficeObjectPurchaseMenuItems()
        {
            var definition = _pendingOfficeObjectPurchaseDefinition;
            var items = new List<OfficeMenuItem>();
            if (definition == null)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No office object selected",
                    DetailFactory = () => "Return to the catalog and choose a valid office object.",
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = ReturnToOfficeObjectsMenu,
                });
                _officeObjectPurchaseMenu.Title = "Confirm Purchase";
                _officeObjectPurchaseMenu.Subtitle = "Office object unavailable";
                _officeObjectPurchaseMenu.SetItems(items);
                return;
            }

            string blockedReason;
            var canPurchase = CanPurchaseOfficeObject(definition, out blockedReason);
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => BuildOfficeObjectCaption(definition),
                DetailFactory = () => BuildOfficeObjectDetail(definition),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => canPurchase ? string.Format("Purchase for {0}", ModFormatting.FormatMoney(definition.Price)) : "Purchase blocked",
                DetailFactory = () => canPurchase
                    ? OfficeObjectCatalogFormatter.BuildPurchaseActionDetail(definition)
                    : blockedReason,
                OnActivate = canPurchase
                    ? (Action)ConfirmOfficeObjectPurchase
                    : (Action)(() => ShowStatus(blockedReason)),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = ReturnToOfficeObjectsMenu,
            });

            _officeObjectPurchaseMenu.Title = "Confirm Purchase";
            _officeObjectPurchaseMenu.Subtitle = _menuOffice != null ? _menuOffice.DisplayName : "Office object";
            _officeObjectPurchaseMenu.SetItems(items);
        }

        private bool CanPurchaseOfficeObject(OfficeObjectDefinition definition, out string blockedReason)
        {
            blockedReason = string.Empty;
            if (definition == null || _menuOffice == null)
            {
                blockedReason = "No office object selected.";
                return false;
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                blockedReason = "Activate this office before buying objects for it.";
                return false;
            }

            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                blockedReason = reason;
                return false;
            }

            return _propertyManager.CanPurchaseOfficeObject(_menuOffice.OfficeId, definition, _profit, out blockedReason);
        }

        private void ConfirmOfficeObjectPurchase()
        {
            if (_pendingOfficeObjectPurchaseDefinition == null || _menuOffice == null)
            {
                ReturnToOfficeObjectsMenu();
                return;
            }

            OfficeObjectPersistenceEntry purchasedEntry;
            string message;
            if (!_propertyManager.TryPurchaseOfficeObject(_menuOffice.OfficeId, _pendingOfficeObjectPurchaseDefinition.ObjectId, ref _profit, out purchasedEntry, out message))
            {
                ShowStatus(message);
                RebuildOfficeObjectPurchaseMenuItems();
                return;
            }

            _tabletStateStore.MarkBalanceDirty();
            _pendingOfficeObjectPurchaseDefinition = null;
            StartOfficeObjectPlacement(purchasedEntry, message);
        }

        private void StartOfficeObjectPlacement(OfficeObjectPersistenceEntry entry, string prefixMessage = null)
        {
            if (_menuOffice == null || entry == null)
            {
                return;
            }

            string placementMessage;
            if (!_officeObjectManager.TryStartPlacement(_menuOffice, entry, out placementMessage))
            {
                ShowOfficeObjectPlacementStatus(prefixMessage, placementMessage);
                ReturnToOfficeObjectsMenu();
                return;
            }

            _officeObjectPurchaseMenu.Close();
            _officeObjectsMenu.Close();
            ShowOfficeObjectPlacementStatus(prefixMessage, placementMessage);
        }

        private void ShowOfficeObjectPlacementStatus(string prefixMessage, string placementMessage)
        {
            var statusMessage = CombineStatusMessages(prefixMessage, placementMessage);
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                ShowStatus(statusMessage);
            }
        }

        private static string CombineStatusMessages(string firstMessage, string secondMessage)
        {
            if (string.IsNullOrWhiteSpace(firstMessage))
            {
                return secondMessage ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(secondMessage))
            {
                return firstMessage;
            }

            return string.Format("{0} {1}", firstMessage, secondMessage);
        }

        private void ReturnToOfficeObjectsMenu()
        {
            _officeObjectPurchaseMenu.Close();
            _pendingOfficeObjectPurchaseDefinition = null;
            RebuildOfficeObjectMenuItems();
            _officeObjectsMenu.Open();
        }

        private void ReturnToOfficeMenuFromObjects()
        {
            _officeObjectPurchaseMenu.Close();
            _officeObjectsMenu.Close();
            _pendingOfficeObjectPurchaseDefinition = null;
            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private void ReturnToOfficeMenuFromFuelManagement()
        {
            _officeFuelManagementMenu.Close();
            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private OfficeObjectDefinition GetSelectedOfficeObjectPreviewDefinition()
        {
            if (_officeObjectsMenu == null || !_officeObjectsMenu.IsOpen || _officeObjectPreviewSlots == null || _officeObjectPreviewSlots.Count == 0)
            {
                return null;
            }

            var selectedIndex = _officeObjectsMenu.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < _officeObjectPreviewSlots.Count
                ? _officeObjectPreviewSlots[selectedIndex]
                : null;
        }

        private DealershipVehicleDefinition GetSelectedPersonalDealershipPreviewDefinition()
        {
            return GetSelectedVisiblePersonalDealershipDefinition();
        }

        private void UpdatePersonalDealershipPreview()
        {
            var definition = GetSelectedPersonalDealershipPreviewDefinition();
            if (definition == null)
            {
                ClearPersonalDealershipPreviewVehicle();
                return;
            }

            _personalDealershipSelectedVehicleModelName = definition.ModelName;
            EnsurePersonalDealershipPreviewVehicle(definition);
        }

        private DealershipVehicleDefinition GetSelectedVisiblePersonalDealershipDefinition()
        {
            if (_personalDealershipMenu == null
                || !_personalDealershipMenu.IsOpen
                || string.IsNullOrWhiteSpace(_activePersonalDealershipCategory)
                || _personalDealershipVisibleVehicles == null
                || _personalDealershipVisibleVehicles.Count == 0)
            {
                return null;
            }

            var selectedIndex = _personalDealershipMenu.SelectedIndex - PersonalDealershipVehicleListStartIndex;
            return selectedIndex >= 0 && selectedIndex < _personalDealershipVisibleVehicles.Count
                ? _personalDealershipVisibleVehicles[selectedIndex]
                : null;
        }

        private void RefreshPersonalDealershipCategoryState()
        {
            var selectedCategory = GetSelectedPersonalDealershipCategory();
            _personalDealershipCategories = BuildPersonalDealershipCategories();
            _selectedPersonalDealershipCategoryIndex = _personalDealershipCategories.FindIndex(category => string.Equals(category, selectedCategory, StringComparison.OrdinalIgnoreCase));
            if (_selectedPersonalDealershipCategoryIndex < 0)
            {
                _selectedPersonalDealershipCategoryIndex = 0;
            }

            if (!string.IsNullOrWhiteSpace(_activePersonalDealershipCategory)
                && !_personalDealershipCategories.Any(category => string.Equals(category, _activePersonalDealershipCategory, StringComparison.OrdinalIgnoreCase)))
            {
                _activePersonalDealershipCategory = PersonalDealershipAllCategory;
            }

            if (string.IsNullOrWhiteSpace(_activePersonalDealershipCategory))
            {
                _personalDealershipVisibleVehicles = new List<DealershipVehicleDefinition>();
                return;
            }

            var activeCategory = _activePersonalDealershipCategory;
            var catalog = _propertyManager != null && _propertyManager.PersonalVehicleCatalog != null
                ? _propertyManager.PersonalVehicleCatalog
                : Enumerable.Empty<DealershipVehicleDefinition>();
            _personalDealershipVisibleVehicles = string.Equals(activeCategory, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                ? catalog.ToList()
                : catalog.Where(definition => string.Equals(GetNormalizedPersonalDealershipCategory(definition), activeCategory, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private List<string> BuildPersonalDealershipCategories()
        {
            var categories = new List<string>
            {
                PersonalDealershipAllCategory,
            };

            if (_propertyManager == null || _propertyManager.PersonalVehicleCatalog == null)
            {
                return categories;
            }

            categories.AddRange(
                _propertyManager.PersonalVehicleCatalog
                    .Select(GetNormalizedPersonalDealershipCategory)
                    .Where(category => !string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(category => category, StringComparer.OrdinalIgnoreCase));
            return categories;
        }

        private string GetSelectedPersonalDealershipCategory()
        {
            if (_personalDealershipCategories == null || _personalDealershipCategories.Count == 0)
            {
                return PersonalDealershipAllCategory;
            }

            if (_selectedPersonalDealershipCategoryIndex < 0 || _selectedPersonalDealershipCategoryIndex >= _personalDealershipCategories.Count)
            {
                _selectedPersonalDealershipCategoryIndex = 0;
            }

            return _personalDealershipCategories[_selectedPersonalDealershipCategoryIndex];
        }

        private string GetNormalizedPersonalDealershipCategory(DealershipVehicleDefinition definition)
        {
            return NormalizePersonalDealershipCategory(definition != null ? definition.Category : string.Empty);
        }

        private static string NormalizePersonalDealershipCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category)
                ? PersonalDealershipUncategorizedCategory
                : category.Trim();
        }

        private void EnterPersonalDealershipCategory(string category)
        {
            RefreshPersonalDealershipCategoryState();
            if (_personalDealershipCategories == null || _personalDealershipCategories.Count == 0)
            {
                return;
            }

            var normalizedCategory = string.IsNullOrWhiteSpace(category)
                ? PersonalDealershipAllCategory
                : category.Trim();
            var categoryIndex = _personalDealershipCategories.FindIndex(entry => string.Equals(entry, normalizedCategory, StringComparison.OrdinalIgnoreCase));
            if (categoryIndex < 0)
            {
                categoryIndex = 0;
            }

            _selectedPersonalDealershipCategoryIndex = categoryIndex;
            _activePersonalDealershipCategory = _personalDealershipCategories[_selectedPersonalDealershipCategoryIndex];
            RebuildPersonalDealershipMenuItems();
            SelectPersonalDealershipVehicleRow(_personalDealershipSelectedVehicleModelName);
        }

        private void ReturnToPersonalDealershipCategories()
        {
            _activePersonalDealershipCategory = null;
            ClearPersonalDealershipPreviewVehicle();
            RebuildPersonalDealershipMenuItems();
            SelectPersonalDealershipCategoryRow();
        }

        private void SelectPersonalDealershipCategoryRow()
        {
            if (_personalDealershipMenu == null)
            {
                return;
            }

            _personalDealershipMenu.SelectedIndex = PersonalDealershipCategoryListStartIndex + _selectedPersonalDealershipCategoryIndex;
        }

        private void SelectPersonalDealershipVehicleRow(string preferredModelName)
        {
            if (_personalDealershipMenu == null)
            {
                return;
            }

            var targetIndex = PersonalDealershipVehicleListStartIndex;
            if (_personalDealershipVisibleVehicles != null && _personalDealershipVisibleVehicles.Count > 0)
            {
                var preferredIndex = !string.IsNullOrWhiteSpace(preferredModelName)
                    ? _personalDealershipVisibleVehicles.FindIndex(definition => string.Equals(definition.ModelName, preferredModelName, StringComparison.OrdinalIgnoreCase))
                    : -1;
                if (preferredIndex >= 0)
                {
                    targetIndex = PersonalDealershipVehicleListStartIndex + preferredIndex;
                }
            }

            _personalDealershipMenu.SelectedIndex = targetIndex;
        }

        private void PurchaseSelectedPersonalDealershipVehicle()
        {
            var definition = GetSelectedVisiblePersonalDealershipDefinition();
            if (definition == null)
            {
                ShowStatus("Select a personal vehicle to purchase.");
                return;
            }

            PurchasePersonalVehicle(definition);
        }

        private string BuildPersonalDealershipCategoryButtonCaption(string category)
        {
            return string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                ? "All Vehicles"
                : category;
        }

        private string BuildPersonalDealershipCategoryButtonDetail(string category)
        {
            var vehicleCount = GetPersonalDealershipCategoryVehicleCount(category);
            if (vehicleCount <= 0)
            {
                return string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                    ? "No personal vehicles are currently listed in the dealership catalog."
                    : string.Format("No vehicles are listed in the {0} category.", category);
            }

            return string.Format(
                "Browse {0} vehicle{1} in {2}.",
                vehicleCount,
                vehicleCount == 1 ? string.Empty : "s",
                string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase) ? "the full catalog" : category);
        }

        private int GetPersonalDealershipCategoryVehicleCount(string category)
        {
            if (_propertyManager == null || _propertyManager.PersonalVehicleCatalog == null)
            {
                return 0;
            }

            return string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                ? _propertyManager.PersonalVehicleCatalog.Count
                : _propertyManager.PersonalVehicleCatalog.Count(definition => string.Equals(GetNormalizedPersonalDealershipCategory(definition), category, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsurePersonalDealershipPreviewVehicle(DealershipVehicleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ModelName))
            {
                ClearPersonalDealershipPreviewVehicle();
                return;
            }

            if (_personalDealershipPreviewVehicle != null
                && _personalDealershipPreviewVehicle.Exists()
                && string.Equals(_personalDealershipPreviewModelName, definition.ModelName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ClearPersonalDealershipPreviewVehicle();

            var model = new Model(definition.ModelName);
            if (!model.Request(500))
            {
                model.MarkAsNoLongerNeeded();
                return;
            }

            var vehicle = World.CreateVehicle(model, PersonalDealershipVehiclePadPosition, PersonalDealershipVehiclePadHeading);
            model.MarkAsNoLongerNeeded();
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            vehicle.IsPersistent = false;
            Function.Call(Hash.SET_ENTITY_COLLISION, vehicle.Handle, false, false);
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, vehicle.Handle, true);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle.Handle, true);
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, false, true, true);

            _personalDealershipPreviewVehicle = vehicle;
            _personalDealershipPreviewModelName = definition.ModelName;
        }

        private void ClearPersonalDealershipPreviewVehicle()
        {
            if (_personalDealershipPreviewVehicle != null)
            {
                try
                {
                    if (_personalDealershipPreviewVehicle.Exists())
                    {
                        _personalDealershipPreviewVehicle.Delete();
                    }
                }
                catch
                {
                    // Preview cleanup should be best-effort only.
                }
            }

            _personalDealershipPreviewVehicle = null;
            _personalDealershipPreviewModelName = null;
        }

        private void RefreshOfficeObjectMenus(bool markCargoDirty)
        {
            if (markCargoDirty)
            {
                _tabletStateStore.MarkCargoDirty();
            }

            RebuildOfficeMenuItems();
            if (_officeObjectsMenu != null && _officeObjectsMenu.IsOpen)
            {
                RebuildOfficeObjectMenuItems();
            }
        }

        private void RefuelVehicleFromOfficeTank()
        {
            string message;
            if (_officeObjectManager.TryRefuelPlayerVehicleAtOffice(Game.Player.Character, out message))
            {
                RefreshOfficeObjectMenus(true);
            }

            ShowStatus(message);
        }

        private void UnloadFuelCargoIntoOfficeTank()
        {
            string message;
            if (_officeObjectManager.TryUnloadFuelCargoIntoOfficeTank(Game.Player.Character, out message))
            {
                RefreshOfficeObjectMenus(true);
            }

            ShowStatus(message);
        }

        private void RequestOfficeFuelDelivery()
        {
            string message;
            if (_officeObjectManager.TryRequestOfficeFuelDelivery(Game.GameTime, out message))
            {
                RefreshOfficeObjectMenus(false);
            }

            ShowStatus(message);
        }

        private void RepairVehicleAtOffice()
        {
            string message;
            if (_officeObjectManager.TryRepairPlayerVehicleAtOffice(Game.Player.Character, out message) && _tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            ShowStatus(message);
        }

        private void RentSelectedOffice()
        {
            RentOfficeById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void TransferSelectedOfficeRental()
        {
            TransferOfficeRentalById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void PurchaseSelectedOffice()
        {
            PurchaseOfficeById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void ActivateSelectedOffice()
        {
            ActivateOfficeById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void RelinquishSelectedOfficeRental()
        {
            RelinquishOfficeRentalById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void PaySelectedOfficeArrears()
        {
            PayOfficeArrearsById(_menuOffice != null ? _menuOffice.OfficeId : null);
        }

        private void RentOfficeById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryRentOfficeAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void TransferOfficeRentalById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryTransferOfficeRentalAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.Network | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.CommercialGarage | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void PurchaseOfficeById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryPurchaseOfficeAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void ActivateOfficeById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryActivateOfficeAction,
                PropertyUiRefreshFlags.Network | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.CommercialGarage | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void RelinquishOfficeRentalById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryRelinquishOfficeRentalAction,
                PropertyUiRefreshFlags.Network | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.CommercialGarage | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void PayOfficeArrearsById(string officeId)
        {
            ExecutePropertyActionForId(
                officeId,
                TryPayOfficeArrearsAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.OfficeMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private PropertyActionResult TryRentOfficeAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryRentOffice(officeId, ref _profit, GetCurrentInGameWeekMinute(), out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryTransferOfficeRentalAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryTransferOfficeRental(officeId, ref _profit, GetCurrentInGameWeekMinute(), out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryPurchaseOfficeAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryPurchaseOffice(officeId, ref _profit, GetCurrentInGameWeekMinute(), out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryActivateOfficeAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryActivateOffice(officeId, out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryRelinquishOfficeRentalAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryRelinquishOfficeRental(officeId, out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryPayOfficeArrearsAction(string officeId)
        {
            string message;
            var success = _propertyManager.TryPayOfficeArrears(officeId, ref _profit, out message);
            return new PropertyActionResult(success, message);
        }

        private void OpenCommercialGarageMenu()
        {
            _commercialGarageMenuContext = CommercialGarageMenuContext.Office;
            OpenCommercialGarageMenuInternal();
        }

        private void OpenIndustryCommercialGarageMenu()
        {
            _commercialGarageMenuContext = CommercialGarageMenuContext.Industry;
            OpenCommercialGarageMenuInternal();
        }

        private void OpenCommercialGarageMenuInternal()
        {
            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            _officeMenu.Close();
            CloseIndustryTablet();
            RebuildCommercialGarageMenuItems();
            _commercialGarageMenu.Open();
        }

        private void RebuildCommercialGarageMenuItems()
        {
            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => BuildCommercialGarageSummary(),
                    DetailFactory = () => _commercialGarageMenuContext == CommercialGarageMenuContext.Industry
                        ? "Select an active garage vehicle to deploy at this industry."
                        : "Enter opens vehicle actions for retrieve, storage, reserve, sale, or rental return.",
                }
            };

            var activeVehicles = _propertyManager.GetActiveCommercialGarageVehicles().ToList();
            if (activeVehicles.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No active commercial vehicles",
                    DetailFactory = () => "Purchase trucks and trailers at the trucks dealership.",
                });
            }
            else
            {
                for (int i = 0; i < activeVehicles.Count; i++)
                {
                    var vehicle = activeVehicles[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildCommercialVehicleStatusCaption(vehicle),
                        DetailFactory = () => BuildCommercialVehicleDetail(vehicle),
                        OnActivate = () => HandleCommercialVehicleActivate(vehicle),
                    });
                }
            }

            var reserveVehicles = _propertyManager.GetReserveCommercialVehicles().ToList();
            if (reserveVehicles.Count > 0)
            {
                items.Add(new OfficeMenuItem { IsSeparator = true });
                for (int i = 0; i < reserveVehicles.Count; i++)
                {
                    var vehicle = reserveVehicles[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildCommercialVehicleStatusCaption(vehicle),
                        DetailFactory = () => BuildCommercialVehicleDetail(vehicle),
                        OnActivate = _commercialGarageMenuContext == CommercialGarageMenuContext.Industry
                            ? (Action)(() => ActivateCommercialReserveVehicle(vehicle))
                            : (Action)(() => OpenCommercialGarageActionMenu(vehicle)),
                    });
                }
            }

            _commercialGarageMenu.Title = _commercialGarageMenuContext == CommercialGarageMenuContext.Industry
                ? "Industry Deployment"
                : "Garage";
            _commercialGarageMenu.Subtitle = _commercialGarageMenuContext == CommercialGarageMenuContext.Industry
                ? "Deploy owned commercial vehicles to the selected industry pad"
                : "Retrieve, store, and swap vehicles";
            _commercialGarageMenu.SetItems(items);
        }

        private string BuildCommercialVehicleDetail(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return string.Empty;
            }

            var location = vehicle.InActiveGarage ? "Active garage" : "Reserve";
            var deployed = _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId) ? "Deployed" : "Stored";
            var acquisition = vehicle.IsRental
                ? string.Format("Rent {0}/day", ModFormatting.FormatMoney(vehicle.DailyRent))
                : "Owned";
            var salePreview = !vehicle.IsRental ? _propertyManager.GetCommercialVehicleSalePreview(vehicle) : null;
            var ownershipDetail = salePreview != null
                ? string.Format("Condition {0:0}% | Resale {1}", salePreview.MaintenanceConditionPercent, ModFormatting.FormatMoney(salePreview.EstimatedResaleValue))
                : acquisition;
            var cargo = string.IsNullOrWhiteSpace(vehicle.Commodity)
                ? "Empty"
                : string.Format("{0} {1}", vehicle.Commodity, ModFormatting.FormatRatio(vehicle.WeightTons, Math.Max(0f, vehicle.CapacityTons), "t"));
            var npcAssignment = BuildCommercialVehicleNpcAssignmentDetail(vehicle);
            return string.IsNullOrWhiteSpace(npcAssignment)
                ? string.Format("{0} | {1} | {2} | {3}", location, deployed, ownershipDetail, cargo)
                : string.Format("{0} | {1} | {2} | {3} | {4}", location, deployed, ownershipDetail, npcAssignment, cargo);
        }

        private string BuildCommercialVehicleSellActionDetail(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null || vehicle.IsRental)
            {
                return "Close the rental and stop future daily rent charges.";
            }

            var preview = _propertyManager.GetCommercialVehicleSalePreview(vehicle);
            var conditionPrefix = preview.ConditionAdjustmentAmount >= 0f ? "+" : "-";
            var conditionDelta = ModFormatting.FormatMoney(Math.Abs(preview.ConditionAdjustmentAmount));
            var depreciationLoss = ModFormatting.FormatMoney(Math.Abs(preview.DepreciationLossAmount));
            var depreciationSuffix = preview.DepreciationPenaltyPercent >= 20f ? " cap" : string.Empty;

            return string.Format(
                "Base {0} | Condition {1}{2} ({3:0}%) | Depreciation -{4} ({5:0.0}%{6}) | Final {7}",
                ModFormatting.FormatMoney(preview.BaseRefund),
                conditionPrefix,
                conditionDelta,
                preview.MaintenanceConditionPercent,
                depreciationLoss,
                preview.DepreciationPenaltyPercent,
                depreciationSuffix,
                ModFormatting.FormatMoney(preview.EstimatedResaleValue));
        }

        private string BuildCommercialVehicleStatusCaption(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return string.Empty;
            }

            return string.Format(
                "[{0}{1}{2}] {3}",
                _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId) ? "Out" : "Stored",
                vehicle.IsRental ? "/Rent" : string.Empty,
                IsCommercialVehicleAssignedToNpcContract(vehicle) ? "/NPC" : string.Empty,
                vehicle.DisplayName);
        }

        private NpcLogisticsContract GetCommercialVehicleAssignedNpcContract(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null || _npcLogisticsManager == null || string.IsNullOrWhiteSpace(vehicle.AssetId))
            {
                return null;
            }

            return _npcLogisticsManager.FindContractUsingAssignedVehicle(vehicle.AssetId);
        }

        private bool IsCommercialVehicleAssignedToNpcContract(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            return GetCommercialVehicleAssignedNpcContract(vehicle) != null;
        }

        private string BuildCommercialVehicleNpcAssignmentDetail(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            var contract = GetCommercialVehicleAssignedNpcContract(vehicle);
            if (contract == null)
            {
                return string.Empty;
            }

            var routeCount = contract.Routes != null ? contract.Routes.Count : 0;
            var tierLabel = contract.Tier != null && !string.IsNullOrWhiteSpace(contract.Tier.DisplayName)
                ? contract.Tier.DisplayName + " NPC"
                : "hired NPC";
            return routeCount > 0
                ? string.Format("Assigned to {0} contract #{1} with {2} route{3}", tierLabel, contract.Id, routeCount, routeCount == 1 ? string.Empty : "s")
                : string.Format("Assigned to {0} contract #{1}", tierLabel, contract.Id);
        }

        private bool ShowNpcAssignedCommercialVehicleBlocked(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (!IsCommercialVehicleAssignedToNpcContract(vehicle))
            {
                return false;
            }

            ShowStatus(string.Format("{0} is assigned to a hired NPC and cannot be managed manually.", vehicle.DisplayName));
            return true;
        }

        private void HandleCommercialVehicleActivate(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            string message;
            if (_commercialGarageMenuContext == CommercialGarageMenuContext.Industry)
            {
                if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
                {
                    return;
                }

                if (_menuIndustry == null || !_menuIndustry.VehicleSpawnPosition.HasValue)
                {
                    ShowStatus("No vehicle spawn configured for this industry.");
                    return;
                }

                _propertyManager.TryDeployCommercialVehicle(
                    vehicle.AssetId,
                    _fleetManager,
                    _vehicleFuelSystem,
                    GetGroundPosition,
                    _menuIndustry.VehicleSpawnPosition.Value,
                    _menuIndustry.VehicleSpawnHeading ?? _config.VehicleSpawnHeading,
                    out message);
            }
            else
            {
                OpenCommercialGarageActionMenu(vehicle);
                return;
            }

            _tabletStateStore.MarkCargoDirty();
            RebuildCommercialGarageMenuItems();
            ShowStatus(message);
        }

        private void ActivateCommercialReserveVehicle(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            if (_propertyManager.TrySetCommercialVehicleActive(vehicle.AssetId, out message))
            {
                if (_commercialGarageMenuContext == CommercialGarageMenuContext.Industry)
                {
                    ReevaluatePlayerSuccesses(true);
                    HandleCommercialVehicleActivate(vehicle);
                    return;
                }

                RebuildCommercialGarageMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void MoveCommercialVehicleToReserve(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            _propertyManager.TrySetCommercialVehicleReserve(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, out message);
            _tabletStateStore.MarkCargoDirty();
            if (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
            {
                RebuildCommercialGarageActionMenuItems();
            }
            RebuildCommercialGarageMenuItems();
            ReevaluatePlayerSuccesses(true);
            ShowStatus(message);
        }

        private void ReturnFromCommercialGarageMenu()
        {
            _commercialGarageMenu.Close();
            _selectedCommercialGarageVehicle = null;

            if (_commercialGarageMenuContext == CommercialGarageMenuContext.Industry)
            {
                ReturnToIndustryTablet();
                return;
            }

            RebuildOfficeMenuItems();
            _officeMenu.Open();
        }

        private void OpenCommercialGarageActionMenu(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            _selectedCommercialGarageVehicle = vehicle;
            RebuildCommercialGarageActionMenuItems();
            _commercialGarageMenu.Close();
            _commercialGarageActionMenu.Open();
        }

        private void RebuildCommercialGarageActionMenuItems()
        {
            var vehicle = _selectedCommercialGarageVehicle;
            var items = new List<OfficeMenuItem>();

            if (vehicle == null)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No vehicle selected",
                    DetailFactory = () => "Return to the garage list and choose a valid vehicle.",
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = ReturnToCommercialGarageMenu,
                });
                _commercialGarageActionMenu.Title = "Vehicle";
                _commercialGarageActionMenu.Subtitle = "Vehicle actions";
                _commercialGarageActionMenu.SetItems(items);
                return;
            }

            var isDeployed = _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId);
            var npcAssignmentDetail = BuildCommercialVehicleNpcAssignmentDetail(vehicle);
            var isNpcAssigned = !string.IsNullOrWhiteSpace(npcAssignmentDetail);
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => vehicle.DisplayName,
                DetailFactory = () => BuildCommercialVehicleDetail(vehicle),
            });

            if (isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Assigned to Hired NPC",
                    DetailFactory = () => npcAssignmentDetail,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Manual Control Locked",
                    DetailFactory = () => "Reassign or dismiss the hired NPC contract before retrieving, storing, reserving, selling, or ending this rental.",
                });
            }
            else if (!isDeployed)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Retrieve Vehicle",
                    DetailFactory = () => vehicle.InActiveGarage
                        ? "Deploy this vehicle at the active office spawn."
                        : "Move the vehicle into the active garage and deploy it at the office spawn.",
                    OnActivate = () => RetrieveCommercialVehicleFromGarage(vehicle),
                });
            }
            else
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Put In Storage",
                    DetailFactory = () => "Store the deployed vehicle back in the company garage.",
                    OnActivate = () => StoreCommercialVehicleFromGarage(vehicle),
                });
            }

            if (vehicle.InActiveGarage && !isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Move To Reserve",
                    DetailFactory = () => "Free an active garage slot by sending this vehicle to reserve storage.",
                    OnActivate = () => MoveCommercialVehicleToReserve(vehicle),
                });
            }

            if (!isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => vehicle.IsRental ? "End Rent" : "Sell Vehicle",
                    DetailFactory = () => vehicle.IsRental
                        ? "Close the rental and stop future daily rent charges."
                        : BuildCommercialVehicleSellActionDetail(vehicle),
                    OnActivate = () =>
                    {
                        if (vehicle.IsRental)
                        {
                            EndCommercialVehicleRentalFromGarage(vehicle);
                        }
                        else
                        {
                            SellCommercialVehicleFromGarage(vehicle);
                        }
                    },
                });
            }
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                DetailFactory = () => "Return to the garage list.",
                OnActivate = ReturnToCommercialGarageMenu,
            });

            _commercialGarageActionMenu.Title = "Vehicle";
            _commercialGarageActionMenu.Subtitle = vehicle.IsRental
                ? string.Format("Rental {0}/day", ModFormatting.FormatMoney(vehicle.DailyRent))
                : "Owned company vehicle";
            _commercialGarageActionMenu.SetItems(items);
        }

        private void RetrieveCommercialVehicleFromGarage(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            if (!vehicle.InActiveGarage)
            {
                if (!_propertyManager.TrySetCommercialVehicleActive(vehicle.AssetId, out message))
                {
                    ShowStatus(message);
                    return;
                }

                ReevaluatePlayerSuccesses(true);
            }

            var activeOffice = _propertyManager.ActiveOffice;
            if (activeOffice == null)
            {
                ShowStatus("No active office selected.");
                return;
            }

            _propertyManager.TryDeployCommercialVehicle(
                vehicle.AssetId,
                _fleetManager,
                _vehicleFuelSystem,
                GetGroundPosition,
                activeOffice.SpawnPosition,
                activeOffice.SpawnHeading,
                out message);
            _tabletStateStore.MarkCargoDirty();
            RebuildCommercialGarageActionMenuItems();
            RebuildCommercialGarageMenuItems();
            ShowStatus(message);
        }

        private void StoreCommercialVehicleFromGarage(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            _propertyManager.TryStoreCommercialVehicle(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, out message);
            _tabletStateStore.MarkCargoDirty();
            RebuildCommercialGarageActionMenuItems();
            RebuildCommercialGarageMenuItems();
            ShowStatus(message);
        }

        private void SellCommercialVehicleFromGarage(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            if (_propertyManager.TrySellCommercialVehicle(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, ref _profit, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                _tabletStateStore.MarkCargoDirty();
                _selectedCommercialGarageVehicle = null;
                ReturnToCommercialGarageMenu();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void EndCommercialVehicleRentalFromGarage(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            if (ShowNpcAssignedCommercialVehicleBlocked(vehicle))
            {
                return;
            }

            string message;
            if (_propertyManager.TryEndCommercialVehicleRental(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, ref _profit, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                _tabletStateStore.MarkCargoDirty();
                _selectedCommercialGarageVehicle = null;
                ReturnToCommercialGarageMenu();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void ReturnToCommercialGarageMenu()
        {
            _commercialGarageActionMenu.Close();
            _selectedCommercialGarageVehicle = null;
            RebuildCommercialGarageMenuItems();
            _commercialGarageMenu.Open();
        }

        private void OpenApartmentMenuFor(InteriorDefinition apartment)
        {
            _menuApartment = apartment;
            CloseAllMenus();
            RebuildApartmentMenuItems();
            _apartmentMenu.Open();
        }

        private void OpenActiveApartmentInteriorMenu()
        {
            var apartment = _propertyManager.ActiveApartment;
            if (apartment == null)
            {
                return;
            }

            CloseAllMenus();
            RebuildApartmentInteriorMenuItems(apartment);
            _apartmentInteriorMenu.Open();
        }

        private void OpenMotelMenuFor(MotelDefinition motel)
        {
            _menuMotel = motel;
            CloseAllMenus();
            RebuildMotelMenuItems();
            _motelMenu.Open();
        }

        private void RebuildApartmentMenuItems()
        {
            var items = new List<OfficeMenuItem>();
            var apartment = _menuApartment;
            var apartmentState = apartment != null ? _propertyManager.GetApartmentState(apartment.InteriorId) : null;
            var transferSourceState = apartment != null
                ? _propertyManager.Interiors
                    .Where(candidate => candidate != null && !string.Equals(candidate.InteriorId, apartment.InteriorId, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => _propertyManager.GetApartmentState(candidate.InteriorId))
                    .FirstOrDefault(state => state != null && state.IsRented && !state.IsOwned)
                : null;
            var isActiveApartment = apartment != null && string.Equals(_propertyManager.ActiveApartmentId, apartment.InteriorId, StringComparison.OrdinalIgnoreCase);
            var isOwned = apartmentState != null && apartmentState.IsOwned;
            var isRentalOnly = apartmentState != null && apartmentState.IsRented && !apartmentState.IsOwned;
            var hasAccess = apartment != null && apartmentState != null && (apartmentState.IsOwned || apartmentState.IsRented);
            var hasArrears = apartmentState != null && apartmentState.OutstandingRent > 0.01f;

            _apartmentMenu.Title = apartment != null ? apartment.DisplayName : "Apartment";
            _apartmentMenu.Subtitle = apartment != null
                ? string.Format("Weekly rent {0} | Purchase {1}", ModFormatting.FormatMoney(apartment.InteriorWeeklyRent), ModFormatting.FormatMoney(apartment.InteriorPrice))
                : "Manage residence access and personal storage";

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_profit)),
                DetailFactory = () => apartment == null
                    ? string.Empty
                    : string.Format(
                        "{0} | {1}",
                        apartment.InteriorIgName ?? apartment.InteriorType ?? string.Empty,
                        isOwned ? "Owned" : isRentalOnly ? "Rented" : "Available"),
            });

            if (apartment == null)
            {
                _apartmentMenu.SetItems(items);
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => apartment.DisplayName,
                DetailFactory = () => string.Format("Weekly rent {0} | Purchase {1}", ModFormatting.FormatMoney(apartment.InteriorWeeklyRent), ModFormatting.FormatMoney(apartment.InteriorPrice)),
            });

            if (!isOwned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Purchase Apartment",
                    DetailFactory = () => isRentalOnly
                        ? string.Format("Pay {0} to convert this apartment to owned access.", ModFormatting.FormatMoney(apartment.InteriorPrice))
                        : string.Format("Pay {0} to purchase this apartment.", ModFormatting.FormatMoney(apartment.InteriorPrice)),
                    OnActivate = PurchaseSelectedApartment,
                });
            }

            if (!hasAccess)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Rent Apartment",
                    DetailFactory = () => transferSourceState != null
                        ? string.Format("Pay {0} to move your current rental to this apartment.", ModFormatting.FormatMoney(apartment.InteriorWeeklyRent))
                        : string.Format("Pay {0} to rent this apartment.", ModFormatting.FormatMoney(apartment.InteriorWeeklyRent)),
                    OnActivate = RentSelectedApartment,
                });
            }

            if (hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Settle Apartment Arrears",
                    DetailFactory = () => string.Format("Outstanding balance: {0}", ModFormatting.FormatMoney(apartmentState.OutstandingRent)),
                    OnActivate = PaySelectedApartmentArrears,
                });
            }

            if (!hasAccess)
            {
                _apartmentMenu.SetItems(items);
                return;
            }

            if (isRentalOnly)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Cancel Rent",
                    DetailFactory = () => isActiveApartment
                        ? "Stop renting this apartment and switch to another available residence if one exists."
                        : "Stop renting this apartment and end future weekly rent charges.",
                    OnActivate = CancelSelectedApartmentRental,
                });
            }

            if (isOwned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Sell Apartment",
                    DetailFactory = () => "Sell this apartment and remove ownership access.",
                    OnActivate = SellSelectedApartment,
                });
            }

            if (!isActiveApartment && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Activate Apartment",
                    DetailFactory = () => "Make this the active personal residence and garage.",
                    OnActivate = ActivateSelectedApartment,
                });
            }

            if (isActiveApartment && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Enter Apartment",
                    DetailFactory = () => string.Format("Enter {0}.", apartment.InteriorIgName),
                    OnActivate = EnterSelectedApartment,
                });
            }

            _apartmentMenu.SetItems(items);
        }

        private void RebuildApartmentInteriorMenuItems(InteriorDefinition apartment)
        {
            var items = new List<OfficeMenuItem>();

            _apartmentInteriorMenu.Title = apartment != null ? apartment.DisplayName : "Apartment";
            _apartmentInteriorMenu.Subtitle = apartment != null
                ? string.Format("{0} | Rest or step outside", apartment.InteriorIgName ?? apartment.InteriorType ?? apartment.DisplayName)
                : "Sleep or leave the active residence";

            if (apartment == null)
            {
                _apartmentInteriorMenu.SetItems(items);
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Sleep ({0} Hours)", ApartmentSleepHours),
                DetailFactory = BuildApartmentSleepMenuDetail,
                OnActivate = SleepInActiveApartment,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Exit Apartment",
                DetailFactory = () => string.Format("Return to the exterior marker for {0}.", apartment.DisplayName),
                OnActivate = ExitActiveApartment,
            });

            _apartmentInteriorMenu.SetItems(items);
        }

        private void RebuildMotelMenuItems()
        {
            var items = new List<OfficeMenuItem>();
            var motel = _menuMotel;

            _motelMenu.Title = motel != null ? motel.DisplayName : "Motel";
            _motelMenu.Subtitle = motel != null
                ? string.Format("{0} | Rest {1}", motel.MotelType ?? "Motel", ModFormatting.FormatMoney(motel.RestPrice))
                : "Pay for a room and rest for the night";

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_profit)),
                DetailFactory = () => motel == null
                    ? string.Empty
                    : string.Format(
                        "{0} | {1}",
                        string.IsNullOrWhiteSpace(motel.MotelIgName) ? motel.DisplayName : motel.MotelIgName,
                        string.IsNullOrWhiteSpace(motel.MotelType) ? "Motel" : motel.MotelType),
            });

            if (motel == null)
            {
                _motelMenu.SetItems(items);
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => motel.DisplayName,
                DetailFactory = () => string.IsNullOrWhiteSpace(motel.MotelIgName)
                    ? string.Format("Type: {0}", string.IsNullOrWhiteSpace(motel.MotelType) ? "Motel" : motel.MotelType)
                    : string.Format("{0} | {1}", motel.MotelIgName, string.IsNullOrWhiteSpace(motel.MotelType) ? "Motel" : motel.MotelType),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Nightly Room Price: {0}", ModFormatting.FormatMoney(motel.RestPrice)),
                DetailFactory = () => "Configured price to buy one room for the night.",
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Buy Room For Night ({0} Hours)", ApartmentSleepHours),
                DetailFactory = BuildMotelRestMenuDetail,
                OnActivate = RestAtSelectedMotel,
            });

            _motelMenu.SetItems(items);
        }

        private void PurchaseSelectedApartment()
        {
            PurchaseApartmentById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void RentSelectedApartment()
        {
            RentApartmentById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void ActivateSelectedApartment()
        {
            ActivateApartmentById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void CancelSelectedApartmentRental()
        {
            CancelApartmentRentalById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void SellSelectedApartment()
        {
            SellApartmentById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void PaySelectedApartmentArrears()
        {
            PayApartmentArrearsById(_menuApartment != null ? _menuApartment.InteriorId : null);
        }

        private void PurchaseApartmentById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TryPurchaseApartmentAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void RentApartmentById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TryRentApartmentAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void ActivateApartmentById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TryActivateApartmentAction,
                PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void CancelApartmentRentalById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TryCancelApartmentRentalAction,
                PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void SellApartmentById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TrySellApartmentAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private void PayApartmentArrearsById(string interiorId)
        {
            ExecutePropertyActionForId(
                interiorId,
                TryPayApartmentArrearsAction,
                PropertyUiRefreshFlags.Balance | PropertyUiRefreshFlags.ApartmentMenu | PropertyUiRefreshFlags.PlayerSuccesses);
        }

        private PropertyActionResult TryPurchaseApartmentAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TryPurchaseApartment(interiorId, ref _profit, GetCurrentInGameWeekMinute(), out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryRentApartmentAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TryRentApartment(interiorId, ref _profit, GetCurrentInGameWeekMinute(), out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryActivateApartmentAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TryActivateApartment(interiorId, out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryCancelApartmentRentalAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TryCancelApartmentRental(interiorId, out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TrySellApartmentAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TrySellApartment(interiorId, ref _profit, out message);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryPayApartmentArrearsAction(string interiorId)
        {
            string message;
            var success = _propertyManager.TryPayApartmentArrears(interiorId, ref _profit, out message);
            return new PropertyActionResult(success, message);
        }

        private void EnterSelectedApartment()
        {
            if (_menuApartment == null)
            {
                return;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            string reason;
            if (!_propertyManager.CanUseApartmentSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            player.Position = _menuApartment.InteriorPosition;
            CloseAllMenus();
        }

        private void ExitActiveApartment()
        {
            var apartment = _propertyManager.ActiveApartment;
            var player = Game.Player.Character;
            if (apartment == null || player == null || !player.Exists())
            {
                return;
            }

            player.Position = apartment.ExteriorPosition;
            CloseAllMenus();
        }

        private void SleepInActiveApartment()
        {
            var apartment = _propertyManager.ActiveApartment;
            var player = Game.Player.Character;
            if (apartment == null || player == null || !player.Exists())
            {
                return;
            }

            string reason;
            if (!_propertyManager.CanUseApartmentSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            string restMessage;
            if (!TryValidateSharedRest(player, out restMessage))
            {
                if (!string.IsNullOrWhiteSpace(restMessage))
                {
                    ShowStatus(restMessage);
                }

                return;
            }

            StartApartmentSleepTransition(player, Game.GameTime);
        }

        private string BuildApartmentSleepMenuDetail()
        {
            return BuildSharedRestMenuDetail(string.Format("Advance the city clock by {0} hours and stay inside.", ApartmentSleepHours));
        }

        private string BuildMotelRestMenuDetail()
        {
            var motel = _menuMotel;
            var price = motel != null ? motel.RestPrice : 0f;
            return BuildSharedRestMenuDetail(string.Format("Pay {0} to buy a room for the night and rest for {1} hours.", ModFormatting.FormatMoney(price), ApartmentSleepHours));
        }

        private string BuildSharedRestMenuDetail(string availableMessage)
        {
            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                return string.Format("Available again in {0}.", FormatApartmentSleepCooldown(remainingCooldownMinutes));
            }

            return availableMessage;
        }

        private void RestAtSelectedMotel()
        {
            RestAtMotelInternal(_menuMotel);
        }

        private void RestAtMotelById(string motelId)
        {
            if (string.IsNullOrWhiteSpace(motelId) || _propertyManager == null)
            {
                return;
            }

            var motel = _propertyManager.Motels.FirstOrDefault(entry => entry != null && string.Equals(entry.MotelId, motelId, StringComparison.OrdinalIgnoreCase));
            if (motel == null)
            {
                ShowStatus("Motel definition unavailable.");
                return;
            }

            RestAtMotelInternal(motel);
        }

        private void RestAtMotelInternal(MotelDefinition motel)
        {
            var player = Game.Player.Character;
            if (motel == null || player == null || !player.Exists())
            {
                return;
            }

            if (_profit + 0.001f < motel.RestPrice)
            {
                ShowStatus(string.Format("You need {0} to rest at {1}.", ModFormatting.FormatMoney(motel.RestPrice), motel.DisplayName));
                return;
            }

            string restMessage;
            if (!TryValidateSharedRest(player, out restMessage))
            {
                if (!string.IsNullOrWhiteSpace(restMessage))
                {
                    ShowStatus(restMessage);
                }

                return;
            }

            _profit -= motel.RestPrice;
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }

            StartApartmentSleepTransition(player, Game.GameTime);
        }

        private bool TryValidateSharedRest(Ped player, out string message)
        {
            message = string.Empty;
            if (IsApartmentSleepTransitionActive)
            {
                return false;
            }

            if (player == null || !player.Exists())
            {
                message = "Player unavailable.";
                return false;
            }

            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                message = string.Format("You can sleep again in {0}.", FormatApartmentSleepCooldown(remainingCooldownMinutes));
                return false;
            }

            return true;
        }

        private void ExecutePropertyActionForId(
            string propertyId,
            Func<string, PropertyActionResult> action,
            PropertyUiRefreshFlags refreshFlags)
        {
            if (string.IsNullOrWhiteSpace(propertyId) || action == null)
            {
                return;
            }

            var result = action(propertyId);
            if (result.Succeeded)
            {
                ApplyPropertyUiRefresh(refreshFlags);
            }

            ShowStatus(result.Message);
        }

        private void ApplyPropertyUiRefresh(PropertyUiRefreshFlags refreshFlags)
        {
            if ((refreshFlags & PropertyUiRefreshFlags.Balance) != 0 && _tabletStateStore != null)
            {
                _tabletStateStore.MarkBalanceDirty();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.Network) != 0 && _tabletStateStore != null)
            {
                _tabletStateStore.MarkNetworkDirty();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.AllTablet) != 0 && _tabletStateStore != null)
            {
                _tabletStateStore.MarkAllDirty();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.OfficeMenu) != 0)
            {
                RebuildOfficeMenuItems();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.ApartmentMenu) != 0)
            {
                RebuildApartmentMenuItems();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.CommercialGarage) != 0)
            {
                RebuildCommercialGarageMenuItems();
            }

            if ((refreshFlags & PropertyUiRefreshFlags.PlayerSuccesses) != 0)
            {
                ReevaluatePlayerSuccesses(true);
            }
        }

        private int GetApartmentSleepCooldownRemainingMinutes(int currentInGameMinute)
        {
            var lastSuccessfulSleepMinute = _propertyManager != null ? _propertyManager.LastSuccessfulApartmentSleepMinute : -1;
            if (lastSuccessfulSleepMinute < 0)
            {
                return 0;
            }

            var elapsedMinutes = currentInGameMinute - lastSuccessfulSleepMinute;
            if (elapsedMinutes < 0)
            {
                return 0;
            }

            return Math.Max(0, ApartmentSleepCooldownMinutes - elapsedMinutes);
        }

        private static string FormatApartmentSleepCooldown(int remainingMinutes)
        {
            var hours = Math.Max(0, remainingMinutes) / 60;
            var minutes = Math.Max(0, remainingMinutes) % 60;
            if (hours <= 0)
            {
                return string.Format("{0}m", minutes);
            }

            return string.Format("{0}h {1:D2}m", hours, minutes);
        }

        private void StartApartmentSleepTransition(Ped player, int gameTime)
        {
            CloseAllMenus();
            _apartmentSleepTransitionPhase = ApartmentSleepTransitionPhase.FadingOut;
            _apartmentSleepTransitionPhaseStartedAt = gameTime;
            _apartmentSleepClockApplied = false;

            if (player != null && player.Exists())
            {
                Function.Call(Hash.FREEZE_ENTITY_POSITION, player.Handle, true);
            }

            Function.Call(Hash.DO_SCREEN_FADE_OUT, ApartmentSleepFadeDurationMs);
        }

        private void UpdateApartmentSleepTransition(Ped player, int gameTime)
        {
            if (!IsApartmentSleepTransitionActive)
            {
                return;
            }

            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
            if (player != null && player.Exists())
            {
                Function.Call(Hash.FREEZE_ENTITY_POSITION, player.Handle, true);
            }

            switch (_apartmentSleepTransitionPhase)
            {
                case ApartmentSleepTransitionPhase.FadingOut:
                    if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) || gameTime - _apartmentSleepTransitionPhaseStartedAt >= ApartmentSleepFadeDurationMs + ApartmentSleepFadeSafetyBufferMs)
                    {
                        ApplyApartmentSleepTimeAdvance();
                        _apartmentSleepTransitionPhase = ApartmentSleepTransitionPhase.HoldingBlack;
                        _apartmentSleepTransitionPhaseStartedAt = gameTime;
                    }

                    break;

                case ApartmentSleepTransitionPhase.HoldingBlack:
                    if (gameTime - _apartmentSleepTransitionPhaseStartedAt >= ApartmentSleepBlackoutDurationMs)
                    {
                        Function.Call(Hash.DO_SCREEN_FADE_IN, ApartmentSleepFadeDurationMs);
                        _apartmentSleepTransitionPhase = ApartmentSleepTransitionPhase.FadingIn;
                        _apartmentSleepTransitionPhaseStartedAt = gameTime;
                    }

                    break;

                case ApartmentSleepTransitionPhase.FadingIn:
                    if (Function.Call<bool>(Hash.IS_SCREEN_FADED_IN) || gameTime - _apartmentSleepTransitionPhaseStartedAt >= ApartmentSleepFadeDurationMs + ApartmentSleepFadeSafetyBufferMs)
                    {
                        CompleteApartmentSleepTransition(player);
                    }

                    break;
            }
        }

        private void ApplyApartmentSleepTimeAdvance()
        {
            if (_apartmentSleepClockApplied)
            {
                return;
            }

            AdvanceWorldClockHours(ApartmentSleepHours);
            if (_propertyManager != null)
            {
                _propertyManager.RecordSuccessfulApartmentSleep(GetCurrentInGameWeekMinute());
            }

            _tabletStateStore.MarkAllDirty();
            _apartmentSleepClockApplied = true;
        }

        private void CompleteApartmentSleepTransition(Ped player)
        {
            if (player != null && player.Exists())
            {
                Function.Call(Hash.FREEZE_ENTITY_POSITION, player.Handle, false);
            }

            _apartmentSleepTransitionPhase = ApartmentSleepTransitionPhase.None;
            _apartmentSleepTransitionPhaseStartedAt = 0;
            _apartmentSleepClockApplied = false;
        }

        private void CancelApartmentSleepTransition()
        {
            var player = Game.Player.Character;
            CompleteApartmentSleepTransition(player);

            if (!Function.Call<bool>(Hash.IS_SCREEN_FADED_IN))
            {
                Function.Call(Hash.DO_SCREEN_FADE_IN, 0);
            }
        }

        private bool IsApartmentSleepTransitionActive
        {
            get { return _apartmentSleepTransitionPhase != ApartmentSleepTransitionPhase.None; }
        }

        private static void AdvanceWorldClockHours(int hours)
        {
            if (hours == 0)
            {
                return;
            }

            var year = Math.Max(2000, Function.Call<int>(Hash.GET_CLOCK_YEAR));
            var month = Math.Max(1, Math.Min(12, Function.Call<int>(Hash.GET_CLOCK_MONTH) + 1));
            var day = Math.Max(1, Function.Call<int>(Hash.GET_CLOCK_DAY_OF_MONTH));
            var clockHours = Math.Max(0, Function.Call<int>(Hash.GET_CLOCK_HOURS)) % 24;
            var minutes = Math.Max(0, Function.Call<int>(Hash.GET_CLOCK_MINUTES)) % 60;
            var seconds = Math.Max(0, Function.Call<int>(Hash.GET_CLOCK_SECONDS)) % 60;
            var clampedDay = Math.Min(day, DateTime.DaysInMonth(year, month));
            var currentClock = new DateTime(year, month, clampedDay, clockHours, minutes, seconds, DateTimeKind.Unspecified);
            var advancedClock = currentClock.AddHours(hours);

            Function.Call(Hash.SET_CLOCK_DATE, advancedClock.Day, advancedClock.Month - 1, advancedClock.Year);
            Function.Call(Hash.SET_CLOCK_TIME, advancedClock.Hour, advancedClock.Minute, advancedClock.Second);
        }

        private string BuildPersonalGarageSummary()
        {
            var count = _propertyManager.GetOwnedPersonalVehicles().Count();
            return count == 1
                ? "1 owned personal vehicle"
                : string.Format("{0} owned personal vehicles", count);
        }

        private void OpenPersonalGarageMenu()
        {
            string reason;
            if (!_propertyManager.CanUseApartmentSystems(out reason))
            {
                ShowStatus(reason);
                return;
            }

            CloseAllMenus();
            RebuildPersonalGarageMenuItems();
            _personalGarageMenu.Open();
        }

        private void RebuildPersonalGarageMenuItems()
        {
            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = BuildPersonalGarageSummary,
                    DetailFactory = () => "Enter retrieves or stores the selected personal vehicle.",
                }
            };

            var vehicles = _propertyManager.GetOwnedPersonalVehicles().ToList();
            if (vehicles.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No personal vehicles owned",
                    DetailFactory = () => "Purchase personal vehicles at the dealership in Downtown Los Santos.",
                });
            }
            else
            {
                for (int i = 0; i < vehicles.Count; i++)
                {
                    var vehicle = vehicles[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => vehicle.DisplayName,
                        DetailFactory = () => BuildPersonalVehicleDetail(vehicle),
                        OnActivate = () => TogglePersonalVehicle(vehicle),
                    });
                }
            }

            _personalGarageMenu.SetItems(items);
        }

        private string BuildPersonalVehicleDetail(OwnedPersonalVehiclePersistenceEntry vehicle)
        {
            return string.Format(
                "{0} | {1}",
                _propertyManager.IsPersonalVehicleDeployed(vehicle.AssetId) ? "Deployed" : "Stored",
                string.IsNullOrWhiteSpace(vehicle.Category) ? "Residence vehicle" : vehicle.Category);
        }

        private void TogglePersonalVehicle(OwnedPersonalVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            var apartment = _propertyManager.ActiveApartment;
            if (apartment == null)
            {
                ShowStatus("No active apartment selected.");
                return;
            }

            string message;
            bool success;
            if (_propertyManager.IsPersonalVehicleDeployed(vehicle.AssetId))
            {
                success = _propertyManager.TryStorePersonalVehicle(vehicle.AssetId, out message);
            }
            else
            {
                success = _propertyManager.TryDeployPersonalVehicle(vehicle.AssetId, apartment.GaragePosition, 0f, out message);
            }

            RebuildPersonalGarageMenuItems();
            if (success)
            {
                RefreshPersonalVehicleBlips();
            }
            ShowStatus(message);
        }

        private void OpenPersonalDealershipMenu()
        {
            CloseAllMenus();
            _selectedPersonalDealershipCategoryIndex = 0;
            _activePersonalDealershipCategory = null;
            _personalDealershipSelectedVehicleModelName = null;
            ClearPersonalDealershipPreviewVehicle();
            RebuildPersonalDealershipMenuItems();
            SelectPersonalDealershipCategoryRow();
            _personalDealershipMenu.Open();
        }

        private void RebuildPersonalDealershipMenuItems()
        {
            RefreshPersonalDealershipCategoryState();

            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_profit)),
                    DetailFactory = () => string.IsNullOrWhiteSpace(_propertyManager.ActiveApartmentId)
                        ? "Purchase and activate an apartment before storing personal vehicles."
                        : string.Format("Active apartment: {0}", _propertyManager.ActiveApartment != null ? _propertyManager.ActiveApartment.DisplayName : "n/a"),
                }
            };

            if (string.IsNullOrWhiteSpace(_activePersonalDealershipCategory))
            {
                foreach (var category in _personalDealershipCategories)
                {
                    var categoryName = category;
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildPersonalDealershipCategoryButtonCaption(categoryName),
                        DetailFactory = () => BuildPersonalDealershipCategoryButtonDetail(categoryName),
                        OnActivate = () => EnterPersonalDealershipCategory(categoryName),
                    });
                }
            }
            else
            {
                if (_personalDealershipVisibleVehicles.Count == 0)
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => _propertyManager.PersonalVehicleCatalog.Count == 0
                            ? "No dealership catalog loaded"
                            : string.Format("No vehicles in {0}", BuildPersonalDealershipCategoryButtonCaption(_activePersonalDealershipCategory)),
                        DetailFactory = () => _propertyManager.PersonalVehicleCatalog.Count == 0
                            ? "dealership.xml is missing or invalid."
                            : "Press Backspace to return to categories and choose another section of the catalog.",
                    });
                }
                else
                {
                    for (int i = 0; i < _personalDealershipVisibleVehicles.Count; i++)
                    {
                        var definition = _personalDealershipVisibleVehicles[i];
                        items.Add(new OfficeMenuItem
                        {
                            CaptionFactory = () => string.Format("{0} - {1}", definition.DisplayName, ModFormatting.FormatMoney(definition.Price)),
                            DetailFactory = () => GetNormalizedPersonalDealershipCategory(definition),
                            OnActivate = PurchaseSelectedPersonalDealershipVehicle,
                        });
                    }
                }
            }

            _personalDealershipMenu.SetItems(items);
        }

        private void PurchasePersonalVehicle(DealershipVehicleDefinition definition)
        {
            string purchaseMessage;
            OwnedPersonalVehiclePersistenceEntry purchasedVehicle;
            if (!_propertyManager.TryPurchasePersonalVehicle(definition, ref _profit, out purchasedVehicle, out purchaseMessage))
            {
                ShowStatus(purchaseMessage);
                return;
            }

            ClearPersonalDealershipPreviewVehicle();

            string deployMessage = string.Empty;
            var deployed = purchasedVehicle != null
                && _propertyManager.TryDeployPersonalVehicle(
                    purchasedVehicle.AssetId,
                    PersonalDealershipVehiclePadPosition,
                    PersonalDealershipVehiclePadHeading,
                    out deployMessage);
            if (purchasedVehicle == null)
            {
                deployMessage = "Purchased vehicle record not found for retrieval.";
            }

            _tabletStateStore.MarkBalanceDirty();
            RebuildPersonalDealershipMenuItems();
            if (deployed)
            {
                RefreshPersonalVehicleBlips();
                _personalDealershipMenu.Close();
            }

            ShowStatus(CombineStatusMessages(purchaseMessage, deployMessage));
        }

        private void OpenCommercialDealershipMenu()
        {
            CloseAllMenus();
            OpenVehicleCargoMenu(VehicleCargoMenuContext.CommercialDealership);
        }

        private bool IsCommercialDealershipRentMode
        {
            get { return _commercialDealershipAcquisitionMode == CommercialDealershipAcquisitionMode.Rent; }
        }

        private string CurrentCommercialDealershipAcquisitionCaption()
        {
            return string.Format("Acquisition: < {0} >", IsCommercialDealershipRentMode ? "Rent" : "Purchase");
        }

        private void ChangeCommercialDealershipAcquisitionMode(int delta)
        {
            if (delta == 0)
            {
                delta = 1;
            }

            _commercialDealershipAcquisitionMode = _commercialDealershipAcquisitionMode == CommercialDealershipAcquisitionMode.Purchase
                ? CommercialDealershipAcquisitionMode.Rent
                : CommercialDealershipAcquisitionMode.Purchase;
            RebuildVehicleCargoMenuItems();
        }

        private string BuildCommercialDealershipAcquisitionModeDetail()
        {
            var dailyRent = GetSelectedCommercialVehicleDailyRent();
            if (!IsCommercialDealershipRentMode)
            {
                return "Left/right switches to rental pricing. Purchase adds the vehicle permanently to the company garage.";
            }

            if (dailyRent <= 0.001f)
            {
                return BuildCommercialDealershipRentUnavailableDetail();
            }

            return string.Format(
                "Rent charges {0}/day with no upfront cost. The first daily charge is billed after time advances into a later in-game day.",
                ModFormatting.FormatMoney(dailyRent));
        }

        private float GetSelectedCommercialVehicleDailyRent()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            var selectedTractor = GetSelectedCommercialDealershipTruckDefinition(selectedVehicle);
            return Math.Max(0f, selectedVehicle != null ? selectedVehicle.DailyRent : 0f)
                + Math.Max(0f, selectedTractor != null ? selectedTractor.DailyRent : 0f);
        }

        private string CurrentVehicleSpawnerActionCaption()
        {
            return _vehicleCargoMenuContext == VehicleCargoMenuContext.CommercialDealership
                ? (IsCommercialDealershipRentMode ? "~b~Rent Vehicle~s~" : "~b~Purchase Vehicle~s~")
                : "~b~Spawn Vehicle~s~";
        }

        private string BuildCommercialDealershipVehicleSelectionDetail()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            if (selectedVehicle == null)
            {
                var selectedTruck = _vehicleSpawnController.SelectedTractorDefinition;
                if (selectedTruck == null)
                {
                    return "No cargo vehicle or trailer selected. Set Cargo / Trailer to None for truck-only purchases, or change the cargo filter.";
                }

                var truckPrice = ModFormatting.FormatMoney(Math.Max(0f, selectedTruck.Price));
                var truckOnlyDailyRent = GetSelectedCommercialVehicleDailyRent();
                return truckOnlyDailyRent > 0.001f
                    ? string.Format("Truck only. Price {0} | Rent {1}/day.", truckPrice, ModFormatting.FormatMoney(truckOnlyDailyRent))
                    : string.Format("Truck only. Price {0}.", truckPrice);
            }

            var vehicleSpecs = BuildCommercialDealershipVehicleSpecs(selectedVehicle);
            var dailyRent = GetSelectedCommercialVehicleDailyRent();
            if (!selectedVehicle.IsTrailer)
            {
                return dailyRent > 0.001f
                    ? string.Format("{0} | Rent {1}/day.", vehicleSpecs, ModFormatting.FormatMoney(dailyRent))
                    : string.Format("{0}.", vehicleSpecs);
            }

            var selectedTractor = _vehicleSpawnController.SelectedTractorDefinition;
            if (selectedTractor == null)
            {
                return dailyRent > 0.001f
                    ? string.Format("Trailer only. {0} | Rent {1}/day. Set Truck to None to keep it standalone.", vehicleSpecs, ModFormatting.FormatMoney(dailyRent))
                    : string.Format("Trailer only. {0}. Set Truck to None to keep it standalone.", vehicleSpecs);
            }

            var totalPrice = Math.Max(0f, selectedVehicle.Price) + Math.Max(0f, selectedTractor.Price);
            return dailyRent > 0.001f
                ? string.Format(
                    "{0} | Total with truck {1} | Rent {2}/day.",
                    vehicleSpecs,
                    ModFormatting.FormatMoney(totalPrice),
                    ModFormatting.FormatMoney(dailyRent))
                : string.Format(
                    "{0} | Total with truck {1}.",
                    vehicleSpecs,
                    ModFormatting.FormatMoney(totalPrice));
        }

        private static string BuildCommercialDealershipVehicleSpecs(VehicleDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.Format(
                "Price {0} | Capacity {1} | Fuel {2} | Rent {3}/day",
                ModFormatting.FormatMoney(Math.Max(0f, definition.Price)),
                ModFormatting.FormatTons(Math.Max(0f, definition.CapacityTons)),
                ModFormatting.FormatLiters(Math.Max(0f, definition.FuelCapacityLiters)),
                ModFormatting.FormatMoney(Math.Max(0f, definition.DailyRent)));
        }

        private string BuildCommercialDealershipRentUnavailableDetail()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            var selectedTractor = GetSelectedCommercialDealershipTruckDefinition(selectedVehicle);
            if (selectedVehicle == null && selectedTractor == null)
            {
                return "Select a truck and/or trailer first.";
            }

            var parts = new List<string>();
            if (selectedVehicle != null)
            {
                parts.Add(string.Format(
                    "{0} {1}/day",
                    string.IsNullOrWhiteSpace(selectedVehicle.DisplayName) ? selectedVehicle.ModelName : selectedVehicle.DisplayName,
                    ModFormatting.FormatMoney(Math.Max(0f, selectedVehicle.DailyRent))));
            }

            if (selectedTractor != null && (selectedVehicle == null || !string.Equals(selectedTractor.ModelName, selectedVehicle.ModelName, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add(string.Format(
                    "{0} {1}/day",
                    string.IsNullOrWhiteSpace(selectedTractor.DisplayName) ? selectedTractor.ModelName : selectedTractor.DisplayName,
                    ModFormatting.FormatMoney(Math.Max(0f, selectedTractor.DailyRent))));
            }

            return string.Format(
                "Rental requires a positive daily rent. Current selection: {0}. Vehicles with dailyRent set to 0 are treated as not rentable.",
                string.Join(" | ", parts));
        }

        private string BuildCommercialDealershipTruckSelectionDetail()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            var selectedTractor = _vehicleSpawnController.SelectedTractorDefinition;
            if (selectedVehicle == null)
            {
                if (selectedTractor == null)
                {
                    return "Select a truck for a truck-only purchase, or pair one with a trailer.";
                }

                var tractorPrice = Math.Max(0f, selectedTractor.Price);
                var truckOnlyDailyRent = GetSelectedCommercialVehicleDailyRent();
                return truckOnlyDailyRent > 0.001f
                    ? string.Format("Truck only. Price {0} | Rent {1}/day.", ModFormatting.FormatMoney(tractorPrice), ModFormatting.FormatMoney(truckOnlyDailyRent))
                    : string.Format("Truck only. Price {0}.", ModFormatting.FormatMoney(tractorPrice));
            }

            if (!selectedVehicle.IsTrailer)
            {
                return "No separate truck tractor is needed for the selected vehicle. Set Cargo / Trailer to None to buy a truck alone.";
            }

            if (selectedTractor == null)
            {
                return "Truck is set to None. The selected trailer will be purchased or rented on its own.";
            }

            var selectedTractorPrice = Math.Max(0f, selectedTractor.Price);
            var totalPrice = Math.Max(0f, selectedVehicle.Price) + selectedTractorPrice;
            var totalDailyRent = GetSelectedCommercialVehicleDailyRent();
            return totalDailyRent > 0.001f
                ? string.Format(
                    "Truck price {0} | Total purchase {1} | Total rent {2}/day.",
                    ModFormatting.FormatMoney(selectedTractorPrice),
                    ModFormatting.FormatMoney(totalPrice),
                    ModFormatting.FormatMoney(totalDailyRent))
                : string.Format(
                    "Truck price {0} | Total purchase {1}.",
                    ModFormatting.FormatMoney(selectedTractorPrice),
                    ModFormatting.FormatMoney(totalPrice));
        }

        private string BuildCommercialDealershipPurchaseDetail()
        {
            var selectedVehicle = _vehicleSpawnController.SelectedVehicleDefinition;
            var selectedTractor = GetSelectedCommercialDealershipTruckDefinition(selectedVehicle);
            if (selectedVehicle == null && selectedTractor == null)
            {
                return "Select a truck and/or trailer first.";
            }

            var price = Math.Max(0f, selectedVehicle != null ? selectedVehicle.Price : 0f) + Math.Max(0f, selectedTractor != null ? selectedTractor.Price : 0f);
            if (!IsCommercialDealershipRentMode)
            {
                return string.Format("Purchase for {0} and assign it to the active office garage.", ModFormatting.FormatMoney(price));
            }

            var dailyRent = GetSelectedCommercialVehicleDailyRent();
            if (dailyRent <= 0.001f)
            {
                return BuildCommercialDealershipRentUnavailableDetail();
            }

            return string.Format(
                "Rent for {0}/day with no upfront cost. Daily billing begins after the next in-game day passes.",
                ModFormatting.FormatMoney(dailyRent));
        }

        private VehicleDefinition GetSelectedCommercialDealershipTruckDefinition(VehicleDefinition selectedVehicle)
        {
            var selectedTractor = _vehicleSpawnController.SelectedTractorDefinition;
            if (selectedVehicle == null)
            {
                return selectedTractor;
            }

            return selectedVehicle.IsTrailer ? selectedTractor : null;
        }

        private enum CommercialDealershipAcquisitionMode
        {
            Purchase = 0,
            Rent = 1,
        }

        private enum CommercialGarageMenuContext
        {
            Office = 0,
            Industry = 1,
        }
    }
}
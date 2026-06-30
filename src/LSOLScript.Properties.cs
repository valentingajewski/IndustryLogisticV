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
        private const int CommercialDealershipContentListStartIndex = 1;
        private const float ApartmentExteriorMarkerScale = 1.3f;
        private const float ApartmentInteriorMarkerScale = 1.2f;
        private const float ApartmentGarageMarkerScale = 1.3f;
        private const float MotelExteriorMarkerScale = 1.3f;
        private const float DealershipInteractionDistance = 4.6f;
        private const float CommercialDealershipVehiclePadHeading = 315f;
        private const float PersonalDealershipVehiclePadHeading = 70f;
        private const string PersonalDealershipAllCategory = "All";
        private const string PersonalDealershipUncategorizedCategory = "Uncategorized";

        private static readonly Vector3 CommercialDealershipMarker = new Vector3(-979.56f, -2232.48f, 8.86f);
        private static readonly Vector3 CommercialDealershipVehiclePadPosition = new Vector3(-968.77f, -2242.95f, 8.85f);
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
        private Vehicle _commercialDealershipPreviewVehicle;
        private Vehicle _personalDealershipPreviewVehicle;
        private OfficeDefinition _menuOffice;
        private InteriorDefinition _menuApartment;
        private MotelDefinition _menuMotel;
        private CommercialGarageMenuContext _commercialGarageMenuContext;
        private OwnedCommercialVehiclePersistenceEntry _selectedCommercialGarageVehicle;
        private List<OfficeObjectDefinition> _officeObjectPreviewSlots;
        private List<VehicleCargoType> _commercialDealershipVisibleCargoTypes;
        private List<VehicleDefinition> _commercialDealershipVisibleVehicles;
        private List<string> _personalDealershipCategories;
        private List<DealershipVehicleDefinition> _personalDealershipVisibleVehicles;
        private CommercialDealershipCatalogSection _activeCommercialDealershipSection;
        private CommercialDealershipMenuView _commercialDealershipMenuView;
        private VehicleCargoType _activeCommercialDealershipCargoType;
        private string _commercialDealershipPreviewModelName;
        private string _commercialDealershipSelectedVehicleModelName;
        private string _activePersonalDealershipCategory;
        private string _personalDealershipPreviewModelName;
        private string _personalDealershipSelectedVehicleModelName;
        private OfficeObjectDefinition _pendingOfficeObjectPurchaseDefinition;
        private ApartmentSleepTransitionPhase _apartmentSleepTransitionPhase;
        private int _apartmentSleepTransitionPhaseStartedAt;
        private bool _apartmentSleepClockApplied;
        private int _selectedCommercialDealershipActionIndex;
        private int _selectedCommercialDealershipCargoTypeIndex;
        private int _selectedPersonalDealershipCategoryIndex;

        private enum ApartmentSleepTransitionPhase
        {
            None = 0,
            FadingOut = 1,
            HoldingBlack = 2,
            FadingIn = 3,
        }

        private enum CommercialDealershipMenuView
        {
            Root = 0,
            CargoTypes = 1,
            Vehicles = 2,
            Actions = 3,
        }

        private enum CommercialDealershipCatalogSection
        {
            TruckTractors = 0,
            Trailers = 1,
            TrucksVans = 2,
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
                SetMotelWaypoint = SetMotelWaypointById,
            };
        }

        private void InitializePropertyMenus()
        {
            _commercialGarageMenu = new LemonMenu(Text(ModTextKey.PropertyMenuCommercialGarageTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuCommercialGarageSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _commercialGarageActionMenu = new LemonMenu(Text(ModTextKey.PropertyMenuVehicleTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuVehicleSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeObjectsMenu = new LemonMenu(Text(ModTextKey.PropertyMenuOfficeObjectsTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuOfficeObjectsSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeObjectPurchaseMenu = new LemonMenu(Text(ModTextKey.PropertyMenuConfirmPurchaseTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuConfirmPurchaseSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeFuelManagementMenu = new LemonMenu(Text(ModTextKey.PropertyMenuFuelManagementTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuFuelManagementSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _apartmentMenu = new LemonMenu(Text(ModTextKey.PropertyMenuApartmentTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuApartmentSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _apartmentInteriorMenu = new LemonMenu(Text(ModTextKey.PropertyMenuApartmentTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuApartmentInteriorSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _motelMenu = new LemonMenu(Text(ModTextKey.PropertyMenuMotelTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuMotelSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _personalGarageMenu = new LemonMenu(Text(ModTextKey.PropertyMenuPersonalGarageTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuPersonalGarageSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _personalDealershipMenu = new LemonMenu(Text(ModTextKey.PropertyMenuVehicleDealershipTitle))
            {
                Subtitle = Text(ModTextKey.PropertyMenuVehicleDealershipSubtitle),
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _commercialGarageMenuContext = CommercialGarageMenuContext.Office;
            _officeObjectPreviewSlots = new List<OfficeObjectDefinition>();
            _commercialDealershipVisibleCargoTypes = new List<VehicleCargoType>();
            _commercialDealershipVisibleVehicles = new List<VehicleDefinition>();
            _activeCommercialDealershipSection = CommercialDealershipCatalogSection.TruckTractors;
            _commercialDealershipMenuView = CommercialDealershipMenuView.Root;
            _activeCommercialDealershipCargoType = VehicleCargoType.Unknown;
            _commercialDealershipSelectedVehicleModelName = null;
            _selectedCommercialDealershipActionIndex = 0;
            _selectedCommercialDealershipCargoTypeIndex = 0;
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
            ClearCommercialDealershipPreviewVehicle();
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
            var balanceBefore = GetCompanyBalance();
            var updatedBalance = balanceBefore;
            var messages = _propertyManager.ProcessWeeklyCharges(GetCurrentInGameWeekMinute(), ref updatedBalance);
            if (Math.Abs(updatedBalance - balanceBefore) > 0.001f)
            {
                SetCompanyBalance(updatedBalance);
                _tabletStateStore.MarkBalanceDirty();
                RequestCareerAutosave();
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
                Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptPressToAction, KeyName(_controls.Interact), facilityInteraction.PromptDescription)));
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
                    Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptManageNamed, KeyName(_controls.Interact), office.DisplayName)));
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
                    Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptManagePersonalGarage, KeyName(_controls.Interact))));
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
                    Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptManageNamed, KeyName(_controls.Interact), apartment.DisplayName)));
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
                    Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptRestAt, KeyName(_controls.Interact), motel.DisplayName)));
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
                    Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptApartmentOptions, KeyName(_controls.Interact))));
                    promptShown = true;
                }
            }

            DrawDealershipMarker(playerPos, CommercialDealershipMarker, Color.FromArgb(205, 94, 174, 220), canShowPrompts, ref promptShown, Text(ModTextKey.PropertyActionBrowseCommercialDealership));
            DrawDealershipMarker(playerPos, PersonalDealershipMarker, Color.FromArgb(205, 228, 156, 82), canShowPrompts, ref promptShown, Text(ModTextKey.PropertyActionBrowsePersonalDealership));
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
                Screen.ShowHelpTextThisFrame(PrefixMessage(Text(ModTextKey.PropertyPromptPressToAction, KeyName(_controls.Interact), promptDescription)));
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
                return Text(ModTextKey.PropertyOfficeSubtitleDefault);
            }

            var officeState = _propertyManager.GetOfficeState(_menuOffice.OfficeId);
            var stateLabel = officeState == null || (!officeState.IsOwned && !officeState.IsRented)
                ? Text(ModTextKey.PropertyOfficeStateAvailable)
                : officeState.IsAccessSuspended || officeState.OutstandingRent > 0.01f
                    ? Text(ModTextKey.PropertyOfficeStateArrears, ModFormatting.FormatMoney(officeState.OutstandingRent))
                    : officeState.IsOwned
                        ? Text(ModTextKey.PropertyOfficeStateOwned)
                        : Text(ModTextKey.PropertyOfficeStateRented);
            if (officeState != null && officeState.IsOwned && !officeState.IsAccessSuspended && officeState.OutstandingRent <= 0.01f)
            {
                return Text(ModTextKey.PropertyOfficeSubtitlePermanentAccess, stateLabel);
            }

        return stateLabel;
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
                CaptionFactory = () => Text(ModTextKey.PropertyOfficeRowBalance, ModFormatting.FormatMoney(GetCompanyBalance())),
                DetailFactory = () => office != null
                    ? Text(ModTextKey.PropertyOfficeDetailVehicleSlotsDistrict, BuildOfficeGarageCapacityLabel(office), office.DistrictName)
                    : Text(ModTextKey.PropertyValueNoOfficeSelected),
            });

            if (office == null)
            {
                return items;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => office.DisplayName,
                DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailRentPurchase, ModFormatting.FormatMoney(office.WeeklyOfficeRent), ModFormatting.FormatMoney(office.OfficePrice)),
            });

            if (!hasAccess)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => transferSourceState != null ? Text(ModTextKey.PropertyOfficeActionTransferRental) : Text(ModTextKey.PropertyOfficeActionRent),
                    DetailFactory = () => transferSourceState != null
                        ? Text(ModTextKey.PropertyOfficeDetailTransferRental, ModFormatting.FormatMoney(office.WeeklyOfficeRent))
                        : Text(ModTextKey.PropertyOfficeDetailRentUnlock, ModFormatting.FormatMoney(office.WeeklyOfficeRent)),
                    OnActivate = transferSourceState != null ? (Action)TransferSelectedOfficeRental : RentSelectedOffice,
                });
            }

            if (!isOwned && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionPurchase),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailPurchase, ModFormatting.FormatMoney(office.OfficePrice)),
                    OnActivate = PurchaseSelectedOffice,
                });
            }

            if (hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionSettleArrears),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailOutstandingBalance, ModFormatting.FormatMoney(officeState.OutstandingRent)),
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
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionRelinquishRental),
                    DetailFactory = () => isActiveOffice
                        ? Text(ModTextKey.PropertyOfficeDetailRelinquishActive)
                        : Text(ModTextKey.PropertyOfficeDetailRelinquishInactive),
                    OnActivate = RelinquishSelectedOfficeRental,
                });
            }

            if (!isActiveOffice && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionActivate),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailActivate, office.DisplayName),
                    OnActivate = ActivateSelectedOffice,
                });
            }
            else if (!hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionPlayerModel, _workerSpawnController.SelectedWorkerDisplayName),
                    OnLeft = () => ChangeWorkerIndex(-1),
                    OnRight = () => ChangeWorkerIndex(1),
                    OnActivate = ApplyWorkerModel,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionGarage),
                    DetailFactory = BuildCommercialGarageSummary,
                    OnActivate = OpenCommercialGarageMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionObjects),
                    DetailFactory = BuildOfficeObjectsSummary,
                    OnActivate = OpenOfficeObjectsMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionFuelManagement),
                    DetailFactory = BuildOfficeFuelManagementSummary,
                    OnActivate = OpenOfficeFuelManagementMenu,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeActionRepairVehicle),
                    DetailFactory = BuildOfficeRepairDetail,
                    OnActivate = RepairVehicleAtOffice,
                });
            }

            return items;
        }

        private string BuildCommercialGarageSummary()
        {
            var activeCount = _propertyManager.GetActiveCommercialGarageVehicles().Count();
            var reserveCount = _propertyManager.GetReserveCommercialVehicles().Count();
            var activeOffice = _propertyManager.ActiveOffice;
            var capacity = activeOffice != null ? BuildOfficeGarageCapacityLabel(activeOffice) : Text(ModTextKey.PropertyValueZero);
            return Text(ModTextKey.PropertyOfficeSummaryActiveReserve, activeCount, capacity, reserveCount);
        }

        private void AddCommercialGarageAttachmentActions(
            List<OfficeMenuItem> items,
            OwnedCommercialVehiclePersistenceEntry vehicle,
            OwnedCommercialVehicleAssetPersistenceEntry poweredAsset,
            OwnedCommercialVehicleAssetPersistenceEntry trailerAsset)
        {
            if (items == null || vehicle == null || _propertyManager == null)
            {
                return;
            }

            if (poweredAsset != null && trailerAsset != null)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyGarageActionDetachPoweredUnit),
                    DetailFactory = () => Text(ModTextKey.PropertyGarageDetailDetachPoweredUnit, poweredAsset.DisplayName),
                    OnActivate = () => DetachCommercialPoweredUnitFromGarageVehicle(vehicle),
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyGarageActionDetachTrailer),
                    DetailFactory = () => Text(ModTextKey.PropertyGarageDetailDetachTrailer, trailerAsset.DisplayName),
                    OnActivate = () => DetachCommercialTrailerFromGarageVehicle(vehicle),
                });
                return;
            }

            if (poweredAsset != null && poweredAsset.FleetRole == CommercialVehicleFleetRole.Tractor && trailerAsset == null)
            {
                var availableTrailers = _propertyManager.GetAvailableCommercialTrailerAssets().ToList();
                if (availableTrailers.Count == 0)
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.PropertyGarageRowNoTrailersFree),
                        DetailFactory = () => Text(ModTextKey.PropertyGarageDetailNoTrailersFree),
                    });
                    return;
                }

                for (int i = 0; i < availableTrailers.Count; i++)
                {
                    var trailerOption = availableTrailers[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.PropertyGarageActionAttachTrailer, trailerOption.DisplayName),
                        DetailFactory = () => Text(ModTextKey.PropertyGarageDetailPairVehicles, trailerOption.DisplayName, vehicle.DisplayName),
                        OnActivate = () => AttachCommercialTrailerToGarageVehicle(vehicle, trailerOption),
                    });
                }

                return;
            }

            if (poweredAsset == null && trailerAsset != null)
            {
                var availablePoweredUnits = _propertyManager.GetAvailableCommercialPoweredAssets()
                    .Where(asset => asset != null && asset.FleetRole == CommercialVehicleFleetRole.Tractor)
                    .ToList();
                if (availablePoweredUnits.Count == 0)
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.PropertyGarageRowNoTractorsFree),
                        DetailFactory = () => Text(ModTextKey.PropertyGarageDetailNoTractorsFree),
                    });
                    return;
                }

                for (int i = 0; i < availablePoweredUnits.Count; i++)
                {
                    var tractorOption = availablePoweredUnits[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.PropertyGarageActionAttachPoweredUnit, tractorOption.DisplayName),
                        DetailFactory = () => Text(ModTextKey.PropertyGarageDetailPairVehicles, tractorOption.DisplayName, vehicle.DisplayName),
                        OnActivate = () => AttachCommercialPoweredUnitToGarageVehicle(vehicle, tractorOption),
                    });
                }
            }
        }

        private void AttachCommercialTrailerToGarageVehicle(OwnedCommercialVehiclePersistenceEntry vehicle, OwnedCommercialVehicleAssetPersistenceEntry trailerAsset)
        {
            if (vehicle == null || trailerAsset == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryAssignCommercialVehicleTrailerAsset(vehicle.AssetId, trailerAsset.AssetId, out message))
            {
                RefreshCommercialGarageMenusAfterPairChange();
            }

            ShowStatus(message);
        }

        private void AttachCommercialPoweredUnitToGarageVehicle(OwnedCommercialVehiclePersistenceEntry vehicle, OwnedCommercialVehicleAssetPersistenceEntry poweredAsset)
        {
            if (vehicle == null || poweredAsset == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryAssignCommercialVehiclePoweredAsset(vehicle.AssetId, poweredAsset.AssetId, out message))
            {
                RefreshCommercialGarageMenusAfterPairChange();
            }

            ShowStatus(message);
        }

        private void DetachCommercialPoweredUnitFromGarageVehicle(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryClearCommercialVehiclePoweredAsset(vehicle.AssetId, out message))
            {
                RefreshCommercialGarageMenusAfterPairChange();
            }

            ShowStatus(message);
        }

        private void DetachCommercialTrailerFromGarageVehicle(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryClearCommercialVehicleTrailerAsset(vehicle.AssetId, out message))
            {
                RefreshCommercialGarageMenusAfterPairChange();
            }

            ShowStatus(message);
        }

        private void RefreshCommercialGarageMenusAfterPairChange()
        {
            if (_tabletStateStore != null)
            {
                _tabletStateStore.MarkCargoDirty();
            }

            if (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
            {
                RebuildCommercialGarageActionMenuItems();
            }

            RebuildCommercialGarageMenuItems();
            ReevaluatePlayerSuccesses(true);
        }

        private string BuildOfficeGarageCapacityLabel(OfficeDefinition office)
        {
            if (office == null)
            {
                return Text(ModTextKey.PropertyValueZero);
            }

            return _officeGarageLimitDifficultyEnabled
                ? Math.Max(0, office.MaxCommercialVehicles).ToString()
                : Text(ModTextKey.PropertyValueUnlimited);
        }

        private string BuildOfficeObjectsSummary()
        {
            if (_menuOffice == null)
            {
                return Text(ModTextKey.PropertyValueNoOfficeSelected);
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return Text(ModTextKey.PropertyOfficeObjectsSummaryActivate);
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
            return Text(ModTextKey.PropertyOfficeObjectsSummaryCatalogPlaced, catalogCount, placed);
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
                return Text(ModTextKey.PropertyOfficeRepairDetailActivate);
            }

            return _propertyManager.HasOfficeObjectFunction(_menuOffice.OfficeId, OfficeObjectFunction.Repair)
                ? Text(ModTextKey.PropertyOfficeRepairDetailFree)
                : Text(ModTextKey.PropertyOfficeRepairDetailInstall);
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
                    CaptionFactory = () => Text(ModTextKey.PropertyFuelRowTankStatus),
                    DetailFactory = BuildOfficeFuelManagementStatusDetail,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyFuelRowImpliedRate),
                    DetailFactory = BuildOfficeFuelManagementPricingDetail,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyFuelRowRefuelVehicle),
                    DetailFactory = BuildOfficeVehicleRefuelDetail,
                    OnActivate = RefuelVehicleFromOfficeTank,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyFuelRowUnloadFuelCargo),
                    DetailFactory = BuildOfficeFuelUnloadDetail,
                    OnActivate = UnloadFuelCargoIntoOfficeTank,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyFuelRowRequestDieselDelivery),
                    DetailFactory = BuildOfficeFuelDeliveryDetail,
                    OnActivate = RequestOfficeFuelDelivery,
                },
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    DetailFactory = () => Text(ModTextKey.PropertyFuelDetailBackToOffice),
                    OnActivate = ReturnToOfficeMenuFromFuelManagement,
                },
            };

            _officeFuelManagementMenu.Title = Text(ModTextKey.PropertyMenuFuelManagementTitle);
            _officeFuelManagementMenu.Subtitle = Text(ModTextKey.PropertyFuelMenuSubtitle);
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
                        ? Text(ModTextKey.PropertyOfficeObjectsDetailCatalogEntries, _propertyManager.OfficeObjectCatalog.Count, _menuOffice.DisplayName)
                        : Text(ModTextKey.PropertyValueNoOfficeSelected),
                }
            };
            _officeObjectPreviewSlots.Add(null);

            var catalog = _propertyManager.OfficeObjectCatalog;
            if (catalog.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeObjectsRowNoneConfigured),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeObjectsDetailNoneConfigured),
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
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = ReturnToOfficeMenuFromObjects,
            });
            _officeObjectPreviewSlots.Add(null);

            _officeObjectsMenu.Title = Text(ModTextKey.PropertyMenuOfficeObjectsTitle);
            _officeObjectsMenu.Subtitle = Text(ModTextKey.PropertyOfficeObjectsSubtitleDefault);
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
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeObjectsRowNoneSelected),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeObjectsDetailNoneSelected),
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    OnActivate = ReturnToOfficeObjectsMenu,
                });
                _officeObjectPurchaseMenu.Title = Text(ModTextKey.PropertyMenuConfirmPurchaseTitle);
                _officeObjectPurchaseMenu.Subtitle = Text(ModTextKey.PropertyOfficeObjectsSubtitleUnavailable);
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
                CaptionFactory = () => canPurchase ? Text(ModTextKey.PropertyOfficeObjectsActionPurchaseFor, ModFormatting.FormatMoney(definition.Price)) : Text(ModTextKey.PropertyOfficeObjectsActionPurchaseBlocked),
                DetailFactory = () => canPurchase
                    ? OfficeObjectCatalogFormatter.BuildPurchaseActionDetail(definition)
                    : blockedReason,
                OnActivate = canPurchase
                    ? (Action)ConfirmOfficeObjectPurchase
                    : (Action)(() => ShowStatus(blockedReason)),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = ReturnToOfficeObjectsMenu,
            });

            _officeObjectPurchaseMenu.Title = Text(ModTextKey.PropertyMenuConfirmPurchaseTitle);
            _officeObjectPurchaseMenu.Subtitle = _menuOffice != null ? _menuOffice.DisplayName : Text(ModTextKey.PropertyOfficeObjectsSubtitleFallback);
            _officeObjectPurchaseMenu.SetItems(items);
        }

        private bool CanPurchaseOfficeObject(OfficeObjectDefinition definition, out string blockedReason)
        {
            blockedReason = string.Empty;
            if (definition == null || _menuOffice == null)
            {
                blockedReason = Text(ModTextKey.PropertyOfficeObjectsBlockedNoSelection);
                return false;
            }

            if (!string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                blockedReason = Text(ModTextKey.PropertyOfficeObjectsBlockedActivateOffice);
                return false;
            }

            string reason;
            if (!_propertyManager.CanUseCommercialSystems(out reason))
            {
                blockedReason = reason;
                return false;
            }

            return _propertyManager.CanPurchaseOfficeObject(_menuOffice.OfficeId, definition, GetCompanyBalance(), out blockedReason);
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
            var balance = GetCompanyBalance();
            if (!_propertyManager.TryPurchaseOfficeObject(_menuOffice.OfficeId, _pendingOfficeObjectPurchaseDefinition.ObjectId, ref balance, out purchasedEntry, out message))
            {
                ShowStatus(message);
                RebuildOfficeObjectPurchaseMenuItems();
                return;
            }

            SetCompanyBalance(balance);
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
                ShowStatus(Text(ModTextKey.PropertyPersonalDealershipStatusSelectVehicle));
                return;
            }

            PurchasePersonalVehicle(definition);
        }

        private string BuildPersonalDealershipCategoryButtonCaption(string category)
        {
            if (string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase))
            {
                return Text(ModTextKey.PropertyPersonalDealershipCaptionAllVehicles);
            }

            if (string.Equals(category, PersonalDealershipUncategorizedCategory, StringComparison.OrdinalIgnoreCase))
            {
                return Text(ModTextKey.PropertyPersonalDealershipCaptionUncategorized);
            }

            return category;
        }

        private string BuildPersonalDealershipCategoryButtonDetail(string category)
        {
            var vehicleCount = GetPersonalDealershipCategoryVehicleCount(category);
            var displayCategory = string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                ? Text(ModTextKey.PropertyPersonalDealershipValueFullCatalog)
                : BuildPersonalDealershipCategoryButtonCaption(category);
            if (vehicleCount <= 0)
            {
                return string.Equals(category, PersonalDealershipAllCategory, StringComparison.OrdinalIgnoreCase)
                    ? Text(ModTextKey.PropertyPersonalDealershipDetailNoVehiclesAll)
                    : Text(ModTextKey.PropertyPersonalDealershipDetailNoVehiclesCategory, displayCategory);
            }

            return vehicleCount == 1
                ? Text(ModTextKey.PropertyPersonalDealershipDetailBrowseCategorySingle, vehicleCount, displayCategory)
                : Text(ModTextKey.PropertyPersonalDealershipDetailBrowseCategoryPlural, vehicleCount, displayCategory);
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
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryRentOffice(officeId, ref balance, GetCurrentInGameWeekMinute(), out message);
            SetCompanyBalance(balance);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryTransferOfficeRentalAction(string officeId)
        {
            string message;
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryTransferOfficeRental(officeId, ref balance, GetCurrentInGameWeekMinute(), out message);
            SetCompanyBalance(balance);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryPurchaseOfficeAction(string officeId)
        {
            string message;
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryPurchaseOffice(officeId, ref balance, GetCurrentInGameWeekMinute(), out message);
            SetCompanyBalance(balance);
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
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryPayOfficeArrears(officeId, ref balance, out message);
            SetCompanyBalance(balance);
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
                        ? Text(ModTextKey.PropertyCommercialGarageDetailIndustryDeploy)
                        : Text(ModTextKey.PropertyCommercialGarageDetailActions),
                }
            };

            var activeVehicles = _propertyManager.GetActiveCommercialGarageVehicles().ToList();
            if (activeVehicles.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowNoActiveVehicles),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailNoActiveVehicles),
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
                ? Text(ModTextKey.PropertyCommercialGarageTitleIndustryDeployment)
                : Text(ModTextKey.PropertyOfficeActionGarage);
            _commercialGarageMenu.Subtitle = _commercialGarageMenuContext == CommercialGarageMenuContext.Industry
                ? Text(ModTextKey.PropertyCommercialGarageSubtitleIndustryDeployment)
                : Text(ModTextKey.PropertyMenuCommercialGarageSubtitle);
            _commercialGarageMenu.SetItems(items);
        }

        private string BuildCommercialVehicleDetail(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return string.Empty;
            }

            var location = vehicle.InActiveGarage ? Text(ModTextKey.PropertyCommercialGarageValueActiveLocation) : Text(ModTextKey.PropertyCommercialGarageValueReserveLocation);
            var deployed = _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId) ? Text(ModTextKey.PropertyCommercialGarageValueDeployed) : Text(ModTextKey.PropertyCommercialGarageValueStored);
            var acquisition = vehicle.IsRental
                ? Text(ModTextKey.PropertyCommercialGarageValueRentalPerDay, ModFormatting.FormatMoney(vehicle.DailyRent))
                : Text(ModTextKey.PropertyCommercialGarageValueOwned);
            var salePreview = !vehicle.IsRental ? _propertyManager.GetCommercialVehicleSalePreview(vehicle) : null;
            var ownershipDetail = salePreview != null
                ? Text(ModTextKey.PropertyCommercialGarageDetailConditionResale, salePreview.MaintenanceConditionPercent, ModFormatting.FormatMoney(salePreview.EstimatedResaleValue))
                : acquisition;
            var cargo = string.IsNullOrWhiteSpace(vehicle.Commodity)
                ? Text(ModTextKey.PropertyCommercialGarageValueEmptyCargo)
                : string.Format("{0} {1}", vehicle.Commodity, ModFormatting.FormatRatio(vehicle.WeightTons, Math.Max(0f, vehicle.CapacityTons), Text(ModTextKey.PropertyCommercialGarageValueTonsUnit)));
            var npcAssignment = BuildCommercialVehicleNpcAssignmentDetail(vehicle);
            return string.IsNullOrWhiteSpace(npcAssignment)
                ? Text(ModTextKey.PropertyCommercialGarageDetailSummary, location, deployed, ownershipDetail, cargo)
                : Text(ModTextKey.PropertyCommercialGarageDetailSummaryWithNpc, location, deployed, ownershipDetail, npcAssignment, cargo);
        }

        private string BuildCommercialVehicleSellActionDetail(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null || vehicle.IsRental)
            {
                return Text(ModTextKey.PropertyCommercialGarageDetailRentalClose);
            }

            var preview = _propertyManager.GetCommercialVehicleSalePreview(vehicle);
            var conditionPrefix = preview.ConditionAdjustmentAmount >= 0f ? "+" : "-";
            var conditionDelta = ModFormatting.FormatMoney(Math.Abs(preview.ConditionAdjustmentAmount));
            var depreciationLoss = ModFormatting.FormatMoney(Math.Abs(preview.DepreciationLossAmount));
            return preview.DepreciationPenaltyPercent >= 20f
                ? Text(
                    ModTextKey.PropertyCommercialGarageDetailSalePreviewCapped,
                    ModFormatting.FormatMoney(preview.BaseRefund),
                    conditionPrefix,
                    conditionDelta,
                    preview.MaintenanceConditionPercent,
                    depreciationLoss,
                    preview.DepreciationPenaltyPercent,
                    ModFormatting.FormatMoney(preview.EstimatedResaleValue))
                : Text(
                    ModTextKey.PropertyCommercialGarageDetailSalePreview,
                    ModFormatting.FormatMoney(preview.BaseRefund),
                    conditionPrefix,
                    conditionDelta,
                    preview.MaintenanceConditionPercent,
                    depreciationLoss,
                    preview.DepreciationPenaltyPercent,
                    ModFormatting.FormatMoney(preview.EstimatedResaleValue));
        }

        private string BuildCommercialVehicleStatusCaption(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return string.Empty;
            }

            return Text(
                ModTextKey.PropertyCommercialGarageStatusCaption,
                _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId) ? Text(ModTextKey.PropertyCommercialGarageStatusOut) : Text(ModTextKey.PropertyCommercialGarageStatusStored),
                vehicle.IsRental ? Text(ModTextKey.PropertyCommercialGarageStatusRentTag) : string.Empty,
                IsCommercialVehicleAssignedToNpcContract(vehicle) ? Text(ModTextKey.PropertyCommercialGarageStatusNpcTag) : string.Empty,
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
                ? Text(ModTextKey.PropertyCommercialGarageNpcTier, contract.Tier.DisplayName)
                : Text(ModTextKey.PropertyCommercialGarageNpcTierFallback);
            return routeCount > 0
                ? Text(ModTextKey.PropertyCommercialGarageNpcAssignedWithRoutes, tierLabel, contract.Id, routeCount)
                : Text(ModTextKey.PropertyCommercialGarageNpcAssigned, tierLabel, contract.Id);
        }

        private bool ShowNpcAssignedCommercialVehicleBlocked(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (!IsCommercialVehicleAssignedToNpcContract(vehicle))
            {
                return false;
            }

            ShowStatus(Text(ModTextKey.PropertyCommercialGarageStatusNpcManaged, vehicle.DisplayName));
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
                    ShowStatus(Text(ModTextKey.PropertyCommercialGarageStatusNoIndustrySpawn));
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
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowNoVehicleSelected),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailNoVehicleSelected),
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    OnActivate = ReturnToCommercialGarageMenu,
                });
                _commercialGarageActionMenu.Title = Text(ModTextKey.PropertyMenuVehicleTitle);
                _commercialGarageActionMenu.Subtitle = Text(ModTextKey.PropertyCommercialGarageSubtitleVehicleActions);
                _commercialGarageActionMenu.SetItems(items);
                return;
            }

            var isDeployed = _propertyManager.IsCommercialVehicleDeployed(vehicle.AssetId);
            var npcAssignmentDetail = BuildCommercialVehicleNpcAssignmentDetail(vehicle);
            var isNpcAssigned = !string.IsNullOrWhiteSpace(npcAssignmentDetail);
            var poweredAsset = _propertyManager.GetCommercialVehicleAssetRecord(vehicle.TractorVehicleId);
            var trailerAsset = _propertyManager.GetCommercialVehicleAssetRecord(vehicle.TrailerVehicleId);
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => vehicle.DisplayName,
                DetailFactory = () => BuildCommercialVehicleDetail(vehicle),
            });

            if (isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowAssignedToNpc),
                    DetailFactory = () => npcAssignmentDetail,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowManualControlLocked),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailManualControlLocked),
                });
            }
            else if (!isDeployed)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowRetrieveVehicle),
                    DetailFactory = () => vehicle.InActiveGarage
                        ? Text(ModTextKey.PropertyCommercialGarageDetailRetrieveActive)
                        : Text(ModTextKey.PropertyCommercialGarageDetailRetrieveReserve),
                    OnActivate = () => RetrieveCommercialVehicleFromGarage(vehicle),
                });
            }
            else
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowPutInStorage),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailPutInStorage),
                    OnActivate = () => StoreCommercialVehicleFromGarage(vehicle),
                });
            }

            if (!isNpcAssigned && !isDeployed)
            {
                AddCommercialGarageAttachmentActions(items, vehicle, poweredAsset, trailerAsset);
            }

            if (vehicle.InActiveGarage && !isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialGarageRowMoveToReserve),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailMoveToReserve),
                    OnActivate = () => MoveCommercialVehicleToReserve(vehicle),
                });
            }

            if (!isNpcAssigned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => vehicle.IsRental ? Text(ModTextKey.PropertyCommercialGarageRowEndRent) : Text(ModTextKey.PropertyCommercialGarageRowSellVehicle),
                    DetailFactory = () => vehicle.IsRental
                        ? Text(ModTextKey.PropertyCommercialGarageDetailRentalClose)
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
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                DetailFactory = () => Text(ModTextKey.PropertyCommercialGarageDetailBackToList),
                OnActivate = ReturnToCommercialGarageMenu,
            });

            _commercialGarageActionMenu.Title = Text(ModTextKey.PropertyMenuVehicleTitle);
            _commercialGarageActionMenu.Subtitle = vehicle.IsRental
                ? Text(ModTextKey.PropertyCommercialGarageSubtitleRental, ModFormatting.FormatMoney(vehicle.DailyRent))
                : Text(ModTextKey.PropertyCommercialGarageSubtitleOwnedVehicle);
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
                ShowStatus(Text(ModTextKey.PropertyCommercialGarageStatusNoActiveOffice));
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
            var balance = GetCompanyBalance();
            if (_propertyManager.TrySellCommercialVehicle(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, ref balance, out message))
            {
                SetCompanyBalance(balance);
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
            var balance = GetCompanyBalance();
            if (_propertyManager.TryEndCommercialVehicleRental(vehicle.AssetId, _fleetManager, _vehicleFuelSystem, ref balance, out message))
            {
                SetCompanyBalance(balance);
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
            var apartmentStateLabel = isOwned
                ? Text(ModTextKey.PropertyOfficeStateOwned)
                : isRentalOnly
                    ? Text(ModTextKey.PropertyOfficeStateRented)
                    : Text(ModTextKey.PropertyOfficeStateAvailable);

            _apartmentMenu.Title = apartment != null ? apartment.DisplayName : Text(ModTextKey.PropertyMenuApartmentTitle);
            _apartmentMenu.Subtitle = apartment != null
                ? Text(ModTextKey.PropertyOfficeDetailRentPurchase, ModFormatting.FormatMoney(apartment.InteriorWeeklyRent), ModFormatting.FormatMoney(apartment.InteriorPrice))
                : Text(ModTextKey.PropertyMenuApartmentSubtitle);

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyOfficeRowBalance, ModFormatting.FormatMoney(GetCompanyBalance())),
                DetailFactory = () => apartment == null
                    ? string.Empty
                    : Text(
                        ModTextKey.PropertyApartmentDetailIdentityState,
                        apartment.InteriorIgName ?? apartment.InteriorType ?? string.Empty,
                        apartmentStateLabel),
            });

            if (apartment == null)
            {
                _apartmentMenu.SetItems(items);
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => apartment.DisplayName,
                DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailRentPurchase, ModFormatting.FormatMoney(apartment.InteriorWeeklyRent), ModFormatting.FormatMoney(apartment.InteriorPrice)),
            });

            if (!isOwned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionPurchase),
                    DetailFactory = () => isRentalOnly
                        ? Text(ModTextKey.PropertyApartmentDetailPurchaseConvert, ModFormatting.FormatMoney(apartment.InteriorPrice))
                        : Text(ModTextKey.PropertyApartmentDetailPurchase, ModFormatting.FormatMoney(apartment.InteriorPrice)),
                    OnActivate = PurchaseSelectedApartment,
                });
            }

            if (!hasAccess)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionRent),
                    DetailFactory = () => transferSourceState != null
                        ? Text(ModTextKey.PropertyApartmentDetailTransferRental, ModFormatting.FormatMoney(apartment.InteriorWeeklyRent))
                        : Text(ModTextKey.PropertyApartmentDetailRent, ModFormatting.FormatMoney(apartment.InteriorWeeklyRent)),
                    OnActivate = RentSelectedApartment,
                });
            }

            if (hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionSettleArrears),
                    DetailFactory = () => Text(ModTextKey.PropertyOfficeDetailOutstandingBalance, ModFormatting.FormatMoney(apartmentState.OutstandingRent)),
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
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionCancelRent),
                    DetailFactory = () => isActiveApartment
                        ? Text(ModTextKey.PropertyApartmentDetailCancelRentActive)
                        : Text(ModTextKey.PropertyApartmentDetailCancelRentInactive),
                    OnActivate = CancelSelectedApartmentRental,
                });
            }

            if (isOwned)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionSell),
                    DetailFactory = () => Text(ModTextKey.PropertyApartmentDetailSell),
                    OnActivate = SellSelectedApartment,
                });
            }

            if (!isActiveApartment && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionActivate),
                    DetailFactory = () => Text(ModTextKey.PropertyApartmentDetailActivate),
                    OnActivate = ActivateSelectedApartment,
                });
            }

            if (isActiveApartment && !hasArrears)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionEnter),
                    DetailFactory = () => Text(ModTextKey.PropertyApartmentDetailEnter, apartment.InteriorIgName),
                    OnActivate = EnterSelectedApartment,
                });
            }

            _apartmentMenu.SetItems(items);
        }

        private void RebuildApartmentInteriorMenuItems(InteriorDefinition apartment)
        {
            var items = new List<OfficeMenuItem>();

            _apartmentInteriorMenu.Title = apartment != null ? apartment.DisplayName : Text(ModTextKey.PropertyMenuApartmentTitle);
            _apartmentInteriorMenu.Subtitle = apartment != null
                ? Text(ModTextKey.PropertyApartmentInteriorSubtitleActive, apartment.InteriorIgName ?? apartment.InteriorType ?? apartment.DisplayName)
                : Text(ModTextKey.PropertyMenuApartmentInteriorSubtitle);

            if (apartment == null)
            {
                _apartmentInteriorMenu.SetItems(items);
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionSleep, ApartmentSleepHours),
                DetailFactory = BuildApartmentSleepMenuDetail,
                OnActivate = SleepInActiveApartment,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyApartmentActionExit),
                DetailFactory = () => Text(ModTextKey.PropertyApartmentDetailExit, apartment.DisplayName),
                OnActivate = ExitActiveApartment,
            });

            _apartmentInteriorMenu.SetItems(items);
        }

        private void RebuildMotelMenuItems()
        {
            var items = new List<OfficeMenuItem>();
            var motel = _menuMotel;
            var motelTypeLabel = motel != null && !string.IsNullOrWhiteSpace(motel.MotelType)
                ? motel.MotelType
                : Text(ModTextKey.PropertyMenuMotelTitle);

            _motelMenu.Title = motel != null ? motel.DisplayName : Text(ModTextKey.PropertyMenuMotelTitle);
            _motelMenu.Subtitle = motel != null
                ? Text(ModTextKey.PropertyMotelSubtitleRest, motelTypeLabel, ModFormatting.FormatMoney(motel.RestPrice))
                : Text(ModTextKey.PropertyMenuMotelSubtitle);

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyOfficeRowBalance, ModFormatting.FormatMoney(GetCompanyBalance())),
                DetailFactory = () => motel == null
                    ? string.Empty
                    : Text(
                        ModTextKey.PropertyMotelDetailIdentityType,
                        string.IsNullOrWhiteSpace(motel.MotelIgName) ? motel.DisplayName : motel.MotelIgName,
                        motelTypeLabel),
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
                    ? Text(ModTextKey.PropertyMotelDetailTypeOnly, motelTypeLabel)
                    : Text(ModTextKey.PropertyMotelDetailIdentityType, motel.MotelIgName, motelTypeLabel),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyMotelRowNightlyRoomPrice, ModFormatting.FormatMoney(motel.RestPrice)),
                DetailFactory = () => Text(ModTextKey.PropertyMotelDetailNightlyRoomPrice),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.PropertyMotelActionBuyRoom, ApartmentSleepHours),
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
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryPurchaseApartment(interiorId, ref balance, GetCurrentInGameWeekMinute(), out message);
            SetCompanyBalance(balance);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryRentApartmentAction(string interiorId)
        {
            string message;
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryRentApartment(interiorId, ref balance, GetCurrentInGameWeekMinute(), out message);
            SetCompanyBalance(balance);
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
            var balance = GetCompanyBalance();
            var success = _propertyManager.TrySellApartment(interiorId, ref balance, out message);
            SetCompanyBalance(balance);
            return new PropertyActionResult(success, message);
        }

        private PropertyActionResult TryPayApartmentArrearsAction(string interiorId)
        {
            string message;
            var balance = GetCompanyBalance();
            var success = _propertyManager.TryPayApartmentArrears(interiorId, ref balance, out message);
            SetCompanyBalance(balance);
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
            return BuildSharedRestMenuDetail(Text(ModTextKey.PropertyApartmentDetailSleepAvailable, ApartmentSleepHours));
        }

        private string BuildMotelRestMenuDetail()
        {
            var motel = _menuMotel;
            var price = motel != null ? motel.RestPrice : 0f;
            return BuildSharedRestMenuDetail(Text(ModTextKey.PropertyMotelDetailRestAvailable, ModFormatting.FormatMoney(price), ApartmentSleepHours));
        }

        private string BuildSharedRestMenuDetail(string availableMessage)
        {
            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                return Text(ModTextKey.PropertySharedRestDetailAvailableAgain, FormatApartmentSleepCooldown(remainingCooldownMinutes));
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
                ShowStatus(Text(ModTextKey.PropertyMotelStatusUnavailable));
                return;
            }

            RestAtMotelInternal(motel);
        }

        private void SetMotelWaypointById(string motelId)
        {
            if (string.IsNullOrWhiteSpace(motelId) || _propertyManager == null)
            {
                return;
            }

            var motel = _propertyManager.Motels.FirstOrDefault(entry => entry != null && string.Equals(entry.MotelId, motelId, StringComparison.OrdinalIgnoreCase));
            if (motel == null)
            {
                ShowStatus(Text(ModTextKey.PropertyMotelStatusUnavailable));
                return;
            }

            var position = motel.ExteriorPosition;
            Function.Call(Hash.SET_NEW_WAYPOINT, position.X, position.Y);
            ShowStatus(Text(ModTextKey.PropertyMotelStatusWaypointSet, motel.DisplayName));
        }

        private void RestAtMotelInternal(MotelDefinition motel)
        {
            var player = Game.Player.Character;
            if (motel == null || player == null || !player.Exists())
            {
                return;
            }

            if (GetCompanyBalance() + 0.001f < motel.RestPrice)
            {
                ShowStatus(Text(ModTextKey.PropertyMotelStatusNeedMoneyToRest, ModFormatting.FormatMoney(motel.RestPrice), motel.DisplayName));
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

            ApplyCompanyBalanceDelta(-motel.RestPrice);
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
                message = Text(ModTextKey.PropertySharedRestStatusPlayerUnavailable);
                return false;
            }

            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                message = Text(ModTextKey.PropertySharedRestStatusCooldown, FormatApartmentSleepCooldown(remainingCooldownMinutes));
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
            var currentClock = BuildInGameClockDateTime(year, month, day, clockHours, minutes, seconds);
            var advancedClock = currentClock.AddHours(hours);

            Function.Call(Hash.SET_CLOCK_DATE, advancedClock.Day, advancedClock.Month - 1, advancedClock.Year);
            Function.Call(Hash.SET_CLOCK_TIME, advancedClock.Hour, advancedClock.Minute, advancedClock.Second);
        }

        private string BuildPersonalGarageSummary()
        {
            var count = _propertyManager.GetOwnedPersonalVehicles().Count();
            return count == 1
                ? Text(ModTextKey.PropertyPersonalGarageSummarySingle)
                : Text(ModTextKey.PropertyPersonalGarageSummaryPlural, count);
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
                    DetailFactory = () => Text(ModTextKey.PropertyPersonalGarageDetailToggle),
                }
            };

            var vehicles = _propertyManager.GetOwnedPersonalVehicles().ToList();
            if (vehicles.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyPersonalGarageRowNoOwned),
                    DetailFactory = () => Text(ModTextKey.PropertyPersonalGarageDetailNoOwned),
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
            return Text(
                ModTextKey.PropertyApartmentDetailIdentityState,
                _propertyManager.IsPersonalVehicleDeployed(vehicle.AssetId)
                    ? Text(ModTextKey.PropertyCommercialGarageValueDeployed)
                    : Text(ModTextKey.PropertyCommercialGarageValueStored),
                string.IsNullOrWhiteSpace(vehicle.Category)
                    ? Text(ModTextKey.PropertyPersonalGarageCategoryResidence)
                    : vehicle.Category);
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
                ShowStatus(Text(ModTextKey.PropertyPersonalGarageStatusNoActiveApartment));
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
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeRowBalance, ModFormatting.FormatMoney(GetCompanyBalance())),
                    DetailFactory = () => string.IsNullOrWhiteSpace(_propertyManager.ActiveApartmentId)
                        ? Text(ModTextKey.PropertyPersonalDealershipDetailNeedApartment)
                        : Text(
                            ModTextKey.PropertyPersonalDealershipDetailActiveApartment,
                            _propertyManager.ActiveApartment != null ? _propertyManager.ActiveApartment.DisplayName : Text(ModTextKey.PropertyValueNotAvailable)),
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
                            ? Text(ModTextKey.PropertyPersonalDealershipRowNoCatalogLoaded)
                            : Text(ModTextKey.PropertyPersonalDealershipRowNoVehiclesInCategory, BuildPersonalDealershipCategoryButtonCaption(_activePersonalDealershipCategory)),
                        DetailFactory = () => _propertyManager.PersonalVehicleCatalog.Count == 0
                            ? Text(ModTextKey.PropertyPersonalDealershipDetailNoCatalogLoaded)
                            : Text(ModTextKey.PropertyPersonalDealershipDetailChooseCategory),
                    });
                }
                else
                {
                    for (int i = 0; i < _personalDealershipVisibleVehicles.Count; i++)
                    {
                        var definition = _personalDealershipVisibleVehicles[i];
                        items.Add(new OfficeMenuItem
                        {
                            CaptionFactory = () => Text(ModTextKey.PropertyPersonalDealershipVehicleCaption, definition.DisplayName, ModFormatting.FormatMoney(definition.Price)),
                            DetailFactory = () => BuildPersonalDealershipCategoryButtonCaption(GetNormalizedPersonalDealershipCategory(definition)),
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
            var balance = GetCompanyBalance();
            if (!_propertyManager.TryPurchasePersonalVehicle(definition, ref balance, out purchasedVehicle, out purchaseMessage))
            {
                ShowStatus(purchaseMessage);
                return;
            }

            SetCompanyBalance(balance);

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
                deployMessage = Text(ModTextKey.PropertyPersonalDealershipStatusPurchaseRecordMissing);
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
            _vehicleCargoMenuContext = VehicleCargoMenuContext.CommercialDealership;
            _activeCommercialDealershipSection = CommercialDealershipCatalogSection.TruckTractors;
            _commercialDealershipMenuView = CommercialDealershipMenuView.Root;
            _activeCommercialDealershipCargoType = VehicleCargoType.Unknown;
            _commercialDealershipSelectedVehicleModelName = null;
            _selectedCommercialDealershipActionIndex = 0;
            _selectedCommercialDealershipCargoTypeIndex = 0;
            _vehicleCargoMenu.Title = Text(ModTextKey.PropertyCommercialDealershipTitle);
            _vehicleCargoMenu.Subtitle = Text(ModTextKey.PropertyCommercialDealershipSubtitleCatalog);
            RebuildCommercialDealershipMenuItems();
            SelectCommercialDealershipRootSectionRow();
            _vehicleCargoMenu.Open();
        }

        private void RebuildCommercialDealershipMenuItems()
        {
            RefreshCommercialDealershipMenuState();

            _vehicleCargoMenu.Title = Text(ModTextKey.PropertyCommercialDealershipTitle);
            _vehicleCargoMenu.Subtitle = BuildCommercialDealershipSubtitle();

            var items = new List<OfficeMenuItem>
            {
                new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyOfficeRowBalance, ModFormatting.FormatMoney(GetCompanyBalance())),
                    DetailFactory = BuildCommercialDealershipBalanceDetail,
                },
            };

            if (_commercialDealershipMenuView == CommercialDealershipMenuView.Root)
            {
                foreach (var section in GetCommercialDealershipSections())
                {
                    var sectionCopy = section;
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildCommercialDealershipSectionCaption(sectionCopy),
                        DetailFactory = () => BuildCommercialDealershipSectionDetail(sectionCopy),
                        OnActivate = () => EnterCommercialDealershipSection(sectionCopy),
                    });
                }
            }
            else if (_commercialDealershipMenuView == CommercialDealershipMenuView.CargoTypes)
            {
                if (_commercialDealershipVisibleCargoTypes.Count == 0)
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = BuildCommercialDealershipEmptyCargoTypeCaption,
                        DetailFactory = BuildCommercialDealershipEmptyCargoTypeDetail,
                    });
                }
                else
                {
                    for (int i = 0; i < _commercialDealershipVisibleCargoTypes.Count; i++)
                    {
                        var cargoType = _commercialDealershipVisibleCargoTypes[i];
                        items.Add(new OfficeMenuItem
                        {
                            CaptionFactory = () => BuildCommercialDealershipCargoTypeCaption(cargoType),
                            DetailFactory = () => BuildCommercialDealershipCargoTypeDetail(cargoType),
                            OnActivate = () => EnterCommercialDealershipCargoType(cargoType),
                        });
                    }
                }
            }
            else if (_commercialDealershipMenuView == CommercialDealershipMenuView.Vehicles)
            {
                if (_commercialDealershipVisibleVehicles.Count == 0)
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = BuildCommercialDealershipEmptyVehicleCaption,
                        DetailFactory = () => Text(ModTextKey.PropertyCommercialDealershipDetailBackPrevious),
                    });
                }
                else
                {
                    for (int i = 0; i < _commercialDealershipVisibleVehicles.Count; i++)
                    {
                        var definition = _commercialDealershipVisibleVehicles[i];
                        items.Add(new OfficeMenuItem
                        {
                            CaptionFactory = () => BuildCommercialDealershipVehicleButtonCaption(definition),
                            DetailFactory = () => BuildCommercialDealershipVehicleSelectionDetail(definition),
                            OnActivate = EnterCommercialDealershipVehicleActions,
                        });
                    }
                }
            }
            else
            {
                var definition = GetSelectedCommercialDealershipVehicleDefinition();
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialDealershipActionBuy),
                    DetailFactory = () => BuildCommercialDealershipPurchaseActionDetail(definition),
                    OnActivate = PurchaseSelectedCommercialDealershipVehicle,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.PropertyCommercialDealershipActionRent),
                    DetailFactory = () => BuildCommercialDealershipRentActionDetail(definition),
                    OnActivate = RentSelectedCommercialDealershipVehicle,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.CommonBack),
                    DetailFactory = () => Text(ModTextKey.PropertyCommercialDealershipDetailBackToSelected),
                    OnActivate = ReturnFromCommercialDealershipMenu,
                });
                if (definition != null && IsCommercialDealershipPhantomLocked(definition))
                {
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.PropertyCommercialDealershipRowUnlockRequired),
                        DetailFactory = () => PhantomRoadVeteranUnlockMessage,
                    });
                }
            }

            _vehicleCargoMenu.SetItems(items);
        }

        private void RefreshCommercialDealershipMenuState()
        {
            var catalog = GetCommercialDealershipCatalog().ToList();
            _commercialDealershipVisibleCargoTypes = GetCommercialDealershipCargoTypes(catalog, _activeCommercialDealershipSection);

            if (_commercialDealershipVisibleCargoTypes.Count == 0)
            {
                _activeCommercialDealershipCargoType = VehicleCargoType.Unknown;
                _selectedCommercialDealershipCargoTypeIndex = 0;
            }
            else
            {
                if (!_commercialDealershipVisibleCargoTypes.Contains(_activeCommercialDealershipCargoType))
                {
                    _activeCommercialDealershipCargoType = _commercialDealershipVisibleCargoTypes[0];
                }

                _selectedCommercialDealershipCargoTypeIndex = _commercialDealershipVisibleCargoTypes.FindIndex(cargoType => cargoType == _activeCommercialDealershipCargoType);
                if (_selectedCommercialDealershipCargoTypeIndex < 0)
                {
                    _selectedCommercialDealershipCargoTypeIndex = 0;
                }
            }

            if (_commercialDealershipMenuView != CommercialDealershipMenuView.Vehicles
                && _commercialDealershipMenuView != CommercialDealershipMenuView.Actions)
            {
                _commercialDealershipVisibleVehicles = new List<VehicleDefinition>();
                return;
            }

            _commercialDealershipVisibleVehicles = GetCommercialDealershipSectionVehicles(catalog, _activeCommercialDealershipSection, _activeCommercialDealershipCargoType)
                .OrderBy(definition => string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.ModelName : definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_commercialDealershipVisibleVehicles.Count == 0)
            {
                _commercialDealershipSelectedVehicleModelName = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(_commercialDealershipSelectedVehicleModelName)
                || !_commercialDealershipVisibleVehicles.Any(definition => string.Equals(definition.ModelName, _commercialDealershipSelectedVehicleModelName, StringComparison.OrdinalIgnoreCase)))
            {
                _commercialDealershipSelectedVehicleModelName = _commercialDealershipVisibleVehicles[0].ModelName;
            }
        }

        private IEnumerable<VehicleDefinition> GetCommercialDealershipCatalog()
        {
            return _fleetManager != null && _fleetManager.Definitions != null
                ? _fleetManager.Definitions.Where(definition => definition != null && definition.IsEnabled && IsCommercialDealershipVehicleAvailableToPlayer(definition))
                : Enumerable.Empty<VehicleDefinition>();
        }

        private static List<VehicleCargoType> GetCommercialDealershipCargoTypes(IEnumerable<VehicleDefinition> catalog, CommercialDealershipCatalogSection section)
        {
            if (catalog == null || section == CommercialDealershipCatalogSection.TruckTractors)
            {
                return new List<VehicleCargoType>();
            }

            return catalog
                .Where(definition => IsCommercialDealershipSectionMatch(definition, section))
                .Select(definition => definition.CargoType)
                .Distinct()
                .OrderBy(cargoType => cargoType.ToDisplayName(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<VehicleDefinition> GetCommercialDealershipSectionVehicles(
            IEnumerable<VehicleDefinition> catalog,
            CommercialDealershipCatalogSection section,
            VehicleCargoType cargoType)
        {
            if (catalog == null)
            {
                return Enumerable.Empty<VehicleDefinition>();
            }

            switch (section)
            {
                case CommercialDealershipCatalogSection.TruckTractors:
                    return catalog.Where(definition => definition.IsTractor);
                case CommercialDealershipCatalogSection.Trailers:
                    return catalog.Where(definition => definition.IsTrailer && definition.CargoType == cargoType);
                default:
                    return catalog.Where(definition => definition.IsRigid && definition.CargoType == cargoType);
            }
        }

        private static bool IsCommercialDealershipSectionMatch(VehicleDefinition definition, CommercialDealershipCatalogSection section)
        {
            if (definition == null)
            {
                return false;
            }

            switch (section)
            {
                case CommercialDealershipCatalogSection.TruckTractors:
                    return definition.IsTractor;
                case CommercialDealershipCatalogSection.Trailers:
                    return definition.IsTrailer;
                default:
                    return definition.IsRigid;
            }
        }

        private static IEnumerable<CommercialDealershipCatalogSection> GetCommercialDealershipSections()
        {
            yield return CommercialDealershipCatalogSection.TruckTractors;
            yield return CommercialDealershipCatalogSection.Trailers;
            yield return CommercialDealershipCatalogSection.TrucksVans;
        }

        private string BuildCommercialDealershipBalanceDetail()
        {
            if (_propertyManager == null || string.IsNullOrWhiteSpace(_propertyManager.ActiveOfficeId) || _propertyManager.ActiveOffice == null)
            {
                return Text(ModTextKey.PropertyCommercialDealershipBalanceDetailNeedOffice);
            }

            return Text(ModTextKey.PropertyCommercialDealershipBalanceDetailActiveOffice, _propertyManager.ActiveOffice.DisplayName);
        }

        private string BuildCommercialDealershipSubtitle()
        {
            switch (_commercialDealershipMenuView)
            {
                case CommercialDealershipMenuView.CargoTypes:
                    return _activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers
                        ? Text(ModTextKey.PropertyCommercialDealershipSubtitleSelectTrailerCargo)
                        : Text(ModTextKey.PropertyCommercialDealershipSubtitleSelectRigidCargo);
                case CommercialDealershipMenuView.Vehicles:
                    switch (_activeCommercialDealershipSection)
                    {
                        case CommercialDealershipCatalogSection.TruckTractors:
                            return Text(ModTextKey.PropertyCommercialDealershipSubtitleBrowseTractors);
                        case CommercialDealershipCatalogSection.Trailers:
                            return Text(ModTextKey.PropertyCommercialDealershipSubtitleBrowseTrailers, _activeCommercialDealershipCargoType.ToDisplayName());
                        default:
                            return Text(ModTextKey.PropertyCommercialDealershipSubtitleBrowseRigids, _activeCommercialDealershipCargoType.ToDisplayName());
                    }
                case CommercialDealershipMenuView.Actions:
                    var selectedDefinition = GetSelectedCommercialDealershipVehicleDefinition();
                    return selectedDefinition == null
                        ? Text(ModTextKey.PropertyCommercialDealershipSubtitleChooseActions)
                        : Text(ModTextKey.PropertyCommercialDealershipSubtitleVehicleOptions, GetCommercialDealershipVehicleLabel(selectedDefinition));
                default:
                    return Text(ModTextKey.PropertyCommercialDealershipSubtitleChooseSections);
            }
        }

        private string BuildCommercialDealershipSectionCaption(CommercialDealershipCatalogSection section)
        {
            switch (section)
            {
                case CommercialDealershipCatalogSection.TruckTractors:
                    return Text(ModTextKey.PropertyCommercialDealershipSectionTruckTractors);
                case CommercialDealershipCatalogSection.Trailers:
                    return Text(ModTextKey.PropertyCommercialDealershipSectionTrailers);
                default:
                    return Text(ModTextKey.PropertyCommercialDealershipSectionTrucksVans);
            }
        }

        private string BuildCommercialDealershipSectionDetail(CommercialDealershipCatalogSection section)
        {
            var vehicleCount = GetCommercialDealershipSectionVehicleCount(section);
            if (section == CommercialDealershipCatalogSection.TruckTractors)
            {
                return vehicleCount <= 0
                    ? Text(ModTextKey.PropertyCommercialDealershipSectionNoTractors)
                    : Text(ModTextKey.PropertyCommercialDealershipSectionBrowseTractors, vehicleCount);
            }

            var cargoTypeCount = GetCommercialDealershipCargoTypeCount(section);
            if (section == CommercialDealershipCatalogSection.Trailers)
            {
                return vehicleCount <= 0
                    ? Text(ModTextKey.PropertyCommercialDealershipSectionNoTrailers)
                    : Text(ModTextKey.PropertyCommercialDealershipSectionBrowseTrailers, cargoTypeCount, vehicleCount);
            }

            return vehicleCount <= 0
                ? Text(ModTextKey.PropertyCommercialDealershipSectionNoRigids)
                : Text(ModTextKey.PropertyCommercialDealershipSectionBrowseRigids, cargoTypeCount, vehicleCount);
        }

        private int GetCommercialDealershipSectionVehicleCount(CommercialDealershipCatalogSection section)
        {
            return GetCommercialDealershipCatalog().Count(definition => IsCommercialDealershipSectionMatch(definition, section));
        }

        private int GetCommercialDealershipCargoTypeCount(CommercialDealershipCatalogSection section)
        {
            return GetCommercialDealershipCargoTypes(GetCommercialDealershipCatalog(), section).Count;
        }

        private string BuildCommercialDealershipCargoTypeCaption(VehicleCargoType cargoType)
        {
            return cargoType.ToDisplayName();
        }

        private string BuildCommercialDealershipCargoTypeDetail(VehicleCargoType cargoType)
        {
            var vehicleCount = GetCommercialDealershipCargoTypeVehicleCount(_activeCommercialDealershipSection, cargoType);
            if (_activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers)
            {
                return vehicleCount <= 0
                    ? Text(ModTextKey.PropertyCommercialDealershipCargoNoTrailers, cargoType.ToDisplayName())
                    : Text(ModTextKey.PropertyCommercialDealershipCargoBrowseTrailers, vehicleCount, cargoType.ToDisplayName());
            }

            return vehicleCount <= 0
                ? Text(ModTextKey.PropertyCommercialDealershipCargoNoRigids, cargoType.ToDisplayName())
                : Text(ModTextKey.PropertyCommercialDealershipCargoBrowseRigids, vehicleCount, cargoType.ToDisplayName());
        }

        private int GetCommercialDealershipCargoTypeVehicleCount(CommercialDealershipCatalogSection section, VehicleCargoType cargoType)
        {
            return GetCommercialDealershipSectionVehicles(GetCommercialDealershipCatalog(), section, cargoType).Count();
        }

        private string BuildCommercialDealershipEmptyCargoTypeCaption()
        {
            return _activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers
                ? Text(ModTextKey.PropertyCommercialDealershipEmptyCargoTrailers)
                : Text(ModTextKey.PropertyCommercialDealershipEmptyCargoRigids);
        }

        private string BuildCommercialDealershipEmptyCargoTypeDetail()
        {
            return _activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers
                ? Text(ModTextKey.PropertyCommercialDealershipEmptyCargoDetailTrailers)
                : Text(ModTextKey.PropertyCommercialDealershipEmptyCargoDetailRigids);
        }

        private void EnterCommercialDealershipSection(CommercialDealershipCatalogSection section)
        {
            _activeCommercialDealershipSection = section;
            _selectedCommercialDealershipActionIndex = 0;
            _commercialDealershipMenuView = section == CommercialDealershipCatalogSection.TruckTractors
                ? CommercialDealershipMenuView.Vehicles
                : CommercialDealershipMenuView.CargoTypes;
            RebuildCommercialDealershipMenuItems();
            if (_commercialDealershipMenuView == CommercialDealershipMenuView.CargoTypes)
            {
                SelectCommercialDealershipCargoTypeRow();
            }
            else
            {
                SelectCommercialDealershipVehicleRow(_commercialDealershipSelectedVehicleModelName);
            }
        }

        private void EnterCommercialDealershipCargoType(VehicleCargoType cargoType)
        {
            _activeCommercialDealershipCargoType = cargoType;
            _selectedCommercialDealershipCargoTypeIndex = _commercialDealershipVisibleCargoTypes.FindIndex(entry => entry == cargoType);
            if (_selectedCommercialDealershipCargoTypeIndex < 0)
            {
                _selectedCommercialDealershipCargoTypeIndex = 0;
            }

            _selectedCommercialDealershipActionIndex = 0;
            _commercialDealershipMenuView = CommercialDealershipMenuView.Vehicles;
            RebuildCommercialDealershipMenuItems();
            SelectCommercialDealershipVehicleRow(_commercialDealershipSelectedVehicleModelName);
        }

        private void EnterCommercialDealershipVehicleActions()
        {
            var definition = GetSelectedCommercialDealershipVehicleDefinition();
            if (definition == null)
            {
                ShowStatus(Text(ModTextKey.PropertyCommercialDealershipStatusSelectVehicleFirst));
                return;
            }

            _commercialDealershipSelectedVehicleModelName = definition.ModelName;
            _selectedCommercialDealershipActionIndex = 0;
            _commercialDealershipMenuView = CommercialDealershipMenuView.Actions;
            RebuildCommercialDealershipMenuItems();
            SelectCommercialDealershipActionRow();
        }

        private void ReturnFromCommercialDealershipMenu()
        {
            if (_vehicleCargoMenu != null && _vehicleCargoMenu.IsOpen && _commercialDealershipMenuView == CommercialDealershipMenuView.Actions)
            {
                _selectedCommercialDealershipActionIndex = Math.Max(0, _vehicleCargoMenu.SelectedIndex - CommercialDealershipContentListStartIndex);
            }

            if (_commercialDealershipMenuView == CommercialDealershipMenuView.Actions)
            {
                _commercialDealershipMenuView = CommercialDealershipMenuView.Vehicles;
                RebuildCommercialDealershipMenuItems();
                SelectCommercialDealershipVehicleRow(_commercialDealershipSelectedVehicleModelName);
                return;
            }

            if (_commercialDealershipMenuView == CommercialDealershipMenuView.Vehicles)
            {
                var selectedDefinition = GetSelectedCommercialDealershipVehicleDefinition();
                _commercialDealershipSelectedVehicleModelName = selectedDefinition != null ? selectedDefinition.ModelName : _commercialDealershipSelectedVehicleModelName;

                if (_activeCommercialDealershipSection == CommercialDealershipCatalogSection.TruckTractors)
                {
                    _commercialDealershipMenuView = CommercialDealershipMenuView.Root;
                    RebuildCommercialDealershipMenuItems();
                    SelectCommercialDealershipRootSectionRow();
                    return;
                }

                _commercialDealershipMenuView = CommercialDealershipMenuView.CargoTypes;
                RebuildCommercialDealershipMenuItems();
                SelectCommercialDealershipCargoTypeRow();
                return;
            }

            if (_commercialDealershipMenuView == CommercialDealershipMenuView.CargoTypes)
            {
                _commercialDealershipMenuView = CommercialDealershipMenuView.Root;
                RebuildCommercialDealershipMenuItems();
                SelectCommercialDealershipRootSectionRow();
                return;
            }

            ClearCommercialDealershipPreviewVehicle();
            _vehicleCargoMenu.Close();
        }

        private void SelectCommercialDealershipRootSectionRow()
        {
            if (_vehicleCargoMenu == null)
            {
                return;
            }

            _vehicleCargoMenu.SelectedIndex = CommercialDealershipContentListStartIndex + (int)_activeCommercialDealershipSection;
        }

        private void SelectCommercialDealershipCargoTypeRow()
        {
            if (_vehicleCargoMenu == null)
            {
                return;
            }

            _vehicleCargoMenu.SelectedIndex = CommercialDealershipContentListStartIndex + _selectedCommercialDealershipCargoTypeIndex;
        }

        private void SelectCommercialDealershipVehicleRow(string preferredModelName)
        {
            if (_vehicleCargoMenu == null)
            {
                return;
            }

            var targetIndex = CommercialDealershipContentListStartIndex;
            if (_commercialDealershipVisibleVehicles != null && _commercialDealershipVisibleVehicles.Count > 0)
            {
                var preferredIndex = !string.IsNullOrWhiteSpace(preferredModelName)
                    ? _commercialDealershipVisibleVehicles.FindIndex(definition => string.Equals(definition.ModelName, preferredModelName, StringComparison.OrdinalIgnoreCase))
                    : -1;
                if (preferredIndex >= 0)
                {
                    targetIndex = CommercialDealershipContentListStartIndex + preferredIndex;
                }
            }

            _vehicleCargoMenu.SelectedIndex = targetIndex;
        }

        private void SelectCommercialDealershipActionRow()
        {
            if (_vehicleCargoMenu == null)
            {
                return;
            }

            if (_selectedCommercialDealershipActionIndex < 0 || _selectedCommercialDealershipActionIndex > 2)
            {
                _selectedCommercialDealershipActionIndex = 0;
            }

            _vehicleCargoMenu.SelectedIndex = CommercialDealershipContentListStartIndex + _selectedCommercialDealershipActionIndex;
        }

        private void RestoreCommercialDealershipSelection()
        {
            switch (_commercialDealershipMenuView)
            {
                case CommercialDealershipMenuView.CargoTypes:
                    SelectCommercialDealershipCargoTypeRow();
                    break;
                case CommercialDealershipMenuView.Vehicles:
                    SelectCommercialDealershipVehicleRow(_commercialDealershipSelectedVehicleModelName);
                    break;
                case CommercialDealershipMenuView.Actions:
                    SelectCommercialDealershipActionRow();
                    break;
                default:
                    SelectCommercialDealershipRootSectionRow();
                    break;
            }
        }

        private VehicleDefinition GetSelectedCommercialDealershipVehicleDefinition()
        {
            if (_vehicleCargoMenu == null
                || !_vehicleCargoMenu.IsOpen
                || _commercialDealershipVisibleVehicles == null
                || _commercialDealershipVisibleVehicles.Count == 0)
            {
                return null;
            }

            if (_commercialDealershipMenuView == CommercialDealershipMenuView.Actions)
            {
                var actionDefinition = FindCommercialDealershipVehicleDefinition(_commercialDealershipSelectedVehicleModelName)
                    ?? _commercialDealershipVisibleVehicles[0];
                _commercialDealershipSelectedVehicleModelName = actionDefinition != null ? actionDefinition.ModelName : _commercialDealershipSelectedVehicleModelName;
                return actionDefinition;
            }

            if (_commercialDealershipMenuView != CommercialDealershipMenuView.Vehicles)
            {
                return null;
            }

            var selectedIndex = _vehicleCargoMenu.SelectedIndex - CommercialDealershipContentListStartIndex;
            if (selectedIndex >= 0 && selectedIndex < _commercialDealershipVisibleVehicles.Count)
            {
                var definition = _commercialDealershipVisibleVehicles[selectedIndex];
                _commercialDealershipSelectedVehicleModelName = definition != null ? definition.ModelName : _commercialDealershipSelectedVehicleModelName;
                return definition;
            }

            return FindCommercialDealershipVehicleDefinition(_commercialDealershipSelectedVehicleModelName);
        }

        private VehicleDefinition FindCommercialDealershipVehicleDefinition(string modelName)
        {
            if (_commercialDealershipVisibleVehicles == null || _commercialDealershipVisibleVehicles.Count == 0 || string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            return _commercialDealershipVisibleVehicles.FirstOrDefault(
                definition => string.Equals(definition.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        private void PurchaseSelectedCommercialDealershipVehicle()
        {
            AcquireSelectedCommercialDealershipVehicle(false);
        }

        private void RentSelectedCommercialDealershipVehicle()
        {
            AcquireSelectedCommercialDealershipVehicle(true);
        }

        private void AcquireSelectedCommercialDealershipVehicle(bool asRental)
        {
            if (_vehicleCargoMenu != null && _vehicleCargoMenu.IsOpen && _commercialDealershipMenuView == CommercialDealershipMenuView.Actions)
            {
                _selectedCommercialDealershipActionIndex = Math.Max(0, _vehicleCargoMenu.SelectedIndex - CommercialDealershipContentListStartIndex);
            }

            var definition = GetSelectedCommercialDealershipVehicleDefinition();
            if (definition == null)
            {
                ShowStatus(Text(ModTextKey.PropertyCommercialDealershipStatusSelectVehicleToAcquire));
                return;
            }

            if (IsCommercialDealershipPhantomLocked(definition))
            {
                RebuildCommercialDealershipMenuItems();
                RestoreCommercialDealershipSelection();
                ShowStatus(PhantomRoadVeteranUnlockMessage);
                return;
            }

            var selectedVehicle = GetCommercialDealershipSelectedVehicle(definition);
            var selectedTractor = GetCommercialDealershipSelectedTractor(definition);
            string purchaseMessage;
            var balance = GetCompanyBalance();
            var acquired = asRental
                ? _propertyManager.TryRentCommercialVehicle(
                    selectedVehicle,
                    selectedTractor,
                    ref balance,
                    GetCurrentInGameWeekMinute(),
                    out _,
                    out purchaseMessage)
                : _propertyManager.TryPurchaseCommercialVehicle(
                    selectedVehicle,
                    selectedTractor,
                    ref balance,
                    out _,
                    out purchaseMessage);

            SetCompanyBalance(balance);
            _commercialDealershipSelectedVehicleModelName = definition.ModelName;
            if (acquired)
            {
                _tabletStateStore.MarkBalanceDirty();
                ReevaluatePlayerSuccesses(true);
                RebuildCommercialDealershipMenuItems();
                RestoreCommercialDealershipSelection();
            }

            ShowStatus(purchaseMessage);
        }

        private float GetSelectedCommercialVehicleDailyRent()
        {
            return GetSelectedCommercialVehicleDailyRent(GetSelectedCommercialDealershipVehicleDefinition());
        }

        private static float GetSelectedCommercialVehicleDailyRent(VehicleDefinition selectedDefinition)
        {
            return Math.Max(0f, selectedDefinition != null ? selectedDefinition.DailyRent : 0f);
        }

        private string CurrentVehicleSpawnerActionCaption()
        {
            return "~b~Spawn Vehicle~s~";
        }

        private string BuildCommercialDealershipVehicleSelectionDetail()
        {
            return BuildCommercialDealershipVehicleSelectionDetail(GetSelectedCommercialDealershipVehicleDefinition());
        }

        private string BuildCommercialDealershipVehicleSelectionDetail(VehicleDefinition selectedDefinition)
        {
            if (selectedDefinition == null)
            {
                if (_commercialDealershipMenuView == CommercialDealershipMenuView.Root)
                {
                    return Text(ModTextKey.PropertyCommercialDealershipVehicleSelectionRoot);
                }

                if (_commercialDealershipMenuView == CommercialDealershipMenuView.CargoTypes)
                {
                    return _activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers
                        ? Text(ModTextKey.PropertyCommercialDealershipVehicleSelectionCargoTrailer)
                        : Text(ModTextKey.PropertyCommercialDealershipVehicleSelectionCargoRigids);
                }

                return BuildCommercialDealershipEmptyVehicleCaption();
            }

            _commercialDealershipSelectedVehicleModelName = selectedDefinition.ModelName;

            var purchaseQuote = GetSelectedCommercialVehiclePurchaseQuote(selectedDefinition);
            var vehiclePrice = purchaseQuote != null
                ? purchaseQuote.EffectivePrice
                : Math.Max(0f, selectedDefinition.Price);
            var detail = Text(ModTextKey.PropertyCommercialDealershipVehicleSelectionOpenActions, BuildCommercialDealershipVehicleSpecs(selectedDefinition, vehiclePrice));
            if (selectedDefinition.IsTrailer)
            {
                return Text(ModTextKey.PropertyCommercialDealershipVehicleSelectionTrailerOnly, detail);
            }

            return AppendCommercialVehiclePurchaseEntitlementNote(detail, purchaseQuote);
        }

        private string BuildCommercialDealershipVehicleSpecs(VehicleDefinition definition, float purchasePrice)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return Text(
                ModTextKey.PropertyCommercialDealershipVehicleSpecs,
                ModFormatting.FormatMoney(Math.Max(0f, purchasePrice)),
                ModFormatting.FormatTons(Math.Max(0f, definition.CapacityTons)),
                ModFormatting.FormatLiters(Math.Max(0f, definition.FuelCapacityLiters)),
                ModFormatting.FormatMoney(Math.Max(0f, definition.DailyRent)));
        }

        private string BuildCommercialDealershipPurchaseActionDetail(VehicleDefinition selectedDefinition)
        {
            if (selectedDefinition == null)
            {
                return BuildCommercialDealershipSelectionPrompt();
            }

            if (IsCommercialDealershipPhantomLocked(selectedDefinition))
            {
                return PhantomRoadVeteranUnlockMessage;
            }

            var purchaseQuote = GetSelectedCommercialVehiclePurchaseQuote(selectedDefinition);
            var price = purchaseQuote != null
                ? purchaseQuote.EffectivePrice
                : Math.Max(0f, selectedDefinition.Price);
            return AppendCommercialVehiclePurchaseEntitlementNote(
                Text(ModTextKey.PropertyCommercialDealershipPurchaseDetail, ModFormatting.FormatMoney(price)),
                purchaseQuote);
        }

        private string BuildCommercialDealershipRentActionDetail(VehicleDefinition selectedDefinition)
        {
            if (selectedDefinition == null)
            {
                return BuildCommercialDealershipSelectionPrompt();
            }

            if (IsCommercialDealershipPhantomLocked(selectedDefinition))
            {
                return PhantomRoadVeteranUnlockMessage;
            }

            var dailyRent = GetSelectedCommercialVehicleDailyRent(selectedDefinition);
            if (dailyRent <= 0.001f)
            {
                return BuildCommercialDealershipRentUnavailableDetail(selectedDefinition);
            }

            return Text(
                ModTextKey.PropertyCommercialDealershipRentDetail,
                ModFormatting.FormatMoney(dailyRent));
        }

        private string BuildCommercialDealershipRentUnavailableDetail()
        {
            return BuildCommercialDealershipRentUnavailableDetail(GetSelectedCommercialDealershipVehicleDefinition());
        }

        private string BuildCommercialDealershipRentUnavailableDetail(VehicleDefinition selectedDefinition)
        {
            if (selectedDefinition == null)
            {
                return BuildCommercialDealershipSelectionPrompt();
            }

            return Text(
                ModTextKey.PropertyCommercialDealershipRentUnavailable,
                GetCommercialDealershipVehicleLabel(selectedDefinition),
                ModFormatting.FormatMoney(Math.Max(0f, selectedDefinition.DailyRent)));
        }

        private string BuildCommercialDealershipPurchaseDetail()
        {
            return BuildCommercialDealershipPurchaseActionDetail(GetSelectedCommercialDealershipVehicleDefinition());
        }

        private CommercialVehiclePurchaseQuote GetSelectedCommercialVehiclePurchaseQuote(VehicleDefinition selectedDefinition)
        {
            return _propertyManager.GetCommercialVehiclePurchaseQuote(
                GetCommercialDealershipSelectedVehicle(selectedDefinition),
                GetCommercialDealershipSelectedTractor(selectedDefinition));
        }

        private string AppendCommercialVehiclePurchaseEntitlementNote(string detail, CommercialVehiclePurchaseQuote purchaseQuote)
        {
            if (purchaseQuote == null || !purchaseQuote.UsesFirstFreeEntitlement)
            {
                return detail;
            }

            return Text(
                ModTextKey.PropertyCommercialDealershipPurchaseEntitlement,
                detail,
                PropertyManager.GetCommercialVehiclePurchaseEntitlementLabel(purchaseQuote.EntitlementFamily));
        }

        private static VehicleDefinition GetCommercialDealershipSelectedVehicle(VehicleDefinition selectedDefinition)
        {
            return selectedDefinition != null && !selectedDefinition.IsTractor
                ? selectedDefinition
                : null;
        }

        private static VehicleDefinition GetCommercialDealershipSelectedTractor(VehicleDefinition selectedDefinition)
        {
            return selectedDefinition != null && selectedDefinition.IsTractor
                ? selectedDefinition
                : null;
        }

        private string BuildCommercialDealershipVehicleButtonCaption(VehicleDefinition definition)
        {
            if (definition == null)
            {
                return Text(ModTextKey.PropertyCommercialDealershipUnknownVehicle);
            }

            var purchaseQuote = GetSelectedCommercialVehiclePurchaseQuote(definition);
            var purchasePrice = purchaseQuote != null
                ? purchaseQuote.EffectivePrice
                : Math.Max(0f, definition.Price);
            return Text(ModTextKey.PropertyPersonalDealershipVehicleCaption, GetCommercialDealershipVehicleLabel(definition), ModFormatting.FormatMoney(purchasePrice));
        }

        private string BuildCommercialDealershipEmptyVehicleCaption()
        {
            switch (_activeCommercialDealershipSection)
            {
                case CommercialDealershipCatalogSection.TruckTractors:
                    return Text(ModTextKey.PropertyCommercialDealershipEmptyVehicleTractors);
                case CommercialDealershipCatalogSection.Trailers:
                    return Text(ModTextKey.PropertyCommercialDealershipEmptyVehicleTrailers, _activeCommercialDealershipCargoType.ToDisplayName());
                default:
                    return Text(ModTextKey.PropertyCommercialDealershipEmptyVehicleRigids, _activeCommercialDealershipCargoType.ToDisplayName());
            }
        }

        private string BuildCommercialDealershipSelectionPrompt()
        {
            switch (_commercialDealershipMenuView)
            {
                case CommercialDealershipMenuView.Root:
                    return Text(ModTextKey.PropertyCommercialDealershipSelectionPromptRoot);
                case CommercialDealershipMenuView.CargoTypes:
                    return _activeCommercialDealershipSection == CommercialDealershipCatalogSection.Trailers
                        ? Text(ModTextKey.PropertyCommercialDealershipSelectionPromptCargoTrailer)
                        : Text(ModTextKey.PropertyCommercialDealershipSelectionPromptCargoRigids);
                case CommercialDealershipMenuView.Actions:
                    return Text(ModTextKey.PropertyCommercialDealershipSelectionPromptActions);
                default:
                    return Text(ModTextKey.PropertyCommercialDealershipSelectionPromptVehicle);
            }
        }

        private void UpdateCommercialDealershipPreview()
        {
            var definition = GetSelectedCommercialDealershipVehicleDefinition();
            if (definition == null)
            {
                ClearCommercialDealershipPreviewVehicle();
                return;
            }

            _commercialDealershipSelectedVehicleModelName = definition.ModelName;
            EnsureCommercialDealershipPreviewVehicle(definition);
        }

        private void EnsureCommercialDealershipPreviewVehicle(VehicleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ModelName))
            {
                ClearCommercialDealershipPreviewVehicle();
                return;
            }

            if (_commercialDealershipPreviewVehicle != null
                && _commercialDealershipPreviewVehicle.Exists()
                && string.Equals(_commercialDealershipPreviewModelName, definition.ModelName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ClearCommercialDealershipPreviewVehicle();

            var model = new Model(definition.ModelName);
            if (!model.Request(500))
            {
                model.MarkAsNoLongerNeeded();
                return;
            }

            var vehicle = World.CreateVehicle(model, CommercialDealershipVehiclePadPosition, CommercialDealershipVehiclePadHeading);
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

            _commercialDealershipPreviewVehicle = vehicle;
            _commercialDealershipPreviewModelName = definition.ModelName;
        }

        private void ClearCommercialDealershipPreviewVehicle()
        {
            if (_commercialDealershipPreviewVehicle != null)
            {
                try
                {
                    if (_commercialDealershipPreviewVehicle.Exists())
                    {
                        _commercialDealershipPreviewVehicle.Delete();
                    }
                }
                catch
                {
                    // Preview cleanup should be best-effort only.
                }
            }

            _commercialDealershipPreviewVehicle = null;
            _commercialDealershipPreviewModelName = null;
        }

        private string GetCommercialDealershipVehicleLabel(VehicleDefinition definition)
        {
            return definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition != null ? definition.ModelName : Text(ModTextKey.PropertyCommercialDealershipUnknownVehicle)
                : definition.DisplayName;
        }

        private enum CommercialGarageMenuContext
        {
            Office = 0,
            Industry = 1,
        }
    }
}
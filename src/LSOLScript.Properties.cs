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
        private const float ApartmentExteriorMarkerScale = 1.3f;
        private const float ApartmentInteriorMarkerScale = 1.2f;
        private const float DealershipInteractionDistance = 4.6f;

        private static readonly Vector3 CommercialDealershipMarker = new Vector3(-979.56f, -2232.48f, 8.86f);
        private static readonly Vector3 PersonalDealershipMarker = new Vector3(38.68f, -1109.47f, 26.44f);

        private LemonMenu _commercialGarageMenu;
        private LemonMenu _commercialGarageActionMenu;
        private LemonMenu _officeObjectsMenu;
        private LemonMenu _officeObjectPurchaseMenu;
        private LemonMenu _apartmentMenu;
        private LemonMenu _apartmentInteriorMenu;
        private LemonMenu _personalGarageMenu;
        private LemonMenu _personalDealershipMenu;
        private OfficeDefinition _menuOffice;
        private InteriorDefinition _menuApartment;
        private CommercialGarageMenuContext _commercialGarageMenuContext;
        private OwnedCommercialVehiclePersistenceEntry _selectedCommercialGarageVehicle;
        private CommercialDealershipAcquisitionMode _commercialDealershipAcquisitionMode;
        private List<OfficeObjectDefinition> _officeObjectPreviewSlots;
        private OfficeObjectDefinition _pendingOfficeObjectPurchaseDefinition;
        private ApartmentSleepTransitionPhase _apartmentSleepTransitionPhase;
        private int _apartmentSleepTransitionPhaseStartedAt;
        private bool _apartmentSleepClockApplied;

        private enum ApartmentSleepTransitionPhase
        {
            None = 0,
            FadingOut = 1,
            HoldingBlack = 2,
            FadingIn = 3,
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
                Subtitle = "Buy modules, haul them from the port, and place decorative props at the active office",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _officeObjectPurchaseMenu = new LemonMenu("Confirm Purchase")
            {
                Subtitle = "Review price, office limits, and placement requirements",
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
        }

        private bool HasPropertyMenuOpen()
        {
            return (_commercialGarageMenu != null && _commercialGarageMenu.IsOpen)
                || (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
                || (_officeObjectsMenu != null && _officeObjectsMenu.IsOpen)
                || (_officeObjectPurchaseMenu != null && _officeObjectPurchaseMenu.IsOpen)
                || (_apartmentMenu != null && _apartmentMenu.IsOpen)
                || (_apartmentInteriorMenu != null && _apartmentInteriorMenu.IsOpen)
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

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Draw();
            }

            if (_apartmentInteriorMenu != null)
            {
                _apartmentInteriorMenu.Draw();
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

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Close();
            }

            if (_apartmentInteriorMenu != null)
            {
                _apartmentInteriorMenu.Close();
            }

            if (_personalGarageMenu != null)
            {
                _personalGarageMenu.Close();
            }

            if (_personalDealershipMenu != null)
            {
                _personalDealershipMenu.Close();
            }
        }

        private bool HandlePropertyMenuKey(WinForms.Keys key)
        {
            if (_personalDealershipMenu != null && _personalDealershipMenu.IsOpen)
            {
                _personalDealershipMenu.HandleKey(key, _controls);
                return true;
            }

            if (_personalGarageMenu != null && _personalGarageMenu.IsOpen)
            {
                if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    ReturnToApartmentMenu();
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

        private void DrawPropertyMarkers(Ped player, bool canShowPrompts, ref bool promptShown)
        {
            var playerPos = player.Position;
            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            var apartmentExteriorInteractionDistance = GetApartmentExteriorInteractionDistance();
            var apartmentInteriorInteractionDistance = GetApartmentInteriorInteractionDistance();
            var activeOffice = _propertyManager.ActiveOffice;
            var activeApartment = _propertyManager.ActiveApartment;

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

        private float GetApartmentInteriorInteractionDistance()
        {
            return _config.MarkerRadius * ApartmentInteriorMarkerScale;
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
                    CaptionFactory = () => "Refuel Vehicle",
                    DetailFactory = BuildOfficeVehicleRefuelDetail,
                    OnActivate = RefuelVehicleFromOfficeTank,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Unload Fuel Cargo",
                    DetailFactory = BuildOfficeFuelUnloadDetail,
                    OnActivate = UnloadFuelCargoIntoOfficeTank,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Request Diesel Delivery",
                    DetailFactory = BuildOfficeFuelDeliveryDetail,
                    OnActivate = RequestOfficeFuelDelivery,
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
            var catalogCount = _propertyManager.OfficeObjectCatalog.Count;
            return string.Format("Catalog {0} | Placed {1} | Pending placement {2}", catalogCount, placed, pending);
        }

        private string BuildOfficeVehicleRefuelDetail()
        {
            if (_menuOffice == null || !string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to refuel company vehicles from its diesel tank.";
            }

            var tank = _propertyManager.GetFirstPlacedOfficeObjectByFunction(_menuOffice.OfficeId, OfficeObjectFunction.Refuel);
            var definition = tank != null ? _propertyManager.GetOfficeObjectDefinition(tank.DefinitionId) : null;
            if (tank == null || definition == null)
            {
                return "Install a Diesel Tank first.";
            }

            return string.Format("Stored diesel: {0}.", ModFormatting.FormatRatio(Math.Max(0f, tank.StoredResourceAmount), definition.Capacity, "L"));
        }

        private string BuildOfficeFuelUnloadDetail()
        {
            if (_menuOffice == null || !string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to unload fuel cargo into its diesel tank.";
            }

            var tank = _propertyManager.GetFirstPlacedOfficeObjectByFunction(_menuOffice.OfficeId, OfficeObjectFunction.Refuel);
            if (tank == null)
            {
                return "Install a Diesel Tank first.";
            }

            return "Unload Fuel cargo from an office truck into the active office tank.";
        }

        private string BuildOfficeFuelDeliveryDetail()
        {
            if (_menuOffice == null || !string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to request refinery diesel delivery.";
            }

            if (_officeObjectManager.HasActiveFuelDelivery && string.Equals(_officeObjectManager.ActiveFuelDeliveryOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "A refinery tanker is already en route to this office.";
            }

            var tank = _propertyManager.GetFirstPlacedOfficeObjectByFunction(_menuOffice.OfficeId, OfficeObjectFunction.Refuel);
            var definition = tank != null ? _propertyManager.GetOfficeObjectDefinition(tank.DefinitionId) : null;
            if (tank == null || definition == null)
            {
                return "Install a Diesel Tank first.";
            }

            var freeLiters = Math.Max(0f, definition.Capacity - tank.StoredResourceAmount);
            return freeLiters <= 0.05f
                ? "The office diesel tank is already full."
                : string.Format("Dispatch a refinery tanker to deliver up to {0} at a service premium.", ModFormatting.FormatLiters(freeLiters));
        }

        private string BuildOfficeRepairDetail()
        {
            if (_menuOffice == null || !string.Equals(_propertyManager.ActiveOfficeId, _menuOffice.OfficeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Activate this office to use its Maintenance Bay.";
            }

            return _propertyManager.HasOfficeObjectFunction(_menuOffice.OfficeId, OfficeObjectFunction.Repair)
                ? "Repair the active office truck and trailer for free."
                : "Install a Maintenance Bay first.";
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

            _officeObjectsMenu.Title = _menuOffice != null ? string.Format("{0} Objects", _menuOffice.DisplayName) : "Office Objects";
            _officeObjectsMenu.Subtitle = "Buy modules, haul them from the port, and place decorative props at the active office";
            _officeObjectsMenu.SetItems(items);
        }

        private string BuildOfficeObjectCaption(OfficeObjectDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var category = definition.IsFunctional ? "Module" : "Decor";
            return string.Format("[{0}] {1}", category, definition.DisplayName);
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
            var limitLabel = definition.PerOfficeLimit > 0 ? string.Format("Limit {0}", definition.PerOfficeLimit) : "No office limit";
            var functionLabel = definition.IsFunctional
                ? string.Format("{0} | Port haul required", BuildOfficeObjectFunctionLabel(definition))
                : "Decorative placement";
            return string.Format(
                "{0} | {1} | Price {2} | Placed {3} | Pending {4}",
                limitLabel,
                functionLabel,
                ModFormatting.FormatMoney(definition.Price),
                placed,
                pending);
        }

        private string BuildOfficeObjectFunctionLabel(OfficeObjectDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            switch (definition.Function)
            {
                case OfficeObjectFunction.Refuel:
                    return string.Format("Diesel storage {0}", ModFormatting.FormatLiters(Math.Max(0f, definition.Capacity)));
                case OfficeObjectFunction.Repair:
                    return "Repairs office trucks and trailers";
                case OfficeObjectFunction.Npc:
                    return string.Format("Supports {0:0} hired NPCs", Math.Max(0f, definition.Capacity));
                default:
                    return definition.Function.ToString();
            }
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
                CaptionFactory = () => definition.DisplayName,
                DetailFactory = () => BuildOfficeObjectDetail(definition),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => canPurchase ? string.Format("Purchase for {0}", ModFormatting.FormatMoney(definition.Price)) : "Purchase blocked",
                DetailFactory = () => canPurchase
                    ? (definition.IsFunctional
                        ? "Buy now, then haul it from the port to the active office before placement."
                        : "Buy now and immediately enter placement mode at the active office.")
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

            if (definition.PerOfficeLimit > 0 && _propertyManager.GetOfficeObjectCount(_menuOffice.OfficeId, definition.ObjectId) >= definition.PerOfficeLimit)
            {
                blockedReason = string.Format("{0} limit reached at this office.", definition.DisplayName);
                return false;
            }

            if (_profit + 0.001f < definition.Price)
            {
                blockedReason = string.Format("Need {0} to purchase {1}.", ModFormatting.FormatMoney(definition.Price), definition.DisplayName);
                return false;
            }

            return true;
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
                ShowStatus(!string.IsNullOrWhiteSpace(prefixMessage)
                    ? string.Format("{0} {1}", prefixMessage, placementMessage)
                    : placementMessage);
                ReturnToOfficeObjectsMenu();
                return;
            }

            _officeObjectPurchaseMenu.Close();
            _officeObjectsMenu.Close();
            ShowStatus(!string.IsNullOrWhiteSpace(prefixMessage)
                ? string.Format("{0} {1}", prefixMessage, placementMessage)
                : placementMessage);
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
            _officeObjectManager.TryRepairPlayerVehicleAtOffice(Game.Player.Character, out message);
            ShowStatus(message);
        }

        private void RentSelectedOffice()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryRentOffice(_menuOffice.OfficeId, ref _profit, GetCurrentInGameWeekMinute(), out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildOfficeMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void TransferSelectedOfficeRental()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryTransferOfficeRental(_menuOffice.OfficeId, ref _profit, GetCurrentInGameWeekMinute(), out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                _tabletStateStore.MarkNetworkDirty();
                RebuildOfficeMenuItems();
                RebuildCommercialGarageMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void PurchaseSelectedOffice()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryPurchaseOffice(_menuOffice.OfficeId, ref _profit, GetCurrentInGameWeekMinute(), out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildOfficeMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void ActivateSelectedOffice()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryActivateOffice(_menuOffice.OfficeId, out message))
            {
                _tabletStateStore.MarkNetworkDirty();
                RebuildOfficeMenuItems();
                RebuildCommercialGarageMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void RelinquishSelectedOfficeRental()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryRelinquishOfficeRental(_menuOffice.OfficeId, out message))
            {
                _tabletStateStore.MarkNetworkDirty();
                RebuildOfficeMenuItems();
                RebuildCommercialGarageMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void PaySelectedOfficeArrears()
        {
            if (_menuOffice == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryPayOfficeArrears(_menuOffice.OfficeId, ref _profit, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildOfficeMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
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
            var cargo = string.IsNullOrWhiteSpace(vehicle.Commodity)
                ? "Empty"
                : string.Format("{0} {1}", vehicle.Commodity, ModFormatting.FormatRatio(vehicle.WeightTons, Math.Max(0f, vehicle.CapacityTons), "t"));
            var npcAssignment = BuildCommercialVehicleNpcAssignmentDetail(vehicle);
            return string.IsNullOrWhiteSpace(npcAssignment)
                ? string.Format("{0} | {1} | {2} | {3}", location, deployed, acquisition, cargo)
                : string.Format("{0} | {1} | {2} | {3} | {4}", location, deployed, acquisition, npcAssignment, cargo);
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
                        ? string.Format("Close the rental and refund {0}.", ModFormatting.FormatMoney(Math.Max(0f, vehicle.DailyRent * 2f)))
                        : string.Format("Sell this vehicle back for {0}.", ModFormatting.FormatMoney(Math.Max(0f, vehicle.PurchasePrice * 0.5f))),
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
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Personal Garage",
                    DetailFactory = BuildPersonalGarageSummary,
                    OnActivate = OpenPersonalGarageMenu,
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

        private void PurchaseSelectedApartment()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryPurchaseApartment(_menuApartment.InteriorId, ref _profit, GetCurrentInGameWeekMinute(), out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void RentSelectedApartment()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryRentApartment(_menuApartment.InteriorId, ref _profit, GetCurrentInGameWeekMinute(), out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void ActivateSelectedApartment()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryActivateApartment(_menuApartment.InteriorId, out message))
            {
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void CancelSelectedApartmentRental()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryCancelApartmentRental(_menuApartment.InteriorId, out message))
            {
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void SellSelectedApartment()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TrySellApartment(_menuApartment.InteriorId, ref _profit, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
        }

        private void PaySelectedApartmentArrears()
        {
            if (_menuApartment == null)
            {
                return;
            }

            string message;
            if (_propertyManager.TryPayApartmentArrears(_menuApartment.InteriorId, ref _profit, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildApartmentMenuItems();
                ReevaluatePlayerSuccesses(true);
            }

            ShowStatus(message);
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
            if (IsApartmentSleepTransitionActive)
            {
                return;
            }

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

            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                ShowStatus(string.Format("You can sleep again in {0}.", FormatApartmentSleepCooldown(remainingCooldownMinutes)));
                return;
            }

            StartApartmentSleepTransition(player, Game.GameTime);
        }

        private string BuildApartmentSleepMenuDetail()
        {
            var remainingCooldownMinutes = GetApartmentSleepCooldownRemainingMinutes(GetCurrentInGameWeekMinute());
            if (remainingCooldownMinutes > 0)
            {
                return string.Format("Available again in {0}.", FormatApartmentSleepCooldown(remainingCooldownMinutes));
            }

            return string.Format("Advance the city clock by {0} hours and stay inside.", ApartmentSleepHours);
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

            _apartmentMenu.Close();
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
            if (_propertyManager.IsPersonalVehicleDeployed(vehicle.AssetId))
            {
                _propertyManager.TryStorePersonalVehicle(vehicle.AssetId, out message);
            }
            else
            {
                _propertyManager.TryDeployPersonalVehicle(vehicle.AssetId, apartment.GaragePosition, 0f, out message);
            }

            RebuildPersonalGarageMenuItems();
            ShowStatus(message);
        }

        private void ReturnToApartmentMenu()
        {
            _personalGarageMenu.Close();
            RebuildApartmentMenuItems();
            _apartmentMenu.Open();
        }

        private void OpenPersonalDealershipMenu()
        {
            CloseAllMenus();
            RebuildPersonalDealershipMenuItems();
            _personalDealershipMenu.Open();
        }

        private void RebuildPersonalDealershipMenuItems()
        {
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

            for (int i = 0; i < _propertyManager.PersonalVehicleCatalog.Count; i++)
            {
                var definition = _propertyManager.PersonalVehicleCatalog[i];
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => string.Format("{0} - {1}", definition.DisplayName, ModFormatting.FormatMoney(definition.Price)),
                    DetailFactory = () => string.IsNullOrWhiteSpace(definition.Category) ? "Personal vehicle" : definition.Category,
                    OnActivate = () => PurchasePersonalVehicle(definition),
                });
            }

            if (_propertyManager.PersonalVehicleCatalog.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No dealership catalog loaded",
                    DetailFactory = () => "dealership.xml is missing or invalid.",
                });
            }

            _personalDealershipMenu.SetItems(items);
        }

        private void PurchasePersonalVehicle(DealershipVehicleDefinition definition)
        {
            string message;
            if (_propertyManager.TryPurchasePersonalVehicle(definition, ref _profit, out _, out message))
            {
                _tabletStateStore.MarkBalanceDirty();
                RebuildPersonalDealershipMenuItems();
            }

            ShowStatus(message);
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
                "Rent charges {0}/day. First day plus a refundable 2-day deposit is collected upfront.",
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

            var upfrontCost = dailyRent * 3f;
            return string.Format(
                "Rent for {0}/day. First day plus a refundable deposit totals {1} upfront.",
                ModFormatting.FormatMoney(dailyRent),
                ModFormatting.FormatMoney(upfrontCost));
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
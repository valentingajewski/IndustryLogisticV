using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
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
        private const float ApartmentInteriorInteractionDistance = 3.2f;
        private const float DealershipInteractionDistance = 4.6f;

        private static readonly Vector3 CommercialDealershipMarker = new Vector3(-979.56f, -2232.48f, 8.86f);
        private static readonly Vector3 PersonalDealershipMarker = new Vector3(38.68f, -1109.47f, 26.44f);

        private LemonMenu _commercialGarageMenu;
        private LemonMenu _commercialGarageActionMenu;
        private LemonMenu _apartmentMenu;
        private LemonMenu _personalGarageMenu;
        private LemonMenu _personalDealershipMenu;
        private OfficeDefinition _menuOffice;
        private InteriorDefinition _menuApartment;
        private CommercialGarageMenuContext _commercialGarageMenuContext;
        private OwnedCommercialVehiclePersistenceEntry _selectedCommercialGarageVehicle;
        private CommercialDealershipAcquisitionMode _commercialDealershipAcquisitionMode;

        private void InitializePropertyMenus()
        {
            _commercialGarageMenu = new LemonMenu("Commercial Garage")
            {
                Subtitle = "Retrieve, store, and swap vehicles",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _commercialGarageActionMenu = new LemonMenu("Garage Vehicle")
            {
                Subtitle = "Retrieve, store, reserve, or close the contract",
                AlignRight = true,
                MaxVisibleItems = 10,
            };
            _apartmentMenu = new LemonMenu("Apartment")
            {
                Subtitle = "Manage residence access and personal storage",
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
        }

        private bool HasPropertyMenuOpen()
        {
            return (_commercialGarageMenu != null && _commercialGarageMenu.IsOpen)
                || (_commercialGarageActionMenu != null && _commercialGarageActionMenu.IsOpen)
                || (_apartmentMenu != null && _apartmentMenu.IsOpen)
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

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Draw();
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

            if (_apartmentMenu != null)
            {
                _apartmentMenu.Close();
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

            if (_apartmentMenu != null && _apartmentMenu.IsOpen)
            {
                _apartmentMenu.HandleKey(key, _controls);
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
                    new Vector3(_config.MarkerRadius * 1.3f, _config.MarkerRadius * 1.3f, _config.MarkerHeight),
                    isActive ? Color.FromArgb(210, 220, 188, 84) : Color.FromArgb(205, 188, 134, 82),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(apartment.ExteriorPosition) <= OfficeInteractionDistance)
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
                    new Vector3(_config.MarkerRadius * 1.2f, _config.MarkerRadius * 1.2f, _config.MarkerHeight),
                    Color.FromArgb(205, 234, 196, 110),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPos.DistanceTo(activeApartment.InteriorPosition) <= ApartmentInteriorInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format("Press {0} to leave the apartment.", KeyName(_controls.Interact))));
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
            if (activeApartment != null && player.Position.DistanceTo(activeApartment.InteriorPosition) <= ApartmentInteriorInteractionDistance)
            {
                ExitActiveApartment();
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
            for (int i = 0; i < _propertyManager.Interiors.Count; i++)
            {
                var apartment = _propertyManager.Interiors[i];
                if (apartment != null && position.DistanceTo(apartment.ExteriorPosition) <= OfficeInteractionDistance)
                {
                    return apartment;
                }
            }

            return null;
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
            var isActiveOffice = office != null && string.Equals(_propertyManager.ActiveOfficeId, office.OfficeId, StringComparison.OrdinalIgnoreCase);
            var isOwned = officeState != null && officeState.IsOwned;
            var hasAccess = office != null && officeState != null && (officeState.IsOwned || officeState.IsRented);
            var hasArrears = officeState != null && officeState.OutstandingRent > 0.01f;

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
                    CaptionFactory = () => "Rent Office",
                    DetailFactory = () => string.Format("Pay {0} to unlock access at this office.", ModFormatting.FormatMoney(office.WeeklyOfficeRent)),
                    OnActivate = RentSelectedOffice,
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

            if (!hasAccess)
            {
                return items;
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
                RebuildOfficeMenuItems();
                RebuildCommercialGarageMenuItems();
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
                : string.Format("{0} {1:0.0}/{2:0.0}t", vehicle.Commodity, vehicle.WeightTons, Math.Max(0f, vehicle.CapacityTons));
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

            var contracts = _npcLogisticsManager.Contracts;
            if (contracts == null || contracts.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < contracts.Count; i++)
            {
                var contract = contracts[i];
                if (contract == null)
                {
                    continue;
                }

                var usesVehicle = contract.Routes != null && contract.Routes.Count > 0
                    ? contract.Routes.Any(route => route != null
                        && !string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId)
                        && string.Equals(route.AssignedVehicleAssetId, vehicle.AssetId, StringComparison.OrdinalIgnoreCase))
                    : !string.IsNullOrWhiteSpace(contract.AssignedVehicleAssetId)
                        && string.Equals(contract.AssignedVehicleAssetId, vehicle.AssetId, StringComparison.OrdinalIgnoreCase);
                if (usesVehicle)
                {
                    return contract;
                }
            }

            return null;
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
                    HandleCommercialVehicleActivate(vehicle);
                    return;
                }

                RebuildCommercialGarageMenuItems();
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
                _commercialGarageActionMenu.Title = "Garage Vehicle";
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

            _commercialGarageActionMenu.Title = vehicle.DisplayName;
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

        private void RebuildApartmentMenuItems()
        {
            var items = new List<OfficeMenuItem>();
            var apartment = _menuApartment;
            var apartmentState = apartment != null ? _propertyManager.GetApartmentState(apartment.InteriorId) : null;
            var isActiveApartment = apartment != null && string.Equals(_propertyManager.ActiveApartmentId, apartment.InteriorId, StringComparison.OrdinalIgnoreCase);
            var hasAccess = apartmentState != null && apartmentState.IsOwned;
            var hasArrears = apartmentState != null && apartmentState.OutstandingRent > 0.01f;

            _apartmentMenu.Title = apartment != null ? apartment.DisplayName : "Apartment";
            _apartmentMenu.Subtitle = apartment != null
                ? string.Format("Weekly rent {0} | Purchase {1}", ModFormatting.FormatMoney(apartment.InteriorWeeklyRent), ModFormatting.FormatMoney(apartment.InteriorPrice))
                : "Manage residence access and personal storage";

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_profit)),
                DetailFactory = () => apartment != null ? (apartment.InteriorIgName ?? apartment.InteriorType ?? string.Empty) : string.Empty,
            });

            if (apartment == null)
            {
                _apartmentMenu.SetItems(items);
                return;
            }

            if (!hasAccess)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Purchase Apartment",
                    DetailFactory = () => string.Format("Pay {0} to purchase this apartment.", ModFormatting.FormatMoney(apartment.InteriorPrice)),
                    OnActivate = PurchaseSelectedApartment,
                });
                _apartmentMenu.SetItems(items);
                return;
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
            ShowStatus(string.Format("Entered {0}.", _menuApartment.InteriorIgName));
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
            ShowStatus(string.Format("Exited {0}.", apartment.DisplayName));
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
                return "Rental is not configured for the selected vehicle.";
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

            var vehiclePrice = ModFormatting.FormatMoney(Math.Max(0f, selectedVehicle.Price));
            var dailyRent = GetSelectedCommercialVehicleDailyRent();
            if (!selectedVehicle.IsTrailer)
            {
                return dailyRent > 0.001f
                    ? string.Format("Vehicle price {0} | Rent {1}/day.", vehiclePrice, ModFormatting.FormatMoney(dailyRent))
                    : string.Format("Vehicle price {0}.", vehiclePrice);
            }

            var selectedTractor = _vehicleSpawnController.SelectedTractorDefinition;
            if (selectedTractor == null)
            {
                return dailyRent > 0.001f
                    ? string.Format("Trailer only. Price {0} | Rent {1}/day. Set Truck to None to keep it standalone.", vehiclePrice, ModFormatting.FormatMoney(dailyRent))
                    : string.Format("Trailer only. Price {0}. Set Truck to None to keep it standalone.", vehiclePrice);
            }

            var totalPrice = Math.Max(0f, selectedVehicle.Price) + Math.Max(0f, selectedTractor.Price);
            return dailyRent > 0.001f
                ? string.Format(
                    "Trailer price {0} | Total with truck {1} | Rent {2}/day.",
                    vehiclePrice,
                    ModFormatting.FormatMoney(totalPrice),
                    ModFormatting.FormatMoney(dailyRent))
                : string.Format(
                    "Trailer price {0} | Total with truck {1}.",
                    vehiclePrice,
                    ModFormatting.FormatMoney(totalPrice));
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
                return "Rental is not configured for this vehicle.";
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
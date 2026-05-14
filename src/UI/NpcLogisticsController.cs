using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSOL;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    public sealed class NpcLogisticsController
    {
        private static readonly int[] TriggerThresholdOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        private sealed class DraftRouteConfig
        {
            public bool IsEnabled { get; set; }

            public Industry OriginIndustry { get; set; }

            public Industry DestinationIndustry { get; set; }

            public string Commodity { get; set; }

            public string AssignedVehicleAssetId { get; set; }

            public string AssignedVehicleDisplayName { get; set; }

            public int OriginTriggerThresholdPercent { get; set; }

            public int DestinationTriggerThresholdPercent { get; set; } = 100;
        }

        private readonly ControlBindings _controls;
        private readonly NpcLogisticsManager _manager;
        private readonly Action _reopenOfficeMenu;
        private readonly Func<string> _getHireBlockedReason;
        private readonly Action<string> _showStatus;

        private readonly SimpleMenu _rootMenu;
        private readonly SimpleMenu _hireMenu;
        private readonly SimpleMenu _contractListMenu;
        private readonly SimpleMenu _contractActionMenu;
        private readonly List<DraftRouteConfig> _draftRoutes;

        private List<Industry> _originOptions;
        private List<Industry> _destinationOptions;
        private List<string> _resourceOptions;
        private List<OwnedCommercialVehiclePersistenceEntry> _garageVehicleOptions;
        private int _selectedOriginIndex;
        private int _selectedDestinationIndex;
        private int _selectedResourceIndex;
        private int _selectedTierIndex;
        private int _selectedGarageVehicleIndex;
        private int _selectedOriginTriggerIndex;
        private int _selectedDestinationTriggerIndex;
        private int _selectedRouteSlotIndex;
        private NpcLogisticsContract _selectedContract;
        private NpcLogisticsContract _editingContract;

        public NpcLogisticsController(
            ControlBindings controls,
            NpcLogisticsManager manager,
            Action reopenOfficeMenu,
            Func<string> getHireBlockedReason,
            Action<string> showStatus)
        {
            _controls = controls;
            _manager = manager;
            _reopenOfficeMenu = reopenOfficeMenu;
            _getHireBlockedReason = getHireBlockedReason;
            _showStatus = showStatus;

            _rootMenu = CreateMenu("Hire NPC", "Automate logistics between industries", 0.70f);
            _hireMenu = CreateMenu("Hire NPC", "Configure an automated logistics route", 0.88f);
            _contractListMenu = CreateMenu("NPC List", "Manage active NPC contracts", 0.92f);
            _contractActionMenu = CreateMenu("NPC Contract", "Modify or fire the selected route", 0.82f);

            _originOptions = new List<Industry>();
            _destinationOptions = new List<Industry>();
            _resourceOptions = new List<string>();
            _garageVehicleOptions = new List<OwnedCommercialVehiclePersistenceEntry>();
            _draftRoutes = CreateEmptyDraftRoutes();
        }

        public bool AnyMenuOpen
        {
            get
            {
                return _rootMenu.IsOpen
                    || _hireMenu.IsOpen
                    || _contractListMenu.IsOpen
                    || _contractActionMenu.IsOpen;
            }
        }

        public void OpenRootMenu()
        {
            Close();
            RebuildRootMenuItems();
            _rootMenu.Open();
        }

        public void Close()
        {
            _rootMenu.Close();
            _hireMenu.Close();
            _contractListMenu.Close();
            _contractActionMenu.Close();
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (_contractActionMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenContractListMenu();
                    return true;
                }

                _contractActionMenu.HandleKey(key, _controls);
                return true;
            }

            if (_contractListMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenRootMenu();
                    return true;
                }

                _contractListMenu.HandleKey(key, _controls);
                return true;
            }

            if (_hireMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    if (_editingContract != null)
                    {
                        OpenContractActionMenu(_editingContract);
                    }
                    else
                    {
                        OpenRootMenu();
                    }

                    return true;
                }

                _hireMenu.HandleKey(key, _controls);
                return true;
            }

            if (_rootMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    CloseAndReturnToOffice();
                    return true;
                }

                _rootMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        public void Draw()
        {
            if (_contractActionMenu.IsOpen)
            {
                _contractActionMenu.Draw();
                return;
            }

            if (_contractListMenu.IsOpen)
            {
                _contractListMenu.Draw();
                return;
            }

            if (_hireMenu.IsOpen)
            {
                _hireMenu.Draw();
                return;
            }

            if (_rootMenu.IsOpen)
            {
                _rootMenu.Draw();
            }
        }

        private static SimpleMenu CreateMenu(string title, string subtitle, float widthScale)
        {
            return new SimpleMenu(title)
            {
                Subtitle = subtitle,
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = widthScale,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
        }

        private void RebuildRootMenuItems()
        {
            var routeLimit = _manager.RouteLimit;
            var hireBlockedReason = GetHireBlockedReason();
            _rootMenu.Title = "Hire NPC";
            _rootMenu.Subtitle = "Automate resource transportation routes";
            _rootMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "Hire New NPC",
                    DetailFactory = () => routeLimit <= 0
                        ? "Hiring NPC is disabled in Options. Increase the NPC route limit to create new contracts."
                        : !string.IsNullOrWhiteSpace(hireBlockedReason)
                            ? hireBlockedReason
                            : "Create a new automated logistics route.",
                    IdleBackgroundColor = Color.FromArgb(170, 46, 66, 50),
                    SelectedBackgroundColor = Color.FromArgb(205, 85, 124, 94),
                    OnActivate = routeLimit <= 0
                        ? (Action)(() => ShowStatus("Hiring NPC is disabled in Options."))
                        : !string.IsNullOrWhiteSpace(hireBlockedReason)
                            ? (Action)(() => ShowStatus(hireBlockedReason))
                            : OpenHireNewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Manage NPC List",
                    DetailFactory = () => GetManageNpcListDetail(),
                    IdleBackgroundColor = Color.FromArgb(170, 50, 58, 72),
                    SelectedBackgroundColor = Color.FromArgb(205, 92, 124, 152),
                    OnActivate = OpenContractListMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Close",
                    DetailFactory = () => "Return to the Office menu.",
                    IdleBackgroundColor = Color.FromArgb(170, 64, 48, 44),
                    SelectedBackgroundColor = Color.FromArgb(205, 138, 104, 94),
                    OnActivate = CloseAndReturnToOffice,
                },
            });
        }

        private void OpenHireNewMenu()
        {
            if (_manager.RouteLimit <= 0)
            {
                ShowStatus("Hiring NPC is disabled in Options.");
                OpenRootMenu();
                return;
            }

            var hireBlockedReason = GetHireBlockedReason();
            if (!string.IsNullOrWhiteSpace(hireBlockedReason))
            {
                ShowStatus(hireBlockedReason);
                OpenRootMenu();
                return;
            }

            _editingContract = null;
            SeedDraftFromContract(null);
            RebuildHireMenuItems();
            Close();
            _hireMenu.Open();
        }

        private void OpenContractListMenu()
        {
            var items = new List<MenuItem>();
            var contracts = _manager.Contracts.ToList();
            for (int i = 0; i < contracts.Count; i++)
            {
                var contract = contracts[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => BuildContractCaption(contract),
                    DetailFactory = () => BuildContractDetail(contract),
                    OnActivate = () => OpenContractActionMenu(contract),
                });
            }

            if (items.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No active NPC routes",
                    DetailFactory = () => "Hire a new NPC from the previous page.",
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenRootMenu,
            });

            _contractListMenu.Title = "NPC List";
            _contractListMenu.Subtitle = contracts.Count == 1
                ? "1 active contract"
                : string.Format("{0} active contracts", contracts.Count);
            _contractListMenu.SetItems(items);
            Close();
            _contractListMenu.Open();
        }

        private void OpenContractActionMenu(NpcLogisticsContract contract)
        {
            _selectedContract = contract;
            _contractActionMenu.Title = contract != null ? BuildContractCaption(contract) : "NPC Contract";
            _contractActionMenu.Subtitle = contract != null ? BuildContractDetail(contract) : "No route selected";
            var routeLimit = _manager.RouteLimit;
            _contractActionMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "Fire NPC",
                    DetailFactory = () => "Immediately delete the NPC, vehicle, and active cargo.",
                    OnActivate = FireSelectedContract,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Modify NPC",
                    DetailFactory = () => routeLimit <= 0
                        ? "Route slots are disabled in Options. Increase the NPC route limit before modifying this contract."
                        : "Change tier, route, assigned truck, trigger thresholds, or transported resource.",
                    OnActivate = routeLimit <= 0
                        ? (Action)(() => ShowStatus("Hiring NPC is disabled in Options."))
                        : ModifySelectedContract,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = OpenContractListMenu,
                },
            });
            Close();
            _contractActionMenu.Open();
        }

        private void ModifySelectedContract()
        {
            if (_selectedContract == null)
            {
                ShowStatus("Select an NPC contract first.");
                OpenContractListMenu();
                return;
            }

            if (_manager.RouteLimit <= 0)
            {
                ShowStatus("Hiring NPC is disabled in Options.");
                OpenContractActionMenu(_selectedContract);
                return;
            }

            _editingContract = _selectedContract;
            SeedDraftFromContract(_selectedContract);
            RebuildHireMenuItems();
            Close();
            _hireMenu.Open();
        }

        private void FireSelectedContract()
        {
            if (_selectedContract == null)
            {
                ShowStatus("Select an NPC contract first.");
                OpenContractListMenu();
                return;
            }

            string message;
            if (!_manager.TryFireContract(_selectedContract, out message))
            {
                ShowStatus(message);
                OpenContractActionMenu(_selectedContract);
                return;
            }

            _selectedContract = null;
            _editingContract = null;
            ShowStatus(message);
            OpenContractListMenu();
        }

        private void RebuildHireMenuItems()
        {
            RefreshDraftSelections();

            _hireMenu.Title = _editingContract == null ? "Hire New NPC" : "Modify NPC";
            _hireMenu.Subtitle = _editingContract == null
                ? "Create a new automated logistics route"
                : "Update the selected NPC route";
            _hireMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = CurrentRouteSlotCaption,
                    DetailFactory = CurrentRouteSlotDetail,
                    OnLeft = () => ChangeRouteSlot(-1),
                    OnRight = () => ChangeRouteSlot(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentRouteEnabledCaption,
                    DetailFactory = CurrentRouteEnabledDetail,
                    OnLeft = ToggleSelectedRouteEnabled,
                    OnRight = ToggleSelectedRouteEnabled,
                    OnActivate = ToggleSelectedRouteEnabled,
                },
                new MenuItem
                {
                    CaptionFactory = CurrentOriginCaption,
                    DetailFactory = CurrentOriginDetail,
                    OnLeft = () => ChangeOrigin(-1),
                    OnRight = () => ChangeOrigin(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentDestinationCaption,
                    DetailFactory = CurrentDestinationDetail,
                    OnLeft = () => ChangeDestination(-1),
                    OnRight = () => ChangeDestination(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentResourceCaption,
                    DetailFactory = CurrentResourceDetail,
                    OnLeft = () => ChangeResource(-1),
                    OnRight = () => ChangeResource(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentAssignedTruckCaption,
                    DetailFactory = CurrentAssignedTruckDetail,
                    OnLeft = () => ChangeAssignedTruck(-1),
                    OnRight = () => ChangeAssignedTruck(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentOriginTriggerCaption,
                    DetailFactory = CurrentOriginTriggerDetail,
                    OnLeft = () => ChangeOriginTriggerThreshold(-1),
                    OnRight = () => ChangeOriginTriggerThreshold(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentDestinationTriggerCaption,
                    DetailFactory = CurrentDestinationTriggerDetail,
                    OnLeft = () => ChangeDestinationTriggerThreshold(-1),
                    OnRight = () => ChangeDestinationTriggerThreshold(1),
                },
                new MenuItem
                {
                    CaptionFactory = CurrentTierCaption,
                    DetailFactory = CurrentTierDetail,
                    OnLeft = () => ChangeTier(-1),
                    OnRight = () => ChangeTier(1),
                },
                new MenuItem
                {
                    CaptionFactory = () => _editingContract == null ? "Hire NPC" : "Modify NPC",
                    DetailFactory = CurrentHireActionDetail,
                    OnActivate = ConfirmDraft,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = () =>
                    {
                        if (_editingContract != null)
                        {
                            OpenContractActionMenu(_editingContract);
                        }
                        else
                        {
                            OpenRootMenu();
                        }
                    },
                },
            });
        }

        private void ConfirmDraft()
        {
            if (_editingContract == null)
            {
                var hireBlockedReason = GetHireBlockedReason();
                if (!string.IsNullOrWhiteSpace(hireBlockedReason))
                {
                    ShowStatus(hireBlockedReason);
                    RebuildHireMenuItems();
                    _hireMenu.Open();
                    return;
                }
            }

            var tier = GetSelectedTier();
            StoreCurrentSelectionsIntoDraftRoute();

            string validationMessage;
            var routes = BuildConfiguredRoutes(out validationMessage);
            if (routes == null)
            {
                ShowStatus(validationMessage);
                RebuildHireMenuItems();
                _hireMenu.Open();
                return;
            }

            string message;
            var success = _editingContract == null
                ? _manager.TryCreateContract(routes, tier, out message)
                : _manager.TryModifyContract(_editingContract, routes, tier, out message);
            ShowStatus(message);
            if (!success)
            {
                RebuildHireMenuItems();
                _hireMenu.Open();
                return;
            }

            _selectedContract = null;
            _editingContract = null;
            OpenContractListMenu();
        }

        private void SeedDraftFromContract(NpcLogisticsContract contract)
        {
            ResetDraftRoutes(contract);
            _selectedRouteSlotIndex = 0;

            _originOptions = _manager.GetOriginIndustryOptions();
            _selectedOriginIndex = 0;
            LoadSelectedDraftRouteIntoSelections();

            RefreshGarageVehicleOptions();

            _selectedTierIndex = 0;
            if (contract != null && contract.Tier != null)
            {
                var tierIndex = _manager.DriverTiers.ToList().FindIndex(tier => string.Equals(tier.Id, contract.Tier.Id, StringComparison.OrdinalIgnoreCase));
                if (tierIndex >= 0)
                {
                    _selectedTierIndex = tierIndex;
                }
            }
        }

        private List<DraftRouteConfig> CreateEmptyDraftRoutes()
        {
            var routes = new List<DraftRouteConfig>();
            for (int i = 0; i < _manager.RouteLimit; i++)
            {
                routes.Add(new DraftRouteConfig
                {
                    IsEnabled = i == 0,
                    OriginTriggerThresholdPercent = 0,
                    DestinationTriggerThresholdPercent = 100,
                });
            }

            return routes;
        }

        private void ResetDraftRoutes(NpcLogisticsContract contract)
        {
            _draftRoutes.Clear();
            _draftRoutes.AddRange(CreateEmptyDraftRoutes());

            if (contract == null)
            {
                return;
            }

            var sourceRoutes = contract.Routes != null && contract.Routes.Count > 0
                ? contract.Routes
                : new List<NpcLogisticsRouteDefinition>
                {
                    new NpcLogisticsRouteDefinition
                    {
                        OriginIndustry = contract.OriginIndustry,
                        DestinationIndustry = contract.DestinationIndustry,
                        Commodity = contract.Commodity,
                        AssignedVehicleAssetId = contract.AssignedVehicleAssetId,
                        AssignedVehicleDisplayName = contract.AssignedVehicleDisplayName,
                        OriginTriggerThresholdPercent = contract.OriginTriggerThresholdPercent,
                        DestinationTriggerThresholdPercent = contract.DestinationTriggerThresholdPercent,
                    },
                };

            for (int i = 0; i < sourceRoutes.Count && i < _draftRoutes.Count; i++)
            {
                var route = sourceRoutes[i];
                if (route == null)
                {
                    continue;
                }

                _draftRoutes[i].IsEnabled = true;
                _draftRoutes[i].OriginIndustry = route.OriginIndustry;
                _draftRoutes[i].DestinationIndustry = route.DestinationIndustry;
                _draftRoutes[i].Commodity = route.Commodity;
                _draftRoutes[i].AssignedVehicleAssetId = route.AssignedVehicleAssetId;
                _draftRoutes[i].AssignedVehicleDisplayName = route.AssignedVehicleDisplayName;
                _draftRoutes[i].OriginTriggerThresholdPercent = route.OriginTriggerThresholdPercent;
                _draftRoutes[i].DestinationTriggerThresholdPercent = route.DestinationTriggerThresholdPercent;
            }
        }

        private DraftRouteConfig GetSelectedDraftRouteConfig()
        {
            return _selectedRouteSlotIndex >= 0 && _selectedRouteSlotIndex < _draftRoutes.Count
                ? _draftRoutes[_selectedRouteSlotIndex]
                : null;
        }

        private void LoadSelectedDraftRouteIntoSelections()
        {
            var route = GetSelectedDraftRouteConfig();
            _originOptions = _manager.GetOriginIndustryOptions();
            _selectedOriginIndex = 0;
            if (route != null && route.OriginIndustry != null)
            {
                var originIndex = _originOptions.FindIndex(industry => string.Equals(industry.Id, route.OriginIndustry.Id, StringComparison.OrdinalIgnoreCase));
                if (originIndex >= 0)
                {
                    _selectedOriginIndex = originIndex;
                }
            }

            RefreshDraftSelections();

            if (route != null && route.DestinationIndustry != null)
            {
                var destinationIndex = _destinationOptions.FindIndex(industry => string.Equals(industry.Id, route.DestinationIndustry.Id, StringComparison.OrdinalIgnoreCase));
                if (destinationIndex >= 0)
                {
                    _selectedDestinationIndex = destinationIndex;
                }
            }

            RefreshResourceOptions();

            if (route != null && !string.IsNullOrWhiteSpace(route.Commodity))
            {
                var resourceIndex = _resourceOptions.FindIndex(resource => string.Equals(resource, route.Commodity, StringComparison.OrdinalIgnoreCase));
                if (resourceIndex >= 0)
                {
                    _selectedResourceIndex = resourceIndex;
                }
            }

            _selectedOriginTriggerIndex = FindTriggerThresholdIndex(route != null ? route.OriginTriggerThresholdPercent : 0, 0);
            _selectedDestinationTriggerIndex = FindTriggerThresholdIndex(route != null ? route.DestinationTriggerThresholdPercent : 100, TriggerThresholdOptions.Length - 1);
            RefreshGarageVehicleOptions();
        }

        private void StoreCurrentSelectionsIntoDraftRoute()
        {
            var route = GetSelectedDraftRouteConfig();
            if (route == null)
            {
                return;
            }

            route.OriginIndustry = GetSelectedOriginIndustry();
            route.DestinationIndustry = GetSelectedDestinationIndustry();
            route.Commodity = GetSelectedResource();
            var assignedVehicle = GetSelectedAssignedVehicle();
            route.AssignedVehicleAssetId = assignedVehicle != null ? assignedVehicle.AssetId : string.Empty;
            route.AssignedVehicleDisplayName = assignedVehicle != null ? BuildAssignedVehicleName(assignedVehicle) : string.Empty;
            route.OriginTriggerThresholdPercent = GetSelectedOriginTriggerThresholdPercent();
            route.DestinationTriggerThresholdPercent = GetSelectedDestinationTriggerThresholdPercent();
        }

        private List<NpcLogisticsRouteDefinition> BuildConfiguredRoutes(out string message)
        {
            message = string.Empty;
            var routes = new List<NpcLogisticsRouteDefinition>();
            for (int routeIndex = 0; routeIndex < _draftRoutes.Count; routeIndex++)
            {
                var draftRoute = _draftRoutes[routeIndex];
                if (draftRoute == null || !draftRoute.IsEnabled)
                {
                    continue;
                }

                if (draftRoute.OriginIndustry == null)
                {
                    message = string.Format("Select a starting point for route {0}.", routeIndex + 1);
                    return null;
                }

                if (draftRoute.DestinationIndustry == null)
                {
                    message = string.Format("Select a destination for route {0}.", routeIndex + 1);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(draftRoute.Commodity))
                {
                    message = string.Format("Select a resource for route {0}.", routeIndex + 1);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(draftRoute.AssignedVehicleAssetId))
                {
                    message = string.Format("Assign a truck for route {0}.", routeIndex + 1);
                    return null;
                }

                routes.Add(new NpcLogisticsRouteDefinition
                {
                    OriginIndustry = draftRoute.OriginIndustry,
                    DestinationIndustry = draftRoute.DestinationIndustry,
                    Commodity = draftRoute.Commodity,
                    AssignedVehicleAssetId = draftRoute.AssignedVehicleAssetId,
                    AssignedVehicleDisplayName = draftRoute.AssignedVehicleDisplayName,
                    OriginTriggerThresholdPercent = draftRoute.OriginTriggerThresholdPercent,
                    DestinationTriggerThresholdPercent = draftRoute.DestinationTriggerThresholdPercent,
                });
            }

            if (routes.Count == 0)
            {
                message = "Enable and configure at least one route first.";
                return null;
            }

            return routes;
        }

        private void RefreshDraftSelections()
        {
            if (_originOptions == null)
            {
                _originOptions = new List<Industry>();
            }

            if (_selectedOriginIndex < 0 || _selectedOriginIndex >= _originOptions.Count)
            {
                _selectedOriginIndex = _originOptions.Count > 0 ? 0 : -1;
            }

            RefreshDestinationOptions();
            RefreshResourceOptions();
            RefreshGarageVehicleOptions();

            if (_selectedTierIndex < 0 || _selectedTierIndex >= _manager.DriverTiers.Count)
            {
                _selectedTierIndex = _manager.DriverTiers.Count > 0 ? 0 : -1;
            }
        }

        private void RefreshDestinationOptions()
        {
            _destinationOptions = _manager.GetDestinationIndustryOptions(GetSelectedOriginIndustry());
            if (_selectedDestinationIndex < 0 || _selectedDestinationIndex >= _destinationOptions.Count)
            {
                _selectedDestinationIndex = _destinationOptions.Count > 0 ? 0 : -1;
            }
        }

        private void RefreshResourceOptions()
        {
            _resourceOptions = _manager.GetResourceOptions(GetSelectedOriginIndustry(), GetSelectedDestinationIndustry());
            if (_selectedResourceIndex < 0 || _selectedResourceIndex >= _resourceOptions.Count)
            {
                _selectedResourceIndex = _resourceOptions.Count > 0 ? 0 : -1;
            }
        }

        private void RefreshGarageVehicleOptions()
        {
            var route = GetSelectedDraftRouteConfig();
            var preferredAssetId = route != null && !string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId)
                ? route.AssignedVehicleAssetId
                : (GetSelectedAssignedVehicle() != null ? GetSelectedAssignedVehicle().AssetId : string.Empty);
            var commodity = GetSelectedResource();
            if (string.IsNullOrWhiteSpace(commodity) && route != null)
            {
                commodity = route.Commodity;
            }

            _garageVehicleOptions = _manager
                .GetAssignableGarageVehicles(commodity, GetDraftAssignedVehicleAssetIds(), _editingContract)
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredAssetId))
            {
                var matchIndex = _garageVehicleOptions.FindIndex(vehicle => string.Equals(vehicle.AssetId, preferredAssetId, StringComparison.OrdinalIgnoreCase));
                if (matchIndex >= 0)
                {
                    _selectedGarageVehicleIndex = matchIndex;
                    return;
                }
            }

            _selectedGarageVehicleIndex = _garageVehicleOptions.Count > 0 ? 0 : -1;
        }

        private void ChangeOrigin(int delta)
        {
            if (_originOptions.Count == 0)
            {
                _selectedOriginIndex = -1;
                return;
            }

            _selectedOriginIndex = (_selectedOriginIndex + delta + _originOptions.Count) % _originOptions.Count;
            _selectedDestinationIndex = 0;
            _selectedResourceIndex = 0;
            RefreshDestinationOptions();
            RefreshResourceOptions();
            RefreshGarageVehicleOptions();
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeDestination(int delta)
        {
            if (_destinationOptions.Count == 0)
            {
                _selectedDestinationIndex = -1;
                _selectedResourceIndex = -1;
                _resourceOptions = new List<string>();
                return;
            }

            _selectedDestinationIndex = (_selectedDestinationIndex + delta + _destinationOptions.Count) % _destinationOptions.Count;
            _selectedResourceIndex = 0;
            RefreshResourceOptions();
            RefreshGarageVehicleOptions();
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeResource(int delta)
        {
            if (_resourceOptions.Count == 0)
            {
                _selectedResourceIndex = -1;
                return;
            }

            _selectedResourceIndex = (_selectedResourceIndex + delta + _resourceOptions.Count) % _resourceOptions.Count;
            RefreshGarageVehicleOptions();
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeRouteSlot(int delta)
        {
            if (_draftRoutes.Count == 0)
            {
                _selectedRouteSlotIndex = -1;
                return;
            }

            StoreCurrentSelectionsIntoDraftRoute();
            _selectedRouteSlotIndex = (_selectedRouteSlotIndex + delta + _draftRoutes.Count) % _draftRoutes.Count;
            LoadSelectedDraftRouteIntoSelections();
        }

        private void ToggleSelectedRouteEnabled()
        {
            var route = GetSelectedDraftRouteConfig();
            if (route == null)
            {
                return;
            }

            route.IsEnabled = !route.IsEnabled;
        }

        private void ChangeAssignedTruck(int delta)
        {
            if (_garageVehicleOptions.Count == 0)
            {
                _selectedGarageVehicleIndex = -1;
                StoreCurrentSelectionsIntoDraftRoute();
                return;
            }

            _selectedGarageVehicleIndex = (_selectedGarageVehicleIndex + delta + _garageVehicleOptions.Count) % _garageVehicleOptions.Count;
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeOriginTriggerThreshold(int delta)
        {
            if (TriggerThresholdOptions.Length == 0)
            {
                _selectedOriginTriggerIndex = -1;
                return;
            }

            _selectedOriginTriggerIndex = (_selectedOriginTriggerIndex + delta + TriggerThresholdOptions.Length) % TriggerThresholdOptions.Length;
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeDestinationTriggerThreshold(int delta)
        {
            if (TriggerThresholdOptions.Length == 0)
            {
                _selectedDestinationTriggerIndex = -1;
                return;
            }

            _selectedDestinationTriggerIndex = (_selectedDestinationTriggerIndex + delta + TriggerThresholdOptions.Length) % TriggerThresholdOptions.Length;
            StoreCurrentSelectionsIntoDraftRoute();
        }

        private void ChangeTier(int delta)
        {
            if (_manager.DriverTiers.Count == 0)
            {
                _selectedTierIndex = -1;
                return;
            }

            _selectedTierIndex = (_selectedTierIndex + delta + _manager.DriverTiers.Count) % _manager.DriverTiers.Count;
        }

        private string CurrentOriginCaption()
        {
            return string.Format("Starting Point: < {0} >", GetSelectedOriginIndustryName());
        }

        private string CurrentRouteSlotCaption()
        {
            return string.Format("Route Slot: < {0}/{1} >", _selectedRouteSlotIndex + 1, _draftRoutes.Count);
        }

        private string CurrentRouteSlotDetail()
        {
            return string.Format(
                "Routes run in slot order. Up to {0} enabled route{1} can be chained on one hired NPC.",
                _draftRoutes.Count,
                _draftRoutes.Count == 1 ? string.Empty : "s");
        }

        private string CurrentRouteEnabledCaption()
        {
            var route = GetSelectedDraftRouteConfig();
            return string.Format("Route Enabled: < {0} >", route != null && route.IsEnabled ? "On" : "Off");
        }

        private string CurrentRouteEnabledDetail()
        {
            return "Disabled slots are skipped. Enabled slots must have a full origin, destination, and resource configured.";
        }

        private string CurrentOriginDetail()
        {
            if (_originOptions.Count == 0)
            {
                return _manager.BuildOriginAvailabilityDetail();
            }

            var industry = GetSelectedOriginIndustry();
            return industry == null
                ? "Select the industry where the route should load cargo."
                : string.Format("Outputs: {0}", string.Join(", ", industry.GetSortedOutputs()));
        }

        private string CurrentDestinationCaption()
        {
            return string.Format("Destination: < {0} >", GetSelectedDestinationIndustryName());
        }

        private string CurrentDestinationDetail()
        {
            if (_destinationOptions.Count == 0)
            {
                return _manager.BuildDestinationAvailabilityDetail(GetSelectedOriginIndustry());
            }

            var destination = GetSelectedDestinationIndustry();
            return destination == null
                ? "Select the delivery destination."
                : string.Format("Inputs: {0}", string.Join(", ", destination.GetSortedInputs()));
        }

        private string CurrentResourceCaption()
        {
            return string.Format("Resource: < {0} >", GetSelectedResourceName());
        }

        private string CurrentResourceDetail()
        {
            if (_resourceOptions.Count == 0)
            {
                return "No transportable resource matches the current route.";
            }

            return "Choose the resource that the NPC should haul on this route.";
        }

        private string CurrentTierCaption()
        {
            return string.Format("Tier: < {0} >", GetSelectedTierName());
        }

        private string CurrentAssignedTruckCaption()
        {
            return string.Format("Assigned Truck: < {0} >", GetSelectedAssignedVehicleName());
        }

        private string CurrentAssignedTruckDetail()
        {
            if (_garageVehicleOptions.Count == 0)
            {
                return "Move a company truck into the active office garage that can handle this route resource.";
            }

            var vehicle = GetSelectedAssignedVehicle();
            if (vehicle == null)
            {
                return "Select which office garage truck this route slot should reserve.";
            }

            return "Each enabled route slot keeps its own reserved office truck. Only active-garage vehicles compatible with this route are listed.";
        }

        private string CurrentOriginTriggerCaption()
        {
            return string.Format("Start Trigger: < {0}% >", GetSelectedOriginTriggerThresholdPercent());
        }

        private string CurrentOriginTriggerDetail()
        {
            return "NPC leaves the office only when source stock reaches at least this percent.";
        }

        private string CurrentDestinationTriggerCaption()
        {
            return string.Format("Destination Trigger: < {0}% >", GetSelectedDestinationTriggerThresholdPercent());
        }

        private string CurrentDestinationTriggerDetail()
        {
            return "NPC delivers until destination storage reaches this percent, then returns to the office.";
        }

        private string CurrentTierDetail()
        {
            var tier = GetSelectedTier();
            if (tier == null)
            {
                return "No NPC driver tier is configured.";
            }

            return string.Format(
                "Model {0} | Loss up to {1} | Speed {2} | Weekly {3}",
                tier.NpcModel,
                ModFormatting.FormatPercent(tier.CargoLossRate * 100f),
                ModFormatting.FormatPercent(tier.SpeedMultiplier * 100f),
                ModFormatting.FormatMoney(_manager.GetWeeklyWage(tier)));
        }

        private string CurrentHireActionDetail()
        {
            var tier = GetSelectedTier();
            StoreCurrentSelectionsIntoDraftRoute();

            string validationMessage;
            var configuredRoutes = BuildConfiguredRoutes(out validationMessage);
            if (configuredRoutes == null || tier == null)
            {
                return "Complete every selection before confirming the contract.";
            }

            var currentRoute = GetSelectedDraftRouteConfig();
            var fullCost = configuredRoutes.Sum(route => _manager.GetContractCost(route.Commodity, tier));
            var additionalCost = _editingContract == null
                ? fullCost
                : Math.Max(0f, fullCost - _editingContract.ContractCost);
            var weeklyWage = _manager.GetWeeklyWage(tier);

            var detail = string.Format(
                "Routes {0} | Slot {1} | Upfront {2} | Weekly {3}",
                configuredRoutes.Count,
                _selectedRouteSlotIndex + 1,
                ModFormatting.FormatMoney(additionalCost),
                ModFormatting.FormatMoney(weeklyWage));
            detail += string.Format(" | Truck {0}", GetSelectedAssignedVehicleName());
            if (currentRoute != null && currentRoute.IsEnabled && currentRoute.OriginIndustry != null && currentRoute.DestinationIndustry != null && !string.IsNullOrWhiteSpace(currentRoute.Commodity))
            {
                detail += string.Format(
                    " | {0}->{1} {2} {3}%->{4}%",
                    currentRoute.OriginIndustry.Name,
                    currentRoute.DestinationIndustry.Name,
                    currentRoute.Commodity,
                    currentRoute.OriginTriggerThresholdPercent,
                    currentRoute.DestinationTriggerThresholdPercent);
            }

            if (_editingContract != null && additionalCost <= 0f)
            {
                detail += " | No extra charge";
            }

            return detail;
        }

        private string GetManageNpcListDetail()
        {
            var count = _manager.Contracts.Count;
            return count == 1
                ? "1 active NPC logistics contract."
                : string.Format("{0} active NPC logistics contracts.", count);
        }

        private string BuildContractCaption(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return "Unknown NPC contract";
            }

            var tierName = contract.Tier != null ? contract.Tier.DisplayName : "Unknown";
            if (contract.Routes != null && contract.Routes.Count > 1)
            {
                var firstRoute = contract.Routes[0];
                return string.Format(
                    "[{0}] {1} routes ({2} -> {3})",
                    tierName,
                    contract.Routes.Count,
                    firstRoute != null && firstRoute.OriginIndustry != null ? firstRoute.OriginIndustry.Name : "Unknown",
                    firstRoute != null && firstRoute.DestinationIndustry != null ? firstRoute.DestinationIndustry.Name : "Unknown");
            }

            return string.Format(
                "[{0}] {1} -> {2}",
                tierName,
                contract.OriginIndustry != null ? contract.OriginIndustry.Name : "Unknown",
                contract.DestinationIndustry != null ? contract.DestinationIndustry.Name : "Unknown");
        }

        private string BuildContractDetail(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return string.Empty;
            }

            return string.Format(
                "{0} route{1} | Truck {2} | Active {3}% -> {4}% | {5} | {6} | Deliveries {7}",
                contract.Routes != null && contract.Routes.Count > 0 ? contract.Routes.Count : 1,
                contract.Routes != null && contract.Routes.Count == 1 ? string.Empty : "s",
                string.IsNullOrWhiteSpace(contract.AssignedVehicleDisplayName) ? "Legacy auto" : contract.AssignedVehicleDisplayName,
                contract.OriginTriggerThresholdPercent,
                contract.DestinationTriggerThresholdPercent,
                _manager.BuildPayrollStatus(contract),
                contract.StatusText,
                contract.CompletedDeliveries);
        }

        private Industry GetSelectedOriginIndustry()
        {
            return _selectedOriginIndex >= 0 && _selectedOriginIndex < _originOptions.Count
                ? _originOptions[_selectedOriginIndex]
                : null;
        }

        private Industry GetSelectedDestinationIndustry()
        {
            return _selectedDestinationIndex >= 0 && _selectedDestinationIndex < _destinationOptions.Count
                ? _destinationOptions[_selectedDestinationIndex]
                : null;
        }

        private string GetSelectedResource()
        {
            return _selectedResourceIndex >= 0 && _selectedResourceIndex < _resourceOptions.Count
                ? _resourceOptions[_selectedResourceIndex]
                : string.Empty;
        }

        private NpcDriverTierDefinition GetSelectedTier()
        {
            return _selectedTierIndex >= 0 && _selectedTierIndex < _manager.DriverTiers.Count
                ? _manager.DriverTiers[_selectedTierIndex]
                : null;
        }

        private OwnedCommercialVehiclePersistenceEntry GetSelectedAssignedVehicle()
        {
            return _selectedGarageVehicleIndex >= 0 && _selectedGarageVehicleIndex < _garageVehicleOptions.Count
                ? _garageVehicleOptions[_selectedGarageVehicleIndex]
                : null;
        }

        private IReadOnlyList<string> GetDraftAssignedVehicleAssetIds()
        {
            return _draftRoutes
                .Where(route => route != null && route.IsEnabled && !string.IsNullOrWhiteSpace(route.AssignedVehicleAssetId))
                .Select(route => route.AssignedVehicleAssetId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private int GetSelectedOriginTriggerThresholdPercent()
        {
            return _selectedOriginTriggerIndex >= 0 && _selectedOriginTriggerIndex < TriggerThresholdOptions.Length
                ? TriggerThresholdOptions[_selectedOriginTriggerIndex]
                : 0;
        }

        private int GetSelectedDestinationTriggerThresholdPercent()
        {
            return _selectedDestinationTriggerIndex >= 0 && _selectedDestinationTriggerIndex < TriggerThresholdOptions.Length
                ? TriggerThresholdOptions[_selectedDestinationTriggerIndex]
                : 100;
        }

        private string GetSelectedOriginIndustryName()
        {
            var industry = GetSelectedOriginIndustry();
            return industry != null ? industry.Name : "None";
        }

        private string GetSelectedDestinationIndustryName()
        {
            var industry = GetSelectedDestinationIndustry();
            return industry != null ? industry.Name : "None";
        }

        private string GetSelectedResourceName()
        {
            var resource = GetSelectedResource();
            return string.IsNullOrWhiteSpace(resource) ? "None" : resource;
        }

        private string GetSelectedTierName()
        {
            var tier = GetSelectedTier();
            return tier != null ? tier.DisplayName : "None";
        }

        private string GetSelectedAssignedVehicleName()
        {
            var vehicle = GetSelectedAssignedVehicle();
            if (vehicle == null)
            {
                var route = GetSelectedDraftRouteConfig();
                return route != null && !string.IsNullOrWhiteSpace(route.AssignedVehicleDisplayName)
                    ? route.AssignedVehicleDisplayName
                    : "None";
            }

            return BuildAssignedVehicleName(vehicle);
        }

        private static string BuildAssignedVehicleName(OwnedCommercialVehiclePersistenceEntry vehicle)
        {
            if (vehicle == null)
            {
                return "Assigned truck";
            }

            return !string.IsNullOrWhiteSpace(vehicle.DisplayName)
                ? vehicle.DisplayName
                : (!string.IsNullOrWhiteSpace(vehicle.PoweredModelName)
                    ? vehicle.PoweredModelName
                    : (vehicle.CargoModelName ?? "Assigned truck"));
        }

        private int FindTriggerThresholdIndex(int thresholdPercent, int fallbackIndex)
        {
            if (TriggerThresholdOptions.Length == 0)
            {
                return -1;
            }

            var clamped = Math.Max(0, Math.Min(100, thresholdPercent));
            var closestIndex = Math.Max(0, Math.Min(TriggerThresholdOptions.Length - 1, fallbackIndex));
            var closestDistance = Math.Abs(TriggerThresholdOptions[closestIndex] - clamped);
            for (int i = 0; i < TriggerThresholdOptions.Length; i++)
            {
                var distance = Math.Abs(TriggerThresholdOptions[i] - clamped);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void CloseAndReturnToOffice()
        {
            Close();
            if (_reopenOfficeMenu != null)
            {
                _reopenOfficeMenu();
            }
        }

        private void ShowStatus(string message)
        {
            if (_showStatus == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _showStatus(message);
        }

        private string GetHireBlockedReason()
        {
            return _getHireBlockedReason != null ? _getHireBlockedReason() : string.Empty;
        }

        private bool IsBackMenuKey(WinForms.Keys key)
        {
            return key == _controls.MenuBack || key == WinForms.Keys.Escape;
        }

    }
}
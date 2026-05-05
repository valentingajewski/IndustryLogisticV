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
        private readonly ControlBindings _controls;
        private readonly NpcLogisticsManager _manager;
        private readonly Action _reopenOfficeMenu;
        private readonly Action<string> _showStatus;

        private readonly SimpleMenu _rootMenu;
        private readonly SimpleMenu _hireMenu;
        private readonly SimpleMenu _contractListMenu;
        private readonly SimpleMenu _contractActionMenu;

        private List<Industry> _originOptions;
        private List<Industry> _destinationOptions;
        private List<string> _resourceOptions;
        private int _selectedOriginIndex;
        private int _selectedDestinationIndex;
        private int _selectedResourceIndex;
        private int _selectedTierIndex;
        private NpcLogisticsContract _selectedContract;
        private NpcLogisticsContract _editingContract;

        public NpcLogisticsController(
            ControlBindings controls,
            NpcLogisticsManager manager,
            Action reopenOfficeMenu,
            Action<string> showStatus)
        {
            _controls = controls;
            _manager = manager;
            _reopenOfficeMenu = reopenOfficeMenu;
            _showStatus = showStatus;

            _rootMenu = CreateMenu("Hire NPC", "Automate logistics between industries", 0.70f);
            _hireMenu = CreateMenu("Hire NPC", "Configure an automated logistics route", 0.88f);
            _contractListMenu = CreateMenu("NPC List", "Manage active NPC contracts", 0.92f);
            _contractActionMenu = CreateMenu("NPC Contract", "Modify or fire the selected route", 0.82f);

            _originOptions = new List<Industry>();
            _destinationOptions = new List<Industry>();
            _resourceOptions = new List<string>();
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
            _rootMenu.Title = "Hire NPC";
            _rootMenu.Subtitle = "Automate resource transportation routes";
            _rootMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "Hire New NPC",
                    DetailFactory = () => "Create a new automated logistics route.",
                    IdleBackgroundColor = Color.FromArgb(170, 46, 66, 50),
                    SelectedBackgroundColor = Color.FromArgb(205, 85, 124, 94),
                    OnActivate = OpenHireNewMenu,
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
                    DetailFactory = () => "Change tier, route, or transported resource.",
                    OnActivate = ModifySelectedContract,
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
            var origin = GetSelectedOriginIndustry();
            var destination = GetSelectedDestinationIndustry();
            var resource = GetSelectedResource();
            var tier = GetSelectedTier();

            string message;
            var success = _editingContract == null
                ? _manager.TryCreateContract(origin, destination, resource, tier, out message)
                : _manager.TryModifyContract(_editingContract, origin, destination, resource, tier, out message);
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
            _originOptions = _manager.GetOriginIndustryOptions();
            _selectedOriginIndex = 0;
            if (contract != null && contract.OriginIndustry != null)
            {
                var originIndex = _originOptions.FindIndex(industry => string.Equals(industry.Id, contract.OriginIndustry.Id, StringComparison.OrdinalIgnoreCase));
                if (originIndex >= 0)
                {
                    _selectedOriginIndex = originIndex;
                }
            }

            RefreshDraftSelections();

            if (contract != null && contract.DestinationIndustry != null)
            {
                var destinationIndex = _destinationOptions.FindIndex(industry => string.Equals(industry.Id, contract.DestinationIndustry.Id, StringComparison.OrdinalIgnoreCase));
                if (destinationIndex >= 0)
                {
                    _selectedDestinationIndex = destinationIndex;
                }
            }

            RefreshResourceOptions();

            if (contract != null && !string.IsNullOrWhiteSpace(contract.Commodity))
            {
                var resourceIndex = _resourceOptions.FindIndex(resource => string.Equals(resource, contract.Commodity, StringComparison.OrdinalIgnoreCase));
                if (resourceIndex >= 0)
                {
                    _selectedResourceIndex = resourceIndex;
                }
            }

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
        }

        private void ChangeResource(int delta)
        {
            if (_resourceOptions.Count == 0)
            {
                _selectedResourceIndex = -1;
                return;
            }

            _selectedResourceIndex = (_selectedResourceIndex + delta + _resourceOptions.Count) % _resourceOptions.Count;
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

        private string CurrentOriginDetail()
        {
            if (_originOptions.Count == 0)
            {
                return "No industry with available outputs is currently accessible.";
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
                return "No compatible destination is available for the selected starting point.";
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

        private string CurrentTierDetail()
        {
            var tier = GetSelectedTier();
            if (tier == null)
            {
                return "No NPC driver tier is configured.";
            }

            return string.Format(
                "Model {0} | Loss up to {1:0}% | Speed {2:0}% | Weekly {3}",
                tier.NpcModel,
                tier.CargoLossRate * 100f,
                tier.SpeedMultiplier * 100f,
                ModFormatting.FormatMoney(_manager.GetWeeklyWage(tier)));
        }

        private string CurrentHireActionDetail()
        {
            var origin = GetSelectedOriginIndustry();
            var destination = GetSelectedDestinationIndustry();
            var resource = GetSelectedResource();
            var tier = GetSelectedTier();
            if (origin == null || destination == null || string.IsNullOrWhiteSpace(resource) || tier == null)
            {
                return "Complete every selection before confirming the contract.";
            }

            var fullCost = _manager.GetContractCost(resource, tier);
            var additionalCost = _editingContract == null
                ? fullCost
                : Math.Max(0f, fullCost - _editingContract.ContractCost);
            var weeklyWage = _manager.GetWeeklyWage(tier);

            var detail = string.Format(
                "{0} -> {1} | {2} | Upfront {3} | Weekly {4}",
                origin.Name,
                destination.Name,
                resource,
                ModFormatting.FormatMoney(additionalCost),
                ModFormatting.FormatMoney(weeklyWage));

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
                "{0} | {1} | {2} | Deliveries {3}",
                contract.Commodity,
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

        private bool IsBackMenuKey(WinForms.Keys key)
        {
            return key == _controls.MenuBack || key == WinForms.Keys.Escape;
        }

    }
}
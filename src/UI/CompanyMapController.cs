using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    public sealed class CompanyMapController
    {
        private readonly ControlBindings _controls;
        private readonly TerritoryManager _territoryManager;
        private readonly IndustryManager _industryManager;
        private readonly Action _closeAllMenus;
        private readonly Func<float> _getCurrentProfit;
        private readonly Func<Industry, string> _secureSupportSite;
        private readonly Func<Industry, string> _assignSupportCrew;
        private readonly Func<Industry, DepotStaffRole, string> _hireSupportStaff;
        private readonly Action<string> _showStatus;
        private readonly SimpleMenu _rootMenu;
        private readonly SimpleMenu _districtMenu;
        private readonly SimpleMenu _districtDetailMenu;
        private readonly SimpleMenu _depotMenu;
        private readonly SimpleMenu _depotDetailMenu;

        private string _selectedDistrictName;
        private Industry _selectedSupportIndustry;

        public CompanyMapController(
            ControlBindings controls,
            TerritoryManager territoryManager,
            IndustryManager industryManager,
            Action closeAllMenus,
            Func<float> getCurrentProfit,
            Func<Industry, string> secureSupportSite,
            Func<Industry, string> assignSupportCrew,
            Func<Industry, DepotStaffRole, string> hireSupportStaff,
            Action<string> showStatus)
        {
            _controls = controls;
            _territoryManager = territoryManager;
            _industryManager = industryManager;
            _closeAllMenus = closeAllMenus;
            _getCurrentProfit = getCurrentProfit;
            _secureSupportSite = secureSupportSite;
            _assignSupportCrew = assignSupportCrew;
            _hireSupportStaff = hireSupportStaff;
            _showStatus = showStatus;

            _rootMenu = BuildMenu("Company Map", "District control, support sites, and corridor rights", 0.82f, 6);
            _districtMenu = BuildMenu("District View", "Review influence, reputation, and strategic coverage", 0.96f, 7);
            _districtDetailMenu = BuildMenu("District Detail", "Inspect site coverage and corridor posture", 0.96f, 8);
            _depotMenu = BuildMenu("Depot / Yard View", "Lease, staff, and expand support sites", 0.96f, 7);
            _depotDetailMenu = BuildMenu("Support Site", "Secure local fleet deployment and district support", 0.96f, 8);
        }

        public bool AnyMenuOpen
        {
            get
            {
                return _rootMenu.IsOpen
                    || _districtMenu.IsOpen
                    || _districtDetailMenu.IsOpen
                    || _depotMenu.IsOpen
                    || _depotDetailMenu.IsOpen;
            }
        }

        public void Open()
        {
            _closeAllMenus();
            OpenRootMenu();
        }

        public void Close()
        {
            _rootMenu.Close();
            _districtMenu.Close();
            _districtDetailMenu.Close();
            _depotMenu.Close();
            _depotDetailMenu.Close();
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (_depotDetailMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenDepotMenu();
                    return true;
                }

                _depotDetailMenu.HandleKey(key, _controls);
                return true;
            }

            if (_districtDetailMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenDistrictMenu();
                    return true;
                }

                _districtDetailMenu.HandleKey(key, _controls);
                return true;
            }

            if (_districtMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenRootMenu();
                    return true;
                }

                _districtMenu.HandleKey(key, _controls);
                return true;
            }

            if (_depotMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenRootMenu();
                    return true;
                }

                _depotMenu.HandleKey(key, _controls);
                return true;
            }

            if (_rootMenu.IsOpen)
            {
                _rootMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        public void Draw()
        {
            _rootMenu.Draw();
            _districtMenu.Draw();
            _districtDetailMenu.Draw();
            _depotMenu.Draw();
            _depotDetailMenu.Draw();
        }

        private void OpenRootMenu()
        {
            Close();
            RebuildRootMenuItems();
            _rootMenu.Open();
        }

        private void OpenDistrictMenu()
        {
            Close();
            RebuildDistrictMenuItems();
            _districtMenu.Open();
        }

        private void OpenDistrictDetailMenu(string districtName)
        {
            _selectedDistrictName = districtName ?? string.Empty;
            Close();
            RebuildDistrictDetailMenuItems();
            _districtDetailMenu.Open();
        }

        private void OpenDepotMenu()
        {
            Close();
            RebuildDepotMenuItems();
            _depotMenu.Open();
        }

        private void OpenDepotDetailMenu(Industry industry)
        {
            _selectedSupportIndustry = industry;
            Close();
            RebuildDepotDetailMenuItems();
            _depotDetailMenu.Open();
        }

        private void RebuildRootMenuItems()
        {
            var supportSiteCount = _territoryManager != null
                ? _territoryManager.GetDepotIndustries().Count(industry =>
                {
                    var siteState = _territoryManager.GetSiteState(industry);
                    return siteState != null && siteState.ControlLevel != TerritoryControlLevel.None;
                })
                : 0;

            _rootMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => string.Format("Balance: {0}", ModFormatting.FormatMoney(_getCurrentProfit != null ? _getCurrentProfit() : 0f)),
                    DetailFactory = () => "District expansion, depots, corridors, and franchise state persist with the save.",
                },
                new MenuItem
                {
                    CaptionFactory = () => "Footprint",
                    DetailFactory = () => string.Format(
                        "{0} districts anchored | {1} corridors active | {2} support sites secured",
                        _territoryManager != null ? _territoryManager.GetControlledDistrictCount() : 0,
                        _territoryManager != null ? _territoryManager.GetActiveCorridorCount() : 0,
                        supportSiteCount),
                },
                new MenuItem
                {
                    CaptionFactory = () => "District View",
                    DetailFactory = () => "Inspect district influence, reputation, site coverage, and corridor posture.",
                    OnActivate = OpenDistrictMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Depot / Yard View",
                    DetailFactory = () => "Lease or buy support sites, assign crews, and grow local staff.",
                    OnActivate = OpenDepotMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = Close,
                },
            });
        }

        private void RebuildDistrictMenuItems()
        {
            var items = new List<MenuItem>();
            var districts = _territoryManager != null
                ? _territoryManager.DistrictStates
                    .OrderByDescending(x => x.InfluenceScore)
                    .ThenBy(x => x.DistrictName)
                    .ToList()
                : new List<TerritoryDistrictState>();

            if (districts.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No districts available",
                    DetailFactory = () => "District data was not loaded from the CSV catalog.",
                });
            }

            for (int i = 0; i < districts.Count; i++)
            {
                var district = districts[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => string.Format("{0} [{1}]", district.DistrictName, district.ReputationLabel),
                    DetailFactory = () => string.Format(
                        "Influence {0:0}% | Depots {1} | Operational {2} | Corridors {3}",
                        district.InfluenceRatio * 100f,
                        district.ControlledDepots,
                        district.OperationalSites,
                        district.RouteRights),
                    OnActivate = () => OpenDistrictDetailMenu(district.DistrictName),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenRootMenu,
            });

            _districtMenu.SetItems(items);
        }

        private void RebuildDistrictDetailMenuItems()
        {
            var items = new List<MenuItem>();
            var district = _territoryManager != null ? _territoryManager.GetDistrictState(_selectedDistrictName) : null;
            if (district == null)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "District unavailable",
                    DetailFactory = () => "Return and select another district.",
                });
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = OpenDistrictMenu,
                });
                _districtDetailMenu.SetItems(items);
                return;
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("{0} [{1}]", district.DistrictName, district.ReputationLabel),
                DetailFactory = () => string.Format(
                    "Influence score {0:0.0} | Reputation score {1:0.0}",
                    district.InfluenceScore,
                    district.ReputationScore),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("Influence: {0:0}%", district.InfluenceRatio * 100f),
                DetailFactory = () => district.InfluenceRatio >= 0.6f
                    ? "District is established enough to support local fleet privileges and better delivery terms."
                    : "Grow deliveries, depots, and corridors here to anchor the district.",
                ProgressRatioFactory = () => Math.Max(0f, Math.Min(1f, district.InfluenceRatio)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => "Network Coverage",
                DetailFactory = () => string.Format(
                    "Sites {0} | Controlled {1} | Operational {2} | Depots {3}",
                    district.SiteCount,
                    district.ControlledSites,
                    district.OperationalSites,
                    district.ControlledDepots),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => "Service & Support",
                DetailFactory = () => string.Format(
                    "Franchises {0} | Corridor rights {1} | Support bonus +{2:0}%",
                    district.FranchiseSites,
                    district.RouteRights,
                    (_territoryManager != null ? _territoryManager.GetDistrictSupportBonus(district.DistrictName) : 0f) * 100f),
            });

            var localIndustries = _industryManager != null && _industryManager.Industries != null
                ? _industryManager.Industries
                    .Where(industry => industry != null && string.Equals(industry.DistrictName, district.DistrictName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(GetDistrictSiteSortValue)
                    .ThenBy(industry => industry.Name)
                    .Take(6)
                    .ToList()
                : new List<Industry>();

            for (int i = 0; i < localIndustries.Count; i++)
            {
                var industry = localIndustries[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetDistrictSiteCaption(industry),
                    DetailFactory = () => GetDistrictSiteDetail(industry),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenDistrictMenu,
            });

            _districtDetailMenu.SetItems(items);
        }

        private void RebuildDepotMenuItems()
        {
            var items = new List<MenuItem>();
            var supportSites = _territoryManager != null
                ? _territoryManager.GetDepotIndustries().ToList()
                : new List<Industry>();

            if (supportSites.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No support sites available",
                    DetailFactory = () => "No warehouses, fleet yards, or starter HQ nodes are available.",
                });
            }

            for (int i = 0; i < supportSites.Count; i++)
            {
                var industry = supportSites[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetSupportSiteCaption(industry),
                    DetailFactory = () => GetSupportSiteDetail(industry),
                    OnActivate = () => OpenDepotDetailMenu(industry),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenRootMenu,
            });

            _depotMenu.SetItems(items);
        }

        private void RebuildDepotDetailMenuItems()
        {
            var items = new List<MenuItem>();
            var industry = _selectedSupportIndustry;
            var siteState = _territoryManager != null ? _territoryManager.GetSiteState(industry) : null;

            if (industry == null || siteState == null)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "Support site unavailable",
                    DetailFactory = () => "Return and select another support site.",
                });
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "Back",
                    OnActivate = OpenDepotMenu,
                });
                _depotDetailMenu.SetItems(items);
                return;
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => industry.Name,
                DetailFactory = () => string.Format("{0} | {1}", industry.DistrictName, _territoryManager.GetActivationSummary(industry)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("Status: {0}", GetSupportControlCaption(siteState)),
                DetailFactory = () => string.Format(
                    "Crew {0} | Spawn rights {1}",
                    siteState.CrewAssigned ? "assigned" : "pending",
                    siteState.HasSpawnRights ? "ready" : "locked"),
            });

            if (!industry.IsStarterHeadquarters && siteState.ControlLevel != TerritoryControlLevel.Owned)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => siteState.ControlLevel == TerritoryControlLevel.None ? "Lease Site" : "Buy Site",
                    DetailFactory = () => string.Format(
                        "Cost {0}. {1}",
                        ModFormatting.FormatMoney(_territoryManager.GetNextDepotControlCost(industry)),
                        siteState.ControlLevel == TerritoryControlLevel.None
                            ? "Lease first to anchor the district and open staffing."
                            : "Ownership locks in support value and resists repossession."),
                    OnActivate = () => ExecuteSupportAction(_secureSupportSite, industry),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => siteState.CrewAssigned ? "Crew Assigned" : "Assign Crew",
                DetailFactory = () => siteState.CrewAssigned
                    ? "Base operating crew is active. Expand specialized staff below."
                    : string.Format("Cost {0}. Required before local fleet deployment.", ModFormatting.FormatMoney(_territoryManager.GetCrewAssignmentCostPreview(industry))),
                OnActivate = siteState.CrewAssigned ? (Action)null : (() => ExecuteSupportAction(_assignSupportCrew, industry)),
            });

            AddSupportStaffItem(items, industry, DepotStaffRole.Loader, "Loaders");
            AddSupportStaffItem(items, industry, DepotStaffRole.Mechanic, "Mechanics");
            AddSupportStaffItem(items, industry, DepotStaffRole.Guard, "Guards");
            AddSupportStaffItem(items, industry, DepotStaffRole.Manager, "Managers");

            items.Add(new MenuItem
            {
                CaptionFactory = () => "District Bonus",
                DetailFactory = () => string.Format(
                    "Current district support bonus +{0:0}% to route reliability and delivery posture.",
                    (_territoryManager != null ? _territoryManager.GetDistrictSupportBonus(industry.DistrictName) : 0f) * 100f),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenDepotMenu,
            });

            _depotDetailMenu.SetItems(items);
        }

        private void AddSupportStaffItem(List<MenuItem> items, Industry industry, DepotStaffRole staffRole, string label)
        {
            var siteState = _territoryManager != null ? _territoryManager.GetSiteState(industry) : null;
            if (items == null || industry == null || siteState == null)
            {
                return;
            }

            var currentCount = _territoryManager.GetStaffCount(industry, staffRole);
            var maxCount = _territoryManager.GetMaxStaffCount(industry, staffRole);
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("{0}: {1}/{2}", label, currentCount, maxCount),
                DetailFactory = () => !siteState.CrewAssigned
                    ? "Assign a base crew first."
                    : (currentCount >= maxCount
                        ? "Staff cap reached for this site tier."
                        : string.Format("Hire for {0}. Strengthens local support and route stability.", ModFormatting.FormatMoney(_territoryManager.GetHireStaffCostPreview(industry, staffRole)))),
                OnActivate = !siteState.CrewAssigned || currentCount >= maxCount
                    ? (Action)null
                    : (() => ExecuteSupportStaffAction(industry, staffRole)),
            });
        }

        private void ExecuteSupportAction(Func<Industry, string> action, Industry industry)
        {
            if (action == null || industry == null)
            {
                return;
            }

            var message = action(industry);
            if (!string.IsNullOrWhiteSpace(message) && _showStatus != null)
            {
                _showStatus(message);
            }

            RebuildDepotDetailMenuItems();
            RebuildDepotMenuItems();
            RebuildRootMenuItems();
        }

        private void ExecuteSupportStaffAction(Industry industry, DepotStaffRole staffRole)
        {
            if (_hireSupportStaff == null || industry == null)
            {
                return;
            }

            var message = _hireSupportStaff(industry, staffRole);
            if (!string.IsNullOrWhiteSpace(message) && _showStatus != null)
            {
                _showStatus(message);
            }

            RebuildDepotDetailMenuItems();
            RebuildDepotMenuItems();
            RebuildRootMenuItems();
        }

        private static SimpleMenu BuildMenu(string title, string subtitle, float widthScale, int maxVisibleItems)
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
                MaxVisibleItems = maxVisibleItems,
            };
        }

        private bool IsBackMenuKey(WinForms.Keys key)
        {
            return key == _controls.MenuBack || key == WinForms.Keys.Escape;
        }

        private float GetDistrictSiteSortValue(Industry industry)
        {
            var siteState = _territoryManager != null ? _territoryManager.GetSiteState(industry) : null;
            if (industry == null || siteState == null)
            {
                return 0f;
            }

            return (siteState.IsOperational ? 40f : 0f)
                + (siteState.TotalDeliveredTons * 0.05f)
                + (siteState.TotalLoadedTons * 0.03f)
                + ((int)siteState.ControlLevel * 8f)
                + ((int)siteState.FranchiseLevel * 6f);
        }

        private string GetDistrictSiteCaption(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var siteState = _territoryManager != null ? _territoryManager.GetSiteState(industry) : null;
            var tag = siteState == null
                ? string.Empty
                : (siteState.IsOperational
                    ? "LIVE"
                    : (siteState.FranchiseLevel != TerritoryFranchiseLevel.None ? "FRANCHISE" : "SETUP"));
            return string.IsNullOrWhiteSpace(tag)
                ? industry.Name
                : string.Format("{0} [{1}]", industry.Name, tag);
        }

        private string GetDistrictSiteDetail(Industry industry)
        {
            if (industry == null || _territoryManager == null)
            {
                return string.Empty;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            if (siteState == null)
            {
                return string.Empty;
            }

            var detail = _territoryManager.GetActivationSummary(industry);
            if (siteState.FranchiseLevel != TerritoryFranchiseLevel.None)
            {
                detail += string.Format(" | Franchise {0}", siteState.FranchiseLevel);
            }

            if (siteState.HasSpawnRights)
            {
                detail += " | Spawn rights ready";
            }

            return detail;
        }

        private string GetSupportSiteCaption(Industry industry)
        {
            if (industry == null || _territoryManager == null)
            {
                return string.Empty;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            var tag = siteState == null ? string.Empty : GetSupportControlCaption(siteState);
            return string.IsNullOrWhiteSpace(tag)
                ? industry.Name
                : string.Format("{0} [{1}]", industry.Name, tag);
        }

        private string GetSupportSiteDetail(Industry industry)
        {
            if (industry == null || _territoryManager == null)
            {
                return string.Empty;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            if (siteState == null)
            {
                return string.Empty;
            }

            return string.Format(
                "{0} | {1} | Staff {2}/{3}/{4}/{5}",
                industry.DistrictName,
                _territoryManager.GetActivationSummary(industry),
                siteState.LoaderCount,
                siteState.MechanicCount,
                siteState.GuardCount,
                siteState.ManagerCount);
        }

        private static string GetSupportControlCaption(TerritorySiteState siteState)
        {
            if (siteState == null)
            {
                return string.Empty;
            }

            switch (siteState.ControlLevel)
            {
                case TerritoryControlLevel.Owned:
                    return "OWNED";
                case TerritoryControlLevel.Leased:
                    return "LEASED";
                default:
                    return "OPEN";
            }
        }
    }
}
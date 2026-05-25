using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA.Native;
using GTA.UI;
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
        private readonly Func<string, string> _acquireDistrictLicense;
        private readonly Func<Industry, string> _secureSupportSite;
        private readonly Func<Industry, string> _assignSupportCrew;
        private readonly Func<Industry, DepotStaffRole, string> _hireSupportStaff;
        private readonly Func<Industry, DepotSpecialization, string> _setDepotSpecialization;
        private readonly Action<string> _showStatus;
        private readonly SimpleMenu _rootMenu;
        private readonly SimpleMenu _districtMenu;
        private readonly SimpleMenu _districtDetailMenu;
        private readonly SimpleMenu _depotMenu;
        private readonly SimpleMenu _depotDetailMenu;
        private Action _returnAction;
        private Action _districtDetailBackAction;
        private bool _networkViewOpen;
        private NetworkViewSnapshot _networkViewSnapshot;
        private NetworkLayoutCache _networkLayoutCache;

        private string _selectedDistrictName;
        private Industry _selectedSupportIndustry;

        public CompanyMapController(
            ControlBindings controls,
            TerritoryManager territoryManager,
            IndustryManager industryManager,
            Action closeAllMenus,
            Func<float> getCurrentProfit,
            Func<string, string> acquireDistrictLicense,
            Func<Industry, string> secureSupportSite,
            Func<Industry, string> assignSupportCrew,
            Func<Industry, DepotStaffRole, string> hireSupportStaff,
            Func<Industry, DepotSpecialization, string> setDepotSpecialization,
            Action<string> showStatus)
        {
            _controls = controls;
            _territoryManager = territoryManager;
            _industryManager = industryManager;
            _closeAllMenus = closeAllMenus;
            _getCurrentProfit = getCurrentProfit;
            _acquireDistrictLicense = acquireDistrictLicense;
            _secureSupportSite = secureSupportSite;
            _assignSupportCrew = assignSupportCrew;
            _hireSupportStaff = hireSupportStaff;
            _setDepotSpecialization = setDepotSpecialization;
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
                return _networkViewOpen
                    || _rootMenu.IsOpen
                    || _districtMenu.IsOpen
                    || _districtDetailMenu.IsOpen
                    || _depotMenu.IsOpen
                    || _depotDetailMenu.IsOpen;
            }
        }

        public void Open(Action returnAction = null)
        {
            _closeAllMenus();
            _returnAction = returnAction;
            OpenRootMenu();
        }

        public void OpenDistrictView(Action returnAction = null)
        {
            _closeAllMenus();
            _returnAction = returnAction;
            OpenDistrictMenu();
        }

        public void OpenDepotView(Action returnAction = null)
        {
            _closeAllMenus();
            _returnAction = returnAction;
            OpenDepotMenu();
        }

        public void OpenNetworkView(Action returnAction = null)
        {
            _closeAllMenus();
            _returnAction = returnAction;
            OpenNetworkViewInternal();
        }

        public void Close()
        {
            CloseMenus();
            _returnAction = null;
        }

        private void CloseMenus()
        {
            _networkViewOpen = false;
            _districtDetailBackAction = null;
            InvalidateNetworkViewCache();
            _rootMenu.Close();
            _districtMenu.Close();
            _districtDetailMenu.Close();
            _depotMenu.Close();
            _depotDetailMenu.Close();
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (_networkViewOpen)
            {
                if (IsBackMenuKey(key))
                {
                    ReturnToPreviousPage();
                    return true;
                }

                if (key == _controls.MenuSelect)
                {
                    if (!string.IsNullOrWhiteSpace(_selectedDistrictName))
                    {
                        OpenDistrictDetailMenu(_selectedDistrictName, OpenNetworkViewInternal);
                    }

                    return true;
                }

                if (key == _controls.MenuLeft
                    || key == _controls.MenuRight
                    || key == _controls.MenuUp
                    || key == _controls.MenuDown)
                {
                    MoveNetworkSelection(key);
                    return true;
                }

                return true;
            }

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
                    BackFromDistrictDetailMenu();
                    return true;
                }

                _districtDetailMenu.HandleKey(key, _controls);
                return true;
            }

            if (_districtMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    BackFromDistrictMenu();
                    return true;
                }

                _districtMenu.HandleKey(key, _controls);
                return true;
            }

            if (_depotMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    BackFromDepotMenu();
                    return true;
                }

                _depotMenu.HandleKey(key, _controls);
                return true;
            }

            if (_rootMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    ReturnToPreviousPage();
                    return true;
                }

                _rootMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        public void Draw()
        {
            if (_networkViewOpen)
            {
                DrawNetworkView();
                return;
            }

            _rootMenu.Draw();
            _districtMenu.Draw();
            _districtDetailMenu.Draw();
            _depotMenu.Draw();
            _depotDetailMenu.Draw();
        }

        private void OpenRootMenu()
        {
            CloseMenus();
            RebuildRootMenuItems();
            _rootMenu.Open();
        }

        private void OpenNetworkViewInternal()
        {
            CloseMenus();
            EnsureSelectedDistrictForNetwork();
            _networkViewOpen = true;
        }

        private void OpenDistrictMenu()
        {
            CloseMenus();
            RebuildDistrictMenuItems();
            _districtMenu.Open();
        }

        private void OpenDistrictDetailMenu(string districtName)
        {
            OpenDistrictDetailMenu(districtName, OpenDistrictMenu);
        }

        private void OpenDistrictDetailMenu(string districtName, Action backAction)
        {
            _selectedDistrictName = districtName ?? string.Empty;
            CloseMenus();
            _districtDetailBackAction = backAction ?? OpenDistrictMenu;
            RebuildDistrictDetailMenuItems();
            _districtDetailMenu.Open();
        }

        private void OpenDepotMenu()
        {
            CloseMenus();
            RebuildDepotMenuItems();
            _depotMenu.Open();
        }

        private void OpenDepotDetailMenu(Industry industry)
        {
            _selectedSupportIndustry = industry;
            CloseMenus();
            RebuildDepotDetailMenuItems();
            _depotDetailMenu.Open();
        }

        private void ReturnToPreviousPage()
        {
            var returnAction = _returnAction;
            CloseMenus();
            _returnAction = null;
            if (returnAction != null)
            {
                returnAction();
            }
        }

        private void BackFromDistrictMenu()
        {
            if (_returnAction != null)
            {
                ReturnToPreviousPage();
                return;
            }

            OpenRootMenu();
        }

        private void BackFromDepotMenu()
        {
            if (_returnAction != null)
            {
                ReturnToPreviousPage();
                return;
            }

            OpenRootMenu();
        }

        private void BackFromDistrictDetailMenu()
        {
            var backAction = _districtDetailBackAction ?? OpenDistrictMenu;
            backAction();
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
            var operationsSummary = _territoryManager != null ? _territoryManager.GetOperationsSummary() : new TerritoryOperationsSummary();

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
                        "{0} districts anchored | {1} charters live | {2} corridors active | {3} support sites secured | Ops {4}/wk",
                        _territoryManager != null ? _territoryManager.GetControlledDistrictCount() : 0,
                        operationsSummary != null ? operationsSummary.ActiveLicensedDistrictCount : 0,
                        _territoryManager != null ? _territoryManager.GetActiveCorridorCount() : 0,
                        supportSiteCount,
                        ModFormatting.FormatMoney(operationsSummary != null ? operationsSummary.WeeklyCost : 0f)),
                },
                new MenuItem
                {
                    CaptionFactory = () => "Metro Network",
                    DetailFactory = () => "Visualize district reputation and corridor rights in a network graph.",
                    OnActivate = OpenNetworkViewInternal,
                },
                new MenuItem
                {
                    CaptionFactory = () => "District View",
                    DetailFactory = () => "Review districts as a sortable strategic ledger.",
                    OnActivate = OpenDistrictMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Depot / Yard",
                    DetailFactory = () => "Lease or buy support sites and grow local crews.",
                    OnActivate = OpenDepotMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Back",
                    DetailFactory = () => "Return to the previous screen.",
                    OnActivate = ReturnToPreviousPage,
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
            var operationsByDistrict = BuildOperationsByDistrict();

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
                var reputationLabel = GetReputationLabel(district);
                TerritoryDistrictOperationsEntry districtOperations;
                operationsByDistrict.TryGetValue(district.DistrictName, out districtOperations);
                items.Add(new MenuItem
                {
                    CaptionFactory = () => string.Format("{0} {1}", district.DistrictName, FormatReputationLabel(reputationLabel)),
                    DetailFactory = () => string.Format(
                        "Influence {0} | Charter {1} | Depots {2} | Corridors {3}{4} | Ops {5}/wk",
                        ModFormatting.FormatPercent(district.InfluenceRatio * 100f),
                        GetDistrictLicenseLabel(district),
                        district.ControlledDepots,
                        district.RouteRights,
                        BuildDistrictRiskSuffix(districtOperations),
                        ModFormatting.FormatMoney(GetDistrictOperationsCost(operationsByDistrict, district.DistrictName))),
                    OnActivate = () => OpenDistrictDetailMenu(district.DistrictName, OpenDistrictMenu),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                DetailFactory = () => _returnAction != null ? "Return to the company hub." : "Return to the company map.",
                OnActivate = BackFromDistrictMenu,
            });

            _districtMenu.SetItems(items);
        }

        private void RebuildDistrictDetailMenuItems()
        {
            var items = new List<MenuItem>();
            var district = _territoryManager != null ? _territoryManager.GetDistrictState(_selectedDistrictName) : null;
            var operationsByDistrict = BuildOperationsByDistrict();
            TerritoryDistrictOperationsEntry districtOperations;
            operationsByDistrict.TryGetValue(_selectedDistrictName ?? string.Empty, out districtOperations);
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
                    OnActivate = BackFromDistrictDetailMenu,
                });
                _districtDetailMenu.SetItems(items);
                return;
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("{0} {1}", district.DistrictName, FormatReputationLabel(GetReputationLabel(district))),
                DetailFactory = () => string.Format(
                    "Influence score {0} | {1}Reputation score {2}~s~",
                    ModFormatting.FormatNumber(district.InfluenceScore),
                    GetReputationColorCode(GetReputationLabel(district)),
                    ModFormatting.FormatNumber(district.ReputationScore)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("Influence: {0}", ModFormatting.FormatPercent(district.InfluenceRatio * 100f)),
                DetailFactory = () => district.InfluenceRatio >= 0.6f
                    ? "District is established enough to support local fleet privileges and better delivery terms."
                    : "Grow deliveries, depots, and corridors here to anchor the district.",
                ProgressRatioFactory = () => Math.Max(0f, Math.Min(1f, district.InfluenceRatio)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => BuildDistrictLicenseCaption(district),
                DetailFactory = () => BuildDistrictLicenseDetail(district, districtOperations),
                OnActivate = () => ExecuteDistrictLicenseAction(district.DistrictName),
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
                    "Franchises {0} | Corridor rights {1} | Support bonus {2}{3}",
                    district.FranchiseSites,
                    district.RouteRights,
                    ModFormatting.FormatSignedPercent((_territoryManager != null ? _territoryManager.GetDistrictSupportBonus(district.DistrictName) : 0f) * 100f),
                    BuildDistrictRiskSuffix(districtOperations)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format(
                    "Competitive Climate: {0} / {1}",
                    ModFormatting.FormatPercent(Math.Max(0f, Math.Min(100f, district.CompetitivePressure * 100f))),
                    ModFormatting.FormatPercent(Math.Max(0f, Math.Min(100f, district.CompetitiveOpportunity * 100f)))),
                DetailFactory = () => string.Format(
                    "{0} | Visible traffic {1} | Competition wins {2}",
                    district.CompetitionStatus ?? string.Empty,
                    Math.Max(0, district.VisibleCompetitionCount),
                    Math.Max(0, district.CompetitiveWinCount)),
            });
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("Territory Operations: {0}/wk", ModFormatting.FormatMoney(districtOperations != null ? districtOperations.TotalWeeklyCost : 0f)),
                DetailFactory = () => BuildDistrictOperationsDetail(districtOperations),
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
                OnActivate = BackFromDistrictDetailMenu,
            });

            _districtDetailMenu.SetItems(items);
        }

        private void EnsureSelectedDistrictForNetwork()
        {
            var districts = GetOrderedDistricts();
            if (districts.Count == 0)
            {
                _selectedDistrictName = string.Empty;
                return;
            }

            if (districts.Any(district => string.Equals(district.DistrictName, _selectedDistrictName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _selectedDistrictName = districts[0].DistrictName;
        }

        private void MoveNetworkSelection(WinForms.Keys key)
        {
            var resolution = Screen.MainWindowResolution;
            var layouts = GetNetworkLayoutCache(resolution).Layouts;
            if (layouts.Count == 0)
            {
                _selectedDistrictName = string.Empty;
                return;
            }

            var current = layouts.FirstOrDefault(layout => string.Equals(layout.District.DistrictName, _selectedDistrictName, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                _selectedDistrictName = layouts[0].District.DistrictName;
                return;
            }

            IEnumerable<MetroNodeLayout> directionalCandidates = layouts.Where(layout => !string.Equals(layout.District.DistrictName, current.District.DistrictName, StringComparison.OrdinalIgnoreCase));
            Func<MetroNodeLayout, bool> filter;
            Func<MetroNodeLayout, float> scoreSelector;

            if (key == _controls.MenuLeft)
            {
                filter = layout => layout.CenterX < current.CenterX - 1f;
                scoreSelector = layout => ((current.CenterX - layout.CenterX) * 1.6f) + Math.Abs(current.CenterY - layout.CenterY);
            }
            else if (key == _controls.MenuRight)
            {
                filter = layout => layout.CenterX > current.CenterX + 1f;
                scoreSelector = layout => ((layout.CenterX - current.CenterX) * 1.6f) + Math.Abs(current.CenterY - layout.CenterY);
            }
            else if (key == _controls.MenuUp)
            {
                filter = layout => layout.CenterY < current.CenterY - 1f;
                scoreSelector = layout => ((current.CenterY - layout.CenterY) * 1.6f) + Math.Abs(current.CenterX - layout.CenterX);
            }
            else
            {
                filter = layout => layout.CenterY > current.CenterY + 1f;
                scoreSelector = layout => ((layout.CenterY - current.CenterY) * 1.6f) + Math.Abs(current.CenterX - layout.CenterX);
            }

            var next = directionalCandidates
                .Where(filter)
                .OrderBy(scoreSelector)
                .ThenBy(layout => layout.District.DistrictName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (next == null)
            {
                next = directionalCandidates
                    .OrderBy(scoreSelector)
                    .ThenBy(layout => layout.District.DistrictName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }

            if (next != null)
            {
                _selectedDistrictName = next.District.DistrictName;
            }
        }

        private void DrawNetworkView()
        {
            var resolution = Screen.MainWindowResolution;
            var networkViewSnapshot = GetNetworkViewSnapshot();
            var panelX = resolution.Width * 0.07f;
            var panelY = resolution.Height * 0.085f;
            var panelWidth = resolution.Width * 0.86f;
            var panelHeight = resolution.Height * 0.83f;
            var graphX = panelX + 22f;
            var graphY = panelY + 82f;
            var graphWidth = panelWidth * 0.60f;
            var graphHeight = panelHeight - 158f;
            var detailX = graphX + graphWidth + 20f;
            var detailY = graphY;
            var detailWidth = panelX + panelWidth - detailX - 22f;
            var detailHeight = graphHeight;
            var footerY = graphY + graphHeight + 16f;
            var footerHeight = panelY + panelHeight - footerY - 20f;

            DrawRect(resolution.Width, resolution.Height, panelX + 10f, panelY + 12f, panelWidth, panelHeight, Color.FromArgb(54, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, panelX, panelY, panelWidth, panelHeight, Color.FromArgb(226, 12, 18, 30));
            DrawRect(resolution.Width, resolution.Height, panelX, panelY, panelWidth, 7f, Color.FromArgb(236, 102, 177, 214));
            DrawRect(resolution.Width, resolution.Height, panelX + 2f, panelY + 2f, panelWidth - 4f, panelHeight - 4f, Color.FromArgb(218, 18, 28, 42));

            DrawTextLine(
                resolution,
                "Metro Network",
                panelX + 22f,
                panelY + 18f,
                0.45f,
                Color.FromArgb(244, 234, 242, 248),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left);
            DrawTextLine(
                resolution,
                "District reputation, corridor rights, and network posture",
                panelX + 22f,
                panelY + 44f,
                0.24f,
                Color.FromArgb(210, 171, 196, 216),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);

            var balanceText = string.Format(
                "Balance {0} | {1} anchored | {2} corridors",
                ModFormatting.FormatMoney(_getCurrentProfit != null ? _getCurrentProfit() : 0f),
                networkViewSnapshot.ControlledDistrictCount,
                networkViewSnapshot.ActiveCorridorCount);
            DrawTextLine(
                resolution,
                balanceText,
                panelX + panelWidth - 20f,
                panelY + 24f,
                0.22f,
                Color.FromArgb(228, 208, 221, 235),
                GTA.UI.Font.ChaletLondon,
                Alignment.Right);

            DrawRect(resolution.Width, resolution.Height, graphX, graphY, graphWidth, graphHeight, Color.FromArgb(148, 8, 13, 22));
            DrawRect(resolution.Width, resolution.Height, graphX + 2f, graphY + 2f, graphWidth - 4f, graphHeight - 4f, Color.FromArgb(204, 15, 23, 34));
            DrawRect(resolution.Width, resolution.Height, detailX, detailY, detailWidth, detailHeight, Color.FromArgb(148, 8, 13, 22));
            DrawRect(resolution.Width, resolution.Height, detailX + 2f, detailY + 2f, detailWidth - 4f, detailHeight - 4f, Color.FromArgb(208, 16, 24, 36));
            DrawRect(resolution.Width, resolution.Height, graphX, footerY, panelWidth - 44f, footerHeight, Color.FromArgb(148, 8, 13, 22));
            DrawRect(resolution.Width, resolution.Height, graphX + 2f, footerY + 2f, panelWidth - 48f, footerHeight - 4f, Color.FromArgb(204, 14, 22, 32));

            var layoutCache = GetNetworkLayoutCache(resolution, networkViewSnapshot);
            var layouts = layoutCache.Layouts;
            if (layouts.Count == 0)
            {
                DrawTextBlock(
                    resolution,
                    "No district data is available. Check scripts/LSOL_Config/Districts.xml and the loaded territory state.",
                    graphX + 24f,
                    graphY + 28f,
                    0.27f,
                    Color.FromArgb(224, 228, 232, 236),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    16f);
                DrawTextLine(
                    resolution,
                    "Backspace/Esc: return",
                    graphX + 24f,
                    footerY + 18f,
                    0.22f,
                    Color.FromArgb(196, 184, 198, 214),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left);
                return;
            }

            DrawNetworkCorridors(resolution, layoutCache, networkViewSnapshot.VisibleCorridors);
            for (int i = 0; i < layouts.Count; i++)
            {
                DrawNetworkNode(resolution, layouts[i]);
            }

            DrawNetworkDetailPanel(resolution, detailX, detailY, detailWidth, detailHeight, networkViewSnapshot);
            DrawNetworkFooter(resolution, graphX, footerY, panelWidth - 44f, footerHeight);
        }

        private void DrawNetworkCorridors(Size resolution, NetworkLayoutCache layoutCache, List<TerritoryCorridorState> visibleCorridors)
        {
            if (layoutCache == null || visibleCorridors == null || visibleCorridors.Count == 0)
            {
                return;
            }

            for (int i = 0; i < visibleCorridors.Count; i++)
            {
                var corridor = visibleCorridors[i];
                MetroNodeLayout left;
                MetroNodeLayout right;
                if (corridor == null
                    || !layoutCache.LayoutsByDistrict.TryGetValue(corridor.DistrictA ?? string.Empty, out left)
                    || !layoutCache.LayoutsByDistrict.TryGetValue(corridor.DistrictB ?? string.Empty, out right)
                    || left == null
                    || right == null)
                {
                    continue;
                }

                var highlight = string.IsNullOrWhiteSpace(_selectedDistrictName)
                    || string.Equals(_selectedDistrictName, corridor.DistrictA, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_selectedDistrictName, corridor.DistrictB, StringComparison.OrdinalIgnoreCase);
                var color = GetCorridorColor(corridor.RightLevel, highlight);
                var thickness = GetCorridorThickness(corridor.RightLevel);
                DrawMetroConnector(resolution, left.CenterX, left.CenterY, right.CenterX, right.CenterY, thickness, color);
            }
        }

        private void DrawNetworkNode(Size resolution, MetroNodeLayout layout)
        {
            if (layout == null || layout.District == null)
            {
                return;
            }

            const float nodeWidth = 132f;
            const float nodeHeight = 54f;
            var selected = string.Equals(layout.District.DistrictName, _selectedDistrictName, StringComparison.OrdinalIgnoreCase);
            var x = layout.CenterX - (nodeWidth * 0.5f);
            var y = layout.CenterY - (nodeHeight * 0.5f);
            var fill = GetDistrictColor(layout.District, selected);
            var frame = selected
                ? Color.FromArgb(236, 248, 250, 252)
                : Color.FromArgb(172, 66, 89, 112);

            DrawRect(resolution.Width, resolution.Height, x + 5f, y + 7f, nodeWidth, nodeHeight, Color.FromArgb(46, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, x, y, nodeWidth, nodeHeight, Color.FromArgb(214, 10, 15, 24));
            DrawRect(resolution.Width, resolution.Height, x + 2f, y + 2f, nodeWidth - 4f, nodeHeight - 4f, fill);
            DrawRect(resolution.Width, resolution.Height, x, y, nodeWidth, 3f, frame);
            DrawRect(resolution.Width, resolution.Height, x, y + nodeHeight - 3f, nodeWidth, 3f, frame);
            DrawRect(resolution.Width, resolution.Height, x, y, 3f, nodeHeight, frame);
            DrawRect(resolution.Width, resolution.Height, x + nodeWidth - 3f, y, 3f, nodeHeight, frame);

            DrawTextLine(
                resolution,
                AbbreviateDistrictName(layout.District.DistrictName, 18),
                x + 10f,
                y + 10f,
                0.26f,
                Color.FromArgb(242, 244, 247, 250),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);
            DrawTextLine(
                resolution,
                string.Format("{0} | {1}", GetReputationLabel(layout.District), ModFormatting.FormatPercent(layout.District.InfluenceRatio * 100f)),
                x + 10f,
                y + 30f,
                0.20f,
                Color.FromArgb(232, 219, 228, 237),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);
        }

        private void DrawNetworkDetailPanel(Size resolution, float x, float y, float width, float height, NetworkViewSnapshot snapshot)
        {
            TerritoryDistrictState district;
            if (snapshot == null
                || snapshot.DistrictsByName == null
                || !snapshot.DistrictsByName.TryGetValue(_selectedDistrictName ?? string.Empty, out district)
                || district == null)
            {
                DrawTextBlock(
                    resolution,
                    "Select a district to inspect its network posture.",
                    x + 18f,
                    y + 20f,
                    0.26f,
                    Color.FromArgb(228, 230, 233, 237),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    16f);
                return;
            }

            float supportBonus;
            if (snapshot.SupportBonusByDistrict == null
                || !snapshot.SupportBonusByDistrict.TryGetValue(district.DistrictName, out supportBonus))
            {
                supportBonus = 0f;
            }

            bool npcReady;
            if (snapshot.NpcReadyByDistrict == null
                || !snapshot.NpcReadyByDistrict.TryGetValue(district.DistrictName, out npcReady))
            {
                npcReady = false;
            }

            TerritoryDistrictOperationsEntry districtOperations;
            if (snapshot.OperationsByDistrict == null
                || !snapshot.OperationsByDistrict.TryGetValue(district.DistrictName, out districtOperations))
            {
                districtOperations = null;
            }

            List<TerritoryCorridorState> corridorLines;
            if (snapshot.VisibleCorridorsByDistrict == null
                || !snapshot.VisibleCorridorsByDistrict.TryGetValue(district.DistrictName, out corridorLines))
            {
                corridorLines = new List<TerritoryCorridorState>();
            }

            DrawTextLine(
                resolution,
                district.DistrictName,
                x + 18f,
                y + 18f,
                0.40f,
                Color.FromArgb(244, 238, 243, 248),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left);
            DrawTextLine(
                resolution,
                string.Format("{0} | Influence {1}", GetReputationLabel(district), ModFormatting.FormatPercent(district.InfluenceRatio * 100f)),
                x + 18f,
                y + 44f,
                0.23f,
                Color.FromArgb(224, 190, 211, 229),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);

            var supportBonusText = supportBonus * 100f;
            var npcStatus = npcReady
                ? "NPC Ready"
                : "NPC Locked";
            var operationsCost = GetDistrictOperationsCost(snapshot.OperationsByDistrict, district.DistrictName);
            var summary = string.Format(
                "Influence score {0}\nReputation score {1}\nSites {2} | Controlled {3} | Operational {4}\nCharter {5} | Activity {6:0}/{7:0} t\nDepots {8} | Franchises {9}\nCorridor rights {10} | Support bonus {11}{12}\nCompetition {13:0}% | Opportunity {14:0}% | Wins {15}\nTerritory ops {16} / week\n{17}",
                ModFormatting.FormatNumber(district.InfluenceScore),
                ModFormatting.FormatNumber(district.ReputationScore),
                district.SiteCount,
                district.ControlledSites,
                district.OperationalSites,
                GetDistrictLicenseLabel(district),
                district.CurrentWeekActivityTons,
                district.RequiredWeeklyActivityTons,
                district.ControlledDepots,
                district.FranchiseSites,
                district.RouteRights,
                ModFormatting.FormatSignedPercent(supportBonusText),
                BuildDistrictRiskSuffix(districtOperations),
                Math.Max(0f, Math.Min(100f, district.CompetitivePressure * 100f)),
                Math.Max(0f, Math.Min(100f, district.CompetitiveOpportunity * 100f)),
                Math.Max(0, district.CompetitiveWinCount),
                ModFormatting.FormatMoney(operationsCost),
                npcStatus);
            DrawTextBlock(
                resolution,
                summary,
                x + 18f,
                y + 82f,
                0.22f,
                Color.FromArgb(226, 228, 232, 238),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                16f);

            DrawRect(resolution.Width, resolution.Height, x + 16f, y + 196f, width - 32f, 2f, Color.FromArgb(88, 115, 149, 177));
            DrawTextLine(
                resolution,
                "Connected Corridors",
                x + 18f,
                y + 212f,
                0.28f,
                Color.FromArgb(236, 239, 243, 248),
                GTA.UI.Font.ChaletComprimeCologne,
                Alignment.Left);

            if (corridorLines.Count == 0)
            {
                DrawTextBlock(
                    resolution,
                    "No active corridors yet. Run cross-district deliveries to unlock them.",
                    x + 18f,
                    y + 242f,
                    0.22f,
                    Color.FromArgb(216, 202, 212, 223),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    16f);
                return;
            }

            var rowY = y + 246f;
            for (int i = 0; i < corridorLines.Count; i++)
            {
                var corridor = corridorLines[i];
                var corridorColor = GetCorridorColor(corridor.RightLevel, true);
                DrawRect(resolution.Width, resolution.Height, x + 18f, rowY + 2f, 12f, 12f, corridorColor);
                DrawTextLine(
                    resolution,
                    string.Format("{0}  {1}", GetOtherDistrictName(corridor, district.DistrictName), FormatCorridorLevel(corridor.RightLevel)),
                    x + 38f,
                    rowY,
                    0.22f,
                    Color.FromArgb(232, 232, 236, 241),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left);
                DrawTextLine(
                    resolution,
                    string.Format("{0} deliveries | {1}", corridor.DeliveryCount, ModFormatting.FormatTons(corridor.TotalDeliveredTons)),
                    x + width - 18f,
                    rowY,
                    0.20f,
                    Color.FromArgb(208, 184, 198, 212),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Right);
                DrawTextLine(
                    resolution,
                    corridor.UpkeepStatus ?? string.Empty,
                    x + 38f,
                    rowY + 14f,
                    0.18f,
                    Color.FromArgb(188, 176, 191, 206),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left);
                rowY += 34f;
            }
        }

        private void DrawNetworkFooter(Size resolution, float x, float y, float width, float height)
        {
            DrawTextLine(
                resolution,
                "Service Permit",
                x + 62f,
                y + 18f,
                0.21f,
                Color.FromArgb(220, 228, 233, 238),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);
            DrawTextLine(
                resolution,
                "Corridor",
                x + 222f,
                y + 18f,
                0.21f,
                Color.FromArgb(220, 228, 233, 238),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);
            DrawTextLine(
                resolution,
                "Priority",
                x + 342f,
                y + 18f,
                0.21f,
                Color.FromArgb(220, 228, 233, 238),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left);

            DrawRect(resolution.Width, resolution.Height, x + 18f, y + 22f, 34f, 4f, GetCorridorColor(CorridorRightLevel.ServicePermit, true));
            DrawRect(resolution.Width, resolution.Height, x + 178f, y + 22f, 34f, 6f, GetCorridorColor(CorridorRightLevel.Corridor, true));
            DrawRect(resolution.Width, resolution.Height, x + 294f, y + 22f, 34f, 8f, GetCorridorColor(CorridorRightLevel.Priority, true));

            DrawTextBlock(
                resolution,
                "Arrow keys move between districts. Enter opens district detail. Backspace/Esc returns.",
                x + 18f,
                y + 48f,
                0.22f,
                Color.FromArgb(216, 196, 207, 219),
                GTA.UI.Font.ChaletLondon,
                Alignment.Left,
                16f);

            DrawTextBlock(
                resolution,
                "Selected district keeps its connected corridors bright. Use district detail for charters, contract risk, and depot specialization.",
                x + width - 18f,
                y + 18f,
                0.20f,
                Color.FromArgb(198, 174, 188, 202),
                GTA.UI.Font.ChaletLondon,
                Alignment.Right,
                16f);
        }

        private void InvalidateNetworkViewCache()
        {
            _networkViewSnapshot = null;
            _networkLayoutCache = null;
        }

        private NetworkViewSnapshot GetNetworkViewSnapshot()
        {
            var stateFingerprint = BuildNetworkViewStateFingerprint();
            if (_networkViewSnapshot != null && _networkViewSnapshot.StateFingerprint == stateFingerprint)
            {
                return _networkViewSnapshot;
            }

            var orderedDistricts = GetOrderedDistricts();
            var districtsByName = new Dictionary<string, TerritoryDistrictState>(StringComparer.OrdinalIgnoreCase);
            var supportBonusByDistrict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var npcReadyByDistrict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < orderedDistricts.Count; i++)
            {
                var district = orderedDistricts[i];
                if (district == null || string.IsNullOrWhiteSpace(district.DistrictName))
                {
                    continue;
                }

                districtsByName[district.DistrictName] = district;
                supportBonusByDistrict[district.DistrictName] = _territoryManager != null
                    ? _territoryManager.GetDistrictSupportBonus(district.DistrictName)
                    : 0f;
                npcReadyByDistrict[district.DistrictName] = _territoryManager != null
                    && _territoryManager.IsDistrictEstablishedForNpc(district.DistrictName);
            }

            var visibleCorridors = _territoryManager != null
                ? _territoryManager.CorridorStates
                    .Where(corridor => corridor != null && corridor.RightLevel != CorridorRightLevel.None)
                    .ToList()
                : new List<TerritoryCorridorState>();
            var operationsSummary = _territoryManager != null
                ? (_territoryManager.GetOperationsSummary() ?? new TerritoryOperationsSummary())
                : new TerritoryOperationsSummary();

            _networkViewSnapshot = new NetworkViewSnapshot
            {
                StateFingerprint = stateFingerprint,
                OrderedDistricts = orderedDistricts,
                DistrictsByName = districtsByName,
                OperationsByDistrict = CreateOperationsByDistrictMap(operationsSummary),
                VisibleCorridors = visibleCorridors,
                VisibleCorridorsByDistrict = BuildVisibleCorridorsByDistrict(visibleCorridors),
                SupportBonusByDistrict = supportBonusByDistrict,
                NpcReadyByDistrict = npcReadyByDistrict,
                ControlledDistrictCount = orderedDistricts.Count(district => district != null && district.InfluenceRatio >= 0.6f),
                ActiveCorridorCount = visibleCorridors.Count,
            };

            if (_networkLayoutCache != null && _networkLayoutCache.StateFingerprint != stateFingerprint)
            {
                _networkLayoutCache = null;
            }

            return _networkViewSnapshot;
        }

        private NetworkLayoutCache GetNetworkLayoutCache(Size resolution, NetworkViewSnapshot snapshot = null)
        {
            snapshot = snapshot ?? GetNetworkViewSnapshot();
            if (_networkLayoutCache != null
                && _networkLayoutCache.StateFingerprint == snapshot.StateFingerprint
                && _networkLayoutCache.Resolution.Width == resolution.Width
                && _networkLayoutCache.Resolution.Height == resolution.Height)
            {
                return _networkLayoutCache;
            }

            var layouts = BuildNetworkLayouts(resolution, snapshot.OrderedDistricts);
            var layoutsByDistrict = new Dictionary<string, MetroNodeLayout>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout == null || layout.District == null || string.IsNullOrWhiteSpace(layout.District.DistrictName))
                {
                    continue;
                }

                layoutsByDistrict[layout.District.DistrictName] = layout;
            }

            _networkLayoutCache = new NetworkLayoutCache
            {
                StateFingerprint = snapshot.StateFingerprint,
                Resolution = resolution,
                Layouts = layouts,
                LayoutsByDistrict = layoutsByDistrict,
            };

            return _networkLayoutCache;
        }

        private List<TerritoryDistrictState> GetOrderedDistricts()
        {
            return _territoryManager != null
                ? _territoryManager.DistrictStates
                    .Where(district => district != null)
                    .OrderBy(district => district.DistrictName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<TerritoryDistrictState>();
        }

        private List<MetroNodeLayout> BuildNetworkLayouts(Size resolution, IList<TerritoryDistrictState> districts)
        {
            var layouts = new List<MetroNodeLayout>();
            if (districts == null || districts.Count == 0)
            {
                return layouts;
            }

            var panelX = resolution.Width * 0.07f;
            var panelY = resolution.Height * 0.085f;
            var panelWidth = resolution.Width * 0.86f;
            var panelHeight = resolution.Height * 0.83f;
            var graphX = panelX + 22f;
            var graphY = panelY + 82f;
            var graphWidth = panelWidth * 0.60f;
            var graphHeight = panelHeight - 158f;
            var centerX = graphX + (graphWidth * 0.5f);
            var centerY = graphY + (graphHeight * 0.5f);
            var radiusX = graphWidth * 0.34f;
            var radiusY = graphHeight * 0.35f;

            if (districts.Count == 1)
            {
                layouts.Add(new MetroNodeLayout { District = districts[0], CenterX = centerX, CenterY = centerY });
                return layouts;
            }

            for (int i = 0; i < districts.Count; i++)
            {
                var angle = (-Math.PI / 2d) + ((Math.PI * 2d * i) / districts.Count);
                var ringScale = districts.Count > 8 && (i % 2 == 1) ? 0.82f : 1f;
                layouts.Add(new MetroNodeLayout
                {
                    District = districts[i],
                    CenterX = centerX + ((float)Math.Cos(angle) * radiusX * ringScale),
                    CenterY = centerY + ((float)Math.Sin(angle) * radiusY * ringScale),
                });
            }

            return layouts;
        }

        private static void DrawMetroConnector(Size resolution, float x0, float y0, float x1, float y1, float thickness, Color color)
        {
            if (Math.Abs(x1 - x0) >= Math.Abs(y1 - y0))
            {
                var pivotX = x0 + ((x1 - x0) * 0.5f);
                DrawAxisLine(resolution, x0, y0, pivotX, y0, thickness, color);
                DrawAxisLine(resolution, pivotX, y0, pivotX, y1, thickness, color);
                DrawAxisLine(resolution, pivotX, y1, x1, y1, thickness, color);
                DrawRect(resolution.Width, resolution.Height, pivotX - (thickness * 0.5f), y0 - (thickness * 0.5f), thickness, thickness, color);
                DrawRect(resolution.Width, resolution.Height, pivotX - (thickness * 0.5f), y1 - (thickness * 0.5f), thickness, thickness, color);
                return;
            }

            var pivotY = y0 + ((y1 - y0) * 0.5f);
            DrawAxisLine(resolution, x0, y0, x0, pivotY, thickness, color);
            DrawAxisLine(resolution, x0, pivotY, x1, pivotY, thickness, color);
            DrawAxisLine(resolution, x1, pivotY, x1, y1, thickness, color);
            DrawRect(resolution.Width, resolution.Height, x0 - (thickness * 0.5f), pivotY - (thickness * 0.5f), thickness, thickness, color);
            DrawRect(resolution.Width, resolution.Height, x1 - (thickness * 0.5f), pivotY - (thickness * 0.5f), thickness, thickness, color);
        }

        private static void DrawAxisLine(Size resolution, float x0, float y0, float x1, float y1, float thickness, Color color)
        {
            if (Math.Abs(x1 - x0) >= Math.Abs(y1 - y0))
            {
                var left = Math.Min(x0, x1);
                DrawRect(resolution.Width, resolution.Height, left, y0 - (thickness * 0.5f), Math.Max(1f, Math.Abs(x1 - x0)), thickness, color);
                return;
            }

            var top = Math.Min(y0, y1);
            DrawRect(resolution.Width, resolution.Height, x0 - (thickness * 0.5f), top, thickness, Math.Max(1f, Math.Abs(y1 - y0)), color);
        }

        private static Color GetCorridorColor(CorridorRightLevel level, bool highlight)
        {
            var color = Color.FromArgb(160, 110, 126, 146);
            switch (level)
            {
                case CorridorRightLevel.Priority:
                    color = Color.FromArgb(220, 110, 206, 146);
                    break;
                case CorridorRightLevel.Corridor:
                    color = Color.FromArgb(220, 108, 174, 230);
                    break;
                case CorridorRightLevel.ServicePermit:
                    color = Color.FromArgb(220, 236, 186, 94);
                    break;
            }

            return highlight
                ? color
                : Color.FromArgb(62, color.R, color.G, color.B);
        }

        private static float GetCorridorThickness(CorridorRightLevel level)
        {
            switch (level)
            {
                case CorridorRightLevel.Priority:
                    return 8f;
                case CorridorRightLevel.Corridor:
                    return 6f;
                case CorridorRightLevel.ServicePermit:
                    return 4f;
                default:
                    return 2f;
            }
        }

        private static Color GetDistrictColor(TerritoryDistrictState district, bool selected)
        {
            var label = GetReputationLabel(district);
            var color = Color.FromArgb(212, 74, 88, 106);
            switch ((label ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DOMINANT":
                    color = Color.FromArgb(220, 146, 132, 62);
                    break;
                case "ANCHORED":
                    color = Color.FromArgb(220, 62, 128, 92);
                    break;
                case "ESTABLISHED":
                    color = Color.FromArgb(220, 58, 98, 156);
                    break;
                case "EMERGING":
                    color = Color.FromArgb(220, 144, 92, 58);
                    break;
            }

            if (!selected)
            {
                return Color.FromArgb(196, color.R, color.G, color.B);
            }

            return Color.FromArgb(236, Math.Min(255, color.R + 16), Math.Min(255, color.G + 16), Math.Min(255, color.B + 16));
        }

        private static string GetOtherDistrictName(TerritoryCorridorState corridor, string districtName)
        {
            if (corridor == null)
            {
                return string.Empty;
            }

            return string.Equals(corridor.DistrictA, districtName, StringComparison.OrdinalIgnoreCase)
                ? corridor.DistrictB
                : corridor.DistrictA;
        }

        private static Dictionary<string, TerritoryDistrictOperationsEntry> CreateOperationsByDistrictMap(TerritoryOperationsSummary summary)
        {
            return summary != null
                ? summary.Districts
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DistrictName))
                    .ToDictionary(entry => entry.DistrictName, entry => entry, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, TerritoryDistrictOperationsEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, List<TerritoryCorridorState>> BuildVisibleCorridorsByDistrict(List<TerritoryCorridorState> visibleCorridors)
        {
            var corridorsByDistrict = new Dictionary<string, List<TerritoryCorridorState>>(StringComparer.OrdinalIgnoreCase);
            if (visibleCorridors == null)
            {
                return corridorsByDistrict;
            }

            for (int i = 0; i < visibleCorridors.Count; i++)
            {
                var corridor = visibleCorridors[i];
                if (corridor == null)
                {
                    continue;
                }

                AddVisibleCorridor(corridorsByDistrict, corridor.DistrictA, corridor);
                AddVisibleCorridor(corridorsByDistrict, corridor.DistrictB, corridor);
            }

            foreach (var pair in corridorsByDistrict)
            {
                var districtName = pair.Key;
                pair.Value.Sort((left, right) => CompareCorridorsForDistrict(left, right, districtName));
                if (pair.Value.Count > 7)
                {
                    pair.Value.RemoveRange(7, pair.Value.Count - 7);
                }
            }

            return corridorsByDistrict;
        }

        private static void AddVisibleCorridor(IDictionary<string, List<TerritoryCorridorState>> corridorsByDistrict, string districtName, TerritoryCorridorState corridor)
        {
            if (corridorsByDistrict == null || corridor == null || string.IsNullOrWhiteSpace(districtName))
            {
                return;
            }

            List<TerritoryCorridorState> districtCorridors;
            if (!corridorsByDistrict.TryGetValue(districtName, out districtCorridors) || districtCorridors == null)
            {
                districtCorridors = new List<TerritoryCorridorState>();
                corridorsByDistrict[districtName] = districtCorridors;
            }

            districtCorridors.Add(corridor);
        }

        private static int CompareCorridorsForDistrict(TerritoryCorridorState left, TerritoryCorridorState right, string districtName)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var levelCompare = ((int)right.RightLevel).CompareTo((int)left.RightLevel);
            if (levelCompare != 0)
            {
                return levelCompare;
            }

            var tonsCompare = right.TotalDeliveredTons.CompareTo(left.TotalDeliveredTons);
            if (tonsCompare != 0)
            {
                return tonsCompare;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(
                GetOtherDistrictName(left, districtName),
                GetOtherDistrictName(right, districtName));
        }

        private long BuildNetworkViewStateFingerprint()
        {
            if (_territoryManager == null)
            {
                return 0L;
            }

            var fingerprint = 17L;
            fingerprint = CombineSignature(fingerprint, BuildUnorderedSignature(_territoryManager.DistrictStates, BuildDistrictStateHash));
            fingerprint = CombineSignature(fingerprint, BuildUnorderedSignature(_territoryManager.SiteStates, BuildSiteStateHash));
            fingerprint = CombineSignature(fingerprint, BuildUnorderedSignature(_territoryManager.CorridorStates, BuildCorridorStateHash));
            return fingerprint;
        }

        private static long BuildUnorderedSignature<T>(IEnumerable<T> items, Func<T, int> itemHashSelector)
            where T : class
        {
            unchecked
            {
                var count = 0L;
                var sum = 0L;
                var xor = 0L;
                var sumSquares = 0L;

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        var itemHash = (long)itemHashSelector(item);
                        count += 1L;
                        sum += itemHash;
                        xor ^= itemHash;
                        sumSquares += itemHash * itemHash;
                    }
                }

                var signature = 17L;
                signature = CombineSignature(signature, count);
                signature = CombineSignature(signature, sum);
                signature = CombineSignature(signature, xor);
                signature = CombineSignature(signature, sumSquares);
                return signature;
            }
        }

        private static int BuildDistrictStateHash(TerritoryDistrictState district)
        {
            if (district == null)
            {
                return 0;
            }

            var hash = 17;
            hash = CombineHash(hash, district.DistrictName);
            hash = CombineHash(hash, district.SiteCount);
            hash = CombineHash(hash, district.ControlledSites);
            hash = CombineHash(hash, district.OperationalSites);
            hash = CombineHash(hash, district.ControlledDepots);
            hash = CombineHash(hash, district.FranchiseSites);
            hash = CombineHash(hash, district.RouteRights);
            hash = CombineHash(hash, district.InfluenceScore);
            hash = CombineHash(hash, district.InfluenceRatio);
            hash = CombineHash(hash, district.ReputationScore);
            hash = CombineHash(hash, district.ReputationLabel);
            hash = CombineHash(hash, (int)district.LicenseStatus);
            hash = CombineHash(hash, district.LicenseStrikeCount);
            hash = CombineHash(hash, district.CurrentWeekActivityCount);
            hash = CombineHash(hash, district.CurrentWeekActivityTons);
            hash = CombineHash(hash, district.RequiredWeeklyActivityTons);
            hash = CombineHash(hash, district.WeeklyLicenseCost);
            hash = CombineHash(hash, district.CompetitivePressure);
            hash = CombineHash(hash, district.CompetitiveOpportunity);
            hash = CombineHash(hash, district.ActiveCompetitionJobs);
            hash = CombineHash(hash, district.VisibleCompetitionCount);
            hash = CombineHash(hash, district.LastCompetitiveTons);
            hash = CombineHash(hash, district.CompetitiveResponseCount);
            hash = CombineHash(hash, district.CompetitiveWinCount);
            hash = CombineHash(hash, district.CompetitionStatus);
            return hash;
        }

        private static int BuildSiteStateHash(TerritorySiteState siteState)
        {
            if (siteState == null)
            {
                return 0;
            }

            var hash = 17;
            hash = CombineHash(hash, siteState.SiteId);
            hash = CombineHash(hash, siteState.DistrictName);
            hash = CombineHash(hash, (int)siteState.ControlLevel);
            hash = CombineHash(hash, siteState.CrewAssigned);
            hash = CombineHash(hash, siteState.TotalDeliveries);
            hash = CombineHash(hash, siteState.TotalDeliveredTons);
            hash = CombineHash(hash, siteState.TotalLoadedTons);
            hash = CombineHash(hash, (int)siteState.FranchiseLevel);
            hash = CombineHash(hash, (int)siteState.EffectiveFranchiseLevel);
            hash = CombineHash(hash, siteState.LoaderCount);
            hash = CombineHash(hash, siteState.MechanicCount);
            hash = CombineHash(hash, siteState.GuardCount);
            hash = CombineHash(hash, siteState.ManagerCount);
            hash = CombineHash(hash, siteState.HasSpawnRights);
            hash = CombineHash(hash, siteState.IsOperational);
            hash = CombineHash(hash, (int)siteState.DepotSpecialization);
            hash = CombineHash(hash, siteState.CurrentWeekServiceDeliveries);
            hash = CombineHash(hash, siteState.CurrentWeekServiceTons);
            hash = CombineHash(hash, siteState.RequiredWeeklyServiceTons);
            hash = CombineHash(hash, siteState.ServicePenaltySteps);
            hash = CombineHash(hash, siteState.ServiceSuccessStreak);
            hash = CombineHash(hash, siteState.ServiceTargetMetLastWeek);
            hash = CombineHash(hash, siteState.ServiceContractStatus);
            return hash;
        }

        private static int BuildCorridorStateHash(TerritoryCorridorState corridor)
        {
            if (corridor == null)
            {
                return 0;
            }

            var hash = 17;
            hash = CombineHash(hash, corridor.CorridorId);
            hash = CombineHash(hash, corridor.DistrictA);
            hash = CombineHash(hash, corridor.DistrictB);
            hash = CombineHash(hash, corridor.DeliveryCount);
            hash = CombineHash(hash, corridor.TotalDeliveredTons);
            hash = CombineHash(hash, (int)corridor.RightLevel);
            hash = CombineHash(hash, corridor.CurrentWeekDeliveryCount);
            hash = CombineHash(hash, corridor.CurrentWeekDeliveredTons);
            hash = CombineHash(hash, corridor.RequiredWeeklyDeliveredTons);
            hash = CombineHash(hash, corridor.DecayPressure);
            hash = CombineHash(hash, corridor.UpkeepStatus);
            return hash;
        }

        private static long CombineSignature(long current, long value)
        {
            unchecked
            {
                return (current * 16777619L) ^ value;
            }
        }

        private static int CombineHash(int current, int value)
        {
            unchecked
            {
                return (current * 397) ^ value;
            }
        }

        private static int CombineHash(int current, float value)
        {
            return CombineHash(current, value.GetHashCode());
        }

        private static int CombineHash(int current, bool value)
        {
            return CombineHash(current, value ? 1 : 0);
        }

        private static int CombineHash(int current, string value)
        {
            return CombineHash(current, StringComparer.OrdinalIgnoreCase.GetHashCode(value ?? string.Empty));
        }

        private Dictionary<string, TerritoryDistrictOperationsEntry> BuildOperationsByDistrict()
        {
            return CreateOperationsByDistrictMap(
                _territoryManager != null
                    ? (_territoryManager.GetOperationsSummary() ?? new TerritoryOperationsSummary())
                    : new TerritoryOperationsSummary());
        }

        private static float GetDistrictOperationsCost(IDictionary<string, TerritoryDistrictOperationsEntry> operationsByDistrict, string districtName)
        {
            if (operationsByDistrict == null || string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            TerritoryDistrictOperationsEntry entry;
            return operationsByDistrict.TryGetValue(districtName, out entry) && entry != null
                ? Math.Max(0f, entry.TotalWeeklyCost)
                : 0f;
        }

        private static string BuildDistrictOperationsDetail(TerritoryDistrictOperationsEntry entry)
        {
            if (entry == null || entry.TotalWeeklyCost <= 0.01f)
            {
                return "No recurring territory obligations are attached to this district yet.";
            }

            var drivers = new List<string>();
            if (entry.AdministrationCost > 0.01f)
            {
                drivers.Add(string.Format("charter {0}", ModFormatting.FormatMoney(entry.AdministrationCost)));
            }

            var supportCost = entry.SupportSiteCost + entry.StaffCost;
            if (supportCost > 0.01f)
            {
                drivers.Add(string.Format("support {0}", ModFormatting.FormatMoney(supportCost)));
            }

            if (entry.CorridorCost > 0.01f)
            {
                drivers.Add(string.Format("corridors {0}", ModFormatting.FormatMoney(entry.CorridorCost)));
            }

            if (entry.FranchiseCost > 0.01f)
            {
                drivers.Add(string.Format("franchises {0}", ModFormatting.FormatMoney(entry.FranchiseCost)));
            }

            if (entry.LicenseStatus != DistrictLicenseStatus.None)
            {
                drivers.Add(string.Format("{0} {1:0}/{2:0} t", entry.LicenseStatus, entry.LicenseActivityTons, entry.LicenseTargetTons));
            }

            if (entry.CorridorRiskCount > 0 || entry.ServiceRiskCount > 0)
            {
                drivers.Add(string.Format("risk C{0}/S{1}", entry.CorridorRiskCount, entry.ServiceRiskCount));
            }

            return string.Format(
                "Weekly burden {0} | {1}",
                ModFormatting.FormatMoney(entry.TotalWeeklyCost),
                drivers.Count > 0 ? string.Join(" | ", drivers.ToArray()) : "No active drivers");
        }

        private static string FormatCorridorLevel(CorridorRightLevel level)
        {
            switch (level)
            {
                case CorridorRightLevel.ServicePermit:
                    return "Service Permit";
                case CorridorRightLevel.Corridor:
                    return "Corridor";
                case CorridorRightLevel.Priority:
                    return "Priority";
                default:
                    return "None";
            }
        }

        private static string AbbreviateDistrictName(string name, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length <= maxLength)
            {
                return name ?? string.Empty;
            }

            return name.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() + "…";
        }

        private static void DrawTextBlock(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment, float lineSpacing)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var lines = text.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                DrawTextLine(resolution, lines[i], x, y + (i * lineSpacing), scale, color, font, alignment);
            }
        }

        private static void DrawTextLine(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment)
        {
            var coords = ToScriptTextCoords(resolution, x, y);
            var normalizedX = coords.X / 1280f;
            var normalizedY = coords.Y / 720f;
            var wrapEnd = alignment == Alignment.Right ? normalizedX : 1f;

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_CENTRE, alignment == Alignment.Center);
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, alignment == Alignment.Right);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, wrapEnd);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, normalizedX, normalizedY, 0);
        }

        private static PointF ToScriptTextCoords(Size resolution, float x, float y)
        {
            const float scriptWidth = 1280f;
            const float scriptHeight = 720f;
            return new PointF(
                x * (scriptWidth / resolution.Width),
                y * (scriptHeight / resolution.Height));
        }

        private static void DrawRect(float screenWidth, float screenHeight, float x, float y, float width, float height, Color color)
        {
            var centerX = (x + (width * 0.5f)) / screenWidth;
            var centerY = (y + (height * 0.5f)) / screenHeight;
            var normalizedWidth = width / screenWidth;
            var normalizedHeight = height / screenHeight;

            Function.Call(Hash.DRAW_RECT, centerX, centerY, normalizedWidth, normalizedHeight, color.R, color.G, color.B, color.A);
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
                DetailFactory = () => _returnAction != null ? "Return to the company hub." : "Return to the company map.",
                OnActivate = BackFromDepotMenu,
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
            items.Add(new MenuItem
            {
                CaptionFactory = () => string.Format("Specialization: < {0} >", FormatDepotSpecialization(_territoryManager.GetDepotSpecialization(industry))),
                DetailFactory = () => _territoryManager.GetDepotSpecializationEffectSummary(industry),
                OnLeft = () => ChangeDepotSpecialization(industry, -1),
                OnRight = () => ChangeDepotSpecialization(industry, 1),
                OnActivate = () => ChangeDepotSpecialization(industry, 1),
            });

            AddSupportStaffItem(items, industry, DepotStaffRole.Loader, "Loaders");
            AddSupportStaffItem(items, industry, DepotStaffRole.Mechanic, "Mechanics");
            AddSupportStaffItem(items, industry, DepotStaffRole.Guard, "Guards");
            AddSupportStaffItem(items, industry, DepotStaffRole.Manager, "Managers");

            items.Add(new MenuItem
            {
                CaptionFactory = () => "District Bonus",
                DetailFactory = () => string.Format(
                    "Current district support bonus {0}. {1}",
                    ModFormatting.FormatSignedPercent((_territoryManager != null ? _territoryManager.GetDistrictSupportBonus(industry.DistrictName) : 0f) * 100f),
                    _territoryManager != null ? _territoryManager.GetDepotSpecializationEffectSummary(industry) : string.Empty),
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

        private void ExecuteDistrictLicenseAction(string districtName)
        {
            if (_acquireDistrictLicense == null || string.IsNullOrWhiteSpace(districtName))
            {
                return;
            }

            var message = _acquireDistrictLicense(districtName);
            if (!string.IsNullOrWhiteSpace(message) && _showStatus != null)
            {
                _showStatus(message);
            }

            RebuildDistrictDetailMenuItems();
            RebuildDistrictMenuItems();
            RebuildRootMenuItems();
        }

        private void ChangeDepotSpecialization(Industry industry, int delta)
        {
            if (_setDepotSpecialization == null || industry == null || _territoryManager == null)
            {
                return;
            }

            var options = new[]
            {
                DepotSpecialization.None,
                DepotSpecialization.Dispatch,
                DepotSpecialization.Maintenance,
                DepotSpecialization.Security,
                DepotSpecialization.Support,
            };
            var current = _territoryManager.GetDepotSpecialization(industry);
            var currentIndex = Array.IndexOf(options, current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var direction = delta == 0 ? 1 : delta;
            var nextIndex = (currentIndex + direction) % options.Length;
            if (nextIndex < 0)
            {
                nextIndex += options.Length;
            }

            var message = _setDepotSpecialization(industry, options[nextIndex]);
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

        private static string GetReputationLabel(TerritoryDistrictState district)
        {
            return district != null && !string.IsNullOrWhiteSpace(district.ReputationLabel)
                ? district.ReputationLabel
                : "Unknown";
        }

        private static string FormatReputationLabel(string reputationLabel)
        {
            var label = string.IsNullOrWhiteSpace(reputationLabel) ? "Unknown" : reputationLabel.Trim();
            return string.Format("[{0}{1}~s~]", GetReputationColorCode(label), label);
        }

        private static string GetReputationColorCode(string reputationLabel)
        {
            switch ((reputationLabel ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DOMINANT":
                    return "~y~";
                case "ANCHORED":
                    return "~g~";
                case "ESTABLISHED":
                    return "~b~";
                case "EMERGING":
                    return "~o~";
                default:
                    return "~c~";
            }
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
                + ((int)siteState.EffectiveFranchiseLevel * 6f);
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
                    : (siteState.ServicePenaltySteps > 0
                        ? "AT RISK"
                        : (siteState.EffectiveFranchiseLevel != TerritoryFranchiseLevel.None ? "CONTRACT" : "SETUP")));
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
            if (siteState.EffectiveFranchiseLevel != TerritoryFranchiseLevel.None)
            {
                detail += string.Format(" | Contract {0}", siteState.EffectiveFranchiseLevel);
            }
            else if (siteState.FranchiseLevel != TerritoryFranchiseLevel.None)
            {
                detail += string.Format(" | Franchise {0}", siteState.FranchiseLevel);
            }

            if (!string.IsNullOrWhiteSpace(siteState.ServiceContractStatus))
            {
                detail += string.Format(" | {0}", siteState.ServiceContractStatus);
            }

            if (siteState.RequiredWeeklyServiceTons > 0.01f)
            {
                detail += string.Format(" | {0:0}/{1:0} t", siteState.CurrentWeekServiceTons, siteState.RequiredWeeklyServiceTons);
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
                "{0} | {1} | {2} | Staff {3}/{4}/{5}/{6}",
                industry.DistrictName,
                _territoryManager.GetActivationSummary(industry),
                FormatDepotSpecialization(siteState.DepotSpecialization),
                siteState.LoaderCount,
                siteState.MechanicCount,
                siteState.GuardCount,
                siteState.ManagerCount);
        }

        private static string GetDistrictLicenseLabel(TerritoryDistrictState district)
        {
            if (district == null)
            {
                return "Unknown";
            }

            switch (district.LicenseStatus)
            {
                case DistrictLicenseStatus.Active:
                    return "Active";
                case DistrictLicenseStatus.Probation:
                    return "Probation";
                case DistrictLicenseStatus.Suspended:
                    return "Suspended";
                default:
                    return "Unlicensed";
            }
        }

        private string BuildDistrictLicenseCaption(TerritoryDistrictState district)
        {
            return string.Format("Operating Charter: {0}", GetDistrictLicenseLabel(district));
        }

        private string BuildDistrictLicenseDetail(TerritoryDistrictState district, TerritoryDistrictOperationsEntry entry)
        {
            if (district == null || _territoryManager == null)
            {
                return string.Empty;
            }

            var restoreCost = _territoryManager.GetDistrictLicenseEnrollmentCost(district.DistrictName);
            if (district.LicenseStatus == DistrictLicenseStatus.Suspended)
            {
                restoreCost *= 0.7f;
            }

            if (district.LicenseStatus == DistrictLicenseStatus.Active || district.LicenseStatus == DistrictLicenseStatus.Probation)
            {
                return string.Format(
                    "Weekly fee {0} | Activity {1:0}/{2:0} t | Better delivery returns and earlier spawn rights.",
                    ModFormatting.FormatMoney(district.WeeklyLicenseCost),
                    district.CurrentWeekActivityTons,
                    district.RequiredWeeklyActivityTons);
            }

            if (_territoryManager.IsDistrictLicensable(district.DistrictName) || district.LicenseStatus == DistrictLicenseStatus.Suspended)
            {
                return string.Format(
                    "Activate for {0}. Requires weekly activity {1:0} t to stay in compliance.",
                    ModFormatting.FormatMoney(restoreCost),
                    Math.Max(district.RequiredWeeklyActivityTons, entry != null ? entry.LicenseTargetTons : 0f));
            }

            return "Reach Established reputation before chartering this district. Starter-headquarters districts stay under home-market coverage.";
        }

        private static string BuildDistrictRiskSuffix(TerritoryDistrictOperationsEntry entry)
        {
            if (entry == null || (entry.CorridorRiskCount <= 0 && entry.ServiceRiskCount <= 0))
            {
                return string.Empty;
            }

            return string.Format(" | Risk C{0}/S{1}", entry.CorridorRiskCount, entry.ServiceRiskCount);
        }

        private static string FormatDepotSpecialization(DepotSpecialization specialization)
        {
            switch (specialization)
            {
                case DepotSpecialization.Dispatch:
                    return "Dispatch";
                case DepotSpecialization.Maintenance:
                    return "Maintenance";
                case DepotSpecialization.Security:
                    return "Security";
                case DepotSpecialization.Support:
                    return "Support";
                default:
                    return "General";
            }
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

        private sealed class MetroNodeLayout
        {
            public TerritoryDistrictState District { get; set; }
            public float CenterX { get; set; }
            public float CenterY { get; set; }
        }

        private sealed class NetworkViewSnapshot
        {
            public long StateFingerprint { get; set; }
            public List<TerritoryDistrictState> OrderedDistricts { get; set; }
            public Dictionary<string, TerritoryDistrictState> DistrictsByName { get; set; }
            public Dictionary<string, TerritoryDistrictOperationsEntry> OperationsByDistrict { get; set; }
            public List<TerritoryCorridorState> VisibleCorridors { get; set; }
            public Dictionary<string, List<TerritoryCorridorState>> VisibleCorridorsByDistrict { get; set; }
            public Dictionary<string, float> SupportBonusByDistrict { get; set; }
            public Dictionary<string, bool> NpcReadyByDistrict { get; set; }
            public int ControlledDistrictCount { get; set; }
            public int ActiveCorridorCount { get; set; }
        }

        private sealed class NetworkLayoutCache
        {
            public long StateFingerprint { get; set; }
            public Size Resolution { get; set; }
            public List<MetroNodeLayout> Layouts { get; set; }
            public Dictionary<string, MetroNodeLayout> LayoutsByDistrict { get; set; }
        }
    }
}
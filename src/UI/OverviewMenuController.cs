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
    public sealed class OverviewMenuController
    {
        private enum OverviewDetailReturnMenu
        {
            Industry = 0,
            Store = 1,
        }

        private readonly ControlBindings _controls;
        private readonly IndustryManager _industryManager;
        private readonly Action _closeAllMenus;
        private readonly Func<bool> _isLicensingEnabled;
        private readonly Func<float> _getCurrentProfit;
        private readonly Func<Industry, string> _purchaseContractorPermit;
        private readonly Action<string> _showStatus;
        private readonly List<Industry> _industryOverviewEntries;
        private readonly List<Industry> _storeOverviewEntries;
        private readonly List<Industry> _gasStationOverviewEntries;
        private readonly IndustryStatisticsSnapshotCache _industryStatisticsSnapshotCache;
        private readonly SimpleMenu _networkOverviewMenu;
        private readonly SimpleMenu _industryOverviewMenu;
        private readonly SimpleMenu _industryStatisticsMenu;
        private readonly SimpleMenu _industryPermitMenu;
        private readonly SimpleMenu _industryPermitConfirmMenu;
        private readonly SimpleMenu _storeOverviewMenu;
        private readonly SimpleMenu _industryDetailMenu;
        private readonly SimpleMenu _gasStationOverviewMenu;

        private Industry _inspectedIndustry;
        private Industry _pendingPermitIndustry;
        private int _industryDetailStatsScrollIndex;
        private OverviewDetailReturnMenu _detailReturnMenu;

        public OverviewMenuController(
            ControlBindings controls,
            IndustryManager industryManager,
            Action closeAllMenus,
            Func<bool> isLicensingEnabled,
            Func<float> getCurrentProfit,
            Func<Industry, string> purchaseContractorPermit,
            Action<string> showStatus)
        {
            _controls = controls;
            _industryManager = industryManager;
            _closeAllMenus = closeAllMenus;
            _isLicensingEnabled = isLicensingEnabled;
            _getCurrentProfit = getCurrentProfit;
            _purchaseContractorPermit = purchaseContractorPermit;
            _showStatus = showStatus;
            _industryOverviewEntries = BuildOverviewEntries(ExternalLocationKind.Industry);
            _storeOverviewEntries = BuildOverviewEntries(ExternalLocationKind.Store);
            _gasStationOverviewEntries = BuildOverviewEntries(ExternalLocationKind.GasStation);
            _industryStatisticsSnapshotCache = new IndustryStatisticsSnapshotCache();

            _networkOverviewMenu = new SimpleMenu("Network Overview")
            {
                Subtitle = "Inspect industries, stores, and gas stations",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.72f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
            };
            _industryOverviewMenu = new SimpleMenu("Industries Overview")
            {
                Subtitle = "Permits and statistics for tracked industries",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.80f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
            _industryStatisticsMenu = new SimpleMenu("Industry Statistics")
            {
                Subtitle = "Select an industry to inspect storage and production",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.98f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
            _industryPermitMenu = new SimpleMenu("Contractor Permit")
            {
                Subtitle = "Purchase industry transport permits",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.98f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
            _industryPermitConfirmMenu = new SimpleMenu("Contractor Permit")
            {
                Subtitle = "Confirm permit purchase",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.82f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
            _storeOverviewMenu = new SimpleMenu("Stores Overview")
            {
                Subtitle = "Select a store to inspect storage and demand",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.92f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
            _industryDetailMenu = new SimpleMenu("Industry Details")
            {
                Subtitle = "Conversion rate, inventories, and module levels",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.92f,
                TabletAlignRight = false,
                MaxVisibleItems = 8,
            };
            _gasStationOverviewMenu = new SimpleMenu("Gas Stations Overview")
            {
                Subtitle = "Review fuel storage across all stations",
                Theme = SimpleMenuTheme.Tablet,
                TabletWidthScale = 0.88f,
                TabletAlignRight = false,
                TabletCaptionScale = 0.46f,
                TabletDetailScale = 0.285f,
                TabletCaptionOffsetY = 18f,
                TabletDetailOffsetY = 49f,
                TabletMinRowHeight = 68f,
                MaxVisibleItems = 6,
            };
        }

        public bool AnyMenuOpen
        {
            get
            {
                return _networkOverviewMenu.IsOpen
                    || _industryOverviewMenu.IsOpen
                    || _industryStatisticsMenu.IsOpen
                    || _industryPermitMenu.IsOpen
                    || _industryPermitConfirmMenu.IsOpen
                    || _storeOverviewMenu.IsOpen
                    || _industryDetailMenu.IsOpen
                    || _gasStationOverviewMenu.IsOpen;
            }
        }

        public void Toggle()
        {
            if (AnyMenuOpen)
            {
                Close();
                return;
            }

            _closeAllMenus();
            OpenNetworkOverviewMenu();
        }

        public void Close()
        {
            _networkOverviewMenu.Close();
            _industryOverviewMenu.Close();
            _industryStatisticsMenu.Close();
            _industryPermitMenu.Close();
            _industryPermitConfirmMenu.Close();
            _storeOverviewMenu.Close();
            _industryDetailMenu.Close();
            _gasStationOverviewMenu.Close();
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (_industryPermitConfirmMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenIndustryPermitMenu();
                    return true;
                }

                _industryPermitConfirmMenu.HandleKey(key, _controls);
                return true;
            }

            if (_industryDetailMenu.IsOpen)
            {
                if (IsBackMenuKey(key) || key == _controls.MenuSelect)
                {
                    ReopenOverviewMenuFromDetail();
                    return true;
                }

                if (key == _controls.MenuUp)
                {
                    _industryDetailStatsScrollIndex = IndustryStatisticsPanelRenderer.MoveScrollIndex(_industryStatisticsSnapshotCache.GetSnapshot(_inspectedIndustry), _industryDetailStatsScrollIndex, -1);
                    return true;
                }

                if (key == _controls.MenuDown)
                {
                    _industryDetailStatsScrollIndex = IndustryStatisticsPanelRenderer.MoveScrollIndex(_industryStatisticsSnapshotCache.GetSnapshot(_inspectedIndustry), _industryDetailStatsScrollIndex, 1);
                    return true;
                }

                return true;
            }

            if (_industryOverviewMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenNetworkOverviewMenu();
                    return true;
                }

                _industryOverviewMenu.HandleKey(key, _controls);
                return true;
            }

            if (_industryStatisticsMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenIndustryOverviewMenu();
                    return true;
                }

                _industryStatisticsMenu.HandleKey(key, _controls);
                return true;
            }

            if (_industryPermitMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenIndustryOverviewMenu();
                    return true;
                }

                _industryPermitMenu.HandleKey(key, _controls);
                return true;
            }

            if (_storeOverviewMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenNetworkOverviewMenu();
                    return true;
                }

                _storeOverviewMenu.HandleKey(key, _controls);
                return true;
            }

            if (_gasStationOverviewMenu.IsOpen)
            {
                if (IsBackMenuKey(key))
                {
                    OpenNetworkOverviewMenu();
                    return true;
                }

                _gasStationOverviewMenu.HandleKey(key, _controls);
                return true;
            }

            if (_networkOverviewMenu.IsOpen)
            {
                _networkOverviewMenu.HandleKey(key, _controls);
                return true;
            }

            return false;
        }

        public void Draw()
        {
            if (_industryDetailMenu.IsOpen)
            {
                IndustryStatisticsPanelRenderer.DrawStandalone(
                    _inspectedIndustry,
                    _industryStatisticsSnapshotCache.GetSnapshot(_inspectedIndustry),
                    _industryDetailStatsScrollIndex,
                    "Arrow Up/Down to scroll | Enter or Backspace or Esc to return");
                return;
            }

            if (_industryOverviewMenu.IsOpen)
            {
                _industryOverviewMenu.Draw();
                return;
            }

            if (_industryStatisticsMenu.IsOpen)
            {
                _industryStatisticsMenu.Draw();
                return;
            }

            if (_industryPermitMenu.IsOpen)
            {
                _industryPermitMenu.Draw();
                return;
            }

            if (_industryPermitConfirmMenu.IsOpen)
            {
                _industryPermitConfirmMenu.Draw();
                return;
            }

            if (_storeOverviewMenu.IsOpen)
            {
                _storeOverviewMenu.Draw();
                return;
            }

            if (_gasStationOverviewMenu.IsOpen)
            {
                _gasStationOverviewMenu.Draw();
                return;
            }

            if (_networkOverviewMenu.IsOpen)
            {
                _networkOverviewMenu.Draw();
            }
        }

        private void OpenNetworkOverviewMenu()
        {
            Close();
            RebuildNetworkOverviewMenuItems();
            _networkOverviewMenu.Open();
        }

        private void OpenIndustryOverviewMenu()
        {
            Close();
            RebuildIndustryOverviewMenuItems();
            _industryOverviewMenu.Open();
        }

        private void OpenIndustryStatisticsMenu()
        {
            Close();
            RebuildIndustryStatisticsMenuItems();
            _industryStatisticsMenu.Open();
        }

        private void OpenIndustryPermitMenu()
        {
            if (!IsLicensingEnabled())
            {
                _showStatus?.Invoke("Licensing system is disabled for this save.");
                OpenIndustryOverviewMenu();
                return;
            }

            Close();
            RebuildIndustryPermitMenuItems();
            _industryPermitMenu.Open();
        }

        private void OpenIndustryPermitConfirmMenu(Industry industry)
        {
            if (industry == null)
            {
                return;
            }

            if (!industry.RequiresContractorPermit)
            {
                _showStatus?.Invoke(string.Format("{0} does not require a contractor permit.", industry.Name));
                return;
            }

            if (industry.HasContractorPermit)
            {
                _showStatus?.Invoke(string.Format("Contractor permit already purchased for {0}.", industry.Name));
                return;
            }

            _pendingPermitIndustry = industry;
            Close();
            RebuildIndustryPermitConfirmMenuItems();
            _industryPermitConfirmMenu.Open();
        }

        private void OpenStoreOverviewMenu()
        {
            Close();
            RebuildStoreOverviewMenuItems();
            _storeOverviewMenu.Open();
        }

        private void OpenGasStationOverviewMenu()
        {
            Close();
            RebuildGasStationOverviewMenuItems();
            _gasStationOverviewMenu.Open();
        }

        private void OpenIndustryDetailMenu(Industry industry)
        {
            OpenIndustryDetailMenu(industry, OverviewDetailReturnMenu.Industry);
        }

        private void OpenIndustryDetailMenu(Industry industry, OverviewDetailReturnMenu returnMenu)
        {
            if (industry == null)
            {
                return;
            }

            _inspectedIndustry = industry;
            _detailReturnMenu = returnMenu;
            _industryDetailStatsScrollIndex = 0;
            Close();
            _industryDetailMenu.Open();
        }

        private void ReopenOverviewMenuFromDetail()
        {
            switch (_detailReturnMenu)
            {
                case OverviewDetailReturnMenu.Store:
                    OpenStoreOverviewMenu();
                    break;
                default:
                    OpenIndustryStatisticsMenu();
                    break;
            }
        }

        private void RebuildNetworkOverviewMenuItems()
        {
            var industryOverviewDetail = string.Format("Permits and statistics for {0} tracked industry locations", _industryOverviewEntries.Count);
            var storeOverviewDetail = string.Format("{0} retail delivery locations", _storeOverviewEntries.Count);
            var gasStationOverviewDetail = string.Format("{0} fuel service stations", _gasStationOverviewEntries.Count);

            _networkOverviewMenu.Title = "Network Overview";
            _networkOverviewMenu.Subtitle = "Inspect industries, stores, and gas stations";
            _networkOverviewMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "INDUSTRY",
                    DetailFactory = () => industryOverviewDetail,
                    IdleBackgroundColor = Color.FromArgb(170, 46, 66, 50),
                    SelectedBackgroundColor = Color.FromArgb(205, 85, 124, 94),
                    OnActivate = OpenIndustryOverviewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "STORES OVERVIEW",
                    DetailFactory = () => storeOverviewDetail,
                    IdleBackgroundColor = Color.FromArgb(170, 63, 58, 42),
                    SelectedBackgroundColor = Color.FromArgb(206, 132, 120, 86),
                    OnActivate = OpenStoreOverviewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "GAS STATIONS OVERVIEW",
                    DetailFactory = () => gasStationOverviewDetail,
                    IdleBackgroundColor = Color.FromArgb(170, 45, 62, 74),
                    SelectedBackgroundColor = Color.FromArgb(206, 88, 125, 150),
                    OnActivate = OpenGasStationOverviewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "CLOSE",
                    DetailFactory = () => "Backspace or Enter closes this overview.",
                    IdleBackgroundColor = Color.FromArgb(170, 56, 45, 61),
                    SelectedBackgroundColor = Color.FromArgb(210, 132, 86, 158),
                    OnActivate = Close,
                },
            });
        }

        private void RebuildIndustryOverviewMenuItems()
        {
            var industries = _industryOverviewEntries;
            var items = new List<MenuItem>();

            if (industries.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No industries available",
                    DetailFactory = () => "No industry nodes are currently configured.",
                });
            }

            if (industries.Count > 0 && IsLicensingEnabled())
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "Contractor Permit",
                    DetailFactory = () => string.Format("Review permit prices for {0} industries.", industries.Count),
                    OnActivate = OpenIndustryPermitMenu,
                });
            }

            if (industries.Count > 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "Industry Statistics",
                    DetailFactory = () => string.Format("Open live statistics for {0} industries.", industries.Count),
                    OnActivate = OpenIndustryStatisticsMenu,
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenNetworkOverviewMenu,
            });

            _industryOverviewMenu.Title = "Industry";
            _industryOverviewMenu.Subtitle = IsLicensingEnabled()
                ? "Permits and live industry statistics"
                : "Live industry statistics";
            _industryOverviewMenu.SetItems(items);
        }

        private void RebuildIndustryStatisticsMenuItems()
        {
            var industries = _industryOverviewEntries;
            var items = new List<MenuItem>();

            for (int i = 0; i < industries.Count; i++)
            {
                var industry = industries[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetIndustryCaption(industry),
                    DetailFactory = () => GetIndustryOverviewDetail(industry),
                    OnActivate = () => OpenIndustryDetailMenu(industry),
                });
            }

            if (items.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No industries available",
                    DetailFactory = () => "No industry nodes are currently configured.",
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenIndustryOverviewMenu,
            });

            _industryStatisticsMenu.Title = "Industry Statistics";
            _industryStatisticsMenu.Subtitle = "Select an industry to inspect storage and production";
            _industryStatisticsMenu.SetItems(items);
        }

        private void RebuildIndustryPermitMenuItems()
        {
            var industries = _industryOverviewEntries;
            var items = new List<MenuItem>();

            for (int i = 0; i < industries.Count; i++)
            {
                var industry = industries[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetIndustryPermitCaption(industry),
                    DetailFactory = () => GetIndustryPermitDetail(industry),
                    OnActivate = () => OpenIndustryPermitConfirmMenu(industry),
                });
            }

            if (items.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No industries available",
                    DetailFactory = () => "No industry nodes are currently configured.",
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenIndustryOverviewMenu,
            });

            _industryPermitMenu.Title = "Contractor Permit";
            _industryPermitMenu.Subtitle = "Select an industry permit to purchase";
            _industryPermitMenu.SetItems(items);
        }

        private void RebuildIndustryPermitConfirmMenuItems()
        {
            var industry = _pendingPermitIndustry;
            if (industry == null)
            {
                _industryPermitConfirmMenu.Title = "Contractor Permit";
                _industryPermitConfirmMenu.Subtitle = "No industry selected";
                _industryPermitConfirmMenu.SetItems(new[]
                {
                    new MenuItem
                    {
                        CaptionFactory = () => "Back",
                        OnActivate = OpenIndustryPermitMenu,
                    },
                });
                return;
            }

            _industryPermitConfirmMenu.Title = "Contractor Permit";
            _industryPermitConfirmMenu.Subtitle = string.Format("Purchase permit for {0}?", industry.Name);
            _industryPermitConfirmMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "Yes",
                    DetailFactory = () => GetIndustryPermitConfirmationDetail(industry),
                    OnActivate = ConfirmIndustryPermitPurchase,
                },
                new MenuItem
                {
                    CaptionFactory = () => "No",
                    DetailFactory = () => "Return to the permit list.",
                    OnActivate = OpenIndustryPermitMenu,
                },
            });
        }

        private void ConfirmIndustryPermitPurchase()
        {
            var industry = _pendingPermitIndustry;
            if (industry == null)
            {
                OpenIndustryPermitMenu();
                return;
            }

            var result = _purchaseContractorPermit != null
                ? _purchaseContractorPermit(industry)
                : "Unable to purchase contractor permit.";

            if (!string.IsNullOrWhiteSpace(result))
            {
                _showStatus?.Invoke(result);
            }

            if (_industryManager.RequiresContractorPermit(industry))
            {
                RebuildIndustryPermitConfirmMenuItems();
                return;
            }

            _pendingPermitIndustry = null;
            OpenIndustryPermitMenu();
        }

        private void RebuildStoreOverviewMenuItems()
        {
            var stores = _storeOverviewEntries;
            var items = new List<MenuItem>();

            for (int i = 0; i < stores.Count; i++)
            {
                var store = stores[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetIndustryCaption(store),
                    DetailFactory = () => GetStoreOverviewDetail(store),
                    OnActivate = () => OpenIndustryDetailMenu(store, OverviewDetailReturnMenu.Store),
                });
            }

            if (items.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No stores available",
                    DetailFactory = () => "No retail store delivery targets are currently configured.",
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenNetworkOverviewMenu,
            });

            _storeOverviewMenu.Title = "Stores Overview";
            _storeOverviewMenu.Subtitle = "Select a store to inspect storage and demand";
            _storeOverviewMenu.SetItems(items);
        }

        private void RebuildGasStationOverviewMenuItems()
        {
            var stations = _gasStationOverviewEntries;
            var items = new List<MenuItem>();

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => GetIndustryCaption(station),
                    DetailFactory = () => GetGasStationOverviewDetail(station),
                });
            }

            if (items.Count == 0)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No gas stations available",
                    DetailFactory = () => "No petrol service stations are currently configured.",
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = OpenNetworkOverviewMenu,
            });

            _gasStationOverviewMenu.Title = "Gas Stations Overview";
            _gasStationOverviewMenu.Subtitle = "Review fuel storage across all stations";
            _gasStationOverviewMenu.SetItems(items);
        }

        private List<Industry> BuildOverviewEntries(ExternalLocationKind locationKind)
        {
            return _industryManager.Industries
                .Where(x => x != null && x.LocationKind == locationKind)
                .OrderBy(x => x.Name)
                .ToList();
        }

        private List<Industry> GetIndustriesForOverview()
        {
            return _industryOverviewEntries;
        }

        private List<Industry> GetStoresForOverview()
        {
            return _storeOverviewEntries;
        }

        private List<Industry> GetGasStationsForOverview()
        {
            return _gasStationOverviewEntries;
        }

        private static string GetIndustryOverviewDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var detail = string.Format(
                "Storage {0} | Omega {1}",
                ModFormatting.FormatTons(industry.GetInputStockTotal() + industry.GetOutputStockTotal()),
                ModFormatting.FormatTons(industry.OmegaStorage));

            var warning = industry.GetProductionWarning();
            if (!string.IsNullOrWhiteSpace(warning))
            {
                detail += string.Format(" | ~r~{0}~s~", warning);
            }

            return detail;
        }

        private string GetIndustryCaption(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var ownershipTag = _industryManager.IsIndustryOwnedForGameplay(industry)
                ? "~g~[OWNED]~s~"
                : "~r~[NOT OWNED]~s~";

            return string.Format("{0} {1}", industry.Name, ownershipTag);
        }

        private string GetIndustryPermitCaption(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var permitTag = !industry.RequiresContractorPermit
                ? "~g~[OPEN]~s~"
                : (industry.HasContractorPermit ? "~g~[PERMIT]~s~" : "~r~[LOCKED]~s~");

            return string.Format("{0} {1}", industry.Name, permitTag);
        }

        private string GetIndustryPermitDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (!industry.RequiresContractorPermit)
            {
                return "No permit required for this industry.";
            }

            var detail = string.Format("Permit {0}", ModFormatting.FormatMoney(industry.IndustryLicencePrice));
            if (industry.HasContractorPermit)
            {
                return detail + " | Transport unlocked.";
            }

            return detail;
        }

        private string GetIndustryPermitConfirmationDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var detail = string.Format(
                "Pay {0} to unlock cargo transport to and from this industry.",
                ModFormatting.FormatMoney(industry.IndustryLicencePrice));

            var currentProfit = GetCurrentProfit();
            if (currentProfit < industry.IndustryLicencePrice)
            {
                detail += string.Format(" Need {0} more.", ModFormatting.FormatMoney(industry.IndustryLicencePrice - currentProfit));
            }

            return detail;
        }

        private static string GetStoreOverviewDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var storage = industry.GetInputStockTotal();
            var fillPercent = ModMath.Clamp01(storage / Math.Max(1f, industry.InputCapacityTons)) * 100f;
            return string.Format("Storage {0} | {1} full", ModFormatting.FormatTons(storage), ModFormatting.FormatPercent(fillPercent));
        }

        private static string GetGasStationOverviewDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var storage = industry.GetInputStockTotal();
            var fillPercent = ModMath.Clamp01(storage / Math.Max(1f, industry.InputCapacityTons)) * 100f;
            return string.Format("Storage {0} | {1} full", ModFormatting.FormatTons(storage), ModFormatting.FormatPercent(fillPercent));
        }

        private bool IsBackMenuKey(WinForms.Keys key)
        {
            return key == _controls.MenuBack || key == WinForms.Keys.Escape;
        }

        private bool IsLicensingEnabled()
        {
            return _isLicensingEnabled != null && _isLicensingEnabled();
        }

        private float GetCurrentProfit()
        {
            return _getCurrentProfit != null ? _getCurrentProfit() : 0f;
        }

        private static bool IsPetrolServiceStation(Industry industry)
        {
            return industry != null && industry.IsGasStation;
        }

        private static bool IsStoreLocation(Industry industry)
        {
            return industry != null && industry.IsStore;
        }

    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;
using IndustryLogisticV.Systems;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.UI
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
        private readonly SimpleMenu _networkOverviewMenu;
        private readonly SimpleMenu _industryOverviewMenu;
        private readonly SimpleMenu _storeOverviewMenu;
        private readonly SimpleMenu _industryDetailMenu;
        private readonly SimpleMenu _gasStationOverviewMenu;

        private Industry _inspectedIndustry;
        private int _industryDetailStatsScrollIndex;
        private OverviewDetailReturnMenu _detailReturnMenu;

        public OverviewMenuController(ControlBindings controls, IndustryManager industryManager, Action closeAllMenus)
        {
            _controls = controls;
            _industryManager = industryManager;
            _closeAllMenus = closeAllMenus;

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
            get { return _networkOverviewMenu.IsOpen || _industryOverviewMenu.IsOpen || _storeOverviewMenu.IsOpen || _industryDetailMenu.IsOpen || _gasStationOverviewMenu.IsOpen; }
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
            _storeOverviewMenu.Close();
            _industryDetailMenu.Close();
            _gasStationOverviewMenu.Close();
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (_industryDetailMenu.IsOpen)
            {
                if (IsBackMenuKey(key) || key == _controls.MenuSelect)
                {
                    ReopenOverviewMenuFromDetail();
                    return true;
                }

                if (key == _controls.MenuUp)
                {
                    _industryDetailStatsScrollIndex = IndustryStatisticsPanelRenderer.MoveScrollIndex(_inspectedIndustry, _industryDetailStatsScrollIndex, -1);
                    return true;
                }

                if (key == _controls.MenuDown)
                {
                    _industryDetailStatsScrollIndex = IndustryStatisticsPanelRenderer.MoveScrollIndex(_inspectedIndustry, _industryDetailStatsScrollIndex, 1);
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
                    _industryDetailStatsScrollIndex,
                    "Arrow Up/Down to scroll | Enter or Backspace or Esc to return");
                return;
            }

            if (_industryOverviewMenu.IsOpen)
            {
                _industryOverviewMenu.Draw();
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
                    OpenIndustryOverviewMenu();
                    break;
            }
        }

        private void RebuildNetworkOverviewMenuItems()
        {
            _networkOverviewMenu.Title = "Network Overview";
            _networkOverviewMenu.Subtitle = "Inspect industries, stores, and gas stations";
            _networkOverviewMenu.SetItems(new[]
            {
                new MenuItem
                {
                    CaptionFactory = () => "INDUSTRIES OVERVIEW",
                    DetailFactory = () => string.Format("{0} tracked industry locations", GetIndustriesForOverview().Count),
                    IdleBackgroundColor = Color.FromArgb(170, 46, 66, 50),
                    SelectedBackgroundColor = Color.FromArgb(205, 85, 124, 94),
                    OnActivate = OpenIndustryOverviewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "STORES OVERVIEW",
                    DetailFactory = () => string.Format("{0} retail delivery locations", GetStoresForOverview().Count),
                    IdleBackgroundColor = Color.FromArgb(170, 63, 58, 42),
                    SelectedBackgroundColor = Color.FromArgb(206, 132, 120, 86),
                    OnActivate = OpenStoreOverviewMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "GAS STATIONS OVERVIEW",
                    DetailFactory = () => string.Format("{0} fuel service stations", GetGasStationsForOverview().Count),
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
            var industries = GetIndustriesForOverview();
            var items = new List<MenuItem>();

            for (int i = 0; i < industries.Count; i++)
            {
                var industry = industries[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => industry.Name,
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
                OnActivate = OpenNetworkOverviewMenu,
            });

            _industryOverviewMenu.Title = "Industries Overview";
            _industryOverviewMenu.Subtitle = "Select an industry to inspect storage and production";
            _industryOverviewMenu.SetItems(items);
        }

        private void RebuildStoreOverviewMenuItems()
        {
            var stores = GetStoresForOverview();
            var items = new List<MenuItem>();

            for (int i = 0; i < stores.Count; i++)
            {
                var store = stores[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => store.Name,
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
            var stations = GetGasStationsForOverview();
            var items = new List<MenuItem>();

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                items.Add(new MenuItem
                {
                    CaptionFactory = () => station.Name,
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

        private List<Industry> GetIndustriesForOverview()
        {
            return _industryManager.Industries
                .Where(x => x != null && !IsStoreLocation(x) && !IsPetrolServiceStation(x))
                .OrderBy(x => x.Name)
                .ToList();
        }

        private List<Industry> GetStoresForOverview()
        {
            return _industryManager.Industries
                .Where(IsStoreLocation)
                .OrderBy(x => x.Name)
                .ToList();
        }

        private List<Industry> GetGasStationsForOverview()
        {
            return _industryManager.Industries
                .Where(IsPetrolServiceStation)
                .OrderBy(x => x.Name)
                .ToList();
        }

        private static string GetIndustryOverviewDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var detail = string.Format(
                "Storage {0:0.0}t | Omega {1:0.0}t",
                industry.GetInputStockTotal() + industry.GetOutputStockTotal(),
                industry.OmegaStorage);

            var warning = industry.GetProductionWarning();
            if (!string.IsNullOrWhiteSpace(warning))
            {
                detail += string.Format(" | ~r~{0}~s~", warning);
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
            var fillPercent = Clamp01(storage / Math.Max(1f, industry.InputCapacityTons)) * 100f;
            return string.Format("Storage {0:0.0}t | {1:0}% full", storage, fillPercent);
        }

        private static string GetGasStationOverviewDetail(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            var storage = industry.GetInputStockTotal();
            var fillPercent = Clamp01(storage / Math.Max(1f, industry.InputCapacityTons)) * 100f;
            return string.Format("Storage {0:0.0}t | {1:0}% full", storage, fillPercent);
        }

        private bool IsBackMenuKey(WinForms.Keys key)
        {
            return key == _controls.MenuBack || key == WinForms.Keys.Escape;
        }

        private static bool IsPetrolServiceStation(Industry industry)
        {
            if (industry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(industry.Id) && industry.Id.IndexOf("Petrol Station", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return industry.IsSink && industry.Inputs.Count == 1 && industry.Inputs.Contains("Fuel");
        }

        private static bool IsStoreLocation(Industry industry)
        {
            if (industry == null || !industry.IsSink || IsPetrolServiceStation(industry))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(industry.Id) && industry.Id.StartsWith("Store", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var name = industry.Name ?? string.Empty;
            return name.IndexOf("Store", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Mall", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Native;
using LSOL.Config;
using LSOL.Domain;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    internal static class TabletAppIds
    {
        public const string Home = "home";
        public const string Network = "network";
        public const string Analytics = "analytics";
        public const string Industry = "industry";
        public const string Missions = "missions";
    }

    internal sealed class TabletRoute
    {
        public TabletRoute(string appId, string pageId, object payload = null)
        {
            AppId = string.IsNullOrWhiteSpace(appId) ? string.Empty : appId.Trim();
            PageId = string.IsNullOrWhiteSpace(pageId) ? string.Empty : pageId.Trim();
            Payload = payload;
        }

        public string AppId { get; private set; }

        public string PageId { get; private set; }

        public object Payload { get; private set; }
    }

    internal sealed class TabletShellPage
    {
        public TabletShellPage()
        {
            WidthScale = 0.92f;
            CaptionScale = 0.46f;
            DetailScale = 0.285f;
            CaptionOffsetY = 18f;
            DetailOffsetY = 49f;
            MinRowHeight = 68f;
            MinProgressRowHeight = 68f;
            MaxVisibleItems = 6;
            Layout = SimpleMenuTabletLayout.List;
            DashboardSidebarCount = 0;
            DashboardTileColumns = 5;
            BottomPanelHeight = 0f;
            FooterText = "Arrow Up/Down to navigate | Enter to select | Backspace/Esc to close";
            Items = Enumerable.Empty<MenuItem>();
        }

        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string HeaderRightText { get; set; }

        public string FooterText { get; set; }

        public float WidthScale { get; set; }

        public float CaptionScale { get; set; }

        public float DetailScale { get; set; }

        public float CaptionOffsetY { get; set; }

        public float DetailOffsetY { get; set; }

        public float MinRowHeight { get; set; }

        public float MinProgressRowHeight { get; set; }

        public int MaxVisibleItems { get; set; }

        public SimpleMenuTabletLayout Layout { get; set; }

        public int DashboardSidebarCount { get; set; }

        public int DashboardTileColumns { get; set; }

        public float BottomPanelHeight { get; set; }

        public Action<SimpleMenuTabletPanelContext> ContentRenderer { get; set; }

        public Action<SimpleMenuTabletPanelContext> BottomPanelRenderer { get; set; }

        public Action SelectAction { get; set; }

        public IEnumerable<MenuItem> Items { get; set; }
    }

    internal interface ITabletApp
    {
        string AppId { get; }

        TabletShellPage BuildPage(TabletShellContext context, TabletRoute route);
    }

    internal sealed class TabletShellContext
    {
        private readonly TabletShellController _shell;

        internal TabletShellContext(TabletShellController shell, TabletStateSnapshot snapshot)
        {
            _shell = shell;
            Snapshot = snapshot;
        }

        public TabletStateSnapshot Snapshot { get; private set; }

        public TabletStateStore StateStore
        {
            get { return _shell.StateStore; }
        }

        public void Navigate(string appId, string pageId, object payload = null)
        {
            _shell.Navigate(appId, pageId, payload);
        }

        public void Push(string appId, string pageId, object payload = null)
        {
            _shell.Push(appId, pageId, payload);
        }

        public void GoBack()
        {
            _shell.GoBack();
        }

        public void Close()
        {
            _shell.Close();
        }

        public void OpenHome()
        {
            _shell.OpenHome();
        }

        public void OpenNetwork()
        {
            _shell.OpenNetwork();
        }

        public void OpenIndustry(Industry industry)
        {
            _shell.OpenIndustry(industry);
        }

        public void Refresh()
        {
            _shell.RefreshState();
        }
    }

    internal sealed class TabletShellController
    {
        private readonly ControlBindings _controls;
        private readonly TabletStateStore _stateStore;
        private readonly Dictionary<string, ITabletApp> _appRegistry;
        private readonly List<TabletRoute> _navigationStack;
        private readonly SimpleMenu _surface;

        private TabletShellPage _activePage;
        private bool _pageDirty;
        private bool _resetSelectionOnNextRender;
        private int _lastStateVersion;

        public TabletShellController(ControlBindings controls, TabletStateStore stateStore)
        {
            _controls = controls ?? new ControlBindings();
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _appRegistry = new Dictionary<string, ITabletApp>(StringComparer.OrdinalIgnoreCase);
            _navigationStack = new List<TabletRoute>();
            _surface = new SimpleMenu("Tablet")
            {
                Theme = SimpleMenuTheme.Tablet,
                TabletAlignRight = false,
            };
            _pageDirty = true;
            _resetSelectionOnNextRender = true;
            _lastStateVersion = int.MinValue;
        }

        public bool IsOpen { get; private set; }

        public string ActiveAppId
        {
            get { return _navigationStack.Count == 0 ? string.Empty : _navigationStack[_navigationStack.Count - 1].AppId; }
        }

        internal TabletStateStore StateStore
        {
            get { return _stateStore; }
        }

        public void RegisterApp(ITabletApp app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.AppId))
            {
                return;
            }

            _appRegistry[app.AppId.Trim()] = app;
        }

        public void OpenHome()
        {
            Open(TabletAppIds.Home, "root");
        }

        public void OpenNetwork()
        {
            Open(TabletAppIds.Network, "root");
        }

        public void OpenIndustry(Industry industry)
        {
            Open(TabletAppIds.Industry, "main", industry);
        }

        public bool HandleKey(WinForms.Keys key)
        {
            if (!IsOpen)
            {
                return false;
            }

            if (key == _controls.Interact)
            {
                Close();
                return true;
            }

            if (key == _controls.MenuBack || key == WinForms.Keys.Escape)
            {
                if (_navigationStack.Count > 1)
                {
                    GoBack();
                }
                else
                {
                    Close();
                }

                return true;
            }

            _surface.HandleKey(key, _controls);
            if (!_surface.IsOpen)
            {
                if (_navigationStack.Count > 1)
                {
                    _surface.Open();
                    GoBack();
                }
                else
                {
                    Close();
                }
            }

            return true;
        }

        public void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            SuppressHudNoise();
            _stateStore.Update();
            if (_pageDirty || _lastStateVersion != _stateStore.Version)
            {
                RebuildActivePage();
            }

            _surface.Draw();
        }

        public void Close()
        {
            if (!IsOpen && !_surface.IsOpen)
            {
                return;
            }

            _surface.Close();
            _navigationStack.Clear();
            _activePage = null;
            _pageDirty = true;
            _resetSelectionOnNextRender = true;
            _lastStateVersion = int.MinValue;
            IsOpen = false;
        }

        internal void RefreshState()
        {
            _stateStore.MarkAllDirty();
            _pageDirty = true;
        }

        internal void Navigate(string appId, string pageId, object payload = null)
        {
            var route = CreateRoute(appId, pageId, payload);
            if (route == null)
            {
                return;
            }

            _navigationStack.Clear();
            _navigationStack.Add(route);
            IsOpen = true;
            _pageDirty = true;
            _resetSelectionOnNextRender = true;
            _stateStore.MarkAllDirty();
            RebuildActivePage();
        }

        internal void Push(string appId, string pageId, object payload = null)
        {
            var route = CreateRoute(appId, pageId, payload);
            if (route == null)
            {
                return;
            }

            if (!IsOpen)
            {
                Open(appId, pageId, payload);
                return;
            }

            _navigationStack.Add(route);
            _pageDirty = true;
            _resetSelectionOnNextRender = true;
        }

        internal void GoBack()
        {
            if (_navigationStack.Count <= 1)
            {
                Close();
                return;
            }

            _navigationStack.RemoveAt(_navigationStack.Count - 1);
            _pageDirty = true;
            _resetSelectionOnNextRender = true;
            RebuildActivePage();
        }

        private void Open(string appId, string pageId, object payload = null)
        {
            Navigate(appId, pageId, payload);
        }

        private TabletRoute CreateRoute(string appId, string pageId, object payload)
        {
            if (string.IsNullOrWhiteSpace(appId) || !_appRegistry.ContainsKey(appId.Trim()))
            {
                return null;
            }

            return new TabletRoute(appId, pageId, payload);
        }

        private void RebuildActivePage()
        {
            var route = _navigationStack.Count == 0 ? null : _navigationStack[_navigationStack.Count - 1];
            if (route == null)
            {
                Close();
                return;
            }

            ITabletApp app;
            if (!_appRegistry.TryGetValue(route.AppId, out app) || app == null)
            {
                Close();
                return;
            }

            var snapshot = _stateStore.GetSnapshot();
            var context = new TabletShellContext(this, snapshot);
            var page = app.BuildPage(context, route) ?? new TabletShellPage();

            ApplyPage(page, _resetSelectionOnNextRender);
            _activePage = page;
            _pageDirty = false;
            _resetSelectionOnNextRender = false;
            _lastStateVersion = _stateStore.Version;
            IsOpen = true;
            if (!_surface.IsOpen)
            {
                _surface.Open();
            }
        }

        private void ApplyPage(TabletShellPage page, bool resetSelection)
        {
            _surface.Title = page.Title ?? "Tablet";
            _surface.Subtitle = page.Subtitle ?? string.Empty;
            _surface.Theme = SimpleMenuTheme.Tablet;
            _surface.TabletWidthScale = page.WidthScale;
            _surface.TabletAlignRight = false;
            _surface.TabletLayout = page.Layout;
            _surface.TabletDashboardSidebarCount = page.DashboardSidebarCount;
            _surface.TabletDashboardTileColumns = page.DashboardTileColumns;
            _surface.TabletCaptionScale = page.CaptionScale;
            _surface.TabletDetailScale = page.DetailScale;
            _surface.TabletCaptionOffsetY = page.CaptionOffsetY;
            _surface.TabletDetailOffsetY = page.DetailOffsetY;
            _surface.TabletMinRowHeight = page.MinRowHeight;
            _surface.TabletMinProgressRowHeight = page.MinProgressRowHeight;
            _surface.MaxVisibleItems = page.MaxVisibleItems;
            _surface.TabletContentRenderer = page.ContentRenderer;
            _surface.TabletBottomPanelHeight = page.BottomPanelHeight;
            _surface.TabletBottomPanelRenderer = page.BottomPanelRenderer;
            _surface.TabletSelectAction = page.SelectAction;
            _surface.HeaderRightTextFactory = () => page.HeaderRightText ?? string.Empty;
            _surface.FooterTextFactory = () => page.FooterText ?? string.Empty;
            _surface.SetItems(page.Items ?? Enumerable.Empty<MenuItem>());
            if (resetSelection)
            {
                _surface.SetSelectedIndex(0);
            }
        }

        private static void SuppressHudNoise()
        {
            Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 6);
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 7);
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 8);
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 9);
        }
    }
}
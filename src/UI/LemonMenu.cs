using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.UI;
using LSOL.Config;
using LemonUI.Menus;
using WinForms = System.Windows.Forms;
using MenuAlignment = GTA.UI.Alignment;

namespace LSOL.UI
{
    public enum LemonMenuTheme
    {
        Default = 0,
        Accent = 1,
        Classic = 2,
    }

    public sealed class LemonMenu
    {
        private const int DynamicRefreshIntervalMs = 200;

        private readonly List<LemonMenuEntry> _entries;
        private readonly NativeMenu _menu;
        private bool _refreshRequested;
        private int _lastRefreshMs;

        public LemonMenu(string title)
        {
            _entries = new List<LemonMenuEntry>();
            _menu = new NativeMenu(title, string.Empty, string.Empty)
            {
                KeepNameCasing = true,
                AcceptsInput = false,
                DisableControls = true,
                MouseBehavior = MenuMouseBehavior.Disabled,
                ResetCursorWhenOpened = false,
            };

            _menu.Buttons.Visible = false;
            Title = title;
            Subtitle = string.Empty;
            Theme = LemonMenuTheme.Default;
            _refreshRequested = true;
            _lastRefreshMs = int.MinValue;
        }

        public string Title
        {
            get { return _menu.BannerText == null ? string.Empty : _menu.BannerText.Text; }
            set
            {
                var text = value ?? string.Empty;
                if (_menu.BannerText != null)
                {
                    _menu.BannerText.Text = text;
                }
            }
        }

        public string Subtitle
        {
            get { return _menu.Name; }
            set { _menu.Name = value ?? string.Empty; }
        }

        public bool AlignRight
        {
            get { return _menu.Alignment == MenuAlignment.Right; }
            set { _menu.Alignment = value ? MenuAlignment.Right : MenuAlignment.Left; }
        }

        public LemonMenuTheme Theme { get; set; }

        public float Width
        {
            get { return _menu.Width; }
            set { _menu.Width = value; }
        }

        public PointF Offset
        {
            get { return _menu.Offset; }
            set { _menu.Offset = value; }
        }

        public int MaxVisibleItems
        {
            get { return _menu.MaxItems; }
            set
            {
                if (value > 0)
                {
                    _menu.MaxItems = value;
                }
            }
        }

        public bool IsOpen
        {
            get { return _menu.Visible; }
        }

        public void SetItems(IEnumerable<MenuItem> items)
        {
            var selectedIndex = _menu.SelectedIndex;

            _entries.Clear();
            _menu.Clear();

            if (items != null)
            {
                foreach (var item in items)
                {
                    var entry = new LemonMenuEntry(item);
                    _entries.Add(entry);
                    _menu.Add(entry.Item);
                }
            }

            if (_menu.Items.Count > 0)
            {
                _menu.SelectedIndex = ClampIndex(selectedIndex, _menu.Items.Count);
            }

            Refresh(true);
        }

        public void Open()
        {
            Refresh(true);
            _menu.Visible = true;
        }

        public void Close()
        {
            _menu.Visible = false;
        }

        public void HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            if (!_menu.Visible)
            {
                return;
            }

            if (controls == null)
            {
                controls = new ControlBindings();
            }

            if (key == controls.MenuUp)
            {
                _menu.Previous();
                Refresh(true);
                return;
            }

            if (key == controls.MenuDown)
            {
                _menu.Next();
                Refresh(true);
                return;
            }

            var entry = GetSelectedEntry();
            if (entry == null)
            {
                if (key == controls.MenuBack || key == WinForms.Keys.Escape)
                {
                    Close();
                }

                return;
            }

            if (key == controls.MenuLeft)
            {
                if (entry.HandleLeft())
                {
                    _menu.SoundLeftRight?.PlayFrontend();
                    Refresh(true);
                }

                return;
            }

            if (key == controls.MenuRight)
            {
                if (entry.HandleRight())
                {
                    _menu.SoundLeftRight?.PlayFrontend();
                    Refresh(true);
                }

                return;
            }

            if (key == controls.MenuSelect)
            {
                if (entry.HandleActivate())
                {
                    _menu.SoundActivated?.PlayFrontend();
                    Refresh(true);
                }

                return;
            }

            if (key == controls.MenuBack || key == WinForms.Keys.Escape)
            {
                Close();
            }
        }

        public void Draw()
        {
            Refresh(false);
            _menu.Process();
        }

        private void Refresh(bool force)
        {
            if (!force && !_refreshRequested && Game.GameTime - _lastRefreshMs < DynamicRefreshIntervalMs)
            {
                return;
            }

            ApplyTheme();

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Refresh(Theme);
            }

            _refreshRequested = false;
            _lastRefreshMs = Game.GameTime;
        }

        private void ApplyTheme()
        {
            if (Theme == LemonMenuTheme.Default)
            {
                return;
            }

            var palette = AccessibilityTheme.Service.Palette;
            if (_menu.Banner != null)
            {
                _menu.Banner.Color = BuildBannerColor(Theme, palette);
            }

            if (_menu.BannerText != null)
            {
                _menu.BannerText.Color = palette.Get(ModColorRole.TextPrimary, 244);
            }
        }

        private static Color BuildBannerColor(LemonMenuTheme theme, AccessibilityPalette palette)
        {
            if (theme == LemonMenuTheme.Classic)
            {
                return BlendColors(
                    palette.Get(ModColorRole.BackgroundHeader),
                    palette.Get(ModColorRole.AccentGold),
                    0.12f,
                    228);
            }

            return palette.Get(ModColorRole.AccentBlue, 228);
        }

        private LemonMenuEntry GetSelectedEntry()
        {
            var selectedItem = _menu.SelectedItem;
            if (selectedItem == null)
            {
                return null;
            }

            return selectedItem.Tag as LemonMenuEntry;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private static ColorSet BuildColorSet(LemonMenuTheme theme)
        {
            var palette = AccessibilityTheme.Service.Palette;
            if (theme == LemonMenuTheme.Classic)
            {
                return new ColorSet
                {
                    TitleNormal = palette.Get(ModColorRole.TextPrimary, 235),
                    TitleHovered = palette.Get(ModColorRole.Highlight, 245),
                    TitleDisabled = palette.Get(ModColorRole.TextMuted, 175),
                    AltTitleNormal = palette.Get(ModColorRole.TextSecondary, 226),
                    AltTitleHovered = palette.Get(ModColorRole.TextPrimary, 242),
                    AltTitleDisabled = palette.Get(ModColorRole.TextMuted, 172),
                    ArrowsNormal = palette.Get(ModColorRole.AccentGold, 236),
                    ArrowsHovered = palette.Get(ModColorRole.Highlight, 244),
                    ArrowsDisabled = palette.Get(ModColorRole.TextMuted, 168),
                    BadgeLeftNormal = palette.Get(ModColorRole.AccentGold, 220),
                    BadgeLeftHovered = palette.Get(ModColorRole.AccentGold, 236),
                    BadgeLeftDisabled = palette.Get(ModColorRole.TextMuted, 168),
                    BadgeRightNormal = palette.Get(ModColorRole.AccentGold, 220),
                    BadgeRightHovered = palette.Get(ModColorRole.AccentGold, 236),
                    BadgeRightDisabled = palette.Get(ModColorRole.TextMuted, 168),
                    BackgroundNormal = BlendColors(
                        palette.Get(ModColorRole.BackgroundHeader),
                        palette.Get(ModColorRole.BackgroundCard),
                        0.35f,
                        194),
                    BackgroundHovered = BlendColors(
                        palette.Get(ModColorRole.BackgroundCard),
                        palette.Get(ModColorRole.AccentGold),
                        0.18f,
                        228),
                    BackgroundDisabled = BlendColors(
                        palette.Get(ModColorRole.BackgroundHeader),
                        palette.Get(ModColorRole.BackgroundPanel),
                        0.45f,
                        138),
                };
            }

            return new ColorSet
            {
                TitleNormal = palette.Get(ModColorRole.TextPrimary, 235),
                TitleHovered = palette.Get(ModColorRole.TextPrimary, 245),
                TitleDisabled = palette.Get(ModColorRole.TextMuted, 175),
                AltTitleNormal = palette.Get(ModColorRole.TextSecondary, 226),
                AltTitleHovered = palette.Get(ModColorRole.TextPrimary, 242),
                AltTitleDisabled = palette.Get(ModColorRole.TextMuted, 172),
                ArrowsNormal = palette.Get(ModColorRole.AccentGold, 236),
                ArrowsHovered = palette.Get(ModColorRole.Highlight, 244),
                ArrowsDisabled = palette.Get(ModColorRole.TextMuted, 168),
                BadgeLeftNormal = palette.Get(ModColorRole.AccentBlue, 220),
                BadgeLeftHovered = palette.Get(ModColorRole.AccentBlue, 236),
                BadgeLeftDisabled = palette.Get(ModColorRole.TextMuted, 168),
                BadgeRightNormal = palette.Get(ModColorRole.AccentGold, 220),
                BadgeRightHovered = palette.Get(ModColorRole.AccentGold, 236),
                BadgeRightDisabled = palette.Get(ModColorRole.TextMuted, 168),
                BackgroundNormal = palette.Get(ModColorRole.BackgroundCard, 190),
                BackgroundHovered = palette.Get(ModColorRole.BackgroundCardSelected, 226),
                BackgroundDisabled = palette.Get(ModColorRole.BackgroundCard, 132),
            };
        }

        private static Color BlendColors(Color baseColor, Color tintColor, float tintRatio, int alpha)
        {
            tintRatio = Math.Max(0f, Math.Min(1f, tintRatio));
            var baseWeight = 1f - tintRatio;
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, alpha)),
                (int)Math.Round((baseColor.R * baseWeight) + (tintColor.R * tintRatio)),
                (int)Math.Round((baseColor.G * baseWeight) + (tintColor.G * tintRatio)),
                (int)Math.Round((baseColor.B * baseWeight) + (tintColor.B * tintRatio)));
        }

        private sealed class LemonMenuEntry
        {
            private readonly MenuItem _source;

            public LemonMenuEntry(MenuItem source)
            {
                _source = source ?? new MenuItem();
                if (_source.IsSeparator)
                {
                    Item = new NativeSeparatorItem();
                }
                else
                {
                    Item = _source.CheckboxStateFactory != null
                        ? new NativeCheckboxItem(string.Empty, false)
                        : new NativeItem(string.Empty, string.Empty);
                }

                Item.Tag = this;
                Item.UseCustomBackground = !_source.IsSeparator;
            }

            public NativeItem Item { get; }

            public void Refresh(LemonMenuTheme theme)
            {
                Item.Title = InvokeString(_source.CaptionFactory);

                if (_source.IsSeparator)
                {
                    Item.Description = string.Empty;
                    Item.AltTitle = string.Empty;
                    Item.Enabled = false;
                    return;
                }

                Item.UseCustomBackground = theme != LemonMenuTheme.Default;
                if (theme != LemonMenuTheme.Default)
                {
                    Item.Colors = BuildColorSet(theme);
                }

                Item.Description = ResolveDescription();
                Item.AltTitle = ResolveAltTitle();

                var checkboxItem = Item as NativeCheckboxItem;
                if (checkboxItem != null && _source.CheckboxStateFactory != null)
                {
                    checkboxItem.Checked = InvokeBool(_source.CheckboxStateFactory);
                }

                Item.Enabled = _source.OnActivate != null || _source.OnLeft != null || _source.OnRight != null;
            }

            public bool HandleActivate()
            {
                if (_source.OnActivate == null)
                {
                    return false;
                }

                _source.OnActivate();
                return true;
            }

            public bool HandleLeft()
            {
                if (_source.OnLeft == null)
                {
                    return false;
                }

                _source.OnLeft();
                return true;
            }

            public bool HandleRight()
            {
                if (_source.OnRight == null)
                {
                    return false;
                }

                _source.OnRight();
                return true;
            }

            private string ResolveDescription()
            {
                var detail = InvokeString(_source.DetailFactory);
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return detail;
                }

                if (_source.CheckboxStateFactory != null)
                {
                    return _source.OnActivate != null ? ModLocalization.Service.Get(ModTextKey.LemonToggleHint) : string.Empty;
                }

                var hasLeftRight = _source.OnLeft != null || _source.OnRight != null;
                if (hasLeftRight && _source.OnActivate != null)
                {
                    return ModLocalization.Service.Get(ModTextKey.LemonAdjustHint);
                }

                if (hasLeftRight)
                {
                    return ModLocalization.Service.Get(ModTextKey.LemonCycleHint);
                }

                if (_source.OnActivate != null)
                {
                    return ModLocalization.Service.Get(ModTextKey.LemonUseHint);
                }

                return string.Empty;
            }

            private string ResolveAltTitle()
            {
                return string.Empty;
            }

            private static string InvokeString(Func<string> factory)
            {
                return factory == null ? string.Empty : factory() ?? string.Empty;
            }

            private static bool InvokeBool(Func<bool> factory)
            {
                return factory != null && factory();
            }
        }
    }
}
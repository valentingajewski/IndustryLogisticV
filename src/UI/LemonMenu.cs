using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.UI;
using IndustryLogisticV.Config;
using LemonUI.Menus;
using WinForms = System.Windows.Forms;
using MenuAlignment = GTA.UI.Alignment;

namespace IndustryLogisticV.UI
{
    public sealed class LemonMenu
    {
        private readonly List<LemonMenuEntry> _entries;
        private readonly NativeMenu _menu;

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

            Refresh();
        }

        public void Open()
        {
            Refresh();
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
                Refresh();
                return;
            }

            if (key == controls.MenuDown)
            {
                _menu.Next();
                Refresh();
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
                    Refresh();
                }

                return;
            }

            if (key == controls.MenuRight)
            {
                if (entry.HandleRight())
                {
                    _menu.SoundLeftRight?.PlayFrontend();
                    Refresh();
                }

                return;
            }

            if (key == controls.MenuSelect)
            {
                if (entry.HandleActivate())
                {
                    _menu.SoundActivated?.PlayFrontend();
                    Refresh();
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
            Refresh();
            _menu.Process();
        }

        private void Refresh()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Refresh();
            }
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

        private sealed class LemonMenuEntry
        {
            private readonly MenuItem _source;

            public LemonMenuEntry(MenuItem source)
            {
                _source = source ?? new MenuItem();
                Item = _source.CheckboxStateFactory != null
                    ? new NativeCheckboxItem(string.Empty, false)
                    : new NativeItem(string.Empty, string.Empty);
                Item.Tag = this;
            }

            public NativeItem Item { get; }

            public void Refresh()
            {
                Item.Title = InvokeString(_source.CaptionFactory);
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
                    return _source.OnActivate != null ? "Press Enter to toggle." : string.Empty;
                }

                var hasLeftRight = _source.OnLeft != null || _source.OnRight != null;
                if (hasLeftRight && _source.OnActivate != null)
                {
                    return "Left/Right to adjust.";
                }

                if (hasLeftRight)
                {
                    return "Left/Right to cycle options.";
                }

                if (_source.OnActivate != null)
                {
                    return "Press Enter to use this option.";
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
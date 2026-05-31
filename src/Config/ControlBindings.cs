using System;
using WinForms = System.Windows.Forms;

namespace LSOL.Config
{
    public sealed class ControlBindings
    {
        public const string ControlsSectionName = "Controls";

        public WinForms.Keys ToggleDashboard { get; set; } = WinForms.Keys.F8;
        public WinForms.Keys OpenModMenu { get; set; } = WinForms.Keys.F7;
        public WinForms.Keys OpenDebugMenu { get; set; } = WinForms.Keys.F9;
        public WinForms.Keys Interact { get; set; } = WinForms.Keys.E;
        public WinForms.Keys OpenUpgrade { get; set; } = WinForms.Keys.U;

        public WinForms.Keys MenuUp { get; set; } = WinForms.Keys.Up;
        public WinForms.Keys MenuDown { get; set; } = WinForms.Keys.Down;
        public WinForms.Keys MenuLeft { get; set; } = WinForms.Keys.Left;
        public WinForms.Keys MenuRight { get; set; } = WinForms.Keys.Right;
        public WinForms.Keys MenuSelect { get; set; } = WinForms.Keys.Enter;
        public WinForms.Keys MenuBack { get; set; } = WinForms.Keys.Back;

        public static ControlBindings LoadFromIni(IniFile iniFile, ControlBindings fallback = null)
        {
            var bindings = Clone(fallback ?? new ControlBindings());
            if (iniFile == null)
            {
                return bindings;
            }

            bindings.ToggleDashboard = ReadBinding(iniFile, "ToggleDashboard", bindings.ToggleDashboard);
            bindings.OpenModMenu = ReadBinding(iniFile, "OpenModMenu", bindings.OpenModMenu);
            bindings.OpenDebugMenu = ReadBinding(iniFile, "OpenDebugMenu", bindings.OpenDebugMenu);
            bindings.Interact = ReadBinding(iniFile, "Interact", bindings.Interact);
            return bindings;
        }

        public static WinForms.Keys ParseOrDefault(string raw, WinForms.Keys fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var normalized = raw.Trim().Replace(" ", string.Empty);
            if (normalized.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
            {
                return WinForms.Keys.Back;
            }

            WinForms.Keys parsed;
            if (Enum.TryParse(normalized, true, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static ControlBindings Clone(ControlBindings source)
        {
            return new ControlBindings
            {
                ToggleDashboard = source.ToggleDashboard,
                OpenModMenu = source.OpenModMenu,
                OpenDebugMenu = source.OpenDebugMenu,
                Interact = source.Interact,
                OpenUpgrade = source.OpenUpgrade,
                MenuUp = source.MenuUp,
                MenuDown = source.MenuDown,
                MenuLeft = source.MenuLeft,
                MenuRight = source.MenuRight,
                MenuSelect = source.MenuSelect,
                MenuBack = source.MenuBack,
            };
        }

        private static WinForms.Keys ReadBinding(IniFile iniFile, string key, WinForms.Keys fallback)
        {
            return ParseOrDefault(iniFile.GetString(ControlsSectionName, key, string.Empty), fallback);
        }
    }
}

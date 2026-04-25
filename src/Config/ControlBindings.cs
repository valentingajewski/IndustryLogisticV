using System;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.Config
{
    public sealed class ControlBindings
    {
        public WinForms.Keys ToggleDashboard { get; set; } = WinForms.Keys.F8;
        public WinForms.Keys ToggleContext { get; set; } = WinForms.Keys.F6;
        public WinForms.Keys Interact { get; set; } = WinForms.Keys.E;
        public WinForms.Keys GateInteract { get; set; } = WinForms.Keys.E;
        public WinForms.Keys OpenUpgrade { get; set; } = WinForms.Keys.U;

        public WinForms.Keys MenuUp { get; set; } = WinForms.Keys.Up;
        public WinForms.Keys MenuDown { get; set; } = WinForms.Keys.Down;
        public WinForms.Keys MenuLeft { get; set; } = WinForms.Keys.Left;
        public WinForms.Keys MenuRight { get; set; } = WinForms.Keys.Right;
        public WinForms.Keys MenuSelect { get; set; } = WinForms.Keys.Enter;
        public WinForms.Keys MenuBack { get; set; } = WinForms.Keys.Back;

        public WinForms.Keys DashboardPageUp { get; set; } = WinForms.Keys.Up;
        public WinForms.Keys DashboardPageDown { get; set; } = WinForms.Keys.Down;

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
    }
}

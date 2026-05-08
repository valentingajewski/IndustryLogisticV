using System;
using System.Collections.Generic;
using System.Drawing;

namespace LSOL.UI
{
    public enum ColorblindMode
    {
        Off = 0,
        Deuteranopia = 1,
        Protanopia = 2,
        Tritanopia = 3,
    }

    internal enum ModColorRole
    {
        AccentGold = 0,
        AccentBlue = 1,
        AccentGreen = 2,
        AccentOrange = 3,
        AccentRed = 4,
        AccentPurple = 5,
        AccentTeal = 6,
        BackgroundOuter = 7,
        BackgroundPanel = 8,
        BackgroundHeader = 9,
        BackgroundCard = 10,
        BackgroundCardSelected = 11,
        TextPrimary = 12,
        TextSecondary = 13,
        TextMuted = 14,
        Highlight = 15,
        ProgressIdle = 16,
        ProgressSelected = 17,
        ChartGrid = 18,
        ChartFill = 19,
    }

    internal sealed class AccessibilityPalette
    {
        private readonly Dictionary<ModColorRole, Color> _colors;

        public AccessibilityPalette(IDictionary<ModColorRole, Color> colors)
        {
            _colors = new Dictionary<ModColorRole, Color>(colors ?? throw new ArgumentNullException(nameof(colors)));
        }

        public Color Get(ModColorRole role, int alpha = -1)
        {
            Color color;
            if (!_colors.TryGetValue(role, out color))
            {
                color = Color.White;
            }

            return alpha >= 0 ? Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color) : color;
        }

        public Color AccentSeries(int index, int alpha = -1)
        {
            var role = index % 5;
            switch (role)
            {
                case 0:
                    return Get(ModColorRole.AccentGreen, alpha);
                case 1:
                    return Get(ModColorRole.AccentBlue, alpha);
                case 2:
                    return Get(ModColorRole.AccentOrange, alpha);
                case 3:
                    return Get(ModColorRole.AccentPurple, alpha);
                default:
                    return Get(ModColorRole.AccentTeal, alpha);
            }
        }
    }

    internal sealed class AccessibilityThemeService
    {
        private static readonly AccessibilityPalette OffPalette = new AccessibilityPalette(new Dictionary<ModColorRole, Color>
        {
            { ModColorRole.AccentGold, Color.FromArgb(227, 170, 58) },
            { ModColorRole.AccentBlue, Color.FromArgb(124, 178, 232) },
            { ModColorRole.AccentGreen, Color.FromArgb(118, 200, 176) },
            { ModColorRole.AccentOrange, Color.FromArgb(214, 168, 94) },
            { ModColorRole.AccentRed, Color.FromArgb(222, 92, 92) },
            { ModColorRole.AccentPurple, Color.FromArgb(132, 104, 178) },
            { ModColorRole.AccentTeal, Color.FromArgb(118, 192, 164) },
            { ModColorRole.BackgroundOuter, Color.FromArgb(8, 12, 18) },
            { ModColorRole.BackgroundPanel, Color.FromArgb(10, 16, 28) },
            { ModColorRole.BackgroundHeader, Color.FromArgb(17, 24, 34) },
            { ModColorRole.BackgroundCard, Color.FromArgb(29, 39, 60) },
            { ModColorRole.BackgroundCardSelected, Color.FromArgb(88, 124, 162) },
            { ModColorRole.TextPrimary, Color.FromArgb(242, 246, 252) },
            { ModColorRole.TextSecondary, Color.FromArgb(214, 225, 236) },
            { ModColorRole.TextMuted, Color.FromArgb(205, 216, 228) },
            { ModColorRole.Highlight, Color.FromArgb(249, 251, 255) },
            { ModColorRole.ProgressIdle, Color.FromArgb(88, 156, 220) },
            { ModColorRole.ProgressSelected, Color.FromArgb(244, 200, 96) },
            { ModColorRole.ChartGrid, Color.FromArgb(201, 212, 228) },
            { ModColorRole.ChartFill, Color.FromArgb(184, 196, 214) },
        });

        private static readonly AccessibilityPalette DeuteranopiaPalette = new AccessibilityPalette(new Dictionary<ModColorRole, Color>
        {
            { ModColorRole.AccentGold, Color.FromArgb(233, 185, 88) },
            { ModColorRole.AccentBlue, Color.FromArgb(108, 176, 240) },
            { ModColorRole.AccentGreen, Color.FromArgb(70, 196, 214) },
            { ModColorRole.AccentOrange, Color.FromArgb(232, 146, 82) },
            { ModColorRole.AccentRed, Color.FromArgb(214, 112, 168) },
            { ModColorRole.AccentPurple, Color.FromArgb(150, 118, 224) },
            { ModColorRole.AccentTeal, Color.FromArgb(100, 210, 190) },
            { ModColorRole.BackgroundOuter, Color.FromArgb(8, 12, 18) },
            { ModColorRole.BackgroundPanel, Color.FromArgb(10, 16, 28) },
            { ModColorRole.BackgroundHeader, Color.FromArgb(17, 24, 34) },
            { ModColorRole.BackgroundCard, Color.FromArgb(29, 39, 60) },
            { ModColorRole.BackgroundCardSelected, Color.FromArgb(78, 118, 176) },
            { ModColorRole.TextPrimary, Color.FromArgb(242, 246, 252) },
            { ModColorRole.TextSecondary, Color.FromArgb(214, 225, 236) },
            { ModColorRole.TextMuted, Color.FromArgb(205, 216, 228) },
            { ModColorRole.Highlight, Color.FromArgb(249, 251, 255) },
            { ModColorRole.ProgressIdle, Color.FromArgb(98, 168, 228) },
            { ModColorRole.ProgressSelected, Color.FromArgb(233, 185, 88) },
            { ModColorRole.ChartGrid, Color.FromArgb(201, 212, 228) },
            { ModColorRole.ChartFill, Color.FromArgb(184, 196, 214) },
        });

        private static readonly AccessibilityPalette ProtanopiaPalette = new AccessibilityPalette(new Dictionary<ModColorRole, Color>
        {
            { ModColorRole.AccentGold, Color.FromArgb(240, 192, 92) },
            { ModColorRole.AccentBlue, Color.FromArgb(104, 178, 242) },
            { ModColorRole.AccentGreen, Color.FromArgb(84, 204, 212) },
            { ModColorRole.AccentOrange, Color.FromArgb(226, 152, 96) },
            { ModColorRole.AccentRed, Color.FromArgb(184, 126, 218) },
            { ModColorRole.AccentPurple, Color.FromArgb(156, 116, 232) },
            { ModColorRole.AccentTeal, Color.FromArgb(90, 214, 196) },
            { ModColorRole.BackgroundOuter, Color.FromArgb(8, 12, 18) },
            { ModColorRole.BackgroundPanel, Color.FromArgb(10, 16, 28) },
            { ModColorRole.BackgroundHeader, Color.FromArgb(17, 24, 34) },
            { ModColorRole.BackgroundCard, Color.FromArgb(29, 39, 60) },
            { ModColorRole.BackgroundCardSelected, Color.FromArgb(82, 120, 182) },
            { ModColorRole.TextPrimary, Color.FromArgb(242, 246, 252) },
            { ModColorRole.TextSecondary, Color.FromArgb(214, 225, 236) },
            { ModColorRole.TextMuted, Color.FromArgb(205, 216, 228) },
            { ModColorRole.Highlight, Color.FromArgb(249, 251, 255) },
            { ModColorRole.ProgressIdle, Color.FromArgb(104, 172, 230) },
            { ModColorRole.ProgressSelected, Color.FromArgb(240, 192, 92) },
            { ModColorRole.ChartGrid, Color.FromArgb(201, 212, 228) },
            { ModColorRole.ChartFill, Color.FromArgb(184, 196, 214) },
        });

        private static readonly AccessibilityPalette TritanopiaPalette = new AccessibilityPalette(new Dictionary<ModColorRole, Color>
        {
            { ModColorRole.AccentGold, Color.FromArgb(236, 188, 88) },
            { ModColorRole.AccentBlue, Color.FromArgb(98, 188, 232) },
            { ModColorRole.AccentGreen, Color.FromArgb(126, 196, 122) },
            { ModColorRole.AccentOrange, Color.FromArgb(232, 138, 112) },
            { ModColorRole.AccentRed, Color.FromArgb(220, 112, 148) },
            { ModColorRole.AccentPurple, Color.FromArgb(166, 120, 230) },
            { ModColorRole.AccentTeal, Color.FromArgb(92, 210, 204) },
            { ModColorRole.BackgroundOuter, Color.FromArgb(8, 12, 18) },
            { ModColorRole.BackgroundPanel, Color.FromArgb(10, 16, 28) },
            { ModColorRole.BackgroundHeader, Color.FromArgb(17, 24, 34) },
            { ModColorRole.BackgroundCard, Color.FromArgb(29, 39, 60) },
            { ModColorRole.BackgroundCardSelected, Color.FromArgb(102, 132, 176) },
            { ModColorRole.TextPrimary, Color.FromArgb(242, 246, 252) },
            { ModColorRole.TextSecondary, Color.FromArgb(214, 225, 236) },
            { ModColorRole.TextMuted, Color.FromArgb(205, 216, 228) },
            { ModColorRole.Highlight, Color.FromArgb(249, 251, 255) },
            { ModColorRole.ProgressIdle, Color.FromArgb(122, 170, 214) },
            { ModColorRole.ProgressSelected, Color.FromArgb(236, 188, 88) },
            { ModColorRole.ChartGrid, Color.FromArgb(201, 212, 228) },
            { ModColorRole.ChartFill, Color.FromArgb(184, 196, 214) },
        });

        private AccessibilityPalette _palette;

        public AccessibilityThemeService()
        {
            Mode = ColorblindMode.Off;
            _palette = OffPalette;
        }

        public ColorblindMode Mode { get; private set; }

        public AccessibilityPalette Palette
        {
            get { return _palette; }
        }

        public bool SetMode(ColorblindMode mode)
        {
            if (Mode == mode)
            {
                return false;
            }

            Mode = mode;
            _palette = ResolvePalette(mode);
            return true;
        }

        private static AccessibilityPalette ResolvePalette(ColorblindMode mode)
        {
            switch (mode)
            {
                case ColorblindMode.Deuteranopia:
                    return DeuteranopiaPalette;
                case ColorblindMode.Protanopia:
                    return ProtanopiaPalette;
                case ColorblindMode.Tritanopia:
                    return TritanopiaPalette;
                default:
                    return OffPalette;
            }
        }
    }

    internal static class AccessibilityTheme
    {
        private static readonly AccessibilityThemeService ServiceInstance = new AccessibilityThemeService();

        public static AccessibilityThemeService Service
        {
            get { return ServiceInstance; }
        }
    }
}
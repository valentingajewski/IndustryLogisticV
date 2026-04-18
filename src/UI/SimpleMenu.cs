using System;
using System.Collections.Generic;
using System.Drawing;
using IndustryLogisticV.Config;
using GTA.Native;
using GTA.UI;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.UI
{
    public sealed class MenuItem
    {
        public Func<string> CaptionFactory { get; set; }
        public Action OnActivate { get; set; }
        public Action OnLeft { get; set; }
        public Action OnRight { get; set; }
    }

    public sealed class SimpleMenu
    {
        private readonly List<MenuItem> _items;

        public SimpleMenu(string title)
        {
            Title = title;
            Subtitle = string.Empty;
            _items = new List<MenuItem>();
            SelectedIndex = 0;
        }

        public string Title { get; set; }
        public string Subtitle { get; set; }
        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; }

        public void SetItems(IEnumerable<MenuItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
            if (SelectedIndex >= _items.Count)
            {
                SelectedIndex = Math.Max(0, _items.Count - 1);
            }
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            if (!IsOpen || _items.Count == 0)
            {
                return;
            }

            if (controls == null)
            {
                controls = new ControlBindings();
            }

            if (key == controls.MenuUp)
            {
                SelectedIndex = SelectedIndex <= 0 ? _items.Count - 1 : SelectedIndex - 1;
                return;
            }

            if (key == controls.MenuDown)
            {
                SelectedIndex = (SelectedIndex + 1) % _items.Count;
                return;
            }

            var selected = _items[SelectedIndex];
            if (key == controls.MenuLeft)
            {
                selected.OnLeft?.Invoke();
                return;
            }

            if (key == controls.MenuRight)
            {
                selected.OnRight?.Invoke();
                return;
            }

            if (key == controls.MenuSelect)
            {
                selected.OnActivate?.Invoke();
                return;
            }

            if (key == controls.MenuBack || key == WinForms.Keys.Escape)
            {
                Close();
            }
        }

        public void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            var resolution = GTA.UI.Screen.MainWindowResolution;
            var x = resolution.Width * 0.56f;
            var y = resolution.Height * 0.15f;
            var width = resolution.Width * 0.38f;
            var lineHeight = resolution.Height * 0.038f;
            var contentHeight = lineHeight * Math.Max(1, _items.Count);
            var headerHeight = lineHeight * 1.55f;
            var footerHeight = lineHeight * 0.86f;
            var height = contentHeight + headerHeight + footerHeight + 10f;

            DrawRect(resolution.Width, resolution.Height, x + 6f, y + 6f, width, height, Color.FromArgb(95, 8, 10, 16));
            DrawRect(resolution.Width, resolution.Height, x, y, width, height, Color.FromArgb(206, 8, 12, 18));
            DrawRect(resolution.Width, resolution.Height, x, y, width, 5f, Color.FromArgb(238, 227, 170, 58));
            DrawRect(resolution.Width, resolution.Height, x, y + 5f, width, headerHeight - 5f, Color.FromArgb(194, 17, 24, 34));

            new TextElement(
                    Title,
                    ToScriptTextCoords(resolution, x + 12f, y + 8f),
                    0.40f,
                    Color.White,
                    GTA.UI.Font.ChaletComprimeCologne,
                    Alignment.Left,
                    true,
                    false)
                .Draw();

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                new TextElement(
                        Subtitle,
                        ToScriptTextCoords(resolution, x + 12f, y + 30f),
                        0.27f,
                        Color.FromArgb(236, 224, 232, 238),
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();
            }

            for (int i = 0; i < _items.Count; i++)
            {
                var rowY = y + headerHeight + (lineHeight * i);
                if (i % 2 == 0)
                {
                    DrawRect(resolution.Width, resolution.Height, x, rowY, width, lineHeight, Color.FromArgb(34, 255, 255, 255));
                }

                if (i == SelectedIndex)
                {
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, width - 4f, lineHeight - 4f, Color.FromArgb(220, 212, 164, 72));
                    DrawRect(resolution.Width, resolution.Height, x + 2f, rowY + 2f, 5f, lineHeight - 4f, Color.FromArgb(240, 252, 246, 220));
                }

                var captionFactory = _items[i].CaptionFactory;
                var caption = captionFactory != null ? captionFactory() : string.Empty;
                var color = i == SelectedIndex ? Color.White : Color.FromArgb(235, 220, 230, 240);
                new TextElement(
                        caption,
                        ToScriptTextCoords(resolution, x + 11f, rowY + 6f),
                        0.30f,
                        color,
                        GTA.UI.Font.ChaletLondon,
                        Alignment.Left,
                        true,
                        false)
                    .Draw();
            }

            var footerY = y + headerHeight + contentHeight;
            DrawRect(resolution.Width, resolution.Height, x, footerY, width, footerHeight, Color.FromArgb(185, 14, 20, 28));
            new TextElement(
                    "Navigate | Edit | Select | Close",
                    ToScriptTextCoords(resolution, x + 12f, footerY + 4f),
                    0.255f,
                    Color.FromArgb(228, 214, 223, 233),
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false)
                .Draw();
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
            var normalizedW = width / screenWidth;
            var normalizedH = height / screenHeight;

            Function.Call(Hash.DRAW_RECT, centerX, centerY, normalizedW, normalizedH, color.R, color.G, color.B, color.A);
        }
    }
}

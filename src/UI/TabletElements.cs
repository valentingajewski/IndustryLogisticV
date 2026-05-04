using System;
using System.Collections;
using System.Collections.Generic;
using LemonUI.Elements;

namespace LSOL.UI
{
    public sealed class CanvasElement : IEnumerable<BaseElement>
    {
        private readonly List<BaseElement> _elements = new List<BaseElement>();

        public int Count
        {
            get { return _elements.Count; }
        }

        public void Add(BaseElement element)
        {
            if (element == null)
            {
                return;
            }

            _elements.Add(element);
        }

        public void Clear()
        {
            _elements.Clear();
        }

        public void Draw()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                _elements[i].Draw();
            }
        }

        public IEnumerator<BaseElement> GetEnumerator()
        {
            return _elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class ScreenElement
    {
        public ScreenElement(CanvasElement canvas)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            Canvas = canvas;
            Visible = true;
        }

        public CanvasElement Canvas { get; }

        public bool Visible { get; set; }

        public void Draw()
        {
            if (!Visible)
            {
                return;
            }

            Canvas.Draw();
        }
    }
}

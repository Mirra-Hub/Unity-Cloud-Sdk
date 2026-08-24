using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Minimal screen navigator: a host shows the top screen of a back stack. SetRoot resets
    /// the stack (auth ↔ services); Push/Back move within it (services → detail → back).
    /// </summary>
    public sealed class Nav
    {
        private readonly VisualElement _host;
        private readonly List<VisualElement> _stack = new List<VisualElement>();

        public Nav(VisualElement host)
        {
            _host = host;
        }

        public bool CanGoBack => _stack.Count > 1;

        public void SetRoot(VisualElement screen)
        {
            _stack.Clear();
            _stack.Add(screen);
            Render();
        }

        public void Push(VisualElement screen)
        {
            _stack.Add(screen);
            Render();
        }

        public void Back()
        {
            if (_stack.Count <= 1)
            {
                return;
            }
            _stack.RemoveAt(_stack.Count - 1);
            Render();
        }

        private void Render()
        {
            _host.Clear();
            if (_stack.Count > 0)
            {
                _host.Add(_stack[_stack.Count - 1]);
            }
        }
    }
}

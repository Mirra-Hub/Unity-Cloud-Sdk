using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Tab strip over lazily built panes. Replaces the button-list + active-class + body-swap triple
    /// the service views hand-roll: a pane's factory runs on its first selection only and the built
    /// element is kept (hidden) afterwards, so switching back never re-issues its SDK calls.
    /// </summary>
    public sealed class Tabs : VisualElement
    {
        private readonly ScrollView _strip;
        private readonly VisualElement _content;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<string> _titles = new List<string>();
        private readonly List<Func<VisualElement>> _builders = new List<Func<VisualElement>>();
        private readonly List<VisualElement> _panes = new List<VisualElement>();
        private int _selected = -1;

        public Tabs()
        {
            _strip = new ScrollView(ScrollViewMode.Horizontal);
            _strip.AddToClassList("sc-tabs__strip");
            _strip.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            _strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            // buttons land in the ScrollView's content container, so that is the row which reuses
            // the existing .sc-tabs styling (the USS pins it to nowrap — wrapping kills scrolling)
            _strip.contentContainer.AddToClassList("sc-tabs");
            Add(_strip);

            _content = new VisualElement();
            _content.AddToClassList("sc-tabs__content");
            Add(_content);
        }

        /// <summary>
        /// Raised with the new index every time the selection actually changes — including the
        /// automatic selection of the first tab, which happens inside <see cref="Add(string,Func{VisualElement})"/>.
        /// </summary>
        public event Action<int> SelectionChanged;

        /// <summary>Index of the visible tab, or -1 while no tab has been added.</summary>
        public int SelectedIndex => _selected;

        public int Count => _buttons.Count;

        public Tabs Add(string title, Func<VisualElement> build)
        {
            return Add(title, null, build);
        }

        public Tabs Add(string title, string glyph, Func<VisualElement> build)
        {
            string label = title ?? string.Empty;

            var btn = new Button();
            btn.AddToClassList("sc-tab");
            if (string.IsNullOrEmpty(glyph))
            {
                btn.text = label;
            }
            else
            {
                // a button paints a single font, so the Lucide glyph needs its own label
                var g = new Label(glyph);
                g.AddToClassList("sc-tab__glyph");
                g.AddToClassList("sc-icon");
                btn.Add(g);

                var t = new Label(label);
                t.enableRichText = false;
                btn.Add(t);
            }

            int index = _buttons.Count;
            btn.clicked += () => Select(index);

            _buttons.Add(btn);
            _titles.Add(label);
            _builders.Add(build);
            _panes.Add(null);
            _strip.Add(btn);

            if (_buttons.Count == 1)
            {
                Select(0);
            }
            return this;
        }

        public Tabs Select(int index)
        {
            if (index < 0 || index >= _buttons.Count)
            {
                Debug.LogWarning("[Showcase] Tabs.Select(" + index + ") ignored: only " + _buttons.Count + " tab(s)");
                return this;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].EnableInClassList("sc-tab--active", i == index);
            }

            EnsurePane(index);
            for (int i = 0; i < _panes.Count; i++)
            {
                if (_panes[i] != null)
                {
                    _panes[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            EnsureVisible(_buttons[index]);

            if (_selected == index)
            {
                return this;
            }
            _selected = index;
            SelectionChanged?.Invoke(index);
            return this;
        }

        /// <summary>
        /// Drops one pane's cached element so its factory runs again on the next selection. This is
        /// how a view refreshes a tab after a write: the pane holds data fetched at build time, and
        /// without this the cache would keep showing the pre-write state forever.
        /// </summary>
        public Tabs Invalidate(int index)
        {
            if (index < 0 || index >= _panes.Count)
            {
                return this;
            }

            var pane = _panes[index];
            if (pane != null)
            {
                pane.RemoveFromHierarchy();
                _panes[index] = null;
            }

            // the visible tab has to come back immediately; hidden ones rebuild when selected
            if (_selected == index)
            {
                EnsurePane(index);
                _panes[index].style.display = DisplayStyle.Flex;
            }
            return this;
        }

        /// <summary>Invalidates every pane — for a full "reload this screen".</summary>
        public Tabs InvalidateAll()
        {
            for (int i = 0; i < _panes.Count; i++)
            {
                Invalidate(i);
            }
            return this;
        }

        /// <summary>
        /// Removes every tab and its cached pane. Named with the suffix because
        /// <c>VisualElement.Clear</c> is non-virtual and only empties the children.
        /// </summary>
        public Tabs Clear2()
        {
            _content.Clear();
            _strip.contentContainer.Clear();
            _buttons.Clear();
            _titles.Clear();
            _builders.Clear();
            _panes.Clear();
            _selected = -1;
            return this;
        }

        /// <summary>Builds a pane on demand and keeps it — the whole point of the component.</summary>
        private void EnsurePane(int index)
        {
            if (_panes[index] != null)
            {
                return;
            }

            VisualElement pane;
            var build = _builders[index];
            if (build == null)
            {
                pane = EmptyState.Default();
            }
            else
            {
                try
                {
                    pane = build() ?? EmptyState.Default();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Showcase] Tabs pane '" + _titles[index] + "' failed to build: " + e.Message);
                    pane = ErrorState.Message(e.Message);
                }
            }

            _panes[index] = pane;
            _content.Add(pane);
        }

        private void EnsureVisible(VisualElement tab)
        {
            // ScrollTo reads resolved layout; before the first layout pass it would compute NaN offsets
            if (tab.panel == null || float.IsNaN(tab.layout.width) || tab.layout.width <= 0f)
            {
                return;
            }
            _strip.ScrollTo(tab);
        }
    }
}

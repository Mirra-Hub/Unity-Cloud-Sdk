using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The action line above a list: debounced search, dropdown filters, refresh, and free-form
    /// buttons, composed fluently in call order. It wraps onto a second line on narrow panels
    /// instead of clipping, and <see cref="SetBusy"/> gives every screen the same in-flight look.
    /// </summary>
    public sealed class Toolbar : VisualElement
    {
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<bool> _enabledBeforeBusy = new List<bool>();
        private TextField _search;
        private Label _placeholder;
        private Button _refresh;
        private IVisualElementScheduledItem _debounce;
        private bool _busy;

        public Toolbar()
        {
            AddToClassList("sc-toolbar");
        }

        /// <summary>Current (un-debounced) content of the search field; empty when there is none.</summary>
        public string SearchText => _search != null && _search.value != null ? _search.value : string.Empty;

        /// <summary>
        /// Adds the search field. <paramref name="onChanged"/> fires <paramref name="debounceMs"/>
        /// after the last keystroke, or immediately on Enter.
        /// </summary>
        public Toolbar WithSearch(string placeholder, Action<string> onChanged, int debounceMs = 250)
        {
            int delay = debounceMs < 0 ? 0 : debounceMs;

            var box = new VisualElement();
            box.AddToClassList("sc-toolbar__search");

            var glyph = new Label(LucideIcon.Search);
            glyph.AddToClassList("sc-toolbar__search-glyph");
            glyph.AddToClassList("sc-icon");
            glyph.pickingMode = PickingMode.Ignore;
            box.Add(glyph);

            // the placeholder is overlaid on a wrapper that hugs the field itself, so it lines up
            // with the caret without hard-coding the glyph's width (UITK 2022.3 has no placeholder)
            var wrap = new VisualElement();
            wrap.AddToClassList("sc-toolbar__search-wrap");

            _search = new TextField();
            _search.AddToClassList("sc-toolbar__search-input");
            wrap.Add(_search);

            _placeholder = new Label(placeholder ?? string.Empty);
            _placeholder.enableRichText = false;
            _placeholder.pickingMode = PickingMode.Ignore;
            _placeholder.AddToClassList("sc-toolbar__placeholder");
            wrap.Add(_placeholder);

            box.Add(wrap);

            _search.RegisterValueChangedCallback(e =>
            {
                _placeholder.style.display = string.IsNullOrEmpty(e.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                Debounce(onChanged, delay);
            });
            _search.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Flush(onChanged);
                }
            });
            box.RegisterCallback<FocusInEvent>(_ => box.AddToClassList("sc-toolbar__search--focus"));
            box.RegisterCallback<FocusOutEvent>(_ => box.RemoveFromClassList("sc-toolbar__search--focus"));

            Add(box);
            return this;
        }

        /// <summary>
        /// Adds a dropdown filter. The initial value is applied silently (no callback); pass
        /// <paramref name="initial"/> = null to start on the first option.
        /// </summary>
        public Toolbar WithFilter(string label, string[] options, Action<string> onChanged, string initial = null)
        {
            var choices = new List<string>();
            if (options != null)
            {
                foreach (var o in options)
                {
                    choices.Add(o);
                }
            }

            var dd = new DropdownField(label);
            dd.AddToClassList("sc-toolbar__filter");
            dd.choices = choices;

            string value = null;
            if (choices.Count > 0)
            {
                value = initial != null && choices.Contains(initial) ? initial : choices[0];
            }
            // SetValueWithoutNotify instead of the value-taking ctor: that one throws when the
            // default is not among the choices, and the caller's list is runtime data
            dd.SetValueWithoutNotify(value);

            dd.RegisterValueChangedCallback(e => onChanged?.Invoke(e.newValue));

            Add(dd);
            return this;
        }

        public Toolbar WithRefresh(Action onRefresh)
        {
            _refresh = new Button(() => onRefresh?.Invoke()) { text = LucideIcon.RefreshCw };
            _refresh.tooltip = "Refresh";
            _refresh.AddToClassList("sc-btn");
            _refresh.AddToClassList("sc-icon");
            _refresh.AddToClassList("sc-toolbar__icon-btn");
            _buttons.Add(_refresh);
            Add(_refresh);
            return this;
        }

        /// <summary>Adds a button; either part may be omitted (glyph-only renders as a square icon button).</summary>
        public Toolbar WithAction(string text, string glyph, Action onClick, bool primary = false)
        {
            bool hasText = !string.IsNullOrEmpty(text);
            bool hasGlyph = !string.IsNullOrEmpty(glyph);

            var btn = new Button(() => onClick?.Invoke());
            btn.AddToClassList("sc-btn");
            if (primary)
            {
                btn.AddToClassList("sc-btn--primary");
            }

            if (hasGlyph && hasText)
            {
                btn.AddToClassList("sc-toolbar__btn");
                var g = new Label(glyph);
                g.AddToClassList("sc-toolbar__btn-glyph");
                g.AddToClassList("sc-icon");
                btn.Add(g);

                var t = new Label(text);
                t.enableRichText = false;
                btn.Add(t);
            }
            else if (hasGlyph)
            {
                btn.text = glyph;
                btn.AddToClassList("sc-icon");
                btn.AddToClassList("sc-toolbar__icon-btn");
            }
            else
            {
                btn.text = text ?? string.Empty;
            }

            _buttons.Add(btn);
            Add(btn);
            return this;
        }

        /// <summary>Opens the request inspector for the screen's last SDK call.</summary>
        public Toolbar WithSdkCall(Action onOpen)
        {
            return WithAction("SDK call", LucideIcon.Code, onOpen);
        }

        /// <summary>Pushes everything added after it to the right edge of the line.</summary>
        public Toolbar WithSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("sc-toolbar__spacer");
            Add(spacer);
            return this;
        }

        /// <summary>
        /// Disables the buttons and turns the refresh glyph into a loader for the duration of a call.
        /// Buttons the view had already disabled on its own (a claimed reward, an action that needs a
        /// selection) stay disabled when the busy state lifts — leaving busy must not re-enable them.
        /// </summary>
        public void SetBusy(bool busy)
        {
            if (busy == _busy)
            {
                return;
            }
            _busy = busy;
            EnableInClassList("sc-toolbar--busy", busy);

            if (busy)
            {
                _enabledBeforeBusy.Clear();
                foreach (var b in _buttons)
                {
                    _enabledBeforeBusy.Add(b.enabledSelf);
                    b.SetEnabled(false);
                }
            }
            else
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    bool wasEnabled = i < _enabledBeforeBusy.Count ? _enabledBeforeBusy[i] : true;
                    _buttons[i].SetEnabled(wasEnabled);
                }
                _enabledBeforeBusy.Clear();
            }

            if (_refresh != null)
            {
                _refresh.text = busy ? LucideIcon.Loader : LucideIcon.RefreshCw;
            }
        }

        private void Debounce(Action<string> onChanged, int delayMs)
        {
            if (onChanged == null)
            {
                return;
            }
            // Pause() unschedules the pending item, so a burst of keystrokes restarts one timer
            // instead of stacking a new schedule per character
            if (_debounce != null)
            {
                _debounce.Pause();
            }
            _debounce = schedule.Execute(() => onChanged(SearchText)).StartingIn(delayMs);
        }

        private void Flush(Action<string> onChanged)
        {
            if (_debounce != null)
            {
                _debounce.Pause();
                _debounce = null;
            }
            onChanged?.Invoke(SearchText);
        }
    }
}

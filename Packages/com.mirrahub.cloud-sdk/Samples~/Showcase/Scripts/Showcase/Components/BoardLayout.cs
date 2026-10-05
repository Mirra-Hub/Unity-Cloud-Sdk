using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The list on the left of a ranking screen (leaderboards, tournaments): one row per configured
    /// board — a click selects it, a trailing button joins or leaves it — plus plain link rows such as
    /// "Rewards" under a section caption.
    /// <para>
    /// Participation lives in a dictionary the view owns and hands in, so it survives the sidebar being
    /// rebuilt by a refresh. The ranking services have no "is the player in" read, so the view fills it
    /// from what it learns (a join, a leave, an entry that loaded); a key that is not there reads as
    /// "not joined" and offers Join, which the services treat as idempotent.
    /// </para>
    /// </summary>
    public sealed class BoardSidebar : VisualElement
    {
        private readonly Dictionary<string, bool> _joined;
        private readonly Dictionary<string, BoardRow> _rows = new Dictionary<string, BoardRow>();
        private readonly Dictionary<string, VisualElement> _links = new Dictionary<string, VisualElement>();

        public BoardSidebar(string title, Dictionary<string, bool> joined)
        {
            _joined = joined ?? new Dictionary<string, bool>();
            AddToClassList("sc-board-side");

            var caption = new Label(title);
            caption.AddToClassList("sc-board-side__title");
            Add(caption);
        }

        /// <summary>A board with its join / leave button. <paramref name="onLeave"/> may confirm first.</summary>
        public void AddBoard(string key, string name, string subtitle, string glyph,
            Action onSelect, Action onJoin, Action onLeave)
        {
            var row = new BoardRow(this, key, name, subtitle, glyph, onSelect, onJoin, onLeave);
            _rows[key ?? string.Empty] = row;
            Add(row.Root);
        }

        public void AddSection(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sc-board-side__sub");
            Add(label);
        }

        /// <summary>A selectable row without a button — for screens that are not a board, like Rewards.</summary>
        public void AddLink(string id, string name, string subtitle, string glyph, Action onSelect)
        {
            var row = RowShell(name, subtitle, glyph);
            row.RegisterCallback<ClickEvent>(_ => onSelect?.Invoke());
            _links[id] = row;
            Add(row);
        }

        /// <summary>Marks one board or link as the one on screen (pass an id from either kind).</summary>
        public void Select(string id)
        {
            foreach (var pair in _rows)
            {
                pair.Value.Root.EnableInClassList("sc-board-row--active", pair.Key == id);
            }
            foreach (var pair in _links)
            {
                pair.Value.EnableInClassList("sc-board-row--active", pair.Key == id);
            }
        }

        public bool IsJoined(string key)
        {
            bool joined;
            return !string.IsNullOrEmpty(key) && _joined.TryGetValue(key, out joined) && joined;
        }

        public void SetJoined(string key, bool joined)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            _joined[key] = joined;
            BoardRow row;
            if (_rows.TryGetValue(key, out row))
            {
                row.Render();
            }
        }

        /// <summary>Puts a row's button into its in-flight look while join / leave runs.</summary>
        public void SetBusy(string key, bool busy)
        {
            BoardRow row;
            if (key != null && _rows.TryGetValue(key, out row))
            {
                row.Busy = busy;
                row.Render();
            }
        }

        private static VisualElement RowShell(string name, string subtitle, string glyph)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-board-row");

            var icon = new Label(glyph);
            icon.AddToClassList("sc-icon");
            icon.AddToClassList("sc-board-row__glyph");
            row.Add(icon);

            var texts = new VisualElement();
            texts.AddToClassList("sc-board-row__texts");

            var title = new Label(name);
            title.enableRichText = false;
            title.AddToClassList("sc-board-row__name");
            texts.Add(title);

            var sub = new Label(subtitle ?? string.Empty);
            sub.enableRichText = false;
            sub.AddToClassList("sc-board-row__sub");
            sub.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
            texts.Add(sub);

            row.Add(texts);
            return row;
        }

        private sealed class BoardRow
        {
            public readonly VisualElement Root;
            public bool Busy;

            private readonly BoardSidebar _owner;
            private readonly string _key;
            private readonly Button _action;

            public BoardRow(BoardSidebar owner, string key, string name, string subtitle, string glyph,
                Action onSelect, Action onJoin, Action onLeave)
            {
                _owner = owner;
                _key = key;

                Root = RowShell(name, subtitle, glyph);

                _action = new Button(() =>
                {
                    if (Busy)
                    {
                        return;
                    }
                    if (_owner.IsJoined(_key))
                    {
                        onLeave?.Invoke();
                    }
                    else
                    {
                        onJoin?.Invoke();
                    }
                });
                _action.AddToClassList("sc-btn");
                _action.AddToClassList("sc-board-row__btn");
                Root.Add(_action);

                Root.RegisterCallback<ClickEvent>(e =>
                {
                    // the trailing button handles its own click
                    if (e.target is Button)
                    {
                        return;
                    }
                    onSelect?.Invoke();
                });

                Render();
            }

            public void Render()
            {
                bool joined = _owner.IsJoined(_key);
                Root.EnableInClassList("sc-board-row--joined", joined);
                _action.text = Busy ? "…" : joined ? "Leave" : "Join";
                _action.EnableInClassList("sc-board-row__btn--join", !joined);
                _action.EnableInClassList("sc-board-row__btn--leave", joined);
                _action.SetEnabled(!Busy);
                _action.tooltip = joined
                    ? "Leave — removes your score of this session"
                    : "Join — scores are accepted from participants only";
            }
        }
    }

    /// <summary>
    /// Building blocks for the right-hand side of a ranking screen: the stacked cards (header with the
    /// configuration on one line, a submit card, the standings with a slice switch) share one shell so
    /// leaderboards and tournaments read the same.
    /// </summary>
    public static class BoardLayout
    {
        // Medal tints are deliberately outside the semantic palette: on a ranking table gold/silver/
        // bronze *are* the meaning, and no status color reads as "third place".
        private static readonly Color Gold = new Color(0.91f, 0.78f, 0.32f);
        private static readonly Color Silver = new Color(0.76f, 0.78f, 0.84f);
        private static readonly Color Bronze = new Color(0.82f, 0.54f, 0.32f);

        /// <summary>Gold / silver / bronze for the podium, null for every other place.</summary>
        public static Color? MedalTint(int position)
        {
            switch (position)
            {
                case 1: return Gold;
                case 2: return Silver;
                case 3: return Bronze;
                default: return null;
            }
        }

        /// <summary>Rank cell: a medal glyph for the top three, plain "#N" below that.</summary>
        public static VisualElement RankCell(int position)
        {
            var text = new Label(position > 0 ? "#" + position : Fmt.Dash);
            text.AddToClassList("sc-rank");
            var tint = MedalTint(position);
            if (!tint.HasValue)
            {
                return text;
            }
            text.style.color = tint.Value;

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-board-rank");

            var medal = new Label(LucideIcon.Medal);
            medal.AddToClassList("sc-board-rank__medal");
            medal.AddToClassList("sc-icon");
            medal.style.color = tint.Value;
            wrap.Add(medal);
            wrap.Add(text);
            return wrap;
        }

        /// <summary>Player cell: initials avatar, name, and a "You" badge on the signed-in player's row.</summary>
        public static VisualElement PlayerCell(string label, string tooltip, bool mine)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("sc-board-player");

            var avatar = new Avatar(26f).SetInitialsFor(label);
            avatar.AddToClassList("sc-board-player__avatar");
            wrap.Add(avatar);

            var name = new Label(Fmt.OrDash(label));
            name.enableRichText = false;
            name.tooltip = tooltip;
            wrap.Add(name);

            if (mine)
            {
                var you = new Badge("You", ChipTone.Accent);
                you.AddToClassList("sc-board-player__you");
                wrap.Add(you);
            }
            return wrap;
        }

        /// <summary>Sidebar + content column side by side; returns the split and hands out the column.</summary>
        public static VisualElement Split(BoardSidebar side, out VisualElement main)
        {
            var split = new VisualElement();
            split.AddToClassList("sc-board-split");
            split.Add(side);

            main = new VisualElement();
            main.AddToClassList("sc-board-main");
            split.Add(main);
            return split;
        }

        /// <summary>A plain card with a glyph + title line; add the body to the returned card, and
        /// anything that belongs on the title line (count, switch) to <paramref name="head"/>.</summary>
        public static VisualElement Card(string glyph, string title, out VisualElement head)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-board-card");

            head = new VisualElement();
            head.AddToClassList("sc-board-card__head");

            var icon = new Label(glyph);
            icon.AddToClassList("sc-icon");
            icon.AddToClassList("sc-board-card__glyph");
            head.Add(icon);

            var caption = new Label(title);
            caption.AddToClassList("sc-board-card__title");
            head.Add(caption);

            card.Add(head);
            return card;
        }

        /// <summary>Muted explanatory line under a card's title.</summary>
        public static Label Hint(string text)
        {
            var hint = new Label(text);
            hint.enableRichText = false;
            hint.AddToClassList("sc-board-card__hint");
            return hint;
        }

        /// <summary>Small grey number next to a card title; hidden while <paramref name="value"/> is null.</summary>
        public static void SetCount(Label count, string value)
        {
            count.text = value ?? string.Empty;
            count.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public static Label Count()
        {
            var count = new Label();
            count.AddToClassList("sc-board-card__count");
            count.style.display = DisplayStyle.None;
            return count;
        }

        /// <summary>
        /// The top card: accent badge, the board's name, its configuration as one line (with an
        /// optional countdown to the next reset) and, on the right, <paramref name="standing"/> — the
        /// host the view fills with <see cref="RenderStanding"/>.
        /// </summary>
        public static VisualElement Head(string glyph, Color accent, string name, string metaLine,
            string metaTooltip, DateTime? countdownUtc, VisualElement standing)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-board-card");
            card.AddToClassList("sc-board-head");

            var badge = new Label(glyph);
            badge.AddToClassList("sc-icon");
            badge.AddToClassList("sc-board-head__badge");
            badge.style.color = accent;
            badge.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.14f);
            card.Add(badge);

            var texts = new VisualElement();
            texts.AddToClassList("sc-board-head__texts");

            var title = new Label(name);
            title.enableRichText = false;
            title.AddToClassList("sc-board-head__name");
            texts.Add(title);

            var meta = new VisualElement();
            meta.AddToClassList("sc-board-head__meta");
            var line = new Label(metaLine);
            line.enableRichText = false;
            line.AddToClassList("sc-board-head__line");
            line.tooltip = metaTooltip;
            meta.Add(line);
            if (countdownUtc.HasValue)
            {
                var countdown = new CountdownChip(countdownUtc.Value);
                countdown.AddToClassList("sc-board-head__countdown");
                meta.Add(countdown);
            }
            texts.Add(meta);
            card.Add(texts);

            standing.AddToClassList("sc-board-head__standing");
            card.Add(standing);
            return card;
        }

        /// <summary>Big rank over a small caption ("#3" / "your place · 1 250").</summary>
        public static void RenderStanding(VisualElement host, string rank, Color? tint, string caption)
        {
            host.Clear();

            var big = new Label(rank);
            big.AddToClassList("sc-board-head__rank");
            if (tint.HasValue)
            {
                big.style.color = tint.Value;
            }
            host.Add(big);

            var small = new Label(caption);
            small.enableRichText = false;
            small.AddToClassList("sc-board-head__caption");
            host.Add(small);
        }

        /// <summary>Segmented switch; the active option wears <c>.sc-btn--primary</c>.</summary>
        public static VisualElement Switch(string[] options, int selected, Action<int> onPick)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-board-switch");

            var buttons = new List<Button>(options.Length);
            int current = selected;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var btn = new Button { text = options[i] };
                btn.AddToClassList("sc-btn");
                btn.AddToClassList("sc-board-switch__btn");
                btn.EnableInClassList("sc-btn--primary", index == selected);
                btn.clicked += () =>
                {
                    if (index == current)
                    {
                        return;
                    }
                    current = index;
                    for (int j = 0; j < buttons.Count; j++)
                    {
                        buttons[j].EnableInClassList("sc-btn--primary", j == current);
                    }
                    onPick?.Invoke(index);
                };
                buttons.Add(btn);
                row.Add(btn);
            }
            return row;
        }

        /// <summary>Text field for an inline form row (label on the left, fixed width).</summary>
        public static TextField Field(string label, string value, float width = 240f)
        {
            var field = new TextField { label = label, value = value ?? string.Empty };
            field.AddToClassList("sc-field");
            field.AddToClassList("sc-board-form__field");
            field.style.width = width;
            return field;
        }

        /// <summary>
        /// Fields, a primary button and an inline result on one line. Enter in any field runs it; the
        /// button stays disabled while <paramref name="run"/> works.
        /// </summary>
        public static VisualElement FormRow(string buttonText, Func<InlineResult, System.Threading.Tasks.Task> run,
            params TextField[] fields)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-board-form");

            foreach (var field in fields)
            {
                row.Add(field);
            }

            var button = new Button { text = buttonText };
            button.AddToClassList("sc-btn");
            button.AddToClassList("sc-btn--primary");
            button.AddToClassList("sc-board-form__btn");
            row.Add(button);

            var result = new InlineResult();
            row.Add(result);

            bool busy = false;
            Action fire = async () =>
            {
                if (busy)
                {
                    return;
                }
                busy = true;
                button.SetEnabled(false);
                result.Reset();
                try
                {
                    await run(result);
                }
                catch (Exception e)
                {
                    // async lambda: an exception escaping here would surface as an unhandled one.
                    Debug.LogWarning("[Showcase] " + buttonText + " threw: " + e);
                    result.Fail(string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message);
                }
                busy = false;
                button.SetEnabled(true);
            };
            button.clicked += fire;
            foreach (var field in fields)
            {
                field.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        fire();
                    }
                });
            }
            return row;
        }
    }

    /// <summary>One-line outcome next to an inline form's button: green on success, red on failure.</summary>
    public sealed class InlineResult : Label
    {
        public InlineResult()
        {
            enableRichText = false;
            AddToClassList("sc-board-form__result");
        }

        public void Ok(string message) => Set(message, true);

        public void Fail(string message) => Set(message, false);

        public void Reset()
        {
            text = string.Empty;
        }

        private void Set(string message, bool ok)
        {
            text = message ?? string.Empty;
            EnableInClassList("sc-board-form__result--ok", ok);
            EnableInClassList("sc-board-form__result--bad", !ok);
        }
    }
}

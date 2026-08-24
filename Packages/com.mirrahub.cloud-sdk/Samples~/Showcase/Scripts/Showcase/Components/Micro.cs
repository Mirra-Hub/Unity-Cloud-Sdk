using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Tighter sibling of <see cref="Chip"/>: no padding budget for a pill, meant to sit inside a
    /// dense row (counts, short statuses) where a full chip would dominate the line.
    /// </summary>
    public sealed class Badge : VisualElement
    {
        private readonly Label _label;

        public Badge(string text, ChipTone tone = ChipTone.Neutral)
        {
            AddToClassList("sc-badge");
            AddToClassList(ToneClass(tone));

            _label = new Label(text ?? string.Empty);
            _label.enableRichText = false;
            _label.AddToClassList("sc-badge__label");
            Add(_label);
        }

        public Badge SetText(string text)
        {
            _label.text = text ?? string.Empty;
            return this;
        }

        private static string ToneClass(ChipTone tone)
        {
            switch (tone)
            {
                case ChipTone.Accent: return "sc-badge--accent";
                case ChipTone.Ok: return "sc-badge--ok";
                case ChipTone.Warn: return "sc-badge--warn";
                case ChipTone.Bad: return "sc-badge--bad";
                case ChipTone.Info: return "sc-badge--info";
                default: return "sc-badge--neutral";
            }
        }
    }

    /// <summary>
    /// One-tap clipboard copy for ids/tokens/urls. Not a <see cref="Button"/> on purpose: it is
    /// routinely nested inside another clickable row, so it swallows the click (StopPropagation)
    /// instead of also triggering the row underneath.
    /// </summary>
    public sealed class CopyButton : VisualElement
    {
        private const long RevertMs = 1200;

        private readonly Label _glyph;
        private readonly Toasts _toasts;
        private string _value;
        private IVisualElementScheduledItem _revert;

        public CopyButton(string value, Toasts toasts = null, string label = null)
        {
            _value = value ?? string.Empty;
            _toasts = toasts;

            AddToClassList("sc-copy-btn");
            tooltip = "Copy to clipboard";

            _glyph = new Label(LucideIcon.Copy);
            _glyph.AddToClassList("sc-copy-btn__glyph");
            _glyph.AddToClassList("sc-icon");
            Add(_glyph);

            if (!string.IsNullOrEmpty(label))
            {
                var text = new Label(label);
                text.enableRichText = false;
                text.AddToClassList("sc-copy-btn__label");
                Add(text);
            }

            RegisterCallback<ClickEvent>(OnClick);
        }

        public CopyButton SetValue(string value)
        {
            _value = value ?? string.Empty;
            return this;
        }

        private void OnClick(ClickEvent evt)
        {
            evt.StopPropagation();

            if (string.IsNullOrEmpty(_value))
            {
                _toasts?.Info("Nothing to copy");
                return;
            }

            GUIUtility.systemCopyBuffer = _value;
            _toasts?.Ok("Copied to clipboard");

            _glyph.text = LucideIcon.Check;
            AddToClassList("sc-copy-btn--done");
            _revert?.Pause();
            _revert = schedule.Execute(() =>
            {
                _glyph.text = LucideIcon.Copy;
                RemoveFromClassList("sc-copy-btn--done");
            }).StartingIn(RevertMs);
        }
    }

    /// <summary>
    /// Human-readable "time since" for the timestamps the SDK returns. Input is treated as UTC
    /// unless it is explicitly local, because backend DTOs carry UTC with an unspecified kind.
    /// </summary>
    public static class RelativeTime
    {
        private const long RefreshMs = 30000;

        public static string Format(DateTime time)
        {
            if (time == default(DateTime))
            {
                return Fmt.Dash;
            }

            var utc = ToUtc(time);
            var delta = DateTime.UtcNow - utc;
            bool future = delta.Ticks < 0;
            var span = future ? delta.Negate() : delta;

            if (span.TotalSeconds < 45d)
            {
                return "just now";
            }

            string amount;
            if (span.TotalMinutes < 60d)
            {
                amount = Math.Max(1, (int)span.TotalMinutes) + "m";
            }
            else if (span.TotalHours < 24d)
            {
                amount = Math.Max(1, (int)span.TotalHours) + "h";
            }
            else if (span.TotalDays < 7d)
            {
                amount = Math.Max(1, (int)span.TotalDays) + "d";
            }
            else
            {
                // Fmt.Date localizes: past a week the text becomes a plain date, and every other
                // date in the showcase is local — a UTC one here would read as a different day for
                // the same timestamp.
                return Fmt.Date(utc);
            }

            return future ? "in " + amount : amount + " ago";
        }

        /// <summary>Absolute UTC stamp, used as the tooltip behind the fuzzy text.</summary>
        public static string Absolute(DateTime time)
        {
            if (time == default(DateTime))
            {
                return "unknown";
            }
            return ToUtc(time).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        }

        /// <summary>Self-refreshing label (ticks while attached, so "just now" does not go stale).</summary>
        public static Label Build(DateTime time)
        {
            var label = new Label(Format(time));
            label.enableRichText = false;
            label.AddToClassList("sc-rel-time");
            label.tooltip = Absolute(time);

            IVisualElementScheduledItem tick = null;
            label.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                label.text = Format(time);
                tick = label.schedule.Execute(() => label.text = Format(time)).Every(RefreshMs);
            });
            label.RegisterCallback<DetachFromPanelEvent>(_ => tick?.Pause());
            return label;
        }

        private static DateTime ToUtc(DateTime time)
        {
            return time.Kind == DateTimeKind.Local
                ? time.ToUniversalTime()
                : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Indent-formatted view over a raw JSON payload (raw bodies, entity documents, cloud-save
    /// blobs). Formatting is done on the string itself — a payload that fails to parse still has
    /// to be readable, so anything that is not an object/array is shown verbatim.
    /// </summary>
    public sealed class JsonViewer : VisualElement
    {
        private const int PreviewChars = 90;

        private readonly Label _chevron;
        private readonly Label _preview;
        private readonly CopyButton _copy;
        private readonly ScrollView _body;
        private readonly Label _text;
        private readonly Label _more;

        private string _raw = string.Empty;
        private int _maxLines = 40;
        private bool _collapsed;

        public JsonViewer()
        {
            AddToClassList("sc-json");

            var head = new VisualElement();
            head.AddToClassList("sc-json__head");

            _chevron = new Label(LucideIcon.ChevronDown);
            _chevron.AddToClassList("sc-json__chev");
            _chevron.AddToClassList("sc-icon");
            head.Add(_chevron);

            var braces = new Label(LucideIcon.Braces);
            braces.AddToClassList("sc-json__glyph");
            braces.AddToClassList("sc-icon");
            head.Add(braces);

            _preview = new Label("JSON");
            _preview.enableRichText = false;
            _preview.AddToClassList("sc-json__preview");
            _preview.style.whiteSpace = WhiteSpace.NoWrap;
            head.Add(_preview);

            _copy = new CopyButton(string.Empty);
            head.Add(_copy);

            head.RegisterCallback<ClickEvent>(_ => SetCollapsed(!_collapsed));
            Add(head);

            _body = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _body.AddToClassList("sc-json__body");

            _text = new Label(string.Empty);
            _text.enableRichText = false;
            _text.AddToClassList("sc-json__text");
            _text.style.whiteSpace = WhiteSpace.NoWrap;
            _body.Add(_text);

            _more = new Label(string.Empty);
            _more.enableRichText = false;
            _more.AddToClassList("sc-json__more");
            _more.style.display = DisplayStyle.None;
            _body.Add(_more);

            Add(_body);
            Render();
        }

        public JsonViewer SetRaw(string json)
        {
            _raw = json ?? string.Empty;
            Render();
            return this;
        }

        public JsonViewer SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            Render();
            return this;
        }

        public JsonViewer SetMaxLines(int lines)
        {
            _maxLines = lines <= 0 ? int.MaxValue : lines;
            Render();
            return this;
        }

        private void Render()
        {
            string pretty = Pretty(_raw);
            int hidden;
            string shown = LimitLines(pretty, _maxLines, out hidden);

            _text.text = string.IsNullOrEmpty(shown) ? Fmt.Dash : shown;
            _more.text = hidden > 0 ? "+" + hidden + " more lines (use copy to get the full payload)" : string.Empty;
            _more.style.display = hidden > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            _copy.SetValue(_raw);
            _chevron.text = _collapsed ? LucideIcon.ChevronRight : LucideIcon.ChevronDown;
            _body.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _preview.text = _collapsed
                ? (string.IsNullOrEmpty(_raw) ? Fmt.Dash : Fmt.Truncate(Minify(_raw), PreviewChars))
                : "JSON";
            EnableInClassList("sc-json--collapsed", _collapsed);
        }

        /// <summary>Indents a JSON document without parsing it (string literals are passed through intact).</summary>
        private static string Pretty(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return string.Empty;
            }

            string src = json.Trim();
            if (src.Length == 0 || (src[0] != '{' && src[0] != '['))
            {
                return src;
            }

            var sb = new StringBuilder(src.Length + 64);
            int indent = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];

                if (inString)
                {
                    sb.Append(c);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                    {
                        sb.Append(c);
                        int next = SkipWhitespace(src, i + 1);
                        // Keep empty containers on one line — "{\n}" reads like a bug.
                        if (next < src.Length && (src[next] == '}' || src[next] == ']'))
                        {
                            sb.Append(src[next]);
                            i = next;
                            break;
                        }
                        indent++;
                        AppendIndentedLine(sb, indent);
                        break;
                    }
                    case '}':
                    case ']':
                        if (indent > 0)
                        {
                            indent--;
                        }
                        AppendIndentedLine(sb, indent);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        AppendIndentedLine(sb, indent);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(c))
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        private static string Minify(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return string.Empty;
            }

            string src = json.Trim();
            // Same guard as Pretty: a non-JSON body (plain text error) must not lose its spaces.
            if (src.Length == 0 || (src[0] != '{' && src[0] != '['))
            {
                return src;
            }

            var sb = new StringBuilder(src.Length);
            bool inString = false;
            bool escaped = false;

            foreach (char c in src)
            {
                if (inString)
                {
                    sb.Append(c);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    continue;
                }
                sb.Append(c);
                if (c == '"')
                {
                    inString = true;
                }
            }

            return sb.ToString();
        }

        private static string LimitLines(string text, int max, out int hidden)
        {
            hidden = 0;
            if (string.IsNullOrEmpty(text) || max <= 0)
            {
                return text ?? string.Empty;
            }

            var lines = text.Split('\n');
            if (lines.Length <= max)
            {
                return text;
            }
            hidden = lines.Length - max;
            return string.Join("\n", lines, 0, max);
        }

        private static int SkipWhitespace(string s, int from)
        {
            int i = from;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
            return i;
        }

        private static void AppendIndentedLine(StringBuilder sb, int indent)
        {
            sb.Append('\n');
            for (int i = 0; i < indent; i++)
            {
                sb.Append("  ");
            }
        }
    }

    /// <summary>
    /// Inline info glyph that explains a heading. Runtime panels do not always render the native
    /// tooltip, so the text is also shown as a hover/tap bubble; the bubble is absolutely
    /// positioned under the glyph and therefore costs no layout space in the header row.
    /// </summary>
    public sealed class InfoHint : VisualElement
    {
        private readonly Label _bubble;
        private bool _hovered;
        private bool _pinned;

        public InfoHint(string text)
        {
            string body = text ?? string.Empty;

            AddToClassList("sc-hint");
            tooltip = body;

            var glyph = new Label(LucideIcon.Info);
            glyph.AddToClassList("sc-hint__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.pickingMode = PickingMode.Ignore;
            Add(glyph);

            _bubble = new Label(body);
            _bubble.enableRichText = false;
            _bubble.AddToClassList("sc-hint__bubble");
            _bubble.pickingMode = PickingMode.Ignore;
            _bubble.style.display = DisplayStyle.None;
            Add(_bubble);

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hovered = true;
                Sync();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hovered = false;
                Sync();
            });
            // Touch input never sends a leave — tapping pins the bubble so it stays readable.
            RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _pinned = !_pinned;
                Sync();
            });
        }

        private void Sync()
        {
            _bubble.style.display = _hovered || _pinned ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("sc-hint--open", _hovered || _pinned);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using MirraCloud.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// One recorded SDK call, flattened from a <see cref="RestApiResult"/> so the UI never has to
    /// keep the result (and its payload) alive. <see cref="Snippet"/> carries the C# line that
    /// produced the call — that pairing (code ↔ wire traffic) is the point of the request log.
    /// </summary>
    public sealed class RequestEntry
    {
        public string Label;
        public string Method;
        public string Route;
        public string Url;
        public long? HttpStatusCode;
        public long DurationMs;
        public bool Ok;
        public string Body;
        public DateTime At;
        public string Snippet;
    }

    /// <summary>
    /// Ring buffer of the SDK traffic the example produced, plus a ready-made inspector panel.
    /// Views call <see cref="Record"/> after every await so the user can see exactly which HTTP
    /// request their button press caused, how long it took and what came back.
    /// </summary>
    public sealed class RequestLog
    {
        private readonly List<RequestEntry> _entries;
        private readonly ReadOnlyCollection<RequestEntry> _view;
        private readonly int _capacity;

        public RequestLog(int capacity = 200)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _entries = new List<RequestEntry>(_capacity);
            _view = new ReadOnlyCollection<RequestEntry>(_entries);
        }

        /// <summary>Recorded calls, newest first.</summary>
        public IReadOnlyList<RequestEntry> Entries => _view;

        public int Count => _entries.Count;

        /// <summary>Raised after every mutation (record / clear); panels re-render on it.</summary>
        public event Action Changed;

        /// <summary>
        /// Records the call meta carried by every <see cref="RestApiResult"/>. <paramref name="snippet"/>
        /// is the C# the view ran, shown next to the response.
        /// </summary>
        public void Record(string label, RestApiResult result, string snippet = null)
        {
            if (result == null)
            {
                Debug.LogWarning("[Showcase] RequestLog.Record got a null result for '" + label + "'.");
                return;
            }

            // On transport failures the meta lives on the error, not on the result — read both.
            var err = result.Error;
            string method = Pick(result.Method, err != null ? err.Method : null);
            string route = Pick(result.Route, err != null ? err.Route : null);
            string url = Pick(result.Url, err != null ? err.Url : null);
            long? code = result.HttpStatusCode ?? (err != null ? err.HttpStatusCode : null);

            RecordManual(new RequestEntry
            {
                Label = string.IsNullOrEmpty(label) ? Pick(route, "request") : label,
                Method = string.IsNullOrEmpty(method) ? Fmt.Dash : method.ToUpperInvariant(),
                Route = route,
                Url = url,
                HttpStatusCode = code,
                DurationMs = result.DurationMs,
                Ok = result.IsSuccess,
                Body = BodyOf(result),
                At = DateTime.Now,
                Snippet = snippet
            });
        }

        /// <summary>Appends a hand-built entry (realtime/WebSocket traffic has no RestApiResult).</summary>
        public void RecordManual(RequestEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            if (entry.At == default)
            {
                entry.At = DateTime.Now;
            }

            _entries.Insert(0, entry);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
            Changed?.Invoke();
        }

        /// <summary>Drops every recorded entry and notifies the panels.</summary>
        public void Clear2()
        {
            if (_entries.Count == 0)
            {
                return;
            }
            _entries.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Builds a live journal panel. It subscribes to <see cref="Changed"/> only while attached
        /// to a panel and drops the subscription on detach, so discarded screens do not keep the
        /// log (or their whole view tree) alive.
        /// </summary>
        public VisualElement BuildPanel()
        {
            return new LogPanel(this);
        }

        private static string Pick(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }

        // A leaderboard page or an asset listing can be hundreds of KB, and the log holds up to
        // `capacity` of them for the whole session. The inspector only ever shows a preview, so
        // keep a bounded prefix instead of pinning every response body in memory.
        private const int MaxBodyChars = 8000;

        private static string Cap(string body)
        {
            if (string.IsNullOrEmpty(body) || body.Length <= MaxBodyChars)
            {
                return body;
            }
            return body.Substring(0, MaxBodyChars) + "\n… (" + (body.Length - MaxBodyChars) + " more characters)";
        }

        private static string BodyOf(RestApiResult result)
        {
            if (!string.IsNullOrEmpty(result.ResponseBody))
            {
                return Cap(result.ResponseBody);
            }
            var err = result.Error;
            if (err == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(err.ResponseBody))
            {
                return Cap(err.ResponseBody);
            }
            return string.IsNullOrEmpty(err.Message) ? err.Type.ToString() : err.Type + ": " + err.Message;
        }

        /// <summary>The journal UI: header (count + clear) over a scrollable list of expandable rows.</summary>
        private sealed class LogPanel : VisualElement
        {
            private readonly RequestLog _log;
            private readonly HashSet<RequestEntry> _expanded = new HashSet<RequestEntry>();
            private readonly Label _count;
            private readonly ScrollView _list;
            private readonly Action _onChanged;

            public LogPanel(RequestLog log)
            {
                _log = log;
                _onChanged = Render;

                AddToClassList("sc-reqlog");

                var head = new VisualElement();
                head.AddToClassList("sc-reqlog__head");

                var glyph = new Label(LucideIcon.History);
                glyph.AddToClassList("sc-reqlog__glyph");
                glyph.AddToClassList("sc-icon");
                head.Add(glyph);

                var title = new Label("Request log");
                title.AddToClassList("sc-reqlog__title");
                head.Add(title);

                _count = new Label("0");
                _count.AddToClassList("sc-reqlog__count");
                head.Add(_count);

                var spacer = new VisualElement();
                spacer.AddToClassList("sc-reqlog__spacer");
                head.Add(spacer);

                var clear = new Button(() => _log.Clear2()) { text = "Clear" };
                clear.AddToClassList("sc-reqlog__clear");
                head.Add(clear);

                Add(head);

                _list = new ScrollView(ScrollViewMode.Vertical);
                _list.AddToClassList("sc-reqlog__list");
                Add(_list);

                RegisterCallback<AttachToPanelEvent>(OnAttach);
                RegisterCallback<DetachFromPanelEvent>(OnDetach);

                Render();
            }

            private void OnAttach(AttachToPanelEvent _)
            {
                _log.Changed -= _onChanged;
                _log.Changed += _onChanged;
                Render();
            }

            private void OnDetach(DetachFromPanelEvent _)
            {
                _log.Changed -= _onChanged;
            }

            private void Render()
            {
                _count.text = _log.Count.ToString(CultureInfo.InvariantCulture);
                _list.Clear();

                if (_log.Count == 0)
                {
                    _expanded.Clear();
                    _list.Add(EmptyState.Build(LucideIcon.History, "No SDK requests yet"));
                    return;
                }

                // Entries evicted by the capacity cap must not stay pinned in the expanded set.
                if (_expanded.Count > 0)
                {
                    _expanded.IntersectWith(new HashSet<RequestEntry>(_log.Entries));
                }

                foreach (var e in _log.Entries)
                {
                    _list.Add(BuildRow(e));
                }
            }

            private VisualElement BuildRow(RequestEntry e)
            {
                var row = new VisualElement();
                row.AddToClassList("sc-reqlog__row");
                if (!e.Ok)
                {
                    row.AddToClassList("sc-reqlog__row--bad");
                }

                bool open = _expanded.Contains(e);

                var bar = new VisualElement();
                bar.AddToClassList("sc-reqlog__bar");

                var method = new Label(string.IsNullOrEmpty(e.Method) ? Fmt.Dash : e.Method);
                method.enableRichText = false;
                method.AddToClassList("sc-reqlog__method");
                method.AddToClassList(MethodClass(e.Method));
                bar.Add(method);

                var texts = new VisualElement();
                texts.AddToClassList("sc-reqlog__texts");
                var label = new Label(string.IsNullOrEmpty(e.Label) ? "request" : e.Label);
                label.enableRichText = false;
                label.AddToClassList("sc-reqlog__label");
                texts.Add(label);
                var route = new Label(Fmt.Truncate(string.IsNullOrEmpty(e.Route) ? e.Url ?? string.Empty : e.Route, 78));
                route.enableRichText = false;
                route.AddToClassList("sc-reqlog__route");
                texts.Add(route);
                bar.Add(texts);

                var status = new Label(StatusText(e));
                status.AddToClassList("sc-reqlog__status");
                status.AddToClassList(StatusClass(e));
                bar.Add(status);

                var ms = new Label(e.DurationMs.ToString(CultureInfo.InvariantCulture) + " ms");
                ms.AddToClassList("sc-reqlog__ms");
                bar.Add(ms);

                var chev = new Label(open ? LucideIcon.ChevronDown : LucideIcon.ChevronRight);
                chev.AddToClassList("sc-reqlog__chev");
                chev.AddToClassList("sc-icon");
                bar.Add(chev);

                bar.RegisterCallback<ClickEvent>(_ => Toggle(e, row));
                row.Add(bar);

                if (open)
                {
                    row.Add(BuildDetail(e));
                }
                return row;
            }

            // Swapping just this row (instead of a full Render) keeps the list's scroll offset.
            private void Toggle(RequestEntry e, VisualElement row)
            {
                if (!_expanded.Remove(e))
                {
                    _expanded.Add(e);
                }

                var host = row.parent;
                if (host == null)
                {
                    Render();
                    return;
                }
                int index = host.IndexOf(row);
                var fresh = BuildRow(e);
                host.Insert(index, fresh);
                row.RemoveFromHierarchy();
            }

            private VisualElement BuildDetail(RequestEntry e)
            {
                var d = new VisualElement();
                d.AddToClassList("sc-reqlog__detail");

                d.Add(Kv("time", e.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
                if (!string.IsNullOrEmpty(e.Route))
                {
                    d.Add(Kv("route", Fmt.Truncate(e.Route, 90)));
                }
                if (!string.IsNullOrEmpty(e.Url))
                {
                    d.Add(Kv("url", Fmt.Truncate(e.Url, 120)));
                }

                if (!string.IsNullOrEmpty(e.Snippet))
                {
                    d.Add(Caption("SDK call"));
                    d.Add(SdkCallDrawer.CodeBlock(e.Snippet));
                }

                d.Add(Caption("Response body"));
                string body = string.IsNullOrEmpty(e.Body) ? "(empty)" : Fmt.Truncate(e.Body, 4000);
                d.Add(SdkCallDrawer.CodeBlock(body));

                var actions = new VisualElement();
                actions.AddToClassList("sc-reqlog__actions");
                actions.Add(new CopyButton(e.Body, null, "Copy body"));
                if (!string.IsNullOrEmpty(e.Url))
                {
                    actions.Add(new CopyButton(e.Url, null, "Copy URL"));
                }
                d.Add(actions);

                return d;
            }

            private static VisualElement Kv(string key, string value)
            {
                var kv = new VisualElement();
                kv.AddToClassList("sc-kv");
                var k = new Label(key);
                k.AddToClassList("sc-kv__k");
                var v = new Label(value);
                v.enableRichText = false;
                v.AddToClassList("sc-kv__v");
                kv.Add(k);
                kv.Add(v);
                return kv;
            }

            private static Label Caption(string text)
            {
                var l = new Label(text);
                l.AddToClassList("sc-reqlog__caption");
                return l;
            }

            private static string StatusText(RequestEntry e)
            {
                if (e.HttpStatusCode.HasValue)
                {
                    return e.HttpStatusCode.Value.ToString(CultureInfo.InvariantCulture);
                }
                return e.Ok ? "ok" : "err";
            }

            private static string StatusClass(RequestEntry e)
            {
                if (!e.HttpStatusCode.HasValue)
                {
                    return e.Ok ? "sc-reqlog__status--ok" : "sc-reqlog__status--bad";
                }
                long c = e.HttpStatusCode.Value;
                if (c >= 500)
                {
                    return "sc-reqlog__status--bad";
                }
                if (c >= 400)
                {
                    return "sc-reqlog__status--warn";
                }
                if (c >= 300)
                {
                    return "sc-reqlog__status--dim";
                }
                if (c >= 200)
                {
                    return "sc-reqlog__status--ok";
                }
                return "sc-reqlog__status--dim";
            }

            private static string MethodClass(string method)
            {
                switch ((method ?? string.Empty).ToUpperInvariant())
                {
                    case "GET": return "sc-reqlog__method--get";
                    case "POST": return "sc-reqlog__method--post";
                    case "PUT": return "sc-reqlog__method--put";
                    case "PATCH": return "sc-reqlog__method--patch";
                    case "DELETE": return "sc-reqlog__method--delete";
                    default: return "sc-reqlog__method--other";
                }
            }
        }
    }
}

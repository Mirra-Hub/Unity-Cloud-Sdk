using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Analytics.Dto;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Analytics screen: the service only writes. There is no endpoint that reads an event back, and
    /// every response carries an empty body, so this screen tallies what it sent itself rather than
    /// pretending to load something.
    /// <para>
    /// That tally is the point. A write-only service is otherwise invisible — you press a button and
    /// nothing on screen changes — so each card records its call, its round trip and its failure
    /// reason into the counters, the by-name chart and the call list on the first tab.
    /// </para>
    /// </summary>
    public sealed class AnalyticsView : ServiceView
    {
        private const string EventSnippet =
@"// One event, sent right away. Parameters are a flat string map — the endpoint has no room
// for nested values, so numbers and flags go over as text.
var parameters = new Dictionary<string, string>
{
    { ""level"", ""7"" },
    { ""result"", ""win"" }
};

var op = sdk.Analytics.SendEventAsync(""level_completed"", parameters);
await op.Task();

// The answer has no body: IsSuccess and the request meta are all there is to read.
if (!op.Result.IsSuccess)
{
    Debug.LogWarning(op.Result.Error?.Message);
}

// Without parameters:
await sdk.Analytics.SendEventAsync(""level_completed"").Task();";

        private const string EnqueueSnippet =
@"// Fire-and-forget into the SDK's own tracker instead of one request per event. The tracker
// flushes on a timer, once 100 events are buffered, and on pause or quit.
sdk.Analytics.EnqueueEvent(""shot_fired"",
    new Dictionary<string, string> { { ""weapon"", ""bow"" } },
    new List<string> { ""combat"" });

// Nothing is returned and nothing is awaited: the call hands the event to the buffer.
// Buffering only runs while a session is being tracked, which the SDK starts on login —
// before that the event is dropped with an error in the log, so use SendEventAsync.";

        private const string BatchSnippet =
@"// The request the tracker itself makes when it flushes: many events in one round trip,
// each carrying its own timestamp.
var events = new List<BatchEventItemDto>
{
    new BatchEventItemDto
    {
        EventName = ""level_start"",
        Date = DateTime.UtcNow.ToString(""O""),
        Parameters = new Dictionary<string, string> { { ""level"", ""7"" } },
        Tags = new List<string> { ""progression"" }
    }
};

var op = sdk.Analytics.SendBatchAsync(events);
await op.Task();";

        private const string SessionSnippet =
@"// A session boundary. The SDK already sends this once when Authentication.OnLogin fires,
// so a game only calls it by hand when its own notion of a session differs.
var op = sdk.Analytics.SendSessionStartedAsync();
await op.Task();";

        private const string PlaytimeSnippet =
@"// Minutes, not seconds. The tracker reports playtime on a heartbeat and again on quit;
// this is the same call, made explicitly.
var op = sdk.Analytics.SendPlaytimeAsync(5);
await op.Task();";

        // Enough history to see a pattern, short enough that the table never needs paging.
        private const int HistoryCap = 25;

        // Past a dozen bars the labels stop being readable, so the chart keeps the busiest names.
        private const int MaxBars = 12;

        private readonly List<Shot> _history = new List<Shot>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);

        private Tabs _tabs;
        private int _calls;
        private int _ok;
        private int _failed;
        private int _queued;

        public AnalyticsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            DeclareCall(new SdkCall("Send one event", EventSnippet,
                "The metric id has to exist in the project's analytics setup."));
            DeclareCall(new SdkCall("Queue an event", EnqueueSnippet,
                "Returns nothing — the event goes into the tracker's buffer."));
            DeclareCall(new SdkCall("Send a batch", BatchSnippet));
            DeclareCall(new SdkCall("Report a session start", SessionSnippet,
                "The SDK already fires this on login."));
            DeclareCall(new SdkCall("Report playtime", PlaytimeSnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);
            SyncStatus();

            _tabs = UseTabs();
            _tabs.Add("This session", LucideIcon.Activity, BuildSession)
                .Add("Actions", LucideIcon.Sparkles, BuildActions);
        }

        private void SyncStatus()
        {
            if (_calls == 0 && _queued == 0)
            {
                SetStatus("Nothing sent yet", ChipTone.Neutral);
                return;
            }

            string text = _ok + " delivered";
            if (_failed > 0)
            {
                text += " · " + _failed + " failed";
            }
            if (_queued > 0)
            {
                text += " · " + _queued + " buffered";
            }
            SetStatus(text, _failed > 0 ? ChipTone.Warn : ChipTone.Ok);
        }

        // ----- this session ---------------------------------------------------------------------

        private VisualElement BuildSession()
        {
            var col = new VisualElement();

            var kpis = new KpiRow();
            if (_calls == 0 && _queued == 0)
            {
                kpis.AddZero("Calls", LucideIcon.Send)
                    .AddZero("Delivered", LucideIcon.CircleCheck)
                    .AddZero("Failed", LucideIcon.CircleX)
                    .AddZero("Buffered", LucideIcon.Hourglass);
            }
            else
            {
                kpis.Add("Calls", LucideIcon.Send, _calls.ToString())
                    .Add("Delivered", LucideIcon.CircleCheck, _ok.ToString())
                    .Add("Failed", LucideIcon.CircleX, _failed.ToString(), null, _failed > 0)
                    .Add("Buffered", LucideIcon.Hourglass, _queued.ToString());
            }
            col.Add(kpis);

            var scope = new Label("These four numbers are this screen's own count of the calls it made. "
                + "Nothing here was read from the server: analytics accepts events and answers with an "
                + "empty body, and the reports are drawn from them in the Mirra Hub console.");
            scope.AddToClassList("sc-fs-hint");
            scope.AddToClassList("sc-an-scope");
            col.Add(scope);

            if (_history.Count == 0)
            {
                col.Add(ZeroState.Panel(LucideIcon.ChartLine, "Analytics only writes",
                    "This service has no read endpoint. The game reports events, session starts and "
                    + "playtime; the funnels and retention charts built from them live in the Mirra Hub "
                    + "console. Fire one of the calls from the Actions tab and it appears in this list "
                    + "with its round trip and its result.",
                    "Send an event", () => _tabs.Select(1),
                    "A custom event needs a metric id that the project's analytics setup already knows."));
                return col;
            }

            var bars = EventBars();
            // The header counts every distinct name; the chart itself only draws the busiest ones.
            col.Add(new SectionHeader("Events by name", _byName.Count.ToString()));
            var chart = new BarChart(170f);
            chart.SetAccent(Meta.Accent)
                .SetData(bars)
                .SetEmptyText("Only session and playtime calls so far — neither carries an event name");
            col.Add(chart);

            col.Add(new SectionHeader("Last calls", _history.Count.ToString()));
            var table = new DataTable(HistoryColumns()).WithZebra().WithMaxHeight(420f);
            table.Bind(_history, o => !((Shot)o).Ok);
            col.Add(table);
            return col;
        }

        /// <summary>Busiest event names first — a dictionary's order is an implementation detail, and
        /// the chart must not reshuffle itself between two refreshes.</summary>
        private List<ChartPoint> EventBars()
        {
            var points = new List<ChartPoint>();
            foreach (var pair in _byName)
            {
                points.Add(new ChartPoint(pair.Key, pair.Value));
            }
            points.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (points.Count > MaxBars)
            {
                points.RemoveRange(MaxBars, points.Count - MaxBars);
            }
            return points;
        }

        private DataColumn[] HistoryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "CALL", Grow = 2f,
                    SortKey = o => ((Shot)o).Name,
                    Cell = o =>
                    {
                        var shot = (Shot)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-an-cell");

                        var name = new Label(Fmt.OrDash(shot.Name));
                        name.enableRichText = false;
                        box.Add(name);

                        if (!string.IsNullOrEmpty(shot.Note))
                        {
                            var sub = new Label(Fmt.Truncate(shot.Note, 64));
                            sub.enableRichText = false;
                            sub.AddToClassList("sc-an-cell__sub");
                            if (!shot.Ok)
                            {
                                sub.AddToClassList("sc-an-cell__sub--bad");
                            }
                            box.Add(sub);
                        }
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "KIND", FixedWidth = true, Px = 104,
                    SortKey = o => ((Shot)o).Kind,
                    Cell = o =>
                    {
                        var shot = (Shot)o;
                        return new Chip(shot.Kind, KindTone(shot.Kind));
                    },
                },
                new DataColumn
                {
                    Header = "RESULT", FixedWidth = true, Px = 112,
                    Cell = o =>
                    {
                        var shot = (Shot)o;
                        if (shot.Buffered)
                        {
                            return new Badge("buffered", ChipTone.Warn);
                        }
                        return new Badge(shot.Ok ? "delivered" : "failed",
                            shot.Ok ? ChipTone.Ok : ChipTone.Bad);
                    },
                },
                new DataColumn
                {
                    Header = "TOOK", FixedWidth = true, Px = 78, Align = "right",
                    SortKey = o => ((Shot)o).Ms,
                    Cell = o =>
                    {
                        var shot = (Shot)o;
                        // A buffered event has not been sent yet, so it has no round trip to show.
                        return new Label(shot.Buffered ? Fmt.Dash : shot.Ms + " ms");
                    },
                },
                new DataColumn
                {
                    Header = "WHEN", FixedWidth = true, Px = 96, Align = "right",
                    SortKey = o => ((Shot)o).At,
                    Cell = o => RelativeTime.Build(((Shot)o).At),
                },
            };
        }

        private static ChipTone KindTone(string kind)
        {
            switch (kind)
            {
                case "event": return ChipTone.Accent;
                case "batch": return ChipTone.Info;
                case "queued": return ChipTone.Warn;
                default: return ChipTone.Neutral;
            }
        }

        // ----- actions --------------------------------------------------------------------------

        private VisualElement BuildActions()
        {
            var col = new VisualElement();

            var hint = new Label("Every call the service exposes, and all of them are writes. Each card "
                + "adds its outcome to the counters and the call list on the first tab, so a request "
                + "that answers with nothing still leaves something to look at.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Send a custom event",
                    "One event, delivered immediately. This is the call to reach for when the event "
                    + "matters on its own — a purchase, a tutorial step, a level completion.",
                    LucideIcon.Send)
                .WithFields(
                    FormField.Text("metricId", "Metric id", "level_completed", true)
                        .WithPlaceholder("The id from the project's analytics setup, not a display name."),
                    FormField.Json("parameters", "Parameters", "{\n  \"level\": 7,\n  \"result\": \"win\"\n}")
                        .WithPlaceholder("A JSON object. Every value is flattened to a string — the "
                            + "endpoint takes no nested payloads."))
                .WithSnippet(EventSnippet)
                .OnRun("Send", SendEvent));

            col.Add(new ActionCard("Queue an event",
                    "Hands the event to the SDK's tracker instead of sending it now. The buffer "
                    + "flushes on a timer, at 100 events, and on pause or quit — which is what a "
                    + "high-frequency gameplay event wants.", LucideIcon.Hourglass)
                .WithFields(
                    FormField.Text("eventName", "Event name", "shot_fired", true),
                    FormField.Json("parameters", "Parameters", "{\n  \"weapon\": \"bow\"\n}"),
                    FormField.Text("tags", "Tags (comma-separated)", "combat"))
                .WithSnippet(EnqueueSnippet)
                .OnRun("Queue", QueueEvent));

            col.Add(new ActionCard("Send a batch",
                    "Several events in one request, each stamped with its own time. This is the call "
                    + "the tracker makes for you when its buffer flushes.", LucideIcon.Layers)
                .WithFields(
                    FormField.Text("names", "Event names (comma-separated)",
                        "level_start, level_completed", true),
                    FormField.Json("parameters", "Parameters shared by every event", "{\n  \"level\": 7\n}"),
                    FormField.Text("tags", "Tags (comma-separated)"))
                .WithSnippet(BatchSnippet)
                .OnRun("Send batch", SendBatch));

            col.Add(new ActionCard("Report a session start",
                    "Marks the beginning of a play session. The SDK already sends this once on login, "
                    + "so a game only repeats it for its own session boundaries.", LucideIcon.CirclePlay)
                .WithSnippet(SessionSnippet)
                .OnRun("Send", SendSession));

            col.Add(new ActionCard("Report playtime",
                    "Adds minutes to the player's total. The tracker does this on a heartbeat and on "
                    + "quit; this card is the same call made by hand.", LucideIcon.Timer)
                .WithFields(FormField.Int("minutes", "Minutes", 5))
                .WithSnippet(PlaytimeSnippet)
                .OnRun("Send", SendPlaytime));

            return col;
        }

        private async Task<ActionOutcome> SendEvent(FormValues values)
        {
            string metricId = values.Text("metricId");

            Dictionary<string, string> parameters;
            string invalid = Parameters(values.Text("parameters"), out parameters);
            if (invalid != null)
            {
                return ActionOutcome.Failure(invalid);
            }

            // The one-argument overload exists precisely for the no-parameter case, so the example
            // uses it rather than posting an empty map.
            var op = parameters == null
                ? Sdk.Analytics.SendEventAsync(metricId)
                : Sdk.Analytics.SendEventAsync(metricId, parameters);

            var outcome = await Await(op, "Analytics · " + Fmt.Truncate(metricId, 24));
            Record(metricId, "event", outcome, Describe(parameters), new[] { metricId });
            return Finish(outcome, "Sent " + metricId);
        }

        private Task<ActionOutcome> QueueEvent(FormValues values)
        {
            string name = values.Text("eventName");

            Dictionary<string, string> parameters;
            string invalid = Parameters(values.Text("parameters"), out parameters);
            if (invalid != null)
            {
                return Task.FromResult(ActionOutcome.Failure(invalid));
            }

            var tags = Tags(values.Text("tags"));
            Sdk.Analytics.EnqueueEvent(name, parameters, tags);

            // Nothing to await and nothing to log in the request journal: no request was made. The
            // row is marked buffered so it is not mistaken for a delivered event.
            _queued++;
            Bump(name);
            Push(new Shot
            {
                Name = name,
                Kind = "queued",
                Ok = true,
                Buffered = true,
                At = DateTime.UtcNow,
                Note = Describe(parameters)
                    + (tags == null ? string.Empty : " · " + tags.Count + " tag" + (tags.Count == 1 ? "" : "s")),
            });
            SyncStatus();
            _tabs.Invalidate(0);

            if (Toasts != null)
            {
                Toasts.Info("Buffered " + name);
            }
            return Task.FromResult(ActionOutcome.Success(
                "Buffered. The tracker sends it with its next flush, so there is no per-event result "
                + "to report. Buffering needs a tracked session, which the SDK starts on login."));
        }

        private async Task<ActionOutcome> SendBatch(FormValues values)
        {
            var names = Split(values.Text("names"));
            if (names.Count == 0)
            {
                return ActionOutcome.Failure("List at least one event name.");
            }

            Dictionary<string, string> parameters;
            string invalid = Parameters(values.Text("parameters"), out parameters);
            if (invalid != null)
            {
                return ActionOutcome.Failure(invalid);
            }
            var tags = Tags(values.Text("tags"));

            var events = new List<BatchEventItemDto>();
            foreach (var name in names)
            {
                var item = new BatchEventItemDto
                {
                    EventName = name,
                    // The item's own Date is what the report groups by, so it is stamped per event
                    // in the round-trip format the backend parses.
                    Date = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                };
                if (parameters != null)
                {
                    // A copy per item: the DTOs are serialized independently and a shared dictionary
                    // would tie them together for anyone editing one afterwards.
                    item.Parameters = new Dictionary<string, string>(parameters);
                }
                if (tags != null)
                {
                    item.Tags = new List<string>(tags);
                }
                events.Add(item);
            }

            var outcome = await Await(Sdk.Analytics.SendBatchAsync(events), "Analytics · batch");
            string title = names.Count == 1 ? names[0] : names.Count + " events";
            Record(title, "batch", outcome,
                names.Count + " event" + (names.Count == 1 ? "" : "s") + " · " + Describe(parameters),
                names);
            return Finish(outcome, "Sent " + title);
        }

        private async Task<ActionOutcome> SendSession(FormValues values)
        {
            var outcome = await Await(Sdk.Analytics.SendSessionStartedAsync(), "Analytics · session started");
            Record("sessions-started", "session", outcome, "no payload", null);
            return Finish(outcome, "Session start reported");
        }

        private async Task<ActionOutcome> SendPlaytime(FormValues values)
        {
            int minutes = values.Int("minutes");
            if (minutes <= 0)
            {
                return ActionOutcome.Failure("Playtime is reported in whole minutes, so send at least 1.");
            }

            var outcome = await Await(Sdk.Analytics.SendPlaytimeAsync(minutes), "Analytics · playtime");
            Record("playtime", "playtime", outcome, minutes + " min", null);
            return Finish(outcome, minutes + " min of playtime reported");
        }

        // ----- local tally ----------------------------------------------------------------------

        /// <summary>
        /// Files one finished call into the session counters, the by-name chart and the call list, and
        /// rebuilds the first tab so the effect is visible even from the Actions tab.
        /// </summary>
        private void Record(string title, string kind, Outcome outcome, string note, IList<string> eventNames)
        {
            _calls++;
            if (outcome.Ok)
            {
                _ok++;
            }
            else
            {
                _failed++;
            }

            if (eventNames != null)
            {
                foreach (var name in eventNames)
                {
                    Bump(name);
                }
            }

            Push(new Shot
            {
                Name = title,
                Kind = kind,
                Ok = outcome.Ok,
                Ms = outcome.Ms,
                At = DateTime.UtcNow,
                // On failure the reason is far more useful than a payload summary nobody can act on.
                Note = outcome.Ok ? note : outcome.Message,
            });

            SyncStatus();
            // The Actions tab is a different pane, so its card keeps showing this run's own result.
            _tabs.Invalidate(0);
        }

        private void Bump(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            int had;
            _byName.TryGetValue(name, out had);
            _byName[name] = had + 1;
        }

        private void Push(Shot shot)
        {
            _history.Insert(0, shot);
            if (_history.Count > HistoryCap)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        private ActionOutcome Finish(Outcome outcome, string success)
        {
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            return ActionOutcome.Success(success + " · " + outcome.Ms + " ms round trip");
        }

        // ----- input parsing --------------------------------------------------------------------

        /// <summary>
        /// Turns the typed JSON object into the flat string map the endpoint takes. Returns null when
        /// the text is acceptable (including blank, which means "no parameters"), otherwise the
        /// message to show on the card.
        /// </summary>
        private static string Parameters(string raw, out Dictionary<string, string> parameters)
        {
            parameters = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            JsonValue tree;
            try
            {
                tree = new JsonService().FromJson<JsonValue>(raw);
            }
            catch (Exception e)
            {
                return "That is not valid JSON: " + e.Message;
            }

            if (tree == null || tree.Type == JsonValueType.Null)
            {
                return null;
            }
            if (tree.Type != JsonValueType.Object)
            {
                return "Parameters have to be a JSON object — the endpoint takes named values.";
            }

            var map = new Dictionary<string, string>();
            // JsonValue is both a list and a dictionary, and its own enumerator is the non-generic
            // one — the cast is what picks the key/value pairs.
            foreach (var pair in (IDictionary<string, JsonValue>)tree)
            {
                map[pair.Key] = Flatten(pair.Value);
            }
            parameters = map.Count == 0 ? null : map;
            return null;
        }

        /// <summary>
        /// One parameter as the endpoint wants it: a string. Numbers use the invariant culture so a
        /// German editor does not send "1,5", and a nested value is written back out as JSON rather
        /// than silently dropped.
        /// </summary>
        private static string Flatten(JsonValue value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            switch (value.Type)
            {
                case JsonValueType.Null: return string.Empty;
                case JsonValueType.String: return (string)value;
                case JsonValueType.Boolean: return (bool)value ? "true" : "false";
                case JsonValueType.Int: return ((int)value).ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Double: return ((double)value).ToString(CultureInfo.InvariantCulture);
                default:
                    try
                    {
                        return new JsonService().ToJson(value);
                    }
                    catch (Exception)
                    {
                        return value.ToString();
                    }
            }
        }

        private static string Describe(Dictionary<string, string> parameters)
        {
            int count = parameters == null ? 0 : parameters.Count;
            if (count == 0)
            {
                return "no parameters";
            }
            return count + (count == 1 ? " parameter" : " parameters");
        }

        private static List<string> Split(string csv)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return parts;
            }
            foreach (var raw in csv.Split(','))
            {
                string trimmed = raw.Trim();
                if (trimmed.Length > 0)
                {
                    parts.Add(trimmed);
                }
            }
            return parts;
        }

        private static List<string> Tags(string csv)
        {
            var tags = Split(csv);
            return tags.Count == 0 ? null : tags;
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async Task<Outcome> Await(AsyncOperation<RestApiResult> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private Outcome Fold(RestApiResult result, string label)
        {
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result);
            }
            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true, Ms = result.DurationMs };
            }
            string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome
            {
                Ok = false,
                Message = message,
                Ms = result != null ? result.DurationMs : 0L,
            };
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;

            // Carried alongside the verdict because the round trip is the only thing a write-only
            // service gives this screen to show.
            public long Ms;
        }

        /// <summary>One call this screen made, kept only in memory — the service has no history endpoint.</summary>
        private sealed class Shot
        {
            public string Name;
            public string Kind;
            public bool Ok;

            /// <summary>Queued events sit in the tracker's buffer: no request, no result, no round trip.</summary>
            public bool Buffered;

            public string Note;
            public long Ms;
            public DateTime At;
        }
    }
}

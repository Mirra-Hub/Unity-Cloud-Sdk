using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Cloud Code screen: run a server-side script by id with a JSON input, and read back whatever it
    /// returned.
    /// <para>
    /// The service exposes exactly one call and no listing of any kind — scripts are authored in the
    /// Mirra Hub console and the client only knows their ids. So the second tab is this screen's own
    /// record of the runs it made: durations, results and both payloads, kept in memory because
    /// there is no endpoint to ask.
    /// </para>
    /// </summary>
    public sealed class CloudCodeView : ServiceView
    {
        private const string ExecuteSnippet =
@"// The script is written in the Mirra Hub console and addressed by its id. The input is a
// plain dictionary and becomes the request body; pass null for a script that takes nothing.
var input = new Dictionary<string, object>
{
    { ""playerLevel"", 7 },
    { ""reward"", ""chest"" }
};

var op = sdk.CloudCode.ExecuteAsync(""grant_daily_chest"", input);
await op.Task();

if (op.Result.IsSuccess)
{
    JsonValue returned = op.Result.Data.Result;   // whatever the script handed back
    // returned.Type says which shape it is: Object, Array, String, Int, Double, Boolean, Null.
}";

        private const string TypedSnippet =
@"// There is a typed overload as well: it re-serializes the script's return value and maps it
// onto a class of yours, so a script's contract can be a C# type instead of a JsonValue walk.
var op = sdk.CloudCode.ExecuteAsync<ChestReward>(""grant_daily_chest"", input);
await op.Task();

ChestReward reward = op.Result.Data;   // default(T) when the script returned null

// A blank script id never reaches the network: the operation completes immediately with a
// validation failure, so guard the id in the UI rather than in a catch block.";

        // Enough runs to compare two attempts at the same script without ever needing paging.
        private const int HistoryCap = 20;

        private readonly List<Run> _runs = new List<Run>();

        private Tabs _tabs;

        // Rebuilt in place after each run, so the run form and its own result stay put.
        private VisualElement _lastSlot;

        private int _ok;
        private int _failed;

        public CloudCodeView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            DeclareCall(new SdkCall("Run a script", ExecuteSnippet,
                "The id is the script's id in the console, and it is case-sensitive."));
            DeclareCall(new SdkCall("Run a script into your own type", TypedSnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);
            SyncStatus();

            _tabs = UseTabs();
            _tabs.Add("Run", LucideIcon.Terminal, BuildRun)
                .Add("History", LucideIcon.History, BuildHistory);
        }

        private void SyncStatus()
        {
            int total = _ok + _failed;
            if (total == 0)
            {
                SetStatus("No runs yet", ChipTone.Neutral);
                return;
            }

            string text = total + (total == 1 ? " run" : " runs");
            if (_failed > 0)
            {
                text += " · " + _failed + " failed";
            }
            SetStatus(text, _failed > 0 ? ChipTone.Warn : ChipTone.Ok);
        }

        // ----- run ------------------------------------------------------------------------------

        private VisualElement BuildRun()
        {
            var col = new VisualElement();

            var hint = new Label("A Cloud Code script is authored in the Mirra Hub console, given an id, "
                + "and invoked by that id from the client. The SDK does not know the script's shape, so "
                + "what comes back is a JSON document rather than a typed model — unless you ask for one "
                + "with the generic overload in the code drawer.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Run a script",
                    "Executes one script and shows its return value. There is no dry run and no "
                    + "listing: if the id does not exist the call comes back as a plain failure.",
                    LucideIcon.Terminal)
                .WithFields(
                    FormField.Text("scriptId", "Script id", null, true)
                        .WithPlaceholder("The id from the console, not the script's display name."),
                    FormField.Json("input", "Input", "{\n  \"playerLevel\": 7\n}")
                        .WithPlaceholder("A JSON object, sent as the request body. Leave it blank to "
                            + "send an empty one."))
                .WithSnippet(ExecuteSnippet)
                .OnRun("Run", Execute));

            col.Add(new SectionHeader("Last run"));
            _lastSlot = new VisualElement();
            col.Add(_lastSlot);
            RenderLast();
            return col;
        }

        private void RenderLast()
        {
            if (_lastSlot == null)
            {
                return;
            }

            if (_runs.Count == 0)
            {
                Replace(_lastSlot, ZeroState.Panel(LucideIcon.Code, "Nothing has run yet",
                    "Scripts are not shipped with the game: you write one in the Mirra Hub console, give "
                    + "it an id, and the client calls it by that id. Run one above and its return value "
                    + "lands here, with the payload you sent kept next to it.",
                    null, null,
                    "A wrong id comes back as a failed request, not as an empty result."));
                return;
            }
            Replace(_lastSlot, RunCard(_runs[0]));
        }

        private VisualElement RunCard(Run run)
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-cc-out");
            card.WithTitle(Fmt.Truncate(Fmt.OrDash(run.ScriptId), 38), Meta.Accent);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(run.Ok ? "succeeded" : "failed", run.Ok ? ChipTone.Ok : ChipTone.Bad));
            chips.Add(new Chip(run.Ms + " ms", ChipTone.Neutral));
            if (run.Ok)
            {
                chips.Add(new Chip("returns " + ReturnKind(run), ChipTone.Info));
            }
            card.Body.Add(chips);

            if (!run.Ok)
            {
                card.Body.Add(ErrorState.Message(Fmt.OrDash(run.Message)));
                card.Body.Add(SentBlock(run));
                return card;
            }

            if (string.IsNullOrEmpty(run.Payload))
            {
                card.Body.Add(ZeroState.Panel(LucideIcon.Braces, "The script returned nothing",
                    "A valid outcome: a script that only writes on the server has no value to hand back. "
                    + "The generic overload gives you default(T) in that case rather than failing."));
                card.Body.Add(SentBlock(run));
                return card;
            }

            card.Body.Add(new SectionHeader("Returned"));
            card.Body.Add(new JsonViewer().SetRaw(run.Payload).SetMaxLines(20));
            card.Body.Add(SentBlock(run));
            return card;
        }

        /// <summary>The payload that produced this result, folded away — it is context, not the answer.</summary>
        private static VisualElement SentBlock(Run run)
        {
            var box = new VisualElement();
            box.Add(new SectionHeader("Sent"));
            box.Add(new JsonViewer().SetRaw(string.IsNullOrEmpty(run.Input) ? "{}" : run.Input)
                .SetMaxLines(14)
                .SetCollapsed(true));
            return box;
        }

        private static string ReturnKind(Run run)
        {
            return string.IsNullOrEmpty(run.Type) ? "nothing" : run.Type.ToLowerInvariant();
        }

        // ----- history --------------------------------------------------------------------------

        private VisualElement BuildHistory()
        {
            var col = new VisualElement();

            var kpis = new KpiRow();
            if (_runs.Count == 0)
            {
                kpis.AddZero("Runs", LucideIcon.Terminal)
                    .AddZero("Succeeded", LucideIcon.CircleCheck)
                    .AddZero("Failed", LucideIcon.CircleX)
                    .AddZero("Avg round trip", LucideIcon.Timer, Fmt.Dash);
            }
            else
            {
                long total = 0L;
                foreach (var run in _runs)
                {
                    total += run.Ms;
                }
                kpis.Add("Runs", LucideIcon.Terminal, (_ok + _failed).ToString())
                    .Add("Succeeded", LucideIcon.CircleCheck, _ok.ToString())
                    .Add("Failed", LucideIcon.CircleX, _failed.ToString(), null, _failed > 0)
                    .Add("Avg round trip", LucideIcon.Timer, total / _runs.Count + " ms");
            }
            col.Add(kpis);

            var scope = new Label("Cloud Code has no endpoint that lists past executions, so this is the "
                + "client's own record: the last " + HistoryCap + " runs made from this screen. The run "
                + "count keeps counting past that, the timings only cover what is still listed.");
            scope.AddToClassList("sc-fs-hint");
            scope.AddToClassList("sc-cc-scope");
            col.Add(scope);

            if (_runs.Count == 0)
            {
                col.Add(ZeroState.Table(HistoryColumns(),
                    "Every script you run from the Run tab is added here with its round trip, its "
                    + "result and both payloads. Nothing is fetched — a fresh screen starts empty even "
                    + "if the project has been running scripts for months.",
                    3, "Run a script", () => _tabs.Select(0)));
                return col;
            }

            col.Add(new SectionHeader("Round trip", _runs.Count + (_runs.Count == 1 ? " run" : " runs")));
            var spark = new Sparkline(64f);
            var series = new List<float>();
            // Oldest first: a sparkline reads left to right, while the list is newest first.
            for (int i = _runs.Count - 1; i >= 0; i--)
            {
                series.Add(_runs[i].Ms);
            }
            spark.SetAccent(Meta.Accent).SetArea(true).SetData(series)
                .SetEmptyText("No runs yet");
            col.Add(spark);

            col.Add(new SectionHeader("Runs", _runs.Count.ToString()));
            var table = new DataTable(HistoryColumns())
                .WithZebra()
                .WithMaxHeight(420f)
                .WithRowClick(o => ShowRun((Run)o));
            table.Bind(_runs, o => !((Run)o).Ok);
            col.Add(table);
            return col;
        }

        private DataColumn[] HistoryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "SCRIPT", Grow = 2f,
                    SortKey = o => ((Run)o).ScriptId,
                    Cell = o =>
                    {
                        var run = (Run)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-cc-cell");

                        var id = new Label(Fmt.OrDash(run.ScriptId));
                        id.enableRichText = false;
                        id.AddToClassList("sc-cc-id");
                        box.Add(id);

                        var sub = new Label(run.Ok
                            ? Fmt.Truncate(Minify(run.Input), 56)
                            : Fmt.Truncate(Fmt.OrDash(run.Message), 56));
                        sub.enableRichText = false;
                        sub.AddToClassList("sc-cc-in");
                        if (!run.Ok)
                        {
                            sub.AddToClassList("sc-cc-in--bad");
                        }
                        box.Add(sub);
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "RESULT", FixedWidth = true, Px = 108,
                    Cell = o =>
                    {
                        var run = (Run)o;
                        return new Badge(run.Ok ? "ok" : "failed", run.Ok ? ChipTone.Ok : ChipTone.Bad);
                    },
                },
                new DataColumn
                {
                    Header = "RETURNED", FixedWidth = true, Px = 112,
                    SortKey = o => ((Run)o).Type,
                    Cell = o =>
                    {
                        var run = (Run)o;
                        if (!run.Ok)
                        {
                            return new Label(Fmt.Dash);
                        }
                        return new Chip(ReturnKind(run), ChipTone.Info);
                    },
                },
                new DataColumn
                {
                    Header = "TOOK", FixedWidth = true, Px = 78, Align = "right",
                    SortKey = o => ((Run)o).Ms,
                    Cell = o => new Label(((Run)o).Ms + " ms"),
                },
                new DataColumn
                {
                    Header = "WHEN", FixedWidth = true, Px = 96, Align = "right",
                    SortKey = o => ((Run)o).At,
                    Cell = o => RelativeTime.Build(((Run)o).At),
                },
            };
        }

        private void ShowRun(Run run)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Script id", Fmt.OrDash(run.ScriptId), run.ScriptId));
            kv.Add(Kv("Result", run.Ok ? "succeeded" : "failed", null));
            kv.Add(Kv("Returned", run.Ok ? ReturnKind(run) : Fmt.Dash, null));
            kv.Add(Kv("Round trip", run.Ms + " ms", null));
            kv.Add(Kv("Ran at", RelativeTime.Absolute(run.At), null));
            body.Add(kv);

            if (!run.Ok)
            {
                body.Add(new SectionHeader("Why it failed"));
                body.Add(ErrorState.Message(Fmt.OrDash(run.Message)));
            }
            else if (string.IsNullOrEmpty(run.Payload))
            {
                body.Add(new SectionHeader("Returned"));
                body.Add(ZeroState.Panel(LucideIcon.Braces, "No value",
                    "The script ran and handed nothing back — normal for one that only writes."));
            }
            else
            {
                body.Add(new SectionHeader("Returned"));
                body.Add(new JsonViewer().SetRaw(run.Payload).SetMaxLines(24));
            }

            body.Add(new SectionHeader("Sent"));
            body.Add(new JsonViewer().SetRaw(string.IsNullOrEmpty(run.Input) ? "{}" : run.Input)
                .SetMaxLines(18));

            var again = new VisualElement();
            again.AddToClassList("sc-chip-row");
            var rerun = new Button(() =>
            {
                if (Popup != null)
                {
                    Popup.Close();
                }
                Rerun(run);
            })
            {
                text = "Run it again",
            };
            rerun.AddToClassList("sc-btn");
            rerun.AddToClassList("sc-btn--primary");
            again.Add(rerun);
            body.Add(again);

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(run.ScriptId), 34));
        }

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }

        // ----- running --------------------------------------------------------------------------

        private async Task<ActionOutcome> Execute(FormValues values)
        {
            string scriptId = values.Text("scriptId");
            string raw = values.Text("input");

            Dictionary<string, object> input;
            string invalid = Input(raw, out input);
            if (invalid != null)
            {
                return ActionOutcome.Failure(invalid);
            }

            var run = await Perform(scriptId, raw, input);
            if (!run.Ok)
            {
                return ActionOutcome.Failure(run.Message);
            }

            if (Toasts != null)
            {
                Toasts.Ok("Ran " + scriptId);
            }

            var detail = new VisualElement();
            detail.AddToClassList("sc-cc-out");
            if (string.IsNullOrEmpty(run.Payload))
            {
                var quiet = new Label("The script returned no value — nothing to render, and nothing "
                    + "wrong with that.");
                quiet.AddToClassList("sc-fs-hint");
                detail.Add(quiet);
            }
            else
            {
                detail.Add(new JsonViewer().SetRaw(run.Payload).SetMaxLines(18));
            }
            return ActionOutcome.Success(
                scriptId + " returned " + ReturnKind(run) + " in " + run.Ms + " ms", detail);
        }

        private async void Rerun(Run previous)
        {
            Dictionary<string, object> input;
            string invalid = Input(previous.Input, out input);
            if (invalid != null)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Not run · " + invalid);
                }
                return;
            }

            var run = await Perform(previous.ScriptId, previous.Input, input);
            if (Toasts == null)
            {
                return;
            }
            if (run.Ok)
            {
                Toasts.Ok("Ran " + previous.ScriptId + " · " + run.Ms + " ms");
                return;
            }
            Toasts.Fail("Run failed · " + run.Message);
        }

        /// <summary>
        /// Issues the call, files it into the session record and refreshes what depends on it. The Run
        /// tab is patched in place rather than invalidated, so the card that started the run keeps
        /// showing its own result.
        /// </summary>
        private async Task<Run> Perform(string scriptId, string raw, Dictionary<string, object> input)
        {
            var op = Sdk.CloudCode.ExecuteAsync(scriptId, input);
            var outcome = await AwaitData(op, "Cloud Code · " + Fmt.Truncate(scriptId, 24));

            var run = new Run
            {
                ScriptId = scriptId,
                Input = string.IsNullOrWhiteSpace(raw) ? "{}" : raw,
                Ok = outcome.Ok,
                Message = outcome.Message,
                Ms = outcome.Ms,
                At = DateTime.UtcNow,
            };

            if (outcome.Ok)
            {
                var returned = op != null && op.Result != null && op.Result.Data != null
                    ? op.Result.Data.Result
                    : null;
                // A script that returns nothing answers with a JSON null, which is a success with no
                // payload rather than a missing response.
                if (returned != null && returned.Type != JsonValueType.Null)
                {
                    run.Type = returned.Type.ToString();
                    run.Payload = Serialize(returned);
                }
                _ok++;
            }
            else
            {
                _failed++;
            }

            _runs.Insert(0, run);
            if (_runs.Count > HistoryCap)
            {
                _runs.RemoveAt(_runs.Count - 1);
            }

            SyncStatus();
            RenderLast();
            _tabs.Invalidate(1);
            return run;
        }

        // ----- payloads -------------------------------------------------------------------------

        /// <summary>
        /// Turns the typed JSON object into the dictionary the call takes. Returns null when the text
        /// is acceptable (blank included, which means "send an empty object"), otherwise the message
        /// to show on the card.
        /// </summary>
        private static string Input(string raw, out Dictionary<string, object> input)
        {
            input = null;
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
                return "The input has to be a JSON object — it is sent as the request body, and the "
                    + "script reads its fields by name.";
            }

            var map = new Dictionary<string, object>();
            // JsonValue is both a list and a dictionary, and its own enumerator is the non-generic
            // one — the cast is what picks the key/value pairs.
            foreach (var pair in (IDictionary<string, JsonValue>)tree)
            {
                map[pair.Key] = ToPlain(pair.Value);
            }
            input = map.Count == 0 ? null : map;
            return null;
        }

        /// <summary>
        /// Flattens a parsed node back to a CLR value for the request body. Objects and arrays are
        /// handed over as their <see cref="JsonValue"/>, which the serializer writes out unchanged.
        /// </summary>
        private static object ToPlain(JsonValue value)
        {
            if (value == null)
            {
                return null;
            }
            switch (value.Type)
            {
                case JsonValueType.Null: return null;
                case JsonValueType.Boolean: return (bool)value;
                case JsonValueType.Int: return (int)value;
                case JsonValueType.Double: return (double)value;
                case JsonValueType.String: return (string)value;
                default: return value;
            }
        }

        /// <summary>
        /// The returned document as text. <c>Fmt.Json</c> deliberately summarises a tree
        /// ("{ 4 keys }"), which is exactly what a result viewer must not do.
        /// </summary>
        private static string Serialize(JsonValue value)
        {
            try
            {
                return new JsonService().ToJson(value, true);
            }
            catch (Exception e)
            {
                return "// could not render the result: " + e.Message;
            }
        }

        /// <summary>
        /// Single-line form of a stored payload, for the table's secondary line. Whitespace is
        /// collapsed rather than stripped so a string literal inside the document stays readable.
        /// </summary>
        private static string Minify(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "{}";
            }

            var sb = new StringBuilder(json.Length);
            bool pendingSpace = false;
            foreach (char c in json)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async Task<Outcome> AwaitData<T>(AsyncOperation<RestApiResult<T>> op, string label)
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
                Ctx.Log.Record(label, result, ExecuteSnippet);
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

            // Kept on the verdict because the history table's whole point is comparing round trips
            // between two runs of the same script.
            public long Ms;
        }

        /// <summary>One execution this screen performed, kept in memory — the service lists nothing.</summary>
        private sealed class Run
        {
            public string ScriptId;
            public string Input;
            public bool Ok;
            public string Message;

            /// <summary>JSON type name of the return value; null when the script returned nothing.</summary>
            public string Type;

            /// <summary>Pretty-printed return value; null when the script returned nothing.</summary>
            public string Payload;

            public long Ms;
            public DateTime At;
        }
    }
}

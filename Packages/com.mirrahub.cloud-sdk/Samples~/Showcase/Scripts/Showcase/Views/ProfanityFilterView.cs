using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.ProfanityFilter.Responses;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Text check screen: submit a string, see whether it passed, what the masked version looks like,
    /// and which fragments were flagged.
    /// <para>
    /// The service is a single call, so the screen keeps a session history instead of leaving a
    /// results pane that is blank until you type — the point is comparing several checks.
    /// </para>
    /// </summary>
    public sealed class ProfanityFilterView : ServiceView
    {
        private const string CheckSnippet =
@"// One call. The group key is required — there is no project-wide default. A blank key is
// rejected on the client, an unknown one comes back as a not-found error from the server.
var op = sdk.ProfanityFilter.CheckAsync(""some user text"", groupKey: ""chat_strict"");
await op.Task();

if (op.Result.IsSuccess)
{
    ProfanityCheckResponse r = op.Result.Data;
    // r.isClean, r.maskedText, r.matches[i].start / .length
}

// Empty input answers clean without a round trip; text over 2000 characters is
// rejected locally before any request is made.";

        private sealed class Check
        {
            public string Text;
            public string Group;
            public bool Clean;
            public string Masked;
            public int Matches;
            public DateTime At;
        }

        private readonly List<Check> _history = new List<Check>();

        private VisualElement _resultSlot;
        private VisualElement _historySlot;
        private TextField _input;
        private TextField _group;

        public ProfanityFilterView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _history.Clear();

            DeclareCall(new SdkCall("Check a string", CheckSnippet,
                "The only call this service has."));

            UseToolbar().WithSpacer().WithRefresh(Refresh);
            SyncStatus();

            Content.Add(BuildForm());

            Content.Add(new SectionHeader("Result"));
            _resultSlot = AddSlot();
            Replace(_resultSlot, ZeroState.Panel(LucideIcon.ShieldCheck, "Nothing checked yet",
                "Type something above and press Check. The answer says whether the text passed, gives "
                + "a masked version you can show instead, and points at the fragments that were flagged."));

            Content.Add(new SectionHeader("This session"));
            _historySlot = AddSlot();
            RenderHistory();
        }

        private VisualElement BuildForm()
        {
            var card = new Card(Meta.Accent);
            card.WithTitle("Check some text", Meta.Accent);

            var hint = new Label("Groups let a project use different rules in different places — a "
                + "nickname field and an open chat rarely want the same strictness. Groups are created "
                + "in the console, and the key is required: there is nothing to fall back on.");
            hint.AddToClassList("sc-fs-hint");
            card.Body.Add(hint);

            _input = new TextField { multiline = true, value = "Have a nice day" };
            _input.AddToClassList("sc-field");
            _input.AddToClassList("sc-field--multiline");
            _input.label = "Text";
            card.Body.Add(_input);

            _group = new TextField { label = "Group key *" };
            _group.AddToClassList("sc-field");
            card.Body.Add(_group);

            var actions = new VisualElement();
            actions.AddToClassList("sc-chip-row");

            var check = new Button(RunCheck) { text = "Check" };
            check.AddToClassList("sc-btn");
            check.AddToClassList("sc-btn--primary");
            actions.Add(check);

            var clear = new Button(() =>
            {
                _history.Clear();
                RenderHistory();
                SyncStatus();
            })
            {
                text = "Clear history",
            };
            clear.AddToClassList("sc-btn");
            actions.Add(clear);

            card.Body.Add(actions);
            return card;
        }

        private async void RunCheck()
        {
            string text = _input != null ? _input.value : null;
            string group = _group != null ? _group.value : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (Toasts != null)
                {
                    Toasts.Info("Type something to check first");
                }
                return;
            }
            if (string.IsNullOrWhiteSpace(group))
            {
                if (Toasts != null)
                {
                    Toasts.Info("Name the group to check against — the key is required");
                }
                return;
            }

            Skeleton.Into(_resultSlot, 3);

            var op = Sdk.ProfanityFilter.CheckAsync(text, group?.Trim());
            if (op == null)
            {
                Replace(_resultSlot, ErrorState.Message("The call could not be started."));
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Text check", result, CheckSnippet);
            }

            if (_resultSlot.panel == null)
            {
                return;
            }
            if (result == null || !result.IsSuccess || result.Data == null)
            {
                Replace(_resultSlot, ErrorState.Build(result != null ? result.Error : null));
                return;
            }

            var response = result.Data;
            _history.Insert(0, new Check
            {
                Text = text,
                Group = group,
                Clean = response.isClean,
                Masked = response.maskedText,
                Matches = response.matches != null ? response.matches.Length : 0,
                At = DateTime.Now,
            });
            if (_history.Count > 20)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            Replace(_resultSlot, BuildResult(text, response));
            RenderHistory();
            SyncStatus();
        }

        private VisualElement BuildResult(string original, ProfanityCheckResponse response)
        {
            var card = new Card(response.isClean ? ShowcaseTheme.Ok : ShowcaseTheme.Warn);
            card.WithTitle(response.isClean ? "Passed" : "Flagged",
                response.isClean ? ShowcaseTheme.Ok : ShowcaseTheme.Warn);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(response.isClean ? "clean" : "not clean",
                response.isClean ? ChipTone.Ok : ChipTone.Warn));
            int matches = response.matches != null ? response.matches.Length : 0;
            chips.Add(new Chip(matches + (matches == 1 ? " fragment" : " fragments"),
                matches == 0 ? ChipTone.Neutral : ChipTone.Bad));
            card.Body.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Submitted", Fmt.Truncate(original, 90)));
            kv.Add(Kv("Masked", Fmt.Truncate(Fmt.OrDash(response.maskedText), 90)));
            card.Body.Add(kv);

            if (matches == 0)
            {
                var none = new Label("Nothing was flagged, so the masked version is the text you sent.");
                none.AddToClassList("sc-fs-hint");
                card.Body.Add(none);
                return card;
            }

            card.Body.Add(new SectionHeader("Flagged fragments", matches.ToString()));
            var list = new VisualElement();
            foreach (var match in response.matches)
            {
                var row = new ListRow();
                row.SetTitle(Excerpt(original, match.start, match.length));
                row.SetSubtitle("at " + match.start + ", " + match.length
                    + (match.length == 1 ? " character" : " characters"));
                list.Add(row);
            }
            card.Body.Add(list);
            return card;
        }

        /// <summary>
        /// Cuts the flagged span out of the original text. The offsets come from the server, so they
        /// are clamped rather than trusted — a mismatched encoding would otherwise throw here.
        /// </summary>
        private static string Excerpt(string text, int start, int length)
        {
            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length || length <= 0)
            {
                return Fmt.Dash;
            }
            int safe = Math.Min(length, text.Length - start);
            return text.Substring(start, safe);
        }

        private void RenderHistory()
        {
            if (_historySlot == null)
            {
                return;
            }
            _historySlot.Clear();

            if (_history.Count == 0)
            {
                _historySlot.Add(ZeroState.Table(HistoryColumns(),
                    "Every check you run in this session is kept here, so you can compare strictness "
                    + "between texts and between groups.", 3));
                return;
            }

            var table = new DataTable(HistoryColumns()).WithZebra().WithMaxHeight(400f);
            table.Bind(_history, o => !((Check)o).Clean);
            _historySlot.Add(table);
        }

        private static DataColumn[] HistoryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "TEXT", Grow = 2f,
                    SortKey = o => ((Check)o).Text,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(((Check)o).Text, 46));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "GROUP", Grow = 1f,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(((Check)o).Group));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "VERDICT", FixedWidth = true, Px = 110,
                    SortKey = o => ((Check)o).Clean ? "clean" : "flagged",
                    Cell = o =>
                    {
                        var check = (Check)o;
                        return new Chip(check.Clean ? "clean" : "flagged",
                            check.Clean ? ChipTone.Ok : ChipTone.Warn);
                    },
                },
                new DataColumn
                {
                    Header = "MASKED", Grow = 2f,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(Fmt.OrDash(((Check)o).Masked), 46));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "WHEN", FixedWidth = true, Px = 80, Align = "right",
                    SortKey = o => ((Check)o).At,
                    Cell = o => new Label(Fmt.Time(((Check)o).At)),
                },
            };
        }

        private void SyncStatus()
        {
            if (_history.Count == 0)
            {
                SetStatus("Nothing checked yet", ChipTone.Neutral);
                return;
            }
            int flagged = 0;
            foreach (var check in _history)
            {
                if (!check.Clean)
                {
                    flagged++;
                }
            }
            SetStatus(_history.Count + " checked · " + flagged + " flagged",
                flagged > 0 ? ChipTone.Warn : ChipTone.Ok);
        }

        private static VisualElement Kv(string key, string value)
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
            return row;
        }
    }
}

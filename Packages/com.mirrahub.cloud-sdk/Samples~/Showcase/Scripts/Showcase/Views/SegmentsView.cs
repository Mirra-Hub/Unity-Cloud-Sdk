using System;
using System.Collections.Generic;
using MirraCloud.Core.Auth;
using Plugins.MirraCloud.Core.Services.Segments.Dto;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Segments screen: the audience groups this project defines, and which of them the signed-in
    /// player fell into.
    /// <para>
    /// The two halves deliberately come from two different services. <c>Segments.LoadConfigAsync</c>
    /// lists what exists; membership is a property of the account
    /// (<c>PlayerAccountInfo.SegmentKeys</c>), because the rule engine evaluates the segments
    /// server-side when the player signs in. Nothing on this screen writes — the Segments service is
    /// read-only, and the calls that change a player's segments belong to Player Account.
    /// </para>
    /// </summary>
    public sealed class SegmentsView : ServiceView
    {
        private const string SegmentsSnippet =
@"// Every segment configured for this project and branch. Read-only: the SDK lists segments,
// it never creates or edits one.
var op = sdk.Segments.LoadConfigAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    foreach (SegmentDto s in result.Data)
    {
        // s.id, s.name, s.description, s.isEnable, s.ruleTreeId,
        // s.createdDate, s.updatedDate   (this service spells its fields in lowerCamelCase)
    }
}";

        private const string AccountSnippet =
@"// Membership does not come from the Segments service: the rule engine evaluates every
// enabled segment at sign-in and stores what matched on the account.
var op = sdk.PlayerAccount.GetAccountAsync();
await op.Task();

if (op.Result.IsSuccess)
{
    PlayerAccountInfo a = op.Result.Data;
    // a.SegmentKeys : the segments this account matched
    // a.AbTestKeys  : the A/B test buckets it landed in
}

// The account captured at sign-in is also available without a request:
PlayerAccountInfo cached = sdk.PlayerAccount.PlayerAccountInfo;";

        private const string WriteSnippet =
@"// Writing segments is a Player Account call, not a Segments one — this screen never runs
// these two, it only shows where they live.
await sdk.PlayerAccount.UpdateSegmentsAsync(new[] { ""vip"", ""whales"" }).Task();

// The same for one of the account's profiles:
await sdk.PlayerAccount.UpdateProfileSegmentsAsync(profileId, new[] { ""vip"" }).Task();";

        private const string FilterAll = "All";
        private const string FilterMine = "Mine";
        private const string FilterOthers = "Not mine";

        // The account stores what the rule engine matched, which is the segment's *name*; a hand
        // written update can put an id in there instead. Both are accepted as a match, so neither
        // way of filling the list makes this screen lie about membership.
        private readonly HashSet<string> _mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private SegmentDto[] _segments;
        private PlayerAccountInfo _account;
        private VisualElement _overviewSlot;
        private VisualElement _tableSlot;
        private string _search = string.Empty;
        private string _membership = FilterAll;

        public SegmentsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _search = string.Empty;
            _membership = FilterAll;
            _segments = null;
            _tableSlot = null;

            // The account cached at sign-in makes the chips and KPIs correct on the first frame; the
            // request below replaces it with a freshly fetched one.
            _account = Sdk.PlayerAccount != null ? Sdk.PlayerAccount.PlayerAccountInfo : null;
            SyncMine();
            SyncStatus();

            DeclareCall(new SdkCall("List the project's segments", SegmentsSnippet));
            DeclareCall(new SdkCall("Read the player's membership", AccountSnippet,
                "A Player Account call: the segments a player is in live on the account."));
            DeclareCall(new SdkCall("Change a player's segments", WriteSnippet,
                "Shown for completeness. This screen is read-only and never issues it."));

            UseToolbar()
                .WithSearch("Filter segments by name or description", OnSearch)
                .WithFilter("Show", new[] { FilterAll, FilterMine, FilterOthers }, OnMembership, FilterAll)
                .WithSpacer()
                .WithRefresh(Refresh);

            _overviewSlot = AddSlot();
            RenderOverview();

            Content.Add(new SectionHeader("This player"));
            var accountSlot = AddSlot();
            ViewBind.Load(
                () => Sdk.PlayerAccount.GetAccountAsync(),
                accountSlot,
                BuildAccount,
                a => a == null,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Account segments",
                    Snippet = AccountSnippet,
                    ServiceName = "Segments",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.User, "No account to read",
                        "Segment membership is stored on the signed-in account. Sign in and this section "
                        + "shows the segments and A/B tests the server matched for that player."),
                });

            Content.Add(new SectionHeader("All segments"));
            var listSlot = AddSlot();
            ViewBind.Load(
                () => Sdk.Segments.LoadConfigAsync(),
                listSlot,
                BuildList,
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Segments",
                    Snippet = SegmentsSnippet,
                    ServiceName = "Segments",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NothingConfigured,
                });

            Content.Add(WritesSection());
        }

        private VisualElement NothingConfigured()
        {
            return ZeroState.Table(SegmentColumns(),
                "This branch defines no segments. A segment is authored in the Mirra Hub console as a "
                + "rule tree over player data; once it exists here, the server evaluates it at sign-in "
                + "and the players it matches carry its name on their account.",
                3);
        }

        // ----- header -------------------------------------------------------------------------

        /// <summary>
        /// Header verdict: how much of the project's audience map this player sits in. Before the
        /// segment list arrives the account's own count is the only honest number available.
        /// </summary>
        private void SyncStatus()
        {
            if (_segments != null)
            {
                int total = _segments.Length;
                int mine = MineCount();
                SetStatus(mine + " of " + total + (total == 1 ? " segment" : " segments"),
                    mine > 0 ? ChipTone.Ok : ChipTone.Neutral);
                return;
            }

            if (_account != null && _account.SegmentKeys != null && _account.SegmentKeys.Length > 0)
            {
                int carried = _account.SegmentKeys.Length;
                SetStatus(carried + (carried == 1 ? " segment on the account" : " segments on the account"),
                    ChipTone.Ok);
                return;
            }

            SetStatus("No segments", ChipTone.Neutral);
        }

        // ----- overview -----------------------------------------------------------------------

        /// <summary>
        /// The derived band on top: it depends on both requests, so it is rebuilt whenever either of
        /// them lands instead of being bound to one of them.
        /// </summary>
        private void RenderOverview()
        {
            if (_overviewSlot == null)
            {
                return;
            }

            var col = new VisualElement();
            col.Add(Kpis());
            col.Add(Breakdown());
            Replace(_overviewSlot, col);
        }

        private VisualElement Kpis()
        {
            var kpis = new KpiRow();

            int total = _segments != null ? _segments.Length : 0;
            int mine = MineCount();
            int enabled = 0;
            if (_segments != null)
            {
                foreach (var s in _segments)
                {
                    if (s != null && s.isEnable)
                    {
                        enabled++;
                    }
                }
            }
            int abTests = _account != null && _account.AbTestKeys != null ? _account.AbTestKeys.Length : 0;

            if (total == 0)
            {
                kpis.AddZero("Segments", LucideIcon.ChartPie);
            }
            else
            {
                kpis.Add("Segments", LucideIcon.ChartPie, total.ToString());
            }

            if (mine == 0)
            {
                kpis.AddZero("This player is in", LucideIcon.UserCheck);
            }
            else
            {
                kpis.Add("This player is in", LucideIcon.UserCheck, mine.ToString(), null, true);
            }

            if (enabled == 0)
            {
                kpis.AddZero("Enabled", LucideIcon.CircleCheck);
            }
            else
            {
                kpis.Add("Enabled", LucideIcon.CircleCheck, enabled + " of " + total);
            }

            if (abTests == 0)
            {
                kpis.AddZero("A/B tests", LucideIcon.Percent);
            }
            else
            {
                kpis.Add("A/B tests", LucideIcon.Percent, abTests.ToString());
            }

            return kpis;
        }

        /// <summary>
        /// "Mine against the rest" as a ring plus the whole audience map as one chip strip — the
        /// player's segments in accent, everything else neutral.
        /// </summary>
        private VisualElement Breakdown()
        {
            var box = new VisualElement();
            box.AddToClassList("sc-seg-split");

            int total = _segments != null ? _segments.Length : 0;
            int mine = MineCount();
            int others = total - mine;

            var donut = new DonutChart(150f);
            donut.SetData(new[]
                {
                    new ChartPoint("In these", mine, ShowcaseTheme.AccentSoft),
                    new ChartPoint("Not in these", others, ShowcaseTheme.TextMuted),
                })
                .SetCenter(mine.ToString(), "of " + total)
                .SetEmptyText(total == 0 ? "No segments yet" : "Not in any segment");
            box.Add(donut);

            var side = new VisualElement();
            side.AddToClassList("sc-seg-split__side");

            var caption = new Label(total == 0
                ? "Once segments exist in this project they all appear here, with the ones this player "
                + "matched picked out in accent."
                : "Every segment in the project. Accent means this player is in it — targeting a reward, "
                + "an offer or a remote-config value at that segment would reach them.");
            caption.AddToClassList("sc-fs-hint");
            side.Add(caption);

            side.Add(AllChips());

            var stray = StrayKeys();
            if (stray.Count > 0)
            {
                var note = new Label("The account also carries " + stray.Count
                    + (stray.Count == 1 ? " key that matches no segment" : " keys that match no segment")
                    + " in this branch — usually a segment that was renamed or removed after the player "
                    + "signed in, or a value written by hand.");
                note.AddToClassList("sc-fs-hint");
                note.AddToClassList("sc-seg-split__note");
                side.Add(note);

                var strayRow = new VisualElement();
                strayRow.AddToClassList("sc-chip-row");
                foreach (var key in stray)
                {
                    strayRow.Add(new Chip(Fmt.Truncate(key, 24), ChipTone.Warn));
                }
                side.Add(strayRow);
            }

            box.Add(side);
            return box;
        }

        private VisualElement AllChips()
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            if (_segments == null || _segments.Length == 0)
            {
                row.Add(new Chip("nothing configured", ChipTone.Neutral));
                return row;
            }

            foreach (var s in _segments)
            {
                if (s == null)
                {
                    continue;
                }
                row.Add(new Chip(Fmt.Truncate(Label(s), 24), IsMine(s) ? ChipTone.Accent : ChipTone.Neutral));
            }
            return row;
        }

        // ----- this player --------------------------------------------------------------------

        private VisualElement BuildAccount(PlayerAccountInfo account)
        {
            _account = account;
            SyncMine();
            SyncStatus();
            RenderOverview();
            RenderTable();

            var col = new VisualElement();

            var hint = new Label("Exactly what the account carries. The server recomputes this list at "
                + "sign-in from every enabled segment's rule tree, so a player whose data changed mid-session "
                + "keeps the segments they had when they logged in.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(Tags("Segments", account.SegmentKeys, ChipTone.Accent,
                LucideIcon.ChartPie, "This player matched no segment.",
                "Either the branch defines none, or none of their rule trees evaluated to true for this "
                + "player's data."));
            col.Add(Tags("A/B tests", account.AbTestKeys, ChipTone.Warn,
                LucideIcon.Percent, "This player is in no A/B test.",
                "A/B buckets are assigned the same way segments are and travel on the same account."));
            return col;
        }

        /// <summary>A titled chip strip that keeps its heading when there is nothing to show, so the
        /// reader learns the account simply carries none instead of wondering where the section went.</summary>
        private VisualElement Tags(string title, string[] values, ChipTone tone, string glyph,
            string emptyTitle, string emptyMessage)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-seg-block");
            box.Add(new SectionHeader(title, values != null ? values.Length.ToString() : "0"));

            if (values == null || values.Length == 0)
            {
                box.Add(ZeroState.Panel(glyph, emptyTitle, emptyMessage));
                return box;
            }

            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }
                row.Add(new Chip(Fmt.Truncate(value.Trim(), 26), tone));
            }
            box.Add(row);
            return box;
        }

        // ----- all segments -------------------------------------------------------------------

        private VisualElement BuildList(SegmentDto[] segments)
        {
            _segments = segments;
            SyncStatus();
            RenderOverview();

            var root = new VisualElement();

            var hint = new Label("Segments are evaluated on the server, so this table is the definition "
                + "side: what each one is called, whether it is switched on, and which rule tree decides "
                + "it. Pick a row for its ids and timestamps.");
            hint.AddToClassList("sc-fs-hint");
            root.Add(hint);

            _tableSlot = new VisualElement();
            root.Add(_tableSlot);
            RenderTable();
            return root;
        }

        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            RenderTable();
        }

        private void OnMembership(string value)
        {
            _membership = string.IsNullOrEmpty(value) ? FilterAll : value;
            RenderTable();
        }

        private void RenderTable()
        {
            if (_tableSlot == null || _segments == null)
            {
                return;
            }

            var rows = new List<SegmentDto>();
            foreach (var s in _segments)
            {
                if (s != null && Matches(s))
                {
                    rows.Add(s);
                }
            }

            if (rows.Count == 0)
            {
                // Offering "clear the filters" when nothing is filtered would be a button that changes
                // nothing, so the call to action only appears when it has something to undo.
                bool filtered = _search.Length > 0 || _membership != FilterAll;
                Replace(_tableSlot, ZeroState.Table(SegmentColumns(), NoMatchMessage(), 3,
                    filtered ? "Clear the filters" : null,
                    filtered ? (Action)ClearFilters : null));
                return;
            }

            var table = new DataTable(SegmentColumns())
                .WithZebra()
                .WithSort(1, true)
                .WithRowClick(row => ShowSegment((SegmentDto)row));
            table.Bind(rows, row => IsMine((SegmentDto)row));
            Replace(_tableSlot, table);
        }

        private string NoMatchMessage()
        {
            if (_search.Length == 0 && _membership == FilterAll)
            {
                // Reachable when the branch answers with a list of empty entries rather than an empty
                // list — rare, but it must not read as "your filter matched nothing".
                return "This branch answered with no usable segment. A segment is authored in the Mirra "
                    + "Hub console as a rule tree over player data.";
            }
            if (_membership == FilterMine)
            {
                return _search.Length == 0
                    ? "This player is in none of this branch's segments. Their rule trees all evaluated "
                      + "to false for the account's data at sign-in."
                    : "None of this player's segments match \"" + Fmt.Truncate(_search, 24) + "\".";
            }
            if (_membership == FilterOthers)
            {
                return _search.Length == 0
                    ? "This player is in every segment this branch defines."
                    : "Nothing outside this player's segments matches \"" + Fmt.Truncate(_search, 24) + "\".";
            }
            return "No segment matches \"" + Fmt.Truncate(_search, 24) + "\".";
        }

        private void ClearFilters()
        {
            _search = string.Empty;
            _membership = FilterAll;
            // The toolbar owns the search field and the dropdown, and neither can be reset from here,
            // so the whole screen is rebuilt — which is also what puts the controls back to their
            // defaults.
            Refresh();
        }

        private bool Matches(SegmentDto segment)
        {
            if (_membership == FilterMine && !IsMine(segment))
            {
                return false;
            }
            if (_membership == FilterOthers && IsMine(segment))
            {
                return false;
            }
            if (_search.Length == 0)
            {
                return true;
            }
            return Contains(segment.name) || Contains(segment.description) || Contains(segment.id);
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private DataColumn[] SegmentColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "SEGMENT", Grow = 2.2f,
                    SortKey = o => Label((SegmentDto)o),
                    Cell = NameCell,
                },
                new DataColumn
                {
                    Header = "PLAYER", FixedWidth = true, Px = 112, Align = "center",
                    // 0 sorts before 1, so ascending puts this player's segments on top — which is
                    // the order the table opens in.
                    SortKey = o => IsMine((SegmentDto)o) ? 0 : 1,
                    Cell = o => IsMine((SegmentDto)o)
                        ? new Chip("in", ChipTone.Accent)
                        : new Chip("out", ChipTone.Neutral),
                },
                new DataColumn
                {
                    Header = "STATE", FixedWidth = true, Px = 104, Align = "center",
                    SortKey = o => ((SegmentDto)o).isEnable,
                    Cell = o => ((SegmentDto)o).isEnable
                        ? new Chip("enabled", ChipTone.Ok)
                        : new Chip("disabled", ChipTone.Neutral),
                },
                new DataColumn
                {
                    Header = "UPDATED", FixedWidth = true, Px = 112, Align = "right",
                    SortKey = o => ((SegmentDto)o).updatedDate,
                    Cell = o => new Label(Fmt.Date(((SegmentDto)o).updatedDate)),
                },
            };
        }

        private static VisualElement NameCell(object row)
        {
            var segment = (SegmentDto)row;

            var box = new VisualElement();
            var name = new Label(Label(segment));
            name.enableRichText = false;
            name.AddToClassList("sc-seg-name");
            box.Add(name);

            if (!string.IsNullOrEmpty(segment.description))
            {
                var description = new Label(Fmt.Truncate(segment.description, 84));
                description.enableRichText = false;
                description.AddToClassList("sc-seg-desc");
                box.Add(description);
            }
            return box;
        }

        private void ShowSegment(SegmentDto segment)
        {
            if (Popup == null || segment == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(IsMine(segment)
                ? new Chip("this player is in it", ChipTone.Accent)
                : new Chip("this player is out", ChipTone.Neutral));
            chips.Add(segment.isEnable
                ? new Chip("enabled", ChipTone.Ok)
                : new Chip("disabled", ChipTone.Neutral));
            body.Add(chips);

            if (!string.IsNullOrEmpty(segment.description))
            {
                var description = new Label(segment.description);
                description.enableRichText = false;
                description.AddToClassList("sc-fs-hint");
                body.Add(description);
            }

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Name", Fmt.OrDash(segment.name), segment.name));
            kv.Add(Kv("Segment id", Fmt.OrDash(segment.id), segment.id));
            kv.Add(Kv("Rule tree id", Fmt.OrDash(segment.ruleTreeId), segment.ruleTreeId));
            kv.Add(Kv("Created", Fmt.DateTime2(segment.createdDate), null));
            kv.Add(Kv("Updated", Fmt.DateTime2(segment.updatedDate), null));
            body.Add(kv);

            var note = new Label("The rule tree is authored in the console and never leaves the server: "
                + "the SDK receives its id, not its conditions. A disabled segment is skipped entirely when "
                + "membership is computed.");
            note.AddToClassList("sc-fs-hint");
            note.AddToClassList("sc-seg-note");
            body.Add(note);

            Popup.Open(body, Fmt.Truncate(Label(segment), 34));
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

        // ----- where the writes live ----------------------------------------------------------

        /// <summary>
        /// The one thing a reader is most likely to look for here and not find: how to put a player
        /// into a segment. It is a Player Account call, so the section says so and shows the code
        /// rather than leaving the screen looking incomplete.
        /// </summary>
        private VisualElement WritesSection()
        {
            var box = new VisualElement();
            box.AddToClassList("sc-seg-block");
            box.Add(new SectionHeader("Changing a player's segments"));

            var card = new Card(Meta.Accent);
            card.WithTitle("Written through Player Account", Meta.Accent);

            var text = new Label("The Segments service only reads. Membership is stored on the account, "
                + "so the two calls that change it — UpdateSegmentsAsync for the account and "
                + "UpdateProfileSegmentsAsync for one of its profiles — belong to PlayerAccountService. "
                + "Open the Player Account screen from the services list to see the account and the "
                + "profiles they write to.");
            text.AddToClassList("sc-fs-hint");
            card.Body.Add(text);

            card.Body.Add(SdkCallDrawer.CodeBlock(WriteSnippet));

            var caveat = new Label("Both calls overwrite the whole list, and neither re-runs the rule "
                + "trees: a value written this way stays until the next sign-in recomputes membership.");
            caveat.AddToClassList("sc-fs-hint");
            caveat.AddToClassList("sc-seg-note");
            card.Body.Add(caveat);

            box.Add(card);
            return box;
        }

        // ----- membership ---------------------------------------------------------------------

        private void SyncMine()
        {
            _mine.Clear();
            var keys = _account != null ? _account.SegmentKeys : null;
            if (keys == null)
            {
                return;
            }
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _mine.Add(key.Trim());
                }
            }
        }

        private bool IsMine(SegmentDto segment)
        {
            if (segment == null || _mine.Count == 0)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(segment.name) && _mine.Contains(segment.name))
            {
                return true;
            }
            return !string.IsNullOrEmpty(segment.id) && _mine.Contains(segment.id);
        }

        private int MineCount()
        {
            if (_segments == null)
            {
                return 0;
            }
            int count = 0;
            foreach (var s in _segments)
            {
                if (IsMine(s))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Account keys that match nothing in the branch — worth surfacing rather than
        /// silently dropping, because they are the one case where the two lists disagree.</summary>
        private List<string> StrayKeys()
        {
            var stray = new List<string>();
            if (_segments == null || _mine.Count == 0)
            {
                return stray;
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in _segments)
            {
                if (s == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(s.name))
                {
                    known.Add(s.name);
                }
                if (!string.IsNullOrEmpty(s.id))
                {
                    known.Add(s.id);
                }
            }

            foreach (var key in _mine)
            {
                if (!known.Contains(key))
                {
                    stray.Add(key);
                }
            }
            return stray;
        }

        // A segment with no name falls back to its id, and a segment with neither is at least printed
        // as a dash instead of an empty cell.
        private static string Label(SegmentDto segment)
        {
            if (segment == null)
            {
                return Fmt.Dash;
            }
            return !string.IsNullOrWhiteSpace(segment.name) ? segment.name : Fmt.Id(segment.id, 12);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Challenges.Dto;
using Plugins.MirraCloud.Core.Services.Challenges.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Challenges detail: one tab per configured challenge, because a challenge is self-contained —
    /// its rules, the player's entry, the ranking around it and the four writes that change it all
    /// belong together.
    /// <para>
    /// A challenge is opt-in, which drives every empty state on this screen: the server refuses a
    /// score from a player who never called <c>JoinAsync</c>, and the entry itself only exists after
    /// the first <c>SubmitScoreAsync</c>. So "nothing here" always says which of the two steps is
    /// missing instead of shrugging.
    /// </para>
    /// <para>
    /// The toolbar's slice picker swaps which ranking endpoint a tab asks for. A write reloads the
    /// tab's own sections rather than rebuilding the tab, so the response the action just produced
    /// stays on screen next to the data it changed.
    /// </para>
    /// </summary>
    public sealed class ChallengesView : ServiceView
    {
        private const int TopCount = 100;
        private const int AroundRange = 10;

        /// <summary>Bars past this point are too thin to read; the table below carries the rest.</summary>
        private const int ChartBars = 8;

        /// <summary>How often the confirmation gate checks whether the dialog was dismissed.</summary>
        private const long ConfirmPollMs = 120;

        // Reward kinds are the backend's EconomyResourceKind numbers (1/2/5), not the SDK's own
        // Economy enum (0/1/2) — the challenge DTO carries the raw server value as an int.
        private const int KindCurrency = 1;
        private const int KindItem = 2;
        private const int KindEnergy = 5;

        // Index-aligned with the Slice enum — the dropdown hands back the label, not the value.
        private static readonly string[] SliceNames = { "Global top", "My cohort", "Around me" };

        private const string ConfigsSnippet = @"// Every challenge configured for this project + branch. Call it once at startup: every other
// challenge call takes the config's business key.
var op = sdk.Challenges.InitializeAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (ChallengeConfigDto cfg in op.Result.Data)
{
    Debug.Log(cfg.key + "" → "" + cfg.targetValue + "" ("" + cfg.orderType + "")"");
}
// the service keeps them as entities too: sdk.Challenges.ChallengeConfigs";

        private const string JoinSnippet = @"// Opt-in. Without this the server answers SubmitScoreAsync with a participation error.
var join = sdk.Challenges.JoinAsync(challengeKey);
await join.Task();

// Leaving is not just a flag: it deletes the player's entry for the current run.
var leave = sdk.Challenges.LeaveAsync(challengeKey);
await leave.Task();";

        private const string SubmitSnippet = @"// One score per player per run. The config's updateStrategy decides what happens to the
// previous one: Best keeps it, Latest replaces it, Total adds up.
var op = sdk.Challenges.SubmitScoreAsync(challengeKey, 47.0, ""Hero42"");
await op.Task();

if (op.Result.IsSuccess)
{
    SubmitScoreResponseDto r = op.Result.Data;
    // r.value, r.isFinished, r.finishPosition, r.rewardGranted
}
// playerName is optional — the SDK falls back to the signed-in nickname, and the server
// refuses an empty one.";

        private const string ClaimSnippet = @"// Only for rewardMode = AllFinishers. With ByFinishOrder the server pays out during the
// SubmitScoreAsync that finished the challenge and refuses this call.
var op = sdk.Challenges.ClaimRewardAsync(challengeKey);
await op.Task();

if (op.Result.IsSuccess && op.Result.Data.rewardGranted)
{
    // the tier that was paid is the one whose valueMin..valueMax covers finishPosition
}
// Grants are recorded per run, so claiming twice does not pay twice.";

        private const string TopSnippet = @"// The whole ranking, best entries first.
var op = sdk.Challenges.GetTopAsync(challengeKey, entriesCount: 100);
await op.Task();

foreach (ChallengeEntryDto e in op.Result.Data.entries)
{
    Debug.Log(e.position + "". "" + e.playerName + "" = "" + e.value);
}";

        private const string CohortSnippet = @"// The top inside the cohort this player was assigned to. With cohorts switched off for the
// challenge the server answers with the global top instead.
var op = sdk.Challenges.GetMyTopAsync(challengeKey, entriesCount: 100);
await op.Task();";

        private const string AroundSnippet = @"// The player's neighbourhood: entriesRange rows above and below their own.
var op = sdk.Challenges.GetAroundPlayerAsync(challengeKey, entriesRange: 10);
await op.Task();

// one flat, already ordered list — unlike the leaderboard's above/target/below split
ChallengeEntryDto[] rows = op.Result.Data.entries;";

        private const string MeSnippet = @"// The player's own entry. 404 while there is none for the current run — which is the case
// both before JoinAsync and after joining but before the first score.
var op = sdk.Challenges.GetPlayerAsync(challengeKey);
await op.Task();

if (op.Result.IsSuccess)
{
    ChallengeEntryDto me = op.Result.Data;
    Debug.Log(""#"" + me.position + "" with "" + me.value + (me.isFinished ? "" (finished)"" : """"));
}";

        private Slice _slice = Slice.Top;
        private Tabs _tabs;

        public ChallengesView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        /// <summary>Which ranking endpoint a tab asks for. Order matches <see cref="SliceNames"/>.</summary>
        private enum Slice
        {
            Top,
            Cohort,
            Around,
        }

        protected override void Populate()
        {
            _tabs = null;
            SetStatus(null);
            SetSubtitle("One tab per configured challenge: the goal and its window, your progress "
                        + "towards it, the ranking, and the join / submit / claim / leave calls that "
                        + "drive all of it.");

            UseToolbar()
                .WithFilter("Slice", SliceNames, OnSliceChanged, SliceNames[(int)_slice])
                .WithSpacer()
                .WithRefresh(Refresh);

            DeclareCall(new SdkCall("List challenges", ConfigsSnippet,
                "Call it once: every other challenge call needs a key from here."));
            DeclareCall(new SdkCall("Join and leave", JoinSnippet,
                "Leaving deletes the entry, it does not merely stop tracking it."));
            DeclareCall(new SdkCall("Submit a score", SubmitSnippet));
            DeclareCall(new SdkCall("Claim the reward", ClaimSnippet,
                "Refused unless the challenge pays all finishers."));
            DeclareCall(new SdkCall("The global top", TopSnippet));
            DeclareCall(new SdkCall("The top inside my cohort", CohortSnippet));
            DeclareCall(new SdkCall("Entries around the player", AroundSnippet));
            DeclareCall(new SdkCall("The player's own entry", MeSnippet,
                "A 404 here means \"no entry yet\", not a broken service."));

            // Zero margin: this slot only carries the loading/failure state — on success the
            // challenges land in the tab strip (chrome) and the panes at the bottom of Content.
            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Challenges.InitializeAsync(),
                slot,
                BuildTabs,
                isEmpty: c => c == null || c.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Challenge configs",
                    Snippet = ConfigsSnippet,
                    ServiceName = "Challenge",
                    // the challenge *configuration* call, so a 404 really does mean
                    // "no challenges exist in this project"
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoChallenges,
                });
        }

        private VisualElement BuildTabs(ChallengeConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 challenge" : configs.Length + " challenges", ChipTone.Ok);

            _tabs = UseTabs();
            foreach (var cfg in configs)
            {
                var captured = cfg;
                _tabs.Add(TitleOf(captured), LucideIcon.Target, () => BuildPane(captured));
            }

            // The strip and its panes live outside this slot, so the slot itself renders nothing.
            return new VisualElement();
        }

        private VisualElement NoChallenges()
        {
            SetStatus("Not configured", ChipTone.Warn);
            return ZeroState.NotConfigured("Challenges");
        }

        private void OnSliceChanged(string name)
        {
            int index = Array.IndexOf(SliceNames, name);
            if (index < 0 || (Slice)index == _slice)
            {
                return;
            }
            _slice = (Slice)index;

            // Panes cache their data, so the visible one has to be thrown away for the new endpoint
            // to be called; the hidden ones rebuild when they are selected again.
            if (_tabs != null)
            {
                _tabs.InvalidateAll();
            }
        }

        // ----- one challenge ----------------------------------------------------------------------

        private VisualElement BuildPane(ChallengeConfigDto cfg)
        {
            var pane = new ChallengePane(cfg);

            var root = new VisualElement();
            root.Add(ConfigCard(cfg));

            pane.Kpis.AddToClassList("sc-chal-kpis");
            root.Add(pane.Kpis);
            RenderKpis(pane);

            root.Add(new SectionHeader("My progress"));
            pane.MineSlot = new VisualElement();
            pane.MineSlot.AddToClassList("sc-chal-block");
            root.Add(pane.MineSlot);
            LoadMine(pane, pane.MineSlot);

            pane.EntriesHeader = new SectionHeader(SliceTitle());
            root.Add(pane.EntriesHeader);

            var note = new Label(SliceNote(cfg));
            note.enableRichText = false;
            note.AddToClassList("sc-fs-hint");
            root.Add(note);

            pane.EntriesSlot = new VisualElement();
            pane.EntriesSlot.AddToClassList("sc-chal-block");
            root.Add(pane.EntriesSlot);
            LoadSlice(pane, pane.EntriesSlot);

            root.Add(RewardsSection(cfg));
            root.Add(ActionsSection(pane));
            return root;
        }

        private VisualElement ConfigCard(ChallengeConfigDto cfg)
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-chal-block");
            card.WithTitle(TitleOf(cfg), Meta.Accent);

            var goal = new Label(GoalSentence(cfg));
            goal.enableRichText = false;
            goal.AddToClassList("sc-chal-goal");
            card.Body.Add(goal);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(cfg.orderType == OrderType.Highest ? "highest wins" : "lowest wins",
                ChipTone.Info));
            chips.Add(new Chip(StrategyChip(cfg.updateStrategy), ChipTone.Neutral));
            chips.Add(new Chip(cfg.rewardMode == RewardMode.AllFinishers
                ? "rewards every finisher"
                : "rewards by finish order", ChipTone.Accent));
            if (cfg.cohortsEnabled)
            {
                chips.Add(new Chip("cohorts of " + cfg.cohortSize, ChipTone.Neutral));
            }
            if (cfg.finishersToEnd.HasValue)
            {
                chips.Add(new Chip("ends at " + cfg.finishersToEnd.Value + " finishers", ChipTone.Warn));
            }
            if (cfg.isReset)
            {
                chips.Add(new Chip("resets every " + ResetEvery(cfg), ChipTone.Warn));
                if (cfg.nextResetDate.HasValue)
                {
                    var countdown = new CountdownChip(cfg.nextResetDate.Value.ToUniversalTime());
                    countdown.tooltip = "Next reset: " + Fmt.DateTime2(cfg.nextResetDate);
                    chips.Add(countdown);
                }
            }
            else
            {
                chips.Add(new Chip("never resets", ChipTone.Neutral));
            }
            card.Body.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Key", Fmt.OrDash(cfg.key), cfg.key));
            kv.Add(Kv("Goal", Fmt.Number(cfg.targetValue), null));
            kv.Add(Kv("Current run", WindowText(cfg), null));
            if (cfg.duration > 0L)
            {
                kv.Add(Kv("Planned length", Fmt.Duration(TimeSpan.FromMilliseconds(cfg.duration)), null));
            }
            kv.Add(Kv("Config updated", Fmt.DateTime2(cfg.updatedDate), null));
            card.Body.Add(kv);
            return card;
        }

        /// <summary>
        /// Redraws the KPI strip from whatever the pane knows so far. Called once per arriving
        /// response, because the player's entry and the ranking are two separate requests.
        /// </summary>
        private void RenderKpis(ChallengePane pane)
        {
            var kpis = pane.Kpis.Clear2();
            var cfg = pane.Config;

            if (pane.Me != null)
            {
                kpis.Add("My score", LucideIcon.Target, Fmt.Number(pane.Me.value), null, pane.Me.isFinished);
                if (pane.Me.position > 0)
                {
                    kpis.Add("My rank", LucideIcon.Medal, "#" + pane.Me.position, null, pane.Me.position <= 3);
                }
                else
                {
                    kpis.AddZero("My rank", LucideIcon.Medal, "unranked");
                }
            }
            else if (pane.MeLoaded)
            {
                kpis.AddZero("My score", LucideIcon.Target, "no entry");
                kpis.AddZero("My rank", LucideIcon.Medal, Fmt.Dash);
            }
            else
            {
                kpis.Add("My score", LucideIcon.Target, Fmt.Dash);
                kpis.Add("My rank", LucideIcon.Medal, Fmt.Dash);
            }

            kpis.Add("Goal", LucideIcon.Flag, Fmt.Number(cfg.targetValue));

            string caption = EntriesCaption();
            if (!pane.EntriesLoaded)
            {
                kpis.Add(caption, LucideIcon.Users, Fmt.Dash);
            }
            else if (pane.Entries == 0)
            {
                kpis.AddZero(caption, LucideIcon.Users);
            }
            else
            {
                kpis.Add(caption, LucideIcon.Users, Fmt.Number(pane.Entries));
            }
        }

        // ----- the player's own entry ---------------------------------------------------------

        /// <summary>
        /// Loads the player's entry by hand rather than through <see cref="ViewBind"/>: a 404 here is
        /// the normal "no entry yet" state of an opt-in challenge, and it needs a panel that explains
        /// joining rather than the generic failure state.
        /// </summary>
        private async void LoadMine(ChallengePane pane, VisualElement slot)
        {
            Skeleton.Into(slot, 2);

            RestApiResult<ChallengeEntryDto> result = null;
            try
            {
                var op = Sdk.Challenges.GetPlayerAsync(pane.Config.key);
                if (op != null)
                {
                    await op.Task();
                    result = op.Result;
                }
            }
            catch (Exception e)
            {
                // async void: an exception escaping here would surface as an unhandled one rather
                // than as a failed panel, so it is logged and the panel falls back to the error state.
                Debug.LogWarning("[Showcase] Challenges: reading the player's entry failed: " + e.Message);
            }

            if (result != null && Ctx.Log != null)
            {
                Ctx.Log.Record("Challenges: my entry", result, MeSnippet);
            }

            pane.Me = result != null && result.IsSuccess ? result.Data : null;
            pane.MeLoaded = true;
            RenderKpis(pane);
            Replace(slot, pane.Me != null ? MineBody(pane) : MineZero(pane, result));

            // The table may have rendered before this landed, and it is the "You" row highlight that
            // depends on it — re-bind rather than leave the player unable to find themselves.
            if (pane.Me != null && pane.Table != null)
            {
                pane.Table.Bind(pane.Rows, pane.IsMine);
            }
        }

        private VisualElement MineBody(ChallengePane pane)
        {
            var cfg = pane.Config;
            var me = pane.Me;

            var box = new VisualElement();

            float ratio = Progress(cfg, me.value);
            var bar = new ProgressBar();
            bar.AddToClassList("sc-chal-progress");
            // Set(ratio, 1) rather than Set(value, target): a lowest-wins challenge counts *down*
            // to its goal, so the raw value/target fraction would fill the bar backwards.
            bar.Set(ratio, 1f);
            bar.SetLabel(Fmt.Number(me.value) + " / " + Fmt.Number(cfg.targetValue)
                + "  ·  " + Fmt.Percent(ratio));
            bar.SetAccent(me.isFinished ? ShowcaseTheme.Ok : Meta.Accent);
            box.Add(bar);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(me.isFinished ? "goal reached" : "in progress",
                me.isFinished ? ChipTone.Ok : ChipTone.Info));
            chips.Add(me.position > 0
                ? new Chip("rank #" + me.position, ChipTone.Accent)
                : new Chip("unranked", ChipTone.Neutral));
            if (me.finishPosition.HasValue)
            {
                chips.Add(new Chip("finisher #" + me.finishPosition.Value, ChipTone.Ok));
            }
            if (me.finishedAt.HasValue)
            {
                chips.Add(new Chip("finished " + Fmt.DateTime2(me.finishedAt), ChipTone.Neutral));
            }
            box.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Shown as", Fmt.OrDash(me.playerName), null));
            kv.Add(Kv("Profile id", Fmt.Id(me.playerId, 14), me.playerId));
            box.Add(kv);

            if (me.isFinished && cfg.rewardMode == RewardMode.AllFinishers)
            {
                var hint = new Label("You reached the goal, and this challenge pays every finisher: "
                    + "ClaimRewardAsync under Actions hands the tier over. The read has no \"already "
                    + "claimed\" flag, but the grant is recorded per run, so claiming twice pays once.");
                hint.enableRichText = false;
                hint.AddToClassList("sc-fs-hint");
                hint.AddToClassList("sc-chal-claim-hint");
                box.Add(hint);
            }
            return box;
        }

        private VisualElement MineZero(ChallengePane pane, RestApiResult<ChallengeEntryDto> result)
        {
            long? code = result == null
                ? null
                : result.HttpStatusCode ?? (result.Error != null ? result.Error.HttpStatusCode : null);

            // A success with no body and a 404 both mean "no entry for the current run"; anything
            // else is a real failure and must not be dressed up as an empty state.
            bool noEntry = result != null && (result.IsSuccess || code == 404L);
            if (!noEntry)
            {
                return ErrorState.Build(result != null ? result.Error : null);
            }

            if (pane.Joined)
            {
                return ZeroState.Panel(LucideIcon.Target, "No score submitted yet",
                    "You joined this run in this session. The entry — score, rank, finish position — "
                    + "is created by the first SubmitScoreAsync, so submit one under Actions and this "
                    + "panel turns into your progress bar.",
                    null, null,
                    "GetPlayerAsync answers 404 until that first score lands.");
            }

            return ZeroState.Panel(LucideIcon.Target, "You are not in this challenge",
                "Challenges are opt-in: JoinAsync first, then SubmitScoreAsync. Scores sent before "
                + "joining are refused, and nothing about this player is tracked until they are.",
                "Join this challenge",
                () => JoinNow(pane),
                "There is no endpoint that reads participation, so a joined player with no score sees "
                + "this same 404 until they submit one.");
        }

        // ----- the ranking ----------------------------------------------------------------------

        private void LoadSlice(ChallengePane pane, VisualElement slot)
        {
            var cfg = pane.Config;
            string key = cfg.key;

            switch (_slice)
            {
                case Slice.Cohort:
                    BindSlice(pane, slot,
                        () => Sdk.Challenges.GetMyTopAsync(key, TopCount),
                        "Challenge cohort top", CohortSnippet,
                        cfg.cohortsEnabled
                            ? "Nobody in your cohort has scored yet. A cohort is filled as players "
                              + "join, and you are assigned to one on your first score."
                            : "Cohorts are off for this challenge, so this endpoint answers with the "
                              + "global top — and that has no entries either.",
                        false);
                    return;

                case Slice.Around:
                    BindSlice(pane, slot,
                        () => Sdk.Challenges.GetAroundPlayerAsync(key, AroundRange),
                        "Challenge entries around me", AroundSnippet,
                        "Your neighbours show up once you have an entry of your own: join the "
                        + "challenge and submit a score.",
                        true);
                    return;

                default:
                    BindSlice(pane, slot,
                        () => Sdk.Challenges.GetTopAsync(key, TopCount),
                        "Challenge top", TopSnippet,
                        "No score has been submitted to this challenge yet. The first "
                        + "SubmitScoreAsync from a joined player creates the ranking, and every "
                        + "later score updates it.",
                        true);
                    return;
            }
        }

        /// <summary>Shared binding for every slice: same rows, same table, a different endpoint.</summary>
        private void BindSlice(ChallengePane pane, VisualElement slot,
            Func<AsyncOperation<RestApiResult<ChallengeEntriesDto>>> start,
            string label, string snippet, string emptyMessage, bool offerJoin)
        {
            ViewBind.Load(
                start,
                slot,
                data => BuildEntries(pane, data != null ? data.entries : null),
                isEmpty: data => data == null || data.entries == null || data.entries.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = label,
                    Snippet = snippet,
                    ServiceName = "Challenge",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane, emptyMessage, offerJoin),
                });
        }

        private VisualElement BuildEntries(ChallengePane pane, ChallengeEntryDto[] entries)
        {
            var rows = Compact(entries);
            pane.Rows = rows;
            pane.Entries = rows.Length;
            pane.EntriesLoaded = true;
            RenderKpis(pane);
            if (pane.EntriesHeader != null)
            {
                pane.EntriesHeader.SetCount(rows.Length.ToString());
            }

            int finished = 0;
            foreach (var e in rows)
            {
                if (e.isFinished)
                {
                    finished++;
                }
            }

            var root = new VisualElement();

            var summary = new Label(finished + " of " + rows.Length + " shown "
                + (finished == 1 ? "entry has" : "entries have") + " reached the goal.");
            summary.enableRichText = false;
            summary.AddToClassList("sc-fs-hint");
            root.Add(summary);

            var chart = BuildChart(rows);
            if (chart != null)
            {
                root.Add(chart);
            }

            pane.Table = new DataTable(Columns(pane))
                .WithZebra()
                .WithMaxHeight(420f)
                .WithSort(0, true)
                .Bind(rows, pane.IsMine);
            root.Add(pane.Table);
            return root;
        }

        private VisualElement EmptySlice(ChallengePane pane, string message, bool offerJoin)
        {
            pane.Rows = Array.Empty<ChallengeEntryDto>();
            pane.Entries = 0;
            pane.EntriesLoaded = true;
            pane.Table = null;
            RenderKpis(pane);
            if (pane.EntriesHeader != null)
            {
                pane.EntriesHeader.SetCount("0");
            }

            // The board keeps the shape it will have once scores arrive — the reader sees the
            // columns they are going to get, not a shrug.
            bool cta = offerJoin && pane.MeLoaded && pane.Me == null;
            return ZeroState.Table(Columns(pane), message, 3,
                cta ? "Join this challenge" : null,
                cta ? (Action)(() => JoinNow(pane)) : null);
        }

        /// <summary>The head of the ranking as bars — the shape of the gap between the leaders, which
        /// a column of numbers hides. Finished entries are tinted, so the cut-off is visible.</summary>
        private VisualElement BuildChart(ChallengeEntryDto[] rows)
        {
            if (rows.Length < 2)
            {
                return null;
            }

            var ordered = new List<ChallengeEntryDto>(rows);
            ordered.Sort((a, b) => a.position.CompareTo(b.position));

            int count = Math.Min(ChartBars, ordered.Count);
            var points = new List<ChartPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var e = ordered[i];
                points.Add(new ChartPoint(e.position > 0 ? "#" + e.position : Fmt.Dash, (float)e.value,
                    e.isFinished ? ShowcaseTheme.Ok : (Color?)null));
            }

            var chart = new BarChart(150f);
            chart.AddToClassList("sc-chal-chart");
            chart.SetAccent(Meta.Accent);
            chart.SetValueFormatter(v => Fmt.Number(v));
            chart.SetData(points);
            return chart;
        }

        private DataColumn[] Columns(ChallengePane pane)
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "#", FixedWidth = true, Px = 64, Align = "center",
                    SortKey = row => ((ChallengeEntryDto)row).position,
                    Cell = RankCell,
                },
                new DataColumn
                {
                    Header = "PLAYER", Grow = 1f,
                    SortKey = row => PlayerLabel((ChallengeEntryDto)row),
                    Cell = row => PlayerCell(pane, row),
                },
                new DataColumn
                {
                    Header = "SCORE", FixedWidth = true, Px = 110, Align = "right",
                    SortKey = row => ((ChallengeEntryDto)row).value,
                    Cell = ScoreCell,
                },
                new DataColumn
                {
                    Header = "STATUS", FixedWidth = true, Px = 132, Align = "right",
                    // finished first when ascending: the cut-off is what the column is about
                    SortKey = row => ((ChallengeEntryDto)row).isFinished ? 0 : 1,
                    Cell = StatusCell,
                },
            };
        }

        private static VisualElement RankCell(object row)
        {
            var e = (ChallengeEntryDto)row;
            var text = new Label(e.position > 0 ? "#" + e.position : Fmt.Dash);
            text.AddToClassList("sc-rank");
            return text;
        }

        private static VisualElement PlayerCell(ChallengePane pane, object row)
        {
            var e = (ChallengeEntryDto)row;
            string label = PlayerLabel(e);

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-chal-player");

            var avatar = new Avatar(26f).SetInitialsFor(label);
            avatar.AddToClassList("sc-chal-player__avatar");
            wrap.Add(avatar);

            var name = new Label(Fmt.OrDash(label));
            name.enableRichText = false;
            name.tooltip = e.playerId;
            wrap.Add(name);

            if (pane.IsMine(row))
            {
                var you = new Badge("You", ChipTone.Accent);
                you.AddToClassList("sc-chal-player__you");
                wrap.Add(you);
            }
            return wrap;
        }

        private static VisualElement ScoreCell(object row)
        {
            var e = (ChallengeEntryDto)row;
            var label = new Label(Fmt.Number(e.value));
            label.AddToClassList("sc-score");
            // Fmt.Number compacts past 10k ("12.4k"), and on a ranking the exact figure is what
            // decides the order — keep it one hover away.
            label.tooltip = e.value.ToString("R", CultureInfo.InvariantCulture);
            return label;
        }

        private static VisualElement StatusCell(object row)
        {
            var e = (ChallengeEntryDto)row;
            if (!e.isFinished)
            {
                return new Badge("in progress", ChipTone.Neutral);
            }

            var badge = new Badge(e.finishPosition.HasValue
                ? "finisher #" + e.finishPosition.Value
                : "finished", ChipTone.Ok);
            if (e.finishedAt.HasValue)
            {
                badge.tooltip = "Finished " + Fmt.DateTime2(e.finishedAt);
            }
            return badge;
        }

        // ----- rewards ----------------------------------------------------------------------------

        private VisualElement RewardsSection(ChallengeConfigDto cfg)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-chal-block");
            box.Add(new SectionHeader("Rewards",
                cfg.rewardMode == RewardMode.AllFinishers ? "all finishers" : "by finish order"));

            box.Add(new InfoHint(cfg.rewardMode == RewardMode.AllFinishers
                ? "Every player who reaches the goal is owed the tier their finish position falls in, "
                  + "and has to ask for it: ClaimRewardAsync pays it out."
                : "The server grants the matching tier inside the SubmitScoreAsync that finishes the "
                  + "challenge. Nothing to claim — and ClaimRewardAsync is refused in this mode."));

            box.Add(TierList("Finisher tiers", cfg.rewardsForFinishers,
                "Ranges are finish positions: the first player to reach the goal is #1.",
                "No finisher tiers are configured, so reaching the goal grants nothing. Tiers are "
                + "authored per challenge in the Mirra Hub console."));

            box.Add(TierList("Non-finisher tiers", cfg.rewardsForNonFinishers,
                "Paid out when the run ends, ranked among the players who never reached the goal.",
                "Nothing is set aside for players who miss the goal — an optional consolation tier "
                + "that this challenge does not use."));
            return box;
        }

        private VisualElement TierList(string title, RewardRangeDto[] ranges, string note, string emptyMessage)
        {
            var box = new VisualElement();
            box.Add(new SectionHeader(title, ranges != null ? ranges.Length.ToString() : "0"));

            if (ranges == null || ranges.Length == 0)
            {
                box.Add(ZeroState.Panel(LucideIcon.Gift, "No " + title.ToLowerInvariant(), emptyMessage));
                return box;
            }

            var hint = new Label(note);
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            box.Add(hint);

            foreach (var range in ranges)
            {
                if (range == null)
                {
                    continue;
                }

                var row = new ListRow();
                row.SetTitle(RangeText(range));
                row.SetSubtitle(RewardCount(range));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-row-actions");
                if (range.rewards != null)
                {
                    foreach (var reward in range.rewards)
                    {
                        if (reward != null)
                        {
                            trailing.Add(RewardPill(reward));
                        }
                    }
                }
                row.SetTrailing(trailing);
                box.Add(row);
            }
            return box;
        }

        private VisualElement RewardPill(RewardDataDto reward)
        {
            var chip = new RewardChip(RewardGlyph(reward.economyResourceKind), "×" + reward.count, Meta.Accent);
            chip.tooltip = KindName(reward.economyResourceKind) + " " + Fmt.OrDash(reward.rewardId);
            return chip;
        }

        private static string RangeText(RewardRangeDto range)
        {
            string min = range.valueMin.ToString("0", CultureInfo.InvariantCulture);
            string max = range.valueMax.ToString("0", CultureInfo.InvariantCulture);
            return range.valueMin == range.valueMax ? "#" + min : "#" + min + "–#" + max;
        }

        private static string RewardCount(RewardRangeDto range)
        {
            int count = range.rewards != null ? range.rewards.Length : 0;
            if (count == 0)
            {
                return "no payload — an empty tier grants nothing";
            }
            return count == 1 ? "1 reward" : count + " rewards";
        }

        private static string RewardGlyph(int kind)
        {
            switch (kind)
            {
                case KindCurrency: return LucideIcon.Coins;
                case KindItem: return LucideIcon.Package;
                case KindEnergy: return LucideIcon.Zap;
                default: return LucideIcon.Gem;
            }
        }

        private static string KindName(int kind)
        {
            switch (kind)
            {
                case KindCurrency: return "currency";
                case KindItem: return "item";
                case KindEnergy: return "energy";
                default: return "reward";
            }
        }

        // ----- actions ------------------------------------------------------------------------

        private VisualElement ActionsSection(ChallengePane pane)
        {
            var cfg = pane.Config;

            var box = new VisualElement();
            box.AddToClassList("sc-chal-block");
            box.Add(new SectionHeader("Actions"));

            var hint = new Label("Every write a challenge exposes, in the order a game calls them. "
                + "Each one reloads the sections above, so the effect shows up on this tab without "
                + "losing the response you are reading.");
            hint.AddToClassList("sc-fs-hint");
            box.Add(hint);

            box.Add(new ActionCard("Join the challenge",
                    "Opt-in. Until this succeeds every score for this challenge is refused.",
                    LucideIcon.DoorOpen)
                .WithSnippet(JoinSnippet)
                .OnRun("Join", _ => JoinAction(pane)));

            string nickname = Nickname();
            box.Add(new ActionCard("Submit a score", SubmitDescription(cfg), LucideIcon.Send)
                .WithFields(
                    FormField.Float("score", "Score", 1f),
                    FormField.Text("playerName", "Player name", nickname,
                        string.IsNullOrWhiteSpace(nickname)))
                .WithSnippet(SubmitSnippet)
                .OnRun("Submit", v => SubmitAction(pane, v)));

            box.Add(new ActionCard("Claim the reward", ClaimDescription(cfg), LucideIcon.Gift)
                .WithSnippet(ClaimSnippet)
                .OnRun("Claim", _ => ClaimAction(pane)));
            if (cfg.rewardMode != RewardMode.AllFinishers)
            {
                box.Add(new InfoHint("Running it on this challenge is expected to fail: it pays by "
                    + "finish order, and that reward is granted by the score that finishes the run."));
            }

            box.Add(new ActionCard("Leave the challenge",
                    "Clears participation and deletes the entry for the current run — the score, the "
                    + "finish position and the place in the ranking go with it.",
                    LucideIcon.LogOut)
                .WithSnippet(JoinSnippet)
                .OnRun("Leave", _ => LeaveAction(pane), true));

            return box;
        }

        private async Task<ActionOutcome> JoinAction(ChallengePane pane)
        {
            var outcome = await Await(Sdk.Challenges.JoinAsync(pane.Config.key), "Challenges · join");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            pane.Joined = true;
            if (Toasts != null)
            {
                Toasts.Ok("Joined " + TitleOf(pane.Config));
            }
            ReloadPane(pane);
            return ActionOutcome.Success("Joined — submit a score to create the entry");
        }

        private async Task<ActionOutcome> SubmitAction(ChallengePane pane, FormValues values)
        {
            var op = Sdk.Challenges.SubmitScoreAsync(pane.Config.key, values.Float("score"),
                Trimmed(values.Text("playerName")));
            var outcome = await AwaitData(op, "Challenges · submit score");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var response = op.Result.Data;
            if (Toasts != null)
            {
                Toasts.Ok(response != null && response.isFinished ? "Goal reached" : "Score submitted");
            }
            ReloadPane(pane);
            return ActionOutcome.Success(SubmitMessage(response), ResponseChips(pane.Config, response));
        }

        private async Task<ActionOutcome> ClaimAction(ChallengePane pane)
        {
            var op = Sdk.Challenges.ClaimRewardAsync(pane.Config.key);
            var outcome = await AwaitData(op, "Challenges · claim reward");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var response = op.Result.Data;
            bool granted = response != null && response.rewardGranted;
            if (Toasts != null)
            {
                Toasts.Ok(granted ? "Reward granted" : "Claim accepted, nothing to grant");
            }
            ReloadPane(pane);
            return ActionOutcome.Success(
                granted
                    ? "The server granted the tier for this finish position"
                    : "The claim went through, but no tier matched — nothing was granted",
                ResponseChips(pane.Config, response));
        }

        private async Task<ActionOutcome> LeaveAction(ChallengePane pane)
        {
            bool confirmed = await ConfirmAsync("Leave the challenge",
                "This clears your participation and deletes your entry for the current run. The "
                + "score, the finish position and the place in the ranking are gone; joining again "
                + "starts from zero.",
                "Leave");
            if (!confirmed)
            {
                return ActionOutcome.Failure("Cancelled — nothing was sent.");
            }

            var outcome = await Await(Sdk.Challenges.LeaveAsync(pane.Config.key), "Challenges · leave");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            pane.Joined = false;
            if (Toasts != null)
            {
                Toasts.Ok("Left " + TitleOf(pane.Config));
            }
            ReloadPane(pane);
            return ActionOutcome.Success("Left the challenge and dropped the entry");
        }

        /// <summary>Join from an empty state, where there is no action card to report into.</summary>
        private async void JoinNow(ChallengePane pane)
        {
            var outcome = await Await(Sdk.Challenges.JoinAsync(pane.Config.key), "Challenges · join");
            if (!outcome.Ok)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Join failed · " + outcome.Message);
                }
                return;
            }

            pane.Joined = true;
            if (Toasts != null)
            {
                Toasts.Ok("Joined " + TitleOf(pane.Config));
            }
            ReloadPane(pane);
        }

        /// <summary>
        /// Re-runs the tab's two reads in place. Deliberately not <c>Tabs.Invalidate</c>: that would
        /// throw away the action card together with the response it is showing, which is the one
        /// thing the reader wants to compare against the refreshed data.
        /// </summary>
        private void ReloadPane(ChallengePane pane)
        {
            pane.Me = null;
            pane.MeLoaded = false;
            pane.Rows = Array.Empty<ChallengeEntryDto>();
            pane.Entries = 0;
            pane.EntriesLoaded = false;
            pane.Table = null;
            RenderKpis(pane);

            if (pane.MineSlot != null)
            {
                LoadMine(pane, pane.MineSlot);
            }
            if (pane.EntriesSlot != null)
            {
                LoadSlice(pane, pane.EntriesSlot);
            }
        }

        /// <summary>
        /// The response of a write as chips, because that payload is the point of the call. When the
        /// server says a reward was granted, the tier it must have paid is resolved from the config
        /// we already hold — the response carries the flag but not the contents.
        /// </summary>
        private VisualElement ResponseChips(ChallengeConfigDto cfg, SubmitScoreResponseDto response)
        {
            var box = new VisualElement();
            if (response == null)
            {
                box.Add(new Chip("no response body", ChipTone.Neutral));
                return box;
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip("value " + Fmt.Number(response.value), ChipTone.Info));
            chips.Add(new Chip(response.isFinished ? "goal reached" : "still short of the goal",
                response.isFinished ? ChipTone.Ok : ChipTone.Neutral));
            if (response.finishPosition.HasValue)
            {
                chips.Add(new Chip("finisher #" + response.finishPosition.Value, ChipTone.Accent));
            }
            chips.Add(new Chip(response.rewardGranted ? "reward granted" : "no reward granted",
                response.rewardGranted ? ChipTone.Ok : ChipTone.Neutral));
            box.Add(chips);

            if (!response.rewardGranted)
            {
                return box;
            }

            var tier = TierFor(cfg, response.finishPosition);
            if (tier.Count == 0)
            {
                return box;
            }

            var granted = new VisualElement();
            granted.AddToClassList("sc-chip-row");
            granted.AddToClassList("sc-chal-granted");
            foreach (var reward in tier)
            {
                granted.Add(RewardPill(reward));
            }
            box.Add(granted);
            return box;
        }

        /// <summary>
        /// The finisher tier the server pays for a position, matched the way the server matches it
        /// (every range whose valueMin..valueMax covers the finish position). A tier the console
        /// marked as open-ended is capped at its valueMax here — the SDK DTO has no flag for it.
        /// </summary>
        private static List<RewardDataDto> TierFor(ChallengeConfigDto cfg, int? finishPosition)
        {
            var rewards = new List<RewardDataDto>();
            if (cfg.rewardsForFinishers == null)
            {
                return rewards;
            }

            // The server falls back to first place when it never recorded a position for the entry.
            double position = finishPosition ?? 1;
            foreach (var range in cfg.rewardsForFinishers)
            {
                if (range == null || range.rewards == null)
                {
                    continue;
                }
                if (position < range.valueMin || position > range.valueMax)
                {
                    continue;
                }
                foreach (var reward in range.rewards)
                {
                    if (reward != null)
                    {
                        rewards.Add(reward);
                    }
                }
            }
            return rewards;
        }

        /// <summary>
        /// Opens the confirmation dialog and resolves to the answer, so a destructive
        /// <see cref="ActionCard"/> can await it from inside its run delegate.
        /// <para>
        /// ConfirmDialog reports the "yes" only, so the "no" is read off the popup: a dialog that is
        /// no longer open without the confirm callback having fired was dismissed. Without that watch
        /// a cancelled card would sit in its "Working…" state forever.
        /// </para>
        /// </summary>
        private Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            var gate = new TaskCompletionSource<bool>();

            var popup = Popup;
            if (popup == null)
            {
                // No dialog host: refusing is the safe answer for something irreversible.
                Debug.LogWarning("[Showcase] Challenges: no popup host to confirm '" + title + "' in.");
                gate.TrySetResult(false);
                return gate.Task;
            }

            ConfirmDialog.Open(popup, title, message, confirmText, () => gate.TrySetResult(true));

            IVisualElementScheduledItem watch = null;
            watch = schedule.Execute(() =>
            {
                if (popup.IsOpen)
                {
                    return;
                }
                // Confirming closes the dialog too, but it answered the gate first, so this is a no-op
                // on that path and the answer on the dismissed one.
                gate.TrySetResult(false);
                watch.Pause();
            }).Every(ConfirmPollMs);

            return gate.Task;
        }

        // ----- texts ------------------------------------------------------------------------------

        private string SliceTitle()
        {
            switch (_slice)
            {
                case Slice.Cohort: return "My cohort";
                case Slice.Around: return "Around me";
                default: return "Global top";
            }
        }

        private string EntriesCaption()
        {
            switch (_slice)
            {
                case Slice.Cohort: return "In my cohort";
                case Slice.Around: return "Rows around me";
                default: return "Players";
            }
        }

        private string SliceNote(ChallengeConfigDto cfg)
        {
            switch (_slice)
            {
                case Slice.Cohort:
                    return cfg.cohortsEnabled
                        ? "GetMyTopAsync ranks the cohort you were assigned to — cohorts of "
                          + cfg.cohortSize + " keep a crowded challenge readable."
                        : "GetMyTopAsync falls back to the global top: this challenge has cohorts off.";
                case Slice.Around:
                    return "GetAroundPlayerAsync returns up to " + AroundRange
                        + " rows above and below your own entry.";
                default:
                    return "GetTopAsync returns the first " + TopCount + " entries of the whole ranking.";
            }
        }

        private static string GoalSentence(ChallengeConfigDto cfg)
        {
            string goal = cfg.orderType == OrderType.Highest
                ? "Reach " + Fmt.Number(cfg.targetValue) + " or more to finish."
                : "Get down to " + Fmt.Number(cfg.targetValue) + " or less to finish.";
            return goal + " " + StrategySentence(cfg.updateStrategy);
        }

        private static string StrategySentence(UpdateStrategy strategy)
        {
            switch (strategy)
            {
                case UpdateStrategy.Latest: return "Every score replaces the previous one.";
                case UpdateStrategy.Total: return "Scores add up across submits.";
                default: return "Only your best score is kept.";
            }
        }

        private static string StrategyChip(UpdateStrategy strategy)
        {
            switch (strategy)
            {
                case UpdateStrategy.Latest: return "keeps the latest score";
                case UpdateStrategy.Total: return "sums the scores";
                default: return "keeps the best score";
            }
        }

        private static string SubmitDescription(ChallengeConfigDto cfg)
        {
            return "Sends one score for the current run. " + StrategySentence(cfg.updateStrategy)
                + " Once you have finished, further scores are refused.";
        }

        private static string ClaimDescription(ChallengeConfigDto cfg)
        {
            return cfg.rewardMode == RewardMode.AllFinishers
                ? "Pays out the tier this player earned by finishing. Grants are recorded per run, so "
                  + "a second claim does not pay twice."
                : "Only challenges that reward every finisher can be claimed. This one pays by finish "
                  + "order, inside the score that finishes it.";
        }

        /// <summary>
        /// The live window of the current run. The reset dates come from the run's own state, unlike
        /// the configured length, which is what the designer planned rather than what is happening.
        /// </summary>
        private static string WindowText(ChallengeConfigDto cfg)
        {
            if (!cfg.isReset)
            {
                return "open-ended — the run only ends when the finisher cap is hit or it is reset";
            }
            return Fmt.DateTime2(cfg.lastResetDate) + "  →  " + Fmt.DateTime2(cfg.nextResetDate);
        }

        private static string ResetEvery(ChallengeConfigDto cfg)
        {
            string unit;
            switch (cfg.resetIntervalType)
            {
                case ResetIntervalType.Weekly:
                    unit = "week";
                    break;
                case ResetIntervalType.Monthly:
                    unit = "month";
                    break;
                default:
                    unit = "day";
                    break;
            }
            int every = cfg.resetIntervalValue <= 1 ? 1 : cfg.resetIntervalValue;
            return every == 1 ? unit : every + " " + unit + "s";
        }

        private static string SubmitMessage(SubmitScoreResponseDto response)
        {
            if (response == null)
            {
                return "Accepted, but the server sent no body back";
            }
            if (!response.isFinished)
            {
                return "Score is now " + Fmt.Number(response.value);
            }
            return response.finishPosition.HasValue
                ? "Finished as #" + response.finishPosition.Value + " with " + Fmt.Number(response.value)
                : "Finished with " + Fmt.Number(response.value);
        }

        // ----- shared plumbing --------------------------------------------------------------------

        /// <summary>Fraction of the goal reached, 0..1, in the direction this challenge counts.</summary>
        private static float Progress(ChallengeConfigDto cfg, double value)
        {
            if (cfg.targetValue <= 0d)
            {
                // A zero (or negative) goal cannot be expressed as a fraction: treat any score as the
                // whole bar for highest-wins, and leave it empty otherwise.
                return cfg.orderType == OrderType.Highest && value > 0d ? 1f : 0f;
            }
            if (cfg.orderType == OrderType.Lowest)
            {
                // Counting down: at or below the goal is full, twice the goal is half a bar.
                return value <= cfg.targetValue ? 1f : (float)(cfg.targetValue / value);
            }
            return (float)(value / cfg.targetValue);
        }

        private static ChallengeEntryDto[] Compact(ChallengeEntryDto[] entries)
        {
            if (entries == null)
            {
                return Array.Empty<ChallengeEntryDto>();
            }

            var list = new List<ChallengeEntryDto>(entries.Length);
            foreach (var e in entries)
            {
                if (e != null)
                {
                    list.Add(e);
                }
            }
            return list.ToArray();
        }

        private static string PlayerLabel(ChallengeEntryDto e)
        {
            return string.IsNullOrWhiteSpace(e.playerName) ? Fmt.Id(e.playerId, 10) : e.playerName;
        }

        private static string TitleOf(ChallengeConfigDto cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.name))
            {
                return cfg.name;
            }
            return string.IsNullOrWhiteSpace(cfg.key) ? Fmt.Id(cfg.id) : cfg.key;
        }

        private string Nickname()
        {
            var account = Sdk.PlayerAccount;
            var info = account != null ? account.PlayerAccountInfo : null;
            return info != null ? info.Nickname : null;
        }

        private static string Trimmed(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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

        private async Task<Outcome> Await(AsyncOperation<RestApiResult> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

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
                Ctx.Log.Record(label, result);
            }
            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true };
            }
            string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome { Ok = false, Message = message };
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;
        }

        /// <summary>
        /// One challenge tab's mutable state. It exists because the pane is filled by two calls that
        /// can land in either order, and because a write reloads those two calls in place — the slots
        /// and the KPI strip have to outlive the data in them.
        /// </summary>
        private sealed class ChallengePane
        {
            public readonly ChallengeConfigDto Config;
            public readonly KpiRow Kpis = new KpiRow();

            public SectionHeader EntriesHeader;
            public VisualElement MineSlot;
            public VisualElement EntriesSlot;

            /// <summary>The player's entry, or null while they have none for the current run.</summary>
            public ChallengeEntryDto Me;

            /// <summary>True once the entry call finished — tells "no entry" apart from "still loading".</summary>
            public bool MeLoaded;

            /// <summary>
            /// True once this screen joined the challenge. The SDK has no read for participation, so
            /// it is the only thing that tells "joined, no score yet" from "never joined" — both of
            /// which answer 404 on GetPlayerAsync.
            /// </summary>
            public bool Joined;

            public ChallengeEntryDto[] Rows = Array.Empty<ChallengeEntryDto>();
            public int Entries;
            public bool EntriesLoaded;
            public DataTable Table;

            public ChallengePane(ChallengeConfigDto config)
            {
                Config = config;
            }

            /// <summary>Row predicate for the table highlight; reads <see cref="Me"/> at render time,
            /// so a late-arriving entry only needs a re-bind.</summary>
            public bool IsMine(object row)
            {
                if (Me == null || string.IsNullOrEmpty(Me.playerId))
                {
                    return false;
                }
                var e = row as ChallengeEntryDto;
                return e != null && e.playerId == Me.playerId;
            }
        }
    }
}

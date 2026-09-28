using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Core.Errors;
using MirraCloud.Core.Friends.Dto;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Enums;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Leaderboard detail: one tab per configured board, and a toolbar filter that swaps which
    /// <c>slice</c> of the ranking the tab requests — the player's table, the global top, the rows
    /// around the player, the friend list, or the player's country. Each of those is a different SDK
    /// endpoint returning the same rows, which is exactly what this screen is meant to demonstrate.
    /// Every call takes the board's key. A pane also plays on its board — join, submit, leave — and a
    /// last tab lists the rewards boards have paid out, which arrive in Economy.
    /// <para>
    /// Every pane fans out into two independent calls (the slice and the player's own entry), so the
    /// KPI strip is re-rendered from <see cref="BoardPane"/> state as each one lands instead of
    /// waiting for both.
    /// </para>
    /// </summary>
    public sealed class LeaderboardView : ServiceView
    {
        private const int TopCount = 100;
        private const int AroundRange = 10;

        /// <summary>Bars past this point are too thin to read; the table below carries the rest.</summary>
        private const int ChartBars = 8;

        // Medal tints are deliberately outside the semantic palette: on a ranking table gold/silver/
        // bronze *are* the meaning, and no status color reads as "third place".
        private static readonly Color Gold = new Color(0.91f, 0.78f, 0.32f);
        private static readonly Color Silver = new Color(0.76f, 0.78f, 0.84f);
        private static readonly Color Bronze = new Color(0.82f, 0.54f, 0.32f);

        // Index-aligned with the Slice enum — the dropdown hands back the label, not the value.
        private static readonly string[] SliceNames = { "My table", "Global top", "Around me", "Friends", "Country" };

        private const string BoardsSnippet = @"// every board configured for this project + branch
var op = sdk.Leaderboard.InitializeAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var cfg in op.Result.Data)
{
    // cfg.key is what every other call takes; cfg.nextResetDate is the next reset, UTC
    Debug.Log(cfg.key + "" / "" + cfg.name + "" / "" + cfg.orderType);
}
// the service keeps them too: sdk.Leaderboard.LeaderboardConfigs";

        private const string TopSnippet = @"// the player's table, best entries first: their cohort on a board with cohorts,
// the whole board otherwise. Every call takes the board's key (cfg.key), not its id.
var op = sdk.Leaderboard.GetLeaderboardTopEntries(leaderboardKey, 100);
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var e in op.Result.Data.entries)
{
    // name, icon and country are the player's profile's own
    Debug.Log(e.position + "". "" + e.playerName + "" ("" + e.countryCode + "") = "" + e.value);
}";

        private const string GlobalTopSnippet = @"// the whole board, every cohort together
var op = sdk.Leaderboard.GetLeaderboardGlobalTopEntries(leaderboardKey, 100);
await op.Task();";

        private const string CountrySnippet = @"// same ranking, narrowed to the country of the player's profile
var op = sdk.Leaderboard.GetLeaderboardTopEntriesByCountry(leaderboardKey, 100);
await op.Task();

foreach (var e in op.Result.Data.entries)
{
    Debug.Log(e.position + "". "" + e.playerName);
}";

        private const string FriendsSnippet = @"// this endpoint ranks only the ids you pass in, so fetch the friends first
var friends = sdk.Friends.GetFriendsAsync(false);
await friends.Task();

var ids = new List<string>();
foreach (var f in friends.Result.Data)
{
    ids.Add(f.PlayerId);
}

var op = sdk.Leaderboard.GetLeaderboardTopEntriesByFriends(leaderboardKey, ids.ToArray());
await op.Task();";

        private const string AroundSnippet = @"// the player's neighbourhood: 10 rows above and below their own
var op = sdk.Leaderboard.GetLeaderboardPlayerAroundEntries(leaderboardKey, 10);
await op.Task();

var data = op.Result.Data;
// data.pLayersAbove (SDK spelling) / data.targetPlayer / data.playersBelow
Debug.Log(data.targetPlayer != null ? data.targetPlayer.position.ToString() : ""unranked"");";

        private const string MeSnippet = @"// the signed-in player's own row on this board
var op = sdk.Leaderboard.GetLeaderboardPlayer(leaderboardKey);
await op.Task();

// a player without a score this session gets 404 leaderboards.entry_not_found
if (op.Result.IsSuccess && op.Result.Data != null)
{
    Debug.Log(""#"" + op.Result.Data.position + "" with "" + op.Result.Data.value);
}";

        private const string JoinSnippet = @"// scores are accepted from participants only: join once, before the first submit
var op = sdk.Leaderboard.JoinAsync(leaderboardKey);
await op.Task();
// op.Result.Data: the player's entry, null until they submit a score";

        private const string SubmitSnippet = @"// a score of the current player; the board's update strategy (best / latest / total)
// decides what happens to the stored one. Returns the entry with the new place.
var op = sdk.Leaderboard.SubmitScoreAsync(1250d, leaderboardKey);
await op.Task();

if (op.Result.IsSuccess)
{
    Debug.Log(""now #"" + op.Result.Data.position);
}
else if (op.Result.Error.HasCode(CloudErrorCodes.LeaderboardsParticipationRequired))
{
    // join first
}

// a time board takes a TimeSpan, sent as seconds
await sdk.Leaderboard.SubmitScoreAsync(TimeSpan.FromSeconds(95.4), leaderboardKey).Task();";

        private const string LeaveSnippet = @"// removes the player and their score of the current session
await sdk.Leaderboard.LeaveAsync(leaderboardKey).Task();";

        private Slice _slice = Slice.Top;
        private Tabs _boards;
        private int _rewardsTab = -1;

        public LeaderboardView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        /// <summary>Which endpoint a board pane asks for. Order matches <see cref="SliceNames"/>.</summary>
        private enum Slice
        {
            Top,
            GlobalTop,
            AroundMe,
            Friends,
            Country,
        }

        protected override void Populate()
        {
            _boards = null;
            _rewardsTab = -1;
            SetStatus(null);
            SetSubtitle("One tab per configured board. The slice picker swaps the endpoint the tab "
                        + "calls — your table, the whole ranking, your neighbours, your friends, or your country.");

            UseToolbar()
                .WithFilter("Slice", SliceNames, OnSliceChanged, SliceNames[(int)_slice])
                .WithSpacer()
                .WithRefresh(Refresh);

            DeclareCall(new SdkCall("List boards", BoardsSnippet,
                "Call it once at startup: every other leaderboard call needs a board key from here."));
            DeclareCall(new SdkCall("Join a board", JoinSnippet,
                "Scores are accepted from participants only."));
            DeclareCall(new SdkCall("Submit a score", SubmitSnippet));
            DeclareCall(new SdkCall("Leave a board", LeaveSnippet));
            DeclareCall(new SdkCall("Top of the player's table", TopSnippet));
            DeclareCall(new SdkCall("Top of the whole board", GlobalTopSnippet));
            DeclareCall(new SdkCall("Entries around the player", AroundSnippet));
            DeclareCall(new SdkCall("Entries among friends", FriendsSnippet));
            DeclareCall(new SdkCall("Entries by country", CountrySnippet));
            DeclareCall(new SdkCall("The player's own entry", MeSnippet,
                "Returns no entry until the player has submitted a score to this board."));
            DeclareCall(new SdkCall("Rewards the boards paid out", PendingRewardsPanel.ReadSnippet,
                "Boards pay out into Economy when a session ends."));
            DeclareCall(new SdkCall("Claim the rewards", PendingRewardsPanel.ClaimSnippet));

            // Zero margin: this slot only carries the loading/failure state — on success the boards
            // land in the tab strip (chrome) and the panes at the bottom of Content.
            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Leaderboard.InitializeAsync(),
                slot,
                BuildBoards,
                isEmpty: c => c == null || c.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Leaderboard boards",
                    Snippet = BoardsSnippet,
                    ServiceName = "Leaderboard",
                    // this is the board *configuration* call, so a 404 really does mean
                    // "no leaderboards exist in this project"
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoBoards,
                });
        }

        private VisualElement BuildBoards(LeaderboardConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 board" : configs.Length + " boards", ChipTone.Ok);

            _boards = UseTabs();
            foreach (var cfg in configs)
            {
                var captured = cfg;
                _boards.Add(BoardTitle(captured), LucideIcon.Trophy, () => BuildBoardPane(captured));
            }

            // Rewards are paid into Economy for every board at once, so they get one tab of their own.
            _rewardsTab = _boards.Count;
            _boards.Add("Rewards", LucideIcon.Gift, BuildRewards);

            // The strip and its panes live outside this slot, so the slot itself renders nothing.
            return new VisualElement();
        }

        private VisualElement BuildRewards()
        {
            return new PendingRewardsPanel(Ctx, RewardSourceType.Leaderboard,
                "When a board resets, the server works out what each place (or score) earned and pays it "
                + "into Economy, where the game reads and claims it. Nothing is claimed by reading.",
                "Nothing is waiting for this player. A reward lands here when a board resets and the "
                + "player's place (or score) falls in one of the board's reward ranges.");
        }

        private VisualElement NoBoards()
        {
            SetStatus("Not configured", ChipTone.Warn);
            return ZeroState.NotConfigured("Leaderboards");
        }

        private void OnSliceChanged(string name)
        {
            int index = Array.IndexOf(SliceNames, name);
            if (index < 0 || (Slice)index == _slice)
            {
                return;
            }
            _slice = (Slice)index;

            if (_boards == null || _rewardsTab < 0)
            {
                return;
            }
            // Panes cache their data, so the visible one has to be thrown away for the new endpoint
            // to be called; the hidden ones rebuild when they are selected again. The rewards tab is
            // left alone — it does not depend on the slice.
            for (int i = 0; i < _rewardsTab; i++)
            {
                _boards.Invalidate(i);
            }
        }

        private VisualElement BuildBoardPane(LeaderboardConfigDto cfg)
        {
            var pane = new BoardPane(cfg);

            var root = new VisualElement();
            root.Add(BuildBoardMeta(cfg));

            pane.Kpis.AddToClassList("sc-lb-kpis");
            root.Add(pane.Kpis);
            RenderKpis(pane);

            root.Add(pane.EntriesSlot);

            root.Add(new SectionHeader("Play on this board"));
            root.Add(PlayHint(cfg));
            root.Add(JoinCard(pane));
            root.Add(SubmitCard(pane));
            root.Add(LeaveCard(pane));

            LoadMyEntry(pane);
            LoadSlice(pane, pane.EntriesSlot);
            return root;
        }

        private static VisualElement BuildBoardMeta(LeaderboardConfigDto cfg)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");
            row.AddToClassList("sc-lb-meta");

            row.Add(new Chip(cfg.orderType.ToString(), ChipTone.Info));
            row.Add(new Chip(cfg.type.ToString(), ChipTone.Neutral));
            row.Add(new Chip(cfg.updateStrategy.ToString(), ChipTone.Neutral));
            if (!string.IsNullOrEmpty(cfg.key))
            {
                row.Add(new Chip("key: " + cfg.key, ChipTone.Neutral));
            }
            if (cfg.cohortsEnabled)
            {
                row.Add(new Chip("cohorts of " + cfg.cohortSize, ChipTone.Neutral));
            }

            if (cfg.isReset)
            {
                var resets = new Chip("resets " + cfg.resetIntervalType, ChipTone.Warn);
                resets.tooltip = string.Format(CultureInfo.InvariantCulture, "at {0:00}:{1:00} UTC",
                    cfg.resetTimeHour, cfg.resetTimeMinute);
                row.Add(resets);
                // Filled once the board's first score started a session.
                if (cfg.nextResetDate.HasValue)
                {
                    var chip = new CountdownChip(cfg.nextResetDate.Value.ToUniversalTime());
                    chip.tooltip = "Next reset: " + Fmt.DateTime2(cfg.nextResetDate);
                    row.Add(chip);
                }
            }
            else
            {
                row.Add(new Chip("never resets", ChipTone.Neutral));
            }

            return row;
        }

        /// <summary>
        /// Redraws the KPI strip from whatever the pane knows so far. Called once per arriving
        /// response, because the player's own entry and the slice are two separate requests.
        /// </summary>
        private void RenderKpis(BoardPane pane)
        {
            var kpis = pane.Kpis.Clear2();

            if (pane.Me != null && pane.Me.position > 0)
            {
                kpis.Add("My rank", LucideIcon.Medal, "#" + pane.Me.position, null, pane.Me.position <= 3);
                kpis.Add("My score", LucideIcon.Target, Fmt.Number(pane.Me.value));
            }
            else if (pane.MeLoaded)
            {
                kpis.AddZero("My rank", LucideIcon.Medal, "unranked");
                kpis.AddZero("My score", LucideIcon.Target, "0");
            }
            else
            {
                kpis.Add("My rank", LucideIcon.Medal, Fmt.Dash);
                kpis.Add("My score", LucideIcon.Target, Fmt.Dash);
            }

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

            kpis.Add("Board updated", LucideIcon.Clock, RelativeTime.Format(pane.Config.updatedDate));
        }

        private string EntriesCaption()
        {
            switch (_slice)
            {
                case Slice.GlobalTop: return "Players on the board";
                case Slice.AroundMe: return "Rows around you";
                case Slice.Friends: return "Friends ranked";
                case Slice.Country: return "In your country";
                default: return "Players";
            }
        }

        private void LoadSlice(BoardPane pane, VisualElement slot)
        {
            string key = pane.Config.key;
            switch (_slice)
            {
                case Slice.GlobalTop:
                    BindSlice(pane, slot,
                        () => Sdk.Leaderboard.GetLeaderboardGlobalTopEntries(key, TopCount),
                        d => d?.entries, "Leaderboard global top", GlobalTopSnippet,
                        "This board has no entries yet. The first score submitted to it creates the ranking.");
                    return;

                case Slice.AroundMe:
                    BindSlice(pane, slot,
                        () => Sdk.Leaderboard.GetLeaderboardPlayerAroundEntries(key, AroundRange),
                        Around, "Leaderboard around me", AroundSnippet,
                        "You have no entry on this board yet. Submit a score and the players just "
                        + "above and below you show up here.");
                    return;

                case Slice.Friends:
                    LoadFriendsSlice(pane, slot);
                    return;

                case Slice.Country:
                    BindSlice(pane, slot,
                        () => Sdk.Leaderboard.GetLeaderboardTopEntriesByCountry(key, TopCount),
                        d => d?.entries, "Leaderboard top by country", CountrySnippet,
                        "Nobody from your country has scored on this board yet. Entries appear after "
                        + "the first SubmitScoreAsync from a profile with the same country.");
                    return;

                default:
                    BindSlice(pane, slot,
                        () => Sdk.Leaderboard.GetLeaderboardTopEntries(key, TopCount),
                        d => d?.entries, "Leaderboard top", TopSnippet,
                        pane.Config.cohortsEnabled
                            ? "You have no table on this board yet: a board with cohorts places you in one "
                              + "with your first score. The Global top slice shows every cohort."
                            : "This board has no entries yet. The first SubmitScoreAsync call against it "
                              + "creates the ranking, and every later score updates it.");
                    return;
            }
        }

        /// <summary>
        /// The friends slice is the only two-step one: the endpoint ranks exactly the ids it is
        /// given, so the friend list has to be fetched first and both calls end up in the journal.
        /// </summary>
        private void LoadFriendsSlice(BoardPane pane, VisualElement slot)
        {
            string key = pane.Config.key;
            ViewBind.Load(
                () => Sdk.Friends.GetFriendsAsync(false),
                slot,
                friends =>
                {
                    var inner = new VisualElement();
                    BindSlice(pane, inner,
                        () => Sdk.Leaderboard.GetLeaderboardTopEntriesByFriends(key, FriendIds(friends)),
                        d => d?.entries, "Leaderboard top by friends", FriendsSnippet,
                        "None of your friends has scored on this board yet.");
                    return inner;
                },
                isEmpty: f => f == null || f.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Friends list",
                    Snippet = FriendsSnippet,
                    ServiceName = "Friends",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane,
                        "This slice ranks only the players on your friend list, and it is empty. "
                        + "Add a friend in the Friends module first."),
                });
        }

        /// <summary>Shared binding for every slice: same rows, same table, different endpoint.</summary>
        private void BindSlice<T>(BoardPane pane, VisualElement slot,
            Func<AsyncOperation<RestApiResult<T>>> start, Func<T, LeaderboardEntryDto[]> rows,
            string label, string snippet, string emptyMessage)
        {
            ViewBind.Load(
                start,
                slot,
                data => BuildEntries(pane, rows(data)),
                isEmpty: data => IsEmpty(rows(data)),
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = label,
                    Snippet = snippet,
                    ServiceName = "Leaderboard",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane, emptyMessage),
                });
        }

        private VisualElement BuildEntries(BoardPane pane, LeaderboardEntryDto[] entries)
        {
            var rows = entries ?? Array.Empty<LeaderboardEntryDto>();
            pane.Rows = rows;
            pane.Entries = rows.Length;
            pane.EntriesLoaded = true;
            RenderKpis(pane);

            var root = new VisualElement();

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

        private VisualElement EmptySlice(BoardPane pane, string message)
        {
            pane.Rows = Array.Empty<LeaderboardEntryDto>();
            pane.Entries = 0;
            pane.EntriesLoaded = true;
            pane.Table = null;
            RenderKpis(pane);

            // The board keeps the shape it will have once scores arrive — the reader sees the
            // columns they are going to get, not a shrug.
            return ZeroState.Table(Columns(pane), message);
        }

        /// <summary>Top of the ranking as bars — the shape of the gap between the leaders, which a
        /// column of numbers hides. Returns null when there is nothing to compare.</summary>
        private VisualElement BuildChart(LeaderboardEntryDto[] rows)
        {
            if (rows.Length < 2)
            {
                return null;
            }

            var ordered = new List<LeaderboardEntryDto>(rows);
            ordered.Sort((a, b) => a.position.CompareTo(b.position));

            int count = Math.Min(ChartBars, ordered.Count);
            var points = new List<ChartPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var e = ordered[i];
                points.Add(new ChartPoint("#" + e.position, (float)e.value, MedalTint(e.position)));
            }

            var chart = new BarChart(150f);
            chart.AddToClassList("sc-lb-chart");
            chart.SetAccent(Meta.Accent);
            chart.SetValueFormatter(v => Fmt.Number(v));
            chart.SetData(points);
            return chart;
        }

        private DataColumn[] Columns(BoardPane pane)
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "#", FixedWidth = true, Px = 74, Align = "center",
                    Cell = RankCell,
                    SortKey = row => ((LeaderboardEntryDto)row).position,
                },
                new DataColumn
                {
                    Header = "PLAYER", Grow = 1f,
                    Cell = row => PlayerCell(pane, row),
                    SortKey = row => PlayerLabel((LeaderboardEntryDto)row),
                },
                new DataColumn
                {
                    Header = "SCORE", FixedWidth = true, Px = 120, Align = "right",
                    Cell = ScoreCell,
                    SortKey = row => ((LeaderboardEntryDto)row).value,
                },
            };
        }

        private static VisualElement RankCell(object row)
        {
            var e = (LeaderboardEntryDto)row;

            var text = new Label(e.position > 0 ? "#" + e.position : Fmt.Dash);
            text.AddToClassList("sc-rank");
            if (e.position < 1 || e.position > 3)
            {
                return text;
            }

            var tint = MedalTint(e.position).Value;
            text.style.color = tint;

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-lb-rank");

            var medal = new Label(LucideIcon.Medal);
            medal.AddToClassList("sc-lb-medal");
            medal.AddToClassList("sc-icon");
            medal.style.color = tint;
            wrap.Add(medal);
            wrap.Add(text);
            return wrap;
        }

        private static VisualElement PlayerCell(BoardPane pane, object row)
        {
            var e = (LeaderboardEntryDto)row;
            string label = PlayerLabel(e);

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-lb-player");

            var avatar = new Avatar(26f).SetInitialsFor(label);
            avatar.AddToClassList("sc-lb-player__avatar");
            wrap.Add(avatar);

            var name = new Label(Fmt.OrDash(label));
            name.enableRichText = false;
            name.tooltip = e.playerId + " · " + e.countryCode;
            wrap.Add(name);

            if (pane.IsMine(row))
            {
                var you = new Badge("You", ChipTone.Accent);
                you.AddToClassList("sc-lb-player__you");
                wrap.Add(you);
            }
            return wrap;
        }

        private static VisualElement ScoreCell(object row)
        {
            var e = (LeaderboardEntryDto)row;
            var label = new Label(Fmt.Number(e.value));
            label.AddToClassList("sc-score");
            // Fmt.Number compacts past 10k ("12.4k"), and on a ranking the exact figure is what
            // decides the order — keep it one hover away.
            label.tooltip = e.value.ToString("R", CultureInfo.InvariantCulture);
            return label;
        }

        /// <summary>
        /// The player's own row, fetched next to the slice. Bound by hand rather than through
        /// <see cref="ViewBind"/>: there is no slot to fill, and a 404 here means "no score yet"
        /// rather than "this service is not set up".
        /// </summary>
        private async void LoadMyEntry(BoardPane pane)
        {
            RestApiResult<LeaderboardEntryDto> result = null;
            try
            {
                var op = Sdk.Leaderboard.GetLeaderboardPlayer(pane.Config.key);
                if (op != null)
                {
                    await op.Task();
                    result = op.Result;
                }
            }
            catch (Exception e)
            {
                // async void: an exception escaping here would surface as an unhandled one rather
                // than as a failed tile, so it is logged and the strip falls back to "unranked".
                Debug.LogWarning("[Showcase] Leaderboard: reading the player's own entry failed: " + e.Message);
            }

            if (result != null)
            {
                Ctx.Log?.Record("Leaderboard: my entry", result, MeSnippet);
                if (result.IsSuccess)
                {
                    pane.Me = result.Data;
                }
            }

            pane.MeLoaded = true;
            RenderKpis(pane);

            // The table may have rendered before this landed, and it is the "You" row highlight that
            // depends on it — re-bind rather than leave the player unable to find themselves.
            if (pane.Me != null && pane.Table != null)
            {
                pane.Table.Bind(pane.Rows, pane.IsMine);
            }
        }

        // ----- playing on a board ---------------------------------------------------------------------

        private static VisualElement PlayHint(LeaderboardConfigDto cfg)
        {
            string strategy;
            switch (cfg.updateStrategy)
            {
                case UpdateStrategy.Best:
                    strategy = "This board keeps the best value, so a worse score leaves the entry alone.";
                    break;
                case UpdateStrategy.Total:
                    strategy = "This board totals the values, so every submission adds to the entry.";
                    break;
                default:
                    strategy = "This board keeps the latest value, so every submission replaces the entry.";
                    break;
            }

            string unit = cfg.type == LeaderboardType.Time
                ? " It is a time board: pass a TimeSpan, which the SDK sends as seconds."
                : string.Empty;

            var hint = new Label("Scores are accepted from participants only, so join once first. " + strategy
                                 + unit + " The name, icon and country shown are your profile's.");
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            return hint;
        }

        private VisualElement JoinCard(BoardPane pane)
        {
            return new ActionCard("Join the board",
                    "Makes the player a participant. Joining twice is harmless.", LucideIcon.UserPlus)
                .WithSnippet(JoinSnippet)
                .OnRun("Join", _ => Join(pane));
        }

        private VisualElement SubmitCard(BoardPane pane)
        {
            return new ActionCard("Submit a score",
                    "Writes one value and reloads the standings above.", LucideIcon.Send)
                .WithFields(FormField.Float("score", pane.Config.type == LeaderboardType.Time ? "Seconds" : "Score", 1000f)
                    .WithPlaceholder("Any number; the update strategy above decides what the server does with it.")
                    .AsRequired())
                .WithSnippet(SubmitSnippet)
                .OnRun("Submit", values => Submit(pane, values));
        }

        private VisualElement LeaveCard(BoardPane pane)
        {
            return new ActionCard("Leave the board",
                    "Removes the player and their score of the current session.", LucideIcon.UserMinus)
                .WithSnippet(LeaveSnippet)
                .OnRun("Leave", _ => Leave(pane), destructive: true);
        }

        private async Task<ActionOutcome> Join(BoardPane pane)
        {
            var op = Sdk.Leaderboard.JoinAsync(pane.Config.key);
            await op.Task();
            Ctx.Log?.Record("Leaderboard: join", op.Result, JoinSnippet);
            if (!op.Result.IsSuccess)
            {
                return ActionOutcome.Failure(ErrorText(op.Result));
            }

            ReloadStandings(pane);
            var entry = op.Result.Data;
            return ActionOutcome.Success(entry != null && entry.position > 0
                ? "Joined — you are #" + entry.position
                : "Joined — submit a score to get a place");
        }

        private async Task<ActionOutcome> Submit(BoardPane pane, FormValues values)
        {
            double score;
            // Parsed here rather than through FormValues.Float: the SDK takes a double, and a score can
            // easily be larger than a float represents exactly.
            if (!double.TryParse(values.Text("score").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out score))
            {
                return ActionOutcome.Failure("Score must be a number (use a dot for decimals).");
            }

            var op = pane.Config.type == LeaderboardType.Time
                ? Sdk.Leaderboard.SubmitScoreAsync(TimeSpan.FromSeconds(score), pane.Config.key)
                : Sdk.Leaderboard.SubmitScoreAsync(score, pane.Config.key);
            await op.Task();
            Ctx.Log?.Record("Leaderboard: submit score", op.Result, SubmitSnippet);
            if (!op.Result.IsSuccess)
            {
                return ActionOutcome.Failure(op.Result.Error.HasCode(CloudErrorCodes.LeaderboardsParticipationRequired)
                    ? "Join the board first — scores are accepted from participants only."
                    : ErrorText(op.Result));
            }

            Toasts?.Ok("Score " + Fmt.Number(score) + " submitted");
            ReloadStandings(pane);
            var entry = op.Result.Data;
            return ActionOutcome.Success(entry != null
                ? "Submitted — you are #" + entry.position + " with " + Fmt.Number(entry.value)
                : "Submitted " + Fmt.Number(score));
        }

        private async Task<ActionOutcome> Leave(BoardPane pane)
        {
            var op = Sdk.Leaderboard.LeaveAsync(pane.Config.key);
            await op.Task();
            Ctx.Log?.Record("Leaderboard: leave", op.Result, LeaveSnippet);
            if (!op.Result.IsSuccess)
            {
                return ActionOutcome.Failure(ErrorText(op.Result));
            }

            ReloadStandings(pane);
            return ActionOutcome.Success("Left the board — your score of this session is gone");
        }

        /// <summary>
        /// Re-reads the player's entry and the current slice in place, so the result the reader just
        /// clicked for stays on the card instead of the whole tab being rebuilt.
        /// </summary>
        private void ReloadStandings(BoardPane pane)
        {
            pane.Me = null;
            pane.MeLoaded = false;
            pane.Entries = 0;
            pane.EntriesLoaded = false;
            pane.Table = null;
            RenderKpis(pane);

            LoadMyEntry(pane);
            LoadSlice(pane, pane.EntriesSlot);
        }

        private static string ErrorText(RestApiResult result)
        {
            if (result == null || result.Error == null)
            {
                return "no response";
            }
            var errors = result.Error.Errors;
            if (errors != null && errors.Count > 0 && errors[0] != null && !string.IsNullOrEmpty(errors[0].Message))
            {
                return errors[0].Message;
            }
            return string.IsNullOrEmpty(result.Error.Message) ? "HTTP " + result.HttpStatusCode : result.Error.Message;
        }

        /// <summary>Flattens the around-me response into one ranked list (the table sorts it).</summary>
        private static LeaderboardEntryDto[] Around(LeaderboardAroundEntriesDto data)
        {
            if (data == null)
            {
                return Array.Empty<LeaderboardEntryDto>();
            }

            var list = new List<LeaderboardEntryDto>();
            Append(list, data.pLayersAbove); // SDK spelling
            if (data.targetPlayer != null)
            {
                list.Add(data.targetPlayer);
            }
            Append(list, data.playersBelow);
            return list.ToArray();
        }

        private static void Append(List<LeaderboardEntryDto> target, LeaderboardEntryDto[] source)
        {
            if (source == null)
            {
                return;
            }
            foreach (var e in source)
            {
                if (e != null)
                {
                    target.Add(e);
                }
            }
        }

        private static string[] FriendIds(GetPlayerDto[] friends)
        {
            var ids = new List<string>(friends.Length);
            foreach (var f in friends)
            {
                if (f != null && !string.IsNullOrEmpty(f.PlayerId))
                {
                    ids.Add(f.PlayerId);
                }
            }
            return ids.ToArray();
        }

        private static bool IsEmpty(LeaderboardEntryDto[] entries)
        {
            return entries == null || entries.Length == 0;
        }

        private static string PlayerLabel(LeaderboardEntryDto e)
        {
            return string.IsNullOrWhiteSpace(e.playerName) ? Fmt.Id(e.playerId, 10) : e.playerName;
        }

        private static string BoardTitle(LeaderboardConfigDto cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.name))
            {
                return cfg.name;
            }
            return string.IsNullOrWhiteSpace(cfg.key) ? Fmt.Id(cfg.id) : cfg.key;
        }

        /// <summary>Null for anything below third place, so the chart falls back to the module accent.</summary>
        private static Color? MedalTint(int position)
        {
            switch (position)
            {
                case 1: return Gold;
                case 2: return Silver;
                case 3: return Bronze;
                default: return null;
            }
        }

        /// <summary>
        /// One board tab's mutable state. It exists because the pane is filled by two calls that can
        /// land in either order: whichever arrives re-renders the KPI strip from here.
        /// </summary>
        private sealed class BoardPane
        {
            public readonly LeaderboardConfigDto Config;
            public readonly KpiRow Kpis = new KpiRow();

            /// <summary>The player's own entry, or null when they have never scored on this board.</summary>
            public LeaderboardEntryDto Me;

            /// <summary>True once the entry call finished — tells "no score" apart from "still loading".</summary>
            public bool MeLoaded;

            /// <summary>Host of the standings, re-filled in place after a join, submit or leave.</summary>
            public readonly VisualElement EntriesSlot = new VisualElement();

            public LeaderboardEntryDto[] Rows = Array.Empty<LeaderboardEntryDto>();
            public int Entries;
            public bool EntriesLoaded;
            public DataTable Table;

            public BoardPane(LeaderboardConfigDto config)
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
                var e = row as LeaderboardEntryDto;
                return e != null && e.playerId == Me.playerId;
            }
        }
    }
}

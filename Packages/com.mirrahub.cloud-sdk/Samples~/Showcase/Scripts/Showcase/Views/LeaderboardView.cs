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
    /// Leaderboard detail, laid out the way a game would use the service: the project's boards in a
    /// sidebar, each with its own join / leave button, and the selected board on the right — one line
    /// of configuration, a card that submits a score, and the standings below it.
    /// <para>
    /// The standings card switches the <c>slice</c> of the ranking it requests — the player's table,
    /// the global top, the rows around the player, the friend list or the player's country. Each of
    /// those is a different SDK endpoint returning the same rows, which is exactly what this screen is
    /// meant to demonstrate. Every call takes the board's key. The last sidebar entry lists the rewards
    /// boards have paid out, which arrive in Economy.
    /// </para>
    /// <para>
    /// The SDK has no "is the player a participant" read: an entry proves it, but a participant
    /// without a score looks exactly like a stranger (404 on <c>GetLeaderboardPlayer</c>). So the
    /// sidebar remembers what it learned from join / leave / the player's own entry, and offers
    /// <c>Join</c> — which is idempotent — whenever it does not know.
    /// </para>
    /// </summary>
    public sealed class LeaderboardView : ServiceView
    {
        private const int TopCount = 100;
        private const int AroundRange = 10;

        // Index-aligned with the Slice enum.
        private static readonly string[] SliceNames = { "My table", "Global", "Around me", "Friends", "Country" };

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

        // Survive Refresh(): the rebuild re-reads the boards but keeps the reader where they were.
        private string _selectedKey;
        private bool _rewardsSelected;

        /// <summary>Participation learned this session, by board key. Absent means "not known".</summary>
        private readonly Dictionary<string, bool> _joined = new Dictionary<string, bool>();

        private BoardSidebar _side;
        private VisualElement _main;
        private BoardPane _pane;

        public LeaderboardView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        /// <summary>Which endpoint the standings card asks for. Order matches <see cref="SliceNames"/>.</summary>
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
            _side = null;
            _main = null;
            _pane = null;
            SetStatus(null);
            SetSubtitle("Pick a board on the left and join it, then submit a score. The standings switch "
                        + "between endpoints: your table, the whole board, your neighbours, friends or country.");

            UseToolbar()
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

            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Leaderboard.InitializeAsync(),
                slot,
                BuildScreen,
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

        private VisualElement NoBoards()
        {
            SetStatus("Not configured", ChipTone.Warn);
            return ZeroState.NotConfigured("Leaderboards");
        }

        // ----- layout: sidebar + selected board -------------------------------------------------------

        private const string RewardsId = "\u0001rewards";

        private VisualElement BuildScreen(LeaderboardConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 board" : configs.Length + " boards", ChipTone.Ok);

            _side = new BoardSidebar("Boards", _joined);

            // The board picked before a refresh, else the first one.
            LeaderboardConfigDto selected = null;
            foreach (var cfg in configs)
            {
                if (cfg == null)
                {
                    continue;
                }
                var captured = cfg;
                _side.AddBoard(cfg.key, BoardTitle(cfg), cfg.key, LucideIcon.Trophy,
                    () => SelectBoard(captured), () => Join(captured), () => ConfirmLeave(captured));

                if (selected == null || (cfg.key == _selectedKey && selected.key != _selectedKey))
                {
                    selected = cfg;
                }
            }

            _side.AddSection("Payouts");
            _side.AddLink(RewardsId, "Rewards", "paid into Economy on reset", LucideIcon.Gift, SelectRewards);

            var split = BoardLayout.Split(_side, out _main);
            if (_rewardsSelected || selected == null)
            {
                SelectRewards();
            }
            else
            {
                SelectBoard(selected);
            }
            return split;
        }

        private void SelectBoard(LeaderboardConfigDto cfg)
        {
            _rewardsSelected = false;
            _selectedKey = cfg.key;
            _side.Select(cfg.key);

            _pane = new BoardPane(cfg);
            _main.Clear();
            _main.Add(BuildHeadCard(_pane));
            _main.Add(BuildSubmitCard(_pane));
            _main.Add(BuildStandingsCard(_pane));

            LoadMyEntry(_pane);
            LoadSlice(_pane);
        }

        private void SelectRewards()
        {
            _rewardsSelected = true;
            _pane = null;
            _side.Select(RewardsId);

            _main.Clear();
            _main.Add(new PendingRewardsPanel(Ctx, RewardSourceType.Leaderboard,
                "When a board resets, the server works out what each place (or score) earned and pays it "
                + "into Economy, where the game reads and claims it. Nothing is claimed by reading.",
                "Nothing is waiting for this player. A reward lands here when a board resets and the "
                + "player's place (or score) falls in one of the board's reward ranges."));
        }

        // ----- board header: one line of configuration + the player's standing -----------------------

        private VisualElement BuildHeadCard(BoardPane pane)
        {
            var cfg = pane.Config;
            string tooltip = cfg.isReset
                ? string.Format(CultureInfo.InvariantCulture, "Resets at {0:00}:{1:00} UTC. Next: {2}",
                    cfg.resetTimeHour, cfg.resetTimeMinute, Fmt.DateTime2(cfg.nextResetDate))
                : "Updated " + RelativeTime.Format(cfg.updatedDate);
            // Filled once the board's first score started a session.
            DateTime? countdown = cfg.isReset && cfg.nextResetDate.HasValue
                ? cfg.nextResetDate.Value.ToUniversalTime()
                : (DateTime?)null;

            var card = BoardLayout.Head(LucideIcon.Trophy, Meta.Accent, BoardTitle(cfg), MetaLine(cfg), tooltip,
                countdown, pane.Standing);
            RenderStanding(pane);
            return card;
        }

        /// <summary>The board's configuration as one sentence-like line: order · type · strategy · key · reset.</summary>
        private static string MetaLine(LeaderboardConfigDto cfg)
        {
            var parts = new List<string>
            {
                cfg.orderType == OrderType.Lowest ? "Lowest first" : "Highest first",
                cfg.type == LeaderboardType.Time ? "Time" : cfg.type.ToString(),
                StrategyShort(cfg.updateStrategy),
            };
            if (cfg.cohortsEnabled)
            {
                parts.Add("cohorts of " + cfg.cohortSize);
            }
            if (cfg.rewardsForPlaces != null && cfg.rewardsForPlaces.Length > 0)
            {
                parts.Add(cfg.rewardsForPlaces.Length == 1 ? "1 reward range" : cfg.rewardsForPlaces.Length + " reward ranges");
            }
            parts.Add(cfg.isReset
                ? string.Format(CultureInfo.InvariantCulture, "resets {0} at {1:00}:{2:00} UTC",
                    cfg.resetIntervalType.ToString().ToLowerInvariant(), cfg.resetTimeHour, cfg.resetTimeMinute)
                : "never resets");
            if (!string.IsNullOrEmpty(cfg.key))
            {
                parts.Add("key " + cfg.key);
            }
            return string.Join("  ·  ", parts);
        }

        private static string StrategyShort(UpdateStrategy strategy)
        {
            switch (strategy)
            {
                case UpdateStrategy.Best: return "keeps best";
                case UpdateStrategy.Total: return "sums scores";
                default: return "keeps latest";
            }
        }

        /// <summary>Right edge of the header: "#3 / 1 250", "no score yet" or a dash while it loads.</summary>
        private void RenderStanding(BoardPane pane)
        {
            if (pane.Me != null && pane.Me.position > 0)
            {
                BoardLayout.RenderStanding(pane.Standing, "#" + pane.Me.position, BoardLayout.MedalTint(pane.Me.position),
                    "your place · " + FormatScore(pane.Config, pane.Me.value));
                return;
            }
            BoardLayout.RenderStanding(pane.Standing, Fmt.Dash, null, pane.MeLoaded ? "no score yet" : "loading…");
        }

        // ----- submit card -----------------------------------------------------------------------------

        private VisualElement BuildSubmitCard(BoardPane pane)
        {
            var card = BoardLayout.Card(LucideIcon.Send, "Submit a score", out _);
            card.Add(BoardLayout.Hint(SubmitHint(pane.Config)));

            var field = BoardLayout.Field(pane.Config.type == LeaderboardType.Time ? "Seconds" : "Score", "1000");
            card.Add(BoardLayout.FormRow("Submit", result => Submit(pane, field.value, result), field));
            return card;
        }

        private static string SubmitHint(LeaderboardConfigDto cfg)
        {
            string strategy;
            switch (cfg.updateStrategy)
            {
                case UpdateStrategy.Best:
                    strategy = "A worse score than the stored one leaves the entry alone.";
                    break;
                case UpdateStrategy.Total:
                    strategy = "Every submission adds to the entry.";
                    break;
                default:
                    strategy = "Every submission replaces the entry.";
                    break;
            }
            string unit = cfg.type == LeaderboardType.Time ? " A time board takes a TimeSpan, sent as seconds." : string.Empty;
            return "Join the board first — scores are accepted from participants only. " + strategy + unit;
        }

        private async Task Submit(BoardPane pane, string text, InlineResult result)
        {
            double score;
            // Parsed here rather than as a float: the SDK takes a double, and a score can easily be
            // larger than a float represents exactly.
            if (!double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out score))
            {
                result.Fail("Score must be a number (use a dot for decimals).");
                return;
            }

            string key = pane.Config.key;
            var op = pane.Config.type == LeaderboardType.Time
                ? Sdk.Leaderboard.SubmitScoreAsync(TimeSpan.FromSeconds(score), key)
                : Sdk.Leaderboard.SubmitScoreAsync(score, key);
            await op.Task();
            var response = op.Result;

            Ctx.Log?.Record("Leaderboard: submit score", response, SubmitSnippet);
            if (!response.IsSuccess)
            {
                bool notJoined = response.Error.HasCode(CloudErrorCodes.LeaderboardsParticipationRequired);
                if (notJoined)
                {
                    _side?.SetJoined(key, false);
                }
                result.Fail(notJoined
                    ? "Join the board first — scores are accepted from participants only."
                    : ErrorText(response));
                return;
            }

            _side?.SetJoined(key, true);
            var entry = response.Data;
            result.Ok(entry != null && entry.position > 0
                ? "You are #" + entry.position + " with " + FormatScore(pane.Config, entry.value)
                : "Submitted " + FormatScore(pane.Config, score));
            if (pane == _pane)
            {
                ReloadStandings(pane);
            }
        }

        // ----- standings card --------------------------------------------------------------------------

        private VisualElement BuildStandingsCard(BoardPane pane)
        {
            var card = BoardLayout.Card(LucideIcon.Users, "Standings", out var head);
            head.AddToClassList("sc-board-card__head--wrap");
            head.Add(pane.Count);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            head.Add(spacer);

            head.Add(BoardLayout.Switch(SliceNames, (int)_slice, index =>
            {
                _slice = (Slice)index;
                LoadSlice(pane);
            }));

            card.Add(pane.EntriesSlot);
            return card;
        }

        private void RenderCount(BoardPane pane)
        {
            BoardLayout.SetCount(pane.Count, pane.EntriesLoaded && pane.Entries > 0 ? Fmt.Number(pane.Entries) : null);
        }

        private void LoadSlice(BoardPane pane)
        {
            pane.Table = null;
            pane.EntriesLoaded = false;
            RenderCount(pane);

            var slot = pane.EntriesSlot;
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
                              + "with your first score. The Global slice shows every cohort."
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
            RenderCount(pane);

            pane.Table = new DataTable(Columns(pane))
                .WithZebra()
                .WithMaxHeight(480f)
                .WithSort(0, true)
                .Bind(rows, pane.IsMine);
            return pane.Table;
        }

        private VisualElement EmptySlice(BoardPane pane, string message)
        {
            pane.Rows = Array.Empty<LeaderboardEntryDto>();
            pane.Entries = 0;
            pane.EntriesLoaded = true;
            pane.Table = null;
            RenderCount(pane);

            // The board keeps the shape it will have once scores arrive — the reader sees the
            // columns they are going to get, not a shrug.
            return ZeroState.Table(Columns(pane), message);
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
                    Cell = row => ScoreCell(pane, row),
                    SortKey = row => ((LeaderboardEntryDto)row).value,
                },
            };
        }

        private static VisualElement RankCell(object row)
        {
            return BoardLayout.RankCell(((LeaderboardEntryDto)row).position);
        }

        private static VisualElement PlayerCell(BoardPane pane, object row)
        {
            var e = (LeaderboardEntryDto)row;
            return BoardLayout.PlayerCell(PlayerLabel(e), e.playerId + " · " + e.countryCode, pane.IsMine(row));
        }

        private static VisualElement ScoreCell(BoardPane pane, object row)
        {
            var e = (LeaderboardEntryDto)row;
            var label = new Label(FormatScore(pane.Config, e.value));
            label.AddToClassList("sc-score");
            // Fmt.Number compacts past 10k ("12.4k"), and on a ranking the exact figure is what
            // decides the order — keep it one hover away.
            label.tooltip = e.value.ToString("R", CultureInfo.InvariantCulture);
            return label;
        }

        /// <summary>A time board stores seconds; "1m 35s" reads better than "95.4" in a ranking.</summary>
        private static string FormatScore(LeaderboardConfigDto cfg, double value)
        {
            if (cfg.type == LeaderboardType.Time && !double.IsNaN(value) && !double.IsInfinity(value)
                && Math.Abs(value) < TimeSpan.MaxValue.TotalSeconds)
            {
                return Fmt.Duration(TimeSpan.FromSeconds(value));
            }
            return Fmt.Number(value);
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
                // than as a failed tile, so it is logged and the header falls back to "no score".
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
            RenderStanding(pane);

            // An entry proves participation; a 404 proves nothing (a participant without a score
            // gets it too), so only the positive answer updates the sidebar.
            if (pane.Me != null)
            {
                _side?.SetJoined(pane.Config.key, true);
            }

            // The table may have rendered before this landed, and it is the "You" row highlight that
            // depends on it — re-bind rather than leave the player unable to find themselves.
            if (pane.Me != null && pane.Table != null)
            {
                pane.Table.Bind(pane.Rows, pane.IsMine);
            }
        }

        /// <summary>Re-reads the player's entry and the current slice in place.</summary>
        private void ReloadStandings(BoardPane pane)
        {
            pane.Me = null;
            pane.MeLoaded = false;
            RenderStanding(pane);

            LoadMyEntry(pane);
            LoadSlice(pane);
        }

        // ----- join / leave from the sidebar ------------------------------------------------------------

        private async void Join(LeaderboardConfigDto cfg)
        {
            _side.SetBusy(cfg.key, true);

            RestApiResult<LeaderboardEntryDto> result = null;
            try
            {
                var op = Sdk.Leaderboard.JoinAsync(cfg.key);
                await op.Task();
                result = op.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Leaderboard: join threw: " + e.Message);
            }

            _side.SetBusy(cfg.key, false);
            if (result == null)
            {
                Toasts?.Fail("Join failed: no response");
                return;
            }

            Ctx.Log?.Record("Leaderboard: join", result, JoinSnippet);
            if (!result.IsSuccess)
            {
                Toasts?.Fail("Join failed: " + ErrorText(result));
                return;
            }

            _side.SetJoined(cfg.key, true);
            var entry = result.Data;
            Toasts?.Ok(entry != null && entry.position > 0
                ? "Joined " + BoardTitle(cfg) + " — you are #" + entry.position
                : "Joined " + BoardTitle(cfg) + " — submit a score to get a place");
            ReloadIfSelected(cfg.key);
        }

        private void ConfirmLeave(LeaderboardConfigDto cfg)
        {
            ConfirmDialog.Open(Popup, "Leave " + BoardTitle(cfg),
                "The player stops being a participant, and their score of the current session is removed "
                + "from the board.",
                "Leave", () => Leave(cfg));
        }

        private async void Leave(LeaderboardConfigDto cfg)
        {
            _side.SetBusy(cfg.key, true);

            RestApiResult result = null;
            try
            {
                var op = Sdk.Leaderboard.LeaveAsync(cfg.key);
                await op.Task();
                result = op.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Leaderboard: leave threw: " + e.Message);
            }

            _side.SetBusy(cfg.key, false);
            if (result == null)
            {
                Toasts?.Fail("Leave failed: no response");
                return;
            }

            Ctx.Log?.Record("Leaderboard: leave", result, LeaveSnippet);
            if (!result.IsSuccess)
            {
                Toasts?.Fail("Leave failed: " + ErrorText(result));
                return;
            }

            _side.SetJoined(cfg.key, false);
            Toasts?.Ok("Left " + BoardTitle(cfg) + " — your score of this session is gone");
            ReloadIfSelected(cfg.key);
        }

        private void ReloadIfSelected(string key)
        {
            if (_pane != null && _pane.Config.key == key)
            {
                ReloadStandings(_pane);
            }
        }

        // ----- helpers ----------------------------------------------------------------------------------

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

        /// <summary>
        /// The selected board's mutable state. It exists because the right side is filled by two
        /// calls that can land in either order: whichever arrives re-renders its part from here.
        /// </summary>
        private sealed class BoardPane
        {
            public readonly LeaderboardConfigDto Config;

            /// <summary>Right edge of the header card: the player's place and score.</summary>
            public readonly VisualElement Standing = new VisualElement();

            /// <summary>Row count badge next to the standings title.</summary>
            public readonly Label Count = BoardLayout.Count();

            /// <summary>The player's own entry, or null when they have never scored on this board.</summary>
            public LeaderboardEntryDto Me;

            /// <summary>True once the entry call finished — tells "no score" apart from "still loading".</summary>
            public bool MeLoaded;

            /// <summary>Host of the standings, re-filled in place after a submit, join or leave.</summary>
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

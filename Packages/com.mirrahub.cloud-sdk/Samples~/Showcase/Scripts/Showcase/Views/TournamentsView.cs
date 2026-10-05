using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Core.Errors;
using MirraCloud.Core.Friends.Dto;
using MirraCloud.Core.Leaderboard.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Tournaments.Dto;
using UnityEngine;
using UnityEngine.UIElements;

// A league's reward ranges reuse the leaderboard's RewardDataDto.
using TournamentEnums = MirraCloud.Core.Tournaments.Enums;
using RewardDistribution = MirraCloud.Core.Leaderboard.Enums.RewardDistributionType;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Tournaments detail, laid out like the leaderboard screen: the project's tournaments in a
    /// sidebar, each with its own join / leave button, and the selected one on the right — one line
    /// of configuration with the player's place, a card that submits a score, the standings of one
    /// league, and the league ladder with its thresholds and rewards.
    /// <para>
    /// A tournament is a leaderboard cut into league tables, so opening one first asks which league
    /// the player sits in (<c>GetPlayerLeagueMetaAsync</c>), then fills that league's standings from
    /// whichever entries endpoint the standings card's switch selects. The last sidebar entry lists
    /// the rewards finished runs paid out, which arrive in Economy.
    /// </para>
    /// <para>
    /// Like leaderboards, the service has no "is the player a participant" read, so the sidebar keeps
    /// what join / leave / the player's own entry taught it and offers <c>Join</c> — idempotent —
    /// otherwise.
    /// </para>
    /// </summary>
    public sealed class TournamentsView : ServiceView
    {
        private const int TopCount = 100;
        private const int AroundRange = 10;

        // Index-aligned with the Slice enum.
        private static readonly string[] SliceNames = { "Top", "Around me", "Top + around", "Friends", "Country" };

        private const string ConfigsSnippet = @"// every tournament configured for this project + branch
var op = sdk.Tournaments.InitializeAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var cfg in op.Result.Data)
{
    // cfg.key is what every other call takes; cfg.tables are its league tables
    Debug.Log(cfg.key + "" / "" + cfg.name + "" / "" + cfg.tables.Length + "" leagues"");
}
// the service keeps them too: sdk.Tournaments.TournamentConfigs";

        private const string LeagueSnippet = @"// which league table the player sits in — every entries call needs a tableId,
// and this is where it comes from
var op = sdk.Tournaments.GetPlayerLeagueMetaAsync(tournamentKey);
await op.Task();

if (op.Result.IsSuccess)
{
    PlayerLeagueMetaDto meta = op.Result.Data;
    // meta.currentLeagueTableId / meta.currentLeagueTableIndex
}";

        private const string TopSnippet = @"// the league's ranking, best entries first
var op = sdk.Tournaments.GetTopAsync(tournamentKey, tableId, 100);
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var e in op.Result.Data.entries)
{
    Debug.Log(e.position + "". "" + e.playerName + "" = "" + e.value);
}";

        private const string CountrySnippet = @"// same league, narrowed to the country on the player's account
var op = sdk.Tournaments.GetTopByCountryAsync(tournamentKey, tableId, 100);
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

var op = sdk.Tournaments.GetTopByFriendsAsync(tournamentKey, tableId, ids.ToArray());
await op.Task();";

        private const string AroundSnippet = @"// the player's neighbourhood inside the league: 10 rows above and below their own
var op = sdk.Tournaments.GetAroundAsync(tournamentKey, tableId, 10);
await op.Task();

var data = op.Result.Data;
// data.pLayersAbove (SDK spelling) / data.targetPlayer / data.playersBelow
Debug.Log(data.targetPlayer != null ? data.targetPlayer.position.ToString() : ""unranked"");";

        private const string TopAndAroundSnippet = @"// the head of the league and the player's neighbourhood in one round trip —
// what a tournament screen usually needs to draw
var op = sdk.Tournaments.GetTopAndAroundAsync(tournamentKey, tableId, 100, 10);
await op.Task();

var data = op.Result.Data;
// data.top                     : the ranking
// data.playersAround           : pLayersAbove / targetPlayer / playersBelow";

        private const string MeSnippet = @"// the signed-in player's own row in this league table
var op = sdk.Tournaments.GetPlayerAsync(tournamentKey, tableId);
await op.Task();

// a player who has never submitted a score simply has no entry here
if (op.Result.IsSuccess && op.Result.Data != null)
{
    Debug.Log(""#"" + op.Result.Data.position + "" with "" + op.Result.Data.value);
}";

        private const string SubmitSnippet = @"// one score for the whole tournament: the server files it under the league table the
// player currently sits in, following the tournament's update strategy (best / latest / total)
var op = sdk.Tournaments.SubmitScoreAsync(tournamentKey, 1250d);
await op.Task();
if (!op.Result.IsSuccess) { return; }

// playerName is optional — left out, the SDK sends the nickname from PlayerAccount
await sdk.Tournaments.SubmitScoreAsync(tournamentKey, 1250d, ""Ada"").Task();";

        private const string JoinSnippet = @"// scores are accepted from participants only: join once — the server places the
// player into a league — before the first submit
var op = sdk.Tournaments.JoinAsync(tournamentKey);
await op.Task();";


        private const string LeaveSnippet = @"// removes the player from the tournament along with their result
await sdk.Tournaments.LeaveAsync(tournamentKey).Task();";

        private const string RewardsId = "\u0001rewards";

        /// <summary>Which league each tournament is being read at, so a refresh or a slice change
        /// does not drop the reader back onto the player's own league.</summary>
        private readonly Dictionary<string, string> _leagueByTournament =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Participation learned this session, by tournament key. Absent means "not known".</summary>
        private readonly Dictionary<string, bool> _joined = new Dictionary<string, bool>();

        private Slice _slice = Slice.Top;

        // Survive Refresh(): the rebuild re-reads the configs but keeps the reader where they were.
        private string _selectedKey;
        private bool _rewardsSelected;

        private BoardSidebar _side;
        private VisualElement _main;
        private TournamentPane _pane;

        public TournamentsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        /// <summary>Which entries endpoint the standings card asks for. Order matches <see cref="SliceNames"/>.</summary>
        private enum Slice
        {
            Top,
            AroundMe,
            TopAndAround,
            Friends,
            Country,
        }

        protected override void Populate()
        {
            _side = null;
            _main = null;
            _pane = null;
            SetStatus(null);
            SetSubtitle("Pick a tournament on the left and join it — the server places you into a league — "
                        + "then submit a score. The standings show one league at a time.");

            UseToolbar()
                .WithSpacer()
                .WithRefresh(Refresh);

            DeclareCall(new SdkCall("List tournaments", ConfigsSnippet,
                "Call it once at startup: every other tournament call needs a key from here."));
            DeclareCall(new SdkCall("Join a tournament", JoinSnippet,
                "Scores are accepted from participants only."));
            DeclareCall(new SdkCall("Submit a score", SubmitSnippet));
            DeclareCall(new SdkCall("Leave a tournament", LeaveSnippet));
            DeclareCall(new SdkCall("The player's league", LeagueSnippet,
                "The server assigns a default league the first time this is asked, so it answers even "
                + "for a player who has never played."));
            DeclareCall(new SdkCall("Top of a league", TopSnippet));
            DeclareCall(new SdkCall("Entries around the player", AroundSnippet));
            DeclareCall(new SdkCall("Top and around in one call", TopAndAroundSnippet));
            DeclareCall(new SdkCall("Entries among friends", FriendsSnippet));
            DeclareCall(new SdkCall("Entries by country", CountrySnippet));
            DeclareCall(new SdkCall("The player's own entry", MeSnippet,
                "Returns no entry until the player has submitted a score to this tournament."));
            DeclareCall(new SdkCall("Rewards the tournaments paid out", PendingRewardsPanel.ReadSnippet,
                "Finished runs pay out into Economy."));
            DeclareCall(new SdkCall("Claim the rewards", PendingRewardsPanel.ClaimSnippet));

            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Tournaments.InitializeAsync(),
                slot,
                BuildScreen,
                isEmpty: c => c == null || c.Length == 0,
                options: new BindOptions
                {
                    Label = "Tournament configs",
                    ServiceName = "Tournament",
                    // this is the tournament *configuration* call, so a 404 really does mean
                    // "no tournaments exist in this project"
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoTournaments,
                });
        }

        private VisualElement NoTournaments()
        {
            SetStatus("Not configured", ChipTone.Warn);
            return ZeroState.NotConfigured("Tournaments");
        }

        // ----- layout: sidebar + selected tournament -------------------------------------------------

        private VisualElement BuildScreen(TournamentConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 tournament" : configs.Length + " tournaments", ChipTone.Ok);

            _side = new BoardSidebar("Tournaments", _joined);

            // The tournament picked before a refresh, else the first one.
            TournamentConfigDto selected = null;
            foreach (var cfg in configs)
            {
                if (cfg == null)
                {
                    continue;
                }
                var captured = cfg;
                string key = Key(cfg);
                _side.AddBoard(key, Title(cfg), SidebarSubtitle(cfg), LucideIcon.Swords,
                    () => SelectTournament(captured), () => Join(captured), () => ConfirmLeave(captured));

                if (selected == null || (key == _selectedKey && Key(selected) != _selectedKey))
                {
                    selected = cfg;
                }
            }

            _side.AddSection("Payouts");
            _side.AddLink(RewardsId, "Rewards", "paid into Economy when a run ends", LucideIcon.Gift, SelectRewards);

            var split = BoardLayout.Split(_side, out _main);
            if (_rewardsSelected || selected == null)
            {
                SelectRewards();
            }
            else
            {
                SelectTournament(selected);
            }
            return split;
        }

        private static string SidebarSubtitle(TournamentConfigDto cfg)
        {
            int leagues = cfg.tables != null ? cfg.tables.Length : 0;
            string count = leagues == 1 ? "1 league" : leagues + " leagues";
            return string.IsNullOrEmpty(cfg.key) ? count : cfg.key + " · " + count;
        }

        private void SelectTournament(TournamentConfigDto cfg)
        {
            _rewardsSelected = false;
            _selectedKey = Key(cfg);
            _side.Select(_selectedKey);

            var pane = new TournamentPane(cfg);
            _pane = pane;
            bool hasLeagues = cfg.tables != null && cfg.tables.Length > 0;

            _main.Clear();
            _main.Add(BuildHeadCard(pane));
            _main.Add(BuildSubmitCard(pane));
            _main.Add(BuildStandingsCard(pane));
            if (hasLeagues)
            {
                _main.Add(BuildLeaguesCard(pane));
                OpenPane(pane);
            }
            else
            {
                // Nothing will ever be loaded, so the header settles instead of sitting on "loading".
                pane.MeLoaded = true;
                pane.MetaLoaded = true;
                RenderStanding(pane);
                Replace(pane.EntriesSlot, ZeroState.Panel(LucideIcon.Layers, "No league tables yet",
                    "A tournament ranks players inside a league table, and this one has none — every "
                    + "entries endpoint needs a table id, so there is nothing to read. Add a league to "
                    + "this tournament in the Mirra Hub console.",
                    null, null,
                    "Leagues also carry the promotion thresholds and the rewards for each place."));
            }
        }

        private void SelectRewards()
        {
            _rewardsSelected = true;
            _pane = null;
            _side.Select(RewardsId);

            _main.Clear();
            _main.Add(new PendingRewardsPanel(Ctx, RewardSourceType.Tournament,
                "When a run resets, the server settles the final standings of every league, works out what "
                + "each place (or score) earned and pays it into Economy, where the game reads and claims it. "
                + "Nothing is claimed by reading.",
                "Nothing is waiting for this player. A reward lands here when a tournament run resets and the "
                + "player's place (or score) matches one of the ranges configured on their league."));
        }

        // ----- header: one line of configuration + the player's place --------------------------------

        private VisualElement BuildHeadCard(TournamentPane pane)
        {
            var cfg = pane.Config;
            // The run ending is when places are settled and rewards are handed out.
            DateTime? countdown = cfg.isReset && cfg.nextResetDate.HasValue
                ? cfg.nextResetDate.Value.ToUniversalTime()
                : (DateTime?)null;
            string tooltip = countdown.HasValue
                ? "The run ends (and rewards are granted) at " + Fmt.DateTime2(cfg.nextResetDate)
                : "Updated " + RelativeTime.Format(cfg.updatedDate);

            var card = BoardLayout.Head(LucideIcon.Swords, Meta.Accent, Title(cfg), MetaLine(cfg), tooltip,
                countdown, pane.Standing);
            RenderStanding(pane);
            return card;
        }

        /// <summary>The configuration as one line: order · type · strategy · rewards · leagues · reset · key.</summary>
        private static string MetaLine(TournamentConfigDto cfg)
        {
            int leagues = cfg.tables != null ? cfg.tables.Length : 0;
            var parts = new List<string>
            {
                cfg.orderType == TournamentEnums.OrderType.Highest ? "Highest first" : "Lowest first",
                cfg.type == TournamentEnums.TournamentsType.Time ? "Time" : "Score",
                StrategyShort(cfg.updateStrategy),
                "rewards by " + (cfg.rewardDistributionType == RewardDistribution.ByScore ? "score" : "place"),
                leagues == 1 ? "1 league" : leagues + " leagues",
            };
            if (cfg.isReset)
            {
                string unit = cfg.resetIntervalType.ToString().ToLowerInvariant();
                parts.Add(cfg.resetIntervalValue > 1
                    ? "resets every " + cfg.resetIntervalValue + " " + unit
                    : "resets " + unit);
            }
            else
            {
                parts.Add("never resets");
            }
            if (!string.IsNullOrEmpty(cfg.key))
            {
                parts.Add("key " + cfg.key);
            }
            return string.Join("  ·  ", parts);
        }

        private static string StrategyShort(TournamentEnums.UpdateStrategy strategy)
        {
            switch (strategy)
            {
                case TournamentEnums.UpdateStrategy.Best: return "keeps best";
                case TournamentEnums.UpdateStrategy.Total: return "sums scores";
                default: return "keeps latest";
            }
        }

        /// <summary>Right edge of the header: the place in the league on screen, and the player's league.</summary>
        private void RenderStanding(TournamentPane pane)
        {
            string league = MyLeagueText(pane);
            if (pane.Me != null && pane.Me.position > 0)
            {
                BoardLayout.RenderStanding(pane.Standing, "#" + pane.Me.position,
                    BoardLayout.MedalTint(pane.Me.position),
                    Fmt.Number(pane.Me.value) + (league != null ? " · " + league : string.Empty));
                return;
            }

            string caption = pane.MeLoaded ? "no score yet" : "loading…";
            if (league != null)
            {
                caption += " · " + league;
            }
            BoardLayout.RenderStanding(pane.Standing, Fmt.Dash, null, caption);
        }

        /// <summary>The player's league by name, or null while it is unknown.</summary>
        private static string MyLeagueText(TournamentPane pane)
        {
            if (pane.Meta == null)
            {
                return null;
            }
            var tables = pane.Config.tables;
            int at = IndexOfTable(tables, pane.Meta.currentLeagueTableId);
            // The config the client holds may not list the league the meta points at (it was removed,
            // or the player's run predates it) — the server's own index is still worth showing.
            return at >= 0 ? LeagueName(tables[at], at) : "league " + (pane.Meta.currentLeagueTableIndex + 1);
        }

        // ----- submit card ---------------------------------------------------------------------------

        private VisualElement BuildSubmitCard(TournamentPane pane)
        {
            var card = BoardLayout.Card(LucideIcon.Send, "Submit a score", out _);
            card.Add(BoardLayout.Hint(SubmitHint(pane.Config)));

            var score = BoardLayout.Field("Score", "1000", 220f);
            var name = BoardLayout.Field("Name", string.Empty, 240f);
            name.tooltip = "Optional. Left blank, the SDK sends the nickname from PlayerAccount.";
            card.Add(BoardLayout.FormRow("Submit", result => Submit(pane, score.value, name.value, result), score, name));
            return card;
        }

        private static string SubmitHint(TournamentConfigDto cfg)
        {
            string strategy;
            switch (cfg.updateStrategy)
            {
                case TournamentEnums.UpdateStrategy.Best:
                    strategy = "A worse score than the stored one leaves the entry alone.";
                    break;
                case TournamentEnums.UpdateStrategy.Total:
                    strategy = "Every submission adds to the entry.";
                    break;
                default:
                    strategy = "Every submission replaces the entry.";
                    break;
            }
            string unit = cfg.type == TournamentEnums.TournamentsType.Time
                ? " The value is a duration in whatever unit the game measures in."
                : string.Empty;
            return "Join first. There is no table id: the score goes to the league the player sits in. "
                   + strategy + unit + " Name is optional — blank sends the profile's nickname.";
        }

        private async Task Submit(TournamentPane pane, string scoreText, string name, InlineResult result)
        {
            double score;
            // Parsed here rather than as a float: the SDK takes a double, and a tournament score can
            // easily be larger than a float represents exactly.
            if (!double.TryParse((scoreText ?? string.Empty).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out score))
            {
                result.Fail("Score must be a number (use a dot for decimals).");
                return;
            }

            string key = Key(pane.Config);
            var op = Sdk.Tournaments.SubmitScoreAsync(key, score,
                string.IsNullOrWhiteSpace(name) ? null : name.Trim());
            await op.Task();
            var response = op.Result;

            if (!response.IsSuccess)
            {
                bool notJoined = response.Error != null
                                 && response.Error.HasCode(CloudErrorCodes.TournamentsParticipationRequired);
                if (notJoined)
                {
                    _side?.SetJoined(key, false);
                }
                result.Fail(notJoined
                    ? "Join the tournament first — scores are accepted from participants only."
                    : ErrorText(response));
                return;
            }

            _side?.SetJoined(key, true);
            result.Ok("Submitted " + Fmt.Number(score));
            if (pane == _pane)
            {
                ReloadStandings(pane);
            }
        }

        // ----- standings card: league picker + slice switch + table ----------------------------------

        private VisualElement BuildStandingsCard(TournamentPane pane)
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
                if (!string.IsNullOrEmpty(pane.TableId))
                {
                    LoadSlice(pane);
                }
            }));

            pane.LeaguePicker.AddToClassList("sc-trn-picker");
            card.Add(pane.LeaguePicker);
            card.Add(pane.EntriesSlot);
            return card;
        }

        private static void RenderCount(TournamentPane pane)
        {
            BoardLayout.SetCount(pane.Count, pane.EntriesLoaded && pane.Entries > 0 ? Fmt.Number(pane.Entries) : null);
        }

        /// <summary>
        /// Opens a tournament: the league comes first because every entries endpoint needs a table id,
        /// and only then are the standings and the player's own row requested. Bound by hand rather
        /// than through <see cref="ViewBind"/> — the response picks the table rather than filling a slot.
        /// </summary>
        private async void OpenPane(TournamentPane pane)
        {
            Skeleton.Into(pane.EntriesSlot);

            RestApiResult<PlayerLeagueMetaDto> result = null;
            try
            {
                var op = Sdk.Tournaments.GetPlayerLeagueMetaAsync(Key(pane.Config));
                if (op != null)
                {
                    await op.Task();
                    result = op.Result;
                }
            }
            catch (Exception e)
            {
                // async void: an exception escaping here would surface as an unhandled one rather
                // than as a pane that fell back to the first league.
                Debug.LogWarning("[Showcase] Tournaments: reading the player's league failed: " + e.Message);
            }

            if (result != null)
            {
                if (result.IsSuccess)
                {
                    pane.Meta = result.Data;
                }
            }
            pane.MetaLoaded = true;

            SelectLeague(pane, PickLeague(pane));
        }

        /// <summary>The league to read: the one the reader last picked, else the player's own, else
        /// the first the config lists.</summary>
        private string PickLeague(TournamentPane pane)
        {
            var tables = pane.Config.tables;
            string remembered;
            if (_leagueByTournament.TryGetValue(Key(pane.Config), out remembered)
                && IndexOfTable(tables, remembered) >= 0)
            {
                return remembered;
            }

            string mine = pane.Meta != null ? pane.Meta.currentLeagueTableId : null;
            if (IndexOfTable(tables, mine) >= 0)
            {
                return mine;
            }
            return tables[0] != null ? tables[0].id : null;
        }

        private void SelectLeague(TournamentPane pane, string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
            {
                Replace(pane.EntriesSlot, ErrorState.Message(
                    "This tournament's leagues carry no table id, so the standings cannot be addressed."));
                return;
            }

            pane.TableId = tableId;
            _leagueByTournament[Key(pane.Config)] = tableId;

            pane.Me = null;
            pane.MeLoaded = false;
            pane.HighlightId = null;
            RenderLeagues(pane);
            RenderStanding(pane);
            LoadMyEntry(pane);
            LoadSlice(pane);
        }

        /// <summary>Repaints the league picker and the ladder; both mark the player's own league, which
        /// is only known once the meta call has landed, and the one on screen.</summary>
        private void RenderLeagues(TournamentPane pane)
        {
            var tables = pane.Config.tables;
            pane.LeaguePicker.Clear();
            pane.LeaguesSlot.Clear();
            if (tables == null || tables.Length == 0)
            {
                return;
            }

            var caption = new Label("League");
            caption.AddToClassList("sc-trn-picker__label");
            pane.LeaguePicker.Add(caption);

            string mine = pane.Meta != null ? pane.Meta.currentLeagueTableId : null;
            for (int i = 0; i < tables.Length; i++)
            {
                var table = tables[i];
                if (table == null)
                {
                    continue;
                }

                string id = table.id;
                bool isMine = !string.IsNullOrEmpty(id) && id == mine;
                bool shown = !string.IsNullOrEmpty(id) && id == pane.TableId;

                var btn = new Button(() => SelectLeague(pane, id));
                btn.AddToClassList("sc-btn");
                btn.AddToClassList("sc-trn-league");
                btn.EnableInClassList("sc-btn--primary", shown);
                btn.tooltip = isMine ? "The league this player is in right now" : "Read this league's standings";
                if (isMine)
                {
                    var crown = new Label(LucideIcon.Crown);
                    crown.AddToClassList("sc-trn-league__glyph");
                    crown.AddToClassList("sc-icon");
                    btn.Add(crown);
                }
                var name = new Label(LeagueName(table, i));
                name.enableRichText = false;
                name.AddToClassList("sc-trn-league__name");
                btn.Add(name);
                pane.LeaguePicker.Add(btn);

                pane.LeaguesSlot.Add(LeagueRow(pane, table, i, isMine, shown));
            }
        }

        // ----- leagues card: the ladder with thresholds and rewards ----------------------------------

        private VisualElement BuildLeaguesCard(TournamentPane pane)
        {
            var card = BoardLayout.Card(LucideIcon.Layers, "Leagues and rewards", out var head);
            var count = BoardLayout.Count();
            BoardLayout.SetCount(count, pane.Config.tables.Length.ToString(CultureInfo.InvariantCulture));
            head.Add(count);
            card.Add(BoardLayout.Hint("On a reset the best players of a league move up, the worst move down, "
                                      + "and every place or score in a reward range is paid into Economy."));
            card.Add(pane.LeaguesSlot);
            return card;
        }

        /// <summary>One league as a row: name and promotion rules on the left, reward ranges on the right.
        /// A click shows its standings above.</summary>
        private VisualElement LeagueRow(TournamentPane pane, TournamentTableDto table, int index, bool isMine, bool shown)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-trn-lrow");
            row.EnableInClassList("sc-trn-lrow--shown", shown);

            var texts = new VisualElement();
            texts.AddToClassList("sc-trn-lrow__texts");

            var title = new VisualElement();
            title.AddToClassList("sc-trn-lrow__title");
            var name = new Label(LeagueName(table, index));
            name.enableRichText = false;
            name.AddToClassList("sc-trn-lrow__name");
            title.Add(name);
            if (isMine)
            {
                var you = new Badge("your league", ChipTone.Accent);
                you.AddToClassList("sc-trn-lrow__badge");
                title.Add(you);
            }
            texts.Add(title);

            // Thresholds are counts of players, not places: on a reset the best N of this league move
            // up to the neighbouring one and the worst M drop down.
            var rules = new Label(
                (table.leagueUpThreshold > 0 ? "top " + table.leagueUpThreshold + " move up" : "no promotion")
                + "  ·  "
                + (table.leagueDownThreshold > 0 ? "bottom " + table.leagueDownThreshold + " move down" : "no demotion"));
            rules.AddToClassList("sc-trn-lrow__rules");
            texts.Add(rules);
            row.Add(texts);

            row.Add(RewardRanges(pane.Config, table.rewardsForPlaces));

            string id = table.id;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(id) && id != pane.TableId)
                {
                    SelectLeague(pane, id);
                }
            });
            return row;
        }

        /// <summary>
        /// The league's reward ranges as pills, "#1–3 · 4f2a…×10". The kind (currency or item) is
        /// deliberately not claimed: the endpoint reports the economy resource id and the amount, and
        /// which of the two it is comes from looking that id up in the Economy module.
        /// </summary>
        private VisualElement RewardRanges(TournamentConfigDto cfg, RewardRangeDto[] ranges)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");
            row.AddToClassList("sc-trn-lrow__rewards");

            if (ranges != null)
            {
                foreach (var range in ranges)
                {
                    if (range == null)
                    {
                        continue;
                    }
                    var parts = new List<string>();
                    var full = new List<string>();
                    if (range.rewards != null)
                    {
                        foreach (var reward in range.rewards)
                        {
                            if (reward == null)
                            {
                                continue;
                            }
                            parts.Add(Fmt.Id(reward.rewardId, 6) + " ×" + reward.count);
                            full.Add(Fmt.OrDash(reward.rewardId) + " ×" + reward.count);
                        }
                    }
                    var chip = new RewardChip(LucideIcon.Gift,
                        RangeLabel(cfg, range) + "  " + (parts.Count == 0 ? "nothing" : string.Join(", ", parts)),
                        Meta.Accent);
                    chip.tooltip = "Economy resources: " + (full.Count == 0 ? "none" : string.Join(", ", full));
                    row.Add(chip);
                }
            }

            if (row.childCount == 0)
            {
                row.Add(new Chip("no rewards", ChipTone.Neutral));
            }
            return row;
        }

        /// <summary>
        /// What a reward range applies to. The tournaments service puts the range in
        /// <c>valueMin</c>/<c>valueMax</c> for both distribution types — places when the tournament
        /// pays by place, raw scores when it pays by score — so the config decides how to read them.
        /// </summary>
        private static string RangeLabel(TournamentConfigDto cfg, RewardRangeDto range)
        {
            bool byScore = cfg.rewardDistributionType == RewardDistribution.ByScore;
            string min = Fmt.Number(range.valueMin);
            string max = Fmt.Number(range.valueMax);
            bool single = Math.Abs(range.valueMax - range.valueMin) < 0.0001d;

            if (byScore)
            {
                return single ? "score " + min : "score " + min + "–" + max;
            }
            return single ? "#" + min : "#" + min + "–" + max;
        }

        // ----- standings -----------------------------------------------------------------------------

        private void LoadSlice(TournamentPane pane)
        {
            var slot = pane.EntriesSlot;
            string key = Key(pane.Config);
            string table = pane.TableId;
            pane.Bound.Clear();
            pane.EntriesLoaded = false;
            RenderCount(pane);

            switch (_slice)
            {
                case Slice.AroundMe:
                    BindSlice(pane, slot,
                        () => Sdk.Tournaments.GetAroundAsync(key, table, AroundRange),
                        d =>
                        {
                            // AdoptTarget works on a flat ranked list, and the around-me response is
                            // three separate arrays — flatten first, then adopt.
                            var rows = Around(d);
                            AdoptTarget(pane, rows);
                            return rows;
                        },
                        "Tournament around me", AroundSnippet,
                        "You have no entry in this league yet. Submit a score and the players just above "
                        + "and below you show up here.");
                    return;

                case Slice.TopAndAround:
                    BindTopAndAround(pane, slot, key, table);
                    return;

                case Slice.Friends:
                    LoadFriendsSlice(pane, slot, key, table);
                    return;

                case Slice.Country:
                    BindSlice(pane, slot,
                        () => Sdk.Tournaments.GetTopByCountryAsync(key, table, TopCount),
                        d => d?.entries, "Tournament top by country", CountrySnippet,
                        "Nobody from your country has scored in this league yet. Entries appear after the "
                        + "first SubmitScoreAsync from an account with the same country.");
                    return;

                default:
                    BindSlice(pane, slot,
                        () => Sdk.Tournaments.GetTopAsync(key, table, TopCount),
                        d => d?.entries, "Tournament top", TopSnippet,
                        "This league has no entries yet. The first SubmitScoreAsync against the tournament "
                        + "creates the standings, and every later score updates them.");
                    return;
            }
        }

        /// <summary>Shared binding for the single-table slices: same rows, same table, different endpoint.</summary>
        private void BindSlice<T>(TournamentPane pane, VisualElement slot,
            Func<AsyncOperation<RestApiResult<T>>> start, Func<T, TournamentEntryDto[]> rows,
            string label, string snippet, string emptyMessage)
        {
            ViewBind.Load(
                start,
                slot,
                data => BuildEntries(pane, rows(data)),
                isEmpty: data => IsEmpty(rows(data)),
                options: new BindOptions
                {
                    Label = label,
                    ServiceName = "Tournament",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane, emptyMessage),
                });
        }

        /// <summary>
        /// The one slice that answers with two lists, which is the whole reason the endpoint exists:
        /// the head of the league for the podium and the player's neighbourhood for their own row.
        /// </summary>
        private void BindTopAndAround(TournamentPane pane, VisualElement slot, string key, string table)
        {
            ViewBind.Load(
                () => Sdk.Tournaments.GetTopAndAroundAsync(key, table, TopCount, AroundRange),
                slot,
                data => BuildTopAndAroundBody(pane, data),
                isEmpty: data => IsEmpty(data?.top) && IsEmpty(Around(data?.playersAround)),
                options: new BindOptions
                {
                    Label = "Tournament top and around",
                    ServiceName = "Tournament",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane,
                        "This league has neither a ranking nor an entry for you yet. Both halves of this "
                        + "response fill up from the first submitted score."),
                });
        }

        /// <summary>
        /// The friends slice is the only two-step one: the endpoint ranks exactly the ids it is
        /// given, so the friend list has to be fetched first.
        /// </summary>
        private void LoadFriendsSlice(TournamentPane pane, VisualElement slot, string key, string table)
        {
            ViewBind.Load(
                () => Sdk.Friends.GetFriendsAsync(false),
                slot,
                friends =>
                {
                    var inner = new VisualElement();
                    BindSlice(pane, inner,
                        () => Sdk.Tournaments.GetTopByFriendsAsync(key, table, FriendIds(friends)),
                        d => d?.entries, "Tournament top by friends", FriendsSnippet,
                        "None of your friends has scored in this league yet.");
                    return inner;
                },
                isEmpty: f => f == null || f.Length == 0,
                options: new BindOptions
                {
                    Label = "Friends list",
                    ServiceName = "Friends",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane,
                        "This slice ranks only the players on your friend list, and it is empty. Add a "
                        + "friend in the Friends module first."),
                });
        }

        private VisualElement BuildEntries(TournamentPane pane, TournamentEntryDto[] entries)
        {
            var rows = entries ?? Array.Empty<TournamentEntryDto>();
            pane.Entries = rows.Length;
            pane.EntriesLoaded = true;
            RenderCount(pane);
            return BuildTable(pane, rows, 480f);
        }

        private VisualElement BuildTopAndAroundBody(TournamentPane pane, TournamentTopAndPlayersAroundDto data)
        {
            var top = data != null && data.top != null ? data.top : Array.Empty<TournamentEntryDto>();
            var around = Around(data?.playersAround);

            pane.Entries = top.Length + around.Length;
            pane.EntriesLoaded = true;
            AdoptTarget(pane, around);
            RenderCount(pane);

            var root = new VisualElement();

            root.Add(Subheader("Top of the league", top.Length));
            root.Add(top.Length == 0
                ? ZeroState.Table(Columns(pane), "Nobody has scored in this league yet.")
                : (VisualElement)BuildTable(pane, top, 320f));

            var aroundHeader = Subheader("Around you", around.Length);
            aroundHeader.AddToClassList("sc-trn-sub--gap");
            root.Add(aroundHeader);
            root.Add(around.Length == 0
                ? ZeroState.Table(Columns(pane),
                    "You have no position in this league, so there is no neighbourhood to show. It "
                    + "appears as soon as you have an entry.")
                : (VisualElement)BuildTable(pane, around, 320f));
            return root;
        }

        private static VisualElement Subheader(string text, int count)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-trn-sub");
            var label = new Label(text);
            label.AddToClassList("sc-trn-sub__text");
            row.Add(label);
            var badge = BoardLayout.Count();
            BoardLayout.SetCount(badge, count.ToString(CultureInfo.InvariantCulture));
            row.Add(badge);
            return row;
        }

        private VisualElement EmptySlice(TournamentPane pane, string message)
        {
            pane.Entries = 0;
            pane.EntriesLoaded = true;
            pane.Bound.Clear();
            RenderCount(pane);

            // The board keeps the shape it will have once scores arrive — the reader sees the
            // columns they are going to get, not a shrug.
            return ZeroState.Table(Columns(pane), message);
        }

        /// <summary>Builds a standings table and registers it, so a late-arriving own entry can
        /// re-highlight every table in the card rather than only the last one.</summary>
        private DataTable BuildTable(TournamentPane pane, TournamentEntryDto[] rows, float maxHeight)
        {
            var table = new DataTable(Columns(pane))
                .WithZebra()
                .WithMaxHeight(maxHeight)
                .WithSort(0, true)
                .Bind(rows, pane.IsMine);
            pane.Bound.Add(new BoundTable { Table = table, Rows = rows });
            return table;
        }

        private DataColumn[] Columns(TournamentPane pane)
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "#", FixedWidth = true, Px = 74, Align = "center",
                    Cell = row => BoardLayout.RankCell(((TournamentEntryDto)row).position),
                    SortKey = row => ((TournamentEntryDto)row).position,
                },
                new DataColumn
                {
                    Header = "PLAYER", Grow = 1f,
                    Cell = row =>
                    {
                        var e = (TournamentEntryDto)row;
                        return BoardLayout.PlayerCell(PlayerLabel(e), e.playerId, pane.IsMine(row));
                    },
                    SortKey = row => PlayerLabel((TournamentEntryDto)row),
                },
                new DataColumn
                {
                    Header = "SCORE", FixedWidth = true, Px = 120, Align = "right",
                    Cell = ScoreCell,
                    SortKey = row => ((TournamentEntryDto)row).value,
                },
            };
        }

        private static VisualElement ScoreCell(object row)
        {
            var e = (TournamentEntryDto)row;
            var label = new Label(Fmt.Number(e.value));
            label.AddToClassList("sc-score");
            // Fmt.Number compacts past 10k ("12.4k"), and on a ranking the exact figure is what
            // decides the order — keep it one hover away.
            label.tooltip = e.value.ToString("R", CultureInfo.InvariantCulture);
            return label;
        }

        /// <summary>
        /// The player's own row in the selected league, fetched next to the slice. Bound by hand
        /// rather than through <see cref="ViewBind"/>: there is no slot to fill, and a 404 here means
        /// "no score in this league yet" rather than "this service is not set up".
        /// </summary>
        private async void LoadMyEntry(TournamentPane pane)
        {
            string table = pane.TableId;
            RestApiResult<TournamentEntryDto> result = null;
            try
            {
                var op = Sdk.Tournaments.GetPlayerAsync(Key(pane.Config), table);
                if (op != null)
                {
                    await op.Task();
                    result = op.Result;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Tournaments: reading the player's own entry failed: " + e.Message);
            }

            // The reader may have switched league while this was in flight; that request's answer
            // belongs to the league it was made for, not to the one now on screen.
            if (table != pane.TableId)
            {
                return;
            }

            if (result != null)
            {
                if (result.IsSuccess)
                {
                    pane.Me = result.Data;
                    if (pane.Me != null && !string.IsNullOrEmpty(pane.Me.playerId))
                    {
                        pane.HighlightId = pane.Me.playerId;
                        // An entry proves participation; a 404 proves nothing.
                        _side?.SetJoined(Key(pane.Config), true);
                    }
                }
            }

            pane.MeLoaded = true;
            RenderStanding(pane);
            RebindHighlight(pane);
        }

        /// <summary>The around-me payloads name the player outright, so the "You" row can be marked
        /// before <c>GetPlayerAsync</c> answers.</summary>
        private static void AdoptTarget(TournamentPane pane, TournamentEntryDto[] rows)
        {
            if (!string.IsNullOrEmpty(pane.HighlightId) || rows == null)
            {
                return;
            }
            foreach (var e in rows)
            {
                if (e != null && pane.Me != null && e.playerId == pane.Me.playerId)
                {
                    pane.HighlightId = e.playerId;
                    return;
                }
            }
        }

        /// <summary>Tables may have rendered before the player's own entry landed, and it is the "You"
        /// highlight that depends on it — re-bind rather than leave the player unable to find themselves.</summary>
        private static void RebindHighlight(TournamentPane pane)
        {
            if (string.IsNullOrEmpty(pane.HighlightId))
            {
                return;
            }
            foreach (var bound in pane.Bound)
            {
                bound.Table?.Bind(bound.Rows, pane.IsMine);
            }
        }

        /// <summary>
        /// Re-reads what a join, leave or submit changes, in place. Joining can move the player into
        /// a league, so the league is asked again too — but the one the reader is looking at stays.
        /// </summary>
        private void ReloadStandings(TournamentPane pane)
        {
            var tables = pane.Config.tables;
            if (tables == null || tables.Length == 0)
            {
                return;
            }
            OpenPane(pane);
        }

        // ----- join / leave from the sidebar ---------------------------------------------------------

        private async void Join(TournamentConfigDto cfg)
        {
            string key = Key(cfg);
            _side.SetBusy(key, true);

            RestApiResult<TournamentEntryDto> result = null;
            try
            {
                var op = Sdk.Tournaments.JoinAsync(key);
                await op.Task();
                result = op.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Tournaments: join threw: " + e.Message);
            }

            _side.SetBusy(key, false);
            if (result == null)
            {
                Toasts?.Fail("Join failed: no response");
                return;
            }

            if (!result.IsSuccess)
            {
                Toasts?.Fail("Join failed: " + ErrorText(result));
                return;
            }

            _side.SetJoined(key, true);
            var entry = result.Data;
            Toasts?.Ok(entry != null && entry.position > 0
                ? "Joined " + Title(cfg) + " — you are #" + entry.position
                : "Joined " + Title(cfg) + " — submit a score to get a place");
            ReloadIfSelected(key);
        }

        private void ConfirmLeave(TournamentConfigDto cfg)
        {
            ConfirmDialog.Open(Popup, "Leave " + Title(cfg),
                "The player stops being a participant, and their result in this tournament is removed.",
                "Leave", () => Leave(cfg));
        }

        private async void Leave(TournamentConfigDto cfg)
        {
            string key = Key(cfg);
            _side.SetBusy(key, true);

            RestApiResult result = null;
            try
            {
                var op = Sdk.Tournaments.LeaveAsync(key);
                await op.Task();
                result = op.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Tournaments: leave threw: " + e.Message);
            }

            _side.SetBusy(key, false);
            if (result == null)
            {
                Toasts?.Fail("Leave failed: no response");
                return;
            }

            if (!result.IsSuccess)
            {
                Toasts?.Fail("Leave failed: " + ErrorText(result));
                return;
            }

            _side.SetJoined(key, false);
            Toasts?.Ok("Left " + Title(cfg) + " — your result is gone");
            ReloadIfSelected(key);
        }

        private void ReloadIfSelected(string key)
        {
            if (_pane != null && Key(_pane.Config) == key)
            {
                ReloadStandings(_pane);
            }
        }

        // ----- shared plumbing ----------------------------------------------------------------------

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

        /// <summary>Flattens an around-me response into one ranked list (the table sorts it).</summary>
        private static TournamentEntryDto[] Around(TournamentPlayersAroundDto data)
        {
            if (data == null)
            {
                return Array.Empty<TournamentEntryDto>();
            }

            var list = new List<TournamentEntryDto>();
            Append(list, data.pLayersAbove); // SDK spelling
            if (data.targetPlayer != null)
            {
                list.Add(data.targetPlayer);
            }
            Append(list, data.playersBelow);
            return list.ToArray();
        }

        private static void Append(List<TournamentEntryDto> target, TournamentEntryDto[] source)
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

        private static bool IsEmpty(TournamentEntryDto[] entries)
        {
            return entries == null || entries.Length == 0;
        }

        private static int IndexOfTable(TournamentTableDto[] tables, string tableId)
        {
            if (tables == null || string.IsNullOrEmpty(tableId))
            {
                return -1;
            }
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] != null && tables[i].id == tableId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string PlayerLabel(TournamentEntryDto e)
        {
            return string.IsNullOrWhiteSpace(e.playerName) ? Fmt.Id(e.playerId, 10) : e.playerName;
        }

        private static string LeagueName(TournamentTableDto table, int index)
        {
            if (table != null && !string.IsNullOrWhiteSpace(table.name))
            {
                return table.name;
            }
            return "League " + (index + 1);
        }

        private static string Title(TournamentConfigDto cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.name))
            {
                return cfg.name;
            }
            return string.IsNullOrWhiteSpace(cfg.key) ? Fmt.Id(cfg.id) : cfg.key;
        }

        /// <summary>What the service methods want. Their parameter is called <c>tournamentId</c>, but the
        /// server resolves tournaments by their business key — the id is only a fallback for a config
        /// that has none.</summary>
        private static string Key(TournamentConfigDto cfg)
        {
            return string.IsNullOrEmpty(cfg.key) ? cfg.id : cfg.key;
        }

        /// <summary>One rendered standings table plus the rows it was bound with, so the "You"
        /// highlight can be reapplied when the player's own entry arrives late.</summary>
        private sealed class BoundTable
        {
            public DataTable Table;
            public TournamentEntryDto[] Rows;
        }

        /// <summary>
        /// The selected tournament's mutable state. It exists because the right side is filled by
        /// three calls that can land in any order (league, own entry, slice): whichever arrives
        /// re-renders its part from here, and the league the reader picked has to survive the others.
        /// </summary>
        private sealed class TournamentPane
        {
            public readonly TournamentConfigDto Config;

            /// <summary>Right edge of the header card: the player's place, score and league.</summary>
            public readonly VisualElement Standing = new VisualElement();

            /// <summary>Row count badge next to the standings title.</summary>
            public readonly Label Count = BoardLayout.Count();

            /// <summary>Host of the league picker buttons (rebuilt whenever the selection changes).</summary>
            public readonly VisualElement LeaguePicker = new VisualElement();

            /// <summary>Host of the league ladder rows (thresholds and reward ranges).</summary>
            public readonly VisualElement LeaguesSlot = new VisualElement();

            /// <summary>Host of the standings — one table, or two for the top-and-around slice.</summary>
            public readonly VisualElement EntriesSlot = new VisualElement();

            public readonly List<BoundTable> Bound = new List<BoundTable>();

            /// <summary>Which league table the card is reading; every entries call needs it.</summary>
            public string TableId;

            /// <summary>The league the server says the player is in, or null when it never answered.</summary>
            public PlayerLeagueMetaDto Meta;

            /// <summary>True once the league call finished.</summary>
            public bool MetaLoaded;

            /// <summary>The player's own entry, or null when they have never scored in this league.</summary>
            public TournamentEntryDto Me;

            /// <summary>True once the entry call finished — tells "no score" apart from "still loading".</summary>
            public bool MeLoaded;

            /// <summary>Player id the table highlights. Read at render time, so a late answer only
            /// needs a re-bind.</summary>
            public string HighlightId;

            public int Entries;
            public bool EntriesLoaded;

            public TournamentPane(TournamentConfigDto config)
            {
                Config = config;
            }

            public bool IsMine(object row)
            {
                if (string.IsNullOrEmpty(HighlightId))
                {
                    return false;
                }
                var e = row as TournamentEntryDto;
                return e != null && e.playerId == HighlightId;
            }
        }
    }
}

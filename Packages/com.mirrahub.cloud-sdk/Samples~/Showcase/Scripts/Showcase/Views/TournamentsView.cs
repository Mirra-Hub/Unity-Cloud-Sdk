using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Friends.Dto;
using MirraCloud.Core.Leaderboard.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Tournaments.Dto;
using UnityEngine;
using UnityEngine.UIElements;

// The tournament and the leaderboard DTO namespaces both declare a PlayerRewardsDto, and this
// screen needs types out of both: a tournament's reward payload is the leaderboard's RewardDataDto.
using TournamentRewardsDto = Plugins.MirraCloud.Core.Services.Tournaments.Dto.PlayerRewardsDto;
using TournamentEnums = MirraCloud.Core.Tournaments.Enums;
using RewardDistribution = MirraCloud.Core.Leaderboard.Enums.RewardDistributionType;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Tournaments detail: one tab per configured tournament, plus a branch-wide Rewards tab.
    /// A tournament is a leaderboard cut into league tables, so a pane first asks which league the
    /// player sits in (<c>GetPlayerLeagueMetaAsync</c>), then fills that league's standings from
    /// whichever entries endpoint the toolbar's slice picker selects.
    /// <para>
    /// Every pane fans out into two independent calls (the slice and the player's own entry), so the
    /// KPI strip is re-rendered from <see cref="TournamentPane"/> state as each one lands. Submitting
    /// a score reloads those two calls in place instead of rebuilding the tab, which would throw away
    /// the result the reader just clicked for.
    /// </para>
    /// </summary>
    public sealed class TournamentsView : ServiceView
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

        private const string RewardsSnippet = @"// what finished runs left for this player. Branch-wide: there is no tournament id here.
// reset:false only peeks — reset:true hands the rewards over and clears them in the same call.
var op = sdk.Tournaments.GetRewardsAsync(false);
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var reward in op.Result.Data.rewards)
{
    // reward.rewardId is an economy resource id, reward.count the amount
    Debug.Log(reward.rewardId + "" x"" + reward.count);
}";

        private const string ClaimSnippet = @"// claim: the server hands the pending rewards to the player
var op = sdk.Tournaments.SubmitRewardsAsync();
await op.Task();

if (op.Result.IsSuccess)
{
    // read them again to see the list empty out
    await sdk.Tournaments.GetRewardsAsync(false).Task();
}";

        /// <summary>Which league each tournament is being read at, so a refresh or a slice change
        /// does not drop the reader back onto the player's own league.</summary>
        private readonly Dictionary<string, string> _leagueByTournament =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private Slice _slice = Slice.Top;
        private Tabs _tabs;
        private VisualElement _rewardsSlot;
        private int _rewardsTab = -1;

        public TournamentsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        /// <summary>Which entries endpoint a pane asks for. Order matches <see cref="SliceNames"/>.</summary>
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
            _tabs = null;
            _rewardsSlot = null;
            _rewardsTab = -1;
            SetStatus(null);
            SetSubtitle("One tab per configured tournament. A pane reads the player's league first, then "
                        + "the slice picker swaps which entries endpoint fills that league's table.");

            UseToolbar()
                .WithFilter("Slice", SliceNames, OnSliceChanged, SliceNames[(int)_slice])
                .WithSpacer()
                .WithRefresh(Refresh);

            DeclareCall(new SdkCall("List tournaments", ConfigsSnippet,
                "Call it once at startup: every other tournament call needs a key from here."));
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
            DeclareCall(new SdkCall("Submit a score", SubmitSnippet));
            DeclareCall(new SdkCall("Pending rewards", RewardsSnippet));
            DeclareCall(new SdkCall("Claim the rewards", ClaimSnippet));

            // Zero margin: this slot only carries the loading/failure state — on success the
            // tournaments land in the tab strip (chrome) and the panes inside Content.
            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Tournaments.InitializeAsync(),
                slot,
                BuildTournaments,
                isEmpty: c => c == null || c.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Tournament configs",
                    Snippet = ConfigsSnippet,
                    ServiceName = "Tournament",
                    // this is the tournament *configuration* call, so a 404 really does mean
                    // "no tournaments exist in this project"
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoTournaments,
                });
        }

        private VisualElement BuildTournaments(TournamentConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 tournament" : configs.Length + " tournaments", ChipTone.Ok);

            _tabs = UseTabs();
            foreach (var cfg in configs)
            {
                var captured = cfg;
                _tabs.Add(Title(captured), LucideIcon.Swords, () => BuildTournamentPane(captured));
            }

            // Rewards are asked for per branch, not per tournament, so they get their own tab rather
            // than the same section repeated inside every pane.
            _rewardsTab = _tabs.Count;
            _tabs.Add("Rewards", LucideIcon.Gift, BuildRewards);

            // The strip and its panes live outside this slot, so the slot itself renders nothing.
            return new VisualElement();
        }

        private VisualElement NoTournaments()
        {
            SetStatus("Not configured", ChipTone.Warn);
            return ZeroState.NotConfigured("Tournaments");
        }

        private void OnSliceChanged(string name)
        {
            int index = Array.IndexOf(SliceNames, name);
            if (index < 0 || (Slice)index == _slice)
            {
                return;
            }
            _slice = (Slice)index;

            if (_tabs == null || _rewardsTab < 0)
            {
                return;
            }
            // Panes cache their data, so the visible one has to be thrown away for the new endpoint
            // to be called; the hidden ones rebuild when they are selected again. The rewards tab is
            // left alone — it does not depend on the slice.
            for (int i = 0; i < _rewardsTab; i++)
            {
                _tabs.Invalidate(i);
            }
        }

        // ----- one tournament -----------------------------------------------------------------------

        private VisualElement BuildTournamentPane(TournamentConfigDto cfg)
        {
            var pane = new TournamentPane(cfg);
            bool hasLeagues = cfg.tables != null && cfg.tables.Length > 0;

            var root = new VisualElement();
            root.Add(BuildMeta(cfg));

            pane.Kpis.AddToClassList("sc-trn-kpis");
            root.Add(pane.Kpis);

            if (!hasLeagues)
            {
                // Nothing will ever be loaded, so the strip settles on zeros instead of sitting on
                // dashes as if two requests were still in flight.
                pane.MeLoaded = true;
                pane.EntriesLoaded = true;
            }
            RenderKpis(pane);

            pane.LeaguePicker.AddToClassList("sc-trn-leagues");
            root.Add(pane.LeaguePicker);
            root.Add(pane.EntriesSlot);

            if (hasLeagues)
            {
                root.Add(new SectionHeader("Leagues and their rewards", cfg.tables.Length.ToString()));
                root.Add(pane.LeaguesSlot);
            }

            root.Add(new SectionHeader("Submit a score"));
            root.Add(SubmitHint(cfg));
            root.Add(SubmitCard(pane));

            if (hasLeagues)
            {
                OpenPane(pane);
            }
            else
            {
                Replace(pane.EntriesSlot, ZeroState.Panel(LucideIcon.Layers, "No league tables yet",
                    "A tournament ranks players inside a league table, and this one has none — every "
                    + "entries endpoint needs a table id, so there is nothing to read. Add a league to "
                    + "this tournament in the Mirra Hub console.",
                    null, null,
                    "Leagues also carry the promotion thresholds and the rewards for each place."));
            }
            return root;
        }

        private static VisualElement BuildMeta(TournamentConfigDto cfg)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");
            row.AddToClassList("sc-trn-meta");

            row.Add(new Chip(cfg.orderType == TournamentEnums.OrderType.Highest
                ? "highest wins"
                : "lowest wins", ChipTone.Info));
            row.Add(new Chip(cfg.type == TournamentEnums.TournamentsType.Time ? "time based" : "score based",
                ChipTone.Neutral));
            row.Add(new Chip("keeps " + cfg.updateStrategy.ToString().ToLowerInvariant(), ChipTone.Neutral));
            row.Add(new Chip("rewards by "
                + (cfg.rewardDistributionType == RewardDistribution.ByScore ? "score" : "place"), ChipTone.Accent));

            if (!string.IsNullOrEmpty(cfg.key))
            {
                row.Add(new Chip("key: " + cfg.key, ChipTone.Neutral));
            }

            int leagues = cfg.tables != null ? cfg.tables.Length : 0;
            row.Add(new Chip(leagues == 1 ? "1 league" : leagues + " leagues", ChipTone.Neutral));

            if (cfg.isReset)
            {
                string every = cfg.resetIntervalValue > 1
                    ? "resets every " + cfg.resetIntervalValue + " " + cfg.resetIntervalType
                    : "resets " + cfg.resetIntervalType;
                row.Add(new Chip(every.ToLowerInvariant(), ChipTone.Warn));
                if (cfg.nextResetDate.HasValue)
                {
                    // The run ending is when places are settled and rewards are handed out, so the
                    // countdown is the most load-bearing number on the pane.
                    var chip = new CountdownChip(cfg.nextResetDate.Value.ToUniversalTime());
                    chip.tooltip = "The run ends (and rewards are granted) at "
                                   + Fmt.DateTime2(cfg.nextResetDate);
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
        /// response, because the league, the player's own entry and the slice are separate requests.
        /// </summary>
        private void RenderKpis(TournamentPane pane)
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

            kpis.Add("My league", LucideIcon.Layers, LeagueText(pane));
        }

        /// <summary>Where the player stands in the league ladder, in the ladder's own terms.</summary>
        private static string LeagueText(TournamentPane pane)
        {
            if (pane.Meta == null)
            {
                return pane.MetaLoaded ? "unassigned" : Fmt.Dash;
            }

            var tables = pane.Config.tables;
            int total = tables != null ? tables.Length : 0;
            int at = IndexOfTable(tables, pane.Meta.currentLeagueTableId);
            if (at >= 0 && total > 0)
            {
                return (at + 1) + " of " + total;
            }
            // The config the client holds does not list the league the meta points at (it was removed,
            // or the player's run predates it) — the server's own index is still worth showing.
            return "index " + pane.Meta.currentLeagueTableIndex;
        }

        private string EntriesCaption()
        {
            switch (_slice)
            {
                case Slice.AroundMe: return "Rows around you";
                case Slice.TopAndAround: return "Rows fetched";
                case Slice.Friends: return "Friends ranked";
                case Slice.Country: return "In your country";
                default: return "Players";
            }
        }

        /// <summary>
        /// Opens a pane: the league comes first because every entries endpoint needs a table id, and
        /// only then are the standings and the player's own row requested. Bound by hand rather than
        /// through <see cref="ViewBind"/> — the response picks the table rather than filling a slot.
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
                Ctx.Log?.Record("Tournaments: player league", result, LeagueSnippet);
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

            RenderLeagues(pane);
            RenderKpis(pane);
            LoadMyEntry(pane);
            LoadSlice(pane);
        }

        /// <summary>Repaints the league picker and the per-league cards; both mark the player's own
        /// league, which is only known once the meta call has landed.</summary>
        private void RenderLeagues(TournamentPane pane)
        {
            var tables = pane.Config.tables;
            pane.LeaguePicker.Clear();
            pane.LeaguesSlot.Clear();
            if (tables == null || tables.Length == 0)
            {
                return;
            }

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

                var btn = new Button(() => SelectLeague(pane, id));
                btn.AddToClassList("sc-trn-league");
                btn.EnableInClassList("sc-trn-league--active", id == pane.TableId);
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
                pane.LeaguesSlot.Add(LeagueCard(pane, table, i, isMine));
            }
        }

        private VisualElement LeagueCard(TournamentPane pane, TournamentTableDto table, int index, bool isMine)
        {
            var card = new Card(isMine ? Meta.Accent : (Color?)null);
            card.AddToClassList("sc-trn-league-card");
            if (isMine)
            {
                card.AddToClassList("sc-trn-league-card--mine");
            }
            card.WithTitle(LeagueName(table, index), isMine ? Meta.Accent : (Color?)null);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");

            // Thresholds are counts of players, not places: on a reset the best N of this league move
            // up to the neighbouring one and the worst M drop down.
            chips.Add(table.leagueUpThreshold > 0
                ? new Chip("top " + table.leagueUpThreshold + " promoted", ChipTone.Ok)
                : new Chip("no promotion", ChipTone.Neutral));
            chips.Add(table.leagueDownThreshold > 0
                ? new Chip("bottom " + table.leagueDownThreshold + " demoted", ChipTone.Bad)
                : new Chip("no demotion", ChipTone.Neutral));
            if (isMine)
            {
                chips.Add(new Chip("your league", ChipTone.Accent));
            }
            if (!string.IsNullOrEmpty(table.id) && table.id == pane.TableId)
            {
                chips.Add(new Chip("shown above", ChipTone.Info));
            }
            card.Body.Add(chips);

            var ranges = table.rewardsForPlaces;
            if (ranges == null || ranges.Length == 0)
            {
                var none = new Label("No rewards are attached to this league, so a reset only moves players "
                                     + "between leagues. Reward ranges are authored per league in the console.");
                none.enableRichText = false;
                none.AddToClassList("sc-fs-hint");
                card.Body.Add(none);
                return card;
            }

            var list = new VisualElement();
            foreach (var range in ranges)
            {
                if (range == null)
                {
                    continue;
                }
                var row = new ListRow();
                row.SetTitle(RangeLabel(pane.Config, range));
                row.SetSubtitle(range.rewards == null || range.rewards.Length == 0
                    ? "nothing granted"
                    : range.rewards.Length + (range.rewards.Length == 1 ? " reward" : " rewards"));
                row.SetTrailing(RewardChips(range.rewards));
                list.Add(row);
            }
            card.Body.Add(list);
            return card;
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

        /// <summary>
        /// The reward payload as pills. The kind (currency or item) is deliberately not claimed: the
        /// tournaments endpoint reports the economy resource id and the amount, and which of the two
        /// it is comes from looking that id up in the Economy module.
        /// </summary>
        private VisualElement RewardChips(RewardDataDto[] rewards)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            if (rewards != null)
            {
                foreach (var reward in rewards)
                {
                    if (reward == null)
                    {
                        continue;
                    }
                    var chip = new RewardChip(LucideIcon.Gift,
                        Fmt.Id(reward.rewardId, 8) + " ×" + reward.count, Meta.Accent);
                    chip.tooltip = "Economy resource " + Fmt.OrDash(reward.rewardId) + " ×" + reward.count;
                    row.Add(chip);
                }
            }

            if (row.childCount == 0)
            {
                row.Add(new Chip("nothing granted", ChipTone.Neutral));
            }
            return row;
        }

        // ----- standings ----------------------------------------------------------------------------

        private void LoadSlice(TournamentPane pane)
        {
            var slot = pane.EntriesSlot;
            string key = Key(pane.Config);
            string table = pane.TableId;
            pane.Bound.Clear();

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
                    Log = Ctx.Log,
                    Label = label,
                    Snippet = snippet,
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
                    Log = Ctx.Log,
                    Label = "Tournament top and around",
                    Snippet = TopAndAroundSnippet,
                    ServiceName = "Tournament",
                    AllowRetry = true,
                    EmptyView = () => EmptySlice(pane,
                        "This league has neither a ranking nor an entry for you yet. Both halves of this "
                        + "response fill up from the first submitted score."),
                });
        }

        /// <summary>
        /// The friends slice is the only two-step one: the endpoint ranks exactly the ids it is
        /// given, so the friend list has to be fetched first and both calls end up in the journal.
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
                    Log = Ctx.Log,
                    Label = "Friends list",
                    Snippet = FriendsSnippet,
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
            RenderKpis(pane);

            var root = new VisualElement();

            var chart = BuildChart(rows);
            if (chart != null)
            {
                root.Add(chart);
            }
            root.Add(BuildTable(pane, rows, 420f));
            return root;
        }

        private VisualElement BuildTopAndAroundBody(TournamentPane pane, TournamentTopAndPlayersAroundDto data)
        {
            var top = data != null && data.top != null ? data.top : Array.Empty<TournamentEntryDto>();
            var around = Around(data?.playersAround);

            pane.Entries = top.Length + around.Length;
            pane.EntriesLoaded = true;
            AdoptTarget(pane, around);
            RenderKpis(pane);

            var root = new VisualElement();

            root.Add(new SectionHeader("Top of the league", top.Length.ToString()));
            if (top.Length == 0)
            {
                root.Add(ZeroState.Table(Columns(pane),
                    "Nobody has scored in this league yet.", 3,
                    "Submit a score", () => OpenSubmitDialog(pane)));
            }
            else
            {
                var chart = BuildChart(top);
                if (chart != null)
                {
                    root.Add(chart);
                }
                root.Add(BuildTable(pane, top, 320f));
            }

            var header = new SectionHeader("Around you", around.Length.ToString());
            header.AddToClassList("sc-trn-around");
            root.Add(header);
            if (around.Length == 0)
            {
                root.Add(ZeroState.Table(Columns(pane),
                    "You have no position in this league, so there is no neighbourhood to show. It "
                    + "appears as soon as you have an entry.", 3,
                    "Submit a score", () => OpenSubmitDialog(pane)));
            }
            else
            {
                root.Add(BuildTable(pane, around, 320f));
            }
            return root;
        }

        private VisualElement EmptySlice(TournamentPane pane, string message)
        {
            pane.Entries = 0;
            pane.EntriesLoaded = true;
            pane.Bound.Clear();
            RenderKpis(pane);

            // The board keeps the shape it will have once scores arrive — the reader sees the columns
            // they are going to get, plus the one call that fills them.
            return ZeroState.Table(Columns(pane), message, 3, "Submit a score", () => OpenSubmitDialog(pane));
        }

        /// <summary>Top of the ranking as bars — the shape of the gap between the leaders, which a
        /// column of numbers hides. Returns null when there is nothing to compare.</summary>
        private VisualElement BuildChart(TournamentEntryDto[] rows)
        {
            if (rows.Length < 2)
            {
                return null;
            }

            var ordered = new List<TournamentEntryDto>(rows);
            ordered.Sort((a, b) => a.position.CompareTo(b.position));

            int count = Math.Min(ChartBars, ordered.Count);
            var points = new List<ChartPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var e = ordered[i];
                points.Add(new ChartPoint("#" + e.position, (float)e.value, MedalTint(e.position)));
            }

            var chart = new BarChart(150f);
            chart.AddToClassList("sc-trn-chart");
            chart.SetAccent(Meta.Accent);
            chart.SetValueFormatter(v => Fmt.Number(v));
            chart.SetData(points);
            return chart;
        }

        /// <summary>Builds a standings table and registers it, so a late-arriving own entry can
        /// re-highlight every table on the pane rather than only the last one.</summary>
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
                    Cell = RankCell,
                    SortKey = row => ((TournamentEntryDto)row).position,
                },
                new DataColumn
                {
                    Header = "PLAYER", Grow = 1f,
                    Cell = row => PlayerCell(pane, row),
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

        private static VisualElement RankCell(object row)
        {
            var e = (TournamentEntryDto)row;

            var text = new Label(e.position > 0 ? "#" + e.position : Fmt.Dash);
            text.AddToClassList("sc-rank");
            if (e.position < 1 || e.position > 3)
            {
                return text;
            }

            var tint = MedalTint(e.position).Value;
            text.style.color = tint;

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-trn-rank");

            var medal = new Label(LucideIcon.Medal);
            medal.AddToClassList("sc-trn-medal");
            medal.AddToClassList("sc-icon");
            medal.style.color = tint;
            wrap.Add(medal);
            wrap.Add(text);
            return wrap;
        }

        private static VisualElement PlayerCell(TournamentPane pane, object row)
        {
            var e = (TournamentEntryDto)row;
            string label = PlayerLabel(e);

            var wrap = new VisualElement();
            wrap.AddToClassList("sc-trn-player");

            var avatar = new Avatar(26f).SetInitialsFor(label);
            avatar.AddToClassList("sc-trn-player__avatar");
            wrap.Add(avatar);

            var name = new Label(Fmt.OrDash(label));
            name.enableRichText = false;
            name.tooltip = e.playerId;
            wrap.Add(name);

            if (pane.IsMine(row))
            {
                var you = new Badge("You", ChipTone.Accent);
                you.AddToClassList("sc-trn-player__you");
                wrap.Add(you);
            }
            return wrap;
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
                Ctx.Log?.Record("Tournaments: my entry", result, MeSnippet);
                if (result.IsSuccess)
                {
                    pane.Me = result.Data;
                    if (pane.Me != null && !string.IsNullOrEmpty(pane.Me.playerId))
                    {
                        pane.HighlightId = pane.Me.playerId;
                    }
                }
            }

            pane.MeLoaded = true;
            RenderKpis(pane);
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

        /// <summary>Re-runs the two calls that a submitted score changes, in place: the tab itself is
        /// left alone so the card that ran the write keeps showing its result.</summary>
        private void ReloadStandings(TournamentPane pane)
        {
            if (string.IsNullOrEmpty(pane.TableId))
            {
                return;
            }

            pane.Me = null;
            pane.MeLoaded = false;
            pane.Entries = 0;
            pane.EntriesLoaded = false;
            RenderKpis(pane);

            LoadMyEntry(pane);
            LoadSlice(pane);
        }

        // ----- submitting a score -------------------------------------------------------------------

        private static VisualElement SubmitHint(TournamentConfigDto cfg)
        {
            string strategy;
            switch (cfg.updateStrategy)
            {
                case TournamentEnums.UpdateStrategy.Best:
                    strategy = "This tournament keeps the best value, so a worse score leaves the entry alone.";
                    break;
                case TournamentEnums.UpdateStrategy.Total:
                    strategy = "This tournament totals the values, so every submission adds to the entry.";
                    break;
                default:
                    strategy = "This tournament keeps the latest value, so every submission replaces the entry.";
                    break;
            }

            string ordering = cfg.orderType == TournamentEnums.OrderType.Highest
                ? " Higher values rank first."
                : " Lower values rank first.";
            if (cfg.type == TournamentEnums.TournamentsType.Time)
            {
                ordering += " It is a time-based tournament, so the value is a duration in whatever unit "
                            + "the game measures in.";
            }

            var hint = new Label("There is no table id here: the score goes to the league the player "
                                 + "currently sits in. " + strategy + ordering);
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            return hint;
        }

        private VisualElement SubmitCard(TournamentPane pane)
        {
            var card = new ActionCard("Submit a score",
                    "Writes one value for this tournament and reloads the standings above.", LucideIcon.Send)
                .WithFields(SubmitFields())
                .WithSnippet(SubmitSnippet)
                .OnRun("Submit", values => SubmitScore(pane, values));
            card.AddToClassList("sc-trn-submit");
            return card;
        }

        private static FormField[] SubmitFields()
        {
            return new[]
            {
                FormField.Float("score", "Score", 1000f)
                    .WithPlaceholder("Any number; the update strategy above decides what the server does with it.")
                    .AsRequired(),
                FormField.Text("playerName", "Player name")
                    .WithPlaceholder("Optional. Left blank, the SDK sends the nickname from PlayerAccount."),
            };
        }

        private void OpenSubmitDialog(TournamentPane pane)
        {
            if (Popup == null)
            {
                return;
            }
            FormDialog.Open(Popup, "Submit a score to " + Title(pane.Config), SubmitFields(), "Submit",
                values => SubmitFromDialog(pane, values));
        }

        /// <summary>The zero-state call to action. It shares <see cref="SubmitScore"/> with the card,
        /// which already toasts on success — only the failure needs reporting here.</summary>
        private async void SubmitFromDialog(TournamentPane pane, FormValues values)
        {
            var outcome = await SubmitScore(pane, values);
            if (!outcome.Ok && Toasts != null)
            {
                Toasts.Fail("Submit score failed · " + outcome.Message);
            }
        }

        private async Task<ActionOutcome> SubmitScore(TournamentPane pane, FormValues values)
        {
            double score;
            // Parsed here rather than through FormValues.Float: the SDK takes a double, and a
            // tournament score can easily be larger than a float represents exactly.
            if (!double.TryParse(values.Text("score").Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out score))
            {
                return ActionOutcome.Failure("Score must be a number (use a dot for decimals).");
            }

            string name = values.Text("playerName");
            var op = Sdk.Tournaments.SubmitScoreAsync(Key(pane.Config), score,
                string.IsNullOrWhiteSpace(name) ? null : name.Trim());

            var outcome = await Await(op, "Tournaments: submit score");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            if (Toasts != null)
            {
                Toasts.Ok("Score " + Fmt.Number(score) + " submitted");
            }
            ReloadStandings(pane);
            return ActionOutcome.Success("Submitted " + Fmt.Number(score)
                + " — the standings and your position above have been reloaded");
        }

        // ----- rewards ------------------------------------------------------------------------------

        private VisualElement BuildRewards()
        {
            var col = new VisualElement();

            var hint = new Label("When a run resets, the server settles the final standings of every league "
                                 + "and works out what each place (or each score) earned. These two calls are "
                                 + "branch-wide — no tournament id — and they are how a client finds out what "
                                 + "is waiting and takes it. A 404 here means this branch hands rewards "
                                 + "straight to the player's economy instead.");
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            var header = new VisualElement();
            header.AddToClassList("sc-row-actions");
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(new SectionHeader("Pending rewards"));

            var clear = new Button(ConfirmReadAndClear) { text = "Read and clear" };
            clear.AddToClassList("sc-btn");
            clear.AddToClassList("sc-btn--danger");
            clear.tooltip = "Calls GetRewardsAsync(reset: true), which empties the list";
            header.Add(clear);
            col.Add(header);

            _rewardsSlot = new VisualElement();
            col.Add(_rewardsSlot);
            LoadRewards();

            col.Add(new SectionHeader("Claiming"));
            col.Add(new ActionCard("Claim the pending rewards",
                    "Hands the pending rewards to the player, then re-reads the list above so the result "
                    + "is visible.", LucideIcon.Gift)
                .WithSnippet(ClaimSnippet)
                .OnRun("Claim", ClaimRewards));
            return col;
        }

        private void LoadRewards()
        {
            var slot = _rewardsSlot;
            if (slot == null)
            {
                return;
            }

            ViewBind.Load(
                () => Sdk.Tournaments.GetRewardsAsync(false),
                slot,
                BuildRewardsBody,
                isEmpty: d => d == null || d.rewards == null || d.rewards.Length == 0,
                options: new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Tournament rewards",
                    Snippet = RewardsSnippet,
                    ServiceName = "Tournament reward",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(RewardColumns(),
                        "Nothing is waiting for this player. A reward lands here when a tournament run "
                        + "resets and the player's place (or score) matches one of the ranges configured "
                        + "on their league.", 3),
                });
        }

        private VisualElement BuildRewardsBody(TournamentRewardsDto data)
        {
            var rewards = data.rewards ?? Array.Empty<RewardDataDto>();

            int total = 0;
            foreach (var reward in rewards)
            {
                if (reward != null)
                {
                    total += reward.count;
                }
            }

            var col = new VisualElement();
            col.Add(new KpiRow()
                .Add("Pending rewards", LucideIcon.Gift, rewards.Length.ToString(), null, rewards.Length > 0)
                .Add("Total amount", LucideIcon.Sigma, Fmt.Number(total))
                .Add("Player", LucideIcon.User, Fmt.Id(data.playerId, 10)));

            col.Add(RewardChips(rewards));

            var table = new DataTable(RewardColumns()).WithZebra().WithMaxHeight(320f);
            table.Bind(rewards);
            col.Add(table);
            return col;
        }

        private static DataColumn[] RewardColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "ECONOMY RESOURCE", Grow = 2f,
                    SortKey = o => ((RewardDataDto)o).rewardId,
                    Cell = o =>
                    {
                        var reward = (RewardDataDto)o;
                        var label = new Label(Fmt.OrDash(reward.rewardId));
                        label.enableRichText = false;
                        // The id is what the Economy module looks up, so the full value stays reachable.
                        label.tooltip = "Look this id up in the Economy module to see what it grants";
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "AMOUNT", FixedWidth = true, Px = 110, Align = "right",
                    SortKey = o => ((RewardDataDto)o).count,
                    Cell = o => new Label("×" + ((RewardDataDto)o).count),
                },
            };
        }

        private async Task<ActionOutcome> ClaimRewards(FormValues values)
        {
            var outcome = await Await(Sdk.Tournaments.SubmitRewardsAsync(), "Tournaments: claim rewards");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            if (Toasts != null)
            {
                Toasts.Ok("Rewards claimed");
            }
            LoadRewards();
            return ActionOutcome.Success("Claimed — the pending list above has been re-read");
        }

        private void ConfirmReadAndClear()
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Read and clear the rewards",
                "GetRewardsAsync(reset: true) returns what is pending and empties the list in the same "
                + "call, so whatever comes back is gone from the server. Everything else on this screen "
                + "reads with reset: false for exactly that reason.",
                "Read and clear", ReadAndClear);
        }

        private async void ReadAndClear()
        {
            var op = Sdk.Tournaments.GetRewardsAsync(true);
            var outcome = await AwaitData(op, "Tournaments: rewards (reset)");
            if (!outcome.Ok)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Read and clear failed · " + outcome.Message);
                }
                return;
            }

            var data = op != null && op.Result != null ? op.Result.Data : null;
            var rewards = data != null && data.rewards != null ? data.rewards : Array.Empty<RewardDataDto>();

            if (Toasts != null)
            {
                Toasts.Ok(rewards.Length == 0
                    ? "Nothing was pending"
                    : rewards.Length + (rewards.Length == 1 ? " reward" : " rewards") + " handed over");
            }

            // The payload is the point of the call and it is gone from the server now, so it is shown
            // rather than summarised into a toast.
            if (Popup != null && rewards.Length > 0)
            {
                var body = new VisualElement();
                var text = new Label("The server has handed these over and cleared them from the pending list.");
                text.enableRichText = false;
                text.AddToClassList("sc-fs-hint");
                body.Add(text);
                body.Add(RewardChips(rewards));
                Popup.Open(body, "Rewards handed over");
            }

            LoadRewards();
        }

        // ----- shared plumbing ----------------------------------------------------------------------

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

        /// <summary>One rendered standings table plus the rows it was bound with, so the "You"
        /// highlight can be reapplied when the player's own entry arrives late.</summary>
        private sealed class BoundTable
        {
            public DataTable Table;
            public TournamentEntryDto[] Rows;
        }

        /// <summary>
        /// One tournament tab's mutable state. It exists because a pane is filled by three calls that
        /// can land in any order (league, own entry, slice): whichever arrives re-renders the KPI strip
        /// from here, and the league the reader picked has to survive both of the others.
        /// </summary>
        private sealed class TournamentPane
        {
            public readonly TournamentConfigDto Config;
            public readonly KpiRow Kpis = new KpiRow();

            /// <summary>Host of the league picker buttons (rebuilt whenever the selection changes).</summary>
            public readonly VisualElement LeaguePicker = new VisualElement();

            /// <summary>Host of the per-league cards (thresholds and reward ranges).</summary>
            public readonly VisualElement LeaguesSlot = new VisualElement();

            /// <summary>Host of the standings — one table, or two for the top-and-around slice.</summary>
            public readonly VisualElement EntriesSlot = new VisualElement();

            public readonly List<BoundTable> Bound = new List<BoundTable>();

            /// <summary>Which league table the pane is reading; every entries call needs it.</summary>
            public string TableId;

            /// <summary>The league the server says the player is in, or null when it never answered.</summary>
            public PlayerLeagueMetaDto Meta;

            /// <summary>True once the league call finished — tells "unassigned" apart from "still loading".</summary>
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

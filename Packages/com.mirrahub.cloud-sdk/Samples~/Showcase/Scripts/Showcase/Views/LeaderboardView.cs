using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Medal tints are deliberately outside the semantic palette: on a ranking table gold/silver/
        // bronze *are* the meaning, and no status color reads as "third place".
        private static readonly Color Gold = new Color(0.91f, 0.78f, 0.32f);
        private static readonly Color Silver = new Color(0.76f, 0.78f, 0.84f);
        private static readonly Color Bronze = new Color(0.82f, 0.54f, 0.32f);

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

        private readonly Dictionary<string, BoardRow> _rows = new Dictionary<string, BoardRow>();
        private VisualElement _rewardsRow;
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
            _rows.Clear();
            _rewardsRow = null;
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

        private VisualElement BuildScreen(LeaderboardConfigDto[] configs)
        {
            SetStatus(configs.Length == 1 ? "1 board" : configs.Length + " boards", ChipTone.Ok);

            var split = new VisualElement();
            split.AddToClassList("sc-lb-split");

            var side = new VisualElement();
            side.AddToClassList("sc-lb-side");

            var title = new Label("Boards");
            title.AddToClassList("sc-lb-side__title");
            side.Add(title);

            // The board picked before a refresh, else the first one.
            LeaderboardConfigDto selected = null;
            foreach (var cfg in configs)
            {
                if (cfg == null)
                {
                    continue;
                }
                var row = new BoardRow(this, cfg);
                _rows[cfg.key ?? string.Empty] = row;
                side.Add(row.Root);

                if (selected == null || (cfg.key == _selectedKey && selected.key != _selectedKey))
                {
                    selected = cfg;
                }
            }

            var payouts = new Label("Payouts");
            payouts.AddToClassList("sc-lb-side__sub");
            side.Add(payouts);
            _rewardsRow = RewardsRow();
            side.Add(_rewardsRow);

            _main = new VisualElement();
            _main.AddToClassList("sc-lb-main");

            split.Add(side);
            split.Add(_main);

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

        private VisualElement RewardsRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sc-lb-board");

            var glyph = new Label(LucideIcon.Gift);
            glyph.AddToClassList("sc-icon");
            glyph.AddToClassList("sc-lb-board__glyph");
            row.Add(glyph);

            var texts = new VisualElement();
            texts.AddToClassList("sc-lb-board__texts");
            var name = new Label("Rewards");
            name.AddToClassList("sc-lb-board__name");
            texts.Add(name);
            var sub = new Label("paid into Economy on reset");
            sub.AddToClassList("sc-lb-board__sub");
            texts.Add(sub);
            row.Add(texts);

            row.RegisterCallback<ClickEvent>(_ => SelectRewards());
            return row;
        }

        private void SelectBoard(LeaderboardConfigDto cfg)
        {
            _rewardsSelected = false;
            _selectedKey = cfg.key;
            MarkActive();

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
            MarkActive();

            _main.Clear();
            _main.Add(new PendingRewardsPanel(Ctx, RewardSourceType.Leaderboard,
                "When a board resets, the server works out what each place (or score) earned and pays it "
                + "into Economy, where the game reads and claims it. Nothing is claimed by reading.",
                "Nothing is waiting for this player. A reward lands here when a board resets and the "
                + "player's place (or score) falls in one of the board's reward ranges."));
        }

        private void MarkActive()
        {
            foreach (var pair in _rows)
            {
                pair.Value.Root.EnableInClassList("sc-lb-board--active",
                    !_rewardsSelected && pair.Key == (_selectedKey ?? string.Empty));
            }
            _rewardsRow?.EnableInClassList("sc-lb-board--active", _rewardsSelected);
        }

        // ----- board header: one line of configuration + the player's standing -----------------------

        private VisualElement BuildHeadCard(BoardPane pane)
        {
            var cfg = pane.Config;

            var card = new VisualElement();
            card.AddToClassList("sc-lb-card");
            card.AddToClassList("sc-lb-head");

            var badge = new Label(LucideIcon.Trophy);
            badge.AddToClassList("sc-icon");
            badge.AddToClassList("sc-lb-head__badge");
            badge.style.color = Meta.Accent;
            badge.style.backgroundColor = new Color(Meta.Accent.r, Meta.Accent.g, Meta.Accent.b, 0.14f);
            card.Add(badge);

            var texts = new VisualElement();
            texts.AddToClassList("sc-lb-head__texts");

            var name = new Label(BoardTitle(cfg));
            name.enableRichText = false;
            name.AddToClassList("sc-lb-head__name");
            texts.Add(name);

            var meta = new VisualElement();
            meta.AddToClassList("sc-lb-head__meta");
            var line = new Label(MetaLine(cfg));
            line.enableRichText = false;
            line.AddToClassList("sc-lb-head__line");
            line.tooltip = cfg.isReset
                ? string.Format(CultureInfo.InvariantCulture, "Resets at {0:00}:{1:00} UTC. Next: {2}",
                    cfg.resetTimeHour, cfg.resetTimeMinute, Fmt.DateTime2(cfg.nextResetDate))
                : "Updated " + RelativeTime.Format(cfg.updatedDate);
            meta.Add(line);
            // Filled once the board's first score started a session.
            if (cfg.isReset && cfg.nextResetDate.HasValue)
            {
                var countdown = new CountdownChip(cfg.nextResetDate.Value.ToUniversalTime());
                countdown.AddToClassList("sc-lb-head__countdown");
                meta.Add(countdown);
            }
            texts.Add(meta);
            card.Add(texts);

            pane.Standing.AddToClassList("sc-lb-head__standing");
            card.Add(pane.Standing);
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

        /// <summary>Right edge of the header: "#3 / 1 250", "not ranked" or a dash while it loads.</summary>
        private void RenderStanding(BoardPane pane)
        {
            var host = pane.Standing;
            host.Clear();

            string rank;
            string caption;
            bool ranked = pane.Me != null && pane.Me.position > 0;
            if (ranked)
            {
                rank = "#" + pane.Me.position;
                caption = "your place · " + FormatScore(pane.Config, pane.Me.value);
            }
            else if (pane.MeLoaded)
            {
                rank = Fmt.Dash;
                caption = "no score yet";
            }
            else
            {
                rank = Fmt.Dash;
                caption = "loading…";
            }

            var big = new Label(rank);
            big.AddToClassList("sc-lb-head__rank");
            var tint = ranked ? MedalTint(pane.Me.position) : null;
            if (tint.HasValue)
            {
                big.style.color = tint.Value;
            }
            host.Add(big);

            var small = new Label(caption);
            small.AddToClassList("sc-lb-head__caption");
            host.Add(small);
        }

        // ----- submit card -----------------------------------------------------------------------------

        private VisualElement BuildSubmitCard(BoardPane pane)
        {
            var cfg = pane.Config;
            bool time = cfg.type == LeaderboardType.Time;

            var card = new VisualElement();
            card.AddToClassList("sc-lb-card");
            card.AddToClassList("sc-lb-submit");

            var head = new VisualElement();
            head.AddToClassList("sc-lb-card__head");
            var glyph = new Label(LucideIcon.Send);
            glyph.AddToClassList("sc-icon");
            glyph.AddToClassList("sc-lb-card__glyph");
            head.Add(glyph);
            var title = new Label("Submit a score");
            title.AddToClassList("sc-lb-card__title");
            head.Add(title);
            card.Add(head);

            var hint = new Label(SubmitHint(cfg));
            hint.enableRichText = false;
            hint.AddToClassList("sc-lb-card__hint");
            card.Add(hint);

            var row = new VisualElement();
            row.AddToClassList("sc-lb-submit__row");

            var field = new TextField { label = time ? "Seconds" : "Score", value = "1000" };
            field.AddToClassList("sc-field");
            field.AddToClassList("sc-lb-submit__field");
            row.Add(field);

            var button = new Button { text = "Submit" };
            button.AddToClassList("sc-btn");
            button.AddToClassList("sc-btn--primary");
            button.AddToClassList("sc-lb-submit__btn");
            row.Add(button);

            var result = new Label();
            result.enableRichText = false;
            result.AddToClassList("sc-lb-submit__result");
            row.Add(result);
            card.Add(row);

            Action fire = () => Submit(pane, field, button, result);
            button.clicked += fire;
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    fire();
                }
            });
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

        private async void Submit(BoardPane pane, TextField field, Button button, Label result)
        {
            if (!button.enabledSelf)
            {
                return;
            }

            double score;
            // Parsed here rather than as a float: the SDK takes a double, and a score can easily be
            // larger than a float represents exactly.
            if (!double.TryParse((field.value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out score))
            {
                ShowResult(result, false, "Score must be a number (use a dot for decimals).");
                return;
            }

            button.SetEnabled(false);
            button.text = "Submitting…";
            ShowResult(result, true, null);

            string key = pane.Config.key;
            RestApiResult<LeaderboardEntryDto> response = null;
            try
            {
                var op = pane.Config.type == LeaderboardType.Time
                    ? Sdk.Leaderboard.SubmitScoreAsync(TimeSpan.FromSeconds(score), key)
                    : Sdk.Leaderboard.SubmitScoreAsync(score, key);
                await op.Task();
                response = op.Result;
            }
            catch (Exception e)
            {
                // async void: an exception escaping here would surface as an unhandled one.
                Debug.LogWarning("[Showcase] Leaderboard: submit threw: " + e.Message);
            }

            button.SetEnabled(true);
            button.text = "Submit";

            if (response == null)
            {
                ShowResult(result, false, "No response");
                return;
            }

            Ctx.Log?.Record("Leaderboard: submit score", response, SubmitSnippet);
            if (!response.IsSuccess)
            {
                bool notJoined = response.Error.HasCode(CloudErrorCodes.LeaderboardsParticipationRequired);
                if (notJoined)
                {
                    SetJoined(key, false);
                }
                ShowResult(result, false, notJoined
                    ? "Join the board first — scores are accepted from participants only."
                    : ErrorText(response));
                return;
            }

            SetJoined(key, true);
            var entry = response.Data;
            ShowResult(result, true, entry != null && entry.position > 0
                ? "You are #" + entry.position + " with " + FormatScore(pane.Config, entry.value)
                : "Submitted " + FormatScore(pane.Config, score));
            if (pane == _pane)
            {
                ReloadStandings(pane);
            }
        }

        private static void ShowResult(Label label, bool ok, string text)
        {
            label.text = text ?? string.Empty;
            label.EnableInClassList("sc-lb-submit__result--ok", ok);
            label.EnableInClassList("sc-lb-submit__result--bad", !ok);
        }

        // ----- standings card --------------------------------------------------------------------------

        private VisualElement BuildStandingsCard(BoardPane pane)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-lb-card");
            card.AddToClassList("sc-lb-standings");

            var head = new VisualElement();
            head.AddToClassList("sc-lb-card__head");
            var glyph = new Label(LucideIcon.Users);
            glyph.AddToClassList("sc-icon");
            glyph.AddToClassList("sc-lb-card__glyph");
            head.Add(glyph);
            var title = new Label("Standings");
            title.AddToClassList("sc-lb-card__title");
            head.Add(title);
            pane.Count.AddToClassList("sc-lb-card__count");
            head.Add(pane.Count);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            head.Add(spacer);

            head.Add(SliceSwitch(pane));
            card.Add(head);

            pane.EntriesSlot.AddToClassList("sc-lb-standings__body");
            card.Add(pane.EntriesSlot);
            return card;
        }

        /// <summary>Segmented slice picker; the active option wears <c>.sc-btn--primary</c>.</summary>
        private VisualElement SliceSwitch(BoardPane pane)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-lb-slices");

            var buttons = new List<Button>(SliceNames.Length);
            for (int i = 0; i < SliceNames.Length; i++)
            {
                var slice = (Slice)i;
                var btn = new Button { text = SliceNames[i] };
                btn.AddToClassList("sc-btn");
                btn.AddToClassList("sc-lb-slices__btn");
                btn.EnableInClassList("sc-btn--primary", slice == _slice);
                btn.clicked += () =>
                {
                    if (slice == _slice)
                    {
                        return;
                    }
                    _slice = slice;
                    for (int j = 0; j < buttons.Count; j++)
                    {
                        buttons[j].EnableInClassList("sc-btn--primary", j == (int)_slice);
                    }
                    LoadSlice(pane);
                };
                buttons.Add(btn);
                row.Add(btn);
            }
            return row;
        }

        private void RenderCount(BoardPane pane)
        {
            pane.Count.text = pane.EntriesLoaded ? Fmt.Number(pane.Entries) : string.Empty;
            pane.Count.style.display = pane.EntriesLoaded && pane.Entries > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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
                SetJoined(pane.Config.key, true);
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

        private void SetJoined(string key, bool joined)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            _joined[key] = joined;
            BoardRow row;
            if (_rows.TryGetValue(key, out row))
            {
                row.Render();
            }
        }

        private bool IsJoined(string key)
        {
            bool joined;
            return !string.IsNullOrEmpty(key) && _joined.TryGetValue(key, out joined) && joined;
        }

        private async void Join(BoardRow row)
        {
            var cfg = row.Config;
            row.SetBusy(true);

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

            row.SetBusy(false);
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

            SetJoined(cfg.key, true);
            var entry = result.Data;
            Toasts?.Ok(entry != null && entry.position > 0
                ? "Joined " + BoardTitle(cfg) + " — you are #" + entry.position
                : "Joined " + BoardTitle(cfg) + " — submit a score to get a place");
            ReloadIfSelected(cfg.key);
        }

        private void ConfirmLeave(BoardRow row)
        {
            ConfirmDialog.Open(Popup, "Leave " + BoardTitle(row.Config),
                "The player stops being a participant, and their score of the current session is removed "
                + "from the board.",
                "Leave", () => Leave(row));
        }

        private async void Leave(BoardRow row)
        {
            var cfg = row.Config;
            row.SetBusy(true);

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

            row.SetBusy(false);
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

            SetJoined(cfg.key, false);
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

        /// <summary>Null for anything below third place.</summary>
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
        /// One board in the sidebar: a click selects it, the trailing button joins or leaves it. The
        /// button reads <see cref="_joined"/> on every render, so a join made from anywhere on the
        /// screen (a submit that succeeded, an entry that loaded) flips it.
        /// </summary>
        private sealed class BoardRow
        {
            public readonly LeaderboardConfigDto Config;
            public readonly VisualElement Root;

            private readonly LeaderboardView _owner;
            private readonly Button _action;
            private bool _busy;

            public BoardRow(LeaderboardView owner, LeaderboardConfigDto config)
            {
                _owner = owner;
                Config = config;

                Root = new VisualElement();
                Root.AddToClassList("sc-lb-board");

                var glyph = new Label(LucideIcon.Trophy);
                glyph.AddToClassList("sc-icon");
                glyph.AddToClassList("sc-lb-board__glyph");
                Root.Add(glyph);

                var texts = new VisualElement();
                texts.AddToClassList("sc-lb-board__texts");
                var name = new Label(BoardTitle(config));
                name.enableRichText = false;
                name.AddToClassList("sc-lb-board__name");
                texts.Add(name);
                var sub = new Label(config.key);
                sub.enableRichText = false;
                sub.AddToClassList("sc-lb-board__sub");
                texts.Add(sub);
                Root.Add(texts);

                _action = new Button(OnAction);
                _action.AddToClassList("sc-btn");
                _action.AddToClassList("sc-lb-board__btn");
                Root.Add(_action);

                Root.RegisterCallback<ClickEvent>(e =>
                {
                    // the trailing button handles its own click
                    if (e.target is Button)
                    {
                        return;
                    }
                    _owner.SelectBoard(Config);
                });

                Render();
            }

            public void Render()
            {
                bool joined = _owner.IsJoined(Config.key);
                Root.EnableInClassList("sc-lb-board--joined", joined);
                _action.text = _busy ? "…" : joined ? "Leave" : "Join";
                _action.EnableInClassList("sc-lb-board__btn--join", !joined);
                _action.EnableInClassList("sc-lb-board__btn--leave", joined);
                _action.SetEnabled(!_busy);
                _action.tooltip = joined
                    ? "Leave the board — removes your score of this session"
                    : "Join the board — scores are accepted from participants only";
            }

            public void SetBusy(bool busy)
            {
                _busy = busy;
                Render();
            }

            private void OnAction()
            {
                if (_busy)
                {
                    return;
                }
                if (_owner.IsJoined(Config.key))
                {
                    _owner.ConfirmLeave(this);
                }
                else
                {
                    _owner.Join(this);
                }
            }
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
            public readonly Label Count = new Label();

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

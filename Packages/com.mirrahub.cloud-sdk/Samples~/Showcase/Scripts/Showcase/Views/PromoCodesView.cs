using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.PromoCodes.Dto;
using MirraCloud.Core.PromoCodes.Enums;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Promo Codes screen: the effects a campaign left running on this player, everything they have
    /// redeemed so far, and the one call that redeems a code.
    /// <para>
    /// Redeeming has two gates rather than one: the transport can fail, and a perfectly successful
    /// response can still refuse the code through <c>status</c>. The Redeem tab is built around that
    /// distinction — it reports a refusal as a failure with the reason, and lists every reason the
    /// service can give, because mapping them to player-facing copy is the client's job.
    /// </para>
    /// <para>
    /// Every timestamp in these DTOs is an ISO-8601 string, and a null one means "never expires", so
    /// they all go through <see cref="ParseUtc"/> instead of being printed raw.
    /// </para>
    /// </summary>
    public sealed class PromoCodesView : ServiceView
    {
        private const string EffectsSnippet =
@"// Effects the player carries right now. A campaign can hand out a timed buff on top of (or
// instead of) an economy reward, and this is where the game reads it back.
var op = sdk.PromoCodes.GetActiveEffectsAsync();
await op.Task();

foreach (PromoActiveEffectDto effect in op.Result.Data)
{
    // effect.key        — what the effect is; the game switches on this
    // effect.metadata   — the campaign's own string values (multiplier, tier, skin id…)
    // effect.grantedAt  — ISO-8601 UTC
    // effect.expiresAt  — ISO-8601 UTC, or null for a permanent effect
    // effect.campaignId
}";

        private const string HistorySnippet =
@"// The signed-in player's own redemptions, newest first. Successes only — a refused code
// never lands here, its reason comes back from RedeemAsync instead.
var op = sdk.PromoCodes.GetHistoryAsync(limit: 50);
await op.Task();

foreach (PromoHistoryItemDto item in op.Result.Data)
{
    // item.campaignKey, item.campaignDisplayName, item.campaignId
    // item.redeemedAt       — ISO-8601 UTC
    // item.effectExpiresAt  — null when the campaign granted no timed effect
}";

        private const string RedeemSnippet =
@"// Two gates, not one: the transport has to succeed AND the payload's status has to be
// Success. A wrong, spent or blocked code answers 200 with a status — not an HTTP error.
var op = sdk.PromoCodes.RedeemAsync(""SUMMER2026"");
await op.Task();

if (!op.Result.IsSuccess)
{
    // network / auth / server trouble — read op.Result.Error
    return;
}

RedeemPromoCodeResponseDto r = op.Result.Data;
if (r.status != RedemptionStatus.Success)
{
    // InvalidCode, Expired, NotYetActive, Disabled, LimitExceeded, RuleFailed,
    // AlreadyRedeemed, CodeBlocked — turn it into copy the player understands
    return;
}

// r.campaignKey / r.campaignDisplayName
// r.rewards: rewardId, economyResourceKind (1 currency, 2 item, 5 energy), count
// r.effects: key, expiresAt (null = permanent), metadata
await sdk.PromoCodes.GetActiveEffectsAsync().Task();   // the new effect is live now";

        private const int DefaultLimit = 50;

        // Backend EconomyResourceKind, which GrantedRewardDto carries as a plain int (the SDK's own
        // Economy enum uses different numbers, so it must not be reused for this).

        private Tabs _tabs;
        private KpiRow _kpis;
        private PromoActiveEffectDto[] _effects;
        private PromoHistoryItemDto[] _history;
        private string _query = string.Empty;
        private int _limit = DefaultLimit;

        public PromoCodesView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            // A rebuild drops the panes, so the cached payloads and the KPI strip they filled go too.
            _query = string.Empty;
            _effects = null;
            _history = null;
            _kpis = null;

            DeclareCall(new SdkCall("Read the active effects", EffectsSnippet,
                "expiresAt is null for an effect that never runs out."));
            DeclareCall(new SdkCall("Read the redemption history", HistorySnippet));
            DeclareCall(new SdkCall("Redeem a code", RedeemSnippet,
                "IsSuccess only says the request arrived — status says whether the code was accepted."));

            UseToolbar()
                .WithSearch("Filter redemptions by campaign", OnSearch)
                .WithFilter("History limit", new[] { "25", "50", "100" }, OnLimit, _limit.ToString())
                .WithSpacer()
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Active effects", LucideIcon.Sparkles, BuildEffects)
                .Add("History", LucideIcon.History, BuildHistory)
                .Add("Redeem", LucideIcon.TicketPercent, BuildRedeem);
        }

        private void OnSearch(string text)
        {
            _query = text == null ? string.Empty : text.Trim();
            _tabs.Invalidate(1);
            _tabs.Select(1);
        }

        private void OnLimit(string value)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                || parsed <= 0)
            {
                return;
            }
            _limit = parsed;
            // Both tabs read the history: one for the table, the other for the redemption tiles.
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
        }

        // ----- active effects ---------------------------------------------------------------------

        private VisualElement BuildEffects()
        {
            var host = new VisualElement();

            // The KPI strip counts both reads, so it is bound to the history call while the effects
            // call below refills it in place once it answers. Either order works: whichever lands
            // second calls SyncKpis and the strip ends up complete.
            var kpiSlot = new VisualElement();
            kpiSlot.AddToClassList("sc-pc-kpis");
            host.Add(kpiSlot);

            ViewBind.Load(
                () => Sdk.PromoCodes.GetHistoryAsync(_limit),
                kpiSlot,
                history =>
                {
                    _history = history;
                    _kpis = new KpiRow();
                    SyncKpis();
                    SyncStatus();
                    return _kpis;
                },
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Promo history",
                    Snippet = HistorySnippet,
                    ServiceName = "Promo Codes",
                    AllowRetry = true,
                });

            host.Add(new SectionHeader("Effects in force"));

            var slot = new VisualElement();
            host.Add(slot);

            ViewBind.Load(
                () => Sdk.PromoCodes.GetActiveEffectsAsync(),
                slot,
                effects =>
                {
                    _effects = effects;
                    SyncKpis();
                    SyncStatus();
                    return EffectGrid(effects);
                },
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Active promo effects",
                    Snippet = EffectsSnippet,
                    ServiceName = "Promo Codes",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        _effects = Array.Empty<PromoActiveEffectDto>();
                        SyncKpis();
                        SyncStatus();
                        return ZeroState.Cards(LucideIcon.Sparkles,
                            "Nothing is boosting this player right now. A campaign that grants a timed "
                            + "effect puts it here until it expires — and the server drops it from this "
                            + "answer the moment it does, so the game never has to check the clock.",
                            3, "Redeem a code", () => _tabs.Select(2));
                    },
                });

            return host;
        }

        private VisualElement EffectGrid(PromoActiveEffectDto[] effects)
        {
            var grid = new VisualElement();
            grid.AddToClassList("sc-pc-grid");
            foreach (var effect in effects)
            {
                grid.Add(EffectCard(effect));
            }
            return grid;
        }

        private VisualElement EffectCard(PromoActiveEffectDto effect)
        {
            var expires = ParseUtc(effect.expiresAt);

            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-pc-effect");
            card.WithTitle(Fmt.Truncate(Fmt.OrDash(effect.key), 28), Meta.Accent);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            if (expires.HasValue)
            {
                // The chip re-renders once a second, so an effect visibly runs out while the tab is open.
                chips.Add(new CountdownChip(expires));
            }
            else
            {
                chips.Add(new Chip("permanent", ChipTone.Ok));
            }
            card.Body.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.AddToClassList("sc-pc-kv");
            kv.Add(Kv("Granted", Fmt.DateTime2(ParseUtc(effect.grantedAt)), null));
            kv.Add(Kv("Expires", expires.HasValue ? Fmt.DateTime2(expires) : "never", null));
            kv.Add(Kv("Campaign", Fmt.Id(effect.campaignId, 10), effect.campaignId));
            card.Body.Add(kv);

            // The key says what kind of effect this is; the metadata carries its value. Both are
            // project-defined strings — the SDK deliberately has no opinion about either.
            if (effect.metadata != null && effect.metadata.Count > 0)
            {
                card.Body.Add(new SectionHeader("Values", effect.metadata.Count.ToString()));
                var values = new VisualElement();
                values.AddToClassList("sc-kv-list");
                values.AddToClassList("sc-pc-kv");
                foreach (var pair in effect.metadata)
                {
                    values.Add(Kv(pair.Key, Fmt.Truncate(Fmt.OrDash(pair.Value), 36), null));
                }
                card.Body.Add(values);
            }
            else
            {
                var note = new Label("The campaign attached no values to this effect, so the key alone "
                    + "is what the game reads.");
                note.AddToClassList("sc-pc-note");
                card.Body.Add(note);
            }

            return card;
        }

        // ----- history --------------------------------------------------------------------------

        private VisualElement BuildHistory()
        {
            var host = new VisualElement();

            var hint = new Label("Successful redemptions only, newest first — the call is capped at "
                + _limit + " rows (the toolbar changes it) and the search box filters what came back. "
                + "A refused code leaves no trace here; its reason arrives from RedeemAsync.");
            hint.AddToClassList("sc-fs-hint");
            host.Add(hint);

            var slot = new VisualElement();
            host.Add(slot);

            ViewBind.Load(
                () => Sdk.PromoCodes.GetHistoryAsync(_limit),
                slot,
                rows =>
                {
                    _history = rows;
                    SyncKpis();
                    SyncStatus();
                    return HistoryBody(rows);
                },
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Promo history",
                    Snippet = HistorySnippet,
                    ServiceName = "Promo Codes",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        _history = Array.Empty<PromoHistoryItemDto>();
                        SyncKpis();
                        SyncStatus();
                        return ZeroState.Table(HistoryColumns(),
                            "This player has redeemed nothing yet. Every accepted code adds one row, "
                            + "carrying the campaign it belonged to and the effect it left behind.",
                            3, "Redeem a code", () => _tabs.Select(2));
                    },
                });

            return host;
        }

        private VisualElement HistoryBody(PromoHistoryItemDto[] rows)
        {
            var matched = Filter(rows);
            var col = new VisualElement();

            col.Add(new SectionHeader("Redemptions",
                matched.Count == rows.Length
                    ? matched.Count.ToString()
                    : matched.Count + " of " + rows.Length));

            if (matched.Count == 0)
            {
                col.Add(ZeroState.Table(HistoryColumns(),
                    "Nothing redeemed matches \"" + Fmt.Truncate(_query, 24) + "\". Clearing the search "
                    + "box brings back all " + rows.Length + " rows — the filter is applied here, not "
                    + "by the server.",
                    3));
                return col;
            }

            var table = new DataTable(HistoryColumns())
                .WithZebra()
                .WithMaxHeight(520f)
                .WithSort(2, false);
            table.Bind(matched, o => IsLive(ParseUtc(((PromoHistoryItemDto)o).effectExpiresAt)));
            col.Add(table);
            return col;
        }

        private List<PromoHistoryItemDto> Filter(PromoHistoryItemDto[] rows)
        {
            var kept = new List<PromoHistoryItemDto>();
            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }
                if (_query.Length == 0
                    || Contains(row.campaignKey, _query)
                    || Contains(row.campaignDisplayName, _query))
                {
                    kept.Add(row);
                }
            }
            return kept;
        }

        private DataColumn[] HistoryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "CAMPAIGN", Grow = 2f,
                    SortKey = o => CampaignName((PromoHistoryItemDto)o),
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(CampaignName((PromoHistoryItemDto)o), 32));
                        label.enableRichText = false;
                        label.AddToClassList("sc-pc-strong");
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "KEY", Grow = 1.4f,
                    SortKey = o => ((PromoHistoryItemDto)o).campaignKey,
                    Cell = o =>
                    {
                        var item = (PromoHistoryItemDto)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-row-actions");
                        box.style.justifyContent = Justify.FlexStart;

                        var label = new Label(Fmt.Truncate(Fmt.OrDash(item.campaignKey), 20));
                        label.enableRichText = false;
                        box.Add(label);

                        if (!string.IsNullOrEmpty(item.campaignKey))
                        {
                            box.Add(new CopyButton(item.campaignKey, Toasts));
                        }
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "REDEEMED", Grow = 1.4f, Align = "right",
                    SortKey = o => ParseUtc(((PromoHistoryItemDto)o).redeemedAt) ?? DateTime.MinValue,
                    Cell = o => new Label(Fmt.DateTime2(ParseUtc(((PromoHistoryItemDto)o).redeemedAt))),
                },
                new DataColumn
                {
                    Header = "RESULT", FixedWidth = true, Px = 104,
                    Cell = o => new Chip("redeemed", ChipTone.Ok),
                },
                new DataColumn
                {
                    Header = "GRANTED", Grow = 1.5f, Align = "right",
                    SortKey = o => ParseUtc(((PromoHistoryItemDto)o).effectExpiresAt) ?? DateTime.MinValue,
                    Cell = o =>
                    {
                        var expires = ParseUtc(((PromoHistoryItemDto)o).effectExpiresAt);
                        if (!expires.HasValue)
                        {
                            // No effect window means the campaign paid out in rewards alone.
                            var none = new Label("rewards only");
                            none.AddToClassList("sc-pc-dim");
                            return none;
                        }
                        if (IsLive(expires))
                        {
                            return new CountdownChip(expires);
                        }
                        var ended = new Label("effect ended " + Fmt.Date(expires));
                        ended.AddToClassList("sc-pc-dim");
                        return ended;
                    },
                },
            };
        }

        // ----- redeem ---------------------------------------------------------------------------

        private VisualElement BuildRedeem()
        {
            var col = new VisualElement();

            var hint = new Label("One code, one player, usually once. The call reports a refusal inside a "
                + "successful response, so this card checks the transport and the status separately — "
                + "exactly what a game has to do before it shows the player anything.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Redeem a code",
                    "Grants the campaign's rewards and effects to the signed-in player, then reloads "
                    + "the other two tabs so the result is visible rather than described.",
                    LucideIcon.TicketPercent)
                .WithFields(FormField.Text("code", "Promo code", null, true)
                    .WithPlaceholder("SUMMER2026"))
                .WithSnippet(RedeemSnippet)
                .OnRun("Redeem", Redeem));

            col.Add(new SectionHeader("Why a code gets refused"));

            var reference = new Label("Each of these arrives as a successful HTTP response carrying its own "
                + "status. The SDK mirrors them as the RedemptionStatus enum.");
            reference.AddToClassList("sc-fs-hint");
            col.Add(reference);
            col.Add(StatusReference());

            return col;
        }

        private async Task<ActionOutcome> Redeem(FormValues values)
        {
            string typed = values.Text("code");
            string code = typed == null ? string.Empty : typed.Trim();
            if (code.Length == 0)
            {
                return ActionOutcome.Failure("Type a code first.");
            }

            var op = Sdk.PromoCodes.RedeemAsync(code);
            var outcome = await AwaitData(op, "Promo codes · redeem");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var response = op.Result.Data;
            if (response == null)
            {
                return ActionOutcome.Failure("The call succeeded but carried no payload.");
            }

            if (response.status != RedemptionStatus.Success)
            {
                // The refusal path: HTTP said yes, the service said no. Nothing changed, so no tab
                // is invalidated and the reason goes straight back to the reader.
                if (Toasts != null)
                {
                    Toasts.Fail("Not redeemed · " + StatusLabel(response.status));
                }
                return ActionOutcome.Failure(StatusLabel(response.status)
                    + " (status " + response.status + ") · " + StatusMeaning(response.status));
            }

            // Both other tabs describe state this call just changed, so neither may keep its pane.
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            if (Toasts != null)
            {
                Toasts.Ok("Redeemed " + Fmt.Truncate(code, 18));
            }
            return ActionOutcome.Success(CampaignName(response) + " redeemed", Granted(response));
        }

        /// <summary>
        /// The granted payload is the reason the call exists, so it is shown rather than counted.
        /// </summary>
        private VisualElement Granted(RedeemPromoCodeResponseDto response)
        {
            var box = new VisualElement();

            int rewards = response.rewards != null ? response.rewards.Count : 0;
            int effects = response.effects != null ? response.effects.Count : 0;

            if (rewards + effects == 0)
            {
                var nothing = new Label("The campaign granted nothing beyond marking the code as used — "
                    + "a tracking-only campaign looks exactly like this.");
                nothing.enableRichText = false;
                nothing.AddToClassList("sc-pc-note");
                box.Add(nothing);
                return box;
            }

            if (rewards > 0)
            {
                box.Add(Caption("Rewards"));
                var row = new VisualElement();
                row.AddToClassList("sc-chip-row");
                foreach (var reward in response.rewards)
                {
                    row.Add(new RewardChip(RewardGlyph(reward.economyResourceKind),
                        Fmt.Truncate(Fmt.OrDash(reward.rewardId), 20) + " ×" + reward.count,
                        Meta.Accent));
                }
                box.Add(row);
            }

            if (effects > 0)
            {
                box.Add(Caption("Effects"));
                var row = new VisualElement();
                row.AddToClassList("sc-chip-row");
                foreach (var effect in response.effects)
                {
                    row.Add(new Chip(Fmt.Truncate(Fmt.OrDash(effect.key), 22), ChipTone.Accent));
                    var expires = ParseUtc(effect.expiresAt);
                    if (expires.HasValue)
                    {
                        row.Add(new CountdownChip(expires));
                    }
                    else
                    {
                        row.Add(new Chip("permanent", ChipTone.Ok));
                    }
                }
                box.Add(row);
            }

            return box;
        }

        private static VisualElement StatusReference()
        {
            var list = new VisualElement();
            list.AddToClassList("sc-pc-statuses");
            list.Add(StatusRow(RedemptionStatus.InvalidCode));
            list.Add(StatusRow(RedemptionStatus.Expired));
            list.Add(StatusRow(RedemptionStatus.NotYetActive));
            list.Add(StatusRow(RedemptionStatus.Disabled));
            list.Add(StatusRow(RedemptionStatus.LimitExceeded));
            list.Add(StatusRow(RedemptionStatus.RuleFailed));
            list.Add(StatusRow(RedemptionStatus.AlreadyRedeemed));
            list.Add(StatusRow(RedemptionStatus.CodeBlocked));
            return list;
        }

        private static VisualElement StatusRow(RedemptionStatus status)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-pc-status");

            // The chip sits in a fixed lane so the explanations line up with one another.
            var lane = new VisualElement();
            lane.AddToClassList("sc-pc-status__lane");
            lane.Add(new Chip(StatusLabel(status), StatusTone(status)));
            row.Add(lane);

            var text = new Label(StatusMeaning(status));
            text.enableRichText = false;
            text.AddToClassList("sc-pc-status__text");
            row.Add(text);

            var code = new Label("status " + status);
            code.AddToClassList("sc-pc-status__code");
            row.Add(code);

            return row;
        }

        // ----- header and KPIs ------------------------------------------------------------------

        /// <summary>
        /// Refills the KPI strip from whichever of the two reads has answered. Called by both, so the
        /// slower one completes the strip instead of replacing it.
        /// </summary>
        private void SyncKpis()
        {
            if (_kpis == null)
            {
                return;
            }
            _kpis.Clear2();

            if (_effects == null)
            {
                _kpis.Add("Active effects", LucideIcon.Sparkles, Fmt.Dash, "still loading");
            }
            else if (_effects.Length == 0)
            {
                _kpis.AddZero("Active effects", LucideIcon.Sparkles);
            }
            else
            {
                _kpis.Add("Active effects", LucideIcon.Sparkles, _effects.Length.ToString(), null, true);
            }

            int redeemed = _history != null ? _history.Length : 0;
            if (redeemed == 0)
            {
                _kpis.AddZero("Redeemed", LucideIcon.TicketPercent);
                _kpis.AddZero("Last redemption", LucideIcon.Clock, Fmt.Dash);
                return;
            }

            // The count is what the limit let through, not the player's lifetime total — say so
            // rather than let the tile imply a number the call never promised.
            _kpis.Add("Redeemed", LucideIcon.TicketPercent, Fmt.Number(redeemed),
                redeemed >= _limit ? "capped at " + _limit : null);

            var latest = LatestRedemption();
            _kpis.Add("Last redemption", LucideIcon.Clock, Fmt.Date(latest),
                latest.HasValue ? RelativeTime.Format(latest.Value) : null);
        }

        private void SyncStatus()
        {
            var parts = new List<string>();
            if (_effects != null)
            {
                parts.Add(_effects.Length + (_effects.Length == 1 ? " effect" : " effects"));
            }
            if (_history != null)
            {
                parts.Add(_history.Length + " redeemed");
            }
            if (parts.Count == 0)
            {
                return;
            }

            bool anything = (_effects != null && _effects.Length > 0)
                || (_history != null && _history.Length > 0);
            SetStatus(string.Join(" · ", parts.ToArray()), anything ? ChipTone.Ok : ChipTone.Neutral);
        }

        /// <summary>Newest redemption in the page we hold; the order the server sends is not assumed.</summary>
        private DateTime? LatestRedemption()
        {
            if (_history == null)
            {
                return null;
            }
            DateTime? latest = null;
            foreach (var item in _history)
            {
                if (item == null)
                {
                    continue;
                }
                var when = ParseUtc(item.redeemedAt);
                if (when.HasValue && (!latest.HasValue || when.Value > latest.Value))
                {
                    latest = when;
                }
            }
            return latest;
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.enableRichText = false;
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

        private static Label Caption(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sc-pc-caption");
            return label;
        }

        private static string CampaignName(PromoHistoryItemDto item)
        {
            if (item == null)
            {
                return Fmt.Dash;
            }
            return string.IsNullOrWhiteSpace(item.campaignDisplayName)
                ? Fmt.OrDash(item.campaignKey)
                : item.campaignDisplayName;
        }

        private static string CampaignName(RedeemPromoCodeResponseDto response)
        {
            if (response == null)
            {
                return Fmt.Dash;
            }
            return string.IsNullOrWhiteSpace(response.campaignDisplayName)
                ? Fmt.OrDash(response.campaignKey)
                : response.campaignDisplayName;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Promo DTOs carry ISO-8601 strings rather than <see cref="DateTime"/>, and a null one means
        /// "no expiry". Parsed as UTC whether or not the text carries an offset, because the fields
        /// are documented as UTC and the countdown chip compares against <c>DateTime.UtcNow</c>.
        /// </summary>
        private static DateTime? ParseUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
            {
                return null;
            }
            DateTime parsed;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
            {
                return null;
            }
            return parsed;
        }

        private static bool IsLive(DateTime? expiresUtc)
        {
            return expiresUtc.HasValue && expiresUtc.Value > DateTime.UtcNow;
        }

        private static string RewardGlyph(PromoRewardKind economyResourceKind)
        {
            switch (economyResourceKind)
            {
                case PromoRewardKind.Currency: return LucideIcon.Coins;
                case PromoRewardKind.Item: return LucideIcon.Package;
                case PromoRewardKind.Energy: return LucideIcon.Zap;
                default: return LucideIcon.Gift;
            }
        }

        private static string StatusLabel(RedemptionStatus status)
        {
            switch (status)
            {
                case RedemptionStatus.Success: return "success";
                case RedemptionStatus.InvalidCode: return "invalid code";
                case RedemptionStatus.Expired: return "expired";
                case RedemptionStatus.NotYetActive: return "not yet active";
                case RedemptionStatus.Disabled: return "disabled";
                case RedemptionStatus.LimitExceeded: return "limit exceeded";
                case RedemptionStatus.RuleFailed: return "rule failed";
                case RedemptionStatus.AlreadyRedeemed: return "already redeemed";
                case RedemptionStatus.CodeBlocked: return "code blocked";
                default: return "status " + status;
            }
        }

        private static string StatusMeaning(RedemptionStatus status)
        {
            switch (status)
            {
                case RedemptionStatus.Success:
                    return "The code was accepted and everything it grants has been applied.";
                case RedemptionStatus.InvalidCode:
                    return "No campaign in this project owns that code.";
                case RedemptionStatus.Expired:
                    return "The campaign's window has closed.";
                case RedemptionStatus.NotYetActive:
                    return "The code is real, but its campaign starts later.";
                case RedemptionStatus.Disabled:
                    return "The campaign was switched off in the Mirra Hub console.";
                case RedemptionStatus.LimitExceeded:
                    return "The campaign, or this player's share of it, is used up.";
                case RedemptionStatus.RuleFailed:
                    return "The player does not satisfy the campaign's rules — segment, level, platform.";
                case RedemptionStatus.AlreadyRedeemed:
                    return "This player has used the code before.";
                case RedemptionStatus.CodeBlocked:
                    return "That one code was blocked while the campaign itself stays open.";
                default:
                    return "The service refused the code without a reason this SDK version knows.";
            }
        }

        private static ChipTone StatusTone(RedemptionStatus status)
        {
            switch (status)
            {
                case RedemptionStatus.Success:
                    return ChipTone.Ok;
                case RedemptionStatus.Expired:
                case RedemptionStatus.NotYetActive:
                case RedemptionStatus.LimitExceeded:
                case RedemptionStatus.AlreadyRedeemed:
                    return ChipTone.Warn;
                default:
                    return ChipTone.Bad;
            }
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
    }
}

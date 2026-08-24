using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.DailyRewards.Dto;
using MirraCloud.Core.DailyRewards.Enums;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Daily Rewards screen: one tab per calendar the project defines, each showing what the designer
    /// authored (cycle rules, day rewards, streak bonuses, milestones) next to where this player
    /// actually stands on it — plus the one write the service has, <c>ClaimAsync</c>.
    /// <para>
    /// A claim only reloads the status section of its own pane, not the whole tab: the claim card sits
    /// in that pane, and rebuilding the tab would wipe the granted-rewards result the reader just
    /// asked for. The screen-level chip is refreshed from the all-calendars status call instead.
    /// </para>
    /// </summary>
    public sealed class DailyRewardsView : ServiceView
    {
        // Reward kinds arrive as plain ints here (the DTO has no enum). The values mirror the backend
        // economy resource kinds, same as Purchases' PurchaseRewardKind: 1 currency, 2 item, 5 energy.
        private const int KindCurrency = 1;
        private const int KindItem = 2;
        private const int KindEnergy = 5;

        private const string CalendarsSnippet =
@"// Every calendar configured for this project + branch, with the days the designer authored.
var op = sdk.DailyRewards.GetCalendarsAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (DailyRewardCalendarDto c in op.Result.Data)
{
    // c.id / c.key / c.name / c.isEnabled
    // c.cycleLengthDays, c.resetHourUtc, c.requireManualClaim
    // c.missedDayBehavior, c.cycleCompletionBehavior, c.nextCalendarId
    // c.dayRewards[i].rewards, c.streakBonuses, c.milestoneRewards, c.segmentKeys
}";

        private const string AllStatusSnippet =
@"// The player's progress on every calendar at once — enough to decide whether the game should
// show a ""reward waiting"" badge, without asking per calendar.
var op = sdk.DailyRewards.GetStatusAsync();
await op.Task();

foreach (DailyRewardStatusDto s in op.Result.Data)
{
    if (s.canClaimToday) { /* nudge the player towards s.calendarKey */ }
}";

        private const string StatusSnippet =
@"// One calendar in full: where the player stands and what every day of the cycle holds.
var op = sdk.DailyRewards.GetStatusAsync(calendarId);
await op.Task();

DailyRewardStatusDto s = op.Result.Data;
// s.currentDayNumber / s.currentCycle / s.totalClaimDays / s.lastClaimDate
// s.canClaimToday, s.nextResetTime (UTC), s.isCompleted
// s.days[i].status: Available | Claimed | Missed | Locked | CatchUpAvailable
// s.streakBonuses, s.milestoneProgress";

        private const string ClaimSnippet =
@"// dayNumber is optional: null claims whichever day the calendar says is due. Naming a day only
// works while that day is still Available or CatchUpAvailable.
var op = sdk.DailyRewards.ClaimAsync(calendarId, dayNumber: null);
await op.Task();
if (!op.Result.IsSuccess) { return; }

ClaimDailyRewardResponseDto got = op.Result.Data;
// got.dayNumberClaimed, got.newTotalClaimDays
// got.baseRewards / got.streakBonusRewards / got.milestoneRewards : rewardId, count
// got.cycleCompleted, got.nextCalendarId — set when the cycle rolled into another calendar";

        private readonly List<CalendarPane> _panes = new List<CalendarPane>();

        private Tabs _tabs;
        private int _calendarCount;

        /// <summary>Calendars with something to claim, or -1 while the all-calendars call is in flight.</summary>
        private int _readyCount = -1;

        public DailyRewardsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _panes.Clear();
            _tabs = null;
            _calendarCount = 0;
            _readyCount = -1;
            SetStatus(null);

            DeclareCall(new SdkCall("List the calendars", CalendarsSnippet,
                "Call it once: every other daily-rewards call needs a calendar id from here."));
            DeclareCall(new SdkCall("Read every calendar's status", AllStatusSnippet,
                "This screen uses it for the header chip — one call answers \"is anything claimable?\"."));
            DeclareCall(new SdkCall("Read one calendar's status", StatusSnippet));
            DeclareCall(new SdkCall("Claim a reward", ClaimSnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            // Zero margin: on success the calendars land in the tab strip and the panes at the bottom
            // of Content, so this slot only ever carries the loading / empty / failure state.
            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.DailyRewards.GetCalendarsAsync(),
                slot,
                BuildCalendars,
                c => c == null || c.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Daily reward calendars",
                    Snippet = CalendarsSnippet,
                    ServiceName = "Daily Rewards",
                    // This is the configuration call, so a 404 here really does mean "this project
                    // has no login calendar" rather than "this player has no progress".
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoCalendars,
                });
        }

        // ----- calendars ----------------------------------------------------------------------------

        private VisualElement BuildCalendars(DailyRewardCalendarDto[] calendars)
        {
            _calendarCount = calendars.Length;
            SyncStatus();
            LoadReadyCount();

            // A single calendar needs no tab strip — that pane *is* the screen.
            if (calendars.Length == 1)
            {
                var only = new CalendarPane(calendars[0], -1);
                _panes.Add(only);
                var host = new VisualElement();
                host.Add(BuildPane(only));
                return host;
            }

            _tabs = UseTabs();
            for (int i = 0; i < calendars.Length; i++)
            {
                var pane = new CalendarPane(calendars[i], i);
                _panes.Add(pane);
                _tabs.Add(TabTitle(pane.Calendar), LucideIcon.CalendarDays, () => BuildPane(pane));
            }

            // The strip and its panes live outside this slot, so the slot renders nothing itself.
            return new VisualElement();
        }

        private VisualElement NoCalendars()
        {
            _calendarCount = 0;
            SyncStatus();
            return ZeroState.Panel(LucideIcon.CalendarDays, "No login calendar in this project",
                "A daily-rewards calendar is authored in the Mirra Hub console: how many days the cycle "
                + "has, what each day grants, the streak bonuses and the milestones. Create one there and "
                + "it shows up here as a tab, with this player's progress on it.",
                null, null,
                "Mirra Hub console → this project → Daily Rewards → new calendar.");
        }

        private static string TabTitle(DailyRewardCalendarDto calendar)
        {
            if (!string.IsNullOrWhiteSpace(calendar.name))
            {
                return Fmt.Truncate(calendar.name, 22);
            }
            return string.IsNullOrWhiteSpace(calendar.key) ? Fmt.Id(calendar.id) : Fmt.Truncate(calendar.key, 22);
        }

        private void SyncStatus()
        {
            if (_calendarCount == 0)
            {
                SetStatus("Not configured", ChipTone.Warn);
                return;
            }

            string calendars = _calendarCount + (_calendarCount == 1 ? " calendar" : " calendars");
            if (_readyCount < 0)
            {
                SetStatus(calendars, ChipTone.Neutral);
                return;
            }
            SetStatus(_readyCount > 0
                    ? calendars + " · " + _readyCount + " ready to claim"
                    : calendars + " · nothing to claim",
                _readyCount > 0 ? ChipTone.Ok : ChipTone.Neutral);
        }

        /// <summary>
        /// Refines the header chip from the all-calendars status call. Bound by hand rather than
        /// through <see cref="ViewBind"/>: there is no slot to fill, and a failure here must not take
        /// the panes down with it — the chip simply keeps showing the calendar count.
        /// </summary>
        private async void LoadReadyCount()
        {
            RestApiResult<DailyRewardStatusDto[]> result = null;
            try
            {
                var op = Sdk.DailyRewards.GetStatusAsync();
                if (op != null)
                {
                    await op.Task();
                    result = op.Result;
                }
            }
            catch (Exception e)
            {
                // async void: an exception escaping here would surface as an unhandled one instead of
                // as a slightly less informative chip.
                Debug.LogWarning("[Showcase] Daily rewards: reading every calendar's status failed: " + e.Message);
            }

            if (result != null)
            {
                Ctx.Log?.Record("Daily rewards: every status", result, AllStatusSnippet);
                if (result.IsSuccess)
                {
                    _readyCount = CountReady(result.Data);
                }
            }
            SyncStatus();
        }

        private static int CountReady(DailyRewardStatusDto[] statuses)
        {
            if (statuses == null)
            {
                return 0;
            }
            int ready = 0;
            foreach (var status in statuses)
            {
                if (status != null && status.canClaimToday)
                {
                    ready++;
                }
            }
            return ready;
        }

        // ----- one calendar -------------------------------------------------------------------------

        private VisualElement BuildPane(CalendarPane pane)
        {
            var root = new VisualElement();
            root.Add(CalendarCard(pane.Calendar));

            pane.StatusSlot = new VisualElement();
            root.Add(pane.StatusSlot);
            LoadStatus(pane);

            root.Add(ClaimSection(pane));
            return root;
        }

        private VisualElement CalendarCard(DailyRewardCalendarDto calendar)
        {
            var card = new Card(Meta.Accent);
            card.WithTitle(string.IsNullOrWhiteSpace(calendar.name)
                ? Fmt.OrDash(calendar.key)
                : calendar.name, Meta.Accent);

            if (!string.IsNullOrEmpty(calendar.description))
            {
                var description = new Label(calendar.description);
                description.enableRichText = false;
                description.AddToClassList("sc-fs-hint");
                card.Body.Add(description);
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(calendar.isEnabled
                ? new Chip("enabled", ChipTone.Ok)
                : new Chip("disabled", ChipTone.Warn));
            chips.Add(new Chip(calendar.cycleLengthDays + "-day cycle", ChipTone.Info));
            chips.Add(new Chip(calendar.calendarType.ToString().ToLowerInvariant(), ChipTone.Neutral));
            chips.Add(new Chip(calendar.requireManualClaim ? "manual claim" : "claims automatically",
                calendar.requireManualClaim ? ChipTone.Accent : ChipTone.Neutral));
            chips.Add(new Chip("resets at " + ResetHour(calendar.resetHourUtc) + " UTC", ChipTone.Neutral));
            if (calendar.isExclusive)
            {
                chips.Add(new Chip("exclusive", ChipTone.Warn));
            }
            if (calendar.priority != 0)
            {
                chips.Add(new Chip("priority " + calendar.priority, ChipTone.Neutral));
            }
            if (calendar.segmentKeys != null)
            {
                foreach (var segment in calendar.segmentKeys)
                {
                    if (!string.IsNullOrEmpty(segment))
                    {
                        chips.Add(new Chip("segment " + Fmt.Truncate(segment, 18), ChipTone.Accent));
                    }
                }
            }
            card.Body.Add(chips);

            var rules = new Label("Missed days: " + Describe(calendar.missedDayBehavior)
                + (calendar.missedDayBehavior == MissedDayBehavior.AllowCatchUp
                    ? " (up to " + calendar.catchUpMaxDays + " day"
                        + (calendar.catchUpMaxDays == 1 ? ")" : "s)")
                    : string.Empty)
                + ". End of cycle: " + Describe(calendar.cycleCompletionBehavior) + ".");
            rules.enableRichText = false;
            rules.AddToClassList("sc-fs-hint");
            card.Body.Add(rules);

            if (!calendar.isEnabled)
            {
                var off = new Label("This calendar is switched off in the console, so the status call can "
                    + "report that there is nothing to claim even when a day is due.");
                off.enableRichText = false;
                off.AddToClassList("sc-fs-hint");
                card.Body.Add(off);
            }

            var ids = new VisualElement();
            ids.AddToClassList("sc-kv-list");
            ids.Add(Kv("Calendar id", Fmt.OrDash(calendar.id), calendar.id));
            ids.Add(Kv("Key", Fmt.OrDash(calendar.key), calendar.key));
            if (calendar.startDate.HasValue || calendar.endDate.HasValue)
            {
                ids.Add(Kv("Live window",
                    Fmt.DateTime2(calendar.startDate) + " → " + Fmt.DateTime2(calendar.endDate), null));
            }
            if (!string.IsNullOrEmpty(calendar.nextCalendarId))
            {
                ids.Add(Kv("Next calendar", Fmt.Id(calendar.nextCalendarId, 12), calendar.nextCalendarId));
            }
            card.Body.Add(ids);
            return card;
        }

        private static string ResetHour(int hourUtc)
        {
            int hour = hourUtc < 0 ? 0 : hourUtc % 24;
            return (hour < 10 ? "0" + hour : hour.ToString()) + ":00";
        }

        private static string Describe(MissedDayBehavior behavior)
        {
            switch (behavior)
            {
                case MissedDayBehavior.ResetToDay1: return "the cycle restarts at day 1";
                case MissedDayBehavior.SkipAndContinue: return "the day is skipped and the cycle continues";
                case MissedDayBehavior.AllowCatchUp: return "the missed day can still be claimed";
                default: return behavior.ToString();
            }
        }

        private static string Describe(CycleCompletionBehavior behavior)
        {
            switch (behavior)
            {
                case CycleCompletionBehavior.Repeat: return "the cycle repeats from day 1";
                case CycleCompletionBehavior.Stop: return "the calendar stops";
                case CycleCompletionBehavior.AdvanceToNext: return "the player moves to the next calendar";
                default: return behavior.ToString();
            }
        }

        // ----- status -------------------------------------------------------------------------------

        private void LoadStatus(CalendarPane pane)
        {
            string calendarId = pane.Calendar.id;
            ViewBind.Load(
                () => Sdk.DailyRewards.GetStatusAsync(calendarId),
                pane.StatusSlot,
                status => BuildStatus(pane, status),
                status => status == null,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Daily reward status",
                    Snippet = StatusSnippet,
                    ServiceName = "Daily Rewards",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.CalendarCheck, "No progress on this calendar",
                        "The server answered without a status, which is what happens before the player has "
                        + "any progress here — the first claim (or the first login, when the calendar claims "
                        + "automatically) creates it.",
                        null, null,
                        "A calendar limited to a segment also answers empty for players outside it."),
                });
        }

        private VisualElement BuildStatus(CalendarPane pane, DailyRewardStatusDto status)
        {
            var col = new VisualElement();

            if (pane.LastClaim != null)
            {
                col.Add(LastClaimCard(pane));
            }

            col.Add(Kpis(status));
            col.Add(StatusStrip(pane, status));

            var progress = new ProgressBar()
                .Set(status.currentDayNumber, status.cycleLengthDays)
                .SetLabel("Day " + status.currentDayNumber + " of " + status.cycleLengthDays
                    + " · cycle " + status.currentCycle)
                .SetAccent(Meta.Accent);
            progress.AddToClassList("sc-dr-progress");
            col.Add(progress);

            col.Add(TrackSection(status));
            col.Add(StreakSection(status));
            col.Add(MilestoneSection(status));
            return col;
        }

        private static KpiRow Kpis(DailyRewardStatusDto status)
        {
            var kpis = new KpiRow();

            if (status.currentDayNumber > 0)
            {
                kpis.Add("Streak", LucideIcon.Flame, "day " + status.currentDayNumber, null,
                    status.currentDayNumber > 1);
            }
            else
            {
                kpis.AddZero("Streak", LucideIcon.Flame, "not started");
            }

            if (status.canClaimToday)
            {
                kpis.Add("Claim now", LucideIcon.Gift, "Ready", null, true);
            }
            else
            {
                kpis.AddZero("Claim now", LucideIcon.Gift, status.isCompleted ? "Finished" : "Waiting");
            }

            kpis.Add("Cycle length", LucideIcon.List, status.cycleLengthDays + " days");

            if (status.totalClaimDays > 0)
            {
                kpis.Add("Days claimed", LucideIcon.CalendarCheck, Fmt.Number(status.totalClaimDays));
            }
            else
            {
                kpis.AddZero("Days claimed", LucideIcon.CalendarCheck);
            }
            return kpis;
        }

        private VisualElement StatusStrip(CalendarPane pane, DailyRewardStatusDto status)
        {
            var strip = new VisualElement();
            strip.AddToClassList("sc-dr-strip");

            if (status.canClaimToday)
            {
                strip.Add(new Chip("claimable now", ChipTone.Ok));
            }
            else if (status.isCompleted)
            {
                strip.Add(new Chip("calendar finished", ChipTone.Warn));
            }
            else
            {
                strip.Add(new Chip("claimed for today", ChipTone.Neutral));
            }

            if (!status.canClaimToday && status.nextResetTime.Ticks != 0L)
            {
                var countdown = new CountdownChip(Utc(status.nextResetTime));
                // RelativeTime.Absolute applies the same "unspecified means UTC" rule as Utc above,
                // so the tooltip and the countdown cannot disagree about the instant.
                countdown.tooltip = "Next reset: " + RelativeTime.Absolute(status.nextResetTime);
                strip.Add(countdown);
            }

            if (status.lastClaimDate.HasValue)
            {
                strip.Add(new Chip("last claim " + Fmt.Date(status.lastClaimDate), ChipTone.Neutral));
            }

            if (status.canClaimToday)
            {
                var claim = new Button { text = "Claim day " + status.currentDayNumber };
                claim.AddToClassList("sc-btn");
                claim.AddToClassList("sc-btn--primary");
                claim.clicked += () => QuickClaim(pane, status.currentDayNumber, claim);
                strip.Add(claim);
            }
            return strip;
        }

        // ----- day track ----------------------------------------------------------------------------

        private VisualElement TrackSection(DailyRewardStatusDto status)
        {
            var box = new VisualElement();
            var days = status.days;

            if (days == null || days.Length == 0)
            {
                box.Add(new SectionHeader("Reward track"));
                box.Add(ZeroState.Panel(LucideIcon.Gift, "This cycle has no days yet",
                    "The days of a cycle — and what each one grants — are authored per calendar in the "
                    + "Mirra Hub console. Add a day there and it appears on this track with its rewards, "
                    + "ready to be claimed."));
                return box;
            }

            int claimed = 0;
            foreach (var day in days)
            {
                if (day != null && day.status == DailyRewardClaimStatus.Claimed)
                {
                    claimed++;
                }
            }

            box.Add(new SectionHeader("Reward track", claimed + " of " + days.Length + " claimed"));

            var track = new VisualElement();
            track.AddToClassList("sc-day-track");
            foreach (var day in days)
            {
                if (day != null)
                {
                    track.Add(DayTile(day, day.dayNumber == status.currentDayNumber));
                }
            }
            box.Add(track);
            return box;
        }

        private VisualElement DayTile(DayStatusDto day, bool current)
        {
            var tile = new VisualElement();
            tile.AddToClassList("sc-day-tile");
            tile.AddToClassList("sc-dr-tile");
            tile.AddToClassList(TileClass(day.status));
            if (current)
            {
                // Safe to combine with the status tint: the sc-dr-tile--* modifiers only paint a
                // background, so the accent border of the shared --current rule is never contested.
                tile.AddToClassList("sc-day-tile--current");
            }

            var head = new VisualElement();
            head.AddToClassList("sc-dr-tile__head");

            var mark = new Label(MarkGlyph(day.status));
            mark.AddToClassList("sc-dr-tile__mark");
            mark.AddToClassList("sc-icon");
            mark.style.color = ShowcaseTheme.Tone(StatusTone(day.status));
            head.Add(mark);

            var number = new Label("Day " + day.dayNumber);
            number.AddToClassList("sc-dr-tile__day");
            head.Add(number);

            if (day.isSpecialDay)
            {
                var star = new Label(LucideIcon.Star);
                star.AddToClassList("sc-dr-tile__star");
                star.AddToClassList("sc-icon");
                star.tooltip = "Special day";
                head.Add(star);
            }
            tile.Add(head);

            tile.Add(new Badge(StatusText(day.status), StatusTone(day.status)));

            bool hasRewards = (day.rewards != null && day.rewards.Length > 0)
                || (day.bonusRewards != null && day.bonusRewards.Length > 0);
            if (!hasRewards)
            {
                var none = new Label("no reward");
                none.AddToClassList("sc-dr-tile__none");
                tile.Add(none);
                return tile;
            }

            var rewards = new VisualElement();
            rewards.AddToClassList("sc-dr-tile__rewards");
            if (day.rewards != null)
            {
                foreach (var reward in day.rewards)
                {
                    rewards.Add(Reward(reward, Meta.Accent));
                }
            }
            if (day.bonusRewards != null)
            {
                foreach (var reward in day.bonusRewards)
                {
                    rewards.Add(Reward(reward, ShowcaseTheme.Warn));
                }
            }
            tile.Add(rewards);
            return tile;
        }

        private static string TileClass(DailyRewardClaimStatus status)
        {
            switch (status)
            {
                case DailyRewardClaimStatus.Claimed: return "sc-dr-tile--claimed";
                case DailyRewardClaimStatus.Missed: return "sc-dr-tile--missed";
                case DailyRewardClaimStatus.Locked: return "sc-dr-tile--locked";
                default: return "sc-dr-tile--open";
            }
        }

        private static string MarkGlyph(DailyRewardClaimStatus status)
        {
            switch (status)
            {
                case DailyRewardClaimStatus.Claimed: return LucideIcon.Check;
                case DailyRewardClaimStatus.Missed: return LucideIcon.X;
                case DailyRewardClaimStatus.Locked: return LucideIcon.Lock;
                case DailyRewardClaimStatus.CatchUpAvailable: return LucideIcon.History;
                default: return LucideIcon.Gift;
            }
        }

        private static string StatusText(DailyRewardClaimStatus status)
        {
            switch (status)
            {
                case DailyRewardClaimStatus.Claimed: return "claimed";
                case DailyRewardClaimStatus.Missed: return "missed";
                case DailyRewardClaimStatus.Locked: return "locked";
                case DailyRewardClaimStatus.CatchUpAvailable: return "catch up";
                default: return "available";
            }
        }

        private static ChipTone StatusTone(DailyRewardClaimStatus status)
        {
            switch (status)
            {
                case DailyRewardClaimStatus.Claimed: return ChipTone.Ok;
                case DailyRewardClaimStatus.Missed: return ChipTone.Bad;
                case DailyRewardClaimStatus.Locked: return ChipTone.Neutral;
                case DailyRewardClaimStatus.CatchUpAvailable: return ChipTone.Warn;
                default: return ChipTone.Accent;
            }
        }

        // ----- streak bonuses and milestones --------------------------------------------------------

        private VisualElement StreakSection(DailyRewardStatusDto status)
        {
            var box = new VisualElement();
            var bonuses = status.streakBonuses;
            box.Add(new SectionHeader("Streak bonuses",
                bonuses == null ? "0" : bonuses.Length.ToString()));

            if (bonuses == null || bonuses.Length == 0)
            {
                box.Add(ZeroState.Panel(LucideIcon.Flame, "No streak bonuses",
                    "A streak bonus rides on top of the day's reward once the player has claimed enough "
                    + "days in a row — either multiplying it or adding a fixed extra. They are configured "
                    + "per calendar in the Mirra Hub console."));
                return box;
            }

            foreach (var bonus in bonuses)
            {
                if (bonus == null)
                {
                    continue;
                }
                bool reached = status.currentDayNumber >= bonus.streakDays;

                var row = new ListRow();
                row.SetTitle(bonus.streakDays + "-day streak");
                row.SetSubtitle(Describe(bonus));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-dr-rewards");
                trailing.Add(new Badge(reached ? "reached" : "locked",
                    reached ? ChipTone.Ok : ChipTone.Neutral));
                if (bonus.rewards != null)
                {
                    foreach (var reward in bonus.rewards)
                    {
                        trailing.Add(Reward(reward, ShowcaseTheme.Warn));
                    }
                }
                row.SetTrailing(trailing);
                box.Add(row);
            }
            return box;
        }

        private static string Describe(StreakBonusDto bonus)
        {
            if (bonus.bonusType == StreakBonusType.Multiplier)
            {
                return "multiplies the day's reward ×"
                    + bonus.multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            }
            int count = bonus.rewards == null ? 0 : bonus.rewards.Length;
            return count == 0 ? "grants a fixed extra reward" : "grants " + count + " extra reward"
                + (count == 1 ? string.Empty : "s");
        }

        private VisualElement MilestoneSection(DailyRewardStatusDto status)
        {
            var box = new VisualElement();
            var milestones = status.milestoneProgress;
            box.Add(new SectionHeader("Milestones",
                milestones == null ? "0" : milestones.Length.ToString()));

            if (milestones == null || milestones.Length == 0)
            {
                box.Add(ZeroState.Panel(LucideIcon.Trophy, "No milestones",
                    "A milestone pays out once the player has claimed a total number of days across every "
                    + "cycle — the long-tail reward of a login calendar. Add one in the Mirra Hub console "
                    + "and its progress shows up here."));
                return box;
            }

            foreach (var milestone in milestones)
            {
                if (milestone != null)
                {
                    box.Add(MilestoneRow(status, milestone));
                }
            }
            return box;
        }

        private VisualElement MilestoneRow(DailyRewardStatusDto status, MilestoneProgressDto milestone)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-dr-milestone");

            var head = new VisualElement();
            head.AddToClassList("sc-dr-milestone__head");

            var title = new Label(milestone.totalDaysRequired + " total days claimed");
            title.AddToClassList("sc-dr-milestone__title");
            head.Add(title);

            var trailing = new VisualElement();
            trailing.AddToClassList("sc-dr-rewards");
            trailing.Add(new Badge(milestone.isReached ? "reached" : "in progress",
                milestone.isReached ? ChipTone.Ok : ChipTone.Neutral));
            if (milestone.rewards != null)
            {
                foreach (var reward in milestone.rewards)
                {
                    trailing.Add(Reward(reward, ShowcaseTheme.Violet));
                }
            }
            head.Add(trailing);
            box.Add(head);

            int remaining = milestone.totalDaysRequired - status.totalClaimDays;
            box.Add(new ProgressBar()
                .Set(status.totalClaimDays, milestone.totalDaysRequired)
                .SetLabel(milestone.isReached || remaining <= 0
                    ? status.totalClaimDays + " / " + milestone.totalDaysRequired
                    : status.totalClaimDays + " / " + milestone.totalDaysRequired + " · " + remaining
                        + (remaining == 1 ? " day to go" : " days to go"))
                .SetAccent(milestone.isReached ? ShowcaseTheme.Ok : ShowcaseTheme.Violet));
            return box;
        }

        // ----- claiming -----------------------------------------------------------------------------

        private VisualElement ClaimSection(CalendarPane pane)
        {
            var box = new VisualElement();
            box.Add(new SectionHeader("Claim"));

            var hint = new Label("The claim response carries exactly what was granted, which is why this "
                + "card shows it instead of only reloading. The status above is re-read on success."
                + (pane.Calendar.requireManualClaim
                    ? string.Empty
                    : " This calendar claims automatically, so the call can answer that the day is already taken."));
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            box.Add(hint);

            box.Add(new ActionCard("Claim a reward",
                    "Claims the day the calendar says is due, or the day you name when it still allows it.",
                    LucideIcon.Gift)
                .WithFields(FormField.Int("day", "Day number", 0)
                    .WithPlaceholder("0 sends null, letting the server pick the day that is due. A named "
                        + "day only works while it is Available or CatchUpAvailable."))
                .WithSnippet(ClaimSnippet)
                .OnRun("Claim", values => ClaimAction(pane, values.Int("day"))));
            return box;
        }

        private async Task<ActionOutcome> ClaimAction(CalendarPane pane, int dayNumber)
        {
            int? day = dayNumber > 0 ? dayNumber : (int?)null;
            var op = StartClaim(pane.Calendar.id, day);
            var outcome = await AwaitData(op, "Daily rewards · claim");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var response = op.Result.Data;
            AfterClaim(pane, response);
            if (Toasts != null)
            {
                Toasts.Ok(ClaimedDayText(response, day));
            }
            return ActionOutcome.Success(ClaimedDayText(response, day), ClaimDetail(response));
        }

        private async void QuickClaim(CalendarPane pane, int dayNumber, Button button)
        {
            button.SetEnabled(false);
            button.text = "Claiming…";

            // Deliberately null rather than dayNumber: this button means "whatever is due", and the
            // server is the one that decides which day that is.
            var op = StartClaim(pane.Calendar.id, null);
            var outcome = await AwaitData(op, "Daily rewards · claim");
            if (!outcome.Ok)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Claim failed · " + outcome.Message);
                }
                button.SetEnabled(true);
                button.text = "Claim day " + dayNumber;
                return;
            }

            var response = op.Result.Data;
            if (Toasts != null)
            {
                Toasts.Ok(ClaimedDayText(response, dayNumber));
            }
            // Reloading the status section replaces this button, so it is not restored here.
            AfterClaim(pane, response);
        }

        /// <summary>
        /// Starts the claim, turning a validation throw into a null operation. <see cref="QuickClaim"/>
        /// is <c>async void</c>, so an exception escaping the call would surface as an unhandled one
        /// instead of as a failed claim.
        /// </summary>
        private AsyncOperation<RestApiResult<ClaimDailyRewardResponseDto>> StartClaim(string calendarId, int? day)
        {
            try
            {
                return Sdk.DailyRewards.ClaimAsync(calendarId, day);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Daily rewards: the claim call could not be started: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Applies one successful claim: the pane keeps the payload (so the reloaded status can show
        /// what was granted), its status section is re-read, and the header chip is recounted.
        /// </summary>
        private void AfterClaim(CalendarPane pane, ClaimDailyRewardResponseDto response)
        {
            pane.LastClaim = response;
            LoadStatus(pane);
            LoadReadyCount();
        }

        private static string ClaimedDayText(ClaimDailyRewardResponseDto response, int? requestedDay)
        {
            if (response != null && response.dayNumberClaimed > 0)
            {
                return "Claimed day " + response.dayNumberClaimed;
            }
            return requestedDay.HasValue ? "Claimed day " + requestedDay.Value : "Reward claimed";
        }

        private VisualElement LastClaimCard(CalendarPane pane)
        {
            var response = pane.LastClaim;

            var card = new Card(ShowcaseTheme.Ok);
            card.AddToClassList("sc-dr-claimed");
            card.WithTitle("Last claim in this session · day " + response.dayNumberClaimed, ShowcaseTheme.Ok);
            card.Body.Add(ClaimDetail(response));

            var meta = new VisualElement();
            meta.AddToClassList("sc-chip-row");
            meta.Add(new Chip(response.newTotalClaimDays + " days claimed in total", ChipTone.Neutral));
            if (response.cycleCompleted)
            {
                meta.Add(new Chip("cycle completed", ChipTone.Ok));
            }
            card.Body.Add(meta);

            if (!string.IsNullOrEmpty(response.nextCalendarId))
            {
                card.Body.Add(NextCalendarRow(response.nextCalendarId));
            }
            return card;
        }

        /// <summary>
        /// A finished cycle can hand the player over to another calendar. When that calendar is one of
        /// this screen's tabs, offer to open it; otherwise hand over the id, which is all the response
        /// gives.
        /// </summary>
        private VisualElement NextCalendarRow(string nextCalendarId)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-dr-next");

            int index = TabIndexOf(nextCalendarId);
            if (index < 0)
            {
                row.Add(new Chip("continues on calendar " + Fmt.Id(nextCalendarId, 10), ChipTone.Info));
                row.Add(new CopyButton(nextCalendarId, Toasts, "id"));
                return row;
            }

            row.Add(new Chip("continues on another calendar", ChipTone.Info));
            var open = new Button(() => _tabs.Select(index)) { text = "Open it" };
            open.AddToClassList("sc-btn");
            row.Add(open);
            return row;
        }

        private int TabIndexOf(string calendarId)
        {
            if (string.IsNullOrEmpty(calendarId) || _tabs == null)
            {
                return -1;
            }
            foreach (var pane in _panes)
            {
                if (pane.TabIndex >= 0 && pane.Calendar != null && pane.Calendar.id == calendarId)
                {
                    return pane.TabIndex;
                }
            }
            return -1;
        }

        // ----- rewards ------------------------------------------------------------------------------

        /// <summary>The three reward buckets of a claim, each tinted so a bonus is not mistaken for the
        /// day's own reward. Rendered rather than summarised: it is the point of the call.</summary>
        private VisualElement ClaimDetail(ClaimDailyRewardResponseDto response)
        {
            var box = new VisualElement();

            int shown = 0;
            if (response != null)
            {
                shown += AppendRewards(box, "Day reward", response.baseRewards, Meta.Accent);
                shown += AppendRewards(box, "Streak bonus", response.streakBonusRewards, ShowcaseTheme.Warn);
                shown += AppendRewards(box, "Milestone", response.milestoneRewards, ShowcaseTheme.Violet);
            }

            if (shown == 0)
            {
                var row = new VisualElement();
                row.AddToClassList("sc-dr-rewards");
                row.Add(new Chip("nothing granted", ChipTone.Neutral));
                box.Add(row);
            }
            return box;
        }

        private static int AppendRewards(VisualElement host, string label, RewardDataDto[] rewards, Color tint)
        {
            if (rewards == null || rewards.Length == 0)
            {
                return 0;
            }

            var line = new VisualElement();
            line.AddToClassList("sc-dr-grant");

            var caption = new Label(label);
            caption.AddToClassList("sc-dr-grant__label");
            line.Add(caption);

            var chips = new VisualElement();
            chips.AddToClassList("sc-dr-rewards");
            int count = 0;
            foreach (var reward in rewards)
            {
                if (reward != null)
                {
                    chips.Add(Reward(reward, tint));
                    count++;
                }
            }
            line.Add(chips);
            host.Add(line);
            return count;
        }

        private static VisualElement Reward(RewardDataDto reward, Color tint)
        {
            if (reward == null)
            {
                return new Chip("no reward", ChipTone.Neutral);
            }

            var chip = new RewardChip(RewardGlyph(reward.economyResourceKind),
                Fmt.Truncate(Fmt.OrDash(reward.rewardId), 16) + " ×" + reward.count, tint);
            chip.tooltip = KindName(reward.economyResourceKind) + " · "
                + Fmt.OrDash(reward.rewardId) + " ×" + reward.count;
            return chip;
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
                default: return "reward kind " + kind;
            }
        }

        // ----- shared plumbing ----------------------------------------------------------------------

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

        /// <summary>
        /// The countdown chip compares against <c>DateTime.UtcNow</c>, and these DTOs carry UTC with an
        /// unspecified kind — so an unspecified stamp is labelled, not converted. Only a genuinely
        /// local one is shifted.
        /// </summary>
        private static DateTime Utc(DateTime time)
        {
            return time.Kind == DateTimeKind.Local
                ? time.ToUniversalTime()
                : DateTime.SpecifyKind(time, DateTimeKind.Utc);
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

        // Every write this screen has is a claim, so the journal row can carry the claim snippet
        // unconditionally; the reads go through ViewBind and pass their own.
        private Outcome Fold(RestApiResult result, string label)
        {
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result, ClaimSnippet);
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
        /// One calendar's tab state. It exists so a claim can re-read just this pane's status and still
        /// show what the claim granted: the payload lives here, outside the section that is replaced.
        /// </summary>
        private sealed class CalendarPane
        {
            public readonly DailyRewardCalendarDto Calendar;

            /// <summary>Index in the tab strip, or -1 when the screen shows a single calendar.</summary>
            public readonly int TabIndex;

            /// <summary>Section the status call renders into; re-bound after every claim.</summary>
            public VisualElement StatusSlot;

            /// <summary>Payload of the last claim made on this pane, or null when there was none.</summary>
            public ClaimDailyRewardResponseDto LastClaim;

            public CalendarPane(DailyRewardCalendarDto calendar, int tabIndex)
            {
                Calendar = calendar;
                TabIndex = tabIndex;
            }
        }
    }
}

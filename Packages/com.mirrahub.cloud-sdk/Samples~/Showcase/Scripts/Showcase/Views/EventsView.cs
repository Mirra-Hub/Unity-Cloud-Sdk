using System;
using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Core.Events;
using MirraCloud.Core.Events.Enums;
using MirraCloud.Json;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Events screen: the LiveOps events running on this branch, and which of them reach this player.
    ///
    /// <para>
    /// The thing worth understanding here is what the game does <b>not</b> do. An event's effect is
    /// applied by the server on every economy read, so a game that never touches this service still
    /// gets the changed numbers. What it cannot do without it is talk about the event — put up a
    /// banner, count down, or open a screen only to the audience the event targets. The Effect tab
    /// exists to make that visible: it reads the same energy twice, through the catalog and through
    /// the runtime, and shows the two answers side by side.
    /// </para>
    ///
    /// <para>
    /// Read-only, like the service. Events are authored in the Mirra Hub console.
    /// </para>
    /// </summary>
    public sealed class EventsView : ServiceView
    {
        private const string ActiveSnippet =
@"// What is running for this player right now. Not part of a splash-screen warm-up: the answer
// depends on the player and is only true for minutes.
var op = sdk.Events.GetActiveEventsAsync();
await op.Task();

foreach (ActiveEvent e in sdk.Events.MyEvents)
{
    // e.Key, e.Name, e.Priority, e.ScheduleType
    // e.StartsAtUtc / e.EndsAtUtc : the run in progress, not the campaign
    // e.NextOccurrenceAtUtc       : when it opens again (Recurring)
    // e.ScheduleEndsAtUtc         : when it stops for good
}";

        private const string GateSnippet =
@"// ""Running"" and ""running for me"" are different questions. IsEventActive asks the second one,
// which is what a content gate means.
if (sdk.Events.IsEventActive(""halloween_2026""))
{
    ShowHalloweenTab();
}

// Time left, measured against the server's clock rather than the device's:
TimeSpan? left = sdk.Events.GetTimeLeft(""halloween_2026"");";

        private const string UnmatchedSnippet =
@"// Also list events running for other audiences, flagged IsMatched = false. This screen asks for
// them so it can show what a player is NOT in; a game's own UI should not, or it promises offers
// the player cannot have.
var op = sdk.Events.GetActiveEventsAsync(includeUnmatched: true);
await op.Task();";

        private const string EffectSnippet =
@"// Nothing here mentions events, and that is the point. The catalog hands back what the branch
// defines; the runtime hands back what this player gets, with any active event already applied.
var catalog = sdk.Economy.LoadConfigsAsync();
var runtime = sdk.Economy.GetEnergiesAsync();
await catalog.Task();
await runtime.Task();

// catalog.Result.Data.Energies[key].Fields[""maxValue""]  : the same for everyone
// runtime.Result.Data.First(e => e.EnergyId == key).MaxValue : personalised by the server";

        private const string FilterAll = "All";
        private const string FilterMine = "For me";
        private const string FilterOthers = "Not for me";

        /// <summary>The field an energy's ceiling lives under in the catalog payload.</summary>
        private const string MaxValueField = "maxValue";

        private Tabs _tabs;
        private string _search = string.Empty;
        private string _audience = FilterAll;

        private IReadOnlyList<ActiveEvent> _events;
        private bool _loaded;
        private VisualElement _kpiSlot;

        public EventsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _search = string.Empty;
            _audience = FilterAll;
            _events = null;
            _loaded = false;
            _kpiSlot = null;

            SetSubtitle(
                "Nothing here is wired up by your game. While an event runs the server hands this "
                + "player different economy values — this screen only shows which ones, and for how long.");
            SyncStatus();

            DeclareCall(new SdkCall("Read what is running", ActiveSnippet));
            DeclareCall(new SdkCall("Gate content on an event", GateSnippet,
                "IsEventActive requires the player to be in the audience — that is the default on purpose."));
            DeclareCall(new SdkCall("Include other audiences", UnmatchedSnippet,
                "What this screen asks for, so the \"Not for me\" filter has anything to show."));
            DeclareCall(new SdkCall("See an event's effect", EffectSnippet,
                "Two Economy calls. The difference between them is the event."));

            UseToolbar()
                .WithSearch("Filter events by key or name", OnSearch)
                .WithFilter("Audience", new[] { FilterAll, FilterMine, FilterOthers }, OnAudience, FilterAll)
                .WithSpacer()
                .WithRefresh(Refresh);

            _kpiSlot = AddSlot();

            _tabs = UseTabs();
            _tabs.Add("Now", LucideIcon.Flame, BuildNow)
                .Add("Effect", LucideIcon.Coins, BuildEffect)
                .Add("Schedule", LucideIcon.CalendarClock, BuildSchedule);
        }

        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            _tabs.Invalidate(0);
            _tabs.Invalidate(2);
        }

        private void OnAudience(string value)
        {
            _audience = value ?? FilterAll;
            _tabs.Invalidate(0);
            _tabs.Invalidate(2);
            RenderKpis();
        }

        // ----- header -----------------------------------------------------------------------------

        /// <summary>
        /// Two numbers, because they answer different questions: how much LiveOps is live on this
        /// branch, and how much of it this particular player can see.
        /// </summary>
        private void SyncStatus()
        {
            if (!_loaded)
            {
                SetStatus("Loading", ChipTone.Neutral);
                return;
            }

            int running = _events != null ? _events.Count : 0;
            if (running == 0)
            {
                SetStatus("Nothing running", ChipTone.Neutral);
                return;
            }

            int mine = MatchedCount();
            string text = running + (running == 1 ? " running" : " running")
                + " · " + (mine == 0 ? "none for you" : mine + " for you");

            SetStatus(text, mine > 0 ? ChipTone.Ok : ChipTone.Warn);
        }

        private int MatchedCount()
        {
            if (_events == null) return 0;

            int mine = 0;
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].IsMatched) mine++;
            }
            return mine;
        }

        private void RenderKpis()
        {
            if (_kpiSlot == null) return;

            if (!_loaded || _events == null || _events.Count == 0)
            {
                Replace(_kpiSlot, new VisualElement());
                return;
            }

            int mine = MatchedCount();
            int recurring = 0;
            int targeted = 0;
            ActiveEvent soonest = null;

            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.ScheduleType == EventScheduleType.Recurring) recurring++;
                if (e.IsTargeted) targeted++;
                if (soonest == null || e.EndsAtUtc < soonest.EndsAtUtc) soonest = e;
            }

            var kpis = new KpiRow();
            kpis.Add("Running", LucideIcon.Flame, _events.Count.ToString(CultureInfo.InvariantCulture));
            kpis.Add("For this player", LucideIcon.Users, mine.ToString(CultureInfo.InvariantCulture), null, mine > 0);
            kpis.Add("Targeted", LucideIcon.Filter, targeted.ToString(CultureInfo.InvariantCulture),
                targeted == 0 ? "everything is for everyone" : null);
            kpis.Add("Repeating", LucideIcon.RotateCw, recurring.ToString(CultureInfo.InvariantCulture));
            kpis.Add("Next to end", LucideIcon.Timer,
                soonest == null ? Fmt.Dash : Fmt.Duration(soonest.TimeLeft(ServerNow())));

            Replace(_kpiSlot, kpis);
        }

        // ----- tab 1: Now -------------------------------------------------------------------------

        private VisualElement BuildNow()
        {
            var root = new VisualElement();
            var slot = new VisualElement();
            root.Add(slot);

            ViewBind.Load(
                // includeUnmatched, so the "Not for me" filter has something to show. A game would
                // ask for its own player's events only.
                () => Sdk.Events.GetActiveEventsAsync(includeUnmatched: true),
                slot,
                // The service caches the response it just parsed, so the render reads it from there
                // rather than re-walking the payload — and that is also where the header numbers and
                // the countdowns get their clock offset from.
                _ => { OnEventsLoaded(); return BuildNowCards(); },
                d => d == null || d.events == null || d.events.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Active events",
                    Snippet = ActiveSnippet,
                    ServiceName = "Events",
                    AllowRetry = true,
                    EmptyView = NothingRunning,
                });

            return root;
        }

        private void OnEventsLoaded()
        {
            _events = Sdk.Events.ActiveEvents;
            _loaded = true;
            SyncStatus();
            RenderKpis();
        }

        /// <summary>
        /// An empty list is the normal state of most projects, not a failure — the service answered,
        /// there is simply nothing scheduled. Saying "not configured" here would send an integrator
        /// looking for a setup step that does not exist.
        /// </summary>
        private VisualElement NothingRunning()
        {
            OnEventsLoaded();

            return ZeroState.Panel(LucideIcon.CalendarClock, "Nothing is running",
                "Events are created in the Mirra Hub console: a schedule, an audience, and the economy "
                + "values to change while it runs. Once one is live it applies on the server by itself — "
                + "this screen is how a game finds out enough to show it.");
        }

        private VisualElement BuildNowCards()
        {
            var col = new VisualElement();
            var shown = Filtered();

            if (shown.Count == 0)
            {
                col.Add(NoneMatchFilter());
                return col;
            }

            for (int i = 0; i < shown.Count; i++)
            {
                col.Add(EventCard(shown[i]));
            }

            return col;
        }

        private VisualElement NoneMatchFilter()
        {
            string message = _audience == FilterMine
                ? "Events are running, but none of them targets this player. Sign in as somebody the "
                  + "audience rule matches, or switch the filter to see the rest."
                : _audience == FilterOthers
                    ? "Everything running right now applies to this player."
                    : "No running event matches that search.";

            return ZeroState.Panel(LucideIcon.Filter, "Nothing to show", message);
        }

        private VisualElement EventCard(ActiveEvent e)
        {
            var card = new Card(Meta.Accent);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var title = new Label(e.Name);
            title.AddToClassList("sc-evt-title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);
            header.Add(AudienceChip(e));
            header.Add(new CountdownChip(ToDeviceClock(e.EndsAtUtc)));
            card.WithHeader(header);

            var keyRow = new VisualElement();
            keyRow.style.flexDirection = FlexDirection.Row;
            keyRow.style.alignItems = Align.Center;

            var key = new Label(e.Key);
            key.AddToClassList("sc-evt-key");
            keyRow.Add(key);
            keyRow.Add(new CopyButton(e.Key, Toasts));
            card.Body.Add(keyRow);

            if (!string.IsNullOrEmpty(e.Description))
            {
                var description = new Label(e.Description);
                description.AddToClassList("sc-evt-note");
                card.Body.Add(description);
            }

            var bar = new ProgressBar();
            bar.Set(e.Progress(ServerNow()), 1f);
            bar.SetLabel(Fmt.Duration(e.TimeLeft(ServerNow())) + " left of this run");
            bar.SetAccent(Meta.Accent);
            card.Body.Add(bar);

            var facts = new VisualElement();
            facts.style.flexDirection = FlexDirection.Row;
            facts.style.flexWrap = Wrap.Wrap;
            facts.Add(new Badge(e.ScheduleType == EventScheduleType.Recurring ? "Repeats" : "One-off"));
            facts.Add(new Badge("Priority " + e.Priority.ToString(CultureInfo.InvariantCulture)));

            if (e.NextOccurrenceAtUtc != null)
            {
                facts.Add(new Badge("Again " + Fmt.DateTime2(e.NextOccurrenceAtUtc)));
            }

            if (e.ScheduleEndsAtUtc != null)
            {
                facts.Add(new Badge("Ends " + Fmt.Date(e.ScheduleEndsAtUtc)));
            }

            card.Body.Add(facts);
            return card;
        }

        private static Chip AudienceChip(ActiveEvent e)
        {
            if (!e.IsTargeted) return new Chip("Everyone", ChipTone.Info);

            return e.IsMatched ? new Chip("For you", ChipTone.Ok) : new Chip("Not for you", ChipTone.Warn);
        }

        // ----- tab 2: Effect ----------------------------------------------------------------------

        /// <summary>
        /// The proof that the rest of the screen is talking about something real.
        ///
        /// <para>
        /// Two Economy reads of the same energies. <c>LoadConfigsAsync</c> returns the branch's
        /// catalog — the definition, identical for every player. <c>GetEnergiesAsync</c> returns the
        /// runtime, which is where the server composes any event overrides that apply to <i>this</i>
        /// player. Where the two disagree, the difference is an event, and the game asked for neither.
        /// </para>
        /// </summary>
        private VisualElement BuildEffect()
        {
            var root = new VisualElement();

            root.Add(new SectionHeader("What the game reads twice"));

            var note = new Label(
                "Neither call below mentions events. The catalog is what the branch defines; the "
                + "runtime is what this player gets. The server puts the difference there.");
            note.AddToClassList("sc-evt-note");
            root.Add(note);

            var slot = new VisualElement();
            root.Add(slot);

            ViewBind.Load(
                () => Sdk.Economy.LoadConfigsAsync(),
                slot,
                catalog => BuildEffectRuntime(catalog),
                c => c == null || c.Energies == null || c.Energies.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Economy catalog",
                    Snippet = EffectSnippet,
                    ServiceName = "Economy",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = () => ZeroState.NotConfigured("Economy",
                        "This tab compares an energy's ceiling as defined with the ceiling this player "
                        + "actually gets. With no energies in the branch there is nothing to compare."),
                });

            return root;
        }

        private VisualElement BuildEffectRuntime(EconomyConfigsDto catalog)
        {
            var root = new VisualElement();
            var slot = new VisualElement();
            root.Add(slot);

            ViewBind.Load(
                () => Sdk.Economy.GetEnergiesAsync(),
                slot,
                runtime => BuildComparison(catalog, runtime),
                r => r == null || r.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Energy runtime",
                    Snippet = EffectSnippet,
                    ServiceName = "Economy",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Coins, "No energies for this player",
                        "The branch defines energies, but this player has no runtime for them yet — "
                        + "sign in and read an energy once."),
                });

            return root;
        }

        private VisualElement BuildComparison(EconomyConfigsDto catalog, List<EnergyBalanceDto> runtime)
        {
            var col = new VisualElement();
            int differing = 0;

            for (int i = 0; i < runtime.Count; i++)
            {
                var balance = runtime[i];
                if (balance == null || string.IsNullOrEmpty(balance.EnergyId)) continue;

                int? defined = DefinedMaxValue(catalog, balance.EnergyId);
                if (defined == null) continue;

                bool differs = defined.Value != balance.MaxValue;
                if (differs) differing++;

                col.Add(ComparisonRow(balance.EnergyId, defined.Value, balance.MaxValue, differs));
            }

            if (col.childCount == 0)
            {
                return ZeroState.Panel(LucideIcon.Coins, "Nothing to compare",
                    "None of this player's energies declares a ceiling in the catalog, so there is no "
                    + "pair of numbers to put side by side.");
            }

            col.Insert(0, ComparisonVerdict(differing));
            return col;
        }

        /// <summary>
        /// Every degradation here has to read as an explanation, not as a broken screen: an integrator
        /// looking at two equal numbers needs to be told that equal is the expected answer when no
        /// event touches that value.
        /// </summary>
        private VisualElement ComparisonVerdict(int differing)
        {
            if (differing > 0)
            {
                int mine = MatchedCount();
                string why = mine > 0
                    ? "An event running for this player changes it. When the event ends the number goes "
                      + "back on its own — the game does not re-read anything special."
                    : "Something is overriding it server-side.";

                return Banner("The server is handing this player a different number", why, ChipTone.Ok);
            }

            if (_loaded && _events != null && _events.Count > 0 && MatchedCount() == 0)
            {
                return Banner("Running, but not for you",
                    "Events are live on this branch, but none targets this player, so the runtime matches "
                    + "the catalog exactly.", ChipTone.Warn);
            }

            return Banner("Same on both sides",
                "No event is changing these values right now, so the catalog and the runtime agree. "
                + "That is what this screen looks like when nothing is running.", ChipTone.Neutral);
        }

        private static VisualElement Banner(string title, string message, ChipTone tone)
        {
            var card = new Card();
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;

            var label = new Label(title);
            label.AddToClassList("sc-evt-title");
            head.Add(label);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            head.Add(spacer);
            head.Add(new Chip(tone == ChipTone.Ok ? "Overridden" : tone == ChipTone.Warn ? "Not for you" : "Base", tone));
            card.WithHeader(head);

            var body = new Label(message);
            body.AddToClassList("sc-evt-note");
            card.Body.Add(body);
            return card;
        }

        private VisualElement ComparisonRow(string energyId, int defined, int effective, bool differs)
        {
            var card = new Card(differs ? Meta.Accent : (UnityEngine.Color?)null);
            card.WithTitle(energyId);

            var row = new VisualElement();
            row.AddToClassList("sc-evt-compare");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            row.Add(SideBySide("Base definition", defined, "LoadConfigsAsync()", "same for everyone"));

            var arrow = new Label("→");
            arrow.AddToClassList("sc-evt-arrow");
            row.Add(arrow);

            row.Add(SideBySide("What this player gets", effective, "GetEnergiesAsync()",
                differs ? "personalised by the server" : "no event touches it"));

            card.Body.Add(row);

            if (differs)
            {
                int delta = effective - defined;
                var chip = new Chip((delta > 0 ? "+" : string.Empty) + delta.ToString(CultureInfo.InvariantCulture),
                    ChipTone.Ok);
                card.Body.Add(chip);
            }

            return card;
        }

        private static VisualElement SideBySide(string caption, int value, string source, string note)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-evt-side");

            var head = new Label(caption);
            head.AddToClassList("sc-evt-side-caption");
            box.Add(head);

            var number = new Label(value.ToString(CultureInfo.InvariantCulture));
            number.AddToClassList("sc-evt-side-value");
            box.Add(number);

            var from = new Label(source);
            from.AddToClassList("sc-evt-side-source");
            box.Add(from);

            var hint = new Label(note);
            hint.AddToClassList("sc-evt-note");
            box.Add(hint);

            return box;
        }

        /// <summary>
        /// The catalog's ceiling for an energy, or null when it does not declare one. Fields are raw
        /// JSON — the catalog is authored per project, so nothing guarantees the shape.
        /// </summary>
        private static int? DefinedMaxValue(EconomyConfigsDto catalog, string energyId)
        {
            EconomySdkResourceDto resource;
            if (catalog.Energies == null || !catalog.Energies.TryGetValue(energyId, out resource)) return null;
            if (resource == null || resource.Fields == null) return null;

            JsonValue fields = resource.Fields;
            if (fields.Type != JsonValueType.Object || !fields.ContainsKey(MaxValueField)) return null;

            JsonValue value = fields[MaxValueField];

            // A project can type this field as anything; the cast throws on everything but an int.
            return value != null && value.Type == JsonValueType.Int ? (int)value : (int?)null;
        }

        // ----- tab 3: Schedule --------------------------------------------------------------------

        private VisualElement BuildSchedule()
        {
            var root = new VisualElement();

            var note = new Label(
                "Windows as the server reports them. This screen does not decide whether an event is "
                + "running — it is told.");
            note.AddToClassList("sc-evt-note");
            root.Add(note);

            var slot = new VisualElement();
            root.Add(slot);

            ViewBind.Load(
                () => Sdk.Events.GetActiveEventsAsync(includeUnmatched: true),
                slot,
                _ => { OnEventsLoaded(); return BuildScheduleTable(); },
                d => d == null || d.events == null || d.events.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Active events",
                    Snippet = ActiveSnippet,
                    ServiceName = "Events",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(ScheduleColumns(),
                        "Nothing is running on this branch, so there is no schedule to show.", 3),
                });

            return root;
        }

        private VisualElement BuildScheduleTable()
        {
            var shown = Filtered();
            if (shown.Count == 0) return NoneMatchFilter();

            var table = new DataTable(ScheduleColumns());
            table.Bind(shown, row => ((ActiveEvent)row).IsMatched);
            return table;
        }

        private DataColumn[] ScheduleColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "Event",
                    Grow = 2.2f,
                    SortKey = row => ((ActiveEvent)row).Name,
                    Cell = row =>
                    {
                        var e = (ActiveEvent)row;
                        var col = new VisualElement();
                        col.Add(new Label(e.Name));
                        var key = new Label(e.Key);
                        key.AddToClassList("sc-evt-key");
                        col.Add(key);
                        return col;
                    },
                },
                new DataColumn
                {
                    Header = "Type",
                    Grow = 0.8f,
                    SortKey = row => ((ActiveEvent)row).ScheduleType.ToString(),
                    Cell = row => new Label(((ActiveEvent)row).ScheduleType == EventScheduleType.Recurring
                        ? "Repeats"
                        : "One-off"),
                },
                new DataColumn
                {
                    Header = "This run",
                    Grow = 1.6f,
                    SortKey = row => ((ActiveEvent)row).EndsAtUtc,
                    Cell = row =>
                    {
                        var e = (ActiveEvent)row;
                        return new Label(Fmt.Time(e.StartsAtUtc) + " → " + Fmt.DateTime2(e.EndsAtUtc));
                    },
                },
                new DataColumn
                {
                    Header = "Again",
                    Grow = 1.2f,
                    SortKey = row => ((ActiveEvent)row).NextOccurrenceAtUtc ?? DateTime.MaxValue,
                    Cell = row =>
                    {
                        var e = (ActiveEvent)row;
                        return new Label(e.NextOccurrenceAtUtc == null
                            ? Fmt.Dash
                            : Fmt.DateTime2(e.NextOccurrenceAtUtc));
                    },
                },
                new DataColumn
                {
                    Header = "Until",
                    Grow = 1f,
                    SortKey = row => ((ActiveEvent)row).ScheduleEndsAtUtc ?? DateTime.MaxValue,
                    Cell = row => new Label(Fmt.Date(((ActiveEvent)row).ScheduleEndsAtUtc)),
                },
                new DataColumn
                {
                    Header = "Priority",
                    FixedWidth = true,
                    Px = 70f,
                    Align = "right",
                    SortKey = row => ((ActiveEvent)row).Priority,
                    Cell = row => new Label(((ActiveEvent)row).Priority.ToString(CultureInfo.InvariantCulture)),
                },
                new DataColumn
                {
                    Header = "Audience",
                    FixedWidth = true,
                    Px = 110f,
                    Cell = row => AudienceChip((ActiveEvent)row),
                },
            };
        }

        // ----- shared -----------------------------------------------------------------------------

        /// <summary>
        /// Now, by the server's clock. The service measured the offset when it fetched; a device whose
        /// clock is minutes out would otherwise show every countdown minutes out with it.
        /// </summary>
        private DateTime ServerNow()
        {
            return Sdk.Events != null ? Sdk.Events.ServerUtcNow : DateTime.UtcNow;
        }

        /// <summary>
        /// CountdownChip ticks against the device clock, so a server-time target is shifted by the
        /// same offset before being handed over — the remaining span then comes out right either way.
        /// </summary>
        private DateTime ToDeviceClock(DateTime serverUtc)
        {
            return Sdk.Events != null ? serverUtc - Sdk.Events.ClockOffset : serverUtc;
        }

        private List<ActiveEvent> Filtered()
        {
            var shown = new List<ActiveEvent>();
            if (_events == null) return shown;

            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];

                if (_audience == FilterMine && !e.IsMatched) continue;
                if (_audience == FilterOthers && e.IsMatched) continue;

                if (_search.Length > 0)
                {
                    bool hit = (e.Key != null && e.Key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                               || (e.Name != null && e.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit) continue;
                }

                shown.Add(e);
            }

            return shown;
        }
    }
}

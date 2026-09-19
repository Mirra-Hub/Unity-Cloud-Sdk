using System;
using System.Collections.Generic;
using MirraCloud.Core.Events.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Events
{
    /// <summary>
    /// The LiveOps events running for this player.
    ///
    /// <para>
    /// <b>This service does not make events work.</b> While an event runs the server already hands
    /// this player different economy values — prices, limits, rewards — and a game that never calls
    /// anything here still gets them. What it cannot do without this is <i>say so</i>: put up a
    /// banner, run a countdown to the end of the offer, or open a screen only to the audience an
    /// event targets. That is what this is for.
    /// </para>
    ///
    /// <para>
    /// Unlike the config services, this is not part of a one-time warm-up on the splash screen: the
    /// answer depends on the player and is only true for minutes. Fetch it when a screen needs it and
    /// re-fetch when <see cref="IsStale"/> says so.
    /// </para>
    /// </summary>
    public class EventsService : ICloudSdkService
    {
        private const string ControllerApi = "/events/v1";

        /// <summary>
        /// Floor and ceiling for how long a fetched answer is trusted. The floor keeps a window that
        /// is about to close from turning IsStale into a request loop; the ceiling is the server's own
        /// snapshot TTL, past which the answer would be guesswork anyway.
        /// </summary>
        private static readonly TimeSpan MinFreshness = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxFreshness = TimeSpan.FromMinutes(5);

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;

        private readonly List<ActiveEvent> _activeEvents = new List<ActiveEvent>();
        private readonly List<ActiveEvent> _myEvents = new List<ActiveEvent>();
        private readonly Dictionary<string, ActiveEvent> _byKey = new Dictionary<string, ActiveEvent>(StringComparer.Ordinal);

        private DateTime? _staleAfterDeviceUtc;

        public EventsService(Configuration configuration, ILogger logger, RestApiClient restApiClient)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApiClient;
        }

        /// <summary>Every event this player is in the audience of, plus — if asked for — the rest.</summary>
        public IReadOnlyList<ActiveEvent> ActiveEvents => _activeEvents;

        /// <summary>The subset that applies to this player. What a game's UI should be built on.</summary>
        public IReadOnlyList<ActiveEvent> MyEvents => _myEvents;

        /// <summary>Whether a successful fetch has landed since the last sign-in or profile switch.</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>The generation the cached answer was fetched at; unchanged means the same set.</summary>
        public long Generation { get; private set; }

        /// <summary>
        /// Difference between the server's clock and this device's, measured on the last fetch. Device
        /// clocks are wrong often enough that countdowns built on them visibly lie.
        /// </summary>
        public TimeSpan ClockOffset { get; private set; }

        /// <summary>Now, as the server would report it. Every countdown here is measured from this.</summary>
        public DateTime ServerUtcNow => DateTime.UtcNow + ClockOffset;

        /// <summary>
        /// Whether the cached answer has outlived what the server said it was good for — usually a
        /// window about to open or close. Nothing refetches on its own: a game decides when to ask.
        /// </summary>
        public bool IsStale => !IsLoaded || _staleAfterDeviceUtc == null || DateTime.UtcNow >= _staleAfterDeviceUtc.Value;

        /// <summary>
        /// Fetches what is running, and caches it for the synchronous lookups below.
        ///
        /// <para>Always hits the network — see the note on this class about warm-up.</para>
        /// </summary>
        /// <param name="includeUnmatched">
        /// Also list events running for other audiences, flagged <see cref="ActiveEvent.IsMatched"/>
        /// false. Useful in a support or debug screen; in a game's UI it promises offers the player
        /// cannot have.
        /// </param>
        public AsyncOperation<RestApiResult<ActiveEventsDto>> GetActiveEventsAsync(bool includeUnmatched = false)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}" +
                           $"/events/active?includeUnmatched={(includeUnmatched ? "true" : "false")}";

            var operation = _restApi.GetAsync<ActiveEventsDto>(route);

            // Hooked synchronously: UseCompleted holds one callback and does not fire for an operation
            // that already finished, so anything deferred would miss a cached or failed-fast response.
            operation.UseCompleted(completed =>
            {
                var result = completed.Result;
                if (!result.IsSuccess || result.Data == null || result.Data.events == null)
                {
                    _logger.Error(result.Error?.Message ?? "Active events request failed.");
                    return;
                }

                Apply(result.Data);
            });

            return operation;
        }

        /// <summary>Looks up a cached event by the key from the console.</summary>
        public bool TryGetEvent(string key, out ActiveEvent activeEvent)
        {
            if (string.IsNullOrEmpty(key))
            {
                activeEvent = null;
                return false;
            }

            return _byKey.TryGetValue(key, out activeEvent);
        }

        /// <summary>
        /// Whether this event is running <b>and</b> applies to this player — the question a content
        /// gate is asking. Running-for-somebody-else answers false, which is why this is here rather
        /// than left to each game to remember.
        /// </summary>
        public bool IsEventActive(string key) => IsEventActive(key, requireMatched: true);

        /// <param name="requireMatched">
        /// False to ask only whether the event is running at all, ignoring the audience. Correct for a
        /// "server is busy with an event" notice; wrong for anything a player gets.
        /// </param>
        public bool IsEventActive(string key, bool requireMatched)
            => TryGetEvent(key, out var activeEvent) && (!requireMatched || activeEvent.IsMatched);

        /// <summary>How long this event's current run has left, or null when it is not cached.</summary>
        public TimeSpan? GetTimeLeft(string key)
            => TryGetEvent(key, out var activeEvent) ? activeEvent.TimeLeft(ServerUtcNow) : (TimeSpan?)null;

        /// <summary>How long until it opens again, or null when it is not cached or will not repeat.</summary>
        public TimeSpan? GetTimeUntilNextOccurrence(string key)
            => TryGetEvent(key, out var activeEvent) ? activeEvent.TimeUntilNextOccurrence(ServerUtcNow) : null;

        /// <summary>
        /// Drops the cached answer. Called for you when the player signs in or switches profile — the
        /// audience is per profile, so the old answer describes someone else.
        /// </summary>
        public void Clear()
        {
            _activeEvents.Clear();
            _myEvents.Clear();
            _byKey.Clear();
            _staleAfterDeviceUtc = null;
            Generation = 0;
            IsLoaded = false;
        }

        private void Apply(ActiveEventsDto dto)
        {
            // Cleared only now that a good answer is in hand: a failed refresh should leave a game
            // showing what it had, not an empty screen.
            _activeEvents.Clear();
            _myEvents.Clear();
            _byKey.Clear();

            var fetchedAtDeviceUtc = DateTime.UtcNow;
            var serverTimeUtc = DateTime.SpecifyKind(dto.serverTimeUtc, DateTimeKind.Utc);
            ClockOffset = serverTimeUtc - fetchedAtDeviceUtc;

            foreach (var eventDto in dto.events)
            {
                if (eventDto == null || string.IsNullOrEmpty(eventDto.key)) continue;

                var activeEvent = new ActiveEvent(eventDto);
                _activeEvents.Add(activeEvent);
                _byKey[activeEvent.Key] = activeEvent;
                if (activeEvent.IsMatched) _myEvents.Add(activeEvent);
            }

            Generation = dto.generation;
            _staleAfterDeviceUtc = fetchedAtDeviceUtc + Freshness(dto, serverTimeUtc);
            IsLoaded = true;
        }

        /// <summary>
        /// How long to trust this answer: what the server said, clamped. Measured against the device's
        /// clock because that is what will be read later — the offset is already accounted for.
        /// </summary>
        private static TimeSpan Freshness(ActiveEventsDto dto, DateTime serverTimeUtc)
        {
            if (dto.staleAfterUtc == null) return MaxFreshness;

            var window = DateTime.SpecifyKind(dto.staleAfterUtc.Value, DateTimeKind.Utc) - serverTimeUtc;
            if (window < MinFreshness) return MinFreshness;

            return window > MaxFreshness ? MaxFreshness : window;
        }

        public void CloudSdkInitialize() { }

        public void CloudSdkDispose() => Clear();
    }
}

using System;
using MirraCloud.Core.Events.Dto;
using MirraCloud.Core.Events.Enums;

namespace MirraCloud.Core.Events
{
    /// <summary>
    /// A LiveOps event that is running right now, with its times already worked out.
    ///
    /// <para>
    /// Wraps the wire DTO for two reasons. Dates arrive parsed with <c>RoundtripKind</c>, so a
    /// timestamp that reached the mapper without its trailing Z would be <c>Unspecified</c> and every
    /// countdown built from it would be silently off by the device's timezone — they are pinned to UTC
    /// here, once. And the arithmetic a game actually wants ("how much longer?") belongs somewhere
    /// other than every caller.
    /// </para>
    /// </summary>
    public sealed class ActiveEvent
    {
        /// <summary>
        /// The key set in the console — the identifier to write into game code. The event's document
        /// id is not stable across edits, which is why none is exposed.
        /// </summary>
        public string Key { get; }

        /// <summary>Name from the console. A LiveOps label, not necessarily player-facing copy.</summary>
        public string Name { get; }

        /// <summary>The team's own note about the event. Show your own localised text to players.</summary>
        public string Description { get; }

        /// <summary>Where two events touch the same value, the higher number wins on the server.</summary>
        public int Priority { get; }

        public EventScheduleType ScheduleType { get; }

        /// <summary>
        /// Whether this player is in the event's audience. An event can be listed, genuinely running,
        /// and still not apply here — that is what targeting rules are for.
        /// </summary>
        public bool IsMatched { get; }

        /// <summary>
        /// Whether the event targets an audience at all. Together with <see cref="IsMatched"/> this
        /// separates "runs for everyone" from "runs, but not for you".
        /// </summary>
        public bool IsTargeted { get; }

        /// <summary>Start of the run in progress — not of the campaign.</summary>
        public DateTime StartsAtUtc { get; }

        /// <summary>End of the run in progress. This is what a countdown counts down to.</summary>
        public DateTime EndsAtUtc { get; }

        /// <summary>When it opens again. Null for a one-off, or a pattern that will not repeat.</summary>
        public DateTime? NextOccurrenceAtUtc { get; }

        /// <summary>When it stops running for good — "on until the 31st".</summary>
        public DateTime? ScheduleEndsAtUtc { get; }

        public ActiveEvent(ActiveEventDto dto)
        {
            Key = dto.key;
            Name = dto.name;
            Description = dto.description;
            Priority = dto.priority;
            ScheduleType = dto.scheduleType;
            IsMatched = dto.isMatched;
            IsTargeted = dto.isTargeted;
            StartsAtUtc = AsUtc(dto.startsAtUtc);
            EndsAtUtc = AsUtc(dto.endsAtUtc);
            NextOccurrenceAtUtc = AsUtc(dto.nextOccurrenceAtUtc);
            ScheduleEndsAtUtc = AsUtc(dto.scheduleEndsAtUtc);
        }

        /// <summary>
        /// How much of this run is left, measured against the server's clock. Zero once it is over;
        /// never negative, so it can be fed straight to a progress bar.
        /// </summary>
        public TimeSpan TimeLeft(DateTime serverUtcNow)
        {
            var left = EndsAtUtc - serverUtcNow;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        /// <summary>How long until it opens again, or null when it will not.</summary>
        public TimeSpan? TimeUntilNextOccurrence(DateTime serverUtcNow)
        {
            if (NextOccurrenceAtUtc == null) return null;

            var until = NextOccurrenceAtUtc.Value - serverUtcNow;
            return until > TimeSpan.Zero ? until : TimeSpan.Zero;
        }

        /// <summary>How far through this run we are, 0 to 1 — for a progress bar under a banner.</summary>
        public float Progress(DateTime serverUtcNow)
        {
            var total = (EndsAtUtc - StartsAtUtc).TotalSeconds;
            if (total <= 0) return 1f;

            var elapsed = (serverUtcNow - StartsAtUtc).TotalSeconds;
            if (elapsed <= 0) return 0f;

            return elapsed >= total ? 1f : (float)(elapsed / total);
        }

        /// <summary>
        /// The server sends ISO-8601 with a trailing Z, but a value that ever loses it would be parsed
        /// as Unspecified and then treated as local time by every comparison below.
        /// </summary>
        private static DateTime AsUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime? AsUtc(DateTime? value)
            => value == null ? (DateTime?)null : AsUtc(value.Value);
    }
}

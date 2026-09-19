using System;
using NUnit.Framework;
using MirraCloud.Core.Events;
using MirraCloud.Core.Events.Dto;
using MirraCloud.Core.Events.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Events.Tests
{
    /// <summary>
    /// The active-events payload, read the way the runtime reads it.
    ///
    /// <para>
    /// Two failure modes here are silent rather than loud, which is why they are pinned. Member names
    /// are matched case-sensitively, so a camelCase payload read into PascalCase fields leaves every
    /// value at its default — an event list that parses into blanks instead of failing. And a date
    /// whose Kind comes back Unspecified is treated as local time by every comparison afterwards, so
    /// a countdown drifts by the device's offset without anything looking wrong.
    /// </para>
    /// </summary>
    [TestFixture]
    public class EventsDtoMappingTests
    {
        /// <summary>A real response: one one-shot for everyone, one recurring the player is not in.</summary>
        private const string Payload =
            "{\"serverTimeUtc\":\"2026-09-19T10:00:00Z\",\"generation\":42,"
            + "\"staleAfterUtc\":\"2026-09-19T10:04:00Z\",\"events\":["
            + "{\"key\":\"halloween_2026\",\"name\":\"Halloween\",\"description\":\"seasonal\",\"priority\":10,"
            + "\"scheduleType\":\"OneShot\",\"isMatched\":true,\"isTargeted\":false,"
            + "\"startsAtUtc\":\"2026-09-19T08:00:00Z\",\"endsAtUtc\":\"2026-09-19T12:00:00Z\","
            + "\"nextOccurrenceAtUtc\":null,\"scheduleEndsAtUtc\":\"2026-09-19T12:00:00Z\"},"
            + "{\"key\":\"happy_hour\",\"name\":\"Happy hour\",\"description\":null,\"priority\":1,"
            + "\"scheduleType\":\"Recurring\",\"isMatched\":false,\"isTargeted\":true,"
            + "\"startsAtUtc\":\"2026-09-19T09:00:00Z\",\"endsAtUtc\":\"2026-09-19T11:00:00Z\","
            + "\"nextOccurrenceAtUtc\":\"2026-09-20T09:00:00Z\",\"scheduleEndsAtUtc\":null}]}";

        [Test]
        public void Reads_the_whole_payload()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);

            Assert.That(dto.generation, Is.EqualTo(42L));
            Assert.That(dto.serverTimeUtc.ToUniversalTime(),
                Is.EqualTo(new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc)));
            Assert.That(dto.staleAfterUtc, Is.Not.Null);
            Assert.That(dto.events, Has.Length.EqualTo(2));

            var halloween = dto.events[0];
            Assert.That(halloween.key, Is.EqualTo("halloween_2026"));
            Assert.That(halloween.name, Is.EqualTo("Halloween"));
            Assert.That(halloween.priority, Is.EqualTo(10));
            Assert.That(halloween.isMatched, Is.True);
            Assert.That(halloween.isTargeted, Is.False);
            Assert.That(halloween.nextOccurrenceAtUtc, Is.Null);
        }

        /// <summary>The host that serves Events writes enums as their names, not their numbers.</summary>
        [Test]
        public void Reads_the_schedule_type_from_its_name()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);

            Assert.That(dto.events[0].scheduleType, Is.EqualTo(EventScheduleType.OneShot));
            Assert.That(dto.events[1].scheduleType, Is.EqualTo(EventScheduleType.Recurring));
        }

        [Test]
        public void Reads_populated_and_null_nullable_dates()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);

            Assert.That(dto.events[0].scheduleEndsAtUtc, Is.Not.Null);
            Assert.That(dto.events[0].nextOccurrenceAtUtc, Is.Null);
            Assert.That(dto.events[1].nextOccurrenceAtUtc?.ToUniversalTime(),
                Is.EqualTo(new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc)));
            Assert.That(dto.events[1].scheduleEndsAtUtc, Is.Null);
        }

        /// <summary>
        /// Whatever the parser decided about Kind, the wrapper reports UTC — otherwise the subtraction
        /// in every countdown below silently picks up the device's offset.
        /// </summary>
        [Test]
        public void Pins_every_date_to_utc()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);
            var activeEvent = new ActiveEvent(dto.events[1]);

            Assert.That(activeEvent.StartsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(activeEvent.EndsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(activeEvent.NextOccurrenceAtUtc?.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void Counts_down_against_the_servers_clock()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);
            var halloween = new ActiveEvent(dto.events[0]);
            var serverNow = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

            Assert.That(halloween.TimeLeft(serverNow), Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(halloween.Progress(serverNow), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(halloween.TimeUntilNextOccurrence(serverNow), Is.Null);
        }

        /// <summary>A run that is over reads as zero, not as a negative span backing a bar.</summary>
        [Test]
        public void A_finished_run_has_no_time_left()
        {
            var dto = JsonMapper.FromJson<ActiveEventsDto>(Payload);
            var halloween = new ActiveEvent(dto.events[0]);

            var afterwards = new DateTime(2026, 9, 19, 13, 0, 0, DateTimeKind.Utc);

            Assert.That(halloween.TimeLeft(afterwards), Is.EqualTo(TimeSpan.Zero));
            Assert.That(halloween.Progress(afterwards), Is.EqualTo(1f));
        }
    }
}

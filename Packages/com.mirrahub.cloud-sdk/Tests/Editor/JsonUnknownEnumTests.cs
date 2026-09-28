using System;
using System.Collections.Generic;
using MirraCloud.Core.Leaderboard.Enums;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// An enum value the server added after this build was released. Reading used to throw, and the whole response
    /// was lost with it — one new reward kind on one board hid every leaderboard of the project. The value now reads
    /// as unknown and the rest of the response is kept.
    /// </summary>
    [TestFixture]
    public class JsonUnknownEnumTests
    {
        [Serializable]
        public sealed class Probe
        {
            public OrderType order;
            public OrderType? maybe;
            public int after;
        }

        private List<string> _warnings;
        private Action<string> _previous;

        [SetUp]
        public void CaptureWarnings()
        {
            _warnings = new List<string>();
            _previous = JsonMapper.Warning;
            JsonMapper.Warning = _warnings.Add;
        }

        [TearDown]
        public void RestoreWarnings()
        {
            JsonMapper.Warning = _previous;
        }

        [Test]
        public void An_unknown_value_reads_as_the_default_and_the_rest_of_the_object_is_kept()
        {
            var probe = JsonMapper.FromJson<Probe>("{\"order\":\"Median\",\"maybe\":\"Lowest\",\"after\":7}");

            Assert.That(probe.order, Is.EqualTo(default(OrderType)));
            Assert.That(probe.maybe, Is.EqualTo(OrderType.Lowest));
            Assert.That(probe.after, Is.EqualTo(7));
            Assert.That(_warnings, Has.Count.EqualTo(1));
            Assert.That(_warnings[0], Does.Contain("Median").And.Contain(nameof(OrderType)));
        }

        [Test]
        public void An_unknown_value_of_a_nullable_enum_reads_as_null()
        {
            var probe = JsonMapper.FromJson<Probe>("{\"order\":\"Highest\",\"maybe\":\"Median\",\"after\":1}");

            Assert.That(probe.order, Is.EqualTo(OrderType.Highest));
            Assert.That(probe.maybe, Is.Null);
            Assert.That(probe.after, Is.EqualTo(1));
        }

        [Test]
        public void Known_values_read_as_before_without_a_warning()
        {
            var probe = JsonMapper.FromJson<Probe>("{\"order\":\"lowest\",\"maybe\":1,\"after\":0}");

            Assert.That(probe.order, Is.EqualTo(OrderType.Lowest));
            Assert.That(probe.maybe, Is.EqualTo(OrderType.Lowest));
            Assert.That(_warnings, Is.Empty);
        }
    }
}

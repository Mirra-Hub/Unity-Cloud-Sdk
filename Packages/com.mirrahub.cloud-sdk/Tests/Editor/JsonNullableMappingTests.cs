using System;
using System.Collections.Generic;
using NUnit.Framework;
using MirraCloud.Json;

namespace MirraCloud.Json.Tests
{
    /// <summary>
    /// Regression cover for reading <see cref="Nullable{T}"/> members.
    /// <see cref="Convert.ChangeType(object, Type)"/> cannot target a nullable type, so every branch of the
    /// reader has to hand the boxed underlying value back instead. Two branches used to forget, which turned
    /// any populated <c>int?</c>, <c>bool?</c>, fractional <c>double?</c> or number-encoded <c>enum?</c>
    /// into a deserialization failure — e.g. the spend-energy response once the meter dropped below full.
    /// </summary>
    [TestFixture]
    public class JsonNullableMappingTests
    {
        private enum Tone
        {
            None = 0,
            Warm = 1,
            Cold = 2,
        }

        [Serializable]
        private sealed class Nullables
        {
            public int? Int;
            public bool? Bool;
            public double? Double;
            public float? Float;
            public long? Long;
            public decimal? Decimal;
            public short? Short;
            public byte? Byte;
            public uint? UInt;
            public ulong? ULong;
            public Tone? EnumFromNumber;
            public Tone? EnumFromString;
            public DateTime? Date;
        }

        [Serializable]
        private sealed class Plain
        {
            public int Int;
            public bool Bool;
            public double Double;
            public float Float;
            public long Long;
            public decimal Decimal;
            public Tone EnumFromNumber;
            public Tone EnumFromString;
            public string String;
            public DateTime Date;
        }

        /// <summary>Mirrors <c>MirraCloud.Core.Economy.Dto.EnergyBalanceDto</c>, the DTO that surfaced the bug.</summary>
        [Serializable]
        private sealed class EnergyBalance
        {
            [JsonNameCamel] public string EnergyId;
            [JsonNameCamel] public int CurrentValue;
            [JsonNameCamel] public int MaxValue;
            [JsonNameCamel] public int? SecondsUntilNextRecharge;
            [JsonNameCamel] public int? SecondsUntilFullRecharge;
            [JsonNameCamel] public bool IsOnCooldown;
            [JsonNameCamel] public int? CooldownRemainingSeconds;
            [JsonNameCamel] public bool IsUnlimited;
            [JsonNameCamel] public int? UnlimitedRemainingSeconds;
        }

        [Serializable]
        private sealed class Containers
        {
            public List<int?> List;
            public int?[] Array;
            public Dictionary<string, int?> Map;
        }

        private const string PopulatedJson =
            "{\"Int\":300,\"Bool\":true,\"Double\":1.5,\"Float\":2.5,\"Long\":9000000000,\"Decimal\":5.25,"
            + "\"Short\":7,\"Byte\":3,\"UInt\":42,\"ULong\":18,\"EnumFromNumber\":1,\"EnumFromString\":\"Cold\","
            + "\"Date\":\"2026-08-31T10:00:00Z\"}";

        [Test]
        public void Reads_nullable_members_that_carry_a_value()
        {
            var dto = JsonMapper.FromJson<Nullables>(PopulatedJson);

            Assert.That(dto.Int, Is.EqualTo(300));
            Assert.That(dto.Bool, Is.True);
            Assert.That(dto.Double, Is.EqualTo(1.5d));
            Assert.That(dto.Float, Is.EqualTo(2.5f));
            Assert.That(dto.Long, Is.EqualTo(9000000000L));
            Assert.That(dto.Decimal, Is.EqualTo(5.25m));
            Assert.That(dto.Short, Is.EqualTo((short)7));
            Assert.That(dto.Byte, Is.EqualTo((byte)3));
            Assert.That(dto.UInt, Is.EqualTo(42u));
            Assert.That(dto.ULong, Is.EqualTo(18ul));
            Assert.That(dto.EnumFromNumber, Is.EqualTo(Tone.Warm));
            Assert.That(dto.EnumFromString, Is.EqualTo(Tone.Cold));
            Assert.That(dto.Date?.ToUniversalTime(), Is.EqualTo(new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Reads_nullable_members_that_carry_null()
        {
            var dto = JsonMapper.FromJson<Nullables>(
                "{\"Int\":null,\"Bool\":null,\"Double\":null,\"EnumFromNumber\":null,\"Date\":null}");

            Assert.That(dto.Int.HasValue, Is.False);
            Assert.That(dto.Bool.HasValue, Is.False);
            Assert.That(dto.Double.HasValue, Is.False);
            Assert.That(dto.EnumFromNumber.HasValue, Is.False);
            Assert.That(dto.Date.HasValue, Is.False);
        }

        [Test]
        public void Reads_non_nullable_members_unchanged()
        {
            var dto = JsonMapper.FromJson<Plain>(
                "{\"Int\":7,\"Bool\":true,\"Double\":1.5,\"Float\":2.5,\"Long\":9000000000,\"Decimal\":5.25,"
                + "\"EnumFromNumber\":2,\"EnumFromString\":\"Warm\",\"String\":\"hi\",\"Date\":\"2026-08-31T10:00:00Z\"}");

            Assert.That(dto.Int, Is.EqualTo(7));
            Assert.That(dto.Bool, Is.True);
            Assert.That(dto.Double, Is.EqualTo(1.5d));
            Assert.That(dto.Float, Is.EqualTo(2.5f));
            Assert.That(dto.Long, Is.EqualTo(9000000000L));
            Assert.That(dto.Decimal, Is.EqualTo(5.25m));
            Assert.That(dto.EnumFromNumber, Is.EqualTo(Tone.Cold));
            Assert.That(dto.EnumFromString, Is.EqualTo(Tone.Warm));
            Assert.That(dto.String, Is.EqualTo("hi"));
            Assert.That(dto.Date.ToUniversalTime(), Is.EqualTo(new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Reads_a_spent_energy_response()
        {
            // The server only fills the recharge fields once the meter drops below max, so this shape only
            // ever appears after a spend — which is exactly why the bug hid behind a full meter.
            var dto = JsonMapper.FromJson<EnergyBalance>(
                "{\"energyId\":\"stamina\",\"currentValue\":9,\"maxValue\":10,\"secondsUntilNextRecharge\":300,"
                + "\"secondsUntilFullRecharge\":300,\"isOnCooldown\":true,\"cooldownRemainingSeconds\":60,"
                + "\"isUnlimited\":true,\"unlimitedRemainingSeconds\":3600}");

            Assert.That(dto.EnergyId, Is.EqualTo("stamina"));
            Assert.That(dto.CurrentValue, Is.EqualTo(9));
            Assert.That(dto.SecondsUntilNextRecharge, Is.EqualTo(300));
            Assert.That(dto.SecondsUntilFullRecharge, Is.EqualTo(300));
            Assert.That(dto.IsOnCooldown, Is.True);
            Assert.That(dto.CooldownRemainingSeconds, Is.EqualTo(60));
            Assert.That(dto.IsUnlimited, Is.True);
            Assert.That(dto.UnlimitedRemainingSeconds, Is.EqualTo(3600));
        }

        [Test]
        public void Reads_a_full_energy_meter()
        {
            var dto = JsonMapper.FromJson<EnergyBalance>(
                "{\"energyId\":\"stamina\",\"currentValue\":10,\"maxValue\":10,\"secondsUntilNextRecharge\":null,"
                + "\"secondsUntilFullRecharge\":null,\"isOnCooldown\":false,\"cooldownRemainingSeconds\":null,"
                + "\"isUnlimited\":false,\"unlimitedRemainingSeconds\":null}");

            Assert.That(dto.CurrentValue, Is.EqualTo(10));
            Assert.That(dto.SecondsUntilNextRecharge.HasValue, Is.False);
            Assert.That(dto.CooldownRemainingSeconds.HasValue, Is.False);
        }

        [Test]
        public void Reads_nullables_inside_collections()
        {
            var dto = JsonMapper.FromJson<Containers>(
                "{\"List\":[1,null,3],\"Array\":[4,null,6],\"Map\":{\"x\":1,\"y\":null}}");

            Assert.That(dto.List, Is.EqualTo(new int?[] { 1, null, 3 }));
            Assert.That(dto.Array, Is.EqualTo(new int?[] { 4, null, 6 }));
            Assert.That(dto.Map["x"], Is.EqualTo(1));
            Assert.That(dto.Map["y"].HasValue, Is.False);
        }

        [Test]
        public void Reads_multidimensional_arrays_of_nullables()
        {
            var plain = JsonMapper.FromJson<int[,]>("[[1,2],[3,4]]");
            Assert.That(plain[0, 0], Is.EqualTo(1));
            Assert.That(plain[1, 1], Is.EqualTo(4));

            var nullable = JsonMapper.FromJson<int?[,]>("[[1,null],[3,4]]");
            Assert.That(nullable[0, 0], Is.EqualTo(1));
            Assert.That(nullable[0, 1].HasValue, Is.False);
            Assert.That(nullable[1, 1], Is.EqualTo(4));
        }

        [Test]
        public void Round_trips_nullables()
        {
            const string json = "{\"Int\":300,\"Bool\":true,\"Double\":1.5,\"EnumFromNumber\":1,\"Long\":null}";

            var back = JsonMapper.FromJson<Nullables>(JsonMapper.ToJson(JsonMapper.FromJson<Nullables>(json)));

            Assert.That(back.Int, Is.EqualTo(300));
            Assert.That(back.Bool, Is.True);
            Assert.That(back.Double, Is.EqualTo(1.5d));
            Assert.That(back.EnumFromNumber, Is.EqualTo(Tone.Warm));
            Assert.That(back.Long.HasValue, Is.False);
        }

        [Test]
        public void Reports_a_readable_error_for_a_genuinely_unassignable_value()
        {
            // The nullable fix must not swallow real mismatches.
            Assert.That(
                () => JsonMapper.FromJson<Nullables>("{\"Int\":\"not a number\"}"),
                Throws.TypeOf<InvalidCastException>());
        }
    }
}

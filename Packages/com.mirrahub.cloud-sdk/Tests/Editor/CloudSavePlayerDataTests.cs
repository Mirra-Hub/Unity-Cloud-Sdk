using MirraCloud.Core.CloudSave;
using MirraCloud.Core.CloudSave.Requests;
using MirraCloud.Core.CloudSave.Responses;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Findings of the cloud-save QA run: objects read as an empty string, long values fell back to the default,
    /// floats were stored with a double tail, a filtered read wiped the cache, and every write sent Owner/Owner masks.
    /// </summary>
    [TestFixture]
    public class CloudSavePlayerDataTests
    {
        private static readonly JsonService Json = new JsonService();

        private static DataItemResponse Item(string key, JsonValue value, CloudSaveFieldType type)
        {
            return new DataItemResponse { key = key, value = value, fieldType = type };
        }

        private static JsonValue Parse(string json)
        {
            return Json.FromJson<JsonValue>(json);
        }

        [Test]
        public void An_object_reads_as_its_json()
        {
            var data = new PlayerData(new[] { Item("inventory", Parse("{\"sword\":1}"), CloudSaveFieldType.String) });

            Assert.That(data.GetString("inventory"), Is.EqualTo("{\"sword\":1}"));
        }

        [Test]
        public void A_long_value_reads_back()
        {
            var data = new PlayerData(new[] { Item("lastSeen", Parse("1759140000000"), CloudSaveFieldType.Int) });

            Assert.That(data.GetLong("lastSeen"), Is.EqualTo(1759140000000L));
            Assert.That(data.GetInt("lastSeen", -1), Is.EqualTo(-1), "out of int range");
        }

        [Test]
        public void Numbers_read_through_every_numeric_getter()
        {
            var data = new PlayerData(new[]
            {
                Item("level", Parse("7"), CloudSaveFieldType.Int),
                Item("progress", Parse("0.42"), CloudSaveFieldType.Float),
            });

            Assert.That(data.GetInt("level"), Is.EqualTo(7));
            Assert.That(data.GetDouble("level"), Is.EqualTo(7.0));
            Assert.That(data.GetFloat("progress"), Is.EqualTo(0.42f));
            Assert.That(data.GetDouble("progress"), Is.EqualTo(0.42));
        }

        [Test]
        public void A_float_is_written_without_a_double_tail()
        {
            var request = new CloudSaveDataRequest().AddFloat("progress", 0.1f);

            Assert.That(Json.ToJson(request.items[0].value), Is.EqualTo("0.1"));
        }

        [Test]
        public void A_long_is_written_whole()
        {
            var request = new CloudSaveDataRequest().AddLong("lastSeen", 1759140000000L);

            Assert.That(Json.ToJson(request.items[0].value), Is.EqualTo("1759140000000"));
        }

        [Test]
        public void Masks_left_out_are_sent_as_null()
        {
            var request = new CloudSaveDataRequest().AddInt("level", 7);

            Assert.That(request.items[0].readMask, Is.Null);
            Assert.That(request.items[0].writeMask, Is.Null);
            StringAssert.Contains("\"readMask\":null", Json.ToJson(request));
        }

        [Test]
        public void A_filtered_read_keeps_the_rest_of_the_cache()
        {
            var full = new PlayerData(new[]
            {
                Item("tutorial", new JsonValue(true), CloudSaveFieldType.Boolean),
                Item("level", new JsonValue(1), CloudSaveFieldType.Int),
                Item("gone", new JsonValue(1), CloudSaveFieldType.Int),
            });

            var merged = full.Merge(new[] { Item("level", new JsonValue(2), CloudSaveFieldType.Int) }, new[] { "level", "gone" });

            Assert.That(merged.GetBool("tutorial"), Is.True, "a key outside the read stays");
            Assert.That(merged.GetInt("level"), Is.EqualTo(2), "a returned key is replaced");
            Assert.That(merged.HasKey("gone"), Is.False, "a requested key that did not come back no longer exists");
        }
    }
}

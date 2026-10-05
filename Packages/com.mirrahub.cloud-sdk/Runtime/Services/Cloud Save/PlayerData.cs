using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Core.CloudSave.Responses;
using MirraCloud.Json;

namespace MirraCloud.Core.CloudSave
{
    public class PlayerData
    {
        private static readonly JsonService Json = new JsonService();

        private readonly List<PlayerDataField> _fields = new List<PlayerDataField>();
        private readonly Dictionary<string, DataItemResponse> _items = new Dictionary<string, DataItemResponse>();

        public IReadOnlyList<PlayerDataField> Fields => _fields;

        private readonly Dictionary<string, string> _stringFields = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _boolFields = new Dictionary<string, bool>();
        private readonly Dictionary<string, double> _numberFields = new Dictionary<string, double>();

        public PlayerData(DataItemResponse[] dataItems)
        {
            if (dataItems == null)
                return;

            foreach (var item in dataItems)
            {
                if (string.IsNullOrEmpty(item.key))
                    continue;

                _items[item.key] = item;
            }

            foreach (var item in _items.Values)
            {
                Index(item);
            }
        }

        /// <summary>
        /// This data with a partial read applied on top: returned keys replace the cached ones, and
        /// <paramref name="requestedKeys"/> that did not come back are dropped (they no longer exist).
        /// Keys outside the read are kept.
        /// </summary>
        public PlayerData Merge(DataItemResponse[] dataItems, IReadOnlyCollection<string> requestedKeys)
        {
            var merged = new Dictionary<string, DataItemResponse>(_items);

            if (requestedKeys != null)
            {
                foreach (var key in requestedKeys)
                {
                    merged.Remove(key);
                }
            }

            if (dataItems != null)
            {
                foreach (var item in dataItems)
                {
                    if (!string.IsNullOrEmpty(item.key))
                        merged[item.key] = item;
                }
            }

            return new PlayerData(new List<DataItemResponse>(merged.Values).ToArray());
        }

        public bool HasKey(string key) => _items.ContainsKey(key);

        /// <summary>
        /// The value as text: strings as they are, numbers and booleans formatted, objects and arrays as JSON.
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            if (_stringFields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (_boolFields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        /// <summary>The value as an <see cref="int"/>; the default when it is not a whole number in range.</summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (_numberFields.TryGetValue(key, out var value) && value % 1 == 0 && value >= int.MinValue && value <= int.MaxValue)
                return (int)value;
            return defaultValue;
        }

        /// <summary>The value as a <see cref="long"/>, e.g. Unix time in milliseconds; the default when it is not a whole number.</summary>
        public long GetLong(string key, long defaultValue = 0)
        {
            if (_numberFields.TryGetValue(key, out var value) && value % 1 == 0 && value >= long.MinValue && value <= long.MaxValue)
                return (long)value;
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0)
        {
            if (_numberFields.TryGetValue(key, out var value))
                return (float)value;
            return defaultValue;
        }

        public double GetDouble(string key, double defaultValue = 0)
        {
            if (_numberFields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        private void Index(DataItemResponse item)
        {
            string stringValue = ExtractStringValue(item.value);

            _fields.Add(new PlayerDataField(
                item.key, stringValue, item.fieldType,
                item.readMask, item.writeMask,
                item.version, item.updatedAtUtc));

            _stringFields[item.key] = stringValue;

            var value = item.value;
            if (value == null)
                return;

            switch (value.Type)
            {
                case JsonValueType.Boolean:
                    _boolFields[item.key] = (bool)value;
                    break;
                case JsonValueType.Int:
                    _numberFields[item.key] = (int)value;
                    break;
                case JsonValueType.Double:
                    _numberFields[item.key] = (double)value;
                    break;
                case JsonValueType.String:
                {
                    // Values written as text by older clients or the console still read as numbers and booleans.
                    if (bool.TryParse(stringValue, out var boolValue))
                        _boolFields[item.key] = boolValue;
                    if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        _numberFields[item.key] = number;
                    break;
                }
            }
        }

        private static string ExtractStringValue(JsonValue value)
        {
            if (value == null || value.Type == JsonValueType.Null)
                return "";

            switch (value.Type)
            {
                case JsonValueType.String:
                    return (string)value;
                case JsonValueType.Int:
                    return ((int)value).ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Double:
                    return ((double)value).ToString("R", CultureInfo.InvariantCulture);
                case JsonValueType.Boolean:
                    return ((bool)value).ToString();
                default:
                    return Json.ToJson(value);
            }
        }
    }
}

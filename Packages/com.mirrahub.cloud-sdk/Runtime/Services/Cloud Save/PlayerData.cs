using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Core.CloudSave.Responses;
using MirraCloud.Json;

namespace MirraCloud.Core.CloudSave
{
    public class PlayerData
    {
        private readonly List<PlayerDataField> _fields = new List<PlayerDataField>();

        public IReadOnlyList<PlayerDataField> Fields => _fields;

        private readonly Dictionary<string, string> _stringFields = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _boolFields = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> _floatFields = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _intFields = new Dictionary<string, int>();

        public PlayerData(DataItemResponse[] dataItems)
        {
            if (dataItems == null)
                return;

            foreach (var item in dataItems)
            {
                if (string.IsNullOrEmpty(item.key))
                    continue;

                string stringValue = ExtractStringValue(item.value);

                _fields.Add(new PlayerDataField(
                    item.key, stringValue, item.fieldType,
                    item.readMask, item.writeMask,
                    item.version, item.updatedAtUtc));

                switch (item.fieldType)
                {
                    case CloudSaveFieldType.String:
                    {
                        _stringFields[item.key] = stringValue;
                        break;
                    }
                    case CloudSaveFieldType.Boolean:
                    {
                        if (item.value != null && item.value.Type == JsonValueType.Boolean)
                        {
                            _boolFields[item.key] = (bool)item.value;
                        }
                        else if (bool.TryParse(stringValue, out var boolVal))
                        {
                            _boolFields[item.key] = boolVal;
                        }
                        break;
                    }
                    case CloudSaveFieldType.Float:
                    {
                        if (item.value != null && (item.value.Type == JsonValueType.Double || item.value.Type == JsonValueType.Int))
                        {
                            _floatFields[item.key] = item.value.Type == JsonValueType.Int
                                ? (int)item.value
                                : (float)(double)item.value;
                        }
                        else if (float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                        {
                            _floatFields[item.key] = floatVal;
                        }
                        break;
                    }
                    case CloudSaveFieldType.Int:
                    {
                        if (item.value != null && item.value.Type == JsonValueType.Int)
                        {
                            _intFields[item.key] = (int)item.value;
                        }
                        else if (int.TryParse(stringValue, out var intVal))
                        {
                            _intFields[item.key] = intVal;
                        }
                        break;
                    }
                }
            }
        }

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

        public int GetInt(string key, int defaultValue = 0)
        {
            if (_intFields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0)
        {
            if (_floatFields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
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
                    return ((int)value).ToString();
                case JsonValueType.Double:
                    return ((double)value).ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Boolean:
                    return ((bool)value).ToString();
                default:
                    return "";
            }
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Json;

namespace MirraCloud.Core.CloudSave.Requests
{
    /// <summary>
    /// Keys to write in one request. Masks are optional: leave them out and an existing key keeps its masks, while a
    /// new key gets the default for where it lives (your own data: you and the game server read and write it).
    /// The game server (Cloud Code) can always reach data a player writes — the <see cref="AccessMask.Server"/> bit
    /// is added by the backend and cannot be removed from the client.
    /// </summary>
    public class CloudSaveDataRequest
    {
        public List<CloudSaveDataItem> items = new List<CloudSaveDataItem>();

        public CloudSaveDataRequest AddInt(string key, int value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, new JsonValue(value), CloudSaveFieldType.Int, readMask, writeMask, expectedVersion);
        }

        /// <summary>
        /// For values beyond <see cref="int"/>, such as Unix time in milliseconds. Exact up to 2^53.
        /// </summary>
        public CloudSaveDataRequest AddLong(string key, long value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, new JsonValue((double)value), CloudSaveFieldType.Int, readMask, writeMask, expectedVersion);
        }

        public CloudSaveDataRequest AddFloat(string key, float value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            // Widening 0.1f to double gives 0.100000001490116; going through the float's shortest text keeps 0.1.
            double shortest = double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return Add(key, new JsonValue(shortest), CloudSaveFieldType.Float, readMask, writeMask, expectedVersion);
        }

        public CloudSaveDataRequest AddDouble(string key, double value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, new JsonValue(value), CloudSaveFieldType.Float, readMask, writeMask, expectedVersion);
        }

        public CloudSaveDataRequest AddBool(string key, bool value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, new JsonValue(value), CloudSaveFieldType.Boolean, readMask, writeMask, expectedVersion);
        }

        public CloudSaveDataRequest AddString(string key, string value,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, new JsonValue(value), CloudSaveFieldType.String, readMask, writeMask, expectedVersion);
        }

        public CloudSaveDataRequest AddJson(string key, JsonValue value, CloudSaveFieldType fieldType = CloudSaveFieldType.String,
            AccessMask? readMask = null, AccessMask? writeMask = null,
            ulong? expectedVersion = null)
        {
            return Add(key, value, fieldType, readMask, writeMask, expectedVersion);
        }

        private CloudSaveDataRequest Add(string key, JsonValue value, CloudSaveFieldType fieldType,
            AccessMask? readMask, AccessMask? writeMask, ulong? expectedVersion)
        {
            items.Add(new CloudSaveDataItem
            {
                key = key,
                value = value,
                fieldType = fieldType,
                readMask = readMask,
                writeMask = writeMask,
                expectedVersion = expectedVersion
            });
            return this;
        }
    }

    public class CloudSaveDataItem
    {
        public string key;
        public JsonValue value;
        public CloudSaveFieldType fieldType;

        /// <summary>Who may read the key; null keeps the current mask (or the default for a new key).</summary>
        public AccessMask? readMask;

        /// <summary>Who may write the key; null keeps the current mask (or the default for a new key).</summary>
        public AccessMask? writeMask;

        public ulong? expectedVersion;
    }
}

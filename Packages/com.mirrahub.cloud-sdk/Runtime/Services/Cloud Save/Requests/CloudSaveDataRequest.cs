using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.CloudSave.Requests
{
    public class CloudSaveDataRequest
    {
        public List<CloudSaveDataItem> items = new List<CloudSaveDataItem>();

        public CloudSaveDataRequest AddInt(string key, int value,
            AccessMask readMask = AccessMask.Owner, AccessMask writeMask = AccessMask.Owner,
            ulong? expectedVersion = null)
        {
            items.Add(new CloudSaveDataItem
            {
                key = key,
                value = new JsonValue(value),
                fieldType = CloudSaveFieldType.Int,
                readMask = readMask,
                writeMask = writeMask,
                expectedVersion = expectedVersion
            });
            return this;
        }

        public CloudSaveDataRequest AddFloat(string key, float value,
            AccessMask readMask = AccessMask.Owner, AccessMask writeMask = AccessMask.Owner,
            ulong? expectedVersion = null)
        {
            items.Add(new CloudSaveDataItem
            {
                key = key,
                value = new JsonValue(value),
                fieldType = CloudSaveFieldType.Float,
                readMask = readMask,
                writeMask = writeMask,
                expectedVersion = expectedVersion
            });
            return this;
        }

        public CloudSaveDataRequest AddBool(string key, bool value,
            AccessMask readMask = AccessMask.Owner, AccessMask writeMask = AccessMask.Owner,
            ulong? expectedVersion = null)
        {
            items.Add(new CloudSaveDataItem
            {
                key = key,
                value = new JsonValue(value),
                fieldType = CloudSaveFieldType.Boolean,
                readMask = readMask,
                writeMask = writeMask,
                expectedVersion = expectedVersion
            });
            return this;
        }

        public CloudSaveDataRequest AddString(string key, string value,
            AccessMask readMask = AccessMask.Owner, AccessMask writeMask = AccessMask.Owner,
            ulong? expectedVersion = null)
        {
            items.Add(new CloudSaveDataItem
            {
                key = key,
                value = new JsonValue(value),
                fieldType = CloudSaveFieldType.String,
                readMask = readMask,
                writeMask = writeMask,
                expectedVersion = expectedVersion
            });
            return this;
        }

        public CloudSaveDataRequest AddJson(string key, JsonValue value, CloudSaveFieldType fieldType = CloudSaveFieldType.String,
            AccessMask readMask = AccessMask.Owner, AccessMask writeMask = AccessMask.Owner,
            ulong? expectedVersion = null)
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
        public AccessMask readMask;
        public AccessMask writeMask;
        public ulong? expectedVersion;
    }
}

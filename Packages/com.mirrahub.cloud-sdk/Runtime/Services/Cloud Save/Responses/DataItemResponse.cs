using MirraCloud.Json;

namespace MirraCloud.Core.CloudSave.Responses
{
    public class DataItemResponse
    {
        public string key;
        public JsonValue value;
        public CloudSaveFieldType fieldType;
        public AccessMask readMask;
        public AccessMask writeMask;
        public string updatedAtUtc;
        public long version;
    }
}

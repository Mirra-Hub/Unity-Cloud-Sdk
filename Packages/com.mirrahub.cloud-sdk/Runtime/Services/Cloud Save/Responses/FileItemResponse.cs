using System.Collections.Generic;

namespace MirraCloud.Core.CloudSave.Responses
{
    public class FileItemResponse
    {
        public string key;
        public Dictionary<string, string> meta;
        public AccessMask readMask;
        public AccessMask writeMask;
        public long fileSize;
        public string extension;
        public string mimeType;
        public string createdAtUtc;
        public string updatedAtUtc;
    }
}

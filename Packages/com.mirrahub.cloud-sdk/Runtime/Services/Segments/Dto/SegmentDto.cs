using System;

namespace Plugins.MirraCloud.Core.Services.Segments.Dto
{
    [Serializable]
    public class SegmentDto
    {
        public string id;
        public string name;
        public string description;
        public bool isEnable;
        public string ruleTreeId;
        public DateTime createdDate;
        public DateTime updatedDate;
    }
}
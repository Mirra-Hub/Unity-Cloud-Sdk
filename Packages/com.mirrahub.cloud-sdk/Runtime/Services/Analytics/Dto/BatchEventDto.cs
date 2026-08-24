using System;
using System.Collections.Generic;

namespace Plugins.MirraCloud.Core.Services.Analytics.Dto
{
    [Serializable]
    public class BatchEventItemDto
    {
        public string EventName;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public List<string> Tags = new List<string>();
        public string Date;
    }

    [Serializable]
    public class BatchEventDto
    {
        public List<BatchEventItemDto> Events = new List<BatchEventItemDto>();
    }
}

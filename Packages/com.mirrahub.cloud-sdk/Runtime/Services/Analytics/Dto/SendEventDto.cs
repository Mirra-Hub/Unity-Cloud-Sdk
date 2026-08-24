using System;
using System.Collections.Generic;

namespace Plugins.MirraCloud.Core.Services.Analytics.Dto
{
    [Serializable]
    public class SendEventDto
    {
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public List<string> Tags = new List<string>();
    }
}

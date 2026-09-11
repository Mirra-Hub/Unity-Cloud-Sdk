using System;
using System.Collections.Generic;
using MirraCloud.Json;

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

    /// <summary>
    /// Body of a successful <c>events/batch</c> response. The status is 200 even when items were
    /// rejected — they are listed in <see cref="Errors"/> instead.
    /// </summary>
    [Serializable]
    internal sealed class BatchEventResultDto
    {
        [JsonNameCamel] public int Published;
        [JsonNameCamel] public List<BatchEventErrorDto> Errors;
    }

    [Serializable]
    internal sealed class BatchEventErrorDto
    {
        /// <summary>Position of the rejected item in the batch that was sent.</summary>
        [JsonNameCamel] public int Index;
        /// <summary>Cloud error code (see <c>CloudErrorCodes</c>); absent on older servers.</summary>
        [JsonNameCamel] public string Code;
        [JsonNameCamel] public string Error;
    }
}

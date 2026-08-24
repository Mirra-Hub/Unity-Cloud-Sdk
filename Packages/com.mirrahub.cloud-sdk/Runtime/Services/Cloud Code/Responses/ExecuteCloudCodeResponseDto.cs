using System;
using MirraCloud.Json;

namespace MirraCloud.Core.CloudCode.Responses
{
    [Serializable]
    public sealed class ExecuteCloudCodeResponseDto
    {
        [JsonNameCamel]
        public JsonValue Result;
    }
}


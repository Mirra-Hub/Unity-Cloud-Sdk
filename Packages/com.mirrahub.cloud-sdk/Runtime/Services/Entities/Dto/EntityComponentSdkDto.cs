using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Entities.Dto
{
    [Serializable]
    public sealed class EntityComponentSdkDto
    {
        [JsonNameCamel] public string TypeStableId;
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public JsonValue Data;
    }
}

using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Entities.Dto
{
    [Serializable]
    public sealed class EntityConfigSdkDto
    {
        [JsonNameCamel] public string StableId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public JsonValue Fields;
        [JsonNameCamel] public EntityComponentSdkDto[] Components;
    }
}

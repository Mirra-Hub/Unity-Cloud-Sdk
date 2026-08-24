using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Entities.Dto
{
    [Serializable]
    public sealed class EntitiesConfigsSnapshotDto
    {
        [JsonNameCamel] public Dictionary<string, EntityConfigSdkDto> Configs;
    }
}

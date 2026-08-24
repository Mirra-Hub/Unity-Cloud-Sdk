using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Economy.Dto
{
    [Serializable]
    public sealed class EconomyConfigsDto
    {
        [JsonNameCamel] public Dictionary<string, EconomySdkResourceDto> Currencies;
        [JsonNameCamel] public Dictionary<string, EconomySdkResourceDto> Items;
        [JsonNameCamel] public Dictionary<string, EconomySdkResourceDto> Energies;
    }

    [Serializable]
    public sealed class EconomySdkResourceDto
    {
        [JsonNameCamel] public JsonValue Fields;
        [JsonNameCamel] public Dictionary<string, JsonValue> Components;
    }
}

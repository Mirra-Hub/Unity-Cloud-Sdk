using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Localization.Dto
{
    [Serializable]
    public sealed class LocalizationResponseDto
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string StableId;
        [JsonNameCamel] public string GroupId;
        [JsonNameCamel] public string KeyName;
        [JsonNameCamel] public DateTime CreatedDate;
        [JsonNameCamel] public DateTime UpdatedDate;
        [JsonNameCamel] public List<LocalizationValueDto> Values;
    }
}

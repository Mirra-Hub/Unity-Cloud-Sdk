using System;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Localization.Dto
{
    [Serializable]
    public sealed class LocalizationValueDto
    {
        [JsonNameCamel] public LanguageCode LanguageCode;
        [JsonNameCamel] public string Value;
    }
}

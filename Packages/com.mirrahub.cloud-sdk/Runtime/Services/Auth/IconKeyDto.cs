using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class IconKeyDto
    {
        [JsonNameCamel] public KeySource Source;
        [JsonNameCamel] public string Key;
    }
}


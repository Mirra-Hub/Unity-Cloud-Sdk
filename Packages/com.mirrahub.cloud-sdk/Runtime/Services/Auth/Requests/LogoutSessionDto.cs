using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LogoutSessionDto
    {
        [JsonNameCamel] public string SessionId;
    }
}


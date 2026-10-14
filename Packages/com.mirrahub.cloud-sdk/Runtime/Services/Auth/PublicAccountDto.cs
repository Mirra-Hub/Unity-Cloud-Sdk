using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>Another player's account as any player may see it.</summary>
    [Serializable]
    public class PublicAccountDto
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string Nickname;
        [JsonNameCamel] public string Username;
        [JsonNameCamel] public string AvatarUrl;
    }
}

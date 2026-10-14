using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    /// <summary>Another player's profile as any player may see it.</summary>
    [Serializable]
    public class PublicProfileDto
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string Nickname;
        [JsonNameCamel] public string Username;
        [JsonNameCamel] public string IconUrl;
    }
}

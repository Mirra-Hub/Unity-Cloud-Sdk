using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    /// <summary>
    /// Change a profile's username handle. <see cref="Username"/> is the player-controlled body
    /// (without the fixed pr_ prefix, which the server prepends).
    /// </summary>
    [Serializable]
    public class UpdateProfileUsernameDto
    {
        [JsonNameCamel] public string Username;
    }
}

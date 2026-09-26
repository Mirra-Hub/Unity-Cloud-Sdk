using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Body of <c>DELETE …/unlink/platform</c>: the address of the store sign-in to remove from the account. The
    /// platform is part of that address, so a store sign-in can be removed on any platform, including one that has
    /// since been switched off.
    /// </summary>
    [Serializable]
    public class UnlinkPlatformDto
    {
        /// <summary>Key of the platform the store sign-in was made on.</summary>
        [JsonNameCamel] public string PlatformKey;

        /// <summary>The player's id at the store, as the sign-in stored it.</summary>
        [JsonNameCamel] public string ExternalUserId;
    }
}

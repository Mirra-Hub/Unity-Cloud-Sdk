using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>One session of the signed-in player, as <see cref="AuthenticationService.GetSessionsAsync"/> lists them.</summary>
    [Serializable]
    public class PlayerSessionInfo
    {
        [JsonNameCamel] public string SessionId;
        /// <summary>The session this client is signed in with.</summary>
        [JsonNameCamel] public bool IsCurrent;
        [JsonNameCamel] public bool IsActive;
        /// <summary>The device name the OS reports; on iOS often just "iPhone" — show <see cref="DeviceModel"/> then.</summary>
        [JsonNameCamel] public string DeviceName;
        [JsonNameCamel] public string DeviceModel;
        [JsonNameCamel] public ClientDeviceType DeviceType;
        [JsonNameCamel] public ClientPlatform ClientPlatform;
        [JsonNameCamel] public string OperatingSystem;
        [JsonNameCamel] public string AppVersion;
        /// <summary>Key of the project platform the session was opened on.</summary>
        [JsonNameCamel] public string PlatformKey;
        [JsonNameCamel] public DateTime CreatedAt;
        /// <summary>When the session was last opened or refreshed.</summary>
        [JsonNameCamel] public DateTime LastSeenAt;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}

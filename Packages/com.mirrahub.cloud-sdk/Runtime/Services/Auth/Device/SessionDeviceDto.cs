using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// What this client runs on, sent with every sign-in, link, conflict resolution, OpenID start and session
    /// refresh, and shown in the player's session list. No location and no network address.
    /// </summary>
    [Serializable]
    public class SessionDeviceDto
    {
        [JsonNameCamel] public string DeviceName;
        [JsonNameCamel] public string DeviceModel;
        [JsonNameCamel] public ClientDeviceType DeviceType;
        [JsonNameCamel] public ClientPlatform ClientPlatform;
        [JsonNameCamel] public string OperatingSystem;
        [JsonNameCamel] public string AppVersion;
        [JsonNameCamel] public string SdkVersion;
        [JsonNameCamel] public string EngineVersion;
    }
}

using System;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    /// <summary>
    /// Response shape returned by GET .../profiles/{id}/status. The server wraps
    /// the value in a tiny object so callers always get an object body even
    /// when the underlying value is "Offline" — this keeps the JSON readable
    /// and leaves room to add metadata (e.g. "lastSeen") later without a
    /// breaking change.
    /// </summary>
    [Serializable]
    public class GetProfilePresenceStatusDto
    {
        [JsonNameCamel] public ProfilePresenceStatus Status;
    }
}

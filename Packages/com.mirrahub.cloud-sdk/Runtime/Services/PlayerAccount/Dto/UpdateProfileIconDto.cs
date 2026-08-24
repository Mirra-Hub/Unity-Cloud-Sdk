using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class UpdateProfileIconDto
    {
        [JsonName("iconKey")] public string IconKey;
    }
}

using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class UpdateAccountIconDto
    {
        [JsonName("iconKey")] public string IconKey;
    }
}


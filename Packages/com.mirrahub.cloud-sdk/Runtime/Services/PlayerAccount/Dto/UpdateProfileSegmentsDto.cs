using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class UpdateProfileSegmentsDto
    {
        [JsonNameCamel] public string[] SegmentIds;
    }
}


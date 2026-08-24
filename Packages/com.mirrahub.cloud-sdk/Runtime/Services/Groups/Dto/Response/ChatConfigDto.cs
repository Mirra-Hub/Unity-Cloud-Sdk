using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class ChatConfigDto
    {
        [JsonNameCamel] public bool ChatEnabled;
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public bool AutoJoinMembers;
    }
}

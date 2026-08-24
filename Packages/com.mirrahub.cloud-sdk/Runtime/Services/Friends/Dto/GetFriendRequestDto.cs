using System;
using MirraCloud.Core.Friends.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Friends.Dto
{
    [Serializable]
    public class GetFriendRequestDto
    {
        [JsonNameCamel] public string ProjectId;
        [JsonNameCamel] public string SourcePlayerId;
        [JsonNameCamel] public string TargetPlayerId;
        [JsonNameCamel] public FriendRequestStatus Status;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime UpdatedAt;
    }
}


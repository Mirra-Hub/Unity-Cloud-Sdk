using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed class FriendsTopRequestDto
    {
        [JsonNameCamel] public string[] FriendIds;
    }
}


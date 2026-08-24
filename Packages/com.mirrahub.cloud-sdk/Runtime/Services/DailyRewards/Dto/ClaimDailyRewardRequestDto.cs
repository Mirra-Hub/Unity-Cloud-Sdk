using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class ClaimDailyRewardRequestDto
    {
        [MirraCloud.Json.JsonNameCamel] public string CalendarId;
        [MirraCloud.Json.JsonNameCamel] public int? DayNumber;
    }
}

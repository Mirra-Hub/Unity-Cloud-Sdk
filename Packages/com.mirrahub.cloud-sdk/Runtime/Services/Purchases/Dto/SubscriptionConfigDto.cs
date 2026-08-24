using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class SubscriptionConfigDto
    {
        [JsonNameCamel] public int IntervalDays;
        [JsonNameCamel] public int? TrialDays;
        [JsonNameCamel] public int? GracePeriodDays;
    }
}

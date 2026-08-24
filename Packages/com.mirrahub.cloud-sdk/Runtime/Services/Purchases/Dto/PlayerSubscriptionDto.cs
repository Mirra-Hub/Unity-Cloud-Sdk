using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class PlayerSubscriptionDto
    {
        [JsonNameCamel] public string SubscriptionId;
        [JsonNameCamel] public string PurchaseConfigId;
        [JsonNameCamel] public PaymentProviderType Provider;
        [JsonNameCamel] public SubscriptionStatus Status;
        [JsonNameCamel] public DateTime? CurrentPeriodStart;
        [JsonNameCamel] public DateTime? CurrentPeriodEnd;
        [JsonNameCamel] public DateTime? TrialEnd;
        [JsonNameCamel] public DateTime? CancelledAt;
        [JsonNameCamel] public DateTime CreatedAt;
    }
}

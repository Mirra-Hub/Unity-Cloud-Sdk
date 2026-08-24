using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class InitiatePurchaseResponseDto
    {
        [JsonNameCamel] public string OperationId;
        [JsonNameCamel] public string PaymentUrl;
        [JsonNameCamel] public string SuccessRedirectUrl;
        [JsonNameCamel] public string CancelRedirectUrl;
        [JsonNameCamel] public bool IsSubscription;
    }
}

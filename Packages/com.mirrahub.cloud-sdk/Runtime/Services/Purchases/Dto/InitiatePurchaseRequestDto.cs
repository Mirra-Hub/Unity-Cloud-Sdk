using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class InitiatePurchaseRequestDto
    {
        [JsonNameCamel] public string PurchaseKey;
        [JsonNameCamel] public string ProviderConfigId;
        [JsonNameCamel] public string SuccessRedirectUrl;
        [JsonNameCamel] public string CancelRedirectUrl;
    }
}

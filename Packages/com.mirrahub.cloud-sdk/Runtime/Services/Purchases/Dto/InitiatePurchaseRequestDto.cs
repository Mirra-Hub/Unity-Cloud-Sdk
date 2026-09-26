using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class InitiatePurchaseRequestDto
    {
        /// <summary><see cref="CatalogItemDto.Key"/>.</summary>
        [JsonNameCamel] public string PurchaseKey;

        /// <summary><see cref="CatalogPriceDto.IntegrationKey"/> — the payment integration to pay through.</summary>
        [JsonNameCamel] public string IntegrationKey;

        [JsonNameCamel] public string SuccessRedirectUrl;
        [JsonNameCamel] public string CancelRedirectUrl;
    }
}

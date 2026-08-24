using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class CatalogPriceDto
    {
        [JsonNameCamel] public string MappingId;
        [JsonNameCamel] public string ProviderConfigId;
        [JsonNameCamel] public PaymentProviderType ProviderType;
        [JsonNameCamel] public string ProviderName;
        [JsonNameCamel] public decimal Amount;
        [JsonNameCamel] public PurchaseCurrency Currency;
    }
}

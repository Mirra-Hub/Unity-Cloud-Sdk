using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class CatalogItemDto
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public PurchaseType Type;
        [JsonNameCamel] public string DisplayName;
        [JsonNameCamel] public string Description;
        [JsonNameCamel] public Dictionary<string, string> Metadata;
        [JsonNameCamel] public List<RewardDataDto> Rewards;
        [JsonNameCamel] public SubscriptionConfigDto SubscriptionConfig;
        [JsonNameCamel] public List<CatalogPriceDto> Prices;
    }
}

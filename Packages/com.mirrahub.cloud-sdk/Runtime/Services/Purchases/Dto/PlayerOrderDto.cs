using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class PlayerOrderDto
    {
        [JsonNameCamel] public string OrderId;
        [JsonNameCamel] public string PurchaseConfigId;
        [JsonNameCamel] public PaymentProviderType Provider;
        [JsonNameCamel] public OrderStatus Status;
        [JsonNameCamel] public decimal Amount;
        [JsonNameCamel] public PurchaseCurrency Currency;
        [JsonNameCamel] public bool RewardsGranted;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime UpdatedAt;
        [JsonNameCamel] public DateTime? CompletedAt;
    }
}

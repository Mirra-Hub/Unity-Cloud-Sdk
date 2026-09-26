using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    /// <summary>One way to buy a catalog item: its price at one payment integration of the project.</summary>
    [Serializable]
    public sealed class CatalogPriceDto
    {
        /// <summary>
        /// Stable key of this price, <c>{purchaseKey}@{integrationKey}</c>. Informational: the same value the order
        /// records, never sent back by the SDK.
        /// </summary>
        [JsonNameCamel] public string MappingId;

        /// <summary>
        /// Key of the payment integration (Integrations in the console). Pass it to
        /// <see cref="PurchasesService.InitiatePurchaseAsync"/> / <see cref="PurchasesService.BuyAsync"/> to pay
        /// through this price.
        /// </summary>
        [JsonNameCamel] public string IntegrationKey;

        /// <summary>Vendor of the integration.</summary>
        [JsonNameCamel] public PaymentProviderType ProviderType;

        /// <summary>Display name of the integration, as set in the console.</summary>
        [JsonNameCamel] public string ProviderName;

        [JsonNameCamel] public decimal Amount;
        [JsonNameCamel] public PurchaseCurrency Currency;
    }
}

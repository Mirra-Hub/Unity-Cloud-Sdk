using System;
using System.Collections.Generic;
using MirraCloud.Core.Attribution.Dto;
using MirraCloud.Core.Purchases;
using MirraCloud.Core.Purchases.Dto;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Attribution.Tests
{
    /// <summary>
    /// The wire shapes of the attribution routes and of the purchase catalog, read and written the way the runtime does
    /// it. Member names are matched case-sensitively, so a camelCase field the DTO spells differently silently stays at
    /// its default — a catalog price without its integration key, which no purchase can then be started with.
    /// </summary>
    [TestFixture]
    public class AttributionDtoMappingTests
    {
        /// <summary><c>GET …/external/v1/projects/{projectId}/me</c>, as GameBackend writes it.</summary>
        private const string ExternalIdsPayload =
            "[{\"providerKey\":\"adjust\",\"externalId\":\"3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b\",\"source\":\"sdk\","
            + "\"firstSeenAt\":\"2026-09-21T10:00:00Z\",\"lastSeenAt\":\"2026-09-23T08:12:40.123456Z\","
            + "\"adjust\":{\"trackerToken\":\"abc123\",\"trackerName\":\"Facebook Installs::Summer::Group A::Banner 1\","
            + "\"network\":\"Facebook Installs\",\"campaign\":\"Summer\",\"adgroup\":\"Group A\",\"creative\":\"Banner 1\","
            + "\"clickLabel\":null}},"
            + "{\"providerKey\":\"someday\",\"externalId\":\"x\",\"source\":\"callback\","
            + "\"firstSeenAt\":\"2026-09-20T10:00:00Z\",\"lastSeenAt\":\"2026-09-20T10:00:00Z\",\"adjust\":null}]";

        /// <summary><c>GET …/purchases/v1/projects/{projectId}/branches/{branch}/catalog</c>, one product, one price.</summary>
        private const string CatalogPayload =
            "[{\"id\":\"66f0c0ffee00000000000001\",\"key\":\"gems\",\"type\":\"Consumable\",\"displayName\":\"Gems\","
            + "\"description\":\"\",\"metadata\":{},"
            + "\"rewards\":[{\"rewardId\":\"gem\",\"economyResourceKind\":\"Currency\",\"count\":100}],"
            + "\"subscriptionConfig\":null,"
            + "\"prices\":[{\"mappingId\":\"gems@stripe-eu\",\"integrationKey\":\"stripe-eu\",\"providerType\":\"Stripe\","
            + "\"providerName\":\"Stripe EU\",\"amount\":4.99,\"currency\":\"EUR\"}]}]";

        [Test]
        public void Reads_the_recorded_external_ids()
        {
            var ids = JsonMapper.FromJson<List<ExternalIdDto>>(ExternalIdsPayload);

            Assert.That(ids, Has.Count.EqualTo(2));
            var adjust = ids[0];
            Assert.That(adjust.ProviderKey, Is.EqualTo("adjust"));
            Assert.That(adjust.ExternalId, Is.EqualTo("3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b"));
            Assert.That(adjust.Source, Is.EqualTo("sdk"));
            Assert.That(adjust.Adjust, Is.Not.Null);
            Assert.That(adjust.Adjust.TrackerToken, Is.EqualTo("abc123"));
            Assert.That(adjust.Adjust.Network, Is.EqualTo("Facebook Installs"));
            Assert.That(adjust.Adjust.Campaign, Is.EqualTo("Summer"));
            Assert.That(adjust.Adjust.Adgroup, Is.EqualTo("Group A"));
            Assert.That(adjust.Adjust.Creative, Is.EqualTo("Banner 1"));
            Assert.That(adjust.Adjust.ClickLabel, Is.Null);
        }

        /// <summary>YDB keeps microseconds, and the dates are UTC — not the device's local time.</summary>
        [Test]
        public void Reads_the_seen_dates_as_utc()
        {
            var ids = JsonMapper.FromJson<List<ExternalIdDto>>(ExternalIdsPayload);

            Assert.That(ids[0].FirstSeenAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(ids[0].FirstSeenAt, Is.EqualTo(new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc)));
            Assert.That(ids[0].LastSeenAt.ToUniversalTime(),
                Is.EqualTo(new DateTime(2026, 9, 23, 8, 12, 40, DateTimeKind.Utc).AddTicks(1234560)));
        }

        [Test]
        public void A_provider_without_details_reads_with_no_adjust_block()
        {
            var ids = JsonMapper.FromJson<List<ExternalIdDto>>(ExternalIdsPayload);

            Assert.That(ids[1].ProviderKey, Is.EqualTo("someday"));
            Assert.That(ids[1].Adjust, Is.Null);
        }

        /// <summary>
        /// The writer uses the member names as they are (the backend binds request bodies case-insensitively), so what
        /// is pinned is the names themselves and that the helper property stays out of the body.
        /// </summary>
        [Test]
        public void Writes_the_report_under_adjusts_field_names_without_helper_members()
        {
            var json = JsonMapper.ToJson(new AdjustAttributionDto
            {
                Adid = "3e4f",
                TrackerToken = "abc123",
                Campaign = "Summer",
                ClickLabel = "cl",
            });

            Assert.That(json, Does.Contain("\"adid\":\"3e4f\"").IgnoreCase);
            Assert.That(json, Does.Contain("\"trackerToken\":\"abc123\"").IgnoreCase);
            Assert.That(json, Does.Contain("\"campaign\":\"Summer\"").IgnoreCase);
            Assert.That(json, Does.Contain("\"clickLabel\":\"cl\"").IgnoreCase);
            Assert.That(json, Does.Not.Contain("hasAttribution").IgnoreCase);
        }

        [Test]
        public void Only_the_attribution_fields_count_as_attribution()
        {
            Assert.That(new AdjustAttributionDto { Adid = "x" }.HasAttribution, Is.False);
            Assert.That(new AdjustAttributionDto { Network = "  " }.HasAttribution, Is.False);
            Assert.That(new AdjustAttributionDto { ClickLabel = "c" }.HasAttribution, Is.True);
        }

        [Test]
        public void Reads_a_catalog_price_by_its_integration_key()
        {
            var catalog = JsonMapper.FromJson<List<CatalogItemDto>>(CatalogPayload);

            var price = catalog[0].Prices[0];
            Assert.That(price.IntegrationKey, Is.EqualTo("stripe-eu"));
            Assert.That(price.MappingId, Is.EqualTo("gems@stripe-eu"));
            Assert.That(price.ProviderType, Is.EqualTo(PaymentProviderType.Stripe));
            Assert.That(price.ProviderName, Is.EqualTo("Stripe EU"));
            Assert.That(price.Amount, Is.EqualTo(4.99m));
            Assert.That(price.Currency, Is.EqualTo(PurchaseCurrency.EUR));
        }

        [Test]
        public void Writes_the_purchase_request_with_the_integration_key()
        {
            var json = JsonMapper.ToJson(new InitiatePurchaseRequestDto
            {
                PurchaseKey = "gems",
                IntegrationKey = "stripe-eu",
                SuccessRedirectUrl = "https://a",
                CancelRedirectUrl = "https://b",
            });

            Assert.That(json, Does.Contain("\"purchaseKey\":\"gems\"").IgnoreCase);
            Assert.That(json, Does.Contain("\"integrationKey\":\"stripe-eu\"").IgnoreCase);
            Assert.That(json, Does.Not.Contain("providerConfigId").IgnoreCase);
        }
    }
}

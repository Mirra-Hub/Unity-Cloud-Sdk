using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Purchases;
using MirraCloud.Core.Purchases.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Purchases screen: the store as the project defines it (catalog), what this player already
    /// bought (orders, subscriptions), and the one write the example is willing to run.
    /// <para>
    /// <c>BuyAsync</c> is behind no button here. It opens a payment page and settles a real payment,
    /// which is the wrong thing for a demo to do on a curious reader's card — the Actions tab shows
    /// its code and explains the flow instead, and stops at <c>InitiatePurchaseAsync</c>, which only
    /// creates a Pending order.
    /// </para>
    /// </summary>
    public sealed class PurchasesView : ServiceView
    {
        private const int CatalogTab = 0;
        private const int OrdersTab = 1;
        private const int SubscriptionsTab = 2;
        private const int ActionsTab = 3;

        // Sentinel redirects: the provider sends the player back to them, the backend ignores them.
        private const string DefaultSuccessUrl = "https://example.com/purchase/success";
        private const string DefaultCancelUrl = "https://example.com/purchase/cancel";

        private const string CatalogSnippet =
@"// The store as the project defines it: every product, the price under each payment provider,
// and what the product grants once an order settles.
var op = sdk.Purchases.LoadCatalogAsync();
await op.Task();

if (op.Result.IsSuccess)
{
    foreach (CatalogItemDto item in op.Result.Data)
    {
        // item.Key / item.Id / item.DisplayName / item.Description / item.Metadata
        // item.Type:    Consumable | NonConsumable | Subscription
        // item.Prices:  ProviderConfigId, ProviderName, ProviderType, Amount, Currency
        // item.Rewards: RewardId, EconomyResourceKind, Count
        // item.SubscriptionConfig: IntervalDays, TrialDays, GracePeriodDays
    }
}";

        private const string OrdersSnippet =
@"// Every order this player has, in whatever order the backend returns them — sort client-side.
var op = sdk.Purchases.GetOrdersAsync();
await op.Task();

foreach (PlayerOrderDto order in op.Result.Data)
{
    // order.OrderId, order.PurchaseConfigId (matches CatalogItemDto.Id),
    // order.Provider, order.Status, order.Amount, order.Currency,
    // order.RewardsGranted, order.CreatedAt, order.UpdatedAt, order.CompletedAt
}";

        private const string OrderSnippet =
@"// One order by id. This is also the polling call: after the payment page closes the order stays
// Pending until the provider's webhook reaches the backend.
var op = sdk.Purchases.GetOrderAsync(orderId);
await op.Task();

PlayerOrderDto order = op.Result.Data;
bool settled = order.Status == OrderStatus.RewardsGranted;";

        private const string SubscriptionsSnippet =
@"// Subscriptions are tracked apart from orders: one row each, carrying the period the provider
// is currently billing.
var op = sdk.Purchases.GetSubscriptionsAsync();
await op.Task();

foreach (PlayerSubscriptionDto sub in op.Result.Data)
{
    // sub.SubscriptionId, sub.PurchaseConfigId, sub.Provider,
    // sub.Status: Active | Trialing | PastDue | Cancelled | Expired
    // sub.CurrentPeriodStart / CurrentPeriodEnd, sub.TrialEnd, sub.CancelledAt
}";

        private const string InitiateSnippet =
@"// Step one of a purchase, and the only write this screen runs: it creates a Pending order and
// returns the provider's payment page. Nothing is charged yet.
var op = sdk.Purchases.InitiatePurchaseAsync(
    purchaseKey: ""starter_pack"",           // CatalogItemDto.Key
    providerConfigId: providerConfigId,    // CatalogPriceDto.ProviderConfigId
    successRedirectUrl: ""https://example.com/purchase/success"",
    cancelRedirectUrl: ""https://example.com/purchase/cancel"");
await op.Task();

InitiatePurchaseResponseDto started = op.Result.Data;
// started.OperationId — order id, or subscription id when started.IsSubscription
// started.PaymentUrl  — open this in a browser or a WebView
// The redirects are only where the provider sends the player back. The backend settles the order
// from the provider's webhook, so poll GetOrderAsync rather than trusting the redirect.";

        private const string BuySnippet =
@"// The one-call flow — NOT executed anywhere in this example, because it settles a real payment.
// It initiates the order, opens the payment page in the SDK WebView, waits for the redirect, then
// polls the order until the provider's webhook has settled it.
var op = sdk.Purchases.BuyAsync(""starter_pack"", providerConfigId, new PurchaseOptions
{
    SuccessRedirectUrl = ""https://example.com/purchase/success"",
    CancelRedirectUrl = ""https://example.com/purchase/cancel"",
    StatusPollTimeout = TimeSpan.FromSeconds(30),
    StatusPollInterval = TimeSpan.FromSeconds(2),
});
await op.Task();

PurchaseResult result = op.Result;
switch (result.Status)
{
    case PurchaseResultStatus.Completed: break;             // result.Order, rewards granted
    case PurchaseResultStatus.SubscriptionActivated: break;  // result.Subscription
    case PurchaseResultStatus.Pending: break;                // webhook late — poll GetOrderAsync
    case PurchaseResultStatus.Cancelled: break;              // the player backed out
    case PurchaseResultStatus.Failed: break;                 // result.Error
}

// It needs a WebView that can intercept URLs, so it fails outright on WebGL. The same outcomes
// are raised as events too, for code that is not awaiting the call:
sdk.Purchases.OnPurchaseCompleted += order => { };
sdk.Purchases.OnPurchaseCancelled += operationId => { };
sdk.Purchases.OnPurchaseFailed += failure => { };";

        private const string BuyExcerpt =
@"// Real money. Shown here, never called by this example.
var op = sdk.Purchases.BuyAsync(""starter_pack"", providerConfigId);
await op.Task();

PurchaseResult result = op.Result;
// Completed | SubscriptionActivated | Pending | Cancelled | Failed";

        private static readonly string[] TypeFilters =
        {
            "Any type", "Consumable", "Non-consumable", "Subscription",
        };

        private static readonly OrderStatus[] StatusOrder =
        {
            OrderStatus.Pending, OrderStatus.Paid, OrderStatus.RewardsGranted,
            OrderStatus.Cancelled, OrderStatus.Refunded, OrderStatus.Failed,
        };

        private Tabs _tabs;
        private string _search = string.Empty;
        private PurchaseType? _typeFilter;
        private string _prefillKey;
        private string _prefillProvider;

        // Kept so an order can be labelled with its product name instead of a raw config id. The
        // catalog tab is built first, but its load is async — every reader of this falls back.
        private List<CatalogItemDto> _catalog = new List<CatalogItemDto>();

        public PurchasesView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _search = string.Empty;
            _typeFilter = null;

            DeclareCall(new SdkCall("Read the store catalog", CatalogSnippet));
            DeclareCall(new SdkCall("Read the player's orders", OrdersSnippet));
            DeclareCall(new SdkCall("Read one order", OrderSnippet,
                "Also the call to poll while the provider's webhook is in flight."));
            DeclareCall(new SdkCall("Read the player's subscriptions", SubscriptionsSnippet));
            DeclareCall(new SdkCall("Start an order", InitiateSnippet,
                "Creates a Pending order and returns a payment URL — no money moves here."));
            DeclareCall(new SdkCall("Buy in one call (never run here)", BuySnippet,
                "Real money: this example shows the code and refuses to execute it."));

            UseToolbar()
                .WithSearch("Filter the catalog by name or key", OnSearch)
                .WithFilter("Type", TypeFilters, OnTypeFilter, TypeFilters[0])
                .WithSpacer()
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Catalog", LucideIcon.ShoppingCart, BuildCatalog)
                .Add("Orders", LucideIcon.ScrollText, BuildOrders)
                .Add("Subscriptions", LucideIcon.CalendarClock, BuildSubscriptions)
                .Add("Actions", LucideIcon.Sparkles, BuildActions);
        }

        // Both toolbar controls narrow the catalog only, so they also bring that tab forward —
        // typing into a search box that changes a pane you cannot see reads as a broken control.
        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            _tabs.Invalidate(CatalogTab);
            _tabs.Select(CatalogTab);
        }

        private void OnTypeFilter(string value)
        {
            if (string.Equals(value, TypeFilters[1], StringComparison.Ordinal))
            {
                _typeFilter = PurchaseType.Consumable;
            }
            else if (string.Equals(value, TypeFilters[2], StringComparison.Ordinal))
            {
                _typeFilter = PurchaseType.NonConsumable;
            }
            else if (string.Equals(value, TypeFilters[3], StringComparison.Ordinal))
            {
                _typeFilter = PurchaseType.Subscription;
            }
            else
            {
                _typeFilter = null;
            }
            _tabs.Invalidate(CatalogTab);
            _tabs.Select(CatalogTab);
        }

        // ----- catalog --------------------------------------------------------------------------

        private VisualElement BuildCatalog()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Purchases.LoadCatalogAsync(),
                slot,
                BuildCatalogBody,
                d => d == null || d.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Store catalog",
                    Snippet = CatalogSnippet,
                    ServiceName = "Purchases",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        _catalog = new List<CatalogItemDto>();
                        SetStatus("No products", ChipTone.Warn);
                        return ZeroState.Cards(LucideIcon.ShoppingCart,
                            "Products, their prices and the payment providers behind them are authored in "
                            + "the Mirra Hub console. Once one exists there it shows up here, and its key "
                            + "plus one provider config id are everything a game needs to start an order.",
                            4, "See how a purchase works", () => _tabs.Select(ActionsTab));
                    },
                });
            return slot;
        }

        private VisualElement BuildCatalogBody(List<CatalogItemDto> items)
        {
            _catalog = items;
            SetStatus(items.Count + (items.Count == 1 ? " product" : " products"), ChipTone.Ok);

            var col = new VisualElement();
            col.Add(CatalogKpis(items));
            col.Add(TypeChart(items));

            var shown = FilterCatalog(items);
            col.Add(new SectionHeader("Products",
                shown.Count == items.Count
                    ? items.Count.ToString()
                    : shown.Count + " of " + items.Count));

            if (shown.Count == 0)
            {
                // No CTA here on purpose: the toolbar owns both filters and exposes no way to reset
                // them from code, so a button claiming to clear them would leave the widgets behind.
                col.Add(ZeroState.Panel(LucideIcon.Search, "Nothing matches those filters", FilterMiss(),
                    hint: "Clear the search box, or set the type filter back to \"" + TypeFilters[0]
                        + "\", to see the whole catalog."));
                return col;
            }

            var grid = new VisualElement();
            grid.AddToClassList("sc-pur-grid");
            foreach (var item in shown)
            {
                grid.Add(ProductCard(item));
            }
            col.Add(grid);
            return col;
        }

        private string FilterMiss()
        {
            if (_search.Length > 0 && _typeFilter.HasValue)
            {
                return "No " + TypeName(_typeFilter.Value).ToLowerInvariant() + " product's name or key "
                    + "contains \"" + Fmt.Truncate(_search, 24) + "\".";
            }
            if (_search.Length > 0)
            {
                return "No product's name or key contains \"" + Fmt.Truncate(_search, 24) + "\".";
            }
            return "This project defines no " + TypeName(_typeFilter.Value).ToLowerInvariant()
                + " product.";
        }

        private VisualElement CatalogKpis(List<CatalogItemDto> items)
        {
            var types = new HashSet<PurchaseType>();
            var prices = new MoneyBucket();
            int rewarding = 0;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                types.Add(item.Type);
                if (item.Rewards != null && item.Rewards.Count > 0)
                {
                    rewarding++;
                }
                if (item.Prices == null)
                {
                    continue;
                }
                foreach (var price in item.Prices)
                {
                    if (price != null)
                    {
                        prices.Add(price.Currency, price.Amount);
                    }
                }
            }

            var kpis = new KpiRow()
                .Add("Products", LucideIcon.ShoppingCart, items.Count.ToString())
                .Add("Types", LucideIcon.Layers, types.Count.ToString());

            if (prices.TryDominant(out string currency, out decimal total, out int count))
            {
                kpis.Add("Average price", LucideIcon.Coins, Fmt.Money(total / count, currency),
                    "over " + count + (count == 1 ? " price" : " prices") + " in " + currency);
            }
            else
            {
                kpis.AddZero("Average price", LucideIcon.Coins, Fmt.Dash);
            }

            kpis.Add("Grant rewards", LucideIcon.Gift, rewarding.ToString(), null, rewarding > 0);
            return kpis;
        }

        private VisualElement TypeChart(List<CatalogItemDto> items)
        {
            int consumable = 0;
            int nonConsumable = 0;
            int subscription = 0;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                switch (item.Type)
                {
                    case PurchaseType.Consumable:
                        consumable++;
                        break;
                    case PurchaseType.NonConsumable:
                        nonConsumable++;
                        break;
                    case PurchaseType.Subscription:
                        subscription++;
                        break;
                }
            }

            var donut = new DonutChart(150f);
            donut.SetData(new[]
                {
                    new ChartPoint(TypeName(PurchaseType.Consumable), consumable),
                    new ChartPoint(TypeName(PurchaseType.NonConsumable), nonConsumable),
                    new ChartPoint(TypeName(PurchaseType.Subscription), subscription),
                })
                .SetCenter(items.Count.ToString(), items.Count == 1 ? "product" : "products")
                .SetEmptyText("Nothing in the catalog");
            return donut;
        }

        private List<CatalogItemDto> FilterCatalog(List<CatalogItemDto> items)
        {
            if (_search.Length == 0 && !_typeFilter.HasValue)
            {
                return items;
            }

            var hits = new List<CatalogItemDto>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                if (_typeFilter.HasValue && item.Type != _typeFilter.Value)
                {
                    continue;
                }
                if (_search.Length > 0 && !Contains(item.DisplayName) && !Contains(item.Key))
                {
                    continue;
                }
                hits.Add(item);
            }
            return hits;
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement ProductCard(CatalogItemDto item)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-pur-card");
            if (item.Type == PurchaseType.Subscription)
            {
                card.AddToClassList("sc-pur-card--sub");
            }

            var head = new VisualElement();
            head.AddToClassList("sc-pur-card__head");

            var icon = new VisualElement();
            icon.AddToClassList("sc-pur-card__icon");
            var glyph = new Label(TypeGlyph(item.Type));
            glyph.AddToClassList("sc-pur-card__glyph");
            glyph.AddToClassList("sc-icon");
            icon.Add(glyph);
            head.Add(icon);

            var texts = new VisualElement();
            texts.AddToClassList("sc-pur-card__texts");

            var name = new Label(DisplayNameOf(item));
            name.enableRichText = false;
            name.AddToClassList("sc-pur-card__name");
            texts.Add(name);

            var key = new Label(Fmt.OrDash(item.Key));
            key.enableRichText = false;
            key.AddToClassList("sc-pur-card__key");
            texts.Add(key);
            head.Add(texts);

            head.Add(new Badge(TypeName(item.Type), TypeTone(item.Type)));
            card.Add(head);

            var price = PrimaryPrice(item);
            var priceLabel = new Label(price == null
                ? "no price"
                : Fmt.Money(price.Amount, price.Currency.ToString()));
            priceLabel.AddToClassList("sc-pur-card__price");
            if (price == null)
            {
                priceLabel.AddToClassList("sc-pur-card__price--none");
            }
            card.Add(priceLabel);

            var priceSub = new Label(PriceCaption(item, price));
            priceSub.enableRichText = false;
            priceSub.AddToClassList("sc-pur-card__price-sub");
            card.Add(priceSub);

            if (!string.IsNullOrEmpty(item.Description))
            {
                var description = new Label(Fmt.Truncate(item.Description, 110));
                description.enableRichText = false;
                description.AddToClassList("sc-pur-card__desc");
                card.Add(description);
            }

            var facts = SubscriptionChips(item);
            if (facts != null)
            {
                card.Add(facts);
            }

            if (item.Rewards != null && item.Rewards.Count > 0)
            {
                var rewards = new VisualElement();
                rewards.AddToClassList("sc-chip-row");
                foreach (var reward in item.Rewards)
                {
                    if (reward != null)
                    {
                        rewards.Add(new RewardChip(RewardGlyph(reward.EconomyResourceKind),
                            RewardText(reward), Meta.Accent));
                    }
                }
                card.Add(rewards);
            }
            else
            {
                var note = new Label("grants nothing by itself");
                note.AddToClassList("sc-pur-card__note");
                card.Add(note);
            }

            var foot = new VisualElement();
            foot.AddToClassList("sc-row-actions");
            foot.AddToClassList("sc-pur-card__foot");

            var details = new Button(() => ShowProduct(item)) { text = "Details" };
            details.AddToClassList("sc-btn");
            foot.Add(details);

            // Without a provider mapping there is no providerConfigId to pass, so the shortcut is
            // absent rather than present and guaranteed to fail.
            if (price != null && !string.IsNullOrEmpty(price.ProviderConfigId))
            {
                var start = new Button(() => Prefill(item.Key, price.ProviderConfigId))
                {
                    text = "Start an order",
                };
                start.AddToClassList("sc-btn");
                start.AddToClassList("sc-btn--primary");
                foot.Add(start);
            }
            card.Add(foot);
            return card;
        }

        private static VisualElement SubscriptionChips(CatalogItemDto item)
        {
            var config = item.SubscriptionConfig;
            if (config == null)
            {
                return null;
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip("renews every " + config.IntervalDays + "d", ChipTone.Info));
            if (config.TrialDays.HasValue && config.TrialDays.Value > 0)
            {
                chips.Add(new Chip(config.TrialDays.Value + "d trial", ChipTone.Ok));
            }
            if (config.GracePeriodDays.HasValue && config.GracePeriodDays.Value > 0)
            {
                chips.Add(new Chip(config.GracePeriodDays.Value + "d grace", ChipTone.Neutral));
            }
            return chips;
        }

        private static string PriceCaption(CatalogItemDto item, CatalogPriceDto price)
        {
            if (price == null)
            {
                return "no provider mapping — a game cannot start an order for this product yet";
            }

            int others = (item.Prices != null ? item.Prices.Count : 1) - 1;
            string provider = ProviderLabel(price);
            return others > 0
                ? provider + " · " + others + (others == 1 ? " more provider" : " more providers")
                : provider;
        }

        private void ShowProduct(CatalogItemDto item)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var head = new VisualElement();
            head.AddToClassList("sc-chip-row");
            head.Add(new Chip(TypeName(item.Type), TypeTone(item.Type)));
            if (item.Rewards != null && item.Rewards.Count > 0)
            {
                head.Add(new Badge(item.Rewards.Count + " rewards", ChipTone.Accent));
            }
            body.Add(head);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Purchase key", Fmt.OrDash(item.Key), item.Key));
            kv.Add(Kv("Product id", Fmt.OrDash(item.Id), item.Id));
            if (!string.IsNullOrEmpty(item.Description))
            {
                kv.Add(Kv("Description", item.Description, null));
            }
            body.Add(kv);

            var config = item.SubscriptionConfig;
            if (config != null)
            {
                body.Add(new SectionHeader("Subscription"));
                var subs = new VisualElement();
                subs.AddToClassList("sc-kv-list");
                subs.Add(Kv("Billing interval", config.IntervalDays + " days", null));
                subs.Add(Kv("Trial", config.TrialDays.HasValue
                    ? config.TrialDays.Value + " days"
                    : "none", null));
                subs.Add(Kv("Grace period", config.GracePeriodDays.HasValue
                    ? config.GracePeriodDays.Value + " days"
                    : "none", null));
                body.Add(subs);
            }

            body.Add(new SectionHeader("Prices",
                item.Prices != null ? item.Prices.Count.ToString() : "0"));
            body.Add(PriceList(item));

            body.Add(new SectionHeader("Rewards",
                item.Rewards != null ? item.Rewards.Count.ToString() : "0"));
            body.Add(RewardList(item));

            body.Add(new SectionHeader("Metadata",
                item.Metadata != null ? item.Metadata.Count.ToString() : "0"));
            if (item.Metadata == null || item.Metadata.Count == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Braces, "No metadata",
                    "Metadata is a free-form string map on the product — a storefront banner, a sort "
                    + "weight, anything the game wants to read alongside the price."));
            }
            else
            {
                var meta = new VisualElement();
                meta.AddToClassList("sc-kv-list");
                foreach (var pair in item.Metadata)
                {
                    meta.Add(Kv(pair.Key, Fmt.OrDash(pair.Value), pair.Value));
                }
                body.Add(meta);
            }

            Popup.Open(body, Fmt.Truncate(DisplayNameOf(item), 34));
        }

        // A dialog is 420px wide, so the prices are rows rather than a table: five columns would
        // either overflow it or shrink the provider name to nothing.
        private VisualElement PriceList(CatalogItemDto item)
        {
            if (item.Prices == null || item.Prices.Count == 0)
            {
                return ZeroState.Panel(LucideIcon.Coins, "No price mapping",
                    "A product needs at least one provider mapping before it can be sold: the mapping "
                    + "is what carries the amount, the currency and the provider config id that "
                    + "InitiatePurchaseAsync takes.");
            }

            var list = new VisualElement();
            foreach (var price in item.Prices)
            {
                if (price == null)
                {
                    continue;
                }

                var row = new ListRow();
                row.SetTitle(ProviderLabel(price));
                row.SetSubtitle("config " + Fmt.Id(price.ProviderConfigId, 12));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-row-actions");
                trailing.Add(new Chip(Fmt.Money(price.Amount, price.Currency.ToString()),
                    ChipTone.Accent));

                if (!string.IsNullOrEmpty(price.ProviderConfigId))
                {
                    string providerConfigId = price.ProviderConfigId;
                    var use = new Button(() => Prefill(item.Key, providerConfigId)) { text = "Use" };
                    use.AddToClassList("sc-btn");
                    use.AddToClassList("sc-btn--primary");
                    trailing.Add(use);
                }

                row.SetTrailing(trailing);
                list.Add(row);
            }
            return list;
        }

        private VisualElement RewardList(CatalogItemDto item)
        {
            if (item.Rewards == null || item.Rewards.Count == 0)
            {
                return ZeroState.Panel(LucideIcon.Gift, "Grants nothing",
                    "This product has no economy rewards attached, so paying for it only records the "
                    + "order. Attach currencies, items or energy to it in the Mirra Hub console and the "
                    + "backend grants them when the order settles.");
            }

            var list = new VisualElement();
            foreach (var reward in item.Rewards)
            {
                if (reward == null)
                {
                    continue;
                }

                var glyph = new Label(RewardGlyph(reward.EconomyResourceKind));
                glyph.AddToClassList("sc-icon");
                glyph.AddToClassList("sc-pur-lead-glyph");

                var row = new ListRow();
                row.SetLead(glyph);
                row.SetTitle(Fmt.OrDash(reward.RewardId));
                row.SetSubtitle(RewardKindName(reward.EconomyResourceKind));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-row-actions");
                trailing.Add(new Badge("×" + Fmt.Number(reward.Count), ChipTone.Ok));
                row.SetTrailing(trailing);
                list.Add(row);
            }
            return list;
        }

        // ----- orders ---------------------------------------------------------------------------

        private VisualElement BuildOrders()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Purchases.GetOrdersAsync(),
                slot,
                BuildOrdersBody,
                d => d == null || d.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Orders",
                    Snippet = OrdersSnippet,
                    ServiceName = "Purchases",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(OrderColumns(),
                        "This player has bought nothing yet. An order appears the moment a purchase "
                        + "starts: InitiatePurchaseAsync creates it as Pending, and the provider's "
                        + "webhook moves it to Paid and then to RewardsGranted.",
                        3, "Start an order", () => _tabs.Select(ActionsTab)),
                });
            return slot;
        }

        private VisualElement BuildOrdersBody(List<PlayerOrderDto> orders)
        {
            var col = new VisualElement();

            var counts = new Dictionary<OrderStatus, int>();
            var spend = new MoneyBucket();
            int settled = 0;
            int inFlight = 0;
            foreach (var order in orders)
            {
                if (order == null)
                {
                    continue;
                }
                counts.TryGetValue(order.Status, out int seen);
                counts[order.Status] = seen + 1;

                if (order.Status == OrderStatus.RewardsGranted)
                {
                    settled++;
                }
                if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Paid)
                {
                    inFlight++;
                }
                // Only money the provider actually took counts as spent; a cancelled or failed order
                // still carries its amount.
                if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.RewardsGranted)
                {
                    spend.Add(order.Currency, order.Amount);
                }
            }

            var kpis = new KpiRow()
                .Add("Orders", LucideIcon.ScrollText, orders.Count.ToString())
                .Add("Settled", LucideIcon.CircleCheck, settled.ToString(), null, settled > 0)
                .Add("In flight", LucideIcon.Hourglass, inFlight.ToString(), null, inFlight > 0);

            if (spend.TryDominant(out string currency, out decimal total, out int paid))
            {
                kpis.Add("Paid", LucideIcon.Wallet, Fmt.Money(total, currency),
                    "over " + paid + (paid == 1 ? " order" : " orders") + " in " + currency);
            }
            else
            {
                kpis.AddZero("Paid", LucideIcon.Wallet, Fmt.Dash);
            }
            col.Add(kpis);

            var chart = new BarChart(170f);
            var points = new List<ChartPoint>();
            foreach (var status in StatusOrder)
            {
                counts.TryGetValue(status, out int n);
                points.Add(new ChartPoint(ShortStatusName(status), n,
                    ShowcaseTheme.Tone(StatusTone(status))));
            }
            chart.SetData(points).SetEmptyText("No orders yet");
            col.Add(chart);

            col.Add(new SectionHeader("Order history", orders.Count.ToString()));

            var hint = new Label("Click a row to re-read that order with GetOrderAsync — the same call a "
                + "game polls while it waits for the provider's webhook.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            var table = new DataTable(OrderColumns())
                .WithZebra()
                .WithMaxHeight(480f)
                .WithSort(4, false)
                .WithRowClick(o => ShowOrder(((PlayerOrderDto)o).OrderId));
            table.Bind(orders, o => ((PlayerOrderDto)o).Status == OrderStatus.RewardsGranted);
            col.Add(table);
            return col;
        }

        private DataColumn[] OrderColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "PRODUCT", Grow = 2f,
                    SortKey = o => OrderTitle((PlayerOrderDto)o),
                    Cell = o =>
                    {
                        var order = (PlayerOrderDto)o;
                        var box = new VisualElement();

                        var title = new Label(OrderTitle(order));
                        title.enableRichText = false;
                        title.AddToClassList("sc-list-row__title");
                        box.Add(title);

                        var id = new Label("order " + Fmt.Id(order.OrderId, 12));
                        id.enableRichText = false;
                        id.AddToClassList("sc-list-row__subtitle");
                        box.Add(id);
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "STATUS", FixedWidth = true, Px = 132,
                    // Sorted by the enum's own order, which runs Pending → Paid → RewardsGranted →
                    // the three failure endings, so the column sorts by progress rather than by name.
                    SortKey = o => (int)((PlayerOrderDto)o).Status,
                    Cell = o =>
                    {
                        var order = (PlayerOrderDto)o;
                        return new Chip(StatusName(order.Status), StatusTone(order.Status));
                    },
                },
                new DataColumn
                {
                    Header = "AMOUNT", FixedWidth = true, Px = 116, Align = "right",
                    SortKey = o => (double)((PlayerOrderDto)o).Amount,
                    Cell = o =>
                    {
                        var order = (PlayerOrderDto)o;
                        return new Label(Fmt.Money(order.Amount, order.Currency.ToString()));
                    },
                },
                new DataColumn
                {
                    Header = "PROVIDER", Grow = 1f,
                    SortKey = o => ProviderName(((PlayerOrderDto)o).Provider),
                    Cell = o => new Badge(ProviderName(((PlayerOrderDto)o).Provider), ChipTone.Neutral),
                },
                new DataColumn
                {
                    Header = "CREATED", FixedWidth = true, Px = 136, Align = "right",
                    SortKey = o => ((PlayerOrderDto)o).CreatedAt,
                    Cell = o => new Label(Fmt.DateTime2(((PlayerOrderDto)o).CreatedAt)),
                },
            };
        }

        private string OrderTitle(PlayerOrderDto order)
        {
            return ProductName(order.PurchaseConfigId) ?? Fmt.Id(order.PurchaseConfigId, 14);
        }

        // The order carries a config id, not a name; the catalog is what turns one into the other.
        private string ProductName(string purchaseConfigId)
        {
            if (string.IsNullOrEmpty(purchaseConfigId))
            {
                return null;
            }
            foreach (var item in _catalog)
            {
                if (item != null
                    && string.Equals(item.Id, purchaseConfigId, StringComparison.OrdinalIgnoreCase))
                {
                    return DisplayNameOf(item);
                }
            }
            return null;
        }

        private void ShowOrder(string orderId)
        {
            if (Popup == null || string.IsNullOrEmpty(orderId))
            {
                return;
            }

            var body = new VisualElement();
            var slot = new VisualElement();
            body.Add(slot);

            // Bound before the dialog opens: the slot shows a skeleton, so the click answers at once
            // even when the read is slow.
            ViewBind.Load(
                () => Sdk.Purchases.GetOrderAsync(orderId),
                slot,
                OrderDetail,
                null,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Order",
                    Snippet = OrderSnippet,
                    ServiceName = "Purchases",
                    AllowRetry = true,
                });

            Popup.Open(body, "Order " + Fmt.Id(orderId, 10));
        }

        private VisualElement OrderDetail(PlayerOrderDto order)
        {
            var box = new VisualElement();
            if (order == null)
            {
                box.Add(ZeroState.Panel(LucideIcon.ScrollText, "No order came back",
                    "The call succeeded with an empty body. That usually means the id belongs to "
                    + "another player's order."));
                return box;
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(StatusName(order.Status), StatusTone(order.Status)));
            chips.Add(new Chip(Fmt.Money(order.Amount, order.Currency.ToString()), ChipTone.Accent));
            chips.Add(new Badge(ProviderName(order.Provider), ChipTone.Neutral));
            chips.Add(new Badge(order.RewardsGranted ? "rewards granted" : "rewards pending",
                order.RewardsGranted ? ChipTone.Ok : ChipTone.Warn));
            box.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Order id", Fmt.OrDash(order.OrderId), order.OrderId));
            string product = ProductName(order.PurchaseConfigId);
            kv.Add(Kv("Product", product ?? Fmt.OrDash(order.PurchaseConfigId), order.PurchaseConfigId));
            kv.Add(Kv("Created", Fmt.DateTime2(order.CreatedAt), null));
            kv.Add(Kv("Updated", Fmt.DateTime2(order.UpdatedAt), null));
            kv.Add(Kv("Completed", Fmt.DateTime2(order.CompletedAt), null));
            box.Add(kv);

            string waiting = WaitingNote(order.Status);
            if (waiting != null)
            {
                var note = new Label(waiting);
                note.AddToClassList("sc-fs-hint");
                box.Add(note);
            }
            return box;
        }

        private static string WaitingNote(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Pending:
                    return "Still pending: either the player has not finished paying, or the provider's "
                        + "webhook has not reached the backend yet. A game keeps calling GetOrderAsync "
                        + "until the status settles instead of assuming the redirect meant success.";
                case OrderStatus.Paid:
                    return "Paid, but the rewards have not landed yet. The backend grants them a moment "
                        + "later and the status becomes RewardsGranted — that is the one to wait for.";
                case OrderStatus.Refunded:
                    return "Refunded after the fact. The rewards are not taken back automatically: if "
                        + "that matters, reconcile it in your own game logic.";
                default:
                    return null;
            }
        }

        // ----- subscriptions --------------------------------------------------------------------

        private VisualElement BuildSubscriptions()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Purchases.GetSubscriptionsAsync(),
                slot,
                BuildSubscriptionsBody,
                d => d == null || d.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Subscriptions",
                    Snippet = SubscriptionsSnippet,
                    ServiceName = "Purchases",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Cards(LucideIcon.CalendarClock,
                        "Nothing recurring for this player. A row appears here once a product of type "
                        + "Subscription has been paid for; the provider then keeps renewing it, and what "
                        + "shows up here is the period it is currently billing.",
                        3, "See how a purchase works", () => _tabs.Select(ActionsTab)),
                });
            return slot;
        }

        private VisualElement BuildSubscriptionsBody(List<PlayerSubscriptionDto> subscriptions)
        {
            var col = new VisualElement();

            int live = 0;
            int trialing = 0;
            DateTime? next = null;
            foreach (var sub in subscriptions)
            {
                if (sub == null)
                {
                    continue;
                }
                if (sub.Status == SubscriptionStatus.Active || sub.Status == SubscriptionStatus.Trialing)
                {
                    live++;
                }
                if (sub.Status == SubscriptionStatus.Trialing)
                {
                    trialing++;
                }
                if (sub.CurrentPeriodEnd.HasValue
                    && (!next.HasValue || sub.CurrentPeriodEnd.Value < next.Value))
                {
                    next = sub.CurrentPeriodEnd.Value;
                }
            }

            col.Add(new KpiRow()
                .Add("Subscriptions", LucideIcon.CalendarClock, subscriptions.Count.ToString())
                .Add("Live", LucideIcon.CircleCheck, live.ToString(), null, live > 0)
                .Add("In trial", LucideIcon.Sparkles, trialing.ToString(), null, trialing > 0)
                .Add("Next renewal", LucideIcon.CalendarDays, Fmt.Date(next)));

            col.Add(new SectionHeader("Subscriptions", subscriptions.Count.ToString()));
            foreach (var sub in subscriptions)
            {
                if (sub != null)
                {
                    col.Add(SubscriptionCard(sub));
                }
            }
            return col;
        }

        private VisualElement SubscriptionCard(PlayerSubscriptionDto sub)
        {
            var tone = SubscriptionTone(sub.Status);
            var card = new Card(ShowcaseTheme.Tone(tone));
            card.AddToClassList("sc-pur-sub");
            card.WithTitle(ProductName(sub.PurchaseConfigId) ?? Fmt.Id(sub.PurchaseConfigId, 16),
                ShowcaseTheme.Tone(tone));

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(SubscriptionStatusName(sub.Status), tone));
            chips.Add(new Badge(ProviderName(sub.Provider), ChipTone.Neutral));

            // A live subscription is the only one whose period end is still ahead, so it is the only
            // one worth counting down to; the rest just report the date.
            if (sub.Status == SubscriptionStatus.Active || sub.Status == SubscriptionStatus.Trialing)
            {
                chips.Add(new CountdownChip(sub.CurrentPeriodEnd.HasValue
                    ? sub.CurrentPeriodEnd.Value.ToUniversalTime()
                    : (DateTime?)null));
            }
            if (sub.TrialEnd.HasValue)
            {
                chips.Add(new Chip("trial ends " + Fmt.Date(sub.TrialEnd), ChipTone.Info));
            }
            if (sub.CancelledAt.HasValue)
            {
                chips.Add(new Chip("cancelled " + Fmt.Date(sub.CancelledAt), ChipTone.Bad));
            }
            card.Body.Add(chips);

            var bar = PeriodBar(sub, ShowcaseTheme.Tone(tone));
            if (bar != null)
            {
                card.Body.Add(bar);
            }
            else
            {
                var note = new Label("No billing period reported yet — the provider fills it in when it "
                    + "charges for the first time.");
                note.AddToClassList("sc-fs-hint");
                card.Body.Add(note);
            }

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Subscription id", Fmt.OrDash(sub.SubscriptionId), sub.SubscriptionId));
            kv.Add(Kv("Product id", Fmt.OrDash(sub.PurchaseConfigId), sub.PurchaseConfigId));
            kv.Add(Kv("Started", Fmt.DateTime2(sub.CreatedAt), null));
            kv.Add(Kv("Current period", Period(sub), null));
            card.Body.Add(kv);
            return card;
        }

        private static string Period(PlayerSubscriptionDto sub)
        {
            if (!sub.CurrentPeriodStart.HasValue && !sub.CurrentPeriodEnd.HasValue)
            {
                return Fmt.Dash;
            }
            return Fmt.Date(sub.CurrentPeriodStart) + " → " + Fmt.Date(sub.CurrentPeriodEnd);
        }

        private static VisualElement PeriodBar(PlayerSubscriptionDto sub, UnityEngine.Color accent)
        {
            if (!sub.CurrentPeriodStart.HasValue || !sub.CurrentPeriodEnd.HasValue)
            {
                return null;
            }

            var start = sub.CurrentPeriodStart.Value.ToUniversalTime();
            var end = sub.CurrentPeriodEnd.Value.ToUniversalTime();
            double total = (end - start).TotalSeconds;
            if (total <= 0d)
            {
                return null;
            }

            double elapsed = (DateTime.UtcNow - start).TotalSeconds;
            if (elapsed < 0d)
            {
                elapsed = 0d;
            }
            if (elapsed > total)
            {
                elapsed = total;
            }

            var remaining = end - DateTime.UtcNow;
            return new ProgressBar()
                .Set((float)elapsed, (float)total)
                .SetLabel(remaining.TotalSeconds > 0d
                    ? Fmt.Duration(remaining) + " left of this period"
                    : "this period has ended")
                .SetAccent(accent);
        }

        // ----- actions --------------------------------------------------------------------------

        private VisualElement BuildActions()
        {
            var col = new VisualElement();

            var hint = new Label("Two of the three purchase calls live here. The third, BuyAsync, is "
                + "described at the bottom and never executed by this example.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new SectionHeader("How a purchase completes"));
            col.Add(FlowSteps());

            col.Add(new SectionHeader("Calls"));

            col.Add(new ActionCard("Start an order",
                    "Creates a Pending order for one product and returns the provider's payment page. "
                    + "Nothing is charged: the player still has to pay on that page, and the order only "
                    + "settles when the provider's webhook reaches the backend.",
                    LucideIcon.CirclePlay)
                .WithFields(
                    FormField.Text("purchaseKey", "Purchase key", _prefillKey, true)
                        .WithPlaceholder("CatalogItemDto.Key — e.g. starter_pack"),
                    FormField.Text("providerConfigId", "Provider config id", _prefillProvider, true)
                        .WithPlaceholder("CatalogPriceDto.ProviderConfigId"),
                    FormField.Text("successUrl", "Success redirect URL", DefaultSuccessUrl),
                    FormField.Text("cancelUrl", "Cancel redirect URL", DefaultCancelUrl))
                .WithSnippet(InitiateSnippet)
                .OnRun("Create the order", InitiateAction));

            col.Add(new ActionCard("Read one order",
                    "The polling call: once the payment window closes, a game asks for the order until "
                    + "its status settles. Clicking a row on the Orders tab runs exactly this.",
                    LucideIcon.FileSearch)
                .WithFields(FormField.Text("orderId", "Order id", null, true))
                .WithSnippet(OrderSnippet)
                .OnRun("Read", ReadOrderAction));

            col.Add(BuyNote());
            return col;
        }

        private static VisualElement FlowSteps()
        {
            var box = new VisualElement();
            box.AddToClassList("sc-pur-flow");
            box.Add(Step(1, "InitiatePurchaseAsync creates a Pending order and hands back the provider's "
                + "payment URL. No money has moved."));
            box.Add(Step(2, "The player pays on the provider's page — in a browser, or in the SDK's "
                + "WebView. Card details never reach the game or the SDK."));
            box.Add(Step(3, "The provider calls the backend's webhook. That, and not the redirect the "
                + "player lands on, is what marks the order Paid."));
            box.Add(Step(4, "The backend grants the product's rewards and the status becomes "
                + "RewardsGranted. The game polls GetOrderAsync until it sees that, then simply reads "
                + "the new economy state — it grants nothing itself."));
            return box;
        }

        private static VisualElement Step(int number, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-pur-step");

            var num = new Label(number.ToString());
            num.AddToClassList("sc-pur-step__num");
            row.Add(num);

            var body = new Label(text);
            body.enableRichText = false;
            body.AddToClassList("sc-pur-step__text");
            row.Add(body);
            return row;
        }

        private VisualElement BuyNote()
        {
            var card = new Card(ShowcaseTheme.Warn);
            card.AddToClassList("sc-pur-note");
            card.WithTitle("BuyAsync — shown, never run", ShowcaseTheme.Warn);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip("real money", ChipTone.Bad));
            chips.Add(new Chip("needs a URL-hooking WebView", ChipTone.Warn));
            card.Body.Add(chips);

            var what = new Label("BuyAsync is the one-call version of everything above: it initiates the "
                + "order, opens the payment page in the SDK WebView, waits for the redirect, polls the "
                + "order until the webhook settles it, and returns a PurchaseResult that is Completed, "
                + "SubscriptionActivated, Pending, Cancelled or Failed.");
            what.AddToClassList("sc-pur-note__text");
            card.Body.Add(what);

            var why = new Label("No button on this screen calls it. It charges a real payment method, and "
                + "an example that spends money on a curious reader's card is the wrong kind of demo. It "
                + "also needs a WebView that can intercept URLs, so on WebGL it fails immediately with "
                + "\"WebView checkout isn't available on this platform\" — use InitiatePurchaseAsync and "
                + "your own checkout surface there.");
            why.AddToClassList("sc-pur-note__text");
            card.Body.Add(why);

            card.Body.Add(SdkCallDrawer.CodeBlock(BuyExcerpt));

            var more = new Label("The full call with its options, its polling window and its three events "
                + "is in the </> drawer at the top of this screen.");
            more.AddToClassList("sc-fs-hint");
            card.Body.Add(more);
            return card;
        }

        private void Prefill(string purchaseKey, string providerConfigId)
        {
            _prefillKey = purchaseKey;
            _prefillProvider = providerConfigId;
            if (Popup != null)
            {
                Popup.Close();
            }

            // The form reads these as its field defaults, so the pane has to be rebuilt before the
            // tab is shown.
            _tabs.Invalidate(ActionsTab);
            _tabs.Select(ActionsTab);
            if (Toasts != null)
            {
                Toasts.Info("Order form filled in for " + Fmt.Truncate(Fmt.OrDash(purchaseKey), 24));
            }
        }

        private async Task<ActionOutcome> InitiateAction(FormValues values)
        {
            var op = Sdk.Purchases.InitiatePurchaseAsync(
                values.Text("purchaseKey").Trim(),
                values.Text("providerConfigId").Trim(),
                UrlOr(values.Text("successUrl"), DefaultSuccessUrl),
                UrlOr(values.Text("cancelUrl"), DefaultCancelUrl));

            var outcome = await AwaitData(op, "Purchases · initiate");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            // The order exists from this moment on, so both player-facing tabs are stale.
            _tabs.Invalidate(OrdersTab);
            _tabs.Invalidate(SubscriptionsTab);
            if (Toasts != null)
            {
                Toasts.Ok("Order created");
            }

            var started = op.Result.Data;
            if (started == null)
            {
                return ActionOutcome.Success("The call succeeded but returned no payment URL.");
            }

            var detail = new VisualElement();

            var note = new Label("Open the payment URL to continue. Nothing is charged until the player "
                + "pays there, and the order stays Pending until the provider's webhook settles it — "
                + "read it back with the card below.");
            note.AddToClassList("sc-fs-hint");
            detail.Add(note);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv(started.IsSubscription ? "Subscription id" : "Order id",
                Fmt.OrDash(started.OperationId), started.OperationId));
            kv.Add(Kv("Payment URL", Fmt.Truncate(Fmt.OrDash(started.PaymentUrl), 44),
                started.PaymentUrl));
            kv.Add(Kv("Kind", started.IsSubscription ? "subscription" : "one-off purchase", null));
            detail.Add(kv);

            return ActionOutcome.Success(
                "Order " + Fmt.Id(started.OperationId, 10) + " is waiting for payment", detail);
        }

        private async Task<ActionOutcome> ReadOrderAction(FormValues values)
        {
            var op = Sdk.Purchases.GetOrderAsync(values.Text("orderId").Trim());
            var outcome = await AwaitData(op, "Purchases · order");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var order = op.Result.Data;
            if (order == null)
            {
                return ActionOutcome.Failure("That id returned no order.");
            }
            return ActionOutcome.Success(
                StatusName(order.Status) + " · " + Fmt.Money(order.Amount, order.Currency.ToString()),
                OrderDetail(order));
        }

        private static string UrlOr(string typed, string fallback)
        {
            return string.IsNullOrWhiteSpace(typed) ? fallback : typed.Trim();
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }

        private async Task<Outcome> AwaitData<T>(AsyncOperation<RestApiResult<T>> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private Outcome Fold(RestApiResult result, string label)
        {
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result);
            }
            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true };
            }
            string message = result != null && result.Error != null
                && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome { Ok = false, Message = message };
        }

        private static string DisplayNameOf(CatalogItemDto item)
        {
            if (item == null)
            {
                return Fmt.Dash;
            }
            return string.IsNullOrEmpty(item.DisplayName) ? Fmt.OrDash(item.Key) : item.DisplayName;
        }

        private static CatalogPriceDto PrimaryPrice(CatalogItemDto item)
        {
            if (item.Prices == null)
            {
                return null;
            }
            foreach (var price in item.Prices)
            {
                if (price != null)
                {
                    return price;
                }
            }
            return null;
        }

        private static string ProviderLabel(CatalogPriceDto price)
        {
            return string.IsNullOrEmpty(price.ProviderName)
                ? ProviderName(price.ProviderType)
                : price.ProviderName;
        }

        private static string ProviderName(PaymentProviderType provider)
        {
            switch (provider)
            {
                case PaymentProviderType.Stripe: return "Stripe";
                case PaymentProviderType.Yookassa: return "YooKassa";
                case PaymentProviderType.VkGames: return "VK Games";
                case PaymentProviderType.GooglePlay: return "Google Play";
                case PaymentProviderType.Apple: return "Apple";
                default: return provider.ToString();
            }
        }

        private static string TypeName(PurchaseType type)
        {
            switch (type)
            {
                case PurchaseType.Consumable: return "Consumable";
                case PurchaseType.NonConsumable: return "Non-consumable";
                case PurchaseType.Subscription: return "Subscription";
                default: return type.ToString();
            }
        }

        private static string TypeGlyph(PurchaseType type)
        {
            switch (type)
            {
                case PurchaseType.NonConsumable: return LucideIcon.Gem;
                case PurchaseType.Subscription: return LucideIcon.CalendarClock;
                default: return LucideIcon.Package;
            }
        }

        private static ChipTone TypeTone(PurchaseType type)
        {
            switch (type)
            {
                case PurchaseType.NonConsumable: return ChipTone.Accent;
                case PurchaseType.Subscription: return ChipTone.Info;
                default: return ChipTone.Neutral;
            }
        }

        private static string StatusName(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Pending: return "pending";
                case OrderStatus.Paid: return "paid";
                case OrderStatus.RewardsGranted: return "rewards granted";
                case OrderStatus.Cancelled: return "cancelled";
                case OrderStatus.Refunded: return "refunded";
                case OrderStatus.Failed: return "failed";
                default: return status.ToString();
            }
        }

        private static string ShortStatusName(OrderStatus status)
        {
            return status == OrderStatus.RewardsGranted ? "Granted" : status.ToString();
        }

        private static ChipTone StatusTone(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.RewardsGranted: return ChipTone.Ok;
                case OrderStatus.Paid: return ChipTone.Info;
                case OrderStatus.Pending: return ChipTone.Warn;
                case OrderStatus.Cancelled: return ChipTone.Neutral;
                case OrderStatus.Refunded:
                case OrderStatus.Failed: return ChipTone.Bad;
                default: return ChipTone.Neutral;
            }
        }

        private static string SubscriptionStatusName(SubscriptionStatus status)
        {
            switch (status)
            {
                case SubscriptionStatus.Active: return "active";
                case SubscriptionStatus.Trialing: return "trialing";
                case SubscriptionStatus.PastDue: return "past due";
                case SubscriptionStatus.Cancelled: return "cancelled";
                case SubscriptionStatus.Expired: return "expired";
                default: return status.ToString();
            }
        }

        private static ChipTone SubscriptionTone(SubscriptionStatus status)
        {
            switch (status)
            {
                case SubscriptionStatus.Active: return ChipTone.Ok;
                case SubscriptionStatus.Trialing: return ChipTone.Info;
                case SubscriptionStatus.PastDue: return ChipTone.Warn;
                case SubscriptionStatus.Cancelled: return ChipTone.Bad;
                default: return ChipTone.Neutral;
            }
        }

        private static string RewardGlyph(PurchaseRewardKind kind)
        {
            switch (kind)
            {
                case PurchaseRewardKind.Currency: return LucideIcon.Coins;
                case PurchaseRewardKind.Energy: return LucideIcon.Zap;
                default: return LucideIcon.Package;
            }
        }

        private static string RewardKindName(PurchaseRewardKind kind)
        {
            switch (kind)
            {
                case PurchaseRewardKind.Currency: return "currency";
                case PurchaseRewardKind.Energy: return "energy";
                case PurchaseRewardKind.Item: return "item";
                default: return kind.ToString();
            }
        }

        private static string RewardText(RewardDataDto reward)
        {
            return Fmt.Truncate(Fmt.OrDash(reward.RewardId), 14) + " ×" + Fmt.Number(reward.Count);
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;
        }

        /// <summary>
        /// Money here is always per-currency: a project may price the same catalog in several, and an
        /// order keeps whichever one was charged. Adding them together would produce a number that
        /// means nothing, so the money tiles report the currency carrying the most rows — and say so.
        /// </summary>
        private sealed class MoneyBucket
        {
            private readonly Dictionary<PurchaseCurrency, decimal> _sums =
                new Dictionary<PurchaseCurrency, decimal>();

            private readonly Dictionary<PurchaseCurrency, int> _counts =
                new Dictionary<PurchaseCurrency, int>();

            public void Add(PurchaseCurrency currency, decimal amount)
            {
                _sums.TryGetValue(currency, out decimal sum);
                _sums[currency] = sum + amount;
                _counts.TryGetValue(currency, out int count);
                _counts[currency] = count + 1;
            }

            public bool TryDominant(out string currency, out decimal total, out int count)
            {
                currency = null;
                total = 0m;
                count = 0;
                foreach (var pair in _counts)
                {
                    if (pair.Value > count)
                    {
                        count = pair.Value;
                        currency = pair.Key.ToString();
                        total = _sums[pair.Key];
                    }
                }
                return count > 0;
            }
        }
    }
}

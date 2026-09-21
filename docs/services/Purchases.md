# Purchases

`PurchasesService` — каталог магазина, заказы и подписки игрока, запуск покупки.

## Цена = товар × платёжная интеграция

Товары и их цены заводятся в консоли. Каждая цена привязана к **платёжной интеграции** проекта
(раздел «Интеграции»: Stripe, ЮKassa, VK Игры, Google Play) и адресуется её **ключом** — тем, что задали
при создании интеграции. Ключи и секреты провайдера живут в интеграции, а не в ветке: в SDK их нет.

У одного товара может быть несколько цен — по одной на интеграцию (например, `stripe-eu` в EUR и
`yookassa` в RUB). Игра выбирает цену и передаёт её `IntegrationKey` в покупку.

## Методы

| Метод | Что делает |
|---|---|
| `LoadCatalogAsync()` → `List<CatalogItemDto>` | товары ветки с ценами и наградами |
| `InitiatePurchaseAsync(purchaseKey, integrationKey, successRedirectUrl, cancelRedirectUrl)` → `InitiatePurchaseResponseDto` | создаёт заказ (или подписку) в статусе Pending и возвращает страницу оплаты. Деньги не списываются |
| `BuyAsync(purchaseKey, integrationKey, PurchaseOptions options = null)` → `PurchaseResult` | весь сценарий: заказ → страница оплаты в WebView → ожидание редиректа → опрос заказа до выдачи наград |
| `GetOrdersAsync()` / `GetOrderAsync(orderId)` | заказы игрока; `GetOrderAsync` — это и есть опрос после оплаты |
| `GetSubscriptionsAsync()` | подписки игрока |

События: `OnPurchaseCompleted(PlayerOrderDto)`, `OnPurchaseCancelled(operationId)`, `OnPurchaseFailed(PurchaseResult)`.

## Каталог

```csharp
var op = Sdk.Purchases.LoadCatalogAsync();
await op.Task();

foreach (var item in op.Result.Data)
{
    // item.Key — ключ товара (его и передают в покупку)
    foreach (var price in item.Prices)
    {
        // price.IntegrationKey — ключ интеграции, через которую платят
        // price.ProviderType   — Stripe | Yookassa | VkGames | GooglePlay
        // price.ProviderName   — имя интеграции из консоли
        // price.Amount, price.Currency
        // price.MappingId      — стабильный ключ цены "{purchaseKey}@{integrationKey}", справочный
    }
}
```

В каталоге только то, что можно оплатить прямо сейчас: цена, чья интеграция удалена, выключена или не умеет
принимать платежи, не приходит, а товар без единой такой цены пропадает из каталога целиком. Выключение
интеграции в консоли видно на следующем же запросе каталога.

## Покупка

```csharp
var price = item.Prices[0];
var op = Sdk.Purchases.BuyAsync(item.Key, price.IntegrationKey);
await op.Task();

switch (op.Result.Status)
{
    case PurchaseResultStatus.Completed: break;             // награды выданы, op.Result.Order
    case PurchaseResultStatus.SubscriptionActivated: break;  // op.Result.Subscription
    case PurchaseResultStatus.Pending: break;                // вебхук запаздывает — опросите GetOrderAsync позже
    case PurchaseResultStatus.Cancelled: break;              // игрок закрыл оплату
    case PurchaseResultStatus.Failed:                        // op.Result.Error; отказ сервера — op.Result.ApiError
        if (op.Result.ApiError.HasCode(CloudErrorCodes.PurchasesPaymentIntegrationUnavailable))
        {
            // интеграцию выключили, пока игрок смотрел магазин — перезапросите каталог
        }
        break;
}
```

`BuyAsync` нужен WebView, умеющий перехватывать URL: на WebGL и в редакторе под WebGL он сразу отвечает
`Failed`. Там используйте `InitiatePurchaseAsync` и свою поверхность оплаты, а заказ опрашивайте
`GetOrderAsync`. Заказ закрывает **вебхук провайдера**, а не редирект игрока — поэтому опрос.

Награды выдаёт сервер, когда заказ переходит в `RewardsGranted`; игра ничего не начисляет сама, а просто
перечитывает экономику.

## Магазины платформ

Цены на интеграциях VK Игры и Google Play оплачиваются **в самом магазине** через его SDK; сервер узнаёт о
покупке из колбэка магазина. `InitiatePurchaseAsync` для такой цены отвечает 422
`purchases.provider_unsupported`. В каталоге такие цены есть — по ним игра показывает стоимость.

## Ошибки

| Код | HTTP | Когда |
|---|---|---|
| `purchases.purchase_key_required` | 422 | не передан `purchaseKey` |
| `purchases.integration_key_required` | 422 | не передан `integrationKey` |
| `purchases.redirect_urls_required` | 422 | не переданы адреса возврата |
| `purchases.purchase_config_not_found` / `purchase_config_not_active` | 404 / 409 | товара нет или он выключен |
| `purchases.provider_mapping_not_found` / `provider_mapping_not_active` | 404 / 409 | у товара нет цены на этой интеграции или она выключена (`data.purchaseKey`, `data.integrationKey`) |
| `purchases.payment_integration_unavailable` | 409 | интеграции нет, она выключена или не принимает платежи (`data.reason`: `not_found` / `disabled` / `not_payment`) — перезапросите каталог |
| `purchases.provider_unsupported` | 422 | цена магазина (VK Игры, Google Play) — оплата в магазине |
| `purchases.non_consumable_already_owned` | 409 | неразменный товар уже куплен |
| `purchases.payment_provider_error`, `purchases.yookassa_payment_creation_failed`, `purchases.yookassa_invalid_response` | 502 | отказ самого провайдера |
| `integrations.secret_unreadable` | 500 | сервер не смог прочитать ключи интеграции |

Константы — `CloudErrorCodes.Purchases*`.

## Переход с 0.5

- `CatalogPriceDto.ProviderConfigId` → `IntegrationKey`.
- `InitiatePurchaseAsync(purchaseKey, providerConfigId, …)` → `InitiatePurchaseAsync(purchaseKey, integrationKey, …)`,
  `BuyAsync` — так же. Передавайте `price.IntegrationKey` из каталога.
- `MappingId` теперь стабильный ключ `{purchaseKey}@{integrationKey}`, а не версионный id.
- Коды `purchases.provider_config_*` и `purchases.stripe_no_active_provider` удалены.

## Code
- `Runtime/Services/Purchases/*`

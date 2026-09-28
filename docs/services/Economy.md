# Economy

`EconomyService` — валюты, предметы, энергии и инвентарь.

## Конфигурация

- `LoadConfigsAsync()` → `EconomyConfigsDto` — загрузка конфигов (валюты, предметы, энергии)
- `ClearCache()` — очистка кеша
- `TryGetResource(key, out resource)` — получить ресурс по ключу
- `GetResourceFields<T>(key)` / `TryGetResourceFields<T>(key, out fields)` — поля ресурса
- `GetResourceComponent<T>(key, componentKey)` / `TryGetResourceComponent<T>(key, componentKey, out component)` — компоненты

## Инвентарь

- `LoadInventoryAsync()` → `PlayerInventoryDto`
- `AddItemAsync(itemId, amount, inventoryKey)` — добавить предметы
- `SubtractItemAsync(itemId, amount, inventoryKey)` — вычесть предметы
- `SubtractItemSafeAsync(itemId, amount, inventoryKey)` — безопасное вычитание (ошибка при нехватке)
- `UpdateItemPropertiesAsync(itemId, slotId, properties, inventoryKey)` — обновить свойства слота
- `ConsumeItemAsync(itemId, slotId, inventoryKey)` → `ConsumeItemResponseDto` — потребить предмет

## Награды к получению

То, что начислили другие сервисы — лидерборд или турнир по окончании сессии, челлендж, дейлик, покупка,
промокод, — ждёт здесь, пока игра не заберёт.

- `GetPendingRewardsAsync()` → `List<RewardContainerDto>` — без забора. Контейнер: `SourceType`
  (`Leaderboard`, `Tournament`, `Challenge`, `DailyReward`, `Purchase`, `PromoCode`), `SourceId` (id конфига-источника
  на момент выплаты, например `LeaderboardConfig.Id`), `Rewards` — `RewardKey` (ключ ресурса), `EconomyResourceKind`, `Count`
- `ClaimRewardsAsync()` → `List<RewardContainerDto>` — забирает всё ожидающее (валюты, предметы, энергии);
  возвращает забранное, пустой список — если нечего
- `PlayerInventoryDto.Rewards` — те же контейнеры в ответе `LoadInventoryAsync()`

## Энергии

- `GetEnergiesAsync()` → `List<EnergyBalanceDto>` — все энергии
- `GetEnergyAsync(energyId)` → `EnergyBalanceDto`
- `SpendEnergyAsync(energyId, amount)` → `EnergyBalanceDto`
- `AddEnergyAsync(energyId, amount)` → `EnergyBalanceDto`
- `SetUnlimitedEnergyAsync(energyId, durationSeconds)` → `EnergyBalanceDto`

## Свойства

- `Currencies` — словарь валют
- `Items` — словарь предметов
- `Energies` — словарь энергий

## Code
- `Core/Services/Economy/*`

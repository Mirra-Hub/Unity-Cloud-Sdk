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

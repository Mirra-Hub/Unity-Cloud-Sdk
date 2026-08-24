# Entities

`EntitiesService` — конфигурации сущностей (templates с полями и компонентами).

## Методы

- `GetConfigsAsync()` → `EntitiesConfigsSnapshotDto` — загрузка всех конфигов
- `ClearCache()` — очистка кеша
- `TryGetConfigRaw(key, out config)` — получить сырой конфиг
- `GetConfig<T>(key)` / `TryGetConfig<T>(key, out config)` — типизированный конфиг
- `GetComponent<T>(configKey, componentKey)` / `TryGetComponent<T>(configKey, componentKey, out component)` — компоненты

## Свойства

- `Configs` — словарь `IReadOnlyDictionary<string, EntityConfigSdkDto>`

## Code
- `Core/Services/Entities/*`

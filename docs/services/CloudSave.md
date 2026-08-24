# CloudSave

`CloudSaveService` — облачные сохранения.

## Области данных

### Player Data (своя)
- `GetPlayerDataAsync(keys, offset, limit)` → `DataItemResponse[]`
- `UpsertPlayerDataAsync(data)` — сохранить
- `DeletePlayerDataAsync(keys)` — удалить

### Player Data (чужая)
- `GetOtherPlayerDataAsync(playerProfileId, keys, offset, limit)` → `DataItemResponse[]`
- `UpsertOtherPlayerDataAsync(playerProfileId, data)`
- `DeleteOtherPlayerDataAsync(playerProfileId, keys)`

### Global Data
- `LoadGlobalDataAsync(keys, offset, limit)` → `DataItemResponse[]`
- `SaveGlobalDataAsync(data)`
- `DeleteGlobalDataAsync(keys)`

### Custom Data
- `LoadCustomDataAsync(customId, keys, offset, limit)` → `DataItemResponse[]`
- `SaveCustomDataAsync(customId, data)`
- `DeleteCustomDataAsync(customId, keys)`

## Query

- `QueryPlayerDataAsync(request)` → `QueryIndexResponse`
- `QueryGlobalDataAsync(request)` → `QueryIndexResponse`
- `QueryCustomDataAsync(customId, request)` → `QueryIndexResponse`

## Свойства

- `PlayerData` — кешированные данные после `GetPlayerDataAsync`

## Code
- `Core/Services/Cloud Save/*`

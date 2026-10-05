# CloudSave

`CloudSaveService` — облачные сохранения: данные (JSON key/value) и файлы.

## Области данных

### Player Data (своя)
- `GetPlayerDataAsync(keys, offset, limit)` → `DataItemResponse[]`, обновляет `PlayerData`
- `UpsertPlayerDataAsync(data)` — сохранить
- `DeletePlayerDataAsync(keys)` — удалить

### Player Data (чужая)
- `GetOtherPlayerDataAsync(playerProfileId, keys, offset, limit)` → `DataItemResponse[]` (только ключи с `Other` в маске чтения)
- `UpsertOtherPlayerDataAsync(playerProfileId, data)` — менять существующие ключи с `Other` в маске записи; создать нельзя
- `DeleteOtherPlayerDataAsync(playerProfileId, keys)`

### Global Data
Глобальные данные публикуются из консоли или Cloud Code. Игрок их читает и меняет/удаляет только ключи, у которых в
маске записи есть `Other`; создать global-ключ из SDK нельзя (`cloud_saves.access_denied`).
- `LoadGlobalDataAsync(keys, offset, limit)` → `DataItemResponse[]`
- `SaveGlobalDataAsync(data)`
- `DeleteGlobalDataAsync(keys)`

### Custom Data
Общие данные «комнат», гильдий, событий. `customId` — `[a-zA-Z0-9_-]`, до 128 символов, кроме `__global__`.
- `LoadCustomDataAsync(customId, keys, offset, limit)` → `DataItemResponse[]`
- `SaveCustomDataAsync(customId, data)` — новый ключ по умолчанию читают и меняют все игроки и сервер
- `DeleteCustomDataAsync(customId, keys)`

## Запись: `CloudSaveDataRequest`

`AddInt`, `AddLong`, `AddFloat`, `AddDouble`, `AddBool`, `AddString`, `AddJson`. У каждого — необязательные
`readMask`, `writeMask` и `expectedVersion` (запись проходит, только если версия ключа совпала).

`AddLong` — для значений за пределами `int` (Unix-время в миллисекундах), точно до 2^53. `AddFloat` пишет
кратчайшее представление (`0.1f` → `0.1`).

## Маски доступа (`AccessMask`)

| Бит | Кто |
| --- | --- |
| `Owner` | игрок, чьи это данные |
| `Other` | любой другой игрок (для global и custom — все игроки) |
| `Server` | игровой сервер: Cloud Code и другая логика бэкенда |

- Маски необязательны. Не переданы — у существующего ключа остаются прежние, новый получает значение по умолчанию.
  Свой новый ключ по умолчанию читают и пишут владелец и сервер.
- Бит `Server` у ключа, созданного игроком, бэкенд ставит сам, и снять его из клиента нельзя: игровая логика всегда
  видит данные игрока. Ключи, которые записал сервер (например, валюта), игрок может читать, но не менять.
- Консоль маски не ограничивают.
- «Ключа нет» и «нет доступа» неразличимы: недоступный ключ не возвращается при чтении, запись в него —
  `cloud_saves.access_denied`.

## Query

Поиск идёт по индексу, созданному в консоли. Находятся только сущности, у которых все поля индекса видны другим
игрокам (`Other` в маске чтения).
- `QueryPlayerDataAsync(request)` → `QueryIndexResponse`
- `QueryGlobalDataAsync(request)` → `QueryIndexResponse` — только глобальные данные
- `QueryCustomDataAsync(request)` → `QueryIndexResponse` — все custom-сущности, без глобальных данных
- `QueryCustomDataAsync(customId, request)` — устарел, `customId` не применяется

## Файлы

### Свои
- `UploadPlayerFileAsync(key, bytes, fileName, mimeType, meta, readMask, writeMask)` — создать или заменить; при
  замене meta и дата создания сохраняются, если meta не передана
- `GetPlayerFileAsync(key)` — метаданные, `GetPlayerFileUrlAsync(key)` — ссылка на скачивание
- `UpdatePlayerFileMetaAsync(key, meta)`, `UpdatePlayerFileContentAsync(key, bytes, fileName, mimeType)`
- `DeletePlayerFileAsync(key)`

### Чужие
- `GetOtherPlayerFileAsync`, `GetOtherPlayerFileUrlAsync`, `UpdateOtherPlayerFileMetaAsync`,
  `UpdateOtherPlayerFileContentAsync` — по маскам файла; недоступный файл отвечает как отсутствующий

### Глобальные
Публикуются из консоли. Игрок читает (`GetGlobalFileAsync`, `GetGlobalFileUrlAsync`) и меняет/удаляет только
файлы с `Other` в маске записи; создать глобальный файл из SDK нельзя.

Ключ файла может содержать пробелы и любые символы — SDK экранирует его как сегмент пути.

## Свойства

- `PlayerData` — кеш своих данных. Полное чтение (`GetPlayerDataAsync()` без фильтров) заменяет его; чтение с `keys`
  обновляет только эти ключи, остальные остаются. Методы: `GetString` (объекты и массивы — как JSON), `GetInt`,
  `GetLong`, `GetFloat`, `GetDouble`, `GetBool`, `HasKey`, `Fields`.

## Code
- `Packages/com.mirrahub.cloud-sdk/Runtime/Services/Cloud Save/*`

# Storage — локальное хранилище

Два уровня локального хранения:

- **`IStorage`** (`Core/Storage/IStorage.cs`, `PrefsStorage`) — строковый key-value поверх `PlayerPrefs`. Используется Auth для guest-id / токенов.
- **`IBlobStorage`** (`Core/Storage/Blob/`) — durable бинарное хранилище (байтовые блобы) с транзакционной записью. Используется кешем ассетов ([AssetsStorage](services/AssetsStorage.md)); подходит для любых локальных данных.

## `IBlobStorage`

Трёхуровневый контракт, async на `System.Threading.Tasks.Task`:

```
IBlobStorage    — OpenContainerAsync / DeleteContainerAsync / ListContainersAsync
IBlobContainer  — ReadAsync / ExistsAsync / ReadManyAsync / ReadByPrefixAsync /
                  BeginWrite / DeleteByPrefixAsync / TryAcquireExclusiveAsync   (IDisposable)
IBlobWriteBatch — Put / Delete / CommitAsync
```

- **Контейнер** — единица владения (например `asset_cache`). Открытие refcounted: повторный `OpenContainerAsync(id)` отдаёт тот же инстанс, `Dispose` декрементит.
- **Ключ** — непрозрачная иерархическая строка (`a/b/c`); собирается только в key-builder'ах домена, не в call-site.
- **Запись** — только батчами: `BeginWrite() → Put/Delete → await CommitAsync()`. Данные durable **только после** `CommitAsync`.
- **Чтение** — `ReadAsync` (одно), `ReadManyAsync` (пачка одним заходом), `ReadByPrefixAsync` (скан префикса). Пачки независимых чтений — через `ReadManyAsync` / `Task.WhenAll`, не последовательные `await`.
- `BlobResult` — `{ BlobStatus Status; byte[] Value; }` (`Success` / `NotFound` / `Error`).

## Бэкенды (выбор по платформе)

| Платформа | Бэкенд |
|---|---|
| Editor / Standalone / Mobile | **SQLite** (пакет `com.gilzoide.sqlite-net`, файл БД на контейнер, WAL) |
| WebGL | **IndexedDB** (jslib-мост, без сети) |
| Тесты / debug | **File** (папка на контейнер) |

Выбор — в `MirraCloudSDK.Initialize()` под `#if UNITY_WEBGL && !UNITY_EDITOR` (IndexedDB) / `#else` (SQLite). Async-модель — `Task` (а не общий для SDK `AsyncOperation<RestApiResult<T>>`), т.к. это низкоуровневая инфраструктура.

## Новый потребитель

1. Свой container-id + свой key-builder (ключи строятся только в нём).
2. `OpenContainerAsync(id)` из переданного в конструктор `IBlobStorage`; `Dispose` контейнера на teardown сервиса.
3. Запись через батч + `CommitAsync`; версионируемые данные — версия в ключе, прунинг старых через `DeleteByPrefixAsync`.

## Code
- `Core/Storage/Blob/*` — контракты (`IBlobStorage`/`IBlobContainer`/`IBlobWriteBatch`/`BlobResult`) + бэкенды SQLite / File / IndexedDB
- `Core/Storage/{IStorage,PrefsStorage}.cs`

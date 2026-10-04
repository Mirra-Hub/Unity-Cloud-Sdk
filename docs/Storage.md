# Storage — локальное хранилище

Два уровня локального хранения:

- **`IStorage`** (`Runtime/Storage/IStorage.cs`, реализация `BlobKeyValueStorage`) — строковый key-value поверх
  `IBlobStorage`. В нём Auth держит guest-id и refresh-токен. `PlayerPrefs` SDK не использует.
- **`IBlobStorage`** (`Runtime/Storage/Blob/`) — durable бинарное хранилище (байтовые блобы) с транзакционной записью.
  На нём стоят `IStorage` и кеш ассетов ([AssetsStorage](services/AssetsStorage.md)); подходит для любых локальных
  данных.

## `IStorage`

```
Ready         — значения загружены (Task, никогда не падает)
HasKey / GetString / SaveString / DeleteKeys — из памяти, синхронно
FlushAsync    — всё записанное до вызова — на диске
```

- **Где лежит.** Контейнер `mirracloud_prefs`; в редакторе — `mirracloud_prefs_editor`: редактор и standalone-сборка
  делят `persistentDataPath`, а с одним контейнером были бы одним игроком (с PlayerPrefs они были разными). Имена —
  `LocalDataContainers`, ключи Auth — `AuthStorageKeys` (`auth/guest_id`, `auth/refresh_token`). Переименование
  ключа или контейнера разлогинит всех игроков — поэтому их литералы закреплены тестом.
- **Область `{projectId}/`.** В WebGL все игры одного сайта делят одну базу IndexedDB; без области игра читала бы
  чужую сессию, а получив на неё отказ, стирала бы её.
- **Загрузка.** Вся область читается в память один раз — в `MirraCloudSDK.Initialize()`, в фоне; это `Ready`.
  Не открылось (нет нативной SQLite, битый файл) — ошибка в лог, дальше только в памяти: сессия не переживёт
  перезапуск, но SDK работает. Auth ждёт `Ready` сам: `InitializeAsync`, `LoginGuestAsync`, `LinkGuestAsync`.
- **Запись.** Сразу меняет память и коммитится без задержки. Записи, пришедшие во время коммита, уходят следующим
  батчем — последнее значение ключа побеждает, порядок сохраняется, батч атомарен. Записанное до загрузки загрузка
  не перетирает. Пустое значение = удаление ключа.
- **`TryAcquireExclusiveAsync` не вызывается:** вторая вкладка WebGL иначе стала бы read-only и не сохранила бы свой
  refresh-токен.
- **Сброс в редакторе** — `MirraCloud → Data → Clear Sign-In` (удаляет `mirracloud_prefs_editor`).
  `Edit → Clear All PlayerPrefs` на вход SDK больше не влияет.

## `IBlobStorage`

Трёхуровневый контракт, async на `System.Threading.Tasks.Task`:

```
IBlobStorage    — OpenContainerAsync / DeleteContainerAsync / ListContainersAsync
IBlobContainer  — ReadAsync / ExistsAsync / ReadManyAsync / ReadByPrefixAsync /
                  BeginWrite / DeleteByPrefixAsync / TryAcquireExclusiveAsync   (IDisposable)
IBlobWriteBatch — Put / Delete / CommitAsync
```

- **Контейнер** — единица владения (например `asset_cache`, `mirracloud_prefs`). Открытие refcounted: повторный `OpenContainerAsync(id)` отдаёт тот же инстанс, `Dispose` декрементит.
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

SQLite выполняет запросы в пуле потоков под семафором и отпускает его там же: закрытие соединения (`Dispose`)
ждёт семафор на вызывающем потоке, и освобождение, поставленное в очередь главного потока, повесило бы главный
поток. Колбэки `onBlob` при этом приходят в контекст вызывающего, как и раньше.

## Новый потребитель

1. Свой container-id + свой key-builder (ключи строятся только в нём).
2. `OpenContainerAsync(id)` из переданного в конструктор `IBlobStorage`; `Dispose` контейнера на teardown сервиса.
3. Запись через батч + `CommitAsync`; версионируемые данные — версия в ключе, прунинг старых через `DeleteByPrefixAsync`.

## Code
- `Runtime/Storage/Blob/*` — контракты (`IBlobStorage`/`IBlobContainer`/`IBlobWriteBatch`/`BlobResult`) + бэкенды SQLite / File / IndexedDB
- `Runtime/Storage/{IStorage,BlobKeyValueStorage,LocalDataContainers}.cs`
- `Runtime/Services/Auth/AuthStorageKeys.cs`, `Editor/ClearSavedSignInMenu.cs`

# AssetsStorage

`AssetsStorageService` — загрузка ассетов ветки из Cloud с локальным кешем.

## Методы

- `LoadConfigAsync()` → `AssetStorageStructureDto` — каталог ветки: все папки и ассеты (нужна сессия игрока)
- `GetAssetsFromType(assetType)` → `List<Asset>` — фильтр каталога по типу
- `TryGetAssetByPath(path, out Asset)` — ассет каталога по пути, без запроса
- `GetAssetsInFolder(folderPath, recursive = false)` → `List<Asset>` — ассеты папки каталога, без запроса;
  пустая строка или `/` — корень ветки

### Загрузка контента

Каждый тип — по `stableId` и по пути, с одинаковыми параметрами:

| По `stableId` | По пути | Результат |
|---|---|---|
| `LoadTextFromId(id, textFileType, useCache, access)` | `LoadTextFromPath(path, …)` | `TextFile` |
| `LoadTextureFromId(id, readable, useCache, access)` | `LoadTextureFromPath(path, …)` | `Texture2D` |
| `LoadSpriteFromId(id, readable, useCache, access)` | `LoadSpriteFromPath(path, …)` | `Sprite` |
| `LoadAudioFromId(id, audioType, useCache, access)` | `LoadAudioFromPath(path, …)` | `AudioClip` |
| `LoadAssetBundleFromId(id, useCache, access)` | `LoadAssetBundleFromPath(path, …)` | `AssetBundle` |

`useCache` по умолчанию `true`, `access` — `AssetAccess.Player`.

## Путь

Путь пишется так, как его показывает консоль: `icons/coin.png` и `/icons/coin.png` равнозначны, `\` читается
как `/`, лишние слеши отбрасываются. **Регистр важен** — сервер сравнивает путь точно, `Icons/Coin.png` ≠
`icons/coin.png`. Пробелы, кириллица, `#`, `?`, `%`, `+` в именах допустимы: SDK кодирует каждый сегмент пути
сам.

Пустой путь и `..` отклоняются без запроса — с тем же кодом, что ответил бы сервер,
`assets_storage.asset_path_invalid`.

После переименования или перемещения в консоли путь меняется, а `stableId` — нет: в сохранённых ссылках надёжнее
держать `stableId`.

## Доступ: `AssetAccess`

| | Что отдаёт | Без сессии игрока |
|---|---|---|
| `Player` (по умолчанию) | любой ассет ветки — приватный и публичный | 401 |
| `Public` | только опубликованное в консоли (само или через папку, `Asset.IsEffectivelyPublic`); приватный — 403 `assets_storage.asset_not_public` | работает |
| `Auto` | есть сессия — как `Player`, нет — как `Public` | работает для публичных |

«Есть сессия» — игрок вошёл или сохранённая сессия ещё восстанавливается после запуска: запрос в этот момент
дождётся восстановления и уйдёт от имени игрока.

`Auto` — для кода, который работает и до входа, и после: заставка, баннер главного меню, конфиг, нужный до
логина. Вошедший игрок при этом идёт своим маршрутом и своим лимитом запросов, а не общим анонимным.

## Ошибки

Неудачная загрузка возвращает ответ сервера: `HttpStatusCode` и коды в `Error.Errors`:

```csharp
var op = sdk.AssetsStorage.LoadTextureFromPath("banners/main.png", access: AssetAccess.Auto);
await op.Task();

if (op.Result.IsSuccess == false)
{
    if (op.Result.Error.HasCode(CloudErrorCodes.AssetsStorageAssetNotPublic)) { /* приватный, а игрок не вошёл */ }
    if (op.Result.Error.HasCode(CloudErrorCodes.AssetsStorageAssetNotFound)) { /* нет такого пути или id */ }
}
```

Файл пришёл, но не читается как запрошенный тип (не картинка, не бандл) — ошибка `Validation`, а
`HttpStatusCode` остаётся 2xx.

## Кеширование

Скачанные ассеты кешируются локально по `stableId + version` в контейнере `asset_cache` (см. [Storage](../Storage.md)). Повторная загрузка отдаётся с диска без обращения к сети — особенно важно на WebGL (без кеша ассеты качаются каждую сессию).

- `useCache` (по умолчанию `true`) — отключает кеш для конкретного вызова.
- Версия берётся из `Asset.Version`, поэтому нужен предварительный `LoadConfigAsync()`. Если версия неизвестна — ассет грузится напрямую, без кеша.
- Путь, который есть в загруженном каталоге, грузится по `stableId` этого ассета — с тем же кешем, что у `Load*FromId`. Пути, которого в каталоге нет (каталог не загружен или ассет новее), и вызов с `useCache: false` сервер разрешает сам, без кеша — так же обходится и каталог, загруженный до переименования.
- Кеш не зависит от `access`: публичный ассет, загруженный после `LoadConfigAsync()`, тоже берётся с диска. До входа игрока каталога нет — анонимные загрузки идут в сеть.
- Хранится только последняя версия (старые вычищаются при обновлении).
- texture / sprite / audio: на промахе типизированный `DownloadHandler` декодит объект на воркер-треде и попутно отдаёт сырые байты в кеш; на хите объект собирается из кешированных байт. text / bundle — сразу из сырых байт.
- Аудио на хите пересобирается через временный файл (native) или `blob:`-URL (WebGL).
- Ключ записи — `{projectId}/{branch}/{stableId}/v{version}` (`AssetCacheKeys`). В редакторе содержимое кеша показывает `MirraCloud → Data → Show Cache`, удаляет — `MirraCloud → Data → Clear Cache`. Контейнер общий с standalone-билдом того же проекта на этой машине: очистка задевает и его.

## Свойства

- `Assets` — `IReadOnlyList<Asset>` загруженные ассеты
- `Folders` — `IReadOnlyList<Folder>` структура папок

## Code
- `Packages/com.mirrahub.cloud-sdk/Runtime/Services/Asset Storage/*`

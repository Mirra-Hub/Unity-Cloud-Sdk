# AssetsStorage

`AssetsStorageService` — загрузка ассетов из Cloud с локальным кешем.

## Методы

- `LoadConfigAsync()` → `AssetStorageStructureDto` — загрузка структуры хранилища
- `GetAssetsFromType(assetType)` → `List<Asset>` — фильтрация по типу

### Загрузка контента
- `LoadTextFromId(id, textFileType, useCache = true)` → `TextFile`
- `LoadTextureFromId(id, readable, useCache = true)` → `Texture2D`
- `LoadSpriteFromId(id, readable, useCache = true)` → `Sprite`
- `LoadAudioFromId(id, audioType, useCache = true)` → `AudioClip`
- `LoadAssetBundleFromId(id, useCache = true)` → `AssetBundle`

## Кеширование

Скачанные ассеты кешируются локально по `id + version` в контейнере `asset_cache` (см. [Storage](../Storage.md)). Повторная загрузка отдаётся с диска без обращения к сети — особенно важно на WebGL (без кеша ассеты качаются каждую сессию).

- `useCache` (по умолчанию `true`) — отключает кеш для конкретного вызова.
- Версия берётся из `Asset.Version`, поэтому нужен предварительный `LoadConfigAsync()`. Если версия неизвестна — ассет грузится напрямую, без кеша.
- Хранится только последняя версия (старые вычищаются при обновлении).
- texture / sprite / audio: на промахе типизированный `DownloadHandler` декодит объект на воркер-треде и попутно отдаёт сырые байты в кеш; на хите объект собирается из кешированных байт. text / bundle — сразу из сырых байт.
- Аудио на хите пересобирается через временный файл (native) или `blob:`-URL (WebGL).

## Свойства

- `Assets` — `IReadOnlyList<Asset>` загруженные ассеты
- `Folders` — `IReadOnlyList<Folder>` структура папок

## Code
- `Core/Services/Asset Storage/*`

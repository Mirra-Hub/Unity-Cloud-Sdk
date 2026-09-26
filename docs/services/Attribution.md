# Attribution

`AttributionService` — внешние идентификаторы установки (сейчас — Adjust), записанные на аккаунт игрока.
По ним в консоли видно, откуда пришёл игрок, а аккаунт находится поиском по adid из кабинета Adjust.

SDK **не зависит** от Adjust SDK: игра передаёт то, что Adjust отдаёт ей в колбэках.

## Что нужно в проекте

Включённая интеграция типа **Adjust** в консоли (раздел «Интеграции»); ключ интеграции значения не имеет.
Без неё сервер отвечает 403 `player_accounts.external_integration_unavailable`, и сервис до следующего
запуска игры больше ничего не шлёт.

## Два способа отправки

| Метод | Когда |
|---|---|
| `ReportAdjustAdid(adid)` / `ReportAdjustAttribution(AdjustAttributionDto)` | **основной**. Вызывайте из колбэков Adjust когда угодно и в любом порядке, до входа и после. Сервис держит данные, пока нет **и** сессии игрока, **и** adid, потом отправляет один раз |
| `LinkAdjustAsync(AdjustAttributionDto)` → `ExternalIdDto` | «голый» вызов `PUT …/players/external/v1/projects/{projectId}/adjust`: нужна сессия и adid прямо сейчас, ничего не откладывается и не повторяется (отказ и сбой сети — тоже; обновление отвергнутой сессии — как везде) |
| `GetMyExternalIdsAsync()` → `List<ExternalIdDto>` | что записано на аккаунт (`GET …/me`), последние сверху |

## Подключение (Adjust Unity SDK v5)

```csharp
var config = new AdjustConfig(appToken, AdjustEnvironment.Production);
config.AttributionChangedDelegate = attribution =>
    Sdk.Attribution.ReportAdjustAttribution(new AdjustAttributionDto
    {
        TrackerToken = attribution.TrackerToken,
        TrackerName = attribution.TrackerName,
        Network = attribution.Network,
        Campaign = attribution.Campaign,
        Adgroup = attribution.Adgroup,
        Creative = attribution.Creative,
        ClickLabel = attribution.ClickLabel,
    });
Adjust.InitSdk(config);

Adjust.GetAdid(adid => Sdk.Attribution.ReportAdjustAdid(adid));
```

Имена — из Adjust Unity SDK v5; в v4 поля атрибуции пишутся с маленькой буквы, а adid отдаёт `Adjust.getAdid()`.
Если колбэк атрибуции уже несёт adid, положите его в `AdjustAttributionDto.Adid` — это то же, что `ReportAdjustAdid`.

**Потоки.** `ReportAdjustAdid` / `ReportAdjustAttribution` можно вызывать из любого потока: Adjust SDK v4 на Android
зовёт колбэки из Java-потока, а запрос можно начать только в главном потоке Unity. Вызов не из главного потока
переносится в него (через `SynchronizationContext`, захваченный при `Initialize()`), атрибуция копируется в момент
вызова. `LinkAdjustAsync` и `GetMyExternalIdsAsync` — только из главного потока, как любые вызовы SDK.

## Как работает отложенная отправка

Adjust отдаёт adid и атрибуцию разными колбэками, часто раньше, чем игрок вошёл, и в любом порядке. Поэтому:

- **Нет adid** — атрибуция хранится до его прихода (`WaitingForAdid`).
- **Нет сессии** — отчёт ждёт входа или восстановления сессии при запуске (`WaitingForSession`).
- **Отправка — одна** на изменение: новые данные от игры или новый вход (возможно, в другой аккаунт; для того
  же аккаунта повтор безвреден — сервер делает идемпотентный upsert). Те же значения повторно не шлются.
- Одновременно в полёте один запрос; то, что пришло во время него, уходит сразу после.
- **Не дошло** (нет сети, 5xx, сессию не удалось обновить, запрос не удалось даже начать) — `Failed`, повтор на
  следующем входе, обновлении сессии или новом `Report*`. Таймеров и циклов повторов нет; сам запрос уходит без
  общего повтора SDK. Ответ 2xx, который не удалось прочитать, считается записанным (`Sent`).
- **Окончательный отказ** — `Rejected`, повтора нет до новых данных или нового входа:
  409 `external_id_conflict` (adid уже на другом аккаунте проекта), 400/422 (кривые данные), 404 (аккаунта нет).
- **403 `external_integration_unavailable`** — в проекте нет включённого Adjust: до следующего запуска не шлём.

Отчёт только с adid **не стирает** атрибуцию, записанную раньше; отчёт с любым полем атрибуции заменяет её
целиком (переатрибуция не должна унаследовать креатив прошлой кампании). Сервис отправляет последнюю
атрибуцию вместе с adid, поэтому порядок колбэков неважен.

## Состояние

| | |
|---|---|
| `AdjustReportState` | `Idle` → `WaitingForAdid` / `WaitingForSession` → `Sending` → `Sent` / `Failed` / `Rejected` |
| `LastAdjustReport` | ответ сервера на последний отложенный отчёт (`RestApiResult<ExternalIdDto>`) |
| `OnAdjustReported` | событие после каждого отложенного отчёта — успешного или нет |

```csharp
Sdk.Attribution.OnAdjustReported += result =>
{
    if (result.Error.HasCode(CloudErrorCodes.PlayerAccountsExternalIdConflict))
    {
        // adid уже записан на другой аккаунт проекта — атрибуция остаётся за первым
    }
};
```

## Один adid — один аккаунт

Adid принадлежит тому аккаунту проекта, который сообщил его первым. Если на том же устройстве войти в другой
аккаунт обычным входом (не через привязку), его отчёт получит 409 и атрибуция останется за первым. Переезжает
adid только при разрешении конфликта привязки — там игрок доказал, что владеет обоими аккаунтами.

## ExternalIdDto

| Поле | |
|---|---|
| `ProviderKey` | `adjust` |
| `ExternalId` | adid |
| `Source` | `sdk` (отчёт игры), `admin`, `callback` |
| `FirstSeenAt` / `LastSeenAt` | UTC; повтор сдвигает только `LastSeenAt` |
| `Adjust` | атрибуция (`TrackerToken`, `TrackerName`, `Network`, `Campaign`, `Adgroup`, `Creative`, `ClickLabel`); `null` для других провайдеров, поля `null`, пока установка органическая или ещё не атрибутирована |

## Ошибки

| Код | HTTP | Когда |
|---|---|---|
| `player_accounts.external_id_required` | 400 | нет adid (`data.field`) |
| `player_accounts.external_field_invalid` | 422 | adid с пробелами/управляющими символами или длиннее 128; поле атрибуции длиннее 512 (`data.field`, `data.reason`, `data.maxLength`) |
| `player_accounts.external_integration_unavailable` | 403 | нет включённой интеграции Adjust (`data.reason`: `not_configured` / `disabled`) |
| `player_accounts.external_id_conflict` | 409 | adid записан на другой аккаунт проекта; окончательно |
| `player_accounts.account_not_found` | 404 | аккаунта сессии нет (например, удалён) |
| `common.unauthorized` | 401 | нет сессии |

403 `external_integration_unavailable` — отказ эндпоинта, а не сессии: SDK на нём сессию не обновляет.

## Code
- `Runtime/Services/Attribution/*`

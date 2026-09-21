# Auth

`AuthenticationService` — аутентификация и управление сессиями.

## Платформа

Каждая сборка игры — это одна платформа проекта (консоль Cloud → «Платформы»). Её **ключ** лежит в
`Configuration.PlatformKey`; выбирается в `Tools → Mirra Cloud → Manager` (дропдаун **Platform**). Ключ
регистрозависимый. Игра, которая выходит на несколько платформ, переключает его перед каждой сборкой.

- SDK шлёт ключ заголовком `PlatformKey` на **каждом вызове входа**: все `Login*`, начало OpenID-входа
  (`LoginOpenIdAsync` / `BeginOpenIdLoginUrlAsync` / `StartOpenIdLoginAsync`) и `GetLoginMethodsAsync`. Эти вызовы
  анонимные, интерцепторы на них молчат, поэтому заголовок ставится в самом `AuthenticationService`.
- Refresh и logout заголовка не шлют: платформу сервер берёт из сессии.
- Link (`Link*`, `ResolveLinkConflictAsync`) идёт на платформе **текущей сессии** — той, на которой игрок вошёл;
  шлюз подменяет заголовок значением из токена.
- Unlink платформу не требует (кроме `UnlinkPlatformAsync`, где ключ — часть адреса записи).
- Тот же ключ — сегмент пути аналитики (см. [Analytics](Analytics.md)).
- Пустой `PlatformKey` → SDK один раз пишет ошибку в лог; сервер отвечает `platforms.platform_key_required`.

Платформа определяет, какие способы входа доступны. Отказы (все — 403; `platforms.*` приходят ещё до эндпоинта,
остальные — до того, как что-то записано):

| Код (`CloudErrorCodes`) | Когда |
|---|---|
| `platforms.platform_not_configured` (`PlatformsPlatformNotConfigured`) | в проекте нет ни одной платформы |
| `platforms.platform_key_required` (`PlatformsPlatformKeyRequired`) | ключ не задан; на link — сессия выдана до того, как сессии получили платформу (лечится новым входом) |
| `platforms.platform_unknown` (`PlatformsPlatformUnknown`) | в проекте нет платформы с таким ключом (`data.platformKey`) |
| `platforms.platform_disabled` (`PlatformsPlatformDisabled`) | платформа выключена в консоли |
| `player_accounts.provider_not_on_platform` | этого способа входа на платформе нет (`data.kind`, `data.integrationKey`) |
| `player_accounts.provider_disabled_on_platform` | способ входа на платформе есть, но выключен |
| `player_accounts.auth_integration_unavailable` | интеграция за способом входа выключена или удалена |
| `player_accounts.platform_marketplace_provider_missing` | `LoginPlatformAsync` на платформе без входа через магазин |

Эти отказы окончательные: SDK на них сессию не обновляет (см. «Сессии»).

Правила пароля тоже задаются на платформе (на её входе по email / username) и проверяются, только когда пароль
создаётся — при регистрации (`Login*` с `createAccount`) и привязке (`Link*`), но не при входе: 422
`player_accounts.password_too_short` (`data.minLength`) или `player_accounts.password_pattern_mismatch`.

## Методы входа

| Метод | Описание |
|-------|----------|
| `GetLoginMethodsAsync()` | Какие способы входа включены на платформе сборки (см. ниже) |
| `LoginGuestAsync(createAccount)` | Гостевой вход |
| `LoginDeviceAsync(deviceId, createAccount)` | По ID устройства |
| `LoginEmailAsync(email, password, createAccount)` | По email/пароль |
| `LoginUsernameAsync(username, password, createAccount)` | По username/пароль |
| `LoginGoogleSignInAsync` / `LoginSignInWithAppleAsync` / `LoginYandexSignInAsync(externalUserId, idToken, authCode, extra, createAccount)` | Google / Apple / Yandex ID через их нативный SDK (токен или код); игрок — проверенный `sub`, `externalUserId` сервер не читает |
| `LoginPlatformAsync(extra, authCode, platformToken, externalUserId, createAccount)` | Вход через магазин платформы: Google Play Games, VK Games, Яндекс Игры, Game Center |
| `LoginOpenIdAsync(providerKey, options)` | Вход через страницу провайдера: OpenID, а также Google / Apple / Yandex ID без нативного SDK (см. ниже) |

Все Login-методы возвращают `AsyncOperation<RestApiResult<GetAuthDataDto>>`.
Параметр `createAccount` (по умолчанию `true` для Login, `false` для Link) — создать аккаунт, если не существует.

### GetLoginMethodsAsync — кнопки входа

`GetLoginMethodsAsync()` → `RestApiResult<LoginMethodsDto>`: `PlatformKey` и `Methods` — включённые способы входа в
порядке, заданном в консоли. Сессия не нужна. У каждого `LoginMethodDto`:

- `Kind` (`LoginMethodKind`): `Guest`, `Device`, `Email`, `Username`, `OpenId`, `Google`, `Apple`, `Yandex`,
  `GooglePlay`, `VkGames`, `YandexGames`, `AppleGameCenter`; `Unknown` — вид, которого эта версия SDK ещё не знает
  (исходное значение — в `KindKey`), список при этом не ломается.
- `IntegrationKey` — для `OpenId` / `Google` / `Apple` / `Yandex`: ключ для `LoginOpenIdAsync`. OpenID-провайдеров на
  платформе может быть несколько — ключ их различает.
- `DisplayName` — для `OpenId`: подпись кнопки.
- `IsStore` — вход через магазин (`LoginPlatformAsync`).

```csharp
var op = sdk.Authentication.GetLoginMethodsAsync();
await op.Task();
if (op.Result.IsSuccess == false)
{
    // platforms.platform_not_configured / platform_key_required / platform_unknown / platform_disabled
    Debug.LogError(op.Result.Error.FirstCloudError()?.Code);
    return;
}

foreach (var method in op.Result.Data.Methods)
{
    switch (method.Kind)
    {
        case LoginMethodKind.Guest: AddButton("Guest", () => sdk.Authentication.LoginGuestAsync()); break;
        case LoginMethodKind.OpenId:
        case LoginMethodKind.Google:
            AddButton(method.DisplayName ?? method.Kind.ToString(),
                () => sdk.Authentication.LoginOpenIdAsync(method.IntegrationKey, new OpenIdLoginOptions { UseInAppWebView = true }));
            break;
        // …
    }
}
```

### LoginPlatformAsync — что передавать

Платформа в вызове не передаётся: магазин — единственный вход через магазин у платформы сборки
(`Configuration.PlatformKey`). Игрок — всегда id, который сервер проверил по подписи/коду; `externalUserId` читает
только Game Center (он входит в подписанные данные).

| Магазин на платформе | `extra` | `authCode` | `externalUserId` |
|---|---|---|---|
| Google Play Games | — | server auth code | — |
| VK Games | launch params целиком (включая `sig` и `user_id`) | — | — |
| Яндекс Игры | подписанные параметры (включая `signature` и `player_id`) | — | — |
| Game Center | `publicKeyUrl`, `signature`, `salt`, `timestamp` | — | `teamPlayerID` |

`platformToken` сейчас не читает ни один магазин.

## OpenID и вход через страницу провайдера

`providerKey` — `IntegrationKey` метода `OpenId` / `Google` / `Apple` / `Yandex` из `GetLoginMethodsAsync()`
(ключ интеграции в консоли). Неизвестный платформе ключ → 403 `player_accounts.provider_not_on_platform`.

- `LoginOpenIdAsync(providerKey, options)` — полный вход: открыть страницу провайдера, дождаться возврата, получить сессию.
- `BeginOpenIdLoginUrlAsync(providerKey, successUrl)` — получить URL страницы провайдера (низкоуровневый шаг 1).
- `StartOpenIdLoginAsync(providerKey, successUrl)` / `CompleteOpenIdLoginAsync(openIdKey)` — двухшаговый flow.

### OpenIdLoginOptions

| Поле | Default | Описание |
|------|---------|----------|
| `LoopbackPort` | `0` | Порт локального HTTP-приёмника на Editor/Standalone (0 = авто) |
| `MobileDeepLinkUrl` | `null` | Deep-link схема для Android/iOS (например `myapp://mirra-openid`) |
| `UseInAppWebView` | `false` | Открыть OAuth-страницу во встроенном WebView вместо системного браузера |
| `WebViewCallbackUrl` | `https://mirra-openid.local/callback` | URL, который WebView перехватит для извлечения `mirra_openid_key` |

### Режим in-app WebView

Если `UseInAppWebView = true`, `LoginOpenIdAsync` открывает OAuth-страницу внутри приложения (через `WebViewService`, обёртка над `gree/unity-webview`), без выхода пользователя в системный браузер и **без необходимости настраивать deep-link схему на мобилках**.

Требования:
- `WebViewCallbackUrl` должен быть добавлен в список допустимых `successUrl` интеграции провайдера (консоль → «Интеграции»).
- `WebViewService` должен быть инициализирован (инициализируется автоматически при `MirraCloudSDK.Initialize()`).
- Поддерживаемые платформы: Editor, Standalone (Windows/macOS), Android, iOS. На WebGL и Editor/Linux режим вернёт ошибку валидации.

Пример:
```csharp
var options = new OpenIdLoginOptions { UseInAppWebView = true };
var op = MirraCloudSDK.Authentication.LoginOpenIdAsync(method.IntegrationKey, options);
```

**Замечание:** низкоуровневый `StartOpenIdLoginAsync(providerKey, successUrl)` игнорирует `UseInAppWebView` и всегда открывает системный браузер.

## Привязка аккаунтов (Link)

Аналогично Login-методам, но с префиксом `Link*` и `createAccount = false` по умолчанию:

| Метод | Описание |
|-------|----------|
| `LinkGuestAsync(createAccount)` | Привязать гостя |
| `LinkDeviceAsync(deviceId, createAccount)` | Привязать устройство |
| `LinkEmailAsync(email, password, createAccount)` | Привязать email |
| `LinkUsernameAsync(username, password, createAccount)` | Привязать username |
| `LinkGoogleSignInAsync` / `LinkSignInWithAppleAsync` / `LinkYandexSignInAsync(externalUserId, idToken, authCode, extra, createAccount)` | Привязать Google / Apple / Yandex ID |
| `LinkPlatformAsync(extra, authCode, platformToken, externalUserId, createAccount)` | Привязать вход через магазин платформы текущей сессии |
| `LinkOpenIdAsync(userId, createAccount)` | Привязать кастомный OpenID-аккаунт |

Привязка идёт на платформе текущей сессии: способ входа, которого на ней нет, сервер отклонит
(`player_accounts.provider_not_on_platform`).

`ResolveLinkConflictAsync(LinkAuthProviderDto dto)` — разрешение конфликта при привязке. Заполните `ProviderType`, `TargetAccountId` и поля своего провайдера (`GuestId`, `DeviceId`, `Email`/`Password`, `Login`/`Password`, `UserId`; для магазина — `ExternalUserId` / `AuthCode` / `Extra`). Платформу DTO не называет: конфликт магазина разрешается на платформе сессии.

## Отвязка (Unlink)

Успешная отвязка отзывает все сессии аккаунта — SDK чистит локальную сессию и поднимает `OnSessionExpired`.

| Метод | Описание |
|-------|----------|
| `UnlinkPlatformAsync(platformKey, externalUserId)` | Убрать вход через магазин: ключ платформы, на которой он был сделан, и id игрока в магазине. Работает и для выключенной с тех пор платформы |
| `UnlinkGoogleSignInAsync(externalUserId)` / `UnlinkSignInWithAppleAsync(externalUserId)` / `UnlinkYandexSignInAsync(externalUserId)` | Убрать вход Google / Apple / Yandex ID по id пользователя у провайдера (`sub`); токен не нужен |
| `UnlinkOpenIdAsync(userId)` | Убрать OpenID-вход |

Убрать можно только вход своего аккаунта и не последний (`player_accounts.last_auth_method`).

## Сессии

- `InitializeAsync()` — инициализация с сохранённым refresh token
- `RefreshSessionAsync()` — обновление сессии. Один запрос на refresh token: вызовы, пришедшие во время обновления (например, несколько одновременных 401), ждут его результата, а не тратят тот же токен повторно — сервер отклонил бы повтор и разлогинил игрока. Неудачный refresh завершает сессию (`OnSessionExpired`); исключение — refresh после смены профиля (`PlayerAccount.SelectProfileAsync`): его сетевой сбой или 5xx игрока не разлогинивает, отказ сервера (4xx) — разлогинивает как обычно.
- Автоматический refresh на 401/403: вызов с токеном SDK повторяет один раз после refresh сессии, только если отказ касается самой сессии — ответ шлюза без кода ошибки (нет/битый/просроченный JWT) или код `common.unauthorized`, `purchases.selected_profile_required`, `player_accounts.session_expired` / `session_mismatch` / `session_project_mismatch`. Отказ самого эндпоинта (`player_accounts.invalid_credentials`, `external_auth_invalid_id_token`, `provider_not_on_platform`, `avatar_change_disabled`, `common.forbidden`, отказы платформы `platforms.*` и любой другой код) возвращается сразу: без refresh и без повторной отправки. Вызовы входа (`NoAuth`) сессию не обновляют, как и раньше.
- `LogoutAsync()` — выход из текущей сессии
- `LogoutAllAsync()` — выход из всех сессий

## Свойства

- `AuthToken` — текущий токен
- `SessionId` — ID текущей auth-сессии (не путать с игровой сессией аналитики `Analytics.SessionId`)
- `IsAuth` — авторизован ли

## События

- `OnLogin` — успешный вход (и успешная привязка провайдера / разрешение конфликта)
- `OnAuthConflict` — конфликт при привязке
- `OnSessionRefreshed` — сессия обновлена: восстановление в `InitializeAsync` или refresh после 401
- `OnSessionExpired` — сессия закончилась (выход, `ClearLocalSession`, unlink, неудачный refresh)

## Code
- `Packages/com.mirrahub.cloud-sdk/Runtime/Services/Auth/*`

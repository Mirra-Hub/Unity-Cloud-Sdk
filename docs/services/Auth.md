# Auth

`AuthenticationService` — аутентификация и управление сессиями.

## Методы входа

| Метод | Описание |
|-------|----------|
| `LoginGuestAsync(createAccount)` | Гостевой вход |
| `LoginDeviceAsync(deviceId, createAccount)` | По ID устройства |
| `LoginEmailAsync(email, password, createAccount)` | По email/пароль |
| `LoginUsernameAsync(username, password, createAccount)` | По username/пароль |
| `LoginPlatformAsync(platformId, externalUserId, authCode, platformToken, extra, createAccount)` | Универсальный вход через `Platform` (Google Play, Apple Game Center, Sign In With Apple, Google Sign-In, VK Games, Yandex.Games, Yandex ID) |
| `LoginOpenIdAsync(providerId, options)` | Кастомный OpenID-провайдер (см. ниже) |

Все Login-методы возвращают `AsyncOperation<RestApiResult<GetAuthDataDto>>`.
Параметр `createAccount` (по умолчанию `true` для Login, `false` для Link) — создать аккаунт, если не существует.

### LoginPlatformAsync — что передавать

`platformId` — ObjectId платформы из проекта (создаётся в админке Cloud → Platforms; маркетплейс платформы определяет verifier на бэкенде).

Остальные поля заполняются в зависимости от `MarketplaceType` платформы:

| MarketplaceType | `externalUserId` | `authCode` | `platformToken` | `extra` |
|---|---|---|---|---|
| `GooglePlay` (GPGS) | Google Player ID | — | server auth code (или ID token) | — |
| `GoogleSignIn` | — (берётся из `sub` ID-токена) | — | Google ID token | — |
| `AppleGameCenter` | `teamPlayerID` | — | — | `publicKeyUrl`, `signature`, `salt`, `timestamp` |
| `SignInWithApple` | — (берётся из JWT `sub`) | — | identity token (JWT) | — |
| `VkGames` | VK `user_id` | — | — | launch params (включая `sig`) |
| `YandexGames` | Yandex player id | — | platform signed token | — |
| `Yandex` (Yandex ID OAuth) | — (берётся из user info) | OAuth code от passport.yandex.ru | — | — |

## OpenID

- `LoginOpenIdAsync(providerId, options)` — вход через кастомный OpenID-провайдер, настроенный в админке проекта.
- `BeginOpenIdLoginUrlAsync(providerId, successUrl)` — получить URL для OpenID (низкоуровневый шаг 1).
- `StartOpenIdLoginAsync(providerId, successUrl)` / `CompleteOpenIdLoginAsync(openIdKey)` — двухшаговый OpenID flow.

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
- `WebViewCallbackUrl` должен быть добавлен в список допустимых `successUrl` на бэкенде для соответствующего OpenID-провайдера проекта.
- `WebViewService` должен быть инициализирован (инициализируется автоматически при `MirraCloudSDK.Initialize()`).
- Поддерживаемые платформы: Editor, Standalone (Windows/macOS), Android, iOS. На WebGL и Editor/Linux режим вернёт ошибку валидации.

Пример:
```csharp
var options = new OpenIdLoginOptions { UseInAppWebView = true };
var op = MirraCloudSDK.Authentication.LoginOpenIdAsync(providerId, options);
```

**Замечание:** низкоуровневый `StartOpenIdLoginAsync(providerId, successUrl)` игнорирует `UseInAppWebView` и всегда открывает системный браузер.

## Привязка аккаунтов (Link)

Аналогично Login-методам, но с префиксом `Link*` и `createAccount = false` по умолчанию:

| Метод | Описание |
|-------|----------|
| `LinkGuestAsync(createAccount)` | Привязать гостя |
| `LinkDeviceAsync(deviceId, createAccount)` | Привязать устройство |
| `LinkEmailAsync(email, password, createAccount)` | Привязать email |
| `LinkUsernameAsync(username, password, createAccount)` | Привязать username |
| `LinkPlatformAsync(platformId, externalUserId, authCode, platformToken, extra, createAccount)` | Привязать аккаунт через `Platform` |
| `LinkOpenIdAsync(userId, createAccount)` | Привязать кастомный OpenID-аккаунт |

`ResolveLinkConflictAsync(LinkAuthProviderDto dto)` — разрешение конфликта при привязке. Для Platform-провайдера заполните `dto.PlatformId`, `dto.ExternalUserId`, при необходимости `AuthCode` / `PlatformToken` / `Extra`.

## Сессии

- `InitializeAsync()` — инициализация с сохранённым refresh token
- `RefreshSessionAsync()` — обновление сессии
- `LogoutAsync()` — выход из текущей сессии
- `LogoutAllAsync()` — выход из всех сессий

## Свойства

- `AuthToken` — текущий токен
- `SessionId` — ID текущей сессии
- `IsAuth` — авторизован ли

## События

- `OnLogin` — успешный вход
- `OnAuthConflict` — конфликт при привязке
- `OnSessionRefreshed` — сессия обновлена
- `OnSessionExpired` — сессия истекла

## Code
- `Core/Services/Auth/*`

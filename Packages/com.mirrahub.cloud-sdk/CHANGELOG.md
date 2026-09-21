# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[SemVer](https://semver.org/).

The SDK is `0.x`: the public API can change between minor versions. Breaking changes are marked
**Breaking**.

## [Unreleased]

Sign-in now happens on a **platform** of the project (console → Platforms): the platform decides
which sign-in methods a player gets, and the SDK names it with every sign-in. Upgrade steps: create a
platform in the console, switch on its sign-in methods, pick it in `Tools → Mirra Cloud → Manager`
(new **Platform** dropdown), then fix the compile errors from the breaking changes below.

Purchases are priced per **payment integration** (console → Integrations) instead of per provider config
of a branch: a catalog price carries the integration's key, and that key is what a purchase is started
with. Upgrade steps: recreate the Stripe / YooKassa / VK credentials as integrations, point the product
prices at them, and pass `price.IntegrationKey` where the game passed `price.ProviderConfigId`.

The new **Attribution** service records the install's Adjust id and campaign on the player's account.

### Added

- **`Attribution` service** — the ids install-attribution SDKs know the install by, recorded on the
  player's account (today: Adjust — the adid plus the campaign Adjust attributed the install to). The
  project needs an enabled Adjust integration. The SDK does not depend on the Adjust SDK; the game hands
  over what Adjust gives it:
  - `ReportAdjustAdid(adid)` / `ReportAdjustAttribution(AdjustAttributionDto)` — call them from Adjust's
    callbacks at any time, in any order, signed in or not. Adjust often hands out the attribution before
    the adid and both before sign-in, so the service keeps what it got until there is both a player
    session and the adid, then sends it once. It is sent again after a sign-in (possibly another
    account) and when something new is handed over; a report that did not get through goes out again
    on the next sign-in or session refresh — no timers. A refusal resending cannot change (409
    `PlayerAccountsExternalIdConflict` — the adid is on another account of the project; 400 / 422 / 404)
    is not resent, and a project without an enabled Adjust integration (403
    `PlayerAccountsExternalIntegrationUnavailable`) gets nothing more until the next launch.
    `AdjustReportState`, `LastAdjustReport` and `OnAdjustReported` say where it stands.
  - `LinkAdjustAsync(AdjustAttributionDto)` — the bare `PUT …/players/external/v1/projects/{projectId}/adjust`,
    for a game that manages the timing itself.
  - `GetMyExternalIdsAsync()` — what is recorded on the signed-in account (`ExternalIdDto`: provider,
    id, source, first / last seen, the Adjust attribution).
- **`PurchaseResult.ApiError`** — the server's refusal when `BuyAsync` could not start the order, so a
  game can dispatch on its code (`ApiError.HasCode(…)`). The failure text is now `code — message`
  instead of the HTTP status line.
- **`CloudErrorCodes`** for the new purchase and attribution refusals:
  `PurchasesIntegrationKeyRequired`, `PurchasesPaymentIntegrationUnavailable` (409 — the price's
  integration is gone, switched off or cannot take payments: reload the catalog),
  `PurchasesProviderMappingIntegrationNotPayment`, `PurchasesStripeWebhookSecretMissing`,
  `PlayerAccountsExternalIdRequired`, `PlayerAccountsExternalFieldInvalid`,
  `PlayerAccountsExternalIdConflict`, `PlayerAccountsExternalIntegrationUnavailable`, and the console
  player-list filter codes `PlayerAccountsPlatformKeyInvalid`, `PlayerAccountsPlatformKeysTooMany`.
- **`Authentication.GetLoginMethodsAsync()`** — the sign-in methods the build's platform offers right
  now, in the order set in the console, so a game draws only buttons that work. Needs no session.
  Returns `LoginMethodsDto { PlatformKey, Methods }`; each `LoginMethodDto` has `Kind`
  (`LoginMethodKind`: `Guest`, `Device`, `Email`, `Username`, `OpenId`, `Google`, `Apple`, `Yandex`,
  `GooglePlay`, `VkGames`, `YandexGames`, `AppleGameCenter`, or `Unknown` for a kind a newer server
  adds — the raw value stays in `KindKey`), `IntegrationKey` (for `OpenId` / `Google` / `Apple` /
  `Yandex`: the key for `LoginOpenIdAsync`) and `DisplayName` (for `OpenId`: the button label).
- **Platform dropdown in `Tools → Mirra Cloud → Manager`.** It lists the project's platforms and writes
  the chosen key into `Configuration.PlatformKey`. It needs the service account to hold
  `platforms.viewer`; a project without platforms gets a warning, because it refuses every sign-in.
- **`CloudErrorCodes`** for sign-in on a platform — `PlatformsPlatformKeyRequired`,
  `PlatformsPlatformUnknown`, `PlatformsPlatformDisabled`, `PlatformsPlatformNotConfigured` (all 403),
  `PlayerAccountsProviderNotOnPlatform`, `PlayerAccountsProviderDisabledOnPlatform`,
  `PlayerAccountsAuthIntegrationUnavailable`, `PlayerAccountsPlatformMarketplaceProviderMissing` — for
  password rules set on the platform (`PlayerAccountsPasswordTooShort`,
  `PlayerAccountsPasswordPatternMismatch`, `PlayerAccountsPasswordPatternInvalid`), for unlinking a store
  sign-in (`PlayerAccountsPlatformKeyRequired`), for analytics (`GameAnalyticsInvalidPlatformKey`) and the
  rest of the Platforms console catalogue (keys, platform types, sign-in / payment / integration links,
  legal info). Purchases codes the mirror had missed are in too (`PurchasesBranchNotEditable`,
  `PurchasesProviderMappingNotInBranch`, `PurchasesPurchaseConfigNotInBranch`,
  `PurchasesProviderMappingReferenceMissing`).
- **`CloudErrorCodes` covers the whole backend catalogue.** The codes the mirror had missed are in:
  new sections `CloudActions*`, `CloudServices*`, `Organizations*`, `Projects*`, `PromoCodes*`, and
  the key / branch codes of existing ones (`*BranchNotEditable`, `*InvalidKey`, `*KeyAlreadyExists`,
  `*KeyImmutable`, `*NotInBranch` for AB tests, challenges, chats, daily rewards, economy,
  leaderboards, profanity filter, rules, segments and tournaments; `Deployment` scope codes;
  `AssetsStorageAssetReferencedByEntities`, `ProjectStatisticsPeriodTooLong`, `TariffsPlan*`).
- **`LinkAuthProviderDto`** carries the credentials of every provider (`GuestId`, `DeviceId`, `Email`,
  `UserId`, `Login`, `Password`), so `ResolveLinkConflictAsync` can resolve more than store conflicts.
- **`CloudErrorCodes`** mirrors the new Integrations module (`Integrations*`: key, field and usage
  errors from the console API, plus `IntegrationsIntegrationTypeMismatch` and
  `IntegrationsSecretUnreadable`, which reach a game only through another module that reads an
  integration, e.g. a purchase) and the new sign-in and account codes:
  `PlayerAccountsAuthCodeRequired`, `PlayerAccountsIdTokenRequired`, `PlayerAccountsSessionIdInvalid`,
  `PlayerAccountsAccountIdInvalid`, `PlayerAccountsProfileIdInvalid`, `PlayerAccountsFileRequired`,
  `PlayerAccountsAvatarChangeDisabled`. Two older codes the mirror had missed are in too:
  `PlayerAccountsBranchNotEditable`, `PlayerAccountsAccountOptionInvalid`.

### Changed

- **Breaking — purchases are priced per payment integration.** `CatalogPriceDto.ProviderConfigId` is
  now `IntegrationKey` (the key of the integration in the console), and the purchase calls take it:
  `InitiatePurchaseAsync(purchaseKey, integrationKey, successRedirectUrl, cancelRedirectUrl)`,
  `BuyAsync(purchaseKey, integrationKey, options)`. `InitiatePurchaseRequestDto.ProviderConfigId` is
  `IntegrationKey` too. `CatalogPriceDto.MappingId` is now the price's stable key
  `{purchaseKey}@{integrationKey}` (it was a version id) and stays informational; `ProviderName` is
  the integration's name. The catalog lists only prices whose integration exists, is switched on and
  can take payments, and drops a product left with none. A price at a store integration (VK Games,
  Google Play) is paid in the store: starting an order for it is refused with 422
  `purchases.provider_unsupported`.
- **Showcase:** the Purchases screen starts orders by integration key (store prices get no start
  button) and explains the integration refusals; a new **Attribution** screen shows what the account
  has recorded, where the queued Adjust report stands, and both ways to send one.
- **Breaking — `Configuration.PlatformKey` replaces `Configuration.AnalyticsPlatformId`.** One field
  for both uses: the SDK sends it in the `PlatformKey` header of every sign-in call (`Login*`, the start
  of an OpenID sign-in, `GetLoginMethodsAsync`) and puts it in the analytics routes. It holds the
  platform's **key** from the console, not an id. The old value is **not carried over**: it was the
  platform's internal id, which no route accepts any more, so every `Configuration.asset` starts with an
  empty key — pick the platform in the Manager (it fills the key in for you when it loads the project's
  platforms). While it is empty the SDK logs one error, the server refuses sign-in with
  `platforms.platform_key_required`, and analytics sends nothing. Refresh and logout do not send it (the
  server takes the platform from the session); link goes on the session's platform.
- **Breaking — `LoginPlatformAsync` and `LinkPlatformAsync` take no platform id.** The store is the one
  of the build's platform (link: of the session's platform). The parameters are reordered so that an
  old call fails to compile instead of shifting its arguments: `LoginPlatformAsync(extra, authCode,
  platformToken, externalUserId, createAccount, nickname)`, `LinkPlatformAsync(extra, authCode,
  platformToken, externalUserId, createAccount)`. The player is always the id the server verified;
  `externalUserId` is read only by Game Center. `LoginByPlatformDto.PlatformId` and
  `LinkAuthProviderDto.PlatformId` are removed.
- **Breaking — `UnlinkPlatformAsync(platformKey, externalUserId)`.** A store sign-in is addressed by the
  key of the platform it was made on and the player's id at the store; the body is
  `{ platformKey, externalUserId }`. The `authCode` / `platformToken` / `extra` parameters are gone —
  nothing verifies them on unlink.
- **Breaking — `UnlinkGoogleSignInAsync` / `UnlinkSignInWithAppleAsync` / `UnlinkYandexSignInAsync`
  take only `externalUserId`.** The sign-in is addressed by the player's id at the provider, which is now
  required; the server no longer reads `idToken` / `authCode` / `extra` there.
- **Breaking — OpenID sign-in by key.** `LoginOpenIdAsync(string providerKey, options)`,
  `BeginOpenIdLoginUrlAsync(string providerKey, successUrl)` and `StartOpenIdLoginAsync(string providerKey,
  successUrl)` take the `IntegrationKey` of an `OpenId` / `Google` / `Apple` / `Yandex` method from
  `GetLoginMethodsAsync()` instead of the numeric provider id. A platform may offer several OpenID
  providers; the key picks one. An empty key fails validation without a request.
- **Analytics routes name the platform by key** (`…/platforms/{PlatformKey}/…`). A malformed key is
  refused with 422 `game_analytics.invalid_platform_key`; an unknown one is not checked, so a typo files
  the events under a platform that does not exist.
- **Showcase:** the auth screen draws its buttons from `GetLoginMethodsAsync()` (buttons for Guest /
  Device / Email / Username, WebView tiles for OpenID / Google / Apple / Yandex ID, a note for store
  sign-ins) and shows why when the platform refuses; the link prompt offers only the methods the platform
  has. The hard-coded OpenID provider ids are gone.
- A failed sign-in is logged with its cloud error code (`platforms.platform_unknown — …`), not only the
  transport message.
- **A 401/403 refreshes the session only when the session is what was refused.** An authenticated
  call used to refresh the session and go out again on any 401/403. Login, link and profile endpoints
  now answer with typed refusals — a wrong password is a 401, a forbidden link or a disabled avatar
  change a 403 — and each of those cost a session rotation and a second request for the same answer.
  The refresh now follows only a gateway's refusal of the token (no error code in the body) or
  `common.unauthorized`, `purchases.selected_profile_required`, `player_accounts.session_expired`,
  `player_accounts.session_mismatch`, `player_accounts.session_project_mismatch`. Any other code is
  the endpoint's answer and comes back at once, without a refresh or a resend. Sign-in calls are
  unchanged. The platform refusals above are final as well: a refreshed token carries the same
  platform.

### Removed

- **Breaking — `CloudErrorCodes` entries the backend no longer has:** `PlayerAccountsProviderNotEnabled`
  (replaced by `PlayerAccountsProviderNotOnPlatform` / `PlayerAccountsProviderDisabledOnPlatform`),
  `PlayerAccountsPlatformIdInvalid`, `PlayerAccountsMarketplaceSettingsMissing`,
  `PlayerAccountsMarketplaceUnsupported`, `PlatformsMarketplaceTypeMismatch`,
  `PlatformsMarketplaceSettingsTypeMismatch`, `PurchasesPlatformIdInvalid`.
- **Breaking — the provider-config codes of Purchases**, gone with the provider configs:
  `PurchasesProviderConfigIdInvalid`, `PurchasesProviderConfigNotActive`,
  `PurchasesProviderConfigNotFound`, `PurchasesProviderConfigWrongType` (a key of another vendor is now
  `IntegrationsIntegrationTypeMismatch`), `PurchasesStripeNoActiveProvider`.
- **Breaking — `CloudErrorCodes` of the removed per-project sign-in provider settings:**
  `PlayerAccountsPlatformDisabled`, `PlayerAccountsPlatformNotFound`, `PlayerAccountsProviderDuplicate`,
  `PlayerAccountsProviderNotFound` (a refused platform is `Platforms*`, a missing sign-in method
  `PlayerAccountsProviderNotOnPlatform`).

### Fixed

- **`RestApiError.Errors` is filled from real responses.** Every Cloud host writes the error envelope
  in camelCase (`{"errors":[{"code":…}]}`), the SDK's JSON mapper matches member names
  case-sensitively, and the error DTOs expected `Errors` / `Code`. So the envelope was never read:
  `HasCode`, `GetByCode` and `FirstCloudError` matched nothing, and failed analytics requests were
  logged without their code. They now see what the server sent.

## [0.5.0] — 2026-09-19

### Added

- **`Events` service.** The LiveOps events running for this player: `GetActiveEventsAsync()` plus
  cached lookups — `MyEvents`, `TryGetEvent`, `IsEventActive(key)`, `GetTimeLeft(key)`,
  `GetTimeUntilNextOccurrence(key)`.

  This does not make events work. While an event runs the server already hands the player different
  economy values, and a game that calls nothing here still gets them. What it could not do before is
  *say so* — put up a banner, count down to the end of the offer, or open a screen only to the
  audience an event targets.

  Events are addressed by the key set in the console. `IsEventActive(key)` answers "running **and**
  for this player": an event can be genuinely running and still not apply here, and a gate that
  forgets the difference opens seasonal content to everyone.

  Not part of a splash-screen warm-up, unlike the config services: the answer depends on the player
  and is only true for minutes. `IsStale` says when the server expects it to change; nothing
  refetches on its own.

  All times are absolute UTC. The service records the offset between the server's clock and the
  device's on each fetch and counts down from `ServerUtcNow`, because device clocks are wrong often
  enough for a countdown built on them to visibly lie.

- **`CloudErrorCodes`** gained the seven Events codes the backend actually returns
  (`EventsEventNotInBranch`, `EventsOverrideNotInBranch`, `EventsBranchNotEditable`,
  `EventsEventKeyConflict`, `EventsEventKeyInvalid`, `EventsOverrideConflict`,
  `EventsTargetingRuleNotFound`).

### Changed

- **The cached event list is dropped on sign-in and on a profile switch.** Which events apply is
  decided per profile, so the previous answer describes somebody else. It is dropped rather than
  refetched — the game decides when it needs it.

### Removed

- **`CloudErrorCodes.EventsEventBranchMismatch` and `EventsOverrideBranchMismatch`.** No such codes
  exist on the backend; nothing could ever have matched them.

## [0.4.0] — 2026-09-12

### Added

- **`PlayerAccountInfo.SelectedProfileId`** — the profile the account plays as, read from the sign-in
  and refresh responses and updated by `SelectProfileAsync`.
- **Error codes** in `CloudErrorCodes` for event keys (`GameAnalyticsEventKeyRequired`,
  `GameAnalyticsInvalidEventKey`, `GameAnalyticsEventKeyDuplicate`), for a token without a selected
  profile (`GameAnalyticsProfileIdHeaderRequired`) and for synthetic analytics data
  (`GameAnalyticsSynthetic*`).
- **`BatchEventItemDto.EventKey`.** Batched events are named by the event's key from the console, which
  stays the same when the event is renamed there. `EventName` is still sent with the same value for
  servers that predate keys.

### Changed

- **Breaking — `SelectProfileAsync` completes after the session is refreshed.** The server switches the
  profile without issuing a token, and every profile-scoped service — economy, saves, purchases,
  rewards, analytics — kept acting for the old profile until the next refresh, up to the token's
  30 minutes. The SDK now refreshes right away, and the operation (and `OnProfileSelected`) completes
  once the token carries the new profile. `CreateProfileAsync(dto, autoSelect: true)` does the same.
  A refresh that fails for want of the server (no connection, a 5xx) does not sign the player out: the
  switch stands, an error is logged, and calls go out as the old profile until the next refresh.
- **A profile switch is a new play session for analytics.** Buffered events and unreported playtime go
  out before the switch, under the old profile; after it a new `Analytics.SessionId` starts with one
  `SessionsStarted`. The account stays the same, so figures by account see one player.
- **Chats reconnect as the new profile** after a switch.
- `EnqueueEvent`'s first parameter is named `eventKey`; it always was the event's key.
- A failed analytics request is logged with its cloud error code, not only the HTTP status.

### Fixed

- `SelectProfileAsync`, `CreateProfileAsync` and the analytics calls return their own operation: a
  game's `UseCompleted` on it no longer replaces the SDK's own bookkeeping (`UseCompleted` replaces the
  callback), which had silently dropped `OnProfileSelected`, the profile list update and the logging of
  rejected batch events.

## [0.3.0] — 2026-09-11

### Added

- **`AnalyticsService.SessionId` — a play session for analytics.** One per entry into the game: each
  launch (a restored session or a sign-in), a sign-in after signing out, a sign-in as another account.
  Coming back from the background continues the same session; the next launch starts a new one. Every
  analytics request — session starts, playtime, batches, `SendEventAsync` — carries it in the
  `AnalyticsSessionId` header, and the server files events under it; servers that predate the header
  ignore it. It is deliberately not `Authentication.SessionId`: that is the auth session, which a
  restored login keeps for as long as the refresh token lives — for a game that signs in once and
  restores ever after, a single id per install that never ends. `SessionId` is `null` until
  analytics starts and again after sign-out.
- **`AnalyticsTracker.StopTracking()`**, which the SDK now calls when the player session ends.

### Changed

- **Breaking — `SessionsStarted` counts play sessions.** The SDK sends exactly one per play session — on
  sign-in or session restore, on a switch to another account — so the number of starts is the number
  of entries into the game. It used to go out on every `OnLogin`, account links
  included, and never for a restored session. `SendSessionStartedAsync()` still sends one on demand,
  which now counts the current session twice.
- **On WebGL and desktop, losing focus counts as being away** for playtime, not only a pause: a player
  who switches to another tab or window leaves without the app being paused, and the playtime
  heartbeat now holds while the app has no focus. Mobile goes on relying on the pause alone: the
  on-screen keyboard, the notification shade and system dialogs take focus while the player is still
  in the game.
- **Breaking —** `AnalyticsTracker.StartTracking` opens a play session itself — it reports
  `SessionsStarted` — and stops tracking that is already running first, dropping its buffered events.
  The SDK drives the tracker; games normally never call it.

### Fixed

- **Analytics starts for returning players.** Only `OnLogin` started it, and restoring a saved
  session in `InitializeAsync` raises `OnSessionRefreshed` instead — so a player who was already
  signed in reported no session start and no playtime, and every `EnqueueEvent` was dropped without
  a word. A restore now starts analytics; the refresh that follows a 401 mid-play leaves it alone.
- **Linking a provider no longer restarts analytics.** A link raises `OnLogin` for the account that
  is already signed in. It used to send another `SessionsStarted` and reset the heartbeat, losing up
  to five minutes of playtime each time. Only a different account starts a new session.
- **Signing out stops analytics.** Nothing stopped the tracker on `LogoutAsync`, `LogoutAllAsync`,
  `ClearLocalSession`, an unlink or an expired session, so it went on sending playtime and batches
  with no token. Events still buffered at that point are dropped with a warning: they were recorded
  as the player who has just left, and the token they would need is gone.
- **`PlayerAccountInfo` is filled in after a session restore.** It was set only from `OnLogin`, so
  for returning players it stayed `null` — and with it the account headers every request carries
  (`Country`, `LanguageCode`, `Nickname`, `IconKey`, `Age`, `TimeZone`, `Status`, segments). The
  refresh response has always contained the account; `SessionRefreshResultDto.PlayerInfo` now reads
  it, and every successful refresh updates `PlayerAccountInfo` from it. An account this build cannot
  read is skipped with an error instead of failing the refresh, which would sign the player out.
- **Requests that come back 401 together share one session refresh.** Each started its own with the
  same refresh token. The server replaces the token on the first refresh and refuses it after that, and
  a refused refresh signs the player out and deletes the saved session. Any two requests in flight when
  the access token ran out could trigger it, for instance a game's calls on resume or the playtime
  report and batch flush at a pause. `RefreshSessionAsync` now sends one request per refresh token, and
  calls that arrive while it is in flight get its outcome.
- **Time away from the app no longer counts as playtime.** The heartbeat clock kept running while
  the app was suspended or its tab hidden, so the first heartbeat after coming back could report the
  whole absence — hours, for an app left in the background overnight.
- **Rejected batch events are logged.** `events/batch` answers 200 even when it rejects every event,
  listing the failures in the body, and the SDK looked only at the status. It now logs a warning with
  how many were rejected and the index, event name, error code and reason of the first three.

## [0.2.6] — 2026-09-04

### Added

- **Folder-inherited asset visibility.** A folder can now be published in the console, which publishes
  everything inside it at every depth. `Asset` and `Folder` gained `IsPublicInherited` alongside
  `IsPublic`, plus `IsEffectivelyPublic` combining the two — that is what decides whether the
  anonymous `LoadPublic*` routes will serve an asset. Code that tested `asset.IsPublic` to predict
  those routes should now test `IsEffectivelyPublic`: an asset published through its folder has
  `IsPublic == false` and still downloads without a player session.

### Fixed

- **Addressing an asset by path.** The by-path routes matched nothing at all, because the stored path
  carries a leading slash and the route never does. Both forms now resolve. This is what makes a
  published folder usable as a Unity Addressables Remote Load Path: the folder's URL with a file name
  appended is a working asset URL.

## [0.2.5] — 2026-09-01

### Added

- **Error codes for background package installs.** Installing or updating a package in the console
  is now an accepted-then-polled operation rather than one long request, and it can be refused with
  `cloud_packages.install_in_progress` (another install already holds the project),
  `cloud_packages.install_queue_full` or `cloud_packages.install_unavailable`. A task that a service
  restart cut short reports `cloud_packages.install_interrupted`. Mirrored here because
  `CloudErrorCodes` is a complete copy of the backend catalogue; the SDK itself exposes no package
  API, so nothing else changes for games.

## [0.2.4] — 2026-08-31

### Fixed

- **Nullable fields no longer break deserialization when they carry a value.** The JSON reader
  handed every parsed value to `Convert.ChangeType`, which cannot target a `Nullable<T>` — so a
  populated `int?`, `bool?`, fractional `double?` or number-encoded `enum?` failed the whole
  response with ``Invalid cast from 'System.Int32' to 'System.Nullable`1[[System.Int32]]'``. Null
  values and the other nullable types happened to take branches that already handled this, which is
  why the hole stayed open. Reading is now routed through one place that knows the rule.
- **Spending energy no longer fails after the request succeeds.** The server fills
  `secondsUntilNextRecharge`, `secondsUntilFullRecharge` and `cooldownRemainingSeconds` only once a
  meter drops below its maximum, so `EnergyBalanceDto` carried nothing but nulls until the first
  spend — and every read of that meter failed from then on, taking `GetInventoryAsync` with it.
  Affected `SpendEnergyAsync`, `AddEnergyAsync`, `SetUnlimitedEnergyAsync`, `GetEnergyAsync`,
  `GetEnergiesAsync` and `GetInventoryAsync`. The same fault reached `finishPosition` on challenge
  entries and score submissions, `finishersToEnd` on challenge configs, and `trialDays` and
  `gracePeriodDays` on subscription configs.
- **Multidimensional arrays of nullable elements now deserialize.** `int?[,]` and friends hit the
  same `Convert.ChangeType` wall; single-dimension arrays never did, and now both take the same path.

## [0.2.3] — 2026-08-31

### Added

- **`ChatsService.ConnectionState`.** The realtime connection's state is now readable, not only
  observable: the property carries the same value that last went out through
  `OnConnectionStateChanged`. The connection lives for the whole session, so a listener that
  subscribes after it came up — a chat screen opened a second time, a UI built lazily — never
  receives an event and, until now, had no way to learn it was already connected.

### Fixed

- **Reopening a chat screen no longer reports the connection as offline.** With no way to read the
  state, a fresh listener assumed `Disconnected`; `ConnectAsync` on an already-open socket completes
  without changing state, so no event ever corrected it. Anything gated on the connection —
  in the Showcase, the composer and read receipts — stayed disabled for the rest of the session.
- **A reconnect that runs out of attempts now publishes `Disconnected`.** The service raised
  `OnError` and stopped, leaving the last published state at `Reconnecting` forever.
- **The realtime connection no longer outlives the session that opened it.** The server freezes the
  sender into the socket at the handshake, so a connection kept across a sign-out went on speaking
  as the player who left: sign in as someone else and their messages were sent, and accepted, under
  the previous player's name. Signing out now closes the socket — until it did, a signed-out client
  stayed connected and kept receiving the previous player's messages — and signing in under a
  different session opens a new one instead of reusing what is already there.
- **Showcase — Chats.** The list of recently opened channels is stored per account, so the next
  player to sign in on the same device no longer sees the previous player's channels.

## [0.2.2] — 2026-08-28

### Changed

- **Showcase — Groups.** Creating a group now offers *Put new members into that chat* and sends
  `AutoJoinMembers`. The sample never set the flag, so every group it created got a chat that
  players joining later could not enter: chat membership is a separate record from group
  membership, and only this flag bridges the two.
- **Showcase — Chats.** A group chat this profile has not joined is no longer reported as a group
  without a chat. The row says so and offers **Join**, taking the channel id from the group's chat
  config because the member-only lookup withholds it. The same refusal on history renders a zero
  state with a Join action instead of an error line, and joining re-runs the subscribe the server
  refused earlier — so sending works without reopening the channel.

## [0.2.1] — 2026-08-28

Metadata only — the code is identical to `v0.2.0`.

### Fixed

- The package reports its real version. `version` in `package.json` said `0.1.0` at every tag up to
  and including `v0.2.0`, so Package Manager displayed `0.1.0` whichever tag you installed. Git tags
  are only revisions as far as UPM is concerned — the version it shows comes from `package.json`
  alone, and there is no "resolve the newest tag" for a git URL. Install `v0.2.1` or later to be
  told the version you actually have.

## [0.2.0] — 2026-08-28

### Fixed

- **Non-ASCII header values are percent-encoded before a request leaves.** `UnityWebRequest` accepts
  only printable ASCII (`0x20..0x7E`) in a header value, and the account metadata headers carry
  free-form player text — so a Cyrillic nickname or an emoji made `SetRequestHeader` throw
  `Header value contains invalid characters` and killed the request coroutine. In practice every
  authenticated call died as soon as such an account was loaded. Values that are already clean go
  out byte-identical; the rest are encoded from their UTF-8 bytes and are restored server-side with
  `Uri.UnescapeDataString`. A null header value is now skipped instead of throwing.

### Changed

- **Showcase — Economy.** The screen was rebuilt, and the currency calls are covered.
- **Showcase — Groups.** The screen was rebuilt around the full group lifecycle.
- **Showcase — Assets Storage.** Public assets are marked as public in the catalog.
- **Showcase — Profanity Filter.** The group key is shown as required, which is what the backend
  expects.

## [0.1.1] — 2026-08-25

### Fixed

- **The editor talks to the service-account gateway** (`sa.mirracloud.com`) instead of
  `api.mirracloud.com`. The editor routes are not declared on the client api-gateway: they fell into
  its `/api/cloud/**` catch-all, where a policy demanding a Cloud client role answered `401` before
  any backend was reached, and every editor request failed.

## [0.1.0] — 2026-08-25

First public release, and the first one distributed as a UPM package.

### Added

- The `com.mirrahub.cloud-sdk` package, installable from a single git URL.
- 23 services on one async contract: every call returns `AsyncOperation<RestApiResult<T>>` and
  reports failures as values rather than exceptions.
- `net.gree.unity-webview` 1.0.0 (zlib) and `com.gilzoide.sqlite-net` 1.3.2 (MIT) are vendored under
  `ThirdParty/`, so there is nothing to install alongside the SDK. Both are unmodified upstream
  copies and keep their own assembly names, guids and import settings — which is also why installing
  either of them separately collides. Provenance and update instructions are in each folder's
  `VENDORED.md`.
- **Showcase** sample: every service on a live UI, built with UI Toolkit and VContainer. Import it
  from Package Manager.
- Editor tooling: the **Manager** window (`Tools → Mirra Cloud → Manager`) for picking project,
  branch and API token, and the **Request Inspector** for tracing SDK calls.
- `Configuration.asset` is created automatically under `Assets/MirraCloud/Resources` the first time
  you open the Manager window. It belongs to your project, not to the package, and holds your API
  token — keep it out of version control.

### Notes for anyone upgrading from the pre-package SDK

The SDK used to be installed by copying `Assets/Plugins/MirraCloud` into a project.

- **Breaking.** It now lives in `Packages/com.mirrahub.cloud-sdk`. Assembly names (`MirraCloudSDK`,
  `MirraCloudSDKEditor`) and every guid are unchanged, so references from your code and scenes
  survive — but delete the old folder from `Assets`, otherwise the types are defined twice.
- **Breaking.** `Configuration.asset` moved out of the SDK and into the project. The Manager window
  picks up an asset left at the old path and keeps writing to it, so nothing has to be moved by
  hand — moving it is still tidier.
- **Breaking.** If your manifest lists `net.gree.unity-webview` or `com.gilzoide.sqlite-net`, remove
  them; the package brings its own copies.
- The example's assembly was renamed from `Example` to `MirraCloud.Showcase`, so it cannot collide
  with an assembly of the same name in your project.
- `Configuration.Load()` no longer throws a `NullReferenceException` when the asset is missing; it
  logs what to do instead.
- The logo in the Manager window renders again — its path used to be hardcoded and pointed at a
  folder that does not exist.

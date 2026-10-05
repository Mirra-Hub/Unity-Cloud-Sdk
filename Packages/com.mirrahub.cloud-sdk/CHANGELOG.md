# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[SemVer](https://semver.org/).

The SDK is `0.x`: the public API can change between minor versions. Breaking changes are marked
**Breaking**.

## [0.12.1] — 2026-10-05

### Changed

- **Showcase — Leaderboard.** The boards live in a sidebar, each row with its own Join / Leave button. The selected
  board shows its configuration on one line next to the player's place, then a submit card, then the standings with
  the slice switch (your table, global, around you, friends, country) in the card header. A leave asks first; time
  boards show scores as durations.

## [0.12.0] — 2026-10-05

Cloud saves follow the backend's new access model: the game server can always reach what a player writes, global
data is published from the console, and masks become optional. Needs the backend release with the same model;
against an older backend the SDK behaves as 0.11.0. Upgrade steps: replace `QueryCustomDataAsync(customId, request)`
with `QueryCustomDataAsync(request)`, and stop creating global keys or files from the client.

### Added

- **`CloudSaveDataRequest.AddLong` / `AddDouble`** and **`PlayerData.GetLong` / `GetDouble` / `HasKey`**. A value
  beyond `int` — Unix time in milliseconds — used to read back as the default.
- **`QueryCustomDataAsync(request)`** — searches every custom entity (rooms, guilds), never global data.

### Changed

- **Breaking:** the masks of `CloudSaveDataRequest.Add*` are `AccessMask?` and default to `null`: an existing key keeps
  its masks and a new one gets the server default. Before, every write sent `Owner/Owner` and reset the masks of the
  key it touched. Code passing masks explicitly compiles as before.
- **Breaking:** a player can no longer create global keys or global files — the backend answers
  `cloud_saves.access_denied`. Global data is published from the console or Cloud Code; a player changes only keys
  and files whose write mask includes `AccessMask.Other`.
- `AddFloat` stores the float's shortest form: `0.1f` is `0.1`, not `0.100000001490116`.
- `GetPlayerDataAsync` with `keys` (or `offset`/`limit`) merges into `PlayerData` instead of replacing it; requested
  keys that did not come back are dropped. A read without filters still replaces it.
- `PlayerData.GetString` returns objects and arrays as JSON (was `""`) and numbers and booleans as text.
- `PlayerData.GetInt` / `GetFloat` read any number, not only one stored with the matching field type.

### Deprecated

- `QueryCustomDataAsync(customId, request)` — the id was never applied; the search always covered every entity.

### Fixed

- A file key with a space was stored as `my+save`: keys are now escaped as path segments.
- The Showcase no longer writes global data or uploads global files.

## [0.11.0] — 2026-10-04

Settings can come from code: `Initialize(MirraCloudOptions)` sets the project, branch, API token and platform over
`Configuration.asset`. The editor tools are gathered under one top-level **MirraCloud** menu, which also gains a
look into the asset cache. Upgrade steps: replace `Configuration.BranchId` with `Configuration.Branch` in your code,
and find the Manager under `MirraCloud → Manager`.

### Added

- **`IMirraCloudSdk.Initialize(MirraCloudOptions)`** — `ProjectId`, `Branch`, `Token` and `PlatformKey` set from code.
  A field the options set wins over the asset and is trimmed; one left `null`, empty or blank comes from the asset.
  The asset itself does not change — the SDK runs on a copy — so `MirraCloud → Manager` keeps showing the asset's own
  values. With `ProjectId` and `Branch` both set, the SDK starts without `Configuration.asset`. The SDK logs which
  fields came from code (never the token's value). Calling `Initialize` again changes nothing; with options it
  warns that they are ignored. `Initialize()` without arguments works as before. Code that implements
  `IMirraCloudSdk` itself has to add the new overload.
- **`MirraCloud → Data → Clear Cache`** — deletes the downloaded assets. A standalone build of the same project on
  this machine shares the cache, so it downloads again too.
- **`MirraCloud → Data → Show Cache`** — a window listing the cached assets by project, branch, asset, version and
  size, largest first, with the total, **Clear** and a button that opens the cache's folder. It reads in Play Mode too.

### Changed

- **Breaking:** `Configuration.BranchId` is renamed to `Configuration.Branch` — it holds the branch name, like
  `MirraCloudOptions.Branch`. Saved `Configuration.asset` files keep their value (`[FormerlySerializedAs]`) and are
  rewritten with the new name the next time the Manager window saves them; code that reads the field has to be
  updated.
- **Every editor tool is in the top-level MirraCloud menu.** `Tools → Mirra Cloud` is gone: the Manager is
  `MirraCloud → Manager`, the Request Inspector moves to `MirraCloud → Debug → Request Inspector`, and
  `Clear Saved Sign-In` becomes `MirraCloud → Data → Clear Sign-In`. The Clear items are greyed out in Play Mode,
  where the running SDK holds the data open.

## [0.10.1] — 2026-10-04

### Changed

- **Breaking:** `CountryCode.Undefined` is the zero value — an account that never set a country reads as
  `Undefined`, not `Afghanistan`. Every other member moved up by one (`Afghanistan` is 1, `Zimbabwe` is 249), the same
  numbers as the server's `Country`. Code that stored a `CountryCode` as a number must re-read it; code that compared
  with `default(CountryCode)` to mean "not set" should compare with `CountryCode.Undefined`. Needs the Cloud backend
  from 2026-10-04 or later: an older one sends the old numbers and never sends `Undefined`.

## [0.10.0] — 2026-09-28

Leaderboards work end to end: boards are addressed by key, a submit returns the player's place, and the rewards
boards and tournaments pay out are read and claimed through Economy. A failed request is repeated only when
repeating cannot do harm — this applies to every service. Upgrade steps: pass `LeaderboardConfig.Key` wherever a
leaderboard method took an id, and read leaderboard and tournament rewards with
`Economy.GetPendingRewardsAsync` / `ClaimRewardsAsync`.

The reset dates and reward keys in the configs, the profile name, icon and country on the entries, and the typed
leaderboard errors need the Cloud backend from 2026-09-28 or later; with an older one those fields stay empty and
refusals come as 400.

### Added

- **`LeaderboardService.GetLeaderboardGlobalTopEntries(leaderboardKey, top)`** — the whole board, every cohort
  together. `GetLeaderboardTopEntries` is the player's table: their cohort on a board with cohorts.
- **`SubmitScoreAsync(TimeSpan, leaderboardKey)`** — for time boards; sent as seconds with a fraction.
- **`Economy.GetPendingRewardsAsync()`** and **`Economy.ClaimRewardsAsync()`** — what leaderboards, tournaments,
  challenges, daily rewards, purchases and promo codes granted the player: read without claiming, and claimed all
  at once into the wallet, items and energies. The same containers come in `PlayerInventoryDto.Rewards`
  (`RewardContainerDto`: `SourceType`, `SourceId`, `Rewards` with `RewardKey`, `EconomyResourceKind`, `Count`).
- Leaderboard fields the server sends and the SDK did not read: an entry's `iconKey` and `countryCode` (the
  profile's), the entries' `leaderboardKey`, a config's `resetTimeHour` / `resetTimeMinute` (UTC),
  `cohortsEnabled` / `cohortSize`, and each reward's `rewardKey` and `economyResourceKind`. A config's
  `nextResetDate` — the next reset with its time of day, for a countdown — and `lastResetDate` are now filled.
- `RestRequestConfig.Idempotent` — marks a POST or PATCH that only reads, so it is repeated like a GET.
- `CloudErrorCodes`: `LeaderboardsEntryNotFound`, `LeaderboardsInvalidScore`, `LeaderboardsPersistenceFailed`
  and the config validation codes (`LeaderboardsInvalidName`, `…InvalidCohortSize`, `…InvalidResetTime`,
  `…InvalidResetInterval`, `…InvalidReward`).
- **Showcase — Leaderboard.** A board plays: join, submit (a `TimeSpan` on a time board) and leave, with the
  standings reloaded in place. A Global top slice, and a Rewards tab with what the boards paid out.
- **Showcase — Economy.** A Rewards tab: every pending reward, and Claim.

### Changed

- **Breaking — boards by key.** Every `LeaderboardService` method takes `leaderboardKey`; the parameter was called
  `leaderboardId`, but the server resolves boards by key only, so a config id never found a board.
- **Breaking — `SubmitScoreAsync(double, leaderboardKey)`** returns the player's entry with the new place
  (`RestApiResult<LeaderboardEntryDto>`, was `RestApiResult`). A `NaN` or an infinite score is refused without a
  request. No name is sent any more: the board shows the profile's nickname, icon and country.
- **Breaking — repeats.** A failed request used to be sent again once, at once, whatever it was: a refused submit
  went out twice, a POST whose response was lost could be applied twice (a Total board counted the score
  double), and a 429 was answered with another refused request. Now a network failure or a 502/503/504 is
  repeated for GET, HEAD, PUT, DELETE and `Idempotent` calls, after a pause (0.5 s, doubling up to 4 s); a 429 is
  repeated once the gateway's `Retry-After` has passed, if it is 10 seconds or less; no other 4xx is repeated.
  `MaxRetries = 0` turns repeats off — it used to mean 1. The reads sent as POST — the profanity check, the branch
  resolve, the leaderboard and tournament friends tops — are marked `Idempotent`.
- `JoinAsync(leaderboardKey)` returns `null` until the player has a score, and `GetLeaderboardPlayer` answers
  404 `leaderboards.entry_not_found` then.
- An enum value the SDK does not know — added on the server after this build — reads as `null` (nullable
  field) or the enum's default, with a warning (`JsonMapper.Warning`), instead of failing the whole response.
- `LeaderboardService` no longer takes `PlayerAccountService`, and `SubmitScoreDto` has no `PlayerName`.
- **Showcase — Tournaments.** Rewards come from Economy, and a pane has a Join card.

### Deprecated

- `SubmitScoreAsync(DateTime, leaderboardKey)` — pass a `TimeSpan`. It now submits the time elapsed since
  `DateTime.MinValue`, in seconds; it sent a fraction of a day, and threw from one day up.
- `LeaderboardService.GetRewardsAsync` / `SubmitRewardsAsync`, `TournamentsService.GetRewardsAsync` /
  `SubmitRewardsAsync` and their `PlayerRewardsDto` — the server has no such routes; use Economy's pending
  rewards.
- Fields nothing fills: `LeaderboardEntriesDto.leaderboardId`, `LeaderboardTopAndPlayersAroundDto.leaderboardId`,
  `RewardRangeDto.pLaceInLeaderboardMin` / `Max`, `RewardDataDto.rewardType` and the `RewardType` enum.

### Fixed

- **Showcase — Leaderboard** read every slice with the config id and so found no board.

## [0.9.0] — 2026-09-28

The sign-in moves out of `PlayerPrefs` into the SDK's own local storage — SQLite, or IndexedDB in WebGL, the
storage the asset cache already uses — and a player's chats can be listed. Upgrade note: a session or guest id
saved by an earlier version is not picked up, so players sign in again, and a guest who never linked another
sign-in method starts as a new player.

`GetMyChannelsAsync` needs the Cloud backend with the `players/me/channels` chat route; the rest works with any
backend.

### Added

- **`Chats.GetMyChannelsAsync(page, pageSize)`** — the chats the player (the selected profile) is in, rooms and
  group chats, newest membership first, each with the player's `UnreadCount` (`ChatPlayerChannelDto`). Deleted
  channels are left out, archived ones stay (`Channel.State`). `pageSize` is capped at 50. The player's own
  messages count as unread until `MarkAsReadAsync` moves past them.
- **Tools → Mirra Cloud → Clear Saved Sign-In** — forgets the editor's saved session and guest id, so the next
  guest sign-in makes a new player. `Edit → Clear All PlayerPrefs` no longer does that.

### Changed

- **Breaking — where the sign-in is kept.** The guest id and the refresh token live in the SDK's local storage
  (container `mirracloud_prefs`, under the project id), not in `PlayerPrefs`. Nothing is carried over from
  `PlayerPrefs`: after the update every player signs in again, and an unlinked guest gets a new account. The
  editor keeps a copy of its own (`mirracloud_prefs_editor`), separate from a build run on the same machine, so
  the two stay two players, as they were with `PlayerPrefs`. The session id and its expiry are no longer stored:
  nothing read them.
- `InitializeAsync()` always completes on a later frame, also when there is nothing to restore — it reads the
  saved session in the background. `OnCompleted` and `UseCompleted` attached after the call now fire in that case
  too. A request sent right after it, and `access: AssetAccess.Auto`, still go out as the player while the saved
  session is read.
- `Login*`, `Link*`, `ResolveLinkConflictAsync` and `CompleteOpenIdLoginAsync` complete once the new session is
  written to disk; `OnLogin` still fires before they do.
- **Showcase — Chats.** The channel list shows the player's chats from the server (**My channels**) instead of the
  ids of recently opened ones, which the sample kept in `PlayerPrefs`.

### Fixed

- `LoginOpenIdAsync` applies the session. It never did: the SDK hooked `UseCompleted` on an operation it had
  already hooked, `UseCompleted` keeps one callback, and the sign-in came back successful while `IsAuth` stayed
  false, the token was not sent and `OnLogin` never fired.
- `UseCompleted` in game code on `Login*`, `Link*`, `ResolveLinkConflictAsync`, `LogoutAsync`, `LogoutAllAsync`
  and the `Unlink*` methods no longer switches off the SDK's own handling: the session is applied and saved, or
  cleared on sign-out, whatever the game hooks.
- Disposing the SDK while it writes to SQLite no longer hangs the main thread.

### Removed

- **Breaking — `PrefsStorage`.** `IStorage` is reworked for the asynchronous storage: `Ready` and `FlushAsync`
  are added, `DeleteKey` is removed (`DeleteKeys` takes a single key too), and `GetString` returns `null` for a
  missing key.

## [0.8.0] — 2026-09-27

Assets load by the path the console shows, and one set of `Load*` methods covers private and public
assets: an optional `access` argument picks the player's route, the anonymous one, or whichever fits
the session. Upgrade steps: replace each `LoadPublic*FromId(id, …)` with the same method without
`Public` and `access: AssetAccess.Public`.

Loading by path as the signed-in player needs the Cloud backend from 2026-09-27 or later; the
anonymous route has always served paths.

### Added

- **Loading by path.** `LoadTextFromPath`, `LoadTextureFromPath`, `LoadSpriteFromPath`,
  `LoadAudioFromPath`, `LoadAssetBundleFromPath` — the same parameters as their `*FromId` twins, and
  the path as the console shows it: `icons/coin.png` or `/icons/coin.png`, case-sensitive. The SDK
  escapes every segment itself, so spaces, Cyrillic, `#`, `?`, `%` and `+` in names are fine. After
  `LoadConfigAsync()` a path the catalog knows is loaded by that asset's stable id and shares its cache
  entry; any other path is resolved by the server, without the cache. An empty path or one with `..` is
  refused without a request, with the server's code `assets_storage.asset_path_invalid`.
- **`AssetAccess`** — an optional `access` argument on every `Load*FromId` and `Load*FromPath`:
  `Player` (the default — the signed-in player's route, every asset), `Public` (anonymous, only assets
  published in the console; a private one answers 403 `assets_storage.asset_not_public`) and `Auto`
  (`Player` while there is a session, signed in or being restored, `Public` otherwise).
- `TryGetAssetByPath(path, out Asset)` and `GetAssetsInFolder(folderPath, recursive = false)` —
  lookups in the loaded catalog, without a request.

### Changed

- A failed load returns the server's answer — `HttpStatusCode` and the error codes in `Error.Errors`
  (`assets_storage.asset_not_found`, `asset_not_public`, …) — instead of a validation error saying
  "download failed". A file that arrived but is not what was asked for (not an image, not a bundle) is
  still a validation error, and keeps its 2xx status. A successful load carries the status, URL and
  timing of its request (none on a cache hit).
- Anonymous loads use the local cache like the others once the version is known (after
  `LoadConfigAsync()`), and an anonymous AssetBundle is built from the downloaded bytes, as the player's
  route always did.
- **Showcase — Assets Storage.** An asset's details load it again by its path (**Load by path**), and
  the anonymous check goes through `access: AssetAccess.Public`, past the cache, so it always makes the
  request it vouches for.

### Fixed

- `LoadConfigAsync().UseCompleted(...)` in game code no longer throws the catalog away. The SDK hooked
  the same operation it returned, and `UseCompleted` keeps one callback, so the game's replaced the SDK's:
  `Assets` / `Folders` stayed empty, and with them the cache. `await op.Task()` was not affected.

### Removed

- **Breaking — `LoadPublicTextFromId`, `LoadPublicTextureFromId`, `LoadPublicSpriteFromId`,
  `LoadPublicAudioFromId`, `LoadPublicAssetBundleFromId`.** The same load with
  `access: AssetAccess.Public` replaces each of them.

## [0.7.1] — 2026-09-27

The Manager window (`Tools → Mirra Cloud → Manager`) checks the picked platform against the build
target. Nothing changes at runtime.

### Added

- **Build target check.** An error under the **Platform** dropdown when the active build target is of
  a type the platform is not set up for (console → Platforms → types: Web, PC, Mobile, Console), with
  a **Build Settings…** button. Windows / macOS / Linux count as PC, WebGL as Web, Android and iOS as
  Mobile, PlayStation, Xbox and Switch as Console. tvOS, visionOS, UWP, embedded targets, dedicated
  servers and platforms without types are not checked.

### Changed

- The **Platform** dropdown shows each platform's types next to its name and key.
- Without a saved platform key the window starts with the first switched-on platform made for the
  active build target, then the first switched-on one (was: the first in the list). A saved key that
  is still in the project is kept.
- When the server fails to return the platforms (5xx), the window says to press Refresh later instead
  of showing the raw HTTP status; the status, error code and URL go to the Console.
- The caption under the **Platform** dropdown is removed.

### Fixed

- A `/` in a project, branch, platform or token name opened a submenu in its dropdown; it is shown as
  a slash now.

## [0.7.0] — 2026-09-27

Remote Config is withdrawn: the backend answers every Remote Config request with 404
`remote_config.deprecated`, so the service leaves the SDK. There is no replacement service. Upgrade
steps: delete the calls to `sdk.RemoteConfig` and the `using MirraCloud.Core.RemoteConfig` lines the
compiler points at.

### Removed

- **Breaking — the Remote Config service.** `IMirraCloudSdk.RemoteConfig`, `RemoteConfigService`
  (`LoadConfigAsync`, `Config`), `RemoteConfig`, `RemoteConfigField`, `RemoteConfigFieldType` and
  `FetchRemoteConfigResponse` — the whole `MirraCloud.Core.RemoteConfig` namespace. Builds on 0.6.0 and
  older keep compiling and running, but `LoadConfigAsync` fails with 404 `remote_config.deprecated` and
  `Config` stays `null`. The SDK never called it on its own, so only games that called it are affected.
- The Remote Config screen of the Showcase sample.

## [0.6.0] — 2026-09-21

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
    `AdjustReportState`, `LastAdjustReport` and `OnAdjustReported` say where it stands. Both may be
    called from any thread — Adjust SDK v4 raises its Android callbacks off Unity's main thread — and
    are carried over to the main thread.
  - `LinkAdjustAsync(AdjustAttributionDto)` — the bare `PUT …/players/external/v1/projects/{projectId}/adjust`,
    for a game that manages the timing itself. Sent once: a refusal or a failed request is not resent.
  - `GetMyExternalIdsAsync()` — what is recorded on the signed-in account (`ExternalIdDto`: provider,
    id, source, first / last seen, the Adjust attribution).
- **`PurchaseResult.ApiError`** — the server's refusal when `BuyAsync` could not start the order, so a
  game can dispatch on its code (`ApiError.HasCode(…)`).
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
- **`CloudErrorCodes`** mirrors the new Integrations module (`Integrations*`: field, usage and
  key-conflict errors from the console API — the server assigns an integration's key, so there are no
  key validation codes — plus `IntegrationsIntegrationTypeMismatch` and
  `IntegrationsSecretUnreadable`, which reach a game only through another module that reads an
  integration, e.g. a purchase) and the new sign-in and account codes:
  `PlayerAccountsAuthCodeRequired`, `PlayerAccountsIdTokenRequired`, `PlayerAccountsSessionIdInvalid`,
  `PlayerAccountsAccountIdInvalid`, `PlayerAccountsProfileIdInvalid`, `PlayerAccountsFileRequired`,
  `PlayerAccountsAvatarChangeDisabled`. Two older codes the mirror had missed are in too:
  `PlayerAccountsBranchNotEditable`, `PlayerAccountsAccountOptionInvalid`.
- **`CloudErrorCodes` for one integration of each service per project.** A project connects a service
  (Google, Apple, Yandex, Stripe, …) once; OpenID Connect is the exception and may be connected as
  many times as needed. The console API refuses a second one with `IntegrationsTypeAlreadyAdded`
  (409), and refuses to switch on a platform's sign-in method whose service the project has not
  connected with `PlatformsAuthProviderIntegrationMissing` (422). Nothing changes for a game: the
  sign-in methods of `GetLoginMethodsAsync` and the catalog prices carry the same integration keys as
  before.

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
- **`PurchaseResult.Error` of a `BuyAsync` whose order could not be started** reads `code — message`
  (e.g. `purchases.payment_integration_unavailable — …`) instead of the HTTP status line.
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

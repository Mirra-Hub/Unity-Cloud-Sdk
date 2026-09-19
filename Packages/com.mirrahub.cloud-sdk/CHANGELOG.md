# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[SemVer](https://semver.org/).

The SDK is `0.x`: the public API can change between minor versions. Breaking changes are marked
**Breaking**.

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

# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[SemVer](https://semver.org/).

The SDK is `0.x`: the public API can change between minor versions. Breaking changes are marked
**Breaking**.

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

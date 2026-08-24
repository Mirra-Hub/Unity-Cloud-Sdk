# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[SemVer](https://semver.org/).

The SDK is `0.x`: the public API can change between minor versions. Breaking changes are marked
**Breaking**.

## [0.1.0] — unreleased

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

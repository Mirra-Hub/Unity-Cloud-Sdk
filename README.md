<h1 align="center">Mirra Cloud SDK for Unity</h1>

<p align="center">
  A cloud backend for games — accounts, saves, economy, leaderboards,<br>
  social systems, LiveOps and analytics. One package, 23 services, no server of your own.
</p>

<p align="center">
  <img alt="Unity 2022.3 LTS" src="https://img.shields.io/badge/Unity-2022.3%20LTS-000?logo=unity">
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-Standalone%20%7C%20Android%20%7C%20iOS%20%7C%20WebGL-2563EB">
  <a href="https://mirrahub.com/documentation/mirra-cloud/sdk-install"><img alt="Documentation" src="https://img.shields.io/badge/docs-mirrahub.com-2563EB"></a>
</p>

<p align="center">
  <a href="#getting-started">Getting started</a> ·
  <a href="#documentation">Documentation</a> ·
  <a href="#services">Services</a> ·
  <a href="#example-showcase">Example</a> ·
  <a href="#editor-tools">Editor tools</a>
</p>

---

## What this is

**Mirra Cloud** is a cloud platform for games: you configure content and rules in the
[mirrahub.com](https://mirrahub.com) dashboard, and your game reaches them through this SDK.

What the SDK gives you:

- **23 services** — from guest sign-in and cloud saves to tournaments, chats, purchases and analytics.
- **One async contract.** Every call returns `AsyncOperation<RestApiResult<T>>`: wait on it with
  `await`, `yield return` or a callback, whichever suits the code around it.
- **Errors are values, not exceptions.** The SDK does not throw on network failures or HTTP errors —
  you read them from `Result.Error`.
- **Set up from the editor.** Project, branch and token are picked in
  `Tools → Mirra Cloud → Manager`; your service account key never reaches a build.
- **Platforms:** Windows / macOS / Linux, Android, iOS, WebGL.

---

## Getting started

### 1. Requirements

Unity **2022.3 LTS** — the version the SDK is developed and tested against. Plus git 2.14+ on your
`PATH`, otherwise Package Manager cannot fetch packages from a git URL.

### 2. Installation

One URL. `Window → Package Manager → + → Add package from git URL…`:

```
https://github.com/Mirra-Hub/Unity-Cloud-Sdk.git?path=/Packages/com.mirrahub.cloud-sdk#v0.2.5
```

The package is self-contained: the native plugins it needs — a WebView (external sign-in providers
and purchase flows) and SQLite (the local asset cache) — ship inside it, along with
`com.unity.editorcoroutines` as a normal registry dependency.

> **Do not also install `net.gree.unity-webview` or `com.gilzoide.sqlite-net`.** They are already
> inside this package. Adding them again gives you two copies of the same assemblies and native
> libraries, and Unity refuses to build that.

Check: the **Tools → Mirra Cloud** entry appears in Unity's top menu.

<details>
<summary>Updating and pinning a version</summary>

The `#v0.2.5` at the end of the URL is a release tag. Package Manager resolves it once, writes the
commit it resolved to into `Packages/packages-lock.json`, and from then on never asks the remote
again — nothing updates on its own.

Dropping the tag does not change that. A bare URL resolves the default branch once and is locked the
same way, which is how a project ends up sitting on a commit from months ago while its manifest
looks like it follows `main`.

To move to another version, change the tag in `Packages/manifest.json`. If the URL text ends up
unchanged, also delete the `com.mirrahub.cloud-sdk` entry from `Packages/packages-lock.json` —
otherwise Unity reuses the commit recorded there and you get the old code back.

The SDK is still `0.x`: the public API can change between minor versions, and breaking changes are
called out in [`CHANGELOG.md`](Packages/com.mirrahub.cloud-sdk/CHANGELOG.md).
</details>

<details>
<summary>Coming from a copy of the folder in Assets?</summary>

The SDK used to be installed by copying `Assets/Plugins/MirraCloud` into a project. **Delete that
folder** before installing the package, or the types will be defined twice and nothing will build.
Assembly names and guids did not change, so references to the SDK from your code and scenes survive
the move.

If your manifest lists `net.gree.unity-webview` or `com.gilzoide.sqlite-net`, **remove them** — the
package brings its own copies now.

You do not have to move `Configuration.asset`: the Manager window finds it at the old path and keeps
writing there. Moving it is still tidier — the new home is `Assets/MirraCloud/Resources`.
</details>

### 3. Connecting a project

No code, done once — [full walkthrough ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-configuration)

1. In the dashboard: **Organization settings → Service accounts** → create an account (give it a
   role or explicit project permissions) and issue a **key** — it is shown once.
2. In Unity: `Tools → Mirra Cloud → Manager` → paste the key into **Service Account Key** →
   **Connect**.
3. Pick a **Project**, **Branch** and **API Token** (you can create a token right there with
   **+ Create Token**).

The choice is saved to `Assets/MirraCloud/Resources/Configuration.asset` — the asset is created for
you and belongs to your project, not to the package.

> **That asset holds your project API token.** Do not commit it to a public repository; add
> `/Assets/MirraCloud/` to your `.gitignore`.

The service account key lives only in `EditorPrefs` and **never reaches a build** — what ships with
the game is the project API token you selected.

### 4. Code

Create the SDK once per run and keep a single instance for the lifetime of the app:

```csharp
using MirraCloud.Core;
using UnityEngine;

public class CloudBootstrap : MonoBehaviour
{
    public static IMirraCloudSdk Sdk { get; private set; }

    private async void Awake()
    {
        DontDestroyOnLoad(gameObject);

        var sdk = MirraCloudSDK.Create();
        sdk.Initialize();               // synchronous, no await
        Sdk = sdk;

        // sign in
        var login = sdk.Authentication.LoginGuestAsync();
        await login.Task();

        if (login.Result.IsSuccess == false)
        {
            Debug.LogError(login.Result.Error.Message);
            return;
        }

        // first request
        var save = sdk.CloudSave.GetPlayerDataAsync(new[] { "level" });
        await save.Task();

        if (save.Result.IsSuccess)
        {
            Debug.Log($"Records loaded: {save.Result.Data.Length}");
        }
    }

    private void OnDestroy() => Sdk?.Dispose();
}
```

The singleton above is just an example. If your project already has a DI container (VContainer,
Zenject), registering the instance as `IMirraCloudSdk` is nicer — see
[Initialization ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-initialize).

---

## How calls work

An SDK method does not hand back data directly — it returns an **operation**, and the result comes
from `.Result`. There are three ways to wait for it:

```csharp
var op = sdk.CloudSave.GetPlayerDataAsync(new[] { "level" });

await op.Task();                       // async / await
yield return op;                       // coroutine
op.OnCompleted += o => { /* … */ };    // callback (subscribe right after the call)
```

Failures are not thrown — check `Result.IsSuccess`:

```csharp
if (op.Result.IsSuccess == false)
{
    var error = op.Result.Error;

    if (error.Type == RestApiErrorType.Network)
    {
        // no connection — offer a retry
    }
    else if (error.HasCode(CloudErrorCodes.CommonRateLimited))
    {
        // too many requests
    }
    else
    {
        var details = error.FirstCloudError();
        Debug.LogError($"{error.HttpStatusCode}: {details?.Code} {details?.Message}");
    }
}
```

More on this: [Async pattern ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-async-pattern) ·
[Error handling ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-error-handling)

---

## Documentation

### Getting started

| Page | About |
| --- | --- |
| [Installation ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-install) | the package and its dependencies |
| [Connecting a project ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-configuration) | service account, the Manager window, picking a branch and token |
| [Initialization ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-initialize) | starting the SDK, singleton or DI |
| [Async pattern ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-async-pattern) | `AsyncOperation`, await / coroutines / callbacks, parallel requests |
| [Error handling ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-error-handling) | `RestApiError`, `CloudErrorCodes` |
| [Events ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-events) | what you can subscribe to |
| [In-app browser ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-webview) | WebView: opening a page, intercepting URLs |

### SDK reference

The technical reference for every service — methods, DTOs, behaviour — lives in this repository,
under [`docs/`](docs/README.md).

| Section | What is inside |
| --- | --- |
| [Overview and architecture](docs/README.md) | the `Runtime/` layout, shared patterns, editor tools |
| [Local storage](docs/Storage.md) | `IStorage` (PlayerPrefs) and `IBlobStorage` (SQLite / IndexedDB / File) |
| [Services](#services) | 23 services, table below |

> The pages under `docs/` are currently written in Russian.

### Cloud feature guides

How everything works from the dashboard side — organizations, projects, branches, LiveOps,
analytics: [full Mirra Cloud documentation ↗](https://mirrahub.com/documentation/mirra-cloud)

---

## Services

Every service is reachable through `IMirraCloudSdk` once `Initialize()` has run.

**Accounts and sign-in**

| Service | What it does |
| --- | --- |
| [Auth](docs/services/Auth.md) | sign-in: guest, device, email, username, gaming platforms, OpenID; linking accounts |
| [PlayerAccount](docs/services/PlayerAccount.md) | player account and profiles, nickname, avatar, roles |

**Player data**

| Service | What it does |
| --- | --- |
| [CloudSave](docs/services/CloudSave.md) | cloud saves: player data, global data, files |
| [DailyRewards](docs/services/DailyRewards.md) | daily rewards, streaks and milestones |
| [PromoCodes ↗](https://mirrahub.com/documentation/mirra-cloud/promo-codes) | redeeming promo codes, history, active effects |
| [Purchases ↗](https://mirrahub.com/documentation/mirra-cloud/purchases) | catalog, purchase, orders, subscriptions |

**Game configuration**

| Service | What it does |
| --- | --- |
| [Economy](docs/services/Economy.md) | currencies, items, containers, energies, inventory |
| [Entities](docs/services/Entities.md) | custom configs: templates and components |
| [RemoteConfig](docs/services/RemoteConfig.md) | remote configuration and A/B tests |
| [Segments](docs/services/Segments.md) | player segments |
| [Localization ↗](https://mirrahub.com/documentation/mirra-cloud/liveops-localization) | translation collections, values by key and language |
| [Deployment](docs/services/Deployment.md) | resolving the config branch for a build version |

**Competition**

| Service | What it does |
| --- | --- |
| [Leaderboard](docs/services/Leaderboard.md) | leaderboards, score submission, the player's neighbourhood |
| [Tournaments](docs/services/Tournaments.md) | tournaments with leagues and rewards per place |
| [Challenges](docs/services/Challenges.md) | challenges — a race to a goal with reward thresholds |

**Social**

| Service | What it does |
| --- | --- |
| [Friends](docs/services/Friends.md) | friends, requests, blocks, presence |
| [Groups](docs/services/Groups.md) | groups and clans with roles and permissions |
| [Chats](docs/services/Chats.md) | chats: REST + realtime over WebSocket |
| [ProfanityFilter](docs/services/ProfanityFilter.md) | checking and masking player-written text |

**Content, code and telemetry**

| Service | What it does |
| --- | --- |
| [AssetsStorage](docs/services/AssetsStorage.md) | downloading assets (textures, audio, bundles) with a local cache |
| [CloudCode](docs/services/CloudCode.md) | calling server-side functions |
| [Analytics](docs/services/Analytics.md) | events, sessions, playtime |
| [WebView ↗](https://mirrahub.com/documentation/mirra-cloud/sdk-webview) | in-app browser: pages, URL interception, events |

> Links marked ↗ go to the user-facing documentation.

---

## Example: Showcase

A ready-made example ships with the SDK and puts **every** service on a live UI — tables, cards,
avatars, progress bars, countdowns and interactive tools.

It is built on UI Toolkit and **VContainer**, so add VContainer first:

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0
```

1. `Window → Package Manager → Mirra Cloud SDK → Samples → Showcase → **Import**`.
2. Make sure the project is connected (step 3 above).
3. Open `Assets/Samples/Mirra Cloud SDK/<version>/Showcase/Scenes/MC_Showcase.unity`.
4. **Play** → auth screen → services grid → any service.

How it is put together is covered in
[`Samples~/Showcase/README.md`](Packages/com.mirrahub.cloud-sdk/Samples~/Showcase/README.md).

---

## Editor tools

| Tool | Where | What for |
| --- | --- | --- |
| **Manager** | `Tools → Mirra Cloud → Manager` | sign in with a service account key, pick project / branch / token, create tokens |
| **Request Inspector** | `MirraCloud → Request Inspector` | tracing the SDK's HTTP requests while debugging |
| **Developer Settings** | `Create → Mirra Cloud → Developer Settings` in any `Resources` folder | optional asset: environment profiles that override the API hosts for local development |

---

## Repository layout

The repository is a Unity project with the SDK inside it as its own package. The package is
embedded, so the project opens and runs as is, and Package Manager pulls the SDK from that same path
(`?path=/Packages/com.mirrahub.cloud-sdk`).

```
Packages/com.mirrahub.cloud-sdk/     ← what UPM installs
├── package.json
├── Runtime/          SDK runtime: services, HTTP client, realtime, storage, logging
│   └── External/     vendored SimpleWebTransport (WebSocket, WebGL included)
├── Editor/           the Manager window and Request Inspector
├── ThirdParty/       vendored dependencies, unmodified upstream copies
│   ├── UnityWebView/ net.gree.unity-webview 1.0.0 (zlib)
│   └── SqliteNet/    com.gilzoide.sqlite-net 1.3.2 (MIT) + native SQLite
├── Tests/            edit-mode tests
└── Samples~/         Showcase — the example covering every service (the tilde hides it from Unity)
Assets/               dev-project scaffolding; the imported sample lands here too
docs/                 per-service reference
```

`ThirdParty/` is where the two plugins that used to be separate installs now live. They keep their
own assembly names (`unity-webview`, `Gilzoide.SqliteNet`), guids and import settings, so they
behave exactly as they do upstream — which is also why installing them again alongside the SDK
collides instead of merging.

The package's tests only show up in the Test Runner for someone who adds
`"testables": [ "com.mirrahub.cloud-sdk" ]` to their `Packages/manifest.json`. In this repository the
package is embedded, so they are visible right away.

---

## Feedback

- A bug or a question about the SDK — [repository issues](https://github.com/Mirra-Hub/Unity-Cloud-Sdk/issues).
- Questions about the cloud, your project or pricing — through the [mirrahub.com](https://mirrahub.com) dashboard.

---

## License

[MIT](LICENSE.md). Third-party code and fonts that ship with the SDK keep their own licenses — they
are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The SDK is still `0.x`: the public API can change between minor versions without notice. Breaking
changes are called out in `CHANGELOG.md`.

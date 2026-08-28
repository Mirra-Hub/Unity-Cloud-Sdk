# Mirra Cloud SDK

A cloud backend for games — accounts, saves, economy, leaderboards, social systems, LiveOps and
analytics. 23 services, no server of your own.

The full description, a quick start and the per-service reference live in the
[repository README](https://github.com/Mirra-Hub/Unity-Cloud-Sdk#readme) and in the
[Mirra Cloud documentation](https://mirrahub.com/documentation/mirra-cloud).

---

## Installation

One URL — `Window → Package Manager → + → Add package from git URL…`:

```
https://github.com/Mirra-Hub/Unity-Cloud-Sdk.git?path=/Packages/com.mirrahub.cloud-sdk#v0.2.1
```

The package is self-contained. The native plugins it needs — a WebView for external sign-in and
purchase flows, SQLite for the local asset cache — ship inside it under `ThirdParty/`.

> **Do not also install `net.gree.unity-webview` or `com.gilzoide.sqlite-net`.** They are already
> here. A second copy means two assemblies with the same name and two sets of native libraries with
> the same filenames, and Unity refuses to build that.

You need git 2.14+ on your `PATH`.

Check: the **Tools → Mirra Cloud** entry appears in Unity's top menu.

## Connecting a project

`Tools → Mirra Cloud → Manager` → paste your service account key → **Connect** → pick a **Project**,
**Branch** and **API Token**.

The choice is saved to `Assets/MirraCloud/Resources/Configuration.asset` — the asset is created for
you and belongs to your project, not to the package. **It holds your project API token: do not
commit it to a public repository.** The service account key lives only in `EditorPrefs` and never
reaches a build.

## Example

`Package Manager → Mirra Cloud SDK → Samples → Showcase → Import`. The sample puts every service on
a live UI; it is built with UI Toolkit and **VContainer**, which is installed separately as well:

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0
```

After importing, open `Assets/Samples/Mirra Cloud SDK/<version>/Showcase/Scenes/MC_Showcase.unity`.

## License

[MIT](LICENSE.md). Third-party code and fonts inside the package are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

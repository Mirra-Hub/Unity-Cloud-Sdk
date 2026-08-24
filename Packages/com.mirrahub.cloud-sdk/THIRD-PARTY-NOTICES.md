# Third party notices

The Mirra Cloud SDK is distributed under [MIT](LICENSE.md). Third-party code and fonts ship with it
under their own licenses, listed below. Full license texts sit next to the files themselves.

---

## SimpleWebTransport

WebSocket transport (WebGL included), vendored with patches.

- Author: James Frowen
- License: MIT
- Path: `Runtime/External/SimpleWebTransport/`
- License text: [`Runtime/External/SimpleWebTransport/LICENSE.md`](Runtime/External/SimpleWebTransport/LICENSE.md)
- Patches applied: [`Runtime/External/SimpleWebTransport/PATCHES.md`](Runtime/External/SimpleWebTransport/PATCHES.md)

```
Copyright (c) 2020 James Frowen
```

---

## Liberation Sans

The body text font in the Showcase example.

- Author: Red Hat, Inc. (digitized data — Google Corporation)
- License: SIL Open Font License 1.1
- Path: `Samples~/Showcase/UI/Fonts/LiberationSans.ttf`
- License text: [`Samples~/Showcase/UI/Fonts/LiberationSans - OFL.txt`](Samples~/Showcase/UI/Fonts/LiberationSans%20-%20OFL.txt)

```
Digitized data copyright (c) 2010 Google Corporation
    with Reserved Font Arimo, Tinos and Cousine.
Copyright (c) 2012 Red Hat, Inc.
    with Reserved Font Name Liberation.
```

---

## Lucide

The icon font in the Showcase example (the `.sc-icon` class).

- Author: Lucide Icons and Contributors
- License: ISC
- Path: `Samples~/Showcase/UI/Fonts/lucide.ttf`
- License text: [`Samples~/Showcase/UI/Fonts/LUCIDE-LICENSE.txt`](Samples~/Showcase/UI/Fonts/LUCIDE-LICENSE.txt)

```
Copyright (c) 2026 Lucide Icons and Contributors
```

---

## unity-webview

Native WebView views — external sign-in providers and purchase flows run on it. Vendored **unmodified**;
see `ThirdParty/UnityWebView/VENDORED.md`.

- Authors: Keijiro Takahashi, GREE, Inc. — https://github.com/gree/unity-webview
- Version: 1.0.0
- License: zlib
- Path: `ThirdParty/UnityWebView/`
- License text: [`ThirdParty/UnityWebView/LICENSE`](ThirdParty/UnityWebView/LICENSE)

```
Copyright (C) 2011 Keijiro Takahashi
Copyright (C) 2012 GREE, Inc.
```

---

## SQLite-net for Unity

Local durable storage — the asset cache lives on it. Vendored; see
`ThirdParty/SqliteNet/VENDORED.md`.

- Author: Gil Barbosa Reis — https://github.com/gilzoide/unity-sqlite-net
- Version: 1.3.2
- License: MIT
- Path: `ThirdParty/SqliteNet/`
- License text: [`ThirdParty/SqliteNet/LICENSE.txt`](ThirdParty/SqliteNet/LICENSE.txt)

```
Copyright (c) 2024 Gil Barbosa Reis
```

It bundles two further works:

- **sqlite-net** by Krueger Systems, Inc. (MIT) — the ORM in
  `ThirdParty/SqliteNet/Runtime/sqlite-net/`, license text alongside it.
- **SQLite** itself (public domain) — the amalgamation in
  `ThirdParty/SqliteNet/Plugins/sqlite-amalgamation/`.

---

Resolved by Package Manager rather than shipped:
[`com.unity.editorcoroutines`](https://docs.unity3d.com/Packages/com.unity.editorcoroutines@1.0/manual/index.html)
(Unity Companion License), declared as a dependency of this package. The Showcase sample
additionally needs [VContainer](https://github.com/hadashiA/VContainer) (MIT), installed separately.

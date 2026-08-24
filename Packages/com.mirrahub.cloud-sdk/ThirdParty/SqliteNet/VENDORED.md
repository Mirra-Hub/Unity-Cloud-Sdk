# Vendored: com.gilzoide.sqlite-net

Copy of [`com.gilzoide.sqlite-net`](https://github.com/gilzoide/unity-sqlite-net) **1.3.2**, which
in turn bundles [sqlite-net](https://github.com/praeclarum/sqlite-net) by Krueger Systems and the
SQLite amalgamation itself.

It ships inside the Mirra Cloud SDK so that installing the SDK is a single git URL. The code that
Unity compiles is byte-identical to upstream — same `Gilzoide.SqliteNet` assembly name, same guids,
same native plugin import settings.

Left out of the copy, because the SDK does not need them: the upstream `Samples~`, `Tests`, the
`Plugins/sqlite-net~` git submodule (build-time source of `Runtime/sqlite-net`) and `Plugins/tools~`
(Docker build tooling). What is kept:

- `Runtime/`, `Editor/` — the C# API and the asset importers
- `Plugins/lib/` — prebuilt native libraries for macOS, Windows (x86/x64/arm64), Linux x64 and
  Android (arm32/arm64/x86/x64)
- `Plugins/sqlite-amalgamation/` — `sqlite3.c`, compiled from source for iOS and WebGL
- `Plugins/idbvfs/` — the IndexedDB VFS used on WebGL

**Do not install `com.gilzoide.sqlite-net` separately.** Two copies mean two assemblies with the
same name and two sets of native libraries with the same filenames, which Unity refuses to build.

To update: replace `Runtime/`, `Editor/` and `Plugins/` with a fresh copy of the upstream package,
keep this file, and note the new version here and in the SDK changelog.

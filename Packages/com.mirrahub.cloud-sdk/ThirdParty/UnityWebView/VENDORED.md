# Vendored: net.gree.unity-webview

Unmodified copy of [`net.gree.unity-webview`](https://github.com/gree/unity-webview) **1.0.0**,
taken from `https://github.com/gree/unity-webview.git?path=/dist/package`.

It ships inside the Mirra Cloud SDK so that installing the SDK is a single git URL. Nothing here is
altered: the assembly is still named `unity-webview`, the guids, import settings and the Android
build postprocessor are byte-identical to upstream.

The only addition is `LICENSE` — the upstream UPM package is distributed without one, and the zlib
license requires the notice to travel with the source.

**Do not install `net.gree.unity-webview` separately.** Two copies mean two assemblies with the same
name and two sets of native plugins with the same filenames, which Unity refuses to build.

To update: replace this folder with a fresh copy of the upstream package, keep `LICENSE` and this
file, and note the new version here and in the SDK changelog.

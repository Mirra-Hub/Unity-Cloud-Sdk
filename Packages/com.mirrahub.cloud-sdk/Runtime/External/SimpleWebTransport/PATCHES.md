# Vendored SimpleWebTransport — local patches

Source: https://github.com/James-Frowen/SimpleWebTransport @ **v3.1.0** (MIT, see `LICENSE.md`).

Only the **client** subset is vendored: `Client/`, `Common/`, `AssemblyInfo.cs`, and the asmdef.
The `Server/` folder and `SslConfigLoader.cs` are intentionally omitted (the SDK is client-only).

The assembly is renamed to **`MirraCloud.External.SimpleWeb`** (asmdef `name`, `autoReferenced:false`)
to avoid clashing with a real SimpleWebTransport / Mirror package, and is referenced explicitly from
`MirraCloudSDK.asmdef`. The C# namespace is left as `JamesFrowen.SimpleWeb`.

## Patch 1 — `Client/Webgl/plugin/SimpleWeb.jslib`: accept TEXT frames

The MirraCloud realtime gateway sends WebSocket **TEXT** frames (UTF-8 JSON). Vanilla SimpleWeb's
`webSocket.onmessage` only handled binary `ArrayBuffer` frames and logged
`"message type not supported"` for text — so the SDK received nothing on WebGL.

`onmessage` was rewritten to also handle `typeof event.data === "string"` by UTF-8 encoding the text
(`new TextEncoder().encode(...)`) into the same byte path used for binary frames. Outgoing frames are
left **binary** (`SimpleWeb_Send` unchanged); the gateway decodes any opcode as UTF-8 JSON.

**Re-apply on upgrade:** after pulling a newer SimpleWebTransport, re-edit `onmessage` in the jslib to
add the string branch (search for `"message type not supported"`).

## No C# patches

The C# client is used as-is via `SimpleWebClient.Create(...)`. The adapter that bridges it to the SDK's
`IRealtimeTransport` lives outside this folder, at
`Core/Realtime/Transport/WebGlWebSocketTransport.cs`, and is selected only on WebGL by
`Core/Realtime/Transport/RealtimeTransportFactory.cs`.

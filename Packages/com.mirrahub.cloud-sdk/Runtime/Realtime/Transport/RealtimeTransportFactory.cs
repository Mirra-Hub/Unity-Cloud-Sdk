using MirraCloud.Core.Logger;
using MirraCloud.Core.Realtime.Abstractions;

namespace MirraCloud.Core.Realtime.Transport
{
    /// <summary>
    /// Selects the realtime <see cref="IRealtimeTransport"/> implementation per platform. WebGL
    /// player builds use <see cref="WebGlWebSocketTransport"/> (browser WebSocket via the vendored
    /// SimpleWeb client), because System.Net.WebSockets.ClientWebSocket is not supported there. All
    /// other targets — and the Editor, for convenient debugging — use
    /// <see cref="ClientWebSocketTransport"/>.
    /// </summary>
    internal static class RealtimeTransportFactory
    {
        public static IRealtimeTransport Create(ILogger logger, ICoroutineRunner coroutineRunner)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGlWebSocketTransport(logger, coroutineRunner);
#else
            return new ClientWebSocketTransport(logger);
#endif
        }
    }
}

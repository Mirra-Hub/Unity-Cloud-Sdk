using MirraCloud.Core.Realtime.Auth;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Realtime.Abstractions
{
    public interface IRealtimeAuthProvider
    {
        AsyncOperation<RestApiResult<RealtimeSessionInfo>> CreateSessionAsync();
    }
}

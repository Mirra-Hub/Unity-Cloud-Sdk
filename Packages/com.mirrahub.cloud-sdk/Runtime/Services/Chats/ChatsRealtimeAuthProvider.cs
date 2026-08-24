using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Core.Realtime.Auth;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Chats
{
    internal sealed class ChatsRealtimeAuthProvider : IRealtimeAuthProvider
    {
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;

        public ChatsRealtimeAuthProvider(RestApiClient restApi, Configuration configuration)
        {
            _restApi = restApi;
            _configuration = configuration;
        }

        public AsyncOperation<RestApiResult<RealtimeSessionInfo>> CreateSessionAsync()
        {
            var route = $"/chats/v1/projects/{_configuration.ProjectId}/rt/session";
            return _restApi.PostAsync<RealtimeSessionInfo>(route);
        }
    }
}

using MirraCloud;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using MirraCloud.Core.Friends.Dto;
using MirraCloud.Core.Logger;

namespace MirraCloud.Core.Friends
{
    public class FriendsService : ICloudSdkService
    {
        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;

        private const string ControllerApi = "/friends/v1/projects";

        public FriendsService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
        }

        #region Friends

        public AsyncOperation<RestApiResult<GetPlayerDto[]>> GetFriendsAsync(bool getProfilesInfo = true)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/friends?getProfilesInfo={getProfilesInfo.ToString().ToLowerInvariant()}";
            return _restApi.GetAsync<GetPlayerDto[]>(route);
        }

        public AsyncOperation<RestApiResult> RemoveFriendAsync(string targetPlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/friends/{targetPlayerId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult> BanAsync(string targetPlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/ban/{targetPlayerId}";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> BanManyAsync(string[] targetPlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/ban";
            return _restApi.PostAsync(route, targetPlayerIds);
        }

        #endregion

        #region Requests

        public AsyncOperation<RestApiResult> SendAsync(string targetPlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/{targetPlayerId}/send";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> SendManyAsync(string[] targetPlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/send";
            return _restApi.PostAsync(route, targetPlayerIds);
        }

        public AsyncOperation<RestApiResult> RevokeAsync(string targetPlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/{targetPlayerId}/revoke";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> RevokeManyAsync(string[] targetPlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/revoke";
            return _restApi.PostAsync(route, targetPlayerIds);
        }

        public AsyncOperation<RestApiResult> AcceptAsync(string sourcePlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/{sourcePlayerId}/accept";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> AcceptManyAsync(string[] sourcePlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/accept";
            return _restApi.PostAsync(route, sourcePlayerIds);
        }

        public AsyncOperation<RestApiResult> RejectAsync(string sourcePlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/{sourcePlayerId}/reject";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> RejectManyAsync(string[] sourcePlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/reject";
            return _restApi.PostAsync(route, sourcePlayerIds);
        }

        public AsyncOperation<RestApiResult<GetFriendRequestDto[]>> GetRequestsAsync()
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests";
            return _restApi.GetAsync<GetFriendRequestDto[]>(route);
        }

        public AsyncOperation<RestApiResult<GetFriendRequestDto[]>> GetOutgoingAsync()
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/outgoing";
            return _restApi.GetAsync<GetFriendRequestDto[]>(route);
        }

        public AsyncOperation<RestApiResult<GetFriendRequestDto[]>> GetIncomingAsync()
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/incoming";
            return _restApi.GetAsync<GetFriendRequestDto[]>(route);
        }

        public AsyncOperation<RestApiResult> DeleteAsync(string targetPlayerId)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests/{targetPlayerId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult> DeleteManyAsync(string[] targetPlayerIds)
        {
            var route = $"{ControllerApi}/{_configuration.ProjectId}/players/requests";
            var config = new RestRequestConfig
            {
                Body = targetPlayerIds
            };
            return _restApi.DeleteAsync(route, config);
        }

        #endregion

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

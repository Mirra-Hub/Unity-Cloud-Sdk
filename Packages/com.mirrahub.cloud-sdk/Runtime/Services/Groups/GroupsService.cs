using MirraCloud;
using MirraCloud.Core.Groups.Dto.Request;
using MirraCloud.Core.Groups.Dto.Response;
using MirraCloud.Core.Logger;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Groups
{
    public class GroupsService : ICloudSdkService
    {
        private const string GroupsApi = "/groups/v1/projects";
        private const string PlayersApi = "/groups/v1/projects";

        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        public GroupsService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
        }

        #region Groups

        public AsyncOperation<RestApiResult<GroupDto>> CreateAsync(CreateGroupDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups";
            return _restApi.PostAsync<GroupDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<GroupDto>> GetAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}";
            return _restApi.GetAsync<GroupDto>(route);
        }

        public AsyncOperation<RestApiResult<GroupDto>> UpdateAsync(string groupId, UpdateGroupDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}";
            return _restApi.PatchAsync<GroupDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> DeleteAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult<PaginatedResult<GroupListItemDto>>> SearchAsync(
            string query = null, string visibility = null, int page = 1, int pageSize = 20)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/search?page={page}&pageSize={pageSize}";
            if (query != null)
            {
                route += $"&query={UnityEngine.Networking.UnityWebRequest.EscapeURL(query)}";
            }
            if (visibility != null)
            {
                route += $"&visibility={visibility}";
            }
            return _restApi.GetAsync<PaginatedResult<GroupListItemDto>>(route);
        }

        public AsyncOperation<RestApiResult> JoinAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/join";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult<ChatConfigDto>> CreateChatAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/chat";
            return _restApi.PostAsync<ChatConfigDto>(route);
        }

        #endregion

        #region Members

        public AsyncOperation<RestApiResult<PaginatedResult<MemberDto>>> GetMembersAsync(
            string groupId, int page = 1, int pageSize = 20)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/members?page={page}&pageSize={pageSize}";
            return _restApi.GetAsync<PaginatedResult<MemberDto>>(route);
        }

        public AsyncOperation<RestApiResult<MemberDto>> UpdateMemberRoleAsync(
            string groupId, string profileId, UpdateMemberRoleDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/members/{profileId}";
            return _restApi.PatchAsync<MemberDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> KickMemberAsync(string groupId, string profileId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/members/{profileId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult> LeaveAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/members/me";
            return _restApi.DeleteAsync(route);
        }

        #endregion

        #region Player Groups

        public AsyncOperation<RestApiResult<PaginatedResult<GroupListItemDto>>> GetMyGroupsAsync(
            int page = 1, int pageSize = 20)
        {
            var route = $"{PlayersApi}/{_configuration.ProjectId}/players/me/groups?page={page}&pageSize={pageSize}";
            return _restApi.GetAsync<PaginatedResult<GroupListItemDto>>(route);
        }

        public AsyncOperation<RestApiResult<PaginatedResult<GroupListItemDto>>> GetPlayerGroupsAsync(
            string playerId, int page = 1, int pageSize = 20)
        {
            var route = $"{PlayersApi}/{_configuration.ProjectId}/players/{playerId}/groups?page={page}&pageSize={pageSize}";
            return _restApi.GetAsync<PaginatedResult<GroupListItemDto>>(route);
        }

        #endregion

        #region Roles

        public AsyncOperation<RestApiResult<RoleDto[]>> GetRolesAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/roles";
            return _restApi.GetAsync<RoleDto[]>(route);
        }

        public AsyncOperation<RestApiResult<RoleDto>> CreateRoleAsync(string groupId, CreateRoleDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/roles";
            return _restApi.PostAsync<RoleDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<RoleDto>> UpdateRoleAsync(string groupId, string roleId, UpdateRoleDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/roles/{roleId}";
            return _restApi.PatchAsync<RoleDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> DeleteRoleAsync(string groupId, string roleId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/roles/{roleId}";
            return _restApi.DeleteAsync(route);
        }

        #endregion

        #region Bans

        public AsyncOperation<RestApiResult<PaginatedResult<BanDto>>> GetBansAsync(
            string groupId, int page = 1, int pageSize = 20)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/bans?page={page}&pageSize={pageSize}";
            return _restApi.GetAsync<PaginatedResult<BanDto>>(route);
        }

        public AsyncOperation<RestApiResult> BanPlayerAsync(string groupId, BanPlayerDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/bans";
            return _restApi.PostAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> UnbanPlayerAsync(string groupId, string profileId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/bans/{profileId}";
            return _restApi.DeleteAsync(route);
        }

        #endregion

        #region Invites

        public AsyncOperation<RestApiResult<InviteDto>> CreateInviteAsync(string groupId, CreateInviteDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites";
            return _restApi.PostAsync<InviteDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> RevokeInviteAsync(string groupId, string inviteId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/{inviteId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult> AcceptInviteAsync(string groupId, string inviteId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/{inviteId}/accept";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> DeclineInviteAsync(string groupId, string inviteId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/{inviteId}/decline";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult<InviteKeyDto>> CreateInviteKeyAsync(string groupId, CreateInviteKeyDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/keys";
            return _restApi.PostAsync<InviteKeyDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> DeleteInviteKeyAsync(string groupId, string inviteKeyId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/keys/{inviteKeyId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult> JoinByKeyAsync(string groupId, JoinByKeyDto dto)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/invites/join-by-key";
            return _restApi.PostAsync(route, dto);
        }

        #endregion

        #region Join Requests

        public AsyncOperation<RestApiResult<JoinRequestDto>> CreateJoinRequestAsync(string groupId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/join-requests";
            return _restApi.PostAsync<JoinRequestDto>(route);
        }

        public AsyncOperation<RestApiResult<PaginatedResult<JoinRequestDto>>> GetJoinRequestsAsync(
            string groupId, string statusFilter = null, int page = 1, int pageSize = 20)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/join-requests?page={page}&pageSize={pageSize}";
            if (statusFilter != null)
            {
                route += $"&statusFilter={statusFilter}";
            }
            return _restApi.GetAsync<PaginatedResult<JoinRequestDto>>(route);
        }

        public AsyncOperation<RestApiResult> ApproveJoinRequestAsync(string groupId, string requestId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/join-requests/{requestId}/approve";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> RejectJoinRequestAsync(string groupId, string requestId)
        {
            var route = $"{GroupsApi}/{_configuration.ProjectId}/groups/{groupId}/join-requests/{requestId}/reject";
            return _restApi.PostAsync(route);
        }

        #endregion

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

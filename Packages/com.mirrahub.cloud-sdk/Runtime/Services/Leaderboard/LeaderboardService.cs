using System;
using System.Collections.Generic;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Entities;
using MirraCloud.Core.Logger;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.PlayerAccount;

namespace MirraCloud.Core.Leaderboard
{
    public class LeaderboardService : ICloudSdkService
    {        
        private const string ControllerApi = "/leaderboards/v1";
        
        private readonly IJsonService _jsonService;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly PlayerAccountService _playerAccountService;

        private readonly List<LeaderboardConfig> _leaderboardConfigs = new List<LeaderboardConfig>();
        public IReadOnlyList<LeaderboardConfig> LeaderboardConfigs => _leaderboardConfigs;

        public LeaderboardService(Configuration configuration, PlayerAccountService playerAccountService, ILogger logger, IJsonService jsonService, RestApiClient restApi) 
        {
            _configuration = configuration;
            _playerAccountService = playerAccountService;
            _restApi = restApi;
            _logger = logger;
            _jsonService = jsonService;
        }

        public AsyncOperation<RestApiResult<LeaderboardConfigDto[]>> InitializeAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards";

            var operation = _restApi.GetAsync<LeaderboardConfigDto[]>(route);

            operation.UseCompleted(completed =>
            {
                _leaderboardConfigs.Clear();

                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    foreach (var leaderboardConfigDto in completed.Result.Data)
                    {
                        _leaderboardConfigs.Add(new LeaderboardConfig(leaderboardConfigDto));
                    }
                }
            });
            
            return operation;
        }

        public AsyncOperation<RestApiResult<LeaderboardConfigDto>> GetConfigAsync(string leaderboardId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}";
            return _restApi.GetAsync<LeaderboardConfigDto>(route);
        }

        /// <summary>
        /// Joins the current player to the leaderboard. Scores are only accepted from participants,
        /// so this has to happen once before the first <see cref="SubmitScoreAsync(double, string)"/>.
        /// Joining twice is harmless. Returns the player's entry, empty until a score is submitted.
        /// </summary>
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> JoinAsync(string leaderboardId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/join";

            return _restApi.PostAsync<LeaderboardEntryDto>(route, new { });
        }

        /// <summary>Removes the current player from the leaderboard along with their result.</summary>
        public AsyncOperation<RestApiResult> LeaveAsync(string leaderboardId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/leave";

            return _restApi.PostAsync(route, new { });
        }

        public AsyncOperation<RestApiResult> SubmitScoreAsync(DateTime score, string leaderboardId)
        {
            return SubmitScoreAsync(score.ToOADate(), leaderboardId);
        }
        
        public AsyncOperation<RestApiResult> SubmitScoreAsync(double score, string leaderboardId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries";

            SubmitScoreDto submitScoreDto = new SubmitScoreDto()
            {
                PlayerName = _playerAccountService.PlayerAccountInfo.Nickname,
                Value = score,
            };
            
            var operation = _restApi.PostAsync(route, submitScoreDto);

            return operation; 
        }
        
        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntries(string leaderboardId, int top = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/top?entriesCount={top}";

            var operation = _restApi.GetAsync<LeaderboardEntriesDto>(route);

            return operation;
        }

        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntriesByCountry(string leaderboardId, int entriesCount = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/top-by-country?entriesCount={entriesCount}";
            return _restApi.GetAsync<LeaderboardEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntriesByFriends(string leaderboardId, string[] friendIds)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/top-by-friends";
            var dto = new FriendsTopRequestDto { FriendIds = friendIds ?? Array.Empty<string>() };
            return _restApi.PostAsync<LeaderboardEntriesDto>(route, dto);
        }
        
        public AsyncOperation<RestApiResult<LeaderboardAroundEntriesDto>> GetLeaderboardPlayerAroundEntries(string leaderboardId, int around = 10)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/around?entriesRange={around}";

            var operation = _restApi.GetAsync<LeaderboardAroundEntriesDto>(route);

            return operation;
        }
        
        public AsyncOperation<RestApiResult<LeaderboardTopAndPlayersAroundDto>> GetLeaderboardEntries(string leaderboardId, int top = 100, int around = 10)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries/top-and-around?topEntriesCount={top}&aroundEntriesRange={around}";

            var operation = _restApi.GetAsync<LeaderboardTopAndPlayersAroundDto>(route);

            return operation;
        }
        
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> GetLeaderboardPlayer(string leaderboardId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/leaderboards/{leaderboardId}/entries";

            var operation = _restApi.GetAsync<LeaderboardEntryDto>(route);

            return operation;
        }

        public AsyncOperation<RestApiResult<PlayerRewardsDto>> GetRewardsAsync(bool reset = true)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/rewards?reset={reset.ToString().ToLowerInvariant()}";
            return _restApi.GetAsync<PlayerRewardsDto>(route);
        }

        public AsyncOperation<RestApiResult> SubmitRewardsAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/rewards";
            return _restApi.PostAsync(route, new { });
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

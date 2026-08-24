using System.Collections.Generic;
using MirraCloud;
using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Challenges.Dto;
using Plugins.MirraCloud.Core.Services.Challenges.Entities;
using Plugins.MirraCloud.Core.Services.PlayerAccount;
using MirraCloud.Core;

namespace Plugins.MirraCloud.Core.Services.Challenges
{
    public class ChallengesService : ICloudSdkService
    {
        private const string ControllerApi = "/challenges/v1";

        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;
        private readonly PlayerAccountService _playerAccountService;

        private readonly List<ChallengeConfig> _challengeConfigs = new List<ChallengeConfig>();
        public IReadOnlyList<ChallengeConfig> ChallengeConfigs => _challengeConfigs;

        public bool IsInitialized { get; private set; }

        public ChallengesService(Configuration configuration, PlayerAccountService playerAccountService, RestApiClient restApi)
        {
            _configuration = configuration;
            _playerAccountService = playerAccountService;
            _restApi = restApi;
        }

        public AsyncOperation<RestApiResult<ChallengeConfigDto[]>> InitializeAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges";

            var operation = _restApi.GetAsync<ChallengeConfigDto[]>(route);

            operation.UseCompleted(completed =>
            {
                _challengeConfigs.Clear();

                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    foreach (var dto in completed.Result.Data)
                    {
                        _challengeConfigs.Add(new ChallengeConfig(dto));
                    }
                }

                IsInitialized = true;
            });

            return operation;
        }

        public AsyncOperation<RestApiResult<ChallengeConfigDto>> GetConfigAsync(string challengeId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}";
            return _restApi.GetAsync<ChallengeConfigDto>(route);
        }

        public AsyncOperation<RestApiResult<SubmitScoreResponseDto>> SubmitScoreAsync(string challengeId, double score, string playerName = null)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries";

            var name = playerName ?? _playerAccountService?.PlayerAccountInfo?.Nickname ?? string.Empty;

            var dto = new SubmitScoreDto
            {
                PlayerName = name,
                Value = score,
            };

            return _restApi.PostAsync<SubmitScoreResponseDto>(route, dto);
        }

        public AsyncOperation<RestApiResult> JoinAsync(string challengeId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/join";
            return _restApi.PostAsync(route, new { });
        }

        public AsyncOperation<RestApiResult> LeaveAsync(string challengeId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/leave";
            return _restApi.PostAsync(route, new { });
        }

        public AsyncOperation<RestApiResult<ChallengeEntriesDto>> GetTopAsync(string challengeId, int entriesCount = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/top/global?entriesCount={entriesCount}";
            return _restApi.GetAsync<ChallengeEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<ChallengeEntriesDto>> GetMyTopAsync(string challengeId, int entriesCount = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/top?entriesCount={entriesCount}";
            return _restApi.GetAsync<ChallengeEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<ChallengeEntriesDto>> GetAroundPlayerAsync(string challengeId, int entriesRange = 10)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/around/global?entriesRange={entriesRange}";
            return _restApi.GetAsync<ChallengeEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<ChallengeEntryDto>> GetPlayerAsync(string challengeId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/global";
            return _restApi.GetAsync<ChallengeEntryDto>(route);
        }

        public AsyncOperation<RestApiResult<SubmitScoreResponseDto>> ClaimRewardAsync(string challengeId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/challenges/{challengeId}/entries/claim";
            return _restApi.PostAsync<SubmitScoreResponseDto>(route, new { });
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

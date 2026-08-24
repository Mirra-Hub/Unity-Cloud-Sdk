using System;
using System.Collections.Generic;
using MirraCloud;
using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.PlayerAccount;
using Plugins.MirraCloud.Core.Services.Tournaments.Dto;

namespace Plugins.MirraCloud.Core.Services.Tournaments
{
    public class TournamentsService : ICloudSdkService
    {
        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;
        private readonly PlayerAccountService _playerAccountService;
        private readonly List<TournamentConfig> _tournamentConfigs = new List<TournamentConfig>();

        private const string ControllerApi = "/tournaments/v1";

        public bool IsInitialized { get; private set; }
        public IReadOnlyList<TournamentConfig> TournamentConfigs => _tournamentConfigs;

        public TournamentsService(Configuration configuration, RestApiClient restApi, PlayerAccountService playerAccountService)
        {
            _restApi = restApi;
            _configuration = configuration;
            _playerAccountService = playerAccountService;
        }

        public AsyncOperation<RestApiResult<TournamentConfigDto[]>> InitializeAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments";

            var operation = _restApi.GetAsync<TournamentConfigDto[]>(route);

            operation.UseCompleted(completed =>
            {
                
                _tournamentConfigs.Clear();

                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    foreach (var tournamentConfigDto in completed.Result.Data)
                    {
                        _tournamentConfigs.Add(new TournamentConfig(tournamentConfigDto));
                    }
                }

                IsInitialized = true;
            });
            
            return operation;
        }

        public AsyncOperation<RestApiResult<TournamentConfigDto>> GetConfigAsync(string tournamentId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}";
            return _restApi.GetAsync<TournamentConfigDto>(route);
        }

        public AsyncOperation<RestApiResult<TournamentEntriesDto>> GetTopAsync(string tournamentId, string tableId, int entriesCount = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/top?tableId={tableId}&entriesCount={entriesCount}";
            return _restApi.GetAsync<TournamentEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<TournamentEntriesDto>> GetTopByCountryAsync(string tournamentId, string tableId, int entriesCount = 100)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/top-by-country?tableId={tableId}&entriesCount={entriesCount}";
            return _restApi.GetAsync<TournamentEntriesDto>(route);
        }

        public AsyncOperation<RestApiResult<TournamentEntriesDto>> GetTopByFriendsAsync(string tournamentId, string tableId, string[] friendIds)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/top-by-friends?tableId={tableId}";
            var dto = new FriendsTopRequestDto { friendIds = friendIds ?? Array.Empty<string>() };
            return _restApi.PostAsync<TournamentEntriesDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<TournamentPlayersAroundDto>> GetAroundAsync(string tournamentId, string tableId, int entriesRange = 10)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/around?tableId={tableId}&entriesRange={entriesRange}";
            return _restApi.GetAsync<TournamentPlayersAroundDto>(route);
        }

        public AsyncOperation<RestApiResult<TournamentTopAndPlayersAroundDto>> GetTopAndAroundAsync(string tournamentId, string tableId, int topEntriesCount = 100, int aroundEntriesRange = 10)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/top-and-around?tableId={tableId}&topEntriesCount={topEntriesCount}&aroundEntriesRange={aroundEntriesRange}";
            return _restApi.GetAsync<TournamentTopAndPlayersAroundDto>(route);
        }

        public AsyncOperation<RestApiResult<TournamentEntryDto>> GetPlayerAsync(string tournamentId, string tableId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries?tableId={tableId}";
            return _restApi.GetAsync<TournamentEntryDto>(route);
        }

        /// <summary>
        /// Joins the current player to the tournament and places them into a league. Scores are only
        /// accepted from participants, so this has to happen once before the first
        /// <see cref="SubmitScoreAsync"/>. Joining twice is harmless. Returns the player's entry.
        /// </summary>
        public AsyncOperation<RestApiResult<TournamentEntryDto>> JoinAsync(string tournamentId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/join";

            return _restApi.PostAsync<TournamentEntryDto>(route, new { });
        }

        /// <summary>Removes the current player from the tournament along with their result.</summary>
        public AsyncOperation<RestApiResult> LeaveAsync(string tournamentId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries/leave";

            return _restApi.PostAsync(route, new { });
        }

        public AsyncOperation<RestApiResult> SubmitScoreAsync(string tournamentId, double score, string playerName = null)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/entries";

            var name = playerName ?? _playerAccountService?.PlayerAccountInfo?.Nickname ?? string.Empty;

            var dto = new SubmitScoreDto
            {
                playerName = name,
                value = score,
            };

            return _restApi.PostAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<PlayerLeagueMetaDto>> GetPlayerLeagueMetaAsync(string tournamentId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/tournaments/{tournamentId}/players-league";
            return _restApi.GetAsync<PlayerLeagueMetaDto>(route);
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

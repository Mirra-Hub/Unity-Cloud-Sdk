using System;
using System.Collections.Generic;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Entities;
using MirraCloud.Core.Logger;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Leaderboard
{
    /// <summary>
    /// Leaderboards of the project. Every method takes the board's <b>key</b> (<see cref="LeaderboardConfig.Key"/>),
    /// not its console id.
    /// </summary>
    /// <remarks>
    /// A player joins a board once (<see cref="JoinAsync"/>), then submits scores. The name, icon and country shown
    /// on the board are the player's profile's own. Rewards are paid into Economy when a session ends: read them with
    /// <c>EconomyService.GetPendingRewardsAsync</c> and claim them with <c>EconomyService.ClaimRewardsAsync</c>.
    /// </remarks>
    public class LeaderboardService : ICloudSdkService
    {        
        private const string ControllerApi = "/leaderboards/v1";
        
        private readonly IJsonService _jsonService;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;

        private readonly List<LeaderboardConfig> _leaderboardConfigs = new List<LeaderboardConfig>();
        public IReadOnlyList<LeaderboardConfig> LeaderboardConfigs => _leaderboardConfigs;

        public LeaderboardService(Configuration configuration, ILogger logger, IJsonService jsonService, RestApiClient restApi) 
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _jsonService = jsonService;
        }

        private string BoardsPath => $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.Branch}/leaderboards";

        private string EntriesPath(string leaderboardKey) => $"{BoardsPath}/{Uri.EscapeDataString(leaderboardKey ?? string.Empty)}/entries";

        public AsyncOperation<RestApiResult<LeaderboardConfigDto[]>> InitializeAsync()
        {
            var operation = _restApi.GetAsync<LeaderboardConfigDto[]>(BoardsPath);

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

        public AsyncOperation<RestApiResult<LeaderboardConfigDto>> GetConfigAsync(string leaderboardKey)
        {
            return _restApi.GetAsync<LeaderboardConfigDto>($"{BoardsPath}/{Uri.EscapeDataString(leaderboardKey ?? string.Empty)}");
        }

        /// <summary>
        /// Joins the current player to the leaderboard. Scores are only accepted from participants,
        /// so this has to happen once before the first <see cref="SubmitScoreAsync(double, string)"/>.
        /// Joining twice is harmless. Returns the player's entry, null until a score is submitted.
        /// </summary>
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> JoinAsync(string leaderboardKey)
        {
            return _restApi.PostAsync<LeaderboardEntryDto>($"{EntriesPath(leaderboardKey)}/join", new { });
        }

        /// <summary>
        /// Removes the current player from the leaderboard along with their result in the current session. The
        /// rewards of a session that already ended are not affected.
        /// </summary>
        public AsyncOperation<RestApiResult> LeaveAsync(string leaderboardKey)
        {
            return _restApi.PostAsync($"{EntriesPath(leaderboardKey)}/leave", new { });
        }

        /// <summary>
        /// Submits a time to a <see cref="Enums.LeaderboardType.Time"/> board, as seconds with a fraction.
        /// </summary>
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> SubmitScoreAsync(TimeSpan time, string leaderboardKey)
        {
            return SubmitScoreAsync(time.TotalSeconds, leaderboardKey);
        }

        /// <summary>Reads <paramref name="score"/> as the time elapsed since <see cref="DateTime.MinValue"/>.</summary>
        [Obsolete("Pass the time as a TimeSpan: SubmitScoreAsync(TimeSpan, string). This overload submits the DateTime " +
                  "as the seconds elapsed since DateTime.MinValue; it used to send a fraction of a day and threw from one day up.")]
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> SubmitScoreAsync(DateTime score, string leaderboardKey)
        {
            return SubmitScoreAsync(new TimeSpan(score.Ticks), leaderboardKey);
        }
        
        /// <summary>
        /// Submits a score of the current player, who must have joined the board (<see cref="JoinAsync"/>); otherwise
        /// the server refuses it with <c>leaderboards.participation_required</c>. How the score combines with the
        /// stored one is the board's <see cref="Enums.UpdateStrategy"/>. Returns the player's entry after the submit,
        /// with their place.
        /// </summary>
        /// <remarks>NaN and infinities are refused here, without a request.</remarks>
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> SubmitScoreAsync(double score, string leaderboardKey)
        {
            if (double.IsNaN(score) || double.IsInfinity(score))
            {
                return AsyncOperation<RestApiResult<LeaderboardEntryDto>>.CreateCompleted(
                    RestApiResult<LeaderboardEntryDto>.ValidationFail("The score must be a finite number."));
            }

            return _restApi.PostAsync<LeaderboardEntryDto>(EntriesPath(leaderboardKey), new SubmitScoreDto { Value = score });
        }
        
        /// <summary>
        /// The top of the current player's table: their cohort on a board with cohorts, the whole board otherwise.
        /// Before the player's first score a board with cohorts has no table for them and returns an empty list.
        /// </summary>
        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntries(string leaderboardKey, int top = 100)
        {
            return _restApi.GetAsync<LeaderboardEntriesDto>($"{EntriesPath(leaderboardKey)}/top?entriesCount={top}");
        }

        /// <summary>The top of the whole board, every cohort together.</summary>
        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardGlobalTopEntries(string leaderboardKey, int top = 100)
        {
            return _restApi.GetAsync<LeaderboardEntriesDto>($"{EntriesPath(leaderboardKey)}/top/global?entriesCount={top}");
        }

        /// <summary>The top among players of the current player's country (the country of their profile).</summary>
        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntriesByCountry(string leaderboardKey, int entriesCount = 100)
        {
            return _restApi.GetAsync<LeaderboardEntriesDto>($"{EntriesPath(leaderboardKey)}/top-by-country?entriesCount={entriesCount}");
        }

        /// <summary>The top among the given players (profile ids).</summary>
        public AsyncOperation<RestApiResult<LeaderboardEntriesDto>> GetLeaderboardTopEntriesByFriends(string leaderboardKey, string[] friendIds)
        {
            var dto = new FriendsTopRequestDto { FriendIds = friendIds ?? Array.Empty<string>() };
            // A read sent as POST (the id list goes in the body): safe to repeat after a network failure.
            return _restApi.PostAsync<LeaderboardEntriesDto>($"{EntriesPath(leaderboardKey)}/top-by-friends", dto,
                new RestRequestConfig { Idempotent = true });
        }
        
        /// <summary>The players right above and below the current player in their table.</summary>
        public AsyncOperation<RestApiResult<LeaderboardAroundEntriesDto>> GetLeaderboardPlayerAroundEntries(string leaderboardKey, int around = 10)
        {
            return _restApi.GetAsync<LeaderboardAroundEntriesDto>($"{EntriesPath(leaderboardKey)}/around?entriesRange={around}");
        }
        
        /// <summary>The top of the whole board and the players around the current player on it, in one call.</summary>
        public AsyncOperation<RestApiResult<LeaderboardTopAndPlayersAroundDto>> GetLeaderboardEntries(string leaderboardKey, int top = 100, int around = 10)
        {
            return _restApi.GetAsync<LeaderboardTopAndPlayersAroundDto>(
                $"{EntriesPath(leaderboardKey)}/top-and-around?topEntriesCount={top}&aroundEntriesRange={around}");
        }
        
        /// <summary>
        /// The current player's entry with their place in their table. Before their first score this session the
        /// server answers 404 <c>leaderboards.entry_not_found</c>.
        /// </summary>
        public AsyncOperation<RestApiResult<LeaderboardEntryDto>> GetLeaderboardPlayer(string leaderboardKey)
        {
            return _restApi.GetAsync<LeaderboardEntryDto>(EntriesPath(leaderboardKey));
        }

        [Obsolete("Leaderboard rewards are paid into Economy: EconomyService.GetPendingRewardsAsync / ClaimRewardsAsync. " +
                  "This route does not exist on the server.")]
        public AsyncOperation<RestApiResult<PlayerRewardsDto>> GetRewardsAsync(bool reset = true)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.Branch}/rewards?reset={reset.ToString().ToLowerInvariant()}";
            return _restApi.GetAsync<PlayerRewardsDto>(route);
        }

        [Obsolete("Leaderboard rewards are paid into Economy: EconomyService.ClaimRewardsAsync. " +
                  "This route does not exist on the server.")]
        public AsyncOperation<RestApiResult> SubmitRewardsAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.Branch}/rewards";
            return _restApi.PostAsync(route, new { });
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

using MirraCloud.Core.DailyRewards.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.DailyRewards
{
    public class DailyRewardsService : ICloudSdkService
    {
        private const string ControllerApi = "/daily-rewards/v1";

        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;

        public DailyRewardsService(Configuration configuration, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
        }

        public AsyncOperation<RestApiResult<DailyRewardCalendarDto[]>> GetCalendarsAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/daily-rewards/calendars";
            return _restApi.GetAsync<DailyRewardCalendarDto[]>(route);
        }

        public AsyncOperation<RestApiResult<DailyRewardStatusDto[]>> GetStatusAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/daily-rewards/status";
            return _restApi.GetAsync<DailyRewardStatusDto[]>(route);
        }

        public AsyncOperation<RestApiResult<DailyRewardStatusDto>> GetStatusAsync(string calendarId)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/daily-rewards/status/{calendarId}";
            return _restApi.GetAsync<DailyRewardStatusDto>(route);
        }

        public AsyncOperation<RestApiResult<ClaimDailyRewardResponseDto>> ClaimAsync(string calendarId, int? dayNumber = null)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/daily-rewards/claim";

            var requestDto = new ClaimDailyRewardRequestDto
            {
                CalendarId = calendarId,
                DayNumber = dayNumber
            };

            return _restApi.PostAsync<ClaimDailyRewardResponseDto>(route, requestDto);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

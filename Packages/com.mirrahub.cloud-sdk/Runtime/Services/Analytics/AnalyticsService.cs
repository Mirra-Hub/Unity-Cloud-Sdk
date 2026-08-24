using System.Collections.Generic;
using MirraCloud;
using MirraCloud.Core;
using MirraCloud.Core.Logger;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Analytics.Dto;
using MirraCloud.Core;

namespace Plugins.MirraCloud.Core.Services.Analytics
{
    public class AnalyticsService : ICloudSdkService
    {
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private AnalyticsTracker _tracker;

        private const string ControllerApi = "/analytics/v1";

        public AnalyticsService(Configuration configuration, ILogger logger, RestApiClient restApiClient)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApiClient;
        }

        internal void SetTracker(AnalyticsTracker tracker)
        {
            _tracker = tracker;
        }

        public AsyncOperation<RestApiResult> SendEventAsync(string metricId)
        {
            return SubmitEventAsync(metricId);
        }

        public AsyncOperation<RestApiResult> SendEventAsync(string metricId, Dictionary<string, string> parameters)
        {
            return SubmitEventAsync(metricId, parameters);
        }

        public void EnqueueEvent(string eventName, Dictionary<string, string> parameters = null, List<string> tags = null)
        {
            if (_tracker != null)
            {
                _tracker.EnqueueEvent(eventName, parameters, tags);
                return;
            }

            _logger.Error("AnalyticsTracker is not initialized. Use SendEventAsync for immediate sending.");
        }

        public AsyncOperation<RestApiResult> SendSessionStartedAsync()
        {
            string route = BuildRoute("metrics/sessions-started");
            return PostWithErrorLogging(route, new { });
        }

        public AsyncOperation<RestApiResult> SendPlaytimeAsync(int playTimeInMinutes)
        {
            string route = BuildRoute("metrics/playtime");
            return PostWithErrorLogging(route, new PlaytimeDto { PlayTimeInMinutes = playTimeInMinutes });
        }

        public AsyncOperation<RestApiResult> SendBatchAsync(List<BatchEventItemDto> events)
        {
            string route = BuildRoute("events/batch");
            var dto = new BatchEventDto { Events = events };
            return PostWithErrorLogging(route, dto);
        }

        private AsyncOperation<RestApiResult> SubmitEventAsync(string metricId, Dictionary<string, string> parameters = null)
        {
            string route = BuildRoute($"custom-metrics/{metricId}");

            var dto = new SendEventDto();
            if (parameters != null)
                dto.Parameters = parameters;

            return PostWithErrorLogging(route, dto);
        }

        private string BuildRoute(string endpoint)
        {
            return $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/platforms/{_configuration.AnalyticsPlatformId}/{endpoint}";
        }

        private AsyncOperation<RestApiResult> PostWithErrorLogging(string route, object body)
        {
            var response = _restApi.PostAsync(route, body);
            response.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess)
                {
                    _logger.Error(completed.Result.Error?.Message ?? "Analytics request failed.");
                }
            });
            return response;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

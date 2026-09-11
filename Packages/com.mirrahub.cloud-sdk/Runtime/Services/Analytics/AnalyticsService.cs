using System;
using System.Collections.Generic;
using System.Text;
using MirraCloud;
using MirraCloud.Core;
using MirraCloud.Core.Logger;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Analytics.Dto;

namespace Plugins.MirraCloud.Core.Services.Analytics
{
    public class AnalyticsService : ICloudSdkService
    {
        private const string SessionIdHeader = "AnalyticsSessionId";

        private const int MaxRejectedEventsInLog = 3;

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private AnalyticsTracker _tracker;

        private readonly RestRequestConfig _sessionRequestConfig = new RestRequestConfig
        {
            Headers = new Dictionary<string, string>(1)
        };

        private const string ControllerApi = "/analytics/v1";

        /// <summary>
        /// The current play session: one per entry into the game. A new one begins when the player enters —
        /// a launch that restores the saved session or signs in, a sign-in after signing out, a sign-in as
        /// another account — and lasts until the app is closed or the player signs out; coming back from the
        /// background continues it. Every analytics request carries it in the <c>AnalyticsSessionId</c>
        /// header, and the server files events under it. <c>null</c> until analytics starts and again after
        /// sign-out.
        /// <para>
        /// Not the same thing as <c>AuthenticationService.SessionId</c>: that is the auth session, which a
        /// restored login keeps for as long as the refresh token lives, across any number of play sessions.
        /// </para>
        /// </summary>
        public string SessionId { get; private set; }

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

        /// <summary>
        /// Opens a new play session under a fresh id and reports <c>SessionsStarted</c> for it. The tracker
        /// calls this once per play session, which is what keeps the count of session starts equal to
        /// the number of sessions.
        /// </summary>
        internal AsyncOperation<RestApiResult> StartSession()
        {
            SessionId = Guid.NewGuid().ToString("D");
            _sessionRequestConfig.Headers[SessionIdHeader] = SessionId;
            return SendSessionStartedAsync();
        }

        /// <summary>Forgets the play session; later requests go out without the header until the next one starts.</summary>
        internal void EndSession()
        {
            SessionId = null;
            _sessionRequestConfig.Headers.Remove(SessionIdHeader);
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

        /// <summary>
        /// Reports <c>SessionsStarted</c> for the current play session (<see cref="SessionId"/>). The SDK
        /// already sends exactly one whenever a play session starts, so calling this by hand counts the
        /// current session twice.
        /// </summary>
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
            return PostWithErrorLogging(route, dto, result => WarnAboutRejectedEvents(result, events));
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

        private AsyncOperation<RestApiResult> PostWithErrorLogging(string route, object body, Action<RestApiResult> onSuccess = null)
        {
            var config = SessionId != null ? _sessionRequestConfig : null;
            var response = _restApi.PostAsync(route, body, config);
            response.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess)
                {
                    _logger.Error(completed.Result.Error?.Message ?? "Analytics request failed.");
                    return;
                }

                onSuccess?.Invoke(completed.Result);
            });
            return response;
        }

        /// <summary>
        /// <c>events/batch</c> answers 200 with <c>{ published, errors }</c> even when it rejected every item,
        /// so a successful status alone says nothing about delivery.
        /// </summary>
        private void WarnAboutRejectedEvents(RestApiResult result, List<BatchEventItemDto> events)
        {
            if (string.IsNullOrEmpty(result.ResponseBody))
            {
                return;
            }

            BatchEventResultDto report;
            try
            {
                report = _restApi.JsonService.FromJson<BatchEventResultDto>(result.ResponseBody);
            }
            catch (Exception)
            {
                return;
            }

            var errors = report?.Errors;
            if (errors == null || errors.Count == 0)
            {
                return;
            }

            var message = new StringBuilder();
            message.Append("Analytics: the server rejected ")
                .Append(errors.Count)
                .Append(" of ")
                .Append(report.Published + errors.Count)
                .Append(" batched event(s).");

            int shown = Math.Min(errors.Count, MaxRejectedEventsInLog);
            for (int i = 0; i < shown; i++)
            {
                var error = errors[i];
                if (error == null)
                {
                    continue;
                }

                message.Append("\n  #").Append(error.Index);

                if (events != null && error.Index >= 0 && error.Index < events.Count && events[error.Index] != null)
                {
                    message.Append(" '").Append(events[error.Index].EventName).Append('\'');
                }

                if (!string.IsNullOrEmpty(error.Code))
                {
                    message.Append(' ').Append(error.Code);
                }

                message.Append(": ").Append(error.Error ?? "no reason given");
            }

            if (errors.Count > shown)
            {
                message.Append("\n  and ").Append(errors.Count - shown).Append(" more.");
            }

            UnityEngine.Debug.LogWarning(message.ToString());
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

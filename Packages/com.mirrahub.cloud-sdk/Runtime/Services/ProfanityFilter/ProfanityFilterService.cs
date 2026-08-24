using MirraCloud.Core.ProfanityFilter.Requests;
using MirraCloud.Core.ProfanityFilter.Responses;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.ProfanityFilter
{
    /// <summary>
    /// Client wrapper around the cloud profanity-filter service. Sends arbitrary user
    /// text to the server, which checks it against the configured group's dictionary
    /// and returns a masked version plus the list of matched fragments.
    /// </summary>
    public class ProfanityFilterService : ICloudSdkService
    {
        private const string SERVICE_ROUTE = "/profanity-filter/v1";
        private const int MaxTextLength = 2000;

        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        public ProfanityFilterService(RestApiClient restApi, Configuration configuration, ILogger logger)
        {
            _restApi = restApi;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Checks the supplied text against the profanity filter.
        /// </summary>
        /// <param name="text">
        /// User-supplied text. Empty input short-circuits to a clean result without a
        /// network round-trip. Inputs longer than 2000 characters are rejected locally.
        /// </param>
        /// <param name="groupKey">
        /// Key of the profanity-filter group to check against. Required — there is no default
        /// group on the server: an empty key is rejected, and an unknown key returns 404.
        /// </param>
        public AsyncOperation<RestApiResult<ProfanityCheckResponse>> CheckAsync(
            string text, string groupKey)
        {
            if (string.IsNullOrEmpty(text))
            {
                return AsyncOperation<RestApiResult<ProfanityCheckResponse>>.CreateCompleted(
                    RestApiResult<ProfanityCheckResponse>.Success(new ProfanityCheckResponse
                    {
                        isClean = true,
                        maskedText = text ?? string.Empty,
                        matches = System.Array.Empty<ProfanityMatchDto>()
                    }));
            }

            if (text.Length > MaxTextLength)
            {
                return AsyncOperation<RestApiResult<ProfanityCheckResponse>>.CreateCompleted(
                    RestApiResult<ProfanityCheckResponse>.ValidationFail(
                        $"text exceeds {MaxTextLength} characters"));
            }

            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return AsyncOperation<RestApiResult<ProfanityCheckResponse>>.CreateCompleted(
                    RestApiResult<ProfanityCheckResponse>.ValidationFail("groupKey is required"));
            }

            var route =
                $"{SERVICE_ROUTE}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/check";
            var body = new ProfanityCheckRequest
            {
                text = text,
                groupKey = groupKey
            };

            return _restApi.PostAsync<ProfanityCheckResponse>(route, body);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

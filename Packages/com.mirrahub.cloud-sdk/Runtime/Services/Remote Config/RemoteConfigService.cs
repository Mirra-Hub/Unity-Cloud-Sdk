using MirraCloud.Core.RemoteConfig.Responses;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.RemoteConfig
{
    public class RemoteConfigService : ICloudSdkService
    {
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        public RemoteConfig Config { get; private set; }

        private const string SERVICE_ROUTE = "/remote-config/v1";

        public RemoteConfigService(RestApiClient restApi, Configuration configuration, ILogger logger)
        {
            _logger = logger;
            _restApi = restApi;
            _configuration = configuration;
        }

        public AsyncOperation<RestApiResult<FetchRemoteConfigResponse>> LoadConfigAsync()
        {
            var request = _restApi.GetAsync<FetchRemoteConfigResponse>($"{SERVICE_ROUTE}/projects/{_configuration.ProjectId}/config");

            request.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess)
                {
                    _logger.Error(completed.Result.Error?.Message ?? "Remote config request failed.");
                    return;
                }

                if (completed.Result.Data?.configs == null || completed.Result.Data.configs.Length == 0)
                {
                    return;
                }

                Config = new RemoteConfig(completed.Result.Data.configs[0]);
            });

            return request;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

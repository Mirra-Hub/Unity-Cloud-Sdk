using MirraCloud;
using MirraCloud.Core;
using MirraCloud.Core.Logger;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Deployment.Dto;

using MirraCloud.Core;

namespace Plugins.MirraCloud.Core.Services.Deployment
{
    public class DeploymentService : ICloudSdkService
    {
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;

        private const string ControllerApi = "/deployment/v1";

        public DeploymentService(Configuration configuration, ILogger logger, RestApiClient restApiClient)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApiClient;
        }

        public AsyncOperation<RestApiResult<ResolveBranchResponseDto>> ResolveBranchAsync(string version)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/resolve-branch";

            var response = _restApi.PostAsync<ResolveBranchResponseDto>(route, new ResolveBranchRequestDto
            {
                ClientVersion = version,
            });

            response.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess)
                {
                    _logger.Error(completed.Result.Error?.Message ?? "ResolveBranch request failed.");
                }
            });

            return response;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

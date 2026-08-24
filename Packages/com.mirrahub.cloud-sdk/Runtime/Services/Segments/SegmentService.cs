using System.Collections.Generic;
using MirraCloud;
using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.Segments.Dto;
using ILogger = MirraCloud.Core.Logger.ILogger;

using MirraCloud.Core;

namespace Plugins.MirraCloud.Core.Services.Segments
{
    public class SegmentService : ICloudSdkService
    {
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;

        private const string ControllerApi = "/segments/v1";

        private readonly Dictionary<string, SegmentDto> _segments = new Dictionary<string, SegmentDto>();

        public SegmentService(Configuration configuration, ILogger logger, RestApiClient restApiClient)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApiClient;
        }

        public AsyncOperation<RestApiResult<SegmentDto[]>> LoadConfigAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/segments";

            var op = _restApi.GetAsync<SegmentDto[]>(route);
            op.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess || completed.Result.Data == null)
                {
                    _logger.Error(completed.Result.Error?.Message ?? "Segments request failed.");
                    return;
                }

                _segments.Clear();

                foreach (var segment in completed.Result.Data)
                {
                    if (segment == null || string.IsNullOrEmpty(segment.id))
                    {
                        continue;
                    }

                    _segments[segment.id] = segment;
                }
            });

            return op;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

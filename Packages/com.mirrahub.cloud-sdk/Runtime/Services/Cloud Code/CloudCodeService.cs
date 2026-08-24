using System;
using System.Collections.Generic;
using MirraCloud.Core.CloudCode.Responses;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.CloudCode
{
    public sealed class CloudCodeService : ICloudSdkService
    {
        private const string SERVICE_ROUTE = "/cloud-actions/v1/projects";

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;

        public CloudCodeService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
        }

        public AsyncOperation<RestApiResult<ExecuteCloudCodeResponseDto>> ExecuteAsync(string scriptId, Dictionary<string, object> input = null)
        {
            if (string.IsNullOrWhiteSpace(scriptId))
            {
                return AsyncOperation<RestApiResult<ExecuteCloudCodeResponseDto>>.CreateCompleted(
                    RestApiResult<ExecuteCloudCodeResponseDto>.ValidationFail("scriptId is empty."));
            }

            var route = $"{SERVICE_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.BranchId}/scripts/{scriptId}:execute";
            return _restApi.PostAsync<ExecuteCloudCodeResponseDto>(route, input ?? new Dictionary<string, object>());
        }

        public AsyncOperation<RestApiResult<T>> ExecuteAsync<T>(string scriptId, Dictionary<string, object> input = null)
        {
            var op = new AsyncOperation<RestApiResult<T>>();
            var rawOp = ExecuteAsync(scriptId, input);

            rawOp.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess)
                {
                    op.Complete(RestApiResult<T>.Fail(completed.Result.Error).WithMetaFrom(completed.Result));
                    return;
                }

                try
                {
                    var dto = completed.Result.Data;
                    if (dto?.Result == null || dto.Result.Type == JsonValueType.Null)
                    {
                        op.Complete(RestApiResult<T>.Success(default).WithMetaFrom(completed.Result));
                        return;
                    }

                    var json = _restApi.JsonService.ToJson(dto.Result);
                    var value = _restApi.JsonService.FromJson<T>(json);
                    op.Complete(RestApiResult<T>.Success(value).WithMetaFrom(completed.Result));
                }
                catch (Exception e)
                {
                    var error = new RestApiError
                    {
                        Type = RestApiErrorType.Deserialize,
                        Message = e.Message,
                        Method = completed.Result.Method,
                        Route = completed.Result.Route,
                        Url = completed.Result.Url,
                        HttpStatusCode = completed.Result.HttpStatusCode,
                        ResponseBody = completed.Result.ResponseBody
                    };

                    op.Complete(RestApiResult<T>.Fail(error).WithMetaFrom(completed.Result));
                    _logger.Error(e.Message);
                }
            });

            return op;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

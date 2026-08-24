using System.Collections.Generic;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Localization.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Localization
{
    public sealed class LocalizationService : ICloudSdkService
    {
        private const string ControllerApi = "/localization/v1/projects";

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;

        public LocalizationService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
        }

        private string BasePath => $"{ControllerApi}/{_configuration.ProjectId}/branches/{_configuration.BranchId}";

        public AsyncOperation<RestApiResult<List<LocalizationValueDto>>> GetValuesAsync(string collectionId, string key)
        {
            string route = $"{BasePath}/collections/{collectionId}/keys/{key}/values";
            return _restApi.GetAsync<List<LocalizationValueDto>>(route);
        }

        public AsyncOperation<RestApiResult<LocalizationValueDto>> GetValueAsync(string collectionId, string key, LanguageCode languageCode)
        {
            string route = $"{BasePath}/collections/{collectionId}/keys/{key}/values/{languageCode.ToLanguageString()}";
            return _restApi.GetAsync<LocalizationValueDto>(route);
        }

        public AsyncOperation<RestApiResult<List<LocalizationResponseDto>>> GetAllLocalizationsAsync(string collectionId)
        {
            string route = $"{BasePath}/collections/{collectionId}/localizations";
            return _restApi.GetAsync<List<LocalizationResponseDto>>(route);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Localization.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Localization
{
    public sealed class LocalizationService : ICloudSdkService
    {
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly AuthenticationService _authentication;

        public LocalizationService(Configuration configuration, ILogger logger, RestApiClient restApi, AuthenticationService authentication)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
            _authentication = authentication;
        }

        /// <summary>
        /// Reads a whole collection by its key: every key with all its translations and the project's
        /// languages. Look values up with <see cref="LocalizationCollectionDto.Get"/>.
        /// </summary>
        /// <param name="collectionKey">The collection key from the console.</param>
        /// <param name="access">Whether to read as the signed-in player or anonymously; see <see cref="LocalizationAccess"/>.</param>
        public AsyncOperation<RestApiResult<LocalizationCollectionDto>> GetCollectionAsync(
            string collectionKey, LocalizationAccess access = LocalizationAccess.Auto)
        {
            return Run(async () =>
            {
                bool anonymous = await IsAnonymous(access);
                string route = LocalizationRoutes.Collection(_configuration.ProjectId, _configuration.Branch, anonymous, collectionKey);
                return await _restApi.GetAsync<LocalizationCollectionDto>(route, Config(anonymous)).AsTask();
            });
        }

        /// <summary>
        /// Reads one key of a collection with its value in every language.
        /// </summary>
        public AsyncOperation<RestApiResult<LocalizationResponseDto>> GetKeyAsync(
            string collectionKey, string key, LocalizationAccess access = LocalizationAccess.Auto)
        {
            return Run(async () =>
            {
                bool anonymous = await IsAnonymous(access);
                string route = LocalizationRoutes.KeyValues(_configuration.ProjectId, _configuration.Branch, anonymous, collectionKey, key);
                return await _restApi.GetAsync<LocalizationResponseDto>(route, Config(anonymous)).AsTask();
            });
        }

        /// <summary>
        /// Reads one value of a key in one language. A missing translation answers with a failure (HTTP 404),
        /// so the caller keeps a fallback.
        /// </summary>
        public AsyncOperation<RestApiResult<LocalizationValueDto>> GetValueAsync(
            string collectionKey, string key, LanguageCode languageCode, LocalizationAccess access = LocalizationAccess.Auto)
        {
            return Run(async () =>
            {
                bool anonymous = await IsAnonymous(access);
                string route = LocalizationRoutes.KeyValue(_configuration.ProjectId, _configuration.Branch, anonymous,
                    collectionKey, key, languageCode.ToLanguageString());
                return await _restApi.GetAsync<LocalizationValueDto>(route, Config(anonymous)).AsTask();
            });
        }

        [Obsolete("Address the collection by its key: GetKeyAsync(collectionKey, key). The id changes when the collection is edited in a committed branch.")]
        public AsyncOperation<RestApiResult<LocalizationResponseDto>> GetValuesAsync(string collectionId, string key)
        {
            string route = $"{LocalizationRoutes.LegacyCollection(_configuration.ProjectId, _configuration.Branch, collectionId)}/keys/{key}/values";
            return _restApi.GetAsync<LocalizationResponseDto>(route);
        }

        [Obsolete("Address the collection by its key: GetValueAsync(collectionKey, key, languageCode). The id changes when the collection is edited in a committed branch.")]
        public AsyncOperation<RestApiResult<LocalizationValueDto>> GetValueAsync(string collectionId, string key, LanguageCode languageCode)
        {
            string route = $"{LocalizationRoutes.LegacyCollection(_configuration.ProjectId, _configuration.Branch, collectionId)}/keys/{key}/values/{languageCode.ToLanguageString()}";
            return _restApi.GetAsync<LocalizationValueDto>(route);
        }

        [Obsolete("Address the collection by its key: GetCollectionAsync(collectionKey). The id changes when the collection is edited in a committed branch.")]
        public AsyncOperation<RestApiResult<List<LocalizationResponseDto>>> GetAllLocalizationsAsync(string collectionId)
        {
            string route = $"{LocalizationRoutes.LegacyCollection(_configuration.ProjectId, _configuration.Branch, collectionId)}/localizations";
            return _restApi.GetAsync<List<LocalizationResponseDto>>(route);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }

        // Auto asks whether there is a session, and right after InitializeAsync the saved one may still be being
        // read: answering "no" then would send a private collection to the public door. Yielding first also makes
        // the operation complete on a later frame, so a UseCompleted attached after the call still fires.
        private async Task<bool> IsAnonymous(LocalizationAccess access)
        {
            await Task.Yield();

            switch (access)
            {
                case LocalizationAccess.Public:
                    return true;
                case LocalizationAccess.Auto:
                    if (_authentication == null)
                    {
                        return true;
                    }

                    await _authentication.SavedSessionKnown;
                    return _authentication.HasSession == false;
                default:
                    return false;
            }
        }

        // The anonymous route sends no token and is not retried: its refusal (403 for a private collection) is final.
        private static RestRequestConfig Config(bool anonymous)
        {
            return anonymous ? new RestRequestConfig { NoAuth = true, DisableRetry = true } : null;
        }

        private static AsyncOperation<RestApiResult<T>> Run<T>(Func<Task<RestApiResult<T>>> load)
        {
            return AsyncOperationExtensions.FromTask(load, exception => RestApiResult<T>.ValidationFail(exception.Message));
        }
    }
}

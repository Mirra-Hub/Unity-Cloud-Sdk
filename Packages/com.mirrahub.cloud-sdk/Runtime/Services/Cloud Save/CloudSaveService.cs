using System;
using System.Collections.Generic;
using MirraCloud.Core.CloudSave.Requests;
using MirraCloud.Core.CloudSave.Responses;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.Networking;

namespace MirraCloud.Core.CloudSave
{
    public class CloudSaveService : ICloudSdkService
    {
        private const string ControllerApi = "/cloud-save/v1";

        private readonly IJsonService _jsonService;
        private readonly Logger.ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;

        public PlayerData PlayerData { get; private set; }

        public CloudSaveService(Configuration configuration, Logger.ILogger logger, IJsonService jsonService, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _jsonService = jsonService;
        }
        
        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }

        #region Player Data (Own)

        /// <summary>
        /// Reads your data and refreshes <see cref="PlayerData"/>. A full read replaces it; a read narrowed by
        /// <paramref name="keys"/>, <paramref name="offset"/> or <paramref name="limit"/> only updates the keys it
        /// covers, so the rest of the cache stays as it was.
        /// </summary>
        public AsyncOperation<RestApiResult<DataItemResponse[]>> GetPlayerDataAsync(string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = BuildDataRoute("player/data", keys, offset, limit);
            var request = _restApi.GetAsync<DataItemResponse[]>(route);
            bool fullRead = (keys == null || keys.Length == 0) && !offset.HasValue && !limit.HasValue;

            request.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    PlayerData = fullRead || PlayerData == null
                        ? new PlayerData(completed.Result.Data)
                        : PlayerData.Merge(completed.Result.Data, offset.HasValue || limit.HasValue ? null : keys);
                }
            });

            return request;
        }

        public AsyncOperation<RestApiResult> UpsertPlayerDataAsync(CloudSaveDataRequest data)
        {
            string route = BuildDataRoute("player/data");
            return _restApi.PostAsync(route, data);
        }

        public AsyncOperation<RestApiResult> DeletePlayerDataAsync(params string[] keys)
        {
            string route = BuildDataRoute("player/data");
            var config = new RestRequestConfig { Body = new DeleteKeysRequest(keys) };
            return _restApi.DeleteAsync(route, config);
        }

        #endregion

        #region Player Data (Other)

        public AsyncOperation<RestApiResult<DataItemResponse[]>> GetOtherPlayerDataAsync(string playerProfileId, string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = BuildDataRoute($"players/{playerProfileId}/data", keys, offset, limit);
            return _restApi.GetAsync<DataItemResponse[]>(route);
        }

        public AsyncOperation<RestApiResult> UpsertOtherPlayerDataAsync(string playerProfileId, CloudSaveDataRequest data)
        {
            string route = BuildDataRoute($"players/{playerProfileId}/data");
            return _restApi.PostAsync(route, data);
        }

        public AsyncOperation<RestApiResult> DeleteOtherPlayerDataAsync(string playerProfileId, params string[] keys)
        {
            string route = BuildDataRoute($"players/{playerProfileId}/data");
            var config = new RestRequestConfig { Body = new DeleteKeysRequest(keys) };
            return _restApi.DeleteAsync(route, config);
        }

        #endregion

        #region Global Data

        public AsyncOperation<RestApiResult<DataItemResponse[]>> LoadGlobalDataAsync(string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = BuildDataRoute("global/data", keys, offset, limit);
            return _restApi.GetAsync<DataItemResponse[]>(route);
        }

        /// <summary>
        /// Changes global data. Global data is published from the console or Cloud Code: a player cannot create a
        /// global key, and can change or delete only keys whose write mask includes <see cref="AccessMask.Other"/>.
        /// Anything else fails with <c>cloud_saves.access_denied</c>.
        /// </summary>
        public AsyncOperation<RestApiResult> SaveGlobalDataAsync(CloudSaveDataRequest data)
        {
            string route = BuildDataRoute("global/data");
            return _restApi.PostAsync(route, data);
        }

        public AsyncOperation<RestApiResult> DeleteGlobalDataAsync(params string[] keys)
        {
            string route = BuildDataRoute("global/data");
            var config = new RestRequestConfig { Body = new DeleteKeysRequest(keys) };
            return _restApi.DeleteAsync(route, config);
        }

        #endregion

        #region Custom Data

        public AsyncOperation<RestApiResult<DataItemResponse[]>> LoadCustomDataAsync(string customId, string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = BuildDataRoute($"custom/{customId}/data", keys, offset, limit);
            return _restApi.GetAsync<DataItemResponse[]>(route);
        }

        public AsyncOperation<RestApiResult> SaveCustomDataAsync(string customId, CloudSaveDataRequest data)
        {
            string route = BuildDataRoute($"custom/{customId}/data");
            return _restApi.PostAsync(route, data);
        }

        public AsyncOperation<RestApiResult> DeleteCustomDataAsync(string customId, params string[] keys)
        {
            string route = BuildDataRoute($"custom/{customId}/data");
            var config = new RestRequestConfig { Body = new DeleteKeysRequest(keys) };
            return _restApi.DeleteAsync(route, config);
        }

        #endregion

        #region Query

        public AsyncOperation<RestApiResult<QueryIndexResponse>> QueryPlayerDataAsync(QueryIndexRequest request)
        {
            string route = BuildDataRoute("player/data/query");
            return _restApi.PostAsync<QueryIndexResponse>(route, request);
        }

        public AsyncOperation<RestApiResult<QueryIndexResponse>> QueryGlobalDataAsync(QueryIndexRequest request)
        {
            string route = BuildDataRoute("global/data/query");
            return _restApi.PostAsync<QueryIndexResponse>(route, request);
        }

        /// <summary>
        /// Finds custom entities (e.g. rooms) by an index — across all of them, never global data. Only entities
        /// whose indexed keys are readable by other players are found.
        /// </summary>
        public AsyncOperation<RestApiResult<QueryIndexResponse>> QueryCustomDataAsync(QueryIndexRequest request)
        {
            string route = BuildDataRoute("custom/data/query");
            return _restApi.PostAsync<QueryIndexResponse>(route, request);
        }

        [Obsolete("A search covers every custom entity; customId was never applied. Use QueryCustomDataAsync(request).")]
        public AsyncOperation<RestApiResult<QueryIndexResponse>> QueryCustomDataAsync(string customId, QueryIndexRequest request)
        {
            return QueryCustomDataAsync(request);
        }

        #endregion

        #region Backward Compatibility

        public AsyncOperation<RestApiResult<DataItemResponse[]>> LoadAsync()
        {
            return GetPlayerDataAsync();
        }

        public AsyncOperation<RestApiResult> SaveAsync(CloudSaveDataRequest data)
        {
            return UpsertPlayerDataAsync(data);
        }

        #endregion

        #region Route Building

        private string BuildDataRoute(string path, string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.Branch}/{path}";

            var parts = new System.Collections.Generic.List<string>();
            if (keys != null && keys.Length > 0)
            {
                foreach (var k in keys)
                    parts.Add("keys=" + Uri.EscapeDataString(k));
            }
            if (offset.HasValue) parts.Add("offset=" + offset.Value);
            if (limit.HasValue) parts.Add("limit=" + limit.Value);

            if (parts.Count == 0) return route;
            return route + "?" + string.Join("&", parts);
        }

        #endregion
        
        
        #region Player Files (Own)

        public AsyncOperation<RestApiResult<FileItemResponse>> UploadPlayerFileAsync(
            string key, byte[] fileData, string fileName, string mimeType,
            Dictionary<string, string> meta = null,
            AccessMask? readMask = null, AccessMask? writeMask = null)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}");
            var formSections = BuildUploadForm(fileData, fileName, mimeType, meta, readMask, writeMask);
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> GetPlayerFileAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetPlayerFileUrlAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult> DeletePlayerFileAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}");
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdatePlayerFileMetaAsync(
            string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdatePlayerFileContentAsync(
            string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"player/files/{EscapeKey(key)}/content");
            var formSections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        #endregion

        #region Player Files (Other)

        public AsyncOperation<RestApiResult<FileItemResponse>> GetOtherPlayerFileAsync(string playerProfileId, string key)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{EscapeKey(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetOtherPlayerFileUrlAsync(string playerProfileId, string key)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{EscapeKey(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateOtherPlayerFileMetaAsync(
            string playerProfileId, string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{EscapeKey(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateOtherPlayerFileContentAsync(
            string playerProfileId, string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{EscapeKey(key)}/content");
            var formSections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        #endregion

        #region Global Files

        /// <summary>
        /// Global files are published from the console: a player cannot create one and can replace or delete only
        /// files whose write mask includes <see cref="AccessMask.Other"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<FileItemResponse>> UploadGlobalFileAsync(
            string key, byte[] fileData, string fileName, string mimeType,
            Dictionary<string, string> meta = null,
            AccessMask? readMask = null, AccessMask? writeMask = null)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}");
            var formSections = BuildUploadForm(fileData, fileName, mimeType, meta, readMask, writeMask);
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> GetGlobalFileAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetGlobalFileUrlAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult> DeleteGlobalFileAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}");
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateGlobalFileMetaAsync(
            string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateGlobalFileContentAsync(
            string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"global/files/{EscapeKey(key)}/content");
            var formSections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// A key is a path segment: UnityWebRequest.EscapeURL encodes a space as '+', which the server keeps as a
        /// literal plus, so "my save" was stored as "my+save".
        /// </summary>
        private static string EscapeKey(string key)
        {
            return Uri.EscapeDataString(key);
        }

        private string BuildFileRoute(string path)
        {
            return $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.Branch}/{path}";
        }

        private List<IMultipartFormSection> BuildUploadForm(
            byte[] fileData, string fileName, string mimeType,
            Dictionary<string, string> meta,
            AccessMask? readMask, AccessMask? writeMask)
        {
            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };

            if (meta != null)
            {
                string metaJson = _jsonService.ToJson(meta);
                sections.Add(new MultipartFormDataSection("metaJson", metaJson));
            }

            if (readMask.HasValue)
            {
                sections.Add(new MultipartFormDataSection("readMask", ((int)readMask.Value).ToString()));
            }

            if (writeMask.HasValue)
            {
                sections.Add(new MultipartFormDataSection("writeMask", ((int)writeMask.Value).ToString()));
            }

            return sections;
        }

        #endregion

  
    }
    

}

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

        public AsyncOperation<RestApiResult<DataItemResponse[]>> GetPlayerDataAsync(string[] keys = null, int? offset = null, int? limit = null)
        {
            string route = BuildDataRoute("player/data", keys, offset, limit);
            var request = _restApi.GetAsync<DataItemResponse[]>(route);

            request.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    PlayerData = new PlayerData(completed.Result.Data);
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

        public AsyncOperation<RestApiResult<QueryIndexResponse>> QueryCustomDataAsync(string customId, QueryIndexRequest request)
        {
            string route = BuildDataRoute($"custom/{customId}/data/query");
            return _restApi.PostAsync<QueryIndexResponse>(route, request);
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
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/{path}";

            var parts = new System.Collections.Generic.List<string>();
            if (keys != null && keys.Length > 0)
            {
                foreach (var k in keys)
                    parts.Add("keys=" + UnityWebRequest.EscapeURL(k));
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
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}");
            var formSections = BuildUploadForm(fileData, fileName, mimeType, meta, readMask, writeMask);
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> GetPlayerFileAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetPlayerFileUrlAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult> DeletePlayerFileAsync(string key)
        {
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}");
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdatePlayerFileMetaAsync(
            string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdatePlayerFileContentAsync(
            string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"player/files/{UnityWebRequest.EscapeURL(key)}/content");
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
            string route = BuildFileRoute($"players/{playerProfileId}/files/{UnityWebRequest.EscapeURL(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetOtherPlayerFileUrlAsync(string playerProfileId, string key)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{UnityWebRequest.EscapeURL(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateOtherPlayerFileMetaAsync(
            string playerProfileId, string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{UnityWebRequest.EscapeURL(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateOtherPlayerFileContentAsync(
            string playerProfileId, string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"players/{playerProfileId}/files/{UnityWebRequest.EscapeURL(key)}/content");
            var formSections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        #endregion

        #region Global Files

        public AsyncOperation<RestApiResult<FileItemResponse>> UploadGlobalFileAsync(
            string key, byte[] fileData, string fileName, string mimeType,
            Dictionary<string, string> meta = null,
            AccessMask? readMask = null, AccessMask? writeMask = null)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}");
            var formSections = BuildUploadForm(fileData, fileName, mimeType, meta, readMask, writeMask);
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> GetGlobalFileAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}");
            return _restApi.GetAsync<FileItemResponse>(route);
        }

        public AsyncOperation<RestApiResult<FileUrlResponse>> GetGlobalFileUrlAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}/url");
            return _restApi.GetAsync<FileUrlResponse>(route);
        }

        public AsyncOperation<RestApiResult> DeleteGlobalFileAsync(string key)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}");
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateGlobalFileMetaAsync(
            string key, Dictionary<string, string> meta)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}/meta");
            var body = new UpdateFileMetaRequest { meta = meta };
            return _restApi.PatchAsync<FileItemResponse>(route, body);
        }

        public AsyncOperation<RestApiResult<FileItemResponse>> UpdateGlobalFileContentAsync(
            string key, byte[] fileData, string fileName, string mimeType)
        {
            string route = BuildFileRoute($"global/files/{UnityWebRequest.EscapeURL(key)}/content");
            var formSections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, mimeType)
            };
            var config = new RestRequestConfig { MultipartFormSections = formSections };
            return _restApi.PutAsync<FileItemResponse>(route, null, config);
        }

        #endregion

        #region Private Helpers

        private string BuildFileRoute(string path)
        {
            return $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/{path}";
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

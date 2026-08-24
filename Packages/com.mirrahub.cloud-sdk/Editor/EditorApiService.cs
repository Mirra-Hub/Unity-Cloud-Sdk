using System;
using System.Collections.Generic;
using MirraCloud.Core;
using MirraCloud.Core.Logger;
using MirraCloud.Editor.Dto;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEditor;

namespace MirraCloud.Editor
{
    public class EditorApiService : ISessionRefresher
    {
        private const string PREF_SA_KEY = "MirraCloud_SA_Key";
        private const string PREF_JWT_TOKEN = "MirraCloud_Editor_JWT";
        private const string PREF_JWT_EXPIRES = "MirraCloud_Editor_JWT_Expires";
        private const string PREF_ORG_ID = "MirraCloud_Editor_OrgId";

        private readonly RestApiClient _restApi;
        private string _jwtToken;
        private DateTime _jwtExpiresAt;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_jwtToken) && DateTime.UtcNow < _jwtExpiresAt;
        public string OrgId => EditorPrefs.GetString(PREF_ORG_ID, "");

        public EditorApiService(Configuration configuration)
        {
            var coroutineRunner = new EditorCoroutineRunner(this);
            var jsonService = new JsonService();
            var logger = new EditorLogger();

            if (string.IsNullOrEmpty(configuration.EditorApiUrl))
            {
                logger.Error(
                    "Mirra Cloud: the editor API url is empty, so every request will fail. The " +
                    "configuration was handed over without ResolveEnvironment() having run.");
            }

            var options = new RestApiClientOptions
            {
                BaseUrl = configuration.EditorApiUrl?.TrimEnd('/')
            };

            _restApi = new RestApiClient(options, coroutineRunner, jsonService, logger);
            _restApi.UseRequestInterceptor(AuthInterceptor);
            _restApi.SetSessionRefresher(this);

            LoadCachedToken();
        }

        private RestRequestConfig AuthInterceptor(RestRequestConfig config)
        {
            if (config.NoAuth) return config;
            if (string.IsNullOrEmpty(_jwtToken)) return config;

            config.Headers ??= new Dictionary<string, string>();
            config.Headers["Authorization"] = "Bearer " + _jwtToken;
            return config;
        }

        private void LoadCachedToken()
        {
            _jwtToken = EditorPrefs.GetString(PREF_JWT_TOKEN, "");
            var expiresStr = EditorPrefs.GetString(PREF_JWT_EXPIRES, "");
            if (DateTime.TryParse(expiresStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
            {
                _jwtExpiresAt = expires;
            }
        }

        private void SaveToken(string token, string expiresAtUtc)
        {
            _jwtToken = token;
            if (DateTime.TryParse(expiresAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
            {
                _jwtExpiresAt = expires;
            }
            EditorPrefs.SetString(PREF_JWT_TOKEN, token);
            EditorPrefs.SetString(PREF_JWT_EXPIRES, expiresAtUtc);
        }

        private void SaveOrgId(string orgId)
        {
            EditorPrefs.SetString(PREF_ORG_ID, orgId);
        }

        public static string GetSavedKey()
        {
            return EditorPrefs.GetString(PREF_SA_KEY, "");
        }

        public static void SaveKey(string key)
        {
            EditorPrefs.SetString(PREF_SA_KEY, key);
        }

        public void Disconnect()
        {
            _jwtToken = null;
            _jwtExpiresAt = DateTime.MinValue;
            EditorPrefs.DeleteKey(PREF_JWT_TOKEN);
            EditorPrefs.DeleteKey(PREF_JWT_EXPIRES);
            EditorPrefs.DeleteKey(PREF_ORG_ID);
            EditorPrefs.DeleteKey(PREF_SA_KEY);
        }

        public AsyncOperation<RestApiResult<ExchangeKeyResponse>> ExchangeKeyAsync(string saKey)
        {
            var body = new ExchangeKeyRequest { key = saKey };
            var config = new RestRequestConfig { NoAuth = true, DisableRetry = true };
            var op = _restApi.PostAsync<ExchangeKeyResponse>("/api/cloud/public/auth/service-account/token", body, config);

            op.OnCompleted += result =>
            {
                var r = ((AsyncOperation<RestApiResult<ExchangeKeyResponse>>)result).Result;
                if (r.IsSuccess && r.Data != null)
                {
                    SaveToken(r.Data.token, r.Data.expiresAtUtc);
                    SaveKey(saKey);
                    SaveOrgId(r.Data.orgId);
                }
            };

            return op;
        }

        public bool CanRefresh => !string.IsNullOrEmpty(GetSavedKey());

        public AsyncOperation<RestApiResult> RefreshSessionAsync()
        {
            var savedKey = GetSavedKey();
            var op = new AsyncOperation<RestApiResult>();

            var exchangeOp = ExchangeKeyAsync(savedKey);
            exchangeOp.OnCompleted += result =>
            {
                var r = ((AsyncOperation<RestApiResult<ExchangeKeyResponse>>)result).Result;
                op.Complete(new RestApiResult { IsSuccess = r.IsSuccess, Error = r.Error, HttpStatusCode = r.HttpStatusCode });
            };

            return op;
        }

        public AsyncOperation<RestApiResult<List<EditorProjectDto>>> GetProjectsAsync(string orgId)
        {
            return _restApi.GetAsync<List<EditorProjectDto>>($"/api/cloud/client/projects/v1/list/{orgId}");
        }

        public AsyncOperation<RestApiResult<List<EditorBranchDto>>> GetBranchesAsync(string projectId)
        {
            return _restApi.GetAsync<List<EditorBranchDto>>($"/api/cloud/client/deployment/v1/projects/{projectId}/branches");
        }

        public AsyncOperation<RestApiResult<List<EditorApiTokenDto>>> GetTokensAsync(string orgId, string projectId)
        {
            return _restApi.GetAsync<List<EditorApiTokenDto>>($"/api/cloud/organizations/{orgId}/projects/{projectId}/tokens");
        }

        public AsyncOperation<RestApiResult<EditorApiTokenDto>> CreateTokenAsync(string orgId, string projectId, string name)
        {
            var body = new CreateApiTokenRequest { name = name, tokenType = "Game" };
            return _restApi.PostAsync<EditorApiTokenDto>($"/api/cloud/organizations/{orgId}/projects/{projectId}/tokens", body);
        }
    }

    internal class EditorLogger : ILogger
    {
        public void Log(string message) => UnityEngine.Debug.Log($"[MirraCloud Editor] {message}");
        public void Error(string message) => UnityEngine.Debug.LogError($"[MirraCloud Editor] {message}");
    }
}

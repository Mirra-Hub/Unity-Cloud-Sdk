using System;
using System.Collections.Generic;
using MirraCloud.Core.Entities.Dto;
using MirraCloud.Core.Logger;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Entities
{
    public class EntitiesService : ICloudSdkService
    {
        private const string ControllerApi = "/entities/v1/projects";

        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;

        private readonly Dictionary<string, EntityConfigSdkDto> _configs = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, EntityConfigSdkDto> Configs => _configs;

        public EntitiesService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
        }

        public AsyncOperation<RestApiResult<EntitiesConfigsSnapshotDto>> GetConfigsAsync()
        {
            string route = $"{ControllerApi}/{_configuration.ProjectId}/branches/{_configuration.BranchId}/configs";
            var operation = _restApi.GetAsync<EntitiesConfigsSnapshotDto>(route);

            operation.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess || completed.Result.Data?.Configs == null)
                {
                    return;
                }

                _configs.Clear();
                foreach (var pair in completed.Result.Data.Configs)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    {
                        continue;
                    }

                    _configs[pair.Key] = pair.Value;
                }
            });

            return operation;
        }

        public void ClearCache()
        {
            _configs.Clear();
        }

        public bool TryGetConfigRaw(string key, out EntityConfigSdkDto config)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _configs.TryGetValue(key, out config);
        }

        public T GetConfig<T>(string key)
        {
            if (TryGetConfig<T>(key, out var config))
            {
                return config;
            }

            throw new InvalidOperationException($"Config '{key}' is missing or cannot be mapped to '{typeof(T).Name}'.");
        }

        public bool TryGetConfig<T>(string key, out T config)
        {
            config = default;
            if (!TryGetConfigRaw(key, out var raw) || raw.Fields == null)
            {
                return false;
            }

            return JsonValueMapper.TryMap(_restApi.JsonService, raw.Fields, out config);
        }

        public T GetComponent<T>(string configKey, string componentKey)
        {
            if (TryGetComponent<T>(configKey, componentKey, out var component))
            {
                return component;
            }

            throw new InvalidOperationException($"Component '{componentKey}' for config '{configKey}' is missing or cannot be mapped to '{typeof(T).Name}'.");
        }

        public bool TryGetComponent<T>(string configKey, string componentKey, out T component)
        {
            component = default;
            if (string.IsNullOrWhiteSpace(componentKey))
            {
                return false;
            }

            if (!TryGetConfigRaw(configKey, out var raw) || raw.Components == null)
            {
                return false;
            }

            foreach (var c in raw.Components)
            {
                if (c == null || !string.Equals(c.Key, componentKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (c.Data == null)
                {
                    return false;
                }

                return JsonValueMapper.TryMap(_restApi.JsonService, c.Data, out component);
            }

            return false;
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

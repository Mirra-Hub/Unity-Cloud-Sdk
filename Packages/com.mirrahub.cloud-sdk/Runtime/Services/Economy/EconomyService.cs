using System;
using System.Collections.Generic;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Economy
{
    public sealed class EconomyService : ICloudSdkService
    {
        private const string ControllerApi = "/economy/v1/projects";

        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;

        private readonly Dictionary<string, EconomyResourceConfig> _currencies = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyResourceConfig> _items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyResourceConfig> _energies = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyResourceConfig> _resourcesByKey = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, EconomyResourceConfig> Currencies => _currencies;
        public IReadOnlyDictionary<string, EconomyResourceConfig> Items => _items;
        public IReadOnlyDictionary<string, EconomyResourceConfig> Energies => _energies;

        public EconomyService(Configuration configuration, ILogger logger, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
        }

        private string BasePath => $"{ControllerApi}/{_configuration.ProjectId}/branches/{_configuration.BranchId}";

        public AsyncOperation<RestApiResult<EconomyConfigsDto>> LoadConfigsAsync()
        {
            string route = $"{BasePath}/configs";
            var operation = _restApi.GetAsync<EconomyConfigsDto>(route);

            operation.UseCompleted(_ =>
            {
                if (!operation.Result.IsSuccess || operation.Result.Data == null)
                {
                    return;
                }

                ApplyConfigs(operation.Result.Data);
            });

            return operation;
        }

        public AsyncOperation<RestApiResult<PlayerInventoryDto>> LoadInventoryAsync()
        {
            string route = $"{BasePath}/inventories";
            return _restApi.GetAsync<PlayerInventoryDto>(route);
        }

        /// <summary>
        /// Adds currency to the player's wallet. Returns the balance after the change.
        /// </summary>
        public AsyncOperation<RestApiResult<WalletEntryDto>> AddCurrencyAsync(string currencyId, decimal amount)
        {
            string route = $"{BasePath}/inventories/wallet/add";
            var dto = new ModifyCurrencyDto { CurrencyId = currencyId, Amount = amount };
            return _restApi.PostAsync<WalletEntryDto>(route, dto);
        }

        /// <summary>
        /// Spends currency from the player's wallet. Fails with <c>economy.not_enough_currency</c>
        /// and changes nothing when the balance is too low, unless the currency allows going negative.
        /// Returns the balance after the change.
        /// </summary>
        public AsyncOperation<RestApiResult<WalletEntryDto>> SubtractCurrencyAsync(string currencyId, decimal amount)
        {
            string route = $"{BasePath}/inventories/wallet/subtract";
            var dto = new ModifyCurrencyDto { CurrencyId = currencyId, Amount = amount };
            return _restApi.PostAsync<WalletEntryDto>(route, dto);
        }

        /// <summary>
        /// Sets the wallet balance to an exact value. Returns the balance after the change.
        /// </summary>
        public AsyncOperation<RestApiResult<WalletEntryDto>> SetCurrencyAsync(string currencyId, decimal amount)
        {
            string route = $"{BasePath}/inventories/wallet/set";
            var dto = new ModifyCurrencyDto { CurrencyId = currencyId, Amount = amount };
            return _restApi.PostAsync<WalletEntryDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<object>> AddItemAsync(string itemId, int amount, string inventoryKey = null)
        {
            string route = $"{BasePath}/inventories/items/add";
            var dto = new ModifyItemDto { ItemId = itemId, Amount = amount, InventoryKey = inventoryKey };
            return _restApi.PostAsync<object>(route, dto);
        }

        public AsyncOperation<RestApiResult<object>> SubtractItemAsync(string itemId, int amount, string inventoryKey = null)
        {
            string route = $"{BasePath}/inventories/items/subtract";
            var dto = new ModifyItemDto { ItemId = itemId, Amount = amount, InventoryKey = inventoryKey };
            return _restApi.PostAsync<object>(route, dto);
        }

        public AsyncOperation<RestApiResult<object>> SubtractItemSafeAsync(string itemId, int amount, string inventoryKey = null)
        {
            string route = $"{BasePath}/inventories/items/subtract-safe";
            var dto = new ModifyItemDto { ItemId = itemId, Amount = amount, InventoryKey = inventoryKey };
            return _restApi.PostAsync<object>(route, dto);
        }

        public AsyncOperation<RestApiResult<object>> UpdateItemPropertiesAsync(string itemId, string slotId, Dictionary<string, object> properties, string inventoryKey = null)
        {
            string route = $"{BasePath}/inventories/items/properties";
            var dto = new UpdateItemPropertiesDto { ItemId = itemId, SlotId = slotId, Properties = properties, InventoryKey = inventoryKey };
            return _restApi.PostAsync<object>(route, dto);
        }

        public AsyncOperation<RestApiResult<ConsumeItemResponseDto>> ConsumeItemAsync(string itemId, string slotId = null, string inventoryKey = null)
        {
            string route = $"{BasePath}/inventories/items/consume";
            var dto = new ConsumeItemDto { ItemId = itemId, SlotId = slotId, InventoryKey = inventoryKey };
            return _restApi.PostAsync<ConsumeItemResponseDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<List<EnergyBalanceDto>>> GetEnergiesAsync()
        {
            string route = $"{BasePath}/inventories/energy";
            return _restApi.GetAsync<List<EnergyBalanceDto>>(route);
        }

        public AsyncOperation<RestApiResult<EnergyBalanceDto>> GetEnergyAsync(string energyId)
        {
            string route = $"{BasePath}/inventories/energy/{energyId}";
            return _restApi.GetAsync<EnergyBalanceDto>(route);
        }

        public AsyncOperation<RestApiResult<EnergyBalanceDto>> SpendEnergyAsync(string energyId, int amount)
        {
            string route = $"{BasePath}/inventories/energy/spend";
            var dto = new ModifyEnergyDto { EnergyId = energyId, Amount = amount };
            return _restApi.PostAsync<EnergyBalanceDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<EnergyBalanceDto>> AddEnergyAsync(string energyId, int amount)
        {
            string route = $"{BasePath}/inventories/energy/add";
            var dto = new ModifyEnergyDto { EnergyId = energyId, Amount = amount };
            return _restApi.PostAsync<EnergyBalanceDto>(route, dto);
        }

        public AsyncOperation<RestApiResult<EnergyBalanceDto>> SetUnlimitedEnergyAsync(string energyId, int durationSeconds)
        {
            string route = $"{BasePath}/inventories/energy/unlimited";
            var dto = new SetUnlimitedEnergyDto { EnergyId = energyId, DurationSeconds = durationSeconds };
            return _restApi.PostAsync<EnergyBalanceDto>(route, dto);
        }

        private void ApplyConfigs(EconomyConfigsDto dto)
        {
            ClearCache();
            AddResources(dto.Currencies, EconomyResourceKind.Currency, _currencies);
            AddResources(dto.Items, EconomyResourceKind.Item, _items);
            AddResources(dto.Energies, EconomyResourceKind.Energy, _energies);
        }

        public void ClearCache()
        {
            _currencies.Clear();
            _items.Clear();
            _energies.Clear();
            _resourcesByKey.Clear();
        }

        private void AddResources(
            Dictionary<string, EconomySdkResourceDto> source,
            EconomyResourceKind kind,
            Dictionary<string, EconomyResourceConfig> target)
        {
            if (source == null)
            {
                return;
            }

            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                var fields = pair.Value.Fields ?? new JsonValue(JsonValueType.Object);
                var components = pair.Value.Components ?? new Dictionary<string, JsonValue>(StringComparer.Ordinal);

                var resource = new EconomyResourceConfig(pair.Key, kind, fields, components);
                target[pair.Key] = resource;
                _resourcesByKey[pair.Key] = resource;
            }
        }

        public bool TryGetResource(string key, out EconomyResourceConfig resource)
        {
            resource = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _resourcesByKey.TryGetValue(key, out resource);
        }

        public T GetResourceFields<T>(string key)
        {
            if (TryGetResourceFields<T>(key, out var fields))
            {
                return fields;
            }

            throw new InvalidOperationException($"Economy resource '{key}' is missing or cannot be mapped to '{typeof(T).Name}'.");
        }

        public bool TryGetResourceFields<T>(string key, out T fields)
        {
            fields = default;
            if (!TryGetResource(key, out var resource) || resource.Fields == null)
            {
                return false;
            }

            return JsonValueMapper.TryMap(_restApi.JsonService, resource.Fields, out fields);
        }

        public T GetResourceComponent<T>(string key, string componentKey)
        {
            if (TryGetResourceComponent<T>(key, componentKey, out var component))
            {
                return component;
            }

            throw new InvalidOperationException(
                $"Economy resource '{key}' component '{componentKey}' is missing or cannot be mapped to '{typeof(T).Name}'.");
        }

        public bool TryGetResourceComponent<T>(string key, string componentKey, out T component)
        {
            component = default;
            if (string.IsNullOrWhiteSpace(componentKey))
            {
                return false;
            }

            if (!TryGetResource(key, out var resource))
            {
                return false;
            }

            if (resource.Components == null || !resource.Components.TryGetValue(componentKey, out var data) || data == null)
            {
                return false;
            }

            return JsonValueMapper.TryMap(_restApi.JsonService, data, out component);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }

    public enum EconomyResourceKind
    {
        Currency = 0,
        Item = 1,
        Energy = 2
    }

    public sealed class EconomyResourceConfig
    {
        public string Key { get; }
        public EconomyResourceKind Kind { get; }
        public JsonValue Fields { get; }
        public IReadOnlyDictionary<string, JsonValue> Components { get; }

        public EconomyResourceConfig(
            string key,
            EconomyResourceKind kind,
            JsonValue fields,
            IReadOnlyDictionary<string, JsonValue> components)
        {
            Key = key;
            Kind = kind;
            Fields = fields;
            Components = components;
        }
    }
}

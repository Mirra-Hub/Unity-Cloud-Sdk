using System.Collections.Generic;
using MirraCloud.Core.RemoteConfig.Responses;

namespace MirraCloud.Core.RemoteConfig
{
    public class RemoteConfig
    {
        public string Key { get; private set; }
        private readonly List<RemoteConfigField> _fields = new List<RemoteConfigField>();

        public IReadOnlyList<RemoteConfigField> Fields => _fields;
        
        
        public RemoteConfig(FetchRemoteConfigResponse.RemoteConfigData configResponse)
        {
            Key = configResponse.key;
            
            foreach (var field in configResponse.fields)
            {
                _fields.Add(new RemoteConfigField(field.key, field.name, field.value, field.fieldType));
            }
        }

        /*public int GetInt(string key, int defaultValue = 0)
        {
            if (_fields.TryGetValue(key, out var valueObj))
            {
                if (valueObj is int value)
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (_fields.TryGetValue(key, out var valueObj))
            {
                if (valueObj is string value)
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            if (_fields.TryGetValue(key, out var valueObj))
            {
                if (valueObj is bool value)
                {
                    return value;
                }
            }

            return defaultValue;
        }*/
    }
}
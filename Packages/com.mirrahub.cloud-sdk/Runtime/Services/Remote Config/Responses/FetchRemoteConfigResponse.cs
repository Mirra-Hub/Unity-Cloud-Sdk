using System;

namespace MirraCloud.Core.RemoteConfig.Responses
{
    [Serializable]
    public class FetchRemoteConfigResponse
    {
        public RemoteConfigData[] configs;
        public MetaData metaData;
        
        [Serializable]
        public class RemoteConfigData
        {
            public string key;
            public Field[] fields;
        }
        
        [Serializable]
        public class Field
        {
            public string name;
            public string key;
            public RemoteConfigFieldType fieldType;
            public string value;
        }
        
        [Serializable]
        public class MetaData
        {
            
        }
    }
}
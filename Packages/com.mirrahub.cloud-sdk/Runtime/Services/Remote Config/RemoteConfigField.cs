namespace MirraCloud.Core.RemoteConfig
{
    public struct RemoteConfigField
    {
        public string Key;
        public string Value;
        public RemoteConfigFieldType FieldType;
        public string Name;

        public RemoteConfigField(string key, string name, string value, RemoteConfigFieldType fieldType)
        {
            Key = key;
            FieldType = fieldType;
            Value = value;
            Name = name;
        }
    }
}
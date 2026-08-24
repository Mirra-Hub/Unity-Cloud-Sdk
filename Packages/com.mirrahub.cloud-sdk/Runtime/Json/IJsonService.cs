namespace MirraCloud.Json
{
    public interface IJsonService
    {
        string ToJson(object val, bool prettyPrint = false);
        T FromJson<T>(string json);
    }
}
namespace MirraCloud.Json
{
    public interface IJsonImporter<T> 
    {
        T ImportJson(JsonMapper mapper, JsonTokenReader tokenReader);
    }
}
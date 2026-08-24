namespace MirraCloud.Core.Realtime.Abstractions
{
    public interface IRealtimeSerializer
    {
        string Serialize<T>(T data);
        T Deserialize<T>(string json);
    }
}

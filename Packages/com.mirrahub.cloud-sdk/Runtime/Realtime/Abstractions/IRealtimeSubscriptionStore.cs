using System.Collections.Generic;

namespace MirraCloud.Core.Realtime.Abstractions
{
    public interface IRealtimeSubscriptionStore
    {
        void Add(string channelId);
        void Remove(string channelId);
        void Clear();
        bool HasSubscribed(string channelId);
        IReadOnlyCollection<string> GetAll();
    }
}

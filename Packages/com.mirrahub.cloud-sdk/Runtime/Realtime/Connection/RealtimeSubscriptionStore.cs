using System.Collections.Generic;
using MirraCloud.Core.Realtime.Abstractions;

namespace MirraCloud.Core.Realtime.Connection
{
    internal sealed class RealtimeSubscriptionStore : IRealtimeSubscriptionStore
    {
        private readonly HashSet<string> _subscriptions = new HashSet<string>();
        private readonly object _sync = new object();

        public void Add(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return;
            }

            lock (_sync)
            {
                _subscriptions.Add(channelId);
            }
        }

        public void Remove(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return;
            }

            lock (_sync)
            {
                _subscriptions.Remove(channelId);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _subscriptions.Clear();
            }
        }

        public bool HasSubscribed(string channelId)
        {
            lock (_sync)
            {
                return _subscriptions.Contains(channelId);
            }
        }

        public IReadOnlyCollection<string> GetAll()
        {
            lock (_sync)
            {
                return new List<string>(_subscriptions);
            }
        }
    }
}

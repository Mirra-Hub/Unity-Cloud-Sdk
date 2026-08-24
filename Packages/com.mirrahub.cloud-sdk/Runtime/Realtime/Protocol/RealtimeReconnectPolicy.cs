namespace MirraCloud.Core.Realtime.Protocol
{
    public sealed class RealtimeReconnectPolicy
    {
        public int MaxAttempts = 5;
        public int InitialDelayMs = 500;
        public int MaxDelayMs = 5000;

        public int GetDelayMs(int attempt)
        {
            var value = InitialDelayMs * (1 << (attempt - 1));
            return value > MaxDelayMs ? MaxDelayMs : value;
        }
    }
}

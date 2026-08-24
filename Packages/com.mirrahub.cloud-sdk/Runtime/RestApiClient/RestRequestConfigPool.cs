using System.Collections.Generic;

namespace MirraCloud.Core
{
    /// <summary>
    /// Pool for the per-request <see cref="RestRequestConfig"/> instances the client works on,
    /// so a steady request stream does not allocate a config + headers dictionary every call.
    /// Main-thread only, like the rest of the coroutine-driven client — no locking.
    /// <see cref="Release"/> ignores instances it does not own (no <see cref="RestRequestConfig.Rented"/>
    /// flag), so caller-created configs can be kept and reused by external code freely.
    /// </summary>
    internal static class RestRequestConfigPool
    {
        private const int MaxSize = 32;

        private static readonly Stack<RestRequestConfig> Pool = new Stack<RestRequestConfig>(MaxSize);

        public static RestRequestConfig Get()
        {
            var config = Pool.Count > 0 ? Pool.Pop() : new RestRequestConfig();
            config.Rented = true;
            config.Headers ??= new Dictionary<string, string>(8);
            return config;
        }

        public static void Release(RestRequestConfig config)
        {
            if (config == null || config.Rented == false)
            {
                return;
            }

            config.Rented = false;
            config.Reset();
            if (Pool.Count < MaxSize)
            {
                Pool.Push(config);
            }
        }
    }
}

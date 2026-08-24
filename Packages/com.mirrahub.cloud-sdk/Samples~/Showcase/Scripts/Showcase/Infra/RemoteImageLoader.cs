using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Loads remote images (avatar / group / CDN URLs) into <see cref="Texture2D"/> with an
    /// in-memory cache and in-flight de-duplication. Never throws — returns null on failure so
    /// callers degrade to a fallback (e.g. an initials avatar) rather than a broken-image box.
    /// </summary>
    public sealed class RemoteImageLoader
    {
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Task<Texture2D>> _inFlight = new Dictionary<string, Task<Texture2D>>();

        public Task<Texture2D> Load(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return Task.FromResult<Texture2D>(null);
            }
            if (_cache.TryGetValue(url, out var cached) && cached != null)
            {
                return Task.FromResult(cached);
            }
            if (_inFlight.TryGetValue(url, out var pending))
            {
                return pending;
            }

            var task = LoadInternal(url);
            _inFlight[url] = task;
            return task;
        }

        /// <summary>Binds an image into a UITK <see cref="Image"/>; keeps existing content on failure.</summary>
        public async void BindInto(Image target, string url, Action onSuccess = null, Action onFail = null)
        {
            if (target == null)
            {
                return;
            }
            var tex = await Load(url);
            if (tex == null)
            {
                onFail?.Invoke();
                return;
            }
            target.image = tex;
            onSuccess?.Invoke();
        }

        private async Task<Texture2D> LoadInternal(string url)
        {
            try
            {
                using (var req = UnityWebRequestTexture.GetTexture(url))
                {
                    await SendAsync(req);
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        return null;
                    }
                    var tex = DownloadHandlerTexture.GetContent(req);
                    _cache[url] = tex;
                    return tex;
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                _inFlight.Remove(url);
            }
        }

        private static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}

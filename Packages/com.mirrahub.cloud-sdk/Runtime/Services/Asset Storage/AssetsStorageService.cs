using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.Networking;
using MirraCloud.Core;
using MirraCloud.Core.Storage.Blob;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.AssetsStorage
{
    public class AssetsStorageService : ICloudSdkService
    {
        private const string ControllerApi = "/assets/v1";
        private const string CacheContainerId = "asset_cache";

        // Anonymous (no player auth) surface. Relative to the SDK BaseUrl (.../api/cloud/sdk),
        // so it resolves to .../api/cloud/sdk/public/assets/v1/... — the sdk-gateway routes this
        // prefix without jwt-auth. Only assets marked public are served (private -> HTTP 403).
        private const string PublicControllerApi = "/public/assets/v1";

        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;
        private readonly ILogger _logger;
        private readonly AssetCache _cache;

        private readonly List<Asset> _assets = new List<Asset>();
        private readonly List<Folder> _folders = new List<Folder>();

        public IReadOnlyList<Asset> Assets => _assets;
        public IReadOnlyList<Folder> Folders => _folders;

        public AssetsStorageService(Configuration configuration, RestApiClient restApi, ILogger logger, IBlobStorage blobStorage)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _cache = new AssetCache(blobStorage, CacheContainerId);
        }

        public AsyncOperation<RestApiResult<AssetStorageStructureDto>> LoadConfigAsync()
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/config";

            var response = _restApi.GetAsync<AssetStorageStructureDto>(route);

            // Swap the lists only once an answer arrives. Clearing up front left the service with an
            // empty catalog for the duration of the request — and permanently if it failed — which
            // also silently disables the cache, since the version lookup reads these lists.
            response.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    _assets.Clear();
                    _folders.Clear();
                    AddStorageItems(completed.Result.Data);
                }
            });

            return response;
        }

        public List<Asset> GetAssetsFromType(AssetType assetType)
        {
            List<Asset> assets = new List<Asset>();

            foreach (var asset in _assets)
            {
                if (asset.Type == assetType)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        public AsyncOperation<RestApiResult<TextFile>> LoadTextFromId(string stableId, ExtractTextFileType textFileType = ExtractTextFileType.Text, bool useCache = true)
        {
            return AsyncOperationExtensions.FromTask<RestApiResult<TextFile>>(
                async () =>
                {
                    TextFile textFile = await LoadAsync<TextFile>(
                        stableId,
                        useCache,
                        bytes => Task.FromResult(BytesToTextFile(bytes, textFileType)),
                        () => DownloadTextAsync(stableId, textFileType));

                    return textFile != null
                        ? RestApiResult<TextFile>.Success(textFile)
                        : RestApiResult<TextFile>.ValidationFail($"Asset '{stableId}' download failed");
                },
                exception => RestApiResult<TextFile>.ValidationFail(exception.Message));
        }

        public AsyncOperation<RestApiResult<Texture2D>> LoadTextureFromId(string stableId, bool readable = false, bool useCache = true)
        {
            return AsyncOperationExtensions.FromTask<RestApiResult<Texture2D>>(
                async () =>
                {
                    Texture2D texture = await LoadAsync<Texture2D>(
                        stableId,
                        useCache,
                        bytes => Task.FromResult(CreateTexture(bytes, readable)),
                        () => DownloadTextureAsync(stableId, readable));

                    return texture != null
                        ? RestApiResult<Texture2D>.Success(texture)
                        : RestApiResult<Texture2D>.ValidationFail($"Asset '{stableId}' download failed");
                },
                exception => RestApiResult<Texture2D>.ValidationFail(exception.Message));
        }

        public AsyncOperation<RestApiResult<Sprite>> LoadSpriteFromId(string stableId, bool readable = false, bool useCache = true)
        {
            return AsyncOperationExtensions.FromTask<RestApiResult<Sprite>>(
                async () =>
                {
                    Texture2D texture = await LoadAsync<Texture2D>(
                        stableId,
                        useCache,
                        bytes => Task.FromResult(CreateTexture(bytes, readable)),
                        () => DownloadTextureAsync(stableId, readable));

                    return texture != null
                        ? RestApiResult<Sprite>.Success(ToSprite(texture))
                        : RestApiResult<Sprite>.ValidationFail($"Asset '{stableId}' download failed");
                },
                exception => RestApiResult<Sprite>.ValidationFail(exception.Message));
        }

        public AsyncOperation<RestApiResult<AudioClip>> LoadAudioFromId(string stableId, AudioType audioType, bool useCache = true)
        {
            return AsyncOperationExtensions.FromTask<RestApiResult<AudioClip>>(
                async () =>
                {
                    AudioClip clip = await LoadAsync<AudioClip>(
                        stableId,
                        useCache,
                        bytes => BytesToAudioClipAsync(bytes, audioType),
                        () => DownloadAudioAsync(stableId, audioType));

                    return clip != null
                        ? RestApiResult<AudioClip>.Success(clip)
                        : RestApiResult<AudioClip>.ValidationFail($"Asset '{stableId}' download failed");
                },
                exception => RestApiResult<AudioClip>.ValidationFail(exception.Message));
        }

        public AsyncOperation<RestApiResult<AssetBundle>> LoadAssetBundleFromId(string stableId, bool useCache = true)
        {
            return AsyncOperationExtensions.FromTask<RestApiResult<AssetBundle>>(
                async () =>
                {
                    AssetBundle bundle = await LoadAsync<AssetBundle>(
                        stableId,
                        useCache,
                        bytes => BytesToBundleAsync(bytes),
                        () => DownloadBundleAsync(stableId));

                    return bundle != null
                        ? RestApiResult<AssetBundle>.Success(bundle)
                        : RestApiResult<AssetBundle>.ValidationFail($"Asset '{stableId}' download failed or is not a valid AssetBundle");
                },
                exception => RestApiResult<AssetBundle>.ValidationFail(exception.Message));
        }

        private async Task<T> LoadAsync<T>(string stableId, bool useCache, Func<byte[], Task<T>> reconstructFromBytes, Func<Task<DownloadedAsset<T>>> download)
        {
            if (useCache && TryResolveVersion(stableId, out int version))
            {
                return await _cache.GetOrLoadAsync(CacheKeyFor(stableId), version, reconstructFromBytes, download);
            }

            return (await download()).Value;
        }

        // A stable id names the same logical asset in every branch, but not the same bytes: branches
        // copy-on-write and carry the version counter over, so dev and prod can both sit on version 3
        // with different content. The cache outlives a session, so without the project and branch in
        // the key, switching branches would serve the other branch's file.
        private string CacheKeyFor(string stableId)
        {
            return $"{_configuration.ProjectId}/{_configuration.BranchId}/{stableId}";
        }

        // Texture / Sprite: typed handler decodes on a worker thread and also exposes the raw
        // bytes, so a miss avoids the main-thread decode while still caching the image bytes.
        private async Task<DownloadedAsset<Texture2D>> DownloadTextureAsync(string stableId, bool readable)
        {
            var config = CreateDownloadConfig(_ => new DownloadHandlerTexture(readable));

            var result = await _restApi.GetAsync<DownloadedAsset<Texture2D>>(BuildAssetRoute(stableId), config, request =>
            {
                byte[] raw = SafeData(request.downloadHandler);
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                return new DownloadedAsset<Texture2D>(texture, raw);
            }).AsTask();

            return result.IsSuccess ? result.Data : default;
        }

        // Audio: typed handler decodes the clip and exposes the raw bytes on a miss (same as
        // texture); a cache hit reconstructs the clip from bytes via BytesToAudioClipAsync.
        private async Task<DownloadedAsset<AudioClip>> DownloadAudioAsync(string stableId, AudioType audioType)
        {
            var config = CreateDownloadConfig(url => new DownloadHandlerAudioClip(url, audioType));

            var result = await _restApi.GetAsync<DownloadedAsset<AudioClip>>(BuildAssetRoute(stableId), config, request =>
            {
                byte[] raw = SafeData(request.downloadHandler);
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                return new DownloadedAsset<AudioClip>(clip, raw);
            }).AsTask();

            return result.IsSuccess ? result.Data : default;
        }

        // Text / AssetBundle: no worker-thread typed decode to gain (text is raw, and the
        // AssetBundle handler cannot expose bytes), so these download raw bytes directly.
        private async Task<DownloadedAsset<TextFile>> DownloadTextAsync(string stableId, ExtractTextFileType textFileType)
        {
            byte[] bytes = await DownloadRawBytesAsync(stableId);

            if (bytes == null)
            {
                return default;
            }

            return new DownloadedAsset<TextFile>(BytesToTextFile(bytes, textFileType), bytes);
        }

        private async Task<DownloadedAsset<AssetBundle>> DownloadBundleAsync(string stableId)
        {
            byte[] bytes = await DownloadRawBytesAsync(stableId);

            if (bytes == null)
            {
                return default;
            }

            AssetBundle bundle = await BytesToBundleAsync(bytes);

            return bundle != null ? new DownloadedAsset<AssetBundle>(bundle, bytes) : default;
        }

        private async Task<byte[]> DownloadRawBytesAsync(string stableId)
        {
            RestApiResult<byte[]> result = await _restApi.GetBytesAsync(BuildAssetRoute(stableId), CreateDownloadConfig()).AsTask();
            return result.IsSuccess ? result.Data : null;
        }

        private bool TryResolveVersion(string stableId, out int version)
        {
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i].StableId == stableId)
                {
                    version = _assets[i].Version;
                    return true;
                }
            }

            _logger.Log($"[AssetsStorageService] version for asset '{stableId}' is unknown (LoadConfigAsync not run?), serving without cache");
            version = 0;
            return false;
        }

        private byte[] SafeData(DownloadHandler handler)
        {
            try
            {
                byte[] data = handler.data;
                return data != null && data.Length > 0 ? data : null;
            }
            catch
            {
                return null;
            }
        }

        private Texture2D CreateTexture(byte[] bytes, bool readable)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(bytes, readable == false);
            return texture;
        }

        private Sprite ToSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(Vector2.zero, new Vector2(texture.width, texture.height)), Vector2.one * 0.5f);
        }

        private TextFile BytesToTextFile(byte[] bytes, ExtractTextFileType textFileType)
        {
            var textFile = new TextFile();

            if (textFileType == ExtractTextFileType.All || textFileType == ExtractTextFileType.Text)
            {
                textFile.Text = Encoding.UTF8.GetString(bytes);
            }

            if (textFileType == ExtractTextFileType.All || textFileType == ExtractTextFileType.Data)
            {
                textFile.Data = bytes;
            }

            return textFile;
        }

        private async Task<AssetBundle> BytesToBundleAsync(byte[] bytes)
        {
            AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(bytes);
            await request.ToTask();
            return request.assetBundle;
        }

        // Cache-hit reconstruction of an AudioClip from raw bytes. UnityWebRequestMultimedia can
        // only decode from a URL, so native platforms round-trip a temp file and WebGL wraps the
        // bytes in a blob: URL (file:// is unsupported there).
        private async Task<AudioClip> BytesToAudioClipAsync(byte[] bytes, AudioType audioType)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = AudioBlobUrl.Create(bytes);

            try
            {
                return await LoadAudioClipFromUrlAsync(url, audioType);
            }
            finally
            {
                AudioBlobUrl.Revoke(url);
            }
#else
            string path = Path.Combine(Application.temporaryCachePath, $"asset_audio_{Guid.NewGuid():N}");

            try
            {
                File.WriteAllBytes(path, bytes);
                return await LoadAudioClipFromUrlAsync("file://" + path, audioType);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
#endif
        }

        private async Task<AudioClip> LoadAudioClipFromUrlAsync(string url, AudioType audioType)
        {
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                await request.SendWebRequest().ToTask();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return null;
                }

                return DownloadHandlerAudioClip.GetContent(request);
            }
        }

        // Addressed by stable id: it survives a re-import and is the same in every environment, so a
        // game can hard-code a reference to an asset. The internal document id is not usable for that.
        private string BuildAssetRoute(string stableId)
        {
            return $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/assets/by-stable-id/{stableId}";
        }

        private string BuildPublicAssetRoute(string stableId)
        {
            return $"{PublicControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/assets/by-stable-id/{stableId}";
        }

        private RestRequestConfig CreateDownloadConfig(Func<string, DownloadHandler> downloadHandlerFactory = null)
        {
            return new RestRequestConfig
            {
                FollowRedirect = true,
                NoAuthOnRedirect = true,
                StripHeadersOnRedirect = true,
                DownloadHandlerFactory = downloadHandlerFactory
            };
        }
        // Anonymous download: no player token attached, and no retry so a 403 (asset not public)
        // returns in a single request instead of being retried.
        private RestRequestConfig CreatePublicDownloadConfig(System.Func<string, DownloadHandler> downloadHandlerFactory = null)
        {
            return new RestRequestConfig
            {
                NoAuth = true,
                DisableRetry = true,
                FollowRedirect = true,
                NoAuthOnRedirect = true,
                StripHeadersOnRedirect = true,
                DownloadHandlerFactory = downloadHandlerFactory
            };
        }

        // --- Public (anonymous, no player auth) ---
        // Fetch an asset by stableId without a signed-in player. The asset must be marked public in the
        // dashboard; otherwise the request fails with HTTP 403 (RestApiResult.IsSuccess == false).

        public AsyncOperation<RestApiResult<TextFile>> LoadPublicTextFromId(string stableId, ExtractTextFileType textFileType = ExtractTextFileType.Text)
        {
            var route = BuildPublicAssetRoute(stableId);
            var config = CreatePublicDownloadConfig();
            return _restApi.GetAsync<TextFile>(route, config, request =>
            {
                var textFile = new TextFile();

                if (textFileType == ExtractTextFileType.All || textFileType == ExtractTextFileType.Text)
                {
                    textFile.Text = request.downloadHandler.text;
                }

                if (textFileType == ExtractTextFileType.All || textFileType == ExtractTextFileType.Data)
                {
                    textFile.Data = request.downloadHandler.data;
                }

                return textFile;
            });
        }

        public AsyncOperation<RestApiResult<Texture2D>> LoadPublicTextureFromId(string stableId, bool readable = false)
        {
            var route = BuildPublicAssetRoute(stableId);
            var config = CreatePublicDownloadConfig(_ => new DownloadHandlerTexture(readable));
            return _restApi.GetAsync<Texture2D>(route, config, request => DownloadHandlerTexture.GetContent(request));
        }

        public AsyncOperation<RestApiResult<Sprite>> LoadPublicSpriteFromId(string stableId, bool readable = false)
        {
            var route = BuildPublicAssetRoute(stableId);
            var config = CreatePublicDownloadConfig(_ => new DownloadHandlerTexture(readable));
            return _restApi.GetAsync(route, config, request =>
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                return Sprite.Create(texture, new Rect(Vector2.zero, new Vector2(texture.width, texture.height)), Vector2.one * 0.5f);
            });
        }

        public AsyncOperation<RestApiResult<AudioClip>> LoadPublicAudioFromId(string stableId, AudioType audioType)
        {
            var route = BuildPublicAssetRoute(stableId);
            var config = CreatePublicDownloadConfig(url => new DownloadHandlerAudioClip(url, audioType));
            return _restApi.GetAsync<AudioClip>(route, config, request => DownloadHandlerAudioClip.GetContent(request));
        }

        public AsyncOperation<RestApiResult<AssetBundle>> LoadPublicAssetBundleFromId(string stableId)
        {
            var route = BuildPublicAssetRoute(stableId);
            var config = CreatePublicDownloadConfig(url => new DownloadHandlerAssetBundle(url, 0));
            return _restApi.GetAsync<AssetBundle>(route, config, request => DownloadHandlerAssetBundle.GetContent(request));
        }

        private void AddStorageItems(AssetStorageStructureDto structureDto)
        {
            if (structureDto.assets != null)
            {
                foreach (var assetDto in structureDto.assets)
                {
                    _assets.Add(new Asset(assetDto));
                }
            }

            if (structureDto.folders != null)
            {
                foreach (var folderDto in structureDto.folders)
                {
                    _folders.Add(new Folder(folderDto));
                }
            }
        }

        public void CloudSdkInitialize() { }

        public void CloudSdkDispose()
        {
            _cache.Dispose();
        }
    }
}

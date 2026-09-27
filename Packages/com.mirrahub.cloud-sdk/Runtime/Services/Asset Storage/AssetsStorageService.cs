using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.Networking;
using MirraCloud.Core;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Errors;
using MirraCloud.Core.Storage.Blob;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>
    /// Downloads assets of the configured branch, with a local cache.
    /// <para>
    /// An asset is addressed by its stable id (<c>Load*FromId</c>) or by its path in the console
    /// (<c>Load*FromPath</c>, <c>icons/coin.png</c>, case-sensitive). The optional <see cref="AssetAccess"/>
    /// argument picks the route: the signed-in player's (the default), the anonymous one that serves only
    /// assets published in the console, or whichever fits the session.
    /// </para>
    /// <para>
    /// The cache works once <see cref="LoadConfigAsync"/> has run: the catalog gives each asset's version,
    /// and a path it knows is loaded by that asset's stable id, sharing the entry with <c>Load*FromId</c>.
    /// A failed load returns the server's answer — HTTP status and error code
    /// (<c>assets_storage.asset_not_found</c>, <c>assets_storage.asset_not_public</c>, …).
    /// </para>
    /// </summary>
    public class AssetsStorageService : ICloudSdkService
    {
        private const string CacheContainerId = "asset_cache";

        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;
        private readonly ILogger _logger;
        private readonly AuthenticationService _authentication;
        private readonly AssetCache _cache;

        private readonly List<Asset> _assets = new List<Asset>();
        private readonly List<Folder> _folders = new List<Folder>();

        public IReadOnlyList<Asset> Assets => _assets;
        public IReadOnlyList<Folder> Folders => _folders;

        public AssetsStorageService(Configuration configuration, RestApiClient restApi, ILogger logger, IBlobStorage blobStorage, AuthenticationService authentication)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _authentication = authentication;
            _cache = new AssetCache(blobStorage, CacheContainerId);
        }

        public AsyncOperation<RestApiResult<AssetStorageStructureDto>> LoadConfigAsync()
        {
            string route = AssetRoutes.Config(_configuration.ProjectId, _configuration.BranchId);

            // The game gets its own operation: UseCompleted replaces the callback, so hooking the one returned
            // to the game would let the game's own UseCompleted drop the catalog — and with it the cache and
            // every path lookup — without a word.
            var raw = _restApi.GetAsync<AssetStorageStructureDto>(route);
            var result = new AsyncOperation<RestApiResult<AssetStorageStructureDto>>();

            // Swap the lists only once an answer arrives. Clearing up front left the service with an
            // empty catalog for the duration of the request — and permanently if it failed — which
            // also silently disables the cache, since the version lookup reads these lists.
            raw.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    _assets.Clear();
                    _folders.Clear();
                    AddStorageItems(completed.Result.Data);
                }

                result.Complete(completed.Result);
            });

            return result;
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

        /// <summary>
        /// The asset at <paramref name="path"/> in the loaded catalog — no request, so
        /// <see cref="LoadConfigAsync"/> has to have run. The path is written as in the console, with or
        /// without the leading slash (<c>icons/coin.png</c>), and is case-sensitive, as on the server.
        /// </summary>
        public bool TryGetAssetByPath(string path, out Asset asset)
        {
            asset = AssetPath.TryParse(path, out string storedPath, out _)
                ? AssetLookup.FindByPath(_assets, storedPath)
                : null;

            return asset != null;
        }

        /// <summary>
        /// The assets in a folder of the loaded catalog — no request, so <see cref="LoadConfigAsync"/> has to
        /// have run. The folder is written like a path (<c>icons</c> or <c>/icons</c>); empty or <c>/</c> is
        /// the branch root.
        /// </summary>
        /// <param name="recursive">Also the assets in its subfolders.</param>
        public List<Asset> GetAssetsInFolder(string folderPath, bool recursive = false)
        {
            if (AssetPath.IsRoot(folderPath))
            {
                return AssetLookup.InFolder(_assets, string.Empty, recursive);
            }

            return AssetPath.TryParse(folderPath, out string storedPath, out _)
                ? AssetLookup.InFolder(_assets, storedPath, recursive)
                : new List<Asset>();
        }

        // --- By stable id ---

        public AsyncOperation<RestApiResult<TextFile>> LoadTextFromId(string stableId, ExtractTextFileType textFileType = ExtractTextFileType.Text, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadTextAsync(stableId, false, textFileType, useCache, access));
        }

        public AsyncOperation<RestApiResult<Texture2D>> LoadTextureFromId(string stableId, bool readable = false, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadTextureAsync(stableId, false, readable, useCache, access));
        }

        public AsyncOperation<RestApiResult<Sprite>> LoadSpriteFromId(string stableId, bool readable = false, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(async () => AsSprite(await LoadTextureAsync(stableId, false, readable, useCache, access)));
        }

        public AsyncOperation<RestApiResult<AudioClip>> LoadAudioFromId(string stableId, AudioType audioType, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadAudioAsync(stableId, false, audioType, useCache, access));
        }

        public AsyncOperation<RestApiResult<AssetBundle>> LoadAssetBundleFromId(string stableId, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadAssetBundleAsync(stableId, false, useCache, access));
        }

        // --- By path: the same loads, addressed the way the console lays the branch out ---

        /// <summary><see cref="LoadTextFromId"/> addressed by path: <c>configs/level_1.json</c>.</summary>
        public AsyncOperation<RestApiResult<TextFile>> LoadTextFromPath(string path, ExtractTextFileType textFileType = ExtractTextFileType.Text, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadTextAsync(path, true, textFileType, useCache, access));
        }

        /// <summary><see cref="LoadTextureFromId"/> addressed by path: <c>icons/coin.png</c>.</summary>
        public AsyncOperation<RestApiResult<Texture2D>> LoadTextureFromPath(string path, bool readable = false, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadTextureAsync(path, true, readable, useCache, access));
        }

        /// <summary><see cref="LoadSpriteFromId"/> addressed by path: <c>icons/coin.png</c>.</summary>
        public AsyncOperation<RestApiResult<Sprite>> LoadSpriteFromPath(string path, bool readable = false, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(async () => AsSprite(await LoadTextureAsync(path, true, readable, useCache, access)));
        }

        /// <summary><see cref="LoadAudioFromId"/> addressed by path: <c>music/theme.mp3</c>.</summary>
        public AsyncOperation<RestApiResult<AudioClip>> LoadAudioFromPath(string path, AudioType audioType, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadAudioAsync(path, true, audioType, useCache, access));
        }

        /// <summary><see cref="LoadAssetBundleFromId"/> addressed by path: <c>bundles/boss_dragon</c>.</summary>
        public AsyncOperation<RestApiResult<AssetBundle>> LoadAssetBundleFromPath(string path, bool useCache = true, AssetAccess access = AssetAccess.Player)
        {
            return Run(() => LoadAssetBundleAsync(path, true, useCache, access));
        }

        // --- One pipeline behind every load ---

        private Task<RestApiResult<TextFile>> LoadTextAsync(string reference, bool byPath, ExtractTextFileType textFileType, bool useCache, AssetAccess access)
        {
            return LoadAsync<TextFile>(
                reference, byPath, access, useCache,
                bytes => Task.FromResult(BytesToTextFile(bytes, textFileType)),
                (route, anonymous) => DownloadTextAsync(route, anonymous, textFileType),
                "text");
        }

        private Task<RestApiResult<Texture2D>> LoadTextureAsync(string reference, bool byPath, bool readable, bool useCache, AssetAccess access)
        {
            return LoadAsync<Texture2D>(
                reference, byPath, access, useCache,
                bytes => Task.FromResult(CreateTexture(bytes, readable)),
                (route, anonymous) => DownloadTextureAsync(route, anonymous, readable),
                "an image");
        }

        private Task<RestApiResult<AudioClip>> LoadAudioAsync(string reference, bool byPath, AudioType audioType, bool useCache, AssetAccess access)
        {
            return LoadAsync<AudioClip>(
                reference, byPath, access, useCache,
                bytes => BytesToAudioClipAsync(bytes, audioType),
                (route, anonymous) => DownloadAudioAsync(route, anonymous, audioType),
                "audio");
        }

        private Task<RestApiResult<AssetBundle>> LoadAssetBundleAsync(string reference, bool byPath, bool useCache, AssetAccess access)
        {
            return LoadAsync<AssetBundle>(
                reference, byPath, access, useCache,
                bytes => BytesToBundleAsync(bytes),
                (route, anonymous) => DownloadBundleAsync(route, anonymous),
                "an AssetBundle");
        }

        private async Task<RestApiResult<T>> LoadAsync<T>(
            string reference,
            bool byPath,
            AssetAccess access,
            bool useCache,
            Func<byte[], Task<T>> reconstructFromBytes,
            Func<string, bool, Task<RestApiResult<DownloadedAsset<T>>>> download,
            string kind) where T : class
        {
            if (TryAddress(reference, byPath, out AssetAddress address, out RestApiError invalid) == false)
            {
                // Completes on a later frame, like every load that makes a request: a caller attaches
                // UseCompleted only after the call returns, and an operation that is already complete never
                // invokes it.
                await Task.Yield();
                return RestApiResult<T>.Fail(invalid);
            }

            bool anonymous = AssetAccessResolver.IsAnonymous(access, _authentication != null && _authentication.HasSession);

            // Where the catalog knows the path, the path is only another name for the stable id: loading by the
            // id shares one cache entry with Load*FromId, and the key and the bytes cannot disagree. A cache
            // keyed by path could — an asset uploaded where a deleted one used to be starts again at version 1
            // and would be served the old bytes. Without the cache the server resolves the path itself, which
            // also gets past a catalog loaded before the asset was renamed or moved.
            if (useCache && address.IsPath)
            {
                address = ResolveKnownPath(address);
            }

            string route = AssetRoutes.Download(_configuration.ProjectId, _configuration.BranchId, anonymous, address);

            // What the request answered, when one was made — a cache hit makes none. Its status and timing stay
            // on the result whatever came of the bytes.
            RestApiResult served = null;
            RestApiResult failure = null;

            async Task<DownloadedAsset<T>> Fetch()
            {
                RestApiResult<DownloadedAsset<T>> result = await download(route, anonymous);

                if (result.IsSuccess)
                {
                    served = result;
                    return result.Data;
                }

                failure = result;
                return default;
            }

            T value = useCache && address.IsPath == false && TryResolveVersion(address.StableId, out int version)
                ? await _cache.GetOrLoadAsync(CacheKeyFor(address.StableId), version, reconstructFromBytes, Fetch)
                : (await Fetch()).Value;

            if (value != null)
            {
                return RestApiResult<T>.Success(value).WithMetaFrom(served);
            }

            // The server's own answer rather than a generic message, so the caller can tell a missing session
            // (401) from a private asset on the anonymous route (403) and from a missing one (404).
            if (failure != null)
            {
                return RestApiResult<T>.Fail(failure.Error).WithMetaFrom(failure);
            }

            // The file arrived but is not what was asked for — not an image, not a bundle. The 2xx stays on the
            // result: the route did its part.
            return RestApiResult<T>.ValidationFail($"Asset '{address}' could not be read as {kind}").WithMetaFrom(served);
        }

        private static bool TryAddress(string reference, bool byPath, out AssetAddress address, out RestApiError error)
        {
            address = default;
            error = null;

            if (byPath == false)
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    error = RestApiError.Validation("Asset stable id is empty");
                    return false;
                }

                address = AssetAddress.ById(reference);
                return true;
            }

            if (AssetPath.TryParse(reference, out string storedPath, out string routePath))
            {
                address = AssetAddress.ByPath(storedPath, routePath);
                return true;
            }

            // Refused before any request, with the code the server would answer: the caller checks one code
            // either way, and the player's route bills a request even when it fails.
            error = new RestApiError
            {
                Type = RestApiErrorType.Validation,
                Message = $"Asset path '{reference}' is empty or contains '..'",
                Errors = new List<CloudApiError>
                {
                    new CloudApiError
                    {
                        Code = CloudErrorCodes.AssetsStorageAssetPathInvalid,
                        Message = "Asset path is empty or contains forbidden segments."
                    }
                }
            };
            return false;
        }

        private AssetAddress ResolveKnownPath(AssetAddress address)
        {
            Asset known = AssetLookup.FindByPath(_assets, address.StoredPath);

            if (known != null && string.IsNullOrEmpty(known.StableId) == false)
            {
                return AssetAddress.ById(known.StableId);
            }

            _logger.Log($"[AssetsStorageService] path '{address}' is not in the loaded catalog (LoadConfigAsync not run, or the asset is newer), serving without cache");
            return address;
        }

        private static AsyncOperation<RestApiResult<T>> Run<T>(Func<Task<RestApiResult<T>>> load)
        {
            return AsyncOperationExtensions.FromTask(load, exception => RestApiResult<T>.ValidationFail(exception.Message));
        }

        private RestApiResult<Sprite> AsSprite(RestApiResult<Texture2D> texture)
        {
            return texture.IsSuccess
                ? RestApiResult<Sprite>.Success(ToSprite(texture.Data)).WithMetaFrom(texture)
                : RestApiResult<Sprite>.Fail(texture.Error).WithMetaFrom(texture);
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
        private Task<RestApiResult<DownloadedAsset<Texture2D>>> DownloadTextureAsync(string route, bool anonymous, bool readable)
        {
            var config = CreateDownloadConfig(anonymous, _ => new DownloadHandlerTexture(readable));

            return _restApi.GetAsync<DownloadedAsset<Texture2D>>(route, config, request =>
            {
                byte[] raw = SafeData(request.downloadHandler);
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                return new DownloadedAsset<Texture2D>(texture, raw);
            }).AsTask();
        }

        // Audio: typed handler decodes the clip and exposes the raw bytes on a miss (same as
        // texture); a cache hit reconstructs the clip from bytes via BytesToAudioClipAsync.
        private Task<RestApiResult<DownloadedAsset<AudioClip>>> DownloadAudioAsync(string route, bool anonymous, AudioType audioType)
        {
            var config = CreateDownloadConfig(anonymous, url => new DownloadHandlerAudioClip(url, audioType));

            return _restApi.GetAsync<DownloadedAsset<AudioClip>>(route, config, request =>
            {
                byte[] raw = SafeData(request.downloadHandler);
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                return new DownloadedAsset<AudioClip>(clip, raw);
            }).AsTask();
        }

        // Text / AssetBundle: no worker-thread typed decode to gain (text is raw, and the
        // AssetBundle handler cannot expose bytes), so these download raw bytes directly.
        private async Task<RestApiResult<DownloadedAsset<TextFile>>> DownloadTextAsync(string route, bool anonymous, ExtractTextFileType textFileType)
        {
            RestApiResult<byte[]> result = await DownloadRawBytesAsync(route, anonymous);

            if (result.IsSuccess == false)
            {
                return RestApiResult<DownloadedAsset<TextFile>>.Fail(result.Error).WithMetaFrom(result);
            }

            byte[] bytes = result.Data;
            TextFile textFile = bytes != null ? BytesToTextFile(bytes, textFileType) : null;

            return RestApiResult<DownloadedAsset<TextFile>>.Success(new DownloadedAsset<TextFile>(textFile, bytes))
                .WithMetaFrom(result);
        }

        private async Task<RestApiResult<DownloadedAsset<AssetBundle>>> DownloadBundleAsync(string route, bool anonymous)
        {
            RestApiResult<byte[]> result = await DownloadRawBytesAsync(route, anonymous);

            if (result.IsSuccess == false)
            {
                return RestApiResult<DownloadedAsset<AssetBundle>>.Fail(result.Error).WithMetaFrom(result);
            }

            byte[] bytes = result.Data;
            AssetBundle bundle = bytes != null ? await BytesToBundleAsync(bytes) : null;

            // Bytes that are not a bundle are not cached; LoadAsync reports them as unreadable.
            return RestApiResult<DownloadedAsset<AssetBundle>>.Success(
                    bundle != null ? new DownloadedAsset<AssetBundle>(bundle, bytes) : default)
                .WithMetaFrom(result);
        }

        private Task<RestApiResult<byte[]>> DownloadRawBytesAsync(string route, bool anonymous)
        {
            return _restApi.GetBytesAsync(route, CreateDownloadConfig(anonymous)).AsTask();
        }

        // Both routes answer with a redirect to the file's storage URL, and the player's token must not
        // follow it there. The anonymous route sends no token at all and is not retried: its refusal (403
        // for a private asset) is final, and a retry would only repeat it.
        private RestRequestConfig CreateDownloadConfig(bool anonymous, Func<string, DownloadHandler> downloadHandlerFactory = null)
        {
            return new RestRequestConfig
            {
                NoAuth = anonymous,
                DisableRetry = anonymous,
                FollowRedirect = true,
                NoAuthOnRedirect = true,
                StripHeadersOnRedirect = true,
                DownloadHandlerFactory = downloadHandlerFactory
            };
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

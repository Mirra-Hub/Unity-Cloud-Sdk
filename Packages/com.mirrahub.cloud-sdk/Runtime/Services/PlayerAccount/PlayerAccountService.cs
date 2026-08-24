using System;
using System.Collections.Generic;
using MirraCloud;
using MirraCloud.Core;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.Services.PlayerAccount.Dto;
using UnityEngine;
using UnityEngine.Networking;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount
{
    public class PlayerAccountService : IDisposable, ICloudSdkService
    {
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly AuthenticationService _authenticationService;

        private const string ACCOUNTS_ROUTE = "/player-accounts/v1/projects";
        private const string PROFILES_ROUTE = "/player-accounts/v1";

        public PlayerAccountInfo PlayerAccountInfo { get; private set; }
        public IReadOnlyList<ProfileInfo> Profiles => _profiles;

        private List<ProfileInfo> _profiles = new();

        public event Action<IReadOnlyList<ProfileInfo>> OnProfilesChanged;
        public event Action<ProfileInfo> OnProfileUpdated;
        public event Action<string> OnProfileSelected;

        public PlayerAccountService(AuthenticationService authenticationService, RestApiClient restApi, Configuration configuration, ILogger logger)
        {
            _authenticationService = authenticationService;
            _logger = logger;
            _restApi = restApi;
            _configuration = configuration;

            _authenticationService.OnLogin += OnAuthLogin;
            _restApi.UseRequestInterceptor(MetaDataHeadersInterceptor);
        }

        public void Dispose()
        {
            CloudSdkDispose();
        }

        public void CloudSdkInitialize() { }

        public void CloudSdkDispose()
        {
            _authenticationService.OnLogin -= OnAuthLogin;
        }
        
        private readonly string _deviceModel = SystemInfo.deviceModel;
        private readonly string _osVersion = SystemInfo.operatingSystem;
        private readonly string _buildVersion = Application.version;

        // Header values below are derived (json/joins/ToString) and would otherwise be
        // re-allocated on every request; each is rebuilt only when its source changes.
        private object _iconKeyHeaderSource;
        private string _iconKeyHeader = string.Empty;
        private int _ageHeaderSource = int.MinValue;
        private string _ageHeader;
        private CountryCode? _countryHeaderSource;
        private string _countryHeader;
        private LanguageCode? _languageHeaderSource;
        private string _languageHeader;
        private string[] _segmentsHeaderSource;
        private string _segmentsHeader = string.Empty;

        private RestRequestConfig MetaDataHeadersInterceptor(RestRequestConfig config)
        {
            if (config.NoAuth == true)
            {
                return config;
            }

            config.Headers ??= new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(_authenticationService.SessionId))
                config.Headers["SessionId"] = _authenticationService.SessionId;

            config.Headers["DeviceModel"] = _deviceModel;
            config.Headers["OSVersion"] = _osVersion;
            config.Headers["BuildVersion"] = _buildVersion;

            var info = PlayerAccountInfo;
            if (info != null)
            {
                if (ReferenceEquals(_iconKeyHeaderSource, info.IconKey) == false)
                {
                    _iconKeyHeaderSource = info.IconKey;
                    _iconKeyHeader = info.IconKey != null
                        ? _restApi.JsonService.ToJson(info.IconKey)
                        : string.Empty;
                }

                if (_ageHeaderSource != info.Age)
                {
                    _ageHeaderSource = info.Age;
                    _ageHeader = info.Age.ToString();
                }

                if (_countryHeaderSource != info.Country)
                {
                    _countryHeaderSource = info.Country;
                    _countryHeader = info.Country.ToCountryString();
                }

                if (_languageHeaderSource != info.LanguageCode)
                {
                    _languageHeaderSource = info.LanguageCode;
                    _languageHeader = info.LanguageCode.ToLanguageString();
                }

                if (ReferenceEquals(_segmentsHeaderSource, info.SegmentKeys) == false)
                {
                    _segmentsHeaderSource = info.SegmentKeys;
                    _segmentsHeader = info.SegmentKeys != null ? string.Join(',', info.SegmentKeys) : string.Empty;
                }

                config.Headers["Nickname"] = info.Nickname;
                config.Headers["IconKey"] = _iconKeyHeader;
                config.Headers["Age"] = _ageHeader;
                config.Headers["Country"] = _countryHeader;
                config.Headers["LanguageCode"] = _languageHeader;
                config.Headers["TimeZone"] = info.TimeZone;
                config.Headers["Status"] = info.Status;
                config.Headers["AccountSegments"] = _segmentsHeader;
                config.Headers["ProfileSegments"] = _segmentsHeader;
            }

            return config;
        }

        private void OnAuthLogin(GetAuthDataDto authData)
        {
            var account = authData.CurrentAccount ?? authData.PlayerInfo;
            if (account == null)
            {
                _logger.Log("AuthData does not contain account info.");
                return;
            }

            PlayerAccountInfo = new PlayerAccountInfo(account);
        }

        #region Account

        public AsyncOperation<RestApiResult<PlayerAccountInfo>> GetAccountAsync()
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts";
            var raw = _restApi.GetAsync<AccountDto>(route);
            var mapped = new AsyncOperation<RestApiResult<PlayerAccountInfo>>();

            raw.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess || completed.Result.Data == null)
                {
                    mapped.Complete(RestApiResult<PlayerAccountInfo>.Fail(completed.Result.Error).WithMetaFrom(completed.Result));
                    return;
                }

                PlayerAccountInfo = new PlayerAccountInfo(completed.Result.Data);
                mapped.Complete(RestApiResult<PlayerAccountInfo>.Success(PlayerAccountInfo).WithMetaFrom(completed.Result));
            });

            return mapped;
        }

        public AsyncOperation<RestApiResult> UpdateNicknameAsync(string nickname)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/nickname";
            var dto = new { Nickname = nickname };
            var operation = _restApi.PatchAsync(route, dto);

            operation.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && PlayerAccountInfo != null)
                {
                    // Nickname is now the *displayed* value (Base or Base+Suffix). The server
                    // composes the displayed result and may keep a suffix, so the value below
                    // is approximate; the caller should refresh the account if it needs the
                    // canonical displayed string.
                    PlayerAccountInfo.Nickname = nickname;
                }
            });

            return operation;
        }

        public AsyncOperation<RestApiResult> UpdateUsernameAsync(string username)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/username";
            var dto = new { Username = username };
            var operation = _restApi.PatchAsync(route, dto);

            operation.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && PlayerAccountInfo != null)
                {
                    // Server prepends the acc_ prefix and may append a uniqueness suffix; the value
                    // below is approximate — refresh the account if you need the canonical handle.
                    PlayerAccountInfo.Username = $"acc_{username}";
                }
            });

            return operation;
        }

        public AsyncOperation<RestApiResult> UpdateGenderAsync(Gender gender)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/gender";
            var dto = new { Gender = (int)gender };
            var operation = _restApi.PatchAsync(route, dto);

            operation.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && PlayerAccountInfo != null)
                {
                    PlayerAccountInfo.Gender = gender;
                }
            });

            return operation;
        }

        public AsyncOperation<RestApiResult> UpdateAgeAsync(int age)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/age";
            var dto = new { Age = age };
            return _restApi.PatchAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> UpdateCountryAsync(CountryCode country)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/country";
            var dto = new { Country = country };
            return _restApi.PatchAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> UpdateLanguageAsync(LanguageCode languageCode)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/language";
            var dto = new { LanguageCode = languageCode };
            return _restApi.PatchAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> SetAccountIconUrlAsync(string iconUrl)
        {
            if (IsValidHttpUrl(iconUrl) == false)
            {
                return CreateValidationErrorOperation("Invalid iconUrl. Only http/https URL is allowed. Use upload methods to set internal icons.");
            }

            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/icon";
            var dto = new UpdateAccountIconDto { IconKey = iconUrl };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    var _ = GetAccountAsync();
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateIconWithUploadAsync(byte[] fileData, string fileName = "icon.png", string contentType = "image/png")
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/icon/upload";
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, contentType)
            };

            var op = _restApi.PatchMultipartAsync(route, form);
            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    var _ = GetAccountAsync();
                }
            });
            return op;
        }

        public AsyncOperation<RestApiResult> UpdateIconWithUploadAsync(Texture2D texture, string fileName = "icon.png", string contentType = null, int jpgQuality = 75)
        {
            if (TryEncodeTexture(texture, fileName, contentType, jpgQuality, out var encoded, out var resolvedFileName, out var resolvedContentType) == false)
            {
                return CreateValidationErrorOperation("Failed to encode texture.");
            }

            return UpdateIconWithUploadAsync(encoded, resolvedFileName, resolvedContentType);
        }

        public AsyncOperation<RestApiResult> UpdateIconWithUploadAsync(Sprite sprite, string fileName = "icon.png", string contentType = null, int jpgQuality = 75)
        {
            if (TryGetSpriteTexture(sprite, out var texture) == false)
            {
                return CreateValidationErrorOperation("Failed to extract texture from sprite.");
            }

            try
            {
                return UpdateIconWithUploadAsync(texture, fileName, contentType, jpgQuality);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        public AsyncOperation<RestApiResult> UpdateSegmentsAsync(string[] segmentIds)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/segments";
            var dto = new { SegmentIds = segmentIds };
            return _restApi.PatchAsync(route, dto);
        }

        #endregion

        #region Profiles

        public AsyncOperation<RestApiResult<ProfileInfo[]>> GetProfilesAsync()
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles";
            var op = _restApi.GetAsync<ProfileInfo[]>(route);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    _profiles = new List<ProfileInfo>(completed.Result.Data);
                    OnProfilesChanged?.Invoke(Profiles);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult<ProfileInfo>> GetProfileAsync(string profileId)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}";
            var op = _restApi.GetAsync<ProfileInfo>(route);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    UpdateLocalProfile(completed.Result.Data);
                }
            });

            return op;
        }

        /// <summary>
        /// Fetch a random sample of profiles from the project — useful for
        /// "featured players" widgets, leaderboard previews, or onboarding
        /// flows that need a few real-looking opponents to display. The server
        /// caps <paramref name="count"/> at 10 and rejects values below 1, so
        /// the client validates the same range to fail fast without a network
        /// round-trip. The returned profiles are NOT merged into the local
        /// profile cache (they belong to other accounts).
        /// </summary>
        public AsyncOperation<RestApiResult<ProfileInfo[]>> GetRandomProfilesAsync(int count)
        {
            if (count < 1 || count > 10)
            {
                return AsyncOperation<RestApiResult<ProfileInfo[]>>.CreateCompleted(
                    RestApiResult<ProfileInfo[]>.ValidationFail("count must be between 1 and 10."));
            }

            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/random?count={count}";
            return _restApi.GetAsync<ProfileInfo[]>(route);
        }

        public AsyncOperation<RestApiResult<ProfileInfo>> CreateProfileAsync(CreateProfileDto dto, bool autoSelect = false)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles?autoSelect={autoSelect}";
            var op = _restApi.PostAsync<ProfileInfo>(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    _profiles.Add(completed.Result.Data);
                    OnProfilesChanged?.Invoke(Profiles);
                    OnProfileUpdated?.Invoke(completed.Result.Data);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> DeleteProfileAsync(string profileId)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}";
            var op = _restApi.DeleteAsync(route);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    _profiles.RemoveAll(p => p.Id == profileId);
                    OnProfilesChanged?.Invoke(Profiles);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfileNicknameAsync(string profileId, string nickname)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/nickname";
            var dto = new UpdateProfileNicknameDto { Nickname = nickname };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfileUsernameAsync(string profileId, string username)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/username";
            var dto = new UpdateProfileUsernameDto { Username = username };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfileGenderAsync(string profileId, Gender gender)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/gender";
            var dto = new { Gender = (int)gender };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> SetProfileIconUrlAsync(string profileId, string iconUrl)
        {
            if (IsValidHttpUrl(iconUrl) == false)
            {
                return CreateValidationErrorOperation("Invalid iconUrl. Only http/https URL is allowed. Use upload methods to set internal icons.");
            }

            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/icon";
            var dto = new UpdateProfileIconDto { IconKey = iconUrl };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfileIconWithUploadAsync(string profileId, byte[] fileData, string fileName = "icon.png", string contentType = "image/png")
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/icon/upload";
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, contentType)
            };

            var op = _restApi.PatchMultipartAsync(route, form);
            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });
            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfileIconWithUploadAsync(string profileId, Texture2D texture, string fileName = "icon.png", string contentType = null, int jpgQuality = 75)
        {
            if (TryEncodeTexture(texture, fileName, contentType, jpgQuality, out var encoded, out var resolvedFileName, out var resolvedContentType) == false)
            {
                return CreateValidationErrorOperation("Failed to encode texture.");
            }

            return UpdateProfileIconWithUploadAsync(profileId, encoded, resolvedFileName, resolvedContentType);
        }

        public AsyncOperation<RestApiResult> UpdateProfileIconWithUploadAsync(string profileId, Sprite sprite, string fileName = "icon.png", string contentType = null, int jpgQuality = 75)
        {
            if (TryGetSpriteTexture(sprite, out var texture) == false)
            {
                return CreateValidationErrorOperation("Failed to extract texture from sprite.");
            }

            try
            {
                return UpdateProfileIconWithUploadAsync(profileId, texture, fileName, contentType, jpgQuality);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        public AsyncOperation<RestApiResult> UpdateProfileSegmentsAsync(string profileId, string[] segmentIds)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/segments";
            var dto = new UpdateProfileSegmentsDto { SegmentIds = segmentIds };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    RefreshProfile(profileId);
                }
            });

            return op;
        }

        public AsyncOperation<RestApiResult> UpdateProfilePresenceStatusAsync(string profileId, ProfilePresenceStatus status)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return CreateValidationErrorOperation("profileId is required.");
            }

            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/status";
            var dto = new UpdateProfilePresenceStatusDto { Status = status };
            return _restApi.PatchAsync(route, dto);
        }

        /// <summary>
        /// Read another profile's presence status (online/offline/etc). The server
        /// caches presence with a short TTL, so consecutive calls within a few
        /// seconds hit the cache and are essentially free. Returns
        /// <see cref="ProfilePresenceStatus.Offline"/> when the cache has no
        /// entry for the profile (the server treats absence as "Offline" and
        /// flattens the failure into a successful Offline response).
        /// </summary>
        public AsyncOperation<RestApiResult<ProfilePresenceStatus>> GetProfilePresenceStatusAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                var validation = AsyncOperation<RestApiResult<ProfilePresenceStatus>>.CreateCompleted(
                    RestApiResult<ProfilePresenceStatus>.ValidationFail("profileId is required."));
                return validation;
            }

            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}/status";
            var raw = _restApi.GetAsync<GetProfilePresenceStatusDto>(route);
            var mapped = new AsyncOperation<RestApiResult<ProfilePresenceStatus>>();

            raw.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess || completed.Result.Data == null)
                {
                    mapped.Complete(RestApiResult<ProfilePresenceStatus>.Fail(completed.Result.Error).WithMetaFrom(completed.Result));
                    return;
                }

                mapped.Complete(RestApiResult<ProfilePresenceStatus>.Success(completed.Result.Data.Status).WithMetaFrom(completed.Result));
            });

            return mapped;
        }

        public AsyncOperation<RestApiResult<ProfileInfo>> ReplaceProfileAsync(string profileId, CreateProfileDto dto)
        {
            var route = $"{PROFILES_ROUTE}/{_configuration.ProjectId}/profiles/{profileId}";
            var op = _restApi.PutAsync<ProfileInfo>(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess && completed.Result.Data != null)
                {
                    UpdateLocalProfile(completed.Result.Data);
                }
            });

            return op;
        }

        private void RefreshProfile(string profileId)
        {
            var _ = GetProfileAsync(profileId);
        }

        private void UpdateLocalProfile(ProfileInfo profile)
        {
            if (profile == null)
            {
                return;
            }

            var index = _profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                _profiles[index] = profile;
            }
            else
            {
                _profiles.Add(profile);
            }

            OnProfilesChanged?.Invoke(Profiles);
            OnProfileUpdated?.Invoke(profile);
        }

        private static AsyncOperation<RestApiResult> CreateValidationErrorOperation(string message)
        {
            return AsyncOperation<RestApiResult>.CreateCompleted(RestApiResult.ValidationFail(message));
        }

        private static bool IsValidHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) == false)
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private bool TryEncodeTexture(Texture2D texture, string fileName, string contentType, int jpgQuality, out byte[] encoded, out string resolvedFileName, out string resolvedContentType)
        {
            encoded = null;
            resolvedFileName = null;
            resolvedContentType = null;

            if (texture == null)
            {
                _logger.Error("Texture is null.");
                return false;
            }

            var useJpg = false;
            if (string.IsNullOrWhiteSpace(contentType))
            {
                var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
                useJpg = ext == ".jpg" || ext == ".jpeg";
                resolvedContentType = useJpg ? "image/jpeg" : "image/png";
            }
            else
            {
                resolvedContentType = contentType;
                useJpg = contentType == "image/jpeg" || contentType == "image/jpg";
            }

            resolvedFileName = string.IsNullOrWhiteSpace(fileName)
                ? (useJpg ? "icon.jpg" : "icon.png")
                : fileName;

            var sourceTexture = texture;
            Texture2D readableTexture = null;
            var createdReadable = false;

            if (sourceTexture.isReadable == false)
            {
                if (TryMakeReadableCopy(sourceTexture, out readableTexture) == false)
                {
                    _logger.Error("Texture is not readable and readable copy creation failed. Enable Read/Write or use a readable texture.");
                    return false;
                }

                createdReadable = true;
                sourceTexture = readableTexture;
            }

            try
            {
                encoded = useJpg
                    ? ImageConversion.EncodeToJPG(sourceTexture, Mathf.Clamp(jpgQuality, 1, 100))
                    : ImageConversion.EncodeToPNG(sourceTexture);

                if (encoded == null || encoded.Length == 0)
                {
                    _logger.Error("Failed to encode texture.");
                    return false;
                }

                return true;
            }
            finally
            {
                if (createdReadable && readableTexture != null)
                {
                    UnityEngine.Object.Destroy(readableTexture);
                }
            }
        }

        private static bool TryMakeReadableCopy(Texture2D source, out Texture2D readableCopy)
        {
            readableCopy = null;

            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                readableCopy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readableCopy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readableCopy.Apply();
                return true;
            }
            catch
            {
                if (readableCopy != null)
                {
                    UnityEngine.Object.Destroy(readableCopy);
                    readableCopy = null;
                }

                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private bool TryGetSpriteTexture(Sprite sprite, out Texture2D texture)
        {
            texture = null;

            if (sprite == null)
            {
                _logger.Error("Sprite is null.");
                return false;
            }

            var rect = sprite.textureRect;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);

            if (width <= 0 || height <= 0)
            {
                _logger.Error("Sprite has invalid textureRect.");
                return false;
            }

            var source = sprite.texture;
            if (source == null)
            {
                _logger.Error("Sprite texture is null.");
                return false;
            }

            if (source.isReadable)
            {
                try
                {
                    var x = Mathf.RoundToInt(rect.x);
                    var y = Mathf.RoundToInt(rect.y);
                    var pixels = source.GetPixels(x, y, width, height);

                    texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    texture.SetPixels(pixels);
                    texture.Apply();
                    return true;
                }
                catch
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                        texture = null;
                    }

                    _logger.Error("Failed to extract sprite pixels from readable texture.");
                    return false;
                }
            }

            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                _logger.Error("Sprite texture is not readable and shader 'Unlit/Texture' not found for GPU extraction.");
                return false;
            }

            var material = new Material(shader)
            {
                mainTexture = source
            };

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;

            try
            {
                var offset = new Vector2(rect.x / source.width, rect.y / source.height);
                var scale = new Vector2(rect.width / source.width, rect.height / source.height);
                material.mainTextureOffset = offset;
                material.mainTextureScale = scale;

                Graphics.Blit(source, rt, material);
                RenderTexture.active = rt;

                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return true;
            }
            catch
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                }

                _logger.Error("Failed to extract sprite pixels from non-readable texture using GPU copy.");
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(material);
            }
        }

        public AsyncOperation<RestApiResult> SelectProfileAsync(string profileId)
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/accounts/profile";
            var dto = new { ProfileId = profileId };
            var op = _restApi.PatchAsync(route, dto);

            op.UseCompleted(completed =>
            {
                if (completed.Result.IsSuccess)
                {
                    OnProfileSelected?.Invoke(profileId);
                }
            });

            return op;
        }

        #endregion

        #region Player Roles

        /// <summary>
        /// Branch-scoped player-role catalog (key + display name), read-only. Roles are assigned
        /// to profiles exclusively from the admin dashboard; the player's own assigned keys arrive
        /// in <see cref="ProfileInfo.RoleKeys"/> — use this catalog to resolve display names.
        /// </summary>
        public AsyncOperation<RestApiResult<PlayerRoleInfo[]>> GetPlayerRolesAsync()
        {
            var route = $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.BranchId}/player-roles";
            return _restApi.GetAsync<PlayerRoleInfo[]>(route);
        }

        #endregion
    }
}

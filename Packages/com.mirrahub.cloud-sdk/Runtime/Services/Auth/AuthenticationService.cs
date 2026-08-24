using System;
using System.Collections.Generic;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Auth.OpenId;
using MirraCloud.Core.WebView;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.Networking;

namespace MirraCloud.Core.Auth
{
    public class AuthenticationService : ISessionRefresher, ICloudSdkService
    {
        private readonly Logger.ILogger _logger;
        private readonly IStorage _storage;
        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;
        private readonly WebViewService _webView;

        // Routes that include the {branchId} segment.
        private const string AUTH_BRANCH_ROUTE = "/players/auth/v1/projects";
        private const string LINK_BRANCH_ROUTE = "/players/link/v1/projects";
        // Routes that do NOT include {branchId} (refresh, logout, unlink, openid result).
        private const string ACCOUNTS_ROUTE = "/players/accounts/v1/projects";
        private const string AUTH_ROUTE = "/players/auth/v1/projects";
        private const string UNLINK_ROUTE = "/players/unlink/v1/projects";
        private const string OPENID_RESULT_ROUTE = "/players/auth/v1/openid/result";

        private const string GUESTID_KEY = "GuestId";
        private const string REFRESH_TOKEN_KEY = "RefreshToken";
        private const string SESSIONID_KEY = "SessionId";
        private const string SESSION_EXPIRESAT_KEY = "SessionExpiresAt";

        private string _authToken;
        public string AuthToken => _authToken;

        // Prebuilt "Bearer <token>" so the interceptor does not concatenate it on every request.
        private string _authHeaderValue;

        private string _sessionId;
        private string _refreshToken;
        private DateTime _sessionExpiresAt;

        public bool IsAuth { get; private set; }
        public string SessionId => _sessionId;

        public event Action<GetAuthDataDto> OnLogin;
        public event Action<GetAuthDataDto> OnAuthConflict;
        public event Action OnSessionRefreshed;
        public event Action OnSessionExpired;

        public AuthenticationService(Configuration configuration, Logger.ILogger logger, IStorage storage, RestApiClient restApi, WebViewService webView)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _storage = storage;
            _webView = webView;

            _restApi.UseRequestInterceptor(AuthTokenInterceptor);
            _restApi.SetSessionRefresher(this);
        }

        // Helpers for URL composition. All login/link routes are scoped per branch;
        // refresh / logout / unlink are NOT — they use the branch already stored on the Session.
        private string AuthLoginScope() => $"{AUTH_BRANCH_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.BranchId}/login";
        private string LinkScope() => $"{LINK_BRANCH_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.BranchId}";
        private string UnlinkScope() => $"{UNLINK_ROUTE}/{_configuration.ProjectId}";
        private string SessionScope() => $"{AUTH_ROUTE}/{_configuration.ProjectId}";
        private string AccountsScope() => $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}";

        public AsyncOperation<RestApiResult<GetAuthDataDto>> InitializeAsync()
        {
            if (_storage.HasKey(REFRESH_TOKEN_KEY) == false)
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.Success(null));
            }

            var savedRefresh = _storage.GetString(REFRESH_TOKEN_KEY);
            if (string.IsNullOrWhiteSpace(savedRefresh))
            {
                ClearSessionAndStorage();
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.Success(null));
            }

            _refreshToken = savedRefresh;

            // Restore session via the refresh endpoint — branch comes from the stored Session, not the URL.
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();
            var refreshOp = RefreshSessionAsync();
            refreshOp.UseCompleted(_ =>
            {
                op.Complete(refreshOp.Result.IsSuccess
                    ? RestApiResult<GetAuthDataDto>.Success(null)
                    : RestApiResult<GetAuthDataDto>.Fail(refreshOp.Result.Error).WithMetaFrom(refreshOp.Result));
            });
            return op;
        }

        #region Login

        // Guest login is anonymous by design — the player doesn't pick a nickname,
        // the server generates one based on ProjectAuthSettings (regex / random
        // suffix / mode). The SDK therefore intentionally does not expose a
        // nickname argument here; if a project later wants to let guests choose
        // a display name they should be using device or username login instead.
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginGuestAsync(bool createAccount = true)
        {
            var route = $"{AuthLoginScope()}/guest";
            var dto = new LoginAsGuestDto { CreateAccount = createAccount };

            if (_storage.HasKey(GUESTID_KEY))
            {
                dto.GuestId = _storage.GetString(GUESTID_KEY);
            }

            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginDeviceAsync(string deviceId, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/device";
            var dto = new LoginByDeviceIdDto { DeviceId = deviceId, CreateAccount = createAccount, Nickname = nickname };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginEmailAsync(string email, string password, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/email";
            var dto = new LoginByEmailDto { Email = email, Password = password, CreateAccount = createAccount, Nickname = nickname };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginUsernameAsync(string username, string password, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/username";
            var dto = new LoginByUsernameDto { Login = username, Password = password, CreateAccount = createAccount, Nickname = nickname };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginPlatformAsync(
            string platformId,
            string externalUserId,
            string authCode = null,
            string platformToken = null,
            Dictionary<string, string> extra = null,
            bool createAccount = true,
            string nickname = null)
        {
            var route = $"{AuthLoginScope()}/platform";
            var dto = BuildPlatformDto(platformId, externalUserId, authCode, platformToken, extra, createAccount);
            dto.Nickname = nickname;
            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginGoogleSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = true, string nickname = null)
            => PostSignInAsync("google-sign-in", externalUserId, idToken, authCode, extra, createAccount, nickname);

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginSignInWithAppleAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = true, string nickname = null)
            => PostSignInAsync("sign-in-with-apple", externalUserId, idToken, authCode, extra, createAccount, nickname);

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginYandexSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = true, string nickname = null)
            => PostSignInAsync("yandex-sign-in", externalUserId, idToken, authCode, extra, createAccount, nickname);

        private AsyncOperation<RestApiResult<GetAuthDataDto>> PostSignInAsync(
            string suffix, string externalUserId, string idToken, string authCode,
            Dictionary<string, string> extra, bool createAccount, string nickname)
        {
            var route = $"{AuthLoginScope()}/{suffix}";
            var dto = BuildSignInDto(externalUserId, idToken, authCode, extra, createAccount);
            dto.Nickname = nickname;
            return PostAuthAsync(route, dto, noAuth: true);
        }

        public AsyncOperation<RestApiResult> StartOpenIdLoginAsync(int providerId, string successUrl)
        {
            var op = new AsyncOperation<RestApiResult>();
            var urlOp = BeginOpenIdLoginUrlAsync(providerId, successUrl);
            urlOp.UseCompleted(_ =>
            {
                if (!urlOp.Result.IsSuccess)
                {
                    op.Complete(RestApiResult.Fail(urlOp.Result.Error).WithMetaFrom(urlOp.Result));
                    return;
                }

                if (string.IsNullOrWhiteSpace(urlOp.Result.Data))
                {
                    op.Complete(RestApiResult.ValidationFail("OpenId auth url is empty.").WithMetaFrom(urlOp.Result));
                    return;
                }

                Application.OpenURL(urlOp.Result.Data);
                op.Complete(RestApiResult.Success().WithMetaFrom(urlOp.Result));
            });

            return op;
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> CompleteOpenIdLoginAsync(string openIdKey)
        {
            if (string.IsNullOrWhiteSpace(openIdKey))
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId key is empty."));
            }

            return GetAuthAsync($"{OPENID_RESULT_ROUTE}/{openIdKey}", noAuth: true);
        }

        public AsyncOperation<RestApiResult<string>> BeginOpenIdLoginUrlAsync(int providerId, string successUrl)
        {
            var route = $"{AuthLoginScope()}/openid/{providerId}";
            return BeginOpenIdLoginUrlAsync(route, successUrl);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginOpenIdAsync(int providerId, OpenIdLoginOptions options = null)
        {
            return LoginOpenIdAsync(successUrl => BeginOpenIdLoginUrlAsync(providerId, successUrl), options);
        }

        private AsyncOperation<RestApiResult<string>> BeginOpenIdLoginUrlAsync(string route, string successUrl)
        {
            if (string.IsNullOrWhiteSpace(successUrl))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("SuccessUrl is empty."));
            }

            var dto = new RegisterOpenIdProviderDto { SuccessUrl = successUrl };
            var config = new RestRequestConfig
            {
                NoAuth = true,
                DisableRetry = true,
                RedirectLimit = 0,
                AllowedHttpStatusCodes = new long[] { 301, 302, 303, 307, 308 }
            };

            return _restApi.PostAsync<string>(route, dto, config, ExtractRedirectLocation);
        }

        private AsyncOperation<RestApiResult<GetAuthDataDto>> LoginOpenIdAsync(Func<string, AsyncOperation<RestApiResult<string>>> beginLoginUrlAsync, OpenIdLoginOptions options)
        {
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();

            if (!OpenIdCallbackReceiverFactory.TryCreate(options, _webView, out var receiver, out var receiverError))
            {
                op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail(receiverError));
                return op;
            }

            var beginOp = beginLoginUrlAsync(receiver.SuccessUrl);
            beginOp.UseCompleted(_ =>
            {
                if (!beginOp.Result.IsSuccess)
                {
                    receiver.Dispose();
                    op.Complete(RestApiResult<GetAuthDataDto>.Fail(beginOp.Result.Error).WithMetaFrom(beginOp.Result));
                    return;
                }

                if (string.IsNullOrWhiteSpace(beginOp.Result.Data))
                {
                    receiver.Dispose();
                    op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId auth url is empty.").WithMetaFrom(beginOp.Result));
                    return;
                }

                if (!receiver.LaunchAuthUrl(beginOp.Result.Data))
                {
                    receiver.Dispose();
                    op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail("Failed to display OpenId auth url.").WithMetaFrom(beginOp.Result));
                    return;
                }

                var waitOp = receiver.WaitForCallbackAsync();
                waitOp.UseCompleted(_ =>
                {
                    receiver.Dispose();

                    var callbackResult = waitOp.Result;
                    if (!string.IsNullOrEmpty(callbackResult.ErrorMessage))
                    {
                        op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail(callbackResult.ErrorMessage));
                        return;
                    }

                    if (!callbackResult.IsSuccess)
                    {
                        op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId callback key was not received."));
                        return;
                    }

                    var completeOp = CompleteOpenIdLoginAsync(callbackResult.Key);
                    completeOp.UseCompleted(completed => op.Complete(completed.Result));
                });
            });

            return op;
        }

        private static string ExtractRedirectLocation(UnityWebRequest request)
        {
            return request.GetResponseHeader("Location") ?? request.GetResponseHeader("location");
        }

        #endregion

        #region Link

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkGuestAsync(bool createAccount = false)
        {
            var route = $"{LinkScope()}/guest";
            var dto = new LoginAsGuestDto { CreateAccount = createAccount };

            if (_storage.HasKey(GUESTID_KEY))
            {
                dto.GuestId = _storage.GetString(GUESTID_KEY);
            }

            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkDeviceAsync(string deviceId, bool createAccount = false)
        {
            var route = $"{LinkScope()}/device";
            var dto = new LoginByDeviceIdDto { DeviceId = deviceId, CreateAccount = createAccount };
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkEmailAsync(string email, string password, bool createAccount = false)
        {
            var route = $"{LinkScope()}/email";
            var dto = new LoginByEmailDto { Email = email, Password = password, CreateAccount = createAccount };
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkUsernameAsync(string username, string password, bool createAccount = false)
        {
            var route = $"{LinkScope()}/username";
            var dto = new LoginByUsernameDto { Login = username, Password = password, CreateAccount = createAccount };
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkOpenIdAsync(string userId, bool createAccount = false)
        {
            var route = $"{LinkScope()}/openid";
            var dto = new LoginByOpenIdDto { UserId = userId, CreateAccount = createAccount };
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkPlatformAsync(
            string platformId,
            string externalUserId,
            string authCode = null,
            string platformToken = null,
            Dictionary<string, string> extra = null,
            bool createAccount = false)
        {
            var route = $"{LinkScope()}/platform";
            var dto = BuildPlatformDto(platformId, externalUserId, authCode, platformToken, extra, createAccount);
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkGoogleSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = false)
            => PostLinkSignInAsync("google-sign-in", externalUserId, idToken, authCode, extra, createAccount);

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkSignInWithAppleAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = false)
            => PostLinkSignInAsync("sign-in-with-apple", externalUserId, idToken, authCode, extra, createAccount);

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkYandexSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null, bool createAccount = false)
            => PostLinkSignInAsync("yandex-sign-in", externalUserId, idToken, authCode, extra, createAccount);

        private AsyncOperation<RestApiResult<GetAuthDataDto>> PostLinkSignInAsync(
            string suffix, string externalUserId, string idToken, string authCode,
            Dictionary<string, string> extra, bool createAccount)
        {
            var route = $"{LinkScope()}/{suffix}";
            var dto = BuildSignInDto(externalUserId, idToken, authCode, extra, createAccount);
            return PostAuthAsync(route, dto);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> ResolveLinkConflictAsync(LinkAuthProviderDto dto)
        {
            var route = $"{LinkScope()}/conflict/resolve";
            return PostAuthAsync(route, dto);
        }

        #endregion

        #region Unlink

        public AsyncOperation<RestApiResult> UnlinkOpenIdAsync(string userId)
        {
            var route = $"{UnlinkScope()}/openid";
            var dto = new LoginByOpenIdDto { UserId = userId };
            return DeleteAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> UnlinkPlatformAsync(
            string platformId, string externalUserId, string authCode = null,
            string platformToken = null, Dictionary<string, string> extra = null)
        {
            var route = $"{UnlinkScope()}/platform";
            var dto = BuildPlatformDto(platformId, externalUserId, authCode, platformToken, extra, createAccount: false);
            return DeleteAsync(route, dto);
        }

        public AsyncOperation<RestApiResult> UnlinkGoogleSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null)
            => DeleteSignInAsync("google-sign-in", externalUserId, idToken, authCode, extra);

        public AsyncOperation<RestApiResult> UnlinkSignInWithAppleAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null)
            => DeleteSignInAsync("sign-in-with-apple", externalUserId, idToken, authCode, extra);

        public AsyncOperation<RestApiResult> UnlinkYandexSignInAsync(
            string externalUserId, string idToken = null, string authCode = null,
            Dictionary<string, string> extra = null)
            => DeleteSignInAsync("yandex-sign-in", externalUserId, idToken, authCode, extra);

        private AsyncOperation<RestApiResult> DeleteSignInAsync(
            string suffix, string externalUserId, string idToken, string authCode, Dictionary<string, string> extra)
        {
            var route = $"{UnlinkScope()}/{suffix}";
            var dto = BuildSignInDto(externalUserId, idToken, authCode, extra, createAccount: false);
            return DeleteAsync(route, dto);
        }

        private AsyncOperation<RestApiResult> DeleteAsync(string route, object dto)
        {
            // Unlink revokes all sessions on success — clear local state too.
            var op = _restApi.DeleteAsync(route, dto);
            op.UseCompleted(_ =>
            {
                if (op.Result.IsSuccess)
                {
                    ClearSessionAndStorage();
                    OnSessionExpired?.Invoke();
                }
            });
            return op;
        }

        #endregion

        #region Session

        public AsyncOperation<RestApiResult> RefreshSessionAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                _logger.Log("RefreshSessionAsync called without refresh token.");
                return AsyncOperation<RestApiResult>.CreateCompleted(RestApiResult.ValidationFail("Refresh token is empty."));
            }

            // Refresh URL does NOT include branchId — the server reads branch/environment from the Session.
            var route = $"{SessionScope()}/session/refresh";
            var dto = new RefreshSessionDto { RefreshToken = _refreshToken };

            var refreshOp = _restApi.PostAsync<SessionRefreshResultDto>(route, dto, new RestRequestConfig { NoAuth = true, DisableRetry = true });
            var resultOp = new AsyncOperation<RestApiResult>();

            refreshOp.UseCompleted(completed =>
            {
                if (!completed.Result.IsSuccess || completed.Result.Data?.Session == null)
                {
                    HandleSessionExpired();
                    resultOp.Complete(completed.Result.IsSuccess
                        ? RestApiResult.Fail(RestApiError.Validation("Refresh response without session."))
                        : completed.Result);
                    return;
                }

                // The refresh response carries a fresh access token; without adopting it the
                // interceptor would keep sending the expired one and every authed call would 401.
                if (!string.IsNullOrEmpty(completed.Result.Data.Token))
                {
                    SetAuthToken(completed.Result.Data.Token);
                }

                ApplySession(completed.Result.Data.Session);
                IsAuth = true;
                SaveSessionToStorage();
                OnSessionRefreshed?.Invoke();
                resultOp.Complete(RestApiResult.Success());
            });

            return resultOp;
        }

        public AsyncOperation<RestApiResult> LogoutAsync()
        {
            var route = $"{AccountsScope()}/logout";
            var dto = new LogoutSessionDto { SessionId = _sessionId };

            var op = _restApi.PostAsync(route, dto);
            op.UseCompleted(_ =>
            {
                ClearSessionAndStorage();
                OnSessionExpired?.Invoke();
            });
            return op;
        }

        public AsyncOperation<RestApiResult> LogoutAllAsync()
        {
            var route = $"{AccountsScope()}/logout/all";
            var op = _restApi.PostAsync(route, new { });
            op.UseCompleted(_ =>
            {
                ClearSessionAndStorage();
                OnSessionExpired?.Invoke();
            });
            return op;
        }

        /// <summary>
        /// Drops the session locally — clears in-memory auth state (token, session id, refresh
        /// token) and removes the persisted session keys — without contacting the server. Fires
        /// <see cref="OnSessionExpired"/> so listeners can return to the auth flow. Use this to
        /// sign out offline or to discard a stored session that can no longer be restored; for a
        /// server-side session revoke use <see cref="LogoutAsync"/> / <see cref="LogoutAllAsync"/>.
        /// </summary>
        public void ClearLocalSession()
        {
            ClearSessionAndStorage();
            OnSessionExpired?.Invoke();
        }

        #endregion

        #region Internal handlers

        private static LoginByPlatformDto BuildPlatformDto(
            string platformId, string externalUserId, string authCode, string platformToken,
            Dictionary<string, string> extra, bool createAccount)
            => new LoginByPlatformDto
            {
                PlatformId = platformId,
                ExternalUserId = externalUserId,
                AuthCode = authCode,
                PlatformToken = platformToken,
                Extra = extra,
                CreateAccount = createAccount
            };

        private static LoginBySignInProviderDto BuildSignInDto(
            string externalUserId, string idToken, string authCode,
            Dictionary<string, string> extra, bool createAccount)
            => new LoginBySignInProviderDto
            {
                ExternalUserId = externalUserId,
                IdToken = idToken,
                AuthCode = authCode,
                Extra = extra,
                CreateAccount = createAccount
            };

        private AsyncOperation<RestApiResult<GetAuthDataDto>> PostAuthAsync(string route, object dto, bool noAuth = false)
        {
            var config = noAuth ? new RestRequestConfig { NoAuth = true, DisableRetry = true } : null;
            var op = _restApi.PostAsync<GetAuthDataDto>(route, dto, config);
            op.UseCompleted(HandleAuthCompleted);
            return op;
        }

        private AsyncOperation<RestApiResult<GetAuthDataDto>> GetAuthAsync(string route, bool noAuth = false)
        {
            var config = noAuth ? new RestRequestConfig { NoAuth = true, DisableRetry = true } : null;
            var operation = _restApi.GetAsync<GetAuthDataDto>(route, config);

            operation.UseCompleted(HandleAuthCompleted);
            return operation;
        }

        private void HandleAuthCompleted(IAsyncOperation<RestApiResult<GetAuthDataDto>> operation)
        {
            _logger.Log("handle auth");
            var result = operation.Result;

            if (!result.IsSuccess)
            {
                _logger.Error(result.Error?.Message ?? "Auth request failed.");
                return;
            }

            var data = result.Data;
            if (data == null)
            {
                _logger.Error("Empty auth response");
                return;
            }

            if (data.Status == AuthResultStatus.Conflict)
            {
                OnAuthConflict?.Invoke(data);
                return;
            }

            if (string.IsNullOrEmpty(data.Token) || data.Session == null)
            {
                _logger.Error("Auth response without token or session");
                return;
            }

            SetAuthToken(data.Token);
            ApplySession(data.Session);
            IsAuth = true;

            if (string.IsNullOrEmpty(data.GuestId) == false)
            {
                _storage.SaveString(GUESTID_KEY, data.GuestId);
            }

            SaveSessionToStorage();
            OnLogin?.Invoke(data);
        }

        private void ApplySession(SessionInfoDto session)
        {
            _sessionId = session.SessionId;
            _refreshToken = session.RefreshToken;
            _sessionExpiresAt = session.ExpiresAt;
        }

        private void SetAuthToken(string token)
        {
            _authToken = token;
            _authHeaderValue = string.IsNullOrEmpty(token) ? null : "Bearer " + token;
        }

        private void ClearSession()
        {
            _authToken = null;
            _authHeaderValue = null;
            _sessionId = null;
            _refreshToken = null;
            _sessionExpiresAt = default;
            IsAuth = false;
        }

        private void SaveSessionToStorage()
        {
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                _storage.SaveString(REFRESH_TOKEN_KEY, _refreshToken);
            }

            if (!string.IsNullOrEmpty(_sessionId))
            {
                _storage.SaveString(SESSIONID_KEY, _sessionId);
            }

            _storage.SaveString(SESSION_EXPIRESAT_KEY, _sessionExpiresAt.ToString("o"));
        }

        private void ClearSessionStorage()
        {
            _storage.DeleteKeys(REFRESH_TOKEN_KEY, SESSIONID_KEY, SESSION_EXPIRESAT_KEY);
        }

        private void ClearSessionAndStorage()
        {
            ClearSession();
            ClearSessionStorage();
        }

        private RestRequestConfig AuthTokenInterceptor(RestRequestConfig config)
        {
            if (config.NoAuth == true)
            {
                return config;
            }

            if (_authHeaderValue != null)
            {
                config.Headers ??= new Dictionary<string, string>();
                config.Headers["Authorization"] = _authHeaderValue;
            }

            return config;
        }

        private void HandleSessionExpired()
        {
            ClearSessionAndStorage();
            OnSessionExpired?.Invoke();
        }

        bool ISessionRefresher.CanRefresh => string.IsNullOrEmpty(_refreshToken) == false;

        AsyncOperation<RestApiResult> ISessionRefresher.RefreshSessionAsync()
        {
            return RefreshSessionAsync();
        }

        #endregion

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

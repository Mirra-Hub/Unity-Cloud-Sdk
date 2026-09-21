using System;
using System.Collections.Generic;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Auth.OpenId;
using MirraCloud.Core.Errors;
using MirraCloud.Core.WebView;
using MirraCloud.Json;
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

        // The platform the player signs in on. The server reads it on every sign-in call (login, OpenID begin,
        // login-methods); on link it takes the platform from the session instead and the gateway drops this header.
        private const string PLATFORM_KEY_HEADER = "PlatformKey";

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

        private string _refreshingToken;
        private List<AsyncOperation<RestApiResult>> _refreshWaiters;
        private bool _refreshSignsOutOnTransientFailure;
        private bool _missingPlatformKeyReported;

        public bool IsAuth { get; private set; }
        public string SessionId => _sessionId;

        public event Action<GetAuthDataDto> OnLogin;
        public event Action<GetAuthDataDto> OnAuthConflict;
        public event Action OnSessionRefreshed;
        public event Action OnSessionExpired;

        /// <summary>
        /// The account snapshot that came with a successful session refresh — the restore in
        /// <see cref="InitializeAsync"/> as well as a refresh after a 401. Raised right before
        /// <see cref="OnSessionRefreshed"/>, and only when the response carried an account. A restored
        /// session never raises <see cref="OnLogin"/>, so this is how PlayerAccountService learns the account.
        /// </summary>
        internal event Action<AccountDto> OnSessionAccountRefreshed;

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

        /// <summary>
        /// Signs in with the store of this build's platform (<see cref="Configuration.PlatformKey"/>): Google Play Games,
        /// VK Games, Yandex Games or Apple Game Center, whichever store sign-in the platform has. Pass the store's
        /// proof: <paramref name="extra"/> for the VK / Yandex Games launch params (with their signature) and the Game
        /// Center signature (<c>publicKeyUrl</c>, <c>signature</c>, <c>salt</c>, <c>timestamp</c>),
        /// <paramref name="authCode"/> for Google Play (the server auth code).
        /// </summary>
        /// <param name="platformToken">Not read by any of today's stores; kept for the request shape.</param>
        /// <param name="externalUserId">Read only by Game Center (its <c>teamPlayerID</c>); every other store takes the
        /// player's id from the verified proof.</param>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginPlatformAsync(
            Dictionary<string, string> extra = null,
            string authCode = null,
            string platformToken = null,
            string externalUserId = null,
            bool createAccount = true,
            string nickname = null)
        {
            var route = $"{AuthLoginScope()}/platform";
            var dto = BuildPlatformDto(externalUserId, authCode, platformToken, extra, createAccount);
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

        /// <summary>
        /// Begins a browser sign-in and opens its page in the system browser. <paramref name="providerKey"/> is the
        /// <see cref="LoginMethodDto.IntegrationKey"/> of an <c>openid</c>, <c>google</c>, <c>apple</c> or
        /// <c>yandex</c> method from <see cref="GetLoginMethodsAsync"/>. Finish it with <see cref="CompleteOpenIdLoginAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult> StartOpenIdLoginAsync(string providerKey, string successUrl)
        {
            var op = new AsyncOperation<RestApiResult>();
            var urlOp = BeginOpenIdLoginUrlAsync(providerKey, successUrl);
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

        /// <summary>
        /// Begins a browser sign-in and returns the provider's page to open (low-level step 1).
        /// <paramref name="providerKey"/> is the <see cref="LoginMethodDto.IntegrationKey"/> of the method.
        /// </summary>
        public AsyncOperation<RestApiResult<string>> BeginOpenIdLoginUrlAsync(string providerKey, string successUrl)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("OpenId provider key is empty."));
            }

            var route = $"{AuthLoginScope()}/openid/{Uri.EscapeDataString(providerKey)}";
            return RequestOpenIdLoginUrlAsync(route, successUrl);
        }

        /// <summary>
        /// Signs in through a provider's page (OpenID, or Google / Apple / Yandex ID without their native SDK) and
        /// waits for the player to come back. <paramref name="providerKey"/> is the
        /// <see cref="LoginMethodDto.IntegrationKey"/> of the method from <see cref="GetLoginMethodsAsync"/>: a platform
        /// may offer several OpenID providers, and the key picks one.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginOpenIdAsync(string providerKey, OpenIdLoginOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId provider key is empty."));
            }

            return RunOpenIdLoginAsync(successUrl => BeginOpenIdLoginUrlAsync(providerKey, successUrl), options);
        }

        private AsyncOperation<RestApiResult<string>> RequestOpenIdLoginUrlAsync(string route, string successUrl)
        {
            if (string.IsNullOrWhiteSpace(successUrl))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("SuccessUrl is empty."));
            }

            var dto = new RegisterOpenIdProviderDto { SuccessUrl = successUrl };
            var config = AuthRequestConfig(noAuth: true);
            config.RedirectLimit = 0;
            config.AllowedHttpStatusCodes = new long[] { 301, 302, 303, 307, 308 };

            return _restApi.PostAsync<string>(route, dto, config, ExtractRedirectLocation);
        }

        private AsyncOperation<RestApiResult<GetAuthDataDto>> RunOpenIdLoginAsync(Func<string, AsyncOperation<RestApiResult<string>>> beginLoginUrlAsync, OpenIdLoginOptions options)
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

        /// <summary>
        /// The sign-in methods this build's platform (<see cref="Configuration.PlatformKey"/>) offers right now, in the
        /// order set in the console — draw the sign-in buttons from it. Needs no session.
        /// </summary>
        /// <remarks>
        /// A platform the project does not know or has switched off answers 403 with
        /// <c>platforms.platform_unknown</c> / <c>platforms.platform_disabled</c> (<c>platforms.platform_key_required</c>
        /// when the key is not set, <c>platforms.platform_not_configured</c> when the project has no platform yet) —
        /// the same refusals every sign-in call would get.
        /// </remarks>
        public AsyncOperation<RestApiResult<LoginMethodsDto>> GetLoginMethodsAsync()
        {
            var route = $"{SessionScope()}/login-methods";
            return _restApi.GetAsync<LoginMethodsDto>(route, AuthRequestConfig(noAuth: true));
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

        /// <summary>
        /// Links the store sign-in of the platform the current session signed in on. Takes the same proof as
        /// <see cref="LoginPlatformAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkPlatformAsync(
            Dictionary<string, string> extra = null,
            string authCode = null,
            string platformToken = null,
            string externalUserId = null,
            bool createAccount = false)
        {
            var route = $"{LinkScope()}/platform";
            var dto = BuildPlatformDto(externalUserId, authCode, platformToken, extra, createAccount);
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

        /// <summary>
        /// Removes a store sign-in from the account. It is addressed by the platform it was made on and the player's id
        /// at the store, so it can be removed from any platform, including one switched off since.
        /// </summary>
        public AsyncOperation<RestApiResult> UnlinkPlatformAsync(string platformKey, string externalUserId)
        {
            var route = $"{UnlinkScope()}/platform";
            var dto = new UnlinkPlatformDto { PlatformKey = platformKey, ExternalUserId = externalUserId };
            return DeleteAsync(route, dto);
        }

        /// <summary>Removes the Google sign-in with this Google user id (<c>sub</c>) from the account.</summary>
        public AsyncOperation<RestApiResult> UnlinkGoogleSignInAsync(string externalUserId)
            => DeleteSignInAsync("google-sign-in", externalUserId);

        /// <summary>Removes the Sign in with Apple sign-in with this Apple user id (<c>sub</c>) from the account.</summary>
        public AsyncOperation<RestApiResult> UnlinkSignInWithAppleAsync(string externalUserId)
            => DeleteSignInAsync("sign-in-with-apple", externalUserId);

        /// <summary>Removes the Yandex ID sign-in with this Yandex user id from the account.</summary>
        public AsyncOperation<RestApiResult> UnlinkYandexSignInAsync(string externalUserId)
            => DeleteSignInAsync("yandex-sign-in", externalUserId);

        private AsyncOperation<RestApiResult> DeleteSignInAsync(string suffix, string externalUserId)
        {
            var route = $"{UnlinkScope()}/{suffix}";
            var dto = new UnlinkSignInProviderDto { ExternalUserId = externalUserId };
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

        /// <summary>
        /// Exchanges the refresh token for a new access token. Calls made while a refresh of the same token
        /// is in flight do not send another request: they wait for that one and get its outcome.
        /// </summary>
        public AsyncOperation<RestApiResult> RefreshSessionAsync() => RefreshSessionAsync(signOutOnTransientFailure: true);

        /// <summary>
        /// <paramref name="signOutOnTransientFailure"/>: whether a refresh that failed for want of the server
        /// (no connection, a timeout, a 5xx) signs the player out. A refresh the SDK starts on its own — after a
        /// profile switch — passes false: the session is still valid, only this attempt did not get through, and
        /// the next 401 refreshes again. A refresh token the server rejects signs the player out either way. When
        /// calls share one request, it signs out if any of them would.
        /// </summary>
        internal AsyncOperation<RestApiResult> RefreshSessionAsync(bool signOutOnTransientFailure)
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                _logger.Log("RefreshSessionAsync called without refresh token.");
                return AsyncOperation<RestApiResult>.CreateCompleted(RestApiResult.ValidationFail("Refresh token is empty."));
            }

            var resultOp = new AsyncOperation<RestApiResult>();

            if (_refreshWaiters != null && _refreshingToken == _refreshToken)
            {
                _refreshWaiters.Add(resultOp);
                _refreshSignsOutOnTransientFailure |= signOutOnTransientFailure;
                return resultOp;
            }

            // Refresh URL does NOT include branchId — the server reads branch/environment from the Session.
            var route = $"{SessionScope()}/session/refresh";
            var dto = new RefreshSessionDto { RefreshToken = _refreshToken };

            var refreshOp = _restApi.PostAsync<SessionRefreshResultDto>(route, dto, new RestRequestConfig { NoAuth = true, DisableRetry = true }, ReadRefreshResult);

            var waiters = new List<AsyncOperation<RestApiResult>> { resultOp };
            _refreshWaiters = waiters;
            _refreshingToken = _refreshToken;
            _refreshSignsOutOnTransientFailure = signOutOnTransientFailure;

            refreshOp.UseCompleted(completed =>
            {
                var signOut = _refreshSignsOutOnTransientFailure;
                if (ReferenceEquals(_refreshWaiters, waiters))
                {
                    _refreshWaiters = null;
                    _refreshingToken = null;
                }

                var result = ApplyRefreshResult(completed.Result, signOut);
                foreach (var waiter in waiters)
                {
                    try
                    {
                        waiter.Complete(result);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            });

            return resultOp;
        }

        private RestApiResult ApplyRefreshResult(RestApiResult<SessionRefreshResultDto> response,
            bool signOutOnTransientFailure)
        {
            if (!response.IsSuccess && !signOutOnTransientFailure && IsTransient(response.Error))
            {
                return response;
            }

            if (!response.IsSuccess || response.Data?.Session == null)
            {
                HandleSessionExpired();
                return response.IsSuccess
                    ? RestApiResult.Fail(RestApiError.Validation("Refresh response without session."))
                    : response;
            }

            // The refresh response carries a fresh access token; without adopting it the
            // interceptor would keep sending the expired one and every authed call would 401.
            if (!string.IsNullOrEmpty(response.Data.Token))
            {
                SetAuthToken(response.Data.Token);
            }

            ApplySession(response.Data.Session);
            IsAuth = true;
            SaveSessionToStorage();

            if (response.Data.PlayerInfo != null)
            {
                OnSessionAccountRefreshed?.Invoke(response.Data.PlayerInfo);
            }

            OnSessionRefreshed?.Invoke();
            return RestApiResult.Success();
        }

        /// <summary>A failure that says nothing about the session: the request did not get a real answer.</summary>
        private static bool IsTransient(RestApiError error) =>
            error != null &&
            (error.Type == RestApiErrorType.Network ||
             error.Type == RestApiErrorType.Cancelled ||
             (error.Type == RestApiErrorType.Http && error.HttpStatusCode >= 500));

        /// <summary>
        /// Reads the refresh response. The account in it is extra: if this build cannot read it — an enum
        /// value it does not know yet, say — the refresh goes through without it, because a refresh that
        /// fails signs the player out. Anything wrong with the token or session still fails as before.
        /// </summary>
        private SessionRefreshResultDto ReadRefreshResult(UnityWebRequest request)
        {
            var body = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            Exception accountError;
            try
            {
                return _restApi.JsonService.FromJson<SessionRefreshResultDto>(body);
            }
            catch (Exception e)
            {
                accountError = e;
            }

            var withoutAccount = _restApi.JsonService.FromJson<SessionRefreshWithoutAccountDto>(body);
            _logger.Error($"Session refresh: the account in the response could not be read and was skipped. {accountError.Message}");

            return withoutAccount == null
                ? null
                : new SessionRefreshResultDto
                {
                    AccountId = withoutAccount.AccountId,
                    ProjectId = withoutAccount.ProjectId,
                    Token = withoutAccount.Token,
                    Session = withoutAccount.Session
                };
        }

        /// <summary><see cref="SessionRefreshResultDto"/> minus the account, for <see cref="ReadRefreshResult"/>'s fallback.</summary>
        [Serializable]
        private sealed class SessionRefreshWithoutAccountDto
        {
            [JsonNameCamel] public string AccountId;
            [JsonNameCamel] public string ProjectId;
            [JsonNameCamel] public string Token;
            [JsonNameCamel] public SessionInfoDto Session;
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
            string externalUserId, string authCode, string platformToken,
            Dictionary<string, string> extra, bool createAccount)
            => new LoginByPlatformDto
            {
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
            var op = _restApi.PostAsync<GetAuthDataDto>(route, dto, AuthRequestConfig(noAuth));
            op.UseCompleted(HandleAuthCompleted);
            return op;
        }

        private AsyncOperation<RestApiResult<GetAuthDataDto>> GetAuthAsync(string route, bool noAuth = false)
        {
            var operation = _restApi.GetAsync<GetAuthDataDto>(route, AuthRequestConfig(noAuth));

            operation.UseCompleted(HandleAuthCompleted);
            return operation;
        }

        /// <summary>
        /// The config of every sign-in and link call, carrying the <c>PlatformKey</c> header. It has to be set here:
        /// sign-in calls are <c>NoAuth</c>, and the request interceptors skip those. Refresh and logout do not come
        /// through here — the server takes their platform from the session.
        /// </summary>
        private RestRequestConfig AuthRequestConfig(bool noAuth)
        {
            var config = noAuth ? new RestRequestConfig { NoAuth = true, DisableRetry = true } : new RestRequestConfig();

            var platformKey = _configuration.ResolvedPlatformKey;
            if (platformKey != null)
            {
                config.Headers = new Dictionary<string, string> { [PLATFORM_KEY_HEADER] = platformKey };
            }
            else if (noAuth && _missingPlatformKeyReported == false)
            {
                // Only a sign-in needs it: a link takes the platform from the session.
                _missingPlatformKeyReported = true;
                _logger.Error(
                    "Configuration.PlatformKey is empty, so the server refuses every sign-in " +
                    $"({CloudErrorCodes.PlatformsPlatformKeyRequired}). Pick the platform of this build in " +
                    "Tools > Mirra Cloud > Manager.");
            }

            return config;
        }

        private void HandleAuthCompleted(IAsyncOperation<RestApiResult<GetAuthDataDto>> operation)
        {
            _logger.Log("handle auth");
            var result = operation.Result;

            if (!result.IsSuccess)
            {
                var cloudError = result.Error.FirstCloudError();
                _logger.Error(cloudError != null
                    ? $"{cloudError.Code} — {cloudError.Message}"
                    : result.Error?.Message ?? "Auth request failed.");
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

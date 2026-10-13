using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Auth.OpenId;
using MirraCloud.Core.Enums;
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
        private string _lastConflictTicket;
        private readonly SessionDeviceDto _device;

        // InitializeAsync calls still reading the saved refresh token, and the refreshes asked for meanwhile. A request
        // sent right after InitializeAsync can come back 401 before the token is known; its refresh has to be the
        // restore's — as it was when the token was read on the spot — not a refusal for want of a token.
        private int _restoresReading;
        private List<(AsyncOperation<RestApiResult> Waiter, bool SignOutOnTransientFailure)> _restoreWaiters;
        private TaskCompletionSource<bool> _savedSessionRead;

        public bool IsAuth { get; private set; }
        public string SessionId => _sessionId;

        public event Action<GetAuthDataDto> OnLogin;
        public event Action<GetAuthDataDto> OnAuthConflict;
        public event Action OnSessionRefreshed;
        public event Action OnSessionExpired;

        /// <summary>
        /// The server revoked the session for good — the account is banned (the argument is the error code,
        /// <see cref="CloudErrorCodes.PlayerAccountsAccountBanned"/>). Raised after <see cref="OnSessionExpired"/>; do
        /// not retry or sign in again automatically.
        /// </summary>
        public event Action<string> OnSessionRevoked;

        /// <summary>
        /// A sign-in or session refresh came back without a usable profile: none is selected, or the selected one is
        /// banned. Profile-scoped services refuse calls until the game selects or creates a profile
        /// (<c>PlayerAccount.SelectProfileAsync</c> / <c>CreateProfileAsync</c>); <c>PlayerAccount.Profiles</c>
        /// carries each profile's status.
        /// </summary>
        public event Action OnProfileSelectionRequired;

        /// <summary>
        /// The account snapshot that came with a successful session refresh — the restore in
        /// <see cref="InitializeAsync"/> as well as a refresh after a 401. Raised right before
        /// <see cref="OnSessionRefreshed"/>, and only when the response carried an account. A restored
        /// session never raises <see cref="OnLogin"/>, so this is how PlayerAccountService learns the account.
        /// </summary>
        internal event Action<AccountDto> OnSessionAccountRefreshed;

        /// <summary>
        /// An email was confirmed with <see cref="ConfirmEmailAsync"/>: the new address of an email change, or null
        /// when the account's current email got confirmed. PlayerAccountService updates the account with it.
        /// </summary>
        internal event Action<string> OnEmailConfirmed;

        /// <summary>The phone number <see cref="ChangePhoneAsync"/> set; null when it was removed.</summary>
        internal event Action<string> OnPhoneChanged;

        // ChangeEmailAsync's new address by the id of its code, so the account learns it once the code is confirmed.
        private readonly Dictionary<string, string> _pendingEmails = new Dictionary<string, string>();

        public AuthenticationService(Configuration configuration, Logger.ILogger logger, IStorage storage, RestApiClient restApi, WebViewService webView)
        {
            _configuration = configuration;
            _restApi = restApi;
            _logger = logger;
            _storage = storage;
            _webView = webView;
            _device = DeviceInfoProvider.Collect();

            _restApi.UseRequestInterceptor(AuthTokenInterceptor);
            _restApi.SetSessionRefresher(this);
        }

        // Helpers for URL composition. All login/link routes are scoped per branch;
        // refresh / logout / unlink are NOT — they use the branch already stored on the Session.
        private string AuthBranchScope() => $"{AUTH_BRANCH_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.Branch}";
        private string AuthLoginScope() => $"{AuthBranchScope()}/login";
        private string LinkScope() => $"{LINK_BRANCH_ROUTE}/{_configuration.ProjectId}/branches/{_configuration.Branch}";
        private string UnlinkScope() => $"{UNLINK_ROUTE}/{_configuration.ProjectId}";
        private string SessionScope() => $"{AUTH_ROUTE}/{_configuration.ProjectId}";
        private string AccountsScope() => $"{ACCOUNTS_ROUTE}/{_configuration.ProjectId}";

        /// <summary>
        /// Restores the session saved by an earlier launch, if there is one: success with <c>null</c> data either
        /// way, and <see cref="IsAuth"/> tells which. A saved session is refreshed, so a stale one fails here
        /// instead of on the first call. Completes on a later frame even with nothing to restore — the saved session
        /// is read from the SDK's local storage (SQLite, or IndexedDB in WebGL) in the background.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> InitializeAsync()
        {
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();
            BeginReadingSavedSession();
            RestoreSavedSessionAsync(op);
            return op;
        }

        private async void RestoreSavedSessionAsync(AsyncOperation<RestApiResult<GetAuthDataDto>> op)
        {
            string savedRefresh = null;

            try
            {
                // Never complete inside InitializeAsync itself, even when the storage is loaded and holds nothing:
                // a handler attached after the call returns would not hear about an operation already complete.
                await Task.Yield();
                await _storage.Ready;
                savedRefresh = _storage.GetString(AuthStorageKeys.RefreshToken);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (string.IsNullOrWhiteSpace(savedRefresh))
            {
                EndReadingSavedSession();
                op.Complete(RestApiResult<GetAuthDataDto>.Success(null));
                return;
            }

            _refreshToken = savedRefresh;
            EndReadingSavedSession();

            // Restore session via the refresh endpoint — branch comes from the stored Session, not the URL.
            OnDone(RefreshSessionAsync(), refreshed => op.Complete(refreshed.IsSuccess
                ? RestApiResult<GetAuthDataDto>.Success(null)
                : RestApiResult<GetAuthDataDto>.Fail(refreshed.Error).WithMetaFrom(refreshed)));
        }

        /// <summary>
        /// Completes once no <see cref="InitializeAsync"/> is still reading the saved session — from then on
        /// <see cref="HasSession"/> knows whether there is one to restore. Complete at once when nothing is read.
        /// </summary>
        internal Task SavedSessionKnown => _savedSessionRead?.Task ?? Task.CompletedTask;

        private void BeginReadingSavedSession()
        {
            if (_restoresReading++ == 0)
            {
                _savedSessionRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>
        /// The saved token is known (or known to be absent): refreshes asked for while it was read go ahead now — with
        /// a token they join the restore's refresh, without one they fail as any refresh without a token does.
        /// </summary>
        private void EndReadingSavedSession()
        {
            if (--_restoresReading > 0)
            {
                return;
            }

            var read = _savedSessionRead;
            var waiters = _restoreWaiters;
            _savedSessionRead = null;
            _restoreWaiters = null;

            if (waiters != null)
            {
                foreach (var (waiter, signOut) in waiters)
                {
                    OnDone(RefreshSessionAsync(signOut), waiter.Complete);
                }
            }

            read?.TrySetResult(true);
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
            var dto = new LoginAsGuestDto { CreateAccount = createAccount, Device = _device };
            return PostWithSavedGuestId(dto, () => PostAuthAsync(route, dto, noAuth: true));
        }

        /// <summary>
        /// Sends a guest sign-in or link once the saved guest id is known. It is read from local storage in the
        /// background at start, and a request sent before that would make a returning guest a new one.
        /// </summary>
        private AsyncOperation<RestApiResult<GetAuthDataDto>> PostWithSavedGuestId(LoginAsGuestDto dto,
            Func<AsyncOperation<RestApiResult<GetAuthDataDto>>> post)
        {
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();
            PostWithSavedGuestIdAsync(dto, post, op);
            return op;
        }

        private async void PostWithSavedGuestIdAsync(LoginAsGuestDto dto,
            Func<AsyncOperation<RestApiResult<GetAuthDataDto>>> post, AsyncOperation<RestApiResult<GetAuthDataDto>> op)
        {
            try
            {
                await _storage.Ready;
                dto.GuestId = _storage.GetString(AuthStorageKeys.GuestId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            OnDone(post(), op.Complete);
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginDeviceAsync(string deviceId, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/device";
            var dto = new LoginByDeviceIdDto { DeviceId = deviceId, CreateAccount = createAccount, Options = LoginOptionsDto.WithNickname(nickname), Device = _device };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        /// <summary>
        /// Signs in with a password and the email or the username (<see cref="LoginMethodKind.Password"/>). The login
        /// is the account's username, compared exactly; fill one of <paramref name="email"/> and
        /// <paramref name="username"/>. With <paramref name="createAccount"/> an unknown player is registered with what
        /// the platform takes (<see cref="LoginMethodDto.Password"/>): the email, the username or both.
        /// </summary>
        /// <remarks>
        /// When the platform confirms emails, a registration with an email answers
        /// <see cref="AuthResultStatus.EmailVerificationRequired"/> and creates nothing yet: a code goes to the email
        /// (<see cref="GetAuthDataDto.EmailVerification"/>) — finish with <see cref="ConfirmPasswordRegistrationAsync"/>.
        /// </remarks>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginPasswordAsync(
            string email, string username, string password, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/password";
            var dto = new LoginByPasswordDto
            {
                Email = NullIfBlank(email), Username = NullIfBlank(username), Password = password,
                CreateAccount = createAccount, Options = LoginOptionsDto.WithNickname(nickname), Device = _device
            };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        /// <summary>
        /// Finishes a registration <see cref="LoginPasswordAsync"/> held for email confirmation: the account is created
        /// with the code from the letter, its email already confirmed.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> ConfirmPasswordRegistrationAsync(string challengeId, string code)
        {
            var route = $"{AuthLoginScope()}/password/confirm";
            var dto = new ConfirmEmailCodeDto { ChallengeId = challengeId, Code = code, Device = _device };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        /// <summary>
        /// Sends a one-time sign-in code to the email (<see cref="LoginMethodKind.EmailCode"/>); finish with
        /// <see cref="LoginEmailCodeAsync"/>. The answer is the same whether the player exists or not; without
        /// <paramref name="createAccount"/> no letter goes to an email no account has.
        /// </summary>
        /// <param name="language">Language of the letter for a new player; a known player gets the account's.</param>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> RequestLoginCodeAsync(
            string email, bool createAccount = true, LanguageCode? language = null)
        {
            var route = $"{AuthLoginScope()}/email-code/request";
            var dto = new RequestEmailCodeDto { Email = email, CreateAccount = createAccount, Language = language?.ToString() };
            return _restApi.PostAsync<EmailVerificationDto>(route, dto, AuthRequestConfig(noAuth: true));
        }

        /// <summary>
        /// Signs in with the code <see cref="RequestLoginCodeAsync"/> sent. An email no account has creates one with
        /// <paramref name="createAccount"/>, its email confirmed.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LoginEmailCodeAsync(
            string challengeId, string code, bool createAccount = true, string nickname = null)
        {
            var route = $"{AuthLoginScope()}/email-code";
            var dto = new LoginByEmailCodeDto
            {
                ChallengeId = challengeId, Code = code, CreateAccount = createAccount,
                Options = LoginOptionsDto.WithNickname(nickname), Device = _device
            };
            return PostAuthAsync(route, dto, noAuth: true);
        }

        /// <summary>
        /// Sends a new code for any request that sent one (sign-in, registration, link, email change, reset) once
        /// <see cref="EmailVerificationDto.ResendAfterSeconds"/> is over; the old code stops working.
        /// </summary>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> ResendEmailCodeAsync(string challengeId)
        {
            if (string.IsNullOrWhiteSpace(challengeId))
            {
                return AsyncOperation<RestApiResult<EmailVerificationDto>>.CreateCompleted(
                    RestApiResult<EmailVerificationDto>.ValidationFail("Challenge id is empty."));
            }

            var route = $"{SessionScope()}/email-challenges/{Uri.EscapeDataString(challengeId)}/resend";
            return _restApi.PostAsync<EmailVerificationDto>(route, new { }, AuthRequestConfig(noAuth: true));
        }

        /// <summary>
        /// Sends a password reset code to the confirmed email of the account found by email or username; finish with
        /// <see cref="ConfirmPasswordResetAsync"/>. The answer is the same for an unknown player, and then no letter goes.
        /// </summary>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> RequestPasswordResetAsync(string email, string username = null)
        {
            var route = $"{AuthBranchScope()}/password/reset/request";
            var dto = new RequestPasswordResetDto { Email = NullIfBlank(email), Username = NullIfBlank(username) };
            return _restApi.PostAsync<EmailVerificationDto>(route, dto, AuthRequestConfig(noAuth: true));
        }

        /// <summary>Sets a new password with the reset code. Every session of the account ends.</summary>
        public AsyncOperation<RestApiResult> ConfirmPasswordResetAsync(string challengeId, string code, string newPassword)
        {
            var route = $"{AuthBranchScope()}/password/reset/confirm";
            var dto = new ConfirmPasswordResetDto { ChallengeId = challengeId, Code = code, NewPassword = newPassword };
            return _restApi.PostAsync(route, dto, AuthRequestConfig(noAuth: true));
        }

        private static string NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
            dto.Options = LoginOptionsDto.WithNickname(nickname);
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
            dto.Options = LoginOptionsDto.WithNickname(nickname);
            return PostAuthAsync(route, dto, noAuth: true);
        }

        /// <summary>
        /// Begins a browser sign-in and opens its page in the system browser. <paramref name="providerKey"/> is the
        /// <see cref="LoginMethodDto.IntegrationKey"/> of an <c>openid</c>, <c>google</c>, <c>apple</c> or
        /// <c>yandex</c> method from <see cref="GetLoginMethodsAsync"/>. On success the data is the code verifier of
        /// this sign-in: keep it and finish with <see cref="CompleteOpenIdLoginAsync"/> once
        /// <paramref name="successUrl"/> receives <c>mirra_openid_key</c>.
        /// </summary>
        /// <remarks>
        /// <paramref name="successUrl"/> must be one of the SDK's own receivers or an address on the return address list
        /// of the sign-in integration (its page in the console's Integrations); otherwise
        /// <see cref="CloudErrorCodes.PlayerAccountsSuccessUrlNotAllowed"/>.
        /// </remarks>
        public AsyncOperation<RestApiResult<string>> StartOpenIdLoginAsync(string providerKey, string successUrl)
        {
            var op = new AsyncOperation<RestApiResult<string>>();
            var verifier = OpenIdPkce.CreateVerifier();
            var urlOp = BeginOpenIdLoginUrlAsync(providerKey, successUrl, OpenIdPkce.ChallengeOf(verifier));
            OnDone(urlOp, _ =>
            {
                if (!urlOp.Result.IsSuccess)
                {
                    op.Complete(RestApiResult<string>.Fail(urlOp.Result.Error).WithMetaFrom(urlOp.Result));
                    return;
                }

                if (string.IsNullOrWhiteSpace(urlOp.Result.Data))
                {
                    op.Complete(RestApiResult<string>.ValidationFail("OpenId auth url is empty.").WithMetaFrom(urlOp.Result));
                    return;
                }

                Application.OpenURL(urlOp.Result.Data);
                op.Complete(RestApiResult<string>.Success(verifier).WithMetaFrom(urlOp.Result));
            });

            return op;
        }

        /// <summary>
        /// Finishes a browser sign-in or link with the <c>mirra_openid_key</c> the return address received and the
        /// code verifier the sign-in was started with. The key is spent by the first attempt.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> CompleteOpenIdLoginAsync(string openIdKey, string codeVerifier)
        {
            if (string.IsNullOrWhiteSpace(openIdKey))
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId key is empty."));
            }

            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId code verifier is empty."));
            }

            var dto = new RedeemOpenIdResultDto { ResultKey = openIdKey, CodeVerifier = codeVerifier };
            return PostAuthAsync(OPENID_RESULT_ROUTE, dto, noAuth: true);
        }

        /// <summary>
        /// Begins a browser sign-in and returns the provider's page to open (low-level step 1).
        /// <paramref name="providerKey"/> is the <see cref="LoginMethodDto.IntegrationKey"/> of the method;
        /// <paramref name="codeChallenge"/> is <see cref="OpenIdPkce.ChallengeOf"/> of a verifier from
        /// <see cref="OpenIdPkce.CreateVerifier"/>, kept for <see cref="CompleteOpenIdLoginAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<string>> BeginOpenIdLoginUrlAsync(string providerKey, string successUrl, string codeChallenge)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("OpenId provider key is empty."));
            }

            var route = $"{AuthLoginScope()}/openid/{Uri.EscapeDataString(providerKey)}";
            return RequestOpenIdLoginUrlAsync(route, successUrl, codeChallenge, noAuth: true);
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

            return RunOpenIdLoginAsync(
                (successUrl, challenge) => BeginOpenIdLoginUrlAsync(providerKey, successUrl, challenge), options);
        }

        private AsyncOperation<RestApiResult<string>> RequestOpenIdLoginUrlAsync(
            string route, string successUrl, string codeChallenge, bool noAuth)
        {
            if (string.IsNullOrWhiteSpace(successUrl))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("SuccessUrl is empty."));
            }

            if (string.IsNullOrWhiteSpace(codeChallenge))
            {
                return AsyncOperation<RestApiResult<string>>.CreateCompleted(RestApiResult<string>.ValidationFail("OpenId code challenge is empty."));
            }

            var dto = new RegisterOpenIdProviderDto { SuccessUrl = successUrl, CodeChallenge = codeChallenge, Device = _device };
            var config = AuthRequestConfig(noAuth);
            config.RedirectLimit = 0;
            config.AllowedHttpStatusCodes = new long[] { 301, 302, 303, 307, 308 };

            return _restApi.PostAsync<string>(route, dto, config, ExtractRedirectLocation);
        }

        private AsyncOperation<RestApiResult<GetAuthDataDto>> RunOpenIdLoginAsync(
            Func<string, string, AsyncOperation<RestApiResult<string>>> beginLoginUrlAsync, OpenIdLoginOptions options)
        {
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();

            if (!OpenIdCallbackReceiverFactory.TryCreate(options, _webView, out var receiver, out var receiverError))
            {
                op.Complete(RestApiResult<GetAuthDataDto>.ValidationFail(receiverError));
                return op;
            }

            var verifier = OpenIdPkce.CreateVerifier();
            var beginOp = beginLoginUrlAsync(receiver.SuccessUrl, OpenIdPkce.ChallengeOf(verifier));
            OnDone(beginOp, _ =>
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
                OnDone(waitOp, _ =>
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

                    OnDone(CompleteOpenIdLoginAsync(callbackResult.Key, verifier), op.Complete);
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
            var dto = new LoginAsGuestDto { CreateAccount = createAccount, Device = _device };
            return PostWithSavedGuestId(dto, () => PostAuthAsync(route, dto));
        }

        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkDeviceAsync(string deviceId, bool createAccount = false)
        {
            var route = $"{LinkScope()}/device";
            var dto = new LoginByDeviceIdDto { DeviceId = deviceId, CreateAccount = createAccount, Device = _device };
            return PostAuthAsync(route, dto);
        }

        /// <summary>
        /// Adds a password to the signed-in account, with the email and/or username it is used with. A new email
        /// becomes the account's; a new username replaces the account's. If the email or username belongs to another
        /// account the answer is a conflict (<see cref="OnAuthConflict"/>). When the platform confirms emails and the
        /// email is not the account's confirmed one, the answer is <see cref="AuthResultStatus.EmailVerificationRequired"/>:
        /// finish with <see cref="ConfirmPasswordLinkAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkPasswordAsync(string email, string username, string password)
        {
            var route = $"{LinkScope()}/password";
            var dto = new LinkPasswordDto { Email = NullIfBlank(email), Username = NullIfBlank(username), Password = password, Device = _device };
            return PostAuthAsync(route, dto);
        }

        /// <summary>Finishes a password link held for email confirmation with the code from the letter.</summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> ConfirmPasswordLinkAsync(string challengeId, string code)
        {
            var route = $"{LinkScope()}/password/confirm";
            var dto = new ConfirmEmailCodeDto { ChallengeId = challengeId, Code = code, Device = _device };
            return PostAuthAsync(route, dto);
        }

        /// <summary>Sends a code to the email whose sign-in by code is being added; finish with <see cref="LinkEmailCodeAsync"/>.</summary>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> RequestEmailCodeLinkAsync(string email)
        {
            var route = $"{LinkScope()}/email-code/request";
            return _restApi.PostAsync<EmailVerificationDto>(route, new EmailDto { Email = email }, AuthRequestConfig(noAuth: false));
        }

        /// <summary>
        /// Adds sign-in by email code with the code from the letter. If the email belongs to another account the
        /// answer is a conflict with a <see cref="GetAuthDataDto.CredentialTicket"/> for <see cref="ResolveLinkConflictAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkEmailCodeAsync(string challengeId, string code)
        {
            var route = $"{LinkScope()}/email-code";
            var dto = new ConfirmEmailCodeDto { ChallengeId = challengeId, Code = code, Device = _device };
            return PostAuthAsync(route, dto);
        }

        /// <summary>
        /// Links an OpenID, or Google / Apple / Yandex ID, sign-in to the signed-in account through the provider's
        /// page, the way <see cref="LoginOpenIdAsync"/> signs in. If the identity already belongs to another account
        /// the answer is a conflict (<see cref="OnAuthConflict"/>) with a <see cref="GetAuthDataDto.CredentialTicket"/>
        /// for <see cref="ResolveLinkConflictAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> LinkOpenIdAsync(string providerKey, OpenIdLoginOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return AsyncOperation<RestApiResult<GetAuthDataDto>>.CreateCompleted(RestApiResult<GetAuthDataDto>.ValidationFail("OpenId provider key is empty."));
            }

            var route = $"{LinkScope()}/openid/{Uri.EscapeDataString(providerKey)}";
            return RunOpenIdLoginAsync(
                (successUrl, challenge) => RequestOpenIdLoginUrlAsync(route, successUrl, challenge, noAuth: false), options);
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

        /// <summary>
        /// Resolves a link conflict by keeping <see cref="LinkAuthProviderDto.TargetAccountId"/> — the signed-in account
        /// or the one the credentials belong to. An OpenID or Google / Apple / Yandex sign-in conflict needs the
        /// conflict's <see cref="GetAuthDataDto.CredentialTicket"/>; when the dto has none, the ticket of the last
        /// conflict is used.
        /// </summary>
        public AsyncOperation<RestApiResult<GetAuthDataDto>> ResolveLinkConflictAsync(LinkAuthProviderDto dto)
        {
            var route = $"{LinkScope()}/conflict/resolve";
            dto.Device ??= _device;
            if (string.IsNullOrEmpty(dto.CredentialTicket) && IsVerifiedIdentityType(dto.ProviderType))
            {
                dto.CredentialTicket = _lastConflictTicket;
                _lastConflictTicket = null;
            }

            return PostAuthAsync(route, dto);
        }

        // OpenId, Google / Apple / Yandex sign-in and the email code: the server holds the proven identity behind a
        // ticket. A password conflict carries the email / username and the password instead.
        private static bool IsVerifiedIdentityType(int providerType) =>
            providerType == AuthProviderTypeOpenId
            || (providerType >= AuthProviderTypeGoogleSignIn && providerType <= AuthProviderTypeYandexSignIn)
            || providerType == AuthProviderTypeEmailCode;

        private const int AuthProviderTypeOpenId = 4;
        private const int AuthProviderTypeGoogleSignIn = 6;
        private const int AuthProviderTypeYandexSignIn = 8;
        private const int AuthProviderTypeEmailCode = 10;

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

        /// <summary>
        /// Removes the password from the account. The email and the username stay on the account; sign-in by email
        /// code, if it has one, keeps working.
        /// </summary>
        public AsyncOperation<RestApiResult> UnlinkPasswordAsync()
            => DeleteAsync($"{UnlinkScope()}/password", new { });

        /// <summary>Removes sign-in by email code from the account; the email stays on it.</summary>
        public AsyncOperation<RestApiResult> UnlinkEmailCodeAsync()
            => DeleteAsync($"{UnlinkScope()}/email-code", new { });

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
            return EndSessionAfter(_restApi.DeleteAsync(route, dto), onlyOnSuccess: true);
        }

        #endregion

        #region Account credentials

        /// <summary>Changes the signed-in player's password; the player's other sessions end.</summary>
        public AsyncOperation<RestApiResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            var route = $"{AccountsScope()}/password";
            var dto = new ChangePasswordDto { CurrentPassword = currentPassword, NewPassword = newPassword };
            return _restApi.PutAsync(route, dto);
        }

        /// <summary>
        /// Sends a code to a new email for the account; the email changes once the code comes back to
        /// <see cref="ConfirmEmailAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> ChangeEmailAsync(string email)
        {
            var route = $"{AccountsScope()}/email";
            var op = new AsyncOperation<RestApiResult<EmailVerificationDto>>();
            OnDone(_restApi.PostAsync<EmailVerificationDto>(route, new EmailDto { Email = email }), result =>
            {
                if (result.IsSuccess && string.IsNullOrEmpty(result.Data?.ChallengeId) == false)
                {
                    _pendingEmails[result.Data.ChallengeId] = email?.Trim();
                }

                op.Complete(result);
            });
            return op;
        }

        /// <summary>Sends a code to the account's current, not yet confirmed email; finish with <see cref="ConfirmEmailAsync"/>.</summary>
        public AsyncOperation<RestApiResult<EmailVerificationDto>> VerifyEmailAsync()
        {
            var route = $"{AccountsScope()}/email/verify";
            return _restApi.PostAsync<EmailVerificationDto>(route, new { });
        }

        /// <summary>Finishes an email change (<see cref="ChangeEmailAsync"/>) or confirmation (<see cref="VerifyEmailAsync"/>) with the code.</summary>
        public AsyncOperation<RestApiResult> ConfirmEmailAsync(string challengeId, string code)
        {
            var route = $"{AccountsScope()}/email/confirm";
            var dto = new ConfirmEmailCodeDto { ChallengeId = challengeId, Code = code };
            var op = new AsyncOperation<RestApiResult>();
            OnDone(_restApi.PostAsync(route, dto), result =>
            {
                if (result.IsSuccess)
                {
                    _pendingEmails.TryGetValue(challengeId ?? string.Empty, out var newEmail);
                    _pendingEmails.Remove(challengeId ?? string.Empty);
                    Raise(OnEmailConfirmed, newEmail);
                }

                op.Complete(result);
            });
            return op;
        }

        /// <summary>
        /// Sets the account's phone number in the international format (<c>+79991234567</c>); empty removes it. Phone
        /// numbers are not confirmed.
        /// </summary>
        public AsyncOperation<RestApiResult> ChangePhoneAsync(string phoneNumber)
        {
            var route = $"{AccountsScope()}/phone";
            var op = new AsyncOperation<RestApiResult>();
            OnDone(_restApi.PatchAsync(route, new UpdatePhoneDto { PhoneNumber = phoneNumber ?? string.Empty }), result =>
            {
                if (result.IsSuccess)
                {
                    Raise(OnPhoneChanged, NullIfBlank(phoneNumber));
                }

                op.Complete(result);
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
            if (string.IsNullOrEmpty(_refreshToken) && _restoresReading > 0)
            {
                // InitializeAsync is still reading the saved token: this refresh is the restore's.
                var waiter = new AsyncOperation<RestApiResult>();
                _restoreWaiters ??= new List<(AsyncOperation<RestApiResult>, bool)>();
                _restoreWaiters.Add((waiter, signOutOnTransientFailure));
                return waiter;
            }

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
            var dto = new RefreshSessionDto { RefreshToken = _refreshToken, Device = _device };

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
                var revokedCode = response.Error.FirstCloudError()?.Code;
                if (revokedCode == CloudErrorCodes.PlayerAccountsAccountBanned)
                {
                    Raise(OnSessionRevoked, revokedCode);
                }

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
            if (response.Data.ProfileSelectionRequired)
            {
                Raise(OnProfileSelectionRequired);
            }

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
            return EndSessionAfter(_restApi.PostAsync(route, dto), onlyOnSuccess: false);
        }

        /// <summary>
        /// The signed-in player's sessions: this one first (<see cref="PlayerSessionInfo.IsCurrent"/>), then the most
        /// recently used, at most 100. Revoke one with <see cref="RevokeSessionAsync"/>.
        /// </summary>
        public AsyncOperation<RestApiResult<List<PlayerSessionInfo>>> GetSessionsAsync(bool activeOnly = true)
        {
            var route = $"{AccountsScope()}/sessions?activeOnly={(activeOnly ? "true" : "false")}";
            return _restApi.GetAsync<List<PlayerSessionInfo>>(route);
        }

        /// <summary>
        /// Signs one of the player's sessions out. For this client's own session it is <see cref="LogoutAsync"/>: the
        /// local session is dropped as well.
        /// </summary>
        public AsyncOperation<RestApiResult> RevokeSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return AsyncOperation<RestApiResult>.CreateCompleted(RestApiResult.ValidationFail("Session id is empty."));
            }

            if (sessionId == _sessionId)
            {
                return LogoutAsync();
            }

            var route = $"{AccountsScope()}/logout";
            return _restApi.PostAsync(route, new LogoutSessionDto { SessionId = sessionId });
        }

        /// <summary>Signs every other device out: revokes all of the player's sessions except this one.</summary>
        public AsyncOperation<RestApiResult> LogoutOtherSessionsAsync()
        {
            var route = $"{AccountsScope()}/logout/others";
            return _restApi.PostAsync(route, new { });
        }

        public AsyncOperation<RestApiResult> LogoutAllAsync()
        {
            var route = $"{AccountsScope()}/logout/all";
            return EndSessionAfter(_restApi.PostAsync(route, new { }), onlyOnSuccess: false);
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
            return CompleteAfterAuthHandled(_restApi.PostAsync<GetAuthDataDto>(route, dto, AuthRequestConfig(noAuth)));
        }

        /// <summary>
        /// The game gets an operation of its own, completed once the SDK has handled the answer. UseCompleted keeps a
        /// single callback, so while the SDK hooked the operation it handed out, a UseCompleted of the game's — or the
        /// SDK's own, as in LoginOpenIdAsync — replaced the handling and the session was never applied. A new session
        /// completes the operation only once it is on disk: a guest id lost to a crash right after sign-in is a lost
        /// account, and a refresh token not kept signs the player out on the next launch.
        /// </summary>
        private AsyncOperation<RestApiResult<GetAuthDataDto>> CompleteAfterAuthHandled(
            AsyncOperation<RestApiResult<GetAuthDataDto>> raw)
        {
            var op = new AsyncOperation<RestApiResult<GetAuthDataDto>>();
            OnDone(raw, result => FinishAuthAsync(result, op));
            return op;
        }

        private async void FinishAuthAsync(RestApiResult<GetAuthDataDto> result, AsyncOperation<RestApiResult<GetAuthDataDto>> op)
        {
            var signedIn = ApplyAuthResult(result);

            if (signedIn != null)
            {
                Raise(OnLogin, signedIn);
                if (signedIn.ProfileSelectionRequired)
                {
                    Raise(OnProfileSelectionRequired);
                }

                await _storage.FlushAsync();
            }

            op.Complete(result);
        }

        /// <summary>
        /// The game's own operation for a call that ends the session: the SDK drops the session before the game hears
        /// back, and a UseCompleted of the game's can no longer skip that and leave a revoked session on disk.
        /// </summary>
        private AsyncOperation<RestApiResult> EndSessionAfter(AsyncOperation<RestApiResult> raw, bool onlyOnSuccess)
        {
            var op = new AsyncOperation<RestApiResult>();
            OnDone(raw, result =>
            {
                if (onlyOnSuccess == false || result.IsSuccess)
                {
                    ClearSessionAndStorage();
                    Raise(OnSessionExpired);
                }

                op.Complete(result);
            });
            return op;
        }

        /// <summary>
        /// Runs <paramref name="then"/> when <paramref name="operation"/> completes, or at once if it already has:
        /// UseCompleted never fires for an operation that is already complete, and a request refused before it was
        /// sent comes back that way.
        /// </summary>
        private static void OnDone<T>(AsyncOperation<T> operation, Action<T> then)
        {
            if (operation.IsDone)
            {
                then(operation.Result);
                return;
            }

            operation.UseCompleted(completed => then(completed.Result));
        }

        // The game's handlers run inside the SDK's own completion: one that throws must not leave the operation
        // hanging, so its exception is logged instead.
        private static void Raise<T>(Action<T> handler, T value)
        {
            try
            {
                handler?.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Raise(Action handler)
        {
            try
            {
                handler?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
                    "MirraCloud > Manager or pass it in MirraCloudOptions.PlatformKey.");
            }

            return config;
        }

        /// <summary>
        /// Applies the answer to a sign-in or link and stores what the next launch needs. Returns the answer when it
        /// brought a new session — for <see cref="OnLogin"/> — and null otherwise.
        /// </summary>
        private GetAuthDataDto ApplyAuthResult(RestApiResult<GetAuthDataDto> result)
        {
            _logger.Log("handle auth");

            if (!result.IsSuccess)
            {
                var cloudError = result.Error.FirstCloudError();
                _logger.Error(cloudError != null
                    ? $"{cloudError.Code} — {cloudError.Message}"
                    : result.Error?.Message ?? "Auth request failed.");
                return null;
            }

            var data = result.Data;
            if (data == null)
            {
                _logger.Error("Empty auth response");
                return null;
            }

            if (data.Status == AuthResultStatus.Conflict)
            {
                if (string.IsNullOrEmpty(data.CredentialTicket) == false)
                {
                    _lastConflictTicket = data.CredentialTicket;
                }

                Raise(OnAuthConflict, data);
                return null;
            }

            if (data.Status == AuthResultStatus.EmailVerificationRequired)
            {
                // Not a failure: the game asks the player for the code from the letter and confirms it.
                _logger.Log("auth held for email confirmation");
                return null;
            }

            if (string.IsNullOrEmpty(data.Token) || data.Session == null)
            {
                _logger.Error("Auth response without token or session");
                return null;
            }

            SetAuthToken(data.Token);
            ApplySession(data.Session);
            IsAuth = true;

            if (string.IsNullOrEmpty(data.GuestId) == false)
            {
                _storage.SaveString(AuthStorageKeys.GuestId, data.GuestId);
            }

            SaveSessionToStorage();
            return data;
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
            _lastConflictTicket = null;
            _pendingEmails.Clear();
            IsAuth = false;
        }

        // The refresh token is all a later launch needs: the session id and its expiry come back with the refresh.
        private void SaveSessionToStorage()
        {
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                _storage.SaveString(AuthStorageKeys.RefreshToken, _refreshToken);
            }
        }

        private void ClearSessionStorage()
        {
            _storage.DeleteKeys(AuthStorageKeys.RefreshToken);
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

        // While InitializeAsync reads the saved token a refresh is still possible: it waits for the token.
        bool ISessionRefresher.CanRefresh => _restoresReading > 0 || string.IsNullOrEmpty(_refreshToken) == false;

        /// <summary>
        /// A signed-in player, or a saved session <see cref="InitializeAsync"/> is still restoring: either way a
        /// request sent now goes out as the player, straight away or once the restore is done.
        /// </summary>
        internal bool HasSession => IsAuth || string.IsNullOrEmpty(_refreshToken) == false;

        AsyncOperation<RestApiResult> ISessionRefresher.RefreshSessionAsync()
        {
            return RefreshSessionAsync();
        }

        #endregion

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}

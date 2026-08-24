using System;
using MirraCloud.Core;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Auth.OpenId;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// VContainer entry point for the official example. Builds the screen/overlay/toast hosts,
    /// gates on auth (AuthScreen ↔ ServicesScreen), and routes provider buttons to the SDK.
    /// Login success navigates via Authentication.OnLogin; OnSessionExpired returns to auth.
    /// </summary>
    public sealed class ShowcaseApp : IStartable
    {
        private readonly IMirraCloudSdk _sdk;
        private readonly UIDocument _document;
        private readonly RemoteImageLoader _images;
        private readonly bool _devForceServices;

        private Nav _nav;
        private Popup _popup;
        private Toasts _toasts;
        private RequestLog _log;
        private ShowcaseContext _ctx;

        // True once we've navigated to the services screen. Guards against re-navigating when
        // Authentication.OnLogin fires again (a successful account link reuses the same
        // auth-complete path and re-raises OnLogin).
        private bool _authed;

        // Set when the player links an account or dismisses the link prompt. Keeps us from
        // re-offering linking for the rest of this app run (per-session, not persisted).
        private bool _linkPromptDismissed;

        public ShowcaseApp(IMirraCloudSdk sdk, UIDocument document, RemoteImageLoader images, ShowcaseOptions options)
        {
            _sdk = sdk;
            _document = document;
            _images = images;
            _devForceServices = options != null && options.DevForceServices;
        }

        public void Start()
        {
            if (!_sdk.IsInitialized)
            {
                _sdk.Initialize();
            }

            var root = _document.rootVisualElement;
            var screen = root.Q<VisualElement>("sc-screen") ?? root;
            screen.Clear();
            screen.AddToClassList("sc-root");

            var navHost = New("sc-nav-host");
            screen.Add(navHost);

            var overlay = New("sc-overlay");
            overlay.pickingMode = PickingMode.Ignore; // children (scrim) still receive clicks
            screen.Add(overlay);

            var toastHost = New("sc-toast-host2");
            toastHost.pickingMode = PickingMode.Ignore;
            screen.Add(toastHost);

            _nav = new Nav(navHost);
            _popup = new Popup(overlay);
            _toasts = new Toasts(toastHost);
            _log = new RequestLog();

            // Built once, right after the hosts exist: every service view is handed this same
            // instance, so the request log is app-wide and dialogs/toasts share one overlay.
            _ctx = new ShowcaseContext(_sdk, _images, _toasts, _popup, _nav, _log);

            _sdk.Authentication.OnLogin += _ => OnLoggedIn();
            _sdk.Authentication.OnSessionExpired += ShowAuth;

            if (_devForceServices || _sdk.Authentication.IsAuth)
            {
                _authed = true;
                ShowServices();
            }
            else
            {
                RestoreSessionThenRoute();
            }
        }

        // Before showing the auth screen, try to restore a previous session from the stored
        // refresh token (AuthenticationService.InitializeAsync). If it succeeds we go straight to
        // the services screen; otherwise (no token, or the token no longer works) we fall back to
        // the auth screen. A successful restore does NOT raise OnLogin, so the link prompt — which
        // is only for fresh logins — is intentionally skipped here.
        private async void RestoreSessionThenRoute()
        {
            ShowSplash();

            var op = _sdk.Authentication.InitializeAsync();
            if (op != null)
            {
                try
                {
                    await op.Task();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Showcase] session restore failed: " + e.Message);
                }
            }

            if (_sdk.Authentication.IsAuth)
            {
                _authed = true;
                ShowServices();
            }
            else
            {
                ShowAuth();
            }
        }

        private void OnLoggedIn()
        {
            if (_authed)
            {
                // OnLogin also fires after a successful account link (shared auth-complete path).
                // We're already on the services screen and the link flow handles its own UI, so
                // don't re-navigate or re-open the link prompt.
                return;
            }

            _authed = true;
            _popup?.Close();
            ShowServices();
            MaybeShowLinkPrompt();
        }

        private void ShowSplash()
        {
            var splash = new VisualElement();
            splash.AddToClassList("sc-auth");

            var inner = new VisualElement();
            inner.AddToClassList("sc-auth__inner");

            var brand = new Label("MirraCloud");
            brand.AddToClassList("sc-auth__brand");
            inner.Add(brand);

            var sub = new Label("Restoring your session…");
            sub.AddToClassList("sc-auth__subtitle");
            inner.Add(sub);

            splash.Add(inner);
            _nav.SetRoot(splash);
        }

        private void ShowAuth()
        {
            _authed = false;

            string project = "", branch = "", platform = "";
            try
            {
                var cfg = MirraCloud.Configuration.Load();
                if (cfg != null)
                {
                    project = cfg.ProjectId;
                    branch = cfg.BranchId;
                    platform = cfg.AnalyticsPlatformId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] config load failed: " + e.Message);
            }

            var auth = new AuthView(_popup, project, branch, platform);
            auth.GuestRequested += () => RunAuth("Guest", _sdk.Authentication.LoginGuestAsync());
            auth.DeviceRequested += () => RunAuth("Device", _sdk.Authentication.LoginDeviceAsync(SystemInfo.deviceUniqueIdentifier));
            auth.EmailLoginRequested += (email, pass) => RunAuth("Email", _sdk.Authentication.LoginEmailAsync(email, pass));
            auth.OpenIdRequested += id =>
            {
                _toasts.Info("Opening provider…");
                RunAuth("Provider", _sdk.Authentication.LoginOpenIdAsync(id, new OpenIdLoginOptions { UseInAppWebView = true }));
            };
            _nav.SetRoot(auth);
        }

        private void ShowServices()
        {
            // The home screen takes the whole context, not just the image loader: it shows the
            // app-wide request log and opens it in the shared popup host.
            var services = new ServicesView(null, _ctx);
            services.ModuleOpened += OpenModule;
            services.LogoutRequested += () => RunAuthVoid("Logout", _sdk.Authentication.LogoutAsync());
            _nav.SetRoot(services);
            LoadProfileHeader(services);
        }

        private async void LoadProfileHeader(ServicesView services)
        {
            var op = _sdk.PlayerAccount.GetAccountAsync();
            if (op == null)
            {
                return;
            }
            await op.Task();
            var r = op.Result;
            _log.Record("Account", r, "var op = sdk.PlayerAccount.GetAccountAsync();\nawait op.Task();");

            // The first call of the session doubles as the reachability probe for the home topbar:
            // if this one round-trips, the backend is up and configured for this project.
            services.SetBackendStatus(r != null && r.IsSuccess, r);

            if (r != null && r.IsSuccess && r.Data != null)
            {
                var a = r.Data;
                services.SetProfile(new ProfileHeader { Nickname = a.Nickname, Username = a.Username, AvatarUrl = a.AvatarUrl });
            }
        }

        private void OpenModule(ServiceMeta m)
        {
            // Resolve a polished per-service view by id; modules without one yet fall back to "coming soon".
            Action back = () => _nav.Back();
            VisualElement view;
            switch (m.Id)
            {
                case "playerAccount":
                    view = new PlayerAccountView(m, back, _ctx);
                    break;
                case "leaderboard":
                    view = new LeaderboardView(m, back, _ctx);
                    break;
                case "economy":
                    view = new EconomyView(m, back, _ctx);
                    break;
                case "friends":
                    view = new FriendsView(m, back, _ctx);
                    break;
                case "assets":
                    view = new AssetsStorageView(m, back, _ctx);
                    break;
                case "chats":
                    view = new ChatsView(m, back, _ctx);
                    break;
                case "tournaments":
                    view = new TournamentsView(m, back, _ctx);
                    break;
                case "challenges":
                    view = new ChallengesView(m, back, _ctx);
                    break;
                case "dailyRewards":
                    view = new DailyRewardsView(m, back, _ctx);
                    break;
                case "groups":
                    view = new GroupsView(m, back, _ctx);
                    break;
                case "remoteConfig":
                    view = new RemoteConfigView(m, back, _ctx);
                    break;
                case "localization":
                    view = new LocalizationView(m, back, _ctx);
                    break;
                case "segments":
                    view = new SegmentsView(m, back, _ctx);
                    break;
                case "entities":
                    view = new EntitiesView(m, back, _ctx);
                    break;
                case "cloudSave":
                    view = new CloudSaveView(m, back, _ctx);
                    break;
                case "purchases":
                    view = new PurchasesView(m, back, _ctx);
                    break;
                case "promoCodes":
                    view = new PromoCodesView(m, back, _ctx);
                    break;
                case "profanity":
                    view = new ProfanityFilterView(m, back, _ctx);
                    break;
                case "cloudCode":
                    view = new CloudCodeView(m, back, _ctx);
                    break;
                case "analytics":
                    view = new AnalyticsView(m, back, _ctx);
                    break;
                case "webview":
                    view = new WebViewView(m, back, _ctx);
                    break;
                case "deployment":
                    view = new DeploymentView(m, back, _ctx);
                    break;
                default:
                    view = new ComingSoonView(m, back);
                    break;
            }
            _nav.Push(view);
        }

        private async void RunAuth<T>(string label, AsyncOperation<RestApiResult<T>> op)
        {
            if (op == null)
            {
                return;
            }
            try
            {
                await op.Task();
            }
            catch (Exception e)
            {
                _toasts.Fail(label + " · " + e.Message);
                return;
            }

            _log.Record(label + " sign-in", op.Result);

            if (_sdk.Authentication.IsAuth)
            {
                // Popup lifecycle is owned by OnLoggedIn: it runs synchronously during OnLogin
                // (fired inside op.Complete, before this await resumes), closing the login form and
                // then opening the link prompt. Closing here would tear that fresh prompt back down.
                _toasts.Ok(label + " · signed in");
                // navigation handled by Authentication.OnLogin
            }
            else
            {
                _toasts.Fail(label + " · " + ErrText(op.Result));
            }
        }

        private async void RunAuthVoid(string label, AsyncOperation<RestApiResult> op)
        {
            if (op == null)
            {
                return;
            }
            await op.Task();
            _log.Record(label, op.Result);
            if (!_sdk.Authentication.IsAuth)
            {
                _toasts.Info("Signed out");
                ShowAuth();
            }
            else
            {
                _toasts.Fail(label + " failed");
            }
        }

        // Offer to link a durable sign-in method right after a fresh login. Skipped once the player
        // links something or dismisses it (per app run). Only credential/device linking is offered —
        // see LinkPromptView for why social providers aren't.
        private void MaybeShowLinkPrompt()
        {
            if (_linkPromptDismissed || _popup == null)
            {
                return;
            }

            var view = new LinkPromptView(ShowcaseAuthConfig.OpenIdProviders.Length > 0);
            view.EmailLinkRequested += (email, pass) => RunLink("Email", _sdk.Authentication.LinkEmailAsync(email, pass));
            view.UsernameLinkRequested += (user, pass) => RunLink("Username", _sdk.Authentication.LinkUsernameAsync(user, pass));
            view.DeviceLinkRequested += () => RunLink("Device", _sdk.Authentication.LinkDeviceAsync(SystemInfo.deviceUniqueIdentifier));
            view.SkipRequested += () =>
            {
                _linkPromptDismissed = true;
                _popup.Close();
            };

            _popup.Open(view, "Link your account", onClose: () => _linkPromptDismissed = true);
        }

        private async void RunLink(string label, AsyncOperation<RestApiResult<GetAuthDataDto>> op)
        {
            if (op == null)
            {
                return;
            }
            try
            {
                await op.Task();
            }
            catch (Exception e)
            {
                _toasts.Fail(label + " · " + e.Message);
                return;
            }

            var r = op.Result;
            _log.Record(label + " link", r);
            if (r != null && r.IsSuccess && r.Data != null && r.Data.Status == AuthResultStatus.Conflict)
            {
                // The provider is already attached to a different account — linking would need a
                // conflict resolution the showcase doesn't build a UI for.
                _toasts.Fail(label + " · already linked to another account");
                return;
            }

            if (r != null && r.IsSuccess)
            {
                _linkPromptDismissed = true;
                _popup?.Close();
                _toasts.Ok(label + " · linked");
            }
            else
            {
                _toasts.Fail(label + " · " + ErrText(r));
            }
        }

        private static string ErrText(RestApiResult r)
        {
            var e = r?.Error;
            if (e == null)
            {
                return "failed";
            }
            return string.IsNullOrEmpty(e.Message) ? e.Type.ToString() : e.Message;
        }

        private static VisualElement New(string cls)
        {
            var e = new VisualElement();
            if (!string.IsNullOrEmpty(cls))
            {
                e.AddToClassList(cls);
            }
            return e;
        }
    }
}

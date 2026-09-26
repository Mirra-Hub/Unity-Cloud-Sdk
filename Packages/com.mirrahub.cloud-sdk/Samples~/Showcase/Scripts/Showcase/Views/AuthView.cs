using System;
using System.Collections.Generic;
using MirraCloud.Core;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Errors;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The dedicated auth screen: brand + the sign-in buttons of this build's platform. The buttons are not
    /// hard-coded: the app layer asks <c>Authentication.GetLoginMethodsAsync()</c> and hands the answer to
    /// <see cref="ShowMethods"/>, so the screen offers exactly what the platform has switched on in the console —
    /// Guest / Device / Email / Username as buttons, OpenID / Google / Apple / Yandex ID as provider tiles (each
    /// opens an in-app WebView). Email and Username open a form popup. Raises intents; the app layer performs the
    /// SDK calls.
    /// </summary>
    public sealed class AuthView : VisualElement
    {
        public event Action GuestRequested;
        public event Action DeviceRequested;
        public event Action<string, string> EmailLoginRequested;
        public event Action<string, string> UsernameLoginRequested;
        /// <summary>The <see cref="LoginMethodDto.IntegrationKey"/> of the provider tile pressed.</summary>
        public event Action<string> OpenIdRequested;
        public event Action RetryRequested;

        private readonly Popup _popup;
        private readonly VisualElement _card;

        public AuthView(Popup popup, string project, string branch, string platform)
        {
            _popup = popup;
            AddToClassList("sc-auth");

            var inner = new VisualElement();
            inner.AddToClassList("sc-auth__inner");

            var brand = new Label("MirraCloud");
            brand.AddToClassList("sc-auth__brand");
            inner.Add(brand);
            var sub = new Label("Sign in to explore the SDK");
            sub.AddToClassList("sc-auth__subtitle");
            inner.Add(sub);

            _card = new VisualElement();
            _card.AddToClassList("sc-auth__card");
            inner.Add(_card);
            ShowLoading();

            var foot = new VisualElement();
            foot.AddToClassList("sc-auth__footer");
            foot.Add(ConfigChip("project", project));
            foot.Add(ConfigChip("branch", branch));
            foot.Add(ConfigChip("platform", platform));
            inner.Add(foot);

            Add(inner);
        }

        public void ShowLoading()
        {
            _card.Clear();
            _card.Add(Status("Loading sign-in methods…", false));
        }

        /// <summary>Draws one control per method, in the order the platform lists them.</summary>
        public void ShowMethods(LoginMethodsDto methods)
        {
            _card.Clear();

            var tiles = new List<VisualElement>();
            var stores = new List<string>();
            var buttons = 0;

            foreach (var method in methods?.Methods ?? new List<LoginMethodDto>())
            {
                if (method == null)
                {
                    continue;
                }

                switch (method.Kind)
                {
                    case LoginMethodKind.Guest:
                        _card.Add(Block("Continue as Guest", buttons++ == 0, () => GuestRequested?.Invoke()));
                        break;
                    case LoginMethodKind.Email:
                        _card.Add(Block("Continue with Email", buttons++ == 0, () => OpenCredentialsPopup("Email", "Email login", EmailLoginRequested)));
                        break;
                    case LoginMethodKind.Username:
                        _card.Add(Block("Continue with Username", buttons++ == 0, () => OpenCredentialsPopup("Username", "Username login", UsernameLoginRequested)));
                        break;
                    case LoginMethodKind.Device:
                        _card.Add(Block("Continue with Device", buttons++ == 0, () => DeviceRequested?.Invoke()));
                        break;
                    case LoginMethodKind.OpenId:
                    case LoginMethodKind.Google:
                    case LoginMethodKind.Apple:
                    case LoginMethodKind.Yandex:
                        if (!string.IsNullOrEmpty(method.IntegrationKey))
                        {
                            var key = method.IntegrationKey;
                            var look = ShowcaseAuthConfig.LookOf(method);
                            tiles.Add(new ProviderTile(look.Label, look.Glyph, look.Accent, () => OpenIdRequested?.Invoke(key)));
                        }
                        break;
                    default:
                        if (method.IsStore)
                        {
                            stores.Add(ShowcaseAuthConfig.StoreName(method.Kind));
                        }
                        break;
                }
            }

            if (tiles.Count > 0)
            {
                if (buttons > 0)
                {
                    _card.Add(Divider());
                }

                var grid = new VisualElement();
                grid.AddToClassList("sc-provider-grid");
                foreach (var tile in tiles)
                {
                    grid.Add(tile);
                }
                _card.Add(grid);
            }

            if (stores.Count > 0)
            {
                _card.Add(Hint(string.Join(", ", stores) + " sign-in needs the store's own SDK on the device " +
                               "(Authentication.LoginPlatformAsync), so this example does not wire it."));
            }

            if (buttons == 0 && tiles.Count == 0)
            {
                _card.Add(Status(stores.Count > 0
                    ? "Nothing this example can sign in with is switched on for this platform."
                    : "No sign-in method is switched on for this platform. Add one in the console (Platforms → sign-in methods).",
                    true));
                _card.Add(Block("Retry", false, () => RetryRequested?.Invoke()));
            }
        }

        /// <summary>The methods could not be loaded — the same refusal every sign-in would get, so say why.</summary>
        public void ShowError(RestApiResult result)
        {
            _card.Clear();
            _card.Add(Status(DescribeFailure(result), true));
            _card.Add(Block("Retry", true, () => RetryRequested?.Invoke()));
        }

        private static string DescribeFailure(RestApiResult result)
        {
            var error = result?.Error;
            var cloud = error.FirstCloudError();
            switch (cloud?.Code)
            {
                case CloudErrorCodes.PlatformsPlatformNotConfigured:
                    return "This project has no platforms yet. Create one in the console (Platforms), then pick it in Tools › Mirra Cloud › Manager.";
                case CloudErrorCodes.PlatformsPlatformKeyRequired:
                    return "This build names no platform. Pick one in Tools › Mirra Cloud › Manager.";
                case CloudErrorCodes.PlatformsPlatformUnknown:
                    return "The project has no platform with this key (keys are case-sensitive). Pick one in Tools › Mirra Cloud › Manager.";
                case CloudErrorCodes.PlatformsPlatformDisabled:
                    return "This platform is switched off in the console.";
            }

            if (cloud != null)
            {
                return cloud.Code + " — " + cloud.Message;
            }

            if (error == null)
            {
                return "Could not load the sign-in methods.";
            }

            return "Could not load the sign-in methods: " + (string.IsNullOrEmpty(error.Message) ? error.Type.ToString() : error.Message);
        }

        private static Button Block(string text, bool primary, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.AddToClassList("sc-btn");
            b.AddToClassList("sc-btn--block");
            if (primary)
            {
                b.AddToClassList("sc-btn--primary");
            }
            return b;
        }

        private void OpenCredentialsPopup(string loginLabel, string title, Action<string, string> submitted)
        {
            var form = new VisualElement();
            form.AddToClassList("sc-form");
            var login = new TextField(loginLabel);
            login.AddToClassList("sc-field");
            var pass = new TextField("Password") { isPasswordField = true };
            pass.AddToClassList("sc-field");
            var submit = Block("Log in", true, () => submitted?.Invoke(login.value, pass.value));
            form.Add(login);
            form.Add(pass);
            form.Add(submit);
            _popup.Open(form, title);
        }

        private static VisualElement Divider()
        {
            var d = new VisualElement();
            d.AddToClassList("sc-auth__divider");
            var l = new Label("or");
            l.AddToClassList("sc-auth__divider-label");
            d.Add(l);
            return d;
        }

        private static Label Status(string text, bool isError)
        {
            var l = new Label(text);
            l.AddToClassList("sc-auth__status");
            if (isError)
            {
                l.AddToClassList("sc-auth__status--error");
            }
            return l;
        }

        private static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("sc-chat-hint");
            return l;
        }

        private static VisualElement ConfigChip(string key, string value)
        {
            var c = new VisualElement();
            c.AddToClassList("sc-config-chip");
            var k = new Label(key);
            k.AddToClassList("sc-config-chip__key");
            bool empty = string.IsNullOrEmpty(value);
            var v = new Label(empty ? "—" : value);
            v.AddToClassList("sc-config-chip__val");
            if (empty)
            {
                v.AddToClassList("sc-config-chip__val--missing");
            }
            c.Add(k);
            c.Add(v);
            return c;
        }
    }
}

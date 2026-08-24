using System;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The dedicated auth screen: brand + provider buttons. Direct methods (Guest/Device/Email)
    /// and OpenID providers (each opens an in-app WebView). Email opens a form popup. Raises
    /// intents; the app layer performs the SDK calls.
    /// </summary>
    public sealed class AuthView : VisualElement
    {
        public event Action GuestRequested;
        public event Action DeviceRequested;
        public event Action<string, string> EmailLoginRequested;
        public event Action<int> OpenIdRequested;

        private readonly Popup _popup;

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

            var card = new VisualElement();
            card.AddToClassList("sc-auth__card");
            card.Add(Block("Continue as Guest", "sc-btn--primary", () => GuestRequested?.Invoke()));
            card.Add(Block("Continue with Email", null, OpenEmailPopup));
            card.Add(Block("Continue with Device", null, () => DeviceRequested?.Invoke()));

            if (ShowcaseAuthConfig.OpenIdProviders.Length > 0)
            {
                card.Add(Divider());
                var grid = new VisualElement();
                grid.AddToClassList("sc-provider-grid");
                foreach (var p in ShowcaseAuthConfig.OpenIdProviders)
                {
                    var prov = p;
                    grid.Add(new ProviderTile(prov.Label, prov.Glyph, prov.Accent, () => OpenIdRequested?.Invoke(prov.ProviderId)));
                }
                card.Add(grid);
            }

            inner.Add(card);

            var foot = new VisualElement();
            foot.AddToClassList("sc-auth__footer");
            foot.Add(ConfigChip("project", project));
            foot.Add(ConfigChip("branch", branch));
            foot.Add(ConfigChip("platform", platform));
            inner.Add(foot);

            Add(inner);
        }

        private Button Block(string text, string extra, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.AddToClassList("sc-btn");
            b.AddToClassList("sc-btn--block");
            if (!string.IsNullOrEmpty(extra))
            {
                b.AddToClassList(extra);
            }
            return b;
        }

        private void OpenEmailPopup()
        {
            var form = new VisualElement();
            form.AddToClassList("sc-form");
            var email = new TextField("Email");
            email.AddToClassList("sc-field");
            var pass = new TextField("Password") { isPasswordField = true };
            pass.AddToClassList("sc-field");
            var submit = new Button(() => EmailLoginRequested?.Invoke(email.value, pass.value)) { text = "Log in" };
            submit.AddToClassList("sc-btn");
            submit.AddToClassList("sc-btn--primary");
            submit.AddToClassList("sc-btn--block");
            form.Add(email);
            form.Add(pass);
            form.Add(submit);
            _popup.Open(form, "Email login");
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

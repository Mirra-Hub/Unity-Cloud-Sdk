using System;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Post-login "link your account" dialog content. Offers the sign-in methods the SDK can link
    /// from a plain client (email / username / device) — each maps to Authentication.Link*Async.
    /// Raises intents; the app layer performs the SDK calls and reports the result.
    ///
    /// Social / OpenID providers are intentionally not offered here: the in-app WebView OpenID flow
    /// on the backend always performs a login (it creates or switches to the provider's own
    /// account), and the /link/openid endpoint needs a provider user id that only a native sign-in
    /// SDK can supply — neither links the current account, so a "Link with Google" button here would
    /// be misleading.
    /// </summary>
    public sealed class LinkPromptView : VisualElement
    {
        public event Action<string, string> EmailLinkRequested;
        public event Action<string, string> UsernameLinkRequested;
        public event Action DeviceLinkRequested;
        public event Action SkipRequested;

        private readonly VisualElement _body;
        private readonly bool _showSocialNote;

        public LinkPromptView(bool showSocialNote)
        {
            _showSocialNote = showSocialNote;
            AddToClassList("sc-form");

            var intro = new Label("You're signed in. Link a sign-in method so you can log back in later — on this or another device.");
            intro.AddToClassList("sc-chat-hint");
            intro.style.marginBottom = 6;
            Add(intro);

            _body = new VisualElement();
            Add(_body);

            ShowChooser();
        }

        private void ShowChooser()
        {
            _body.Clear();

            _body.Add(Block("Link email & password", "sc-btn--primary", ShowEmailForm));
            _body.Add(Block("Link username & password", null, ShowUsernameForm));
            _body.Add(Block("Link this device", null, () => DeviceLinkRequested?.Invoke()));

            if (_showSocialNote)
            {
                var note = new Label("Social sign-in linking (Google, Apple, …) needs a native provider SDK, so it isn't wired in this example.");
                note.AddToClassList("sc-chat-hint");
                note.style.marginTop = 6;
                _body.Add(note);
            }

            var later = Block("Maybe later", null, () => SkipRequested?.Invoke());
            later.style.marginTop = 6;
            _body.Add(later);
        }

        private void ShowEmailForm()
        {
            _body.Clear();

            var email = Field("Email", false);
            var pass = Field("Password", true);
            _body.Add(email);
            _body.Add(pass);
            _body.Add(Block("Link", "sc-btn--primary", () => EmailLinkRequested?.Invoke(email.value, pass.value)));
            _body.Add(BackButton());
        }

        private void ShowUsernameForm()
        {
            _body.Clear();

            var username = Field("Username", false);
            var pass = Field("Password", true);
            _body.Add(username);
            _body.Add(pass);
            _body.Add(Block("Link", "sc-btn--primary", () => UsernameLinkRequested?.Invoke(username.value, pass.value)));
            _body.Add(BackButton());
        }

        private Button BackButton()
        {
            var b = Block("Back", null, ShowChooser);
            b.style.marginTop = 4;
            return b;
        }

        private static TextField Field(string label, bool isPassword)
        {
            var f = new TextField(label) { isPasswordField = isPassword };
            f.AddToClassList("sc-field");
            return f;
        }

        private static Button Block(string text, string extra, Action onClick)
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
    }
}

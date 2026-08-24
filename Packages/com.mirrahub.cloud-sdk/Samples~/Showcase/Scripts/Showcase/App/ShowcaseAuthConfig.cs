using UnityEngine;

namespace MirraCloud.Example.Showcase
{
    /// <summary>One OpenID provider button on the auth screen. ProviderId is the integer the
    /// backend assigns to a configured OpenID provider (used in /openid/{providerId}).</summary>
    public struct OpenIdProvider
    {
        public int ProviderId;
        public string Label;
        public string Glyph;
        public Color Accent;
    }

    /// <summary>
    /// Auth-screen configuration. The SDK has no "list providers" API — fill these with YOUR
    /// project's configured OpenID providers (providerId from the MirraCloud dashboard). Each
    /// signs in through an in-app WebView via LoginOpenIdAsync(providerId, {UseInAppWebView=true})
    /// — no native plugins required. (Standalone/mobile; OpenID is unsupported on WebGL.)
    /// </summary>
    public static class ShowcaseAuthConfig
    {
        public static readonly OpenIdProvider[] OpenIdProviders = Build();

        private static OpenIdProvider[] Build()
        {
            // Sample providers — replace ProviderId values with your real ones.
            return new[]
            {
                P(1, "Google", "G", "#EA4335"),
                P(2, "Apple", "A", "#E6E6EA"),
                P(3, "Yandex", "Я", "#FC3F1D"),
            };
        }

        private static OpenIdProvider P(int id, string label, string glyph, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new OpenIdProvider { ProviderId = id, Label = label, Glyph = glyph, Accent = c };
        }
    }
}

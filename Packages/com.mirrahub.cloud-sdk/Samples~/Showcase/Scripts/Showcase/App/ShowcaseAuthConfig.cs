using MirraCloud.Core.Auth;
using UnityEngine;

namespace MirraCloud.Example.Showcase
{
    /// <summary>How the auth screen draws one provider tile: label, glyph and accent colour.</summary>
    public struct ProviderLook
    {
        public string Label;
        public string Glyph;
        public Color Accent;
    }

    /// <summary>
    /// Auth-screen look. Which buttons the screen shows is not configured here: it asks the SDK
    /// (<c>Authentication.GetLoginMethodsAsync()</c>) what the build's platform offers and draws exactly that.
    /// This only decides how a provider tile looks.
    /// </summary>
    public static class ShowcaseAuthConfig
    {
        /// <summary>
        /// The tile of a method that signs in through the provider's page (<c>LoginOpenIdAsync(IntegrationKey)</c>):
        /// OpenID, Google, Apple or Yandex ID.
        /// </summary>
        public static ProviderLook LookOf(LoginMethodDto method)
        {
            switch (method.Kind)
            {
                case LoginMethodKind.Google:
                    return Look("Google", "G", "#EA4335");
                case LoginMethodKind.Apple:
                    return Look("Apple", "A", "#E6E6EA");
                case LoginMethodKind.Yandex:
                    return Look("Yandex", "Я", "#FC3F1D");
                default:
                    // A project may offer several OpenID providers: the console's display name tells them apart.
                    var label = string.IsNullOrWhiteSpace(method.DisplayName) ? method.IntegrationKey : method.DisplayName;
                    var glyph = string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1).ToUpperInvariant();
                    return Look(label, glyph, "#6366F1");
            }
        }

        /// <summary>The name of a store sign-in, for the note that it needs the store's own SDK.</summary>
        public static string StoreName(LoginMethodKind kind)
        {
            switch (kind)
            {
                case LoginMethodKind.GooglePlay: return "Google Play Games";
                case LoginMethodKind.VkGames: return "VK Games";
                case LoginMethodKind.YandexGames: return "Yandex Games";
                case LoginMethodKind.AppleGameCenter: return "Game Center";
                default: return kind.ToString();
            }
        }

        private static ProviderLook Look(string label, string glyph, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new ProviderLook { Label = label, Glyph = glyph, Accent = c };
        }
    }
}

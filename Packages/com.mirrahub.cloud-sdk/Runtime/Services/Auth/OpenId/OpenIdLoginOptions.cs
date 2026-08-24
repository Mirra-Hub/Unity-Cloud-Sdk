using System;

namespace MirraCloud.Core.Auth.OpenId
{
    [Serializable]
    public sealed class OpenIdLoginOptions
    {
        public const string DefaultWebViewCallbackUrl = "https://mirra-openid.local/callback";

        public int LoopbackPort;
        public string MobileDeepLinkUrl;

        public bool UseInAppWebView;
        public string WebViewCallbackUrl = DefaultWebViewCallbackUrl;
    }
}

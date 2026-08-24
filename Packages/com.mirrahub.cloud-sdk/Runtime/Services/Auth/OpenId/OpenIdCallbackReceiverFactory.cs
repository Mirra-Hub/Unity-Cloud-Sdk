using MirraCloud.Core.WebView;

namespace MirraCloud.Core.Auth.OpenId
{
    internal static class OpenIdCallbackReceiverFactory
    {
        public static bool TryCreate(OpenIdLoginOptions options, WebViewService webView, out IOpenIdCallbackReceiver receiver, out string error)
        {
            receiver = null;
            error = null;

            var wantsWebView = options != null && options.UseInAppWebView;

            // Use the embedded WebView only when it can actually intercept the redirect to the
            // callback URL. In the Editor under the WebGL build target (and on WebGL in general)
            // the WebView can't hook URLs, so we fall through to the platform default below.
            if (wantsWebView && webView != null && webView.IsReady && webView.SupportsUrlHooking)
            {
                var callbackUrl = string.IsNullOrWhiteSpace(options.WebViewCallbackUrl)
                    ? OpenIdLoginOptions.DefaultWebViewCallbackUrl
                    : options.WebViewCallbackUrl;

                receiver = new WebViewOpenIdReceiver(webView, callbackUrl);
                return true;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            // Editor / desktop: open the auth page in the system browser and catch the redirect
            // on a localhost loopback listener. This also covers the Editor-on-WebGL case where
            // an in-app WebView was requested but isn't available.
            if (LoopbackOpenIdReceiver.TryCreate(options?.LoopbackPort ?? 0, out receiver, out error))
            {
                return true;
            }

            return false;
#elif UNITY_ANDROID || UNITY_IOS
            if (wantsWebView)
            {
                error = "OpenId in-app WebView mode requires an initialized WebViewService.";
                return false;
            }

            var deepLinkUrl = options?.MobileDeepLinkUrl;
            if (string.IsNullOrWhiteSpace(deepLinkUrl))
            {
                error = "OpenId login on mobile requires OpenIdLoginOptions.MobileDeepLinkUrl (e.g. myapp://mirra-openid).";
                return false;
            }

            receiver = new DeepLinkOpenIdReceiver(deepLinkUrl);
            return true;
#else
            error = wantsWebView
                ? "OpenId in-app WebView is not available on this platform."
                : "OpenId login receiver is not supported on this platform yet.";
            return false;
#endif
        }
    }
}

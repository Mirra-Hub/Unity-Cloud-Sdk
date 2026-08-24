using System;
using UnityEngine;

namespace MirraCloud.Core.WebView
{
    internal sealed class WebViewBridge : MonoBehaviour
    {
        private WebViewObject _webViewObject;
        private bool _browserFallback;

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;
        public event Action<string> OnHttpError;
        public event Action<string> OnPageStarted;
        public event Action<string> OnPageLoaded;
        public event Action<string> OnUrlHooked;

        public bool IsReady { get; private set; }

        // Whether the live webview can intercept ("hook") redirect URLs. The OpenID and
        // Purchases flows wait for a redirect to a callback URL, so they require this. The
        // system-browser fallback and the WebGL backend cannot hook URLs.
        public bool SupportsUrlHooking { get; private set; }

        // True when there is no embedded webview and URLs are opened in the system browser
        // instead (the Editor running with the WebGL build target).
        public bool IsBrowserFallback => _browserFallback;

        public static WebViewBridge CreateInstance()
        {
            // Name must contain no spaces: on WebGL the gree backend uses it verbatim as the
            // iframe's DOM id and then looks it up with a jQuery id selector ("#webview_" + name).
            // A space would be parsed as a descendant combinator, the lookup would return nothing,
            // and LoadURL would throw on the missing contentWindow.
            var go = new GameObject("MirraCloudSDKWebView");
            DontDestroyOnLoad(go);
            return go.AddComponent<WebViewBridge>();
        }

        public void Init()
        {
#if UNITY_WEBGL && UNITY_EDITOR
            // The embedded gree webview is a no-op in the Editor under the WebGL build target
            // (its JS backend needs a real browser document). Fall back to opening URLs in the
            // system browser so WebView-based features stay usable while developing for Web.
            _browserFallback = true;
            SupportsUrlHooking = false;
            IsReady = true;
#else
            _webViewObject = gameObject.AddComponent<WebViewObject>();
            _webViewObject.Init(
                cb: msg => OnMessageReceived?.Invoke(msg),
                err: err => OnError?.Invoke(err),
                httpErr: err => OnHttpError?.Invoke(err),
                started: url => OnPageStarted?.Invoke(url),
                ld: url => OnPageLoaded?.Invoke(url),
                hooked: url => OnUrlHooked?.Invoke(url),
                enableWKWebView: true
            );
            _webViewObject.SetVisibility(false);
#if UNITY_WEBGL || UNITY_EDITOR_LINUX || UNITY_SERVER
            // The WebGL backend can't intercept redirect URLs (SetURLPattern is unsupported).
            SupportsUrlHooking = false;
#else
            SupportsUrlHooking = true;
#endif
            IsReady = true;
#endif
        }

        public void LoadUrl(string url)
        {
            if (_browserFallback)
            {
                if (string.IsNullOrEmpty(url) || url == "about:blank") return;
                OnPageStarted?.Invoke(url);
                Application.OpenURL(url);
                OnPageLoaded?.Invoke(url);
                return;
            }
            _webViewObject.LoadURL(url);
        }

        public void LoadHtml(string html, string baseUrl)
        {
            // No browser surface for raw HTML in the Editor fallback.
            if (_browserFallback) return;
            _webViewObject.LoadHTML(html, baseUrl);
        }

        public void SetUrlPattern(string allowPattern, string denyPattern, string hookPattern)
        {
            if (_browserFallback) return;
            _webViewObject.SetURLPattern(allowPattern, denyPattern, hookPattern);
        }

        public void EvaluateJS(string script)
        {
            if (_browserFallback) return;
            _webViewObject.EvaluateJS(script);
        }

        public void SetVisibility(bool visible)
        {
            if (_browserFallback) return;
            _webViewObject.SetVisibility(visible);
        }

        public void SetMargins(int left, int top, int right, int bottom)
        {
            if (_browserFallback) return;
            _webViewObject.SetMargins(left, top, right, bottom);
        }

        public void GoBack()
        {
            if (_browserFallback) return;
            _webViewObject.GoBack();
        }

        public void GoForward()
        {
            if (_browserFallback) return;
            _webViewObject.GoForward();
        }

        public bool CanGoBack()
        {
            if (_browserFallback) return false;
            return _webViewObject.CanGoBack();
        }

        public bool CanGoForward()
        {
            if (_browserFallback) return false;
            return _webViewObject.CanGoForward();
        }

        private void OnDestroy()
        {
            if (_webViewObject != null)
                Destroy(_webViewObject);
        }
    }
}

using System;
using MirraCloud.Core.WebView.Dispatching;
using MirraCloud.Core.WebView.Protocol;
using MirraCloud.Core.WebView.Utils;
using Plugins.MirraCloud.Core.General.LifeCycle;
using UnityEngine;

namespace MirraCloud.Core.WebView
{
    public sealed class WebViewService : ICloudSdkService
    {
        private WebViewBridge _bridge;
        private readonly WebViewCallbackDispatcher _dispatcher = new WebViewCallbackDispatcher();

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;
        public event Action<string> OnHttpError;
        public event Action<string> OnPageStarted;
        public event Action<string> OnPageLoaded;
        public event Action<string> OnUrlHooked;

        public bool IsReady => _bridge != null && _bridge.IsReady;

        /// <summary>
        /// Whether the active WebView can intercept ("hook") redirect URLs. The OpenID and
        /// Purchases flows rely on this to detect their callback URL. False under the system-browser
        /// fallback (Editor + WebGL target) and on WebGL builds.
        /// </summary>
        public bool SupportsUrlHooking => _bridge != null && _bridge.SupportsUrlHooking;

        /// <summary>
        /// True when there is no embedded WebView and <see cref="LoadUrl"/> opens the URL in the
        /// system browser instead — the Editor running with the WebGL build target.
        /// </summary>
        public bool IsBrowserFallback => _bridge != null && _bridge.IsBrowserFallback;

        public void CloudSdkInitialize()
        {
            _bridge = WebViewBridge.CreateInstance();
            _bridge.Init();

            _bridge.OnMessageReceived += HandleMessageReceived;
            _bridge.OnError += HandleError;
            _bridge.OnHttpError += HandleHttpError;
            _bridge.OnPageStarted += HandlePageStarted;
            _bridge.OnPageLoaded += HandlePageLoaded;
            _bridge.OnUrlHooked += HandleUrlHooked;
        }

        public void CloudSdkDispose()
        {
            if (_bridge != null)
            {
                _bridge.OnMessageReceived -= HandleMessageReceived;
                _bridge.OnError -= HandleError;
                _bridge.OnHttpError -= HandleHttpError;
                _bridge.OnPageStarted -= HandlePageStarted;
                _bridge.OnPageLoaded -= HandlePageLoaded;
                _bridge.OnUrlHooked -= HandleUrlHooked;

                UnityEngine.Object.Destroy(_bridge.gameObject);
                _bridge = null;
            }
        }

        public void LoadUrl(string url)
        {
            if (!IsReady) return;
            _bridge.LoadUrl(url);
        }

        public void LoadHtml(string html, string baseUrl = null)
        {
            if (!IsReady) return;
            _bridge.LoadHtml(html, baseUrl);
        }

        public void SetUrlPattern(string allowPattern, string denyPattern, string hookPattern)
        {
            if (!IsReady) return;
            _bridge.SetUrlPattern(allowPattern, denyPattern, hookPattern);
        }

        internal void RegisterCallbackHandler(string urlKey, IWebViewCallbackHandler handler)
        {
            _dispatcher.Register(urlKey, handler);
        }

        internal void UnregisterCallbackHandler(string urlKey)
        {
            _dispatcher.Unregister(urlKey);
        }

        internal void ClearCallbackHandlers()
        {
            _dispatcher.Clear();
            SetUrlPattern(null, null, null);
        }

        internal void ActivateHookPattern()
        {
            SetUrlPattern(null, null, _dispatcher.BuildHookRegex());
        }

        public void EvaluateJS(string script)
        {
            if (!IsReady) return;
            _bridge.EvaluateJS(script);
        }

        public void SetVisibility(bool visible)
        {
            if (!IsReady) return;
            _bridge.SetVisibility(visible);
        }

        public void SetMargins(int left, int top, int right, int bottom)
        {
            if (!IsReady) return;
            _bridge.SetMargins(left, top, right, bottom);
        }

        public void GoBack()
        {
            if (!IsReady) return;
            _bridge.GoBack();
        }

        public void GoForward()
        {
            if (!IsReady) return;
            _bridge.GoForward();
        }

        public bool CanGoBack()
        {
            if (!IsReady) return false;
            return _bridge.CanGoBack();
        }

        public bool CanGoForward()
        {
            if (!IsReady) return false;
            return _bridge.CanGoForward();
        }

        private void HandleMessageReceived(string msg) => OnMessageReceived?.Invoke(msg);
        private void HandleError(string err) => OnError?.Invoke(err);
        private void HandleHttpError(string err) => OnHttpError?.Invoke(err);

        private void HandlePageStarted(string url)
        {
            _dispatcher.Dispatch(CallbackUrlParser.BuildEnvelope(url, WebViewCallbackSource.PageStarted));
            OnPageStarted?.Invoke(url);
        }

        private void HandlePageLoaded(string url) => OnPageLoaded?.Invoke(url);

        private void HandleUrlHooked(string url)
        {
            _dispatcher.Dispatch(CallbackUrlParser.BuildEnvelope(url, WebViewCallbackSource.UrlHooked));
            OnUrlHooked?.Invoke(url);
        }
    }
}

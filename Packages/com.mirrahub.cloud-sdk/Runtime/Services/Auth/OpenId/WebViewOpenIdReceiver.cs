using System;
using MirraCloud.Core.WebView;
using MirraCloud.Core.WebView.Dispatching;
using MirraCloud.Core.WebView.Protocol;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Auth.OpenId
{
    internal sealed class WebViewOpenIdReceiver : IOpenIdCallbackReceiver
    {
        private const string KeyParamName = "mirra_openid_key";
        private const string ErrorParamName = "mirra_openid_error";

        private readonly WebViewService _webView;
        private readonly string _callbackUrl;
        private readonly AsyncOperation<OpenIdCallbackResult> _resultOp = new AsyncOperation<OpenIdCallbackResult>();

        private bool _disposed;
        private bool _completed;

        public string SuccessUrl => _callbackUrl;

        public WebViewOpenIdReceiver(WebViewService webView, string callbackUrl)
        {
            _webView = webView;
            _callbackUrl = callbackUrl;

            _webView.RegisterCallbackHandler(_callbackUrl, new CallbackHandler(CompleteWith));
        }

        public bool LaunchAuthUrl(string authUrl)
        {
            if (_disposed || _completed)
            {
                return false;
            }

            if (string.IsNullOrEmpty(authUrl))
            {
                return false;
            }

            if (!_webView.IsReady)
            {
                return false;
            }

            _webView.ActivateHookPattern();
            _webView.SetVisibility(true);
            _webView.LoadUrl(authUrl);
            return true;
        }

        public AsyncOperation<OpenIdCallbackResult> WaitForCallbackAsync()
        {
            return _resultOp;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try { _webView.ClearCallbackHandlers(); } catch { /* ignored */ }
            try { _webView.SetVisibility(false); } catch { /* ignored */ }

            if (!_completed)
            {
                _completed = true;
                _resultOp.Complete(OpenIdCallbackResult.Empty);
            }
        }

        private void CompleteWith(OpenIdCallbackResult result)
        {
            if (_completed || _disposed)
            {
                return;
            }

            try { _webView.LoadUrl("about:blank"); } catch { /* ignored */ }

            _completed = true;
            _resultOp.Complete(result);
        }

        private sealed class CallbackHandler : IWebViewCallbackHandler
        {
            private readonly Action<OpenIdCallbackResult> _onMatch;

            public CallbackHandler(Action<OpenIdCallbackResult> onMatch)
            {
                _onMatch = onMatch;
            }

            public void Handle(WebViewCallbackEnvelope envelope)
            {
                // Error wins over key — the backend redirects with one or the
                // other, but if both ever appeared we'd want the error to
                // surface so the caller can show a proper message.
                if (envelope.QueryParams.TryGetValue(ErrorParamName, out var error) && !string.IsNullOrWhiteSpace(error))
                {
                    _onMatch(OpenIdCallbackResult.Failure(error));
                    return;
                }

                if (envelope.QueryParams.TryGetValue(KeyParamName, out var key) && !string.IsNullOrWhiteSpace(key))
                {
                    _onMatch(OpenIdCallbackResult.Success(key));
                    return;
                }

                _onMatch(OpenIdCallbackResult.Empty);
            }
        }
    }
}

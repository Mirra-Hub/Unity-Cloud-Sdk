#if UNITY_ANDROID || UNITY_IOS
using System;
using MirraCloud.Core.WebView.Utils;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;

namespace MirraCloud.Core.Auth.OpenId
{
    internal sealed class DeepLinkOpenIdReceiver : IOpenIdCallbackReceiver
    {
        private const string KeyParamName = "mirra_openid_key";
        private const string ErrorParamName = "mirra_openid_error";

        private readonly string _successUrl;
        private readonly AsyncOperation<OpenIdCallbackResult> _resultOp = new AsyncOperation<OpenIdCallbackResult>();
        private bool _isWaiting;

        public string SuccessUrl => _successUrl;

        public DeepLinkOpenIdReceiver(string successUrl)
        {
            _successUrl = successUrl;
        }

        public bool LaunchAuthUrl(string authUrl)
        {
            if (string.IsNullOrEmpty(authUrl))
            {
                return false;
            }

            Application.OpenURL(authUrl);
            return true;
        }

        public AsyncOperation<OpenIdCallbackResult> WaitForCallbackAsync()
        {
            if (_isWaiting)
            {
                return _resultOp;
            }

            _isWaiting = true;
            Application.deepLinkActivated += OnDeepLinkActivated;

            TryCompleteFromUrl(Application.absoluteURL);

            return _resultOp;
        }

        public void Dispose()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }

        private void OnDeepLinkActivated(string url)
        {
            TryCompleteFromUrl(url);
        }

        private void TryCompleteFromUrl(string url)
        {
            var query = CallbackUrlParser.ParseQuery(url);

            if (query.TryGetValue(ErrorParamName, out var error) && !string.IsNullOrWhiteSpace(error))
            {
                Complete(OpenIdCallbackResult.Failure(error));
                return;
            }

            if (query.TryGetValue(KeyParamName, out var key) && !string.IsNullOrWhiteSpace(key))
            {
                Complete(OpenIdCallbackResult.Success(key));
            }
        }

        private void Complete(OpenIdCallbackResult result)
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
            _resultOp.Complete(result);
        }
    }
}
#endif

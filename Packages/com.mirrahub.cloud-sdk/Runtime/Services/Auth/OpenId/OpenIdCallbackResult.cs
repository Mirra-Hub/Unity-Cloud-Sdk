namespace MirraCloud.Core.Auth.OpenId
{
    /// <summary>
    /// Outcome of waiting on an OpenID callback. Either a successful one-time
    /// key (to be exchanged for auth data via <c>/openid/result/{key}</c>) or
    /// an error message forwarded from the backend's <c>?mirra_openid_error</c>
    /// query — used when the provider returns <c>access_denied</c>, the user
    /// cancels in-WebView, or token exchange fails server-side.
    /// </summary>
    internal readonly struct OpenIdCallbackResult
    {
        public string Key { get; }
        public string ErrorMessage { get; }

        public bool IsSuccess => !string.IsNullOrEmpty(Key);

        private OpenIdCallbackResult(string key, string errorMessage)
        {
            Key = key;
            ErrorMessage = errorMessage;
        }

        public static OpenIdCallbackResult Success(string key) => new OpenIdCallbackResult(key, null);
        public static OpenIdCallbackResult Failure(string error) => new OpenIdCallbackResult(null, error);
        public static OpenIdCallbackResult Empty => new OpenIdCallbackResult(null, null);
    }
}

using System;

namespace MirraCloud.Core.Purchases
{
    public sealed class PurchaseOptions
    {
        // If null — SDK uses its default sentinel URLs. The payment provider redirects
        // the user there; the WebView intercepts the URL and closes. The backend does not
        // process these URLs — completion is driven by provider webhooks.
        public string SuccessRedirectUrl;
        public string CancelRedirectUrl;

        // How long to poll GET /orders/{orderId} after the WebView closes, waiting for
        // the provider webhook to be processed on the backend.
        public TimeSpan StatusPollTimeout = TimeSpan.FromSeconds(30);
        public TimeSpan StatusPollInterval = TimeSpan.FromSeconds(2);
    }
}

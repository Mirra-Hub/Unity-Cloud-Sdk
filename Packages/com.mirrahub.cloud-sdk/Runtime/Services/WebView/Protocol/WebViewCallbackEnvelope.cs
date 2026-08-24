using System.Collections.Generic;

namespace MirraCloud.Core.WebView.Protocol
{
    internal sealed class WebViewCallbackEnvelope
    {
        public string RawUrl { get; }
        public string NormalizedBase { get; }
        public IReadOnlyDictionary<string, string> QueryParams { get; }
        public WebViewCallbackSource Source { get; }

        public WebViewCallbackEnvelope(
            string rawUrl,
            string normalizedBase,
            IReadOnlyDictionary<string, string> queryParams,
            WebViewCallbackSource source)
        {
            RawUrl = rawUrl;
            NormalizedBase = normalizedBase;
            QueryParams = queryParams;
            Source = source;
        }
    }
}

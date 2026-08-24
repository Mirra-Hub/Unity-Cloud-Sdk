using MirraCloud.Core.WebView.Protocol;

namespace MirraCloud.Core.WebView.Dispatching
{
    internal interface IWebViewCallbackHandler
    {
        void Handle(WebViewCallbackEnvelope envelope);
    }
}

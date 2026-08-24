using System;
using MirraCloud.Core.WebView;
using MirraCloud.Core.WebView.Dispatching;
using MirraCloud.Core.WebView.Protocol;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Purchases.WebView
{
    internal sealed class WebViewPurchaseReceiver : IPurchaseCallbackReceiver
    {
        private readonly WebViewService _webView;
        private readonly string _successUrl;
        private readonly string _cancelUrl;
        private readonly AsyncOperation<PaymentFlowOutcome> _outcomeOp = new AsyncOperation<PaymentFlowOutcome>();

        private bool _disposed;
        private bool _completed;

        public WebViewPurchaseReceiver(WebViewService webView, string successUrl, string cancelUrl)
        {
            _webView = webView;
            _successUrl = successUrl;
            _cancelUrl = cancelUrl;

            _webView.RegisterCallbackHandler(_successUrl, new OutcomeHandler(PaymentFlowOutcome.Success, CompleteWith));
            _webView.RegisterCallbackHandler(_cancelUrl, new OutcomeHandler(PaymentFlowOutcome.Cancelled, CompleteWith));
        }

        public bool LaunchPaymentUrl(string paymentUrl)
        {
            if (_disposed || _completed)
            {
                return false;
            }

            if (string.IsNullOrEmpty(paymentUrl))
            {
                return false;
            }

            if (!_webView.IsReady)
            {
                return false;
            }

            _webView.ActivateHookPattern();
            _webView.SetVisibility(true);
            _webView.LoadUrl(paymentUrl);
            return true;
        }

        public AsyncOperation<PaymentFlowOutcome> WaitForOutcomeAsync()
        {
            return _outcomeOp;
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
                _outcomeOp.Complete(PaymentFlowOutcome.Dismissed);
            }
        }

        private void CompleteWith(PaymentFlowOutcome outcome)
        {
            if (_completed || _disposed)
            {
                return;
            }

            try { _webView.LoadUrl("about:blank"); } catch { /* ignored */ }

            _completed = true;
            _outcomeOp.Complete(outcome);
        }

        private sealed class OutcomeHandler : IWebViewCallbackHandler
        {
            private readonly PaymentFlowOutcome _outcome;
            private readonly Action<PaymentFlowOutcome> _onMatch;

            public OutcomeHandler(PaymentFlowOutcome outcome, Action<PaymentFlowOutcome> onMatch)
            {
                _outcome = outcome;
                _onMatch = onMatch;
            }

            public void Handle(WebViewCallbackEnvelope envelope)
            {
                _onMatch(_outcome);
            }
        }
    }
}

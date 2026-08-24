using System;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Purchases.WebView
{
    internal interface IPurchaseCallbackReceiver : IDisposable
    {
        bool LaunchPaymentUrl(string paymentUrl);
        AsyncOperation<PaymentFlowOutcome> WaitForOutcomeAsync();
    }
}

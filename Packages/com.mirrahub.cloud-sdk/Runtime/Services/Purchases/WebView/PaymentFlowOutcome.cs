namespace MirraCloud.Core.Purchases.WebView
{
    public enum PaymentFlowOutcome
    {
        Success = 0,    // user returned to the success redirect URL
        Cancelled = 1,  // user returned to the cancel redirect URL
        Dismissed = 2   // WebView was disposed before any redirect was intercepted
    }
}

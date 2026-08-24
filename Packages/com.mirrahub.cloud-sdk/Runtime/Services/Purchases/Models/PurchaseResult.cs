using MirraCloud.Core.Purchases.Dto;

namespace MirraCloud.Core.Purchases.Models
{
    public enum PurchaseResultStatus
    {
        Completed = 0,              // consumable / non-consumable: order is RewardsGranted
        Pending = 1,                // payment initiated but webhook not yet processed (polling timeout)
        Cancelled = 2,              // user returned via cancelRedirectUrl or closed the WebView
        Failed = 3,                 // validation / network / backend / provider error
        SubscriptionActivated = 4   // subscription purchase succeeded
    }

    public sealed class PurchaseResult
    {
        public PurchaseResultStatus Status;
        public string OperationId;
        public PlayerOrderDto Order;
        public PlayerSubscriptionDto Subscription;
        public string Error;

        public static PurchaseResult Completed(PlayerOrderDto order) => new PurchaseResult
        {
            Status = PurchaseResultStatus.Completed,
            OperationId = order?.OrderId,
            Order = order
        };

        public static PurchaseResult Pending(string operationId, PlayerOrderDto order) => new PurchaseResult
        {
            Status = PurchaseResultStatus.Pending,
            OperationId = operationId,
            Order = order
        };

        public static PurchaseResult Cancelled(string operationId) => new PurchaseResult
        {
            Status = PurchaseResultStatus.Cancelled,
            OperationId = operationId
        };

        public static PurchaseResult Failed(string error, string operationId = null) => new PurchaseResult
        {
            Status = PurchaseResultStatus.Failed,
            OperationId = operationId,
            Error = error
        };

        public static PurchaseResult SubscriptionActivated(string operationId, PlayerSubscriptionDto subscription) => new PurchaseResult
        {
            Status = PurchaseResultStatus.SubscriptionActivated,
            OperationId = operationId,
            Subscription = subscription
        };
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MirraCloud.Core.Errors;
using MirraCloud.Core.Purchases.Dto;
using MirraCloud.Core.Purchases.Models;
using MirraCloud.Core.Purchases.WebView;
using MirraCloud.Core.WebView;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Purchases
{
    public sealed class PurchasesService : ICloudSdkService
    {
        private const string ControllerApi = "/purchases/v1/projects";
        private const string DefaultSuccessUrl = "https://mirra-purchases.local/success";
        private const string DefaultCancelUrl = "https://mirra-purchases.local/cancel";

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly WebViewService _webView;
        private readonly ICoroutineRunner _coroutineRunner;

        public event Action<PlayerOrderDto> OnPurchaseCompleted;
        public event Action<string> OnPurchaseCancelled;
        public event Action<PurchaseResult> OnPurchaseFailed;

        public PurchasesService(
            Configuration configuration,
            ILogger logger,
            RestApiClient restApi,
            WebViewService webView,
            ICoroutineRunner coroutineRunner)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
            _webView = webView;
            _coroutineRunner = coroutineRunner;
        }

        private string BasePath => $"{ControllerApi}/{_configuration.ProjectId}/branches/{_configuration.BranchId}";

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }

        // ----------------------------------------------------------------
        // Low-level API — thin wrappers over backend endpoints.
        // ----------------------------------------------------------------

        public AsyncOperation<RestApiResult<List<CatalogItemDto>>> LoadCatalogAsync()
        {
            var route = $"{BasePath}/catalog";
            return _restApi.GetAsync<List<CatalogItemDto>>(route);
        }

        public AsyncOperation<RestApiResult<List<PlayerOrderDto>>> GetOrdersAsync()
        {
            var route = $"{BasePath}/orders";
            return _restApi.GetAsync<List<PlayerOrderDto>>(route);
        }

        public AsyncOperation<RestApiResult<PlayerOrderDto>> GetOrderAsync(string orderId)
        {
            var route = $"{BasePath}/orders/{orderId}";
            return _restApi.GetAsync<PlayerOrderDto>(route);
        }

        public AsyncOperation<RestApiResult<List<PlayerSubscriptionDto>>> GetSubscriptionsAsync()
        {
            var route = $"{BasePath}/subscriptions";
            return _restApi.GetAsync<List<PlayerSubscriptionDto>>(route);
        }

        /// <summary>
        /// Creates a Pending order (or subscription) for one product and returns the payment page of the chosen
        /// integration. Nothing is charged yet.
        /// </summary>
        /// <param name="purchaseKey"><see cref="CatalogItemDto.Key"/>.</param>
        /// <param name="integrationKey">
        /// <see cref="CatalogPriceDto.IntegrationKey"/> of one of the product's prices. An integration that is gone,
        /// switched off or cannot take payments is refused with 409
        /// <see cref="CloudErrorCodes.PurchasesPaymentIntegrationUnavailable"/>; store integrations (VK Games,
        /// Google Play) are paid through the store itself and get 422
        /// <see cref="CloudErrorCodes.PurchasesProviderUnsupported"/>.
        /// </param>
        public AsyncOperation<RestApiResult<InitiatePurchaseResponseDto>> InitiatePurchaseAsync(
            string purchaseKey,
            string integrationKey,
            string successRedirectUrl,
            string cancelRedirectUrl)
        {
            var route = $"{BasePath}/orders";
            var dto = new InitiatePurchaseRequestDto
            {
                PurchaseKey = purchaseKey,
                IntegrationKey = integrationKey,
                SuccessRedirectUrl = successRedirectUrl,
                CancelRedirectUrl = cancelRedirectUrl
            };
            return _restApi.PostAsync<InitiatePurchaseResponseDto>(route, dto);
        }

        // ----------------------------------------------------------------
        // High-level BuyAsync — full end-to-end purchase flow via WebView.
        // ----------------------------------------------------------------

        /// <param name="purchaseKey"><see cref="CatalogItemDto.Key"/>.</param>
        /// <param name="integrationKey"><see cref="CatalogPriceDto.IntegrationKey"/> of the price to pay.</param>
        /// <param name="options">Redirect URLs and the order polling window; defaults when null.</param>
        public AsyncOperation<PurchaseResult> BuyAsync(
            string purchaseKey,
            string integrationKey,
            PurchaseOptions options = null)
        {
            var op = new AsyncOperation<PurchaseResult>();

            if (string.IsNullOrWhiteSpace(purchaseKey))
            {
                return FailSync(op, "PurchaseKey is required.");
            }

            if (string.IsNullOrWhiteSpace(integrationKey))
            {
                return FailSync(op, "IntegrationKey is required.");
            }

            options ??= new PurchaseOptions();
            var successUrl = string.IsNullOrWhiteSpace(options.SuccessRedirectUrl) ? DefaultSuccessUrl : options.SuccessRedirectUrl;
            var cancelUrl = string.IsNullOrWhiteSpace(options.CancelRedirectUrl) ? DefaultCancelUrl : options.CancelRedirectUrl;
            var pollTimeout = options.StatusPollTimeout;
            var pollInterval = options.StatusPollInterval;

            if (!_webView.IsReady || !_webView.SupportsUrlHooking)
            {
                // The checkout flow waits for the payment provider to redirect to the success/cancel
                // URL, which needs the WebView to intercept URLs. That isn't available on WebGL or in
                // the Editor under the WebGL build target.
                return FailSync(op, "WebView checkout isn't available on this platform.");
            }

            var receiver = new WebViewPurchaseReceiver(_webView, successUrl, cancelUrl);

            var initiateOp = InitiatePurchaseAsync(purchaseKey, integrationKey, successUrl, cancelUrl);
            initiateOp.UseCompleted(_ =>
            {
                if (!initiateOp.Result.IsSuccess || initiateOp.Result.Data == null)
                {
                    receiver.Dispose();
                    var apiError = initiateOp.Result.Error;
                    CompleteWithFailure(op, Describe(apiError, "Failed to initiate purchase."), apiError: apiError);
                    return;
                }

                var data = initiateOp.Result.Data;

                if (!receiver.LaunchPaymentUrl(data.PaymentUrl))
                {
                    receiver.Dispose();
                    CompleteWithFailure(op, "Failed to open payment URL in WebView.", data.OperationId);
                    return;
                }

                var waitOp = receiver.WaitForOutcomeAsync();
                waitOp.UseCompleted(_ =>
                {
                    receiver.Dispose();

                    if (waitOp.Result == PaymentFlowOutcome.Cancelled || waitOp.Result == PaymentFlowOutcome.Dismissed)
                    {
                        var result = PurchaseResult.Cancelled(data.OperationId);
                        OnPurchaseCancelled?.Invoke(data.OperationId);
                        op.Complete(result);
                        return;
                    }

                    if (data.IsSubscription)
                    {
                        ResolveSubscriptionResultAsync(data.OperationId, op);
                    }
                    else
                    {
                        PollOrderStatusUntilCompleteAsync(data.OperationId, pollTimeout, pollInterval, op);
                    }
                });
            });

            return op;
        }

        // ----------------------------------------------------------------
        // Internals
        // ----------------------------------------------------------------

        private void ResolveSubscriptionResultAsync(string subscriptionId, AsyncOperation<PurchaseResult> op)
        {
            var subsOp = GetSubscriptionsAsync();
            subsOp.UseCompleted(_ =>
            {
                if (!subsOp.Result.IsSuccess || subsOp.Result.Data == null)
                {
                    CompleteWithFailure(op, subsOp.Result.Error?.Message ?? "Failed to load subscriptions.", subscriptionId);
                    return;
                }

                var subscription = subsOp.Result.Data.FirstOrDefault(s => s.SubscriptionId == subscriptionId);
                if (subscription == null)
                {
                    // Webhook may not have activated the subscription yet — treat as pending.
                    op.Complete(new PurchaseResult
                    {
                        Status = PurchaseResultStatus.Pending,
                        OperationId = subscriptionId
                    });
                    return;
                }

                var result = PurchaseResult.SubscriptionActivated(subscriptionId, subscription);
                op.Complete(result);
            });
        }

        private void PollOrderStatusUntilCompleteAsync(
            string orderId,
            TimeSpan timeout,
            TimeSpan interval,
            AsyncOperation<PurchaseResult> op)
        {
            _coroutineRunner.StartCoroutine(PollOrderStatusCoroutine(orderId, timeout, interval, op));
        }

        private IEnumerator PollOrderStatusCoroutine(
            string orderId,
            TimeSpan timeout,
            TimeSpan interval,
            AsyncOperation<PurchaseResult> op)
        {
            var deadline = Time.realtimeSinceStartup + (float)timeout.TotalSeconds;
            var intervalSeconds = (float)interval.TotalSeconds;
            PlayerOrderDto lastOrder = null;

            while (true)
            {
                var getOp = GetOrderAsync(orderId);
                yield return getOp;

                if (getOp.Result.IsSuccess && getOp.Result.Data != null)
                {
                    lastOrder = getOp.Result.Data;
                    var status = lastOrder.Status;

                    if (status == OrderStatus.RewardsGranted)
                    {
                        var completed = PurchaseResult.Completed(lastOrder);
                        OnPurchaseCompleted?.Invoke(lastOrder);
                        op.Complete(completed);
                        yield break;
                    }

                    if (status == OrderStatus.Cancelled || status == OrderStatus.Refunded || status == OrderStatus.Failed)
                    {
                        var failed = PurchaseResult.Failed($"Order finished in status {status}.", orderId);
                        failed.Order = lastOrder;
                        OnPurchaseFailed?.Invoke(failed);
                        op.Complete(failed);
                        yield break;
                    }
                }

                if (Time.realtimeSinceStartup >= deadline)
                {
                    // Webhook has not confirmed the purchase within the timeout.
                    // Return Pending so the game can retry later (order status will eventually settle).
                    op.Complete(PurchaseResult.Pending(orderId, lastOrder));
                    yield break;
                }

                yield return new WaitForSeconds(intervalSeconds);
            }
        }

        private static AsyncOperation<PurchaseResult> FailSync(AsyncOperation<PurchaseResult> op, string error)
        {
            op.Complete(PurchaseResult.Failed(error));
            return op;
        }

        private void CompleteWithFailure(AsyncOperation<PurchaseResult> op, string error, string operationId = null,
            RestApiError apiError = null)
        {
            var result = PurchaseResult.Failed(error, operationId);
            result.ApiError = apiError;
            _logger?.Error($"[Purchases] {error}");
            OnPurchaseFailed?.Invoke(result);
            op.Complete(result);
        }

        /// <summary>
        /// The server's typed refusal (<c>code — message</c>) when there is one; the transport message otherwise, which
        /// for an HTTP error is only the status line.
        /// </summary>
        private static string Describe(RestApiError error, string fallback)
        {
            var cloudError = error.FirstCloudError();
            if (cloudError != null && !string.IsNullOrEmpty(cloudError.Code))
            {
                return string.IsNullOrEmpty(cloudError.Message) ? cloudError.Code : $"{cloudError.Code} — {cloudError.Message}";
            }

            return error?.Message ?? fallback;
        }
    }
}

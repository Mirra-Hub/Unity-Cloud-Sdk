using System;
using System.Collections.Generic;
using System.Threading;
using MirraCloud.Core.Attribution.Dto;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Errors;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Attribution
{
    /// <summary>
    /// Install attribution: the ids third-party SDKs know this install by, recorded on the player's account so the
    /// console can show where a player came from. Today that is Adjust — its device id (the adid) plus the campaign
    /// Adjust attributed the install to.
    ///
    /// <para>
    /// The project needs an enabled Adjust integration (Integrations in the console); without one the server answers
    /// 403 <see cref="CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable"/>. The SDK does not depend on the
    /// Adjust SDK: the game passes on what Adjust hands it.
    /// </para>
    ///
    /// <para>
    /// Two ways to report:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="ReportAdjustAdid"/> / <see cref="ReportAdjustAttribution"/> — call them from Adjust's callbacks,
    ///     at any time and in any order, signed in or not. What they hand over is kept until there is both a player
    ///     session and the adid, then sent once; <see cref="AdjustReportState"/> says where it stands. This is the way
    ///     to use. Safe to call from any thread: a call from outside Unity's main thread is carried over to it.
    ///   </item>
    ///   <item>
    ///     <see cref="LinkAdjustAsync"/> — the bare call. Needs a session and the adid right now; nothing is kept or
    ///     resent. Main thread only, like every other SDK call.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class AttributionService : ICloudSdkService
    {
        private const string ControllerApi = "/players/external/v1/projects";

        private readonly Configuration _configuration;
        private readonly RestApiClient _restApi;
        private readonly AuthenticationService _authentication;
        private readonly AdjustReportQueue _adjustQueue;

        public AttributionService(Configuration configuration, ILogger logger, RestApiClient restApi,
            AuthenticationService authentication)
        {
            _configuration = configuration;
            _restApi = restApi;
            _authentication = authentication;
            // The SDK is created on Unity's main thread: the queue posts hand-overs from other threads there.
            _adjustQueue = new AdjustReportQueue(() => _authentication.IsAuth, SendAdjustReport, logger,
                SynchronizationContext.Current);
            _adjustQueue.Reported += result => OnAdjustReported?.Invoke(result);
        }

        private string BasePath => $"{ControllerApi}/{_configuration.ProjectId}";

        /// <summary>Where the report handed over through <see cref="ReportAdjustAdid"/> / <see cref="ReportAdjustAttribution"/> stands.</summary>
        public AdjustReportState AdjustReportState => _adjustQueue.State;

        /// <summary>The server's answer to the last queued report, or null before the first one went out.</summary>
        public RestApiResult<ExternalIdDto> LastAdjustReport => _adjustQueue.LastResult;

        /// <summary>Raised after every queued report — answered by the server or not delivered.</summary>
        public event Action<RestApiResult<ExternalIdDto>> OnAdjustReported;

        public void CloudSdkInitialize()
        {
            _authentication.OnLogin += HandleSignedIn;
            _authentication.OnSessionRefreshed += _adjustQueue.HandleSessionRefreshed;
        }

        public void CloudSdkDispose()
        {
            _authentication.OnLogin -= HandleSignedIn;
            _authentication.OnSessionRefreshed -= _adjustQueue.HandleSessionRefreshed;
        }

        // ----------------------------------------------------------------
        // Queued reporting
        // ----------------------------------------------------------------

        /// <summary>
        /// Hands over the adid from Adjust (<c>Adjust.GetAdid</c>). Sent as soon as there is a player session, with the
        /// attribution handed over before or after it. The same adid again sends nothing new. Any thread.
        /// </summary>
        public void ReportAdjustAdid(string adid) => _adjustQueue.ReportAdid(adid);

        /// <summary>
        /// Hands over Adjust's attribution (its attribution callback). It may come before the adid — it is kept until
        /// the adid is in. When <see cref="AdjustAttributionDto.Adid"/> is set it counts as
        /// <see cref="ReportAdjustAdid"/> too. The same attribution again sends nothing new; a different one replaces
        /// the previous one as a whole. Any thread.
        /// </summary>
        public void ReportAdjustAttribution(AdjustAttributionDto attribution) => _adjustQueue.ReportAttribution(attribution);

        // ----------------------------------------------------------------
        // Bare calls
        // ----------------------------------------------------------------

        /// <summary>
        /// Records the Adjust adid of this install, and the attribution fields that are set, on the signed-in account.
        /// Needs a player session. Idempotent: a repeat refreshes the last-seen time; a report without attribution
        /// fields keeps the attribution stored earlier, a report with any replaces it as a whole. Sent once: a refusal
        /// or a failure is not resent (a session the server refused is still refreshed and the call repeated, as
        /// everywhere).
        /// </summary>
        /// <remarks>
        /// Refusals: 400 <see cref="CloudErrorCodes.PlayerAccountsExternalIdRequired"/> (no adid), 422
        /// <see cref="CloudErrorCodes.PlayerAccountsExternalFieldInvalid"/>, 403
        /// <see cref="CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable"/> (no enabled Adjust integration in
        /// the project), 409 <see cref="CloudErrorCodes.PlayerAccountsExternalIdConflict"/> (another account of the
        /// project holds this adid — final, resending does not help), 404
        /// <see cref="CloudErrorCodes.PlayerAccountsAccountNotFound"/>.
        /// </remarks>
        public AsyncOperation<RestApiResult<ExternalIdDto>> LinkAdjustAsync(AdjustAttributionDto attribution)
            => SendAdjustReport(attribution);

        /// <summary>Every external id recorded on the signed-in account, most recently reported first.</summary>
        public AsyncOperation<RestApiResult<List<ExternalIdDto>>> GetMyExternalIdsAsync()
            => _restApi.GetAsync<List<ExternalIdDto>>($"{BasePath}/me");

        private void HandleSignedIn(GetAuthDataDto _) => _adjustQueue.HandleSignedIn();

        // No generic resend: a refusal (409, 422, …) would only be refused again, and a report that did not get
        // through goes out again on the queue's next chance or at the game's own timing.
        private AsyncOperation<RestApiResult<ExternalIdDto>> SendAdjustReport(AdjustAttributionDto body)
            => _restApi.PutAsync<ExternalIdDto>($"{BasePath}/adjust", body, new RestRequestConfig { DisableRetry = true });
    }
}

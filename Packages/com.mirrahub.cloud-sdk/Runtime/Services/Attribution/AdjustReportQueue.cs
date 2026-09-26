using System;
using System.Threading;
using MirraCloud.Core.Attribution.Dto;
using MirraCloud.Core.Errors;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core.Attribution
{
    /// <summary>
    /// Holds what the game handed over from Adjust until it can be sent, and sends it once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Adjust hands out the adid and the attribution through separate callbacks, often before the player has a
    /// session and in either order. A report needs both a session and the adid, so everything is kept here until then.
    /// </para>
    /// <para>
    /// Sent at most once per change: new data from the game, or a sign-in (possibly another account — the server's
    /// upsert is idempotent for the same one). A report that did not get through is sent again on the next sign-in,
    /// session refresh or hand-over; there is no timer. A refusal that resending cannot change (400/404/409/422) is
    /// not resent until one of those changes; a project without an enabled Adjust integration (403) gets nothing more
    /// in this run.
    /// </para>
    /// <para>
    /// Everything runs on the thread the queue was created on — Unity's main thread, which the transport needs. A
    /// hand-over from another thread (Adjust SDK v4 raises its Android callbacks on a Java thread) is posted there.
    /// </para>
    /// Kept apart from <see cref="AttributionService"/> so it can be tested without the transport.
    /// </remarks>
    internal sealed class AdjustReportQueue
    {
        private readonly Func<bool> _hasSession;
        private readonly Func<AdjustAttributionDto, AsyncOperation<RestApiResult<ExternalIdDto>>> _send;
        private readonly ILogger _logger;
        private readonly SynchronizationContext _mainThread;
        private readonly int _mainThreadId;

        // The attribution is replaced as a whole, the way the server stores it: a re-attribution must not keep the
        // previous campaign's creative.
        private string _adid;
        private AdjustAttributionDto _attribution;

        // _version grows whenever the server may lack something; _settledVersion is the last one the server answered
        // for good — stored or refused.
        private int _version;
        private int _settledVersion;
        private bool _adjustUnavailable;

        // The request on its way, or null. Only its own, first completion settles it, so a stray second completion
        // cannot end the one that followed it.
        private object _inFlight;

        /// <param name="mainThread">
        /// The context of the thread creating the queue (Unity's main thread); hand-overs from other threads are posted
        /// to it. Null: every call is handled where it is made.
        /// </param>
        internal AdjustReportQueue(
            Func<bool> hasSession,
            Func<AdjustAttributionDto, AsyncOperation<RestApiResult<ExternalIdDto>>> send,
            ILogger logger,
            SynchronizationContext mainThread = null)
        {
            _hasSession = hasSession;
            _send = send;
            _logger = logger;
            _mainThread = mainThread;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        internal AdjustReportState State { get; private set; }

        internal RestApiResult<ExternalIdDto> LastResult { get; private set; }

        internal event Action<RestApiResult<ExternalIdDto>> Reported;

        internal void ReportAdid(string adid)
        {
            if (IsOffMainThread)
            {
                _mainThread.Post(_ => ReportAdid(adid), null);
                return;
            }

            var trimmed = adid?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            if (!string.Equals(trimmed, _adid, StringComparison.Ordinal))
            {
                _adid = trimmed;
                _version++;
            }

            TrySend();
        }

        internal void ReportAttribution(AdjustAttributionDto attribution)
        {
            if (attribution == null)
            {
                return;
            }

            if (IsOffMainThread)
            {
                // Copied here: the caller may reuse its object before the main thread gets to it.
                var copy = CopyAttribution(attribution);
                copy.Adid = attribution.Adid;
                _mainThread.Post(_ => ReportAttribution(copy), null);
                return;
            }

            var changed = false;

            var adid = attribution.Adid?.Trim();
            if (!string.IsNullOrEmpty(adid) && !string.Equals(adid, _adid, StringComparison.Ordinal))
            {
                _adid = adid;
                changed = true;
            }

            if (attribution.HasAttribution)
            {
                var copy = CopyAttribution(attribution);
                if (!SameAttribution(copy, _attribution))
                {
                    _attribution = copy;
                    changed = true;
                }
            }

            if (changed)
            {
                _version++;
            }

            TrySend();
        }

        /// <summary>A sign-in: possibly another account than the one the report went out for.</summary>
        internal void HandleSignedIn()
        {
            _version++;
            TrySend();
        }

        /// <summary>The session restored at launch (it raises no sign-in), or a refreshed one: a chance to retry.</summary>
        internal void HandleSessionRefreshed() => TrySend();

        private bool IsOffMainThread => _mainThread != null && Thread.CurrentThread.ManagedThreadId != _mainThreadId;

        private void TrySend()
        {
            if (_inFlight != null)
            {
                // The completion looks again.
                return;
            }

            if (_adid == null && _attribution == null)
            {
                State = AdjustReportState.Idle;
                return;
            }

            if (_adjustUnavailable)
            {
                State = AdjustReportState.Rejected;
                return;
            }

            if (_version == _settledVersion)
            {
                return;
            }

            if (_adid == null)
            {
                State = AdjustReportState.WaitingForAdid;
                return;
            }

            if (!_hasSession())
            {
                State = AdjustReportState.WaitingForSession;
                return;
            }

            var sentVersion = _version;
            State = AdjustReportState.Sending;

            AsyncOperation<RestApiResult<ExternalIdDto>> op;
            try
            {
                op = _send(Snapshot());
            }
            catch (Exception e)
            {
                // The request did not even start. Left for the next chance like any report that did not get through,
                // instead of staying "on its way" for the rest of the run.
                Settle(RestApiResult<ExternalIdDto>.Fail(
                    RestApiError.Validation($"The report could not be sent: {e.Message}")), sentVersion);
                return;
            }

            _inFlight = op;
            op.UseCompleted(done => HandleReported(op, done.Result, sentVersion));

            // UseCompleted does not fire for an operation that finished before it was hooked.
            if (op.IsDone)
            {
                HandleReported(op, op.Result, sentVersion);
            }
        }

        private void HandleReported(object op, RestApiResult<ExternalIdDto> result, int sentVersion)
        {
            if (!ReferenceEquals(op, _inFlight))
            {
                return;
            }

            _inFlight = null;
            Settle(result, sentVersion);
        }

        private void Settle(RestApiResult<ExternalIdDto> result, int sentVersion)
        {
            LastResult = result;

            if (result != null && (result.IsSuccess || IsStored(result.Error)))
            {
                _settledVersion = sentVersion;
                State = AdjustReportState.Sent;
            }
            else if (result?.Error.HasCode(CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable) == true)
            {
                _adjustUnavailable = true;
                _settledVersion = sentVersion;
                State = AdjustReportState.Rejected;
                _logger?.Log("[Attribution] The project has no enabled Adjust integration, so the adid is not " +
                             "recorded. Add one in the console (Integrations); reports stop until the next launch.");
            }
            else if (IsFinal(result?.Error))
            {
                _settledVersion = sentVersion;
                State = AdjustReportState.Rejected;
                _logger?.Error($"[Attribution] Adjust report refused: {Describe(result?.Error)}");
            }
            else
            {
                State = AdjustReportState.Failed;
                _logger?.Log("[Attribution] Adjust report did not get through; it goes out again on the next " +
                             $"sign-in or session refresh: {Describe(result?.Error)}");
            }

            try
            {
                Reported?.Invoke(result);
            }
            catch (Exception e)
            {
                _logger?.Error($"[Attribution] OnAdjustReported handler failed: {e}");
            }

            if (_version != sentVersion)
            {
                // Something newer came in while this one was in flight.
                TrySend();
            }
        }

        /// <summary>
        /// A 2xx whose body could not be read: the server stored the report all the same, and sending it again would
        /// only be answered the same way.
        /// </summary>
        private static bool IsStored(RestApiError error) =>
            error != null && error.Type == RestApiErrorType.Deserialize &&
            error.HttpStatusCode >= 200 && error.HttpStatusCode < 300;

        /// <summary>
        /// A refusal resending the same report cannot change: invalid data (400/422), another account's adid (409), an
        /// account the session no longer has (404). The rest — no connection, 5xx, a session that could not be
        /// refreshed — is worth another try.
        /// </summary>
        private static bool IsFinal(RestApiError error)
        {
            if (error == null || error.Type != RestApiErrorType.Http || error.HttpStatusCode == null)
            {
                return false;
            }

            var status = error.HttpStatusCode.Value;
            return status == 400 || status == 404 || status == 409 || status == 422;
        }

        private static string Describe(RestApiError error)
        {
            var cloudError = error.FirstCloudError();
            if (cloudError != null && !string.IsNullOrEmpty(cloudError.Code))
            {
                return $"{cloudError.Code} — {cloudError.Message}";
            }

            return error?.Message ?? "no response";
        }

        private AdjustAttributionDto Snapshot()
        {
            var body = _attribution != null ? CopyAttribution(_attribution) : new AdjustAttributionDto();
            body.Adid = _adid;
            return body;
        }

        private static AdjustAttributionDto CopyAttribution(AdjustAttributionDto source) => new AdjustAttributionDto
        {
            TrackerToken = source.TrackerToken,
            TrackerName = source.TrackerName,
            Network = source.Network,
            Campaign = source.Campaign,
            Adgroup = source.Adgroup,
            Creative = source.Creative,
            ClickLabel = source.ClickLabel,
        };

        private static bool SameAttribution(AdjustAttributionDto a, AdjustAttributionDto b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.TrackerToken == b.TrackerToken && a.TrackerName == b.TrackerName && a.Network == b.Network &&
                   a.Campaign == b.Campaign && a.Adgroup == b.Adgroup && a.Creative == b.Creative &&
                   a.ClickLabel == b.ClickLabel;
        }
    }
}

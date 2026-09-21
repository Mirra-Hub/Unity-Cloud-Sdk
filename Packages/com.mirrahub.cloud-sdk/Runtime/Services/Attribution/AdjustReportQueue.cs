using System;
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
    /// Kept apart from <see cref="AttributionService"/> so it can be tested without the transport.
    /// </remarks>
    internal sealed class AdjustReportQueue
    {
        private readonly Func<bool> _hasSession;
        private readonly Func<AdjustAttributionDto, AsyncOperation<RestApiResult<ExternalIdDto>>> _send;
        private readonly ILogger _logger;

        // The attribution is replaced as a whole, the way the server stores it: a re-attribution must not keep the
        // previous campaign's creative.
        private string _adid;
        private AdjustAttributionDto _attribution;

        // _version grows whenever the server may lack something; _settledVersion is the last one the server answered
        // for good — stored or refused.
        private int _version;
        private int _settledVersion;
        private bool _sending;
        private bool _adjustUnavailable;

        internal AdjustReportQueue(
            Func<bool> hasSession,
            Func<AdjustAttributionDto, AsyncOperation<RestApiResult<ExternalIdDto>>> send,
            ILogger logger)
        {
            _hasSession = hasSession;
            _send = send;
            _logger = logger;
        }

        internal AdjustReportState State { get; private set; }

        internal RestApiResult<ExternalIdDto> LastResult { get; private set; }

        internal event Action<RestApiResult<ExternalIdDto>> Reported;

        internal void ReportAdid(string adid)
        {
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

        private void TrySend()
        {
            if (_sending)
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
            _sending = true;
            State = AdjustReportState.Sending;

            var op = _send(Snapshot());
            op.UseCompleted(done => HandleReported(done.Result, sentVersion));

            // UseCompleted does not fire for an operation that finished before it was hooked.
            if (op.IsDone && _sending)
            {
                HandleReported(op.Result, sentVersion);
            }
        }

        private void HandleReported(RestApiResult<ExternalIdDto> result, int sentVersion)
        {
            _sending = false;
            LastResult = result;

            if (result != null && result.IsSuccess)
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

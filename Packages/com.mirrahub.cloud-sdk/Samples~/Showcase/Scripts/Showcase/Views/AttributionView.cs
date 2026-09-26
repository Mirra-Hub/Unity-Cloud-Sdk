using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Attribution;
using MirraCloud.Core.Attribution.Dto;
using MirraCloud.Core.Errors;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Attribution screen: what the account has recorded from install-attribution SDKs (Adjust), the
    /// state of the SDK's queued report, and the two ways to send one.
    /// <para>
    /// The example does not ship the Adjust SDK, so both cards take the values a game would get from
    /// Adjust's callbacks by hand. The queued card shows the part that matters in a real game: the
    /// attribution may arrive before the adid, and neither needs a session to be handed over.
    /// </para>
    /// </summary>
    public sealed class AttributionView : ServiceView
    {
        private const string QueueSnippet =
@"// Hand Adjust's values over as they arrive, in any order and before or after sign-in.
// The SDK keeps them until there is a session and an adid, then sends once.
// (Adjust Unity SDK v5 names.)
var config = new AdjustConfig(appToken, AdjustEnvironment.Production);
config.AttributionChangedDelegate = attribution =>
    sdk.Attribution.ReportAdjustAttribution(new AdjustAttributionDto
    {
        TrackerToken = attribution.TrackerToken,
        TrackerName = attribution.TrackerName,
        Network = attribution.Network,
        Campaign = attribution.Campaign,
        Adgroup = attribution.Adgroup,
        Creative = attribution.Creative,
        ClickLabel = attribution.ClickLabel,
    });
Adjust.InitSdk(config);
Adjust.GetAdid(adid => sdk.Attribution.ReportAdjustAdid(adid));

// Where it stands: Idle | WaitingForAdid | WaitingForSession | Sending | Sent | Failed | Rejected
AdjustReportState state = sdk.Attribution.AdjustReportState;
sdk.Attribution.OnAdjustReported += result => { /* result.IsSuccess, result.Error */ };";

        private const string LinkSnippet =
@"// The bare call: needs a session and the adid now. Nothing is queued or retried.
var op = sdk.Attribution.LinkAdjustAsync(new AdjustAttributionDto
{
    Adid = adid,               // required
    Campaign = ""Summer"",       // attribution fields are optional:
    Network = ""Facebook Installs"", // none set keeps what the server has
});
await op.Task();

if (op.Result.IsSuccess)
{
    ExternalIdDto recorded = op.Result.Data;  // FirstSeenAt, LastSeenAt, Adjust.*
}
else if (op.Result.Error.HasCode(CloudErrorCodes.PlayerAccountsExternalIdConflict))
{
    // 409: another account of the project holds this adid — final, do not resend.
}
else if (op.Result.Error.HasCode(CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable))
{
    // 403: the project has no enabled Adjust integration.
}";

        private const string ReadSnippet =
@"// Every external id recorded on the signed-in account, most recently reported first.
var op = sdk.Attribution.GetMyExternalIdsAsync();
await op.Task();

foreach (ExternalIdDto id in op.Result.Data)
{
    // id.ProviderKey (""adjust""), id.ExternalId (the adid), id.Source (sdk | admin | callback),
    // id.FirstSeenAt, id.LastSeenAt, id.Adjust?.Campaign / Network / TrackerName / …
}";

        private VisualElement _stateSlot;
        private VisualElement _recordedSlot;

        public AttributionView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            DeclareCall(new SdkCall("Report from Adjust's callbacks", QueueSnippet,
                "The way to use it: kept until there is a session and an adid, then sent once."));
            DeclareCall(new SdkCall("Report right now", LinkSnippet,
                "The bare call behind the queue — needs a session and the adid at hand."));
            DeclareCall(new SdkCall("Read what the account has recorded", ReadSnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            Content.Add(new SectionHeader("Recorded on this account"));
            _recordedSlot = AddSlot();
            LoadRecorded();

            Content.Add(new SectionHeader("Queued report"));
            _stateSlot = AddSlot();
            RenderState();
            Content.Add(BuildQueueCard());

            Content.Add(new SectionHeader("Report right now"));
            Content.Add(BuildLinkCard());
        }

        // ----- recorded ids ---------------------------------------------------------------------

        private void LoadRecorded()
        {
            ViewBind.Load(
                () => Sdk.Attribution.GetMyExternalIdsAsync(),
                _recordedSlot,
                RenderRecorded,
                d => d == null || d.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "External ids",
                    Snippet = ReadSnippet,
                    ServiceName = "Attribution",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        SetStatus("Nothing recorded", ChipTone.Neutral);
                        return ZeroState.Table(RecordedColumns(),
                            "No attribution id is recorded on this account yet. A game reports the Adjust "
                            + "adid once per launch; try one of the cards below.", 2);
                    },
                });
        }

        private VisualElement RenderRecorded(List<ExternalIdDto> ids)
        {
            SetStatus(ids.Count + (ids.Count == 1 ? " id" : " ids"), ChipTone.Ok);
            var table = new DataTable(RecordedColumns()).WithZebra().WithMaxHeight(320f);
            table.Bind(ids, null);
            return table;
        }

        private static DataColumn[] RecordedColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "SERVICE", FixedWidth = true, Px = 90,
                    Cell = o => new Badge(Fmt.OrDash(((ExternalIdDto)o).ProviderKey), ChipTone.Info),
                },
                new DataColumn
                {
                    Header = "ID", Grow = 1.6f,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(Fmt.OrDash(((ExternalIdDto)o).ExternalId), 40));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "CAMPAIGN", Grow = 1.2f,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(CampaignOf((ExternalIdDto)o), 34));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "FIRST SEEN", FixedWidth = true, Px = 130,
                    SortKey = o => ((ExternalIdDto)o).FirstSeenAt,
                    Cell = o => new Label(Fmt.DateTime2(((ExternalIdDto)o).FirstSeenAt.ToLocalTime())),
                },
                new DataColumn
                {
                    Header = "LAST SEEN", FixedWidth = true, Px = 130,
                    SortKey = o => ((ExternalIdDto)o).LastSeenAt,
                    Cell = o => new Label(Fmt.DateTime2(((ExternalIdDto)o).LastSeenAt.ToLocalTime())),
                },
            };
        }

        private static string CampaignOf(ExternalIdDto id)
        {
            var adjust = id.Adjust;
            if (adjust == null)
            {
                return Fmt.Dash;
            }
            if (string.IsNullOrEmpty(adjust.Network) && string.IsNullOrEmpty(adjust.Campaign))
            {
                return "not attributed (yet)";
            }
            if (string.IsNullOrEmpty(adjust.Campaign))
            {
                return adjust.Network;
            }
            return string.IsNullOrEmpty(adjust.Network) ? adjust.Campaign : adjust.Network + " · " + adjust.Campaign;
        }

        // ----- queued report --------------------------------------------------------------------

        private void RenderState()
        {
            if (_stateSlot == null)
            {
                return;
            }
            _stateSlot.Clear();

            var service = Sdk.Attribution;
            var card = new Card(Meta.Accent);
            card.WithTitle("Where the queued report stands", Meta.Accent);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(service.AdjustReportState.ToString(), StateTone(service.AdjustReportState)));
            card.Body.Add(chips);

            var what = new Label(StateText(service.AdjustReportState));
            what.AddToClassList("sc-fs-hint");
            card.Body.Add(what);

            var last = service.LastAdjustReport;
            if (last != null && !last.IsSuccess)
            {
                var why = new Label("Last answer: " + Describe(last));
                why.enableRichText = false;
                why.AddToClassList("sc-fs-hint");
                card.Body.Add(why);
            }
            _stateSlot.Add(card);
        }

        private static ChipTone StateTone(AdjustReportState state)
        {
            switch (state)
            {
                case AdjustReportState.Sent: return ChipTone.Ok;
                case AdjustReportState.Failed: return ChipTone.Warn;
                case AdjustReportState.Rejected: return ChipTone.Bad;
                case AdjustReportState.Sending: return ChipTone.Info;
                default: return ChipTone.Neutral;
            }
        }

        private static string StateText(AdjustReportState state)
        {
            switch (state)
            {
                case AdjustReportState.WaitingForAdid:
                    return "Attribution is in, the adid is not. It goes out as soon as the adid is handed over.";
                case AdjustReportState.WaitingForSession:
                    return "Waiting for a player session; it goes out right after the sign-in.";
                case AdjustReportState.Sending:
                    return "On its way.";
                case AdjustReportState.Sent:
                    return "The account has what was handed over. The same values again send nothing.";
                case AdjustReportState.Failed:
                    return "Did not get through; sent again on the next sign-in, session refresh or report.";
                case AdjustReportState.Rejected:
                    return "Refused for good. Not resent until something new is handed over or another sign-in "
                        + "happens — or, without an Adjust integration in the project, until the next launch.";
                default:
                    return "Nothing handed over from Adjust in this run.";
            }
        }

        private VisualElement BuildQueueCard()
        {
            return new ActionCard("Hand over Adjust's values",
                    "What a game calls from Adjust's callbacks. Leave the adid empty to see the report wait "
                    + "for it; leave every attribution field empty to send the adid alone, which keeps the "
                    + "attribution the account already has.", LucideIcon.Waypoints)
                .WithFields(AttributionFields(false))
                .WithSnippet(QueueSnippet)
                .OnRun("Hand over", QueueAction);
        }

        private async Task<ActionOutcome> QueueAction(FormValues values)
        {
            var service = Sdk.Attribution;

            // Waits for the report this click may start, through the same event a game would use.
            var reported = new TaskCompletionSource<RestApiResult<ExternalIdDto>>();
            Action<RestApiResult<ExternalIdDto>> onReported = r => reported.TrySetResult(r);
            service.OnAdjustReported += onReported;
            try
            {
                service.ReportAdjustAttribution(ReadAttribution(values));
                RenderState();

                if (service.AdjustReportState == AdjustReportState.Sending)
                {
                    await reported.Task;
                }
            }
            finally
            {
                service.OnAdjustReported -= onReported;
            }

            if (reported.Task.IsCompleted)
            {
                Ctx.Log?.Record("Attribution · queued report", reported.Task.Result, QueueSnippet);
                if (reported.Task.Result.IsSuccess)
                {
                    LoadRecorded();
                }
            }
            RenderState();

            switch (service.AdjustReportState)
            {
                case AdjustReportState.Sent:
                    return ActionOutcome.Success("Recorded on this account");
                case AdjustReportState.WaitingForAdid:
                    return ActionOutcome.Success("Kept — waiting for the adid");
                case AdjustReportState.WaitingForSession:
                    return ActionOutcome.Success("Kept — waiting for a session");
                case AdjustReportState.Idle:
                    return ActionOutcome.Failure("Nothing to hand over: fill the adid or an attribution field.");
                case AdjustReportState.Rejected:
                case AdjustReportState.Failed:
                    return ActionOutcome.Failure(service.LastAdjustReport != null
                        ? Describe(service.LastAdjustReport)
                        : StateText(service.AdjustReportState));
                default:
                    return ActionOutcome.Success(service.AdjustReportState.ToString());
            }
        }

        // ----- bare call ------------------------------------------------------------------------

        private VisualElement BuildLinkCard()
        {
            return new ActionCard("Record the adid now",
                    "LinkAdjustAsync, without the queue: one PUT with whatever is in the fields. Repeat it "
                    + "and only the last-seen time moves.", LucideIcon.Send)
                .WithFields(AttributionFields(true))
                .WithSnippet(LinkSnippet)
                .OnRun("Record", LinkAction);
        }

        private async Task<ActionOutcome> LinkAction(FormValues values)
        {
            var op = Sdk.Attribution.LinkAdjustAsync(ReadAttribution(values));
            await op.Task();
            var result = op.Result;
            Ctx.Log?.Record("Attribution · link Adjust", result, LinkSnippet);

            if (result == null || !result.IsSuccess)
            {
                return ActionOutcome.Failure(Describe(result));
            }

            LoadRecorded();
            var recorded = result.Data;
            if (Toasts != null)
            {
                Toasts.Ok("Adjust adid recorded");
            }
            return ActionOutcome.Success(recorded == null
                ? "Recorded"
                : "Recorded · first seen " + Fmt.DateTime2(recorded.FirstSeenAt.ToLocalTime()));
        }

        // ----- shared ---------------------------------------------------------------------------

        private static FormField[] AttributionFields(bool adidRequired)
        {
            return new[]
            {
                FormField.Text("adid", "Adid", null, adidRequired)
                    .WithPlaceholder("Adjust.GetAdid — e.g. 3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b"),
                FormField.Text("network", "Network").WithPlaceholder("e.g. Facebook Installs"),
                FormField.Text("campaign", "Campaign").WithPlaceholder("e.g. Summer"),
                FormField.Text("adgroup", "Ad group"),
                FormField.Text("creative", "Creative"),
                FormField.Text("trackerToken", "Tracker token"),
                FormField.Text("trackerName", "Tracker name"),
                FormField.Text("clickLabel", "Click label"),
            };
        }

        private static AdjustAttributionDto ReadAttribution(FormValues values)
        {
            return new AdjustAttributionDto
            {
                Adid = OrNull(values.Text("adid")),
                Network = OrNull(values.Text("network")),
                Campaign = OrNull(values.Text("campaign")),
                Adgroup = OrNull(values.Text("adgroup")),
                Creative = OrNull(values.Text("creative")),
                TrackerToken = OrNull(values.Text("trackerToken")),
                TrackerName = OrNull(values.Text("trackerName")),
                ClickLabel = OrNull(values.Text("clickLabel")),
            };
        }

        private static string OrNull(string typed)
        {
            return string.IsNullOrWhiteSpace(typed) ? null : typed.Trim();
        }

        /// <summary>The refusals this call is built around, in words; anything else as <c>code — message</c>.</summary>
        private static string Describe(RestApiResult result)
        {
            var error = result != null ? result.Error : null;
            var cloud = error.FirstCloudError();
            switch (cloud != null ? cloud.Code : null)
            {
                case CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable:
                    return "The project has no enabled Adjust integration. Add one in the console (Integrations).";
                case CloudErrorCodes.PlayerAccountsExternalIdConflict:
                    return "Another account of this project already holds this adid. That is final — the "
                        + "attribution stays with the first account.";
                case CloudErrorCodes.PlayerAccountsExternalIdRequired:
                    return "The adid is required.";
                case CloudErrorCodes.PlayerAccountsExternalFieldInvalid:
                    return "A value is malformed: the adid has spaces or is longer than 128 characters, or a "
                        + "field is longer than 512.";
                case CloudErrorCodes.PlayerAccountsAccountNotFound:
                    return "The signed-in account is gone.";
            }

            if (cloud != null)
            {
                return string.IsNullOrEmpty(cloud.Message) ? cloud.Code : cloud.Code + " — " + cloud.Message;
            }
            if (error == null)
            {
                return "no response";
            }
            return string.IsNullOrEmpty(error.Message) ? error.Type.ToString() : error.Message;
        }
    }
}

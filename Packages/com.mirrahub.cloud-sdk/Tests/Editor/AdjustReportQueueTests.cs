using System.Collections.Generic;
using MirraCloud.Core;
using MirraCloud.Core.Attribution;
using MirraCloud.Core.Attribution.Dto;
using MirraCloud.Core.Errors;
using NUnit.Framework;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Attribution.Tests
{
    /// <summary>
    /// The queue behind <c>AttributionService.ReportAdjustAdid</c> / <c>ReportAdjustAttribution</c>: Adjust hands the
    /// adid and the attribution over separately, often before sign-in; the report has to wait for both a session and
    /// the adid, go out once, and not hammer the server with what it already refused.
    /// </summary>
    public class AdjustReportQueueTests
    {
        private bool _hasSession;
        private List<Sent> _sent;
        private AdjustReportQueue _queue;

        private sealed class Sent
        {
            public AdjustAttributionDto Body;
            public AsyncOperation<RestApiResult<ExternalIdDto>> Operation;
        }

        [SetUp]
        public void SetUp()
        {
            _hasSession = false;
            _sent = new List<Sent>();
            _queue = new AdjustReportQueue(() => _hasSession, body =>
            {
                var sent = new Sent { Body = body, Operation = new AsyncOperation<RestApiResult<ExternalIdDto>>() };
                _sent.Add(sent);
                return sent.Operation;
            }, null);
        }

        [Test]
        public void Nothing_handed_over_is_idle_and_sends_nothing()
        {
            _hasSession = true;
            _queue.HandleSignedIn();
            _queue.HandleSessionRefreshed();

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Idle));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void Attribution_before_the_adid_waits_for_the_adid_and_goes_out_with_it()
        {
            _hasSession = true;
            _queue.ReportAttribution(new AdjustAttributionDto { Network = "Facebook Installs", Campaign = "Summer" });

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.WaitingForAdid));
            Assert.That(_sent, Is.Empty);

            _queue.ReportAdid(" adid-1 ");

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].Body.Adid, Is.EqualTo("adid-1"));
            Assert.That(_sent[0].Body.Network, Is.EqualTo("Facebook Installs"));
            Assert.That(_sent[0].Body.Campaign, Is.EqualTo("Summer"));
            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Sending));
        }

        [Test]
        public void Without_a_session_the_report_waits_for_the_sign_in()
        {
            _queue.ReportAdid("adid-1");

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.WaitingForSession));
            Assert.That(_sent, Is.Empty);

            _hasSession = true;
            _queue.HandleSignedIn();

            Assert.That(_sent, Has.Count.EqualTo(1));
        }

        [Test]
        public void A_session_restored_at_launch_sends_the_waiting_report()
        {
            _queue.ReportAdid("adid-1");
            _hasSession = true;
            _queue.HandleSessionRefreshed();

            Assert.That(_sent, Has.Count.EqualTo(1));
        }

        [Test]
        public void A_stored_report_is_not_sent_again_for_the_same_data()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Ok());

            _queue.ReportAdid("adid-1");
            _queue.ReportAttribution(new AdjustAttributionDto { Adid = "adid-1" });
            _queue.HandleSessionRefreshed();

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Sent));
            Assert.That(_sent, Has.Count.EqualTo(1));
        }

        [Test]
        public void A_sign_in_sends_the_report_again_for_the_account_now_signed_in()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Ok());

            _queue.HandleSignedIn();

            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[1].Body.Adid, Is.EqualTo("adid-1"));
        }

        [Test]
        public void An_adid_only_report_after_the_attribution_still_carries_the_attribution()
        {
            _hasSession = true;
            _queue.ReportAttribution(new AdjustAttributionDto { Adid = "adid-1", Campaign = "Summer" });
            Complete(0, Ok());

            _queue.ReportAdid("adid-2");

            Assert.That(_sent[1].Body.Adid, Is.EqualTo("adid-2"));
            Assert.That(_sent[1].Body.Campaign, Is.EqualTo("Summer"));
        }

        [Test]
        public void A_new_attribution_replaces_the_previous_one_as_a_whole()
        {
            _hasSession = true;
            _queue.ReportAttribution(new AdjustAttributionDto { Adid = "adid-1", Campaign = "Summer", Creative = "Banner" });
            Complete(0, Ok());

            _queue.ReportAttribution(new AdjustAttributionDto { Campaign = "Winter" });

            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[1].Body.Campaign, Is.EqualTo("Winter"));
            Assert.That(_sent[1].Body.Creative, Is.Null);
        }

        [Test]
        public void Data_handed_over_while_a_report_is_in_flight_goes_out_after_it()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            _queue.ReportAttribution(new AdjustAttributionDto { Campaign = "Summer" });

            Assert.That(_sent, Has.Count.EqualTo(1), "one request at a time");

            Complete(0, Ok());

            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[1].Body.Campaign, Is.EqualTo("Summer"));
        }

        [Test]
        public void A_report_that_did_not_get_through_goes_out_again_on_the_next_session_refresh()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Http(503, null));

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Failed));
            Assert.That(_sent, Has.Count.EqualTo(1), "no retry loop");

            _queue.HandleSessionRefreshed();

            Assert.That(_sent, Has.Count.EqualTo(2));
        }

        [Test]
        public void A_network_failure_is_retried_too()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, RestApiResult<ExternalIdDto>.Fail(new RestApiError { Type = RestApiErrorType.Network, Message = "offline" }));

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Failed));

            _queue.HandleSessionRefreshed();

            Assert.That(_sent, Has.Count.EqualTo(2));
        }

        [Test]
        public void Another_accounts_adid_is_final_until_the_next_sign_in()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Http(409, CloudErrorCodes.PlayerAccountsExternalIdConflict));

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Rejected));

            _queue.HandleSessionRefreshed();
            _queue.ReportAdid("adid-1");

            Assert.That(_sent, Has.Count.EqualTo(1));

            _queue.HandleSignedIn();

            Assert.That(_sent, Has.Count.EqualTo(2), "another sign-in may be another account");
        }

        [TestCase(400L, CloudErrorCodes.PlayerAccountsExternalIdRequired)]
        [TestCase(422L, CloudErrorCodes.PlayerAccountsExternalFieldInvalid)]
        [TestCase(404L, CloudErrorCodes.PlayerAccountsAccountNotFound)]
        public void A_refusal_resending_cannot_change_is_not_resent_until_new_data(long status, string code)
        {
            _hasSession = true;
            _queue.ReportAdid("adid 1");
            Complete(0, Http(status, code));

            _queue.HandleSessionRefreshed();

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Rejected));
            Assert.That(_sent, Has.Count.EqualTo(1));

            _queue.ReportAdid("adid-1");

            Assert.That(_sent, Has.Count.EqualTo(2));
        }

        [Test]
        public void A_project_without_an_Adjust_integration_gets_nothing_more_in_this_run()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Http(403, CloudErrorCodes.PlayerAccountsExternalIntegrationUnavailable));

            _queue.HandleSignedIn();
            _queue.HandleSessionRefreshed();
            _queue.ReportAdid("adid-2");
            _queue.ReportAttribution(new AdjustAttributionDto { Campaign = "Summer" });

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Rejected));
            Assert.That(_sent, Has.Count.EqualTo(1));
        }

        [Test]
        public void A_forbidden_answer_without_the_integration_code_is_retried()
        {
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            Complete(0, Http(403, null));

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Failed));

            _queue.HandleSessionRefreshed();

            Assert.That(_sent, Has.Count.EqualTo(2));
        }

        [Test]
        public void Every_answer_is_raised_and_kept()
        {
            RestApiResult<ExternalIdDto> raised = null;
            _queue.Reported += r => raised = r;
            _hasSession = true;
            _queue.ReportAdid("adid-1");

            var answer = Ok();
            Complete(0, answer);

            Assert.That(raised, Is.SameAs(answer));
            Assert.That(_queue.LastResult, Is.SameAs(answer));
        }

        [Test]
        public void A_throwing_handler_does_not_wedge_the_queue()
        {
            _queue.Reported += _ => throw new System.InvalidOperationException("game code");
            _hasSession = true;
            _queue.ReportAdid("adid-1");
            _queue.ReportAttribution(new AdjustAttributionDto { Campaign = "Summer" });

            Assert.DoesNotThrow(() => Complete(0, Ok()));
            Assert.That(_sent, Has.Count.EqualTo(2), "the newer data still went out");
        }

        [Test]
        public void An_operation_that_finished_before_it_was_hooked_is_still_handled()
        {
            _queue = new AdjustReportQueue(() => true,
                _ => AsyncOperation<RestApiResult<ExternalIdDto>>.CreateCompleted(Ok()), null);

            _queue.ReportAdid("adid-1");

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Sent));
        }

        [Test]
        public void Blank_hand_overs_are_ignored()
        {
            _hasSession = true;
            _queue.ReportAdid("  ");
            _queue.ReportAdid(null);
            _queue.ReportAttribution(null);
            _queue.ReportAttribution(new AdjustAttributionDto { Campaign = " " });

            Assert.That(_queue.State, Is.EqualTo(AdjustReportState.Idle));
            Assert.That(_sent, Is.Empty);
        }

        private void Complete(int index, RestApiResult<ExternalIdDto> result) => _sent[index].Operation.Complete(result);

        private static RestApiResult<ExternalIdDto> Ok() =>
            RestApiResult<ExternalIdDto>.Success(new ExternalIdDto { ProviderKey = "adjust", ExternalId = "adid-1" });

        private static RestApiResult<ExternalIdDto> Http(long status, string code) =>
            RestApiResult<ExternalIdDto>.Fail(new RestApiError
            {
                Type = RestApiErrorType.Http,
                HttpStatusCode = status,
                Message = "HTTP/1.1 " + status,
                Errors = code == null
                    ? null
                    : new List<CloudApiError> { new CloudApiError { Code = code, Message = code } },
            });
    }
}

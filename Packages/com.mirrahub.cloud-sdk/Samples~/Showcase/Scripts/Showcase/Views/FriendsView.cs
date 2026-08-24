using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Friends.Dto;
using MirraCloud.Core.Friends.Enums;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Friends screen: the list with presence, both directions of pending requests, and every
    /// operation the service exposes — accept, reject, revoke, unfriend, block, delete, and their
    /// bulk variants.
    /// <para>
    /// This service has no player search, so ids are typed in. That is not a gap in the screen: a
    /// game gets the id from its own social flow (a nearby-players list, an invite link, a match
    /// result), and the screen says so instead of pretending otherwise.
    /// </para>
    /// </summary>
    public sealed class FriendsView : ServiceView
    {
        private const string FriendsSnippet =
@"// getProfilesInfo also brings each friend's nickname, icon and last login — without it you
// only get ids and presence.
var op = sdk.Friends.GetFriendsAsync(getProfilesInfo: true);
await op.Task();

foreach (GetPlayerDto p in op.Result.Data)
{
    // p.PlayerId, p.Status (presence), p.PlayerInfo.Nickname, p.PlayerInfo.IconKey
}";

        private const string RequestsSnippet =
@"// Three reads: both directions at once, or one side at a time.
var all = sdk.Friends.GetRequestsAsync();
var incoming = sdk.Friends.GetIncomingAsync();
var outgoing = sdk.Friends.GetOutgoingAsync();
await incoming.Task();

foreach (GetFriendRequestDto r in incoming.Result.Data)
{
    // r.SourcePlayerId, r.TargetPlayerId, r.Status, r.CreatedAt
}";

        private const string SendSnippet =
@"// Ask someone to be a friend. The id comes from your own social flow — this service has no
// player search.
await sdk.Friends.SendAsync(targetPlayerId).Task();

// Same call for a batch, one round trip.
await sdk.Friends.SendManyAsync(new[] { idA, idB, idC }).Task();";

        private const string AnswerSnippet =
@"// Incoming requests are answered by the *sender's* id; outgoing ones are revoked by target.
await sdk.Friends.AcceptAsync(sourcePlayerId).Task();
await sdk.Friends.RejectAsync(sourcePlayerId).Task();
await sdk.Friends.RevokeAsync(targetPlayerId).Task();

// Bulk variants exist for all three.
await sdk.Friends.AcceptManyAsync(sourceIds).Task();
await sdk.Friends.RejectManyAsync(sourceIds).Task();
await sdk.Friends.RevokeManyAsync(targetIds).Task();";

        private const string RemoveSnippet =
@"// Three different endings, deliberately separate calls:
await sdk.Friends.RemoveFriendAsync(targetPlayerId).Task();  // unfriend, both sides
await sdk.Friends.BanAsync(targetPlayerId).Task();           // block: no more requests
await sdk.Friends.DeleteAsync(targetPlayerId).Task();        // wipe the relation record

// …each with a bulk variant: BanManyAsync, DeleteManyAsync.
await sdk.Friends.BanManyAsync(ids).Task();";

        private List<GetPlayerDto> _friends = new List<GetPlayerDto>();
        private List<GetFriendRequestDto> _incoming = new List<GetFriendRequestDto>();
        private List<GetFriendRequestDto> _outgoing = new List<GetFriendRequestDto>();
        private Tabs _tabs;
        private string _search = string.Empty;

        public FriendsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _search = string.Empty;

            DeclareCall(new SdkCall("Read the friend list", FriendsSnippet));
            DeclareCall(new SdkCall("Read pending requests", RequestsSnippet));
            DeclareCall(new SdkCall("Send a request", SendSnippet));
            DeclareCall(new SdkCall("Accept, reject, revoke", AnswerSnippet,
                "Incoming requests are answered by the sender's id, outgoing ones by the target's."));
            DeclareCall(new SdkCall("Unfriend, block, delete", RemoveSnippet,
                "Three separate endings — the screen keeps them separate too."));

            UseToolbar()
                .WithSearch("Filter friends by nickname or id", OnSearch)
                .WithSpacer()
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Friends", LucideIcon.Users, BuildFriends)
                .Add("Requests", LucideIcon.UserPlus, BuildRequests)
                .Add("Actions", LucideIcon.Sparkles, BuildActions);
        }

        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            // Only the friend list filters; rebuilding that pane re-reads and re-applies it.
            _tabs.Invalidate(0);
        }

        // ----- friends --------------------------------------------------------------------------

        private VisualElement BuildFriends()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Friends.GetFriendsAsync(true),
                slot,
                BuildFriendsBody,
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Friends",
                    Snippet = FriendsSnippet,
                    ServiceName = "Friends",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(FriendColumns(),
                        "No friends yet. Send a request from the Actions tab — you need the other "
                        + "player's id, which a game normally already has from its own social flow.",
                        3, "Send a request", () => _tabs.Select(2)),
                });
            return slot;
        }

        private VisualElement BuildFriendsBody(GetPlayerDto[] friends)
        {
            _friends = new List<GetPlayerDto>(friends);
            SyncStatus();

            var col = new VisualElement();

            int online = 0;
            foreach (var f in _friends)
            {
                if (f.Status == ProfilePresenceStatus.Online)
                {
                    online++;
                }
            }

            col.Add(new KpiRow()
                .Add("Friends", LucideIcon.Users, _friends.Count.ToString())
                .Add("Online now", LucideIcon.Wifi, online.ToString(), null, online > 0)
                .Add("Incoming", LucideIcon.UserPlus, _incoming.Count.ToString())
                .Add("Outgoing", LucideIcon.Send, _outgoing.Count.ToString()));

            var shown = Filter(_friends);
            col.Add(new SectionHeader("Friend list", shown.Count + " of " + _friends.Count));

            if (shown.Count == 0)
            {
                col.Add(ZeroState.Panel(LucideIcon.Search, "Nothing matches that filter",
                    "No friend's nickname or id contains \"" + Fmt.Truncate(_search, 24) + "\"."));
                return col;
            }

            var table = new DataTable(FriendColumns()).WithZebra().WithMaxHeight(520f);
            table.Bind(shown);
            col.Add(table);
            return col;
        }

        private List<GetPlayerDto> Filter(List<GetPlayerDto> source)
        {
            if (_search.Length == 0)
            {
                return source;
            }
            var hits = new List<GetPlayerDto>();
            foreach (var f in source)
            {
                string nickname = f.PlayerInfo != null ? f.PlayerInfo.Nickname : null;
                if (Contains(nickname) || Contains(f.PlayerId))
                {
                    hits.Add(f);
                }
            }
            return hits;
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private DataColumn[] FriendColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = 46,
                    Cell = o =>
                    {
                        var player = (GetPlayerDto)o;
                        var info = player.PlayerInfo;
                        string name = NameOf(player);
                        var avatar = new Avatar(34f);

                        // Only an External icon key is a URL; a preset key is not fetchable, so those
                        // fall back to initials rather than to a broken image.
                        if (info != null && info.IconKey != null && info.IconKey.Source == KeySource.External)
                        {
                            avatar.BindUrl(Images, info.IconKey.Key, name);
                        }
                        else
                        {
                            avatar.SetInitialsFor(name);
                        }
                        avatar.SetPresence(PresenceColor(player.Status));
                        return avatar;
                    },
                },
                new DataColumn
                {
                    Header = "PLAYER", Grow = 2f,
                    SortKey = o => NameOf((GetPlayerDto)o),
                    Cell = o =>
                    {
                        var player = (GetPlayerDto)o;
                        var box = new VisualElement();

                        var name = new Label(NameOf(player));
                        name.enableRichText = false;
                        name.AddToClassList("sc-list-row__title");
                        box.Add(name);

                        var id = new Label(Fmt.Id(player.PlayerId, 12));
                        id.enableRichText = false;
                        id.AddToClassList("sc-list-row__subtitle");
                        box.Add(id);
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "PRESENCE", Grow = 1f,
                    SortKey = o => ((GetPlayerDto)o).Status.ToString(),
                    Cell = o =>
                    {
                        var player = (GetPlayerDto)o;
                        return new Chip(player.Status.ToString(), PresenceTone(player.Status));
                    },
                },
                new DataColumn
                {
                    Header = "LAST LOGIN", Grow = 1f, Align = "right",
                    SortKey = o =>
                    {
                        var info = ((GetPlayerDto)o).PlayerInfo;
                        return info != null ? info.LastLogin : DateTime.MinValue;
                    },
                    Cell = o =>
                    {
                        var info = ((GetPlayerDto)o).PlayerInfo;
                        return new Label(info != null ? Fmt.Date(info.LastLogin) : Fmt.Dash);
                    },
                },
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = 208, Align = "right",
                    Cell = o =>
                    {
                        var player = (GetPlayerDto)o;
                        var row = new VisualElement();
                        // Not .sc-chip-row: that one wraps, which would stack the two buttons and
                        // double the height of every row in the table.
                        row.AddToClassList("sc-row-actions");

                        var unfriend = new Button(() => ConfirmUnfriend(player)) { text = "Unfriend" };
                        unfriend.AddToClassList("sc-btn");
                        row.Add(unfriend);

                        var block = new Button(() => ConfirmBlock(player)) { text = "Block" };
                        block.AddToClassList("sc-btn");
                        block.AddToClassList("sc-btn--danger");
                        row.Add(block);
                        return row;
                    },
                },
            };
        }

        private static string NameOf(GetPlayerDto player)
        {
            var info = player.PlayerInfo;
            return info != null && !string.IsNullOrEmpty(info.Nickname)
                ? info.Nickname
                : Fmt.Id(player.PlayerId, 10);
        }

        private void SyncStatus()
        {
            if (_incoming.Count > 0)
            {
                SetStatus(_friends.Count + " friends · " + _incoming.Count + " waiting", ChipTone.Warn);
                return;
            }
            SetStatus(_friends.Count + (_friends.Count == 1 ? " friend" : " friends"),
                _friends.Count > 0 ? ChipTone.Ok : ChipTone.Neutral);
        }

        // ----- requests -------------------------------------------------------------------------

        private VisualElement BuildRequests()
        {
            var col = new VisualElement();

            col.Add(new SectionHeader("Incoming"));
            var incoming = new VisualElement();
            incoming.style.marginBottom = 18f;
            col.Add(incoming);
            ViewBind.Load(
                () => Sdk.Friends.GetIncomingAsync(),
                incoming,
                data =>
                {
                    _incoming = new List<GetFriendRequestDto>(data);
                    SyncStatus();
                    return BuildRequestList(data, true);
                },
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Incoming requests",
                    Snippet = RequestsSnippet,
                    ServiceName = "Friends",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Inbox, "Nobody is waiting",
                        "Requests other players send you land here, with Accept and Reject on each row."),
                });

            col.Add(new SectionHeader("Outgoing"));
            var outgoing = new VisualElement();
            col.Add(outgoing);
            ViewBind.Load(
                () => Sdk.Friends.GetOutgoingAsync(),
                outgoing,
                data =>
                {
                    _outgoing = new List<GetFriendRequestDto>(data);
                    return BuildRequestList(data, false);
                },
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Outgoing requests",
                    Snippet = RequestsSnippet,
                    ServiceName = "Friends",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Send, "No requests sent",
                        "Requests you send sit here until the other player answers, and can be revoked "
                        + "from the row.",
                        "Send a request", () => _tabs.Select(2)),
                });

            return col;
        }

        private VisualElement BuildRequestList(GetFriendRequestDto[] requests, bool inbound)
        {
            var list = new VisualElement();
            foreach (var request in requests)
            {
                // An inbound request is keyed by who sent it; an outbound one by who it went to.
                string otherId = inbound ? request.SourcePlayerId : request.TargetPlayerId;

                var row = new ListRow();
                row.SetLead(new Avatar(34f).SetInitialsFor(otherId));
                row.SetTitle(Fmt.Id(otherId, 14));
                row.SetSubtitle((inbound ? "sent " : "waiting since ") + Fmt.Date(request.CreatedAt));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-chip-row");
                trailing.Add(new Chip(request.Status.ToString(), StatusTone(request.Status)));
                trailing.Add(new CopyButton(otherId, Toasts, "id"));

                if (request.Status == FriendRequestStatus.Pending)
                {
                    if (inbound)
                    {
                        var accept = new Button(() => Answer(otherId, RequestAction.Accept)) { text = "Accept" };
                        accept.AddToClassList("sc-btn");
                        accept.AddToClassList("sc-btn--primary");
                        trailing.Add(accept);

                        var reject = new Button(() => Answer(otherId, RequestAction.Reject)) { text = "Reject" };
                        reject.AddToClassList("sc-btn");
                        trailing.Add(reject);
                    }
                    else
                    {
                        var revoke = new Button(() => Answer(otherId, RequestAction.Revoke)) { text = "Revoke" };
                        revoke.AddToClassList("sc-btn");
                        trailing.Add(revoke);
                    }
                }

                row.SetTrailing(trailing);
                list.Add(row);
            }
            return list;
        }

        private enum RequestAction
        {
            Accept,
            Reject,
            Revoke,
        }

        private async void Answer(string playerId, RequestAction action)
        {
            AsyncOperation<RestApiResult> op;
            string done;
            switch (action)
            {
                case RequestAction.Accept:
                    op = Sdk.Friends.AcceptAsync(playerId);
                    done = "Request accepted";
                    break;
                case RequestAction.Reject:
                    op = Sdk.Friends.RejectAsync(playerId);
                    done = "Request rejected";
                    break;
                default:
                    op = Sdk.Friends.RevokeAsync(playerId);
                    done = "Request revoked";
                    break;
            }

            var outcome = await Await(op, "Friends · " + action);
            if (!outcome.Ok)
            {
                if (Toasts != null)
                {
                    Toasts.Fail(action + " failed · " + outcome.Message);
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok(done);
            }
            // Accepting changes the friend list too, so both panes are dropped.
            _tabs.Invalidate(1);
            _tabs.Invalidate(0);
        }

        private static ChipTone StatusTone(FriendRequestStatus status)
        {
            switch (status)
            {
                case FriendRequestStatus.Accepted: return ChipTone.Ok;
                case FriendRequestStatus.Rejected: return ChipTone.Bad;
                case FriendRequestStatus.Cancelled: return ChipTone.Neutral;
                default: return ChipTone.Warn;
            }
        }

        // ----- row actions ----------------------------------------------------------------------

        private void ConfirmUnfriend(GetPlayerDto player)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Remove friend",
                "This removes the friendship for both players. " + NameOf(player)
                + " can send a new request afterwards.",
                "Unfriend",
                () => RowAction(Sdk.Friends.RemoveFriendAsync(player.PlayerId),
                    "Removed " + NameOf(player), "Remove"));
        }

        private void ConfirmBlock(GetPlayerDto player)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Block player",
                "Blocking removes the friendship and stops " + NameOf(player)
                + " from sending new requests.",
                "Block",
                () => RowAction(Sdk.Friends.BanAsync(player.PlayerId),
                    "Blocked " + NameOf(player), "Block"));
        }

        private async void RowAction(AsyncOperation<RestApiResult> op, string done, string label)
        {
            var outcome = await Await(op, "Friends · " + label);
            if (!outcome.Ok)
            {
                if (Toasts != null)
                {
                    Toasts.Fail(label + " failed · " + outcome.Message);
                }
                return;
            }
            if (Toasts != null)
            {
                Toasts.Ok(done);
            }
            _tabs.Invalidate(0);
        }

        // ----- actions tab ----------------------------------------------------------------------

        private VisualElement BuildActions()
        {
            var col = new VisualElement();

            var hint = new Label("This service has no player search: every call takes an id the game "
                + "already holds — from a nearby-players list, an invite link, a match result. Paste "
                + "one below to try the calls.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Send a friend request",
                    "Asks one player to be friends. They see it under Requests · Incoming.",
                    LucideIcon.UserPlus)
                .WithFields(FormField.Text("playerId", "Player id", null, true))
                .WithSnippet(SendSnippet)
                .OnRun("Send", v => Run(Sdk.Friends.SendAsync(v.Text("playerId")), "Request sent", 1)));

            col.Add(new ActionCard("Send many at once",
                    "One round trip for a whole list of comma-separated ids.", LucideIcon.Users)
                .WithFields(FormField.Text("ids", "Player ids (comma-separated)", null, true))
                .WithSnippet(SendSnippet)
                .OnRun("Send batch", v => RunMany(v.Text("ids"),
                    ids => Sdk.Friends.SendManyAsync(ids), "Requests sent", 1)));

            col.Add(new ActionCard("Accept a request",
                    "Answered by the id of the player who sent it, not by a request id.",
                    LucideIcon.UserCheck)
                .WithFields(FormField.Text("sourceId", "Sender's player id", null, true))
                .WithSnippet(AnswerSnippet)
                .OnRun("Accept", v => Run(Sdk.Friends.AcceptAsync(v.Text("sourceId")),
                    "Request accepted", 1)));

            col.Add(new ActionCard("Reject a request", "Declines it; the sender can try again later.",
                    LucideIcon.UserX)
                .WithFields(FormField.Text("sourceId", "Sender's player id", null, true))
                .WithSnippet(AnswerSnippet)
                .OnRun("Reject", v => Run(Sdk.Friends.RejectAsync(v.Text("sourceId")),
                    "Request rejected", 1)));

            col.Add(new ActionCard("Revoke your request",
                    "Takes back a request you sent, by the id you sent it to.", LucideIcon.UserMinus)
                .WithFields(FormField.Text("targetId", "Target player id", null, true))
                .WithSnippet(AnswerSnippet)
                .OnRun("Revoke", v => Run(Sdk.Friends.RevokeAsync(v.Text("targetId")),
                    "Request revoked", 1)));

            col.Add(new ActionCard("Unfriend", "Removes the friendship for both sides.",
                    LucideIcon.UserMinus)
                .WithFields(FormField.Text("targetId", "Player id", null, true))
                .WithSnippet(RemoveSnippet)
                .OnRun("Unfriend", v => Run(Sdk.Friends.RemoveFriendAsync(v.Text("targetId")),
                    "Friend removed", 0), true));

            col.Add(new ActionCard("Block",
                    "Removes the friendship and stops further requests from that player.",
                    LucideIcon.Ban)
                .WithFields(FormField.Text("targetId", "Player id", null, true))
                .WithSnippet(RemoveSnippet)
                .OnRun("Block", v => Run(Sdk.Friends.BanAsync(v.Text("targetId")),
                    "Player blocked", 0), true));

            col.Add(new ActionCard("Block many", "The bulk variant, for comma-separated ids.",
                    LucideIcon.Shield)
                .WithFields(FormField.Text("ids", "Player ids (comma-separated)", null, true))
                .WithSnippet(RemoveSnippet)
                .OnRun("Block batch", v => RunMany(v.Text("ids"),
                    ids => Sdk.Friends.BanManyAsync(ids), "Players blocked", 0), true));

            col.Add(new ActionCard("Delete the relation",
                    "Wipes the relationship record itself rather than unfriending or blocking — for "
                    + "when a player is being erased.", LucideIcon.Trash)
                .WithFields(FormField.Text("targetId", "Player id", null, true))
                .WithSnippet(RemoveSnippet)
                .OnRun("Delete", v => Run(Sdk.Friends.DeleteAsync(v.Text("targetId")),
                    "Relation deleted", 0), true));

            col.Add(new ActionCard("Delete many relations", "The bulk variant.", LucideIcon.Trash)
                .WithFields(FormField.Text("ids", "Player ids (comma-separated)", null, true))
                .WithSnippet(RemoveSnippet)
                .OnRun("Delete batch", v => RunMany(v.Text("ids"),
                    ids => Sdk.Friends.DeleteManyAsync(ids), "Relations deleted", 0), true));

            return col;
        }

        private async Task<ActionOutcome> Run(AsyncOperation<RestApiResult> op, string success, int tabToRefresh)
        {
            var outcome = await Await(op, "Friends write");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            _tabs.Invalidate(tabToRefresh);
            return ActionOutcome.Success(success);
        }

        private async Task<ActionOutcome> RunMany(string raw,
            Func<string[], AsyncOperation<RestApiResult>> call, string success, int tabToRefresh)
        {
            var ids = SplitIds(raw);
            if (ids.Length == 0)
            {
                return ActionOutcome.Failure("Give at least one id, separated by commas.");
            }

            var outcome = await Await(call(ids), "Friends bulk write");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            _tabs.Invalidate(tabToRefresh);
            return ActionOutcome.Success(success + " · " + ids.Length + " ids");
        }

        private static string[] SplitIds(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }
            var ids = new List<string>();
            foreach (var part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0 && !ids.Contains(trimmed))
                {
                    ids.Add(trimmed);
                }
            }
            return ids.ToArray();
        }

        /// <summary>
        /// Awaits one of the many <c>RestApiResult</c> writes and folds it into a flag plus a message,
        /// so seventeen call sites do not each re-derive the same null checks.
        /// </summary>
        private async Task<Outcome> Await(AsyncOperation<RestApiResult> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result);
            }

            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true };
            }
            string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome { Ok = false, Message = message };
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;
        }

        private static Color? PresenceColor(ProfilePresenceStatus status)
        {
            switch (status)
            {
                case ProfilePresenceStatus.Online: return ShowcaseTheme.Ok;
                case ProfilePresenceStatus.Away:
                case ProfilePresenceStatus.OnTheWay: return ShowcaseTheme.Warn;
                case ProfilePresenceStatus.Busy: return ShowcaseTheme.Bad;
                default: return null;
            }
        }

        private static ChipTone PresenceTone(ProfilePresenceStatus status)
        {
            switch (status)
            {
                case ProfilePresenceStatus.Online: return ChipTone.Ok;
                case ProfilePresenceStatus.Away:
                case ProfilePresenceStatus.OnTheWay: return ChipTone.Warn;
                case ProfilePresenceStatus.Busy: return ChipTone.Bad;
                default: return ChipTone.Neutral;
            }
        }
    }
}

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
    /// Friends screen laid out like a game's: the friend list with presence and Unfriend / Block on
    /// each row, an "Add friend" button in the toolbar, and the requests in both directions with
    /// their answers on the rows and "all at once" buttons above — every operation the service has,
    /// bulk variants included, sits where a player would look for it.
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
            DeclareCall(new SdkCall("Send a request", SendSnippet,
                "Add friend takes one id or several, comma-separated — several go out in one call."));
            DeclareCall(new SdkCall("Accept, reject, revoke", AnswerSnippet,
                "Incoming requests are answered by the sender's id, outgoing ones by the target's. "
                + "The \"all\" buttons above each list are the bulk variants."));
            DeclareCall(new SdkCall("Unfriend, block, delete", RemoveSnippet,
                "Unfriend and Block sit on a friend's row, Block also on an incoming request; "
                + "Remove on an answered request deletes the relation."));

            UseToolbar()
                .WithSearch("Filter friends by nickname or id", OnSearch)
                .WithSpacer()
                .WithAction("Add friend", LucideIcon.UserPlus, OpenAddFriend, true)
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Friends", LucideIcon.Users, BuildFriends)
                .Add("Requests", LucideIcon.Inbox, BuildRequests);
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
                        "No friends yet. Add one by id — a game normally already has it from its own "
                        + "social flow, since this service has no player search.",
                        3, "Add friend", OpenAddFriend),
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

            // Request counts are not shown here: they are only known once the Requests tab has loaded.
            col.Add(new KpiRow()
                .Add("Friends", LucideIcon.Users, _friends.Count.ToString())
                .Add("Online now", LucideIcon.Wifi, online.ToString(), null, online > 0));

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

                        var block = new Button(() => ConfirmBlock(player.PlayerId, NameOf(player))) { text = "Block" };
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
                        "Requests other players send you land here, with Accept, Reject and Block on each row."),
                });

            col.Add(new SectionHeader("Sent"));
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
                        "Add friend", OpenAddFriend),
                });

            return col;
        }

        /// <summary>
        /// One direction of requests: the "all at once" buttons (the bulk calls) above the rows, then a
        /// row per request with the answers that make sense for its direction and status.
        /// </summary>
        private VisualElement BuildRequestList(GetFriendRequestDto[] requests, bool inbound)
        {
            // An inbound request is keyed by who sent it; an outbound one by who it went to.
            var pending = new List<string>();
            var answered = new List<string>();
            foreach (var request in requests)
            {
                string otherId = OtherId(request, inbound);
                if (request.Status == FriendRequestStatus.Pending)
                {
                    pending.Add(otherId);
                }
                else if (IsClosed(request.Status))
                {
                    answered.Add(otherId);
                }
            }

            var col = new VisualElement();
            var bulk = new VisualElement();
            bulk.AddToClassList("sc-row-actions");
            bulk.AddToClassList("sc-fr-bulk");
            if (pending.Count > 1)
            {
                if (inbound)
                {
                    bulk.Add(Btn("Accept all", "sc-btn--primary",
                        () => Bulk(Sdk.Friends.AcceptManyAsync(pending.ToArray()), "Accepted " + pending.Count + " requests", "Accept all")));
                    bulk.Add(Btn("Reject all", null,
                        () => Bulk(Sdk.Friends.RejectManyAsync(pending.ToArray()), "Rejected " + pending.Count + " requests", "Reject all")));
                    bulk.Add(Btn("Block all", "sc-btn--danger", () => ConfirmBlockAll(pending.ToArray())));
                }
                else
                {
                    bulk.Add(Btn("Revoke all", null,
                        () => Bulk(Sdk.Friends.RevokeManyAsync(pending.ToArray()), "Revoked " + pending.Count + " requests", "Revoke all")));
                }
            }
            if (answered.Count > 1)
            {
                bulk.Add(Btn("Remove answered", null,
                    () => Bulk(Sdk.Friends.DeleteManyAsync(answered.ToArray()), "Removed " + answered.Count + " requests", "Remove answered")));
            }
            if (bulk.childCount > 0)
            {
                col.Add(bulk);
            }

            foreach (var request in requests)
            {
                col.Add(RequestRow(request, inbound));
            }
            return col;
        }

        private VisualElement RequestRow(GetFriendRequestDto request, bool inbound)
        {
            string otherId = OtherId(request, inbound);

            var row = new ListRow();
            row.SetLead(new Avatar(34f).SetInitialsFor(otherId));
            row.SetTitle(Fmt.Id(otherId, 14));
            row.SetSubtitle((!inbound && request.Status == FriendRequestStatus.Pending ? "waiting since " : "sent ")
                           + Fmt.Date(request.CreatedAt));

            var trailing = new VisualElement();
            trailing.AddToClassList("sc-row-actions");
            trailing.AddToClassList("sc-fr-request__actions");
            if (request.Status != FriendRequestStatus.Pending)
            {
                trailing.Add(new Chip(request.Status.ToString(), StatusTone(request.Status)));
            }
            trailing.Add(new CopyButton(otherId, Toasts, "id"));

            if (request.Status == FriendRequestStatus.Pending)
            {
                if (inbound)
                {
                    trailing.Add(Btn("Accept", "sc-btn--primary", () => Answer(otherId, RequestAction.Accept)));
                    trailing.Add(Btn("Reject", null, () => Answer(otherId, RequestAction.Reject)));
                    trailing.Add(Btn("Block", "sc-btn--danger", () => ConfirmBlock(otherId, Fmt.Id(otherId, 14))));
                }
                else
                {
                    trailing.Add(Btn("Revoke", null, () => Answer(otherId, RequestAction.Revoke)));
                }
            }
            else if (IsClosed(request.Status))
            {
                var remove = Btn("Remove", null, () => Answer(otherId, RequestAction.Delete));
                remove.tooltip = "Deletes the request from both players' lists";
                trailing.Add(remove);
            }

            row.SetTrailing(trailing);
            return row;
        }

        private static string OtherId(GetFriendRequestDto request, bool inbound)
        {
            return inbound ? request.SourcePlayerId : request.TargetPlayerId;
        }

        /// <summary>A request nobody can answer any more — the only kind worth clearing away.</summary>
        private static bool IsClosed(FriendRequestStatus status)
        {
            return status == FriendRequestStatus.Rejected || status == FriendRequestStatus.Cancelled;
        }

        private static Button Btn(string text, string tone, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("sc-btn");
            if (!string.IsNullOrEmpty(tone))
            {
                btn.AddToClassList(tone);
            }
            return btn;
        }

        private enum RequestAction
        {
            Accept,
            Reject,
            Revoke,
            Delete,
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
                case RequestAction.Delete:
                    op = Sdk.Friends.DeleteAsync(playerId);
                    done = "Request removed";
                    break;
                default:
                    op = Sdk.Friends.RevokeAsync(playerId);
                    done = "Request revoked";
                    break;
            }
            await Write(op, done, action.ToString());
        }

        private async void Bulk(AsyncOperation<RestApiResult> op, string done, string label)
        {
            await Write(op, done, label);
        }

        /// <summary>
        /// Runs one write, toasts the outcome and drops both panes: accepting changes the friend
        /// list, unfriending or blocking can change the requests.
        /// </summary>
        private async Task Write(AsyncOperation<RestApiResult> op, string done, string label)
        {
            var outcome = await Await(op, "Friends · " + label);
            if (!outcome.Ok)
            {
                Toasts?.Fail(label + " failed · " + outcome.Message);
                return;
            }

            Toasts?.Ok(done);
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

        // ----- adding, unfriending, blocking ----------------------------------------------------

        /// <summary>
        /// "Add friend" the way a game has it, by id: the service has no player search, so the id
        /// comes from the game's own social flow (a nearby-players list, an invite link, a match
        /// result). Several comma-separated ids go out as one <c>SendManyAsync</c>.
        /// </summary>
        private void OpenAddFriend()
        {
            if (Popup == null)
            {
                return;
            }
            FormDialog.Open(Popup, "Add friend",
                new[]
                {
                    FormField.Text("ids", "Player id", null, true)
                        .WithPlaceholder("One id, or several separated by commas"),
                },
                "Send request",
                v => SendRequests(v.Text("ids")));
        }

        private async void SendRequests(string raw)
        {
            var ids = SplitIds(raw);
            if (ids.Length == 0)
            {
                Toasts?.Fail("Give at least one player id");
                return;
            }
            var op = ids.Length == 1 ? Sdk.Friends.SendAsync(ids[0]) : Sdk.Friends.SendManyAsync(ids);
            await Write(op, ids.Length == 1 ? "Request sent" : ids.Length + " requests sent", "Send");
        }

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
                () => Bulk(Sdk.Friends.RemoveFriendAsync(player.PlayerId), "Removed " + NameOf(player), "Unfriend"));
        }

        private void ConfirmBlock(string playerId, string name)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Block player",
                "Blocking removes any friendship or request and stops " + name + " from sending new ones.",
                "Block",
                () => Bulk(Sdk.Friends.BanAsync(playerId), "Blocked " + name, "Block"));
        }

        private void ConfirmBlockAll(string[] ids)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Block " + ids.Length + " players",
                "Every player with a pending request to you is blocked and cannot send new ones.",
                "Block all",
                () => Bulk(Sdk.Friends.BanManyAsync(ids), "Blocked " + ids.Length + " players", "Block all"));
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Groups.Dto.Request;
using MirraCloud.Core.Groups.Dto.Response;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Groups screen, built as a browse → open flow rather than a menu of calls: the player's own
    /// groups and a public search on the front, and opening any row swaps the whole screen for that
    /// one group's page — its card, the actions that apply to *it*, and its members, roles, join
    /// requests, invites and bans.
    /// <para>
    /// Nothing is addressed by typing an id into a form. Creating a group happens from the toolbar
    /// and the new group lands in the list; joining happens on the row you found; editing, leaving
    /// and deleting happen on the group's own page, gated on whether the player is a member or the
    /// owner, so a control that cannot possibly work is absent instead of present and failing.
    /// </para>
    /// <para>
    /// Most of the reads are paged (<c>PaginatedResult</c>), so each list owns a <see cref="Pager"/>
    /// and asks the server for one page at a time rather than pretending the whole list fits.
    /// </para>
    /// </summary>
    public sealed class GroupsView : ServiceView
    {
        private const string MyGroupsSnippet =
@"// Paged, like most group reads: Items plus TotalCount, and you ask for a page.
var op = sdk.Groups.GetMyGroupsAsync(page: 1, pageSize: 20);
await op.Task();

PaginatedResult<GroupListItemDto> page = op.Result.Data;
// page.Items, page.TotalCount, page.Page, page.PageSize

// Someone else's groups, if the project allows it:
var theirs = sdk.Groups.GetPlayerGroupsAsync(profileId);";

        private const string SearchSnippet =
@"// Public discovery. Both filters are optional; visibility is a plain string (""public"",
// ""private"", …) because the set is project-defined rather than an SDK enum.
var op = sdk.Groups.SearchAsync(query: ""guild"", visibility: ""public"", page: 1, pageSize: 20);
await op.Task();";

        private const string GroupSnippet =
@"// One group in full, including its chat config.
var op = sdk.Groups.GetAsync(groupId);
await op.Task();

GroupDto g = op.Result.Data;
// g.Name, g.Tag, g.Description, g.OwnerId, g.Visibility, g.JoinPolicy,
// g.MemberCount / g.MaxMembers, g.Metadata, g.ChatConfig.ChannelId";

        private const string MembersSnippet =
@"var op = sdk.Groups.GetMembersAsync(groupId, page: 1, pageSize: 20);
await op.Task();

foreach (MemberDto m in op.Result.Data.Items)
{
    // m.ProfileId, m.RoleId, m.RoleName, m.IsOwner, m.JoinedAt
}

// Moderation, both keyed by profile id:
await sdk.Groups.UpdateMemberRoleAsync(groupId, profileId, new UpdateMemberRoleDto { RoleId = roleId }).Task();
await sdk.Groups.KickMemberAsync(groupId, profileId).Task();";

        private const string RolesSnippet =
@"// Roles are not paged — a group has a handful.
var op = sdk.Groups.GetRolesAsync(groupId);
await op.Task();

var create = sdk.Groups.CreateRoleAsync(groupId, new CreateRoleDto
{
    Name = ""Officer"",
    Permissions = new GroupPermissionsDto { CanInvite = true, CanKick = true }
});

await sdk.Groups.UpdateRoleAsync(groupId, roleId, new UpdateRoleDto { Name = ""Veteran"" }).Task();
await sdk.Groups.DeleteRoleAsync(groupId, roleId).Task();";

        private const string BansSnippet =
@"var op = sdk.Groups.GetBansAsync(groupId, page: 1, pageSize: 20);
await op.Task();

// Note the asymmetry: banning takes an account id, unbanning a profile id.
await sdk.Groups.BanPlayerAsync(groupId, new BanPlayerDto { AccountId = accountId, Reason = ""spam"" }).Task();
await sdk.Groups.UnbanPlayerAsync(groupId, profileId).Task();";

        private const string InvitesSnippet =
@"// A direct invite goes to one player…
var invite = sdk.Groups.CreateInviteAsync(groupId, new CreateInviteDto
{
    TargetPlayerId = profileId,
    InviteType = ""direct"",
    ExpiresAt = DateTime.UtcNow.AddDays(7)
});
await sdk.Groups.RevokeInviteAsync(groupId, inviteId).Task();

// …an invite key is a shareable secret anyone can redeem.
var key = sdk.Groups.CreateInviteKeyAsync(groupId, new CreateInviteKeyDto
{
    InviteType = ""key"",
    ExpiresAt = DateTime.UtcNow.AddDays(30)
});
await sdk.Groups.JoinByKeyAsync(groupId, new JoinByKeyDto { SecretKey = secret }).Task();
await sdk.Groups.DeleteInviteKeyAsync(groupId, inviteKeyId).Task();

// The invited player answers:
await sdk.Groups.AcceptInviteAsync(groupId, inviteId).Task();
await sdk.Groups.DeclineInviteAsync(groupId, inviteId).Task();";

        private const string JoinSnippet =
@"// Which of these works depends on the group's JoinPolicy: an open group takes JoinAsync,
// a request-only one wants a join request that a moderator then answers.
await sdk.Groups.JoinAsync(groupId).Task();

var request = sdk.Groups.CreateJoinRequestAsync(groupId);
await request.Task();

var pending = sdk.Groups.GetJoinRequestsAsync(groupId, statusFilter: ""pending"");
await sdk.Groups.ApproveJoinRequestAsync(groupId, requestId).Task();
await sdk.Groups.RejectJoinRequestAsync(groupId, requestId).Task();

await sdk.Groups.LeaveAsync(groupId).Task();";

        private const string LifecycleSnippet =
@"var created = sdk.Groups.CreateAsync(new CreateGroupDto
{
    Name = ""Night Owls"",
    Description = ""Late-night raids"",
    Visibility = ""public"",
    JoinPolicy = ""open"",
    MaxMembers = 50,
    CreateChat = true,         // asks for a chat channel up front
    AutoJoinMembers = true     // ...and puts everyone who joins the group into it
});
await created.Task();

await sdk.Groups.UpdateAsync(groupId, new UpdateGroupDto { Description = ""Now with loot"" }).Task();
await sdk.Groups.DeleteAsync(groupId).Task();

// A chat can also be added to an existing group:
var chat = sdk.Groups.CreateChatAsync(groupId);";

        private const int PageSize = 20;

        /// <summary>
        /// How many of the player's groups the membership probe pulls in one go. Only used when the
        /// first page of "My groups" did not already settle the question (see <see cref="IsMember"/>).
        /// </summary>
        private const int MembershipProbeSize = 100;

        private static readonly string[] Visibilities = { "Any", "public", "private" };

        // Group ids the player belongs to, accumulated from every "My groups" page that has loaded.
        // Membership decides which actions a group's page offers, so it is tracked rather than guessed.
        private readonly HashSet<string> _myGroupIds = new HashSet<string>(StringComparer.Ordinal);

        // The SDK exposes no "current profile id", so ownership is decided by set membership: every
        // profile this account owns counts as me — which is what the server enforces anyway.
        private readonly HashSet<string> _myProfileIds = new HashSet<string>(StringComparer.Ordinal);

        private Toolbar _toolbar;
        private Tabs _tabs;
        private VisualElement _panes;
        private VisualElement _detail;

        private string _query = string.Empty;
        private string _visibility;
        private int _myGroupsTotal = -1;

        // The last group this session asked to join. Nothing reads a *pending* request back out of
        // the service for the asking player, so the answer is remembered here.
        private string _requestedGroupId;
        private bool _closed;

        // ----- open group -----
        private string _groupId;
        private GroupDto _group;
        private RoleDto[] _roles = new RoleDto[0];
        private Label _heroRole;
        private VisualElement _actionSlot;
        private VisualElement _sectionsSlot;
        private Tabs _groupTabs;
        private int _tabMembers = -1;
        private int _tabRequests = -1;
        private int _tabRoles = -1;
        private int _tabBans = -1;
        private bool _membershipResolved;

        public GroupsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
            // Async continuations must not paint into a screen the player has walked away from.
            RegisterCallback<DetachFromPanelEvent>(_ => _closed = true);
        }

        protected override void Populate()
        {
            _closed = false;
            _query = string.Empty;
            _visibility = null;
            _myGroupsTotal = -1;
            _requestedGroupId = null;
            _myGroupIds.Clear();
            ClearOpenGroup();

            DeclareCall(new SdkCall("Read the player's groups", MyGroupsSnippet));
            DeclareCall(new SdkCall("Search public groups", SearchSnippet));
            DeclareCall(new SdkCall("Read one group", GroupSnippet));
            DeclareCall(new SdkCall("Members and moderation", MembersSnippet));
            DeclareCall(new SdkCall("Roles and permissions", RolesSnippet));
            DeclareCall(new SdkCall("Bans", BansSnippet,
                "Banning takes an account id while unbanning takes a profile id — easy to get wrong."));
            DeclareCall(new SdkCall("Invites and invite keys", InvitesSnippet));
            DeclareCall(new SdkCall("Joining and leaving", JoinSnippet,
                "Which call applies depends on the group's JoinPolicy."));
            DeclareCall(new SdkCall("Create, update, delete", LifecycleSnippet));

            _toolbar = UseToolbar()
                .WithSearch("Search groups by name", OnSearch)
                .WithSpacer()
                .WithAction("New group", LucideIcon.Plus, OpenCreateDialog, true)
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("My groups", LucideIcon.Users, BuildMyGroups)
                .Add("Discover", LucideIcon.Search, BuildDiscover);

            // ServiceView pins the strip above the scroller and moves the panes into it. Both halves
            // are hidden together while a group's page is open, which is what makes the detail read
            // as its own screen instead of a third tab.
            _panes = Content.Q<VisualElement>(className: "sc-svc__panes");

            _detail = new VisualElement();
            _detail.AddToClassList("sc-grp-detail");
            _detail.style.display = DisplayStyle.None;
            Content.Add(_detail);

            ResolveOwnProfiles();
        }

        private void OnSearch(string text)
        {
            string next = text == null ? string.Empty : text.Trim();
            if (string.Equals(next, _query, StringComparison.Ordinal))
            {
                return;
            }
            _query = next;

            // Both lists answer to the search box, so whichever one is in front reacts and the other
            // rebuilds when it is next selected.
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
        }

        // ----- my groups ------------------------------------------------------------------------

        private VisualElement BuildMyGroups()
        {
            var host = new VisualElement();

            if (_query.Length > 0)
            {
                // GetMyGroupsAsync takes no query, so this filter is honest about being local.
                host.Add(Hint("Filtering the loaded page by \"" + Fmt.Truncate(_query, 24)
                    + "\" — the SDK has no server-side search over the player's own groups."));
            }

            var slot = new VisualElement();
            var pager = new Pager(PageSize);
            host.Add(slot);
            host.Add(pager);

            pager.PageRequested += page => LoadMyGroups(slot, pager, page);
            LoadMyGroups(slot, pager, 1);
            return host;
        }

        private void LoadMyGroups(VisualElement slot, Pager pager, int page)
        {
            ViewBind.Load(
                () => Sdk.Groups.GetMyGroupsAsync(page, PageSize),
                slot,
                data =>
                {
                    RememberMyGroups(data);
                    pager.SetTotal(data.TotalCount, page);
                    SetBrowseStatus();

                    var matching = Filter(data.Items);
                    if (matching.Count == 0)
                    {
                        return ZeroState.Panel(LucideIcon.Search, "Nothing on this page matches",
                            "Clear the search box, or turn the page — only the groups already loaded "
                            + "are filtered.");
                    }
                    return GroupList(matching, true);
                },
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "My groups",
                    Snippet = MyGroupsSnippet,
                    ServiceName = "Groups",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        _myGroupsTotal = 0;
                        pager.SetTotal(0, 1);
                        SetBrowseStatus();
                        return ZeroState.Panel(LucideIcon.Users, "Not in any group yet",
                            "Create one and it appears right here, or find an existing one under "
                            + "Discover and join it. A group can carry its own chat channel, roles "
                            + "and bans.",
                            "Create a group", OpenCreateDialog);
                    },
                });
        }

        /// <summary>
        /// Records what the player belongs to. <see cref="_myGroupsTotal"/> is what later tells
        /// <see cref="IsMember"/> whether the cached set is the whole truth or just a page of it.
        /// </summary>
        private void RememberMyGroups(PaginatedResult<GroupListItemDto> page)
        {
            if (page == null)
            {
                return;
            }
            _myGroupsTotal = page.TotalCount;
            if (page.Items == null)
            {
                return;
            }
            foreach (var group in page.Items)
            {
                if (group != null && !string.IsNullOrEmpty(group.GroupId))
                {
                    _myGroupIds.Add(group.GroupId);
                }
            }
        }

        private List<GroupListItemDto> Filter(GroupListItemDto[] groups)
        {
            var kept = new List<GroupListItemDto>();
            if (groups == null)
            {
                return kept;
            }
            foreach (var group in groups)
            {
                if (group == null)
                {
                    continue;
                }
                if (_query.Length == 0
                    || (group.Name != null
                        && group.Name.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    kept.Add(group);
                }
            }
            return kept;
        }

        // ----- discover -------------------------------------------------------------------------

        private VisualElement BuildDiscover()
        {
            var host = new VisualElement();

            host.Add(Hint(_query.Length == 0
                ? "Groups this project lets you find. Type in the search box above to narrow it."
                : "Results for \"" + Fmt.Truncate(_query, 28) + "\"."));

            var slot = new VisualElement();
            var pager = new Pager(PageSize);

            host.Add(VisibilityFilter());
            host.Add(slot);
            host.Add(pager);

            pager.PageRequested += page => LoadSearch(slot, pager, page);
            LoadSearch(slot, pager, 1);

            host.Add(InvitePanel());
            host.Add(PlayerLookupPanel());
            return host;
        }

        /// <summary>
        /// Segmented visibility filter. It lives in the pane rather than the toolbar because it only
        /// means something for the search — a toolbar dropdown that does nothing on the other tab
        /// is worse than no dropdown at all.
        /// </summary>
        private VisualElement VisibilityFilter()
        {
            var row = new VisualElement();
            row.AddToClassList("sc-grp-filters");

            var caption = new Label("Visibility");
            caption.AddToClassList("sc-grp-filters__label");
            row.Add(caption);

            foreach (var option in Visibilities)
            {
                string value = option == "Any" ? null : option;
                bool active = string.Equals(value, _visibility, StringComparison.Ordinal);

                var btn = new Button(() =>
                {
                    if (string.Equals(value, _visibility, StringComparison.Ordinal))
                    {
                        return;
                    }
                    _visibility = value;
                    _tabs.Invalidate(1);
                })
                {
                    text = option,
                };
                btn.AddToClassList("sc-btn");
                btn.AddToClassList("sc-grp-filters__btn");
                if (active)
                {
                    btn.AddToClassList("sc-btn--primary");
                }
                row.Add(btn);
            }
            return row;
        }

        private void LoadSearch(VisualElement slot, Pager pager, int page)
        {
            string query = _query.Length == 0 ? null : _query;
            ViewBind.Load(
                () => Sdk.Groups.SearchAsync(query, _visibility, page, PageSize),
                slot,
                data =>
                {
                    pager.SetTotal(data.TotalCount, page);
                    return GroupList(new List<GroupListItemDto>(data.Items), false);
                },
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Group search",
                    Snippet = SearchSnippet,
                    ServiceName = "Groups",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        pager.SetTotal(0, 1);
                        return ZeroState.Cards(LucideIcon.Search,
                            query == null
                                ? "No group is public in this project yet."
                                : "Nothing public matches \"" + Fmt.Truncate(query, 24) + "\".",
                            3);
                    },
                });
        }

        /// <summary>
        /// Invites arrive out of band and the service has no endpoint that lists them, so redeeming
        /// one is a paste-and-go panel rather than a hidden dialog.
        /// </summary>
        private VisualElement InvitePanel()
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-grp-panel");
            card.WithTitle("Have an invite?", Meta.Accent);

            card.Body.Add(Hint("There is no call that lists the invites a player received — they come "
                + "from a chat message, a deep link or your own backend. Paste what you were given: "
                + "a shareable key secret, or the id of a direct invite."));

            var groupId = Field("Group id");
            var groupRow = new VisualElement();
            groupRow.AddToClassList("sc-grp-inline");
            groupRow.Add(groupId);
            card.Body.Add(groupRow);

            var key = Field("Invite key secret");
            var keyRow = new VisualElement();
            keyRow.AddToClassList("sc-grp-inline");
            keyRow.Add(key);
            keyRow.Add(GlyphButton("Join with key", LucideIcon.KeyRound,
                () => JoinByKey(groupId.value, key.value)));
            card.Body.Add(keyRow);

            var invite = Field("Direct invite id");
            var inviteRow = new VisualElement();
            inviteRow.AddToClassList("sc-grp-inline");
            inviteRow.Add(invite);
            inviteRow.Add(GlyphButton("Accept", LucideIcon.Check,
                () => AnswerInvite(groupId.value, invite.value, true), "sc-btn--primary"));
            inviteRow.Add(GlyphButton("Decline", LucideIcon.X,
                () => AnswerInvite(groupId.value, invite.value, false)));
            card.Body.Add(inviteRow);

            return card;
        }

        private VisualElement PlayerLookupPanel()
        {
            var card = new Card();
            card.AddToClassList("sc-grp-panel");
            card.WithTitle("Another player's groups");
            card.Body.Add(Hint("Whether this answers at all depends on the project's visibility rules."));

            var results = new VisualElement();

            var profileId = Field("Profile id");
            var row = new VisualElement();
            row.AddToClassList("sc-grp-inline");
            row.Add(profileId);
            row.Add(GlyphButton("Look up", LucideIcon.Search, () =>
            {
                string id = profileId.value == null ? null : profileId.value.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    Warn("Type a profile id first.");
                    return;
                }
                LoadPlayerGroups(results, id);
            }));

            card.Body.Add(row);
            card.Body.Add(results);
            return card;
        }

        private void LoadPlayerGroups(VisualElement slot, string profileId)
        {
            ViewBind.Load(
                () => Sdk.Groups.GetPlayerGroupsAsync(profileId, 1, PageSize),
                slot,
                data => GroupList(new List<GroupListItemDto>(data.Items), false),
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Player groups",
                    Snippet = MyGroupsSnippet,
                    ServiceName = "Groups",
                    AllowRetry = true,
                    EmptyMessage = "That player is in no group this account may see.",
                });
        }

        // ----- the list rows --------------------------------------------------------------------

        private VisualElement GroupList(List<GroupListItemDto> groups, bool mine)
        {
            var list = new VisualElement();
            foreach (var group in groups)
            {
                list.Add(GroupRow(group, mine));
            }
            return list;
        }

        private VisualElement GroupRow(GroupListItemDto group, bool mine)
        {
            string groupId = group.GroupId;
            bool joined = mine || _myGroupIds.Contains(groupId);

            var row = new ListRow();
            row.AddToClassList("sc-grp-row");
            row.SetLead(new Avatar(38f).SetInitialsFor(Fmt.OrDash(group.Name)));
            row.SetTitle(Fmt.OrDash(group.Name));
            row.SetSubtitle(string.IsNullOrEmpty(group.Description)
                ? "no description"
                : Fmt.Truncate(group.Description, 72));

            var trailing = new VisualElement();
            trailing.AddToClassList("sc-row-actions");
            trailing.Add(new Badge(group.MemberCount + "/" + group.MaxMembers, ChipTone.Neutral));
            if (!string.IsNullOrEmpty(group.Visibility))
            {
                trailing.Add(new Badge(group.Visibility, ChipTone.Info));
            }
            if (joined && !mine)
            {
                trailing.Add(new Badge("joined", ChipTone.Ok));
            }

            if (!joined)
            {
                // An open group can be joined outright; anything else needs a request the moderators
                // answer, so the button says which one it is.
                bool open = IsOpenPolicy(group.JoinPolicy);
                var join = GlyphButton(open ? "Join" : "Request",
                    open ? LucideIcon.DoorOpen : LucideIcon.UserPlus,
                    () => JoinOrRequest(groupId, open),
                    "sc-btn--primary");
                trailing.Add(join);
            }

            trailing.Add(GlyphButton("Open", LucideIcon.ChevronRight, () => OpenGroup(groupId)));
            row.SetTrailing(trailing);

            // The whole row opens the group; the buttons inside keep their own meaning.
            row.RegisterCallback<ClickEvent>(e =>
            {
                for (var t = e.target as VisualElement; t != null && t != row; t = t.parent)
                {
                    if (t is Button)
                    {
                        return;
                    }
                }
                OpenGroup(groupId);
            });
            return row;
        }

        private async void JoinOrRequest(string groupId, bool open)
        {
            if (!open)
            {
                var request = Sdk.Groups.CreateJoinRequestAsync(groupId);
                var requested = await AwaitData(request, "Groups · join request");
                Report(requested, "Join request sent — a moderator has to approve it", "Join request");
                if (requested.Ok && !_closed)
                {
                    _requestedGroupId = groupId;
                    if (groupId == _groupId)
                    {
                        RenderActions();
                    }
                }
                return;
            }

            var outcome = await Await(Sdk.Groups.JoinAsync(groupId), "Groups · join");
            Report(outcome, "Joined the group", "Join");
            if (!outcome.Ok || _closed)
            {
                return;
            }

            _myGroupIds.Add(groupId);
            if (_myGroupsTotal >= 0)
            {
                _myGroupsTotal++;
            }
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            if (groupId == _groupId)
            {
                LoadGroup();
            }
        }

        private async void JoinByKey(string groupId, string secret)
        {
            string id = groupId == null ? null : groupId.Trim();
            string key = secret == null ? null : secret.Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
            {
                Warn("Both the group id and the key secret are needed.");
                return;
            }

            var outcome = await Await(
                Sdk.Groups.JoinByKeyAsync(id, new JoinByKeyDto { SecretKey = key }),
                "Groups · join by key");
            Report(outcome, "Joined with the key", "Join with key");
            if (!outcome.Ok || _closed)
            {
                return;
            }
            _myGroupIds.Add(id);
            _tabs.Invalidate(0);
            OpenGroup(id);
        }

        private async void AnswerInvite(string groupId, string inviteId, bool accept)
        {
            string id = groupId == null ? null : groupId.Trim();
            string invite = inviteId == null ? null : inviteId.Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(invite))
            {
                Warn("Both the group id and the invite id are needed.");
                return;
            }

            var op = accept
                ? Sdk.Groups.AcceptInviteAsync(id, invite)
                : Sdk.Groups.DeclineInviteAsync(id, invite);
            var outcome = await Await(op, "Groups · invite answer");
            Report(outcome, accept ? "Invite accepted" : "Invite declined", "Invite answer");
            if (!outcome.Ok || _closed || !accept)
            {
                return;
            }
            _myGroupIds.Add(id);
            _tabs.Invalidate(0);
            OpenGroup(id);
        }

        // ----- creating a group -----------------------------------------------------------------

        private void OpenCreateDialog()
        {
            FormDialog.Open(Popup, "New group",
                new[]
                {
                    FormField.Text("name", "Name", null, true).WithPlaceholder("Shown in every list"),
                    FormField.LongText("description", "Description"),
                    FormField.Choice("visibility", "Visibility", new[] { "public", "private" }, "public")
                        .WithPlaceholder("Only public groups turn up under Discover"),
                    FormField.Choice("joinPolicy", "Join policy",
                            new[] { "open", "request", "invite" }, "open")
                        .WithPlaceholder("open joins outright · request needs approval · invite is closed"),
                    FormField.Int("maxMembers", "Max members", 50),
                    FormField.Bool("chat", "Create a chat channel too", true),
                    FormField.Bool("chatAutoJoin", "Put new members into that chat", true)
                        .WithPlaceholder("Off means a player joins the group but not its chat, "
                            + "and cannot post there until they join the channel by hand"),
                },
                "Create", CreateGroup);
        }

        private async void CreateGroup(FormValues values)
        {
            var op = Sdk.Groups.CreateAsync(new CreateGroupDto
            {
                Name = values.Text("name"),
                Description = values.Text("description"),
                Visibility = values.Choice("visibility"),
                JoinPolicy = values.Choice("joinPolicy"),
                MaxMembers = Math.Max(1, values.Int("maxMembers")),
                CreateChat = values.Bool("chat"),
                // Group membership and chat membership are separate records on the server: without
                // this flag everyone who joins the group later stays outside its chat.
                AutoJoinMembers = values.Bool("chatAutoJoin"),
            });

            var outcome = await AwaitData(op, "Groups · create");
            Report(outcome, "Group created", "Create group");
            if (!outcome.Ok || _closed)
            {
                return;
            }

            var created = op.Result.Data;
            if (created != null && !string.IsNullOrEmpty(created.GroupId))
            {
                _myGroupIds.Add(created.GroupId);
            }
            if (_myGroupsTotal >= 0)
            {
                _myGroupsTotal++;
            }

            // The new group belongs in the list the player already knows, so this lands there rather
            // than jumping straight into the group's page.
            CloseGroup();
            _tabs.Invalidate(0);
            _tabs.Select(0);
        }

        // ----- one group ------------------------------------------------------------------------

        private void OpenGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            _groupId = groupId;
            _group = null;
            _roles = new RoleDto[0];
            _groupTabs = null;
            _membershipResolved = _myGroupIds.Contains(groupId) || MyGroupsFullyKnown;

            ShowDetail(true);
            LoadGroup();

            if (!_membershipResolved)
            {
                ProbeMembership(groupId);
            }
        }

        private void CloseGroup()
        {
            ClearOpenGroup();
            if (_detail != null)
            {
                _detail.Clear();
            }
            ShowDetail(false);
            SetBrowseStatus();
        }

        private void ClearOpenGroup()
        {
            _groupId = null;
            _group = null;
            _roles = new RoleDto[0];
            _groupTabs = null;
            _heroRole = null;
            _actionSlot = null;
            _sectionsSlot = null;
            _tabMembers = -1;
            _tabRequests = -1;
            _tabRoles = -1;
            _tabBans = -1;
            _membershipResolved = false;
        }

        private void ShowDetail(bool on)
        {
            var browse = on ? DisplayStyle.None : DisplayStyle.Flex;
            if (_toolbar != null)
            {
                _toolbar.style.display = browse;
            }
            if (_tabs != null)
            {
                _tabs.style.display = browse;
            }
            if (_panes != null)
            {
                _panes.style.display = browse;
            }
            if (_detail != null)
            {
                _detail.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Content.scrollOffset = Vector2.zero;
        }

        private void LoadGroup()
        {
            if (_detail == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }

            _detail.Clear();
            _detail.Add(BackRow());

            var slot = new VisualElement();
            _detail.Add(slot);

            string groupId = _groupId;
            ViewBind.Load(
                () => Sdk.Groups.GetAsync(groupId),
                slot,
                BuildGroupBody,
                null,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Group",
                    Snippet = GroupSnippet,
                    ServiceName = "Group",
                    // No ConfigurationRequest: a 404 here means "no such group" — usually a mistyped
                    // id pasted into the invite panel — not "the project has nothing set up".
                    AllowRetry = true,
                });
        }

        private VisualElement BackRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sc-grp-back");
            row.Add(GlyphButton("All groups", LucideIcon.ArrowLeft, CloseGroup));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            row.Add(spacer);

            // The toolbar is hidden on this sub-screen, and its two useful buttons are the ones a
            // reader wants most here — the group's own reads are what the snippets describe.
            if (Popup != null)
            {
                row.Add(GlyphButton("SDK call", LucideIcon.Code, OpenSdkCalls));
            }
            row.Add(GlyphButton("Reload", LucideIcon.RefreshCw, LoadGroup));
            return row;
        }

        private VisualElement BuildGroupBody(GroupDto group)
        {
            _group = group;
            SetStatus(Fmt.Truncate(Fmt.OrDash(group.Name), 24), ChipTone.Ok);

            // The role picker on the Members tab needs the group's roles, and the Roles tab may never
            // be opened — so they are fetched once here as well.
            LoadRoles(group.GroupId);

            var col = new VisualElement();
            col.Add(GroupCard(group));

            col.Add(new KpiRow()
                .Add("Members", LucideIcon.Users, group.MemberCount + " / " + group.MaxMembers)
                .Add("Visibility", LucideIcon.Eye, Fmt.OrDash(group.Visibility))
                .Add("Join policy", LucideIcon.DoorOpen, Fmt.OrDash(group.JoinPolicy))
                .Add("Created", LucideIcon.CalendarDays, Fmt.Date(group.CreatedAt)));

            _actionSlot = new VisualElement();
            col.Add(_actionSlot);
            RenderActions();

            _sectionsSlot = new VisualElement();
            col.Add(_sectionsSlot);
            RenderSections();
            return col;
        }

        private VisualElement GroupCard(GroupDto group)
        {
            var card = new Card(Meta.Accent);

            var head = new VisualElement();
            head.AddToClassList("sc-grp-hero");
            head.Add(new Avatar(46f).SetInitialsFor(Fmt.OrDash(group.Name)));

            var texts = new VisualElement();
            texts.AddToClassList("sc-grp-hero__texts");
            var name = new Label(Fmt.OrDash(group.Name));
            name.enableRichText = false;
            name.AddToClassList("sc-grp-hero__name");
            name.style.color = Meta.Accent;
            texts.Add(name);

            _heroRole = new Label(MembershipLine(group));
            _heroRole.AddToClassList("sc-grp-hero__role");
            texts.Add(_heroRole);
            head.Add(texts);
            card.WithHeader(head);

            if (!string.IsNullOrEmpty(group.Description))
            {
                var description = new Label(group.Description);
                description.enableRichText = false;
                description.AddToClassList("sc-fs-hint");
                card.Body.Add(description);
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            if (group.Tag != null)
            {
                foreach (var tag in group.Tag)
                {
                    if (!string.IsNullOrEmpty(tag))
                    {
                        chips.Add(new Chip(tag, ChipTone.Accent));
                    }
                }
            }
            if (!string.IsNullOrEmpty(group.OwnerId))
            {
                chips.Add(new Chip("owner " + Fmt.Id(group.OwnerId, 8), ChipTone.Neutral));
            }
            card.Body.Add(chips);

            var ids = new VisualElement();
            ids.AddToClassList("sc-kv-list");
            ids.Add(Kv("Group id", Fmt.OrDash(group.GroupId), group.GroupId));
            if (!string.IsNullOrEmpty(group.Metadata))
            {
                ids.Add(Kv("Metadata", Fmt.Truncate(group.Metadata, 48), group.Metadata));
            }
            card.Body.Add(ids);

            card.Body.Add(ChatRow(group));
            return card;
        }

        private string MembershipLine(GroupDto group)
        {
            if (!_membershipResolved)
            {
                return "checking your membership…";
            }
            if (IsOwner(group))
            {
                return "you own this group";
            }
            return IsMember(group.GroupId) ? "you are a member" : "you are not a member";
        }

        private VisualElement ChatRow(GroupDto group)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            var chat = group.ChatConfig;
            bool hasChannel = chat != null && !string.IsNullOrEmpty(chat.ChannelId);

            if (hasChannel)
            {
                row.Add(new Chip("chat " + Fmt.Id(chat.ChannelId, 8), ChipTone.Ok));
                row.Add(new CopyButton(chat.ChannelId, Toasts, "channel id"));

                var hint = new Label("Open the Chats service and paste this channel id to talk in it.");
                hint.AddToClassList("sc-fs-hint");
                var box = new VisualElement();
                box.Add(row);
                box.Add(hint);
                return box;
            }

            // The "add one" button lives in the action row below, which is the part that knows
            // whether this player is a member.
            row.Add(new Chip("no chat", ChipTone.Neutral));
            return row;
        }

        private async void CreateChat(string groupId)
        {
            var outcome = await AwaitData(Sdk.Groups.CreateChatAsync(groupId), "Groups · create chat");
            Report(outcome, "Chat channel created", "Create chat");
            if (outcome.Ok && !_closed && groupId == _groupId)
            {
                LoadGroup();
            }
        }

        // ----- what the player may do with this group -------------------------------------------

        /// <summary>
        /// Fills the action row from what is known right now. It is re-run when the membership probe
        /// or the profile lookup lands, so the row settles instead of guessing.
        /// </summary>
        private void RenderActions()
        {
            if (_actionSlot == null || _group == null)
            {
                return;
            }

            var group = _group;
            _actionSlot.Clear();

            var row = new VisualElement();
            row.AddToClassList("sc-grp-actions");

            if (!_membershipResolved)
            {
                row.Add(new Chip("checking your membership…", ChipTone.Neutral));
                _actionSlot.Add(row);
                return;
            }

            bool member = IsMember(group.GroupId);
            bool owner = IsOwner(group);

            if (!member)
            {
                bool open = IsOpenPolicy(group.JoinPolicy);
                if (open)
                {
                    row.Add(GlyphButton("Join group", LucideIcon.DoorOpen,
                        () => JoinOrRequest(group.GroupId, true), "sc-btn--primary"));
                }
                else if (string.Equals(group.GroupId, _requestedGroupId, StringComparison.Ordinal))
                {
                    // Non-members cannot see the Requests tab, so this chip is the only trace of
                    // the request they just sent.
                    row.Add(new Chip("request pending", ChipTone.Warn));
                }
                else if (string.Equals(group.JoinPolicy, "invite", StringComparison.OrdinalIgnoreCase))
                {
                    row.Add(new Chip("invite only", ChipTone.Warn));
                }
                else
                {
                    row.Add(GlyphButton("Request to join", LucideIcon.UserPlus,
                        () => JoinOrRequest(group.GroupId, false), "sc-btn--primary"));
                }

                if (!open)
                {
                    row.Add(GlyphButton("Join with a key", LucideIcon.KeyRound,
                        () => OpenJoinByKeyDialog(group.GroupId)));
                }

                _actionSlot.Add(row);
                _actionSlot.Add(Hint("Only members see the roles, invites, bans and join requests of "
                    + "a group."));
                return;
            }

            row.Add(GlyphButton("Edit group", LucideIcon.Pencil, () => OpenEditDialog(group)));
            row.Add(GlyphButton("Invite a player", LucideIcon.UserPlus,
                () => OpenInviteDialog(group.GroupId)));

            if (group.ChatConfig == null || string.IsNullOrEmpty(group.ChatConfig.ChannelId))
            {
                row.Add(GlyphButton("Add a chat channel", LucideIcon.MessageCircle,
                    () => CreateChat(group.GroupId)));
            }

            if (owner)
            {
                row.Add(GlyphButton("Delete group", LucideIcon.Trash,
                    () => ConfirmDeleteGroup(group), "sc-btn--danger"));
            }
            else
            {
                row.Add(GlyphButton("Leave group", LucideIcon.LogOut,
                    () => ConfirmLeave(group), "sc-btn--danger"));
            }

            _actionSlot.Add(row);
            _actionSlot.Add(Hint(owner
                ? "The owner cannot leave — deleting the group is the way out, and it takes the "
                  + "members, roles and chat with it."
                : "Your role decides which of these the server accepts; anything it does not allow "
                  + "comes back as an error rather than silently doing nothing."));
        }

        private void OpenEditDialog(GroupDto group)
        {
            FormDialog.Open(Popup, "Edit group",
                new[]
                {
                    FormField.Text("name", "Name", group.Name, true),
                    FormField.LongText("description", "Description", group.Description),
                    FormField.Choice("visibility", "Visibility", new[] { "public", "private" },
                        string.IsNullOrEmpty(group.Visibility) ? "public" : group.Visibility),
                    FormField.Choice("joinPolicy", "Join policy",
                        new[] { "open", "request", "invite" },
                        string.IsNullOrEmpty(group.JoinPolicy) ? "open" : group.JoinPolicy),
                    FormField.Int("maxMembers", "Max members", group.MaxMembers),
                },
                "Save", values => UpdateGroup(group.GroupId, values));
        }

        private async void UpdateGroup(string groupId, FormValues values)
        {
            // Every field is prefilled from the group, so the whole set is sent back — the dialog
            // shows exactly what will be stored.
            var dto = new UpdateGroupDto
            {
                Name = values.Text("name"),
                Description = values.Text("description"),
                Visibility = values.Choice("visibility"),
                JoinPolicy = values.Choice("joinPolicy"),
                MaxMembers = Math.Max(1, values.Int("maxMembers")),
            };

            var outcome = await AwaitData(Sdk.Groups.UpdateAsync(groupId, dto), "Groups · update");
            Report(outcome, "Group updated", "Update group");
            if (!outcome.Ok || _closed)
            {
                return;
            }
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            if (groupId == _groupId)
            {
                LoadGroup();
            }
        }

        private void ConfirmLeave(GroupDto group)
        {
            ConfirmDialog.Open(Popup, "Leave group",
                "You stop being a member of \"" + Fmt.OrDash(group.Name) + "\". Whether you can come "
                + "back depends on its join policy.",
                "Leave",
                () => Leave(group.GroupId));
        }

        private async void Leave(string groupId)
        {
            var outcome = await Await(Sdk.Groups.LeaveAsync(groupId), "Groups · leave");
            Report(outcome, "Left the group", "Leave");
            if (!outcome.Ok || _closed)
            {
                return;
            }
            _myGroupIds.Remove(groupId);
            if (_myGroupsTotal > 0)
            {
                _myGroupsTotal--;
            }
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            CloseGroup();
        }

        private void ConfirmDeleteGroup(GroupDto group)
        {
            // Retyping the name is the gate; a group with no name falls back to a plain confirm
            // rather than asking the player to type an em dash.
            ConfirmDialog.Open(Popup, "Delete group",
                "Permanent, and it takes the members, roles, bans and chat channel with it.",
                "Delete",
                () => DeleteGroup(group.GroupId),
                string.IsNullOrWhiteSpace(group.Name) ? null : group.Name);
        }

        private async void DeleteGroup(string groupId)
        {
            var outcome = await Await(Sdk.Groups.DeleteAsync(groupId), "Groups · delete");
            Report(outcome, "Group deleted", "Delete group");
            if (!outcome.Ok || _closed)
            {
                return;
            }
            _myGroupIds.Remove(groupId);
            if (_myGroupsTotal > 0)
            {
                _myGroupsTotal--;
            }
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            CloseGroup();
        }

        private void OpenJoinByKeyDialog(string groupId)
        {
            FormDialog.Open(Popup, "Join with a key",
                new[] { FormField.Text("secret", "Secret key", null, true) },
                "Join",
                values => JoinByKey(groupId, values.Text("secret")));
        }

        // ----- membership and ownership ---------------------------------------------------------

        /// <summary>True once every group the player belongs to is in <see cref="_myGroupIds"/>.</summary>
        private bool MyGroupsFullyKnown => _myGroupsTotal >= 0 && _myGroupIds.Count >= _myGroupsTotal;

        private bool IsMember(string groupId)
        {
            return !string.IsNullOrEmpty(groupId) && _myGroupIds.Contains(groupId);
        }

        private bool IsOwner(GroupDto group)
        {
            return group != null && !string.IsNullOrEmpty(group.OwnerId)
                && _myProfileIds.Contains(group.OwnerId);
        }

        /// <summary>
        /// Settles membership for a group opened from the search, where the first page of "My groups"
        /// may not have covered it. One extra read, and only when the answer is genuinely unknown.
        /// </summary>
        private async void ProbeMembership(string groupId)
        {
            var op = Sdk.Groups.GetMyGroupsAsync(1, MembershipProbeSize);
            if (op == null)
            {
                return;
            }
            await op.Task();
            if (_closed)
            {
                return;
            }

            var result = op.Result;
            if (result != null && result.IsSuccess && result.Data != null)
            {
                RememberMyGroups(result.Data);
            }

            if (_groupId != groupId)
            {
                return;
            }
            _membershipResolved = true;

            // Only the parts that read membership are redrawn — re-issuing the group read to move
            // one line of text would be silly.
            if (_heroRole != null && _group != null)
            {
                _heroRole.text = MembershipLine(_group);
            }
            RenderActions();
            RenderSections();
        }

        private async void ResolveOwnProfiles()
        {
            var op = Sdk.PlayerAccount.GetProfilesAsync();
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed || result == null || !result.IsSuccess || result.Data == null)
            {
                return;
            }

            bool added = false;
            foreach (var profile in result.Data)
            {
                if (profile != null && !string.IsNullOrEmpty(profile.Id))
                {
                    added |= _myProfileIds.Add(profile.Id);
                }
            }
            if (added && _group != null)
            {
                RenderActions();
            }
        }

        private async void LoadRoles(string groupId)
        {
            var op = Sdk.Groups.GetRolesAsync(groupId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed || _groupId != groupId || result == null || !result.IsSuccess)
            {
                return;
            }
            _roles = result.Data ?? new RoleDto[0];
        }

        // ----- the group's own sections ---------------------------------------------------------

        /// <summary>
        /// The five section lists as tabs rather than one long scroll: each is paged, and stacking
        /// them would fire five requests the moment a group opens. Everything past Members is for
        /// members only — for anyone else those calls are a guaranteed 403, which is also why this
        /// is re-run rather than built once when the membership probe lands.
        /// </summary>
        private void RenderSections()
        {
            if (_sectionsSlot == null || _group == null)
            {
                return;
            }
            _sectionsSlot.Clear();

            _groupTabs = new Tabs();
            _groupTabs.AddToClassList("sc-grp-tabs");

            _tabMembers = 0;
            _groupTabs.Add("Members", LucideIcon.Users, MembersPane);

            if (IsMember(_group.GroupId))
            {
                _tabRequests = 1;
                _tabRoles = 2;
                _tabBans = 4;
                _groupTabs.Add("Requests", LucideIcon.Inbox, JoinRequestsPane)
                    .Add("Roles", LucideIcon.KeyRound, RolesPane)
                    .Add("Invites", LucideIcon.UserPlus, InvitesPane)
                    .Add("Bans", LucideIcon.Ban, BansPane);
            }
            else
            {
                _tabRequests = -1;
                _tabRoles = -1;
                _tabBans = -1;
            }
            _sectionsSlot.Add(_groupTabs);
        }

        private void InvalidateSection(int index)
        {
            if (_groupTabs != null && index >= 0)
            {
                _groupTabs.Invalidate(index);
            }
        }

        // ----- members --------------------------------------------------------------------------

        private VisualElement MembersPane()
        {
            var box = new VisualElement();

            var slot = new VisualElement();
            var pager = new Pager(PageSize);
            box.Add(slot);
            box.Add(pager);

            pager.PageRequested += page => LoadMembers(slot, pager, page);
            LoadMembers(slot, pager, 1);
            return box;
        }

        private void LoadMembers(VisualElement slot, Pager pager, int page)
        {
            string groupId = _groupId;
            ViewBind.Load(
                () => Sdk.Groups.GetMembersAsync(groupId, page, PageSize),
                slot,
                data =>
                {
                    pager.SetTotal(data.TotalCount, page);
                    var table = new DataTable(MemberColumns()).WithZebra().WithMaxHeight(420f);
                    table.Bind(data.Items, o => ((MemberDto)o).IsOwner);
                    return table;
                },
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Members",
                    Snippet = MembersSnippet,
                    ServiceName = "Group",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        pager.SetTotal(0, 1);
                        return ZeroState.Table(MemberColumns(),
                            "Nobody has joined yet. Invite someone from the group's actions above.", 3);
                    },
                });
        }

        private DataColumn[] MemberColumns()
        {
            bool member = IsMember(_groupId);
            return new[]
            {
                new DataColumn
                {
                    Header = "PROFILE", Grow = 2f,
                    SortKey = o => ((MemberDto)o).ProfileId,
                    Cell = o =>
                    {
                        var m = (MemberDto)o;
                        var row = new VisualElement();
                        row.AddToClassList("sc-row-actions");
                        row.style.justifyContent = Justify.FlexStart;
                        row.Add(new Avatar(28f).SetInitialsFor(m.ProfileId));
                        var id = new Label(Fmt.Id(m.ProfileId, 12));
                        id.enableRichText = false;
                        id.style.marginLeft = 8f;
                        row.Add(id);
                        if (_myProfileIds.Contains(m.ProfileId))
                        {
                            var you = new Badge("you", ChipTone.Info);
                            you.style.marginLeft = 6f;
                            row.Add(you);
                        }
                        return row;
                    },
                },
                new DataColumn
                {
                    Header = "ROLE", Grow = 1f,
                    SortKey = o => ((MemberDto)o).RoleName,
                    Cell = o =>
                    {
                        var m = (MemberDto)o;
                        return m.IsOwner
                            ? new Chip("owner", ChipTone.Warn)
                            : new Chip(Fmt.OrDash(m.RoleName), ChipTone.Neutral);
                    },
                },
                new DataColumn
                {
                    Header = "JOINED", Grow = 1f, Align = "right",
                    SortKey = o => ((MemberDto)o).JoinedAt,
                    Cell = o => new Label(Fmt.Date(((MemberDto)o).JoinedAt)),
                },
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = member ? 176 : 80, Align = "right",
                    Cell = o =>
                    {
                        var m = (MemberDto)o;
                        var row = new VisualElement();
                        row.AddToClassList("sc-row-actions");

                        // The owner cannot be demoted or kicked, so those controls are simply absent
                        // rather than present and failing. Neither can a non-member moderate.
                        if (m.IsOwner)
                        {
                            row.Add(new Badge("owner", ChipTone.Warn));
                            return row;
                        }
                        if (!member)
                        {
                            return row;
                        }

                        var role = new Button(() => OpenRoleDialog(m)) { text = "Role" };
                        role.AddToClassList("sc-btn");
                        row.Add(role);

                        var kick = new Button(() => ConfirmKick(m)) { text = "Kick" };
                        kick.AddToClassList("sc-btn");
                        kick.AddToClassList("sc-btn--danger");
                        row.Add(kick);
                        return row;
                    },
                },
            };
        }

        private void OpenRoleDialog(MemberDto member)
        {
            // Offer the group's real roles when they are loaded; fall back to a free-text id when
            // the roles call has not answered (or the group has none).
            var options = new List<string>();
            foreach (var role in _roles)
            {
                if (role != null && !string.IsNullOrEmpty(role.RoleId))
                {
                    options.Add(role.Name + " · " + role.RoleId);
                }
            }

            FormField field = options.Count > 0
                ? FormField.Choice("role", "Role", options.ToArray(), options[0])
                : FormField.Text("role", "Role id", member.RoleId, true);

            FormDialog.Open(Popup, "Change role", new[] { field }, "Apply", values =>
            {
                string picked = options.Count > 0 ? values.Choice("role") : values.Text("role");
                SetRole(member.ProfileId, ExtractRoleId(picked));
            });
        }

        private static string ExtractRoleId(string picked)
        {
            if (string.IsNullOrEmpty(picked))
            {
                return null;
            }
            int marker = picked.LastIndexOf(" · ", StringComparison.Ordinal);
            return marker < 0 ? picked.Trim() : picked.Substring(marker + 3).Trim();
        }

        private async void SetRole(string profileId, string roleId)
        {
            var outcome = await AwaitData(
                Sdk.Groups.UpdateMemberRoleAsync(_groupId, profileId,
                    new UpdateMemberRoleDto { RoleId = roleId }),
                "Groups · member role");
            Report(outcome, "Role updated", "Role change");
            if (outcome.Ok && !_closed)
            {
                InvalidateSection(_tabMembers);
            }
        }

        private void ConfirmKick(MemberDto member)
        {
            ConfirmDialog.Open(Popup, "Kick member",
                "Removes " + Fmt.Id(member.ProfileId, 10) + " from the group. They can rejoin unless "
                + "you ban them.",
                "Kick",
                () => Write(Sdk.Groups.KickMemberAsync(_groupId, member.ProfileId),
                    "Member kicked", "Kick", () => InvalidateSection(_tabMembers)));
        }

        // ----- roles ----------------------------------------------------------------------------

        private VisualElement RolesPane()
        {
            var box = new VisualElement();
            box.Add(SectionBar("Roles", "New role", LucideIcon.Plus, OpenCreateRoleDialog));

            var slot = new VisualElement();
            box.Add(slot);

            string groupId = _groupId;
            ViewBind.Load(
                () => Sdk.Groups.GetRolesAsync(groupId),
                slot,
                roles =>
                {
                    _roles = roles;
                    return RoleList(roles);
                },
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Roles",
                    Snippet = RolesSnippet,
                    ServiceName = "Group",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.KeyRound, "No custom roles",
                        "Every member sits on the group's default role. Add one to hand out permissions "
                        + "like inviting, kicking or approving requests.",
                        "New role", OpenCreateRoleDialog),
                });
            return box;
        }

        private VisualElement RoleList(RoleDto[] roles)
        {
            var list = new VisualElement();
            foreach (var role in roles)
            {
                var row = new ListRow();
                row.SetLead(new Avatar(30f).SetInitialsFor(Fmt.OrDash(role.Name)));
                row.SetTitle(Fmt.OrDash(role.Name));
                row.SetSubtitle(DescribePermissions(role.Permissions));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-row-actions");
                trailing.Add(GlyphButton("Edit", LucideIcon.Pencil, () => OpenEditRoleDialog(role)));
                trailing.Add(GlyphButton("Delete", LucideIcon.Trash,
                    () => ConfirmDeleteRole(role), "sc-btn--danger"));
                row.SetTrailing(trailing);
                list.Add(row);
            }
            return list;
        }

        private static string DescribePermissions(GroupPermissionsDto permissions)
        {
            if (permissions == null)
            {
                return "no permissions";
            }
            var granted = new List<string>();
            if (permissions.CanInvite)
            {
                granted.Add("invite");
            }
            if (permissions.CanKick)
            {
                granted.Add("kick");
            }
            if (permissions.CanApplyRequests)
            {
                granted.Add("approve requests");
            }
            if (permissions.CanUpdateGroupData)
            {
                granted.Add("edit group");
            }
            if (permissions.CanDeleteGroup)
            {
                granted.Add("delete group");
            }
            if (permissions.CanAddRoleToOthers)
            {
                granted.Add("assign roles");
            }
            return granted.Count == 0 ? "no permissions" : string.Join(" · ", granted.ToArray());
        }

        private static FormField[] RoleFields(RoleDto role)
        {
            var permissions = role != null ? role.Permissions : null;
            return new[]
            {
                FormField.Text("name", "Name", role != null ? role.Name : null, true),
                FormField.Bool("invite", "Can invite", permissions != null && permissions.CanInvite),
                FormField.Bool("kick", "Can kick", permissions != null && permissions.CanKick),
                FormField.Bool("requests", "Can approve join requests",
                    permissions != null && permissions.CanApplyRequests),
                FormField.Bool("edit", "Can edit the group",
                    permissions != null && permissions.CanUpdateGroupData),
                FormField.Bool("delete", "Can delete the group",
                    permissions != null && permissions.CanDeleteGroup),
                FormField.Bool("roles", "Can assign roles",
                    permissions != null && permissions.CanAddRoleToOthers),
            };
        }

        private static GroupPermissionsDto PermissionsFrom(FormValues values)
        {
            return new GroupPermissionsDto
            {
                CanInvite = values.Bool("invite"),
                CanKick = values.Bool("kick"),
                CanApplyRequests = values.Bool("requests"),
                CanUpdateGroupData = values.Bool("edit"),
                CanDeleteGroup = values.Bool("delete"),
                CanAddRoleToOthers = values.Bool("roles"),
            };
        }

        private void OpenCreateRoleDialog()
        {
            if (string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "New role", RoleFields(null), "Create", values =>
                WriteData(
                    Sdk.Groups.CreateRoleAsync(_groupId, new CreateRoleDto
                    {
                        Name = values.Text("name"),
                        Permissions = PermissionsFrom(values),
                    }),
                    "Role created", "Create role", ReloadRoles));
        }

        private void OpenEditRoleDialog(RoleDto role)
        {
            FormDialog.Open(Popup, "Edit role", RoleFields(role), "Save", values =>
                WriteData(
                    Sdk.Groups.UpdateRoleAsync(_groupId, role.RoleId, new UpdateRoleDto
                    {
                        Name = values.Text("name"),
                        Permissions = PermissionsFrom(values),
                    }),
                    "Role updated", "Update role", ReloadRoles));
        }

        private void ConfirmDeleteRole(RoleDto role)
        {
            ConfirmDialog.Open(Popup, "Delete role",
                "Members holding \"" + Fmt.OrDash(role.Name) + "\" fall back to the default role.",
                "Delete",
                () => Write(Sdk.Groups.DeleteRoleAsync(_groupId, role.RoleId),
                    "Role deleted", "Delete role", ReloadRoles));
        }

        /// <summary>Members carry a role name, so a role write refreshes both lists.</summary>
        private void ReloadRoles()
        {
            InvalidateSection(_tabRoles);
            InvalidateSection(_tabMembers);
        }

        // ----- join requests --------------------------------------------------------------------

        private VisualElement JoinRequestsPane()
        {
            var box = new VisualElement();
            box.Add(Hint("Players asking to join a request-only group wait here for a moderator."));

            var slot = new VisualElement();
            var pager = new Pager(PageSize);
            box.Add(slot);
            box.Add(pager);

            pager.PageRequested += page => LoadJoinRequests(slot, pager, page);
            LoadJoinRequests(slot, pager, 1);
            return box;
        }

        private void LoadJoinRequests(VisualElement slot, Pager pager, int page)
        {
            string groupId = _groupId;
            ViewBind.Load(
                () => Sdk.Groups.GetJoinRequestsAsync(groupId, "pending", page, PageSize),
                slot,
                data =>
                {
                    pager.SetTotal(data.TotalCount, page);
                    var list = new VisualElement();
                    foreach (var request in data.Items)
                    {
                        var row = new ListRow();
                        row.SetLead(new Avatar(30f).SetInitialsFor(request.SourcePlayerId));
                        row.SetTitle(Fmt.Id(request.SourcePlayerId, 14));
                        row.SetSubtitle("asked " + Fmt.Date(request.CreatedAt)
                            + " · expires " + Fmt.Date(request.ExpiresAt));

                        var trailing = new VisualElement();
                        trailing.AddToClassList("sc-row-actions");
                        trailing.Add(new Badge(Fmt.OrDash(request.Status), ChipTone.Warn));

                        string requestId = request.RequestId;
                        trailing.Add(GlyphButton("Approve", LucideIcon.Check,
                            () => Write(Sdk.Groups.ApproveJoinRequestAsync(_groupId, requestId),
                                "Request approved", "Approve", ReloadRequests),
                            "sc-btn--primary"));
                        trailing.Add(GlyphButton("Reject", LucideIcon.X,
                            () => Write(Sdk.Groups.RejectJoinRequestAsync(_groupId, requestId),
                                "Request rejected", "Reject", ReloadRequests)));

                        row.SetTrailing(trailing);
                        list.Add(row);
                    }
                    return list;
                },
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Join requests",
                    Snippet = JoinSnippet,
                    ServiceName = "Group",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        pager.SetTotal(0, 1);
                        return ZeroState.Panel(LucideIcon.Inbox, "No pending requests",
                            "An approved request adds the player to the group, so the members list "
                            + "moves with this one.");
                    },
                });
        }

        private void ReloadRequests()
        {
            InvalidateSection(_tabRequests);
            InvalidateSection(_tabMembers);
        }

        // ----- invites --------------------------------------------------------------------------

        private VisualElement InvitesPane()
        {
            var box = new VisualElement();
            box.Add(Hint("There is no endpoint that lists a group's invites, so this is the write side "
                + "only. Both calls hand back the id you need to take the invite away again — keep it, "
                + "because nothing else will tell you."));

            var row = new VisualElement();
            row.AddToClassList("sc-grp-actions");
            row.Add(GlyphButton("Invite a player", LucideIcon.UserPlus,
                () => OpenInviteDialog(_groupId), "sc-btn--primary"));
            row.Add(GlyphButton("Create an invite key", LucideIcon.KeyRound,
                () => OpenInviteKeyDialog(_groupId)));
            row.Add(GlyphButton("Revoke an invite", LucideIcon.UserX, OpenRevokeInviteDialog));
            row.Add(GlyphButton("Delete an invite key", LucideIcon.Trash, OpenDeleteKeyDialog));
            box.Add(row);
            return box;
        }

        private void OpenInviteDialog(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Invite a player",
                new[]
                {
                    FormField.Text("playerId", "Target player id", null, true)
                        .WithPlaceholder("The player's profile id"),
                    FormField.Int("days", "Expires in (days)", 7),
                },
                "Invite",
                values => InviteResult(Sdk.Groups.CreateInviteAsync(groupId, new CreateInviteDto
                {
                    TargetPlayerId = values.Text("playerId"),
                    InviteType = "direct",
                    ExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, values.Int("days"))),
                })));
        }

        private async void InviteResult(AsyncOperation<RestApiResult<InviteDto>> op)
        {
            var outcome = await AwaitData(op, "Groups · invite");
            if (!outcome.Ok)
            {
                Report(outcome, null, "Invite");
                return;
            }
            if (_closed)
            {
                return;
            }

            var invite = op.Result.Data;
            if (Toasts != null)
            {
                Toasts.Ok("Invite created");
            }
            // The invite id only comes back here, and revoking needs it, so it is handed over rather
            // than left in a log line.
            if (Popup != null && invite != null)
            {
                var body = new VisualElement();
                body.Add(Hint("Keep this invite id — revoking the invite needs it."));

                var ids = new VisualElement();
                ids.AddToClassList("sc-kv-list");
                ids.Add(Kv("Invite id", Fmt.OrDash(invite.InviteId), invite.InviteId));
                ids.Add(Kv("Target", Fmt.Id(invite.TargetPlayerId, 12), invite.TargetPlayerId));
                ids.Add(Kv("Expires", Fmt.DateTime2(invite.ExpiresAt), null));
                body.Add(ids);
                Popup.Open(body, "Invite created");
            }
        }

        private void OpenInviteKeyDialog(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Create an invite key",
                new[] { FormField.Int("days", "Expires in (days)", 30) },
                "Create",
                values => InviteKeyResult(Sdk.Groups.CreateInviteKeyAsync(groupId, new CreateInviteKeyDto
                {
                    InviteType = "key",
                    ExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, values.Int("days"))),
                })));
        }

        private async void InviteKeyResult(AsyncOperation<RestApiResult<InviteKeyDto>> op)
        {
            var outcome = await AwaitData(op, "Groups · invite key");
            if (!outcome.Ok)
            {
                Report(outcome, null, "Invite key");
                return;
            }
            if (_closed)
            {
                return;
            }

            var key = op.Result.Data;
            if (Toasts != null)
            {
                Toasts.Ok("Invite key created");
            }
            if (Popup != null && key != null)
            {
                var body = new VisualElement();
                body.Add(Hint("Share the secret; anyone holding it can join from the Discover tab."));

                var ids = new VisualElement();
                ids.AddToClassList("sc-kv-list");
                ids.Add(Kv("Secret", Fmt.OrDash(key.SecretKey), key.SecretKey));
                ids.Add(Kv("Key id", Fmt.OrDash(key.InviteKeyId), key.InviteKeyId));
                ids.Add(Kv("Expires", Fmt.DateTime2(key.ExpiresAt), null));
                body.Add(ids);
                Popup.Open(body, "Invite key created");
            }
        }

        private void OpenRevokeInviteDialog()
        {
            FormDialog.Open(Popup, "Revoke an invite",
                new[] { FormField.Text("inviteId", "Invite id", null, true) },
                "Revoke",
                values => Write(Sdk.Groups.RevokeInviteAsync(_groupId, values.Text("inviteId")),
                    "Invite revoked", "Revoke invite", null),
                true);
        }

        private void OpenDeleteKeyDialog()
        {
            FormDialog.Open(Popup, "Delete an invite key",
                new[] { FormField.Text("keyId", "Invite key id", null, true) },
                "Delete",
                values => Write(Sdk.Groups.DeleteInviteKeyAsync(_groupId, values.Text("keyId")),
                    "Invite key deleted", "Delete invite key", null),
                true);
        }

        // ----- bans -----------------------------------------------------------------------------

        private VisualElement BansPane()
        {
            var box = new VisualElement();
            box.Add(SectionBar("Banned players", "Ban a player", LucideIcon.Ban, OpenBanDialog,
                "sc-btn--danger"));

            var slot = new VisualElement();
            var pager = new Pager(PageSize);
            box.Add(slot);
            box.Add(pager);

            pager.PageRequested += page => LoadBans(slot, pager, page);
            LoadBans(slot, pager, 1);
            return box;
        }

        private void LoadBans(VisualElement slot, Pager pager, int page)
        {
            string groupId = _groupId;
            ViewBind.Load(
                () => Sdk.Groups.GetBansAsync(groupId, page, PageSize),
                slot,
                data =>
                {
                    pager.SetTotal(data.TotalCount, page);
                    var list = new VisualElement();
                    foreach (var ban in data.Items)
                    {
                        var row = new ListRow();
                        row.SetLead(new Avatar(30f).SetInitialsFor(ban.ProfileId));
                        row.SetTitle(Fmt.Id(ban.ProfileId, 14));
                        row.SetSubtitle(string.IsNullOrEmpty(ban.Reason)
                            ? "banned " + Fmt.Date(ban.BannedAt)
                            : ban.Reason + " · " + Fmt.Date(ban.BannedAt));

                        var trailing = new VisualElement();
                        trailing.AddToClassList("sc-row-actions");
                        string profileId = ban.ProfileId;
                        trailing.Add(GlyphButton("Unban", LucideIcon.UserCheck,
                            () => Write(Sdk.Groups.UnbanPlayerAsync(_groupId, profileId),
                                "Player unbanned", "Unban", ReloadBans)));
                        row.SetTrailing(trailing);
                        list.Add(row);
                    }
                    return list;
                },
                d => d == null || d.Items == null || d.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Bans",
                    Snippet = BansSnippet,
                    ServiceName = "Group",
                    AllowRetry = true,
                    EmptyView = () =>
                    {
                        pager.SetTotal(0, 1);
                        return ZeroState.Panel(LucideIcon.Ban, "Nobody is banned",
                            "A ban keeps a player out for good, unlike a kick. Banning takes an account "
                            + "id; unbanning takes a profile id.");
                    },
                });
        }

        private void OpenBanDialog()
        {
            if (string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Ban a player",
                new[]
                {
                    FormField.Text("accountId", "Account id", null, true)
                        .WithPlaceholder("An account id here — unbanning takes a profile id"),
                    FormField.Text("reason", "Reason"),
                },
                "Ban",
                values => Write(
                    Sdk.Groups.BanPlayerAsync(_groupId, new BanPlayerDto
                    {
                        AccountId = values.Text("accountId"),
                        Reason = values.Text("reason"),
                    }),
                    "Player banned", "Ban", ReloadBans),
                true);
        }

        private void ReloadBans()
        {
            InvalidateSection(_tabBans);
            InvalidateSection(_tabMembers);
        }

        // ----- small shared pieces ---------------------------------------------------------------

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.enableRichText = false;
            label.AddToClassList("sc-fs-hint");
            return label;
        }

        private static TextField Field(string label)
        {
            var field = new TextField(label);
            field.AddToClassList("sc-field");
            field.AddToClassList("sc-grp-inline__field");
            return field;
        }

        /// <summary>Button with a leading Lucide glyph — the icon has to be its own label.</summary>
        private static Button GlyphButton(string text, string glyph, Action onClick, string tone = null)
        {
            var btn = new Button(() => onClick?.Invoke());
            btn.AddToClassList("sc-btn");
            btn.AddToClassList("sc-grp-btn");
            if (!string.IsNullOrEmpty(tone))
            {
                btn.AddToClassList(tone);
            }

            var g = new Label(glyph);
            g.AddToClassList("sc-grp-btn__glyph");
            g.AddToClassList("sc-icon");
            btn.Add(g);

            var t = new Label(text);
            t.enableRichText = false;
            btn.Add(t);
            return btn;
        }

        private static VisualElement SectionBar(string title, string action, string glyph,
                                                Action onClick, string tone = null)
        {
            var bar = new VisualElement();
            bar.AddToClassList("sc-row-actions");
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.Add(new SectionHeader(title));
            bar.Add(GlyphButton(action, glyph, onClick, tone));
            return bar;
        }

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }

        private static bool IsOpenPolicy(string joinPolicy)
        {
            return string.Equals(joinPolicy, "open", StringComparison.OrdinalIgnoreCase);
        }

        private void SetBrowseStatus()
        {
            if (_groupId != null)
            {
                return;
            }
            if (_myGroupsTotal < 0)
            {
                SetStatus(null);
                return;
            }
            SetStatus(_myGroupsTotal + (_myGroupsTotal == 1 ? " group" : " groups"),
                _myGroupsTotal > 0 ? ChipTone.Ok : ChipTone.Neutral);
        }

        private void Warn(string message)
        {
            if (Toasts != null)
            {
                Toasts.Fail(message);
            }
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async void Write(AsyncOperation<RestApiResult> op, string success, string label,
                                 Action onOk)
        {
            var outcome = await Await(op, "Groups · " + label);
            Report(outcome, success, label);
            if (outcome.Ok && !_closed && onOk != null)
            {
                onOk();
            }
        }

        private async void WriteData<T>(AsyncOperation<RestApiResult<T>> op, string success,
                                        string label, Action onOk)
        {
            var outcome = await AwaitData(op, "Groups · " + label);
            Report(outcome, success, label);
            if (outcome.Ok && !_closed && onOk != null)
            {
                onOk();
            }
        }

        private void Report(Outcome outcome, string success, string label)
        {
            if (Toasts == null)
            {
                return;
            }
            if (outcome.Ok)
            {
                if (!string.IsNullOrEmpty(success))
                {
                    Toasts.Ok(success);
                }
                return;
            }
            Toasts.Fail(label + " failed · " + outcome.Message);
        }

        private async Task<Outcome> Await(AsyncOperation<RestApiResult> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private async Task<Outcome> AwaitData<T>(AsyncOperation<RestApiResult<T>> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private Outcome Fold(RestApiResult result, string label)
        {
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
    }
}

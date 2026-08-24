using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Groups.Dto.Request;
using MirraCloud.Core.Groups.Dto.Response;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Groups screen: the player's groups, a public search, one group in full (members, roles, bans,
    /// invites, join requests), and every write the service exposes.
    /// <para>
    /// Most of the reads here are paged (<c>PaginatedResult</c>), so each section owns a
    /// <see cref="Pager"/> and asks the server for one page at a time rather than pretending the
    /// whole list fits. Picking a group on the first two tabs is what fills the Group tab.
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
    CreateChat = true          // asks for a chat channel up front
});
await created.Task();

await sdk.Groups.UpdateAsync(groupId, new UpdateGroupDto { Description = ""Now with loot"" }).Task();
await sdk.Groups.DeleteAsync(groupId).Task();

// A chat can also be added to an existing group:
var chat = sdk.Groups.CreateChatAsync(groupId);";

        private const int PageSize = 20;

        private Tabs _tabs;
        private string _groupId;
        private string _query = string.Empty;
        private string _visibility;
        private RoleDto[] _roles = new RoleDto[0];

        public GroupsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _query = string.Empty;
            _visibility = null;

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

            UseToolbar()
                .WithSearch("Search public groups by name", OnSearch)
                .WithFilter("Visibility", new[] { "Any", "public", "private" }, OnVisibility, "Any")
                .WithSpacer()
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("My groups", LucideIcon.Users, BuildMyGroups)
                .Add("Search", LucideIcon.Search, BuildSearch)
                .Add("Group", LucideIcon.Shield, BuildGroup)
                .Add("Actions", LucideIcon.Sparkles, BuildActions);
        }

        private void OnSearch(string text)
        {
            _query = text == null ? string.Empty : text.Trim();
            _tabs.Invalidate(1);
            _tabs.Select(1);
        }

        private void OnVisibility(string value)
        {
            _visibility = value == "Any" ? null : value;
            _tabs.Invalidate(1);
        }

        private void Select(string groupId)
        {
            _groupId = groupId;
            _roles = new RoleDto[0];
            _tabs.Invalidate(2);
            _tabs.Select(2);
        }

        // ----- my groups ------------------------------------------------------------------------

        private VisualElement BuildMyGroups()
        {
            var host = new VisualElement();
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
                    pager.SetTotal(data.TotalCount, page);
                    SetStatus(data.TotalCount + (data.TotalCount == 1 ? " group" : " groups"),
                        data.TotalCount > 0 ? ChipTone.Ok : ChipTone.Neutral);
                    return GroupList(data.Items, true);
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
                        pager.SetTotal(0, 1);
                        return ZeroState.Panel(LucideIcon.Users, "Not in any group yet",
                            "Create one from the Actions tab, or find an existing one under Search and "
                            + "join it. A group can carry its own chat channel, roles and bans.",
                            "Create a group", () => _tabs.Select(3));
                    },
                });
        }

        // ----- search ---------------------------------------------------------------------------

        private VisualElement BuildSearch()
        {
            var host = new VisualElement();

            var hint = new Label(_query.Length == 0
                ? "Type in the search box above to look for public groups. The visibility filter narrows it."
                : "Results for \"" + Fmt.Truncate(_query, 28) + "\""
                    + (_visibility == null ? string.Empty : " · " + _visibility));
            hint.AddToClassList("sc-fs-hint");
            host.Add(hint);

            var slot = new VisualElement();
            var pager = new Pager(PageSize);
            host.Add(slot);
            host.Add(pager);

            pager.PageRequested += page => LoadSearch(slot, pager, page);
            LoadSearch(slot, pager, 1);
            return host;
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
                    return GroupList(data.Items, false);
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
                                ? "No public groups in this project yet."
                                : "Nothing public matches \"" + Fmt.Truncate(query, 24) + "\".",
                            3);
                    },
                });
        }

        private VisualElement GroupList(GroupListItemDto[] groups, bool mine)
        {
            var list = new VisualElement();
            foreach (var group in groups)
            {
                var row = new ListRow();
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

                string groupId = group.GroupId;
                if (!mine)
                {
                    // An open group can be joined outright; anything else needs a request the
                    // moderators answer, so the button says which one it is.
                    bool open = string.Equals(group.JoinPolicy, "open", StringComparison.OrdinalIgnoreCase);
                    var join = new Button(() => JoinOrRequest(groupId, open))
                    {
                        text = open ? "Join" : "Request",
                    };
                    join.AddToClassList("sc-btn");
                    join.AddToClassList("sc-btn--primary");
                    trailing.Add(join);
                }

                var open2 = new Button(() => Select(groupId)) { text = "Open" };
                open2.AddToClassList("sc-btn");
                trailing.Add(open2);

                row.SetTrailing(trailing);
                list.Add(row);
            }
            return list;
        }

        private async void JoinOrRequest(string groupId, bool open)
        {
            var op = open ? Sdk.Groups.JoinAsync(groupId) : null;
            if (!open)
            {
                var request = Sdk.Groups.CreateJoinRequestAsync(groupId);
                var requestOutcome = await AwaitData(request, "Groups · join request");
                Report(requestOutcome, "Join request sent", "Join request");
                return;
            }

            var outcome = await Await(op, "Groups · join");
            Report(outcome, "Joined the group", "Join");
            if (outcome.Ok)
            {
                _tabs.Invalidate(0);
            }
        }

        // ----- one group ------------------------------------------------------------------------

        private VisualElement BuildGroup()
        {
            var host = new VisualElement();

            var picker = new VisualElement();
            picker.AddToClassList("sc-chat-lookup");
            var field = new TextField { label = "Group id", value = _groupId ?? string.Empty };
            field.AddToClassList("sc-field");
            picker.Add(field);
            var load = new Button(() => Select(field.value == null ? null : field.value.Trim())) { text = "Load" };
            load.AddToClassList("sc-btn");
            picker.Add(load);
            host.Add(picker);

            if (string.IsNullOrEmpty(_groupId))
            {
                host.Add(ZeroState.Panel(LucideIcon.Shield, "No group selected",
                    "Pick one under My groups or Search, or paste a group id above. This tab then shows "
                    + "its members, roles, bans, invites and join requests.",
                    "Browse my groups", () => _tabs.Select(0)));
                return host;
            }

            var slot = new VisualElement();
            host.Add(slot);
            ViewBind.Load(
                () => Sdk.Groups.GetAsync(_groupId),
                slot,
                BuildGroupBody,
                null,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Group",
                    Snippet = GroupSnippet,
                    ServiceName = "Group",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                });
            return host;
        }

        private VisualElement BuildGroupBody(GroupDto group)
        {
            SetStatus(Fmt.Truncate(Fmt.OrDash(group.Name), 24), ChipTone.Ok);

            var col = new VisualElement();
            col.Add(GroupCard(group));

            col.Add(new KpiRow()
                .Add("Members", LucideIcon.Users, group.MemberCount + " / " + group.MaxMembers)
                .Add("Visibility", LucideIcon.Eye, Fmt.OrDash(group.Visibility))
                .Add("Join policy", LucideIcon.DoorOpen, Fmt.OrDash(group.JoinPolicy))
                .Add("Created", LucideIcon.CalendarDays, Fmt.Date(group.CreatedAt)));

            col.Add(MembersSection());
            col.Add(RolesSection());
            col.Add(BansSection());
            col.Add(InvitesSection());
            col.Add(JoinRequestsSection());
            return col;
        }

        private VisualElement GroupCard(GroupDto group)
        {
            var card = new Card(Meta.Accent);
            card.WithTitle(Fmt.OrDash(group.Name), Meta.Accent);

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

            row.Add(new Chip("no chat", ChipTone.Neutral));
            var create = new Button(() => CreateChat(group.GroupId)) { text = "Create a chat channel" };
            create.AddToClassList("sc-btn");
            row.Add(create);
            return row;
        }

        private async void CreateChat(string groupId)
        {
            var outcome = await AwaitData(Sdk.Groups.CreateChatAsync(groupId), "Groups · create chat");
            Report(outcome, "Chat channel created", "Create chat");
            if (outcome.Ok)
            {
                _tabs.Invalidate(2);
            }
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

        // ----- members --------------------------------------------------------------------------

        private VisualElement MembersSection()
        {
            var box = new VisualElement();
            box.Add(new SectionHeader("Members"));

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
                            "Nobody has joined yet. Invite someone from the sections below.", 3);
                    },
                });
        }

        private DataColumn[] MemberColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "PROFILE", Grow = 2f,
                    SortKey = o => ((MemberDto)o).ProfileId,
                    Cell = o =>
                    {
                        var member = (MemberDto)o;
                        var row = new VisualElement();
                        row.AddToClassList("sc-row-actions");
                        row.style.justifyContent = Justify.FlexStart;
                        row.Add(new Avatar(28f).SetInitialsFor(member.ProfileId));
                        var id = new Label(Fmt.Id(member.ProfileId, 12));
                        id.enableRichText = false;
                        id.style.marginLeft = 8f;
                        row.Add(id);
                        return row;
                    },
                },
                new DataColumn
                {
                    Header = "ROLE", Grow = 1f,
                    SortKey = o => ((MemberDto)o).RoleName,
                    Cell = o =>
                    {
                        var member = (MemberDto)o;
                        return member.IsOwner
                            ? new Chip("owner", ChipTone.Warn)
                            : new Chip(Fmt.OrDash(member.RoleName), ChipTone.Neutral);
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
                    Header = string.Empty, FixedWidth = true, Px = 176, Align = "right",
                    Cell = o =>
                    {
                        var member = (MemberDto)o;
                        var row = new VisualElement();
                        row.AddToClassList("sc-row-actions");

                        // The owner cannot be demoted or kicked, so those controls are simply absent
                        // rather than present and failing.
                        if (member.IsOwner)
                        {
                            row.Add(new Badge("owner", ChipTone.Warn));
                            return row;
                        }

                        var role = new Button(() => OpenRoleDialog(member)) { text = "Role" };
                        role.AddToClassList("sc-btn");
                        row.Add(role);

                        var kick = new Button(() => ConfirmKick(member)) { text = "Kick" };
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
            if (Popup == null)
            {
                return;
            }

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
                string roleId = ExtractRoleId(picked);
                SetRole(member.ProfileId, roleId);
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
                Sdk.Groups.UpdateMemberRoleAsync(_groupId, profileId, new UpdateMemberRoleDto { RoleId = roleId }),
                "Groups · member role");
            Report(outcome, "Role updated", "Role change");
            if (outcome.Ok)
            {
                _tabs.Invalidate(2);
            }
        }

        private void ConfirmKick(MemberDto member)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Kick member",
                "Removes " + Fmt.Id(member.ProfileId, 10) + " from the group. They can rejoin unless "
                + "you ban them.",
                "Kick",
                () => RunAndReload(Sdk.Groups.KickMemberAsync(_groupId, member.ProfileId),
                    "Member kicked", "Kick"));
        }

        // ----- roles ----------------------------------------------------------------------------

        private VisualElement RolesSection()
        {
            var box = new VisualElement();

            var header = new VisualElement();
            header.AddToClassList("sc-row-actions");
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(new SectionHeader("Roles"));
            var add = new Button(OpenCreateRoleDialog) { text = "New role" };
            add.AddToClassList("sc-btn");
            header.Add(add);
            box.Add(header);

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

                var edit = new Button(() => OpenEditRoleDialog(role)) { text = "Edit" };
                edit.AddToClassList("sc-btn");
                trailing.Add(edit);

                var remove = new Button(() => ConfirmDeleteRole(role)) { text = "Delete" };
                remove.AddToClassList("sc-btn");
                remove.AddToClassList("sc-btn--danger");
                trailing.Add(remove);

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
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "New role", RoleFields(null), "Create", values =>
                RunDataAndReload(
                    Sdk.Groups.CreateRoleAsync(_groupId, new CreateRoleDto
                    {
                        Name = values.Text("name"),
                        Permissions = PermissionsFrom(values),
                    }),
                    "Role created", "Create role"));
        }

        private void OpenEditRoleDialog(RoleDto role)
        {
            if (Popup == null)
            {
                return;
            }
            FormDialog.Open(Popup, "Edit role", RoleFields(role), "Save", values =>
                RunDataAndReload(
                    Sdk.Groups.UpdateRoleAsync(_groupId, role.RoleId, new UpdateRoleDto
                    {
                        Name = values.Text("name"),
                        Permissions = PermissionsFrom(values),
                    }),
                    "Role updated", "Update role"));
        }

        private void ConfirmDeleteRole(RoleDto role)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Delete role",
                "Members holding \"" + Fmt.OrDash(role.Name) + "\" fall back to the default role.",
                "Delete",
                () => RunAndReload(Sdk.Groups.DeleteRoleAsync(_groupId, role.RoleId),
                    "Role deleted", "Delete role"));
        }

        // ----- bans -----------------------------------------------------------------------------

        private VisualElement BansSection()
        {
            var box = new VisualElement();

            var header = new VisualElement();
            header.AddToClassList("sc-row-actions");
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(new SectionHeader("Bans"));
            var add = new Button(OpenBanDialog) { text = "Ban a player" };
            add.AddToClassList("sc-btn");
            add.AddToClassList("sc-btn--danger");
            header.Add(add);
            box.Add(header);

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
                        var unban = new Button(() => RunAndReload(
                            Sdk.Groups.UnbanPlayerAsync(_groupId, profileId), "Player unbanned", "Unban"))
                        {
                            text = "Unban",
                        };
                        unban.AddToClassList("sc-btn");
                        trailing.Add(unban);
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
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Ban a player",
                new[]
                {
                    FormField.Text("accountId", "Account id", null, true),
                    FormField.Text("reason", "Reason"),
                },
                "Ban",
                values => RunAndReload(
                    Sdk.Groups.BanPlayerAsync(_groupId, new BanPlayerDto
                    {
                        AccountId = values.Text("accountId"),
                        Reason = values.Text("reason"),
                    }),
                    "Player banned", "Ban"),
                true);
        }

        // ----- invites --------------------------------------------------------------------------

        private VisualElement InvitesSection()
        {
            var box = new VisualElement();
            box.Add(new SectionHeader("Invites"));

            var hint = new Label("There is no endpoint to list a group's invites, so this section is the "
                + "write side only: a direct invite for one player, or a shareable key anyone can redeem. "
                + "Both come back with the id you need to revoke them.");
            hint.AddToClassList("sc-fs-hint");
            box.Add(hint);

            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            var direct = new Button(OpenInviteDialog) { text = "Invite a player" };
            direct.AddToClassList("sc-btn");
            direct.AddToClassList("sc-btn--primary");
            row.Add(direct);

            var key = new Button(OpenInviteKeyDialog) { text = "Create an invite key" };
            key.AddToClassList("sc-btn");
            row.Add(key);

            var revoke = new Button(OpenRevokeInviteDialog) { text = "Revoke an invite" };
            revoke.AddToClassList("sc-btn");
            row.Add(revoke);

            var deleteKey = new Button(OpenDeleteKeyDialog) { text = "Delete an invite key" };
            deleteKey.AddToClassList("sc-btn");
            row.Add(deleteKey);

            box.Add(row);
            return box;
        }

        private void OpenInviteDialog()
        {
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Invite a player",
                new[]
                {
                    FormField.Text("playerId", "Target player id", null, true),
                    FormField.Int("days", "Expires in (days)", 7),
                },
                "Invite",
                values => InviteResult(Sdk.Groups.CreateInviteAsync(_groupId, new CreateInviteDto
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
                var text = new Label("Keep this invite id — revoking the invite needs it.");
                text.AddToClassList("sc-fs-hint");
                body.Add(text);

                var ids = new VisualElement();
                ids.AddToClassList("sc-kv-list");
                ids.Add(Kv("Invite id", Fmt.OrDash(invite.InviteId), invite.InviteId));
                ids.Add(Kv("Target", Fmt.Id(invite.TargetPlayerId, 12), invite.TargetPlayerId));
                ids.Add(Kv("Expires", Fmt.DateTime2(invite.ExpiresAt), null));
                body.Add(ids);
                Popup.Open(body, "Invite created");
            }
        }

        private void OpenInviteKeyDialog()
        {
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Create an invite key",
                new[] { FormField.Int("days", "Expires in (days)", 30) },
                "Create",
                values => InviteKeyResult(Sdk.Groups.CreateInviteKeyAsync(_groupId, new CreateInviteKeyDto
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
            var key = op.Result.Data;
            if (Toasts != null)
            {
                Toasts.Ok("Invite key created");
            }
            if (Popup != null && key != null)
            {
                var body = new VisualElement();
                var text = new Label("Share the secret; anyone holding it can join with JoinByKeyAsync.");
                text.AddToClassList("sc-fs-hint");
                body.Add(text);

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
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Revoke an invite",
                new[] { FormField.Text("inviteId", "Invite id", null, true) },
                "Revoke",
                values => RunAndReload(Sdk.Groups.RevokeInviteAsync(_groupId, values.Text("inviteId")),
                    "Invite revoked", "Revoke invite"),
                true);
        }

        private void OpenDeleteKeyDialog()
        {
            if (Popup == null || string.IsNullOrEmpty(_groupId))
            {
                return;
            }
            FormDialog.Open(Popup, "Delete an invite key",
                new[] { FormField.Text("keyId", "Invite key id", null, true) },
                "Delete",
                values => RunAndReload(Sdk.Groups.DeleteInviteKeyAsync(_groupId, values.Text("keyId")),
                    "Invite key deleted", "Delete invite key"),
                true);
        }

        // ----- join requests --------------------------------------------------------------------

        private VisualElement JoinRequestsSection()
        {
            var box = new VisualElement();
            box.Add(new SectionHeader("Join requests"));

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
                        var approve = new Button(() => RunAndReload(
                            Sdk.Groups.ApproveJoinRequestAsync(_groupId, requestId),
                            "Request approved", "Approve")) { text = "Approve" };
                        approve.AddToClassList("sc-btn");
                        approve.AddToClassList("sc-btn--primary");
                        trailing.Add(approve);

                        var reject = new Button(() => RunAndReload(
                            Sdk.Groups.RejectJoinRequestAsync(_groupId, requestId),
                            "Request rejected", "Reject")) { text = "Reject" };
                        reject.AddToClassList("sc-btn");
                        trailing.Add(reject);

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
                            "Players asking to join a request-only group appear here for a moderator to "
                            + "approve or reject.");
                    },
                });
        }

        // ----- actions --------------------------------------------------------------------------

        private VisualElement BuildActions()
        {
            var col = new VisualElement();

            var hint = new Label("The group lifecycle and the calls that do not belong to one section. "
                + "Anything taking a group id uses the one selected on the Group tab when you leave the "
                + "field empty.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Create a group",
                    "Visibility and join policy are project-defined strings, not SDK enums.",
                    LucideIcon.Plus)
                .WithFields(
                    FormField.Text("name", "Name", "Night Owls", true),
                    FormField.Text("description", "Description"),
                    FormField.Choice("visibility", "Visibility", new[] { "public", "private" }, "public"),
                    FormField.Choice("joinPolicy", "Join policy",
                        new[] { "open", "request", "invite" }, "open"),
                    FormField.Int("maxMembers", "Max members", 50),
                    FormField.Bool("chat", "Create a chat channel too", true))
                .WithSnippet(LifecycleSnippet)
                .OnRun("Create", CreateGroup));

            col.Add(new ActionCard("Update the group", "Only the fields you fill in are sent.",
                    LucideIcon.Pencil)
                .WithFields(
                    FormField.Text("groupId", "Group id (blank = selected)"),
                    FormField.Text("name", "Name"),
                    FormField.Text("description", "Description"),
                    FormField.Int("maxMembers", "Max members", 0))
                .WithSnippet(LifecycleSnippet)
                .OnRun("Update", UpdateGroup));

            col.Add(new ActionCard("Join an open group", "Works when the join policy is open.",
                    LucideIcon.DoorOpen)
                .WithFields(FormField.Text("groupId", "Group id (blank = selected)"))
                .WithSnippet(JoinSnippet)
                .OnRun("Join", v => Run(Sdk.Groups.JoinAsync(GroupIdFrom(v)), "Joined the group", 0)));

            col.Add(new ActionCard("Ask to join",
                    "For a request-only group: creates a request a moderator answers.",
                    LucideIcon.UserPlus)
                .WithFields(FormField.Text("groupId", "Group id (blank = selected)"))
                .WithSnippet(JoinSnippet)
                .OnRun("Request", v => RunData(Sdk.Groups.CreateJoinRequestAsync(GroupIdFrom(v)),
                    "Join request sent", 0)));

            col.Add(new ActionCard("Join with a key", "Redeems an invite key's secret.", LucideIcon.KeyRound)
                .WithFields(
                    FormField.Text("groupId", "Group id (blank = selected)"),
                    FormField.Text("secret", "Secret key", null, true))
                .WithSnippet(InvitesSnippet)
                .OnRun("Join", v => Run(
                    Sdk.Groups.JoinByKeyAsync(GroupIdFrom(v), new JoinByKeyDto { SecretKey = v.Text("secret") }),
                    "Joined with the key", 0)));

            col.Add(new ActionCard("Answer an invite", "Accept or decline an invite you received.",
                    LucideIcon.BadgeCheck)
                .WithFields(
                    FormField.Text("groupId", "Group id (blank = selected)"),
                    FormField.Text("inviteId", "Invite id", null, true),
                    FormField.Choice("answer", "Answer", new[] { "accept", "decline" }, "accept"))
                .WithSnippet(InvitesSnippet)
                .OnRun("Send", v => Run(
                    v.Choice("answer") == "accept"
                        ? Sdk.Groups.AcceptInviteAsync(GroupIdFrom(v), v.Text("inviteId"))
                        : Sdk.Groups.DeclineInviteAsync(GroupIdFrom(v), v.Text("inviteId")),
                    "Invite " + v.Choice("answer") + "ed", 0)));

            col.Add(new ActionCard("Leave the group", "Removes you; the owner has to transfer or delete.",
                    LucideIcon.LogOut)
                .WithFields(FormField.Text("groupId", "Group id (blank = selected)"))
                .WithSnippet(JoinSnippet)
                .OnRun("Leave", v => Run(Sdk.Groups.LeaveAsync(GroupIdFrom(v)), "Left the group", 0), true));

            col.Add(new ActionCard("Delete the group",
                    "Permanent, and takes the members, roles and chat with it.", LucideIcon.Trash)
                .WithFields(FormField.Text("groupId", "Group id (blank = selected)"))
                .WithSnippet(LifecycleSnippet)
                .OnRun("Delete", v => Run(Sdk.Groups.DeleteAsync(GroupIdFrom(v)),
                    "Group deleted", 0), true));

            col.Add(new ActionCard("Read another player's groups",
                    "Whether this answers depends on the project's visibility rules.", LucideIcon.Users)
                .WithFields(FormField.Text("profileId", "Profile id", null, true))
                .WithSnippet(MyGroupsSnippet)
                .OnRun("Read", ReadPlayerGroups));

            return col;
        }

        private string GroupIdFrom(FormValues values)
        {
            string typed = values.Text("groupId");
            return string.IsNullOrWhiteSpace(typed) ? _groupId : typed.Trim();
        }

        private async Task<ActionOutcome> CreateGroup(FormValues values)
        {
            var op = Sdk.Groups.CreateAsync(new CreateGroupDto
            {
                Name = values.Text("name"),
                Description = values.Text("description"),
                Visibility = values.Choice("visibility"),
                JoinPolicy = values.Choice("joinPolicy"),
                MaxMembers = Math.Max(1, values.Int("maxMembers")),
                CreateChat = values.Bool("chat"),
            });

            var outcome = await AwaitData(op, "Groups · create");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var created = op.Result.Data;
            _tabs.Invalidate(0);
            if (created != null && !string.IsNullOrEmpty(created.GroupId))
            {
                _groupId = created.GroupId;
                _tabs.Invalidate(2);
            }
            if (Toasts != null)
            {
                Toasts.Ok("Group created");
            }

            var detail = new VisualElement();
            detail.AddToClassList("sc-kv-list");
            detail.Add(Kv("Group id", created != null ? Fmt.OrDash(created.GroupId) : Fmt.Dash,
                created != null ? created.GroupId : null));
            return ActionOutcome.Success("Created and selected on the Group tab", detail);
        }

        private async Task<ActionOutcome> UpdateGroup(FormValues values)
        {
            string groupId = GroupIdFrom(values);
            if (string.IsNullOrEmpty(groupId))
            {
                return ActionOutcome.Failure("Select a group first, or type its id.");
            }

            // Only non-empty fields are sent, so this card can nudge one property without wiping
            // the rest of the group.
            var dto = new UpdateGroupDto();
            string name = values.Text("name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                dto.Name = name.Trim();
            }
            string description = values.Text("description");
            if (!string.IsNullOrWhiteSpace(description))
            {
                dto.Description = description.Trim();
            }
            int max = values.Int("maxMembers");
            if (max > 0)
            {
                dto.MaxMembers = max;
            }

            var outcome = await AwaitData(Sdk.Groups.UpdateAsync(groupId, dto), "Groups · update");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            _tabs.Invalidate(2);
            _tabs.Invalidate(0);
            if (Toasts != null)
            {
                Toasts.Ok("Group updated");
            }
            return ActionOutcome.Success("Group updated");
        }

        private async Task<ActionOutcome> ReadPlayerGroups(FormValues values)
        {
            var op = Sdk.Groups.GetPlayerGroupsAsync(values.Text("profileId"), 1, PageSize);
            var outcome = await AwaitData(op, "Groups · player groups");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var page = op.Result.Data;
            int total = page != null ? page.TotalCount : 0;
            if (total == 0)
            {
                return ActionOutcome.Success("That player is in no visible group");
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            if (page.Items != null)
            {
                foreach (var group in page.Items)
                {
                    chips.Add(new Chip(Fmt.Truncate(Fmt.OrDash(group.Name), 22), ChipTone.Accent));
                }
            }
            return ActionOutcome.Success(total + (total == 1 ? " group" : " groups"), chips);
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async Task<ActionOutcome> Run(AsyncOperation<RestApiResult> op, string success, int tab)
        {
            var outcome = await Await(op, "Groups write");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            _tabs.Invalidate(tab);
            _tabs.Invalidate(2);
            return ActionOutcome.Success(success);
        }

        private async Task<ActionOutcome> RunData<T>(AsyncOperation<RestApiResult<T>> op, string success, int tab)
        {
            var outcome = await AwaitData(op, "Groups write");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }
            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            _tabs.Invalidate(tab);
            _tabs.Invalidate(2);
            return ActionOutcome.Success(success);
        }

        private async void RunAndReload(AsyncOperation<RestApiResult> op, string success, string label)
        {
            var outcome = await Await(op, "Groups · " + label);
            Report(outcome, success, label);
            if (outcome.Ok)
            {
                _tabs.Invalidate(2);
            }
        }

        private async void RunDataAndReload<T>(AsyncOperation<RestApiResult<T>> op, string success, string label)
        {
            var outcome = await AwaitData(op, "Groups · " + label);
            Report(outcome, success, label);
            if (outcome.Ok)
            {
                _tabs.Invalidate(2);
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
                if (Popup != null)
                {
                    Popup.Close();
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

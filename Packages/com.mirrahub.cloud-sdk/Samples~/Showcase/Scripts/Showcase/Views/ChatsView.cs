using System;
using System.Collections.Generic;
using MirraCloud.Core;
using MirraCloud.Core.Chats.Dto;
using MirraCloud.Core.Chats.Events;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Errors;
using MirraCloud.Core.Groups.Dto.Response;
using MirraCloud.Core.Realtime.Protocol;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Chats screen: a working game chat, not a table of rows. Channels on the left, a live
    /// conversation on the right — send, edit, delete, mark as read, and every realtime event the
    /// service raises applied to the UI as it arrives.
    /// <para>
    /// Two SDK shapes meet here. History, members and channel metadata are REST
    /// (<c>RestApiResult</c>); sending, editing, deleting and read receipts travel over the WebSocket
    /// connection (<c>RealtimeResult</c>) and only work once <c>ConnectAsync</c> has succeeded — which
    /// is why the composer stays disabled until the connection chip goes green.
    /// </para>
    /// <para>
    /// Realtime callbacks can arrive off the main thread, so every handler marshals through
    /// <c>schedule.Execute</c> before touching the tree, and all of them are detached (and the
    /// channel unsubscribed) when the screen closes — otherwise reopening it would double up.
    /// The connection itself is left up, which is why this screen seeds its indicator from
    /// <c>Chats.ConnectionState</c> rather than waiting for an event that a still-connected socket
    /// has no reason to raise.
    /// </para>
    /// </summary>
    public sealed class ChatsView : ServiceView
    {
        private const string ChannelsSnippet =
@"// There is no ""list all channels"" endpoint: a channel belongs to something. For a group
// chat, resolve it from the group.
var groups = sdk.Groups.GetMyGroupsAsync();
await groups.Task();

foreach (GroupListItemDto g in groups.Result.Data.Items)
{
    var lookup = sdk.Chats.LookupGroupChannelAsync(g.GroupId);
    await lookup.Task();
    if (lookup.Result.IsSuccess)
    {
        string channelId = lookup.Result.Data.ChannelId;
    }
}";

        private const string CreateSnippet =
@"// A room channel from a chat template. templateKey is required and must exist in the
// project; the caller is joined as its first member.
var op = sdk.Chats.CreateChannelAsync(""Guild hall"", ""guild-chat"", ""Raid planning"");
await op.Task();

if (op.Result.IsSuccess)
{
    string channelId = op.Result.Data.ChannelId;
}";

        private const string HistorySnippet =
@"// Newest page first. Walk backwards with `before` — the Number of the oldest message you
// already hold — to page up through the history.
var op = sdk.Chats.GetMessagesAsync(channelId, limit: 50);
await op.Task();

foreach (ChatMessageDto m in op.Result.Data)
{
    // m.MessageId, m.Number, m.SenderId, m.Body, m.CreatedAt, m.EditedAt, m.DeletedAt
}";

        private const string ConnectSnippet =
@"// One connection per session, then a subscription per channel. Until this succeeds every
// realtime command fails with code ""not_connected"".
sdk.Chats.OnMessageReceived += m => AppendToUi(m);
sdk.Chats.OnConnectionStateChanged += state => UpdateIndicator(state);

// That event only fires on a change, and the connection outlives any one screen: a UI built
// after it came up reads where it stands from the property instead.
UpdateIndicator(sdk.Chats.ConnectionState);

var connect = sdk.Chats.ConnectAsync();
await connect.Task();

var subscribe = sdk.Chats.SubscribeAsync(channelId);
await subscribe.Task();";

        private const string SendSnippet =
@"// Realtime, not REST: the server echoes the stored message back as the result and
// broadcasts OnMessageReceived to everyone else in the channel.
var op = sdk.Chats.SendMessageAsync(channelId, ""hello"");
await op.Task();

if (op.Result.IsSuccess)
{
    ChatMessageDto stored = op.Result.Data;
}";

        private const string EditSnippet =
@"// Editing and deleting are realtime commands too, and only work on your own messages.
var edit = sdk.Chats.EditMessageAsync(channelId, messageId, ""fixed typo"");
await edit.Task();

var remove = sdk.Chats.DeleteMessageAsync(channelId, messageId);
await remove.Task();

// A read receipt moves the member's LastReadMessageNumber.
var read = sdk.Chats.MarkAsReadAsync(channelId, message.Number);
await read.Task();";

        private const string MembersSnippet =
@"var op = sdk.Chats.GetMembersAsync(channelId);
await op.Task();

foreach (ChatMemberDto m in op.Result.Data)
{
    // m.ProfileId, m.JoinedAt, m.LastReadMessageNumber, m.LastReadAt
}

// Joining and leaving are REST.
await sdk.Chats.JoinAsync(channelId).Task();
await sdk.Chats.LeaveAsync(channelId).Task();";

        private const string RecentsKey = "sc_showcase_chat_recents";
        private const int RecentsMax = 6;
        private const int HistoryPage = 50;

        /// <summary>Server code behind every "this profile is not in that channel" refusal.</summary>
        private const string NotChannelMemberCode = "chats.member_not_in_channel";

        // Within this many pixels of the bottom, a new message scrolls itself into view. Further up,
        // the reader is reading history and must not be yanked away from it.
        private const float StickyBottomPx = 80f;

        private readonly List<ChatMessageDto> _messages = new List<ChatMessageDto>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _avatars = new Dictionary<string, string>();
        private readonly HashSet<string> _nameRequests = new HashSet<string>();
        private readonly HashSet<string> _myProfileIds = new HashSet<string>();
        private readonly List<string> _eventLog = new List<string>();

        private VisualElement _channelList;
        private VisualElement _chatPane;
        private VisualElement _chatHeader;
        private ScrollView _messageScroll;
        private TextField _input;
        private Button _sendButton;
        private Button _loadEarlier;
        private VisualElement _eventLogBody;
        private Label _eventLogCount;
        private Button _eventLogToggle;

        private string _channelId;
        private ChatChannelDto _channel;
        private RealtimeConnectionState _state = RealtimeConnectionState.Disconnected;
        private long _markedRead;
        private bool _subscribed;
        private bool _closed;
        private bool _loadingHistory;
        private bool _handlersAttached;

        public ChatsView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
            RegisterCallback<DetachFromPanelEvent>(_ => Close());
        }

        protected override void Populate()
        {
            _closed = false;
            _messages.Clear();
            _eventLog.Clear();
            _channelId = null;
            _channel = null;
            _markedRead = 0L;

            DeclareCall(new SdkCall("Find the channels a player can open", ChannelsSnippet,
                "The SDK has no list-all endpoint: a channel is owned by a group, or addressed by id."));
            DeclareCall(new SdkCall("Create a channel", CreateSnippet));
            DeclareCall(new SdkCall("Read the history", HistorySnippet));
            DeclareCall(new SdkCall("Connect and subscribe", ConnectSnippet,
                "Everything below fails with \"not_connected\" until this has run."));
            DeclareCall(new SdkCall("Send a message", SendSnippet));
            DeclareCall(new SdkCall("Edit, delete, mark as read", EditSnippet));
            DeclareCall(new SdkCall("Members, join and leave", MembersSnippet));

            UseToolbar()
                .WithAction("New channel", LucideIcon.Plus, OpenCreateDialog, true)
                .WithSpacer()
                .WithRefresh(Refresh);

            AttachRealtimeHandlers();
            // The connection outlives this screen, so reopening it produces no state event to
            // listen for: read where the connection actually stands instead of assuming offline.
            _state = Sdk.Chats.ConnectionState;
            SyncStateChip();
            ResolveOwnProfiles();

            var split = new VisualElement();
            split.AddToClassList("sc-chat-split");

            _channelList = new VisualElement();
            _channelList.AddToClassList("sc-chat-channels");
            split.Add(_channelList);

            _chatPane = new VisualElement();
            _chatPane.AddToClassList("sc-chat-pane");
            split.Add(_chatPane);

            Content.Add(split);
            Content.Add(BuildEventLog());

            RenderChannelList();
            RenderNoChannel();
        }

        // ----- channel list ---------------------------------------------------------------------

        private void RenderChannelList()
        {
            _channelList.Clear();

            var header = new Label("Channels");
            header.AddToClassList("sc-chat-channels__title");
            _channelList.Add(header);

            var lookup = new VisualElement();
            lookup.AddToClassList("sc-chat-lookup");
            var field = new TextField { label = "Channel id" };
            field.AddToClassList("sc-field");
            lookup.Add(field);
            var openBtn = new Button(() => OpenChannel(field.value)) { text = "Open" };
            openBtn.AddToClassList("sc-btn");
            lookup.Add(openBtn);
            _channelList.Add(lookup);

            var recents = LoadRecents();
            if (recents.Count > 0)
            {
                _channelList.Add(Subheader("Recent"));
                foreach (var id in recents)
                {
                    _channelList.Add(ChannelRow(Fmt.Id(id, 10), "opened before", id, LucideIcon.History));
                }
            }

            _channelList.Add(Subheader("My groups"));
            var groupsSlot = new VisualElement();
            _channelList.Add(groupsSlot);
            ViewBind.Load(
                () => Sdk.Groups.GetMyGroupsAsync(1, 20),
                groupsSlot,
                BuildGroupChannels,
                p => p == null || p.Items == null || p.Items.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "My groups",
                    Snippet = ChannelsSnippet,
                    ServiceName = "Chats",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Users, "No group chats",
                        "Group channels appear here once the player joins a group. You can still open any "
                        + "channel by pasting its id above, or create a room from a template."),
                });
        }

        private static VisualElement Subheader(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sc-chat-channels__sub");
            return label;
        }

        private VisualElement BuildGroupChannels(PaginatedResult<GroupListItemDto> page)
        {
            var list = new VisualElement();
            foreach (var group in page.Items)
            {
                // A group id is not a channel id: it has to be resolved, and a group without a chat
                // resolves to nothing — so the row starts as a placeholder and becomes a channel.
                var row = ChannelRow(Fmt.OrDash(group.Name), "resolving channel…", null, LucideIcon.Users);
                list.Add(row);
                ResolveGroupChannel(group, row);
            }
            return list;
        }

        private async void ResolveGroupChannel(GroupListItemDto group, VisualElement row)
        {
            var op = Sdk.Chats.LookupGroupChannelAsync(group.GroupId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Lookup " + Fmt.OrDash(group.Name), result, ChannelsSnippet);
            }

            if (_closed || row.panel == null)
            {
                return;
            }

            var subtitle = row.Q<Label>(className: "sc-chat-channel__sub");
            if (result == null || !result.IsSuccess || result.Data == null
                || string.IsNullOrEmpty(result.Data.ChannelId))
            {
                // A refusal is not the same as an absence: the lookup is member-only, so a group
                // whose chat this profile never joined answers 403 and withholds the channel id.
                // That case is one call away from working, so the row offers to join instead of
                // claiming the group has no chat at all.
                if (result != null && IsNotChannelMember(result.Error))
                {
                    OfferChannelJoin(group, row);
                    return;
                }

                if (subtitle != null)
                {
                    subtitle.text = "no chat for this group";
                }
                row.SetEnabled(false);
                return;
            }

            string channelId = result.Data.ChannelId;
            if (subtitle != null)
            {
                subtitle.text = result.Data.LastMessageNumber + " messages";
            }
            row.RegisterCallback<ClickEvent>(_ => OpenChannel(channelId));
        }

        /// <summary>
        /// Turns a row the player cannot open into one they can act on. The channel id has to come
        /// from the group itself here, because the member-only lookup refused to hand it over.
        /// </summary>
        private async void OfferChannelJoin(GroupListItemDto group, VisualElement row)
        {
            var subtitle = row.Q<Label>(className: "sc-chat-channel__sub");
            if (subtitle != null)
            {
                subtitle.text = "you have not joined this chat";
            }

            var op = Sdk.Groups.GetAsync(group.GroupId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Group " + Fmt.OrDash(group.Name), result, ChannelsSnippet);
            }

            if (_closed || row.panel == null)
            {
                return;
            }

            var chat = result != null && result.IsSuccess && result.Data != null
                ? result.Data.ChatConfig
                : null;
            if (chat == null || string.IsNullOrEmpty(chat.ChannelId))
            {
                // Nothing to join after all — the group really has no chat.
                if (subtitle != null)
                {
                    subtitle.text = "no chat for this group";
                }
                row.SetEnabled(false);
                return;
            }

            string channelId = chat.ChannelId;
            var join = new Button(() => JoinFromList(channelId)) { text = "Join" };
            join.AddToClassList("sc-btn");
            row.Add(join);
        }

        private async void JoinFromList(string channelId)
        {
            var op = Sdk.Chats.JoinAsync(channelId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Join channel", result, MembersSnippet);
            }

            if (_closed)
            {
                return;
            }

            if (result == null || !result.IsSuccess)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Join failed · "
                        + (result != null && result.Error != null ? Fmt.OrDash(result.Error.Message) : "no response"));
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok("Joined the channel");
            }
            OpenChannel(channelId);
        }

        /// <summary>
        /// Chat membership is its own record — being in a group does not put a profile in the
        /// group's channel. The backend says so with this code, and unlike other refusals the
        /// player can fix it from here, so it is worth telling apart from a plain error.
        /// </summary>
        private static bool IsNotChannelMember(RestApiError error)
        {
            if (error == null)
            {
                return false;
            }
            return error.HasCode(NotChannelMemberCode) || error.HttpStatusCode == 403L;
        }

        private VisualElement ChannelRow(string title, string subtitle, string channelId, string glyph)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chat-channel");

            var icon = new Label(glyph);
            icon.AddToClassList("sc-icon");
            icon.AddToClassList("sc-chat-channel__glyph");
            row.Add(icon);

            var texts = new VisualElement();
            texts.AddToClassList("sc-chat-channel__texts");

            var name = new Label(title);
            name.enableRichText = false;
            name.AddToClassList("sc-chat-channel__name");
            texts.Add(name);

            var sub = new Label(subtitle);
            sub.enableRichText = false;
            sub.AddToClassList("sc-chat-channel__sub");
            texts.Add(sub);
            row.Add(texts);

            if (!string.IsNullOrEmpty(channelId))
            {
                row.RegisterCallback<ClickEvent>(_ => OpenChannel(channelId));
            }
            return row;
        }

        // ----- opening a channel ----------------------------------------------------------------

        private void RenderNoChannel()
        {
            _chatPane.Clear();
            _chatPane.Add(ZeroState.Panel(LucideIcon.MessageCircle, "No channel open",
                "Pick a group chat on the left, paste a channel id, or create a room from a chat "
                + "template. Once a channel is open this pane becomes a live conversation.",
                hint: "Sending needs the realtime connection — the chip in the header shows when it is up."));
        }

        private void OpenChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                if (Toasts != null)
                {
                    Toasts.Info("Enter a channel id first");
                }
                return;
            }
            channelId = channelId.Trim();
            if (_channelId == channelId)
            {
                return;
            }

            UnsubscribeCurrent();

            _channelId = channelId;
            _channel = null;
            _messages.Clear();
            _markedRead = 0L;
            RememberRecent(channelId);

            BuildChatPane();
            LoadChannel();
            LoadHistory(true);
            EnsureConnected();
        }

        private void BuildChatPane()
        {
            _chatPane.Clear();

            _chatHeader = new VisualElement();
            _chatHeader.AddToClassList("sc-chat-head");
            _chatPane.Add(_chatHeader);
            RenderChatHeader();

            _loadEarlier = new Button(() => LoadHistory(false)) { text = "Load earlier messages" };
            _loadEarlier.AddToClassList("sc-btn");
            _loadEarlier.AddToClassList("sc-chat-earlier");
            _loadEarlier.style.display = DisplayStyle.None;
            _chatPane.Add(_loadEarlier);

            // The message log keeps its own scroller: a chat has to grow from the bottom and stay
            // put while the page around it scrolls, which one shared scroller cannot do.
            _messageScroll = new ScrollView(ScrollViewMode.Vertical);
            _messageScroll.AddToClassList("sc-chat-log");
            _chatPane.Add(_messageScroll);

            var composer = new VisualElement();
            composer.AddToClassList("sc-chat-composer");

            _input = new TextField();
            _input.AddToClassList("sc-field");
            _input.AddToClassList("sc-chat-composer__input");
            _input.RegisterCallback<KeyDownEvent>(OnComposerKey);
            composer.Add(_input);

            _sendButton = new Button(SendCurrent) { text = "Send" };
            _sendButton.AddToClassList("sc-btn");
            _sendButton.AddToClassList("sc-btn--primary");
            composer.Add(_sendButton);

            var members = new Button(OpenMembers) { text = "Members" };
            members.AddToClassList("sc-btn");
            composer.Add(members);

            _chatPane.Add(composer);
            SyncComposerEnabled();
        }

        private void RenderChatHeader()
        {
            if (_chatHeader == null)
            {
                return;
            }
            _chatHeader.Clear();

            string title = _channel != null && !string.IsNullOrEmpty(_channel.Name)
                ? _channel.Name
                : Fmt.Id(_channelId, 12);
            var name = new Label(title);
            name.enableRichText = false;
            name.AddToClassList("sc-chat-head__name");
            _chatHeader.Add(name);

            if (_channel != null && !string.IsNullOrEmpty(_channel.Topic))
            {
                var topic = new Label(_channel.Topic);
                topic.enableRichText = false;
                topic.AddToClassList("sc-chat-topic");
                _chatHeader.Add(topic);
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(ConnectionChip());
            if (_channel != null)
            {
                if (!string.IsNullOrEmpty(_channel.Type))
                {
                    chips.Add(new Chip(_channel.Type, ChipTone.Neutral));
                }
                if (!string.IsNullOrEmpty(_channel.State))
                {
                    chips.Add(new Chip(_channel.State, ChipTone.Info));
                }
                if (!string.IsNullOrEmpty(_channel.TemplateKey))
                {
                    chips.Add(new Chip("tpl " + _channel.TemplateKey, ChipTone.Neutral));
                }
            }
            _chatHeader.Add(chips);
        }

        private Chip ConnectionChip()
        {
            switch (_state)
            {
                case RealtimeConnectionState.Connected: return new Chip("live", ChipTone.Ok);
                case RealtimeConnectionState.Connecting: return new Chip("connecting…", ChipTone.Warn);
                case RealtimeConnectionState.Reconnecting: return new Chip("reconnecting…", ChipTone.Warn);
                default: return new Chip("offline", ChipTone.Bad);
            }
        }

        private async void LoadChannel()
        {
            string channelId = _channelId;
            var op = Sdk.Chats.GetChannelAsync(channelId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Channel", result);
            }

            if (_closed || _channelId != channelId)
            {
                return;
            }
            if (result != null && result.IsSuccess && result.Data != null)
            {
                _channel = result.Data;
                RenderChatHeader();
            }
        }

        // ----- history --------------------------------------------------------------------------

        private async void LoadHistory(bool initial)
        {
            if (_loadingHistory)
            {
                return;
            }
            _loadingHistory = true;

            string channelId = _channelId;
            long? before = null;
            if (!initial && _messages.Count > 0)
            {
                before = _messages[0].Number;
            }

            if (initial && _messageScroll != null)
            {
                _messageScroll.Clear();
                Skeleton.Into(_messageScroll.contentContainer, 5);
            }

            try
            {
                var op = Sdk.Chats.GetMessagesAsync(channelId, before, null, HistoryPage);
                if (op == null)
                {
                    return;
                }
                await op.Task();
                var result = op.Result;
                if (Ctx.Log != null && result != null)
                {
                    Ctx.Log.Record("History", result, HistorySnippet);
                }

                if (_closed || _channelId != channelId || _messageScroll == null)
                {
                    return;
                }

                if (result == null || !result.IsSuccess)
                {
                    _messageScroll.Clear();
                    if (result != null && IsNotChannelMember(result.Error))
                    {
                        _messageScroll.Add(ZeroState.Panel(LucideIcon.Lock, "You are not in this channel",
                            "The channel is there, this profile just never joined it — chat membership "
                            + "is a separate record from group membership. Join it to read the history "
                            + "and post.",
                            "Join channel", () => JoinLeave(true)));
                        return;
                    }
                    _messageScroll.Add(ErrorState.Build(result != null ? result.Error : null));
                    return;
                }

                var page = result.Data ?? new ChatMessageDto[0];
                foreach (var m in page)
                {
                    Merge(m);
                }

                // A short page means the history ran out, so stop offering to page further back.
                _loadEarlier.style.display = page.Length >= HistoryPage ? DisplayStyle.Flex : DisplayStyle.None;
                RenderMessages(initial);
            }
            finally
            {
                _loadingHistory = false;
            }
        }

        /// <summary>Inserts or replaces by message number, keeping the list ordered oldest first.</summary>
        private void Merge(ChatMessageDto message)
        {
            if (message == null)
            {
                return;
            }
            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Number == message.Number)
                {
                    _messages[i] = message;
                    return;
                }
                if (_messages[i].Number > message.Number)
                {
                    _messages.Insert(i, message);
                    return;
                }
            }
            _messages.Add(message);
        }

        // ----- message rendering ----------------------------------------------------------------

        private void RenderMessages(bool stickToBottom)
        {
            if (_messageScroll == null)
            {
                return;
            }

            bool wasAtBottom = stickToBottom || IsNearBottom();
            _messageScroll.Clear();

            if (_messages.Count == 0)
            {
                _messageScroll.Add(ZeroState.Panel(LucideIcon.MessageSquare, "No messages yet",
                    "Say something in the box below — it goes out over the realtime connection and "
                    + "everyone else in the channel receives it immediately."));
                return;
            }

            string previousSender = null;
            DateTime previousAt = DateTime.MinValue;
            foreach (var m in _messages)
            {
                bool sameAuthor = m.SenderId == previousSender
                    && (m.CreatedAt - previousAt).TotalMinutes < 5d;
                _messageScroll.Add(Bubble(m, sameAuthor));
                previousSender = m.SenderId;
                previousAt = m.CreatedAt;
            }

            if (wasAtBottom)
            {
                ScrollToBottom();
            }
            MarkNewestRead();
        }

        private VisualElement Bubble(ChatMessageDto message, bool sameAuthor)
        {
            bool mine = IsMine(message);

            var row = new VisualElement();
            row.AddToClassList("sc-msg");
            row.AddToClassList(mine ? "sc-msg--mine" : "sc-msg--theirs");
            if (sameAuthor)
            {
                row.AddToClassList("sc-msg--grouped");
            }

            if (!mine)
            {
                var avatar = new Avatar(28f);
                avatar.AddToClassList("sc-msg__avatar");
                if (sameAuthor)
                {
                    // Kept in the tree, just invisible, so grouped bubbles stay aligned with the first.
                    avatar.style.visibility = Visibility.Hidden;
                }
                else
                {
                    string url;
                    _avatars.TryGetValue(message.SenderId ?? string.Empty, out url);
                    avatar.BindUrl(Images, url, NameFor(message.SenderId));
                }
                row.Add(avatar);
            }

            var column = new VisualElement();
            column.AddToClassList("sc-msg__col");

            if (!sameAuthor)
            {
                var meta = new VisualElement();
                meta.AddToClassList("sc-msg__meta");

                var who = new Label(mine ? "You" : NameFor(message.SenderId));
                who.enableRichText = false;
                who.AddToClassList("sc-msg__who");
                meta.Add(who);

                var when = new Label(Fmt.Time(message.CreatedAt));
                when.AddToClassList("sc-msg__when");
                meta.Add(when);
                column.Add(meta);
            }

            var bubble = new VisualElement();
            bubble.AddToClassList("sc-msg__bubble");

            bool deleted = message.DeletedAt.HasValue;
            var body = new Label(deleted
                ? "This message was deleted"
                : (string.IsNullOrEmpty(message.Body) ? Fmt.Dash : message.Body));
            body.enableRichText = false;
            body.AddToClassList("sc-msg__body");
            if (deleted)
            {
                body.AddToClassList("sc-msg__body--deleted");
            }
            bubble.Add(body);

            var marks = new VisualElement();
            marks.AddToClassList("sc-msg__marks");
            if (message.EditedAt.HasValue && !deleted)
            {
                var edited = new Label("edited");
                edited.AddToClassList("sc-msg__mark");
                marks.Add(edited);
            }
            if (message.TaggedMembers != null && message.TaggedMembers.Length > 0)
            {
                var tagged = new Label("@" + message.TaggedMembers.Length);
                tagged.AddToClassList("sc-msg__mark");
                marks.Add(tagged);
            }
            if (marks.childCount > 0)
            {
                bubble.Add(marks);
            }
            column.Add(bubble);

            if (mine && !deleted)
            {
                column.Add(OwnActions(message));
            }
            row.Add(column);

            if (!mine)
            {
                // Names are resolved lazily, once per sender; the answer re-renders the bubbles.
                RequestName(message.SenderId);
            }
            return row;
        }

        private VisualElement OwnActions(ChatMessageDto message)
        {
            var actions = new VisualElement();
            actions.AddToClassList("sc-msg__actions");

            var edit = new Button(() => OpenEditDialog(message)) { text = "Edit" };
            edit.AddToClassList("sc-msg__action");
            actions.Add(edit);

            var remove = new Button(() => ConfirmDelete(message)) { text = "Delete" };
            remove.AddToClassList("sc-msg__action");
            remove.AddToClassList("sc-msg__action--danger");
            actions.Add(remove);
            return actions;
        }

        private bool IsMine(ChatMessageDto message)
        {
            return message != null && !string.IsNullOrEmpty(message.SenderId)
                && _myProfileIds.Contains(message.SenderId);
        }

        private string NameFor(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                return "Unknown";
            }
            string name;
            return _names.TryGetValue(profileId, out name) && !string.IsNullOrEmpty(name)
                ? name
                : Fmt.Id(profileId, 8);
        }

        private bool IsNearBottom()
        {
            if (_messageScroll == null)
            {
                return true;
            }
            float max = _messageScroll.verticalScroller.highValue;
            return max <= 0f || _messageScroll.scrollOffset.y >= max - StickyBottomPx;
        }

        private void ScrollToBottom()
        {
            // Deferred because the new bubbles have no resolved height yet, and ScrollTo rather than
            // a raw offset because the scroller's range is still settling in that first frame —
            // setting highValue directly leaves the newest bubble half under the composer.
            schedule.Execute(() =>
            {
                if (_closed || _messageScroll == null || _messageScroll.childCount == 0)
                {
                    return;
                }
                var last = _messageScroll.contentContainer.ElementAt(_messageScroll.contentContainer.childCount - 1);
                _messageScroll.ScrollTo(last);
            }).StartingIn(0);
        }

        // ----- own profiles and sender names ----------------------------------------------------

        /// <summary>
        /// The SDK exposes no "current profile id", so ownership is decided by set membership: every
        /// profile this account owns counts as me — which is what the server enforces on edit and
        /// delete anyway.
        /// </summary>
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

            foreach (var profile in result.Data)
            {
                if (profile == null || string.IsNullOrEmpty(profile.Id))
                {
                    continue;
                }
                _myProfileIds.Add(profile.Id);
                _names[profile.Id] = string.IsNullOrEmpty(profile.Nickname) ? profile.Username : profile.Nickname;
                _avatars[profile.Id] = profile.IconUrl;
            }

            if (_messages.Count > 0)
            {
                RenderMessages(false);
            }
        }

        private async void RequestName(string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || _names.ContainsKey(profileId)
                || !_nameRequests.Add(profileId))
            {
                return;
            }

            var op = Sdk.PlayerAccount.GetProfileAsync(profileId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed)
            {
                return;
            }

            if (result == null || !result.IsSuccess || result.Data == null)
            {
                // Remember the miss too, or every re-render asks again.
                _names[profileId] = null;
                return;
            }

            _names[profileId] = string.IsNullOrEmpty(result.Data.Nickname)
                ? result.Data.Username
                : result.Data.Nickname;
            _avatars[profileId] = result.Data.IconUrl;
            RenderMessages(false);
        }

        // ----- realtime -------------------------------------------------------------------------

        private void AttachRealtimeHandlers()
        {
            if (_handlersAttached)
            {
                return;
            }
            _handlersAttached = true;

            Sdk.Chats.OnConnectionStateChanged += HandleState;
            Sdk.Chats.OnSubscribedChannel += HandleSubscribed;
            Sdk.Chats.OnMessageReceived += HandleReceived;
            Sdk.Chats.OnMessageEdited += HandleEdited;
            Sdk.Chats.OnMessageDeleted += HandleDeleted;
            Sdk.Chats.OnMemberAdded += HandleMemberAdded;
            Sdk.Chats.OnMemberRemoved += HandleMemberRemoved;
            Sdk.Chats.OnMemberBanned += HandleMemberBanned;
            Sdk.Chats.OnChannelDeleted += HandleChannelDeleted;
            Sdk.Chats.OnError += HandleError;
        }

        private void DetachRealtimeHandlers()
        {
            if (!_handlersAttached)
            {
                return;
            }
            _handlersAttached = false;

            Sdk.Chats.OnConnectionStateChanged -= HandleState;
            Sdk.Chats.OnSubscribedChannel -= HandleSubscribed;
            Sdk.Chats.OnMessageReceived -= HandleReceived;
            Sdk.Chats.OnMessageEdited -= HandleEdited;
            Sdk.Chats.OnMessageDeleted -= HandleDeleted;
            Sdk.Chats.OnMemberAdded -= HandleMemberAdded;
            Sdk.Chats.OnMemberRemoved -= HandleMemberRemoved;
            Sdk.Chats.OnMemberBanned -= HandleMemberBanned;
            Sdk.Chats.OnChannelDeleted -= HandleChannelDeleted;
            Sdk.Chats.OnError -= HandleError;
        }

        /// <summary>
        /// Realtime callbacks may arrive on the socket's thread and UI Toolkit is main-thread only, so
        /// every handler hands its work to the scheduler instead of touching the tree directly.
        /// </summary>
        private void OnMain(Action work)
        {
            if (_closed)
            {
                return;
            }
            schedule.Execute(() =>
            {
                if (!_closed)
                {
                    work();
                }
            }).StartingIn(0);
        }

        private void HandleState(RealtimeConnectionState state)
        {
            OnMain(() =>
            {
                _state = state;
                SyncStateChip();
                RenderChatHeader();
                SyncComposerEnabled();
                LogEvent("connection · " + state);
            });
        }

        private void HandleSubscribed(string channelId)
        {
            OnMain(() =>
            {
                LogEvent("subscribed · " + Fmt.Id(channelId, 10));
                if (channelId == _channelId)
                {
                    _subscribed = true;
                }
            });
        }

        private void HandleReceived(ChatMessageDto message)
        {
            OnMain(() =>
            {
                LogEvent("message · #" + (message != null ? message.Number.ToString() : "?"));
                if (message == null || message.ChannelId != _channelId)
                {
                    return;
                }
                bool atBottom = IsNearBottom();
                Merge(message);
                RenderMessages(atBottom);
            });
        }

        private void HandleEdited(ChatMessageDto message)
        {
            OnMain(() =>
            {
                LogEvent("edited · #" + (message != null ? message.Number.ToString() : "?"));
                if (message == null || message.ChannelId != _channelId)
                {
                    return;
                }
                Merge(message);
                RenderMessages(false);
            });
        }

        private void HandleDeleted(RealtimeDeletePayload payload)
        {
            OnMain(() =>
            {
                LogEvent("deleted · " + (payload != null ? Fmt.Id(payload.MessageId, 8) : "?"));
                if (payload == null || payload.ChannelId != _channelId)
                {
                    return;
                }
                MarkDeletedLocally(payload.MessageId);
                RenderMessages(false);
            });
        }

        private void HandleMemberAdded(ChatMemberEvent evt)
        {
            OnMain(() => LogEvent("member joined · " + (evt != null ? Fmt.Id(evt.ProfileId, 8) : "?")));
        }

        private void HandleMemberRemoved(ChatMemberEvent evt)
        {
            OnMain(() => LogEvent("member left · " + (evt != null ? Fmt.Id(evt.ProfileId, 8) : "?")));
        }

        private void HandleMemberBanned(ChatMemberBannedEvent evt)
        {
            OnMain(() => LogEvent("member banned · " + (evt != null ? Fmt.Id(evt.ProfileId, 8) : "?")));
        }

        private void HandleChannelDeleted(string channelId)
        {
            OnMain(() =>
            {
                LogEvent("channel deleted · " + Fmt.Id(channelId, 10));
                if (channelId != _channelId)
                {
                    return;
                }
                if (Toasts != null)
                {
                    Toasts.Fail("This channel was deleted");
                }
                _channelId = null;
                _channel = null;
                _messages.Clear();
                RenderNoChannel();
            });
        }

        private void HandleError(ChatErrorEvent evt)
        {
            OnMain(() =>
            {
                string code = evt != null ? evt.Code : "error";
                string message = evt != null ? evt.Message : null;
                LogEvent("error · " + code + (string.IsNullOrEmpty(message) ? string.Empty : " · " + message));
                if (Toasts != null)
                {
                    Toasts.Fail("Chat · " + (string.IsNullOrEmpty(message) ? code : message));
                }
            });
        }

        private void MarkDeletedLocally(string messageId)
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].MessageId == messageId)
                {
                    _messages[i].DeletedAt = DateTime.UtcNow;
                    return;
                }
            }
        }

        private void SyncStateChip()
        {
            switch (_state)
            {
                case RealtimeConnectionState.Connected:
                    SetStatus("Realtime connected", ChipTone.Ok);
                    break;
                case RealtimeConnectionState.Connecting:
                    SetStatus("Connecting…", ChipTone.Warn);
                    break;
                case RealtimeConnectionState.Reconnecting:
                    SetStatus("Reconnecting…", ChipTone.Warn);
                    break;
                default:
                    SetStatus("Realtime offline", ChipTone.Bad);
                    break;
            }
        }

        private void SyncComposerEnabled()
        {
            bool live = _state == RealtimeConnectionState.Connected;
            if (_input != null)
            {
                _input.SetEnabled(live);
            }
            if (_sendButton != null)
            {
                _sendButton.SetEnabled(live);
                _sendButton.tooltip = live
                    ? "Send over the realtime connection"
                    : "Sending needs the realtime connection, which is not up yet";
            }
        }

        private async void EnsureConnected()
        {
            string channelId = _channelId;

            var connect = Sdk.Chats.ConnectAsync();
            if (connect != null)
            {
                await connect.Task();
                var connected = connect.Result;
                if (connected != null && !connected.IsSuccess)
                {
                    LogEvent("connect failed · " + Fmt.OrDash(connected.Message));
                    if (Toasts != null)
                    {
                        Toasts.Fail("Realtime · " + Fmt.OrDash(connected.Message));
                    }
                    return;
                }
            }

            if (_closed || _channelId != channelId)
            {
                return;
            }

            var subscribe = Sdk.Chats.SubscribeAsync(channelId);
            if (subscribe == null)
            {
                return;
            }
            await subscribe.Task();
            var result = subscribe.Result;
            if (_closed || _channelId != channelId)
            {
                return;
            }

            if (result != null && result.IsSuccess)
            {
                _subscribed = true;
                LogEvent("subscribe ok · " + Fmt.Id(channelId, 10));
            }
            else
            {
                LogEvent("subscribe failed · " + (result != null ? Fmt.OrDash(result.Message) : "no result"));
            }
        }

        private void UnsubscribeCurrent()
        {
            if (!_subscribed || string.IsNullOrEmpty(_channelId))
            {
                return;
            }
            // Fire and forget: the screen is moving on either way, and the connection itself is
            // shared per session so it is deliberately left up.
            Sdk.Chats.UnsubscribeAsync(_channelId);
            _subscribed = false;
        }

        // ----- sending --------------------------------------------------------------------------

        private void OnComposerKey(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }
            evt.StopPropagation();
            SendCurrent();
        }

        private void SendCurrent()
        {
            string body = _input != null ? _input.value : null;
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }
            _input.value = string.Empty;
            Send(body.Trim());
        }

        private async void Send(string body)
        {
            string channelId = _channelId;
            _sendButton.SetEnabled(false);

            var op = Sdk.Chats.SendMessageAsync(channelId, body);
            if (op == null)
            {
                SyncComposerEnabled();
                return;
            }
            await op.Task();
            var result = op.Result;

            if (_closed || _channelId != channelId)
            {
                return;
            }
            SyncComposerEnabled();

            if (result == null || !result.IsSuccess)
            {
                string why = result != null && !string.IsNullOrEmpty(result.Message)
                    ? result.Message
                    : "the message was not accepted";
                if (Toasts != null)
                {
                    Toasts.Fail("Not sent · " + why);
                }
                LogEvent("send failed · " + why);

                // Put the text back rather than losing it to a dropped connection.
                if (_input != null && string.IsNullOrEmpty(_input.value))
                {
                    _input.value = body;
                }
                return;
            }

            // The command echoes the stored message; the broadcast may also reach us, and Merge is
            // idempotent by number either way.
            Merge(result.Data);
            RenderMessages(true);
        }

        private void OpenEditDialog(ChatMessageDto message)
        {
            if (Popup == null)
            {
                return;
            }
            FormDialog.Open(Popup, "Edit message",
                new[] { FormField.LongText("body", "Message", message.Body) },
                "Save",
                values => Edit(message, values.Text("body")));
        }

        private async void Edit(ChatMessageDto message, string body)
        {
            string channelId = _channelId;
            var op = Sdk.Chats.EditMessageAsync(channelId, message.MessageId, body);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed || _channelId != channelId)
            {
                return;
            }

            if (result == null || !result.IsSuccess)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Not edited · " + (result != null ? Fmt.OrDash(result.Message) : "no result"));
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok("Message updated");
            }
            Merge(result.Data);
            RenderMessages(false);
        }

        private void ConfirmDelete(ChatMessageDto message)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Delete message",
                "The message stays in the history marked as deleted, for everyone in the channel.",
                "Delete", () => Delete(message));
        }

        private async void Delete(ChatMessageDto message)
        {
            string channelId = _channelId;
            var op = Sdk.Chats.DeleteMessageAsync(channelId, message.MessageId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed || _channelId != channelId)
            {
                return;
            }

            if (result == null || !result.IsSuccess)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Not deleted · " + (result != null ? Fmt.OrDash(result.Message) : "no result"));
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok("Message deleted");
            }
            MarkDeletedLocally(message.MessageId);
            RenderMessages(false);
        }

        private async void MarkNewestRead()
        {
            if (_state != RealtimeConnectionState.Connected || _messages.Count == 0)
            {
                return;
            }
            long newest = _messages[_messages.Count - 1].Number;
            if (newest <= _markedRead)
            {
                return;
            }
            _markedRead = newest;

            string channelId = _channelId;
            var op = Sdk.Chats.MarkAsReadAsync(channelId, newest);
            if (op == null)
            {
                return;
            }
            await op.Task();
            if (!_closed && _channelId == channelId && op.Result != null && !op.Result.IsSuccess)
            {
                // Not worth a toast — a lost receipt costs the reader nothing — but allow a retry.
                LogEvent("mark-as-read failed · " + Fmt.OrDash(op.Result.Message));
                _markedRead = 0L;
            }
        }

        // ----- members and creation -------------------------------------------------------------

        private void OpenMembers()
        {
            if (Popup == null || string.IsNullOrEmpty(_channelId))
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var slot = new VisualElement();
            body.Add(slot);

            var buttons = new VisualElement();
            buttons.AddToClassList("sc-chip-row");
            var join = new Button(() => JoinLeave(true)) { text = "Join channel" };
            join.AddToClassList("sc-btn");
            buttons.Add(join);
            var leave = new Button(() => JoinLeave(false)) { text = "Leave channel" };
            leave.AddToClassList("sc-btn");
            buttons.Add(leave);
            body.Add(buttons);

            string channelId = _channelId;
            ViewBind.Load(
                () => Sdk.Chats.GetMembersAsync(channelId),
                slot,
                BuildMembers,
                m => m == null || m.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Members",
                    Snippet = MembersSnippet,
                    ServiceName = "Chats",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Users, "No members",
                        "Nobody has joined this channel yet. Join it to start receiving its messages."),
                });

            Popup.Open(body, "Channel members");
        }

        private VisualElement BuildMembers(ChatMemberDto[] members)
        {
            var list = new VisualElement();
            foreach (var m in members)
            {
                var row = new ListRow();
                row.SetLead(new Avatar(30f).SetInitialsFor(NameFor(m.ProfileId)));
                row.SetTitle(NameFor(m.ProfileId));
                row.SetSubtitle("joined " + Fmt.Date(m.JoinedAt));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-chip-row");
                trailing.Add(new Badge("read #" + m.LastReadMessageNumber, ChipTone.Neutral));
                if (_myProfileIds.Contains(m.ProfileId))
                {
                    trailing.Add(new Badge("you", ChipTone.Accent));
                }
                row.SetTrailing(trailing);
                list.Add(row);

                RequestName(m.ProfileId);
            }
            return list;
        }

        private async void JoinLeave(bool join)
        {
            string channelId = _channelId;
            var op = join ? Sdk.Chats.JoinAsync(channelId) : Sdk.Chats.LeaveAsync(channelId);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(join ? "Join channel" : "Leave channel", result, MembersSnippet);
            }
            if (_closed)
            {
                return;
            }

            if (result != null && result.IsSuccess)
            {
                if (Toasts != null)
                {
                    Toasts.Ok(join ? "Joined the channel" : "Left the channel");
                }
                if (Popup != null)
                {
                    Popup.Close();
                }
                if (join && _channelId == channelId)
                {
                    LoadHistory(true);
                    // The subscribe made when the screen opened was refused for a non-member, and
                    // the connection keeps no memory of it — without asking again every send fails
                    // with "not subscribed to room".
                    EnsureConnected();
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Fail((join ? "Join" : "Leave") + " failed · "
                    + (result != null && result.Error != null ? Fmt.OrDash(result.Error.Message) : "no response"));
            }
        }

        private void OpenCreateDialog()
        {
            if (Popup == null)
            {
                return;
            }
            FormDialog.Open(Popup, "Create a channel",
                new[]
                {
                    FormField.Text("name", "Name", "Guild hall", true),
                    FormField.Text("templateKey", "Template key", null, true),
                    FormField.Text("topic", "Topic"),
                },
                "Create",
                values => Create(values.Text("name"), values.Text("templateKey"), values.Text("topic")));
        }

        private async void Create(string name, string templateKey, string topic)
        {
            var op = Sdk.Chats.CreateChannelAsync(name, templateKey, topic);
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Create channel", result, CreateSnippet);
            }
            if (_closed)
            {
                return;
            }

            if (result == null || !result.IsSuccess || result.Data == null)
            {
                if (Toasts != null)
                {
                    Toasts.Fail("Not created · "
                        + (result != null && result.Error != null ? Fmt.OrDash(result.Error.Message) : "no response"));
                }
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok("Channel created");
            }
            if (Popup != null)
            {
                Popup.Close();
            }
            RenderChannelList();
            OpenChannel(result.Data.ChannelId);
        }

        // ----- realtime event log ---------------------------------------------------------------

        private VisualElement BuildEventLog()
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-chat-events");

            var head = new VisualElement();
            head.AddToClassList("sc-chat-events__head");

            var title = new Label("Realtime events");
            title.AddToClassList("sc-card__title");
            head.Add(title);

            _eventLogCount = new Label("0");
            _eventLogCount.AddToClassList("sc-section-header__count");
            head.Add(_eventLogCount);

            _eventLogToggle = new Button { text = "Show" };
            _eventLogToggle.AddToClassList("sc-btn");
            head.Add(_eventLogToggle);
            card.Body.Add(head);

            var hint = new Label("Everything the channel pushes to this client, newest first — the "
                + "protocol the chat above is built on.");
            hint.AddToClassList("sc-fs-hint");
            card.Body.Add(hint);

            _eventLogBody = new VisualElement();
            _eventLogBody.AddToClassList("sc-chat-events__body");
            _eventLogBody.style.display = DisplayStyle.None;
            card.Body.Add(_eventLogBody);

            _eventLogToggle.clicked += () =>
            {
                bool shown = _eventLogBody.style.display == DisplayStyle.Flex;
                _eventLogBody.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
                _eventLogToggle.text = shown ? "Show" : "Hide";
                if (!shown)
                {
                    RenderEventLog();
                }
            };

            RenderEventLog();
            return card;
        }

        private void LogEvent(string text)
        {
            _eventLog.Insert(0, Fmt.Time(DateTime.Now) + "  " + text);
            if (_eventLog.Count > 100)
            {
                _eventLog.RemoveAt(_eventLog.Count - 1);
            }
            if (_eventLogCount != null)
            {
                _eventLogCount.text = _eventLog.Count.ToString();
            }
            if (_eventLogBody != null && _eventLogBody.style.display == DisplayStyle.Flex)
            {
                RenderEventLog();
            }
        }

        private void RenderEventLog()
        {
            if (_eventLogBody == null)
            {
                return;
            }
            _eventLogBody.Clear();

            if (_eventLog.Count == 0)
            {
                var idle = new Label("Nothing yet. Open a channel and the connection, subscription and "
                    + "message events will appear here as they happen.");
                idle.AddToClassList("sc-fs-hint");
                _eventLogBody.Add(idle);
                return;
            }

            foreach (var line in _eventLog)
            {
                var label = new Label(line);
                label.enableRichText = false;
                label.AddToClassList("sc-chat-events__line");
                _eventLogBody.Add(label);
            }
        }

        // ----- recents and teardown -------------------------------------------------------------

        private static List<string> LoadRecents()
        {
            var list = new List<string>();
            string raw = PlayerPrefs.GetString(RecentsKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return list;
            }
            foreach (var part in raw.Split('|'))
            {
                if (!string.IsNullOrEmpty(part) && !list.Contains(part))
                {
                    list.Add(part);
                }
            }
            return list;
        }

        private static void RememberRecent(string channelId)
        {
            var list = LoadRecents();
            list.Remove(channelId);
            list.Insert(0, channelId);
            while (list.Count > RecentsMax)
            {
                list.RemoveAt(list.Count - 1);
            }
            PlayerPrefs.SetString(RecentsKey, string.Join("|", list.ToArray()));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Leaves the connection itself up — it is shared per session — but drops this screen's
        /// subscription and every handler, so reopening the screen does not double up on events.
        /// </summary>
        private void Close()
        {
            if (_closed)
            {
                return;
            }
            _closed = true;
            UnsubscribeCurrent();
            DetachRealtimeHandlers();
        }
    }
}

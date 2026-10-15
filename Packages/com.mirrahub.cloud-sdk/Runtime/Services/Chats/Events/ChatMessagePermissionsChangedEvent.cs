using MirraCloud.Core.Chats.Dto;

namespace MirraCloud.Core.Chats.Events
{
    /// <summary>
    /// The group admin changed what players may do with messages in a subscribed channel.
    /// </summary>
    public sealed class ChatMessagePermissionsChangedEvent
    {
        public string ChannelId;
        public ChatMessagePermissionsDto MessagePermissions;
    }
}

namespace MirraCloud.Core.Chats.Events
{
    public sealed class ChatMemberBannedEvent
    {
        public string ChannelId;
        public string ProfileId;
        public string BannedBy;
    }
}

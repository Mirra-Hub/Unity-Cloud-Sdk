using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    /// <summary>
    /// A channel the player is a member of, as <see cref="ChatsService.GetMyChannelsAsync"/> lists it: the channel
    /// and where the player left off in it.
    /// </summary>
    [Serializable]
    public sealed class ChatPlayerChannelDto
    {
        [JsonNameCamel] public ChatChannelDto Channel;
        [JsonNameCamel] public DateTime JoinedAt;

        /// <summary>The last message the player has read — moved by <see cref="ChatsService.MarkAsReadAsync"/>.</summary>
        [JsonNameCamel] public long LastReadMessageNumber;
        [JsonNameCamel] public DateTime? LastReadAt;

        /// <summary>
        /// Messages after <see cref="LastReadMessageNumber"/>. The player's own messages count too until
        /// <see cref="ChatsService.MarkAsReadAsync"/> moves the marker past them.
        /// </summary>
        [JsonNameCamel] public long UnreadCount;
    }
}

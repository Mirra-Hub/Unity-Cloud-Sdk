using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    /// <summary>
    /// What a player may do with messages in a channel: the chat template's settings with the group admin's
    /// changes applied. Use it to show or hide the edit and delete actions.
    /// </summary>
    [Serializable]
    public sealed class ChatMessagePermissionsDto
    {
        /// <summary>The player may edit their own messages.</summary>
        [JsonNameCamel] public bool EditOwn;

        /// <summary>The player may delete their own messages.</summary>
        [JsonNameCamel] public bool DeleteOwn;

        /// <summary>The player may edit other players' messages.</summary>
        [JsonNameCamel] public bool EditOthers;

        /// <summary>The player may delete other players' messages.</summary>
        [JsonNameCamel] public bool DeleteOthers;

        /// <summary>The group admin may change these permissions for the group chat.</summary>
        [JsonNameCamel] public bool AdminOverrideAllowed;

        /// <summary>Whether a message can be edited by the player: <paramref name="ownMessage"/> picks the setting.</summary>
        public bool CanEdit(bool ownMessage) => ownMessage ? EditOwn : EditOthers;

        /// <summary>Whether a message can be deleted by the player: <paramref name="ownMessage"/> picks the setting.</summary>
        public bool CanDelete(bool ownMessage) => ownMessage ? DeleteOwn : DeleteOthers;
    }
}

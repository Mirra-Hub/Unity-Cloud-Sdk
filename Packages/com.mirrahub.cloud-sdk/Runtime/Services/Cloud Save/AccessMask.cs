using System;

namespace MirraCloud.Core.CloudSave
{
    /// <summary>
    /// Who may read or write a key or file. The console is never limited by masks.
    /// </summary>
    [Flags]
    public enum AccessMask
    {
        /// <summary>The player whose data it is.</summary>
        Owner = 1,

        /// <summary>Any other player. For global and custom data this is every player.</summary>
        Other = 2,

        /// <summary>The game server: Cloud Code and other backend logic.</summary>
        Server = 4
    }
}

using System;

namespace MirraCloud.Core.CloudSave
{
    [Flags]
    public enum AccessMask
    {
        Owner = 1,
        Other = 2,
        Server = 4
    }
}

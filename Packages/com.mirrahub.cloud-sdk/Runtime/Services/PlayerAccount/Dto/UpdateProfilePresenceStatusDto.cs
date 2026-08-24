using System;
using MirraCloud.Core.Enums;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public struct UpdateProfilePresenceStatusDto
    {
        public ProfilePresenceStatus Status;
    }
}


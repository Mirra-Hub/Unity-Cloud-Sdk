using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class SubmitScoreDto
    {
        public string playerName;
        public double value;
    }
}


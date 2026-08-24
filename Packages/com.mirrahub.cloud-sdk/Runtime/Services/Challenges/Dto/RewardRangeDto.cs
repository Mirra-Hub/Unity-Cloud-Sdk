using System;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed class RewardRangeDto
    {
        public double valueMin;
        public double valueMax;
        public RewardDataDto[] rewards;
    }

    [Serializable]
    public sealed class RewardDataDto
    {
        public string rewardId;
        public int economyResourceKind;
        public int count;
    }
}

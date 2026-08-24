using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class RewardDataDto
    {
        public string rewardId;
        public int economyResourceKind;
        public int count;
    }
}

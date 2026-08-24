using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Purchases.Dto
{
    [Serializable]
    public sealed class RewardDataDto
    {
        [JsonNameCamel] public string RewardId;
        [JsonNameCamel] public PurchaseRewardKind EconomyResourceKind;
        [JsonNameCamel] public int Count;
    }
}

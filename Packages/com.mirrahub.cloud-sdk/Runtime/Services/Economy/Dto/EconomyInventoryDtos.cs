using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Economy.Dto
{
    [Serializable]
    public sealed class PlayerInventoryDto
    {
        [JsonNameCamel] public List<WalletEntryDto> Wallet;
        [JsonNameCamel] public List<ItemSlotDto> Items;
        [JsonNameCamel] public List<EnergyBalanceDto> Energies;

        /// <summary>
        /// Rewards waiting to be claimed (<see cref="EconomyService.ClaimRewardsAsync"/>): what leaderboards,
        /// tournaments, challenges and other sources granted the player.
        /// </summary>
        [JsonNameCamel] public List<RewardContainerDto> Rewards;
    }

    /// <summary>What granted a reward container.</summary>
    public enum RewardSourceType
    {
        Leaderboard = 1,
        Tournament = 2,
        Challenge = 3,
        DailyReward = 4,
        Purchase = 5,
        PromoCode = 6
    }

    /// <summary>The rewards one source granted the player — e.g. a leaderboard for the session that ended.</summary>
    [Serializable]
    public sealed class RewardContainerDto
    {
        [JsonNameCamel] public RewardSourceType SourceType;

        /// <summary>
        /// The id the granting config had when it paid out, e.g. a leaderboard's <c>LeaderboardConfig.Id</c> — an edit
        /// of the config since gives it a new id.
        /// </summary>
        [JsonNameCamel] public string SourceId;

        [JsonNameCamel] public List<RewardEntryDto> Rewards;
    }

    [Serializable]
    public sealed class RewardEntryDto
    {
        /// <summary>The key of the economy resource, as in <see cref="EconomyService.Currencies"/> and the rest.</summary>
        [JsonName("rewardId")] public string RewardKey;

        [JsonNameCamel] public EconomyResourceKind EconomyResourceKind;
        [JsonNameCamel] public int Count;
    }

    [Serializable]
    public sealed class WalletEntryDto
    {
        [JsonNameCamel] public string CurrencyId;
        [JsonNameCamel] public decimal Balance;
    }

    [Serializable]
    public sealed class ItemSlotDto
    {
        [JsonNameCamel] public string SlotId;
        [JsonNameCamel] public string ItemId;
        [JsonNameCamel] public int Quantity;
        [JsonNameCamel] public string InventoryKey;
        [JsonNameCamel] public Dictionary<string, object> Properties;
    }

    [Serializable]
    public sealed class EnergyBalanceDto
    {
        [JsonNameCamel] public string EnergyId;
        [JsonNameCamel] public int CurrentValue;
        [JsonNameCamel] public int MaxValue;
        [JsonNameCamel] public int OverflowValue;
        [JsonNameCamel] public int? SecondsUntilNextRecharge;
        [JsonNameCamel] public int? SecondsUntilFullRecharge;
        [JsonNameCamel] public bool IsOnCooldown;
        [JsonNameCamel] public int? CooldownRemainingSeconds;
        [JsonNameCamel] public bool IsUnlimited;
        [JsonNameCamel] public int? UnlimitedRemainingSeconds;
    }

    [Serializable]
    public sealed class ModifyCurrencyDto
    {
        [JsonNameCamel] public string CurrencyId;
        [JsonNameCamel] public decimal Amount;
    }

    [Serializable]
    public sealed class ModifyItemDto
    {
        [JsonNameCamel] public string ItemId;
        [JsonNameCamel] public int Amount;
        [JsonNameCamel] public string InventoryKey;
    }

    [Serializable]
    public sealed class UpdateItemPropertiesDto
    {
        [JsonNameCamel] public string ItemId;
        [JsonNameCamel] public string SlotId;
        [JsonNameCamel] public string InventoryKey;
        [JsonNameCamel] public Dictionary<string, object> Properties;
    }

    [Serializable]
    public sealed class ConsumeItemDto
    {
        [JsonNameCamel] public string ItemId;
        [JsonNameCamel] public string SlotId;
        [JsonNameCamel] public string InventoryKey;
    }

    [Serializable]
    public sealed class ConsumeItemResponseDto
    {
        [JsonNameCamel] public List<GrantedCurrencyDto> GrantedCurrencies;
        [JsonNameCamel] public List<GrantedItemDto> GrantedItems;
        [JsonNameCamel] public List<GrantedEnergyDto> GrantedEnergies;
    }

    [Serializable]
    public sealed class GrantedCurrencyDto
    {
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public decimal Amount;
    }

    [Serializable]
    public sealed class GrantedItemDto
    {
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public int Quantity;
    }

    [Serializable]
    public sealed class GrantedEnergyDto
    {
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public int Amount;
        [JsonNameCamel] public int GrantType;
    }

    [Serializable]
    public sealed class ModifyEnergyDto
    {
        [JsonNameCamel] public string EnergyId;
        [JsonNameCamel] public int Amount;
    }

    [Serializable]
    public sealed class SetUnlimitedEnergyDto
    {
        [JsonNameCamel] public string EnergyId;
        [JsonNameCamel] public int DurationSeconds;
    }
}

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

using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class GetAuthDataDto
    {
        [JsonNameCamel] public AuthResultStatus Status;
        [JsonNameCamel] public string Token;
        [JsonNameCamel] public AccountDto PlayerInfo;
        [JsonNameCamel] public AccountDto CurrentAccount;
        [JsonNameCamel] public AccountDto ExistingAccount;
        [JsonNameCamel] public string GuestId;
        [JsonNameCamel] public SessionInfoDto Session;
    }
}

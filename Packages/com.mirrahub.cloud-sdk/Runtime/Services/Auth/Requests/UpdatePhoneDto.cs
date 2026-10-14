using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>The account's phone number in the international format (<c>+79991234567</c>); empty removes it.</summary>
    [Serializable]
    public class UpdatePhoneDto
    {
        public string PhoneNumber;
    }
}

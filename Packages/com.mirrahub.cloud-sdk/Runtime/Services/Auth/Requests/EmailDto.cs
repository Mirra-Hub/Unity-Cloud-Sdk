using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>An email: a new one for the account, or one to link sign-in by code to.</summary>
    [Serializable]
    public class EmailDto
    {
        public string Email;
    }
}

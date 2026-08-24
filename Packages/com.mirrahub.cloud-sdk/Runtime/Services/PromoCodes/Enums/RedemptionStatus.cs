namespace MirraCloud.Core.PromoCodes.Enums
{
    /// <summary>
    /// Mirror of backend <c>PromoCodes.Enums.RedemptionStatus</c> (values 1..9). The backend
    /// serialises enums as PascalCase strings, so this must stay a real enum — an int field
    /// would fail to deserialise.
    /// </summary>
    public enum RedemptionStatus
    {
        Success = 1,
        InvalidCode = 2,
        Expired = 3,
        NotYetActive = 4,
        Disabled = 5,
        LimitExceeded = 6,
        RuleFailed = 7,
        AlreadyRedeemed = 8,
        CodeBlocked = 9
    }
}

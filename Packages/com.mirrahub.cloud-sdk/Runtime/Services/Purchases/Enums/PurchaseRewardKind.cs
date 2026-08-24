namespace MirraCloud.Core.Purchases
{
    // Mirrors backend CloudShared.Enums.Economy.EconomyResourceKind (values 1/2/5).
    // We cannot reuse MirraCloud.Core.Economy.EconomyResourceKind because it uses
    // different numeric values (0/1/2) for its own purposes.
    public enum PurchaseRewardKind
    {
        Currency = 1,
        Item = 2,
        Energy = 5
    }
}

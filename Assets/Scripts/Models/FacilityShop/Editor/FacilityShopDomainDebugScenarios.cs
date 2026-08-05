using System;
using System.Linq;

public static class FacilityShopDomainDebugScenarios
{
    public static void Validate()
    {
        FacilityShopUnlockState state = new();
        if (!state.UnlockBasicPurchaseById(7)
            || state.UnlockBasicPurchaseById(7)
            || !state.MarkBlueprintAcquiredById(9))
        {
            throw new InvalidOperationException(
                "Facility-shop unlock commands are not idempotent.");
        }

        state.SetCurrentOfferDay(4);
        FacilityShopStateSnapshot snapshot = state.Capture();
        FacilityShopUnlockState restored = new();
        restored.Restore(snapshot);
        if (restored.CurrentOfferDay != 4
            || !restored.BasicPurchaseBuildingIds.SequenceEqual(new[] { 7 })
            || !restored.AcquiredBlueprintIds.SequenceEqual(new[] { 9 }))
        {
            throw new InvalidOperationException(
                "Facility-shop snapshot restore changed canonical state.");
        }
    }
}

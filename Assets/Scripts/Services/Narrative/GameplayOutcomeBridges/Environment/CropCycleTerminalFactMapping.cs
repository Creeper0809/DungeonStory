using System;

public static class CropCycleTerminalFactMapping
{
    public static CropPlanGameplayOutcomeReceipt Capture(
        CropCycleTerminalFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropCycleExecutionReceiptSaveData snapshot = fact.Snapshot();
        CropPlanExecutionReceipt projected =
            CropPlanExecutionReceiptAuthority.ProjectTerminal(
                snapshot.correlationId,
                snapshot);
        return EnvironmentOutcomeReceiptFactory.CreateCropPlan(
            projected,
            fact.PlotId,
            fact.PlotDisplayName,
            fact.CropDisplayName,
            fact.AbsoluteDay,
            fact.OwnerRevision);
    }
}

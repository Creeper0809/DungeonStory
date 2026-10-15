using DungeonStory.Environment;

public static class EnvironmentalFireResourceFactMapping
{
    public static EnvironmentalFireFuelOutcomeReceipt Capture(EnvironmentalFireFuelLostFact fact)
    {
        var value = fact.Snapshot();
        var input = fact.Physical.Receipt;
        var source = input.SourceFacts[0];
        return EnvironmentOutcomeReceiptFactory.CreateFireFuelLoss(new EnvironmentalFireFuelLossReceipt(
                new EnvironmentalFireTargetRef(EnvironmentalFireTargetKind.ItemStack, value.targetId),
                input.OperationId, input.Quantity, input.InputMassGrams, input.CommitId),
            source.StackId, source.DisplayName,
            NarrativeInferenceHash.ComputeSha256Utf8(input.RequestFingerprint),
            new CoreGridCell(value.positionX, value.positionY), value.absoluteDay, input.OwnerRevision);
    }
    public static EnvironmentalFireWaterOutcomeReceipt Capture(EnvironmentalFireWaterConsumedFact fact)
    {
        var value = fact.Snapshot();
        var input = fact.Physical.Receipt;
        var source = input.SourceFacts[0];
        return EnvironmentOutcomeReceiptFactory.CreateFireWaterConsumed(new EnvironmentalFireWaterReceipt(
                input.OperationId, value.leaseId, input.Quantity, input.CommitId),
            new CharacterId(value.workerId), value.workerName, source.ItemDefinitionId, source.DisplayName,
            input.InputMassGrams, NarrativeInferenceHash.ComputeSha256Utf8(input.RequestFingerprint),
            new CoreGridCell(value.positionX, value.positionY), value.absoluteDay, input.OwnerRevision);
    }
}

using DungeonStory.Foundation;

public interface ISurvivalTreatmentCompletionCommand
{
    bool TryEnsureTreatmentSupply(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out bool completionOnly,
        out DomainFailure failure);

    bool TryApplyTreatmentWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out int amount,
        out DomainFailure failure);
}

public interface ISurvivalTreatmentTerminalMaintenance
{
    bool TryReconcileLostTreatmentOwners(out string failureReason);
}

/// <summary>Stable treatment identities shared by execution and save validation.</summary>
internal static class SurvivalTreatmentIdentity
{
    internal const string TreatmentPhysicalSinkReason =
        "survival-treatment-material-consumed";
    internal const string DetoxOperationPrefix = "consumable-operation:detox:";
    internal const string TreatmentOperationPrefix = "consumable-operation:treatment:";

    internal static string CreateTreatmentPhysicalOperationId(
        string treatmentOperationId)
    {
        // This is a stable operation identity, not a URI path component.
        // Percent-escaping ':' makes the real physical outcome bridge reject it.
        string source = new GameplayOperationId(treatmentOperationId).Value;
        return new GameplayOperationId("survival-treatment-material:" + source).Value;
    }
}

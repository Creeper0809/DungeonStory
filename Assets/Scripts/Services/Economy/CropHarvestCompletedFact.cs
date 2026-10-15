using System;
using System.Linq;

// Approved work belongs to the crop cycle; log identities do not.
public sealed class CropHarvestCompletedFact
{
    public CropHarvestCompletedFact(BuildingInstanceId plotId,
        CropHarvestOutputSaveData harvest, CropCycleExecutionReceiptSaveData cycle)
    {
        if (!plotId.IsValid || harvest == null || cycle == null || cycle.plotId != plotId.Value
            || !Canonical(harvest.operationId) || !Canonical(harvest.harvesterId)
            || !Canonical(harvest.outcomeId) || !Canonical(cycle.correlationId)
            || harvest.operationSequence < 0 || harvest.completionAbsoluteDay < 0
            || cycle.harvestContributions == null)
            throw new ArgumentException("Crop harvest completion provenance is incomplete.");
        var contributions = cycle.harvestContributions
            .Where(value => value?.characterId == harvest.harvesterId).ToArray();
        if (contributions.Length > 1 || contributions.Length == 1
            && (!float.IsFinite(contributions[0].contributedWork) || contributions[0].contributedWork <= 0f))
            throw new ArgumentException("Crop completion has invalid approved contributions.");
        PlotId = plotId;
        OperationId = harvest.operationId;
        OperationSequence = harvest.operationSequence;
        WorkerId = new CharacterId(harvest.harvesterId);
        if (!WorkerId.IsValid) throw new ArgumentException("Crop completion worker is invalid.");
        OutcomeId = harvest.outcomeId;
        CycleId = cycle.correlationId;
        AbsoluteDay = harvest.completionAbsoluteDay;
        // A golden harvester can finalize work entirely supplied by others.
        ContributionWork = contributions.Length == 0 ? 0f : contributions[0].contributedWork;
        ParentConfirmed = harvest.cropOutcomeAcknowledged;
        ParentPublicationKey = harvest.terminalPublicationKey
            ?? throw new ArgumentException("Crop terminal publication key must be explicit.");
    }

    public BuildingInstanceId PlotId { get; }
    public string OperationId { get; }
    public int OperationSequence { get; }
    public CharacterId WorkerId { get; }
    public string OutcomeId { get; }
    public string CycleId { get; }
    public int AbsoluteDay { get; }
    public float ContributionWork { get; }
    public bool ParentConfirmed { get; }
    public string ParentPublicationKey { get; }

    private static bool Canonical(string value) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim() && !value.Any(char.IsControl);
}

public readonly struct CropPlotRetiredFact
{
    public CropPlotRetiredFact(BuildingInstanceId plotId)
    {
        if (!plotId.IsValid) throw new ArgumentException("Retired crop plot is invalid.");
        PlotId = plotId;
    }
    public BuildingInstanceId PlotId { get; }
}

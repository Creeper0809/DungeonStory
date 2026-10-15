using System;
using UnityEngine;

public readonly struct GolemUpkeepFact
{
    private readonly GolemUpkeepFactSaveData value;

    public GolemUpkeepFact(
        GolemUpkeepFactSaveData source,
        bool proposed = false)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate(proposed);
        value = source.Clone();
    }

    public GolemUpkeepFactSaveData Snapshot() =>
        value?.Clone()
        ?? throw new InvalidOperationException("golem-upkeep-fact-uninitialized");
}

public readonly struct SpeciesIncidentFact
{
    private readonly SpeciesIncidentFactSaveData value;

    public SpeciesIncidentFact(
        SpeciesIncidentFactSaveData source,
        bool proposed = false)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate(proposed);
        value = source.Clone();
    }

    public SpeciesIncidentFactSaveData Snapshot() =>
        value?.Clone()
        ?? throw new InvalidOperationException("species-incident-fact-uninitialized");
}

public readonly struct HarpyGaleFact
{
    private readonly HarpyGaleFactSaveData value;

    public HarpyGaleFact(HarpyGaleFactSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        value = source.Clone();
    }

    public HarpyGaleFactSaveData Snapshot() =>
        value?.Clone()
        ?? throw new InvalidOperationException("harpy-gale-fact-uninitialized");

    public PreparedPhysicalItemRelocationPreview ToPhysicalPreview()
    {
        HarpyGaleFactSaveData source = Snapshot();
        return new PreparedPhysicalItemRelocationPreview(
            new PhysicalItemRelocationReceipt(
                source.operationId,
                source.reasonCode,
                source.sourceStackId,
                source.destinationStackId,
                source.itemId,
                source.quantity,
                source.massGrams,
                new Vector2Int(source.sourceX, source.sourceY),
                new Vector2Int(
                    source.destinationX,
                    source.destinationY)),
            source.itemInstanceId,
            source.itemDisplayName,
            source.physicalRevision);
    }
}

using System;
using System.Collections.Generic;
using DungeonStory.Factions;

namespace DungeonStory.Infrastructure
{

public sealed class FactionSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonFactionSaveData,
        FactionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.factions";

    private readonly IFactionRuntime runtime;
    private readonly IDungeonItemCatalogProvider itemCatalog;

    public FactionSaveSection(
        IFactionRuntime runtime,
        IDungeonItemCatalogProvider itemCatalog)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonFactionSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        OffenseAggregateSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    protected override DungeonFactionSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override FactionRestoreCandidate BuildRestoreCandidate(
        DungeonFactionSaveData payload)
    {
        IReadOnlyList<string> errors = FactionPayloadValidation.Validate(
            payload,
            runtime.Definitions,
            itemId => itemCatalog.TryGetDefinition(itemId, out _));
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Faction restore candidate is invalid: "
                + string.Join(" | ", errors));
        }
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        FactionRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate);
    }
}
}

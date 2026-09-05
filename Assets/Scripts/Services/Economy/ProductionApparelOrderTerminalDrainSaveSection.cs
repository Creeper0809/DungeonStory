using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Current-format section for apparel order terminal producers.
/// The base-eight aggregate restore is complete before its exact producer
/// records are validated and published.
/// </summary>
public sealed class ProductionApparelOrderTerminalDrainSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonProductionApparelOrderTerminalDrainSaveData,
        ProductionApparelOrderTerminalDrainRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.production-apparel-terminal-drains";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id,
        ProductionPreparedOutputRoutingSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        EquipmentMaintenanceSaveSection.Id,
        CharacterEnvironmentSaveSection.Id
    };

    private readonly IProductionApparelOrderTerminalDrainQuery query;
    private readonly IProductionApparelOrderTerminalDrainCommand command;
    private readonly IProductionOutputLifecycleRestoreCandidateQuery lifecycle;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        publisher;
    private readonly ProductionApparelOrderTerminalDrainSaveValidation
        validation;

    public ProductionApparelOrderTerminalDrainSaveSection(
        IProductionApparelOrderTerminalDrainQuery query,
        IProductionApparelOrderTerminalDrainCommand command,
        IProductionOutputLifecycleRestoreCandidateQuery lifecycle,
        IProductionOutputLifecycleRestoreCandidatePublisher publisher,
        ProductionApparelOrderTerminalDrainSaveValidation validation)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.command = command
            ?? throw new ArgumentNullException(nameof(command));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.publisher = publisher
            ?? throw new ArgumentNullException(nameof(publisher));
        this.validation = validation
            ?? throw new ArgumentNullException(nameof(validation));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonProductionApparelOrderTerminalDrainSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonProductionApparelOrderTerminalDrainSaveData
        CapturePayload() => new()
    {
        version =
            DungeonProductionApparelOrderTerminalDrainSaveData.CurrentVersion,
        entries = (query.CaptureCurrentFormat()
                ?? Array.Empty<ProductionApparelOrderTerminalDrainSaveData>())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .Select(value => value?.Clone())
            .ToList()
    };

    protected override ProductionApparelOrderTerminalDrainRestoreCandidate
        BuildRestoreCandidate(
            DungeonProductionApparelOrderTerminalDrainSaveData payload)
    {
        validation.ValidateOwnPayload(payload);
        return new ProductionApparelOrderTerminalDrainRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ProductionApparelOrderTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        if (!lifecycle.TryCapture(out _))
        {
            throw new InvalidOperationException(
                "Apparel order terminal-drain staged commit requires all eight lifecycle candidates; found "
                + lifecycle.PublishedSourceCount + "/8.");
        }
        if (!command.TryRestoreCurrentFormat(
                candidate.Payload.entries,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Apparel order terminal-drain current-format restore failed: "
                + failureReason);
        }
    }

    protected override void PublishRestoreCandidateProjection(
        DungeonProductionApparelOrderTerminalDrainSaveData payload,
        ProductionApparelOrderTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        publisher.SetApparelTerminalDrains(payload);
    }

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "entries");
}

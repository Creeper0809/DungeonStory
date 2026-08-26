using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unregistered current-format section for combat craft/repair terminal
/// producers. It validates only after the base-eight lifecycle and detached
/// Items child candidates have been staged.
/// </summary>
public sealed class CombatEquipmentTerminalDrainSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCombatEquipmentTerminalDrainSaveData,
        CombatEquipmentTerminalDrainRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.equipment-terminal-drains";

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

    private readonly ICombatEquipmentTerminalDrainQuery query;
    private readonly ICombatEquipmentTerminalDrainCommand command;
    private readonly IProductionOutputLifecycleRestoreCandidateQuery lifecycle;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        publisher;
    private readonly IProductionInputDestinationCustodyDrainRestoreCandidateQuery
        inputDrains;
    private readonly CombatEquipmentTerminalDrainSaveValidation validation;

    public CombatEquipmentTerminalDrainSaveSection(
        ICombatEquipmentTerminalDrainQuery query,
        ICombatEquipmentTerminalDrainCommand command,
        IProductionOutputLifecycleRestoreCandidateQuery lifecycle,
        IProductionOutputLifecycleRestoreCandidatePublisher publisher,
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery inputDrains,
        CombatEquipmentTerminalDrainSaveValidation validation)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.command = command
            ?? throw new ArgumentNullException(nameof(command));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.publisher = publisher
            ?? throw new ArgumentNullException(nameof(publisher));
        this.inputDrains = inputDrains
            ?? throw new ArgumentNullException(nameof(inputDrains));
        this.validation = validation
            ?? throw new ArgumentNullException(nameof(validation));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCombatEquipmentTerminalDrainSaveData
        CapturePayload() => new()
    {
        version = DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion,
        entries = (query.CaptureCurrentFormat()
                ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .Select(value => value?.Clone())
            .ToList()
    };

    protected override CombatEquipmentTerminalDrainRestoreCandidate
        BuildRestoreCandidate(
            DungeonCombatEquipmentTerminalDrainSaveData payload)
    {
        validation.ValidateOwnPayload(payload);
        return new CombatEquipmentTerminalDrainRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        CombatEquipmentTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        if (!lifecycle.TryCapture(out _))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal-drain staged commit requires all eight lifecycle candidates; found "
                + lifecycle.PublishedSourceCount + "/8.");
        }
        if (!inputDrains.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Combat equipment terminal-drain staged commit requires the detached Items input-drain candidate.");
        }
        if (!command.TryRestoreCurrentFormat(
                candidate.Payload.entries,
                inputDrains.Drains,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal-drain current-format restore failed: "
                + failureReason);
        }
    }

    protected override void PublishRestoreCandidateProjection(
        DungeonCombatEquipmentTerminalDrainSaveData payload,
        CombatEquipmentTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        publisher.SetCombatTerminalDrains(payload);
    }

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "entries");
}

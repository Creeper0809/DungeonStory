using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unregistered current-format section for the Production-owned generic bill
/// terminal producer. Registration is intentionally deferred until the upper
/// destructive-drain journal joins the complete participant set.
/// </summary>
public sealed class ProductionGenericBillTerminalDrainSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonProductionGenericBillTerminalDrainSaveData,
        ProductionGenericBillTerminalDrainRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id =
        "economy.production-generic-bill-terminal-drains";

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

    private readonly IProductionGenericBillTerminalDrainQuery query;
    private readonly IProductionGenericBillTerminalDrainCommand command;
    private readonly IProductionOutputLifecycleRestoreCandidateQuery
        lifecycleCandidates;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleCandidatePublisher;
    private readonly IProductionInputDestinationCustodyDrainRestoreCandidateQuery
        inputDrainCandidates;
    private readonly ProductionGenericBillTerminalDrainSaveValidation validation;

    public ProductionGenericBillTerminalDrainSaveSection(
        IProductionGenericBillTerminalDrainQuery query,
        IProductionGenericBillTerminalDrainCommand command,
        IProductionOutputLifecycleRestoreCandidateQuery lifecycleCandidates,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleCandidatePublisher,
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery
            inputDrainCandidates,
        ProductionGenericBillTerminalDrainSaveValidation validation)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.command = command
            ?? throw new ArgumentNullException(nameof(command));
        this.lifecycleCandidates = lifecycleCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleCandidates));
        this.lifecycleCandidatePublisher = lifecycleCandidatePublisher
            ?? throw new ArgumentNullException(
                nameof(lifecycleCandidatePublisher));
        this.inputDrainCandidates = inputDrainCandidates
            ?? throw new ArgumentNullException(nameof(inputDrainCandidates));
        this.validation = validation
            ?? throw new ArgumentNullException(nameof(validation));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonProductionGenericBillTerminalDrainSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonProductionGenericBillTerminalDrainSaveData
        CapturePayload() => new()
    {
        version = DungeonProductionGenericBillTerminalDrainSaveData
            .CurrentVersion,
        entries = (query.CaptureCurrentFormat()
                ?? Array.Empty<ProductionGenericBillTerminalDrainSaveData>())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .Select(value => value?.Clone())
            .ToList()
    };

    protected override ProductionGenericBillTerminalDrainRestoreCandidate
        BuildRestoreCandidate(
            DungeonProductionGenericBillTerminalDrainSaveData payload)
    {
        validation.ValidateOwnPayload(payload);
        return new ProductionGenericBillTerminalDrainRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidateProjection(
        DungeonProductionGenericBillTerminalDrainSaveData payload,
        ProductionGenericBillTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        lifecycleCandidatePublisher.SetGenericTerminalDrains(payload);
    }

    protected override void PublishRestoreCandidate(
        ProductionGenericBillTerminalDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        if (!lifecycleCandidates.TryCapture(
                out ProductionOutputLifecycleRestoreCandidateBundle lifecycle))
        {
            throw new InvalidOperationException(
                "Generic terminal-drain staged commit requires all eight lifecycle candidates; found "
                + lifecycleCandidates.PublishedSourceCount + "/8.");
        }
        if (!inputDrainCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Generic terminal-drain staged commit requires the detached Items input-drain candidate.");
        }

        validation.ValidateCrossAggregate(
            lifecycle,
            candidate.Payload,
            inputDrainCandidates);
        if (!command.TryRestoreCurrentFormat(
                candidate.Payload.entries,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Generic terminal-drain current-format restore failed: "
                + failureReason);
        }
    }

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(payloadJson, "entries");
    }
}

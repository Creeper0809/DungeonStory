using System;
using System.Collections.Generic;
using DungeonStory.Infrastructure;

public sealed class CharacterNarrativeSaveSection :
    DungeonStrictJsonSaveSection<CharacterNarrativeWorldSaveData, CharacterNarrativeAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.narrative";
    private readonly ICharacterNarrativePersistence persistence;
    public CharacterNarrativeSaveSection(ICharacterNarrativePersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterNarrativeWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CharacterLifeSaveSection.Id,
        KinshipHouseholdSaveSection.Id,
        CharacterCareerSaveSection.Id
    };
    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(
            payloadJson,
            "characters",
            "identityStates",
            "workCompletionDeliveries");
    protected override CharacterNarrativeWorldSaveData CapturePayload() => persistence.Capture();
    protected override CharacterNarrativeAggregateState BuildRestoreCandidate(CharacterNarrativeWorldSaveData payload) => persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(CharacterNarrativeAggregateState candidate) => persistence.PublishRestore(candidate);
}

public sealed class SeasonalWorldEventsSaveSection :
    DungeonStrictJsonSaveSection<SeasonalEventWorldSaveData, SeasonalEventAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.seasonal-events";
    private readonly IV20CampaignPersistence persistence;
    public SeasonalWorldEventsSaveSection(IV20CampaignPersistence persistence) => this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => SeasonalEventWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CalendarClimateSaveSection.Id,
        CropEcologySaveSection.Id,
        WildlifeSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };
    protected override SeasonalEventWorldSaveData CapturePayload() => persistence.CaptureSeasonal();
    protected override SeasonalEventAggregateState BuildRestoreCandidate(SeasonalEventWorldSaveData payload) => persistence.PrepareSeasonal(payload);
    protected override void PublishRestoreCandidate(SeasonalEventAggregateState candidate) => persistence.PublishSeasonal(candidate);
}

public sealed class SocietyEventsSaveSection :
    DungeonStrictJsonSaveSection<SocietyEventWorldSaveData, SocietyEventAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "society.events";
    private readonly IV20CampaignPersistence persistence;
    public SocietyEventsSaveSection(IV20CampaignPersistence persistence) => this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => SocietyEventWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CharacterNarrativeSaveSection.Id,
        SeasonalWorldEventsSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PopulationHealthSaveSection.Id
    };
    protected override SocietyEventWorldSaveData CapturePayload() => persistence.CaptureSociety();
    protected override SocietyEventAggregateState BuildRestoreCandidate(SocietyEventWorldSaveData payload) => persistence.PrepareSociety(payload);
    protected override void PublishRestoreCandidate(SocietyEventAggregateState candidate) => persistence.PublishSociety(candidate);
}

public sealed class FactionCampaignSaveSection :
    DungeonStrictJsonSaveSection<FactionCampaignWorldSaveData, FactionCampaignAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "factions.campaign";
    private readonly IV20CampaignPersistence persistence;
    public FactionCampaignSaveSection(IV20CampaignPersistence persistence) => this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => FactionCampaignWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        FactionSaveSection.Id,
        SocietyEventsSaveSection.Id,
        OffenseAggregateSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    protected override FactionCampaignWorldSaveData CapturePayload() => persistence.CaptureFactions();
    protected override FactionCampaignAggregateState BuildRestoreCandidate(FactionCampaignWorldSaveData payload) => persistence.PrepareFactions(payload);
    protected override void PublishRestoreCandidate(FactionCampaignAggregateState candidate) => persistence.PublishFactions(candidate);
}

public sealed class RunMilestonesSaveSection :
    DungeonStrictJsonSaveSection<RunMilestoneWorldSaveData, RunMilestoneAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "run.milestones";
    private readonly IV20CampaignPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    public RunMilestonesSaveSection(
        IV20CampaignPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }
    public override string SectionId => Id;
    public override int SectionVersion => RunMilestoneWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        BlueprintResearchSaveSection.Id,
        ProductionBillsSaveSection.Id,
        OffenseAggregateSaveSection.Id,
        FactionCampaignSaveSection.Id,
        CharacterCareerSaveSection.Id
    };
    protected override RunMilestoneWorldSaveData CapturePayload() => persistence.CaptureMilestones();
    protected override RunMilestoneAggregateState BuildRestoreCandidate(RunMilestoneWorldSaveData payload)
    {
        ValidateAccordSignalPhysicalJoin(payload, physicalCandidates);
        return persistence.PrepareMilestones(payload);
    }

    protected override void ValidateParsedPayload(
        RunMilestoneWorldSaveData payload)
    {
        _ = persistence.PrepareMilestones(payload)
            ?? throw new InvalidOperationException(
                "Run milestone restore candidate builder returned null.");
    }

    public static void ValidateAccordSignalPhysicalJoin(
        RunMilestoneWorldSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        const string prefix = "accord-signal-support:";
        const string reason = "alliance-signal-kit-consumed";
        if (payload == null || query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException("Run milestone restore requires the incoming physical candidate.");
        bool hasOwner = !string.IsNullOrEmpty(payload.pendingAccordSignalOperationId);
        if (hasOwner)
        {
            if (!query.TryGetPendingBatchDisposition(payload.pendingAccordSignalOperationId, out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Sink
                || !string.Equals(receipt.ReasonCode, reason, StringComparison.Ordinal)
                || !string.Equals(receipt.CommitId, payload.pendingAccordSignalCommitId, StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.InputMassGrams != payload.pendingAccordSignalMassGrams
                || receipt.SourceStackIds.Count != 1
                || !string.Equals(receipt.SourceStackIds[0], payload.pendingAccordSignalSourceStackId, StringComparison.Ordinal))
                throw new InvalidOperationException("Pending accord signal has no exact incoming physical Sink receipt.");
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null || !receipt.OperationId.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!hasOwner || !string.Equals(receipt.OperationId, payload.pendingAccordSignalOperationId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Incoming accord signal Sink '{receipt.OperationId}' has no milestone owner.");
        }
    }
    protected override void PublishRestoreCandidate(RunMilestoneAggregateState candidate) => persistence.PublishMilestones(candidate);
}

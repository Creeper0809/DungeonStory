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
        RequireTopLevelArrayFields(payloadJson, "characters", "identityStates");
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
    public RunMilestonesSaveSection(IV20CampaignPersistence persistence) => this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => RunMilestoneWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        BlueprintResearchSaveSection.Id,
        ProductionBillsSaveSection.Id,
        OffenseAggregateSaveSection.Id,
        FactionCampaignSaveSection.Id,
        CharacterCareerSaveSection.Id
    };
    protected override RunMilestoneWorldSaveData CapturePayload() => persistence.CaptureMilestones();
    protected override RunMilestoneAggregateState BuildRestoreCandidate(RunMilestoneWorldSaveData payload) => persistence.PrepareMilestones(payload);
    protected override void PublishRestoreCandidate(RunMilestoneAggregateState candidate) => persistence.PublishMilestones(candidate);
}

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FoundationSessionSaveSection :
    DungeonStrictJsonSaveSection<GameSessionSaveData, FoundationSessionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "foundation.session";
    private readonly IGameSessionPersistence persistence;

    public FoundationSessionSaveSection(IGameSessionPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => GameSessionSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Foundation;
    protected override GameSessionSaveData CapturePayload() => persistence.CaptureSession();
    protected override FoundationSessionRestoreCandidate BuildRestoreCandidate(GameSessionSaveData payload) =>
        new(persistence.PrepareSessionRestore(payload));
    protected override void PublishRestoreCandidate(FoundationSessionRestoreCandidate candidate) =>
        persistence.StageSessionRestore(candidate.Snapshot);
}

public sealed class FoundationSessionRestoreCandidate
{
    public FoundationSessionRestoreCandidate(GameSessionSnapshot snapshot) => Snapshot = snapshot;
    public GameSessionSnapshot Snapshot { get; }
}

public sealed class CalendarClimateSaveSection :
    DungeonStrictJsonSaveSection<ClimateWorldSaveData, ClimateAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.calendar-climate";
    private readonly IClimatePersistence persistence;
    public CalendarClimateSaveSection(IClimatePersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => ClimateWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.World;
    public override IReadOnlyList<string> DependsOn => new[] { FoundationSessionSaveSection.Id };
    protected override ClimateWorldSaveData CapturePayload() => persistence.Capture();
    protected override ClimateAggregateState BuildRestoreCandidate(ClimateWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(ClimateAggregateState candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class CharacterLifeSaveSection :
    DungeonStrictJsonSaveSection<CharacterLifeWorldSaveData, CharacterLifeRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.life";
    private readonly ICharacterLifePersistence persistence;
    private readonly ICharacterLifeQuery query;
    private readonly ICharacterLifeCommand commands;
    private readonly ICharacterWorldPersistenceIdentityQuery persistentCharacters;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly ICharacterLifePublicationService lifePublication;

    public CharacterLifeSaveSection(
        ICharacterLifePersistence persistence,
        ICharacterLifeQuery query,
        ICharacterLifeCommand commands,
        ICharacterWorldPersistenceIdentityQuery persistentCharacters,
        ICharacterLifetimeQuery characterLifetime,
        ICharacterLifePublicationService lifePublication)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.persistentCharacters = persistentCharacters
            ?? throw new ArgumentNullException(nameof(persistentCharacters));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.lifePublication = lifePublication
            ?? throw new ArgumentNullException(nameof(lifePublication));
    }
    public override string SectionId => Id;
    public override int SectionVersion => CharacterLifeWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Characters;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    protected override CharacterLifeWorldSaveData CapturePayload()
    {
        HashSet<CharacterId> persistentIds = new(
            persistentCharacters.GetPersistentCharacterIds()
                ?? Array.Empty<CharacterId>());
        CharacterActor[] persistentActors = (characterLifetime.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(CharacterWorldPersistenceRules.IsPersistentActor)
            .Where(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                && persistentIds.Contains(id))
            .ToArray();
        foreach (CharacterActor actor in persistentActors)
        {
            lifePublication.EnsureRegistered(actor);
        }

        CharacterId[] staleIds = query.Records
            .Select(record => record.CharacterId)
            .Where(id => !persistentIds.Contains(id))
            .ToArray();
        foreach (CharacterId staleId in staleIds)
        {
            commands.Remove(staleId);
        }

        return persistence.Capture();
    }
    protected override CharacterLifeRestoreCandidate BuildRestoreCandidate(CharacterLifeWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(CharacterLifeRestoreCandidate candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class KinshipHouseholdSaveSection :
    DungeonStrictJsonSaveSection<KinshipHouseholdWorldSaveData, KinshipHouseholdAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.kinship-households";
    private readonly IKinshipHouseholdPersistence persistence;

    public KinshipHouseholdSaveSection(IKinshipHouseholdPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => KinshipHouseholdWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Characters;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterLifeSaveSection.Id };
    protected override KinshipHouseholdWorldSaveData CapturePayload() => persistence.Capture();
    protected override KinshipHouseholdAggregateState BuildRestoreCandidate(KinshipHouseholdWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(KinshipHouseholdAggregateState candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class ReproductionSaveSection :
    DungeonStrictJsonSaveSection<ReproductionWorldSaveData, ReproductionWorldAggregate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.reproduction";
    private readonly IReproductionPersistence persistence;

    public ReproductionSaveSection(IReproductionPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => ReproductionWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        KinshipHouseholdSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };
    protected override ReproductionWorldSaveData CapturePayload() => persistence.Capture();
    protected override ReproductionWorldAggregate BuildRestoreCandidate(ReproductionWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(ReproductionWorldAggregate candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class PopulationHealthSaveSection :
    DungeonStrictJsonSaveSection<PopulationHealthWorldSaveData, PopulationHealthAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "population.epidemics";
    private readonly IPopulationHealthPersistence persistence;
    public PopulationHealthSaveSection(IPopulationHealthPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => PopulationHealthWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CharacterLifeSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };
    protected override PopulationHealthWorldSaveData CapturePayload() => persistence.Capture();
    protected override PopulationHealthAggregateState BuildRestoreCandidate(PopulationHealthWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(PopulationHealthAggregateState candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class CharacterCareerSaveSection :
    DungeonStrictJsonSaveSection<CharacterCareerWorldSaveData, CharacterCareerAggregate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.careers";
    private readonly ICareerPersistence persistence;

    public CharacterCareerSaveSection(ICareerPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterCareerWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        WorkOrdersSaveSection.Id,
        BlueprintResearchSaveSection.Id,
        ReproductionSaveSection.Id
    };
    protected override CharacterCareerWorldSaveData CapturePayload() => persistence.Capture();
    protected override CharacterCareerAggregate BuildRestoreCandidate(CharacterCareerWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(CharacterCareerAggregate candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class CharacterPsychosocialSaveSection :
    DungeonStrictJsonSaveSection<CharacterPsychosocialWorldSaveData, PsychosocialAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.psychosocial";
    private readonly IPsychosocialPersistence persistence;

    public CharacterPsychosocialSaveSection(IPsychosocialPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterPsychosocialWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        PopulationHealthSaveSection.Id,
        CharacterCareerSaveSection.Id,
        KinshipHouseholdSaveSection.Id
    };
    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "characters");
    protected override CharacterPsychosocialWorldSaveData CapturePayload() => persistence.Capture();
    protected override PsychosocialAggregateState BuildRestoreCandidate(CharacterPsychosocialWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(PsychosocialAggregateState candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class CropEcologySaveSection :
    DungeonStrictJsonSaveSection<CropEcologyWorldSaveData, CropEcologyAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.crop-ecology";
    private readonly ICropEcologyPersistence persistence;
    public CropEcologySaveSection(ICropEcologyPersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => CropEcologyWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CropPlotSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    protected override CropEcologyWorldSaveData CapturePayload() => persistence.Capture();
    protected override CropEcologyAggregateState BuildRestoreCandidate(CropEcologyWorldSaveData payload) =>
        persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(CropEcologyAggregateState candidate) =>
        persistence.PublishRestore(candidate);
}

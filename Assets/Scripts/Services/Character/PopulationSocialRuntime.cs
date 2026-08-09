using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

[Serializable]
public sealed class KinshipHouseholdWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public KinshipWorldSaveData kinship = new();
    public HouseholdWorldSaveData households = new();
}

public interface IHouseholdService
{
    void Assign(CharacterId characterId, HouseholdId householdId, BuildingInstanceId roomId, BuildingInstanceId bedId);
    void Clear(CharacterId characterId);
    bool TryGet(CharacterId characterId, out CharacterRoomAssignmentSaveData assignment);
    IReadOnlyList<CharacterId> GetMembers(HouseholdId householdId);
}

public interface IKinshipHouseholdPersistence
{
    KinshipHouseholdWorldSaveData Capture();
    KinshipHouseholdAggregateState PrepareRestore(KinshipHouseholdWorldSaveData data);
    void PublishRestore(KinshipHouseholdAggregateState candidate);
}

public sealed class KinshipHouseholdAggregateState
{
    public KinshipHouseholdAggregateState(
        CharacterKinshipAggregate kinship,
        CharacterHouseholdAggregate households)
    {
        Kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        Households = households ?? throw new ArgumentNullException(nameof(households));
    }
    public CharacterKinshipAggregate Kinship { get; }
    public CharacterHouseholdAggregate Households { get; }
    public KinshipHouseholdWorldSaveData Capture() => new()
    {
        kinship = Kinship.Capture(),
        households = Households.Capture()
    };
    public static KinshipHouseholdAggregateState Restore(KinshipHouseholdWorldSaveData data)
    {
        if (data == null || data.version != KinshipHouseholdWorldSaveData.CurrentVersion)
            throw new InvalidOperationException("Kinship-household payload is unsupported.");
        return new KinshipHouseholdAggregateState(
            CharacterKinshipAggregate.Restore(data.kinship),
            CharacterHouseholdAggregate.Restore(data.households));
    }
}

public sealed class KinshipHouseholdRuntime :
    IKinshipQuery,
    IKinshipCommand,
    IHouseholdService,
    IKinshipHouseholdPersistence
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    public KinshipHouseholdRuntime(DungeonRuntimeAggregateRootStore rootStore) =>
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));

    public IReadOnlyCollection<CharacterTombstoneSaveData> Tombstones =>
        Current.Kinship.Tombstones;

    public IReadOnlyList<CharacterId> GetParents(CharacterId child, bool includeAdoptive) =>
        Current.Kinship.GetParents(child, includeAdoptive);
    public IReadOnlyList<CharacterId> GetChildren(CharacterId parent, bool includeAdoptive) =>
        Current.Kinship.GetChildren(parent, includeAdoptive);
    public int GetGeneration(CharacterId characterId) =>
        Current.Kinship.GetGeneration(characterId);
    public bool IsAncestor(CharacterId possibleAncestor, CharacterId descendant, int maximumDepth) =>
        Current.Kinship.IsAncestor(possibleAncestor, descendant, maximumDepth);
    public bool IsSibling(CharacterId left, CharacterId right) => Current.Kinship.IsSibling(left, right);
    public KinshipRestriction GetPartnershipOrReproductionRestriction(CharacterId left, CharacterId right) =>
        Current.Kinship.GetPartnershipOrReproductionRestriction(left, right);
    public CharacterId GetPartner(CharacterId characterId) => Current.Kinship.GetPartner(characterId);
    public CharacterId GetGuardian(CharacterId child) => Current.Kinship.GetGuardian(child);
    public bool TryGetTombstone(
        CharacterId characterId,
        out CharacterTombstoneSaveData tombstone) =>
        Current.Kinship.TryGetTombstone(characterId, out tombstone);
    public void AddParent(CharacterId child, CharacterId parent, bool adoptive) =>
        Writable.Kinship.AddParent(child, parent, adoptive);
    public void SetPartner(CharacterId left, CharacterId right) => Writable.Kinship.SetPartner(left, right);
    public void ClearPartner(CharacterId characterId) => Writable.Kinship.ClearPartner(characterId);
    public void SetGuardian(CharacterId child, CharacterId guardian) => Writable.Kinship.SetGuardian(child, guardian);
    public void ArchiveDeath(CharacterId characterId, CharacterSpeciesId phenotypeSpeciesId,
        int birthAbsoluteDay, int deathAbsoluteDay, bool famous,
        HouseholdId householdId, int generation) =>
        Writable.Kinship.ArchiveDeath(characterId, phenotypeSpeciesId, birthAbsoluteDay,
            deathAbsoluteDay, famous, householdId, generation);
    public void ArchiveColdData(int currentAbsoluteDay,
        IReadOnlyCollection<CharacterId> livingCharacters) =>
        Writable.Kinship.ArchiveColdData(currentAbsoluteDay, livingCharacters);
    public void Assign(CharacterId characterId, HouseholdId householdId,
        BuildingInstanceId roomId, BuildingInstanceId bedId) =>
        Writable.Households.Assign(characterId, householdId, roomId, bedId);
    public void Clear(CharacterId characterId) => Writable.Households.Clear(characterId);
    public bool TryGet(CharacterId characterId, out CharacterRoomAssignmentSaveData assignment) =>
        Current.Households.TryGet(characterId, out assignment);
    public IReadOnlyList<CharacterId> GetMembers(HouseholdId householdId) =>
        Current.Households.GetMembers(householdId);
    public KinshipHouseholdWorldSaveData Capture() => Current.Capture();
    public KinshipHouseholdAggregateState PrepareRestore(KinshipHouseholdWorldSaveData data) =>
        KinshipHouseholdAggregateState.Restore(data);
    public void PublishRestore(KinshipHouseholdAggregateState candidate) =>
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));

    private KinshipHouseholdAggregateState Current => rootStore.GetOrCreate(CreateFresh);
    private KinshipHouseholdAggregateState Writable => rootStore.GetOrCreateWritable(
        CreateFresh,
        value => KinshipHouseholdAggregateState.Restore(value.Capture()));
    private static KinshipHouseholdAggregateState CreateFresh() =>
        new(new CharacterKinshipAggregate(), new CharacterHouseholdAggregate());
}

public sealed class ReproductionRuntime : IReproductionService, IReproductionPersistence
{
    private const string RandomStreamId = "population:reproduction";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IReproductionDefinitionCatalog definitions;
    private readonly ICharacterLifeQuery life;
    private readonly IKinshipQuery kinship;
    private readonly ICharacterWorldQuery world;
    private readonly IRandomStream random;
    public ReproductionRuntime(DungeonRuntimeAggregateRootStore rootStore,
        IReproductionDefinitionCatalog definitions,
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        ICharacterWorldQuery world,
        IRandomStreamProvider randomStreams)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams))).Get(RandomStreamId);
    }
    public FamilyPlanningPolicy FamilyPlanningPolicy => Current.FamilyPlanningPolicy;
    public int LastAllowedPolicyEvaluationDay =>
        Current.LastAllowedPolicyEvaluationDay;
    public IReadOnlyList<ReproductionProcess> Processes => Current.Processes;
    public void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy) => Writable.SetFamilyPlanningPolicy(policy);
    public void MarkAllowedPolicyEvaluation(int absoluteDay) =>
        Writable.MarkAllowedPolicyEvaluation(absoluteDay);
    public void AddProcess(ReproductionProcess process)
    {
        ValidateNewProcess(process);
        Writable.Add(process);
    }
    public void AdvanceProcess(string processId, ReproductionDailyContext context)
    {
        if (!Writable.TryGet(processId, out ReproductionProcess process))
            throw new KeyNotFoundException($"Unknown reproduction process '{processId}'.");
        process.AdvanceDay(context, random.NextFloat());
    }
    public void NotifyCarrierDeath(CharacterId carrierId, int absoluteDay)
    {
        foreach (ReproductionProcess process in Writable.Processes.Where(value => value.CarrierId.Equals(carrierId)))
            process.NotifyCarrierDeath(absoluteDay);
    }
    public void EmergencyExtract(string processId, int absoluteDay)
    {
        if (!Writable.TryGet(processId, out ReproductionProcess process))
            throw new KeyNotFoundException($"Unknown reproduction process '{processId}'.");
        process.EmergencyExtract(absoluteDay);
    }
    public void MarkResultPublished(string processId, CharacterId resultCharacterId)
    {
        if (!Writable.TryGet(processId, out ReproductionProcess process))
            throw new KeyNotFoundException($"Unknown reproduction process '{processId}'.");
        process.MarkResultPublished(resultCharacterId);
    }
    public ReproductionWorldSaveData Capture() => Current.Capture();
    public ReproductionWorldAggregate PrepareRestore(ReproductionWorldSaveData data) =>
        ReproductionWorldAggregate.Restore(data, definitions);
    public void PublishRestore(ReproductionWorldAggregate candidate) =>
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
    private ReproductionWorldAggregate Current => rootStore.GetOrCreate(() => new ReproductionWorldAggregate());
    private ReproductionWorldAggregate Writable => rootStore.GetOrCreateWritable(
        () => new ReproductionWorldAggregate(),
        value => ReproductionWorldAggregate.Restore(value.Capture(), definitions));

    private void ValidateNewProcess(ReproductionProcess process)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));
        if (FamilyPlanningPolicy == FamilyPlanningPolicy.Off)
            throw new InvalidOperationException("Family planning is disabled.");
        if (Current.Processes.Any(value => IsActive(value)
            && SharesParticipant(value, process)))
        {
            throw new InvalidOperationException(
                "A reproduction participant already has an active process.");
        }

        CharacterLifeRecord first = RequireReproductiveAdult(
            process.FirstParentId);
        CharacterActor firstActor = RequireLivingActor(process.FirstParentId);
        if (process.Mode == ReproductionMode.GolemAssembly)
        {
            if (firstActor.Identity?.Profile?.ReproductiveRole
                != ReproductiveRole.Assembler)
                throw new InvalidOperationException(
                    "Golem assembly requires an adult assembler.");
            return;
        }

        ReproductionDefinition firstDefinition = definitions.RequireReproduction(
            first.PhenotypeSpeciesId);

        CharacterLifeRecord second = RequireReproductiveAdult(
            process.SecondParentId);
        ReproductionDefinition secondDefinition = definitions.RequireReproduction(
            second.PhenotypeSpeciesId);
        CharacterActor secondActor = RequireLivingActor(process.SecondParentId);
        ReproductionFailureCode pairFailure = ReproductionRules.ValidatePair(
            process.FirstParentId,
            process.SecondParentId,
            firstDefinition,
            secondDefinition,
            kinship,
            process.CrossLineageIncubatorUsed);
        if (pairFailure != ReproductionFailureCode.None)
            throw new InvalidOperationException(
                $"Reproduction pair is invalid: {pairFailure}.");
        if (!process.PhenotypeSpeciesId.Equals(first.PhenotypeSpeciesId)
            && !process.PhenotypeSpeciesId.Equals(second.PhenotypeSpeciesId))
            throw new InvalidOperationException(
                "Offspring phenotype must be one of the two parent phenotypes.");
        ValidateRoles(process, firstActor, secondActor);
    }

    private CharacterLifeRecord RequireReproductiveAdult(CharacterId id)
    {
        if (!life.TryGet(id, out CharacterLifeRecord record)
            || record.LifeStage != CharacterLifeStage.Adult)
            throw new InvalidOperationException(
                $"Reproduction participant '{id.Value}' is not a living reproductive adult.");
        return record;
    }

    private CharacterActor RequireLivingActor(CharacterId id) =>
        world.Characters.FirstOrDefault(actor => actor != null && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId candidate)
            && candidate.Equals(id))
        ?? throw new InvalidOperationException(
            $"Reproduction participant '{id.Value}' is not in the living world.");

    private static void ValidateRoles(
        ReproductionProcess process,
        CharacterActor first,
        CharacterActor second)
    {
        ReproductiveRole firstRole = first.Identity?.Profile?.ReproductiveRole
            ?? ReproductiveRole.None;
        ReproductiveRole secondRole = second.Identity?.Profile?.ReproductiveRole
            ?? ReproductiveRole.None;
        if (process.CrossLineageIncubatorUsed)
        {
            if (firstRole == ReproductiveRole.None
                || secondRole == ReproductiveRole.None
                || !process.CarrierId.Equals(process.FirstParentId)
                    && !process.CarrierId.Equals(process.SecondParentId))
                throw new InvalidOperationException(
                    "Cross-lineage reproduction requires two authored roles and a parent carrier.");
            return;
        }
        bool valid = process.Mode switch
        {
            ReproductionMode.Pregnancy =>
                HasComplementaryRoles(firstRole, secondRole,
                    ReproductiveRole.Carrier, ReproductiveRole.Contributor)
                && (process.CarrierId.Equals(process.FirstParentId)
                        && firstRole == ReproductiveRole.Carrier
                    || process.CarrierId.Equals(process.SecondParentId)
                        && secondRole == ReproductiveRole.Carrier),
            ReproductionMode.Egg =>
                HasComplementaryRoles(firstRole, secondRole,
                    ReproductiveRole.Layer, ReproductiveRole.Fertilizer)
                && (process.CarrierId.Equals(process.FirstParentId)
                        && firstRole == ReproductiveRole.Layer
                    || process.CarrierId.Equals(process.SecondParentId)
                        && secondRole == ReproductiveRole.Layer),
            ReproductionMode.Spore => firstRole == ReproductiveRole.SporeContributor
                && secondRole == ReproductiveRole.SporeContributor,
            ReproductionMode.CoreDivision => firstRole == ReproductiveRole.DivisionCore
                && secondRole == ReproductiveRole.DivisionCore,
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException(
                "Reproduction participants do not satisfy the authored reproductive roles.");
    }

    private static bool HasComplementaryRoles(
        ReproductiveRole left,
        ReproductiveRole right,
        ReproductiveRole first,
        ReproductiveRole second) =>
        left == first && right == second || left == second && right == first;

    private static bool IsActive(ReproductionProcess process) =>
        process.Status is ReproductionProcessStatus.Planned
            or ReproductionProcessStatus.Active
            or ReproductionProcessStatus.WaitingForEnvironment
            or ReproductionProcessStatus.WaitingForEmergencyExtraction;

    private static bool SharesParticipant(
        ReproductionProcess left,
        ReproductionProcess right) =>
        new[] { left.FirstParentId, left.SecondParentId, left.CarrierId }
            .Where(id => id.IsValid)
            .Intersect(new[]
            {
                right.FirstParentId,
                right.SecondParentId,
                right.CarrierId
            }.Where(id => id.IsValid))
            .Any();
}

public interface ICareerPersistence
{
    CharacterCareerWorldSaveData Capture();
    CharacterCareerAggregate PrepareRestore(CharacterCareerWorldSaveData data);
    void PublishRestore(CharacterCareerAggregate candidate);
}

public sealed class CareerRuntime : ICareerService, ICareerPersistence
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    public CareerRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        IMilestoneGameplayModifierQuery milestoneModifiers = null)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
    }
    public IReadOnlyList<CareerMentorshipSnapshot> Mentorships => Current.Mentorships;
    public bool TryGet(CharacterId characterId, out CharacterCareerSnapshot snapshot) =>
        Current.TryGet(characterId, out snapshot);
    public void Retire(CharacterId characterId, int absoluteDay) => Writable.Retire(characterId, absoluteDay);
    public void AssignPosition(CharacterId characterId, CareerPositionKind position, string scopeId, int absoluteDay) =>
        Writable.AssignPosition(characterId, position, scopeId, absoluteDay);
    public bool CanPerformRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        bool safeWork,
        out string reason) =>
        Current.CanPerformRetiredWork(
            characterId,
            absoluteDay,
            safeWork,
            out reason);
    public void RecordRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        float elapsedSeconds) =>
        Writable.RecordRetiredWork(characterId, absoluteDay, elapsedSeconds);
    public void AssignMentorship(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId) =>
        Writable.AssignMentorship(
            mentorCharacterId,
            studentCharacterId,
            academyBuildingId);
    public void ClearMentorship(CharacterId studentCharacterId) =>
        Writable.ClearMentorship(studentCharacterId);
    public bool TryMarkMentoringAwarded(
        CharacterId studentCharacterId,
        int absoluteDay) =>
        Writable.TryMarkMentoringAwarded(studentCharacterId, absoluteDay);
    public int ResolveMentoringXp(int requestedXp) => Math.Clamp(
        requestedXp,
        0,
        Math.Max(0, milestoneModifiers.MentorshipDailyXpCap));
    public CharacterCareerWorldSaveData Capture() => Current.CaptureWorld();
    public CharacterCareerAggregate PrepareRestore(CharacterCareerWorldSaveData data) =>
        CharacterCareerAggregate.Restore(data);
    public void PublishRestore(CharacterCareerAggregate candidate) =>
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
    private CharacterCareerAggregate Current => rootStore.GetOrCreate(() => new CharacterCareerAggregate());
    private CharacterCareerAggregate Writable => rootStore.GetOrCreateWritable(
        () => new CharacterCareerAggregate(),
        value => CharacterCareerAggregate.Restore(value.CaptureWorld()));
}

public interface IGriefTraumaService
{
    bool TryGet(CharacterId characterId, out CharacterGriefAggregate state);
    CharacterGriefAggregate Require(CharacterId characterId);
    void RecordDeath(CharacterId characterId, CharacterLifeDeathRecord death, GriefRelationshipKind relationship);
    void CompleteFuneral(CharacterId characterId, CharacterId deceasedId, int absoluteDay, bool matchingRitual);
    void CompleteJointMemorial(
        CharacterId characterId,
        IReadOnlyList<CharacterId> deceasedIds,
        int absoluteDay,
        bool matchingRitual);
    void ApplyLongNightMemorial(CharacterId characterId, int absoluteDay);
    void ApplyGriefConversion(CharacterId characterId, float percent);
    void Counsel(CharacterId characterId);
    void ApplyTraumaDelta(
        CharacterId characterId,
        string eventType,
        int absoluteDay,
        float amount);
}

public interface IPsychosocialPersistence
{
    CharacterPsychosocialWorldSaveData Capture();
    PsychosocialAggregateState PrepareRestore(CharacterPsychosocialWorldSaveData data);
    void PublishRestore(PsychosocialAggregateState candidate);
}

public sealed class PsychosocialAggregateState
{
    private readonly Dictionary<CharacterId, CharacterGriefAggregate> characters = new();
    public CharacterGriefAggregate Require(CharacterId id)
    {
        if (!characters.TryGetValue(id, out CharacterGriefAggregate value))
        {
            value = new CharacterGriefAggregate(id);
            characters.Add(id, value);
        }
        return value;
    }
    public bool TryGet(CharacterId id, out CharacterGriefAggregate value) =>
        characters.TryGetValue(id, out value);
    public CharacterPsychosocialWorldSaveData Capture() => new()
    {
        characters = characters.Values.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .Select(value => value.Capture()).ToList()
    };
    public static PsychosocialAggregateState Restore(CharacterPsychosocialWorldSaveData data)
    {
        if (data == null || data.version != CharacterPsychosocialWorldSaveData.CurrentVersion
            || data.characters == null)
            throw new InvalidOperationException("Psychosocial payload is incomplete or unsupported.");
        PsychosocialAggregateState result = new();
        foreach (CharacterPsychosocialRecordSaveData source in data.characters)
        {
            CharacterGriefAggregate value = CharacterGriefAggregate.Restore(source);
            if (!result.characters.TryAdd(value.CharacterId, value))
                throw new InvalidOperationException("Psychosocial character records are duplicated.");
        }
        return result;
    }
}

public sealed class GriefTraumaRuntime : IGriefTraumaService, IPsychosocialPersistence
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    public GriefTraumaRuntime(DungeonRuntimeAggregateRootStore rootStore) =>
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
    public CharacterGriefAggregate Require(CharacterId characterId) => Writable.Require(characterId);
    public bool TryGet(CharacterId characterId, out CharacterGriefAggregate state) =>
        Current.TryGet(characterId, out state);
    public void RecordDeath(CharacterId characterId, CharacterLifeDeathRecord death, GriefRelationshipKind relationship) =>
        Writable.Require(characterId).RecordDeath(death, relationship);
    public void CompleteFuneral(CharacterId characterId, CharacterId deceasedId, int absoluteDay, bool matchingRitual) =>
        Writable.Require(characterId).CompleteFuneral(deceasedId, absoluteDay, matchingRitual);
    public void CompleteJointMemorial(
        CharacterId characterId,
        IReadOnlyList<CharacterId> deceasedIds,
        int absoluteDay,
        bool matchingRitual) =>
        Writable.Require(characterId).CompleteJointMemorial(
            deceasedIds,
            absoluteDay,
            matchingRitual);
    public void ApplyLongNightMemorial(CharacterId characterId, int absoluteDay) =>
        Writable.Require(characterId).ApplyLongNightMemorial(absoluteDay);
    public void ApplyGriefConversion(CharacterId characterId, float percent) =>
        Writable.Require(characterId).ApplyGriefConversion(percent);
    public void Counsel(CharacterId characterId) => Writable.Require(characterId).ApplyCounseling();
    public void ApplyTraumaDelta(
        CharacterId characterId,
        string eventType,
        int absoluteDay,
        float amount) => Writable.Require(characterId).ApplyTraumaDelta(
            eventType,
            absoluteDay,
            amount);
    public CharacterPsychosocialWorldSaveData Capture() => Current.Capture();
    public PsychosocialAggregateState PrepareRestore(CharacterPsychosocialWorldSaveData data) =>
        PsychosocialAggregateState.Restore(data);
    public void PublishRestore(PsychosocialAggregateState candidate) =>
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
    private PsychosocialAggregateState Current => rootStore.GetOrCreate(() => new PsychosocialAggregateState());
    private PsychosocialAggregateState Writable => rootStore.GetOrCreateWritable(
        () => new PsychosocialAggregateState(),
        value => PsychosocialAggregateState.Restore(value.Capture()));
}
